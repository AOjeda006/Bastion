/**
 * Un tramo de tarifa, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce
 * `api/consultas.ts`.
 *
 * **La divisa no está aquí, y no es un recorte de la pantalla.** El contrato la trae, por su código
 * ISO 4217 desde el 3.3 (ADR-0076), y lo que hace que aquí no falte es que esta pantalla **no pinta
 * ningún precio**: no hay ni un número al que le falte su moneda. Donde el precio se resuelve
 * —`GET /tarifas/{codigo}/precio`— viaja SIEMPRE con su `divisa` pegada, y eso es lo que hace
 * segura la decisión de aceptar una tarifa en una divisa distinta de la de la empresa.
 */
export interface Tarifa {
  readonly id: string;
  /** El código, en mayúsculas. **Se repite**: una tarifa son varios tramos con el mismo código. */
  readonly codigo: string;
  readonly nombre: string;
  /** Primer día en que rige, incluido. Un día suelto —`aaaa-mm-dd`—, no un instante. */
  readonly vigenteDesde: string;
  /** Último día en que rige, **incluido**; nulo mientras siga vigente. */
  readonly vigenteHasta: string | null;
}

/** Una página de tramos de tarifa. */
export interface PaginaDeTarifas {
  readonly elementos: readonly Tarifa[];
  readonly total: number;
}

/** Si un tramo rige hoy, si todavía no le toca, o si ya pasó. */
export type EstadoDeVigencia = 'rige' | 'futura' | 'caducada';

/**
 * En qué estado está un tramo el día `hoy`.
 *
 * **Los dos extremos están INCLUIDOS, y el segundo es el que se olvida**: el último día de vigencia
 * todavía rige. Es la misma convención que el `daterange(…, '[]')` de la restricción de exclusión y
 * que el `<=` de `Tarifa.RigeEl`; tres sitios y una sola convención a propósito, porque si aquí se
 * pintara con `<`, el día en que un tramo acaba diría «ya no rige» en la pantalla mientras la API
 * sigue devolviendo su precio — y no hay error, solo dos versiones de la verdad.
 *
 * **Se comparan CADENAS y no fechas, y eso es lo que evita el error de un día.** Los dos extremos
 * llegan como días sueltos (`format: date`), sin hora y sin huso. `new Date('2026-09-11')` se
 * interpreta como medianoche UTC, así que en un navegador al oeste de Greenwich cae en el día
 * anterior: un tramo que empieza hoy se pintaría como «todavía no rige» durante un día entero, y
 * solo para parte del mundo. Los días en ISO-8601 se ordenan igual como texto que como fechas, así
 * que comparar cadenas es exacto y no tiene huso que equivocar.
 *
 * `hoy` se recibe, no se lee dentro: una función que mirara el reloj no se podría ejercer en la
 * frontera, que es el único sitio donde esto se equivoca. Y es el del servidor, `hoyEnElServidor`:
 * así la etiqueta dice lo mismo que resuelve `ResolverPrecio`, que sin fecha usa el día UTC.
 */
export function estadoDeVigencia(tarifa: Tarifa, hoy: string): EstadoDeVigencia {
  if (hoy < tarifa.vigenteDesde) {
    return 'futura';
  }

  return tarifa.vigenteHasta !== null && tarifa.vigenteHasta < hoy ? 'caducada' : 'rige';
}
