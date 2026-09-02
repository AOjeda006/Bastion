import { api } from './cliente.ts';
import { traducirSesion } from './traduccion.ts';
import type { Sesion } from '@/shared/sesion/sesion.ts';

/**
 * Las cuatro operaciones de sesión, ya traducidas al modelo de la aplicación.
 *
 * Cada una emite un testigo NUEVO. Eso importa fuera de aquí: quien cambia de empresa cambia con
 * qué datos se está trabajando, y lo que hubiera en la caché de consultas es de la empresa anterior.
 */

/**
 * Por qué no se ha podido entrar. Motivos, no frases: esta capa es red y no sabe en qué idioma se
 * está (ver el mismo razonamiento, entero, en `errores.ts`).
 *
 * Cada operación devuelve su unión EXACTA y no una común: así la pantalla de acceso no tiene que
 * contemplar «no se ha podido cambiar de empresa», que ahí no puede pasar, y el compilador la
 * obliga a cubrir los dos que sí.
 */
export type MotivoDeAcceso = 'credenciales' | 'sinRed';
export type MotivoDeCambioDeEmpresa = 'cambioDeEmpresa';

/** Lo que devuelve una operación de sesión: la sesión, o el motivo por el que no hay. */
export type ResultadoDeSesion<M extends string> =
  { readonly sesion: Sesion } | { readonly error: M };

/** Abre sesión con correo y contraseña. */
export async function iniciarSesion(
  correo: string,
  contrasena: string,
): Promise<ResultadoDeSesion<MotivoDeAcceso>> {
  const { data, response } = await api.POST('/api/v1/identidad/sesiones', {
    body: { correo, contrasena },
  });

  if (data === undefined) {
    // El 401 de credenciales se traduce a UN mensaje, el mismo para «no existe ese correo» y para
    // «la contraseña no es esa»: distinguirlos le diría a quien prueba correos cuáles existen.
    return { error: response.status === 401 ? 'credenciales' : 'sinRed' };
  }

  return { sesion: traducirSesion(data) };
}

/** Cambia con qué empresa se opera (R8). El testigo nuevo lleva la empresa dentro. */
export async function cambiarEmpresa(
  empresaId: string,
): Promise<ResultadoDeSesion<MotivoDeCambioDeEmpresa>> {
  const { data } = await api.PUT('/api/v1/identidad/sesiones/actual/empresa', {
    body: { empresaId },
  });

  if (data === undefined) {
    return { error: 'cambioDeEmpresa' };
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
