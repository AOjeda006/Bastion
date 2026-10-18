import { describe, expect, it } from 'vitest';

import { enLaZona } from '@/pruebas/zona.ts';
import { hoyEnElServidor } from './dias.ts';

/**
 * El único «hoy» del frontal es el día UTC, que es el del servidor, se mire desde donde se mire.
 *
 * Las dos direcciones, porque cada una pone rojo un error distinto: al oeste de Greenwich, un «hoy»
 * local se queda en el día anterior; al este, se adelanta al siguiente.
 */
describe('El día de hoy', () => {
  it('es el día UTC, que es el del servidor, y no el del calendario de quien mira', async () => {
    // Las once y media de la noche en Bogotá ya son el día siguiente en Greenwich.
    await enLaZona('America/Bogota', () => {
      expect(hoyEnElServidor(new Date('2026-09-11T23:30:00-05:00'))).toBe('2026-09-12');
    });
    // Y la una de la madrugada en Madrid todavía es el día anterior.
    await enLaZona('Europe/Madrid', () => {
      expect(hoyEnElServidor(new Date('2026-09-12T01:00:00+02:00'))).toBe('2026-09-11');
    });
  });
});
