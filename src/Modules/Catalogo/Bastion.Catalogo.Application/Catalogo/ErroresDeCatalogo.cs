using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Los desenlaces fallidos que comparten los casos de uso de artículo.</summary>
internal static class ErroresDeArticulo
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "articulo-no-encontrado",
        $"No hay ningún artículo con el identificador {id}.");

    /// <summary>
    /// El código ya lo tiene otro artículo de esta empresa.
    /// </summary>
    /// <remarks>
    /// <b>Lleva el código dentro</b>, al revés que el conflicto de un tercero. Allí el cuerpo tiene
    /// que ser indistinguible entre «lo ocupa uno activo» y «lo ocupa uno bloqueado», porque
    /// distinguirlos convertiría el formulario de alta en el censo de las bajas del art. 32. Aquí
    /// no hay dos casos que igualar —un artículo no se bloquea ni se retira— y el código es lo
    /// único que quien lo ve necesita para saber qué corregir.
    /// </remarks>
    internal static ErrorDeOperacion CodigoDuplicado(string codigo) => ErrorDeOperacion.Conflicto(
        "articulo-duplicado",
        $"Esta empresa ya tiene un artículo con el código {codigo}.");

    internal static ErrorDeOperacion TipoNoValido(string valores) => ErrorDeOperacion.Validacion(
        "articulo-tipo-no-valido",
        $"El tipo de artículo tiene que ser uno de estos: {valores}.");
}

/// <summary>Los desenlaces fallidos que comparten los casos de uso de categoría.</summary>
internal static class ErroresDeCategoria
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "categoria-no-encontrada",
        $"No hay ninguna categoría con el identificador {id}.");

    internal static ErrorDeOperacion CodigoDuplicado(string codigo) => ErrorDeOperacion.Conflicto(
        "categoria-duplicada",
        $"Esta empresa ya tiene una categoría con el código {codigo}.");
}

/// <summary>Los desenlaces fallidos de los casos de uso de suministro.</summary>
internal static class ErroresDeArticuloProveedor
{
    /// <summary>Código del error del tercero que no se puede poner como proveedor.</summary>
    /// <remarks>
    /// Público como constante porque lo cita la prueba que comprueba que los estados que no
    /// autorizan <b>se responden igual</b>. Sin la constante, esa prueba tendría el literal escrito
    /// tres veces y podría seguir verde con un literal cambiado en el código.
    /// </remarks>
    public const string CodigoDeTerceroNoValido = "articulo-proveedor-tercero-no-valido";

    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "articulo-proveedor-no-encontrado",
        $"No hay ningún suministro con el identificador {id}.");

    /// <summary>Ese tercero ya consta como proveedor de ese artículo.</summary>
    /// <remarks>
    /// <b>Lleva el identificador dentro y no delata nada</b>, porque quien recibe esta respuesta es
    /// quien acaba de mandar ese identificador en el cuerpo: no se le está contando nada que no
    /// supiera. Lo que sí delataría es distinguir POR QUÉ no se puede añadir a uno que no está —y
    /// eso es lo que hace <see cref="TerceroNoValido"/>, callándolo—. Y solo se llega aquí con un
    /// tercero que el listado enseña: el estado se pregunta antes, así que el suministro escondido
    /// de un bloqueado contesta aquel error y no este.
    /// </remarks>
    internal static ErrorDeOperacion YaEsProveedor(Guid terceroId) => ErrorDeOperacion.Conflicto(
        "articulo-proveedor-duplicado",
        $"El tercero {terceroId} ya consta como proveedor de este artículo.");

    /// <summary>
    /// El tercero no sirve: no existe, o está bloqueado, o no es proveedor. <b>Una sola respuesta
    /// para los tres.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SIN PARÁMETROS, y esa es toda la decisión.</b> <c>IConsultaDeTerceros</c> contesta tres
    /// estados para que la regla pueda decidir; los dos que no autorizan —y las tres situaciones
    /// que caben en <c>NoExiste</c>— salen por aquí como uno solo. Si se distinguieran, el formulario de «añadir proveedor» sería el censo de las bajas
    /// del art. 32: cualquiera con permiso para añadir proveedores podría recorrer identificadores
    /// y separar los que no existen de los que existen y están reservados. Es el mismo criterio con
    /// el que <c>ErroresDeTercero.IdentificacionDuplicada</c> no dice si el ocupante está activo o
    /// bloqueado (ítem 1.5).
    /// </para>
    /// <para>
    /// <b>Y el código de estado también es uno solo</b>, que es la mitad que se olvida: un
    /// <c>400</c> para «no existe» y un <c>409</c> para «está bloqueado» distinguirían los dos
    /// casos sin escribir una palabra distinta. Igualar el cuerpo y no el estado no iguala nada.
    /// Los tres salen como <b>validación</b>, porque lo que quien lo recibe tiene que hacer es lo
    /// mismo en los tres: mirar el identificador que ha mandado.
    /// </para>
    /// <para>
    /// El texto dice las dos causas que un usuario legítimo puede arreglar —el identificador y el
    /// papel— y no dice cuál es la suya. La tercera no se nombra: nombrarla diría que existe.
    /// </para>
    /// </remarks>
    internal static ErrorDeOperacion TerceroNoValido() => ErrorDeOperacion.Validacion(
        CodigoDeTerceroNoValido,
        "Ese tercero no se puede poner como proveedor de este artículo. Compruebe el " +
        "identificador y que la ficha esté dada de alta como proveedor.");
}
