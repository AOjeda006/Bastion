/**
 * Un alta que se ha intentado: lo que se mandó y la clave de idempotencia que llevó.
 *
 * **La clave va con el intento, no con el envío.** Volver a mandar lo mismo —tras un fallo de red,
 * o con un doble clic— es la misma operación, y el servidor contesta lo que ya contestó sin dar de
 * alta otra vez. Cambiar lo que se manda es otra operación, y estrena clave: con la anterior, el
 * servidor contestaría un 409 por cuerpo distinto. Tras un alta que sale bien se olvida: la
 * siguiente es otra, aunque se teclee el mismo número.
 */
export interface IntentoDeAlta {
  /** El cuerpo, tal como se manda, en texto: lo que se compara. */
  readonly huella: string;
  readonly clave: string;
}

/** El intento que toca a un cuerpo: el anterior si es el mismo, uno con clave nueva si no. */
export function intentoPara(cuerpo: unknown, anterior: IntentoDeAlta | null): IntentoDeAlta {
  const huella = JSON.stringify(cuerpo);

  return anterior?.huella === huella ? anterior : { huella, clave: crypto.randomUUID() };
}
