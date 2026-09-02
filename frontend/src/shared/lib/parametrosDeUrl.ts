import { z } from 'zod';

/**
 * La paginación vive en la URL, no en `useState`.
 *
 * Así un listado en la página 3 es enlazable, compartible y sobrevive a una recarga — que en un ERP
 * es la mitad de las peticiones de soporte resueltas—. Y así el botón «atrás» del navegador hace lo
 * que el usuario espera en vez de sacarle de la pantalla.
 *
 * Los valores vienen de la barra de direcciones, o sea de fuera: se validan antes de usarlos. Un
 * `?pagina=-4` o un `?tamanio=99999` no son un error que enseñar, son ruido que se ignora cayendo a
 * lo razonable; el tope de 200 es el mismo que impone el servidor.
 */
const TAMANIO_POR_OMISION = 20;

const esquemaDePaginacion = z.object({
  pagina: z.coerce.number().int().min(1).catch(1),
  tamanio: z.coerce.number().int().min(1).max(200).catch(TAMANIO_POR_OMISION),
});

/** Cómo se está paginando ahora mismo, según la URL. */
export interface Paginacion {
  readonly pagina: number;
  readonly tamanio: number;
}

export function leerPaginacion(parametros: URLSearchParams): Paginacion {
  return esquemaDePaginacion.parse({
    pagina: parametros.get('pagina') ?? 1,
    tamanio: parametros.get('tamanio') ?? TAMANIO_POR_OMISION,
  });
}
