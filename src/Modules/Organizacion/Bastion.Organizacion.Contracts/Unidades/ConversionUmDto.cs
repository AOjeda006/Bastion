using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Unidades;

/// <summary>Una conversión entre dos unidades de medida, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la conversión.</param>
/// <param name="UnidadOrigenId">Unidad desde la que se convierte.</param>
/// <param name="UnidadDestinoId">Unidad a la que se convierte.</param>
/// <param name="Factor">Por cuánto hay que multiplicar para pasar de origen a destino.</param>
/// <remarks>
/// <b>La inversa no sale sola, y es a propósito.</b> Doce unidades por caja se declara en un
/// sentido; el otro sería 1/12, que no cabe en seis decimales, y una inversa calculada convertiría
/// doce unidades en 0,999996 cajas. Si hace falta el camino de vuelta, se da de alta con el
/// factor que corresponda. Tampoco hay transitividad: de caja a palé no se deduce de caja a
/// unidad más unidad a palé.
/// </remarks>
public sealed record ConversionUmDto(
    Guid Id,
    Guid UnidadOrigenId,
    Guid UnidadDestinoId,
    decimal Factor);

/// <summary>Lo que hace falta para dar de alta una conversión.</summary>
public sealed record CrearConversionUmDto
{
    /// <summary>Unidad desde la que se convierte.</summary>
    [Required(ErrorMessage = "La unidad de origen es obligatoria.")]
    public Guid UnidadOrigenId { get; init; }

    /// <summary>Unidad a la que se convierte.</summary>
    [Required(ErrorMessage = "La unidad de destino es obligatoria.")]
    public Guid UnidadDestinoId { get; init; }

    /// <summary>Por cuánto hay que multiplicar para pasar de origen a destino.</summary>
    [Range(typeof(decimal), "0.000001", "1000000", ErrorMessage = "El factor va de {1} a {2}.")]
    public decimal Factor { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una conversión.
/// </summary>
/// <remarks>
/// Solo el factor. El par de unidades es la identidad de la fila —hay un índice único sobre él—,
/// así que cambiarlo no sería corregir esta conversión sino inventar otra.
/// </remarks>
public sealed record ModificarConversionUmDto
{
    /// <summary>Por cuánto hay que multiplicar para pasar de origen a destino.</summary>
    [Range(typeof(decimal), "0.000001", "1000000", ErrorMessage = "El factor va de {1} a {2}.")]
    public decimal Factor { get; init; }
}
