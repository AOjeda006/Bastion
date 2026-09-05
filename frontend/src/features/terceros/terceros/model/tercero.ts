/**
 * Un tercero, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce `api/consultas.ts`.
 *
 * El identificador fiscal viene en **tres campos y no en uno**, igual que en el contrato y por el
 * mismo motivo: son un solo hecho —este número, de este país, comprobado hasta aquí—. Aplanarlo a
 * una cadena aquí dejaría a la tabla pintando un número sin poder decir si está comprobado, que es
 * exactamente lo que este módulo existe para no permitir.
 */
export interface Tercero {
  readonly id: string;
  readonly pais: string;
  readonly numero: string;
  readonly verificacion: Verificacion;
  readonly razonSocial: string;
  readonly nombreComercial: string | null;
  readonly poblacion: string;
  readonly esCliente: boolean;
  readonly esProveedor: boolean;
}

/**
 * Cuánto se sabe de que el identificador sea el que dice ser.
 *
 * Los dos valores que la API emite hoy, más `desconocida` para lo que llegue mañana. **No es
 * defensa por si acaso**: el enumerado viaja como texto y el frontal se despliega aparte del
 * backend, así que un valor nuevo llega antes de que este fichero lo conozca. Sin el tercer caso,
 * la traducción devolvería `undefined` y la celda se quedaría vacía —que es lo mismo que se ve
 * cuando algo falla— en vez de decir que no se sabe.
 */
export type Verificacion = 'verificado' | 'sinVerificar' | 'desconocida';

/** Una página de terceros. */
export interface PaginaDeTerceros {
  readonly elementos: readonly Tercero[];
  readonly total: number;
}
