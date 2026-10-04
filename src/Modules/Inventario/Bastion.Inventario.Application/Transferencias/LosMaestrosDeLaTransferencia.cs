using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>
/// Qué contesta el alta de una transferencia a cada estado que le devuelven los puertos.
/// </summary>
/// <remarks>
/// <para>
/// <b>La misma tabla que la del ajuste, con su prefijo</b>: el alta solo admite
/// <c>SeOfreceParaLoNuevo</c>, y lo bloqueado se sigue leyendo (ADR-0037).
/// </para>
/// <para>
/// <b>Un almacén de otra empresa contesta lo mismo que uno inventado</b>, porque el puerto los
/// pregunta con el filtro de la empresa puesto, y los dos llegan aquí como <c>NoExiste</c>: no hay
/// oráculo (ADR-0053 §8). Por eso el mensaje dice el identificador y no «de otra empresa».
/// </para>
/// </remarks>
internal static class LosMaestrosDeLaTransferencia
{
    internal const string CodigoDeAlmacenNoEncontrado = "transferencia-almacen-no-encontrado";
    internal const string CodigoDeAlmacenBloqueado = "transferencia-almacen-bloqueado";
    internal const string CodigoDeUbicacionNoEncontrada = "transferencia-ubicacion-no-encontrada";
    internal const string CodigoDeUbicacionBloqueada = "transferencia-ubicacion-bloqueada";
    internal const string CodigoDeUnidadNoEncontrada = "transferencia-unidad-no-encontrada";
    internal const string CodigoDeUnidadRetirada = "transferencia-unidad-retirada";
    internal const string CodigoDeArticuloNoEncontrado = "transferencia-articulo-no-encontrado";
    internal const string CodigoDeArticuloNoSeAlmacena = "transferencia-articulo-no-se-almacena";
    internal const string CodigoDeSerieNoEncontrada = "transferencia-serie-no-encontrada";
    internal const string CodigoDeSerieCerrada = "transferencia-serie-cerrada";

    internal static Resultado ElAlmacen(EstadoDeMaestro estado, Guid almacenId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeAlmacenBloqueado,
            $"El almacén {almacenId} está bloqueado: sus movimientos anteriores se siguen leyendo " +
            "y valorando, pero no sale ni llega a él una transferencia nueva.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeAlmacenNoEncontrado,
            $"No hay ningún almacén con el identificador {almacenId}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de almacenes ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>La serie que numerará el documento al enviarlo, preguntada al darlo de alta.</summary>
    /// <remarks>
    /// Como en el ajuste, es la cortesía: la garantía de la R5 la cobra la sentencia que toma el
    /// número, al enviar, y contesta <c>serie-no-numera</c>.
    /// </remarks>
    /// <param name="estado">Lo que contestó el puerto de series.</param>
    /// <param name="serieId">Por cuál se preguntó.</param>
    /// <returns>Correcto si esa serie se ofrece para numerar algo nuevo.</returns>
    internal static Resultado LaSerie(EstadoDeMaestro estado, Guid serieId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeSerieCerrada,
            $"La serie {serieId} está cerrada: sigue resolviendo los documentos que ya numeró, " +
            "pero no entrega ni un número más.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeSerieNoEncontrada,
            $"No hay ninguna serie con el identificador {serieId}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de series ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>Una ubicación, preguntada con el almacén de su punta.</summary>
    /// <param name="estado">Lo que contestó el puerto de ubicaciones.</param>
    /// <param name="ubicacionId">Por cuál se preguntó.</param>
    /// <param name="almacenId">El almacén de su punta: el origen o el destino.</param>
    /// <returns>Correcto si la ubicación es de ese almacén y se ofrece para lo nuevo.</returns>
    internal static Resultado LaUbicacion(EstadoDeMaestro estado, Guid ubicacionId, Guid almacenId) =>
        estado switch
        {
            EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
            EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeUbicacionBloqueada,
                $"La ubicación {ubicacionId} está bloqueada: lo que ya hay apuntado a ella se sigue " +
                "leyendo, pero no se mueve nada nuevo a ese hueco ni desde él.")),
            EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeUbicacionNoEncontrada,
                $"No hay ninguna ubicación {ubicacionId} en el almacén {almacenId}.")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(estado),
                estado,
                "El puerto de ubicaciones ha contestado un estado que este caso de uso no sabe traducir."),
        };

    internal static Resultado LaUnidad(EstadoDeMaestro estado, Guid unidadId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeUnidadRetirada,
            $"La unidad de medida {unidadId} está retirada: sigue resolviendo lo que ya está " +
            "escrito en ella, pero no se escribe un movimiento nuevo con ella.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeUnidadNoEncontrada,
            $"No hay ninguna unidad de medida con el identificador {unidadId}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de unidades ha contestado un estado que este caso de uso no sabe traducir."),
    };

    internal static Resultado ElArticulo(AptitudParaMoverExistencias aptitud, Guid articuloId) =>
        aptitud switch
        {
            AptitudParaMoverExistencias.SeOfreceParaLoNuevo => Resultado.Correcto(),
            AptitudParaMoverExistencias.NoSeAlmacena => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeArticuloNoSeAlmacena,
                $"El artículo {articuloId} no se almacena: es un servicio, y un servicio no tiene " +
                "existencias que llevar de un almacén a otro.")),
            AptitudParaMoverExistencias.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeArticuloNoEncontrado,
                $"No hay ningún artículo con el identificador {articuloId}.")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(aptitud),
                aptitud,
                "El puerto de artículos ha contestado una aptitud que este caso de uso no sabe traducir."),
        };
}
