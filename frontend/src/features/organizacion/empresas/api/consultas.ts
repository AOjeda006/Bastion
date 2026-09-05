import type { Empresa, PaginaDeEmpresas } from '../model/empresa.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/** La frontera de esta funcionalidad con el contrato. El tipo es el GENERADO, no uno escrito aquí. */
type EmpresaDto = components['schemas']['EmpresaDto'];

function traducir(dto: EmpresaDto): Empresa {
  return {
    id: dto.id,
    nif: dto.nif,
    razonSocial: dto.razonSocial,
    poblacion: dto.domicilioFiscal.poblacion,
    divisaBase: dto.divisaBase,
  };
}

/**
 * Pide una página de empresas.
 *
 * OJO con lo que devuelve: NO es lo mismo que el selector de la cabecera. El selector trae las
 * empresas a las que se pertenece —y en las que se puede operar—, venga de donde venga el permiso;
 * esto es el listado del módulo de organización y exige `organizacion.empresa.ver`. Se puede tener
 * lo primero sin lo segundo, y es lo normal para quien no administra.
 */
export async function consultarEmpresas(paginacion: Paginacion): Promise<PaginaDeEmpresas> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/empresas', {
    params: { query: { page: paginacion.pagina, size: paginacion.tamanio } },
  });

  if (data === undefined) {
    // El cuerpo del error va con el estado: de él salen el `type` y la traza, que son
    // lo que decide qué frase lee una persona (ADR-0030, y `useTextoDeFallo`).
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}
