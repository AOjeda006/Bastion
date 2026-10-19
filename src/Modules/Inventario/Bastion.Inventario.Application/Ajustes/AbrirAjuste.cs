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
/// <para>
/// <b>El factor de cada línea lo pone el servidor</b> (ADR-0065 §2), después de los maestros: 1 si
/// la unidad es la base del artículo, y si no, el de su conversión de Organización. La serie
/// unitaria se mira entonces, en una segunda pasada de la forma, porque antes no se sabe cuántas
/// unidades base mueve la línea.
/// </para>
/// <para>
/// <b>Lo que no cabe en su columna es un <c>400</c></b>: el motivo, la cantidad, la cantidad base y el
/// coste. Hasta el ítem 2.17 lo decía el dominio, que lanza, o la base al guardar, y el alta
/// contestaba un <c>500</c>. La regla es la del dominio (<see cref="LineaDeAjuste.LaCantidadCabe"/>,
/// <see cref="LineaDeAjuste.SePuedeAjustar"/>, <see cref="LineaDeAjuste.ElCosteCabe"/>), preguntada
/// antes.
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
/// <param name="conversiones">Puerto de las conversiones entre unidades (ítem 2.17).</param>
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
    IConsultaDeConversiones conversiones,
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

        if (string.IsNullOrWhiteSpace(peticion.Motivo) || peticion.Motivo.Trim().Length > Ajuste.LargoDelMotivo)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.MotivoDelAltaNoValido());
        }

        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.SinLineas(Guid.Empty));
        }

        // LA CANTIDAD, ANTES QUE EL COSTE Y LA FORMA: lo escrito se dice sin preguntar a nadie. Lo
        // que mueve en la unidad base espera al factor.
        if (LaPrimeraQueNoCabe(peticion.Lineas, (linea, _) => LineaDeAjuste.LaCantidadCabe(linea.CantidadIntroducida))
            is { } sinCantidad)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.CantidadNoValida(sinCantidad));
        }

        if (peticion.Lineas.Any(linea =>
            linea.CosteUnitario is { } coste && (linea.CantidadIntroducida < 0m || !LineaDeAjuste.ElCosteCabe(coste))))
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.CosteNoValido());
        }

        // LA FORMA, EN SU PRIMERA PASADA: lo que se dice sin preguntar a nadie. La serie unitaria
        // espera al factor, y va en la segunda (ADR-0065 §2).
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

        Resultado<IReadOnlyList<decimal>> losFactores = await ResolverLosFactoresAsync(peticion.Lineas, cancelacion)
            .ConfigureAwait(false);

        if (!losFactores.EsCorrecto)
        {
            return Resultado.Fallo<AjusteDto>(losFactores.Error!);
        }

        IReadOnlyList<decimal> factores = losFactores.Valor;

        if (LaPrimeraQueNoCabe(
                peticion.Lineas,
                (linea, indice) => LineaDeAjuste.SePuedeAjustar(linea.CantidadIntroducida, factores[indice]))
            is { } sinCantidadBase)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.CantidadNoValida(sinCantidadBase));
        }

        // LA FORMA, EN SU SEGUNDA PASADA: con el factor puesto. Lo demás ya pasó en la primera, así
        // que lo único que puede salir aquí es una serie que no mueve una unidad base.
        conForma = [.. conForma.Select((linea, indice) => linea with { FactorAUnidadBase = factores[indice] })];

        if (LaTrazabilidadDeLasLineas.LoQueNoTieneForma(conForma) is { } noUnitaria)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.DeLaForma(noUnitaria));
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

        for (int indice = 0; indice < peticion.Lineas.Count; indice++)
        {
            LineaDeAjusteDto linea = peticion.Lineas[indice];

            ajuste.AnadirLinea(
                linea.UbicacionId,
                linea.ArticuloId,
                linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                factores[indice],
                linea.CosteUnitario,
                momento,
                linea.CodigoDeLote,
                linea.NumeroDeSerie);
        }

        ajustes.Agregar(ajuste);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(Mapeos.ADto(ajuste));
    }

    /// <summary>La primera línea que no cumple la regla, por su orden.</summary>
    /// <param name="lineas">Las líneas de la petición.</param>
    /// <param name="cabe">La regla, con la línea y su índice desde cero.</param>
    /// <returns>Su número, desde uno, o <c>null</c> si todas la cumplen.</returns>
    private static int? LaPrimeraQueNoCabe(
        IReadOnlyList<LineaDeAjusteDto> lineas,
        Func<LineaDeAjusteDto, int, bool> cabe)
    {
        for (int indice = 0; indice < lineas.Count; indice++)
        {
            if (!cabe(lineas[indice], indice))
            {
                return indice + 1;
            }
        }

        return null;
    }

    private async Task<Resultado> ComprobarLasLineasAsync(
        AbrirAjusteDto peticion,
        CancellationToken cancelacion)
    {
        HashSet<Guid> ubicacionesVistas = [];
        HashSet<Guid> articulosVistos = [];
        HashSet<Guid> unidadesVistas = [];

        for (int indice = 0; indice < peticion.Lineas.Count; indice++)
        {
            LineaDeAjusteDto linea = peticion.Lineas[indice];
            int numero = indice + 1;

            if (ubicacionesVistas.Add(linea.UbicacionId))
            {
                EstadoDeMaestro estado = await ubicaciones
                    .EstadoDeAsync(peticion.AlmacenId, linea.UbicacionId, cancelacion)
                    .ConfigureAwait(false);

                Resultado laUbicacion = LosMaestrosDelAjuste.LaUbicacion(estado, linea.UbicacionId, numero);

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

                Resultado elArticulo = LosMaestrosDelAjuste.ElArticulo(aptitud, linea.ArticuloId, numero);

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

                Resultado laUnidad = LosMaestrosDelAjuste.LaUnidad(estado, linea.UnidadIntroducidaId, numero);

                if (!laUnidad.EsCorrecto)
                {
                    return laUnidad;
                }
            }
        }

        return Resultado.Correcto();
    }

    /// <summary>El factor de cada línea a la unidad base de su artículo (ADR-0065 §2).</summary>
    /// <remarks>
    /// <para>
    /// <b>Dos consultas como mucho, sea cual sea el número de líneas</b>: las unidades base de todos
    /// los artículos, y las conversiones de todos los pares distintos. Una línea en la unidad base no
    /// pregunta nada, porque una conversión de una unidad a sí misma no puede existir.
    /// </para>
    /// <para>
    /// <b>Contesta la primera línea que falla, por su orden</b>, le falte la conversión o la tenga
    /// retirada: el número que sale es el de la línea que hay que corregir primero.
    /// </para>
    /// </remarks>
    /// <param name="lineas">Las líneas de la petición, con sus maestros ya comprobados.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El factor de cada línea, en su orden, o el motivo por el que una no lo tiene.</returns>
    /// <exception cref="InvalidOperationException">
    /// Falta la unidad base de un artículo: los artículos no se borran, y el alta ya comprobó que
    /// existía.
    /// </exception>
    private async Task<Resultado<IReadOnlyList<decimal>>> ResolverLosFactoresAsync(
        IReadOnlyList<LineaDeAjusteDto> lineas,
        CancellationToken cancelacion)
    {
        IReadOnlyDictionary<Guid, Guid> unidadesBase = await articulos
            .UnidadesBaseDeAsync([.. lineas.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        ParDeUnidades[] pares =
        [
            .. lineas.Select((linea, indice) => new ParDeUnidades(
                linea.UnidadIntroducidaId,
                unidadesBase.TryGetValue(linea.ArticuloId, out Guid unidadBaseId)
                    ? unidadBaseId
                    : throw new InvalidOperationException(
                        $"El artículo {linea.ArticuloId} de la línea {indice + 1} no tiene unidad base: el " +
                        "alta comprobó que existía y un artículo no se borra, así que o el puerto no lo " +
                        "leyó o no es de esta empresa.")))
        ];

        ParDeUnidades[] porPreguntar = [.. pares.Where(par => par.OrigenId != par.DestinoId).Distinct()];

        IReadOnlyDictionary<ParDeUnidades, ConversionResuelta> resueltas = porPreguntar.Length == 0
            ? new Dictionary<ParDeUnidades, ConversionResuelta>()
            : await conversiones.DeLosParesAsync(porPreguntar, cancelacion).ConfigureAwait(false);

        decimal[] factores = new decimal[pares.Length];

        for (int indice = 0; indice < pares.Length; indice++)
        {
            ParDeUnidades par = pares[indice];

            if (par.OrigenId == par.DestinoId)
            {
                factores[indice] = 1m;
            }
            else if (!resueltas.TryGetValue(par, out ConversionResuelta? conversion))
            {
                return Resultado.Fallo<IReadOnlyList<decimal>>(ErroresDeAjuste.SinConversion(indice + 1, par));
            }
            else if (conversion.EstaRetirada)
            {
                return Resultado.Fallo<IReadOnlyList<decimal>>(ErroresDeAjuste.ConversionRetirada(indice + 1, par));
            }
            else
            {
                factores[indice] = conversion.Factor;
            }
        }

        return Resultado.Correcto<IReadOnlyList<decimal>>(factores);
    }
}
