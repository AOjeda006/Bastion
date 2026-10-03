import { describe, expect, it } from 'vitest';

import { cadenaDelListado } from './listado.ts';

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

  it('se lleva los cuatro del listado, en su orden y tal como estaban, y nada más', () => {
    expect(
      cadenaDelListado(
        new URLSearchParams('categoria=x&ajeno=1&busqueda=tornillo+m6&tamanio=5&pagina=-4'),
      ),
    ).toBe('?categoria=x&busqueda=tornillo+m6&tamanio=5&pagina=-4');
  });
});
