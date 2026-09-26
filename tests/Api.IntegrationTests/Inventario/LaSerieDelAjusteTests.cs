using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las dos puertas que mira la serie de un ajuste, y por qué hacen falta las dos: el puerto, al
/// abrir el borrador, y el <c>WHERE</c> de la sentencia que numera, al confirmarlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>La segunda no es una repetición de la primera</b>, y el caso que lo demuestra es
/// <see cref="Cerrar_la_serie_despues_del_borrador_lo_deja_sin_poder_confirmarse"/>: entre abrir y
/// confirmar pasa tiempo —minutos, o los días que tarde quien lo rellene— y una serie se puede
/// cerrar en ese hueco. Lo que el puerto consigue es que nadie abra un borrador contra una serie
/// que ya no sirve; lo que la R5 exige lo sostiene la sentencia, en la transacción del documento.
/// Sin este fichero, las dos parecerían la misma comprobación escrita dos veces.
/// </para>
/// <para>
/// <b>Y el número se cobra por el efecto</b>, no mirando la respuesta del mecanismo: se confirman
/// dos ajustes de la misma serie y se exige el 1 y el 2 <i>en el documento</i>, más el contador de
/// la serie leído por la API. Un <c>Confirmar</c> que compusiera el número por su cuenta —o que lo
/// dejara fuera del DTO— pasaría cualquier afirmación hecha sobre lo que devuelve el numerador.
/// </para>
/// <para>
/// <b>Semillas: las empresas van por el 309 y los maestros de instalación por el 350, y los dos
/// casos del ADR-0043 usan la 333 y la 334, con los maestros 367 y 368</b> —el 313 y el 314 eran
/// de <c>ElNumeroEntraEnElReciboTests</c>, y chocaron en la primera pasada—. El resto
/// del reparto de este carril está en <c>ElCerrojoDeLaNumeracionTests</c> —que gasta del 301 al
/// 308— y en <c>UnAlmacenBloqueadoNoAdmiteAjustesTests</c>. Un número de empresa acaba en un NIF
/// único y uno de maestro en el código único de otra tabla, así que son dos cuentas y no una.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaSerieDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
{
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

    [Fact]
    public async Task Una_serie_cerrada_no_deja_abrir_el_borrador()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(309);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "SER-A");
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, "SERA");

        await CerrarAsync(empresa.Id, serie.Id);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            UnaPeticionContra(serie.Id, almacen.Id), CancellationToken.None);

        alta.EsCorrecto.ShouldBeFalse();

        alta.Error!.Codigo.ShouldBe(
            "ajuste-serie-cerrada",
            "es un 409 y no un 400: el identificador que se envió es válido y la serie existe, " +
            "lo que pasa es que está en un estado que no admite un documento nuevo");
    }

    [Fact]
    public async Task Una_serie_que_no_existe_no_deja_abrir_el_borrador()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(310);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "SER-B");

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            UnaPeticionContra(Guid.CreateVersion7(), almacen.Id), CancellationToken.None);

        alta.EsCorrecto.ShouldBeFalse();

        alta.Error!.Codigo.ShouldBe(
            "ajuste-serie-no-encontrada",
            "y este SÍ es un 400: lo que se envió no corresponde a ninguna fila, que es la " +
            "cuarta vía del ADR-0024 — sin este puerto el borrador se guardaría apuntando a nada");
    }

    [Fact]
    public async Task Confirmar_pone_el_numero_en_el_documento_y_lo_sube_en_la_serie()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(311);

        (AbrirAjusteDto peticion, SerieDto serie) = await UnAjusteCompletoAsync(cliente, "SER-C", 350);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> primero = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        Resultado<AjusteDto> segundo = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        primero.EsCorrecto.ShouldBeTrue($"«{primero.Error?.Codigo}»");
        segundo.EsCorrecto.ShouldBeTrue($"«{segundo.Error?.Codigo}»");

        primero.Valor.Numero.ShouldBeNull(
            "un borrador no ha gastado ningún correlativo: si se tira, no deja hueco");

        Resultado<AjusteDto> uno = await modulo.ConfirmarAsync(primero.Valor.Id);
        Resultado<AjusteDto> dos = await modulo.ConfirmarAsync(segundo.Valor.Id);

        uno.EsCorrecto.ShouldBeTrue($"«{uno.Error?.Codigo}»");
        dos.EsCorrecto.ShouldBeTrue($"«{dos.Error?.Codigo}»");

        // EL NÚMERO SALE EN EL CUERPO DE LA RESPUESTA, que es lo que hace que entre en el recibo
        // de idempotencia: lo que se guarda como recibo es este DTO. Si el número se compusiera
        // fuera de lo que se devuelve, el reintento con la misma clave contestaría sin él.
        uno.Valor.Numero.ShouldBe(1);
        dos.Valor.Numero.ShouldBe(2);

        uno.Valor.SerieId.ShouldBe(serie.Id);

        // Y la serie lo dice por su cuenta, leída por la API. Es la comprobación que no se cree
        // nada de lo anterior: si el documento se hubiera puesto el número sin pasar por el
        // contador, aquí seguiría habiendo un cero.
        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(2);
    }

    [Fact]
    public async Task Cerrar_la_serie_despues_del_borrador_lo_deja_sin_poder_confirmarse()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(312);

        (AbrirAjusteDto peticion, SerieDto serie) = await UnAjusteCompletoAsync(cliente, "SER-D", 351);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue(
            $"con la serie todavía activa el alta tiene que pasar, o el rechazo de abajo no " +
            $"diría nada del cierre. Contestó «{alta.Error?.Codigo}»");

        // AQUÍ ES DONDE SE VE QUE EL PUERTO NO ES LA GARANTÍA. La respuesta que dejó nacer el
        // borrador era verdad cuando se dio, y ha dejado de serlo sin que nadie toque el ajuste.
        await CerrarAsync(empresa.Id, serie.Id);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "la serie ya no numera, y quien lo impide no es el puerto del alta —que contestó " +
            "hace rato— sino el `WHERE` de la sentencia, dentro de la transacción");

        confirmacion.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeSerieNoNumera);

        // Y EL DOCUMENTO SE QUEDA INTACTO, leído de la base y no de lo que devolvió el caso de
        // uso: sin número y en borrador, o sea confirmable el día que alguien reabra la serie. Un
        // ajuste que se quedara confirmado y sin número es justo lo que la R5 no permite.
        await using (InventarioDbContext inventario = postgres.AbrirInventario(empresa.Id))
        {
            Ajuste comoQuedo = await inventario.Ajustes
                .SingleAsync(fila => fila.Id == alta.Valor.Id);

            comoQuedo.Estado.ShouldBe(EstadoDeAjuste.Borrador);
            comoQuedo.Numero.ShouldBeNull();
        }

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(
            0,
            "la transacción se deshizo entera, así que el incremento que llegó a ejecutarse —si " +
            "llegó— no queda gastado: es lo que una secuencia de PostgreSQL no sabría hacer");
    }

    [Fact]
    public async Task Un_ajuste_abierto_sobre_una_serie_de_facturas_no_se_confirma()
    {
        // LA NOTA DEL ÍTEM 2.4, DE EXTREMO A EXTREMO. El alta solo pregunta por el estado de la
        // serie, así que el borrador nace; lo que lo para es el `WHERE`, al confirmar. Antes del
        // ADR-0043 se confirmaba, y el número que consumía era un correlativo de la serie de
        // facturas: el hueco se lo llevaba la factura siguiente.
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(333);

        (AbrirAjusteDto peticion, SerieDto serie) =
            await UnAjusteCompletoAsync(cliente, "SER-E", 367, TipoDeDocumento.FacturaEmitida);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue(
            $"el alta no mira el tipo de la serie, y si rechazara aquí el caso no diría nada del " +
            $"`WHERE`. Contestó «{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeFalse("una serie de facturas no le da número a un ajuste");
        confirmacion.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeSerieDeOtroDocumento);

        await ElBorradorSigueSinNumeroAsync(empresa.Id, alta.Valor.Id);

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(0, "la serie de facturas no ha gastado ningún número");
    }

    [Fact]
    public async Task Un_ajuste_de_este_anio_sobre_la_serie_del_anio_pasado_no_se_confirma()
    {
        // LA NOTA DEL ÍTEM 2.6, DE EXTREMO A EXTREMO. Los dos ejercicios están abiertos, así que la
        // R9 contesta que sí —la fecha de hoy cae en el de este año— y el borrador llega al
        // número. Lo que lo para es que la serie cuelga del año pasado. Antes del ADR-0043 se
        // confirmaba, y el correlativo del año pasado seguía corriendo con documentos de este.
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(334);

        (AbrirAjusteDto deEsteAnio, _) = await UnAjusteCompletoAsync(cliente, "SER-F", 368);

        int anioPasado = DateTime.UtcNow.Year - 1;
        EjercicioDto elAnioPasado = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, anioPasado);
        SerieDto delAnioPasado =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, elAnioPasado.Id, "SER-F-V");

        AbrirAjusteDto peticion = deEsteAnio with { SerieId = delAnioPasado.Id };

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "la serie del año pasado no numera un documento de este, aunque los dos años estén abiertos");
        confirmacion.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeFechaFueraDelEjercicioDeLaSerie);

        await ElBorradorSigueSinNumeroAsync(empresa.Id, alta.Valor.Id);

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{delAnioPasado.Id}");

        despues!.Contador.ShouldBe(0);
    }

    /// <summary>Una petición mínima: las líneas no se llegan a mirar si la serie no pasa.</summary>
    /// <remarks>
    /// El alta comprueba la serie <b>antes</b> que las líneas, así que para los dos casos de
    /// rechazo no hace falta ni artículo ni ubicación de verdad. Que ese orden es el que hay se ve
    /// en que estos casos pasen: con las líneas primero, el rechazo llegaría con otro código.
    /// </remarks>
    /// <param name="serieId">La serie que se envía.</param>
    /// <param name="almacenId">El almacén, que sí es de verdad.</param>
    private static AbrirAjusteDto UnaPeticionContra(Guid serieId, Guid almacenId) => new(
        serieId,
        almacenId,
        DateOnly.FromDateTime(DateTime.UtcNow),
        "Ajuste que no debería pasar de la serie",
        [new LineaDeAjusteDto(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 1m, Guid.CreateVersion7(), 1m, 1m, "EUR")]);

    /// <summary>Todos los maestros de un ajuste que sí se puede abrir, y su petición.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="tipo">Qué documentos numera la serie. Por omisión, ajustes.</param>
    /// <returns>La petición lista para el alta, y la serie que la numerará.</returns>
    private static async Task<(AbrirAjusteDto Peticion, SerieDto Serie)> UnAjusteCompletoAsync(
        HttpClient cliente,
        string codigo,
        int semillaDeInstalacion,
        TipoDeDocumento tipo = TipoDeDocumento.AjusteDeInventario)
    {
        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo, tipo);

        AbrirAjusteDto peticion = new(
            serie.Id,
            almacen.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Regularización de un recuento",
            [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m, "EUR")]);

        return (peticion, serie);
    }

    /// <summary>Cierra una serie por el contexto, porque la API todavía no tiene por dónde.</summary>
    /// <remarks>
    /// <b>No es un atajo que se salte una validación</b>: cerrar una serie es una transición del
    /// dominio —<c>Serie.Cerrar</c>— y eso es lo que se llama aquí. Lo que falta es el borde HTTP,
    /// que no es de este ítem. Un <c>UPDATE</c> a mano sí sería un atajo, y por eso no se usa.
    /// </remarks>
    /// <param name="empresaId">Empresa dueña de la serie (R8).</param>
    /// <param name="serieId">La serie que cerrar.</param>
    private async Task CerrarAsync(Guid empresaId, Guid serieId)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresaId);

        Serie serie = await contexto.Series.SingleAsync(fila => fila.Id == serieId);
        serie.Cerrar();

        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// El documento, leído de la base: en borrador y sin número, o sea confirmable el día que se
    /// corrija la serie. Un ajuste confirmado sin número es justo lo que la R5 no permite.
    /// </summary>
    /// <param name="empresaId">La empresa del caso (R8).</param>
    /// <param name="ajusteId">El ajuste que no se pudo confirmar.</param>
    private async Task ElBorradorSigueSinNumeroAsync(Guid empresaId, Guid ajusteId)
    {
        await using InventarioDbContext inventario = postgres.AbrirInventario(empresaId);

        Ajuste comoQuedo = await inventario.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        comoQuedo.Estado.ShouldBe(EstadoDeAjuste.Borrador);
        comoQuedo.Numero.ShouldBeNull();
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }
}
