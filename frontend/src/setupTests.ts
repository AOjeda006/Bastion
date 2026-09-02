import { transferableAbortController } from 'node:util';

import '@testing-library/jest-dom/vitest';
import { configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { escribirSesion } from '@/shared/sesion/deposito.ts';
import { reiniciarServidor, servidor } from '@/pruebas/servidor.ts';

/**
 * PRIMERO: devolverle al entorno un `AbortController` que su propio `fetch` acepte.
 *
 * jsdom trae su `AbortController`, y vitest lo pone en `globalThis`. Pero jsdom NO trae `fetch`: el
 * que se usa es el de Node, que comprueba `signal instanceof AbortSignal` contra SU clase. Una
 * señal de jsdom no la pasa, y `new Request(url, { signal })` revienta con «Expected signal to be
 * an instance of AbortSignal».
 *
 * Eso no es un fallo de la aplicación —en un navegador de verdad `fetch` y `AbortController` son de
 * la misma realización— sino del entorno de pruebas. Se manifiesta como algo que despista mucho: el
 * enrutador crea un `AbortController` por navegación, la construcción de su `Request` lanza, y la
 * navegación se queda a medias sin decir por qué. Un enlace pulsado que no lleva a ninguna parte.
 *
 * `node:util` es el único sitio por el que se alcanza el `AbortController` de Node desde dentro del
 * entorno ya montado: `transferableAbortController()` devuelve uno.
 */
const deNodo = transferableAbortController();
const ControladorDeNodo = deNodo.constructor as typeof AbortController;
const prototipoDeSenal = Object.getPrototypeOf(deNodo.signal) as {
  constructor: typeof AbortSignal;
};
const SenalDeNodo = prototipoDeSenal.constructor;

globalThis.AbortController = ControladorDeNodo;
globalThis.AbortSignal = SenalDeNodo;
window.AbortController = ControladorDeNodo;
window.AbortSignal = SenalDeNodo;

/**
 * SEGUNDO: un `localStorage` y un `sessionStorage` que de verdad guarden.
 *
 * En Node 25 `globalThis.localStorage` ya viene definido —como par de accesores, experimental y
 * detrás de `--localstorage-file`— y gana al de jsdom. Lo que queda en el entorno es un objeto
 * pelado, sin `setItem` ni `clear`: `Object.getPrototypeOf(localStorage)` es `Object`, no `Storage`.
 *
 * Dejarlo así envenenaría justo el test que importa. «El testigo no llega a `localStorage`» pasaría
 * siempre, incluso con el testigo guardándose, porque la línea que lo guardara reventaría antes con
 * un `TypeError` — verde por avería del entorno, no por la propiedad. Así que se pone un almacén de
 * verdad, con la API estándar, y la prueba vuelve a significar lo que dice.
 *
 * Guarda solo lo que entre por `setItem`, que es la API por la que entra todo lo que se guarda de
 * verdad; `almacen.loQueSea = 'x'` no se registra.
 */
class AlmacenEnMemoria implements Storage {
  readonly #datos = new Map<string, string>();

  get length(): number {
    return this.#datos.size;
  }

  clear(): void {
    this.#datos.clear();
  }

  getItem(clave: string): string | null {
    return this.#datos.get(clave) ?? null;
  }

  key(indice: number): string | null {
    return [...this.#datos.keys()][indice] ?? null;
  }

  removeItem(clave: string): void {
    this.#datos.delete(clave);
  }

  setItem(clave: string, valor: string): void {
    this.#datos.set(clave, valor);
  }
}

for (const nombre of ['localStorage', 'sessionStorage'] as const) {
  // `defineProperty` y no una asignación: lo que hay debajo es un accesor, y asignarle encima deja
  // el accesor en pie y el valor donde no se lee.
  Object.defineProperty(globalThis, nombre, {
    value: new AlmacenEnMemoria(),
    writable: true,
    configurable: true,
    enumerable: true,
  });
}

/**
 * El segundo que trae Testing Library por omisión se queda corto aquí, y no por lentitud de la
 * aplicación: cada test monta el armazón ENTERO, que arranca pidiendo la sesión con la cookie antes
 * de pintar nada. Con los ficheros de test corriendo en paralelo eso pasa del segundo de vez en
 * cuando, y un test que falla unas veces sí y otras no es peor que no tenerlo. El plazo no tapa
 * nada: si algo se cuelga de verdad, sigue fallando.
 */
configure({ asyncUtilTimeout: 5000 });

/**
 * `onUnhandledRequest: 'error'` a propósito: una petición que ningún manejador esperaba es un
 * agujero en el test, no un detalle. Sin esto, una llamada que se cuela a la red de verdad se
 * queda colgada y el test falla por tiempo agotado, que es el peor diagnóstico posible.
 */
beforeAll(() => {
  servidor.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  servidor.resetHandlers();
  reiniciarServidor();
  // El depósito de sesión es una variable de módulo: si no se vacía, el test siguiente arranca con
  // la sesión del anterior y pasa por motivos que no son suyos.
  escribirSesion(null);
});

afterAll(() => {
  servidor.close();
});
