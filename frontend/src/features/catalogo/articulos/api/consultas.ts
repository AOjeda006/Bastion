import type { ListadoDeArticulos } from '../model/listado.ts';
import type { Articulo, PaginaDeArticulos, TipoDeArticulo } from '../model/articulo.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de los artículos con el contrato.
 *
 * `ArticuloDto` viene de `esquema.ts`, que se GENERA de `docs/api/openapi.json` con `npm run api`.
 * Nunca se escribe a mano un tipo del contrato: si mañana un artículo dejara de traer la categoría,
 * esto deja de compilar aquí —en una función de diez líneas— y no en la tabla que lo pintaba.
 */
type ArticuloDto = components['schemas']['ArticuloDto'];

/**
 * Del texto del contrato al valor que esta funcionalidad pinta.
 *
 * El tipo viaja como texto —`Bien` o `Servicio`—, así que traducirlo es leer un dato de fuera: lo
 * que no sea uno de los dos conocidos sale como `desconocido` y la tabla lo dice, en vez de dejar
 * la celda en blanco, que es lo que se ve cuando algo está roto.
 */
function tipoDe(texto: string): TipoDeArticulo {
  if (texto === 'Bien') {
    return 'bien';
  }

  return texto === 'Servicio' ? 'servicio' : 'desconocido';
}

function traducir(dto: ArticuloDto): Articulo {
  return {
    id: dto.id,
    codigo: dto.codigo,
    descripcion: dto.descripcion,
    tipo: tipoDe(dto.tipo),
    categoriaId: dto.categoriaId,
  };
}

/**
 * Pide una página de artículos de la empresa con la que se está operando.
 *
 * El filtro de texto viaja como `q`, que en esta ruta busca por código y descripción; la rama, como
 * `categoria`. **Acota por la categoría dicha, no por su subárbol**, que es la consecuencia directa
 * de haber modelado el árbol como lista de adyacencia (el motivo entero, en `docs/PLAN.md`).
 */
export async function consultarArticulos(listado: ListadoDeArticulos): Promise<PaginaDeArticulos> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/articulos', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.busqueda === '' ? {} : { q: listado.busqueda }),
        ...(listado.categoriaId === null ? {} : { categoria: listado.categoriaId }),
      },
    },
  });

  if (data === undefined) {
    // El cuerpo del error va con el estado: de él salen el `type` y la traza, que son lo que decide
    // qué frase lee una persona (ADR-0030, y `useTextoDeFallo`).
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}
