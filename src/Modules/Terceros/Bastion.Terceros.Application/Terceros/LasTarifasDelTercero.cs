using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Qué tarifa tiene asignada un tercero en un rol, si tiene alguna.</summary>
public interface IObtenerTarifaAsignada
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="rol">Cliente, la de venta, o proveedor, la de compra.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
        RolDeTercero rol,
        CancellationToken cancelacion);
}

/// <summary>Asigna —o quita— la tarifa de venta o la de compra de un tercero.</summary>
public interface IAsignarTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="rol">Cliente, la de venta, o proveedor, la de compra.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">La tarifa, o nada para quitarla.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
        RolDeTercero rol,
        VersionDeRecurso version,
        AsignarTarifaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerTarifaAsignada"/>
/// <remarks>
/// <b>No pregunta por el estado de la tarifa</b>, y no es un olvido: lo que aquí se lee es una
/// decisión que la empresa tomó, no una promesa de que siga sirviendo. Preguntar por el puerto en
/// la lectura costaría una consulta a otro módulo en cada apertura de ficha para poder esconder un
/// dato verdadero; y esconderlo es peor que enseñarlo, porque la pantalla diría «sin tarifa» y
/// quien la mirara asignaría otra creyendo que nunca hubo ninguna. Por lo mismo, la de venta de
/// quien ya no es cliente sale igual.
/// </remarks>
internal sealed class ObtenerTarifaAsignada(IRepositorioDeTerceros terceros)
    : IObtenerTarifaAsignada
{
    public async Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
        RolDeTercero rol,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        return tercero is null
            ? Resultado.Fallo<TarifaAsignadaDto>(ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto(tercero.ATarifaAsignadaDto(rol));
    }
}

/// <inheritdoc cref="IAsignarTarifa"/>
/// <remarks>
/// <para>
/// <b>Es la mitad de vuelta del primer cruce mutuo del proyecto</b>, y el primer sitio en el que
/// Terceros le pregunta algo a Catálogo. Lo que cruza es un <c>Guid</c>, una <c>DateOnly</c> y dos
/// enumerados de <c>Catalogo.Contracts</c>: ni un tipo de este módulo viaja hacia allá, que es lo
/// que impide que la mutua sea un ciclo. Por eso el caso de uso recibe el rol, que es de aquí, y
/// lo traduce al tipo de la tarifa por el que pregunta: el controlador no ve Catálogo.
/// </para>
/// <para>
/// <b>Desde el 3.3 son dos huecos, uno por rol</b>: la de venta pide un cliente y una tarifa de
/// venta, y la de compra, un proveedor y una tarifa de compra. El rol se mira antes que el puerto,
/// porque no hace falta preguntarle nada a Catálogo para saber que un proveedor que no vende no
/// lleva lista de precios de venta.
/// </para>
/// <para>
/// <b>Y la divisa no se compara con nada</b> —ni con la del límite de crédito de esta misma ficha,
/// ni con la de la empresa—, a propósito y con el motivo escrito en el PLAN del ítem 1.10. Hasta
/// ahora eso estaba garantizado por una ausencia: este módulo no tenía ningún puerto con el que
/// preguntar por una divisa. Desde este fichero la tiene a un paso, así que la decisión pasa a ser
/// una decisión y no una imposibilidad, y queda dicha aquí: <b>asignar una tarifa no comprueba
/// divisas</b>. Un límite de crédito en euros y una tarifa en dólares conviven; lo que decide en
/// qué divisa se emite un documento es el documento, en la fase 5.
/// </para>
/// </remarks>
internal sealed class AsignarTarifa(
    IRepositorioDeTerceros terceros,
    IConsultaDeTarifas tarifas,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones,
    TimeProvider reloj) : IAsignarTarifa
{
    public async Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
        RolDeTercero rol,
        VersionDeRecurso version,
        AsignarTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<TarifaAsignadaDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        TipoDeLaTarifa tipo = LaTarifaSeOfreceParaLoNuevo.TipoDe(rol);

        if (peticion.TarifaId is { } tarifaId)
        {
            // EL ROL, ANTES QUE NADA. El agregado lo vuelve a exigir lanzando; este es el que sabe
            // explicarse con un `type`. Quitar no llega aquí: un cliente que deja de serlo tiene
            // que poder soltar la suya.
            if (!LaTarifaSeOfreceParaLoNuevo.HaceElRol(tercero, rol))
            {
                return Resultado.Fallo<TarifaAsignadaDto>(
                    LaTarifaSeOfreceParaLoNuevo.SinSuRol(terceroId, rol));
            }

            // EL PUERTO, Y AQUÍ ES DONDE DEJA DE SER DECORATIVO. La columna no tiene clave ajena
            // —vive en el esquema `catalogo` y entre esquemas no se cruza, §5 regla 4—, así que
            // sin esta pregunta aceptaría cualquier `uuid`: compilaría, migraría y serviría
            // peticiones, y el fallo saldría en la primera venta. Es la cuarta vía del ADR-0024.
            //
            // El día con el que se pregunta es HOY, en UTC como todo instante del sistema. Se
            // puede elegir así porque lo que se guarda es la lista de precios que se le propone a
            // este tercero a partir de ahora; qué precio se aplica de verdad lo decidirá el
            // documento con SU fecha, volviendo a preguntar.
            DateOnly hoy = DiaDeNegocio.Hoy(reloj);

            EstadoDeLaTarifa estado = await tarifas
                .EstadoDeAsync(tarifaId, tipo, hoy, cancelacion)
                .ConfigureAwait(false);

            Resultado laTarifa = LaTarifaSeOfreceParaLoNuevo.Traducir(estado, tarifaId, tipo, hoy);

            if (!laTarifa.EsCorrecto)
            {
                return Resultado.Fallo<TarifaAsignadaDto>(laTarifa.Error!);
            }
        }

        // Un tercero bloqueado no se toca: lo impide el agregado, no este caso de uso (R16).
        if (rol == RolDeTercero.Proveedor)
        {
            tercero.AsignarTarifaDeCompra(peticion.TarifaId);
        }
        else
        {
            tercero.AsignarTarifaDeVenta(peticion.TarifaId);
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ATarifaAsignadaDto(rol));
    }
}

/// <summary>
/// Traduce los valores de <see cref="EstadoDeLaTarifa"/> a los desenlaces que una asignación
/// puede tener, y dice si el tercero hace el rol de la tarifa.
/// </summary>
/// <remarks>
/// <para>
/// Es el gemelo de <c>ElMaestroSeOfreceParaLoNuevo</c> de Catálogo, y son dos clases y no una
/// porque aquella es <c>internal</c> de <c>Bastion.Catalogo.Application</c>: compartirla exigiría
/// que un módulo viera el <c>Application</c> de otro, que es justo lo que la regla 1 del §4
/// prohíbe. Lo que se comparte es el <b>razonamiento</b>, no el código, y esa es la forma correcta
/// de compartir entre módulos.
/// </para>
/// <para>
/// <b>Los noes son distinguibles</b>, y aquí sí se pueden distinguir: lo que hay al otro lado es
/// una lista de precios de la propia empresa, no la ficha de una persona. Quien teclea un
/// identificador que no existe tiene que corregir el identificador; quien apunta a un tramo
/// caducado tiene que elegir el vigente; y quien apunta a una de compra para el hueco de venta
/// tiene que elegir otro código. Tres arreglos, tres <c>type</c> del catálogo (ADR-0030).
/// </para>
/// </remarks>
internal static class LaTarifaSeOfreceParaLoNuevo
{
    /// <summary>Código del error de una tarifa que no existe.</summary>
    public const string CodigoDeTarifaNoEncontrada = "tercero-tarifa-no-encontrada";

    /// <summary>Código del error de una tarifa cuya vigencia no cubre el día de la asignación.</summary>
    public const string CodigoDeTarifaNoVigente = "tercero-tarifa-no-vigente";

    /// <summary>Código del error de una tarifa de compra en el hueco de venta, o al revés.</summary>
    public const string CodigoDeTarifaDeOtroTipo = "tercero-tarifa-de-otro-tipo";

    /// <summary>
    /// Código del error de una tarifa de venta para quien no es cliente, o de compra para quien no
    /// es proveedor.
    /// </summary>
    public const string CodigoDeTarifaSinSuRol = "tercero-tarifa-sin-su-rol";

    /// <summary>
    /// De qué tipo es la tarifa de un rol: de venta la del cliente, de compra la del proveedor.
    /// </summary>
    /// <param name="rol">El rol del hueco.</param>
    internal static TipoDeLaTarifa TipoDe(RolDeTercero rol) => rol switch
    {
        RolDeTercero.Cliente => TipoDeLaTarifa.Venta,
        RolDeTercero.Proveedor => TipoDeLaTarifa.Compra,
        _ => throw new ArgumentOutOfRangeException(
            nameof(rol), rol, "Un tercero es cliente o proveedor."),
    };

    /// <summary>Si el tercero hace ese rol.</summary>
    /// <param name="tercero">La ficha.</param>
    /// <param name="rol">El rol del hueco en el que se asigna.</param>
    internal static bool HaceElRol(Tercero tercero, RolDeTercero rol) =>
        rol == RolDeTercero.Proveedor ? tercero.EsProveedor : tercero.EsCliente;

    /// <summary>El no de un tercero que no hace el rol de la tarifa.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="rol">El rol del hueco en el que se quería asignar.</param>
    internal static ErrorDeOperacion SinSuRol(Guid terceroId, RolDeTercero rol) =>
        ErrorDeOperacion.Conflicto(
            CodigoDeTarifaSinSuRol,
            rol == RolDeTercero.Proveedor
                ? $"El tercero {terceroId} no es proveedor, y la tarifa de compra es la de un " +
                  "proveedor. Márquelo como proveedor antes de asignársela."
                : $"El tercero {terceroId} no es cliente, y la tarifa de venta es la de un " +
                  "cliente. Márquelo como cliente antes de asignársela.");

    /// <summary>El desenlace de haber preguntado por la tarifa.</summary>
    /// <param name="estado">Lo que contestó <c>IConsultaDeTarifas</c>.</param>
    /// <param name="tarifaId">El identificador por el que se preguntó.</param>
    /// <param name="tipo">El tipo por el que se preguntó.</param>
    /// <param name="enLaFecha">La fecha con la que se preguntó.</param>
    internal static Resultado Traducir(
        EstadoDeLaTarifa estado,
        Guid tarifaId,
        TipoDeLaTarifa tipo,
        DateOnly enLaFecha) => estado switch
        {
            EstadoDeLaTarifa.RigeEnEsaFecha => Resultado.Correcto(),

            // «Retirada» no vale como palabra: una tarifa no se retira, deja de regir —o todavía no
            // rige, si su vigencia empieza mañana—. Los dos casos caen en el mismo valor y los dos
            // se cuentan diciendo la fecha, que es lo que quien lee necesita para entender el no.
            EstadoDeLaTarifa.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeTarifaNoVigente,
                $"La tarifa {tarifaId} no rige el {enLaFecha:yyyy-MM-dd}: sigue resolviendo los " +
                "precios de los documentos que ya la usan, pero no se asigna a un tercero hoy. " +
                "Elija la que esté vigente.")),

            // El tipo va con el código y no cambia, así que el arreglo es otro código, no otro
            // tramo: por eso es un `type` aparte del de la vigencia.
            EstadoDeLaTarifa.EsDeOtroTipo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeTarifaDeOtroTipo,
                tipo == TipoDeLaTarifa.Compra
                    ? $"La tarifa {tarifaId} es de venta, y aquí va la tarifa de compra del " +
                      "tercero. Elija una tarifa de compra."
                    : $"La tarifa {tarifaId} es de compra, y aquí va la tarifa de venta del " +
                      "tercero. Elija una tarifa de venta.")),

            EstadoDeLaTarifa.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeTarifaNoEncontrada,
                $"No hay ninguna tarifa con el identificador {tarifaId}.")),

            // El `default` no es defensivo por costumbre: un valor nuevo del enumerado tiene que
            // reventar ruidosamente en la primera petición y no colarse por la rama permisiva. No
            // es un desenlace de negocio, es un valor que este código no sabe interpretar
            // (ADR-0004).
            _ => throw new ArgumentOutOfRangeException(
                nameof(estado),
                estado,
                "El puerto de tarifas ha contestado un estado que este caso de uso no sabe traducir."),
        };
}
