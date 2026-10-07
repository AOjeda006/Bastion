/**
 * Los rechazos del servidor que van en un campo y no arriba del formulario.
 *
 * Son los `type` de la API (ADR-0030), y el texto es el de `errores.tipos`: lo diga el esquema del
 * formulario o lo diga el servidor, el campo enseña la misma frase. El tipo literal es lo que deja
 * escribir `t(`errores.tipos.${rechazo}`)` sin un `as`: una errata aquí no compila.
 */
export const RECHAZOS_DEL_ALMACEN = [
  'recuento-ya-hay-uno-en-curso',
  'recuento-almacen-bloqueado',
  'recuento-almacen-no-encontrado',
] as const;

export const RECHAZO_DEL_MOTIVO = 'recuento-motivo-no-valido';
export const RECHAZO_DE_LO_CONTADO = 'recuento-contado-no-valido';

export type RechazoDeCampo =
  (typeof RECHAZOS_DEL_ALMACEN)[number] | typeof RECHAZO_DEL_MOTIVO | typeof RECHAZO_DE_LO_CONTADO;

const DE_CAMPO: ReadonlySet<string> = new Set<RechazoDeCampo>([
  ...RECHAZOS_DEL_ALMACEN,
  RECHAZO_DEL_MOTIVO,
  RECHAZO_DE_LO_CONTADO,
]);

export function esRechazoDeCampo(mensaje: string): mensaje is RechazoDeCampo {
  return DE_CAMPO.has(mensaje);
}

export function esDelAlmacen(tipo: string | null): boolean {
  return RECHAZOS_DEL_ALMACEN.some((rechazo) => rechazo === tipo);
}
