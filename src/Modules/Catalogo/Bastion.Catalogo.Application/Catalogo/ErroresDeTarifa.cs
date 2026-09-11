using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Los desenlaces fallidos que comparten los casos de uso de tarifa.</summary>
internal static class ErroresDeTarifa
{
    /// <summary>Código del error de una tarifa que no existe.</summary>
    internal const string CodigoNoEncontrada = "tarifa-no-encontrada";

    /// <summary>Código del error de una línea de tarifa que no existe.</summary>
    internal const string CodigoLineaNoEncontrada = "tarifa-linea-no-encontrada";

    /// <summary>Código del error de una tarifa que existe pero no rige esa fecha.</summary>
    internal const string CodigoNoVigente = "tarifa-no-vigente";

    /// <summary>Código del error de una tarifa que rige pero no dice nada de ese artículo.</summary>
    internal const string CodigoSinLineaAplicable = "tarifa-sin-linea-aplicable";

    /// <summary>Código del error de una línea con precio y descuento, o sin ninguno.</summary>
    internal const string CodigoPrecioODescuento = "tarifa-linea-precio-o-descuento";

    /// <summary>Código del error de una línea sin destino, o con los dos.</summary>
    internal const string CodigoArticuloOCategoria = "tarifa-linea-articulo-o-categoria";

    /// <summary>Código del error del primer tramo de un destino que no empieza en cero.</summary>
    internal const string CodigoPrimerTramoNoEmpiezaEnCero = "tarifa-linea-primer-tramo-sin-cero";

    /// <summary>Código del error de un tramo repetido para el mismo destino.</summary>
    internal const string CodigoTramoDuplicado = "tarifa-linea-tramo-duplicado";

    /// <summary>Código del error de dos tramos de la misma tarifa que se pisan.</summary>
    internal const string CodigoVigenciasSolapadas = "tarifa-vigencias-solapadas";

    /// <summary>Código del error de una vigencia que acaba antes de empezar.</summary>
    internal const string CodigoVigenciaAlReves = "tarifa-vigencia-al-reves";

    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrada,
        $"No hay ningún tramo de tarifa con el identificador {id}.");

    internal static ErrorDeOperacion LineaNoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        CodigoLineaNoEncontrada,
        $"No hay ninguna línea de tarifa con el identificador {id}.");

    /// <summary>
    /// Ningún tramo con ese código, que es distinto de «hay tarifa y no rige hoy».
    /// </summary>
    /// <remarks>
    /// <b>Los dos errores no se unifican, y es la misma decisión que la unidad retirada frente a la
    /// inexistente</b> (ADR-0030): quien escribe un código que no existe tiene que corregir el
    /// código, y quien pregunta por una fecha que ninguna vigencia cubre tiene que abrir el tramo
    /// que falta o preguntar por otra fecha. Son dos arreglos distintos, así que son dos
    /// <c>type</c> distintos y el frontal escribe un texto para cada uno. Unificados, el segundo
    /// —que es el que de verdad pasa, porque una tarifa caducada y sin sucesora es el descuido
    /// normal— saldría como «esa tarifa no existe» delante de alguien que la tiene en pantalla.
    /// </remarks>
    internal static ErrorDeOperacion CodigoNoConocido(string codigo) =>
        ErrorDeOperacion.NoEncontrado(
            CodigoNoEncontrada,
            $"Esta empresa no tiene ninguna tarifa con el código {codigo}.");

    internal static ErrorDeOperacion NoVigente(string codigo, DateOnly fecha) =>
        ErrorDeOperacion.Validacion(
            CodigoNoVigente,
            $"La tarifa {codigo} existe, pero ninguno de sus tramos rige el " +
            $"{fecha:yyyy-MM-dd}. Abra el tramo que falta o pregunte por una fecha que alguno " +
            "cubra.");

    /// <summary>
    /// Nada de la tarifa alcanza a ese artículo.
    /// </summary>
    /// <remarks>
    /// <b>Un error con nombre y nunca un precio cero</b>, que es la decisión 3 del ADR-0023 con
    /// otro sujeto. Un cero se propaga sin ruido: entra en una línea de documento, suma cero al
    /// total, y el descuadre aparece semanas después sin autor. Un rechazo con nombre para el
    /// trabajo en el sitio exacto donde falta el dato y dice qué falta.
    /// </remarks>
    internal static ErrorDeOperacion SinLineaAplicable(
        string codigo,
        Guid articuloId,
        decimal cantidad) =>
        ErrorDeOperacion.Validacion(
            CodigoSinLineaAplicable,
            $"La tarifa {codigo} no le pone precio al artículo {articuloId} para una cantidad de " +
            $"{cantidad}: no hay línea suya ni de ninguna categoría de la que cuelgue. No se " +
            "devuelve cero, que sería un precio que nadie ha escrito.");

    internal static ErrorDeOperacion PrecioODescuento() => ErrorDeOperacion.Validacion(
        CodigoPrecioODescuento,
        "Una línea de tarifa lleva un precio o un descuento, exactamente uno. Con los dos, el " +
        "orden en que se aplican no lo dice nadie; sin ninguno, lo que devolvería al aplicarse " +
        "es un cero que nadie ha escrito.");

    internal static ErrorDeOperacion ArticuloOCategoria() => ErrorDeOperacion.Validacion(
        CodigoArticuloOCategoria,
        "Una línea de tarifa se aplica a un artículo o a una categoría, exactamente a uno.");

    internal static ErrorDeOperacion PrimerTramoNoEmpiezaEnCero(decimal cantidadDesde) =>
        ErrorDeOperacion.Validacion(
            CodigoPrimerTramoNoEmpiezaEnCero,
            $"La primera línea de este destino tiene que empezar en 0 y empieza en " +
            $"{cantidadDesde}. Si no, toda cantidad por debajo de {cantidadDesde} se quedaría sin " +
            "tramo y la tarifa contestaría «sin línea aplicable» con el precio escrito debajo.");

    internal static ErrorDeOperacion TramoDuplicado(decimal cantidadDesde) =>
        ErrorDeOperacion.Conflicto(
            CodigoTramoDuplicado,
            $"Este destino ya tiene una línea que empieza en {cantidadDesde} dentro de esta " +
            "tarifa. Dos tramos que empiezan en la misma cantidad se solapan, y cuál gana lo " +
            "decidiría el orden en que salgan las filas.");

    /// <summary>
    /// Dos tramos de la misma tarifa se pisan.
    /// </summary>
    /// <remarks>
    /// <b>Lo detecta la base y no el caso de uso</b>, con una restricción de exclusión, igual que
    /// los tramos de impuesto del 0.15: una comprobación en la aplicación es una carrera —dos
    /// peticiones simultáneas la pasan las dos— y además solo protege el camino que pasa por ella.
    /// Esto traduce el fallo de la restricción a un error de negocio con nombre.
    /// </remarks>
    /// <summary>
    /// La vigencia acaba antes de empezar.
    /// </summary>
    /// <remarks>
    /// <b>Se contesta aquí y no se deja llegar al dominio</b>, aunque <c>Tarifa.Crear</c> también
    /// lo rechace. Es la puerta doble del ADR-0004: lo que llega de una petición HTTP se convierte
    /// en un error de negocio con nombre, y la excepción del dominio queda para quien construya la
    /// entidad por un camino que no pase por aquí. Ninguna de las dos sobra y ninguna sustituye a
    /// la otra.
    /// </remarks>
    internal static ErrorDeOperacion VigenciaAlReves(DateOnly desde, DateOnly hasta) =>
        ErrorDeOperacion.Validacion(
            CodigoVigenciaAlReves,
            $"La tarifa dejaría de regir el {hasta:yyyy-MM-dd}, antes de empezar a regir el " +
            $"{desde:yyyy-MM-dd}.");

    internal static ErrorDeOperacion VigenciasSolapadas(string codigo) =>
        ErrorDeOperacion.Conflicto(
            CodigoVigenciasSolapadas,
            $"Esta empresa ya tiene un tramo de la tarifa {codigo} que se pisa con el que se " +
            "intenta guardar. Dos tramos solapados harían que «la tarifa del día D» tuviera dos " +
            "respuestas. Cierre el anterior antes de abrir el siguiente.");
}
