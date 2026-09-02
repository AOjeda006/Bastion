/**
 * Por qué los enteros del contrato llegan como `number | string`.
 *
 * No es un defecto del generador: la API usa `JsonSerializerDefaults.Web`, que trae
 * `NumberHandling = AllowReadingFromString`, así que al LEER acepta de verdad `"total": "3"`. El
 * documento OpenAPI describe un solo esquema por tipo para la petición y para la respuesta, y por
 * eso esa permisividad de entrada aparece también en la salida — donde el servidor siempre escribe
 * un número.
 *
 * Se estrecha AQUÍ, en la frontera, y no retocando el documento: el documento no miente, describe
 * lo que la API acepta. Apretarlo de verdad sería poner `NumberHandling.Strict` en el servidor, que
 * es un cambio de comportamiento de la API —dejaría de admitir clientes que mandan números como
 * texto— y no algo que se decide de rebote desde el frontal. Queda anotado en `docs/PLAN.md`.
 */
export function enteroDelContrato(valor: number | string): number {
  return typeof valor === 'number' ? valor : Number.parseInt(valor, 10);
}
