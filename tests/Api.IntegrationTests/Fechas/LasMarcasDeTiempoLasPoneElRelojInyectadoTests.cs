using System.Data;
using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Fechas;

/// <summary>
/// Las dos marcas de R14 salen de donde dicen que salen: <c>CreadoEn</c> del dominio, en la
/// fábrica, y <c>ModificadoEn</c> del interceptor, leyendo el <c>TimeProvider</c> inyectado.
/// Ninguna de las dos la pone la base de datos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se mira desde dos sitios porque hay dos cosas distintas que comprobar.</b> Que el interceptor
/// <i>está puesto</i> en el host solo se ve pasando por la API entera: es una línea de
/// <c>ModuloDeOrganizacion</c> que, si se borra, no rompe nada visible —<c>modificado_en</c> se
/// queda con la fecha del alta para siempre— y ningún test de negocio se entera. Y que la hora
/// <i>sale del reloj inyectado</i> no se ve desde ahí, porque el reloj real y el <c>now()</c> de
/// PostgreSQL marcan lo mismo con unos milisegundos de diferencia: por eso los dos últimos casos
/// van por la puerta de atrás, con un reloj parado en un instante que la base no puede producir.
/// </para>
/// <para>
/// <b>Y se lee con SQL en crudo</b> por lo mismo que en <c>LaFilaBloqueadaSigueEnLaBase</c>: lo que
/// se comprueba es el valor que quedó en la columna, y leerlo con el mismo EF Core que lo escribió
/// dejaría fuera de la prueba justo el tramo que interesa.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasMarcasDeTiempoLasPoneElRelojInyectadoTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";

    /// <summary>Un instante que <c>now()</c> no puede devolver: ya pasó.</summary>
    /// <remarks>
    /// Ahí está toda la fuerza de los dos casos de la puerta de atrás. Con un reloj parado en
    /// «ahora», un <c>DEFAULT now()</c> y el interceptor escribirían valores indistinguibles y el
    /// test daría verde con cualquiera de los dos mecanismos. Con 2019, solo uno de los dos puede
    /// haber escrito lo que hay en la columna.
    /// </remarks>
    private static readonly DateTimeOffset s_relojParadoEn =
        new(2019, 3, 4, 11, 22, 33, TimeSpan.Zero);

    /// <summary>El instante que recibe la fábrica, distinto del anterior a propósito.</summary>
    private static readonly DateTimeOffset s_instanteDelAlta =
        new(2020, 6, 7, 8, 9, 10, TimeSpan.Zero);

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Un_cambio_por_la_API_mueve_una_marca_y_deja_la_otra_donde_estaba()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000078D");
        using HttpClient suyo = cliente;

        // Recién nacida, las dos marcas son el MISMO instante. No es un detalle de presentación:
        // es lo que permite leer «nunca se ha tocado» comparándolas, y por eso `ModificadoEn` no
        // es anulable.
        (DateTimeOffset Creado, DateTimeOffset Modificado) alta =
            await MarcasAsync("empresas", empresa.Id);

        alta.Modificado.ShouldBe(alta.Creado);

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Empresas}/{empresa.Id}",
            new ModificarEmpresaDto
            {
                RazonSocial = "La misma empresa con otro nombre",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "EUR",
                RegimenDeIva = "General",
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));

        // LO QUE DE VERDAD SE PRUEBA. `ModificarEmpresa` no toca `ModificadoEn` —ni ningún caso de
        // uso lo hace, ni tiene por dónde: el `set` es privado—, así que si esta marca se ha
        // movido es porque el interceptor está enganchado al `DbContext` del host. Borrar la línea
        // de `ModuloDeOrganizacion` que lo engancha deja este caso rojo y todo lo demás verde.
        (DateTimeOffset Creado, DateTimeOffset Modificado) despues =
            await MarcasAsync("empresas", empresa.Id);

        despues.Modificado.ShouldBeGreaterThan(alta.Modificado);

        // Y la de creación no se mueve nunca. Un interceptor que escribiera las dos convertiría
        // `creado_en` en «la última vez que se guardó», que es la columna que ya está al lado.
        despues.Creado.ShouldBe(alta.Creado);
    }

    [Fact]
    public async Task La_hora_del_cambio_sale_del_reloj_inyectado_y_no_del_de_la_base()
    {
        (HttpClient _, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000079X");

        var almacen = Almacen.Crear(
            empresa.Id, "RELOJ-CAMBIO", "El que se toca con el reloj parado", null,
            TipoDeAlmacen.Virtual, s_instanteDelAlta);

        await using (OrganizacionDbContext alta = postgres.AbrirOrganizacionConMarcasDeTiempo(
            empresa.Id, new RelojParado(s_relojParadoEn)))
        {
            alta.Almacenes.Add(almacen);
            await alta.SaveChangesAsync();
        }

        // El cambio va en OTRO contexto, porque así ocurre de verdad: la petición que modifica algo
        // no es la que lo creó, y lo que se modifica viene de la base y no de la memoria.
        await using (OrganizacionDbContext cambio = postgres.AbrirOrganizacionConMarcasDeTiempo(
            empresa.Id, new RelojParado(s_relojParadoEn)))
        {
            Almacen suyo = await cambio.Almacenes.SingleAsync(fila => fila.Id == almacen.Id);
            suyo.Modificar("Otro nombre", null, TipoDeAlmacen.Virtual);
            await cambio.SaveChangesAsync();
        }

        (DateTimeOffset Creado, DateTimeOffset Modificado) marcas =
            await MarcasAsync("almacenes", almacen.Id);

        // Marzo de 2019, al microsegundo. Un `DEFAULT now()` o un disparador habrían escrito el
        // instante en el que corrió el test; el reloj del sistema, también. Solo el reloj que se
        // inyectó pudo poner esto.
        marcas.Modificado.ShouldBe(s_relojParadoEn);
    }

    [Fact]
    public async Task El_alta_no_pasa_por_el_interceptor_y_por_eso_lleva_la_hora_del_dominio()
    {
        (HttpClient _, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000080B");

        var almacen = Almacen.Crear(
            empresa.Id, "RELOJ-ALTA", "El que nace con su propia hora", null,
            TipoDeAlmacen.Virtual, s_instanteDelAlta);

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacionConMarcasDeTiempo(
            empresa.Id, new RelojParado(s_relojParadoEn));

        contexto.Almacenes.Add(almacen);
        await contexto.SaveChangesAsync();

        (DateTimeOffset Creado, DateTimeOffset Modificado) marcas =
            await MarcasAsync("almacenes", almacen.Id);

        // Las dos son las del dominio, y el reloj del interceptor —parado en un año DISTINTO— no
        // aparece por ningún lado. Si el interceptor marcara también las altas, `modificado_en`
        // sería 2019 y una ficha recién creada diría que se modificó un año antes de nacer.
        marcas.Creado.ShouldBe(s_instanteDelAlta);
        marcas.Modificado.ShouldBe(s_instanteDelAlta);
    }

    private async Task<(DateTimeOffset Creado, DateTimeOffset Modificado)> MarcasAsync(
        string tabla, Guid id)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"SELECT creado_en, modificado_en FROM {OrganizacionDbContext.Esquema}.{tabla} " +
            $"WHERE id = '{id}'",
            conexion);

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync(CommandBehavior.Default);

        (await lector.ReadAsync()).ShouldBeTrue($"la fila {id} no está en {tabla}");

        return (lector.GetFieldValue<DateTimeOffset>(0), lector.GetFieldValue<DateTimeOffset>(1));
    }
}

/// <summary>
/// Un <c>TimeProvider</c> que siempre contesta lo mismo.
/// </summary>
/// <remarks>
/// Son tres líneas y no un paquete. <c>Microsoft.Extensions.TimeProvider.Testing</c> trae un
/// <c>FakeTimeProvider</c> que hace esto y bastante más —avanzarlo, disparar temporizadores—, y
/// nada de eso hace falta aquí: lo único que se necesita es que <c>GetUtcNow()</c> devuelva un
/// instante elegido. Una dependencia nueva se justifica por lo que ahorra, y aquí no ahorra nada.
/// </remarks>
/// <param name="instante">Lo que contesta, siempre.</param>
internal sealed class RelojParado(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante;
}
