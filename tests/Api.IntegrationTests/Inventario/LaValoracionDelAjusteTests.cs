using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La valoración del ajuste contra la base de verdad (ADR-0046): la divisa del documento, el coste
/// que admite cada línea y lo que el precio medio deja en el libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>La divisa sale de la empresa, y el caso lo ve con una empresa en dólares.</b> Con una en
/// euros, un documento que escribiera <c>EUR</c> a fuego saldría verde, y la pregunta al puerto no
/// estaría comprobada por nadie.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 500 al 519.</b> Las empresas, del 500 al 509; los maestros
/// de instalación, del 510 al 519. Este carril comparte la base entre todos sus ficheros, así que
/// una semilla repetida no falla aquí: falla en el fichero de otro que la pedía primero.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaValoracionDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// Una salida con coste y un coste negativo se rechazan con el mismo <c>400</c>, y no queda
    /// ningún borrador.
    /// </summary>
    /// <remarks>
    /// <b>Los maestros son inventados, y eso también se afirma.</b> El coste se mira antes que el
    /// almacén, la serie y las líneas, porque es un cuerpo mal escrito y no hace falta preguntar a
    /// nadie para saberlo. Si se mirara después, el código sería el del almacén que no existe.
    /// </remarks>
    [Fact]
    public async Task Una_salida_con_coste_o_un_coste_negativo_no_abren_el_borrador()
    {
        (_, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(Escenario.NuevaEmpresa(Escenario.NifInventado(500)));

        await using (ElModuloDeInventario modulo = new(postgres, empresa.Id))
        {
            foreach ((decimal cantidad, decimal coste) in new[] { (-2m, 1.50m), (2m, -1.50m) })
            {
                Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
                    new AbrirAjusteDto(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        "Ajuste que no debería pasar del coste",
                        [new LineaDeAjusteDto(
                            Guid.CreateVersion7(), Guid.CreateVersion7(), cantidad, Guid.CreateVersion7(), 1m, coste)]),
                    CancellationToken.None);

                alta.Error.ShouldNotBeNull($"{cantidad} a {coste} no tendría que abrir un borrador");
                alta.Error.Codigo.ShouldBe("ajuste-coste-no-valido");
                alta.Error.Tipo.ShouldBe(TipoDeError.Validacion);
            }
        }

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresa.Id);

        (await contexto.Ajustes.CountAsync()).ShouldBe(0, "un alta rechazada no deja un borrador a medias");
    }

    /// <summary>El borrador toma la divisa base de la empresa, que aquí es el dólar.</summary>
    [Fact]
    public async Task El_borrador_toma_la_divisa_base_de_la_empresa()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(
            Escenario.NuevaEmpresa(Escenario.NifInventado(501)) with { DivisaBase = "USD" });

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "V28-A");
        UbicacionDto ubicacion = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, "V28-A-01");
        (Guid articuloId, Guid unidadId) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 510);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, "V28");

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serie.Id,
                almacen.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Recuento del ítem 2.8",
                [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m)]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");
        alta.Valor.Divisa.ShouldBe("USD", "la divisa del documento es la base de la empresa, no el euro");
    }

    /// <summary>Da de alta una empresa con ese cuerpo y deja un cliente operando dentro de ella.</summary>
    /// <param name="alta">El cuerpo del alta.</param>
    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(CrearEmpresaDto alta)
    {
        (HttpClient cliente, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        _clientes.Add(cliente);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(RutaDeEmpresas, alta);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);
        EmpresaDto empresa = (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>())!;

        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, empresa.Id);

        return (cliente, empresa);
    }
}
