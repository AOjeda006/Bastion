import type { ListadoDeSaldos } from '../model/saldo.ts';

/**
 * Las claves de consulta del saldo, todas en un módulo y jerárquicas.
 *
 * El saldo solo se lee: no hay escritura que invalide nada, y la clave lleva el listado entero, el
 * día incluido. La empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el
 * servidor (R8).
 *
 * Los nombres del almacén y del artículo de cada fila **no cuelgan de aquí**: salen por el mismo
 * camino que los del recuento, con la clave de los maestros del inventario (ADR-0063 §8), así que
 * un artículo que ya nombró una pantalla no se vuelve a pedir en la otra. Lo que sí es de aquí es
 * la lista de artículos que se ofrecen para acotar, que el recuento no pide.
 */
export const clavesDeSaldos = {
  todo: ['saldos'] as const,
  listas: () => [...clavesDeSaldos.todo, 'lista'] as const,
  lista: (listado: ListadoDeSaldos) => [...clavesDeSaldos.listas(), listado] as const,
  /** Los artículos que se ofrecen en el filtro. */
  articulos: () => [...clavesDeSaldos.todo, 'articulos'] as const,
};
