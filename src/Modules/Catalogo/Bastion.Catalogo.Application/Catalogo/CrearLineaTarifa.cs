using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Añade una línea a un tramo de tarifa.</summary>
public interface ICrearLineaTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="tarifaId">Tramo al que se le añade la línea.</param>
    /// <param name="peticion">Datos de la línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LineaTarifaDto>> EjecutarAsync(
        Guid tarifaId,
        CrearLineaTarifaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearLineaTarifa"/>
/// <remarks>
/// <para>
/// <b>Las dos exclusividades se comprueban aquí ADEMÁS de donde de verdad viven</b>, y ninguna de
/// las dos comprobaciones sobra. La de precio o descuento vive en <see cref="PrecioODescuento"/>,
/// que lanza; la de artículo o categoría vive en un CHECK de la base. Las dos saldrían como un
/// <c>500</c> si llegaran hasta ahí desde una petición HTTP, y lo que corresponde a una petición
/// mal formada es un error de negocio con nombre: es la puerta doble del ADR-0004, escrita por los
/// dos lados a propósito.
/// </para>
/// <para>
/// <b>Y los DOS negativos del precio, no uno.</b> Que vengan los dos puestos se rechaza porque el
/// orden en que se aplicarían no lo dice nadie. Que no venga <b>ninguno</b> se rechaza porque es el
/// camino al precio cero por la puerta de atrás: una línea vacía casa con el artículo, gana la
/// precedencia y devuelve un importe de cero que nadie escribió. La condición está escrita como una
/// <b>igualdad entre los dos «hay»</b> justamente para que no pueda cubrirse una mitad y olvidarse
/// la otra.
/// </para>
/// <para>
/// <b>Las dos reglas del tramo cierran el hueco, y el hueco es peor que el solape.</b> El primer
/// tramo de cada destino tiene que empezar en <b>cero</b> —si empezara en cinco, una cantidad de
/// tres contestaría «sin línea aplicable» con el precio escrito dos filas más abajo— y dos tramos
/// del mismo destino no pueden empezar en la misma cantidad. Con eso, más una
/// <c>CantidadDesde</c> que no se modifica y líneas que no se borran, <b>todo número cae en algún
/// tramo y en uno solo</b>, sin que haya que comprobarlo en cada lectura.
/// </para>
/// </remarks>
internal sealed class CrearLineaTarifa(
    IUsuarioActual usuarioActual,
    IRepositorioDeTarifas tarifas,
    IRepositorioDeLineasDeTarifa lineas,
    IRepositorioDeArticulos articulos,
    IRepositorioDeCategorias categorias,
    IConsultaDeEmpresas empresas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : ICrearLineaTarifa
{
    public async Task<Resultado<LineaTarifaDto>> EjecutarAsync(
        Guid tarifaId,
        CrearLineaTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        // La tarifa se lee con el filtro de inquilinato puesto (R8), así que una de otra empresa
        // sale como «no encontrada» sin una rama propia aquí que se pueda olvidar.
        if (await tarifas.ObtenerAsync(tarifaId, cancelacion).ConfigureAwait(false) is null)
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeTarifa.NoEncontrada(tarifaId));
        }

        // Exactamente un destino. La igualdad entre los dos «hay» cubre las dos mitades —los dos, y
        // ninguno— en una sola condición que no se puede dejar a medias.
        if (peticion.ArticuloId is not null == (peticion.CategoriaId is not null))
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeTarifa.ArticuloOCategoria());
        }

        // Exactamente un precio o un descuento. Los DOS negativos, y el segundo es el que produce
        // el cero que nadie escribió.
        if (peticion.Precio is not null == (peticion.DescuentoPorcentaje is not null))
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeTarifa.PrecioODescuento());
        }

        Resultado destino = await ElDestinoExisteAsync(peticion, cancelacion).ConfigureAwait(false);

        if (!destino.EsCorrecto)
        {
            return Resultado.Fallo<LineaTarifaDto>(destino.Error!);
        }

        TramosDelDestino tramos = await lineas
            .TramosDelDestinoAsync(
                tarifaId,
                peticion.ArticuloId,
                peticion.CategoriaId,
                peticion.CantidadDesde,
                cancelacion)
            .ConfigureAwait(false);

        if (tramos.YaEmpiezaEnEsaCantidad)
        {
            return Resultado.Fallo<LineaTarifaDto>(
                ErroresDeTarifa.TramoDuplicado(peticion.CantidadDesde));
        }

        if (!tramos.TieneAlguno && peticion.CantidadDesde != 0m)
        {
            return Resultado.Fallo<LineaTarifaDto>(
                ErroresDeTarifa.PrimerTramoNoEmpiezaEnCero(peticion.CantidadDesde));
        }

        var precioODescuento = PrecioODescuento.De(
            peticion.Precio, peticion.DescuentoPorcentaje);

        LineaTarifa linea = peticion.ArticuloId is { } articuloId
            ? LineaTarifa.ParaArticulo(
                empresaId,
                tarifaId,
                articuloId,
                peticion.CantidadDesde,
                precioODescuento,
                reloj.GetUtcNow())
            : LineaTarifa.ParaCategoria(
                empresaId,
                tarifaId,
                peticion.CategoriaId!.Value,
                peticion.CantidadDesde,
                precioODescuento,
                reloj.GetUtcNow());

        lineas.Agregar(linea);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(linea.ADto());
    }

    // El destino es del PROPIO módulo —artículo o categoría—, así que no hay puerto que cruzar y
    // además hay clave ajena de verdad: las dos tablas están en el mismo esquema. Se comprueba
    // igual, porque el mensaje de un identificador inventado tiene que decir cuál era.
    private async Task<Resultado> ElDestinoExisteAsync(
        CrearLineaTarifaDto peticion,
        CancellationToken cancelacion)
    {
        if (peticion.ArticuloId is { } articuloId)
        {
            return await articulos.ObtenerAsync(articuloId, cancelacion).ConfigureAwait(false) is null
                ? Resultado.Fallo(ErroresDeArticulo.NoEncontrado(articuloId))
                : Resultado.Correcto();
        }

        Guid categoriaId = peticion.CategoriaId!.Value;

        return await categorias.EslabonAsync(categoriaId, cancelacion).ConfigureAwait(false) is null
            ? Resultado.Fallo(ErroresDeCategoria.NoEncontrada(categoriaId))
            : Resultado.Correcto();
    }
}
