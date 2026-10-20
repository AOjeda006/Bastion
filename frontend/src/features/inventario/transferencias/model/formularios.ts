import { z } from 'zod';

import {
  RECHAZO_DE_LA_CANTIDAD,
  RECHAZO_DE_LA_FECHA,
  RECHAZO_DE_LA_RECEPCION,
  RECHAZO_DEL_MISMO_ALMACEN,
  RECHAZO_DEL_MOTIVO,
} from './rechazos.ts';
import { LINEAS_COMO_MUCHO } from '../../lineas/model/lineas.ts';
import { hoyEnElServidor } from '@/shared/lib/dias.ts';

/**
 * Los formularios de la transferencia: el alta, con sus líneas, el día de la recepción y el motivo
 * de la anulación.
 *
 * Replican lo que el servidor rechaza sin consultar nada, y con su mismo `type` (ADR-0030): lo diga
 * el esquema o lo diga el servidor, el campo enseña la misma frase. Lo que no es un rechazo del
 * servidor —no haber elegido, no haber escrito— lleva un aviso propio, porque el servidor nunca
 * llega a verlo. Los nombres de los campos son los del cuerpo: la ruta de un error de `zod` y la de
 * un `400` del servidor (`lineas[2].ubicacionDestinoId`) dicen el mismo campo (ADR-0068 §6).
 */

/** Los avisos que solo dice el formulario. Son claves de `inventario.transferencias.avisos`. */
export const SIN_ORIGEN = 'eligeElOrigen';
export const SIN_DESTINO = 'eligeElDestino';
export const SIN_SERIE = 'eligeLaSerie';
export const SIN_ENVIO = 'escribeElEnvio';
export const SIN_RECEPCION = 'escribeLaRecepcion';
export const SIN_UBICACION = 'eligeLaUbicacion';
export const SIN_ARTICULO = 'eligeElArticulo';
export const SIN_UNIDAD = 'eligeLaUnidad';
export const SIN_CANTIDAD = 'escribeLaCantidad';
export const TOPE_DE_LINEAS = 'topeDeLineas';

export type AvisoDelFormulario =
  | typeof SIN_ORIGEN
  | typeof SIN_DESTINO
  | typeof SIN_SERIE
  | typeof SIN_ENVIO
  | typeof SIN_RECEPCION
  | typeof SIN_UBICACION
  | typeof SIN_ARTICULO
  | typeof SIN_UNIDAD
  | typeof SIN_CANTIDAD
  | typeof TOPE_DE_LINEAS;

const AVISOS: ReadonlySet<string> = new Set<AvisoDelFormulario>([
  SIN_ORIGEN,
  SIN_DESTINO,
  SIN_SERIE,
  SIN_ENVIO,
  SIN_RECEPCION,
  SIN_UBICACION,
  SIN_ARTICULO,
  SIN_UNIDAD,
  SIN_CANTIDAD,
  TOPE_DE_LINEAS,
]);

export function esAvisoDelFormulario(mensaje: string): mensaje is AvisoDelFormulario {
  return AVISOS.has(mensaje);
}

/** El largo máximo de un motivo, que es el de la columna (`Transferencia.LargoDelMotivo`). */
const LARGO_DEL_MOTIVO = 300;

/**
 * Lo escrito en la cantidad, ya como la cifra que viaja, o nulo si no vale.
 *
 * **Sin signo**: una transferencia lleva lo que sale del origen, y la negativa es la de su inverso,
 * que se construye anulando. Doce cifras enteras y seis decimales como mucho, y nunca cero, que es
 * lo que cabe en el libro. Esta regla la replica; la autoridad es el servidor, que además mira la
 * cantidad en la unidad base, con el factor que pone él.
 *
 * **Viaja como texto**, con punto, y no como número, como en el ajuste: un `0.1` de coma flotante
 * no es la décima que se escribió. La coma decimal se acepta; los separadores de miles, no.
 */
export function cantidadEscrita(texto: string): string | null {
  const limpio = texto.trim().replace(',', '.');

  if (!/^\d{1,12}(\.\d{1,6})?$/.test(limpio) || Number(limpio) === 0) {
    return null;
  }

  return limpio;
}

const linea = z.object({
  ubicacionOrigenId: z.string().min(1, SIN_UBICACION),
  ubicacionDestinoId: z.string().min(1, SIN_UBICACION),
  articuloId: z.string().min(1, SIN_ARTICULO),
  cantidadIntroducida: z
    .string()
    .trim()
    .min(1, SIN_CANTIDAD)
    .refine((texto) => cantidadEscrita(texto) !== null, RECHAZO_DE_LA_CANTIDAD),
  unidadIntroducidaId: z.string().min(1, SIN_UNIDAD),
  codigoDeLote: z.string().trim(),
  numeroDeSerie: z.string().trim(),
});

/** Si lo que llega al esquema trae ya los dos almacenes elegidos. */
function conLosDosAlmacenes(valor: unknown): boolean {
  if (typeof valor !== 'object' || valor === null) {
    return false;
  }

  const { almacenOrigenId, almacenDestinoId } = valor as Record<string, unknown>;

  return (
    typeof almacenOrigenId === 'string' &&
    almacenOrigenId !== '' &&
    typeof almacenDestinoId === 'string' &&
    almacenDestinoId !== ''
  );
}

export const esquemaDelAlta = z
  .object({
    almacenOrigenId: z.string().min(1, SIN_ORIGEN),
    almacenDestinoId: z.string().min(1, SIN_DESTINO),
    serieId: z.string().min(1, SIN_SERIE),
    // El día se compara como texto: `AAAA-MM-DD` ordena igual que el calendario. Y con el hoy del
    // servidor, que es quien rechaza el de mañana al enviarla.
    fechaDeEnvio: z.iso
      .date(SIN_ENVIO)
      .refine((dia) => dia <= hoyEnElServidor(), RECHAZO_DE_LA_FECHA),
    lineas: z.array(linea).max(LINEAS_COMO_MUCHO, TOPE_DE_LINEAS),
  })
  // En el destino, que es el que se elige después. Y en cuanto están los dos, aunque el resto del
  // formulario no valga todavía: `zod` no mira lo del objeto entero mientras algún campo falla.
  .refine((alta) => alta.almacenOrigenId !== alta.almacenDestinoId, {
    message: RECHAZO_DEL_MISMO_ALMACEN,
    path: ['almacenDestinoId'],
    when: ({ value }) => conLosDosAlmacenes(value),
  });

export type DatosDelAlta = z.infer<typeof esquemaDelAlta>;
export type LineaDelFormulario = DatosDelAlta['lineas'][number];

/** Una línea por escribir: es lo que trae el alta al abrirse y lo que añade cada Intro. */
export const LINEA_EN_BLANCO: LineaDelFormulario = {
  ubicacionOrigenId: '',
  ubicacionDestinoId: '',
  articuloId: '',
  cantidadIntroducida: '',
  unidadIntroducidaId: '',
  codigoDeLote: '',
  numeroDeSerie: '',
};

/** Un campo de una línea, con el nombre que le da `react-hook-form`. */
export type RutaDeLaLinea<C extends keyof LineaDelFormulario> = `lineas.${number}.${C}`;

/**
 * El nombre del campo `campo` de la línea `indice` en el formulario.
 *
 * `react-hook-form` lo tipa con un `${number}`, y ningún `String()` lo da: la conversión se afirma
 * aquí, una vez, y no en cada celda.
 */
export function rutaDeLaLinea<C extends keyof LineaDelFormulario>(
  indice: number,
  campo: C,
): RutaDeLaLinea<C> {
  return `lineas.${String(indice)}.${campo}` as RutaDeLaLinea<C>;
}

/** Una línea como viaja: la cantidad, como texto con punto, y lo vacío, nulo. */
export interface LineaDelAlta {
  readonly ubicacionOrigenId: string;
  readonly ubicacionDestinoId: string;
  readonly articuloId: string;
  readonly cantidadIntroducida: string;
  readonly unidadIntroducidaId: string;
  readonly codigoDeLote: string | null;
  readonly numeroDeSerie: string | null;
}

/** El alta como viaja. El factor no va: lo pone el servidor (ADR-0068 §2). */
export interface AltaDeTransferencia {
  readonly serieId: string;
  readonly almacenOrigenId: string;
  readonly almacenDestinoId: string;
  readonly fechaDeEnvio: string;
  readonly lineas: readonly LineaDelAlta[];
}

function oNulo(texto: string): string | null {
  return texto === '' ? null : texto;
}

/**
 * Lo que viaja, de lo que pasó el esquema. Con las mismas reglas que lo validaron: lo que dice la
 * pantalla que vale es exactamente lo que se manda.
 */
export function altaDe(datos: DatosDelAlta): AltaDeTransferencia {
  return {
    serieId: datos.serieId,
    almacenOrigenId: datos.almacenOrigenId,
    almacenDestinoId: datos.almacenDestinoId,
    fechaDeEnvio: datos.fechaDeEnvio,
    lineas: datos.lineas.map((escrita) => ({
      ubicacionOrigenId: escrita.ubicacionOrigenId,
      ubicacionDestinoId: escrita.ubicacionDestinoId,
      articuloId: escrita.articuloId,
      cantidadIntroducida:
        cantidadEscrita(escrita.cantidadIntroducida) ?? escrita.cantidadIntroducida,
      unidadIntroducidaId: escrita.unidadIntroducidaId,
      codigoDeLote: oNulo(escrita.codigoDeLote),
      numeroDeSerie: oNulo(escrita.numeroDeSerie),
    })),
  };
}

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
      .date(SIN_RECEPCION)
      .refine((dia) => dia <= hoyEnElServidor(), RECHAZO_DE_LA_FECHA)
      .refine((dia) => dia >= envio, RECHAZO_DE_LA_RECEPCION),
  });
}

export type DatosDeRecepcion = z.infer<EsquemaDeRecepcion>;
