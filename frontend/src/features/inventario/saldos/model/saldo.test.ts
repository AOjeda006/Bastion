import { describe, expect, it } from 'vitest';

import { hoyEnElServidor, importeLegible, leerListado } from './saldo.ts';

const PRIMERA = { pagina: 1, tamanio: 20 };
const ALMACEN = 'a1a1a1a1-0000-0000-0000-000000000001';
const ARTICULO = 'fffffff1-0000-0000-0000-000000000001';

describe('El saldo, en el modelo', () => {
  it('lee de la URL el día, el almacén y el artículo con los nombres de la API', () => {
    const parametros = new URLSearchParams({
      fecha: '2026-08-31',
      almacen: ALMACEN,
      articulo: ARTICULO,
    });

    expect(leerListado(parametros, PRIMERA)).toEqual({
      ...PRIMERA,
      fecha: '2026-08-31',
      almacenId: ALMACEN,
      articuloId: ARTICULO,
    });
  });

  it('lo que no tiene forma de día ni de identificador se ignora, y un día futuro no', () => {
    expect(
      leerListado(
        new URLSearchParams({ fecha: '2026-02-31', almacen: 'constructor', articulo: '7' }),
        PRIMERA,
      ),
    ).toEqual({ ...PRIMERA, fecha: null, almacenId: null, articuloId: null });
    // Quien dice que el saldo del futuro no existe es el servidor, con su reloj.
    expect(leerListado(new URLSearchParams({ fecha: '2999-01-01' }), PRIMERA).fecha).toBe(
      '2999-01-01',
    );
  });

  it('hoy es el día UTC, que es el del servidor, y no el del calendario de quien mira', () => {
    // Las once y media de la noche en Bogotá ya son el día siguiente en Greenwich.
    expect(hoyEnElServidor(new Date('2026-09-11T23:30:00-05:00'))).toBe('2026-09-12');
    // Y la una de la madrugada en Madrid todavía es el día anterior.
    expect(hoyEnElServidor(new Date('2026-09-12T01:00:00+02:00'))).toBe('2026-09-11');
  });

  it('un importe lleva los decimales de su divisa y hasta cuatro, los del libro', () => {
    expect(importeLegible(12.5, 'EUR', 'es')).toBe('12,50 €');
    expect(importeLegible(0.1234, 'EUR', 'es')).toBe('0,1234 €');
    expect(importeLegible(0.12345, 'EUR', 'es')).toBe('0,1235 €');
    expect(importeLegible(12.5, 'EUR', 'en')).toBe('€12.50');
  });

  it('una divisa que Intl no admite se pinta con su código detrás, sin romper la pantalla', () => {
    expect(() => new Intl.NumberFormat('es', { style: 'currency', currency: 'EURO' })).toThrow(
      RangeError,
    );
    expect(importeLegible(3.5, 'EURO', 'es')).toBe('3,5 EURO');
  });
});
