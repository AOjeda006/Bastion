import { screen, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { userEvent } from 'vitest/browser';

import { ALFA, PERMISOS_DE_LECTURA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/manejadores.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { medirLaVentana, usarEnElNavegador } from '@/pruebas/navegador.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

const ANCHO = 320;

/** Un código que no se parte y una descripción de catálogo, largos a propósito. */
const ARTICULO: components['schemas']['ArticuloDto'] = {
  id: 'fffffff1-0000-0000-0000-000000000009',
  empresaId: ALFA.id,
  codigo: 'TOR-DIN933-M8X40-A2',
  descripcion: 'Tornillo hexagonal DIN 933 M8x40 de acero inoxidable A2, caja de cien unidades',
  tipo: 'Bien',
  trazabilidad: 'PorLote',
  unidadBaseId: 'aaaa0001-0000-0000-0000-000000000001',
  impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
  categoriaId: null,
  retirada: false,
};

/**
 * LA VENTANA DE LA RETIRADA CABE EN 320 PX (ítem 3.6), como las de la zona
 * (`LasVentanasDeLaZonaCabenEn320`), con la misma medida y su pareja: sin su ancho —el de su
 * contenido, sin tope—, la misma medida sí la ve salirse. Lo que más la estira es lo que dice antes
 * del botón, y el código del artículo, que no se parte.
 */
describe(`La ventana de la retirada a ${String(ANCHO)} px`, () => {
  it('cabe, y lo de dentro no pasa de su borde', async () => {
    expect(window.innerWidth).toBe(ANCHO);

    abrirSesionSimulada(ALFA.id, [...PERMISOS_DE_LECTURA, PERMISOS.articuloRetirar]);
    usarEnElNavegador(
      http.get('/api/v1/catalogo/articulos', () =>
        HttpResponse.json({ elementos: [ARTICULO], pagina: 1, tamanio: 20, total: 1 }),
      ),
      http.get('/api/v1/catalogo/articulos/:id', () =>
        HttpResponse.json(ARTICULO, { headers: { ETag: '"1"' } }),
      ),
    );
    montarAplicacion('/articulos');

    await userEvent.click(
      await screen.findByRole('button', { name: `Retirar ${ARTICULO.codigo}` }, { timeout: 5000 }),
    );
    await screen.findByRole('button', { name: 'Retirar el artículo' });
    const ventana = screen.getByRole('dialog');
    expect(ventana.matches(':modal')).toBe(true);

    const ajustada = medirLaVentana(ventana);
    expect(ajustada.izquierda).toBeGreaterThanOrEqual(0);
    expect(
      ajustada.derecha,
      `la ventana se sale: ${ajustada.seSale.join('; ')}`,
    ).toBeLessThanOrEqual(ANCHO);
    expect(ajustada.seSale).toEqual([]);

    // La pareja de los textos: el artículo en una sola línea se sale de su `<dd>` sin mover la caja
    // de ningún elemento, y solo lo ve la medida de los textos.
    const articulo = within(ventana).getByText(ARTICULO.descripcion, { exact: false });
    articulo.style.whiteSpace = 'nowrap';
    const enUnaLinea = medirLaVentana(ventana);
    articulo.style.whiteSpace = '';

    expect(enUnaLinea.seSale.length, 'en una línea, el artículo cabe igual').toBeGreaterThan(0);
    expect(enUnaLinea.seSale.every((sale) => sale.startsWith('el texto de <dd>'))).toBe(true);

    ventana.style.maxWidth = 'none';
    ventana.style.width = 'max-content';
    const sinAjustar = medirLaVentana(ventana);
    ventana.style.maxWidth = '';
    ventana.style.width = '';

    expect(sinAjustar.derecha, 'sin su ancho, la ventana cabe igual').toBeGreaterThan(ANCHO);
  });
});
