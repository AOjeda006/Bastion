import type { ListadoDeLineas, ListadoDeRecuentos } from '../model/listado.ts';

/**
 * Las claves de consulta de los recuentos, todas en un módulo y jerárquicas.
 *
 * Jerárquicas para invalidar por prefijo: `una(id)` alcanza la ficha **y** sus líneas, que es lo
 * que cambia al contar, al confirmar, al anular y al descartar. La empresa NO forma parte de la
 * clave: va dentro del testigo y quien filtra es el servidor (R8).
 *
 * **Los maestros de otros módulos también cuelgan de aquí** (ADR-0055 §12). Esta funcionalidad los
 * pide por la API de su dueño para nombrarlos, y la clave es suya: si fuera la del catálogo, la
 * forma de su caché la decidirían dos funcionalidades que no se pueden importar la una a la otra.
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
  /** Los maestros que se nombran, uno a uno, por su identificador. */
  maestro: (clase: 'almacen' | 'articulo' | 'ubicacion' | 'unidad', id: string) =>
    [...clavesDeRecuentos.todo, 'maestro', clase, id] as const,
  /** Los que se ofrecen al abrir: los almacenes y las series de la empresa. */
  almacenes: () => [...clavesDeRecuentos.todo, 'almacenes'] as const,
  series: () => [...clavesDeRecuentos.todo, 'series'] as const,
};
