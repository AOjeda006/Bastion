import { traducirEmpresa } from './consultas.ts';
import type { Empresa } from '../model/empresa.ts';
import { zonaParaElContrato } from '../../zonas/api/contrato.ts';
import type { Zona } from '../../zonas/model/zona.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { fallo } from '@/shared/api/errores.ts';

type ModificarEmpresaDto = components['schemas']['ModificarEmpresaDto'];

/**
 * Una empresa leída para cambiarle la zona, como la ficha del almacén (`almacenes/api/zona.ts`):
 * el `PUT` la reemplaza entera, así que lo demás vuelve tal como llegó, con su versión.
 */
export interface FichaDeEmpresa {
  readonly empresa: Empresa;
  /** El `ETag` de la lectura, que vuelve en el `If-Match`. */
  readonly version: string;
  readonly intacto: Omit<ModificarEmpresaDto, 'zonaHoraria'>;
}

/** Lee UNA empresa con su versión; sin `ETag`, es un fallo de carga. */
export async function consultarFichaDeEmpresa(id: string): Promise<FichaDeEmpresa> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/empresas/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  const version = response.headers.get('ETag');

  if (version === null) {
    throw fallo(response.status);
  }

  return {
    empresa: traducirEmpresa(data),
    version,
    intacto: {
      razonSocial: data.razonSocial,
      domicilioFiscal: data.domicilioFiscal,
      divisaBase: data.divisaBase,
      regimenDeIva: data.regimenDeIva,
    },
  };
}

/** Guarda la empresa leída con otra zona, y la devuelve como queda. */
export async function cambiarZonaDeLaEmpresa(ficha: FichaDeEmpresa, zona: Zona): Promise<Empresa> {
  const { data, error, response } = await api.PUT('/api/v1/organizacion/empresas/{id}', {
    params: { path: { id: ficha.empresa.id }, header: { 'If-Match': ficha.version } },
    body: { ...ficha.intacto, zonaHoraria: zonaParaElContrato(zona) },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirEmpresa(data);
}
