import { milisegundosHasta } from '@/shared/lib/dias.ts';

/**
 * Hoy, como lo dice el servidor: en la empresa, en la más adelantada de sus zonas y en cada zona
 * que admite la API (ADR-0079 §12).
 *
 * **El frontal no calcula el día.** Cada día es el de la zona de su almacén, y lo que abarca la
 * empresa entera, el de la zona de la empresa: la regla vive en el servidor, que es quien rechaza
 * una fecha futura, y aquí solo se lee. Un formulario lleva el día de la zona del almacén que se
 * elige, y antes de elegirlo, el de la empresa.
 */
export interface Hoy {
  /** El día en la zona de la empresa: el de lo que abarca la empresa entera, como una tarifa. */
  readonly deLaEmpresa: string;
  /** El más adelantado de las zonas de la empresa: el tope de lo que mira varios almacenes. */
  readonly elMasAdelantado: string;
  /**
   * El día de cada zona que admite la API, por su identificador IANA (`Atlantic/Canary`).
   *
   * Están todas, y no solo las de la empresa: un almacén puede cambiar de zona mientras la pantalla
   * tiene la lista de antes.
   */
  readonly enCadaZona: ReadonlyMap<string, DiaDeUnaZona>;
}

/** El día de una zona, y el instante en que acaba, que es cuando hay que volver a preguntar. */
export interface DiaDeUnaZona {
  readonly dia: string;
  /** ISO 8601, en UTC, como lo da la API. */
  readonly acabaEn: string;
}

/**
 * El día de hoy en una zona, y sin zona, el de la empresa.
 *
 * Sin zona es antes de elegir el almacén, o sin poder verlo. Una zona que esta versión no conoce
 * cae también en la de la empresa: quien decide es el servidor, y su rechazo vuelve al campo.
 */
export function diaEn(hoy: Hoy, zona: string | null): string {
  return (zona === null ? undefined : hoy.enCadaZona.get(zona)?.dia) ?? hoy.deLaEmpresa;
}

/** Lo que se espera para volver a preguntar si el día ya tenía que haber acabado. */
const UN_MINUTO = 60_000;

/** Lo que se espera de más tras el fin del día, para preguntar cuando el servidor ya está en el siguiente. */
const MARGEN = 1_000;

/**
 * Cuánto vale lo que dijo el servidor: hasta que acaba el primero de los días de sus zonas.
 *
 * **Si ya tenía que haber acabado, un minuto, y no cero.** Pasa con un reloj de aquí adelantado,
 * o con una respuesta que tardó: preguntar sin pausa hasta que el servidor cambie de día sería un
 * bucle, y TanStack Query toma un intervalo de cero por «nunca». El minuto acota las dos cosas.
 *
 * `momento` se recibe para poder ejercerlo en la frontera; quien pregunta lo llama sin él.
 */
export function vidaDeHoy(hoy: Hoy, momento?: Date): number {
  const faltan = [...hoy.enCadaZona.values()].map((zona) =>
    milisegundosHasta(zona.acabaEn, momento),
  );

  if (faltan.length === 0) {
    return UN_MINUTO;
  }

  const falta = Math.min(...faltan);

  return falta > 0 ? falta + MARGEN : UN_MINUTO;
}
