import { useSyncExternalStore } from 'react';

import { leerSesion, observarSesion } from './deposito.ts';
import type { Sesion } from './sesion.ts';

/**
 * La sesión de ahora mismo, o `null` si no hay ninguna.
 *
 * `useSyncExternalStore` y no un `useState` sincronizado por un efecto: el depósito ya es la
 * verdad, y copiarla al estado de React sería una segunda verdad que se queda atrás justo cuando
 * importa —entre el 401 y el reintento con el testigo nuevo—.
 */
export function useSesion(): Sesion | null {
  return useSyncExternalStore(observarSesion, leerSesion, leerSesion);
}

/**
 * La sesión, dando por hecho que hay una.
 *
 * Solo se usa por debajo de una ruta protegida, que es quien garantiza que la hay. Si se llama
 * fuera de ahí, revienta con un mensaje que dice qué se ha montado mal, en vez de dejar pasar un
 * `null` que se manifestaría tres componentes más abajo.
 */
export function useSesionAbierta(): Sesion {
  const sesion = useSesion();

  if (sesion === null) {
    throw new Error(
      'useSesionAbierta() fuera de una ruta protegida: aquí todavía puede no haber sesión.',
    );
  }

  return sesion;
}
