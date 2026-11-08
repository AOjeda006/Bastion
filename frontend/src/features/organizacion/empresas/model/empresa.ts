import type { Zona } from '../../zonas/model/zona.ts';

/** Una empresa, tal como la pinta esta funcionalidad. */
export interface Empresa {
  readonly id: string;
  readonly nif: string;
  readonly razonSocial: string;
  readonly poblacion: string;
  readonly divisaBase: string;
  /** La zona de su día de negocio, y la de sus almacenes que no tienen otra (ADR-0079 §2). */
  readonly zona: Zona;
}

/** Una página de empresas. */
export interface PaginaDeEmpresas {
  readonly elementos: readonly Empresa[];
  readonly total: number;
}
