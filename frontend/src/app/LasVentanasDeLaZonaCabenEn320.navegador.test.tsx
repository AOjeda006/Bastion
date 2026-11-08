import { screen } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { userEvent } from 'vitest/browser';

import { ALFA, EMPRESA_ALFA, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/manejadores.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { medirLaVentana, usarEnElNavegador } from '@/pruebas/navegador.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

const ANCHO = 320;

const [CENTRAL] = almacenesDe(ALFA.id).elementos;

/**
 * LAS DOS VENTANAS DE LA ZONA CABEN EN 320 PX (ítem 3.5), como las páginas
 * (`LasPaginasCabenEn320`). Una ventana modal está en la capa de arriba, fuera de la página, y no la
 * ensancha: lo que hay que medir es ella, que no pase del borde, y que nada de dentro pase del suyo.
 *
 * **Con su pareja**: sin su ancho —el de su contenido, sin tope—, la misma medida sí la ve salirse.
 * Sin ella, una ventana que cabe porque sus textos son cortos pasaría igual el día que perdiera
 * las clases que la ajustan.
 */
describe(`Las ventanas de la zona a ${String(ANCHO)} px`, () => {
  it.each([
    {
      camino: '/almacenes',
      boton: `Cambiar la zona de ${CENTRAL!.codigo}`,
      manejador: http.get('/api/v1/organizacion/almacenes/:id', () =>
        HttpResponse.json(CENTRAL, { headers: { ETag: '"1"' } }),
      ),
    },
    {
      camino: '/empresas',
      boton: `Cambiar la zona de ${EMPRESA_ALFA.razonSocial}`,
      manejador: http.get('/api/v1/organizacion/empresas/:id', () =>
        HttpResponse.json(EMPRESA_ALFA, { headers: { ETag: '"1"' } }),
      ),
    },
  ])('la de $camino cabe, y lo de dentro no pasa de su borde', async (caso) => {
    expect(window.innerWidth).toBe(ANCHO);

    abrirSesionSimulada(ALFA.id, [
      ...PERMISOS_DE_LECTURA,
      PERMISOS.almacenModificar,
      PERMISOS.empresaModificar,
    ]);
    usarEnElNavegador(caso.manejador);
    montarAplicacion(caso.camino);

    await userEvent.click(
      await screen.findByRole('button', { name: caso.boton }, { timeout: 5000 }),
    );
    await screen.findByRole('radio', { name: 'Canarias' });
    const ventana = screen.getByRole('dialog');
    expect(ventana.matches(':modal')).toBe(true);

    const ajustada = medirLaVentana(ventana);
    expect(ajustada.izquierda).toBeGreaterThanOrEqual(0);
    expect(
      ajustada.derecha,
      `la ventana se sale: ${ajustada.seSale.join('; ')}`,
    ).toBeLessThanOrEqual(ANCHO);
    expect(ajustada.seSale).toEqual([]);

    ventana.style.maxWidth = 'none';
    ventana.style.width = 'max-content';
    const sinAjustar = medirLaVentana(ventana);
    ventana.style.maxWidth = '';
    ventana.style.width = '';

    expect(sinAjustar.derecha, 'sin su ancho, la ventana cabe igual').toBeGreaterThan(ANCHO);
  });
});
