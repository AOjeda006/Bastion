/**
 * Los días de la API: cómo se leen, y cuál es hoy.
 *
 * Vive en `shared/` porque lo usan tres funcionalidades: la vigencia de las tarifas, las fechas del
 * recuento y el saldo a una fecha. Los días viajan como `AAAA-MM-DD`, sin hora y sin huso.
 */

/**
 * Un día suelto, escrito como lo escribe quien lo lee.
 *
 * La fecha se compone en UTC y se formatea en UTC: así el día pintado es exactamente el que vino,
 * sin que el huso del navegador pueda restarle uno. Y lo que no tenga forma de día se pinta TAL
 * CUAL: viene de la red, y una pantalla que reventara por un dato mal formado cambiaría una celda
 * rara —que se ve y se puede contar— por una pantalla en blanco.
 */
export function diaLegible(dia: string, idioma: string): string {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dia);

  if (partes === null) {
    return dia;
  }

  return new Intl.DateTimeFormat(idioma, { dateStyle: 'medium', timeZone: 'UTC' }).format(
    Date.UTC(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3])),
  );
}

/**
 * El día de hoy **del servidor**, como `AAAA-MM-DD`: el único «hoy» del frontal.
 *
 * Es el día UTC (ADR-0063 §3), el que calcula `DiaDeNegocio` en el backend, y no el del calendario
 * de quien mira, a propósito. Entre la medianoche de aquí y la de Greenwich los dos no coinciden, y
 * en esa ventana un «hoy» local diría que rige un tramo de tarifa cuyo precio la API ya no da, o
 * ofrecería como tope del saldo un día que la API rechaza con un `400`. Hubo dos, uno por cada
 * criterio, hasta el 2.16. La zona que regirá cuando el almacén lleve la suya la dice la nota de la
 * zona, en `docs/PLAN.md`.
 *
 * `momento` se recibe para poder ejercerlo en la frontera; quien pinta lo llama sin argumentos.
 */
export function hoyEnElServidor(momento: Date = new Date()): string {
  return momento.toISOString().slice(0, 10);
}
