import { describe, expect, it } from 'vitest';

import { esquemaDeAltaDeGtin, motivoDelGtin } from './esquemaDeAltaDeGtin.ts';
import { intentoPara } from './intentoDeAlta.ts';

/**
 * La regla del formulario de alta, sin pantalla: la cuenta del dígito de control en los cuatro
 * largos, y que un error no tape al otro.
 *
 * La pantalla prueba que cada rechazo llega a su campo; esto, que la cuenta es la del dominio
 * (`ElGtinTests` del backend usa los mismos números), que es lo que una pantalla con un solo ejemplo
 * no puede asegurar.
 */
describe('La regla del alta de un GTIN', () => {
  it.each([
    ['96385074', 'GTIN-8'],
    ['036000291452', 'GTIN-12'],
    ['4006381333931', 'GTIN-13'],
    ['10012345678902', 'GTIN-14'],
    ['001234567895', 'GTIN-12 con ceros delante'],
    ['00036000291452', 'GTIN-12 rellenado a catorce'],
  ])('%s (%s) puede ser un GTIN', (gtin) => {
    expect(motivoDelGtin(gtin)).toBeNull();
  });

  it.each(['96385074', '036000291452', '4006381333931', '10012345678902'])(
    'en %s, cualquier otra última cifra no cuadra',
    (gtin) => {
      const buena = Number(gtin.at(-1));
      const otras = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9].filter((cifra) => cifra !== buena);

      expect(otras.map((cifra) => motivoDelGtin(gtin.slice(0, -1) + String(cifra)))).toEqual(
        otras.map(() => 'gtin-digito-de-control'),
      );
    },
  );

  it.each([
    ['', 'gtin-largo-no-admitido'],
    ['   ', 'gtin-largo-no-admitido'],
    ['4006381', 'gtin-largo-no-admitido'],
    ['400638133393', 'gtin-digito-de-control'],
    ['40063813339', 'gtin-largo-no-admitido'],
    ['400638133393100', 'gtin-largo-no-admitido'],
    [' 4006381333931 ', null],
    ['4006381 333931', 'gtin-no-son-digitos'],
    ['40063813339３１', 'gtin-no-son-digitos'],
    ['400638133393A', 'gtin-no-son-digitos'],
  ])('«%s» → %s', (texto, motivo) => {
    expect(motivoDelGtin(texto)).toBe(motivo);
  });

  it('una caja sin unidades lo dice aunque el GTIN también esté mal', () => {
    const resultado = esquemaDeAltaDeGtin.safeParse({
      gtin: '4006381333932',
      nivel: 'caja',
      unidades: '',
    });

    expect(resultado.success).toBe(false);
    expect(
      resultado.error?.issues.map((problema) => [problema.path.join('.'), problema.message]),
    ).toEqual([
      ['gtin', 'gtin-digito-de-control'],
      ['unidades', 'codigo-barras-unidades-no-validas'],
    ]);
  });

  it.each([
    ['base', '', true],
    ['base', 'lo que sea', true],
    ['caja', '2', true],
    ['palet', ' 48 ', true],
    ['caja', '1', false],
    ['caja', '0', false],
    ['caja', '2.5', false],
    ['palet', '-4', false],
    // El tope es el `int` del contrato: por encima, el servidor ni lo enlaza.
    ['palet', '2147483647', true],
    ['palet', '2147483648', false],
  ])('nivel %s con unidades «%s»: %s', (nivel, unidades, vale) => {
    expect(esquemaDeAltaDeGtin.safeParse({ gtin: '4006381333931', nivel, unidades }).success).toBe(
      vale,
    );
  });

  it('el mismo cuerpo repite la clave; otro la estrena', () => {
    const primero = intentoPara({ gtin: '4006381333931', nivel: 'Base' }, null);

    expect(intentoPara({ gtin: '4006381333931', nivel: 'Base' }, primero)).toBe(primero);
    expect(intentoPara({ gtin: '96385074', nivel: 'Base' }, primero).clave).not.toBe(primero.clave);
  });
});
