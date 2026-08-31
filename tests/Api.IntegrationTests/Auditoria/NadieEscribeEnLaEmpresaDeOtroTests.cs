using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// El cabo que dejó suelto el 0.6: <b>el filtro global no interviene en una escritura</b>.
/// </summary>
/// <remarks>
/// <para>
/// <c>HasQueryFilter</c> solo toca lo que pasa por el traductor de consultas. Un <c>INSERT</c> no
/// pasa por ahí, así que hasta el 0.7 que una fila naciera con la empresa buena dependía de que
/// cada caso de uso se acordara de escribir su <c>usuarioActual.EmpresaId</c> a mano — hoy son
/// tres sitios, con dieciséis módulos serán cientos. Y el fallo no da error: da una fila en la
/// empresa equivocada, que se lee como un dato correcto.
/// </para>
/// <para>
/// El interceptor ya recorre las entradas pendientes para escribir la traza, así que la
/// comprobación sale casi gratis y deja de depender de la memoria de nadie.
/// </para>
/// <para>
/// <b>Se prueba aquí y no por la API</b> porque ninguna petición puede nombrar una empresa —lo
/// comprueba <c>NingunaPeticionNombraLaEmpresaTests</c>, y es el diseño de R8—. Provocar la
/// situación exige ponerse justo donde se pondría el caso de uso equivocado.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class NadieEscribeEnLaEmpresaDeOtroTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Un_alta_con_la_empresa_de_otro_no_llega_a_la_base()
    {
        (HttpClient _, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000042L");
        (HttpClient _, EmpresaDto ajena) = await _api.EnUnaEmpresaNuevaAsync("00000043C");

        // La empresa la pone el caso de uso, y aquí la pone MAL. Es exactamente lo que pasa cuando
        // alguien copia un `CrearX` y se deja el `usuarioActual.EmpresaId` del original.
        var almacen = Almacen.Crear(ajena.Id, "AJENO-ALTA", "En la empresa de otro", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using OrganizacionDbContext contexto =
            postgres.AbrirOrganizacionAuditada(propia.Id, Guid.CreateVersion7());

        contexto.Almacenes.Add(almacen);

        EscrituraEnOtraEmpresaException error =
            await Should.ThrowAsync<EscrituraEnOtraEmpresaException>(() => contexto.SaveChangesAsync());

        error.Entidad.ShouldBe("Almacen");
        error.Intentada.ShouldBe(ajena.Id);
        error.Activa.ShouldBe(propia.Id);

        await using OrganizacionDbContext lectura = postgres.AbrirOrganizacion(ajena.Id);
        (await lectura.Almacenes.AnyAsync(fila => fila.Id == almacen.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Y_una_modificacion_que_cambia_la_empresa_de_una_fila_tampoco()
    {
        (HttpClient _, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000044K");
        (HttpClient _, EmpresaDto ajena) = await _api.EnUnaEmpresaNuevaAsync("00000045E");

        var almacen = Almacen.Crear(propia.Id, "AJENO-MOD", "Nace bien", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using (OrganizacionDbContext alta = postgres.AbrirOrganizacionAuditada(propia.Id, Guid.CreateVersion7()))
        {
            alta.Almacenes.Add(almacen);
            await alta.SaveChangesAsync();
        }

        await using OrganizacionDbContext contexto =
            postgres.AbrirOrganizacionAuditada(propia.Id, Guid.CreateVersion7());

        Almacen cargado = await contexto.Almacenes.SingleAsync(fila => fila.Id == almacen.Id);

        // Mudar una fila de empresa por la puerta de atrás: `EmpresaId` tiene el `set` privado, así
        // que se hace por donde único se puede, que es el rastreador de cambios. Sirve para lo
        // mismo que el caso anterior — la guarda mira las altas Y las modificaciones, porque el
        // camino de mudar una fila de empresa existe y no da error por sí solo.
        contexto.Entry(cargado).Property(nameof(Almacen.EmpresaId)).CurrentValue = ajena.Id;

        await Should.ThrowAsync<EscrituraEnOtraEmpresaException>(() => contexto.SaveChangesAsync());
    }

    [Fact]
    public async Task Con_la_empresa_de_uno_no_estorba()
    {
        // El control positivo: sin él, una guarda que lanzara SIEMPRE pasaría los dos casos de
        // arriba y rompería el sistema entero sin que estos tests se enteraran.
        (HttpClient _, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000046T");

        var almacen = Almacen.Crear(propia.Id, "PROPIO", "En la empresa de uno", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using OrganizacionDbContext contexto =
            postgres.AbrirOrganizacionAuditada(propia.Id, Guid.CreateVersion7());

        contexto.Almacenes.Add(almacen);

        await contexto.SaveChangesAsync();

        (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).ShouldHaveSingleItem();
    }
}
