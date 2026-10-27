import type { i18n as InstanciaDeI18n } from 'i18next';
import { createBrowserRouter, type RouteObject } from 'react-router';

import { Disposicion } from './Disposicion.tsx';
import { Guarda } from './Guarda.tsx';
import { cargarTextosDe } from './i18n/index.ts';
import { RUTAS, type DeclaracionDeRuta } from './rutas.tsx';
import { Cargando } from '@/shared/ui/Estados.tsx';

/**
 * El enrutador, construido A PARTIR DE LA TABLA. Aquí no se escribe ninguna ruta a mano.
 *
 * Todas cuelgan de la misma disposición —incluidas la de acceso y la de «no encontrada»— porque los
 * tres pasos del cambio de ruta accesible viven ahí: si una ruta se saliera del armazón, entrar en
 * ella sería silencio para un lector de pantalla.
 *
 * Que esto sea un `map` y no una lista escrita es lo que hace honesto al barrido: la única forma de
 * añadir una ruta sin declararla es meterla aquí a mano, y el barrido compara la lista entera del
 * enrutador ya construido contra la tabla.
 *
 * Recibe el i18n porque la carga de una ruta trae, a la vez que su pantalla, los textos de su
 * funcionalidad (ADR-0071): el diccionario del arranque solo lleva el armazón. El dueño que declara
 * la tabla es el que decide cuáles, y las del armazón no traen nada, porque sus textos ya están.
 *
 * Lo recibe como PROMESA, y no por gusto: el enrutador empieza a cargar la pantalla de la URL en
 * cuanto se crea, y `main.tsx` lo crea mientras el diccionario todavía viaja. Así la pantalla y el
 * diccionario se descargan a la vez, como antes del 3.1, y los textos esperan solo a la instancia.
 */
export function crearRutas(i18n: Promise<InstanciaDeI18n>): RouteObject[] {
  return [
    {
      element: <Disposicion />,
      // Mientras el enrutador carga por primera vez el módulo de la pantalla no hay árbol que
      // pintar —ni siquiera la disposición—, y sin esto la pantalla se queda en blanco. Es el mismo
      // «cargando» del resto de la aplicación, no una pantalla aparte.
      HydrateFallback: () => <Cargando que="Bastion" />,
      children: RUTAS.map(
        (declaracion) =>
          ({
            path: declaracion.ruta,
            // `handle` va en el objeto de la ruta y NO dentro del `lazy`: la disposición necesita
            // el título antes de que la pantalla esté cargada, y el barrido lo lee sin ejecutar
            // ninguna carga.
            handle: { declaracion },
            lazy: async () => {
              const [Pagina] = await Promise.all([
                declaracion.cargar(),
                i18n.then((instancia) => cargarLosTextosDeSuDuenio(instancia, declaracion)),
              ]);

              return {
                Component: () => (
                  <Guarda exigencia={declaracion.exigencia}>
                    <Pagina />
                  </Guarda>
                ),
              };
            },
          }) satisfies RouteObject,
      ),
    },
  ];
}

/** El enrutador de la aplicación, el del navegador. */
export type Enrutador = ReturnType<typeof createBrowserRouter>;

/**
 * Crea el enrutador, que empieza a cargar la pantalla de la URL en cuanto existe.
 *
 * Lo llama `main.tsx` ANTES de tener el i18n, con la promesa de la instancia: así la pantalla y el
 * diccionario viajan a la vez, como cuando el enrutador se creaba al importar `App.tsx`. Desde el
 * 3.1 la carga de una ruta necesita la instancia para meter los textos de su funcionalidad
 * (ADR-0071), y por eso ya no se crea allí.
 */
export function crearEnrutador(i18n: Promise<InstanciaDeI18n>): Enrutador {
  return createBrowserRouter(crearRutas(i18n));
}

/** Los textos de la funcionalidad dueña de una ruta. Las del armazón los tienen desde el arranque. */
async function cargarLosTextosDeSuDuenio(
  i18n: InstanciaDeI18n,
  declaracion: DeclaracionDeRuta,
): Promise<void> {
  if (declaracion.duenio !== 'armazon') {
    await cargarTextosDe(i18n, declaracion.duenio);
  }
}
