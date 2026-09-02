import type { Sesion } from './sesion.ts';

/**
 * Dónde vive la sesión mientras la pestaña está abierta: en una variable de módulo.
 *
 * POR QUÉ NO ES SOLO UN CONTEXT
 * -----------------------------
 * El testigo lo necesita el cliente HTTP, que no es un componente y no puede leer un Context. Y lo
 * necesita también el reintento tras un 401, que ocurre entre dos renderizados. Un depósito de
 * módulo lo resuelve sin exponerlo por ninguna parte más: React se suscribe con
 * `useSyncExternalStore` y ve exactamente lo mismo que ve la capa de red, sin copiarlo.
 *
 * Es la única pieza de estado global de cliente que hay (`stacks/react`: identidad, tema e idioma).
 * No se guarda nada en `localStorage`: al recargar, la sesión se recupera de la cookie de refresco.
 */

let actual: Sesion | null = null;
const oyentes = new Set<() => void>();

/** La sesión de ahora mismo, o `null` si no hay ninguna abierta. */
export function leerSesion(): Sesion | null {
  return actual;
}

/** El testigo de acceso de ahora mismo. Lo usa el cliente HTTP en cada petición. */
export function leerTestigo(): string | null {
  return actual?.testigo ?? null;
}

/**
 * Cambia la sesión y avisa a quien esté mirando.
 *
 * Se llama al entrar, al renovar, al cambiar de empresa y al salir. Cada una de esas cosas emite un
 * testigo NUEVO, y por tanto cambia también qué se puede ver: quien cambia de empresa tiene que
 * tirar además la caché de consultas (ver `usarCambioDeEmpresa`).
 */
export function escribirSesion(sesion: Sesion | null): void {
  actual = sesion;

  for (const avisar of oyentes) {
    avisar();
  }
}

/** Suscripción para `useSyncExternalStore`; devuelve cómo darse de baja. */
export function observarSesion(alCambiar: () => void): () => void {
  oyentes.add(alCambiar);

  return () => {
    oyentes.delete(alCambiar);
  };
}
