using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las escrituras de las líneas del recuento cuando llegan a la vez (ADR-0055 §4 y §13): dos personas
/// que cuentan líneas distintas, una clave o una serie que llega sin el cerrojo, y otra empresa que
/// no espera a nadie.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos conteos de líneas distintas se ponen en fila en la cabecera, y no se pisan.</b> Toda
/// escritura en una línea toca la versión de la cabecera, así que sin el cerrojo la segunda
/// actualizaría una cabecera que la primera ya cambió, y saldría <c>412</c> por algo que no ha visto
/// ni le importa. Con el cerrojo, la segunda lee la cabecera ya publicada y sale <c>200</c>.
/// </para>
/// <para>
/// <b>En esa carrera la primera ya ha escrito, y se admite</b> con las dos condiciones de la regla de
/// las carreras: el código distingue la causa —<c>200</c> frente al <c>412</c> de la versión
/// obsoleta— y la mutación que quita el cerrojo, sola, pone el caso rojo, porque sin él la segunda
/// espera igual, pero en la fila de la cabecera, y al soltarla su versión ya no casa.
/// </para>
/// <para>
/// <b>Los índices de clave y de serie son la red</b> de lo que llegara sin cerrojo. Para verlos solos,
/// la línea que gana se escribe por debajo del ORM, en una transacción sin publicar que no pide el
/// cerrojo de la cabecera: el alta por la API pasa su comprobación previa, se para en el índice, y
/// cuando la otra publica sale con el mismo <c>409</c> que daría la comprobación.
/// </para>
/// <para>
/// <b>Semillas: del 788 al 792</b>, empresas y maestros con el mismo número; el 792 es solo una
/// empresa, la ajena. El reparto del bloque del 2.12 está en la cabecera de
/// <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasCarrerasDeLasLineasDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly TimeSpan s_plazo = TimeSpan.FromSeconds(30);

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
    /// Dos personas cuentan líneas distintas a la vez: la segunda espera a la primera en la cabecera,
    /// y cuando la primera publica, cuenta sin un <c>412</c>.
    /// </summary>
    [Fact]
    public async Task Dos_conteos_de_lineas_distintas_a_la_vez_se_esperan_y_ninguno_es_un_412()
    {
        EscenaDeRecuento escena = await MontarAsync(788, "RCC-C", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 2m, lote: "L-1");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-2");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        IReadOnlyList<LineaDeRecuentoDto> lineas = await escena.TodasLasLineasAsync(abierto.Id);

        lineas.Count.ShouldBe(2);
        LineaDeRecuentoDto primera = lineas[0];
        LineaDeRecuentoDto segunda = lineas[1];

        string dePrimera =
            await escena.Cliente.EtiquetaDeAsync(EscenaDeRecuento.RutaDeLaLinea(abierto.Id, primera.Id));
        string deSegunda =
            await escena.Cliente.EtiquetaDeAsync(EscenaDeRecuento.RutaDeLaLinea(abierto.Id, segunda.Id));

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        // TRANSACCIÓN 1: cuenta la primera línea, con la cabecera bloqueada y tocada, y NO suelta.
        (Resultado<LineaDeRecuentoDto> conteo, IDbContextTransaction enVuelo) =
            await modulo.ContarYQuedarseDentroAsync(abierto.Id, primera.Id, dePrimera, 4m);

        await using (enVuelo)
        {
            conteo.EsCorrecto.ShouldBeTrue($"«{conteo.Error?.Codigo}»");

            // TRANSACCIÓN 2: la otra línea por la API, con su propia versión, que nadie ha cambiado.
            Task<HttpResponseMessage> laOtra = escena.ContarAsync(abierto.Id, segunda.Id, deSegunda, 6m);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                modulo.ProcesoDeLaBase,
                laOtra,
                "el conteo en vuelo",
                "ha contado sin esperar al cerrojo de la cabecera");

            await enVuelo.CommitAsync();

            using HttpResponseMessage respuesta = await laOtra.WaitAsync(s_plazo);

            // SIN EL CERROJO ESTO ES UN 412: la segunda leyó la cabecera de antes, la tocó con esa
            // versión y, al soltarla la primera, ya no casaba.
            (await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(respuesta)).Contado.ShouldBe(6m);
        }

        (await escena.LineaAsync(abierto.Id, primera.Id)).Contado.ShouldBe(4m);
        (await escena.LineaAsync(abierto.Id, segunda.Id)).Contado.ShouldBe(6m);
        (await escena.FichaAsync(abierto.Id)).LineasSinContar.ShouldBe(0);
    }

    /// <summary>
    /// Una clave que llega sin el cerrojo y gana: el alta por la API se para en el índice detrás de
    /// ella, y cuando publica es un <c>409</c> de clave repetida.
    /// </summary>
    [Fact]
    public async Task La_misma_clave_llegada_sin_cerrojo_la_para_el_indice_con_el_mismo_409()
    {
        EscenaDeRecuento escena = await MontarAsync(789, "RCC-K");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        UbicacionDto otroHueco =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, "RCC-K-A2");

        await using NpgsqlConnection plantada = new(postgres.CadenaDeConexion);
        await plantada.OpenAsync();

        await using (NpgsqlTransaction sinPublicar = await plantada.BeginTransactionAsync())
        {
            await PlantarAsync(plantada, abierto.Id, otroHueco.Id, de, serie: null);

            Task<HttpResponseMessage> alta = EscenaDeRecuento.AnadirAsync(
                escena.Cliente, abierto.Id, new(otroHueco.Id, de.ArticuloId, null, null, null));

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                plantada.ProcessID,
                alta,
                "la línea sin publicar",
                "ha añadido la clave sin chocar con el índice de una clave por recuento");

            await sinPublicar.CommitAsync();

            using HttpResponseMessage respuesta = await alta.WaitAsync(s_plazo);

            // SIN EL ÍNDICE ESTO ES UN 201, y la clave se contaría dos veces; sin su declaración, el
            // 23505 no sería una regla sino un defecto.
            await EscenaDeRecuento.ExigirElProblemaAsync(
                respuesta, HttpStatusCode.Conflict, "recuento-clave-repetida", "la clave que ya escribió otro");
        }

        (await escena.TodasLasLineasAsync(abierto.Id)).Count.ShouldBe(2, "la precargada y la que ganó");
    }

    /// <summary>
    /// Un número de serie que llega sin el cerrojo a otra ubicación y gana: el alta por la API se
    /// para en el índice de la serie, y es un <c>409</c> de serie repetida.
    /// </summary>
    [Fact]
    public async Task La_misma_serie_llegada_sin_cerrojo_a_otra_ubicacion_la_para_el_indice_con_el_mismo_409()
    {
        EscenaDeRecuento escena = await MontarAsync(790, "RCC-S", "PorNumeroSerie");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 1m, serie: "S-1");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        UbicacionDto otroHueco =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, "RCC-S-A2");

        await using NpgsqlConnection plantada = new(postgres.CadenaDeConexion);
        await plantada.OpenAsync();

        await using (NpgsqlTransaction sinPublicar = await plantada.BeginTransactionAsync())
        {
            await PlantarAsync(plantada, abierto.Id, otroHueco.Id, de, serie: "S-9");

            // EN OTRA UBICACIÓN: el índice de la clave no los separa, y el de la serie sí.
            Task<HttpResponseMessage> alta = EscenaDeRecuento.AnadirAsync(
                escena.Cliente, abierto.Id, new(de.UbicacionA, de.ArticuloId, null, "S-9", null));

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                plantada.ProcessID,
                alta,
                "la serie sin publicar",
                "ha añadido la serie sin chocar con el índice de una serie por recuento");

            await sinPublicar.CommitAsync();

            using HttpResponseMessage respuesta = await alta.WaitAsync(s_plazo);

            await EscenaDeRecuento.ExigirElProblemaAsync(
                respuesta, HttpStatusCode.Conflict, "recuento-serie-repetida", "la serie que ya escribió otro");
        }

        (await escena.TodasLasLineasAsync(abierto.Id)).Count.ShouldBe(2, "la precargada y la que ganó");
    }

    /// <summary>
    /// Con el recuento bloqueado por quien cuenta, otra empresa que pide el mismo identificador no
    /// espera: el cerrojo no la ve, y es un <c>404</c> en el momento.
    /// </summary>
    /// <remarks>
    /// Es la pareja del primer caso, que demuestra que el cerrojo frena a la misma empresa: este
    /// demuestra que no frena a otra. Sin la empresa en el <c>WHERE</c> del cerrojo, la ajena se
    /// quedaría esperando a una transacción de otra empresa, y una empresa podría parar a otra
    /// probando identificadores.
    /// </remarks>
    [Fact]
    public async Task Otra_empresa_no_espera_al_cerrojo_de_un_recuento_que_no_es_suyo()
    {
        EscenaDeRecuento escena = await MontarAsync(791, "RCC-E");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id));

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(792));
        _clientes.Add(ajena);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        (Resultado<LineaDeRecuentoDto> conteo, IDbContextTransaction enVuelo) =
            await modulo.ContarYQuedarseDentroAsync(abierto.Id, linea.Id, etiqueta, 2m);

        await using (enVuelo)
        {
            conteo.EsCorrecto.ShouldBeTrue($"«{conteo.Error?.Codigo}»");

            // CON EL CERROJO PUESTO: si la ajena esperara, esto vencería el plazo.
            using HttpResponseMessage deLaAjena = await EscenaDeRecuento
                .ContarAsync(ajena, abierto.Id, linea.Id, etiqueta, 5m)
                .WaitAsync(TimeSpan.FromSeconds(10));

            await EscenaDeRecuento.ExigirElProblemaAsync(
                deLaAjena, HttpStatusCode.NotFound, "recuento-no-encontrado", "contar desde otra empresa");

            await enVuelo.RollbackAsync();
        }

        (await escena.LineaAsync(abierto.Id, linea.Id)).Contado.ShouldBeNull("las dos escrituras se deshicieron");
    }

    // UNA LÍNEA ESCRITA POR DEBAJO DEL ORM y sin el cerrojo de la cabecera: es lo que llegaría por un
    // camino que se saltara el caso de uso. El 999 no choca con ningún número del recuento, así que el
    // índice que la para es el de la clave o el de la serie.
    private static async Task PlantarAsync(
        NpgsqlConnection conexion, Guid recuentoId, Guid ubicacionId, EscenaDeTransferencia de, string? serie)
    {
        await using NpgsqlCommand orden = new(
            "INSERT INTO inventario.lineas_recuento (id, recuento_id, numero, ubicacion_id, articulo_id, " +
            "codigo_de_lote, numero_de_serie, unidad_base_id, origen, creado_en, modificado_en) " +
            "VALUES (@id, @recuento, 999, @ubicacion, @articulo, NULL, @serie, @unidad, 'Anadida', now(), now())",
            conexion);

        orden.Parameters.AddWithValue("id", Guid.CreateVersion7());
        orden.Parameters.AddWithValue("recuento", recuentoId);
        orden.Parameters.AddWithValue("ubicacion", ubicacionId);
        orden.Parameters.AddWithValue("articulo", de.ArticuloId);
        orden.Parameters.Add(new NpgsqlParameter<string?>("serie", serie));
        orden.Parameters.AddWithValue("unidad", de.UnidadId);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
