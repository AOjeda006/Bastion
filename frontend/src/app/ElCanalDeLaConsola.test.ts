import { it } from 'vitest';

/**
 * EL CENTINELA DEL CANAL DE LA CONSOLA (ADR-0060).
 *
 * Los avisos de `act()` son un canal desde el 1.8, y su fondo está a cero: un solo aviso es un
 * hallazgo. Llegan al registro porque React los escribe con `console.error` y el informe `default`
 * de vitest imprime la consola de cada caso, también la de los que salen en verde. Ese cero solo
 * mide mientras el canal esté abierto, y vitest 4 lo cierra por su cuenta cuando detecta un agente:
 * lo mantiene abierto el informe fijado en `vite.config.ts` (ADR-0058).
 *
 * Este caso escribe una marca por el mismo camino que el aviso: un `console.error` desde un caso en
 * verde. El paso «Canal de la consola» de la CI la busca en el registro de los tests. Si no está, el
 * canal está mudo y el cero no vale nada, así que el paso falla; y falla también con un solo aviso
 * de `act()`. La CI lanza los tests con `AI_AGENT` puesto: así, lo único que mantiene la marca en el
 * registro es el informe fijado, y quitarlo pone el paso en rojo también allí.
 *
 * La marca va escrita aquí y en `scripts/ci/canal-de-la-consola.sh`. Si las dos dejan de coincidir,
 * el paso no la encuentra y falla, que es el lado bueno del error.
 */
it('escribe en la consola la marca que el paso de la CI busca en el registro', () => {
  console.error('CENTINELA DEL CANAL DE LA CONSOLA');
});
