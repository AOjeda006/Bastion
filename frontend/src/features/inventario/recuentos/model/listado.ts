import { z } from 'zod';

import { ESTADOS_DE_RECUENTO, type EstadoConocido, type EstadoDeRecuento } from './recuento.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va, en qué estado y de qué almacén.
 *
 * Sale entero de la URL, como en el resto de listados: el listado acotado se puede pegar en un
 * correo, la flecha de atrás deshace el filtro y una recarga no pierde el sitio.
 *
 * **Ninguno de los dos criterios es sensible** (ADR-0025): un estado y el identificador de un
 * almacén de la propia empresa.
 */
export interface ListadoDeRecuentos extends Paginacion {
  /** El estado por el que se acota, o nulo para verlos todos. */
  readonly estado: EstadoConocido | null;
  /** El almacén por el que se acota, o nulo para verlos todos. */
  readonly almacenId: string | null;
}

/** Los nombres de los parámetros. Son **los mismos** que espera la API (`?estado=`, `?almacen=`). */
export const PARAMETRO_DE_ESTADO = 'estado';
export const PARAMETRO_DE_ALMACEN = 'almacen';

/**
 * Del valor de la pantalla al del contrato, y al revés.
 *
 * En la URL va el del contrato, `EnCurso`, por lo mismo que el nombre del parámetro: un enlace
 * copiado de la barra de direcciones vale para probar la API a mano.
 */
const EN_EL_CONTRATO: Record<EstadoConocido, string> = {
  enCurso: 'EnCurso',
  confirmado: 'Confirmado',
  anulado: 'Anulado',
  descartado: 'Descartado',
};

export function estadoEnElContrato(estado: EstadoConocido): string {
  return EN_EL_CONTRATO[estado];
}

/**
 * El estado que dice el contrato, o `desconocido` si esta versión no lo conoce.
 *
 * Con una búsqueda en la lista y no con un registro: un registro es un objeto, y `constructor` está
 * en él por herencia.
 */
export function estadoDelContrato(texto: string): EstadoDeRecuento {
  return ESTADOS_DE_RECUENTO.find((estado) => EN_EL_CONTRATO[estado] === texto) ?? 'desconocido';
}

/** `z.guid()` y no `z.uuid()`: un `Guid` de .NET no tiene por qué llevar los bits de la RFC. */
const esquemaDeAlmacen = z.guid();

/**
 * Lee de la URL cómo hay que pedir el listado.
 *
 * Lo que no se reconoce se ignora, como un `?pagina=-4`: viene de fuera, y mandárselo al servidor
 * solo cambiaría una pantalla sin filtro por un `400` que nadie ha provocado a propósito.
 */
export function leerListado(
  parametros: URLSearchParams,
  paginacion: Paginacion,
): ListadoDeRecuentos {
  const estado = estadoDelContrato(parametros.get(PARAMETRO_DE_ESTADO) ?? '');
  const almacen = esquemaDeAlmacen.safeParse(parametros.get(PARAMETRO_DE_ALMACEN));

  return {
    ...paginacion,
    estado: estado === 'desconocido' ? null : estado,
    almacenId: almacen.success ? almacen.data : null,
  };
}

/** Las dos vistas parciales de las líneas, con el nombre que les da la API (`?solo=`). */
export const VISTAS_DE_LINEAS = ['sin-contar', 'teorico-cambiado'] as const;

export type VistaDeLineas = (typeof VISTAS_DE_LINEAS)[number];

/** Cómo se están mirando las líneas de un recuento: la página y, si la hay, la vista parcial. */
export interface ListadoDeLineas extends Paginacion {
  readonly solo: VistaDeLineas | null;
}

export const PARAMETRO_DE_VISTA = 'solo';

export function leerLineas(parametros: URLSearchParams, paginacion: Paginacion): ListadoDeLineas {
  const solo = parametros.get(PARAMETRO_DE_VISTA);

  return { ...paginacion, solo: VISTAS_DE_LINEAS.find((vista) => vista === solo) ?? null };
}
