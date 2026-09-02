/** Un almacén, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce `api/consultas.ts`. */
export interface Almacen {
  readonly id: string;
  readonly codigo: string;
  readonly nombre: string;
  readonly tipo: string;
  /** La población del domicilio, o `null` si es un almacén sin dirección (virtual o de tránsito). */
  readonly poblacion: string | null;
}

/** Una página de almacenes. */
export interface PaginaDeAlmacenes {
  readonly elementos: readonly Almacen[];
  readonly total: number;
}
