using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Lo que una línea escribe en su fila del libro: el valor que sumó o restó, y el precio medio que
/// congela (ADR-0046 §5).
/// </summary>
/// <param name="Valor">
/// El valor con signo. Una entrada suma y una salida resta, y la valoración de la clave es la suma
/// de estos importes.
/// </param>
/// <param name="PrecioMedio">
/// El precio medio que la fila congela: el de después en una entrada y el de antes en una salida,
/// que es el precio con el que se valoró.
/// </param>
public sealed record LineaValorada(Importe Valor, PrecioUnitario PrecioMedio);
