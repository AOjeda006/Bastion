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

/** Cuántos GTIN al azar recorre cada caso de las propiedades del control. */
const GTIN_DE_PARTIDA = 1000;

const CIFRAS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'] as const;

/**
 * Un generador con semilla (mulberry32), sin biblioteca: la misma semilla da la misma secuencia en
 * cualquier máquina, y un rojo se repite.
 */
function generadorConSemilla(semilla: number): () => number {
  let estado = semilla >>> 0;

  return () => {
    estado = (estado + 0x6d2b79f5) >>> 0;
    let t = estado;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);

    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * El oráculo del control, que es otra cuenta: de izquierda a derecha sobre los catorce, incluido el
 * propio control, con peso 3 en las posiciones pares, y la suma tiene que acabar en cero.
 */
function cuadraSegunElOraculo(cifras: string): boolean {
  const catorce = cifras.padStart(14, '0');
  let suma = 0;

  for (let posicion = 0; posicion < catorce.length; posicion += 1) {
    suma += Number(catorce.charAt(posicion)) * (posicion % 2 === 0 ? 3 : 1);
  }

  return suma % 10 === 0;
}

/** El control que saldría con todos los pesos a 3: la cuenta que acierta por casualidad. */
function controlConTodosLosPesosA3(cifras: string): string {
  let suma = 0;

  for (const cifra of cifras.slice(0, -1)) {
    suma += Number(cifra) * 3;
  }

  return String((10 - (suma % 10)) % 10);
}

/**
 * Un GTIN al azar del largo pedido, con el control que pone el oráculo. Aquí no hay tabla de
 * prefijos, así que cualquier cifra vale delante.
 */
function unGtinAlAzar(azar: () => number, largo: number): string {
  const cuerpo = Array.from({ length: largo - 1 }, () => String(Math.floor(azar() * 10))).join('');
  const control = CIFRAS.find((cifra) => cuadraSegunElOraculo(cuerpo + cifra));

  if (control === undefined) {
    throw new Error('Siempre hay una cifra que cuadra la suma.');
  }

  return cuerpo + control;
}

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

  // Con todos los pesos a 3, la suma se pasa en el doble de lo que suman las cifras de peso 1, así
  // que el control sale bien por casualidad cuando esas cifras suman un múltiplo de 5. Les pasa al
  // 4006381333931 y al 10012345678902 de arriba, y por eso la mutación 139 solo se vio en 8 y en 12
  // cifras. Cada ejemplo comprueba primero que no es uno de esos.
  it.each([
    ['96385074', 'GTIN-8'],
    ['036000291452', 'GTIN-12'],
    ['8412345678905', 'GTIN-13'],
    ['18412345678902', 'GTIN-14'],
  ])('%s (%s) no cuadraría con todos los pesos a 3', (gtin) => {
    const conTodosA3 = controlConTodosLosPesosA3(gtin);

    expect(conTodosA3, 'si coincide, el ejemplo no distingue una cuenta de la otra').not.toBe(
      gtin.at(-1),
    );
    expect(motivoDelGtin(gtin)).toBeNull();
    expect(motivoDelGtin(gtin.slice(0, -1) + conTodosA3)).toBe('gtin-digito-de-control');
  });

  // Una cifra cambiada en d mueve la suma d o 3d, y ninguno es múltiplo de 10. Se prueban todas las
  // posiciones y todas las cifras, incluido el propio control. La semilla va en el nombre del caso,
  // y cada contraejemplo, entero en la lista de lo que no se rechazó.
  it.each([
    [8, 2108],
    [12, 2112],
    [13, 2113],
    [14, 2114],
  ])(
    'con %i cifras y la semilla %i, cambiar una sola cifra lo rechaza siempre',
    (largo, semilla) => {
      const azar = generadorConSemilla(semilla);
      const fallos: string[] = [];
      let cambiados = 0;

      for (let vuelta = 0; vuelta < GTIN_DE_PARTIDA; vuelta += 1) {
        const bueno = unGtinAlAzar(azar, largo);

        if (motivoDelGtin(bueno) !== null) {
          fallos.push(`«${bueno}» se rechaza, y es un GTIN`);
          continue;
        }

        for (let posicion = 0; posicion < largo; posicion += 1) {
          for (const cifra of CIFRAS) {
            if (cifra === bueno.charAt(posicion)) {
              continue;
            }

            const cambiado = bueno.slice(0, posicion) + cifra + bueno.slice(posicion + 1);
            cambiados += 1;

            if (motivoDelGtin(cambiado) !== 'gtin-digito-de-control') {
              fallos.push(
                `«${bueno}» con un ${cifra} en la posición ${String(posicion)}: «${cambiado}»`,
              );
            }
          }
        }
      }

      expect(fallos.slice(0, 5)).toEqual([]);
      expect(cambiados).toBe(GTIN_DE_PARTIDA * largo * 9);
    },
  );

  // Dos posiciones contiguas llevan pesos 3 y 1: el intercambio mueve la suma 2(a − b), y solo es
  // múltiplo de 10 si a − b es 0 o ±5. Con todos los pesos a 3 no la mueve nunca. La pareja: el que
  // difiere en 5 no lo ve la cuenta, y la regla tampoco lo rechaza.
  it.each([
    [8, 2108],
    [12, 2112],
    [13, 2113],
    [14, 2114],
  ])(
    'con %i cifras y la semilla %i, intercambiar dos contiguas lo rechaza salvo si difieren en 5',
    (largo, semilla) => {
      const azar = generadorConSemilla(semilla);
      const fallos: string[] = [];
      let vistos = 0;
      let queNoVe = 0;

      for (let vuelta = 0; vuelta < GTIN_DE_PARTIDA; vuelta += 1) {
        const bueno = unGtinAlAzar(azar, largo);

        if (motivoDelGtin(bueno) !== null) {
          fallos.push(`«${bueno}» se rechaza, y es un GTIN`);
          continue;
        }

        for (let posicion = 0; posicion < largo - 1; posicion += 1) {
          const a = bueno.charAt(posicion);
          const b = bueno.charAt(posicion + 1);
          const diferencia = Math.abs(Number(a) - Number(b));

          if (diferencia === 0) {
            continue;
          }

          const cambiado = bueno.slice(0, posicion) + b + a + bueno.slice(posicion + 2);
          const motivo = motivoDelGtin(cambiado);
          const contraejemplo = `«${bueno}» con ${String(posicion)} y ${String(posicion + 1)} cambiadas: «${cambiado}»`;

          if (diferencia === 5) {
            queNoVe += 1;

            if (motivo !== null) {
              fallos.push(`${contraejemplo} da ${motivo}, y la cuenta no puede verlo`);
            }

            continue;
          }

          vistos += 1;

          if (motivo !== 'gtin-digito-de-control') {
            fallos.push(`${contraejemplo} se admite`);
          }
        }
      }

      expect(fallos.slice(0, 5)).toEqual([]);
      expect(vistos).toBeGreaterThan(0);
      expect(queNoVe).toBeGreaterThan(0);
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
