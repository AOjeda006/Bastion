using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Dice qué precio le pone una tarifa a un artículo para una cantidad y una fecha.</summary>
public interface IResolverPrecio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="codigo">Código de la tarifa, tal como lo escribieron.</param>
    /// <param name="articuloId">Artículo al que se le quiere poner precio.</param>
    /// <param name="cantidad">Cantidad por la que se pregunta.</param>
    /// <param name="fecha">Día para el que se resuelve, o nulo para hoy.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PrecioResueltoDto>> EjecutarAsync(
        string codigo,
        Guid articuloId,
        decimal cantidad,
        DateOnly? fecha,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IResolverPrecio"/>
/// <remarks>
/// <para>
/// <b>Es el camino caliente del módulo y su coste está acotado a propósito: CUATRO consultas, y
/// ninguna de las cuatro depende de la profundidad del artículo en el árbol.</b> El artículo, el
/// tramo de tarifa que rige ese día, la ascendencia de su categoría —el árbol de la empresa traído
/// plano de una vez— y las líneas candidatas. Un documento de cuarenta líneas cuesta cuarenta veces
/// eso y no cuarenta veces la profundidad: la diferencia entre 160 consultas y 600 no se ve en
/// ninguna traza de consulta lenta, porque todas son rápidas.
/// </para>
/// <para>
/// <b>Los tres fallos son tres, con tres <c>type</c> distintos, y ninguno es un cero.</b> «Esa
/// tarifa no existe» y «esa tarifa existe y ninguno de sus tramos cubre esta fecha» son dos
/// arreglos distintos —corregir el código, o abrir el tramo que falta—, igual que la unidad
/// retirada frente a la inexistente del ADR-0030. Y «la tarifa rige pero no dice nada de este
/// artículo» es la decisión 3 del ADR-0023 con otro sujeto: se devuelve un error con nombre y nunca
/// un precio cero, que se propagaría sin ruido hasta un total descuadrado semanas después.
/// </para>
/// <para>
/// <b>La precedencia no está aquí</b>, está en <see cref="ElAntepasadoMasCercanoGana"/>, y este
/// caso de uso solo le da de comer. Así la regla que de verdad se puede implementar mal —artículo
/// antes que categoría, y antepasado más cercano y no más profundo— se prueba sin base de datos y
/// se pone roja en segundos.
/// </para>
/// </remarks>
internal sealed class ResolverPrecio(
    IUsuarioActual usuarioActual,
    IRepositorioDeTarifas tarifas,
    IRepositorioDeLineasDeTarifa lineas,
    IRepositorioDeArticulos articulos,
    IRepositorioDeCategorias categorias,
    IHoyEnLaEmpresa hoyEnLaEmpresa) : IResolverPrecio
{
    public async Task<Resultado<PrecioResueltoDto>> EjecutarAsync(
        string codigo,
        Guid articuloId,
        decimal cantidad,
        DateOnly? fecha,
        CancellationToken cancelacion)
    {
        Guid empresaId = usuarioActual.EmpresaId;
        string normalizado = Tarifa.NormalizarCodigo(codigo);

        // Sin fecha, hoy en la zona de la empresa (ADR-0079). La fecha es de NEGOCIO —el día para
        // el que se quiere el precio— y por eso es `DateOnly` y no un instante (R14): un
        // presupuesto con fecha de mañana se valora con la tarifa de mañana, no con la de cuando
        // se teclea.
        DateOnly dia = fecha ?? await hoyEnLaEmpresa.DeLaEmpresaAsync(cancelacion).ConfigureAwait(false);

        Articulo? articulo = await articulos
            .ObtenerAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<PrecioResueltoDto>(ErroresDeArticulo.NoEncontrado(articuloId));
        }

        Tarifa? tarifa = await tarifas
            .VigenteAsync(empresaId, normalizado, dia, cancelacion)
            .ConfigureAwait(false);

        if (tarifa is null)
        {
            // Los dos errores se separan aquí, y la segunda consulta solo se hace por el camino de
            // fallo: quien resuelve un precio correctamente nunca llega a preguntarla.
            bool existeElCodigo = await tarifas
                .ExisteElCodigoAsync(empresaId, normalizado, cancelacion)
                .ConfigureAwait(false);

            return Resultado.Fallo<PrecioResueltoDto>(existeElCodigo
                ? ErroresDeTarifa.NoVigente(normalizado, dia)
                : ErroresDeTarifa.CodigoNoConocido(normalizado));
        }

        // Lo que queda —la ascendencia, las candidatas y la línea que gana— lo comparte con el
        // precio de compra que pide otro módulo (ADR-0077).
        PrecioResueltoDto? precio = await ElPrecioDelTramo
            .ResolverAsync(tarifa, articulo, cantidad, dia, lineas, categorias, cancelacion)
            .ConfigureAwait(false);

        return precio is null
            ? Resultado.Fallo<PrecioResueltoDto>(
                ErroresDeTarifa.SinLineaAplicable(normalizado, articuloId, cantidad))
            : Resultado.Correcto(precio);
    }
}
