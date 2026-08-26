using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>
/// Los desenlaces fallidos que comparten varios casos de uso de empresa.
/// </summary>
/// <remarks>
/// Están juntos porque el <c>Codigo</c> es CONTRATO —acaba publicado en el <c>type</c> del
/// ProblemDetails y un cliente ramifica sobre él (ADR-0004)—. Escrito a mano en cada caso de uso,
/// el día que alguien corrija una errata en uno de los tres sitios rompe a los clientes que
/// miraban ese código, sin enterarse.
/// </remarks>
internal static class ErroresDeEmpresa
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "empresa-no-encontrada",
        $"No hay ninguna empresa con el identificador {id}.");

    internal static ErrorDeOperacion Bloqueada(Guid id) => ErrorDeOperacion.Conflicto(
        "empresa-bloqueada",
        $"La empresa {id} está bloqueada y sus datos no se pueden tratar (art. 32 LOPDGDD). " +
        "Desbloquéela antes de modificarla.");
}
