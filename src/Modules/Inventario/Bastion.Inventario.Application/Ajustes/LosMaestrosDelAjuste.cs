using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>
/// Qué contesta el alta de un ajuste a cada estado que le devuelven los puertos del ítem 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>El alta solo admite <c>SeOfreceParaLoNuevo</c>, y esa es la mitad que se ve aquí.</b> La
/// otra mitad —que un movimiento <b>ya escrito</b> contra un almacén bloqueado se sigue leyendo—
/// no está en este fichero porque no es una validación: es que la lectura no pregunta. Las dos
/// juntas son la regla del ADR-0037: lo que el bloqueo del artículo 32 reserva es la privacidad de
/// una persona, no la existencia de una estantería.
/// </para>
/// <para>
/// Los mensajes dicen el identificador. Un «no encontrado» sin decir cuál obliga a quien lo lee a
/// adivinar cuál de los cuatro que envió es el que no existe.
/// </para>
/// </remarks>
internal static class LosMaestrosDelAjuste
{
    internal const string CodigoDeAlmacenNoEncontrado = "ajuste-almacen-no-encontrado";
    internal const string CodigoDeAlmacenBloqueado = "ajuste-almacen-bloqueado";
    internal const string CodigoDeUbicacionNoEncontrada = "ajuste-ubicacion-no-encontrada";
    internal const string CodigoDeUbicacionBloqueada = "ajuste-ubicacion-bloqueada";
    internal const string CodigoDeUnidadNoEncontrada = "ajuste-unidad-no-encontrada";
    internal const string CodigoDeUnidadRetirada = "ajuste-unidad-retirada";
    internal const string CodigoDeArticuloNoEncontrado = "ajuste-articulo-no-encontrado";
    internal const string CodigoDeArticuloNoSeAlmacena = "ajuste-articulo-no-se-almacena";

    internal static Resultado ElAlmacen(EstadoDeMaestro estado, Guid almacenId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeAlmacenBloqueado,
            $"El almacén {almacenId} está bloqueado: sus movimientos anteriores se siguen leyendo " +
            "y valorando, pero no se admite un ajuste nuevo contra él.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeAlmacenNoEncontrado,
            $"No hay ningún almacén con el identificador {almacenId}.")),

        // Igual que en el alta de artículo: el `default` no es defensivo por costumbre. Un cuarto
        // valor del enumerado tiene que reventar en la primera petición y no colarse por la rama
        // permisiva, porque no es un desenlace de negocio sino un valor que este código no sabe
        // interpretar (ADR-0004).
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de almacenes ha contestado un estado que este caso de uso no sabe traducir."),
    };

    internal static Resultado LaUbicacion(EstadoDeMaestro estado, Guid ubicacionId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),
        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeUbicacionBloqueada,
            $"La ubicación {ubicacionId} está bloqueada: lo que ya hay apuntado a ella se sigue " +
            "leyendo, pero no se mueve nada nuevo a ese hueco.")),
        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeUbicacionNoEncontrada,
            $"No hay ninguna ubicación {ubicacionId} en ese almacén.")),
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

    /// <summary>
    /// El artículo se pregunta por su <b>aptitud</b> y no por su estado, y el enumerado es otro.
    /// </summary>
    /// <remarks>
    /// El artículo no se bloquea —no hay un solo dato de persona en su ficha— así que su puerto no
    /// contesta <c>EstadoDeMaestro</c>: contesta a «¿puedo mover existencias contra esto?», y su
    /// tercer valor es <c>NoSeAlmacena</c>, que es lo que pasa con un servicio. Traducirlo al
    /// mismo enumerado que los otros dos habría hecho que un servicio se contara como «retirado»,
    /// que no es lo que es.
    /// </remarks>
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
                "existencias que ajustar.")),
            AptitudParaMoverExistencias.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeArticuloNoEncontrado,
                $"No hay ningún artículo con el identificador {articuloId}.")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(aptitud),
                aptitud,
                "El puerto de artículos ha contestado una aptitud que este caso de uso no sabe traducir."),
        };
}
