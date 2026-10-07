using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>
/// Qué contesta el recuento a cada estado que le devuelven los puertos.
/// </summary>
/// <remarks>
/// <para>
/// <b>La misma tabla que la del ajuste y la de la transferencia, con su prefijo</b>: solo se admite
/// <c>SeOfreceParaLoNuevo</c>, y lo bloqueado se sigue leyendo (ADR-0037). Un almacén bloqueado no
/// se cuenta, porque el recuento acaba en un ajuste, y un ajuste no entra en él.
/// </para>
/// <para>
/// <b>Un almacén de otra empresa contesta lo mismo que uno inventado</b>, porque el puerto lo
/// pregunta con el filtro de la empresa puesto: no hay oráculo.
/// </para>
/// </remarks>
internal static class LosMaestrosDelRecuento
{
    internal const string CodigoDeAlmacenNoEncontrado = "recuento-almacen-no-encontrado";
    internal const string CodigoDeAlmacenBloqueado = "recuento-almacen-bloqueado";
    internal const string CodigoDeSerieNoEncontrada = "recuento-serie-no-encontrada";
    internal const string CodigoDeSerieCerrada = "recuento-serie-cerrada";
    internal const string CodigoDeUbicacionNoEncontrada = "recuento-ubicacion-no-encontrada";
    internal const string CodigoDeUbicacionBloqueada = "recuento-ubicacion-bloqueada";
    internal const string CodigoDeArticuloNoEncontrado = "recuento-articulo-no-encontrado";
    internal const string CodigoDeArticuloNoSeAlmacena = "recuento-articulo-no-se-almacena";

    internal static Resultado ElAlmacen(EstadoDeMaestro estado, Guid almacenId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeAlmacenBloqueado,
            $"El almacén {almacenId} está bloqueado: sus movimientos anteriores se siguen leyendo, " +
            "pero no admite un ajuste nuevo, y un recuento acaba en uno.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeAlmacenNoEncontrado,
            $"No hay ningún almacén con el identificador {almacenId}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de almacenes ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>Una de las dos series del alta, preguntada al abrir (ADR-0055 §1.3).</summary>
    /// <remarks>
    /// Es la cortesía, como en el ajuste: la garantía de la R5 la cobra la sentencia que toma el
    /// número, al confirmar, y contesta <c>serie-no-numera</c> o <c>serie-de-otro-documento</c>. El
    /// mensaje dice cuál de las dos es, porque los códigos son los mismos.
    /// </remarks>
    /// <param name="estado">Lo que contestó el puerto de series.</param>
    /// <param name="serieId">Por cuál se preguntó.</param>
    /// <param name="cual">Cuál de las dos es, dicha para una persona: «del recuento» o «del ajuste».</param>
    /// <returns>Correcto si esa serie se ofrece para numerar algo nuevo.</returns>
    internal static Resultado LaSerie(EstadoDeMaestro estado, Guid serieId, string cual) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeSerieCerrada,
            $"La serie {cual}, {serieId}, está cerrada: sigue resolviendo los documentos que ya " +
            "numeró, pero no entrega ni un número más.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeSerieNoEncontrada,
            $"No hay ninguna serie con el identificador {serieId}, que es la {cual}.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de series ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>La ubicación de una clave añadida, que tiene que ser del almacén del recuento (ADR-0055 §6).</summary>
    /// <remarks>
    /// <b>Una ubicación de otro almacén contesta lo mismo que una inventada</b>: el puerto pregunta por
    /// las dos cosas a la vez, y para este recuento no existe.
    /// </remarks>
    /// <param name="estado">Lo que contestó el puerto de ubicaciones.</param>
    /// <param name="ubicacionId">Por cuál se preguntó.</param>
    /// <returns>Correcto si se ofrece para lo nuevo.</returns>
    internal static Resultado LaUbicacion(EstadoDeMaestro estado, Guid ubicacionId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeUbicacionBloqueada,
            $"La ubicación {ubicacionId} está bloqueada: lo que ya hay apuntado a ella se sigue " +
            "leyendo, pero no se cuenta una clave nueva en ese hueco, porque acabaría en un ajuste.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeUbicacionNoEncontrada,
            $"No hay ninguna ubicación {ubicacionId} en el almacén de este recuento.")),
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de ubicaciones ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>El artículo de una clave añadida, que tiene que almacenarse (ADR-0055 §6).</summary>
    /// <param name="aptitud">Lo que contestó el puerto de artículos.</param>
    /// <param name="articuloId">Por cuál se preguntó.</param>
    /// <returns>Correcto si se pueden mover existencias contra él.</returns>
    internal static Resultado ElArticulo(AptitudParaMoverExistencias aptitud, Guid articuloId) =>
        aptitud switch
        {
            AptitudParaMoverExistencias.SeOfreceParaLoNuevo => Resultado.Correcto(),
            AptitudParaMoverExistencias.NoSeAlmacena => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeArticuloNoSeAlmacena,
                $"El artículo {articuloId} no se almacena: es un servicio, y un servicio no tiene " +
                "existencias que contar.")),
            AptitudParaMoverExistencias.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeArticuloNoEncontrado,
                $"No hay ningún artículo con el identificador {articuloId}.")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(aptitud),
                aptitud,
                "El puerto de artículos ha contestado una aptitud que este caso de uso no sabe traducir."),
        };
}
