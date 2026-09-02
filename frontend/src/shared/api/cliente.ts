import createClient from 'openapi-fetch';

import type { paths } from './esquema.ts';
import { traducirCuerpoDeSesion } from './traduccion.ts';
import { escribirSesion, leerSesion, leerTestigo } from '@/shared/sesion/deposito.ts';

/**
 * El cliente HTTP: tipado por el contrato, con el testigo en memoria y un solo reintento tras 401.
 *
 * MISMO ORIGEN, A PROPÓSITO
 * -------------------------
 * No hay URL absoluta ni `VITE_API_URL` en el empaquetado: las peticiones van a `/api/...` del
 * mismo origen, y quien decide dónde está la API es el proxy (Vite en desarrollo, nginx en el
 * contenedor). Tiene que ser así porque la cookie de refresco es `__Host-bastion-refresco`, y ese
 * prefijo exige `Secure`, sin `Domain` y `Path=/`: solo viaja al origen exacto que la puso.
 *
 * EL REINTENTO
 * ------------
 * El testigo de acceso dura quince minutos y el de refresco catorce días. Cuando el primero
 * caduca, el servidor responde 401 y aquí se pide uno nuevo con la cookie —que este código no
 * puede leer, porque es `HttpOnly`— y se repite la petición UNA vez. Si la renovación tampoco
 * vale, se borra la sesión y la aplicación manda a la pantalla de acceso.
 */

const RENOVACION = '/api/v1/identidad/sesiones/renovacion';

/**
 * El origen de la propia página. No hay `VITE_API_URL` ni ninguna URL absoluta empaquetada: la API
 * está donde esté servido el frontal, y quien decide dónde es eso es el proxy.
 *
 * Se escribe el origen en vez de dejar la ruta relativa porque `fetch` fuera de un navegador —el de
 * los tests— no tiene documento contra el que resolverla. En el navegador el resultado es el mismo
 * y sigue siendo el mismo origen, que es lo que la cookie `__Host-` exige.
 */
const ORIGEN = globalThis.location.origin;

/** Renovación en vuelo, si la hay. Diez peticiones que caducan a la vez piden UNA sesión nueva. */
let renovacionEnCurso: Promise<boolean> | null = null;

function conTestigo(peticion: Request): Request {
  const testigo = leerTestigo();

  if (testigo === null) {
    return peticion;
  }

  const cabeceras = new Headers(peticion.headers);
  cabeceras.set('Authorization', `Bearer ${testigo}`);

  return new Request(peticion, { headers: cabeceras });
}

async function pedirSesionNueva(): Promise<boolean> {
  // Con `fetch` pelado y no con el cliente: si la renovación pasara por aquí, un 401 suyo
  // dispararía otra renovación, y otra.
  const respuesta = await fetch(`${ORIGEN}${RENOVACION}`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  });

  if (!respuesta.ok) {
    escribirSesion(null);
    return false;
  }

  escribirSesion(traducirCuerpoDeSesion(await respuesta.json()));
  return true;
}

function renovar(): Promise<boolean> {
  renovacionEnCurso ??= pedirSesionNueva().finally(() => {
    renovacionEnCurso = null;
  });

  return renovacionEnCurso;
}

async function enviar(peticion: Request): Promise<Response> {
  // La copia se saca ANTES de mandar nada: el cuerpo de una petición se lee una sola vez, y sin
  // ella el reintento saldría sin cuerpo — un `PUT` vacío que el servidor rechazaría por otra cosa.
  const reserva = peticion.clone();

  const respuesta = await fetch(conTestigo(peticion));

  const reintentable =
    respuesta.status === 401 && !peticion.url.endsWith(RENOVACION) && leerSesion() !== null;

  if (!reintentable) {
    return respuesta;
  }

  return (await renovar()) ? fetch(conTestigo(reserva)) : respuesta;
}

/** Cliente tipado por el contrato. Las rutas y los cuerpos los comprueba el compilador. */
export const api = createClient<paths>({
  baseUrl: ORIGEN,
  fetch: enviar,
  credentials: 'same-origin',
});

/**
 * Abre una sesión con la cookie de refresco, si es que hay una.
 *
 * Es lo que se hace al arrancar la aplicación: como el testigo de acceso vive en memoria, una
 * recarga de página lo pierde siempre. Devolver `false` no es un error —significa «aquí no había
 * ninguna sesión»— y la aplicación pinta la pantalla de acceso.
 */
export function recuperarSesion(): Promise<boolean> {
  return renovar();
}
