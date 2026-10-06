using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Abre el recuento de un almacén entero, con sus claves precargadas y sin contar.</summary>
public interface IAbrirRecuento
{
    /// <summary>Ejecuta el alta.</summary>
    /// <param name="peticion">Qué almacén, con qué series y por qué.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El recuento en curso, o el motivo por el que no se abre.</returns>
    Task<Resultado<RecuentoDto>> EjecutarAsync(AbrirRecuentoDto peticion, CancellationToken cancelacion);
}

/// <summary>El alta del recuento (ADR-0055 §1 y §5).</summary>
/// <remarks>
/// <para>
/// <b>El orden es el de las otras altas, de lo barato a lo caro</b>: la empresa, que es la misma
/// pregunta que da la divisa, y el motivo, que no pregunta a nadie; después el almacén y las dos
/// series; después si ya hay uno en curso, y por último las existencias del almacén entero, que es la
/// lectura grande.
/// </para>
/// <para>
/// <b>La precarga son las claves con físico mayor que cero</b>, sin contar (§5). Una clave con solo
/// tránsito no se precarga: lo que vuela no está en el almacén, y se cuenta después de recibirlo.
/// </para>
/// <para>
/// <b>Cada línea va en la unidad base de su artículo</b>, que se pregunta a Catálogo de una vez
/// (§1.2). Un artículo con existencias que Catálogo no conoce es un libro roto, no una petición mal
/// hecha, y se lanza.
/// </para>
/// <para>
/// <b>Sin transacción propia</b>: el alta escribe en un solo <c>SaveChanges</c>, que es atómico, y la
/// carrera de dos altas la para el índice único parcial, que espera a la otra transacción y la
/// traduce el borde a <c>409</c>. Con <c>Idempotency-Key</c>, la transacción es la del filtro.
/// </para>
/// </remarks>
/// <param name="usuarioActual">De dónde sale la empresa (R8).</param>
/// <param name="recuentos">Dónde se apunta el documento, y de dónde salen las existencias.</param>
/// <param name="empresas">Puerto de empresas: si está activa, y su divisa base.</param>
/// <param name="almacenes">Puerto de almacenes.</param>
/// <param name="series">Puerto de series.</param>
/// <param name="articulos">Puerto de artículos: la unidad base de cada uno.</param>
/// <param name="unidadTrabajo">Dónde se confirma.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class AbrirRecuento(
    IUsuarioActual usuarioActual,
    IRepositorioDeRecuentos recuentos,
    IConsultaDeEmpresas empresas,
    IConsultaDeAlmacenes almacenes,
    IConsultaDeSeries series,
    IConsultaDeArticulos articulos,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAbrirRecuento
{
    /// <inheritdoc/>
    public async Task<Resultado<RecuentoDto>> EjecutarAsync(
        AbrirRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Guid empresaId = usuarioActual.EmpresaId;

        // LA DIVISA DEL AJUSTE QUE SALGA ES LA BASE DE LA EMPRESA AL ABRIR (ADR-0055 §8), y la misma
        // pregunta dice si la empresa está activa.
        string? divisa = await empresas.DivisaBaseDeAsync(empresaId, cancelacion).ConfigureAwait(false);

        if (divisa is null)
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        if (string.IsNullOrWhiteSpace(peticion.Motivo) || peticion.Motivo.Trim().Length > Recuento.LargoDelMotivo)
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeRecuento.MotivoNoValido());
        }

        Resultado losMaestros = await ComprobarLosMaestrosAsync(peticion, cancelacion).ConfigureAwait(false);

        if (!losMaestros.EsCorrecto)
        {
            return Resultado.Fallo<RecuentoDto>(losMaestros.Error!);
        }

        if (await recuentos.HayUnoEnCursoAsync(peticion.AlmacenId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeRecuento.YaHayUnoEnCurso());
        }

        IReadOnlyList<ExistenciaDeUnaClave> existencias = await recuentos
            .ExistenciasAsync(peticion.AlmacenId, articulos: null, cancelacion)
            .ConfigureAwait(false);

        ExistenciaDeUnaClave[] conFisico = [.. existencias.Where(existencia => existencia.Fisico > 0m)];

        IReadOnlyDictionary<Guid, Guid> unidadesBase = await articulos
            .UnidadesBaseDeAsync([.. conFisico.Select(existencia => existencia.Clave.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        LineaAPrecargar[] precarga = [.. conFisico.Select(existencia => new LineaAPrecargar(
            existencia.Clave,
            unidadesBase.TryGetValue(existencia.Clave.ArticuloId, out Guid unidadBaseId)
                ? unidadBaseId
                : throw new InvalidOperationException(
                    $"El almacén {peticion.AlmacenId} tiene existencias del artículo " +
                    $"{existencia.Clave.ArticuloId}, y Catálogo no lo conoce en esta empresa: el libro " +
                    "apunta a un artículo que no existe.")))];

        DateTimeOffset momento = reloj.GetUtcNow();

        var recuento = Recuento.Abrir(
            empresaId,
            peticion.SerieId,
            peticion.SerieDelAjusteId,
            peticion.AlmacenId,
            DateOnly.FromDateTime(momento.UtcDateTime),
            peticion.Motivo,
            divisa,
            precarga,
            momento);

        recuentos.Agregar(recuento);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(recuento.ADto(ElTeoricoDeLasLineas.Ahora(recuento, existencias)));
    }

    private async Task<Resultado> ComprobarLosMaestrosAsync(
        AbrirRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        EstadoDeMaestro estadoDelAlmacen = await almacenes
            .EstadoDeAsync(peticion.AlmacenId, cancelacion)
            .ConfigureAwait(false);

        Resultado elAlmacen = LosMaestrosDelRecuento.ElAlmacen(estadoDelAlmacen, peticion.AlmacenId);

        if (!elAlmacen.EsCorrecto)
        {
            return elAlmacen;
        }

        (Guid SerieId, string Cual)[] lasDos =
        [
            (peticion.SerieId, "del recuento"),
            (peticion.SerieDelAjusteId, "del ajuste"),
        ];

        foreach ((Guid serieId, string cual) in lasDos)
        {
            EstadoDeMaestro estado = await series.EstadoDeAsync(serieId, cancelacion).ConfigureAwait(false);
            Resultado laSerie = LosMaestrosDelRecuento.LaSerie(estado, serieId, cual);

            if (!laSerie.EsCorrecto)
            {
                return laSerie;
            }
        }

        return Resultado.Correcto();
    }
}
