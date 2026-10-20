import { describe, expect, it } from 'vitest';

import { esRechazoDeCampo, rechazoEnLaLinea } from './rechazos.ts';
import { fallo } from '@/shared/api/errores.ts';

/** Un rechazo como lo escribe la API, con su `type` y lo que cada estado lleva aparte. */
function rechazo(estado: number, codigo: string, aparte: Record<string, unknown> = {}): unknown {
  return fallo(estado, { type: `/errors/${codigo}`, status: estado, ...aparte });
}

describe('Dónde va un rechazo del alta de la transferencia (ADR-0068 §6)', () => {
  it('un 400 dice la línea en la clave de `errors`, desde cero', () => {
    const error = rechazo(400, 'transferencia-numero-de-serie-no-valido', {
      errors: { 'lineas[2].numeroDeSerie': ['El número de serie no cabe.'] },
    });

    expect(rechazoEnLaLinea(error)).toEqual({
      indice: 2,
      campo: 'numeroDeSerie',
      tipo: 'transferencia-numero-de-serie-no-valido',
    });
  });

  it('un 409 o un 422 la dice en `actual.linea`, desde uno, que es el número de la ficha', () => {
    expect(
      rechazoEnLaLinea(rechazo(422, 'transferencia-sin-conversion', { actual: { linea: 2 } })),
    ).toEqual({ indice: 1, campo: 'unidadIntroducidaId', tipo: 'transferencia-sin-conversion' });
    expect(
      rechazoEnLaLinea(rechazo(409, 'transferencia-serie-repetida', { actual: { linea: 3 } })),
    ).toEqual({ indice: 2, campo: 'numeroDeSerie', tipo: 'transferencia-serie-repetida' });
  });

  it('la ubicación que no existe dice su punta en la ruta, y la bloqueada, en `actual.punta`', () => {
    expect(
      rechazoEnLaLinea(
        rechazo(400, 'transferencia-ubicacion-no-encontrada', {
          errors: { 'lineas[1].ubicacionDestinoId': ['No es de ese almacén.'] },
        }),
      ),
    ).toEqual({
      indice: 1,
      campo: 'ubicacionDestinoId',
      tipo: 'transferencia-ubicacion-no-encontrada',
    });
    expect(
      rechazoEnLaLinea(
        rechazo(409, 'transferencia-ubicacion-bloqueada', {
          actual: { linea: 1, punta: 'Origen' },
        }),
      ),
    ).toEqual({ indice: 0, campo: 'ubicacionOrigenId', tipo: 'transferencia-ubicacion-bloqueada' });
    expect(
      rechazoEnLaLinea(
        rechazo(409, 'transferencia-ubicacion-bloqueada', {
          actual: { linea: 2, punta: 'Destino' },
        }),
      ),
    ).toMatchObject({ indice: 1, campo: 'ubicacionDestinoId' });
  });

  it('la trazabilidad que no casa va al artículo, y la serie que no es unitaria, a la cantidad', () => {
    expect(
      rechazoEnLaLinea(
        rechazo(422, 'transferencia-trazabilidad-no-casa', { actual: { linea: 1 } }),
      ),
    ).toMatchObject({ indice: 0, campo: 'articuloId' });
    expect(
      rechazoEnLaLinea(rechazo(422, 'transferencia-serie-no-unitaria', { actual: { linea: 3 } })),
    ).toMatchObject({ indice: 2, campo: 'cantidadIntroducida' });
  });

  it('una ubicación sin su punta va arriba: elegir una de las dos celdas sería inventárselo', () => {
    expect(
      rechazoEnLaLinea(rechazo(409, 'transferencia-ubicacion-bloqueada', { actual: { linea: 1 } })),
    ).toBeNull();
    expect(
      rechazoEnLaLinea(
        rechazo(409, 'transferencia-ubicacion-bloqueada', {
          actual: { linea: 1, punta: 'origen' },
        }),
      ),
    ).toBeNull();
    // La ruta de otra celda de la línea no dice la punta, aunque diga la línea.
    expect(
      rechazoEnLaLinea(
        rechazo(400, 'transferencia-ubicacion-no-encontrada', {
          errors: { 'lineas[0].articuloId': ['No es una ubicación.'] },
        }),
      ),
    ).toBeNull();
  });

  it('un rechazo de línea que no dice cuál va arriba, y el de otra cosa también', () => {
    expect(rechazoEnLaLinea(rechazo(422, 'transferencia-sin-conversion'))).toBeNull();
    expect(
      rechazoEnLaLinea(rechazo(422, 'transferencia-sin-conversion', { actual: { linea: 0 } })),
    ).toBeNull();
    expect(
      rechazoEnLaLinea(rechazo(422, 'transferencia-sin-conversion', { actual: { linea: '2' } })),
    ).toBeNull();
    // Los dos almacenes, aunque el servidor los rechace juntos, no son de ninguna línea.
    expect(rechazoEnLaLinea(rechazo(400, 'transferencia-mismo-almacen'))).toBeNull();
    expect(
      rechazoEnLaLinea(rechazo(422, 'transferencia-almacen-bloqueado', { actual: { linea: 1 } })),
    ).toBeNull();
    expect(rechazoEnLaLinea(new Error('no es de la API'))).toBeNull();
  });

  it('un `type` heredado de un objeto no es de ningún campo', () => {
    expect(esRechazoDeCampo('constructor')).toBe(false);
    expect(rechazoEnLaLinea(rechazo(422, 'constructor', { actual: { linea: 1 } }))).toBeNull();
    expect(esRechazoDeCampo('transferencia-ubicacion-bloqueada')).toBe(true);
    expect(esRechazoDeCampo('transferencia-mismo-almacen')).toBe(true);
    expect(esRechazoDeCampo('transferencia-en-ejercicio-cerrado')).toBe(true);
    expect(esRechazoDeCampo('transferencia-almacen-bloqueado')).toBe(false);
  });
});
