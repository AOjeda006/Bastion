import { z } from 'zod';

import { RECHAZO_DE_LA_FECHA, RECHAZO_DE_LA_RECEPCION, RECHAZO_DEL_MOTIVO } from './rechazos.ts';
import { hoyEnElServidor } from '@/shared/lib/dias.ts';

/**
 * Los formularios de la transferencia.
 *
 * Replican lo que el servidor rechaza sin consultar nada, y con su mismo `type` (ADR-0030): lo diga
 * el esquema o lo diga el servidor, el campo enseña la misma frase. Lo que no es un rechazo del
 * servidor —no haber escrito— lleva un aviso propio, porque el servidor nunca llega a verlo.
 */

/** Los avisos que solo dice el formulario. Son claves de `inventario.transferencias.avisos`. */
export const SIN_FECHA = 'escribeLaFecha';

export type AvisoDelFormulario = typeof SIN_FECHA;

const AVISOS: ReadonlySet<string> = new Set<AvisoDelFormulario>([SIN_FECHA]);

export function esAvisoDelFormulario(mensaje: string): mensaje is AvisoDelFormulario {
  return AVISOS.has(mensaje);
}

/** El largo máximo de un motivo, que es el de la columna (`Transferencia.LargoDelMotivo`). */
const LARGO_DEL_MOTIVO = 300;

/** El motivo de una anulación, que queda en el inverso. */
export const esquemaDeMotivo = z.object({
  motivo: z.string().trim().min(1, RECHAZO_DEL_MOTIVO).max(LARGO_DEL_MOTIVO, RECHAZO_DEL_MOTIVO),
});

export type DatosDeMotivo = z.infer<typeof esquemaDeMotivo>;

type EsquemaDeRecepcion = z.ZodObject<{ fechaDeRecepcion: z.ZodISODate }>;

/**
 * El día de la recepción de una transferencia que salió el día `envio`.
 *
 * Entre el envío y hoy, los dos incluidos: nada llega antes de salir, ni el libro recoge lo que
 * todavía no ha pasado. El día se compara como texto, porque `AAAA-MM-DD` ordena igual que el
 * calendario; y con el hoy del servidor, que es quien rechaza el de mañana.
 */
export function esquemaDeRecepcion(envio: string): EsquemaDeRecepcion {
  return z.object({
    fechaDeRecepcion: z.iso
      .date(SIN_FECHA)
      .refine((dia) => dia <= hoyEnElServidor(), RECHAZO_DE_LA_FECHA)
      .refine((dia) => dia >= envio, RECHAZO_DE_LA_RECEPCION),
  });
}

export type DatosDeRecepcion = z.infer<EsquemaDeRecepcion>;
