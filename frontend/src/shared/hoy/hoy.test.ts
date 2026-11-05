import { describe, expect, it } from 'vitest';

import { diaEn, vidaDeHoy, type Hoy } from './hoy.ts';
import { traducirHoy } from '@/shared/api/hoy.ts';
import { HOY_EN_LAS_TRES, HOY_PARTIDO, HOY_SIMULADO, MANANA_SIMULADO } from '@/pruebas/datos.ts';

const PARTIDO: Hoy = traducirHoy(HOY_PARTIDO);
const UN_MINUTO = 60_000;

/**
 * Hoy, como lo dice el servidor (ADR-0079 §12): el día de cada zona, y cuándo volver a preguntar.
 *
 * Con el día partido, que es el único escenario en el que elegir mal la zona se ve: con las tres en
 * el mismo día, cualquier zona daría el día bueno.
 */
describe('El día de hoy en una zona', () => {
  it('es el de su zona, y sin zona, el de la empresa', () => {
    expect(diaEn(PARTIDO, 'Atlantic/Canary')).toBe(HOY_SIMULADO);
    expect(diaEn(PARTIDO, 'Europe/Madrid')).toBe(MANANA_SIMULADO);
    expect(diaEn(PARTIDO, 'Africa/Ceuta')).toBe(MANANA_SIMULADO);
    expect(diaEn(PARTIDO, null)).toBe(MANANA_SIMULADO);
  });

  it('en una zona que el servidor no ha dicho, es el de la empresa: quien decide es el servidor', () => {
    const enCanarias: Hoy = { ...PARTIDO, deLaEmpresa: HOY_SIMULADO };

    expect(diaEn(enCanarias, 'America/Bogota')).toBe(HOY_SIMULADO);
    expect(diaEn(PARTIDO, 'America/Bogota')).toBe(MANANA_SIMULADO);
  });
});

describe('Lo que vale lo que dijo el servidor', () => {
  it('dura hasta que acaba el primero de los días de sus zonas, y un segundo más', () => {
    // A las diez y media de la noche UTC: Canarias acaba a las once, y las otras dos al día siguiente.
    expect(vidaDeHoy(PARTIDO, new Date('2026-09-11T22:30:00Z'))).toBe(30 * 60 * 1000 + 1000);

    // Con las tres en el 11, el primero que acaba es el de la península, a las diez UTC.
    expect(vidaDeHoy(traducirHoy(HOY_EN_LAS_TRES), new Date('2026-09-11T21:00:00Z'))).toBe(
      60 * 60 * 1000 + 1000,
    );
  });

  it('un minuto, y no cero, si el día ya tenía que haber acabado: cero sería «nunca»', () => {
    expect(vidaDeHoy(PARTIDO, new Date('2026-09-11T23:00:00Z'))).toBe(UN_MINUTO);
    expect(vidaDeHoy(PARTIDO, new Date('2027-01-01T00:00:00Z'))).toBe(UN_MINUTO);
  });

  it('un minuto si el servidor no dice ninguna zona', () => {
    expect(vidaDeHoy({ ...PARTIDO, enCadaZona: new Map() }, new Date('2026-09-11T22:30:00Z'))).toBe(
      UN_MINUTO,
    );
  });
});
