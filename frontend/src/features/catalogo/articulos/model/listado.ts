import { z } from 'zod';

import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va, qué texto filtra y en qué rama.
 *
 * Sale entero de la URL, como la paginación y por lo mismo: el listado acotado se puede pegar en un
 * correo, la flecha de atrás deshace el filtro y una recarga no pierde el sitio.
 *
 * **Ninguno de los dos criterios es sensible** (ADR-0025). `busqueda` mira código y descripción de
 * un artículo, que es lo que sale impreso en una factura; `categoria` es el identificador de una
 * rama del árbol de clasificación de la propia empresa. Ni uno ni otro son datos de una persona,
 * que es lo que el ADR saca de la barra de direcciones.
 */
export interface ListadoDeArticulos extends Paginacion {
  /** Trozo de código o de descripción. Cadena vacía para no filtrar. */
  readonly busqueda: string;
  /** La rama por la que se acota, o nula para traerlos todos. */
  readonly categoriaId: string | null;
}

/** El nombre del parámetro del filtro de texto en la barra de direcciones. */
export const PARAMETRO_DE_BUSQUEDA = 'busqueda';

/**
 * El nombre del parámetro de la rama. Es **el mismo** que espera la API (`?categoria=`).
 *
 * Que coincida no es casualidad ni acoplamiento: son el mismo criterio, y ponerle aquí otro nombre
 * obligaría a una traducción de nombres cuyo único efecto sería que un enlace copiado de la barra
 * de direcciones no valiera para probar la API a mano.
 */
export const PARAMETRO_DE_CATEGORIA = 'categoria';

/**
 * `z.guid()` y NO `z.uuid()`, que es la trampa de este fichero.
 *
 * `z.uuid()` exige lo que manda la RFC 9562 —la versión y los bits de variante—, y un `Guid` de
 * .NET no tiene por qué cumplirlos: `11111111-1111-1111-1111-111111111111` es un identificador
 * perfectamente válido para el servidor, que enlaza `Guid?`, y `z.uuid()` lo rechaza. Con el
 * esquema estricto, un enlace legítimo se abriría SIN filtro y sin decir por qué.
 */
const esquemaDeCategoria = z.guid();

/**
 * Lee de la URL cómo hay que pedir el listado.
 *
 * Una categoría que no tiene forma de identificador se ignora, igual que `leerPaginacion` ignora
 * un `?pagina=-4`: viene de fuera, no es un error que enseñar sino ruido, y mandárselo al servidor
 * solo cambiaría una pantalla sin filtro por un 400 que nadie ha provocado a propósito.
 */
export function leerListado(
  parametros: URLSearchParams,
  paginacion: Paginacion,
): ListadoDeArticulos {
  const categoria = esquemaDeCategoria.safeParse(parametros.get(PARAMETRO_DE_CATEGORIA));

  return {
    ...paginacion,
    busqueda: (parametros.get(PARAMETRO_DE_BUSQUEDA) ?? '').trim(),
    categoriaId: categoria.success ? categoria.data : null,
  };
}
