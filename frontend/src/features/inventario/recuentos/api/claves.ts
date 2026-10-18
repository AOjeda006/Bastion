import type { ListadoDeLineas, ListadoDeRecuentos } from '../model/listado.ts';

/**
 * Las claves de consulta de los recuentos, todas en un módulo y jerárquicas.
 *
 * Jerárquicas para invalidar por prefijo: `una(id)` alcanza la ficha **y** sus líneas, que es lo
 * que cambia al contar, al confirmar, al anular y al descartar. La empresa NO forma parte de la
 * clave: va dentro del testigo y quien filtra es el servidor (R8).
 *
 * Los maestros de otros módulos que el recuento nombra u ofrece **no cuelgan de aquí**: son de todo
 * el inventario, y sus claves están en `maestros/api/claves.ts`.
 */
export const clavesDeRecuentos = {
  todo: ['recuentos'] as const,
  listas: () => [...clavesDeRecuentos.todo, 'lista'] as const,
  lista: (listado: ListadoDeRecuentos) => [...clavesDeRecuentos.listas(), listado] as const,
  /** Un recuento: su ficha, y debajo sus líneas. */
  una: (id: string) => [...clavesDeRecuentos.todo, 'una', id] as const,
  ficha: (id: string) => [...clavesDeRecuentos.una(id), 'ficha'] as const,
  lineas: (id: string, listado: ListadoDeLineas) =>
    [...clavesDeRecuentos.una(id), 'lineas', listado] as const,
};
