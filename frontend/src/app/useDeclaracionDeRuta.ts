import { useMatches } from 'react-router';

import { RUTAS, type DeclaracionDeRuta } from './rutas.tsx';

function llevaDeclaracion(manejo: unknown): manejo is { declaracion: DeclaracionDeRuta } {
  return typeof manejo === 'object' && manejo !== null && 'declaracion' in manejo;
}

/**
 * La declaración de la ruta en la que se está.
 *
 * Sale del `handle` que el enrutador cuelga de cada ruta, no de comparar `pathname` con la tabla:
 * quien sabe qué ruta ha casado es el enrutador, y el día que una ruta lleve parámetros —
 * `/almacenes/:id`— compararla a mano dejaría de funcionar sin avisar.
 */
export function useDeclaracionDeRuta(): DeclaracionDeRuta {
  const casadas = useMatches();

  for (let i = casadas.length - 1; i >= 0; i -= 1) {
    const manejo = casadas[i]?.handle;

    if (llevaDeclaracion(manejo)) {
      return manejo.declaracion;
    }
  }

  // No puede pasar: la tabla incluye `*`, que casa con todo. Si pasara, es que alguien ha metido
  // una ruta en el enrutador sin declaración — justo lo que el barrido de rutas persigue.
  throw new Error(
    'Ruta sin declaración: toda ruta se declara en app/rutas.tsx (ver ElBarridoDeRutas).',
  );
}

/** Solo para el barrido: la tabla, expuesta con un nombre que dice para qué se lee. */
export const TABLA_DE_RUTAS = RUTAS;
