using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.Api.FunctionalTests.Salud;

/// <summary>
/// La API levantada sin ninguna dependencia externa: ni PostgreSQL ni recolector de
/// telemetría (ver <see cref="AjustesMinimos"/>).
/// </summary>
public sealed class ApiSinDependencias : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => AjustesMinimos.Aplicar(builder);
}
