using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// El criterio del ítem, palabra por palabra: <b>un cambio en un maestro deja su rastro</b>.
/// </summary>
/// <remarks>
/// Va por la API de verdad, con sesión de verdad, y se comprueba mirando la tabla. Que el
/// interceptor esté registrado no se lee en ningún sitio: se nota aquí o no se nota en ninguna
/// parte, porque un módulo sin traza funciona igual de bien y pasa igual de bien sus tests.
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnCambioEnUnMaestroDejaSuRastroTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";
    private const string Roles = "/api/v1/identidad/roles";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task El_alta_de_un_almacen_deja_una_fila_con_quien_donde_y_que()
    {
        (HttpClient cliente, EmpresaDto empresa, SesionDto sesion) = await EnEmpresaAsync("00000034B");

        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "RASTRO-ALTA");

        IReadOnlyList<RegistroDeAuditoria> trazas = await Trazas.DeAsync(postgres, "Almacen", almacen.Id);

        RegistroDeAuditoria alta = trazas.ShouldHaveSingleItem();
        alta.Cambio.ShouldBe(TipoDeCambio.Alta);
        alta.EmpresaId.ShouldBe(empresa.Id);
        alta.SinInquilino.ShouldBeNull();
        alta.UsuarioId.ShouldBe(sesion.UsuarioId);

        // Un alta no lleva `antes`: el hueco ES la información, y rellenarlo con un nulo lo
        // confundiría con «cambió a nulo».
        Trazas.Valor(alta, "Codigo", "despues").ShouldBe("RASTRO-ALTA");
        Trazas.Valor(alta, "Codigo", "antes").ShouldBeNull();
        Trazas.Valor(alta, "Nombre", "despues").ShouldBe("Almacén RASTRO-ALTA");

        // El enumerado va como TEXTO, igual que en la columna de al lado: quien lea la traza
        // espera lo mismo que vería en la tabla, no el número que tiene el enumerado hoy.
        Trazas.Valor(alta, "Tipo", "despues").ShouldBe("Fisico");
    }

    [Fact]
    public async Task La_direccion_de_un_almacen_viaja_DENTRO_de_la_traza_de_su_dueno()
    {
        (HttpClient cliente, EmpresaDto _, SesionDto _) = await EnEmpresaAsync("00000035N");

        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "RASTRO-DIR");

        RegistroDeAuditoria alta = (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).ShouldHaveSingleItem();

        // Una entidad de propiedad es una entrada APARTE en el rastreador de cambios de EF Core:
        // sin plegarla en su dueño, cambiar solo la calle dejaría una traza de un «Direccion» sin
        // identidad propia y ninguna de «Almacen», que es de lo que se está hablando.
        Trazas.Valor(alta, "Direccion.Calle", "despues").ShouldBe("Gran Vía");
        Trazas.Valor(alta, "Direccion.CodigoPostal", "despues").ShouldBe("28013");
    }

    [Fact]
    public async Task Una_modificacion_deja_el_antes_y_el_despues_de_lo_que_cambio_y_solo_de_eso()
    {
        (HttpClient cliente, EmpresaDto _, SesionDto _) = await EnEmpresaAsync("00000036J");

        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "RASTRO-MOD");

        HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Almacenes}/{almacen.Id}",
            new ModificarAlmacenDto
            {
                Nombre = "Con otro nombre",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));

        IReadOnlyList<RegistroDeAuditoria> trazas = await Trazas.DeAsync(postgres, "Almacen", almacen.Id);
        trazas.Count.ShouldBe(2);

        RegistroDeAuditoria modificacion = trazas[1];
        modificacion.Cambio.ShouldBe(TipoDeCambio.Modificacion);
        Trazas.Valor(modificacion, "Nombre", "antes").ShouldBe("Almacén RASTRO-MOD");
        Trazas.Valor(modificacion, "Nombre", "despues").ShouldBe("Con otro nombre");

        // Y SOLO de eso. Repetir en cada modificación el valor de las diez columnas que no se han
        // tocado convierte «qué cambió» en un ejercicio de comparar dos listas, en una tabla que
        // por diseño no se puede limpiar.
        Trazas.Propiedades(modificacion).ShouldBe(["Nombre"]);
    }

    [Fact]
    public async Task Una_peticion_que_no_cambia_nada_no_deja_traza()
    {
        (HttpClient cliente, EmpresaDto _, SesionDto _) = await EnEmpresaAsync("00000037Z");

        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "RASTRO-IGUAL");

        // El mismo cuerpo con el que se creó: la fila queda como estaba.
        HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Almacenes}/{almacen.Id}",
            new ModificarAlmacenDto
            {
                Nombre = "Almacén RASTRO-IGUAL",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));

        // Una fila de traza vacía no es información, es ruido — y el ruido en una tabla de solo
        // añadido se queda para siempre.
        (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task La_traza_de_una_entidad_global_lleva_la_empresa_DESDE_LA_QUE_se_actuo()
    {
        (HttpClient cliente, EmpresaDto empresa, SesionDto _) = await EnEmpresaAsync("00000038S");

        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];

        HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Roles,
            new CrearRolDto
            {
                Codigo = $"rastro-{sufijo}",
                Nombre = "Rol con rastro",
                Permisos = ["organizacion.almacen.ver"],
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        RolDto rol = (await alta.Content.ReadFromJsonAsync<RolDto>())!;

        RegistroDeAuditoria traza = (await Trazas.DeAsync(postgres, "Rol", rol.Id)).ShouldHaveSingleItem();

        // Un rol es global (ADR-0011): no es «de» ninguna empresa. Su traza sí lo es, y de la que
        // estaba activa cuando se creó. La consecuencia se asume y se escribe: un mismo rol
        // acumula trazas de varias empresas, y cada una solo ve las suyas.
        traza.EmpresaId.ShouldBe(empresa.Id);
        traza.SinInquilino.ShouldBeNull();
    }

    [Fact]
    public async Task Lo_que_se_escribe_sin_empresa_lleva_el_motivo_y_no_un_hueco()
    {
        // La semilla de arranque corre antes de que exista nadie: no hay petición, no hay token y
        // por tanto no hay empresa. Eso no la deja sin explicación — lleva su motivo en su propia
        // columna. `Guid.Empty` habría rellenado el hueco y lo habría escondido.
        IReadOnlyList<RegistroDeAuditoria> todas = await Trazas.TodasAsync(postgres);

        IReadOnlyList<RegistroDeAuditoria> sinEmpresa =
            [.. todas.Where(fila => fila.EmpresaId is null)];

        sinEmpresa.ShouldNotBeEmpty("la semilla de arranque escribe sin empresa: tiene que haber trazas así");
        sinEmpresa.ShouldAllBe(fila => fila.SinInquilino != null);

        todas.ShouldAllBe(fila => fila.EmpresaId != Guid.Empty);
        todas.ShouldContain(fila => fila.SinInquilino == MotivoSinInquilino.SemillaDeArranque);
    }

    [Fact]
    public async Task Todas_las_filas_de_un_mismo_guardado_comparten_correlacion()
    {
        (HttpClient cliente, EmpresaDto _, SesionDto _) = await EnEmpresaAsync("00000039Q");

        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "RASTRO-CORR");

        RegistroDeAuditoria alta = (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).ShouldHaveSingleItem();

        // Hoy un alta de almacén es una fila sola, así que lo que se comprueba es que el
        // identificador existe y no es el de nadie más. Es lo que convertirá seis filas en «un
        // cambio» el día que un caso de uso toque seis entidades a la vez.
        alta.CorrelacionId.ShouldNotBe(Guid.Empty);
        alta.CorrelacionId.ShouldNotBe(alta.Id);
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa, SesionDto Sesion)> EnEmpresaAsync(string nif)
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(nif);

        // La sesión que vale es la de DESPUÉS de entrar en la empresa nueva: el `UsuarioId` es el
        // mismo, pero pedirla de nuevo deja claro contra qué token se está llamando.
        SesionDto sesion = await Sesiones.AbrirAsync(
            cliente,
            ApiDeVerdad.CorreoDelAdministrador,
            ApiDeVerdad.ContrasenaDelAdministrador);

        return (cliente, empresa, sesion);
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
