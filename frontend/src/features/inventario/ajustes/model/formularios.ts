import { z } from 'zod';

import {
  RECHAZO_DE_LA_CANTIDAD,
  RECHAZO_DE_LA_FECHA,
  RECHAZO_DEL_COSTE,
  RECHAZO_DEL_MOTIVO,
} from './rechazos.ts';
import { LINEAS_COMO_MUCHO } from '../../lineas/model/lineas.ts';
import { hoyEnElServidor } from '@/shared/lib/dias.ts';

/**
 * Los dos formularios del ajuste: el alta, con sus líneas, y el motivo de la anulación.
 *
 * Replican lo que el servidor rechaza sin consultar nada, y con su mismo `type` (ADR-0030): lo diga
 * el esquema o lo diga el servidor, el campo enseña la misma frase. Lo que no es un rechazo del
 * servidor —no haber elegido, no haber escrito— lleva un aviso propio, porque el servidor nunca
 * llega a verlo. Los nombres de los campos son los del cuerpo: la ruta de un error de `zod` y la de
 * un `400` del servidor (`lineas[2].numeroDeSerie`) dicen el mismo campo (ADR-0065 §3).
 */

/** Los avisos que solo dice el formulario. Son claves del diccionario de la funcionalidad. */
export const SIN_ALMACEN = 'eligeElAlmacen';
export const SIN_SERIE = 'eligeLaSerie';
export const SIN_FECHA = 'escribeLaFecha';
export const SIN_UBICACION = 'eligeLaUbicacion';
export const SIN_ARTICULO = 'eligeElArticulo';
export const SIN_UNIDAD = 'eligeLaUnidad';
export const SIN_CANTIDAD = 'escribeLaCantidad';
export const COSTE_MAL_ESCRITO = 'costeMalEscrito';
export const TOPE_DE_LINEAS = 'topeDeLineas';

export type AvisoDelFormulario =
  | typeof SIN_ALMACEN
  | typeof SIN_SERIE
  | typeof SIN_FECHA
  | typeof SIN_UBICACION
  | typeof SIN_ARTICULO
  | typeof SIN_UNIDAD
  | typeof SIN_CANTIDAD
  | typeof COSTE_MAL_ESCRITO
  | typeof TOPE_DE_LINEAS;

const AVISOS: ReadonlySet<string> = new Set<AvisoDelFormulario>([
  SIN_ALMACEN,
  SIN_SERIE,
  SIN_FECHA,
  SIN_UBICACION,
  SIN_ARTICULO,
  SIN_UNIDAD,
  SIN_CANTIDAD,
  COSTE_MAL_ESCRITO,
  TOPE_DE_LINEAS,
]);

export function esAvisoDelFormulario(mensaje: string): mensaje is AvisoDelFormulario {
  return AVISOS.has(mensaje);
}

/** El largo máximo de un motivo, que es el de la columna. */
const LARGO_DEL_MOTIVO = 300;

/**
 * Lo escrito en la cantidad, ya como la cifra que viaja, o nulo si no vale.
 *
 * **Con su signo**: lo que entra, en positivo, y lo que sale, en negativo. Doce cifras enteras y
 * seis decimales como mucho, y nunca cero, que es lo que cabe en el libro
 * (`LineaDeAjuste.LaCantidadCabe`). Esta regla la replica; la autoridad es el servidor, que además
 * mira la cantidad en la unidad base, con el factor que pone él.
 *
 * **Viaja como texto**, con punto, y no como número, como lo contado del recuento: un `0.1` de coma
 * flotante no es la décima que se escribió. La coma decimal se acepta; los separadores de miles,
 * no, porque «1.234» sería una cosa para unos y otra para otros.
 */
export function cantidadEscrita(texto: string): string | null {
  const limpio = texto.trim().replace(',', '.');

  if (!/^-?\d{1,12}(\.\d{1,6})?$/.test(limpio) || Number(limpio) === 0) {
    return null;
  }

  return limpio;
}

/**
 * Lo escrito en el coste, ya como viaja, o nulo si no vale. Vacío no llega aquí: es «sin coste».
 *
 * Catorce cifras enteras y **cuatro decimales como mucho**. El servidor redondea a cuatro lo que
 * traiga más (`LineaDeAjuste.ElCosteCabe`); el formulario no lo deja escribir, para que lo que se
 * escribe sea lo que se guarda.
 */
export function costeEscrito(texto: string): string | null {
  const limpio = texto.trim().replace(',', '.');

  return /^\d{1,14}(\.\d{1,4})?$/.test(limpio) ? limpio : null;
}

const motivo = z
  .string()
  .trim()
  .min(1, RECHAZO_DEL_MOTIVO)
  .max(LARGO_DEL_MOTIVO, RECHAZO_DEL_MOTIVO);

const linea = z
  .object({
    ubicacionId: z.string().min(1, SIN_UBICACION),
    articuloId: z.string().min(1, SIN_ARTICULO),
    cantidadIntroducida: z
      .string()
      .trim()
      .min(1, SIN_CANTIDAD)
      .refine((texto) => cantidadEscrita(texto) !== null, RECHAZO_DE_LA_CANTIDAD),
    unidadIntroducidaId: z.string().min(1, SIN_UNIDAD),
    costeUnitario: z
      .string()
      .trim()
      .refine((texto) => texto === '' || costeEscrito(texto) !== null, COSTE_MAL_ESCRITO),
    codigoDeLote: z.string().trim(),
    numeroDeSerie: z.string().trim(),
  })
  // Una línea que baja no lleva coste: se valora al precio medio, y uno escrito ahí no se usaría
  // (ADR-0046 §7). Solo se mira cuando el resto de la línea vale, que es cuando `zod` la deja pasar.
  .refine(
    (escrita) =>
      escrita.costeUnitario === '' || !escrita.cantidadIntroducida.trim().startsWith('-'),
    { message: RECHAZO_DEL_COSTE, path: ['costeUnitario'] },
  );

export const esquemaDelAlta = z.object({
  almacenId: z.string().min(1, SIN_ALMACEN),
  serieId: z.string().min(1, SIN_SERIE),
  // El día se compara como texto: `AAAA-MM-DD` ordena igual que el calendario. Y con el hoy del
  // servidor, que es quien rechaza el de mañana (`ajuste-con-fecha-futura`).
  fechaDeOperacion: z.iso
    .date(SIN_FECHA)
    .refine((dia) => dia <= hoyEnElServidor(), RECHAZO_DE_LA_FECHA),
  motivo,
  lineas: z.array(linea).max(LINEAS_COMO_MUCHO, TOPE_DE_LINEAS),
});

export type DatosDelAlta = z.infer<typeof esquemaDelAlta>;
export type LineaDelFormulario = DatosDelAlta['lineas'][number];

/** Una línea por escribir: es lo que trae el alta al abrirse y lo que añade cada Intro. */
export const LINEA_EN_BLANCO: LineaDelFormulario = {
  ubicacionId: '',
  articuloId: '',
  cantidadIntroducida: '',
  unidadIntroducidaId: '',
  costeUnitario: '',
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

/** Una línea como viaja: la cantidad y el coste, como texto con punto, y lo vacío, nulo. */
export interface LineaDelAlta {
  readonly ubicacionId: string;
  readonly articuloId: string;
  readonly cantidadIntroducida: string;
  readonly unidadIntroducidaId: string;
  readonly costeUnitario: string | null;
  readonly codigoDeLote: string | null;
  readonly numeroDeSerie: string | null;
}

/** El alta como viaja. El factor no va: lo pone el servidor (ADR-0065 §2). */
export interface AltaDeAjuste {
  readonly serieId: string;
  readonly almacenId: string;
  readonly fechaDeOperacion: string;
  readonly motivo: string;
  readonly lineas: readonly LineaDelAlta[];
}

function oNulo(texto: string): string | null {
  return texto === '' ? null : texto;
}

/**
 * Lo que viaja, de lo que pasó el esquema. Con las mismas reglas que lo validaron: lo que dice la
 * pantalla que vale es exactamente lo que se manda.
 */
export function altaDe(datos: DatosDelAlta): AltaDeAjuste {
  return {
    serieId: datos.serieId,
    almacenId: datos.almacenId,
    fechaDeOperacion: datos.fechaDeOperacion,
    motivo: datos.motivo,
    lineas: datos.lineas.map((escrita) => ({
      ubicacionId: escrita.ubicacionId,
      articuloId: escrita.articuloId,
      cantidadIntroducida:
        cantidadEscrita(escrita.cantidadIntroducida) ?? escrita.cantidadIntroducida,
      unidadIntroducidaId: escrita.unidadIntroducidaId,
      costeUnitario:
        escrita.costeUnitario === ''
          ? null
          : (costeEscrito(escrita.costeUnitario) ?? escrita.costeUnitario),
      codigoDeLote: oNulo(escrita.codigoDeLote),
      numeroDeSerie: oNulo(escrita.numeroDeSerie),
    })),
  };
}

/** El motivo de una anulación. */
export const esquemaDeMotivo = z.object({ motivo });

export type DatosDeMotivo = z.infer<typeof esquemaDeMotivo>;
