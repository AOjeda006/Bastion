using System.Text.Json;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Escribe la traza de cada cambio <b>dentro del mismo <c>SaveChanges</c></b> que lo produce, y de
/// paso comprueba que ninguna fila de inquilino se escriba con la empresa de otro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué aquí y no después.</b> Una auditoría que se escribe fuera de la transacción del
/// cambio miente en las dos direcciones: un cambio confirmado cuya traza se perdió, y una traza de
/// un cambio que se revirtió. Las dos son peores que no tener auditoría, porque nadie duda de una
/// tabla que se llama así. Al añadir las filas en <c>SavingChanges</c>, van en el <b>mismo</b>
/// <c>SaveChanges</c> —o sea, en la misma transacción implícita de EF Core— que el cambio: o entran
/// las dos cosas o no entra ninguna. «Mejor esfuerzo» no es una propiedad que esta tabla pueda
/// tener.
/// </para>
/// <para>
/// <b>Y de una sola fase.</b> La receta canónica es de dos —recoger al guardar, completar las
/// claves generadas después y volver a guardar—, y existe porque en el caso general la clave de un
/// <c>INSERT</c> no se sabe hasta después. Aquí sí se sabe: las claves salen del constructor del
/// dominio (ADR-0010) y ningún valor lo pone la base. Eso no se ha copiado del ADR: lo comprueba
/// <c>LasClavesSeConocenAntesDeGuardarTests</c>, y el día que deje de ser cierto se pone rojo.
/// </para>
/// <para>
/// <b>La guarda de escritura</b> es el cabo que el 0.6 dejó suelto: el filtro global arregló las
/// lecturas, pero un <c>INSERT</c> no pasa por ningún filtro, así que cada caso de uso seguía
/// teniendo que acordarse de poner la empresa buena. Este interceptor ya recorre las entradas
/// pendientes, así que la comprobación sale casi gratis y deja de depender de la memoria de nadie.
/// </para>
/// </remarks>
/// <param name="inquilino">De dónde sale la empresa activa, y el motivo si no la hay.</param>
/// <param name="usuario">Quién pide la operación.</param>
/// <param name="reloj">De dónde sale el instante.</param>
public sealed class InterceptorDeAuditoria(
    IInquilinoActual inquilino,
    IUsuarioActual usuario,
    TimeProvider reloj) : SaveChangesInterceptor
{
    private const string Antes = "antes";
    private const string Despues = "despues";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    // Los parametros van en ingles, y es la unica excepcion del proyecto: CA1725 exige que un
    // metodo sobrescrito conserve los nombres de la base, porque son los que ve quien llama por
    // argumento con nombre. Renombrarlos aqui romperia esa llamada sin que nada avisara.

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Anotar(eventData);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Anotar(eventData);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Anotar(DbContextEventData datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        DbContext? contexto = datos.Context;

        if (contexto is null)
        {
            return;
        }

        // Las entradas se congelan ANTES de añadir nada: `context.Add` de la primera fila de traza
        // invalidaría el recorrido de la colección que las está produciendo.
        List<EntityEntry> cambios = [.. contexto.ChangeTracker.Entries()
            .Where(entrada => entrada.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entrada => entrada.Entity is not RegistroDeAuditoria)];

        if (cambios.Count == 0)
        {
            return;
        }

        Guid? empresaId = inquilino.HayEmpresaActiva ? inquilino.EmpresaDelFiltro : null;
        MotivoSinInquilino? motivo = empresaId.HasValue ? null : DeclararSinInquilino();

        ComprobarQueNadieEscribeEnOtraEmpresa(cambios, empresaId);

        var correlacion = Guid.CreateVersion7();
        DateTimeOffset ahora = reloj.GetUtcNow();

        foreach ((Rastro rastro, Borrador borrador) in Recoger(cambios))
        {
            // Una modificación que no cambia ningún valor auditado no es un cambio del que haya
            // nada que decir: escribir una fila vacía solo añadiría ruido a una tabla que no se
            // limpia. Un alta y una baja sí dejan traza aunque no tengan valores que enseñar —hay
            // entidades cuyas columnas son todas la clave, y ahí el alta ES el cambio—.
            if (borrador.Cambio == TipoDeCambio.Modificacion && borrador.Valores.Count == 0)
            {
                continue;
            }

            contexto.Add(RegistroDeAuditoria.De(
                correlacion,
                ahora,
                empresaId,
                motivo,
                usuario.EstaAutenticado ? usuario.UsuarioId : null,
                rastro.Entidad,
                rastro.EntidadId,
                borrador.Cambio,
                JsonSerializer.Serialize(borrador.Valores, s_json)));
        }
    }

    // Sin empresa activa hay exactamente dos situaciones, y solo una es legítima: que alguien haya
    // abierto un ámbito con su motivo. La otra —ni empresa ni ámbito— es el hueco que el 0.6 se
    // negó a rellenar en silencio, y aquí tampoco se rellena: `EmpresaDelFiltro` lanza.
    private MotivoSinInquilino DeclararSinInquilino() =>
        inquilino.MotivoDelAmbito ?? throw new FaltaLaEmpresaActivaException();

    // El cabo del 0.6. `HasQueryFilter` no interviene en un INSERT ni en un UPDATE: solo protege
    // lo que pasa por el traductor de consultas. Sin esto, que una fila nazca con la empresa buena
    // depende de que cada caso de uso escriba su `usuarioActual.EmpresaId` a mano —hoy son tres,
    // con dieciséis módulos serán cientos—, y el fallo no da error: da una fila en la empresa
    // equivocada, que se lee como un dato correcto.
    //
    // Dentro de un ámbito sin inquilino no hay contra qué comparar, así que no se comprueba. Es el
    // límite honesto de esta guarda: la semilla y el alta de pertenencias escriben filas de otra
    // empresa a propósito, y quién puede hacerlo lo decide `PuedeAdministrarAsync`, no esto.
    private static void ComprobarQueNadieEscribeEnOtraEmpresa(
        List<EntityEntry> cambios,
        Guid? empresaId)
    {
        if (empresaId is not { } activa)
        {
            return;
        }

        foreach (EntityEntry entrada in cambios)
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified)
                || entrada.Entity is not IDeInquilino fila
                || fila.EmpresaId == activa)
            {
                continue;
            }

            throw new EscrituraEnOtraEmpresaException(entrada.Metadata.ShortName(), fila.EmpresaId, activa);
        }
    }

    // UNA FILA DE TRAZA POR ENTIDAD CAMBIADA, y una entidad de propiedad no es una entidad cambiada:
    // es una parte de su dueño. EF Core sí las sigue por separado —cambiar solo la calle deja el
    // `Almacen` como `Unchanged` y su `Direccion` como cambiada—, y tomarlas al pie de la letra
    // daría trazas de un «Direccion» sin identidad propia y ninguna de «Almacen», que es de lo que
    // se está hablando. Aquí se pliegan en la fila de su dueño, con el nombre de la navegación por
    // delante: `Direccion.Calle`.
    //
    // Sustituir un objeto de valor entero —que es justo lo que hace un `Modificar` de dominio—
    // aparece además como DOS entradas con la misma clave, una baja y un alta. Plegadas, vuelven a
    // ser lo que de verdad son: el antes y el después de las mismas propiedades.
    private static IEnumerable<(Rastro Rastro, Borrador Borrador)> Recoger(List<EntityEntry> cambios)
    {
        Dictionary<Rastro, Borrador> borradores = [];
        List<Rastro> orden = [];

        foreach (EntityEntry entrada in cambios)
        {
            (IEntityType dueno, string prefijo) = Dueno(entrada.Metadata);

            if (dueno.Auditoria().Que != ClasificacionDeAuditoria.Auditada)
            {
                continue;
            }

            Rastro rastro = new(dueno.ShortName(), Clave(entrada));

            if (!borradores.TryGetValue(rastro, out Borrador? borrador))
            {
                borrador = new Borrador();
                borradores[rastro] = borrador;
                orden.Add(rastro);
            }

            // Solo el dueño decide de qué cambio se trata. Una dirección que se añade sobre un
            // almacén que ya existía es una MODIFICACIÓN del almacén, no un alta.
            if (prefijo.Length == 0)
            {
                borrador.Cambio = entrada.State switch
                {
                    EntityState.Added => TipoDeCambio.Alta,
                    EntityState.Deleted => TipoDeCambio.Baja,
                    _ => TipoDeCambio.Modificacion,
                };
            }

            Apuntar(borrador, entrada, prefijo);
        }

        foreach (Rastro rastro in orden)
        {
            Borrador borrador = borradores[rastro];
            borrador.DejarSoloLoQueCambio();

            yield return (rastro, borrador);
        }
    }

    private static void Apuntar(Borrador borrador, EntityEntry entrada, string prefijo) =>
        Apuntar(borrador, entrada.State, entrada.Properties, entrada.ComplexProperties, prefijo);

    // LAS DE UN TIPO COMPLEJO TAMBIÉN, y por eso este método recibe las dos colecciones en vez de
    // la entrada entera. Un tipo POSEÍDO llega aquí por su cuenta —EF Core lo sigue como una
    // entrada más del rastreador, y `Recoger` lo pliega en su dueño—; un tipo COMPLEJO no: sus
    // propiedades cuelgan de la entrada del dueño y `entrada.Properties` NO las devuelve. Sin esta
    // recursión, mover un objeto de valor a tipo complejo deja de auditar sus columnas sin que
    // nada falle. Medido en el 0.10: con la dirección compleja y esto sin escribir, el único rojo
    // de las 152 pruebas de integración fue el que mira la traza de una dirección.
    private static void Apuntar(
        Borrador borrador,
        EntityState estado,
        IEnumerable<PropertyEntry> propiedades,
        IEnumerable<ComplexPropertyEntry> complejas,
        string prefijo)
    {
        foreach (PropertyEntry propiedad in propiedades)
        {
            if (propiedad.Metadata.Auditoria().Que != ClasificacionDeAuditoria.Auditada)
            {
                continue;
            }

            Dictionary<string, object?> detalle = borrador.Detalle(prefijo + propiedad.Metadata.Name);

            // Un alta no lleva `antes` y una baja no lleva `despues`: el hueco ES la información, y
            // rellenarlo con un nulo lo confundiría con «cambió a nulo».
            if (estado != EntityState.Added)
            {
                detalle[Antes] = ParaLaTraza(propiedad, propiedad.OriginalValue);
            }

            if (estado != EntityState.Deleted)
            {
                detalle[Despues] = ParaLaTraza(propiedad, propiedad.CurrentValue);
            }
        }

        foreach (ComplexPropertyEntry compleja in complejas)
        {
            Apuntar(
                borrador,
                estado,
                compleja.Properties,
                compleja.ComplexProperties,
                $"{prefijo}{compleja.Metadata.Name}.");
        }
    }

    // Sube hasta el dueño de verdad, componiendo el camino. Hoy no hay ninguna entidad de propiedad
    // dentro de otra; el bucle está para que el día que la haya la traza siga diciendo dónde estaba
    // el valor, en vez de perder un tramo del camino en silencio.
    private static (IEntityType Dueno, string Prefijo) Dueno(IEntityType tipo)
    {
        string prefijo = string.Empty;

        while (tipo.FindOwnership() is { } propiedad)
        {
            prefijo = $"{propiedad.PrincipalToDependent?.Name}.{prefijo}";
            tipo = propiedad.PrincipalEntityType;
        }

        return (tipo, prefijo);
    }

    // El valor tal como va a la columna, no tal como lo ve C#. Un `Nif` serializado como objeto
    // daría `{"valor":"..."}` y un enumerado daría su número; quien lea la traza espera lo mismo
    // que vería en la tabla de al lado. Y de paso deja los valores comparables entre sí, que es lo
    // que permite descartar los que no han cambiado.
    //
    // Se pregunta por el convertidor DOS VECES a propósito. `GetValueConverter` solo devuelve el
    // que se puso a mano —`HasConversion(ida, vuelta)`, como el del `Nif`—; cuando se declara por
    // el tipo de destino —`HasConversion<string>()`, como el de cada enumerado— la conversión vive
    // en la correspondencia de tipos y aquel devuelve nulo. Quedarse en el primero dejaba los
    // enumerados en la traza como el número que tienen HOY, que además cambia si alguien reordena
    // el enumerado: la fila de ayer diría otra cosa sin que nadie la tocara.
    private static object? ParaLaTraza(PropertyEntry propiedad, object? valor)
    {
        if (valor is null)
        {
            return null;
        }

        IProperty metadatos = propiedad.Metadata;
        ValueConverter? convertidor = metadatos.GetValueConverter() ?? metadatos.GetTypeMapping().Converter;

        return convertidor is null ? valor : convertidor.ConvertToProvider(valor);
    }

    // De una entidad poseída se cuenta la clave de su DUEÑO: la traza habla de la empresa cuyo
    // domicilio ha cambiado, no de un domicilio que no tiene identidad propia.
    private static string Clave(EntityEntry entrada) =>
        string.Join('|', entrada.Metadata.FindPrimaryKey()?.Properties
            .Select(clave => entrada.Property(clave.Name).CurrentValue?.ToString() ?? string.Empty)
            ?? []);

    /// <summary>De qué fila habla una traza.</summary>
    /// <param name="Entidad">Nombre corto del tipo, que es el del dueño si la entrada es poseída.</param>
    /// <param name="EntidadId">La clave, con sus partes unidas por <c>|</c> si es compuesta.</param>
    private readonly record struct Rastro(string Entidad, string EntidadId);

    /// <summary>La fila de traza a medio hacer, mientras se le pliegan encima sus poseídas.</summary>
    private sealed class Borrador
    {
        public TipoDeCambio Cambio { get; set; } = TipoDeCambio.Modificacion;

        public Dictionary<string, Dictionary<string, object?>> Valores { get; } = [];

        public Dictionary<string, object?> Detalle(string propiedad)
        {
            if (!Valores.TryGetValue(propiedad, out Dictionary<string, object?>? detalle))
            {
                detalle = [];
                Valores[propiedad] = detalle;
            }

            return detalle;
        }

        // Fuera las propiedades cuyo antes y después son el mismo valor. EF Core entrega el original
        // y el actual de TODAS las columnas de una entidad cambiada, así que sin esto un cambio de
        // nombre arrastraría las otras diez columnas intactas, y «qué cambió» pasaría a ser un
        // ejercicio de comparar dos listas en una tabla que por diseño no se puede limpiar.
        //
        // Se compara por VALOR y no por `IsModified`, que es una bandera del rastreador: al
        // sustituir un objeto de valor entero, EF da por cambiadas todas sus columnas aunque
        // vuelvan a llevar exactamente lo mismo.
        public void DejarSoloLoQueCambio()
        {
            foreach (string propiedad in Valores.Keys.ToList())
            {
                Dictionary<string, object?> detalle = Valores[propiedad];

                if (detalle.TryGetValue(Antes, out object? antes)
                    && detalle.TryGetValue(Despues, out object? despues)
                    && Equals(antes, despues))
                {
                    Valores.Remove(propiedad);
                }
            }
        }
    }
}
