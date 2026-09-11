using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Una línea de tarifa, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la línea.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="TarifaId">Tramo de tarifa del que cuelga.</param>
/// <param name="ArticuloId">Artículo al que se aplica, o nulo si la línea es de categoría.</param>
/// <param name="CategoriaId">Categoría a la que se aplica, o nulo si la línea es de artículo.</param>
/// <param name="CantidadDesde">
/// Cantidad a partir de la cual se aplica, <b>incluida</b>. El tramo llega hasta donde empieza el
/// siguiente, sin incluirlo: pedir exactamente 100 con tramos en 0 y en 100 aplica el de 100.
/// </param>
/// <param name="Precio">Precio por unidad en la divisa de la tarifa, o nulo si es un descuento.</param>
/// <param name="DescuentoPorcentaje">Descuento en tanto por ciento, o nulo si es un precio.</param>
public sealed record LineaTarifaDto(
    Guid Id,
    Guid EmpresaId,
    Guid TarifaId,
    Guid? ArticuloId,
    Guid? CategoriaId,
    decimal CantidadDesde,
    decimal? Precio,
    decimal? DescuentoPorcentaje);

/// <summary>Lo que hace falta para añadir una línea a un tramo de tarifa.</summary>
/// <remarks>
/// <b>No lleva empresa ni tarifa</b>: la primera sale del <i>claim</i> (R8) y la segunda, de la
/// ruta.
/// </remarks>
public sealed record CrearLineaTarifaDto
{
    /// <summary>Artículo al que se aplica. Exactamente uno de los dos destinos.</summary>
    public Guid? ArticuloId { get; init; }

    /// <summary>Categoría a la que se aplica, y con ella todo lo que cuelgue. Exactamente uno.</summary>
    public Guid? CategoriaId { get; init; }

    /// <summary>
    /// Cantidad a partir de la cual se aplica, incluida.
    /// </summary>
    /// <remarks>
    /// <b>La primera línea de cada destino tiene que empezar en cero</b>, y es lo que hace
    /// imposible el hueco: con el cero puesto y los tramos llegando hasta donde empieza el
    /// siguiente, toda cantidad cae en alguno. Sin él, una tarifa cuyo primer tramo empezara en
    /// cinco diría «sin tarifa aplicable» para una cantidad de tres, con el precio escrito dos
    /// líneas más abajo.
    /// </remarks>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "La cantidad desde la que se aplica no puede ser negativa.")]
    public decimal CantidadDesde { get; init; }

    /// <summary>
    /// Precio por unidad en la divisa de la tarifa. Uno de los dos, no los dos, y no ninguno.
    /// </summary>
    /// <remarks>
    /// <b>El cero es válido</b> y el rango empieza ahí a propósito: una muestra comercial o un
    /// artículo de regalo se factura a cero <b>escrito por alguien</b>. Lo que este ítem prohíbe es
    /// el cero que nadie escribió, que es el que sale de una línea sin precio y sin descuento.
    /// </remarks>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El precio de una línea de tarifa no puede ser negativo.")]
    public decimal? Precio { get; init; }

    /// <summary>Descuento en tanto por ciento. Uno de los dos, no los dos, y no ninguno.</summary>
    [Range(typeof(decimal), "0", "100",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El descuento va del 0 al 100 por ciento.")]
    public decimal? DescuentoPorcentaje { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una línea de tarifa: el precio o el descuento.
/// </summary>
/// <remarks>
/// <b>Ni el destino ni la cantidad desde la que se aplica.</b> Lo segundo es lo que cierra el hueco
/// del todo: si el tramo que empieza en cero pudiera moverse a cinco, una cantidad de tres se
/// quedaría sin tramo sin que nadie hubiera borrado nada.
/// </remarks>
public sealed record ModificarLineaTarifaDto
{
    /// <summary>
    /// Precio por unidad en la divisa de la tarifa. Uno de los dos, no los dos, y no ninguno.
    /// </summary>
    /// <remarks>
    /// <b>El cero es válido</b> y el rango empieza ahí a propósito: una muestra comercial o un
    /// artículo de regalo se factura a cero <b>escrito por alguien</b>. Lo que este ítem prohíbe es
    /// el cero que nadie escribió, que es el que sale de una línea sin precio y sin descuento.
    /// </remarks>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El precio de una línea de tarifa no puede ser negativo.")]
    public decimal? Precio { get; init; }

    /// <summary>Descuento en tanto por ciento. Uno de los dos, no los dos, y no ninguno.</summary>
    [Range(typeof(decimal), "0", "100",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El descuento va del 0 al 100 por ciento.")]
    public decimal? DescuentoPorcentaje { get; init; }
}
