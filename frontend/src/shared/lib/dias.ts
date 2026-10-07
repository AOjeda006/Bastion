/**
 * Los días de la API, para leerlos.
 *
 * Vive en `shared/` porque lo usan dos funcionalidades: la vigencia de las tarifas y las fechas del
 * recuento. Los días viajan como `AAAA-MM-DD`, sin hora y sin huso.
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
