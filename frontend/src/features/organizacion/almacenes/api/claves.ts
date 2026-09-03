import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Las claves de consulta de esta funcionalidad, todas en un módulo y jerárquicas.
 *
 * Jerárquicas para poder invalidar por prefijo —`['almacenes']` alcanza a todas las listas— y en un
 * módulo porque una clave escrita a mano en un componente es una invalidación que no invalida: se
 * parece a la buena y no es la misma.
 *
 * Nota para el cambio de empresa: NADA de esto se invalida a mano al cambiar. La empresa no forma
 * parte de la clave a propósito —va dentro del testigo— y por eso el cambio vacía la caché entera.
 */
export const clavesDeAlmacenes = {
  todo: ['almacenes'] as const,
  listas: () => [...clavesDeAlmacenes.todo, 'lista'] as const,
  lista: (paginacion: Paginacion) => [...clavesDeAlmacenes.listas(), paginacion] as const,
};
