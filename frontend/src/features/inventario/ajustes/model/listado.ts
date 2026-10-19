import { z } from 'zod';

import { ESTADOS_DE_AJUSTE, type EstadoConocido, type EstadoDeAjuste } from './ajuste.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va, en qué estado, de qué almacén, qué se
 * busca en el motivo y en qué orden (ADR-0065 §6).
 *
 * Sale entero de la URL, como en el resto de listados. **Ninguno de los criterios es sensible**
 * (ADR-0025): un estado, el identificador de un almacén de la propia empresa, un texto que se busca
 * en el motivo y un orden.
 */
export interface ListadoDeAjustes extends Paginacion {
  readonly estado: EstadoConocido | null;
  readonly almacenId: string | null;
  /** Lo que se busca en el motivo, ya sin espacios a los lados, o vacío. */
  readonly busqueda: string;
  /** El orden, con el valor de la API, o nulo para el de siempre: la fecha, de la última. */
  readonly orden: Orden | null;
}

/**
 * Los nombres de los parámetros. El estado y el almacén son los de la API (`?estado=`, `?almacen=`),
 * como en el recuento; la búsqueda, el del listado de artículos (`?busqueda=`), y el orden lleva el
 * valor de la API (`?orden=-numero`).
 */
export const PARAMETRO_DE_ESTADO = 'estado';
export const PARAMETRO_DE_ALMACEN = 'almacen';
export const PARAMETRO_DE_BUSQUEDA = 'busqueda';
export const PARAMETRO_DE_ORDEN = 'orden';

/** El largo máximo de la búsqueda, que es el del servidor: uno más es un `400`. */
export const LARGO_DE_LA_BUSQUEDA = 100;

/**
 * Los órdenes que se ofrecen, además del de siempre, con el valor que va en `?sort=`. Un guion
 * delante es de mayor a menor.
 */
export const ORDENES = ['fechaDeOperacion', '-numero', 'numero'] as const;

export type Orden = (typeof ORDENES)[number];

const EN_EL_CONTRATO: Record<EstadoConocido, string> = {
  borrador: 'Borrador',
  confirmado: 'Confirmado',
  anulado: 'Anulado',
};

export function estadoEnElContrato(estado: EstadoConocido): string {
  return EN_EL_CONTRATO[estado];
}

/**
 * El estado que dice el contrato, o `desconocido` si esta versión no lo conoce. Con una búsqueda en
 * la lista y no con un registro: un registro es un objeto, y `constructor` está en él por herencia.
 */
export function estadoDelContrato(texto: string): EstadoDeAjuste {
  return ESTADOS_DE_AJUSTE.find((estado) => EN_EL_CONTRATO[estado] === texto) ?? 'desconocido';
}

/** `z.guid()` y no `z.uuid()`: un `Guid` de .NET no tiene por qué llevar los bits de la RFC. */
const esquemaDeAlmacen = z.guid();

/**
 * Lee de la URL cómo hay que pedir el listado.
 *
 * Lo que no se reconoce se ignora: viene de fuera, y mandárselo al servidor solo cambiaría una
 * pantalla sin filtro por un `400` que nadie ha provocado a propósito. Una búsqueda más larga que la
 * del servidor se corta, por lo mismo.
 */
export function leerListado(parametros: URLSearchParams, paginacion: Paginacion): ListadoDeAjustes {
  const estado = estadoDelContrato(parametros.get(PARAMETRO_DE_ESTADO) ?? '');
  const almacen = esquemaDeAlmacen.safeParse(parametros.get(PARAMETRO_DE_ALMACEN));
  const orden = parametros.get(PARAMETRO_DE_ORDEN);

  return {
    ...paginacion,
    estado: estado === 'desconocido' ? null : estado,
    almacenId: almacen.success ? almacen.data : null,
    busqueda: (parametros.get(PARAMETRO_DE_BUSQUEDA) ?? '').trim().slice(0, LARGO_DE_LA_BUSQUEDA),
    orden: ORDENES.find((valor) => valor === orden) ?? null,
  };
}

/** Si el listado está acotado: entonces una página vacía es «ninguno con este filtro». */
export function estaAcotado(listado: ListadoDeAjustes): boolean {
  return listado.estado !== null || listado.almacenId !== null || listado.busqueda !== '';
}
