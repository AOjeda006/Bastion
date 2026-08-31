using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// Que el cambio y su traza entren <b>los dos o ninguno</b>.
/// </summary>
/// <remarks>
/// <para>
/// Una auditoría escrita fuera de la transacción del cambio miente en las dos direcciones: un
/// cambio confirmado cuya traza se perdió, y una traza de un cambio que se revirtió. Las dos son
/// peores que no tener auditoría, porque nadie duda de una tabla que se llama así.
/// </para>
/// <para>
/// <b>Por qué no se puede probar por la API.</b> Hace falta un guardado que reviente <b>después</b>
/// de haberse quedado a medias, y la API no ofrece ninguno: los casos de uso comprueban antes y
/// devuelven un <c>409</c> sin llegar a guardar, que es justo lo que tienen que hacer. Así que el
/// choque se provoca aquí, con un contexto que lleva el interceptor igual que el del host.
/// </para>
/// <para>
/// <b>Y la prueba que no se puede fingir</b> es la tercera: el <c>xmin</c> de la fila y el de su
/// traza tienen que ser el mismo número. Los otros dos casos los pasa también una auditoría escrita
/// después de que el guardado fuera bien; ese, no.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTrazaVaEnLaMismaTransaccionTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Un_guardado_que_revienta_no_deja_ni_la_fila_ni_su_traza()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000040V");

        // Un código que ya está ocupado. El índice único de (empresa, código) es quien va a tirar
        // el guardado, y lo va a hacer DENTRO de `SaveChanges`, que es lo que hace falta.
        AlmacenDto ocupado = await CrearAlmacenAsync(cliente, "ATOMICO");

        var usuarioId = Guid.CreateVersion7();
        var buena = Almacen.Crear(empresa.Id, "ATOMICO-BUENA", "La que iba a entrar", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());
        var chocante = Almacen.Crear(empresa.Id, ocupado.Codigo, "La que choca", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacionAuditada(empresa.Id, usuarioId))
        {
            contexto.Almacenes.AddRange(buena, chocante);

            await Should.ThrowAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        }

        // LO QUE DE VERDAD SE PRUEBA. La primera de las dos era válida: si la traza se escribiera
        // por su cuenta —en otro contexto, en otra transacción, o después de que `SaveChanges`
        // fuera bien— aquí quedaría el rastro de un alta que nunca ocurrió. Y si se escribiera
        // «al mejor esfuerzo» después del guardado, quedaría el alta sin rastro.
        (await Trazas.DeAsync(postgres, "Almacen", buena.Id)).ShouldBeEmpty(
            "la traza de un cambio que se revirtió no puede sobrevivir al cambio");

        await using OrganizacionDbContext lectura = postgres.AbrirOrganizacion(empresa.Id);
        (await lectura.Almacenes.AnyAsync(fila => fila.Id == buena.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Y_uno_que_va_bien_deja_las_dos_cosas()
    {
        // El control positivo del anterior. Sin él, un interceptor que no escribiera NADA nunca
        // haría pasar aquel test con nota.
        (HttpClient _, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000041H");

        var usuarioId = Guid.CreateVersion7();
        var almacen = Almacen.Crear(empresa.Id, "ATOMICO-OK", "La que sí entra", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacionAuditada(empresa.Id, usuarioId))
        {
            contexto.Almacenes.Add(almacen);
            await contexto.SaveChangesAsync();
        }

        RegistroDeAuditoria traza = (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).ShouldHaveSingleItem();
        traza.Cambio.ShouldBe(TipoDeCambio.Alta);
        traza.UsuarioId.ShouldBe(usuarioId);
        traza.EmpresaId.ShouldBe(empresa.Id);
    }

    [Fact]
    public async Task La_fila_y_su_traza_las_escribe_LA_MISMA_transaccion()
    {
        // Los dos tests de arriba dejan viva una ruta: escribir la traza DESPUÉS de que
        // `SaveChanges` haya ido bien. Con esa ruta el guardado que revienta tampoco deja traza
        // —nunca se llega a escribir— y el que va bien deja las dos cosas, así que los dos pasan.
        // Lo que no pasa es esto.
        //
        // PostgreSQL numera cada transacción y guarda en cada fila la que la insertó, en la columna
        // de sistema `xmin`. Dos filas con el mismo `xmin` entraron en la misma transacción; dos
        // filas escritas por dos guardados seguidos no pueden compartirlo. Y no hace falta
        // instrumentar nada: lo cuenta la propia base.
        (HttpClient _, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000047R");

        var almacen = Almacen.Crear(empresa.Id, "ATOMICO-XMIN", "La que se compara", null, TipoDeAlmacen.Virtual, TimeProvider.System.GetUtcNow());

        await using (OrganizacionDbContext contexto =
            postgres.AbrirOrganizacionAuditada(empresa.Id, Guid.CreateVersion7()))
        {
            contexto.Almacenes.Add(almacen);
            await contexto.SaveChangesAsync();
        }

        RegistroDeAuditoria traza = (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).ShouldHaveSingleItem();

        string deLaFila = await TransaccionAsync(
            "organizacion.almacenes", $"id = '{almacen.Id}'");
        string deLaTraza = await TransaccionAsync(
            "auditoria.registros", $"id = '{traza.Id}'");

        deLaTraza.ShouldBe(
            deLaFila,
            "el cambio y su traza los ha escrito una transacción distinta cada uno: hay un momento " +
            "en el que uno de los dos está confirmado y el otro no");
    }

    private async Task<string> TransaccionAsync(string tabla, string filtro)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new($"SELECT xmin::text FROM {tabla} WHERE {filtro}", conexion);

        return (string)(await orden.ExecuteScalarAsync())!;
    }

    private static async Task<AlmacenDto> CrearAlmacenAsync(HttpClient cliente, string codigo)
    {
        HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes,
            new CrearAlmacenDto
            {
                Codigo = codigo,
                Nombre = $"Almacén {codigo}",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
    }
}
