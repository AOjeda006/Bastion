import { describe, expect, it } from 'vitest';

import { esRechazoDeCampo, rechazoEnLaLinea } from './rechazos.ts';
import { fallo } from '@/shared/api/errores.ts';

/** Un rechazo como lo escribe la API, con su `type` y lo que cada estado lleva aparte. */
function rechazo(estado: number, codigo: string, aparte: Record<string, unknown> = {}): unknown {
  return fallo(estado, { type: `/errors/${codigo}`, status: estado, ...aparte });
}

describe('Dónde va un rechazo del alta (ADR-0065 §3)', () => {
  it('un 400 dice la línea en la clave de `errors`, desde cero', () => {
    const error = rechazo(400, 'ajuste-numero-de-serie-no-valido', {
      errors: { 'lineas[2].numeroDeSerie': ['El número de serie no cabe.'] },
    });

    expect(rechazoEnLaLinea(error)).toEqual({
      indice: 2,
      campo: 'numeroDeSerie',
      tipo: 'ajuste-numero-de-serie-no-valido',
    });
  });

  it('un 422 la dice en `actual.linea`, desde uno, que es el número que enseña la ficha', () => {
    const error = rechazo(422, 'ajuste-sin-conversion', { actual: { linea: 2 } });

    expect(rechazoEnLaLinea(error)).toEqual({
      indice: 1,
      campo: 'unidadIntroducidaId',
      tipo: 'ajuste-sin-conversion',
    });
  });

  it('la trazabilidad que no casa va al artículo, y la serie que no es unitaria, a la cantidad', () => {
    expect(
      rechazoEnLaLinea(rechazo(422, 'ajuste-trazabilidad-no-casa', { actual: { linea: 1 } })),
    ).toMatchObject({ indice: 0, campo: 'articuloId' });
    expect(
      rechazoEnLaLinea(rechazo(422, 'ajuste-serie-no-unitaria', { actual: { linea: 3 } })),
    ).toMatchObject({ indice: 2, campo: 'cantidadIntroducida' });
  });

  it('un rechazo de línea que no dice cuál va arriba, y el de otra cosa también', () => {
    // Sin línea: el campo existe en todas, y elegir una sería inventárselo.
    expect(rechazoEnLaLinea(rechazo(422, 'ajuste-sin-conversion'))).toBeNull();
    expect(
      rechazoEnLaLinea(rechazo(422, 'ajuste-sin-conversion', { actual: { linea: 0 } })),
    ).toBeNull();
    expect(
      rechazoEnLaLinea(rechazo(422, 'ajuste-sin-conversion', { actual: { linea: '2' } })),
    ).toBeNull();
    // Una entrada de `errors` sin la forma de la API no se lee: su clave tampoco.
    expect(
      rechazoEnLaLinea(
        rechazo(400, 'ajuste-lote-no-valido', {
          errors: { 'lineas[1].codigoDeLote': 'sin lista' },
        }),
      ),
    ).toBeNull();
    // De la cabecera, aunque traiga una clave de `errors`: no es de ninguna línea.
    expect(
      rechazoEnLaLinea(
        rechazo(400, 'ajuste-motivo-no-valido', { errors: { motivo: ['Demasiado largo.'] } }),
      ),
    ).toBeNull();
    // El coste del servidor no dice campo (ADR-0065 §3): va arriba aunque nombre la línea.
    expect(
      rechazoEnLaLinea(rechazo(422, 'ajuste-coste-no-valido', { actual: { linea: 1 } })),
    ).toBeNull();
    expect(rechazoEnLaLinea(new Error('no es de la API'))).toBeNull();
  });

  it('un `type` heredado de un objeto no es de ningún campo', () => {
    expect(esRechazoDeCampo('constructor')).toBe(false);
    expect(rechazoEnLaLinea(rechazo(422, 'constructor', { actual: { linea: 1 } }))).toBeNull();
    expect(esRechazoDeCampo('ajuste-sin-conversion')).toBe(true);
    expect(esRechazoDeCampo('ajuste-motivo-no-valido')).toBe(true);
  });
});
