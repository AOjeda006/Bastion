import type { ListadoDeTransferencias } from '../model/listado.ts';

/**
 * Las claves de consulta de las transferencias, todas en un módulo y jerárquicas.
 *
 * La empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el servidor (R8).
 * Los maestros que la transferencia nombra u ofrece cuelgan de `maestros/api/claves.ts`.
 */
export const clavesDeTransferencias = {
  todo: ['transferencias'] as const,
  listas: () => [...clavesDeTransferencias.todo, 'lista'] as const,
  lista: (listado: ListadoDeTransferencias) =>
    [...clavesDeTransferencias.listas(), listado] as const,
};
