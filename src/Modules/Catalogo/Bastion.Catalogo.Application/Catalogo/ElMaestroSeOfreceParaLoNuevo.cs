using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Traduce los tres valores de <see cref="EstadoDeMaestro"/> a los tres desenlaces que el alta de
/// un artículo puede tener con un maestro ajeno.
/// </summary>
/// <remarks>
/// <para>
/// <b>Catálogo es el consumidor para el que se construyó la retirada</b>, y esta clase es donde el
/// enumerado deja de ser una forma y pasa a significar algo. El ADR-0023 y el ítem 1.7 dejaron los
/// tres valores contestables por los puertos; hasta aquí nadie los había ramificado en un camino
/// de negocio. Las tres casillas:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="EstadoDeMaestro.SeOfreceParaLoNuevo"/> — el alta pasa.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/> — el alta se rechaza, <b>y el artículo
///     que ya la usaba sigue resolviéndola</b>. Las dos mitades, no una: la segunda es la que
///     distingue la retirada de un borrado, y la que impide que retirar una unidad se convierta en
///     un congelador para todos los artículos que la usan.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="EstadoDeMaestro.NoExiste"/> — el alta se rechaza con un error <b>distinguible
///     del anterior</b>. No es cortesía: quien teclea un identificador que no existe tiene que
///     corregir el identificador, y quien apunta a uno retirado tiene que elegir otro que sí se
///     ofrezca. Son dos arreglos distintos, así que son dos <c>type</c> distintos del catálogo
///     (ADR-0030) y el frontal escribe un texto para cada uno.
///     </description>
///   </item>
/// </list>
/// <para>
/// <b>Y por qué distinguirlos aquí sí, cuando en Terceros el conflicto de identificador NO se
/// distingue.</b> Allí las dos respuestas se igualan a propósito porque distinguirlas convertiría
/// el formulario de alta en el censo de quién está dado de baja por el art. 32 — datos personales.
/// Aquí lo que hay al otro lado es un maestro de instalación: qué unidades de medida existen y
/// cuáles se han retirado no es un dato de nadie, se ve entero en su propio listado, y esconderlo
/// solo serviría para que quien da de alta un artículo no supiera qué corregir.
/// </para>
/// <para>
/// <b>Vive en la capa de aplicación</b>, como <c>LaInversaEsPlausible</c> del ítem 1.7: preguntar
/// a otro módulo es exactamente lo que un dominio no hace. Y el desenlace es un error de negocio
/// con nombre, no una excepción (ADR-0004).
/// </para>
/// </remarks>
internal static class ElMaestroSeOfreceParaLoNuevo
{
    /// <summary>Código del error de una unidad de medida que no existe.</summary>
    public const string CodigoDeUnidadNoEncontrada = "articulo-unidad-no-encontrada";

    /// <summary>Código del error de una unidad de medida retirada.</summary>
    public const string CodigoDeUnidadRetirada = "articulo-unidad-retirada";

    /// <summary>Código del error de un tramo de impuesto que no existe.</summary>
    public const string CodigoDeImpuestoNoEncontrado = "articulo-impuesto-no-encontrado";

    /// <summary>Código del error de un tramo de impuesto que no rige en esa fecha.</summary>
    public const string CodigoDeImpuestoNoVigente = "articulo-impuesto-no-vigente";

    /// <summary>El desenlace de haber preguntado por la unidad base.</summary>
    /// <param name="estado">Lo que contestó <c>IConsultaDeUnidadesDeMedida</c>.</param>
    /// <param name="unidadId">El identificador por el que se preguntó.</param>
    internal static Resultado LaUnidad(EstadoDeMaestro estado, Guid unidadId) => estado switch
    {
        EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),

        EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeUnidadRetirada,
            $"La unidad de medida {unidadId} está retirada: sigue resolviendo lo que ya apunta a " +
            "ella, pero no se ofrece para un artículo nuevo. Elija una que siga en uso.")),

        EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
            CodigoDeUnidadNoEncontrada,
            $"No hay ninguna unidad de medida con el identificador {unidadId}.")),

        // El `default` NO es defensivo por costumbre. `EstadoDeMaestro` es una lista cerrada de
        // tres valores HOY; un cuarto entraría por aquí, y lo que tiene que pasar entonces es que
        // el alta reviente ruidosamente en la primera petición y no que se cuele por la rama
        // permisiva. Lanzar es lo correcto porque no es un desenlace de negocio: es un valor que
        // este código no sabe interpretar (ADR-0004).
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "El puerto de unidades ha contestado un estado que este caso de uso no sabe traducir."),
    };

    /// <summary>El desenlace de haber preguntado por el tramo de impuesto.</summary>
    /// <param name="estado">Lo que contestó <c>IConsultaDeImpuestos</c>.</param>
    /// <param name="impuestoId">El identificador por el que se preguntó.</param>
    /// <param name="enLaFechaDeDevengo">La fecha con la que se preguntó.</param>
    internal static Resultado ElImpuesto(
        EstadoDeMaestro estado,
        Guid impuestoId,
        DateOnly enLaFechaDeDevengo) => estado switch
        {
            EstadoDeMaestro.SeOfreceParaLoNuevo => Resultado.Correcto(),

            // «Retirado» no vale como palabra aquí: un tramo de impuesto no se retira, deja de regir
            // —o todavía no rige, si su vigencia empieza mañana—. Los dos casos caen en el mismo valor
            // del enumerado y los dos se cuentan diciendo la fecha, que es lo que quien lee necesita
            // para entender por qué le han dicho que no.
            EstadoDeMaestro.SoloResuelveLoViejo => Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeImpuestoNoVigente,
                $"El tramo de impuesto {impuestoId} no rige el {enLaFechaDeDevengo:yyyy-MM-dd}: sigue " +
                "resolviendo la cuota de las facturas que ya lo usan, pero no se propone para un " +
                "artículo nuevo. Elija el tramo vigente.")),

            EstadoDeMaestro.NoExiste => Resultado.Fallo(ErrorDeOperacion.Validacion(
                CodigoDeImpuestoNoEncontrado,
                $"No hay ningún tramo de impuesto con el identificador {impuestoId}.")),

            _ => throw new ArgumentOutOfRangeException(
                nameof(estado),
                estado,
                "El puerto de impuestos ha contestado un estado que este caso de uso no sabe traducir."),
        };
}
