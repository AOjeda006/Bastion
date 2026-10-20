import type { ListadoDeTransferencias } from '../model/listado.ts';

/**
 * Las claves de consulta de las transferencias, todas en un módulo y jerárquicas.
 *
 * La ficha trae las líneas enteras (ADR-0068 §1), así que una transferencia es una sola consulta.
 * La empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el servidor (R8).
 * Los maestros que la transferencia nombra u ofrece cuelgan de `maestros/api/claves.ts`.
 */
export const clavesDeTransferencias = {
  todo: ['transferencias'] as const,
  listas: () => [...clavesDeTransferencias.todo, 'lista'] as const,
  lista: (listado: ListadoDeTransferencias) =>
    [...clavesDeTransferencias.listas(), listado] as const,
  /** Una transferencia: su ficha, con sus líneas y su versión. */
  una: (id: string) => [...clavesDeTransferencias.todo, 'una', id] as const,
};
