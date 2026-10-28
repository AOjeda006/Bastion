import { screen } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA } from '@/pruebas/datos.ts';
import { capturarExportaciones, exportarYComprobar } from '@/pruebas/exportaciones.ts';
import { abrirSesionSimulada, servidor } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

/**
 * El listado de empresas, por lo que el ítem 3.2 le añade: la exportación.
 *
 * Lo demás de la pantalla —la tabla, el testigo que la protege, la disposición a 320 px— lo
 * prueban el barrido de rutas, el del testigo y el del navegador. Aquí va lo que es solo suyo.
 */
describe('El listado de empresas', () => {
  it('exportar se lleva las empresas del listado, con el nombre del servidor', async () => {
    abrirSesionSimulada();
    const exportaciones = capturarExportaciones();

    montarAplicacion('/empresas');

    expect(await screen.findByText(ALFA.razonSocial)).toBeVisible();
    await exportarYComprobar(exportaciones, '/api/v1/organizacion/empresas/exportacion');
  });

  it('sin empresas que ver, no ofrece exportar', async () => {
    abrirSesionSimulada();
    servidor.use(
      http.get('/api/v1/organizacion/empresas', () =>
        HttpResponse.json({ elementos: [], pagina: 1, tamanio: 20, total: 0 }),
      ),
    );

    montarAplicacion('/empresas');

    expect(await screen.findByText('No hay ninguna empresa que puedas ver.')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Exportar a CSV' })).not.toBeInTheDocument();
  });
});
