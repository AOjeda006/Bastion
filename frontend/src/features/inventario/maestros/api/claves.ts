/**
 * Las claves de consulta de los maestros de otros módulos que nombra el inventario.
 *
 * **Son del inventario, no de sus dueños** (ADR-0055 §12). Esta funcionalidad los pide por la API
 * del catálogo y de la organización para nombrarlos, y la clave es suya: si fuera la de ellos, la
 * forma de su caché la decidirían dos funcionalidades que no se pueden importar la una a la otra.
 * Y son de todo el inventario: lo que nombró una pantalla no se vuelve a pedir en otra.
 *
 * Nada las invalida, porque nada del inventario escribe un maestro. Duran lo que dice
 * `VIDA_DE_UN_MAESTRO`, y el cambio de empresa vacía la caché entera.
 */
export const clavesDeMaestros = {
  todo: ['maestros-del-inventario'] as const,
  /** Los maestros que se nombran, uno a uno, por su identificador. */
  maestro: (clase: 'almacen' | 'articulo' | 'ubicacion' | 'unidad', id: string) =>
    [...clavesDeMaestros.todo, 'maestro', clase, id] as const,
  /** Los que se ofrecen en un alta o en un filtro: los almacenes y las series de la empresa. */
  almacenes: () => [...clavesDeMaestros.todo, 'almacenes'] as const,
  series: () => [...clavesDeMaestros.todo, 'series'] as const,
};
