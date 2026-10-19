import { api } from '@/shared/api/cliente.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * Los maestros de otros módulos que las pantallas del inventario nombran u ofrecen (ADR-0055 §12).
 *
 * Un documento del inventario identifica el almacén, el artículo, la ubicación y la unidad por su
 * identificador, como todo el módulo. Esta funcionalidad no puede importar las del catálogo ni las
 * de la organización: **pregunta a sus dueños por su API pública**, y se queda con el código y el
 * nombre, que es lo que pinta. Viven aquí y no en una de sus pantallas porque los leen todas
 * (ADR-0065 §8).
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

/** Una ubicación, con el almacén al que pertenece: el alta del ajuste solo ofrece las suyas. */
export interface Ubicacion extends Maestro {
  readonly almacenId: string;
}

/**
 * Si el artículo se sigue por lote, por número de serie o por ninguno. `desconocida` es lo que
 * llegue mañana: el enumerado viaja como texto.
 */
export type Trazabilidad = 'ninguna' | 'porLote' | 'porNumeroSerie' | 'desconocida';

/** Un artículo encontrado al escribir en la línea, con lo que la línea necesita saber de él. */
export interface ArticuloEncontrado extends Maestro {
  readonly trazabilidad: Trazabilidad;
  /** La unidad en la que el libro lo cuenta, que es la que la línea ofrece primero. */
  readonly unidadBaseId: string;
}

const TRAZABILIDADES: ReadonlyMap<string, Trazabilidad> = new Map([
  ['Ninguna', 'ninguna'],
  ['PorLote', 'porLote'],
  ['PorNumeroSerie', 'porNumeroSerie'],
]);

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

/**
 * Las ubicaciones de la empresa, de todos sus almacenes: la API de la organización no acota por
 * almacén, y quien elige es la línea del alta.
 */
export async function consultarUbicaciones(): Promise<Ubicacion[]> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/ubicaciones', {
    params: { query: { page: 1, size: LOS_QUE_CABEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((u) => ({
    id: u.id,
    codigo: u.codigo,
    nombre: u.descripcion,
    almacenId: u.almacenId,
  }));
}

/** Las unidades de medida de la instalación, sin las retiradas, que es como las da la API. */
export async function consultarUnidades(): Promise<Maestro[]> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/unidades-de-medida', {
    params: { query: { page: 1, size: LOS_QUE_CABEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((u) => ({ id: u.id, codigo: u.codigo, nombre: u.nombre }));
}

/** Lo que se ofrece de cada búsqueda: los primeros que casan, que es lo que cabe en la lista. */
const LOS_QUE_SE_OFRECEN = 10;

/**
 * Los artículos que casan con lo escrito, por el filtro de texto del listado del catálogo, que mira
 * el código y la descripción (ADR-0065 §8).
 */
export async function buscarArticulos(busqueda: string): Promise<ArticuloEncontrado[]> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/articulos', {
    params: { query: { q: busqueda, page: 1, size: LOS_QUE_SE_OFRECEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((a) => ({
    id: a.id,
    codigo: a.codigo,
    nombre: a.descripcion,
    trazabilidad: TRAZABILIDADES.get(a.trazabilidad) ?? 'desconocida',
    unidadBaseId: a.unidadBaseId,
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
