import type { Zona } from '../../zonas/model/zona.ts';

/** Un almacén, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce `api/consultas.ts`. */
export interface Almacen {
  readonly id: string;
  readonly codigo: string;
  readonly nombre: string;
  readonly tipo: string;
  /** La población del domicilio, o `null` si es un almacén sin dirección (virtual o de tránsito). */
  readonly poblacion: string | null;
  /** La zona de la que sale su día: la suya, o la de su empresa si no tiene (ADR-0079 §2). */
  readonly zona: Zona;
  /** Si la zona es suya, o la hereda de su empresa, en vivo. */
  readonly zonaPropia: boolean;
}

/** Una página de almacenes. */
export interface PaginaDeAlmacenes {
  readonly elementos: readonly Almacen[];
  readonly total: number;
}
