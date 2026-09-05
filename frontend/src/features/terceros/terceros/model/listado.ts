import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Cómo se está mirando el listado ahora mismo: por dónde va y qué está filtrando.
 *
 * Sale entero de la URL, como la paginación y por lo mismo: así el listado filtrado se puede
 * pegar en un correo, la flecha de atrás deshace el filtro y una recarga no lo pierde.
 *
 * **`busqueda` filtra por razón social y nombre comercial, NUNCA por identificador fiscal**, y esa
 * frase es la mitad interesante de este fichero. Lo que va en la URL queda escrito en el historial
 * del navegador, en el enlace que se copia por chat, en la cabecera `Referer` que el navegador
 * manda al sitio siguiente y en el registro de acceso del servidor de delante — que se guarda más
 * tiempo y con menos cuidado que la base de datos. Un trozo de nombre comercial ahí es un dato de
 * pantalla; el NIF de un cliente es muy a menudo el DNI de una persona física, y por eso buscar
 * por él va por el cuerpo, con `POST .../buscar` (ADR-0025).
 */
export interface ListadoDeTerceros extends Paginacion {
  /** Trozo de razón social o nombre comercial. Cadena vacía para no filtrar. */
  readonly busqueda: string;
}

/** El nombre del parámetro del filtro en la barra de direcciones. */
export const PARAMETRO_DE_BUSQUEDA = 'busqueda';

/** Lee de la URL cómo hay que pedir el listado. */
export function leerListado(
  parametros: URLSearchParams,
  paginacion: Paginacion,
): ListadoDeTerceros {
  return { ...paginacion, busqueda: (parametros.get(PARAMETRO_DE_BUSQUEDA) ?? '').trim() };
}
