import { describe, expect, it } from 'vitest';

import { cadenaDelListado, leerListado } from './listado.ts';

/**
 * La vuelta al listado, sin pantalla: qué parte de la URL viaja de ida y vuelta.
 *
 * Las pantallas comparan el `href` entero, pero el enrutador quita un `?` suelto antes de pintarlo,
 * así que «o nada» no lo pueden ver: lo ve esto. La mutación 178, que dejaba el `?` siempre, salió
 * verde en todas las pantallas.
 */
describe('La vuelta al listado', () => {
  it('sin nada del listado, no deja un «?» suelto', () => {
    expect(cadenaDelListado(new URLSearchParams())).toBe('');
    expect(cadenaDelListado(new URLSearchParams('ajeno=1&otro=2'))).toBe('');
  });

  it('se lleva los cinco del listado, en su orden y tal como estaban, y nada más', () => {
    expect(
      cadenaDelListado(
        new URLSearchParams(
          'categoria=x&ajeno=1&busqueda=tornillo+m6&retiradas=si&tamanio=5&pagina=-4',
        ),
      ),
    ).toBe('?categoria=x&busqueda=tornillo+m6&retiradas=si&tamanio=5&pagina=-4');
  });
});

/** Los retirados (ítem 3.6): el mismo parámetro que la API, y solo cuenta su `true`. */
describe('Los retirados en la URL', () => {
  const paginacion = { pagina: 1, tamanio: 20 };

  it.each([
    ['', false],
    ['retiradas=true', true],
    ['retiradas=false', false],
    ['retiradas=si', false],
    ['retiradas=', false],
    ['retiradas=TRUE', false],
  ])('«%s» los trae: %s', (cadena, conRetirados) => {
    expect(leerListado(new URLSearchParams(cadena), paginacion).conRetirados).toBe(conRetirados);
  });
});
