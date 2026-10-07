import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDeRecuentos } from './PaginaDeRecuentos.tsx';
import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type RecuentoResumenDto = components['schemas']['RecuentoResumenDto'];
type RecuentoDto = components['schemas']['RecuentoDto'];
type SerieDto = components['schemas']['SerieDto'];

const PUEDE_TODO = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.recuentoVer,
  PERMISOS.recuentoAbrir,
  PERMISOS.serieVer,
];

/** La nave de Alfa, la única que tiene: ALM-ALFA. */
const ALMACEN = almacenesDe(ALFA.id).elementos[0]!;

const EJERCICIO = 'e1e1e1e1-0000-0000-0000-000000000001';

function serie(numero: number, tipoDeDocumento: string, codigo: string, estado: string): SerieDto {
  return {
    id: `5e5e5e5e-0000-0000-0000-${String(numero).padStart(12, '0')}`,
    empresaId: ALFA.id,
    ejercicioId: EJERCICIO,
    tipoDeDocumento,
    codigo,
    formato: '{SERIE}-{NUMERO}',
    contador: 0,
    estado,
  };
}

/** Una de cada clase que se ofrece, una cerrada y una de otro documento, que no se ofrecen. */
const SERIES = [
  serie(1, 'RecuentoDeInventario', 'REC', 'Activa'),
  serie(2, 'AjusteDeInventario', 'AJU', 'Activa'),
  serie(3, 'RecuentoDeInventario', 'REC-VIEJA', 'Cerrada'),
  serie(4, 'FacturaEmitida', 'FAC', 'Activa'),
];

const [REC, AJU] = SERIES as [SerieDto, SerieDto];

function resumen(
  numero: number,
  estado: string,
  apertura: string,
  confirmacion: string | null,
  motivo: string,
): RecuentoResumenDto {
  return {
    id: `7e7e7e7e-0000-0000-0000-${String(numero).padStart(12, '0')}`,
    // Sin número hasta que se confirma (ADR-0055 §1.4).
    numero: estado === 'EnCurso' || estado === 'Descartado' ? null : numero,
    almacenId: ALMACEN.id,
    fechaDeApertura: apertura,
    fechaDeConfirmacion: confirmacion,
    estado,
    motivo,
  };
}

/** Uno en curso y uno confirmado, del más reciente al más antiguo, como los ordena la API. */
function losDeSiempre(): RecuentoResumenDto[] {
  return [
    resumen(8, 'EnCurso', '2026-11-02', null, 'Inventario de noviembre'),
    resumen(7, 'Confirmado', '2026-10-01', '2026-10-03', 'Cierre de octubre'),
  ];
}

const NUEVO = '7e7e7e7e-0000-0000-0000-000000000099';

/**
 * Los recuentos del lado del servidor, lo que se ha pedido y lo que ha llegado a cada alta.
 *
 * El listado acota por estado y por almacén y pagina como la API. Lo que se pide se anota entero,
 * porque una pantalla que filtrara en el navegador pintaría lo mismo con la lista completa.
 */
const servidorDeRecuentos = {
  recuentos: [] as RecuentoResumenDto[],
  pedidos: [] as {
    pagina: string | null;
    tamanio: string | null;
    estado: string | null;
    almacen: string | null;
  }[],
  falloDelListado: false,
  rechazoDelAlta: null as { estado: number; codigo: string } | null,
  altas: [] as { clave: string | null; cuerpo: unknown }[],
};

beforeEach(() => {
  servidorDeRecuentos.recuentos = losDeSiempre();
  servidorDeRecuentos.pedidos = [];
  servidorDeRecuentos.falloDelListado = false;
  servidorDeRecuentos.rechazoDelAlta = null;
  servidorDeRecuentos.altas = [];

  servidor.use(
    http.get('/api/v1/inventario/recuentos', ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      const pedido = {
        pagina: consulta.get('page'),
        tamanio: consulta.get('size'),
        estado: consulta.get('estado'),
        almacen: consulta.get('almacen'),
      };
      servidorDeRecuentos.pedidos.push(pedido);

      if (servidorDeRecuentos.falloDelListado) {
        return HttpResponse.json({ type: '/errors/error-interno', status: 500 }, { status: 500 });
      }

      const pagina = Number(pedido.pagina ?? 1);
      const tamanio = Number(pedido.tamanio ?? 20);
      const todos = servidorDeRecuentos.recuentos
        .filter((r) => pedido.estado === null || r.estado === pedido.estado)
        .filter((r) => pedido.almacen === null || r.almacenId === pedido.almacen);

      return HttpResponse.json({
        elementos: todos.slice((pagina - 1) * tamanio, pagina * tamanio),
        pagina,
        tamanio,
        total: todos.length,
      });
    }),

    http.get('/api/v1/organizacion/almacenes/:id', ({ params }) =>
      params['id'] === ALMACEN.id
        ? HttpResponse.json(ALMACEN)
        : HttpResponse.json(
            { type: '/errors/almacen-no-encontrado', status: 404 },
            { status: 404 },
          ),
    ),

    http.get('/api/v1/organizacion/series', () =>
      HttpResponse.json({ elementos: SERIES, pagina: 1, tamanio: 200, total: SERIES.length }),
    ),

    http.post('/api/v1/inventario/recuentos', async ({ request }) => {
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDeRecuentos.altas.push({ clave: request.headers.get('Idempotency-Key'), cuerpo });

      if (servidorDeRecuentos.rechazoDelAlta !== null) {
        const { estado, codigo } = servidorDeRecuentos.rechazoDelAlta;
        return HttpResponse.json({ type: `/errors/${codigo}`, status: estado }, { status: estado });
      }

      const abierto: RecuentoDto = {
        id: NUEVO,
        serieId: String(cuerpo['serieId']),
        serieDelAjusteId: String(cuerpo['serieDelAjusteId']),
        numero: null,
        almacenId: String(cuerpo['almacenId']),
        fechaDeApertura: '2026-11-05',
        fechaDeConfirmacion: null,
        estado: 'EnCurso',
        motivo: String(cuerpo['motivo']),
        divisa: 'EUR',
        motivoDelDescarte: null,
        motivoDeLaAnulacion: null,
        ajusteId: null,
        lineas: 0,
        lineasSinContar: 0,
        lineasConElTeoricoCambiado: 0,
        lineasConTransito: 0,
        huellaDelTeorico: 'a'.repeat(64),
      };

      return HttpResponse.json(abierto, {
        status: 201,
        headers: { Location: `/api/v1/inventario/recuentos/${NUEVO}`, ETag: '"1"' },
      });
    }),
  );
});

function montar(
  ruta = '/recuentos',
  idioma: Idioma = 'es',
  permisos: readonly string[] = PUEDE_TODO,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDeRecuentos />, ruta, idioma, undefined, [
    { path: '/recuentos/:id', element: <p>La ficha del recuento abierto</p> },
  ]);
}

/** Las filas de la tabla, celda a celda y sin la cabecera. */
function filas(nombre = 'Recuentos de la empresa activa'): string[][] {
  const tabla = screen.getByRole('table', { name: nombre });

  return within(tabla)
    .getAllByRole('row')
    .slice(1)
    .map((fila) =>
      within(fila)
        .getAllByRole('cell')
        .map((celda) => celda.textContent),
    );
}

function ultimoPedido(): (typeof servidorDeRecuentos.pedidos)[number] | undefined {
  return servidorDeRecuentos.pedidos.at(-1);
}

describe('El listado de recuentos', () => {
  it('nombra cada recuento por su número o, sin él, por su almacén y su día', async () => {
    montar();

    await waitFor(() => {
      expect(filas()).toEqual([
        [
          'Recuento de ALM-ALFA del 2 nov 2026',
          'ALM-ALFA · Nave central de Alfa',
          '2 nov 2026',
          '',
          'En curso',
          'Inventario de noviembre',
        ],
        [
          'Recuento 7',
          'ALM-ALFA · Nave central de Alfa',
          '1 oct 2026',
          '3 oct 2026',
          'Confirmado',
          'Cierre de octubre',
        ],
      ]);
    });

    expect(screen.getByRole('link', { name: 'Recuento 7' })).toHaveAttribute(
      'href',
      `/recuentos/${servidorDeRecuentos.recuentos[1]!.id}`,
    );
    // La primera página, del tamaño de siempre, y sin acotar.
    expect(ultimoPedido()).toEqual({ pagina: '1', tamanio: '20', estado: null, almacen: null });
  });

  it('el estado y el almacén van a la URL y al servidor, y vuelven a la primera página', async () => {
    const { enrutador } = montar('/recuentos?pagina=3');
    const usuario = userEvent.setup();
    const filtros = screen.getByRole('search');

    await within(filtros).findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
    await usuario.selectOptions(within(filtros).getByRole('combobox', { name: 'Estado' }), [
      'Confirmado',
    ]);
    await usuario.selectOptions(within(filtros).getByRole('combobox', { name: 'Almacén' }), [
      'ALM-ALFA · Nave central de Alfa',
    ]);
    await usuario.click(within(filtros).getByRole('button', { name: 'Filtrar' }));

    await waitFor(() => {
      expect(filas()).toHaveLength(1);
    });
    expect(Object.fromEntries(new URLSearchParams(enrutador.state.location.search))).toEqual({
      estado: 'Confirmado',
      almacen: ALMACEN.id,
    });
    expect(ultimoPedido()).toEqual({
      pagina: '1',
      tamanio: '20',
      estado: 'Confirmado',
      almacen: ALMACEN.id,
    });
    // Con la URL ya acotada, las dos listas enseñan el filtro que se está viendo.
    expect(within(filtros).getByRole('combobox', { name: 'Estado' })).toHaveValue('Confirmado');
    expect(within(filtros).getByRole('combobox', { name: 'Almacén' })).toHaveValue(ALMACEN.id);
  });

  it('lo que la URL trae y no se reconoce se ignora: ni se manda ni se enseña', async () => {
    montar('/recuentos?estado=constructor&almacen=no-es-un-almacen');

    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
    expect(ultimoPedido()).toEqual({ pagina: '1', tamanio: '20', estado: null, almacen: null });
    expect(screen.getByRole('combobox', { name: 'Estado' })).toHaveValue('');
  });

  it('sin recuentos lo dice, y no es lo mismo que ninguno con el filtro', async () => {
    servidorDeRecuentos.recuentos = [];
    const primera = montar();

    expect(
      await screen.findByText('Todavía no hay ningún recuento en esta empresa.'),
    ).toBeInTheDocument();
    primera.unmount();

    montar('/recuentos?estado=Anulado');

    expect(await screen.findByText('Ningún recuento coincide con el filtro.')).toBeInTheDocument();
  });

  it('si el listado falla, lo dice y deja volver a intentarlo', async () => {
    servidorDeRecuentos.falloDelListado = true;
    montar();
    const usuario = userEvent.setup();

    const fallo = await screen.findByRole('alert');
    servidorDeRecuentos.falloDelListado = false;
    await usuario.click(within(fallo).getByRole('button', { name: 'Volver a intentarlo' }));

    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
  });

  it('un estado que esta versión no conoce sale marcado, y la celda dice por qué', async () => {
    servidorDeRecuentos.recuentos = [
      resumen(9, 'Revisado', '2026-11-04', null, 'Uno de una versión futura'),
    ];
    montar();

    const celda = await screen.findByRole('cell', { name: 'Sin reconocer' });

    expect(celda).toHaveAccessibleDescription(
      'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
    );
  });

  it('pagina en el servidor, con la página en la URL', async () => {
    servidorDeRecuentos.recuentos = Array.from({ length: 25 }, (_, indice) =>
      resumen(100 + indice, 'Confirmado', '2026-10-01', '2026-10-02', `Recuento ${String(indice)}`),
    );
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await waitFor(() => {
      expect(filas()).toHaveLength(20);
    });
    await usuario.click(screen.getByRole('button', { name: 'Siguiente' }));

    await waitFor(() => {
      expect(filas()).toHaveLength(5);
    });
    expect(new URLSearchParams(enrutador.state.location.search).get('pagina')).toBe('2');
    expect(ultimoPedido()?.pagina).toBe('2');
  });

  describe('el alta', () => {
    it('sin elegir ni escribir nada, lo dice en cada campo y no va al servidor', async () => {
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Abrir el recuento' }));

      const alta = screen.getByRole('region', { name: 'Abrir un recuento' });
      expect(within(alta).getByRole('combobox', { name: 'Almacén' })).toHaveAccessibleDescription(
        'Elige el almacén que se cuenta.',
      );
      expect(
        within(alta).getByRole('combobox', { name: 'Serie del recuento' }),
      ).toHaveAccessibleDescription('Elige una serie.');
      expect(
        within(alta).getByRole('combobox', { name: 'Serie del ajuste' }),
      ).toHaveAccessibleDescription('Elige una serie.');
      expect(within(alta).getByRole('textbox', { name: 'Motivo' })).toHaveAccessibleDescription(
        /^Escribe el motivo, en 300 caracteres o menos/,
      );
      expect(servidorDeRecuentos.altas).toEqual([]);
    });

    it('ofrece solo las series activas de cada clase', async () => {
      montar();

      const alta = screen.getByRole('region', { name: 'Abrir un recuento' });
      const delRecuento = within(alta).getByRole('combobox', { name: 'Serie del recuento' });

      await within(delRecuento).findByRole('option', { name: 'REC' });
      expect(
        within(delRecuento)
          .getAllByRole('option')
          .map((opcion) => opcion.textContent),
      ).toEqual(['Sin elegir', 'REC']);
      expect(
        within(within(alta).getByRole('combobox', { name: 'Serie del ajuste' }))
          .getAllByRole('option')
          .map((opcion) => opcion.textContent),
      ).toEqual(['Sin elegir', 'AJU']);
    });

    it('abre el recuento con su clave, y lleva a su ficha', async () => {
      const { enrutador } = montar();
      const usuario = userEvent.setup();

      await rellenar(usuario, '  Inventario anual  ');
      await usuario.click(screen.getByRole('button', { name: 'Abrir el recuento' }));

      expect(await screen.findByText('La ficha del recuento abierto')).toBeInTheDocument();
      expect(enrutador.state.location.pathname).toBe(`/recuentos/${NUEVO}`);
      expect(servidorDeRecuentos.altas).toHaveLength(1);
      expect(servidorDeRecuentos.altas[0]).toEqual({
        clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown,
        cuerpo: {
          almacenId: ALMACEN.id,
          serieId: REC.id,
          serieDelAjusteId: AJU.id,
          motivo: 'Inventario anual',
        },
      });
    });

    it('el almacén que ya se está contando va en su campo, con el foco; repetir repite la clave', async () => {
      servidorDeRecuentos.rechazoDelAlta = { estado: 409, codigo: 'recuento-ya-hay-uno-en-curso' };
      montar();
      const usuario = userEvent.setup();

      await rellenar(usuario, 'Inventario anual');
      await usuario.click(screen.getByRole('button', { name: 'Abrir el recuento' }));

      const alta = screen.getByRole('region', { name: 'Abrir un recuento' });
      const almacen = within(alta).getByRole('combobox', { name: 'Almacén' });
      await waitFor(() => {
        expect(almacen).toHaveFocus();
      });
      expect(almacen).toHaveAccessibleDescription(
        'Ese almacén ya se está contando. Confirma o descarta el recuento en curso antes de abrir otro.',
      );

      // Lo mismo otra vez es la misma operación; con otro motivo, otra.
      await usuario.click(screen.getByRole('button', { name: 'Abrir el recuento' }));
      await usuario.type(within(alta).getByRole('textbox', { name: 'Motivo' }), ' bis');
      await usuario.click(screen.getByRole('button', { name: 'Abrir el recuento' }));

      await waitFor(() => {
        expect(servidorDeRecuentos.altas).toHaveLength(3);
      });
      const [primera, segunda, tercera] = servidorDeRecuentos.altas;
      expect(segunda?.clave).toBe(primera?.clave);
      expect(tercera?.clave).not.toBe(primera?.clave);
    });

    it('lo que no es de un campo va arriba del formulario', async () => {
      servidorDeRecuentos.rechazoDelAlta = { estado: 409, codigo: 'recuento-serie-cerrada' };
      montar();
      const usuario = userEvent.setup();

      await rellenar(usuario, 'Inventario anual');
      await usuario.click(screen.getByRole('button', { name: 'Abrir el recuento' }));

      const alta = screen.getByRole('region', { name: 'Abrir un recuento' });
      const arriba = await within(alta).findByRole('alert');
      expect(arriba.textContent).not.toBe('');
      expect(within(alta).getByRole('combobox', { name: 'Almacén' })).toHaveAttribute(
        'aria-invalid',
        'false',
      );
    });

    it('quien no puede abrir no encuentra el formulario', async () => {
      montar('/recuentos', 'es', [...PERMISOS_DE_LECTURA, PERMISOS.recuentoVer]);

      await waitFor(() => {
        expect(filas()).toHaveLength(2);
      });
      expect(screen.queryByRole('region', { name: 'Abrir un recuento' })).not.toBeInTheDocument();
    });

    it('sin poder ver las series, lo dice en vez de ofrecer listas vacías', async () => {
      montar('/recuentos', 'es', [
        ...PERMISOS_DE_LECTURA,
        PERMISOS.recuentoVer,
        PERMISOS.recuentoAbrir,
      ]);

      const alta = screen.getByRole('region', { name: 'Abrir un recuento' });

      expect(
        within(alta).getByText(
          'Para abrir un recuento hace falta poder ver los almacenes y las series.',
        ),
      ).toBeInTheDocument();
      expect(within(alta).queryByRole('combobox')).not.toBeInTheDocument();
      await waitFor(() => {
        expect(filas()).toHaveLength(2);
      });
    });
  });

  it('en inglés, la pantalla entera', async () => {
    montar('/recuentos', 'en');

    await waitFor(() => {
      expect(filas('Stock counts of the active company')).toEqual([
        [
          'Stock count of ALM-ALFA on Nov 2, 2026',
          'ALM-ALFA · Nave central de Alfa',
          'Nov 2, 2026',
          '',
          'In progress',
          'Inventario de noviembre',
        ],
        [
          'Stock count 7',
          'ALM-ALFA · Nave central de Alfa',
          'Oct 1, 2026',
          'Oct 3, 2026',
          'Confirmed',
          'Cierre de octubre',
        ],
      ]);
    });
    expect(screen.getByRole('region', { name: 'Open a stock count' })).toBeInTheDocument();
  });
});

/** Elige el almacén y las dos series, y escribe el motivo. */
async function rellenar(
  usuario: ReturnType<typeof userEvent.setup>,
  motivo: string,
): Promise<void> {
  const alta = screen.getByRole('region', { name: 'Abrir un recuento' });
  const almacen = within(alta).getByRole('combobox', { name: 'Almacén' });
  const delRecuento = within(alta).getByRole('combobox', { name: 'Serie del recuento' });

  await within(almacen).findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
  await within(delRecuento).findByRole('option', { name: 'REC' });

  await usuario.selectOptions(almacen, ['ALM-ALFA · Nave central de Alfa']);
  await usuario.selectOptions(delRecuento, ['REC']);
  await usuario.selectOptions(within(alta).getByRole('combobox', { name: 'Serie del ajuste' }), [
    'AJU',
  ]);
  await usuario.type(within(alta).getByRole('textbox', { name: 'Motivo' }), motivo);
}
