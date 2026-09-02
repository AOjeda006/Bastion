import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/** Las claves de consulta de empresas, jerárquicas y en un único módulo (ver `almacenes/api`). */
export const clavesDeEmpresas = {
  todo: ['empresas'] as const,
  listas: () => [...clavesDeEmpresas.todo, 'lista'] as const,
  lista: (paginacion: Paginacion) => [...clavesDeEmpresas.listas(), paginacion] as const,
};
