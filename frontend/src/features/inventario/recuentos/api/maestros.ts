import { api } from '@/shared/api/cliente.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * Los maestros de otros módulos que la pantalla del recuento nombra u ofrece (ADR-0055 §12).
 *
 * La ficha del recuento identifica el almacén, el artículo, la ubicación y la unidad por su
 * identificador, como todo el módulo. Esta funcionalidad no puede importar las del catálogo ni las
 * de la organización: **pregunta a sus dueños por su API pública**, y se queda con el código y el
 * nombre, que es lo que pinta.
 */
export interface Maestro {
  readonly id: string;
  readonly codigo: string;
  /** El nombre o la descripción, o nulo si el maestro no tiene. */
  readonly nombre: string | null;
}

/** Una serie, con lo que hace falta para ofrecerla: qué numera y si está activa. */
export interface Serie extends Maestro {
  readonly tipoDeDocumento: string;
  readonly activa: boolean;
}

/** Qué numeran las dos series del alta, con el texto del contrato (`TipoDeDocumento`). */
export const SERIE_DE_RECUENTOS = 'RecuentoDeInventario';
export const SERIE_DE_AJUSTES = 'AjusteDeInventario';

/**
 * El tope de la página de los desplegables, que es el del servidor.
 *
 * Una empresa con más de doscientos almacenes o series no verá los siguientes en el alta. Queda
 * anotado en el README de la funcionalidad, con su disparador.
 */
const LOS_QUE_CABEN = 200;

export async function consultarAlmacenes(): Promise<Maestro[]> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/almacenes', {
    params: { query: { page: 1, size: LOS_QUE_CABEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((a) => ({ id: a.id, codigo: a.codigo, nombre: a.nombre }));
}

export async function consultarSeries(): Promise<Serie[]> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/series', {
    params: { query: { page: 1, size: LOS_QUE_CABEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((s) => ({
    id: s.id,
    codigo: s.codigo,
    nombre: null,
    tipoDeDocumento: s.tipoDeDocumento,
    activa: s.estado === 'Activa',
  }));
}

export async function consultarAlmacen(id: string): Promise<Maestro> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/almacenes/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { id: data.id, codigo: data.codigo, nombre: data.nombre };
}

export async function consultarArticulo(id: string): Promise<Maestro> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/articulos/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { id: data.id, codigo: data.codigo, nombre: data.descripcion };
}

export async function consultarUbicacion(id: string): Promise<Maestro> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/ubicaciones/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { id: data.id, codigo: data.codigo, nombre: data.descripcion };
}

export async function consultarUnidad(id: string): Promise<Maestro> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/unidades-de-medida/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { id: data.id, codigo: data.codigo, nombre: data.nombre };
}
