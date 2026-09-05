using Bastion.BuildingBlocks.Application.Multiempresa;
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
    /// <b>Delega desde el ítem 1.5</b>, y no repite el código ni el texto. La situación no es de
    /// este módulo —le pasa a cualquier caso de uso que mire la empresa del <i>claim</i>, y a
    /// Terceros le pasa igual—, así que el <c>type</c> es uno solo y vive en el bloque común. Este
    /// método se queda porque es el nombre por el que lo llaman los cinco casos de uso de aquí.
    /// </remarks>
    internal static ErrorDeOperacion NoOperativa() =>
        ErroresDeInquilinato.EmpresaActivaNoOperativa();
}
