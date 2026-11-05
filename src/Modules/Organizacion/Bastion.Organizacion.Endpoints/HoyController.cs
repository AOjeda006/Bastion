using Bastion.Organizacion.Application.Fechas;
using Bastion.Organizacion.Contracts.Fechas;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Hoy, bajo <c>/api/v1/organizacion/hoy</c>: el día de la empresa activa y el de cada zona.</summary>
/// <param name="consultar">El caso de uso.</param>
public sealed class HoyController(IConsultarHoy consultar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve hoy en la empresa activa y en cada zona admitida (ADR-0079 §12).</summary>
    /// <remarks>
    /// Autenticada pero SIN permiso: saber qué día es no es una facultad que se conceda. Lo necesita
    /// todo formulario con fecha, sea del módulo que sea. Y solo dice las zonas de la empresa
    /// activa, que sale del <i>claim</i>, como en todo lo demás (R8).
    /// </remarks>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ProducesResponseType(typeof(HoyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Consultar(CancellationToken cancelacion) =>
        Ok(await consultar.EjecutarAsync(cancelacion).ConfigureAwait(false));
}
