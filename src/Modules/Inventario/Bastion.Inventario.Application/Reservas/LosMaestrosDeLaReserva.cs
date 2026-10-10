using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>
/// Qué contestan reservar y consumir a cada estado que les devuelven los puertos de maestros.
/// </summary>
/// <remarks>
/// <b>La misma tabla que la del ajuste, con su prefijo</b>: reservar solo admite
/// <c>SeOfreceParaLoNuevo</c> (ADR-0059 §5, paso 5). Un almacén o un artículo de otra empresa
/// contestan lo mismo que uno inventado, porque los puertos preguntan con el filtro de la empresa.
/// </remarks>
internal static class LosMaestrosDeLaReserva
{
    internal const string CodigoDeAlmacenNoEncontrado = "reserva-almacen-no-encontrado";
    internal const string CodigoDeAlmacenBloqueado = "reserva-almacen-bloqueado";
    internal const string CodigoDeArticuloNoEncontrado = "reserva-articulo-no-encontrado";
    internal const string CodigoDeArticuloNoSeAlmacena = "reserva-articulo-no-se-almacena";
    internal const string CodigoDeUbicacionBloqueada = "reserva-ubicacion-bloqueada";

    internal static Resultado ElAlmacen(EstadoDeMaestro estado, Guid almacenId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeAlmacenBloqueado,
            $"El almacén {almacenId} está bloqueado: sus movimientos anteriores se siguen leyendo, " +
            "pero no se aparta en él mercancía para servirla.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeAlmacenNoEncontrado,
            $"No hay ningún almacén con el identificador {almacenId}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de almacenes ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>La ubicación de una línea del consumo, preguntada con el almacén de la reserva.</summary>
    /// <remarks>
    /// <para>
    /// <b>Hereda el estado de su almacén</b> (ADR-0037): con el almacén bloqueado, sus ubicaciones
    /// contestan lo mismo que una bloqueada, y el consumo no sale.
    /// </para>
    /// <para>
    /// <b>Una ubicación que no es del almacén pasa</b>: no tiene nada que sacar, y la contesta el
    /// físico del hueco, <c>reserva-consumo-sin-stock</c>, como un lote que no existe.
    /// </para>
    /// </remarks>
    /// <param name="estado">Lo que contestó el puerto de ubicaciones.</param>
    /// <param name="linea">La primera línea que sale de ella, desde uno.</param>
    /// <param name="ubicacionId">Por cuál se preguntó.</param>
    /// <returns>Correcto si de esa ubicación puede salir algo nuevo, o si no es del almacén.</returns>
    internal static Resultado LaUbicacion(EstadoDeMaestro estado, int linea, Guid ubicacionId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo or EstadoDeMaestro.NoExiste => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeUbicacionBloqueada,
            $"La ubicación {ubicacionId} de la línea {linea} está bloqueada, o lo está su almacén: lo " +
            "que ya hay apuntado a ella se sigue leyendo, pero no sale nada nuevo de ese hueco.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de ubicaciones ha contestado un estado que este caso de uso no sabe traducir."),
    };

    internal static Resultado ElArticulo(AptitudParaMoverExistencias aptitud, Guid articuloId) =>
        aptitud switch
        {
            AptitudParaMoverExistencias.SeOfreceParaLoNuevo => Resultado.Correcto(),
            AptitudParaMoverExistencias.NoSeAlmacena => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeArticuloNoSeAlmacena,
                $"El artículo {articuloId} no se almacena: es un servicio, y un servicio no tiene " +
                "existencias que apartar.")),
            AptitudParaMoverExistencias.NoExiste => Resultado.Fallo(ArticuloNoEncontrado(articuloId)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(aptitud),
                aptitud,
                "El puerto de artículos ha contestado una aptitud que este caso de uso no sabe traducir."),
        };

    /// <summary>El artículo no existe en la empresa, o no tiene unidad base que copiar.</summary>
    /// <remarks>
    /// <b>Los dos caminos dan el mismo código</b>: un artículo apto tiene siempre unidad base, así que
    /// que falte es que el artículo desapareció entre las dos preguntas, y para quien llama es lo
    /// mismo que no haberlo encontrado.
    /// </remarks>
    /// <param name="articuloId">El artículo.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ArticuloNoEncontrado(Guid articuloId) => ErrorDeOperacion.Validacion(
        CodigoDeArticuloNoEncontrado,
        $"No hay ningún artículo con el identificador {articuloId}.");
}
