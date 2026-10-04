using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Abre una transferencia entre almacenes en borrador.</summary>
public interface IAbrirTransferencia
{
    /// <summary>Ejecuta el alta.</summary>
    /// <param name="peticion">Lo que se quiere llevar, de dónde y a dónde.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La transferencia en borrador, o el motivo por el que no se abre.</returns>
    Task<Resultado<TransferenciaDto>> EjecutarAsync(AbrirTransferenciaDto peticion, CancellationToken cancelacion);
}

/// <summary>El alta de la transferencia: lo que el ajuste pregunta, por las dos puntas.</summary>
/// <remarks>
/// <para>
/// <b>Sin acción HTTP</b>, como el alta del ajuste (ADR-0053 §9): llega con su pantalla. Hasta
/// entonces la usan los casos de integración, y lo que contesta es lo que contestará.
/// </para>
/// <para>
/// <b>Los dos almacenes se preguntan al puerto</b>, con el filtro de la empresa puesto, así que uno
/// de otra empresa contesta lo mismo que uno inventado. <b>Y cada ubicación con el almacén de su
/// punta</b>: la del origen con el origen, y la del destino con el destino. Preguntar las dos con
/// el mismo almacén dejaría pasar una ubicación del almacén de enfrente (ADR-0053 §8).
/// </para>
/// <para>
/// <b>El orden es el del ajuste, de lo barato a lo caro</b>: primero lo que se dice sin preguntar
/// a nadie —sin líneas, el mismo almacén, la cantidad y la forma de los códigos—, después los
/// maestros, y la marca la última, porque solo un artículo que existe tiene marca.
/// </para>
/// </remarks>
/// <param name="usuarioActual">De dónde sale la empresa (R8).</param>
/// <param name="transferencias">Dónde se apunta el documento.</param>
/// <param name="empresas">Puerto de empresas: si está activa, y su divisa base.</param>
/// <param name="almacenes">Puerto de almacenes.</param>
/// <param name="series">Puerto de series.</param>
/// <param name="ubicaciones">Puerto de ubicaciones.</param>
/// <param name="articulos">Puerto de artículos.</param>
/// <param name="trazabilidad">La marca de los artículos, sin cerrojo: la cortesía (ADR-0048 §4).</param>
/// <param name="unidades">Puerto de unidades de medida.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class AbrirTransferencia(
    IUsuarioActual usuarioActual,
    IRepositorioDeTransferencias transferencias,
    IConsultaDeEmpresas empresas,
    IConsultaDeAlmacenes almacenes,
    IConsultaDeSeries series,
    IConsultaDeUbicaciones ubicaciones,
    IConsultaDeArticulos articulos,
    IConsultaDeTrazabilidad trazabilidad,
    IConsultaDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAbrirTransferencia
{
    /// <inheritdoc/>
    public async Task<Resultado<TransferenciaDto>> EjecutarAsync(
        AbrirTransferenciaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Guid empresaId = usuarioActual.EmpresaId;

        // LA DIVISA DEL DOCUMENTO ES LA BASE DE LA EMPRESA (ADR-0046 §7), y la misma pregunta dice
        // si está activa, como en el ajuste.
        string? divisa = await empresas.DivisaBaseDeAsync(empresaId, cancelacion).ConfigureAwait(false);

        if (divisa is null)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.SinLineas());
        }

        if (peticion.AlmacenOrigenId == peticion.AlmacenDestinoId)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.MismoAlmacen());
        }

        IReadOnlyList<LineaConForma> conForma = LaTrazabilidadDeLasLineas.DeLaPeticion(peticion.Lineas);

        // LA CANTIDAD, ANTES QUE LA FORMA: una serie con cantidad negativa es, antes que nada, una
        // línea que no lleva nada al destino.
        foreach (LineaConForma linea in conForma)
        {
            if (NoLlevaNada(linea))
            {
                return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.CantidadNoValida(linea.Numero));
            }
        }

        if (LaTrazabilidadDeLasLineas.LoQueNoTieneForma(conForma) is { } sinForma)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.DeLaForma(sinForma));
        }

        Resultado losMaestros = await ComprobarLosMaestrosAsync(peticion, cancelacion).ConfigureAwait(false);

        if (!losMaestros.EsCorrecto)
        {
            return Resultado.Fallo<TransferenciaDto>(losMaestros.Error!);
        }

        // LA MARCA, SIN CERROJO: la cortesía. La guarda es la del envío (ADR-0048 §4).
        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasDeAsync([.. peticion.Lineas.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        if (LaTrazabilidadDeLasLineas.LoQueNoCasa(LaTrazabilidadDeLasLineas.SusCodigos(conForma), marcas)
            is { } noCasa)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.TrazabilidadNoCasa(noCasa));
        }

        DateTimeOffset momento = reloj.GetUtcNow();

        var transferencia = Transferencia.Abrir(
            empresaId,
            peticion.SerieId,
            peticion.AlmacenOrigenId,
            peticion.AlmacenDestinoId,
            peticion.FechaDeEnvio,
            divisa,
            momento);

        foreach (LineaDeTransferenciaDto linea in peticion.Lineas)
        {
            transferencia.AnadirLinea(
                linea.UbicacionOrigenId,
                linea.UbicacionDestinoId,
                linea.ArticuloId,
                linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                linea.FactorAUnidadBase,
                momento,
                linea.CodigoDeLote,
                linea.NumeroDeSerie);
        }

        transferencias.Agregar(transferencia);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(transferencia.ADto());
    }

    /// <summary>
    /// Las tres formas en que una línea no lleva nada: lo que el dominio lanza, dicho antes con su
    /// <c>type</c>.
    /// </summary>
    private static bool NoLlevaNada(LineaConForma linea) =>
        linea.CantidadIntroducida <= 0m
        || linea.FactorAUnidadBase <= 0m
        || MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase) == 0m;

    private async Task<Resultado> ComprobarLosMaestrosAsync(
        AbrirTransferenciaDto peticion,
        CancellationToken cancelacion)
    {
        Guid[] puntas = [peticion.AlmacenOrigenId, peticion.AlmacenDestinoId];

        foreach (Guid almacenId in puntas)
        {
            EstadoDeMaestro estado = await almacenes.EstadoDeAsync(almacenId, cancelacion).ConfigureAwait(false);
            Resultado elAlmacen = LosMaestrosDeLaTransferencia.ElAlmacen(estado, almacenId);

            if (!elAlmacen.EsCorrecto)
            {
                return elAlmacen;
            }
        }

        EstadoDeMaestro estadoDeLaSerie = await series
            .EstadoDeAsync(peticion.SerieId, cancelacion)
            .ConfigureAwait(false);

        Resultado laSerie = LosMaestrosDeLaTransferencia.LaSerie(estadoDeLaSerie, peticion.SerieId);

        if (!laSerie.EsCorrecto)
        {
            return laSerie;
        }

        // Cada ubicación se pregunta una vez POR SU ALMACÉN: el mismo identificador en las dos
        // puntas son dos preguntas, y una de las dos tiene que contestar que no.
        HashSet<(Guid Almacen, Guid Ubicacion)> ubicacionesVistas = [];
        HashSet<Guid> articulosVistos = [];
        HashSet<Guid> unidadesVistas = [];

        foreach (LineaDeTransferenciaDto linea in peticion.Lineas)
        {
            Resultado laDelOrigen = await LaUbicacionAsync(
                peticion.AlmacenOrigenId, linea.UbicacionOrigenId, ubicacionesVistas, cancelacion).ConfigureAwait(false);

            if (!laDelOrigen.EsCorrecto)
            {
                return laDelOrigen;
            }

            Resultado laDelDestino = await LaUbicacionAsync(
                peticion.AlmacenDestinoId, linea.UbicacionDestinoId, ubicacionesVistas, cancelacion).ConfigureAwait(false);

            if (!laDelDestino.EsCorrecto)
            {
                return laDelDestino;
            }

            if (articulosVistos.Add(linea.ArticuloId))
            {
                AptitudParaMoverExistencias aptitud = await articulos
                    .AptitudDeAsync(linea.ArticuloId, cancelacion)
                    .ConfigureAwait(false);

                Resultado elArticulo = LosMaestrosDeLaTransferencia.ElArticulo(aptitud, linea.ArticuloId);

                if (!elArticulo.EsCorrecto)
                {
                    return elArticulo;
                }
            }

            if (unidadesVistas.Add(linea.UnidadIntroducidaId))
            {
                EstadoDeMaestro estado = await unidades
                    .EstadoDeAsync(linea.UnidadIntroducidaId, cancelacion)
                    .ConfigureAwait(false);

                Resultado laUnidad = LosMaestrosDeLaTransferencia.LaUnidad(estado, linea.UnidadIntroducidaId);

                if (!laUnidad.EsCorrecto)
                {
                    return laUnidad;
                }
            }
        }

        return Resultado.Correcto();
    }

    private async Task<Resultado> LaUbicacionAsync(
        Guid almacenId,
        Guid ubicacionId,
        HashSet<(Guid Almacen, Guid Ubicacion)> vistas,
        CancellationToken cancelacion)
    {
        if (!vistas.Add((almacenId, ubicacionId)))
        {
            return Resultado.Correcto();
        }

        EstadoDeMaestro estado = await ubicaciones
            .EstadoDeAsync(almacenId, ubicacionId, cancelacion)
            .ConfigureAwait(false);

        return LosMaestrosDeLaTransferencia.LaUbicacion(estado, ubicacionId, almacenId);
    }
}
