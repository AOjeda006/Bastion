/**
 * Un envío que se ha intentado: lo que se mandó y la clave de idempotencia que llevó.
 *
 * **La clave va con el intento, no con el envío.** Volver a mandar lo mismo —tras un fallo de red,
 * o con un doble clic— es la misma operación, y el servidor contesta lo que ya contestó sin hacerla
 * otra vez. Cambiar lo que se manda es otra operación, y estrena clave: con la anterior, el servidor
 * contestaría un 409 por cuerpo distinto. Tras un envío que sale bien se olvida: el siguiente es
 * otro, aunque lleve lo mismo.
 *
 * Vive en `shared/` porque lo usan dos funcionalidades: el alta de un código de barras y las
 * acciones del recuento. Lo que comparan es lo que viaja —el cuerpo y, si la hay, la versión—, así
 * que una confirmación repetida con la versión de antes no reutiliza la clave de la de ahora.
 */
export interface Intento {
  /** Lo que se manda, en texto: lo que se compara. */
  readonly huella: string;
  readonly clave: string;
}

/** El intento que toca a un envío: el anterior si es el mismo, uno con clave nueva si no. */
export function intentoPara(envio: unknown, anterior: Intento | null): Intento {
  const huella = JSON.stringify(envio);

  return anterior?.huella === huella ? anterior : { huella, clave: crypto.randomUUID() };
}
