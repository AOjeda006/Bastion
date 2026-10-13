import { z } from 'zod';

import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * El saldo de un artículo en un almacén al final de un día, tal como lo pinta esta funcionalidad.
 * No es el DTO: lo traduce `api/saldos.ts` (ADR-0063 §1 y §4).
 *
 * El artículo y el almacén llegan como identificadores, porque son maestros de otros módulos: la
 * pantalla los nombra por el mismo camino que el recuento.
 */
export interface Saldo {
  readonly articuloId: string;
  readonly almacenId: string;
  /** Las unidades base que había al acabar el día. */
  readonly cantidad: number;
  /** Lo que valían, en la divisa de la valoración. */
  readonly valor: number;
  /** Lo que viajaba hacia el almacén ese día, en unidades base. **No está sumado** en `cantidad`. */
  readonly enTransito: number;
  /** Lo que valía lo que viajaba. **No está sumado** en `valor`. */
  readonly valorEnTransito: number;
  /** El código ISO 4217 de la divisa de la valoración. */
  readonly divisa: string;
}

/** Una página de saldos, con el día que contesta: sin fecha, el servidor contesta hoy. */
export interface SaldosAUnaFecha {
  /** El día cuyo final se contesta, `AAAA-MM-DD`. */
  readonly fecha: string;
  readonly elementos: readonly Saldo[];
  readonly total: number;
}

/**
 * Cómo se está mirando el saldo ahora mismo: qué día, de qué almacén y de qué artículo, y por qué
 * página va.
 *
 * Sale entero de la URL, como en el resto de listados. **Ninguno de los tres criterios es
 * sensible** (ADR-0025): un día, y el identificador de un almacén o de un artículo de la propia
 * empresa.
 */
export interface ListadoDeSaldos extends Paginacion {
  /** El día pedido, `AAAA-MM-DD`, o nulo para hoy. */
  readonly fecha: string | null;
  readonly almacenId: string | null;
  readonly articuloId: string | null;
}

/** Los nombres de los parámetros. Son **los mismos** que espera la API. */
export const PARAMETRO_DE_FECHA = 'fecha';
export const PARAMETRO_DE_ALMACEN = 'almacen';
export const PARAMETRO_DE_ARTICULO = 'articulo';

/** `z.guid()` y no `z.uuid()`: un `Guid` de .NET no tiene por qué llevar los bits de la RFC. */
const esquemaDeIdentificador = z.guid();

/**
 * Un día con forma de día y que existe en el calendario. `z.iso.date()` rechaza el 31 de febrero, y
 * eso es lo que se quiere: el enlace del modelo del servidor también lo rechazaría, con un `400`.
 */
const esquemaDeDia = z.iso.date();

/**
 * Lee de la URL cómo hay que pedir el saldo.
 *
 * Lo que no se reconoce se ignora: viene de fuera, y mandárselo al servidor solo cambiaría una
 * pantalla sin filtro por un `400` que nadie ha provocado a propósito. **Un día futuro sí se
 * manda**: tiene forma de día, y quien dice que el saldo del futuro no existe es el servidor, con
 * su reloj y con `saldo-con-fecha-futura`.
 */
export function leerListado(parametros: URLSearchParams, paginacion: Paginacion): ListadoDeSaldos {
  const fecha = esquemaDeDia.safeParse(parametros.get(PARAMETRO_DE_FECHA));
  const almacen = esquemaDeIdentificador.safeParse(parametros.get(PARAMETRO_DE_ALMACEN));
  const articulo = esquemaDeIdentificador.safeParse(parametros.get(PARAMETRO_DE_ARTICULO));

  return {
    ...paginacion,
    fecha: fecha.success ? fecha.data : null,
    almacenId: almacen.success ? almacen.data : null,
    articuloId: articulo.success ? articulo.data : null,
  };
}

/**
 * El día de hoy **del servidor**, que es el UTC (ADR-0063 §3), como `AAAA-MM-DD`.
 *
 * No es el del calendario de quien mira, a propósito: es el tope de la fecha que se puede pedir, y
 * ofrecer el día local entre la medianoche de aquí y la de Greenwich sería ofrecer un `400` seguro.
 * Que «hoy» sea el día UTC es una nota abierta para el cierre de la fase.
 */
export function hoyEnElServidor(momento: Date = new Date()): string {
  return momento.toISOString().slice(0, 10);
}

/**
 * Un importe con su divisa, como lo escribe quien lo lee: con los decimales de la divisa y hasta
 * cuatro, que son los que guarda el libro.
 *
 * Una divisa que `Intl` no conoce no revienta la pantalla: se pinta la cifra con el código detrás.
 * Viene de la red, y una celda rara se ve y se puede contar.
 */
export function importeLegible(valor: number, divisa: string, idioma: string): string {
  try {
    return new Intl.NumberFormat(idioma, {
      style: 'currency',
      currency: divisa,
      maximumFractionDigits: 4,
    }).format(valor);
  } catch {
    return `${new Intl.NumberFormat(idioma, { maximumFractionDigits: 4 }).format(valor)} ${divisa}`;
  }
}
