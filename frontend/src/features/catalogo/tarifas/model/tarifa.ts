/**
 * Un tramo de tarifa, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce
 * `api/consultas.ts`.
 *
 * **La divisa está por su código ISO 4217**, que es como la guarda la tarifa desde el 3.3
 * (ADR-0076): el código ya la nombra, y no hace falta preguntarle nada a Organización. Lo que esta
 * pantalla sigue sin pintar es un precio. Donde el precio se resuelve
 * —`GET /tarifas/{codigo}/precio`— viaja SIEMPRE con su `divisa` pegada, y eso es lo que hace
 * segura la decisión de aceptar una tarifa en una divisa distinta de la de la empresa.
 */
export interface Tarifa {
  readonly id: string;
  /** El código, en mayúsculas. **Se repite**: una tarifa son varios tramos con el mismo código. */
  readonly codigo: string;
  readonly nombre: string;
  /** De venta o de compra. Es el de todos los tramos de su código: el tipo va con el código. */
  readonly tipo: TipoDeTarifa;
  /** El código ISO 4217 de la divisa, en mayúsculas, como lo guarda la tarifa. */
  readonly divisa: string;
  /** Primer día en que rige, incluido. Un día suelto —`aaaa-mm-dd`—, no un instante. */
  readonly vigenteDesde: string;
  /** Último día en que rige, **incluido**; nulo mientras siga vigente. */
  readonly vigenteHasta: string | null;
}

/**
 * Para vender o para comprar (ADR-0076).
 *
 * Los dos valores que la API emite hoy, más `desconocido`, por lo mismo que el tipo del artículo:
 * el tipo **viaja como texto** y el frontal se despliega aparte del backend, así que un valor nuevo
 * llega antes de que este fichero lo conozca. Sin el tercer caso, la celda saldría en blanco.
 */
export type TipoDeTarifa = 'venta' | 'compra' | 'desconocido';

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
 * frontera, que es el único sitio donde esto se equivoca. Y es el que dice el servidor, el de la
 * zona de la empresa (`useHoy`): así la etiqueta dice lo mismo que resuelve `ResolverPrecio`, que
 * sin fecha usa ese día (ADR-0079 §3).
 */
export function estadoDeVigencia(tarifa: Tarifa, hoy: string): EstadoDeVigencia {
  if (hoy < tarifa.vigenteDesde) {
    return 'futura';
  }

  return tarifa.vigenteHasta !== null && tarifa.vigenteHasta < hoy ? 'caducada' : 'rige';
}
