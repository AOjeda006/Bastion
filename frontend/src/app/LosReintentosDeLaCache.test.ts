import type { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';

import { fallo } from '@/shared/api/errores.ts';

import { crearCache } from './cache.ts';

/**
 * CUÁNTAS VECES PIDE LA CACHÉ DE LA APLICACIÓN LO QUE HA FALLADO (respuesta 7 del repaso de la
 * fase 2).
 *
 * Se mide sobre `crearCache()`, la de la aplicación, y no sobre la de los tests, que monta la suya
 * sin reintentos (`src/pruebas/montar.tsx`): por eso ninguna pantalla podía ver el `400` pedido dos
 * veces. La consulta solo cambia el retraso del reintento, para no esperar un segundo; cuántas veces
 * se reintenta lo decide la caché.
 */
async function vecesQuePide(cache: QueryClient, error: Error): Promise<number> {
  let veces = 0;

  await expect(
    cache.query({
      queryKey: ['reintentos'],
      queryFn: () => {
        veces += 1;
        return Promise.reject(error);
      },
      retryDelay: 0,
    }),
  ).rejects.toBe(error);

  return veces;
}

describe('Los reintentos de la caché', () => {
  it.each([400, 401, 403, 404, 409, 412, 422])('un %i se pide una sola vez', async (estado) => {
    expect(await vecesQuePide(crearCache(), fallo(estado))).toBe(1);
  });

  it.each([500, 502, 503])(
    'un %i se pide dos veces: la segunda es el reintento',
    async (estado) => {
      expect(await vecesQuePide(crearCache(), fallo(estado))).toBe(2);
    },
  );

  it('un corte de red, que no es un fallo de la API, también se reintenta una vez', async () => {
    expect(await vecesQuePide(crearCache(), new TypeError('Failed to fetch'))).toBe(2);
  });
});
