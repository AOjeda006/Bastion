using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Catalogo.Application.Catalogo;

/// <inheritdoc cref="IConsultaDePrecios"/>
/// <remarks>
/// <para>
/// <b>Vive en la aplicación y no en la infraestructura, al revés que los otros puertos del
/// módulo.</b> <c>ConsultaDeTarifas</c> y <c>ConsultaDeArticulos</c> son una consulta cada uno, y
/// los escribe quien tiene el contexto. Este es la precedencia de las líneas —el artículo antes que
/// la categoría, y el antepasado más cercano— sobre los mismos repositorios que usa el precio por
/// HTTP, y se prueba sin base en <c>Catalogo.UnitTests</c>, motivo por motivo (ADR-0077). Se
/// registra con los otros puertos en <c>ModuloDeCatalogo</c>, que ve lo interno de esta capa.
/// </para>
/// <para>
/// <b>Seis consultas en el camino que propone</b>: el tramo que lleva el tercero, el de su código
/// que rige ese día, la divisa base de la empresa, el artículo, su ascendencia y las candidatas.
/// Cada motivo para en cuanto se sabe, así que lo que no propone cuesta menos.
/// </para>
/// <para>
/// <b>Va por el filtro de la empresa</b>, como todo repositorio del módulo: un tramo o un artículo
/// de otra empresa no se encuentra, y contesta lo mismo que uno inventado (R8).
/// </para>
/// </remarks>
internal sealed class ConsultaDePrecios(
    IRepositorioDeTarifas tarifas,
    IRepositorioDeLineasDeTarifa lineas,
    IRepositorioDeArticulos articulos,
    IRepositorioDeCategorias categorias,
    IConsultaDeEmpresas empresas) : IConsultaDePrecios
{
    public async Task<PrecioPropuesto> DeCompraAsync(
        Guid tarifaId,
        Guid articuloId,
        decimal cantidad,
        DateOnly fecha,
        CancellationToken cancelacion)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);

        Tarifa? asignado = await tarifas.ObtenerAsync(tarifaId, cancelacion).ConfigureAwait(false);

        if (asignado is null)
        {
            return PrecioPropuesto.Sin(PropuestaDePrecio.NoExisteLaTarifa);
        }

        // El tipo es del código y no del tramo: la base no deja que un código tenga tramos de los
        // dos tipos (ADR-0076). El del tramo asignado es, por tanto, el del que rija.
        if (asignado.Tipo != TipoDeTarifa.Compra)
        {
            return PrecioPropuesto.Sin(PropuestaDePrecio.EsDeVenta);
        }

        // EL TRAMO QUE RIGE ESE DÍA, del mismo código. El tercero guarda el que se le asignó, y ese
        // puede haber caducado y tener sucesor: el precio es el del sucesor, como el de
        // `GET /tarifas/{codigo}/precio`, y no el de un tramo que ya no rige.
        Tarifa? vigente = await tarifas
            .VigenteAsync(asignado.EmpresaId, asignado.Codigo, fecha, cancelacion)
            .ConfigureAwait(false);

        if (vigente is null)
        {
            return PrecioPropuesto.Sin(PropuestaDePrecio.NoRigeEseDia);
        }

        // LA DIVISA, comparada por su código ISO 4217, que es como la guardan las dos desde el 3.3
        // (ADR-0076): el invariante 8, desarmado. Nula —la empresa está bloqueada— no es la divisa
        // de ningún tramo, así que tampoco propone.
        string? divisaBase = await empresas
            .DivisaBaseDeAsync(vigente.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        if (!string.Equals(vigente.Divisa, divisaBase, StringComparison.Ordinal))
        {
            return PrecioPropuesto.Sin(PropuestaDePrecio.NoEstaEnLaDivisaBase);
        }

        Articulo? articulo = await articulos.ObtenerAsync(articuloId, cancelacion).ConfigureAwait(false);

        if (articulo is null)
        {
            return PrecioPropuesto.Sin(PropuestaDePrecio.NoExisteElArticulo);
        }

        PrecioResueltoDto? precio = await ElPrecioDelTramo
            .ResolverAsync(vigente, articulo, cantidad, fecha, lineas, categorias, cancelacion)
            .ConfigureAwait(false);

        return precio is null
            ? PrecioPropuesto.Sin(PropuestaDePrecio.SinLineaParaElArticulo)
            : PrecioPropuesto.Con(precio);
    }
}
