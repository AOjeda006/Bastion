using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Los desenlaces de negocio del recuento que no vienen de un puerto.</summary>
/// <remarks>
/// <b>Con el prefijo <c>recuento-</c></b> (ADR-0055 §13), como el ajuste y la transferencia llevan
/// el suyo: el mismo almacén bloqueado se dice distinto en cada documento, porque el frontal lo
/// explica distinto.
/// </remarks>
internal static class ErroresDeRecuento
{
    internal const string CodigoNoEncontrado = "recuento-no-encontrado";
    internal const string CodigoLineaNoEncontrada = "recuento-linea-no-encontrada";
    internal const string CodigoMotivoNoValido = "recuento-motivo-no-valido";
    internal const string CodigoYaHayUnoEnCurso = "recuento-ya-hay-uno-en-curso";

    internal static ErrorDeOperacion NoEncontrado(Guid recuentoId) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrado,
        $"No hay ningún recuento con el identificador {recuentoId}.");

    /// <summary>La línea no es de ese recuento, o no existe.</summary>
    /// <remarks>
    /// <b>Las dos cosas contestan lo mismo</b>: una línea de otro recuento, por la ruta de este, no
    /// existe. Distinguirlas diría que la línea está en otro sitio.
    /// </remarks>
    /// <param name="recuentoId">El recuento de la ruta.</param>
    /// <param name="lineaId">La línea que se buscó en él.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion LineaNoEncontrada(Guid recuentoId, Guid lineaId) =>
        ErrorDeOperacion.NoEncontrado(
            CodigoLineaNoEncontrada,
            $"El recuento {recuentoId} no tiene ninguna línea con el identificador {lineaId}.");

    /// <summary>El motivo del alta, que es el que llevará su ajuste.</summary>
    /// <remarks>
    /// <b>Se comprueba aquí aunque el dominio también lo compruebe</b>, como en el ajuste: ahí es una
    /// invariante y se lanza, y aquí es un cuerpo mal escrito, que es un <c>400</c> con su código.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        $"El motivo no puede estar vacío ni pasar de {Recuento.LargoDelMotivo} caracteres: es el que " +
        "llevará el ajuste de la diferencia, y lo único que queda para entenderla dentro de dos años.");

    /// <summary>El almacén ya tiene un recuento en curso (ADR-0055 §1.7).</summary>
    /// <remarks>
    /// <para>
    /// <b>Lo dan dos caminos, y tienen que dar el mismo cuerpo</b>, como el GTIN repetido del 2.10. La
    /// comprobación previa del alta lo devuelve sin llegar al motor, y el borde lo contesta cuando el
    /// índice único parcial para a quien pierde la carrera de dos altas a la vez. El índice solo trae
    /// su nombre, así que el error no lleva el almacén: si lo llevara, la respuesta diría por qué
    /// camino se llegó.
    /// </para>
    /// <para>
    /// <b>Por eso Infrastructure ve esta clase</b>: la declaración de la restricción vive en
    /// <c>ModuloDeInventario</c>, y una sola fábrica es un solo texto.
    /// </para>
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion YaHayUnoEnCurso() => ErrorDeOperacion.Conflicto(
        CodigoYaHayUnoEnCurso,
        "Ese almacén ya tiene un recuento en curso, y dos a la vez se pisarían: cada uno movería la " +
        "diferencia contra el teórico que deja el otro. Confírmelo o descártelo antes de abrir otro.");
}
