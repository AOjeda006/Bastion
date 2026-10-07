import type { LineaDeRecuento } from './recuento.ts';

/**
 * Las cantidades del recuento: cómo se escriben, cómo se leen y qué cambió.
 *
 * Van en la unidad base del artículo, con seis decimales como mucho y doce cifras enteras, que es
 * lo que cabe en un `numeric(18,6)` (`LineaDeRecuento.SePuedeContar`). Esta regla la replica; la
 * autoridad es el servidor.
 */

/**
 * Lo escrito en el campo, ya como la cifra que viaja, o nulo si no vale.
 *
 * **Viaja como texto**, con punto, y no como número: el contrato admite las dos formas, y un `0.1`
 * de coma flotante no es la décima que se escribió. La coma decimal se acepta, porque es la que
 * escribe quien cuenta en castellano; los separadores de miles, no, porque «1.234» sería mil
 * doscientos treinta y cuatro para unos y uno con dos décimas para otros.
 *
 * Un número de serie es una pieza: se cuenta 0 o 1.
 */
export function contadoEscrito(texto: string, esUnaSerie: boolean): string | null {
  const limpio = texto.trim().replace(',', '.');

  if (!/^\d{1,12}(\.\d{1,6})?$/.test(limpio)) {
    return null;
  }

  if (esUnaSerie && Number(limpio) !== 0 && Number(limpio) !== 1) {
    return null;
  }

  return limpio;
}

/**
 * Cuánto ha cambiado el teórico desde que se contó la línea, o nulo si no ha cambiado o si no se
 * ha contado: el de ahora menos el de entonces.
 */
export function cambioDelTeorico(linea: LineaDeRecuento): number | null {
  if (!linea.teoricoCambiado || linea.teorico === null || linea.teoricoAlContar === null) {
    return null;
  }

  return linea.teorico - linea.teoricoAlContar;
}

/** Una cantidad, como la escribe quien la lee: con sus decimales, y sin rellenar con ceros. */
export function cantidadLegible(valor: number, idioma: string): string {
  return new Intl.NumberFormat(idioma, { maximumFractionDigits: 6 }).format(valor);
}

/** Una diferencia, con su signo delante, salvo el cero, que no sube ni baja. */
export function diferenciaLegible(valor: number, idioma: string): string {
  return new Intl.NumberFormat(idioma, {
    maximumFractionDigits: 6,
    signDisplay: 'exceptZero',
  }).format(valor);
}
