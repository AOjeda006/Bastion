/**
 * Los días de la API: cómo se leen, y cuánto falta para que acabe uno.
 *
 * Vive en `shared/` porque lo usan varias funcionalidades: la vigencia de las tarifas, las fechas de
 * los documentos del inventario y el saldo a una fecha. Los días viajan como `AAAA-MM-DD`, sin hora
 * y sin huso.
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
 * Cuántos milisegundos faltan para `instante`, o cero si ya pasó: **la única lectura del reloj del
 * frontal**.
 *
 * El frontal no sabe qué día es: se lo dice el servidor, en la zona de la empresa y en la de cada
 * almacén, con el instante en que acaba cada día (ADR-0079 §12). Lo único que decide el reloj de
 * aquí es cuándo volver a preguntar, y uno que vaya adelantado o atrasado solo adelanta o atrasa
 * la pregunta: el día lo sigue diciendo el servidor. Hasta el 3.5 aquí se calculaba el día, el
 * UTC, y se equivocaba en cuanto la empresa tenía zona.
 *
 * `momento` se recibe para poder ejercerlo en la frontera; quien pregunta lo llama sin él.
 */
export function milisegundosHasta(instante: string, momento: Date = new Date()): number {
  return Math.max(0, Date.parse(instante) - momento.getTime());
}
