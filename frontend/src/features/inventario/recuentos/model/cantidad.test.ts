import { describe, expect, it } from 'vitest';

import {
  cambioDelTeorico,
  cantidadLegible,
  contadoEscrito,
  diferenciaLegible,
} from './cantidad.ts';
import type { LineaDeRecuento } from './recuento.ts';

describe('Lo contado, escrito', () => {
  it.each([
    ['0', '0'],
    ['12', '12'],
    ['12,5', '12.5'],
    ['12.5', '12.5'],
    [' 3 ', '3'],
    ['0,000001', '0.000001'],
    ['999999999999,999999', '999999999999.999999'],
  ])('«%s» viaja como «%s»', (escrito, viaja) => {
    expect(contadoEscrito(escrito, false)).toBe(viaja);
  });

  it.each([
    ['', 'vacío'],
    ['-1', 'negativo'],
    ['1,0000001', 'con siete decimales'],
    ['1000000000000', 'con trece cifras enteras: el tope es exclusivo'],
    ['1.234,5', 'con separador de miles'],
    ['1e3', 'en notación científica'],
    ['12,', 'con la coma sin decimales'],
    ['doce', 'en letra'],
  ])('«%s» no vale: %s', (escrito) => {
    expect(contadoEscrito(escrito, false)).toBeNull();
  });

  it('un número de serie se cuenta 0 o 1, y nada más', () => {
    expect(contadoEscrito('0', true)).toBe('0');
    expect(contadoEscrito('1', true)).toBe('1');
    expect(contadoEscrito('1,0', true)).toBe('1.0');
    expect(contadoEscrito('2', true)).toBeNull();
    expect(contadoEscrito('0,5', true)).toBeNull();
    // La misma cifra en un artículo sin serie sí vale: lo que decide es la línea.
    expect(contadoEscrito('2', false)).toBe('2');
  });
});

describe('El cambio del teórico', () => {
  const contada: LineaDeRecuento = {
    id: 'l',
    numero: 1,
    ubicacionId: 'u',
    articuloId: 'a',
    codigoDeLote: null,
    numeroDeSerie: null,
    unidadBaseId: 'm',
    contado: 10,
    teoricoAlContar: 10,
    teorico: 8,
    enTransito: 0,
    diferencia: 2,
    teoricoCambiado: true,
  };

  it('es el de ahora menos el de cuando se contó', () => {
    expect(cambioDelTeorico(contada)).toBe(-2);
  });

  it('no hay cambio si el servidor no lo marca, aunque las cifras difieran', () => {
    // La marca es del servidor, que compara en decimal; aquí no se recalcula con coma flotante.
    expect(cambioDelTeorico({ ...contada, teoricoCambiado: false })).toBeNull();
  });

  it('no hay cambio en una línea sin contar, ni en una descartada', () => {
    expect(cambioDelTeorico({ ...contada, teoricoAlContar: null })).toBeNull();
    expect(cambioDelTeorico({ ...contada, teorico: null })).toBeNull();
  });
});

describe('Las cantidades, para leerlas', () => {
  it('con sus decimales y sin ceros de relleno, en cada idioma', () => {
    expect(cantidadLegible(12.5, 'es')).toBe('12,5');
    expect(cantidadLegible(12.5, 'en')).toBe('12.5');
    expect(cantidadLegible(0.000001, 'es')).toBe('0,000001');
    expect(cantidadLegible(3, 'es')).toBe('3');
  });

  it('una diferencia lleva su signo, y el cero no', () => {
    expect(diferenciaLegible(2.5, 'es')).toBe('+2,5');
    expect(diferenciaLegible(-2, 'es')).toBe('-2');
    expect(diferenciaLegible(0, 'es')).toBe('0');
  });
});
