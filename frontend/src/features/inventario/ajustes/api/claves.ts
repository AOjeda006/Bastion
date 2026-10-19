import type { ListadoDeAjustes } from '../model/listado.ts';

/**
 * Las claves de consulta de los ajustes, todas en un módulo y jerárquicas.
 *
 * La ficha trae las líneas enteras (ADR-0065 §6), así que un ajuste es una sola consulta. La
 * empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el servidor (R8). Los
 * maestros que el ajuste nombra u ofrece cuelgan de `maestros/api/claves.ts`.
 */
export const clavesDeAjustes = {
  todo: ['ajustes'] as const,
  listas: () => [...clavesDeAjustes.todo, 'lista'] as const,
  lista: (listado: ListadoDeAjustes) => [...clavesDeAjustes.listas(), listado] as const,
  /** Un ajuste: su ficha, con sus líneas y su versión. */
  una: (id: string) => [...clavesDeAjustes.todo, 'una', id] as const,
};
