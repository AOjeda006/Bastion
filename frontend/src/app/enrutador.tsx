import type { RouteObject } from 'react-router';

import { Disposicion } from './Disposicion.tsx';
import { Guarda } from './Guarda.tsx';
import { RUTAS } from './rutas.tsx';
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
 */
export function crearRutas(): RouteObject[] {
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
              const Pagina = await declaracion.cargar();

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
