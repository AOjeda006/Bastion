import type { Categoria, PaginaDeCategorias } from '../model/categoria.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';
import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/** La frontera de las categorías con el contrato. Se GENERA: `npm run api`. */
type CategoriaDto = components['schemas']['CategoriaDto'];

function traducir(dto: CategoriaDto): Categoria {
  return { id: dto.id, codigo: dto.codigo, nombre: dto.nombre, padreId: dto.padreId };
}

/**
 * Pide una página de categorías.
 *
 * Vienen PLANAS, con el padre de cada una: el árbol lo compone `model/arbol.ts`. Y vienen
 * paginadas como todo lo demás, que es lo que hace posible que a una categoría se le quede el
 * padre en la página anterior — ese caso lo contempla la composición, no se descarta la fila.
 */
export async function consultarCategorias(paginacion: Paginacion): Promise<PaginaDeCategorias> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/categorias', {
    params: { query: { page: paginacion.pagina, size: paginacion.tamanio } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}

/**
 * Pide UNA categoría por su identificador.
 *
 * Existe para poner nombre a la rama por la que se está filtrando en la pantalla de artículos.
 * Traerse el catálogo entero de categorías para resolver un solo nombre sería pedir hasta
 * doscientas filas —y quedarse corto en la empresa que tenga más— para leer una.
 */
export async function consultarCategoria(id: string): Promise<Categoria> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/categorias/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}
