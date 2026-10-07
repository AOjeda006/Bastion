import { describe, expect, it } from 'vitest';

import { estadoDelContrato, leerLineas, leerListado } from './listado.ts';

const PRIMERA = { pagina: 1, tamanio: 20 };
const NAVE = 'aaaaaaa1-0000-0000-0000-000000000001';

describe('El listado de recuentos, leído de la URL', () => {
  it('el estado va con el nombre del contrato, y el almacén con su identificador', () => {
    const listado = leerListado(new URLSearchParams(`estado=EnCurso&almacen=${NAVE}`), PRIMERA);

    expect(listado).toEqual({ ...PRIMERA, estado: 'enCurso', almacenId: NAVE });
  });

  it('lo que no se reconoce se ignora, en vez de mandarlo al servidor', () => {
    const listado = leerListado(new URLSearchParams('estado=enCurso&almacen=nave-1'), PRIMERA);

    // `enCurso` es el nombre de la pantalla, no el del contrato: tampoco vale.
    expect(listado).toEqual({ ...PRIMERA, estado: null, almacenId: null });
  });

  it('un estado que esta versión no conoce sale como desconocido, también los heredados', () => {
    expect(estadoDelContrato('Confirmado')).toBe('confirmado');
    expect(estadoDelContrato('Archivado')).toBe('desconocido');
    expect(estadoDelContrato('constructor')).toBe('desconocido');
  });
});

describe('Las líneas de un recuento, leídas de la URL', () => {
  it('las dos vistas parciales, y ninguna otra', () => {
    expect(leerLineas(new URLSearchParams('solo=sin-contar'), PRIMERA).solo).toBe('sin-contar');
    expect(leerLineas(new URLSearchParams('solo=teorico-cambiado'), PRIMERA).solo).toBe(
      'teorico-cambiado',
    );
    expect(leerLineas(new URLSearchParams('solo=con-transito'), PRIMERA).solo).toBeNull();
    expect(leerLineas(new URLSearchParams(''), PRIMERA).solo).toBeNull();
  });
});
