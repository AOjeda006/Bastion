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

    /// <summary>La empresa del <i>claim</i> no existe o está bloqueada.</summary>
    /// <remarks>
    /// Sin identificador en el mensaje, y no por descuido: quien recibe esto no ha escrito ninguna
    /// empresa —le vino en el token—, así que repetírsela no le ayuda a corregir nada. Lo que
    /// necesita saber es que su sesión apunta a una empresa que ya no opera y que tiene que volver
    /// a entrar.
    /// </remarks>
    internal static ErrorDeOperacion NoOperativa() => ErrorDeOperacion.Conflicto(
        "empresa-activa-no-operativa",
        "La empresa con la que está operando no existe o está bloqueada. Vuelva a iniciar sesión.");
}
