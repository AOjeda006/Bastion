using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Contracts;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Importacion;

/// <summary>
/// La importación de terceros hace lo que dice el ADR-0034, comprobado por lo que queda en la base y por
/// lo que devuelve la API al leerlo, no por lo que dice el informe de sí mismo.
/// </summary>
/// <remarks>
/// <para>
/// <b>La premisa, contra Excel.</b> El primer caso importa los dos ficheros que escribió Excel en
/// español —«CSV (delimitado por comas)» y «CSV UTF-8»— tal como salieron, y lee cada valor de vuelta
/// por la API. Un lector que solo supiera leer los ficheros de las pruebas pasaría todos los demás casos
/// y fallaría con el primer fichero de un usuario.
/// </para>
/// <para>
/// <b>Cada caso afirma por el efecto.</b> «La fila mala no aborta el fichero» se ve contando las buenas en
/// la tabla; «la mala no entra a medias», buscándola en la tabla; «los importes se leen en español»,
/// leyendo el límite que quedó guardado. El informe se afirma entero y además, pero nunca en lugar de eso.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaImportacionDeTercerosTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";
    private const int SucesoDeLaImportacionConOcupados = 8401;

    // No cabe en ningún identificador aleatorio: letras fuera del alfabeto hexadecimal y una «ñ».
    private const string Canario = "CANARIO-ÑANDÚ-QZX";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Theory]
    [InlineData(Importaciones.ExcelDelimitadoPorComas, 234, 1)]
    [InlineData(Importaciones.ExcelUtf8, 235, 4)]
    public async Task Los_dos_CSV_que_escribe_Excel_en_espanol_entran_enteros_y_con_cada_valor_en_su_sitio(
        string fichero, int empresa, int primerNif)
    {
        (HttpClient cliente, _) = await EnUnaEmpresaNuevaAsync(empresa);

        using HttpResponseMessage respuesta =
            await Importaciones.ImportarAsync(cliente, Importaciones.DeExcel(fichero, primerNif));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);

        // La fila en blanco que Excel escribe como diecisiete separadores no es una fila leída.
        informe.ShouldBe(informe with { Leidas = 3, Importadas = 3, Rechazadas = 0 });
        informe.Rechazos.ShouldBeEmpty();

        TerceroDto ferreteria = await LeerAsync(cliente, Importaciones.Nif(primerNif));
        ferreteria.RazonSocial.ShouldBe("Ferretería Peña, S.L.");
        ferreteria.NombreComercial.ShouldBe("Peña");
        ferreteria.DomicilioFiscal.Calle.ShouldBe("Calle de la Constitución");
        ferreteria.DomicilioFiscal.CodigoPostal.ShouldBe("08001", "el cero de delante es parte del código postal");
        (ferreteria.EsCliente, ferreteria.EsProveedor).ShouldBe((true, false));
        (await LimiteAsync(cliente, ferreteria.Id)).ShouldBe((1234.56m, "EUR"), "Excel escribe «1.234,56»");

        TerceroDto distribuciones = await LeerAsync(cliente, Importaciones.Nif(primerNif + 1));
        distribuciones.RazonSocial.ShouldBe("Distribuciones Norte; Sur, S.A.");
        distribuciones.NombreComercial.ShouldBe("Almacén\ncentral", "el salto de línea de dentro de la celda");
        distribuciones.DomicilioFiscal.Numero.ShouldBeNull();
        (distribuciones.EsCliente, distribuciones.EsProveedor).ShouldBe((true, true), "Excel escribe «VERDADERO»");
        distribuciones.RegimenFiscal.Territorio.ShouldBe("Canarias");
        distribuciones.RegimenFiscal.CriterioDeCaja.ShouldBeTrue();
        (await LimiteAsync(cliente, distribuciones.Id)).ShouldBe((2500m, "EUR"));

        TerceroDto bar = await LeerAsync(cliente, Importaciones.Nif(primerNif + 2));
        bar.RazonSocial.ShouldBe("Bar \"El Rincón\"", "las comillas dobladas de Excel");
        bar.NombreComercial.ShouldBeNull();
        (bar.EsCliente, bar.EsProveedor).ShouldBe((false, true));
        bar.RegimenFiscal.Territorio.ShouldBe("PeninsulaYBaleares");
        (await LimiteAsync(cliente, bar.Id)).ShouldBe((null, null));
    }

    /// <summary>Las malas no impiden que entren las buenas, y el informe dice línea, columna y motivo.</summary>
    /// <remarks>
    /// Las líneas son las de la hoja de cálculo: la cabecera es la 1, una fila vacía cuenta y un salto de
    /// línea dentro de una celda no. Es lo que permite ir a la fila del informe en Excel sin contar a mano.
    /// </remarks>
    [Fact]
    public async Task Las_filas_malas_no_impiden_que_entren_las_buenas_y_el_informe_dice_su_linea_de_Excel()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(236);

        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(Importaciones.Nif(101)),
            Importaciones.Fila(Escenario.NifConElControlCambiado(Importaciones.BaseDeNifs + 102), razonSocial: Canario),
            Importaciones.Fila(Importaciones.Nif(103), nombreComercial: "\"Almacén\ncentral\""),
            string.Empty,
            Importaciones.Fila(Importaciones.Nif(104), esCliente: "quizá " + Canario),
            Importaciones.Fila(Importaciones.Nif(105), razonSocial: string.Empty, esCliente: "no", esProveedor: "no"),
            Importaciones.Fila(Importaciones.Nif(106), limite: "1.234,5", divisa: "EUR"),
            Importaciones.Fila(Importaciones.Nif(101), razonSocial: "La misma, otra vez"));

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        // Por el efecto, primero: las tres buenas están en la tabla y las tres malas no.
        (await Importaciones.CuantosAsync(
            postgres, empresa.Id, Importaciones.Nif(101), Importaciones.Nif(103), Importaciones.Nif(106)))
            .ShouldBe(3, "una fila mala ha impedido que entraran las buenas");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(104), Importaciones.Nif(105)))
            .ShouldBe(0);

        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);
        informe.ShouldBe(informe with { Leidas = 7, Importadas = 3, Rechazadas = 4 });

        Serializado(informe.Rechazos).ShouldBe(Serializado(
        [
            new RechazoDto(ImportacionDeTerceros.IdentificacionNumero, MotivoDeRechazo.NoValido, [3]),
            new RechazoDto(ImportacionDeTerceros.IdentificacionNumero, MotivoDeRechazo.RepetidaEnElFichero, [9]),
            new RechazoDto(ImportacionDeTerceros.RazonSocial, MotivoDeRechazo.Obligatorio, [7]),
            new RechazoDto(ImportacionDeTerceros.EsCliente, MotivoDeRechazo.FormatoNoValido, [6]),
            new RechazoDto(ImportacionDeTerceros.EsCliente, MotivoDeRechazo.NiClienteNiProveedor, [7]),
        ]));

        // Y los motivos viajan con el nombre del ADR, que es lo que el frontal traduce.
        cuerpo.ShouldContain("\"motivo\":\"repetida-en-el-fichero\"");
        cuerpo.ShouldNotContain(Canario, Case.Insensitive, "el informe repite un dato del fichero");
    }

    /// <summary>Una fila con el límite mal escrito no entra sin límite: no entra.</summary>
    [Fact]
    public async Task Una_fila_con_el_limite_mal_escrito_no_deja_el_tercero_dado_de_alta_sin_limite()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(237);

        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(Importaciones.Nif(111), limite: "1.5", divisa: "EUR"),
            Importaciones.Fila(Importaciones.Nif(112), limite: "300", divisa: string.Empty),
            Importaciones.Fila(Importaciones.Nif(113), limite: "300", divisa: "EUR"));

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(111), Importaciones.Nif(112)))
            .ShouldBe(0, "una fila con el límite mal escrito ha entrado a medias");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(113))).ShouldBe(1);

        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);
        informe.ShouldBe(informe with { Leidas = 3, Importadas = 1, Rechazadas = 2 });
        Serializado(informe.Rechazos).ShouldBe(Serializado(
        [
            new RechazoDto(ImportacionDeTerceros.LimiteCredito, MotivoDeRechazo.FormatoNoValido, [2]),
            new RechazoDto(ImportacionDeTerceros.LimiteCreditoDivisa, MotivoDeRechazo.NoValido, [3]),
        ]));
    }

    /// <summary>
    /// El importe es el que se escribe en español, y se ve en el límite que queda guardado, no en si la
    /// fila entra.
    /// </summary>
    /// <remarks>
    /// <b>Los dos valores están elegidos para que la cultura equivocada NO los rechace.</b> «1.234» y
    /// «1,234» se leen en inglés sin ningún error, como 1,234 y 1 234: un lector con la cultura cambiada
    /// importaría las dos filas y el informe saldría limpio. Lo único que lo delata es el valor guardado,
    /// así que es lo primero que se afirma.
    /// </remarks>
    [Fact]
    public async Task Los_importes_se_leen_con_la_coma_decimal_y_se_ve_en_el_limite_guardado()
    {
        (HttpClient cliente, _) = await EnUnaEmpresaNuevaAsync(238);

        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(Importaciones.Nif(121), limite: "1.234", divisa: "EUR"),
            Importaciones.Fila(Importaciones.Nif(122), limite: "1,234", divisa: "EUR"));

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        TerceroDto conMiles = await LeerAsync(cliente, Importaciones.Nif(121));
        TerceroDto conDecimales = await LeerAsync(cliente, Importaciones.Nif(122));

        (await LimiteAsync(cliente, conMiles.Id)).ShouldBe((1234m, "EUR"), "«1.234» es mil doscientos treinta y cuatro");
        (await LimiteAsync(cliente, conDecimales.Id)).ShouldBe((1.234m, "EUR"), "«1,234» es uno con doscientos treinta y cuatro");

        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);
        informe.ShouldBe(informe with { Leidas = 2, Importadas = 2, Rechazadas = 0 });
    }

    /// <summary>
    /// Lo que ya existe, activo o bloqueado, sale en el MISMO grupo del informe; la traza dice cuántos de
    /// ellos estaban bloqueados y no cuáles.
    /// </summary>
    /// <remarks>
    /// Es el caso del alta suelta (<c>ElConflictoQueNoRevelaTests</c>) con N filas. Si el informe separara
    /// las dos, la importación sería la manera de sacar en una sola petición la lista de quién está dado de
    /// baja: basta con subir un fichero con los identificadores que se quieran comprobar.
    /// </remarks>
    [Fact]
    public async Task Lo_que_ya_existe_activo_o_bloqueado_sale_en_el_mismo_grupo_y_la_traza_no_dice_cuales()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(239);

        TerceroDto activo = await CrearAsync(cliente, Importaciones.Nif(131));
        TerceroDto bloqueado = await CrearAsync(cliente, Importaciones.Nif(132));

        using (HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{bloqueado.Id}"))
        {
            bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));
        }

        RegistroDeSucesos.Olvidar();

        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(activo.Identificacion.Numero),
            Importaciones.Fila(bloqueado.Identificacion.Numero),
            Importaciones.Fila(Importaciones.Nif(133)));

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(133))).ShouldBe(1);

        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);
        informe.ShouldBe(informe with { Leidas = 3, Importadas = 1, Rechazadas = 2 });
        Serializado(informe.Rechazos).ShouldBe(
            Serializado([new RechazoDto(ImportacionDeTerceros.IdentificacionNumero, MotivoDeRechazo.YaExiste, [2, 3])]),
            "el activo y el bloqueado tienen que salir en el mismo grupo, sin nada que los distinga");

        IReadOnlyList<RegistroDeSucesos.Suceso> anotados = RegistroDeSucesos.Con(SucesoDeLaImportacionConOcupados);
        anotados.Count.ShouldBe(1, "la importación que miró lo bloqueado tiene que dejarlo escrito, una vez");
        anotados[0].Mensaje.ShouldEndWith("Ocupados: 2. De ellos, en fichas bloqueadas: 1.");
        anotados[0].Mensaje.ShouldNotContain(activo.Identificacion.Numero);
        anotados[0].Mensaje.ShouldNotContain(bloqueado.Identificacion.Numero);
    }

    /// <summary>Sin el permiso de importar no se entra, aunque se pueda dar de alta de uno en uno.</summary>
    [Fact]
    public async Task Sin_el_permiso_de_importar_no_se_entra_aunque_se_puedan_dar_altas_de_una_en_una()
    {
        using HttpClient cliente = await _api.ConPermisosAsync(
            PermisosDeTerceros.TerceroCrear, PermisosDeTerceros.LimiteCreditoFijar);
        Guid empresaId = await EmpresaDeLaSemillaAsync();

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(
            cliente, Importaciones.Fichero(Importaciones.Fila(Importaciones.Nif(141))));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await Escenario.Detalle(respuesta));
        (await Importaciones.CuantosAsync(postgres, empresaId, Importaciones.Nif(141))).ShouldBe(0);
    }

    /// <summary>Importar es dar de alta: sin ese permiso se rechaza el fichero, aunque se pueda importar.</summary>
    [Fact]
    public async Task Con_el_permiso_de_importar_y_sin_el_de_dar_de_alta_se_rechaza_el_fichero_entero()
    {
        using HttpClient cliente = await _api.ConPermisosAsync(PermisosDeTerceros.TerceroImportar);
        Guid empresaId = await EmpresaDeLaSemillaAsync();

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(
            cliente, Importaciones.Fichero(Importaciones.Fila(Importaciones.Nif(151))));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await Escenario.Detalle(respuesta));
        (await TipoAsync(respuesta)).ShouldBe("/errors/importacion-sin-permiso-de-alta");
        (await Importaciones.CuantosAsync(postgres, empresaId, Importaciones.Nif(151))).ShouldBe(0);
    }

    /// <summary>
    /// Sin el permiso del límite, un fichero que trae límite se rechaza entero; el mismo sin límite, entra.
    /// </summary>
    /// <remarks>
    /// La segunda mitad es la que impide el falso verde: un servidor que contestara <c>403</c> a esta
    /// sesión por cualquier otro motivo pasaría la primera sin haber mirado el límite.
    /// </remarks>
    [Fact]
    public async Task Sin_el_permiso_del_limite_un_fichero_con_limite_se_rechaza_entero_y_sin_limite_entra()
    {
        using HttpClient cliente = await _api.ConPermisosAsync(
            PermisosDeTerceros.TerceroImportar, PermisosDeTerceros.TerceroCrear);
        Guid empresaId = await EmpresaDeLaSemillaAsync();

        using HttpResponseMessage conLimite = await Importaciones.ImportarAsync(
            cliente,
            Importaciones.Fichero(
                Importaciones.Fila(Importaciones.Nif(161)),
                Importaciones.Fila(Importaciones.Nif(162), limite: "500", divisa: "EUR")));

        conLimite.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await Escenario.Detalle(conLimite));
        (await TipoAsync(conLimite)).ShouldBe("/errors/importacion-sin-permiso-de-limite");
        (await Importaciones.CuantosAsync(postgres, empresaId, Importaciones.Nif(161), Importaciones.Nif(162)))
            .ShouldBe(0, "sin el permiso del límite no entra ninguna fila, tampoco la que no lo trae");

        using HttpResponseMessage sinLimite = await Importaciones.ImportarAsync(
            cliente, Importaciones.Fichero(Importaciones.Fila(Importaciones.Nif(161))));

        sinLimite.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(sinLimite));
        (await Importaciones.CuantosAsync(postgres, empresaId, Importaciones.Nif(161))).ShouldBe(1);
    }

    private static string Serializado(IReadOnlyList<RechazoDto> rechazos) => JsonSerializer.Serialize(rechazos, s_json);

    private static async Task<string?> TipoAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("type").GetString();

    private static async Task<TerceroDto> LeerAsync(HttpClient cliente, string nif)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            $"{Terceros}/buscar", new BuscarTercerosDto { Pais = "ES", Numero = nif });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
        TramoDe<TerceroDto> tramo = (await respuesta.Content.ReadFromJsonAsync<TramoDe<TerceroDto>>())!;

        return tramo.Elementos.ShouldHaveSingleItem($"el tercero {nif} no está, o está dos veces");
    }

    private static async Task<(decimal? Cantidad, string? Divisa)> LimiteAsync(HttpClient cliente, Guid terceroId)
    {
        LimiteCreditoDto limite =
            (await cliente.GetFromJsonAsync<LimiteCreditoDto>($"{Terceros}/{terceroId}/limite-credito"))!;

        return (limite.Cantidad, limite.Divisa);
    }

    private static async Task<TerceroDto> CrearAsync(HttpClient cliente, string nif)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, new CrearTerceroDto
        {
            Identificacion = new IdentificacionDeAltaDto { Pais = "ES", Numero = nif },
            RazonSocial = "Dado de alta a mano",
            DomicilioFiscal = Escenario.Domicilio(),
            EsCliente = true,
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int orden)
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(orden));
        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    private async Task<Guid> EmpresaDeLaSemillaAsync()
    {
        (HttpClient administrador, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        _clientes.Add(administrador);

        return sesion.EmpresaActivaId;
    }
}
