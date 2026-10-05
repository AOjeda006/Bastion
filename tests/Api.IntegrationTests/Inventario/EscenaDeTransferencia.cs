using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Una empresa con tres almacenes de un hueco cada uno, un artículo y las dos series de su
/// ejercicio, la de ajustes y la de transferencias, todo por la API: lo que necesita un caso de la
/// transferencia (ítem 2.11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tres almacenes y no dos</b>, porque la carrera de la serie en tránsito necesita un tercero: la
/// serie sale de A hacia B, y un ajuste la da de alta en C mientras vuela (ADR-0053 §7). Los demás
/// casos usan A y B y no miran C.
/// </para>
/// <para>
/// <b>El ejercicio es del año que se pida</b>, el actual si no se dice, y las dos series cuelgan de
/// él. El caso del cambio de año y el del ejercicio cerrado montan el suyo en años pasados, porque
/// «el 30 de diciembre y el 3 de enero» tienen que ser días que ya pasaron cualquier día que corra
/// el carril, también el 1 de enero.
/// </para>
/// <para>
/// <b>El alta va por el caso de uso cableado a mano, y el resto por la API</b>, como en el ajuste:
/// el alta no tiene borde (ADR-0053 §9), y lo que se envía, se recibe o se anula por HTTP pasa por
/// el filtro de idempotencia, que es el dueño de la transacción.
/// </para>
/// <para>
/// <b>Las guardas del ajuste la usan también</b> (epílogo del 2.11, <c>LasGuardasDelAjusteTests</c>):
/// tiene todo lo que un ajuste necesita, y un segundo almacén cuyo hueco no es del primero.
/// </para>
/// </remarks>
/// <param name="Cliente">Cliente autenticado en la empresa de la escena.</param>
/// <param name="EmpresaId">La empresa (R8).</param>
/// <param name="Ejercicio">El ejercicio del que cuelgan las dos series.</param>
/// <param name="SerieDeAjustes">La que numera los ajustes que llenan los almacenes.</param>
/// <param name="SerieDeTransferencias">La que numera las transferencias.</param>
/// <param name="AlmacenA">El primer almacén, de donde suelen salir.</param>
/// <param name="UbicacionA">Su hueco.</param>
/// <param name="AlmacenB">El segundo, a donde suelen llegar.</param>
/// <param name="UbicacionB">Su hueco.</param>
/// <param name="AlmacenC">El tercero, el de la carrera de la serie.</param>
/// <param name="UbicacionC">Su hueco.</param>
/// <param name="ArticuloId">El artículo, con la marca con la que se montó.</param>
/// <param name="UnidadId">Su unidad base.</param>
internal sealed record EscenaDeTransferencia(
    HttpClient Cliente,
    Guid EmpresaId,
    EjercicioDto Ejercicio,
    SerieDto SerieDeAjustes,
    SerieDto SerieDeTransferencias,
    Guid AlmacenA,
    Guid UbicacionA,
    Guid AlmacenB,
    Guid UbicacionB,
    Guid AlmacenC,
    Guid UbicacionC,
    Guid ArticuloId,
    Guid UnidadId)
{
    /// <summary>La ruta de las tres acciones.</summary>
    internal const string Transferencias = "/api/v1/inventario/transferencias";

    private const string Cabecera = "Idempotency-Key";

    /// <summary>Hoy, con el reloj de verdad: el de la API.</summary>
    internal static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Monta la escena en una empresa nueva.</summary>
    /// <param name="api">La API del caso, que da la empresa y su cliente.</param>
    /// <param name="semilla">Número de empresa, que acaba en su NIF.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="maestro">Número de instalación del artículo, su unidad y su tramo.</param>
    /// <param name="trazabilidad">La marca del artículo.</param>
    /// <param name="anio">El año del ejercicio; el actual si no se dice.</param>
    /// <returns>La escena; su cliente lo cierra quien la pidió.</returns>
    internal static async Task<EscenaDeTransferencia> MontarAsync(
        ApiDeVerdad api,
        int semilla,
        string codigo,
        int maestro,
        string trazabilidad = "Ninguna",
        int? anio = null)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        AlmacenDto almacenA = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-A");
        AlmacenDto almacenB = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-B");
        AlmacenDto almacenC = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-C");

        UbicacionDto a = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenA.Id, codigo + "-A1");
        UbicacionDto b = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenB.Id, codigo + "-B1");
        UbicacionDto c = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenC.Id, codigo + "-C1");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, maestro, trazabilidad);

        EjercicioDto ejercicio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, anio ?? Hoy.Year);

        SerieDto deAjustes = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, ejercicio.Id, codigo + "-AJ");
        SerieDto deTransferencias = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            cliente, ejercicio.Id, codigo + "-TR", TipoDeDocumento.TransferenciaDeInventario);

        return new EscenaDeTransferencia(
            cliente,
            empresa.Id,
            ejercicio,
            deAjustes,
            deTransferencias,
            almacenA.Id,
            a.Id,
            almacenB.Id,
            b.Id,
            almacenC.Id,
            c.Id,
            articuloId,
            unidadId);
    }

    /// <summary>Mete unidades en un almacén con un ajuste confirmado por la API.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">Dónde.</param>
    /// <param name="ubicacionId">En qué hueco.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="coste">El coste unitario.</param>
    /// <param name="fecha">El día; hoy si no se dice.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <returns>El ajuste, ya confirmado.</returns>
    internal Task<Guid> EntrarAsync(
        PostgresConTodosLosModulos postgres,
        Guid almacenId,
        Guid ubicacionId,
        decimal cantidad,
        decimal coste,
        DateOnly? fecha = null,
        string? serie = null,
        string? lote = null) =>
        AjustarAsync(
            postgres,
            almacenId,
            new LineaDeAjusteDto(ubicacionId, ArticuloId, cantidad, UnidadId, 1m, coste, lote, serie),
            fecha);

    /// <summary>Saca unidades de un almacén con un ajuste confirmado por la API, al precio medio.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">De dónde.</param>
    /// <param name="ubicacionId">De qué hueco.</param>
    /// <param name="cantidad">Cuánto, en unidad base y sin signo.</param>
    /// <returns>El ajuste, ya confirmado.</returns>
    internal Task<Guid> SalirAsync(
        PostgresConTodosLosModulos postgres, Guid almacenId, Guid ubicacionId, decimal cantidad) =>
        AjustarAsync(
            postgres,
            almacenId,
            new LineaDeAjusteDto(ubicacionId, ArticuloId, -cantidad, UnidadId, 1m, null),
            fecha: null);

    /// <summary>Un ajuste en borrador, abierto con el módulo que se le pase.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="almacenId">El almacén del documento.</param>
    /// <param name="linea">Lo que mueve.</param>
    /// <param name="serieId">La serie que lo numerará; la de ajustes de la escena si no se dice.</param>
    /// <param name="fecha">El día; hoy si no se dice.</param>
    /// <returns>El identificador del borrador.</returns>
    internal async Task<Guid> AbrirUnAjusteAsync(
        ElModuloDeInventario modulo,
        Guid almacenId,
        LineaDeAjusteDto linea,
        Guid? serieId = null,
        DateOnly? fecha = null)
    {
        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serieId ?? SerieDeAjustes.Id, almacenId, fecha ?? Hoy, "Regularización de un recuento", [linea]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>Una transferencia en borrador de una línea, abierta por el caso de uso.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="fechaDeEnvio">El día en que sale; hoy si no se dice.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <param name="desdeC">Si sale de C en vez de A; llega siempre a B.</param>
    /// <returns>El identificador del borrador.</returns>
    internal async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo,
        decimal cantidad,
        DateOnly? fechaDeEnvio = null,
        string? serie = null,
        bool desdeC = false)
    {
        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(
            modulo,
            desdeC ? AlmacenC : AlmacenA,
            AlmacenB,
            [Linea(desdeC ? UbicacionC : UbicacionA, UbicacionB, cantidad, serie)],
            fechaDeEnvio);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>El alta de una transferencia entre dos almacenes cualesquiera, tal como salga.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="origen">De dónde sale.</param>
    /// <param name="destino">A dónde va.</param>
    /// <param name="lineas">Lo que lleva.</param>
    /// <param name="fechaDeEnvio">El día en que sale; hoy si no se dice.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<TransferenciaDto>> IntentarAbrirAsync(
        ElModuloDeInventario modulo,
        Guid origen,
        Guid destino,
        IReadOnlyList<LineaDeTransferenciaDto> lineas,
        DateOnly? fechaDeEnvio = null) =>
        modulo.AltaDeTransferencia.EjecutarAsync(
            new AbrirTransferenciaDto(SerieDeTransferencias.Id, origen, destino, fechaDeEnvio ?? Hoy, lineas),
            CancellationToken.None);

    /// <summary>Una línea del artículo de la escena, en su unidad base.</summary>
    /// <param name="ubicacionOrigen">El hueco de donde sale.</param>
    /// <param name="ubicacionDestino">El hueco a donde va.</param>
    /// <param name="cantidad">Cuánto.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <returns>La línea.</returns>
    internal LineaDeTransferenciaDto Linea(
        Guid ubicacionOrigen, Guid ubicacionDestino, decimal cantidad, string? serie = null, string? lote = null) =>
        new(ubicacionOrigen, ubicacionDestino, ArticuloId, cantidad, UnidadId, 1m, lote, serie);

    /// <summary>Una transferencia en borrador, abierta con un módulo propio que se cierra al volver.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="fechaDeEnvio">El día en que sale; hoy si no se dice.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>El identificador del borrador.</returns>
    internal async Task<Guid> AbrirAsync(
        PostgresConTodosLosModulos postgres, decimal cantidad, DateOnly? fechaDeEnvio = null, string? serie = null)
    {
        await using ElModuloDeInventario modulo = new(postgres, EmpresaId);

        return await AbrirAsync(modulo, cantidad, fechaDeEnvio, serie);
    }

    /// <summary>Abre una transferencia de A a B y la envía por la API, que tiene que salir bien.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="fechaDeEnvio">El día en que sale; hoy si no se dice.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>Lo que contestó la API: la transferencia enviada.</returns>
    internal async Task<TransferenciaDto> EnviarAsync(
        PostgresConTodosLosModulos postgres, decimal cantidad, DateOnly? fechaDeEnvio = null, string? serie = null)
    {
        Guid transferenciaId = await AbrirAsync(postgres, cantidad, fechaDeEnvio, serie);

        using HttpResponseMessage envio = await EnviarPorLaApiAsync(transferenciaId);

        return await LeerAsync<TransferenciaDto>(envio);
    }

    /// <summary>El envío por HTTP, con su clave, tal como salga.</summary>
    /// <param name="transferenciaId">El borrador.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> EnviarPorLaApiAsync(Guid transferenciaId) =>
        ConClaveAsync(new HttpRequestMessage(HttpMethod.Post, $"{Transferencias}/{transferenciaId}/envio"));

    /// <summary>La recepción por HTTP, con su clave, tal como salga.</summary>
    /// <param name="transferenciaId">La transferencia enviada.</param>
    /// <param name="fecha">El día en que llega.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> RecibirPorLaApiAsync(Guid transferenciaId, DateOnly fecha) =>
        ConClaveAsync(new HttpRequestMessage(HttpMethod.Post, $"{Transferencias}/{transferenciaId}/recepcion")
        {
            Content = JsonContent.Create(new RecibirTransferenciaDto { FechaDeRecepcion = fecha }),
        });

    /// <summary>La anulación por HTTP, con su clave, tal como salga.</summary>
    /// <param name="transferenciaId">La transferencia enviada o recibida.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> AnularPorLaApiAsync(Guid transferenciaId) =>
        ConClaveAsync(new HttpRequestMessage(HttpMethod.Post, $"{Transferencias}/{transferenciaId}/anulacion")
        {
            Content = JsonContent.Create(new AnularTransferenciaDto("Se envió al almacén equivocado")),
        });

    /// <summary>Por dónde va una serie, leído por la API.</summary>
    /// <param name="serieId">La serie; la de transferencias de la escena si no se dice.</param>
    /// <returns>Cuántos correlativos ha entregado.</returns>
    internal async Task<long> ContadorAsync(Guid? serieId = null) =>
        (await Cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serieId ?? SerieDeTransferencias.Id}"))!.Contador;

    /// <summary>
    /// Si otra transacción tiene bloqueado el contador de una serie, preguntado sin esperar.
    /// </summary>
    /// <remarks>
    /// <b>Es lo único que dice desde fuera si una operación llegó a numerar.</b> Lo que falla se
    /// deshace entero, número incluido, así que el contador dice lo mismo después tanto si se tomó el
    /// número como si no. Mientras la transacción sigue abierta, el cerrojo de la fila sí lo dice. La
    /// fila tiene que existir, porque la serie ya ha numerado, y se exige: sin fila, «no está
    /// bloqueado» saldría por no haber mirado.
    /// </remarks>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="serieId">La serie; la de transferencias de la escena si no se dice.</param>
    /// <returns>Si el <c>FOR UPDATE NOWAIT</c> chocó con un cerrojo.</returns>
    internal async Task<bool> ElContadorEstaBloqueadoAsync(
        PostgresConTodosLosModulos postgres, Guid? serieId = null)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();
        const string Contadores =
            $"{NumeradorDeSerie<TipoDeDocumento>.Esquema}.{NumeradorDeSerie<TipoDeDocumento>.TablaDeContadores}";

        await using NpgsqlCommand orden = new(
            $"SELECT serie_id FROM {Contadores} WHERE serie_id = @serie FOR UPDATE NOWAIT",
            conexion,
            transaccion);

        orden.Parameters.AddWithValue("serie", serieId ?? SerieDeTransferencias.Id);

        try
        {
            (await orden.ExecuteScalarAsync()).ShouldNotBeNull(
                "sin la fila del contador, «no está bloqueado» sale por no haber mirado");

            return false;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return true;
        }
    }

    /// <summary>Cierra el ejercicio por la API, tal como salga.</summary>
    /// <param name="ejercicioId">El ejercicio; el de la escena si no se dice.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> CerrarElEjercicioAsync(Guid? ejercicioId = null)
    {
        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{ejercicioId ?? Ejercicio.Id}";

        return Cliente.AccionarAsync(recurso, $"{recurso}/cierre", HttpMethod.Post);
    }

    /// <summary>El <c>type</c> del problema que trae una respuesta de error.</summary>
    /// <param name="respuesta">La respuesta.</param>
    /// <returns>El <c>type</c>.</returns>
    internal static async Task<string?> TipoDelProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.GetProperty("type").GetString();
    }

    /// <summary>La transferencia como está en la base, con sus líneas.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="transferenciaId">El documento.</param>
    /// <returns>El agregado, leído con el filtro de la empresa puesto.</returns>
    internal async Task<Transferencia> LaTransferenciaAsync(
        PostgresConTodosLosModulos postgres, Guid transferenciaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(EmpresaId);

        return await contexto.Transferencias.AsNoTracking().SingleAsync(fila => fila.Id == transferenciaId);
    }

    /// <summary>Cuántos inversos tiene una transferencia, leído con el filtro de la empresa puesto.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="originalId">La transferencia anulada, o que se intentó anular.</param>
    /// <returns>Cero o uno: el índice único de <c>anula_a_id</c> no deja más.</returns>
    internal async Task<int> InversosDeAsync(PostgresConTodosLosModulos postgres, Guid originalId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(EmpresaId);

        return await contexto.Transferencias.CountAsync(fila => fila.AnulaAId == originalId);
    }

    /// <summary>Las filas del libro de un documento, ordenadas por cantidad, de la salida a la entrada.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="documentoId">El documento.</param>
    /// <returns>Sus filas, leídas con el filtro de la empresa puesto.</returns>
    internal async Task<IReadOnlyList<MovimientoStock>> FilasDelLibroAsync(
        PostgresConTodosLosModulos postgres, Guid documentoId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(EmpresaId);

        List<MovimientoStock> filas = await contexto.Movimientos
            .AsNoTracking()
            .Where(fila => fila.DocumentoOrigenId == documentoId)
            .ToListAsync();

        return [.. filas.OrderBy(fila => fila.CantidadEnUnidadBase)];
    }

    /// <summary>La valoración de un almacén, leída con el filtro de la empresa puesto.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">El almacén.</param>
    /// <returns>Su valoración del artículo de la escena.</returns>
    internal async Task<Valoracion> LaValoracionDeAsync(PostgresConTodosLosModulos postgres, Guid almacenId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(EmpresaId);

        return await contexto.Valoraciones
            .AsNoTracking()
            .SingleAsync(fila => fila.ArticuloId == ArticuloId && fila.AlmacenId == almacenId);
    }

    /// <summary>
    /// Lo que la empresa tiene valorado, en el almacén y en vuelo: la suma de <c>valor</c> y de
    /// <c>valor_en_transito</c> de todas sus valoraciones (ADR-0053 §6).
    /// </summary>
    /// <param name="postgres">El contenedor.</param>
    /// <returns>El valor de la empresa.</returns>
    internal async Task<decimal> ElValorDeLaEmpresaAsync(PostgresConTodosLosModulos postgres)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(EmpresaId);

        List<Valoracion> todas = await contexto.Valoraciones.AsNoTracking().ToListAsync();

        return todas.Sum(fila => fila.Valor.Cantidad + fila.ValorEnTransito.Cantidad);
    }

    /// <summary>Lee el cuerpo de una respuesta que tenía que ser un <c>200</c>.</summary>
    /// <typeparam name="T">Lo que trae.</typeparam>
    /// <param name="respuesta">La respuesta.</param>
    /// <returns>El cuerpo.</returns>
    internal static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>La confirmación de un ajuste por HTTP, con su clave, tal como salga.</summary>
    /// <param name="ajusteId">El borrador.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> ConfirmarElAjustePorLaApiAsync(Guid ajusteId) =>
        ConClaveAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/confirmacion"));

    /// <summary>La anulación de un ajuste por HTTP, con su clave, tal como salga.</summary>
    /// <param name="ajusteId">El ajuste confirmado.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> AnularElAjustePorLaApiAsync(Guid ajusteId) =>
        ConClaveAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/anulacion")
        {
            Content = JsonContent.Create(new AnularAjusteDto("Se contó dos veces la misma caja")),
        });

    /// <summary>Cambia el tipo y la marca del artículo de la escena por la API, tal como salga.</summary>
    /// <param name="tipo">El tipo nuevo.</param>
    /// <param name="marca">La marca nueva.</param>
    /// <returns>La respuesta cruda.</returns>
    internal async Task<HttpResponseMessage> CambiarElArticuloAsync(string tipo, string marca)
    {
        string ruta = $"{LosMaestrosPorLaApi.Articulos}/{ArticuloId}";
        ArticuloDto articulo = (await Cliente.GetFromJsonAsync<ArticuloDto>(ruta))!;

        return await Cliente.ModificarAsync(
            ruta,
            new ModificarArticuloDto
            {
                Descripcion = articulo.Descripcion,
                Tipo = tipo,
                Trazabilidad = marca,
                ImpuestoPorDefectoId = articulo.ImpuestoPorDefectoId,
                CategoriaId = articulo.CategoriaId,
            });
    }

    /// <summary>Cierra una serie de la empresa de la escena con el método del dominio.</summary>
    /// <remarks>La serie no tiene borde para cerrarse, y por eso no va por la API.</remarks>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="serieId">La serie.</param>
    /// <returns>La tarea.</returns>
    internal async Task CerrarLaSerieAsync(PostgresConTodosLosModulos postgres, Guid serieId)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(EmpresaId);

        Serie serie = await contexto.Series.SingleAsync(fila => fila.Id == serieId);
        serie.Cerrar();

        await contexto.SaveChangesAsync();
    }

    private async Task<Guid> AjustarAsync(
        PostgresConTodosLosModulos postgres, Guid almacenId, LineaDeAjusteDto linea, DateOnly? fecha)
    {
        Guid ajusteId;

        await using (ElModuloDeInventario modulo = new(postgres, EmpresaId))
        {
            ajusteId = await AbrirUnAjusteAsync(modulo, almacenId, linea, fecha: fecha);
        }

        using HttpResponseMessage confirmado = await ConfirmarElAjustePorLaApiAsync(ajusteId);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));

        return ajusteId;
    }

    private async Task<HttpResponseMessage> ConClaveAsync(HttpRequestMessage peticion)
    {
        using (peticion)
        {
            peticion.Headers.TryAddWithoutValidation(Cabecera, Guid.NewGuid().ToString());

            return await Cliente.SendAsync(peticion);
        }
    }
}
