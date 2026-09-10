/**
 * Una categoría, tal como la pinta esta funcionalidad. No es el DTO: lo traduce `api/consultas.ts`.
 *
 * **Lleva el padre, no los hijos**, y eso es la lista de adyacencia del backend asomando por el
 * contrato: la API devuelve la lista PLANA con el padre de cada una, no el árbol montado. Quien lo
 * compone es `arbol.ts`, aquí, en una vuelta por la lista.
 */
export interface Categoria {
  readonly id: string;
  readonly codigo: string;
  readonly nombre: string;
  /** De la que cuelga, o nula si es una raíz. */
  readonly padreId: string | null;
}

/** Una página de categorías. */
export interface PaginaDeCategorias {
  readonly elementos: readonly Categoria[];
  readonly total: number;
}
