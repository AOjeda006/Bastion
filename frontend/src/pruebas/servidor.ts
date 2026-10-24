import { setupServer } from 'msw/node';

import { manejadores } from './manejadores.ts';

export {
  abrirSesionSimulada,
  abrirSesionYaRecuperada,
  reiniciarServidor,
  servidorSimulado,
} from './manejadores.ts';

/**
 * El servidor simulado del carril de jsdom, con los manejadores de siempre.
 *
 * Los manejadores y el estado viven en `manejadores.ts`, que no importa nada de Node, porque
 * los usa también el carril del navegador (ítem 2.20), donde `msw/node` no carga. Aquí se
 * quedan el servidor y lo que los tests importaban de este fichero, para que ninguno cambie.
 */
export const servidor = setupServer(...manejadores);
