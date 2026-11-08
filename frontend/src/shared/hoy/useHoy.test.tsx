import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useHoy } from './useHoy.ts';
import { HOY_EN_LAS_TRES, HOY_PARTIDO, HOY_SIMULADO, MANANA_SIMULADO } from '@/pruebas/datos.ts';
import { servidor } from '@/pruebas/servidor.ts';

/** Lo único que pinta: el día de la empresa, como lo lee un formulario antes de elegir almacén. */
function ElDiaDeLaEmpresa(): React.JSX.Element {
  const { data } = useHoy();

  return <p>{data === undefined ? 'Sin día' : data.deLaEmpresa}</p>;
}

afterEach(() => {
  vi.useRealTimers();
});

/**
 * «Hoy», preguntado otra vez en cuanto acaba el día (ADR-0079 §12).
 *
 * `vidaDeHoy` dice cuánto vale lo que dijo el servidor, y la prueba `hoy.test.ts`. Esto prueba que
 * esa vida se usa para VOLVER A PREGUNTAR: que un dato caducado no se queda en la pantalla hasta que
 * algo la recargue. Lo pide la 1178 de la F2 del 3.5, que quitó el `refetchInterval` y salió verde
 * en los dos carriles: la caducidad sola no pregunta, solo marca el dato como viejo.
 *
 * El reloj se para a dos décimas del fin del día en la península, que es el primero de los tres en
 * acabar, así que la vida es de 1,2 s: el segundo que se espera de más, y las dos décimas. Solo se
 * finge la fecha: los plazos corren de verdad, como en la pantalla.
 */
describe('Hoy, al acabar el día', () => {
  it('se vuelve a preguntar con la pantalla quieta, y el día cambia sin recargar', async () => {
    const respuestas = [HOY_EN_LAS_TRES, HOY_PARTIDO];
    let preguntas = 0;
    servidor.use(
      http.get('/api/v1/organizacion/hoy', () => {
        const respuesta = respuestas[Math.min(preguntas, respuestas.length - 1)];
        preguntas += 1;

        return HttpResponse.json(respuesta);
      }),
    );
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-11T21:59:59.800Z'));

    render(
      <QueryClientProvider
        client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
      >
        <ElDiaDeLaEmpresa />
      </QueryClientProvider>,
    );

    expect(await screen.findByText(HOY_SIMULADO)).toBeVisible();

    // Nadie toca nada: lo que pregunta otra vez es el plazo.
    expect(await screen.findByText(MANANA_SIMULADO, {}, { timeout: 4000 })).toBeVisible();
    expect(preguntas).toBe(2);
  });
});
