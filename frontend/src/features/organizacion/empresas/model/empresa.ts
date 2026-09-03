/** Una empresa, tal como la pinta esta funcionalidad. */
export interface Empresa {
  readonly id: string;
  readonly nif: string;
  readonly razonSocial: string;
  readonly poblacion: string;
  readonly divisaBase: string;
}

/** Una página de empresas. */
export interface PaginaDeEmpresas {
  readonly elementos: readonly Empresa[];
  readonly total: number;
}
