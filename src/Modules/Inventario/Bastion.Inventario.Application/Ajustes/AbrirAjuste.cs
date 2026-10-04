using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Abre un ajuste de existencias en borrador.</summary>
public interface IAbrirAjuste
{
    /// <summary>Ejecuta el alta.</summary>
    /// <param name="peticion">Lo que se quiere ajustar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El ajuste en borrador, o el motivo por el que no se abre.</returns>
    Task<Resultado<AjusteDto>> EjecutarAsync(AbrirAjusteDto peticion, CancellationToken cancelacion);
}

/// <summary>
/// El alta del ajuste, y el sitio donde los cuatro puertos dejan de ser decorativos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ni el almacén, ni la ubicación, ni el artículo, ni la unidad tienen clave ajena.</b> Viven en
/// los esquemas de Organización y Catálogo, y ninguna clave ajena cruza de esquema (§5, regla 4).
/// Sin estas preguntas las cuatro columnas aceptarían cualquier <c>uuid</c>: compilaría, migraría y
/// serviría peticiones, y el fallo aparecería al valorar el inventario, fases después. Es la cuarta
/// vía del ADR-0024.
/// </para>
/// <para>
/// <b>El alta solo admite <c>SeOfreceParaLoNuevo</c>.</b> Un almacén bloqueado contesta
/// <c>SoloResuelveLoViejo</c> y aquí eso es un «no»: lo que el artículo 32 reserva es la privacidad
/// de una persona, y un almacén bloqueado no admite operaciones nuevas. Lo que sí sigue pasando es
/// que los movimientos ya escritos contra él <b>se leen</b>, y eso lo enseña
/// <see cref="MovimientosDelDocumento"/> con el mismo puerto (ADR-0037).
/// </para>
/// <para>
/// <b>Se pregunta por cada línea y no una vez por documento.</b> Dos líneas pueden apuntar a
/// ubicaciones distintas del mismo almacén y a artículos distintos; preguntar una vez y suponer el
/// resto sería exactamente el atajo que deja entrar el identificador que nadie miró. Lo que no se
/// repite es la pregunta idéntica: los artículos y las unidades se preguntan una vez por
/// identificador distinto.
/// </para>
/// </remarks>
/// <param name="usuarioActual">De dónde sale la empresa (R8).</param>
/// <param name="ajustes">Dónde se apunta el documento.</param>
/// <param name="empresas">Puerto de empresas: si está activa, y su divisa base.</param>
/// <param name="almacenes">Puerto de almacenes (ítem 2.2).</param>
/// <param name="series">Puerto de series (ítem 2.4).</param>
/// <param name="ubicaciones">Puerto de ubicaciones (ítem 2.2).</param>
/// <param name="articulos">Puerto de artículos (ítem 2.2).</param>
/// <param name="trazabilidad">La marca de los artículos, sin cerrojo: la cortesía (ADR-0048 §4).</param>
/// <param name="unidades">Puerto de unidades de medida.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class AbrirAjuste(
    IUsuarioActual usuarioActual,
    IRepositorioDeAjustes ajustes,
    IConsultaDeEmpresas empresas,
    IConsultaDeAlmacenes almacenes,
    IConsultaDeSeries series,
    IConsultaDeUbicaciones ubicaciones,
    IConsultaDeArticulos articulos,
    IConsultaDeTrazabilidad trazabilidad,
    IConsultaDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAbrirAjuste
{
    /// <inheritdoc/>
    public async Task<Resultado<AjusteDto>> EjecutarAsync(
        AbrirAjusteDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Guid empresaId = usuarioActual.EmpresaId;

        // LA DIVISA DEL DOCUMENTO ES LA BASE DE LA EMPRESA (ADR-0046 §7), y la misma pregunta dice
        // si está activa: nula es que no existe o está bloqueada.
        string? divisa = await empresas.DivisaBaseDeAsync(empresaId, cancelacion).ConfigureAwait(false);

        if (divisa is null)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.SinLineas(Guid.Empty));
        }

        if (peticion.Lineas.Any(linea =>
            linea.CosteUnitario is { } coste && (linea.CantidadIntroducida < 0m || coste < 0m)))
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.CosteNoValido());
        }

        IReadOnlyList<LineaConForma> conForma = LaTrazabilidadDeLasLineas.DeLaPeticion(peticion.Lineas);

        if (LaTrazabilidadDeLasLineas.LoQueNoTieneForma(conForma) is { } sinForma)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.DeLaForma(sinForma));
        }

        EstadoDeMaestro estadoDelAlmacen = await almacenes
            .EstadoDeAsync(peticion.AlmacenId, cancelacion)
            .ConfigureAwait(false);

        Resultado elAlmacen = LosMaestrosDelAjuste.ElAlmacen(estadoDelAlmacen, peticion.AlmacenId);

        if (!elAlmacen.EsCorrecto)
        {
            return Resultado.Fallo<AjusteDto>(elAlmacen.Error!);
        }

        EstadoDeMaestro estadoDeLaSerie = await series
            .EstadoDeAsync(peticion.SerieId, cancelacion)
            .ConfigureAwait(false);

        Resultado laSerie = LosMaestrosDelAjuste.LaSerie(estadoDeLaSerie, peticion.SerieId);

        if (!laSerie.EsCorrecto)
        {
            return Resultado.Fallo<AjusteDto>(laSerie.Error!);
        }

        Resultado lasLineas = await ComprobarLasLineasAsync(peticion, cancelacion).ConfigureAwait(false);

        if (!lasLineas.EsCorrecto)
        {
            return Resultado.Fallo<AjusteDto>(lasLineas.Error!);
        }

        // LA MARCA, SIN CERROJO: es la cortesía, y la guarda es la de confirmar (ADR-0048 §4). Va
        // después de los maestros, porque solo un artículo que existe tiene marca.
        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasDeAsync([.. peticion.Lineas.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        if (LaTrazabilidadDeLasLineas.LoQueNoCasa(
                LaTrazabilidadDeLasLineas.SusCodigos(conForma), marcas) is { } noCasa)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.TrazabilidadNoCasa(noCasa));
        }

        DateTimeOffset momento = reloj.GetUtcNow();

        var ajuste = Ajuste.Abrir(
            empresaId,
            peticion.SerieId,
            peticion.AlmacenId,
            peticion.FechaDeOperacion,
            peticion.Motivo,
            divisa,
            momento);

        foreach (LineaDeAjusteDto linea in peticion.Lineas)
        {
            ajuste.AnadirLinea(
                linea.UbicacionId,
                linea.ArticuloId,
                linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                linea.FactorAUnidadBase,
                linea.CosteUnitario,
                momento,
                linea.CodigoDeLote,
                linea.NumeroDeSerie);
        }

        ajustes.Agregar(ajuste);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(Mapeos.ADto(ajuste));
    }

    private async Task<Resultado> ComprobarLasLineasAsync(
        AbrirAjusteDto peticion,
        CancellationToken cancelacion)
    {
        HashSet<Guid> ubicacionesVistas = [];
        HashSet<Guid> articulosVistos = [];
        HashSet<Guid> unidadesVistas = [];

        foreach (LineaDeAjusteDto linea in peticion.Lineas)
        {
            if (ubicacionesVistas.Add(linea.UbicacionId))
            {
                EstadoDeMaestro estado = await ubicaciones
                    .EstadoDeAsync(peticion.AlmacenId, linea.UbicacionId, cancelacion)
                    .ConfigureAwait(false);

                Resultado laUbicacion = LosMaestrosDelAjuste.LaUbicacion(estado, linea.UbicacionId);

                if (!laUbicacion.EsCorrecto)
                {
                    return laUbicacion;
                }
            }

            if (articulosVistos.Add(linea.ArticuloId))
            {
                AptitudParaMoverExistencias aptitud = await articulos
                    .AptitudDeAsync(linea.ArticuloId, cancelacion)
                    .ConfigureAwait(false);

                Resultado elArticulo = LosMaestrosDelAjuste.ElArticulo(aptitud, linea.ArticuloId);

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

                Resultado laUnidad = LosMaestrosDelAjuste.LaUnidad(estado, linea.UnidadIntroducidaId);

                if (!laUnidad.EsCorrecto)
                {
                    return laUnidad;
                }
            }
        }

        return Resultado.Correcto();
    }
}
