import { describe, expect, it } from 'vitest';

import { estaAcotado, estadoDelContrato, estadoEnElContrato, leerListado } from './listado.ts';

const PRIMERA = { pagina: 1, tamanio: 20 };
const NAVE = 'aaaaaaa1-0000-0000-0000-000000000001';
const NORTE = 'aaaaaaa2-0000-0000-0000-000000000002';

const SIN_FILTRO = { estado: null, origenId: null, destinoId: null, orden: null };

describe('El listado de transferencias, leído de la URL', () => {
  it('el estado va con el nombre del contrato; los dos almacenes y el orden, como están', () => {
    const listado = leerListado(
      new URLSearchParams(`estado=Enviada&origen=${NAVE}&destino=${NORTE}&orden=-fechaDeRecepcion`),
      PRIMERA,
    );

    expect(listado).toEqual({
      ...PRIMERA,
      estado: 'enviada',
      origenId: NAVE,
      destinoId: NORTE,
      orden: '-fechaDeRecepcion',
    });
    expect(estaAcotado(listado)).toBe(true);
  });

  it('cada punta acota sola', () => {
    expect(estaAcotado(leerListado(new URLSearchParams(`origen=${NAVE}`), PRIMERA))).toBe(true);
    expect(estaAcotado(leerListado(new URLSearchParams(`destino=${NORTE}`), PRIMERA))).toBe(true);
  });

  it('lo que no se reconoce se ignora, en vez de mandarlo al servidor', () => {
    const listado = leerListado(
      new URLSearchParams('estado=enviada&origen=nave-1&destino=&orden=motivo&busqueda=rotura'),
      PRIMERA,
    );

    // `enviada` es el nombre de la pantalla, no el del contrato: tampoco vale. Y la búsqueda no
    // es un criterio de este listado.
    expect(listado).toEqual({ ...PRIMERA, ...SIN_FILTRO });
    expect(estaAcotado(listado)).toBe(false);
  });

  it('el orden no acota: un listado solo ordenado no está filtrado', () => {
    const listado = leerListado(new URLSearchParams('orden=fechaDeEnvio'), PRIMERA);

    expect(listado.orden).toBe('fechaDeEnvio');
    expect(estaAcotado(listado)).toBe(false);
  });

  it('un estado que esta versión no conoce sale como desconocido, también los heredados', () => {
    expect(estadoDelContrato('Recibida')).toBe('recibida');
    expect(estadoDelContrato('Descartada')).toBe('desconocido');
    expect(estadoDelContrato('constructor')).toBe('desconocido');
    expect(estadoEnElContrato('anulada')).toBe('Anulada');
  });
});
