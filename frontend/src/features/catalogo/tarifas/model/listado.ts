import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va, qué texto filtra y de qué tarifa se
 * están viendo los tramos.
 *
 * Sale entero de la URL, como la paginación y por lo mismo: el listado acotado se puede pegar en un
 * correo, la flecha de atrás deshace el filtro y una recarga no pierde el sitio.
 *
 * **Ninguno de los dos criterios es sensible** (ADR-0025), y eso no se dice solo aquí: `codigo`
 * tuvo que añadirse a la lista declarada de `NingunCriterioSensibleViajaEnLaUrlTests`, que compara
 * los parámetros de todos los listados de la API contra ella. Lo que queda escrito en el registro
 * de acceso es «alguien listó los tramos de la tarifa PVP», que es exactamente lo que un ERP tiene
 * que poder decir en voz alta.
 */
export interface ListadoDeTarifas extends Paginacion {
  /** Trozo de código o de nombre. Cadena vacía para no filtrar. */
  readonly busqueda: string;
  /** El código cuyos tramos se están viendo, o nulo para verlos todos. */
  readonly codigo: string | null;
}

/** El nombre del parámetro del filtro de texto en la barra de direcciones. */
export const PARAMETRO_DE_BUSQUEDA = 'busqueda';

/**
 * El nombre del parámetro del código. Es **el mismo** que espera la API (`?codigo=`).
 *
 * Mismo criterio que el `?categoria=` de los artículos: son el mismo criterio, y ponerle aquí otro
 * nombre obligaría a una traducción cuyo único efecto sería que un enlace copiado de la barra de
 * direcciones no valiera para probar la API a mano.
 */
export const PARAMETRO_DE_CODIGO = 'codigo';

/**
 * Lee de la URL cómo hay que pedir el listado.
 *
 * **El código se normaliza a mayúsculas aquí**, que es la forma en la que está guardado. El
 * servidor lo normaliza también antes de consultar —así que un `?codigo=pvp` encuentra la tarifa
 * igual—, y esto no se apoya en aquello: lo que arregla es la pantalla, que enseña el código por el
 * que está acotando. Sin normalizar, el rótulo diría «pvp» y las filas de debajo «PVP», y quien lo
 * viera se preguntaría cuál de los dos es el de verdad.
 *
 * Un código vacío o en blanco es no filtrar, no filtrar por la cadena vacía: `?codigo=` se escribe
 * solo, borrando lo que había en la barra de direcciones, y acotar por nada no devolvería ninguna
 * fila y parecería que la empresa no tiene tarifas.
 */
export function leerListado(parametros: URLSearchParams, paginacion: Paginacion): ListadoDeTarifas {
  const codigo = (parametros.get(PARAMETRO_DE_CODIGO) ?? '').trim().toUpperCase();

  return {
    ...paginacion,
    busqueda: (parametros.get(PARAMETRO_DE_BUSQUEDA) ?? '').trim(),
    codigo: codigo === '' ? null : codigo,
  };
}
