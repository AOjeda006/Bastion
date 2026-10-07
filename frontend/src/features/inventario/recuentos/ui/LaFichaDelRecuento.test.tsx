import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDelRecuento } from './PaginaDelRecuento.tsx';
import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, PERMISOS_DE_LECTURA, almacenesDe, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type ArticuloDto = components['schemas']['ArticuloDto'];
type RecuentoDto = components['schemas']['RecuentoDto'];
type LineaDeRecuentoDto = components['schemas']['LineaDeRecuentoDto'];
type UbicacionDto = components['schemas']['UbicacionDto'];
type UnidadMedidaDto = components['schemas']['UnidadMedidaDto'];

const PATRON = '/recuentos/:id';

const PUEDE_VER = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.recuentoVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
];

const PUEDE_TODO = [
  ...PUEDE_VER,
  PERMISOS.recuentoContar,
  PERMISOS.recuentoConfirmar,
  PERMISOS.recuentoAnular,
  PERMISOS.recuentoDescartar,
];

/** ALM-ALFA, y los dos artículos de Alfa que se cuentan: TOR-M6 y TUE-M6. */
const ALMACEN = almacenesDe(ALFA.id).elementos[0]!;
const [TORNILLO, TUERCA] = articulosDe(ALFA.id).elementos as [ArticuloDto, ArticuloDto];

const UNIDAD: UnidadMedidaDto = {
  id: 'aaaa0001-0000-0000-0000-000000000001',
  codigo: 'UD',
  nombre: 'Unidad',
  decimales: 0,
  retirada: false,
};

const UBICACION: UbicacionDto = {
  id: 'abab0000-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  almacenId: ALMACEN.id,
  codigo: 'A-01',
  pasillo: 'A',
  estante: '1',
  hueco: null,
  descripcion: 'Pasillo A',
};

const RECUENTO = '7e7e7e7e-0000-0000-0000-000000000008';
const AJUSTE = 'adadadad-0000-0000-0000-000000000001';
const HUELLA = 'a'.repeat(64);

function linea(numero: number, cambios: Partial<LineaDeRecuentoDto>): LineaDeRecuentoDto {
  return {
    id: `11ae0000-0000-0000-0000-${String(numero).padStart(12, '0')}`,
    numero,
    ubicacionId: UBICACION.id,
    articuloId: TORNILLO.id,
    codigoDeLote: null,
    numeroDeSerie: null,
    unidadBaseId: UNIDAD.id,
    origen: 'Precargada',
    costeUnitario: null,
    contado: null,
    teoricoAlContar: null,
    teorico: 10,
    enTransito: 0,
    diferencia: null,
    teoricoCambiado: false,
    lineaDeAjusteId: null,
    ...cambios,
  };
}

/**
 * Tres claves del almacén: una sin contar, un lote contado con uno de menos y con mercancía en
 * tránsito, y un número de serie sin contar.
 */
function lasDeSiempre(): LineaDeRecuentoDto[] {
  return [
    linea(1, { teorico: 10 }),
    linea(2, {
      articuloId: TUERCA.id,
      codigoDeLote: 'L-2026-07',
      teorico: 5,
      contado: 4,
      teoricoAlContar: 5,
      diferencia: -1,
      enTransito: 2,
    }),
    linea(3, { numeroDeSerie: 'SN-0001', teorico: 1 }),
  ];
}

/** Las tres, ya contadas: lo que hace falta para poder confirmar. */
function todasContadas(): LineaDeRecuentoDto[] {
  const [primera, segunda, tercera] = lasDeSiempre() as [
    LineaDeRecuentoDto,
    LineaDeRecuentoDto,
    LineaDeRecuentoDto,
  ];

  return [
    { ...primera, contado: 10, teoricoAlContar: 10, diferencia: 0 },
    segunda,
    { ...tercera, contado: 1, teoricoAlContar: 1, diferencia: 0 },
  ];
}

interface Escritura {
  ifMatch: string | null;
  clave: string | null;
  cuerpo: Record<string, unknown>;
}

/**
 * El recuento del lado del servidor: su estado, sus líneas, su versión y la huella de su teórico,
 * y lo que ha llegado a cada escritura.
 *
 * Responde como la API: la ficha lleva su `ETag` y las cuentas de sus líneas; cada línea, el suyo;
 * contar cambia la versión del recuento; confirmar compara la versión, después la huella y después
 * las líneas sin contar, en ese orden; y en curso cuenta el tránsito, cerrado no.
 */
const servidorDelRecuento = {
  estado: 'EnCurso',
  numero: null as number | null,
  fechaDeConfirmacion: null as string | null,
  motivoDelDescarte: null as string | null,
  motivoDeLaAnulacion: null as string | null,
  lineas: [] as LineaDeRecuentoDto[],
  version: 1,
  huella: HUELLA,
  rechazoDeContar: null as { estado: number; codigo: string } | null,
  conteos: [] as (Escritura & { lineaId: string })[],
  confirmaciones: [] as Escritura[],
  anulaciones: [] as Escritura[],
  descartes: [] as Escritura[],
  /** Los maestros que se han pedido, por su identificador: la caché los pide una vez. */
  maestros: [] as string[],
};

function enCurso(): boolean {
  return servidorDelRecuento.estado === 'EnCurso';
}

function ficha(): RecuentoDto {
  const s = servidorDelRecuento;

  return {
    id: RECUENTO,
    serieId: '5e5e5e5e-0000-0000-0000-000000000001',
    serieDelAjusteId: '5e5e5e5e-0000-0000-0000-000000000002',
    numero: s.numero,
    almacenId: ALMACEN.id,
    fechaDeApertura: '2026-11-02',
    fechaDeConfirmacion: s.fechaDeConfirmacion,
    estado: s.estado,
    motivo: 'Inventario de noviembre',
    divisa: 'EUR',
    motivoDelDescarte: s.motivoDelDescarte,
    motivoDeLaAnulacion: s.motivoDeLaAnulacion,
    ajusteId: s.numero === null ? null : AJUSTE,
    lineas: s.lineas.length,
    lineasSinContar: s.lineas.filter((l) => l.contado === null).length,
    lineasConElTeoricoCambiado: enCurso() ? s.lineas.filter((l) => l.teoricoCambiado).length : null,
    lineasConTransito: enCurso() ? s.lineas.filter((l) => Number(l.enTransito) > 0).length : null,
    huellaDelTeorico: enCurso() ? s.huella : null,
  };
}

function comoSeLee(l: LineaDeRecuentoDto): LineaDeRecuentoDto {
  return enCurso() ? l : { ...l, enTransito: null, teoricoCambiado: false };
}

function rechazo(estado: number, codigo: string): Response {
  return HttpResponse.json({ type: `/errors/${codigo}`, status: estado }, { status: estado });
}

/** Lo que se le manda a la ficha, con su versión y su clave. */
async function escritura(request: Request): Promise<Escritura> {
  return {
    ifMatch: request.headers.get('If-Match'),
    clave: request.headers.get('Idempotency-Key'),
    cuerpo: (await request.json()) as Record<string, unknown>,
  };
}

/** Alguien mueve mercancía de una clave: cambia su teórico y la huella del recuento. */
function moverElStock(numero: number, teorico: number): void {
  servidorDelRecuento.lineas = servidorDelRecuento.lineas.map((l) =>
    l.numero === numero
      ? {
          ...l,
          teorico,
          teoricoCambiado: l.contado !== null && Number(l.teoricoAlContar) !== teorico,
          diferencia: l.contado === null ? null : Number(l.contado) - teorico,
        }
      : l,
  );
  servidorDelRecuento.huella = 'b'.repeat(64);
}

beforeEach(() => {
  Object.assign(servidorDelRecuento, {
    estado: 'EnCurso',
    numero: null,
    fechaDeConfirmacion: null,
    motivoDelDescarte: null,
    motivoDeLaAnulacion: null,
    lineas: lasDeSiempre(),
    version: 1,
    huella: HUELLA,
    rechazoDeContar: null,
    conteos: [],
    confirmaciones: [],
    anulaciones: [],
    descartes: [],
    maestros: [],
  });

  const s = servidorDelRecuento;
  const base = '/api/v1/inventario/recuentos/:id';

  servidor.use(
    http.get(base, ({ params }) =>
      params['id'] === RECUENTO
        ? HttpResponse.json(ficha(), { headers: { ETag: `"${String(s.version)}"` } })
        : rechazo(404, 'recuento-no-encontrado'),
    ),

    http.get(`${base}/lineas`, ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      const solo = consulta.get('solo');
      const pagina = Number(consulta.get('page') ?? 1);
      const tamanio = Number(consulta.get('size') ?? 20);
      const todas = s.lineas
        .filter((l) => solo !== 'sin-contar' || l.contado === null)
        .filter((l) => solo !== 'teorico-cambiado' || l.teoricoCambiado)
        .map(comoSeLee);

      return HttpResponse.json({
        elementos: todas.slice((pagina - 1) * tamanio, pagina * tamanio),
        pagina,
        tamanio,
        total: todas.length,
      });
    }),

    http.get(`${base}/lineas/:lineaId`, ({ params }) => {
      const una = s.lineas.find((l) => l.id === params['lineaId']);

      return una === undefined
        ? rechazo(404, 'recuento-linea-no-encontrada')
        : HttpResponse.json(comoSeLee(una), { headers: { ETag: `"L${String(una.numero)}"` } });
    }),

    http.put(`${base}/lineas/:lineaId`, async ({ params, request }) => {
      const lineaId = String(params['lineaId']);
      s.conteos.push({ lineaId, ...(await escritura(request)) });

      if (s.rechazoDeContar !== null) {
        return rechazo(s.rechazoDeContar.estado, s.rechazoDeContar.codigo);
      }

      const contado = Number(s.conteos.at(-1)?.cuerpo['contado']);
      s.lineas = s.lineas.map((l) =>
        l.id === lineaId
          ? {
              ...l,
              contado,
              teoricoAlContar: l.teorico,
              diferencia: contado - Number(l.teorico),
              teoricoCambiado: false,
            }
          : l,
      );
      s.version += 1;

      return HttpResponse.json(s.lineas.find((l) => l.id === lineaId));
    }),

    http.post(`${base}/confirmacion`, async ({ request }) => {
      const recibida = await escritura(request);
      s.confirmaciones.push(recibida);

      if (recibida.ifMatch !== `"${String(s.version)}"`) {
        return rechazo(412, 'version-obsoleta');
      }

      if (recibida.cuerpo['huellaDelTeorico'] !== s.huella) {
        return rechazo(409, 'recuento-teorico-cambiado');
      }

      if (s.lineas.some((l) => l.contado === null)) {
        return rechazo(422, 'recuento-con-lineas-sin-contar');
      }

      s.estado = 'Confirmado';
      s.numero = 12;
      s.fechaDeConfirmacion = '2026-11-06';
      s.version += 1;

      return HttpResponse.json(ficha());
    }),

    http.post(`${base}/anulacion`, async ({ request }) => {
      const recibida = await escritura(request);
      s.anulaciones.push(recibida);
      s.estado = 'Anulado';
      s.motivoDeLaAnulacion = String(recibida.cuerpo['motivo']);
      s.version += 1;

      return HttpResponse.json(ficha());
    }),

    http.post(`${base}/descarte`, async ({ request }) => {
      const recibida = await escritura(request);
      s.descartes.push(recibida);
      s.estado = 'Descartado';
      s.motivoDelDescarte = String(recibida.cuerpo['motivo']);
      s.lineas = s.lineas.map((l) => ({ ...l, teorico: null, diferencia: null }));
      s.version += 1;

      return HttpResponse.json(ficha());
    }),

    http.get('/api/v1/organizacion/almacenes/:id', ({ params }) => {
      s.maestros.push(String(params['id']));
      return HttpResponse.json(ALMACEN);
    }),

    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      s.maestros.push(String(params['id']));
      const articulo = [TORNILLO, TUERCA].find((a) => a.id === params['id']);

      return articulo === undefined
        ? rechazo(404, 'articulo-no-encontrado')
        : HttpResponse.json(articulo);
    }),

    http.get('/api/v1/organizacion/ubicaciones/:id', ({ params }) => {
      s.maestros.push(String(params['id']));
      return HttpResponse.json(UBICACION);
    }),

    http.get('/api/v1/organizacion/unidades-de-medida/:id', ({ params }) => {
      s.maestros.push(String(params['id']));
      return HttpResponse.json(UNIDAD);
    }),
  );
});

function montar(
  idioma: Idioma = 'es',
  permisos: readonly string[] = PUEDE_TODO,
  ruta = `/recuentos/${RECUENTO}`,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDelRecuento />, ruta, idioma, PATRON);
}

/** Las filas de las líneas, celda a celda y sin la cabecera. */
function filas(nombre = 'Líneas del recuento'): string[][] {
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

/** La cabecera de la ficha, término a término. */
function datos(): Record<string, string> {
  const terminos = screen.getAllByRole('term').map((termino) => termino.textContent);
  const valores = screen.getAllByRole('definition').map((valor) => valor.textContent);

  return Object.fromEntries(terminos.map((termino, indice) => [termino, valores[indice] ?? '']));
}

const LINEA_1_SIN_CONTAR = [
  '1',
  'TOR-M6 · Tornillo M6 zincado',
  'A-01 · Pasillo A',
  '',
  'UD',
  '10',
  '0',
  'Sin contar',
  '',
  'Contar',
];

describe('La ficha del recuento', () => {
  it('dice la cabecera y cada línea: el teórico, lo contado, la diferencia y la unidad', async () => {
    montar();

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Recuento de ALM-ALFA del 2 nov 2026' }),
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(filas()).toEqual([
        LINEA_1_SIN_CONTAR,
        [
          '2',
          'TUE-M6 · Tuerca M6 zincada',
          'A-01 · Pasillo A',
          'L-2026-07',
          'UD',
          '5',
          '2',
          '4',
          '-1',
          'Corregir',
        ],
        [
          '3',
          'TOR-M6 · Tornillo M6 zincado',
          'A-01 · Pasillo A',
          'SN-0001',
          'UD',
          '1',
          '0',
          'Sin contar',
          '',
          'Contar',
        ],
      ]);
    });
    expect(datos()).toEqual({
      Estado: 'En curso',
      Almacén: 'ALM-ALFA · Nave central de Alfa',
      'Abierto el': '2 nov 2026',
      Motivo: 'Inventario de noviembre',
      Líneas: '3 en total, 2 sin contar, 0 con el teórico cambiado y 1 con mercancía en tránsito',
    });
    expect(screen.getByText(/^Cada línea se cuenta en la unidad base/)).toBeInTheDocument();
    // Los nombres se piden a sus dueños una vez cada uno, aunque salgan en varias filas.
    expect([...servidorDelRecuento.maestros].sort()).toEqual(
      [ALMACEN.id, TORNILLO.id, TUERCA.id, UBICACION.id, UNIDAD.id].sort(),
    );
  });

  describe('contar', () => {
    it('lee la versión de la línea y manda la cifra con punto; el foco vuelve a la fila', async () => {
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Contar la línea 1' }));
      const campo = screen.getByRole('textbox', { name: 'Contado en la línea 1, en UD' });
      await waitFor(() => {
        expect(campo).toHaveFocus();
      });
      await usuario.type(campo, '12,5');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

      expect(await screen.findByText('Línea 1 contada.')).toBeInTheDocument();
      expect(servidorDelRecuento.conteos).toEqual([
        {
          lineaId: lasDeSiempre()[0]!.id,
          ifMatch: '"L1"',
          clave: null,
          cuerpo: { contado: '12.5' },
        },
      ]);
      await waitFor(() => {
        expect(filas()[0]?.slice(7, 9)).toEqual(['12,5', '+2,5']);
      });
      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: 'Corregir lo contado en la línea 1' }),
        ).toHaveFocus();
      });
    });

    it('lo que no vale lo para el formulario, en el campo y sin ir al servidor', async () => {
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Contar la línea 1' }));
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
      const campo = screen.getByRole('textbox', { name: 'Contado en la línea 1, en UD' });
      expect(campo).toHaveAccessibleDescription('Escribe lo contado; si no hay ninguno, un 0.');

      await usuario.type(campo, '1.234,5');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
      await waitFor(() => {
        expect(campo).toHaveAccessibleDescription(/^Lo contado va en la unidad base del artículo/);
      });

      // Un número de serie es una pieza: 0 o 1.
      await usuario.click(screen.getByRole('button', { name: 'Contar la línea 3' }));
      const serie = screen.getByRole('textbox', { name: 'Contado en la línea 3, en UD' });
      await usuario.type(serie, '2');
      await usuario.keyboard('{Enter}');
      await waitFor(() => {
        expect(serie).toHaveAccessibleDescription(/^Lo contado va en la unidad base del artículo/);
      });

      expect(servidorDelRecuento.conteos).toEqual([]);
    });

    it('la cifra que rechaza el servidor va en el campo, y el campo sigue abierto', async () => {
      servidorDelRecuento.rechazoDeContar = { estado: 422, codigo: 'recuento-contado-no-valido' };
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Contar la línea 1' }));
      const campo = screen.getByRole('textbox', { name: 'Contado en la línea 1, en UD' });
      await usuario.type(campo, '3');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() => {
        expect(campo).toHaveAccessibleDescription(/^Lo contado va en la unidad base del artículo/);
      });
      expect(campo).toHaveFocus();
    });

    it('cualquier otro rechazo cierra el campo y lo dice arriba, con el foco', async () => {
      servidorDelRecuento.rechazoDeContar = { estado: 412, codigo: 'version-obsoleta' };
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Contar la línea 1' }));
      await usuario.type(screen.getByRole('textbox', { name: /^Contado en la línea 1/ }), '3');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

      const aviso = await screen.findByRole('alert');
      expect(aviso).toHaveTextContent(/^Alguien ha guardado antes que tú/);
      await waitFor(() => {
        expect(aviso).toHaveFocus();
      });
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    });

    it('Escape cancela sin mandar nada, y el foco vuelve al botón de la fila', async () => {
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Contar la línea 1' }));
      await usuario.type(screen.getByRole('textbox', { name: /^Contado en la línea 1/ }), '7');
      await usuario.keyboard('{Escape}');

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Contar la línea 1' })).toHaveFocus();
      });
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      expect(servidorDelRecuento.conteos).toEqual([]);
    });
  });

  describe('confirmar', () => {
    it('se pregunta antes, con el foco en Cancelar; Escape cierra sin mandar nada', async () => {
      montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Confirmar el recuento' }));
      const pregunta = screen.getByRole('group', { name: /^Al confirmarlo se congela el teórico/ });
      await waitFor(() => {
        expect(within(pregunta).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
      });
      await usuario.keyboard('{Escape}');

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Confirmar el recuento' })).toHaveFocus();
      });
      expect(servidorDelRecuento.confirmaciones).toEqual([]);
    });

    it('con líneas sin contar, el rechazo lleva a las que faltan', async () => {
      const { enrutador } = montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Confirmar el recuento' }));
      await usuario.click(screen.getByRole('button', { name: 'Sí, confirmar' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(/^Quedan líneas sin contar/);
      await waitFor(() => {
        expect(filas().map((fila) => fila[0])).toEqual(['1', '3']);
      });
      expect(new URLSearchParams(enrutador.state.location.search).get('solo')).toBe('sin-contar');
      expect(screen.getByRole('link', { name: 'Sin contar' })).toHaveAttribute(
        'aria-current',
        'page',
      );
    });

    it('si el stock se mueve, lleva a lo que cambió; la segunda manda la huella nueva con otra clave; y anular lo deshace', async () => {
      servidorDelRecuento.lineas = todasContadas();
      const { enrutador } = montar();
      const usuario = userEvent.setup();

      await usuario.click(await screen.findByRole('button', { name: 'Confirmar el recuento' }));
      // Entre que se leyó la ficha y se confirma, alguien recibe dos tuercas del lote.
      moverElStock(2, 7);
      await usuario.click(screen.getByRole('button', { name: 'Sí, confirmar' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(/^El almacén ha cambiado/);
      expect(new URLSearchParams(enrutador.state.location.search).get('solo')).toBe(
        'teorico-cambiado',
      );
      await waitFor(() => {
        expect(filas()).toEqual([
          [
            '2',
            'TUE-M6 · Tuerca M6 zincada',
            'A-01 · Pasillo A',
            'L-2026-07',
            'UD',
            '7Al contar era 5 (+2)',
            '2',
            '4',
            '-3',
            'Corregir',
          ],
        ]);
      });

      // La ficha se ha leído otra vez: la segunda vez va con la huella nueva y estrena clave.
      const confirmar = screen.getByRole('button', { name: 'Confirmar el recuento' });
      await waitFor(() => {
        expect(confirmar).toBeEnabled();
      });
      await usuario.click(confirmar);
      await usuario.click(screen.getByRole('button', { name: 'Sí, confirmar' }));

      expect(await screen.findByText('Recuento confirmado con el número 12.')).toBeInTheDocument();
      const [primera, segunda] = servidorDelRecuento.confirmaciones;
      expect(primera).toMatchObject({ ifMatch: '"1"', cuerpo: { huellaDelTeorico: HUELLA } });
      expect(segunda).toMatchObject({
        ifMatch: '"1"',
        cuerpo: { huellaDelTeorico: 'b'.repeat(64) },
      });
      expect(primera?.clave).toMatch(/^[0-9a-f-]{36}$/);
      expect(segunda?.clave).not.toBe(primera?.clave);

      // Cerrado, enseña todas sus líneas, y la cabecera dice el número y el ajuste.
      expect(
        await screen.findByRole('heading', { level: 2, name: 'Recuento 12' }),
      ).toBeInTheDocument();
      await waitFor(() => {
        expect(filas()).toHaveLength(3);
      });
      expect(enrutador.state.location.search).toBe('');
      expect(datos()).toMatchObject({
        Estado: 'Confirmado',
        'Confirmado el': '6 nov 2026',
        Ajuste: 'Movió la diferencia',
      });

      await usuario.click(screen.getByRole('button', { name: 'Anular el recuento' }));
      const motivo = screen.getByRole('textbox', { name: 'Motivo' });
      await waitFor(() => {
        expect(motivo).toHaveFocus();
      });
      await usuario.type(motivo, 'Se contó el almacén equivocado');
      await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));

      expect(await screen.findByText('Recuento anulado.')).toBeInTheDocument();
      expect(servidorDelRecuento.anulaciones).toEqual([
        {
          ifMatch: '"2"',
          clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown,
          cuerpo: { motivo: 'Se contó el almacén equivocado' },
        },
      ]);
      await waitFor(() => {
        expect(datos()).toMatchObject({
          Estado: 'Anulado',
          'Motivo de la anulación': 'Se contó el almacén equivocado',
        });
      });
      expect(screen.queryByRole('button', { name: 'Anular el recuento' })).not.toBeInTheDocument();
    });
  });

  it('descartar pide el motivo y lo manda con la versión; descartado, no se cuenta nada', async () => {
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Descartar el recuento' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));
    const motivo = screen.getByRole('textbox', { name: 'Motivo' });
    expect(motivo).toHaveAccessibleDescription(/^Escribe el motivo, en 300 caracteres o menos/);
    expect(servidorDelRecuento.descartes).toEqual([]);

    await usuario.type(motivo, 'Se abrió por error');
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));

    expect(await screen.findByText('Recuento descartado.')).toBeInTheDocument();
    expect(servidorDelRecuento.descartes).toEqual([
      {
        ifMatch: '"1"',
        clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown,
        cuerpo: { motivo: 'Se abrió por error' },
      },
    ]);
    await waitFor(() => {
      expect(datos()).toMatchObject({
        Estado: 'Descartado',
        'Motivo del descarte': 'Se abrió por error',
      });
    });
    expect(screen.getByText('Se descartó sin mover nada.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Contar/ })).not.toBeInTheDocument();
  });

  it('quien solo ve lee la ficha entera y no encuentra ni un botón', async () => {
    montar('es', PUEDE_VER);

    await waitFor(() => {
      expect(filas()[0]).toEqual(LINEA_1_SIN_CONTAR.slice(0, -1));
    });
    expect(screen.queryByRole('columnheader', { name: 'Acciones' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /Contar|Confirmar|Anular|Descartar/ }),
    ).not.toBeInTheDocument();
  });

  it('un estado que esta versión no conoce sale marcado, y dice por qué', async () => {
    servidorDelRecuento.estado = 'Revisado';
    montar();

    await waitFor(() => {
      expect(datos()['Estado']).toBe('Sin reconocer');
    });
    expect(screen.getAllByRole('definition')[0]).toHaveAccessibleDescription(
      'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
    );
  });

  it('si el recuento no existe, lo dice y deja volver al listado', async () => {
    montar('es', PUEDE_TODO, '/recuentos/7e7e7e7e-0000-0000-0000-000000000404');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ese recuento ya no existe. Vuelve al listado y actualiza.',
    );
    expect(screen.getByRole('link', { name: 'Volver a los recuentos' })).toHaveAttribute(
      'href',
      '/recuentos',
    );
  });

  it('en inglés, la ficha entera', async () => {
    montar('en');

    expect(
      await screen.findByRole('heading', {
        level: 2,
        name: 'Stock count of ALM-ALFA on Nov 2, 2026',
      }),
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(filas('Stock count lines')[0]).toEqual([
        '1',
        'TOR-M6 · Tornillo M6 zincado',
        'A-01 · Pasillo A',
        '',
        'UD',
        '10',
        '0',
        'Not counted',
        '',
        'Count',
      ]);
    });
    expect(screen.getByRole('button', { name: 'Confirm the stock count' })).toBeInTheDocument();
  });
});
