import { traducirAlmacen } from './consultas.ts';
import type { Almacen } from '../model/almacen.ts';
import { zonaParaElContrato } from '../../zonas/api/contrato.ts';
import type { ZonaDeAlmacen } from '../../zonas/model/zona.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { fallo } from '@/shared/api/errores.ts';

type ModificarAlmacenDto = components['schemas']['ModificarAlmacenDto'];

/**
 * Un almacén leído para cambiarle la zona: lo que se pinta, la versión de su `ETag`, y lo demás tal
 * como llegó, en la forma del contrato. **El `PUT` reemplaza el almacén entero** —dejar fuera la
 * zona es volver a heredarla—, así que lo demás se devuelve sin tocar, sobre la versión con la que
 * se leyó. La pantalla no mira dentro de `intacto`: solo lo pasa de vuelta.
 */
export interface FichaDeAlmacen {
  readonly almacen: Almacen;
  /** El `ETag` de la lectura, que vuelve en el `If-Match`. */
  readonly version: string;
  readonly intacto: Omit<ModificarAlmacenDto, 'zonaHoraria'>;
}

/**
 * Lee UN almacén con su versión. Sin `ETag` no se podría guardar, y se trata como un fallo de carga,
 * como la ficha del artículo: un formulario que acabaría en un `428` no se enseña.
 */
export async function consultarFichaDeAlmacen(id: string): Promise<FichaDeAlmacen> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/almacenes/{id}', {
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
    almacen: traducirAlmacen(data),
    version,
    intacto: { nombre: data.nombre, direccion: data.direccion, tipo: data.tipo },
  };
}

/**
 * Guarda el almacén leído con otra zona: una de las tres, o nula, que es volver a la de su empresa.
 * Devuelve el almacén como queda, que dice la zona que rige ahora, también cuando la hereda.
 */
export async function cambiarZonaDelAlmacen(
  ficha: FichaDeAlmacen,
  zona: ZonaDeAlmacen,
): Promise<Almacen> {
  const { data, error, response } = await api.PUT('/api/v1/organizacion/almacenes/{id}', {
    params: { path: { id: ficha.almacen.id }, header: { 'If-Match': ficha.version } },
    body: {
      ...ficha.intacto,
      zonaHoraria: zona === 'deLaEmpresa' ? null : zonaParaElContrato(zona),
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirAlmacen(data);
}
