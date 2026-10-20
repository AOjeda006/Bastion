import { z } from 'zod';

import {
  ESTADOS_DE_TRANSFERENCIA,
  type EstadoConocido,
  type EstadoDeTransferencia,
} from './transferencia.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va, en qué estado, de qué almacén sale, a
 * cuál llega y en qué orden (ADR-0068 §1).
 *
 * Sale entero de la URL, como en el resto de listados. **Ninguno de los criterios es sensible**
 * (ADR-0025): un estado, el identificador de dos almacenes de la propia empresa y un orden. **No
 * hay búsqueda**: el alta no tiene ningún campo que escriba una persona, y la API no filtra por
 * texto.
 */
export interface ListadoDeTransferencias extends Paginacion {
  readonly estado: EstadoConocido | null;
  readonly origenId: string | null;
  readonly destinoId: string | null;
  /** El orden, con el valor de la API, o nulo para el de siempre: el envío, del último. */
  readonly orden: Orden | null;
}

/**
 * Los nombres de los parámetros, que son los de la API (`?estado=`, `?origen=`, `?destino=`); el
 * orden lleva el valor de la API (`?orden=-numero`), como en el ajuste.
 */
export const PARAMETRO_DE_ESTADO = 'estado';
export const PARAMETRO_DE_ORIGEN = 'origen';
export const PARAMETRO_DE_DESTINO = 'destino';
export const PARAMETRO_DE_ORDEN = 'orden';

/**
 * Los órdenes que se ofrecen, además del de siempre, con el valor que va en `?sort=`. Un guion
 * delante es de mayor a menor.
 */
export const ORDENES = [
  'fechaDeEnvio',
  '-fechaDeRecepcion',
  'fechaDeRecepcion',
  '-numero',
  'numero',
] as const;

export type Orden = (typeof ORDENES)[number];

const EN_EL_CONTRATO: Record<EstadoConocido, string> = {
  borrador: 'Borrador',
  enviada: 'Enviada',
  recibida: 'Recibida',
  anulada: 'Anulada',
};

export function estadoEnElContrato(estado: EstadoConocido): string {
  return EN_EL_CONTRATO[estado];
}

/**
 * El estado que dice el contrato, o `desconocido` si esta versión no lo conoce. Con una búsqueda en
 * la lista y no con un registro: un registro es un objeto, y `constructor` está en él por herencia.
 */
export function estadoDelContrato(texto: string): EstadoDeTransferencia {
  return (
    ESTADOS_DE_TRANSFERENCIA.find((estado) => EN_EL_CONTRATO[estado] === texto) ?? 'desconocido'
  );
}

/** `z.guid()` y no `z.uuid()`: un `Guid` de .NET no tiene por qué llevar los bits de la RFC. */
const esquemaDeAlmacen = z.guid();

function almacen(parametros: URLSearchParams, nombre: string): string | null {
  const leido = esquemaDeAlmacen.safeParse(parametros.get(nombre));

  return leido.success ? leido.data : null;
}

/**
 * Lee de la URL cómo hay que pedir el listado.
 *
 * Lo que no se reconoce se ignora: viene de fuera, y mandárselo al servidor solo cambiaría una
 * pantalla sin filtro por un `400` que nadie ha provocado a propósito.
 */
export function leerListado(
  parametros: URLSearchParams,
  paginacion: Paginacion,
): ListadoDeTransferencias {
  const estado = estadoDelContrato(parametros.get(PARAMETRO_DE_ESTADO) ?? '');
  const orden = parametros.get(PARAMETRO_DE_ORDEN);

  return {
    ...paginacion,
    estado: estado === 'desconocido' ? null : estado,
    origenId: almacen(parametros, PARAMETRO_DE_ORIGEN),
    destinoId: almacen(parametros, PARAMETRO_DE_DESTINO),
    orden: ORDENES.find((valor) => valor === orden) ?? null,
  };
}

/** Si el listado está acotado: entonces una página vacía es «ninguna con este filtro». */
export function estaAcotado(listado: ListadoDeTransferencias): boolean {
  return listado.estado !== null || listado.origenId !== null || listado.destinoId !== null;
}
