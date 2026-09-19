using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;
namespace Bastion.Organizacion.Domain.Series;

/// <summary>
/// Serie documental: la que numera los documentos de una empresa en un ejercicio (R5).
/// </summary>
/// <remarks>
/// <para>
/// <b>El contador es una columna de una fila, no una secuencia de PostgreSQL.</b> Es una decisión
/// de esquema, y de las que no tienen segunda oportunidad: <c>nextval</c> NO se revierte al deshacer
/// la transacción, así que una confirmación que falla dejaría un hueco permanente en la
/// numeración. R5 dice «correlativa y sin huecos», y eso descarta la secuencia.
/// </para>
/// <para>
/// <b>Pero no es una columna de ESTA fila</b>, y eso lo enmendó el ADR-0039: vive en
/// <see cref="ContadorDeSerie"/>, su propia tabla con <c>serie_id</c> de clave. El motivo es el
/// testigo de concurrencia de aquí —<c>xmin</c>, que PostgreSQL mueve en cada escritura de la
/// fila—: con el contador dentro, cada documento confirmado tiraba el <c>ETag</c> de esta ficha.
/// <see cref="Contador"/> sigue leéndose igual desde fuera, porque la fila hija se carga
/// <b>siempre</b> con la serie.
/// </para>
/// <para>
/// <b>Y el procedimiento tampoco está aquí, ni puede estarlo.</b> Esta clase decía que asignar el
/// número —bloquear la fila dentro de la transacción de confirmación, incrementar y componer— era
/// del módulo de Facturación, y eso no era ambiguo: era <b>imposible</b>. <c>Serie</c> vive en
/// <c>Organizacion.Domain</c> y ningún módulo ve el interior de otro, así que ni Inventario ni
/// Facturación podrían llamar nunca a un método de aquí. El mecanismo vive en el bloque común,
/// como la bandeja y como el almacén de idempotencia, y la invariante de R5 viaja <b>en su
/// sentencia</b>: incrementa sobre lo que hay, así que no hay número que un llamante pueda
/// equivocar.
/// </para>
/// </remarks>
public sealed class Serie : EntidadBase, IDeInquilino
{
    /// <summary>
    /// Tope del código. El <c>NumSerieFactura</c> de Veri*factu admite 60 caracteres para
    /// serie MÁS número; este tope deja sitio al número, al año y al separador.
    /// </summary>
    public const int LongitudMaximaDeCodigo = 20;

    private Serie(
        Guid id,
        Guid empresaId,
        Guid ejercicioId,
        TipoDeDocumento tipoDeDocumento,
        string codigo,
        string formato,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        EjercicioId = ejercicioId;
        TipoDeDocumento = tipoDeDocumento;
        Codigo = codigo;
        Formato = formato;
        Estado = EstadoDeSerie.Activa;

        // La fila del contador nace CON la serie y en cero. Que exista siempre es lo que
        // permite que «ninguna fila devuelta» sea un fallo sin ambigüedad al numerar.
        Numeracion = ContadorDeSerie.ParaSerieNueva(id);
    }

    private Serie()
    {
        Codigo = null!;
        Formato = null!;
    }

    /// <summary>Identificador de la serie.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Ejercicio al que pertenece: R5 numera por serie Y ejercicio.</summary>
    public Guid EjercicioId { get; private set; }

    /// <summary>Clase de documento que numera.</summary>
    public TipoDeDocumento TipoDeDocumento { get; private set; }

    /// <summary>Código de la serie, en mayúsculas.</summary>
    public string Codigo { get; private set; }

    /// <summary>Plantilla con la que se compone el número completo.</summary>
    public string Formato { get; private set; }

    /// <summary>
    /// La fila donde vive el contador (ADR-0039). Se carga <b>siempre</b> con la serie.
    /// </summary>
    /// <remarks>
    /// Pública porque el listado ordena por <c>contador</c> y esa expresión la traduce el ORM,
    /// que necesita la navegación; no porque nadie tenga que tocarla. No hay por dónde: la fila
    /// hija no expone ninguna forma de cambiar su valor.
    /// </remarks>
    public ContadorDeSerie? Numeracion { get; private set; }

    /// <summary>Último número asignado. Cero mientras no haya numerado nada.</summary>
    /// <remarks>
    /// <b>Lanza si la fila hija no viene cargada, en vez de contestar cero</b>, y la diferencia
    /// no es de estilo: un cero silencioso aquí es <see cref="SePuedeSuprimir"/> diciendo que sí
    /// sobre una serie que ya ha numerado, con un <c>DELETE</c> detrás. De los dos modos de
    /// fallar —«borra lo que no debía» y «no contesta»— solo el segundo se puede depurar.
    /// </remarks>
    public long Contador => (Numeracion ?? throw new InvalidOperationException(
        $"La serie {Codigo} se ha cargado sin su fila de contador. Esa fila existe siempre y " +
        "se carga con la serie: si falta, lo que hay delante es una consulta que la ha dejado " +
        "fuera, no una serie sin numerar.")).UltimoNumero;

    /// <summary>Activa o cerrada.</summary>
    public EstadoDeSerie Estado { get; private set; }

    /// <summary>
    /// Una serie solo se puede suprimir mientras no haya numerado nada. Después, borrarla
    /// dejaría documentos legales apuntando a una serie inexistente.
    /// </summary>
    public bool SePuedeSuprimir => Contador == 0;

    /// <summary>Crea una serie activa con el contador a cero.</summary>
    /// <remarks>El <c>momento</c> es la fecha de creación, y la pone quien tiene el
    /// <c>TimeProvider</c>: no la base de datos.</remarks>
    public static Serie Crear(
        Guid empresaId,
        Guid ejercicioId,
        TipoDeDocumento tipoDeDocumento,
        string codigo,
        string formato,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una serie pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        if (ejercicioId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una serie numera por serie Y ejercicio (R5): sin ejercicio no hay correlatividad " +
                "que garantizar.", nameof(ejercicioId));
        }

        return new Serie(
            Guid.CreateVersion7(),
            empresaId,
            ejercicioId,
            tipoDeDocumento,
            CodigoValido(codigo),
            FormatoValido(formato),
            momento);
    }

    /// <summary>Cambia el formato de composición del número.</summary>
    /// <remarks>
    /// El código NO se puede cambiar: aparece en los documentos ya emitidos.
    /// </remarks>
    public void Modificar(string formato)
    {
        ExigirQueEsteActiva();
        Formato = FormatoValido(formato);
    }

    /// <summary>Cierra la serie: deja de numerar, y conserva el contador donde está.</summary>
    public void Cerrar() => Estado = EstadoDeSerie.Cerrada;

    /// <summary>Reabre la serie.</summary>
    public void Reabrir() => Estado = EstadoDeSerie.Activa;

    private void ExigirQueEsteActiva()
    {
        if (Estado == EstadoDeSerie.Cerrada)
        {
            throw new InvalidOperationException(
                $"La serie {Codigo} está cerrada y no admite más operaciones.");
        }
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública a propósito: sobre esta forma hay un índice único, y quien comprueba si el código ya
    /// existe ANTES de insertar tiene que preguntar por ella. Preguntando por lo que escribió el
    /// usuario, «fac» pasaría el filtro, chocaría contra el índice y saldría como un 500 en vez de
    /// como un 409 con explicación. No valida nada —de la longitud se encarga la creación—: una
    /// pregunta no tiene por qué reventar.
    /// </remarks>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de serie admite {LongitudMaximaDeCodigo} caracteres como máximo: el " +
                $"NumSerieFactura de Veri*factu tiene 60 para serie y número juntos.", nameof(codigo));
    }

    private static string FormatoValido(string formato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formato);
        return formato.Trim();
    }
}
