import '@/index.css';

import { cleanup } from '@testing-library/react';
import { getResponse, type RequestHandler } from 'msw';
import { afterEach } from 'vitest';

import { manejadores, reiniciarServidor } from './manejadores.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';

/**
 * EL ARRANQUE DEL CARRIL DEL NAVEGADOR (ítem 2.20), el de `vitest.navegador.config.ts`.
 *
 * Es el `setupTests.ts` de un Chromium de verdad, y hace lo mismo con otras piezas. Lo que no hace
 * falta aquí es lo que allí remienda jsdom: el `AbortController` y el `localStorage` del navegador
 * son los suyos, y funcionan.
 *
 * **La hoja de estilos, entera.** Es lo que el carril mide: lo que ocupa una página con su
 * Tailwind. En jsdom no hay disposición, y `css: false` la quita del todo.
 */

/** Los manejadores que pone un caso, por delante de los de siempre. Se vacían entre caso y caso. */
let delCaso: RequestHandler[] = [];

/** Como `servidor.use(...)` en el carril de jsdom: lo último que se pone es lo primero que contesta. */
export function usarEnElNavegador(...nuevos: RequestHandler[]): void {
  delCaso = [...nuevos, ...delCaso];
}

/**
 * **La API, en la frontera de red, como en jsdom**, pero sin *service worker*. `msw/node` no carga
 * en un navegador, y el `setupWorker` del navegador necesita servir su fichero desde la raíz. Así
 * que se cambia el `fetch` de la página por uno que pregunta a los mismos manejadores con
 * `getResponse`, que es la pieza de msw que los resuelve.
 *
 * **Solo lo de `/api/`**: lo demás va al `fetch` de verdad, porque el carril también lo usa para
 * sus cosas. Y una petición a la API que ningún manejador espera es un error, como el
 * `onUnhandledRequest: 'error'` de jsdom: un hueco en el caso, no un detalle.
 */
const fetchDeVerdad = globalThis.fetch.bind(globalThis);

globalThis.fetch = async (entrada: RequestInfo | URL, opciones?: RequestInit) => {
  const peticion = new Request(entrada, opciones);

  if (!new URL(peticion.url).pathname.startsWith('/api/')) {
    return fetchDeVerdad(peticion);
  }

  const respuesta = await getResponse([...delCaso, ...manejadores], peticion);

  if (respuesta === undefined) {
    throw new Error(`Ningún manejador espera ${peticion.method} ${peticion.url}`);
  }

  return respuesta;
};

afterEach(() => {
  // Desmontar primero, por lo mismo que en `setupTests.ts`: vaciar la sesión con la pantalla en
  // pie la repinta fuera de `act()`.
  cleanup();

  delCaso = [];
  reiniciarServidor();
  escribirSesion(null);
});
