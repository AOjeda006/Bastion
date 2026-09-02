import { api } from './cliente.ts';
import { traducirSesion } from './traduccion.ts';
import type { Sesion } from '@/shared/sesion/sesion.ts';

/**
 * Las cuatro operaciones de sesión, ya traducidas al modelo de la aplicación.
 *
 * Cada una emite un testigo NUEVO. Eso importa fuera de aquí: quien cambia de empresa cambia con
 * qué datos se está trabajando, y lo que hubiera en la caché de consultas es de la empresa anterior.
 */

/** Lo que devuelve una operación de sesión: la sesión, o el motivo por el que no hay. */
export type ResultadoDeSesion = { readonly sesion: Sesion } | { readonly error: string };

const CREDENCIALES = 'El correo o la contraseña no son correctos.';
const SIN_RED = 'No se ha podido contactar con el servidor. Inténtalo de nuevo.';

/** Abre sesión con correo y contraseña. */
export async function iniciarSesion(
  correo: string,
  contrasena: string,
): Promise<ResultadoDeSesion> {
  const { data, response } = await api.POST('/api/v1/identidad/sesiones', {
    body: { correo, contrasena },
  });

  if (data === undefined) {
    // El 401 de credenciales se traduce a UN mensaje, el mismo para «no existe ese correo» y para
    // «la contraseña no es esa»: distinguirlos le diría a quien prueba correos cuáles existen.
    return { error: response.status === 401 ? CREDENCIALES : SIN_RED };
  }

  return { sesion: traducirSesion(data) };
}

/** Cambia con qué empresa se opera (R8). El testigo nuevo lleva la empresa dentro. */
export async function cambiarEmpresa(empresaId: string): Promise<ResultadoDeSesion> {
  const { data } = await api.PUT('/api/v1/identidad/sesiones/actual/empresa', {
    body: { empresaId },
  });

  if (data === undefined) {
    return { error: 'No se ha podido cambiar de empresa. Vuelve a intentarlo.' };
  }

  return { sesion: traducirSesion(data) };
}

/**
 * Cierra la sesión: revoca la familia de testigos en el servidor y borra la cookie.
 *
 * No devuelve nada porque no hay nada que decidir: salga bien o mal, en este navegador la sesión se
 * acaba. Que el servidor no conteste no es motivo para dejar a alguien dentro.
 */
export async function cerrarSesion(): Promise<void> {
  await api.DELETE('/api/v1/identidad/sesiones/actual', {});
}
