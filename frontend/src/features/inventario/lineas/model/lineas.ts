/**
 * Lo que comparten los documentos de líneas del inventario: el ajuste y la transferencia (ADR-0068
 * §8). El tercero, el pedido de compra, lo encontrará aquí.
 */

/** Las líneas que caben en un alta, que es el tope de una página del servidor (ADR-0065 §6). */
export const LINEAS_COMO_MUCHO = 200;

/** Cómo se ofrece un maestro en una lista: su código, y su nombre detrás si lo tiene. */
export function textoDeLaOpcion(maestro: {
  readonly codigo: string;
  readonly nombre: string | null;
}): string {
  return maestro.nombre === null ? maestro.codigo : `${maestro.codigo} · ${maestro.nombre}`;
}
