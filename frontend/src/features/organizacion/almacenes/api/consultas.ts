import type { Almacen, PaginaDeAlmacenes } from '../model/almacen.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * La frontera de esta funcionalidad con el contrato.
 *
 * `AlmacenDto` viene de `esquema.ts`, que se GENERA de `docs/api/openapi.json` con `npm run api`.
 * Nunca se escribe a mano un tipo del contrato: si mañana `AlmacenDto` pierde `direccion`, esto
 * deja de compilar aquí —en una función de cinco líneas— y no en la tabla que lo pintaba.
 */
type AlmacenDto = components['schemas']['AlmacenDto'];

function traducir(dto: AlmacenDto): Almacen {
  return {
    id: dto.id,
    codigo: dto.codigo,
    nombre: dto.nombre,
    tipo: dto.tipo,
    poblacion: dto.direccion?.poblacion ?? null,
  };
}

/** Pide una página de almacenes de la empresa con la que se está operando. */
export async function consultarAlmacenes(paginacion: Paginacion): Promise<PaginaDeAlmacenes> {
  const { data, response } = await api.GET('/api/v1/organizacion/almacenes', {
    params: { query: { page: paginacion.pagina, size: paginacion.tamanio } },
  });

  if (data === undefined) {
    throw fallo(response.status);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}
