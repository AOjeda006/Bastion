import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Las claves de consulta de las categorías. Mismo criterio que las de artículos.
 *
 * Hay dos ramas y no una porque hay dos lecturas distintas: la página del árbol y **una categoría
 * suelta**, que es la que resuelve el nombre de la rama por la que se está filtrando en la pantalla
 * de artículos. Colgar las dos del mismo prefijo es lo que permite que una futura alta las
 * invalide juntas con `['categorias']`.
 */
export const clavesDeCategorias = {
  todo: ['categorias'] as const,
  listas: () => [...clavesDeCategorias.todo, 'lista'] as const,
  lista: (paginacion: Paginacion) => [...clavesDeCategorias.listas(), paginacion] as const,
  una: (id: string) => [...clavesDeCategorias.todo, 'una', id] as const,
};
