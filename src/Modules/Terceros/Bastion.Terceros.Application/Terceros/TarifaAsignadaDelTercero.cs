using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Qué tarifa tiene asignada un tercero, si tiene alguna.</summary>
public interface IObtenerTarifaAsignada
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(Guid terceroId, CancellationToken cancelacion);
}

/// <summary>Asigna —o quita— la tarifa de un tercero.</summary>
public interface IAsignarTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">La tarifa, o nada para quitarla.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
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
/// quien la mirara asignaría otra creyendo que nunca hubo ninguna.
/// </remarks>
internal sealed class ObtenerTarifaAsignada(IRepositorioDeTerceros terceros)
    : IObtenerTarifaAsignada
{
    public async Task<Resultado<TarifaAsignadaDto>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        return tercero is null
            ? Resultado.Fallo<TarifaAsignadaDto>(ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto(tercero.ATarifaAsignadaDto());
    }
}

/// <inheritdoc cref="IAsignarTarifa"/>
/// <remarks>
/// <para>
/// <b>Es la mitad de vuelta del primer cruce mutuo del proyecto</b>, y el primer sitio en el que
/// Terceros le pregunta algo a Catálogo. Lo que cruza es un <c>Guid</c>, una <c>DateOnly</c> y un
/// enumerado de <c>Catalogo.Contracts</c>: ni un tipo de este módulo viaja hacia allá, que es lo
/// que impide que la mutua sea un ciclo.
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

        if (peticion.TarifaId is { } tarifaId)
        {
            // EL PUERTO, Y AQUÍ ES DONDE DEJA DE SER DECORATIVO. `TarifaAsignadaId` no tiene clave
            // ajena —vive en el esquema `catalogo` y entre esquemas no se cruza, §5 regla 4—, así
            // que sin esta pregunta la columna aceptaría cualquier `uuid`: compilaría, migraría y
            // serviría peticiones, y el fallo saldría en la primera venta. Es la cuarta vía del
            // ADR-0024.
            //
            // El día con el que se pregunta es HOY, en UTC como todo instante del sistema. Se
            // puede elegir así porque lo que se guarda es la lista de precios que se le propone a
            // este cliente a partir de ahora; qué precio se aplica de verdad lo decidirá el
            // documento con SU fecha, volviendo a preguntar.
            var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

            EstadoDeLaTarifa estado = await tarifas
                .EstadoDeAsync(tarifaId, hoy, cancelacion)
                .ConfigureAwait(false);

            Resultado laTarifa = LaTarifaSeOfreceParaLoNuevo.Traducir(estado, tarifaId, hoy);

            if (!laTarifa.EsCorrecto)
            {
                return Resultado.Fallo<TarifaAsignadaDto>(laTarifa.Error!);
            }
        }

        // Un tercero bloqueado no se toca: lo impide el agregado, no este caso de uso (R16).
        tercero.AsignarTarifa(peticion.TarifaId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ATarifaAsignadaDto());
    }
}

/// <summary>
/// Traduce los tres valores de <see cref="EstadoDeLaTarifa"/> a los desenlaces que una asignación
/// puede tener.
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
/// <b>Los dos noes son distinguibles</b>, y aquí sí se pueden distinguir: lo que hay al otro lado
/// es una lista de precios de la propia empresa, no la ficha de una persona. Quien teclea un
/// identificador que no existe tiene que corregir el identificador; quien apunta a un tramo
/// caducado tiene que elegir el vigente. Dos arreglos, dos <c>type</c> del catálogo (ADR-0030).
/// </para>
/// </remarks>
internal static class LaTarifaSeOfreceParaLoNuevo
{
    /// <summary>Código del error de una tarifa que no existe.</summary>
    public const string CodigoDeTarifaNoEncontrada = "tercero-tarifa-no-encontrada";

    /// <summary>Código del error de una tarifa cuya vigencia no cubre el día de la asignación.</summary>
    public const string CodigoDeTarifaNoVigente = "tercero-tarifa-no-vigente";

    /// <summary>El desenlace de haber preguntado por la tarifa.</summary>
    /// <param name="estado">Lo que contestó <c>IConsultaDeTarifas</c>.</param>
    /// <param name="tarifaId">El identificador por el que se preguntó.</param>
    /// <param name="enLaFecha">La fecha con la que se preguntó.</param>
    internal static Resultado Traducir(
        EstadoDeLaTarifa estado,
        Guid tarifaId,
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

            EstadoDeLaTarifa.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeTarifaNoEncontrada,
                $"No hay ninguna tarifa con el identificador {tarifaId}.")),

            // El `default` no es defensivo por costumbre: un cuarto valor del enumerado tiene que
            // reventar ruidosamente en la primera petición y no colarse por la rama permisiva. No
            // es un desenlace de negocio, es un valor que este código no sabe interpretar
            // (ADR-0004).
            _ => throw new ArgumentOutOfRangeException(
                nameof(estado),
                estado,
                "El puerto de tarifas ha contestado un estado que este caso de uso no sabe traducir."),
        };
}
