using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Añade a un recuento en curso una clave que la precarga no traía, sin contar.</summary>
public interface IAnadirLineaDeRecuento
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="peticion">La clave, y su coste si se sabe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La línea nueva, con su teórico, o el motivo por el que no se añade.</returns>
    Task<Resultado<LineaDeRecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        AnadirLineaDeRecuentoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IAnadirLineaDeRecuento"/>
/// <remarks>
/// <para>
/// <b>Lo que pide el alta del ajuste a una línea, sin la cantidad</b> (ADR-0055 §6): la ubicación es
/// del almacén del recuento, el artículo se almacena, y el lote o el número de serie casan con su
/// marca. La marca se lee sin cerrojo: es la cortesía, y la guarda es la de confirmar.
/// </para>
/// <para>
/// <b>El orden, de lo barato a lo caro</b>: lo que se dice sin preguntar a nadie —el coste y la forma
/// de los códigos— va antes de abrir la transacción; lo que pregunta a los puertos, después del
/// cerrojo, porque la ubicación se mira contra el almacén del recuento y hay que leerlo.
/// </para>
/// <para>
/// <b>La clave repetida se mira con la cabecera bloqueada</b>, así que dos altas de la misma clave a la
/// vez se ponen en fila y la segunda la ve. El índice único de las líneas es la red de debajo, y el
/// borde lo traduce al mismo <c>409</c>.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento y las existencias.</param>
/// <param name="ubicaciones">Puerto de ubicaciones: que sea del almacén, y que no esté bloqueada.</param>
/// <param name="articulos">Puerto de artículos: que se almacene, y su unidad base.</param>
/// <param name="trazabilidad">La marca del artículo, sin cerrojo: la cortesía (ADR-0048 §4).</param>
/// <param name="versiones">La versión de la cabecera, que se toca.</param>
/// <param name="unidadTrabajo">La transacción que abarca el cerrojo, la lectura y el guardado.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class AnadirLineaDeRecuento(
    IRepositorioDeRecuentos recuentos,
    IConsultaDeUbicaciones ubicaciones,
    IConsultaDeArticulos articulos,
    IConsultaDeTrazabilidad trazabilidad,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAnadirLineaDeRecuento
{
    /// <inheritdoc/>
    public Task<Resultado<LineaDeRecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        AnadirLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        if (peticion.CosteUnitario is { } coste
            && (coste < 0m
                || decimal.Round(coste, Importe.Decimales, MidpointRounding.AwayFromZero) >= ErroresDeRecuento.TopeDelCoste))
        {
            return Task.FromResult(Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.CosteNoValido()));
        }

        if (peticion.CodigoDeLote is not null && CodigoGs1.Normalizar(peticion.CodigoDeLote) is null)
        {
            return Task.FromResult(Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.LoteNoValido()));
        }

        if (peticion.NumeroDeSerie is not null && CodigoGs1.Normalizar(peticion.NumeroDeSerie) is null)
        {
            return Task.FromResult(Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.NumeroDeSerieNoValido()));
        }

        return unidadTrabajo.EnTransaccionAsync(
            enCurso => AnadirAsync(recuentoId, peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<LineaDeRecuentoDto>> AnadirAsync(
        Guid recuentoId,
        AnadirLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        Resultado<Recuento> enCurso = await ElRecuentoEnCurso
            .BloquearYLeerAsync(recuentos, recuentoId, cancelacion)
            .ConfigureAwait(false);

        if (!enCurso.EsCorrecto)
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(enCurso.Error!);
        }

        Recuento recuento = enCurso.Valor;

        Resultado losMaestros = await ComprobarLosMaestrosAsync(recuento, peticion, cancelacion).ConfigureAwait(false);

        if (!losMaestros.EsCorrecto)
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(losMaestros.Error!);
        }

        // LA MARCA, DESPUÉS DEL ARTÍCULO: solo uno que existe tiene marca. Y LOTE Y SERIE A LA VEZ los
        // rechaza aquí, porque una marca es de una sola cosa; por eso la clave se construye después.
        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasDeAsync([peticion.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        LineaConCodigos[] laClave =
            [new(1, peticion.ArticuloId, peticion.CodigoDeLote, peticion.NumeroDeSerie)];

        if (LaTrazabilidadDeLasLineas.LoQueNoCasa(laClave, marcas) is { } noCasa)
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.TrazabilidadNoCasa(noCasa));
        }

        var clave = ClaveDelRecuento.De(
            peticion.UbicacionId, peticion.ArticuloId, peticion.CodigoDeLote, peticion.NumeroDeSerie);

        if (recuento.LlevaLaClave(clave))
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.ClaveRepetida());
        }

        if (recuento.LlevaLaSerie(clave))
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.SerieRepetida());
        }

        IReadOnlyDictionary<Guid, Guid> unidadesBase = await articulos
            .UnidadesBaseDeAsync([peticion.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        // EL ARTÍCULO YA DIJO QUE SE ALMACENA, y todo lo que se almacena tiene unidad base. Si falta,
        // los dos puertos de Catálogo no dicen lo mismo, y eso es un defecto.
        Guid unidadBaseId = unidadesBase.TryGetValue(peticion.ArticuloId, out Guid laUnidad)
            ? laUnidad
            : throw new InvalidOperationException(
                $"El artículo {peticion.ArticuloId} se almacena y Catálogo no da su unidad base.");

        LineaDeRecuento linea = recuento.AnadirLinea(clave, unidadBaseId, peticion.CosteUnitario, reloj.GetUtcNow());
        ElRecuentoEnCurso.Tocar(versiones, recuento);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        ElTeoricoDeLasLineas teorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, [peticion.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(linea.ADto(teorico));
    }

    private async Task<Resultado> ComprobarLosMaestrosAsync(
        Recuento recuento,
        AnadirLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        EstadoDeMaestro estadoDeLaUbicacion = await ubicaciones
            .EstadoDeAsync(recuento.AlmacenId, peticion.UbicacionId, cancelacion)
            .ConfigureAwait(false);

        Resultado laUbicacion = LosMaestrosDelRecuento.LaUbicacion(estadoDeLaUbicacion, peticion.UbicacionId);

        if (!laUbicacion.EsCorrecto)
        {
            return laUbicacion;
        }

        AptitudParaMoverExistencias aptitud = await articulos
            .AptitudDeAsync(peticion.ArticuloId, cancelacion)
            .ConfigureAwait(false);

        return LosMaestrosDelRecuento.ElArticulo(aptitud, peticion.ArticuloId);
    }
}
