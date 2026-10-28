import { fallo } from './errores.ts';

/**
 * La exportación de un listado, del lado del navegador (ítem 3.2, ADR-0073).
 *
 * **El fichero lo escribe el servidor, entero, y aquí solo se guarda.** El navegador pide la ruta
 * hermana del listado, `…/exportacion`, con los mismos filtros y el mismo orden que la pantalla
 * manda al listado y sin `page` ni `size`, y recibe el CSV en flujo. Lo pide por el cliente de
 * siempre, con `fetch`, y no con un enlace: así lleva el testigo, que vive en memoria, y repite la
 * petición una vez tras un `401`, como cualquier otra (respuesta 3 de la puerta del 3.2).
 *
 * Vive en `shared/` porque la usan las cuatro funcionalidades que tienen listados.
 */

/** Un fichero que el servidor ha mandado para guardar: lo que lleva y cómo lo llama. */
export interface Fichero {
  readonly contenido: Blob;
  readonly nombre: string;
}

/**
 * Lo que contesta el cliente a una exportación pedida con `parseAs: 'blob'`: el fichero, o el
 * cuerpo del error, que el cliente lee como JSON aunque se haya pedido un `Blob`.
 */
interface RespuestaDeExportacion {
  readonly data?: Blob;
  readonly error?: unknown;
  readonly response: Response;
}

/** El nombre que se le pone al fichero cuando la respuesta no trae ninguno. */
export const NOMBRE_SIN_ADJUNTO = 'exportacion.csv';

/**
 * El nombre del fichero, del `Content-Disposition` de la respuesta.
 *
 * El servidor manda los dos parámetros, `filename*` y `filename`, con el mismo valor
 * (`<recurso>-AAAA-MM-DD.csv`, con el día de negocio). Se lee primero el de la RFC 6266, que es el
 * que manda cuando vienen los dos. Sin ninguno, el de {@link NOMBRE_SIN_ADJUNTO}: el fichero se
 * guarda igual.
 *
 * @param cabecera - El valor de `Content-Disposition`, o `null` si no viene.
 * @returns El nombre del fichero.
 */
export function nombreDelAdjunto(cabecera: string | null): string {
  const extendido = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(cabecera ?? '')?.[1];

  if (extendido !== undefined) {
    return decodeURIComponent(extendido.trim());
  }

  const sencillo = /filename\s*=\s*("?)([^";]+)\1/i.exec(cabecera ?? '')?.[2];

  return sencillo?.trim() ?? NOMBRE_SIN_ADJUNTO;
}

/**
 * El fichero de una exportación, o su fallo.
 *
 * Lo que el listado rechaza, la exportación lo rechaza antes del primer byte, con el mismo
 * ProblemDetails (ADR-0073 §4): por eso el fallo sale por {@link fallo}, como el del listado, y la
 * pantalla lo dice con la misma frase.
 *
 * @param respuesta - Lo que ha contestado el cliente.
 * @returns El fichero, con el nombre que le pone el servidor.
 * @throws {FalloDeApi} Si el servidor no ha mandado el fichero.
 */
export function ficheroDe({ data, error, response }: RespuestaDeExportacion): Fichero {
  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { contenido: data, nombre: nombreDelAdjunto(response.headers.get('Content-Disposition')) };
}
