import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { PaginaDeSaldos } from './PaginaDeSaldos.tsx';
import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import {
  ALFA,
  HOY_SIMULADO,
  PERMISOS_DE_LECTURA,
  almacenesDe,
  articulosDe,
} from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { capturarExportaciones, exportarYComprobar } from '@/pruebas/exportaciones.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type SaldoDto = components['schemas']['SaldoDto'];

const PUEDE_TODO = [...PERMISOS_DE_LECTURA, PERMISOS.saldoVer];

/** La nave de Alfa, la única que tiene: ALM-ALFA. */
const ALMACEN = almacenesDe(ALFA.id).elementos[0]!;

/** Los artículos de Alfa: TOR-M6, TUE-M6 y MO-TALLER. */
const ARTICULOS = articulosDe(ALFA.id).elementos;
const [TORNILLO, TUERCA] = ARTICULOS as [(typeof ARTICULOS)[number], (typeof ARTICULOS)[number]];

/**
 * El reloj, parado en un instante UTC y no en uno local: «hoy» es el día UTC (ADR-0063 §3), y un
 * instante local daría otro día en una máquina al otro lado del meridiano. Solo el reloj, como en
 * las tarifas: los temporizadores siguen siendo los de verdad.
 */
beforeAll(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));
});

afterAll(() => {
  vi.useRealTimers();
});

function saldo(
  articuloId: string,
  cantidad: number | string,
  valor: number | string,
  enTransito: number | string = 0,
  valorEnTransito: number | string = 0,
): SaldoDto {
  return {
    articuloId,
    almacenId: ALMACEN.id,
    cantidad,
    valor,
    cantidadEnTransito: enTransito,
    valorEnTransito,
    divisa: 'EUR',
  };
}

/** Dos pares, uno con algo en camino. Las cifras, como `string` y como `number`: valen las dos. */
function losDeSiempre(): SaldoDto[] {
  return [saldo(TORNILLO.id, '12.5', '31.25', 4, '10'), saldo(TUERCA.id, 0, 0)];
}

/**
 * El saldo del lado del servidor: lo que se ha pedido y lo que contesta.
 *
 * Acota por almacén y por artículo y pagina como la API, y contesta con el día pedido o, sin él,
 * con su hoy. Lo que se pide se anota entero, porque una pantalla que acotara en el navegador
 * pintaría lo mismo con la lista completa.
 */
const servidorDeSaldos = {
  saldos: [] as SaldoDto[],
  pedidos: [] as {
    pagina: string | null;
    tamanio: string | null;
    fecha: string | null;
    almacen: string | null;
    articulo: string | null;
  }[],
  falloDelSaldo: false,
  /** El día del reloj del servidor, que no tiene por qué ser el del navegador. */
  hoy: HOY_SIMULADO,
};

beforeEach(() => {
  servidorDeSaldos.saldos = losDeSiempre();
  servidorDeSaldos.pedidos = [];
  servidorDeSaldos.falloDelSaldo = false;
  servidorDeSaldos.hoy = HOY_SIMULADO;

  servidor.use(
    http.get('/api/v1/inventario/saldos', ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      const pedido = {
        pagina: consulta.get('page'),
        tamanio: consulta.get('size'),
        fecha: consulta.get('fecha'),
        almacen: consulta.get('almacen'),
        articulo: consulta.get('articulo'),
      };
      servidorDeSaldos.pedidos.push(pedido);

      if (servidorDeSaldos.falloDelSaldo) {
        return HttpResponse.json({ type: '/errors/error-interno', status: 500 }, { status: 500 });
      }

      const fecha = pedido.fecha ?? servidorDeSaldos.hoy;

      if (fecha > servidorDeSaldos.hoy) {
        return HttpResponse.json(
          { type: '/errors/saldo-con-fecha-futura', status: 400 },
          { status: 400 },
        );
      }

      const pagina = Number(pedido.pagina ?? 1);
      const tamanio = Number(pedido.tamanio ?? 20);
      const todos = servidorDeSaldos.saldos
        .filter((s) => pedido.almacen === null || s.almacenId === pedido.almacen)
        .filter((s) => pedido.articulo === null || s.articuloId === pedido.articulo);

      return HttpResponse.json({
        fecha,
        saldos: {
          elementos: todos.slice((pagina - 1) * tamanio, pagina * tamanio),
          pagina,
          tamanio,
          total: todos.length,
        },
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

    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      const articulo = ARTICULOS.find((a) => a.id === params['id']);

      return articulo === undefined
        ? HttpResponse.json(
            { type: '/errors/articulo-no-encontrado', status: 404 },
            { status: 404 },
          )
        : HttpResponse.json(articulo);
    }),
  );
});

function montar(
  ruta = '/saldos',
  idioma: Idioma = 'es',
  permisos: readonly string[] = PUEDE_TODO,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDeSaldos />, ruta, idioma);
}

const TABLA_DE_HOY = 'Saldo al final del 11 sept 2026, por artículo y almacén';

/** Las filas de la tabla, celda a celda y sin la cabecera. */
function filas(nombre = TABLA_DE_HOY): string[][] {
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

function ultimoPedido(): (typeof servidorDeSaldos.pedidos)[number] | undefined {
  return servidorDeSaldos.pedidos.at(-1);
}

const SIN_ACOTAR = { pagina: '1', tamanio: '20', fecha: null, almacen: null, articulo: null };

describe('El saldo a una fecha', () => {
  it('sin día pide el de hoy, y pinta el día que contesta, con lo que vuela aparte', async () => {
    montar();

    await waitFor(() => {
      expect(filas()).toEqual([
        [
          'TOR-M6 · Tornillo M6 zincado',
          'ALM-ALFA · Nave central de Alfa',
          '12,5',
          '31,25 €',
          '4',
          '10,00 €',
        ],
        [
          'TUE-M6 · Tuerca M6 zincada',
          'ALM-ALFA · Nave central de Alfa',
          '0',
          '0,00 €',
          '0',
          '0,00 €',
        ],
      ]);
    });

    // Sin día no se manda ninguno: contesta el servidor con el suyo.
    expect(ultimoPedido()).toEqual(SIN_ACOTAR);
    // El campo enseña hoy, y no deja pasar de hoy.
    const dia = screen.getByLabelText('Al final del día');
    expect(dia).toHaveValue(HOY_SIMULADO);
    expect(dia).toHaveAttribute('max', HOY_SIMULADO);
    // Las dos columnas de lo que vuela dicen que no está sumado.
    const aparte =
      'Lo que está en tránsito salió de otro almacén hacia este y todavía no había llegado al ' +
      'final del día. No está sumado ni en la cantidad ni en el valor.';
    expect(screen.getByRole('columnheader', { name: 'En tránsito' })).toHaveAccessibleDescription(
      aparte,
    );
    expect(
      screen.getByRole('columnheader', { name: 'Valor en tránsito' }),
    ).toHaveAccessibleDescription(aparte);
  });

  it('el día, el almacén y el artículo van a la URL y al servidor, y vuelven a la primera página', async () => {
    const { enrutador } = montar('/saldos?pagina=3');
    const usuario = userEvent.setup();
    const filtros = screen.getByRole('search');

    await within(filtros).findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
    await within(filtros).findByRole('option', { name: 'TOR-M6 · Tornillo M6 zincado' });
    const dia = within(filtros).getByLabelText('Al final del día');
    await usuario.clear(dia);
    await usuario.type(dia, '2026-08-31');
    await usuario.selectOptions(within(filtros).getByRole('combobox', { name: 'Almacén' }), [
      'ALM-ALFA · Nave central de Alfa',
    ]);
    await usuario.selectOptions(within(filtros).getByRole('combobox', { name: 'Artículo' }), [
      'TOR-M6 · Tornillo M6 zincado',
    ]);
    await usuario.click(within(filtros).getByRole('button', { name: 'Consultar' }));

    await waitFor(() => {
      expect(filas('Saldo al final del 31 ago 2026, por artículo y almacén')).toHaveLength(1);
    });
    expect(Object.fromEntries(new URLSearchParams(enrutador.state.location.search))).toEqual({
      fecha: '2026-08-31',
      almacen: ALMACEN.id,
      articulo: TORNILLO.id,
    });
    expect(ultimoPedido()).toEqual({
      pagina: '1',
      tamanio: '20',
      fecha: '2026-08-31',
      almacen: ALMACEN.id,
      articulo: TORNILLO.id,
    });
    // Con la URL ya acotada, los tres campos enseñan lo que se está viendo.
    expect(within(filtros).getByLabelText('Al final del día')).toHaveValue('2026-08-31');
    expect(within(filtros).getByRole('combobox', { name: 'Almacén' })).toHaveValue(ALMACEN.id);
    expect(within(filtros).getByRole('combobox', { name: 'Artículo' })).toHaveValue(TORNILLO.id);
  });

  it('lo que la URL trae y no se reconoce se ignora: ni se manda ni se enseña', async () => {
    montar('/saldos?fecha=2026-02-31&almacen=constructor&articulo=7');

    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
    expect(ultimoPedido()).toEqual(SIN_ACOTAR);
    expect(screen.getByLabelText('Al final del día')).toHaveValue(HOY_SIMULADO);
  });

  it('un día futuro que llega por la URL lo rechaza el servidor, y no se ofrece reintentarlo', async () => {
    montar('/saldos?fecha=2026-09-12');

    const fallo = await screen.findByRole('alert');

    expect(fallo).toHaveTextContent(
      'Esa fecha es posterior a hoy: el saldo es la suma del libro, y el libro solo recoge lo ' +
        'que ya ha pasado. Elige hoy o un día anterior.',
    );
    expect(within(fallo).queryByRole('button')).not.toBeInTheDocument();
    expect(ultimoPedido()?.fecha).toBe('2026-09-12');
  });

  it('si el saldo falla, lo dice y deja volver a intentarlo', async () => {
    servidorDeSaldos.falloDelSaldo = true;
    montar();
    const usuario = userEvent.setup();

    const fallo = await screen.findByRole('alert');
    servidorDeSaldos.falloDelSaldo = false;
    await usuario.click(within(fallo).getByRole('button', { name: 'Volver a intentarlo' }));

    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
  });

  it('sin saldos lo dice, y no es lo mismo que ninguno con el filtro; y no ofrece exportar', async () => {
    servidorDeSaldos.saldos = [];
    const primera = montar();

    expect(await screen.findByText('Esta empresa todavía no ha movido stock.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Exportar a CSV' })).not.toBeInTheDocument();
    primera.unmount();

    for (const filtro of [`almacen=${ALMACEN.id}`, `articulo=${TORNILLO.id}`]) {
      const acotada = montar(`/saldos?${filtro}`);

      expect(await screen.findByText('Ningún saldo coincide con el filtro.')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Exportar a CSV' })).not.toBeInTheDocument();
      acotada.unmount();
    }
  });

  it('el título dice el día que contesta el servidor, aunque el navegador crea que es otro', async () => {
    // Un reloj que va por detrás: el servidor todavía está en el día 10.
    servidorDeSaldos.hoy = '2026-09-10';
    montar();

    await waitFor(() => {
      expect(filas('Saldo al final del 10 sept 2026, por artículo y almacén')).toHaveLength(2);
    });
    expect(ultimoPedido()).toEqual(SIN_ACOTAR);
  });

  it('sin día, exportar se lleva el que contestó el listado, con los dos filtros y sin la página', async () => {
    // El del servidor y no el del navegador: el fichero tiene que ser la tabla que se mira, y la
    // exportación pedida sin día contestaría con el de otra petición, que puede ser otro.
    servidorDeSaldos.hoy = '2026-09-10';
    const exportaciones = capturarExportaciones();
    montar(`/saldos?almacen=${ALMACEN.id}&articulo=${TORNILLO.id}&pagina=1`);

    await waitFor(() => {
      expect(filas('Saldo al final del 10 sept 2026, por artículo y almacén')).toHaveLength(1);
    });
    await exportarYComprobar(exportaciones, '/api/v1/inventario/saldos/exportacion', {
      fecha: '2026-09-10',
      almacen: ALMACEN.id,
      articulo: TORNILLO.id,
    });
  });

  it('pagina en el servidor, con la página en la URL', async () => {
    servidorDeSaldos.saldos = Array.from({ length: 25 }, (_, indice) =>
      saldo(`fffffff1-0000-0000-0000-${String(100 + indice).padStart(12, '0')}`, indice, indice),
    );
    const { enrutador } = montar('/saldos', 'es', [PERMISOS.saldoVer]);
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

  it('sin ver almacenes ni artículos no los ofrece ni los pregunta, y pinta sus identificadores', async () => {
    montar('/saldos', 'es', [PERMISOS.saldoVer]);

    await waitFor(() => {
      expect(filas()[0]?.slice(0, 2)).toEqual([TORNILLO.id, ALMACEN.id]);
    });
    const filtros = screen.getByRole('search');
    expect(within(filtros).queryByRole('combobox')).not.toBeInTheDocument();
    expect(within(filtros).getByLabelText('Al final del día')).toBeInTheDocument();
  });

  it('en inglés, el día, las cifras y la divisa se escriben en inglés', async () => {
    montar('/saldos', 'en');

    await waitFor(() => {
      expect(
        filas('Stock balance at the end of Sep 11, 2026, by item and warehouse')[0]?.slice(2),
      ).toEqual(['12.5', '€31.25', '4', '€10.00']);
    });
  });
});
