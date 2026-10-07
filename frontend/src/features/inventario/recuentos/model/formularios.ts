import { z } from 'zod';

import { contadoEscrito } from './cantidad.ts';
import { RECHAZO_DE_LO_CONTADO, RECHAZO_DEL_MOTIVO } from './rechazos.ts';

/**
 * Los tres formularios del recuento: abrirlo, contar una línea y cerrar con un motivo.
 *
 * Replican lo que el servidor rechaza sin consultar nada, y con su mismo `type` (ADR-0030): lo diga
 * el esquema o lo diga el servidor, el campo enseña la misma frase. Lo que no es un rechazo del
 * servidor —no haber elegido, no haber escrito— lleva un aviso propio, porque el servidor nunca
 * llega a verlo.
 */

/** Los avisos que solo dice el formulario. Son claves del diccionario de la funcionalidad. */
export const SIN_ALMACEN = 'eligeElAlmacen';
export const SIN_SERIE = 'eligeLaSerie';
export const SIN_CONTADO = 'escribeLoContado';

/** El largo máximo de un motivo, que es el de la columna. */
const LARGO_DEL_MOTIVO = 300;

const motivo = z
  .string()
  .trim()
  .min(1, RECHAZO_DEL_MOTIVO)
  .max(LARGO_DEL_MOTIVO, RECHAZO_DEL_MOTIVO);

export const esquemaDeApertura = z.object({
  almacenId: z.string().min(1, SIN_ALMACEN),
  serieId: z.string().min(1, SIN_SERIE),
  serieDelAjusteId: z.string().min(1, SIN_SERIE),
  motivo,
});

export type DatosDeApertura = z.infer<typeof esquemaDeApertura>;

/** El motivo de una anulación o de un descarte. */
export const esquemaDeMotivo = z.object({ motivo });

export type DatosDeMotivo = z.infer<typeof esquemaDeMotivo>;

/**
 * Lo contado en una línea. Hay dos, porque un número de serie es una pieza y se cuenta 0 o 1.
 *
 * **Vacío no es cero** (ADR-0055 §6): el campo vacío pide que se escriba algo, y el cero se escribe.
 * Lo escrito se valida con la misma regla que lo convierte en lo que viaja.
 */
function contado(esUnaSerie: boolean): z.ZodString {
  return z
    .string()
    .trim()
    .min(1, SIN_CONTADO)
    .refine((texto) => contadoEscrito(texto, esUnaSerie) !== null, RECHAZO_DE_LO_CONTADO);
}

export const esquemaDeContado = z.object({ contado: contado(false) });
export const esquemaDeContadoDeUnaSerie = z.object({ contado: contado(true) });

export type DatosDeContado = z.infer<typeof esquemaDeContado>;
