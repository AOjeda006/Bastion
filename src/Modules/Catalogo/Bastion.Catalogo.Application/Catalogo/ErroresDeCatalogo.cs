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
