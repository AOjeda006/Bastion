import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  LINEA_EN_BLANCO,
  altaDe,
  cantidadEscrita,
  esquemaDelAlta,
  rutaDeLaLinea,
  type DatosDelAlta,
  type LineaDelFormulario,
} from './formularios.ts';
import { HOY_SIMULADO } from '@/pruebas/datos.ts';

const CENTRAL = 'aaaaaaa1-0000-0000-0000-000000000001';
const NORTE = 'aaaaaaa2-0000-0000-0000-000000000002';
const SALE_DE = 'abab0000-0000-0000-0000-000000000001';
const ENTRA_EN = 'abab0000-0000-0000-0000-000000000002';
const ARTICULO = 'fffffff1-0000-0000-0000-000000000001';
const UNIDAD = 'aaaa0001-0000-0000-0000-000000000001';

function linea(cambios: Partial<LineaDelFormulario> = {}): LineaDelFormulario {
  return {
    ...LINEA_EN_BLANCO,
    ubicacionOrigenId: SALE_DE,
    ubicacionDestinoId: ENTRA_EN,
    articuloId: ARTICULO,
    cantidadIntroducida: '5',
    unidadIntroducidaId: UNIDAD,
    ...cambios,
  };
}

function datos(cambios: Partial<DatosDelAlta> = {}): DatosDelAlta {
  return {
    almacenOrigenId: CENTRAL,
    almacenDestinoId: NORTE,
    serieId: '5e5e5e5e-0000-0000-0000-000000000004',
    fechaDeEnvio: HOY_SIMULADO,
    lineas: [linea()],
    ...cambios,
  };
}

/** El mensaje de cada campo, por su ruta: el primero, que es el que enseña el formulario. */
function mensajes(escritos: DatosDelAlta): Record<string, string> {
  const resultado = esquemaDelAlta.safeParse(escritos);
  const primeros: Record<string, string> = {};

  for (const problema of resultado.error?.issues ?? []) {
    primeros[problema.path.join('.')] ??= problema.message;
  }

  return primeros;
}

/** Todos los de un campo, y no solo el primero: lo que el formulario no enseña también cuenta. */
function todosLosDe(escritos: DatosDelAlta, ruta: string): string[] {
  return (esquemaDelAlta.safeParse(escritos).error?.issues ?? [])
    .filter((problema) => problema.path.join('.') === ruta)
    .map((problema) => problema.message);
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('La cantidad de una línea de la transferencia', () => {
  it('no lleva signo, acepta la coma decimal y viaja con punto', () => {
    expect(cantidadEscrita('12')).toBe('12');
    expect(cantidadEscrita(' 3,5 ')).toBe('3.5');
    expect(cantidadEscrita('0.000001')).toBe('0.000001');
    expect(cantidadEscrita('999999999999.999999')).toBe('999999999999.999999');
  });

  it('no vale el cero, ni un signo, ni más cifras de las que caben, ni separadores de miles', () => {
    // La negativa es la del inverso, que se construye anulando.
    expect(cantidadEscrita('-3')).toBeNull();
    expect(cantidadEscrita('+3')).toBeNull();
    expect(cantidadEscrita('0')).toBeNull();
    expect(cantidadEscrita('0,000')).toBeNull();
    expect(cantidadEscrita('1000000000000')).toBeNull();
    expect(cantidadEscrita('1.0000001')).toBeNull();
    expect(cantidadEscrita('1.234,5')).toBeNull();
    expect(cantidadEscrita('')).toBeNull();
  });
});

describe('El esquema del alta de la transferencia', () => {
  it('dice qué falta, campo a campo, con las claves de la pantalla', () => {
    expect(
      mensajes({
        almacenOrigenId: '',
        almacenDestinoId: '',
        serieId: '',
        fechaDeEnvio: '',
        lineas: [LINEA_EN_BLANCO],
      }),
    ).toEqual({
      almacenOrigenId: 'eligeElOrigen',
      almacenDestinoId: 'eligeElDestino',
      serieId: 'eligeLaSerie',
      fechaDeEnvio: 'escribeElEnvio',
      'lineas.0.ubicacionOrigenId': 'eligeLaUbicacion',
      'lineas.0.ubicacionDestinoId': 'eligeLaUbicacion',
      'lineas.0.articuloId': 'eligeElArticulo',
      'lineas.0.cantidadIntroducida': 'escribeLaCantidad',
      'lineas.0.unidadIntroducidaId': 'eligeLaUnidad',
    });
  });

  it('el mismo almacén en las dos puntas va en el destino, aunque falte lo demás', () => {
    expect(mensajes(datos({ almacenDestinoId: CENTRAL }))).toEqual({
      almacenDestinoId: 'transferencia-mismo-almacen',
    });
    // A la vez que lo que falta: `zod` mira la regla del objeto aunque falle un campo. La salta tras
    // un error de tipo, que el formulario no produce, y pasado el tope de líneas, que va abajo.
    expect(mensajes(datos({ almacenDestinoId: CENTRAL, serieId: '', lineas: [] }))).toEqual({
      almacenDestinoId: 'transferencia-mismo-almacen',
      serieId: 'eligeLaSerie',
    });
    // Dos sin elegir no son el mismo almacén. El formulario enseña solo el primer aviso de cada
    // campo, así que lo que se mira son todos: un segundo, oculto, aparecería en cuanto cambiara.
    expect(mensajes(datos({ almacenOrigenId: '', almacenDestinoId: '' }))).toEqual({
      almacenOrigenId: 'eligeElOrigen',
      almacenDestinoId: 'eligeElDestino',
    });
    expect(
      todosLosDe(datos({ almacenOrigenId: '', almacenDestinoId: '' }), 'almacenDestinoId'),
    ).toEqual(['eligeElDestino']);
  });

  it('hoy vale y mañana no, con el día del servidor', () => {
    expect(mensajes(datos({ fechaDeEnvio: HOY_SIMULADO }))).toEqual({});
    expect(mensajes(datos({ fechaDeEnvio: '2026-09-12' }))).toEqual({
      fechaDeEnvio: 'transferencia-con-fecha-futura',
    });
  });

  it('una cantidad mal escrita tiene la frase del servidor', () => {
    expect(mensajes(datos({ lineas: [linea({ cantidadIntroducida: '-1' })] }))).toEqual({
      'lineas.0.cantidadIntroducida': 'transferencia-cantidad-no-valida',
    });
  });

  it('doscientas líneas caben y doscientas una no', () => {
    expect(mensajes(datos({ lineas: Array.from({ length: 200 }, () => linea()) }))).toEqual({});
    expect(mensajes(datos({ lineas: Array.from({ length: 201 }, () => linea()) }))).toEqual({
      lineas: 'topeDeLineas',
    });
    // Pasado el tope, `zod` no mira nada más: ni las líneas ni la regla del mismo almacén. Es lo
    // que cambió en la 4.6.6, por el GHSA-mrhx-c7xc-m3w5: con la 4.5.4 salían también los de las
    // líneas y el del mismo almacén.
    expect(
      mensajes(
        datos({
          almacenDestinoId: CENTRAL,
          lineas: Array.from({ length: 201 }, () => linea({ articuloId: '' })),
        }),
      ),
    ).toEqual({ lineas: 'topeDeLineas' });
  });
});

describe('Lo que viaja en el alta de la transferencia', () => {
  it('la cantidad con punto, lo vacío nulo, y sin factor: lo pone el servidor', () => {
    const alta = altaDe(
      esquemaDelAlta.parse(
        datos({
          lineas: [
            linea({ cantidadIntroducida: '1,5', codigoDeLote: ' L-07 ', numeroDeSerie: '  ' }),
            linea({ cantidadIntroducida: '1', numeroDeSerie: 'SN-1' }),
          ],
        }),
      ),
    );

    expect(alta).toEqual({
      serieId: '5e5e5e5e-0000-0000-0000-000000000004',
      almacenOrigenId: CENTRAL,
      almacenDestinoId: NORTE,
      fechaDeEnvio: HOY_SIMULADO,
      lineas: [
        {
          ubicacionOrigenId: SALE_DE,
          ubicacionDestinoId: ENTRA_EN,
          articuloId: ARTICULO,
          cantidadIntroducida: '1.5',
          unidadIntroducidaId: UNIDAD,
          codigoDeLote: 'L-07',
          numeroDeSerie: null,
        },
        {
          ubicacionOrigenId: SALE_DE,
          ubicacionDestinoId: ENTRA_EN,
          articuloId: ARTICULO,
          cantidadIntroducida: '1',
          unidadIntroducidaId: UNIDAD,
          codigoDeLote: null,
          numeroDeSerie: 'SN-1',
        },
      ],
    });
  });

  it('el nombre de un campo de una línea es el de `react-hook-form`', () => {
    expect(rutaDeLaLinea(3, 'ubicacionDestinoId')).toBe('lineas.3.ubicacionDestinoId');
  });
});
