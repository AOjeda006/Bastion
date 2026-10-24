import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  LINEA_EN_BLANCO,
  altaDe,
  cantidadEscrita,
  costeEscrito,
  esquemaDelAlta,
  rutaDeLaLinea,
  type DatosDelAlta,
  type LineaDelFormulario,
} from './formularios.ts';
import { HOY_SIMULADO } from '@/pruebas/datos.ts';

const UBICACION = 'abab0000-0000-0000-0000-000000000001';
const ARTICULO = 'fffffff1-0000-0000-0000-000000000001';
const UNIDAD = 'aaaa0001-0000-0000-0000-000000000001';

function linea(cambios: Partial<LineaDelFormulario> = {}): LineaDelFormulario {
  return {
    ...LINEA_EN_BLANCO,
    ubicacionId: UBICACION,
    articuloId: ARTICULO,
    cantidadIntroducida: '5',
    unidadIntroducidaId: UNIDAD,
    ...cambios,
  };
}

function datos(cambios: Partial<DatosDelAlta> = {}): DatosDelAlta {
  return {
    almacenId: 'aaaaaaa1-0000-0000-0000-000000000001',
    serieId: '5e5e5e5e-0000-0000-0000-000000000002',
    fechaDeOperacion: HOY_SIMULADO,
    motivo: 'Rotura en el muelle',
    lineas: [linea()],
    ...cambios,
  };
}

/**
 * El mensaje de cada campo, por su ruta: el primero, que es el que enseña el formulario
 * (`zodResolver`). Una cantidad vacía tampoco es una cifra, y dice antes que falta.
 */
function mensajes(escritos: DatosDelAlta): Record<string, string> {
  const resultado = esquemaDelAlta.safeParse(escritos);
  const primeros: Record<string, string> = {};

  for (const problema of resultado.error?.issues ?? []) {
    primeros[problema.path.join('.')] ??= problema.message;
  }

  return primeros;
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('La cantidad de una línea', () => {
  it('lleva su signo, acepta la coma decimal y viaja con punto', () => {
    expect(cantidadEscrita('12')).toBe('12');
    expect(cantidadEscrita(' -3,5 ')).toBe('-3.5');
    expect(cantidadEscrita('0.000001')).toBe('0.000001');
    expect(cantidadEscrita('-999999999999.999999')).toBe('-999999999999.999999');
  });

  it('no vale el cero, ni más cifras de las que caben, ni separadores de miles', () => {
    expect(cantidadEscrita('0')).toBeNull();
    expect(cantidadEscrita('-0,000')).toBeNull();
    expect(cantidadEscrita('1000000000000')).toBeNull();
    expect(cantidadEscrita('1.0000001')).toBeNull();
    expect(cantidadEscrita('1.234,5')).toBeNull();
    expect(cantidadEscrita('+3')).toBeNull();
    expect(cantidadEscrita('')).toBeNull();
  });
});

describe('El coste de una línea', () => {
  it('es una cifra sin signo con cuatro decimales como mucho: lo que se escribe se guarda', () => {
    expect(costeEscrito('2,5')).toBe('2.5');
    expect(costeEscrito('0')).toBe('0');
    expect(costeEscrito('12.3456')).toBe('12.3456');
    expect(costeEscrito('12.34567')).toBeNull();
    expect(costeEscrito('-1')).toBeNull();
    expect(costeEscrito('100000000000000')).toBeNull();
  });

  it('una línea que sale no lo lleva, y el error va en el coste', () => {
    expect(
      mensajes(datos({ lineas: [linea({ cantidadIntroducida: '-2', costeUnitario: '3' })] })),
    ).toEqual({ 'lineas.0.costeUnitario': 'ajuste-coste-no-valido' });
    expect(mensajes(datos({ lineas: [linea({ cantidadIntroducida: '-2' })] }))).toEqual({});
    expect(mensajes(datos({ lineas: [linea({ costeUnitario: '3' })] }))).toEqual({});
    // A la vez que lo que falte: `zod` mira la regla de la línea aunque falle otro de sus campos.
    expect(
      mensajes(
        datos({
          lineas: [linea({ articuloId: '', cantidadIntroducida: '-2', costeUnitario: '3' })],
        }),
      ),
    ).toEqual({
      'lineas.0.articuloId': 'eligeElArticulo',
      'lineas.0.costeUnitario': 'ajuste-coste-no-valido',
    });
  });
});

describe('El esquema del alta', () => {
  it('dice qué falta, campo a campo, con las claves de la pantalla', () => {
    expect(
      mensajes({
        almacenId: '',
        serieId: '',
        fechaDeOperacion: '',
        motivo: '  ',
        lineas: [LINEA_EN_BLANCO],
      }),
    ).toEqual({
      almacenId: 'eligeElAlmacen',
      serieId: 'eligeLaSerie',
      fechaDeOperacion: 'escribeLaFecha',
      motivo: 'ajuste-motivo-no-valido',
      'lineas.0.ubicacionId': 'eligeLaUbicacion',
      'lineas.0.articuloId': 'eligeElArticulo',
      'lineas.0.cantidadIntroducida': 'escribeLaCantidad',
      'lineas.0.unidadIntroducidaId': 'eligeLaUnidad',
    });
  });

  it('hoy vale y mañana no, con el día del servidor', () => {
    expect(mensajes(datos({ fechaDeOperacion: HOY_SIMULADO }))).toEqual({});
    expect(mensajes(datos({ fechaDeOperacion: '2026-09-12' }))).toEqual({
      fechaDeOperacion: 'ajuste-con-fecha-futura',
    });
  });

  it('una cantidad mal escrita y un coste mal escrito tienen sus frases', () => {
    expect(
      mensajes(datos({ lineas: [linea({ cantidadIntroducida: '0', costeUnitario: '1.23456' })] })),
    ).toEqual({
      'lineas.0.cantidadIntroducida': 'ajuste-cantidad-no-valida',
      'lineas.0.costeUnitario': 'costeMalEscrito',
    });
  });

  it('doscientas líneas caben y doscientas una no', () => {
    expect(mensajes(datos({ lineas: Array.from({ length: 200 }, () => linea()) }))).toEqual({});
    expect(mensajes(datos({ lineas: Array.from({ length: 201 }, () => linea()) }))).toEqual({
      lineas: 'topeDeLineas',
    });
  });
});

describe('Lo que viaja', () => {
  it('la cantidad y el coste con punto, lo vacío nulo, y sin factor: lo pone el servidor', () => {
    // Lo que pasó el esquema, que es lo que le llega al envío: lo que se valida es lo que se manda.
    const alta = altaDe(
      esquemaDelAlta.parse(
        datos({
          motivo: '  Rotura en el muelle  ',
          lineas: [
            linea({
              cantidadIntroducida: '1,5',
              costeUnitario: '2,25',
              codigoDeLote: ' L-07 ',
              numeroDeSerie: '  ',
            }),
            linea({ cantidadIntroducida: '-1', numeroDeSerie: 'SN-1' }),
          ],
        }),
      ),
    );

    expect(alta).toEqual({
      serieId: '5e5e5e5e-0000-0000-0000-000000000002',
      almacenId: 'aaaaaaa1-0000-0000-0000-000000000001',
      fechaDeOperacion: HOY_SIMULADO,
      motivo: 'Rotura en el muelle',
      lineas: [
        {
          ubicacionId: UBICACION,
          articuloId: ARTICULO,
          cantidadIntroducida: '1.5',
          unidadIntroducidaId: UNIDAD,
          costeUnitario: '2.25',
          codigoDeLote: 'L-07',
          numeroDeSerie: null,
        },
        {
          ubicacionId: UBICACION,
          articuloId: ARTICULO,
          cantidadIntroducida: '-1',
          unidadIntroducidaId: UNIDAD,
          costeUnitario: null,
          codigoDeLote: null,
          numeroDeSerie: 'SN-1',
        },
      ],
    });
  });

  it('el nombre de un campo de una línea es el de `react-hook-form`', () => {
    expect(rutaDeLaLinea(3, 'unidadIntroducidaId')).toBe('lineas.3.unidadIntroducidaId');
  });
});
