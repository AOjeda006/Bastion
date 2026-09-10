import type { ListadoDeArticulos } from '../model/listado.ts';

/**
 * Las claves de consulta de los artículos, todas en un módulo y jerárquicas.
 *
 * Jerárquicas para poder invalidar por prefijo —`['articulos']` alcanza a todas las listas— y en un
 * módulo porque una clave escrita a mano en un componente es una invalidación que no invalida: se
 * parece a la buena y no es la misma.
 *
 * La empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el servidor (R8).
 */
export const clavesDeArticulos = {
  todo: ['articulos'] as const,
  listas: () => [...clavesDeArticulos.todo, 'lista'] as const,
  lista: (listado: ListadoDeArticulos) => [...clavesDeArticulos.listas(), listado] as const,
};
