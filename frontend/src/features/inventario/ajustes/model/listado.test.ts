import { describe, expect, it } from 'vitest';

import { estaAcotado, estadoDelContrato, estadoEnElContrato, leerListado } from './listado.ts';

const PRIMERA = { pagina: 1, tamanio: 20 };
const NAVE = 'aaaaaaa1-0000-0000-0000-000000000001';

const SIN_FILTRO = { estado: null, almacenId: null, busqueda: '', orden: null };

describe('El listado de ajustes, leído de la URL', () => {
  it('el estado va con el nombre del contrato; el almacén, la búsqueda y el orden, como están', () => {
    const listado = leerListado(
      new URLSearchParams(`estado=Borrador&almacen=${NAVE}&busqueda=rotura&orden=-numero`),
      PRIMERA,
    );

    expect(listado).toEqual({
      ...PRIMERA,
      estado: 'borrador',
      almacenId: NAVE,
      busqueda: 'rotura',
      orden: '-numero',
    });
    expect(estaAcotado(listado)).toBe(true);
  });

  it('lo que no se reconoce se ignora, en vez de mandarlo al servidor', () => {
    const listado = leerListado(
      new URLSearchParams('estado=borrador&almacen=nave-1&orden=motivo'),
      PRIMERA,
    );

    // `borrador` es el nombre de la pantalla, no el del contrato: tampoco vale.
    expect(listado).toEqual({ ...PRIMERA, ...SIN_FILTRO });
    expect(estaAcotado(listado)).toBe(false);
  });

  it('la búsqueda se recorta y se corta en los cien que admite el servidor', () => {
    const larga = 'a'.repeat(120);

    expect(leerListado(new URLSearchParams(`busqueda=  ${larga}  `), PRIMERA).busqueda).toBe(
      'a'.repeat(100),
    );
    expect(leerListado(new URLSearchParams('busqueda=%20%20'), PRIMERA).busqueda).toBe('');
  });

  it('el orden no acota: un listado solo ordenado no está filtrado', () => {
    const listado = leerListado(new URLSearchParams('orden=fechaDeOperacion'), PRIMERA);

    expect(listado.orden).toBe('fechaDeOperacion');
    expect(estaAcotado(listado)).toBe(false);
  });

  it('un estado que esta versión no conoce sale como desconocido, también los heredados', () => {
    expect(estadoDelContrato('Anulado')).toBe('anulado');
    expect(estadoDelContrato('Descartado')).toBe('desconocido');
    expect(estadoDelContrato('constructor')).toBe('desconocido');
    expect(estadoEnElContrato('confirmado')).toBe('Confirmado');
  });
});
