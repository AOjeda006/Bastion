import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDeAjustes } from './PaginaDeAjustes.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { capturarExportaciones, exportarYComprobar } from '@/pruebas/exportaciones.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type AjusteResumenDto = components['schemas']['AjusteResumenDto'];

const PUEDE_VER = [...PERMISOS_DE_LECTURA, PERMISOS.ajusteVer];
const PUEDE_ABRIR = [...PUEDE_VER, PERMISOS.ajusteAbrir];

/** La nave de Alfa, la única que tiene: ALM-ALFA. */
const ALMACEN = almacenesDe(ALFA.id).elementos[0]!;

function resumen(n: number, estado: string, fecha: string, motivo: string): AjusteResumenDto {
  return {
    id: `adadadad-0000-0000-0000-${String(n).padStart(12, '0')}`,
    // Sin número hasta que se confirma (ADR-0065 §5).
    numero: estado === 'Borrador' ? null : n,
    almacenId: ALMACEN.id,
    fechaDeOperacion: fecha,
    estado,
    motivo,
  };
}

/** Un borrador, uno confirmado y su inverso, del último día al primero, como los ordena la API. */
function losDeSiempre(): AjusteResumenDto[] {
  return [
    resumen(15, 'Borrador', '2026-11-05', 'Rotura en el muelle'),
    resumen(14, 'Confirmado', '2026-11-03', 'Anulación: mal contado'),
    resumen(13, 'Anulado', '2026-11-02', 'Entrada sin albarán'),
  ];
}

/**
 * Los ajustes del lado del servidor y lo que se ha pedido. Se anota entero, porque una pantalla que
 * filtrara en el navegador pintaría lo mismo con la lista completa.
 */
const servidorDeAjustes = {
  ajustes: [] as AjusteResumenDto[],
  pedidos: [] as Record<string, string | null>[],
};

beforeEach(() => {
  servidorDeAjustes.ajustes = losDeSiempre();
  servidorDeAjustes.pedidos = [];

  servidor.use(
    http.get('/api/v1/inventario/ajustes', ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      const pedido = Object.fromEntries(
        ['page', 'size', 'estado', 'almacen', 'q', 'sort'].map((nombre) => [
          nombre,
          consulta.get(nombre),
        ]),
      );
      servidorDeAjustes.pedidos.push(pedido);

      const todos = servidorDeAjustes.ajustes
        .filter((a) => pedido['estado'] === null || a.estado === pedido['estado'])
        .filter((a) => pedido['almacen'] === null || a.almacenId === pedido['almacen'])
        // Sin distinguir mayúsculas, como el filtro del motivo de la API.
        .filter(
          (a) =>
            pedido['q'] === null ||
            a.motivo.toLocaleLowerCase('es').includes((pedido['q'] ?? '').toLocaleLowerCase('es')),
        );

      return HttpResponse.json({
        elementos: todos,
        pagina: Number(pedido['page'] ?? 1),
        tamanio: Number(pedido['size'] ?? 20),
        total: todos.length,
      });
    }),

    http.get('/api/v1/organizacion/almacenes/:id', () => HttpResponse.json(ALMACEN)),
  );
});

function montar(
  ruta = '/ajustes',
  permisos: readonly string[] = PUEDE_ABRIR,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDeAjustes />, ruta, 'es', '/ajustes', [
    { path: '/ajustes/nuevo', element: <p>El alta del ajuste</p> },
  ]);
}

/** Las filas de la tabla, celda a celda y sin la cabecera. */
function filas(): string[][] {
  const tabla = screen.getByRole('table', { name: 'Ajustes de la empresa activa' });

  return within(tabla)
    .getAllByRole('row')
    .slice(1)
    .map((fila) =>
      within(fila)
        .getAllByRole('cell')
        .map((celda) => celda.textContent),
    );
}

describe('El listado de ajustes', () => {
  it('nombra cada uno por su número, y el borrador, que no lo tiene, por su almacén y su día', async () => {
    montar();

    await screen.findByRole('table', { name: 'Ajustes de la empresa activa' });
    await waitFor(() => {
      expect(filas()).toEqual([
        [
          'Borrador de ALM-ALFA del 5 nov 2026',
          'ALM-ALFA · Nave central de Alfa',
          '5 nov 2026',
          'Borrador',
          'Rotura en el muelle',
        ],
        [
          'Ajuste 14',
          'ALM-ALFA · Nave central de Alfa',
          '3 nov 2026',
          'Confirmado',
          'Anulación: mal contado',
        ],
        [
          'Ajuste 13',
          'ALM-ALFA · Nave central de Alfa',
          '2 nov 2026',
          'Anulado',
          'Entrada sin albarán',
        ],
      ]);
    });
    expect(screen.getByRole('link', { name: 'Ajuste 14' })).toHaveAttribute(
      'href',
      '/ajustes/adadadad-0000-0000-0000-000000000014',
    );
    // Sin filtro no viaja ninguno: ni la búsqueda vacía ni el orden de siempre.
    expect(servidorDeAjustes.pedidos).toEqual([
      { page: '1', size: '20', estado: null, almacen: null, q: null, sort: null },
    ]);
  });

  it('los filtros van a la URL y al servidor con los nombres de la API, desde la primera página', async () => {
    const { enrutador } = montar('/ajustes?pagina=2');
    const usuario = userEvent.setup();

    await screen.findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Estado' }), 'Borrador');
    await usuario.selectOptions(
      screen.getByRole('combobox', { name: 'Almacén' }),
      'ALM-ALFA · Nave central de Alfa',
    );
    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar en el motivo' }), ' rotura ');
    await usuario.selectOptions(
      screen.getByRole('combobox', { name: 'Orden' }),
      'Número, de mayor a menor',
    );
    await usuario.click(screen.getByRole('button', { name: 'Filtrar' }));

    await waitFor(() => {
      expect(servidorDeAjustes.pedidos.at(-1)).toEqual({
        page: '1',
        size: '20',
        estado: 'Borrador',
        almacen: ALMACEN.id,
        q: 'rotura',
        sort: '-numero',
      });
    });
    expect(Object.fromEntries(new URLSearchParams(enrutador.state.location.search))).toEqual({
      estado: 'Borrador',
      almacen: ALMACEN.id,
      busqueda: 'rotura',
      orden: '-numero',
    });
    await waitFor(() => {
      expect(filas().map((fila) => fila[0])).toEqual(['Borrador de ALM-ALFA del 5 nov 2026']);
    });
  });

  it('exportar se lleva los filtros y el orden del listado con los nombres de la API, sin la página', async () => {
    const exportaciones = capturarExportaciones();
    montar(`/ajustes?estado=Borrador&almacen=${ALMACEN.id}&busqueda=rotura&orden=-numero&pagina=1`);

    await waitFor(() => {
      expect(filas().map((fila) => fila[0])).toEqual(['Borrador de ALM-ALFA del 5 nov 2026']);
    });
    await exportarYComprobar(exportaciones, '/api/v1/inventario/ajustes/exportacion', {
      estado: 'Borrador',
      almacen: ALMACEN.id,
      q: 'rotura',
      sort: '-numero',
    });
  });

  it('acotado y sin nada, lo dice del filtro, no de la empresa', async () => {
    montar('/ajustes?busqueda=inexistente');

    expect(await screen.findByText('Ningún ajuste coincide con el filtro.')).toBeInTheDocument();
    expect(servidorDeAjustes.pedidos.at(-1)?.['q']).toBe('inexistente');
  });

  it('un estado que esta versión no conoce lleva la marca y su explicación', async () => {
    servidorDeAjustes.ajustes = [resumen(16, 'Archivado', '2026-11-06', 'De otra versión')];
    montar();

    const tabla = await screen.findByRole('table', { name: 'Ajustes de la empresa activa' });
    const celda = within(tabla).getByText('Sin reconocer');
    const explicacion = screen.getByText(
      'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
    );

    expect(celda.closest('td')).toHaveAttribute(
      'aria-describedby',
      explicacion.closest('[id]')?.id,
    );
  });

  it('el alta se ofrece a quien puede abrir, y a quien solo ve, no', async () => {
    const { unmount } = montar();

    expect(await screen.findByRole('link', { name: 'Nuevo ajuste' })).toHaveAttribute(
      'href',
      '/ajustes/nuevo',
    );
    unmount();

    montar('/ajustes', PUEDE_VER);
    await screen.findByRole('table', { name: 'Ajustes de la empresa activa' });
    expect(screen.queryByRole('link', { name: 'Nuevo ajuste' })).not.toBeInTheDocument();
  });
});
