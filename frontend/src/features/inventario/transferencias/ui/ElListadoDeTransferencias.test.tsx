import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDeTransferencias } from './PaginaDeTransferencias.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, NAVE_DEL_NORTE, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type TransferenciaResumenDto = components['schemas']['TransferenciaResumenDto'];

const PUEDE_VER = [...PERMISOS_DE_LECTURA, PERMISOS.transferenciaVer];

/** Las dos naves de Alfa: la central, que es la de siempre, y la del norte. */
const CENTRAL = almacenesDe(ALFA.id).elementos[0]!;
const NAVES = [CENTRAL, NAVE_DEL_NORTE];

function resumen(
  n: number,
  estado: string,
  envio: string,
  recepcion: string | null,
  sentido: 'alNorte' | 'alCentro' = 'alNorte',
): TransferenciaResumenDto {
  const [origen, destino] = sentido === 'alNorte' ? NAVES : [...NAVES].reverse();

  return {
    id: `7a7a7a7a-0000-0000-0000-${String(n).padStart(12, '0')}`,
    // Sin número hasta que se envía (ADR-0068 §5).
    numero: estado === 'Borrador' ? null : n,
    almacenOrigenId: origen!.id,
    almacenDestinoId: destino!.id,
    fechaDeEnvio: envio,
    fechaDeRecepcion: recepcion,
    estado,
    anulaAId: null,
  };
}

/** Un borrador, una en camino, una recibida y una anulada, del último envío al primero. */
function lasDeSiempre(): TransferenciaResumenDto[] {
  return [
    resumen(16, 'Borrador', '2026-11-05', null),
    resumen(15, 'Enviada', '2026-11-04', null),
    resumen(14, 'Recibida', '2026-11-03', '2026-11-04', 'alCentro'),
    resumen(13, 'Anulada', '2026-11-02', '2026-11-02'),
  ];
}

/**
 * Las transferencias del lado del servidor y lo que se ha pedido. Se anota entero, porque una
 * pantalla que filtrara en el navegador pintaría lo mismo con la lista completa.
 */
const servidorDeTransferencias = {
  transferencias: [] as TransferenciaResumenDto[],
  pedidos: [] as Record<string, string | null>[],
};

beforeEach(() => {
  servidorDeTransferencias.transferencias = lasDeSiempre();
  servidorDeTransferencias.pedidos = [];

  servidor.use(
    http.get('/api/v1/inventario/transferencias', ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      const pedido = Object.fromEntries(
        ['page', 'size', 'estado', 'origen', 'destino', 'sort'].map((nombre) => [
          nombre,
          consulta.get(nombre),
        ]),
      );
      servidorDeTransferencias.pedidos.push(pedido);

      const todas = servidorDeTransferencias.transferencias
        .filter((t) => pedido['estado'] === null || t.estado === pedido['estado'])
        .filter((t) => pedido['origen'] === null || t.almacenOrigenId === pedido['origen'])
        .filter((t) => pedido['destino'] === null || t.almacenDestinoId === pedido['destino']);

      return HttpResponse.json({
        elementos: todas,
        pagina: Number(pedido['page'] ?? 1),
        tamanio: Number(pedido['size'] ?? 20),
        total: todas.length,
      });
    }),

    http.get('/api/v1/organizacion/almacenes', () =>
      HttpResponse.json({ elementos: NAVES, pagina: 1, tamanio: 200, total: NAVES.length }),
    ),

    http.get('/api/v1/organizacion/almacenes/:id', ({ params }) => {
      const nave = NAVES.find((n) => n.id === params['id']);

      return nave === undefined ? new HttpResponse(null, { status: 404 }) : HttpResponse.json(nave);
    }),
  );
});

function montar(
  ruta = '/transferencias',
  permisos: readonly string[] = PUEDE_VER,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDeTransferencias />, ruta, 'es', '/transferencias');
}

function tabla(): HTMLElement {
  return screen.getByRole('table', { name: 'Transferencias de la empresa activa' });
}

/** Las filas de la tabla, celda a celda y sin la cabecera. */
function filas(): string[][] {
  return within(tabla())
    .getAllByRole('row')
    .slice(1)
    .map((fila) =>
      within(fila)
        .getAllByRole('cell')
        .map((celda) => celda.textContent),
    );
}

const CENTRAL_LARGO = 'ALM-ALFA · Nave central de Alfa';
const NORTE_LARGO = 'ALM-NORTE · Nave del norte';

describe('El listado de transferencias', () => {
  it('nombra cada una por su número, y el borrador, que no lo tiene, por sus almacenes y su día', async () => {
    montar();

    await screen.findByRole('table', { name: 'Transferencias de la empresa activa' });
    await waitFor(() => {
      expect(filas()).toEqual([
        [
          'Borrador de ALM-ALFA a ALM-NORTE del 5 nov 2026',
          CENTRAL_LARGO,
          NORTE_LARGO,
          '5 nov 2026',
          '',
          'Borrador',
        ],
        ['Transferencia 15', CENTRAL_LARGO, NORTE_LARGO, '4 nov 2026', '', 'Enviada'],
        ['Transferencia 14', NORTE_LARGO, CENTRAL_LARGO, '3 nov 2026', '4 nov 2026', 'Recibida'],
        ['Transferencia 13', CENTRAL_LARGO, NORTE_LARGO, '2 nov 2026', '2 nov 2026', 'Anulada'],
      ]);
    });
    expect(screen.getByRole('link', { name: 'Transferencia 15' })).toHaveAttribute(
      'href',
      '/transferencias/7a7a7a7a-0000-0000-0000-000000000015',
    );
    // La tabla corre dentro de su contenedor, y no la página (ADR-0068 §8).
    expect(tabla().parentElement).toHaveClass('overflow-x-auto');
    // Sin filtro no viaja ninguno: ni los almacenes vacíos ni el orden de siempre.
    expect(servidorDeTransferencias.pedidos).toEqual([
      { page: '1', size: '20', estado: null, origen: null, destino: null, sort: null },
    ]);
  });

  it('los filtros van a la URL y al servidor con los nombres de la API, desde la primera página', async () => {
    const { enrutador } = montar('/transferencias?pagina=2');
    const usuario = userEvent.setup();

    // Las dos listas se vuelven a montar cuando llegan sus opciones: se leen después.
    await waitFor(() => {
      expect(screen.getAllByRole('option', { name: NORTE_LARGO })).toHaveLength(2);
    });
    const origen = screen.getByRole('combobox', { name: 'Origen' });
    const destino = screen.getByRole('combobox', { name: 'Destino' });
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Estado' }), 'Enviada');
    await usuario.selectOptions(origen, CENTRAL_LARGO);
    await usuario.selectOptions(destino, NORTE_LARGO);
    await usuario.selectOptions(
      screen.getByRole('combobox', { name: 'Orden' }),
      'Recepción, del último día al primero',
    );
    await usuario.click(screen.getByRole('button', { name: 'Filtrar' }));

    await waitFor(() => {
      expect(servidorDeTransferencias.pedidos.at(-1)).toEqual({
        page: '1',
        size: '20',
        estado: 'Enviada',
        origen: CENTRAL.id,
        destino: NAVE_DEL_NORTE.id,
        sort: '-fechaDeRecepcion',
      });
    });
    expect(Object.fromEntries(new URLSearchParams(enrutador.state.location.search))).toEqual({
      estado: 'Enviada',
      origen: CENTRAL.id,
      destino: NAVE_DEL_NORTE.id,
      orden: '-fechaDeRecepcion',
    });
    await waitFor(() => {
      expect(filas().map((fila) => fila[0])).toEqual(['Transferencia 15']);
    });
  });

  it('cada punta acota sola: lo que llega a la central es lo que sale del norte', async () => {
    montar(`/transferencias?destino=${CENTRAL.id}`);

    await waitFor(() => {
      expect(filas().map((fila) => fila[0])).toEqual(['Transferencia 14']);
    });
    expect(servidorDeTransferencias.pedidos.at(-1)).toMatchObject({
      origen: null,
      destino: CENTRAL.id,
    });
    // El filtro de la URL sale elegido, y el otro, en «Todos».
    expect(screen.getByRole('combobox', { name: 'Destino' })).toHaveValue(CENTRAL.id);
    expect(screen.getByRole('combobox', { name: 'Origen' })).toHaveValue('');
  });

  it('acotada y sin nada, lo dice del filtro; sin acotar, de la empresa', async () => {
    servidorDeTransferencias.transferencias = [];
    const { unmount } = montar(`/transferencias?origen=${NAVE_DEL_NORTE.id}`);

    expect(
      await screen.findByText('Ninguna transferencia coincide con el filtro.'),
    ).toBeInTheDocument();
    unmount();

    montar();
    expect(
      await screen.findByText('Todavía no hay ninguna transferencia en esta empresa.'),
    ).toBeInTheDocument();
  });

  it('un estado que esta versión no conoce lleva la marca y su explicación', async () => {
    servidorDeTransferencias.transferencias = [resumen(17, 'Devuelta', '2026-11-06', null)];
    montar();

    const celda = within(
      await screen.findByRole('table', { name: 'Transferencias de la empresa activa' }),
    ).getByText('Sin reconocer');
    const explicacion = screen.getByText(
      'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
    );

    expect(celda.closest('td')).toHaveAttribute(
      'aria-describedby',
      explicacion.closest('[id]')?.id,
    );
  });

  it('quien no ve los almacenes no encuentra los dos filtros, y cada fila lleva su identificador', async () => {
    montar(
      '/transferencias',
      PUEDE_VER.filter((permiso) => permiso !== PERMISOS.almacenVer),
    );

    await screen.findByRole('table', { name: 'Transferencias de la empresa activa' });
    expect(screen.queryByRole('combobox', { name: 'Origen' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Destino' })).not.toBeInTheDocument();
    expect(filas()[1]?.slice(0, 3)).toEqual(['Transferencia 15', CENTRAL.id, NAVE_DEL_NORTE.id]);
  });

  it('el alta se ofrece a quien puede abrir, y a quien solo ve, no', async () => {
    const { unmount } = montar('/transferencias', [...PUEDE_VER, PERMISOS.transferenciaAbrir]);

    expect(await screen.findByRole('link', { name: 'Nueva transferencia' })).toHaveAttribute(
      'href',
      '/transferencias/nueva',
    );
    unmount();

    montar();
    await screen.findByRole('table', { name: 'Transferencias de la empresa activa' });
    expect(screen.queryByRole('link', { name: 'Nueva transferencia' })).not.toBeInTheDocument();
  });
});
