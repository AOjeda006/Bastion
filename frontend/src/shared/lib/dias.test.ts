import { describe, expect, it } from 'vitest';

import { enLaZona } from '@/pruebas/zona.ts';
import { milisegundosHasta } from './dias.ts';

/**
 * Lo único que el frontal le pregunta al reloj: cuánto falta para que acabe un día que dijo el
 * servidor (ADR-0079 §12).
 *
 * Los instantes llevan su desfase, así que la cuenta es la misma se mire desde donde se mire. Se
 * ejerce desde las dos direcciones, porque un instante leído como hora local se equivocaría al
 * oeste y al este de Greenwich por razones distintas.
 */
describe('Lo que falta para un instante', () => {
  it('es la diferencia entre los dos instantes, sea cual sea la zona de quien mira', async () => {
    const madrid = '2026-09-11T22:00:00+00:00';
    const canarias = '2026-09-11T23:00:00+00:00';
    // Las once y media de la noche del 11 en Canarias, que en Madrid ya son las doce y media del 12.
    const momento = new Date('2026-09-11T23:30:00+01:00');

    for (const zona of ['America/Bogota', 'Europe/Madrid', 'Asia/Tokyo']) {
      await enLaZona(zona, () => {
        expect(milisegundosHasta(canarias, momento), zona).toBe(30 * 60 * 1000);
        expect(milisegundosHasta(madrid, momento), zona).toBe(0);
      });
    }
  });

  it('es cero si el instante ya pasó, y no un número negativo', () => {
    expect(milisegundosHasta('2026-09-11T22:00:00+00:00', new Date('2026-09-12T22:00:00Z'))).toBe(
      0,
    );
    expect(milisegundosHasta('2026-09-11T22:00:00+00:00', new Date('2026-09-11T22:00:00Z'))).toBe(
      0,
    );
    expect(
      milisegundosHasta('2026-09-11T22:00:00+00:00', new Date('2026-09-11T21:59:59.999Z')),
    ).toBe(1);
  });
});
