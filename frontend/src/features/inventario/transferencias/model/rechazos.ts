/**
 * Los rechazos que van en un campo, y en cuál (ADR-0068 §6).
 *
 * Son los `type` de la API (ADR-0030), y el texto es el de `errores.tipos`: lo diga el esquema del
 * formulario o lo diga el servidor, el campo enseña la misma frase. Los tipos literales son lo que
 * deja escribir `t(`errores.tipos.${rechazo}`)` sin un `as`: una errata aquí no compila.
 */

export const RECHAZO_DEL_MOTIVO = 'transferencia-motivo-no-valido';
export const RECHAZO_DE_LA_FECHA = 'transferencia-con-fecha-futura';
export const RECHAZO_DE_LA_RECEPCION = 'transferencia-recepcion-antes-del-envio';

/**
 * Los que dicen algo de la fecha de la recepción, y van en ella: los dos que también dice su
 * esquema, y los que solo sabe el servidor, que son del ejercicio en que cae y del último
 * movimiento de algún artículo en el destino. Todos se arreglan cambiando el día.
 */
const DE_LA_RECEPCION = [
  RECHAZO_DE_LA_FECHA,
  RECHAZO_DE_LA_RECEPCION,
  'transferencia-sin-ejercicio',
  'transferencia-en-ejercicio-cerrado',
  'transferencia-fecha-anterior-al-ultimo-movimiento',
] as const;

export type RechazoDeLaRecepcion = (typeof DE_LA_RECEPCION)[number];

/** En un conjunto y no leyendo un objeto: el `type` viene de la red, y un objeto hereda `constructor`. */
const RECHAZOS_DE_LA_RECEPCION: ReadonlySet<string> = new Set(DE_LA_RECEPCION);

export function esRechazoDeLaRecepcion(tipo: string | null): tipo is RechazoDeLaRecepcion {
  return tipo !== null && RECHAZOS_DE_LA_RECEPCION.has(tipo);
}
