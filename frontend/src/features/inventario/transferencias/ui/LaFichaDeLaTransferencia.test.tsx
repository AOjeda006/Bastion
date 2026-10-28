import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PaginaDeLaTransferencia } from './PaginaDeLaTransferencia.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import {
  ALFA,
  HOY_SIMULADO,
  NAVE_DEL_NORTE,
  PERMISOS_DE_LECTURA,
  almacenesDe,
  articulosDe,
} from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { capturarExportaciones, exportarYComprobar } from '@/pruebas/exportaciones.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type ArticuloDto = components['schemas']['ArticuloDto'];
type FichaDeTransferenciaDto = components['schemas']['FichaDeTransferenciaDto'];
type LineaDeFichaDeTransferenciaDto = components['schemas']['LineaDeFichaDeTransferenciaDto'];
type TransferenciaDto = components['schemas']['TransferenciaDto'];
type UbicacionDto = components['schemas']['UbicacionDto'];
type UnidadMedidaDto = components['schemas']['UnidadMedidaDto'];

const PATRON = '/transferencias/:id';

const PUEDE_VER = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.transferenciaVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
];

const PUEDE_TODO = [
  ...PUEDE_VER,
  PERMISOS.transferenciaEnviar,
  PERMISOS.transferenciaRecibir,
  PERMISOS.transferenciaAnular,
  PERMISOS.transferenciaDescartar,
];

/** Las dos naves de Alfa, y sus dos artículos: TOR-M6 y TUE-M6. */
const CENTRAL = almacenesDe(ALFA.id).elementos[0]!;
const NAVES = [CENTRAL, NAVE_DEL_NORTE];
const [TORNILLO, TUERCA] = articulosDe(ALFA.id).elementos as [ArticuloDto, ArticuloDto];

const UD: UnidadMedidaDto = {
  id: 'aaaa0001-0000-0000-0000-000000000001',
  codigo: 'UD',
  nombre: 'Unidad',
  decimales: 0,
  retirada: false,
};
const CAJA: UnidadMedidaDto = {
  id: 'aaaa0001-0000-0000-0000-000000000004',
  codigo: 'CAJA',
  nombre: 'Caja de 100',
  decimales: 0,
  retirada: false,
};

/** Un hueco en cada nave: de donde sale y adonde llega. */
const EN_LA_CENTRAL: UbicacionDto = {
  id: 'abab0000-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  almacenId: CENTRAL.id,
  codigo: 'A-01',
  pasillo: 'A',
  estante: '1',
  hueco: null,
  descripcion: 'Pasillo A',
};
const EN_EL_NORTE: UbicacionDto = {
  id: 'abab0000-0000-0000-0000-000000000002',
  empresaId: ALFA.id,
  almacenId: NAVE_DEL_NORTE.id,
  codigo: 'N-01',
  pasillo: 'N',
  estante: '1',
  hueco: null,
  descripcion: 'Muelle del norte',
};

const TRANSFERENCIA = '7a7a7a7a-0000-0000-0000-000000000015';
const INVERSA = '7a7a7a7a-0000-0000-0000-000000000017';

/** Sale tres días antes de hoy, que es el 11 de septiembre. */
const ENVIO = '2026-09-08';

/** Una línea en la unidad base, y otra en cajas, con su lote. */
function lasDeSiempre(): LineaDeFichaDeTransferenciaDto[] {
  return [
    {
      numero: 1,
      ubicacionOrigenId: EN_LA_CENTRAL.id,
      ubicacionDestinoId: EN_EL_NORTE.id,
      articuloId: TORNILLO.id,
      cantidadIntroducida: 5,
      unidadIntroducidaId: UD.id,
      factorAUnidadBase: 1,
      cantidadEnUnidadBase: 5,
      codigoDeLote: null,
      numeroDeSerie: null,
      valor: null,
    },
    {
      numero: 2,
      ubicacionOrigenId: EN_LA_CENTRAL.id,
      ubicacionDestinoId: EN_EL_NORTE.id,
      articuloId: TUERCA.id,
      cantidadIntroducida: 2,
      unidadIntroducidaId: CAJA.id,
      // Lo pone el servidor, con la conversión del día (ADR-0068 §2).
      factorAUnidadBase: 100,
      cantidadEnUnidadBase: 200,
      codigoDeLote: 'L-24',
      numeroDeSerie: null,
      valor: null,
    },
  ];
}

interface Escritura {
  ifMatch: string | null;
  clave: string | null;
  cuerpo: unknown;
}

/**
 * Las transferencias del lado del servidor y lo que ha llegado a cada acción.
 *
 * Responde como la API: la ficha lleva su `ETag`; enviar, recibir y anular, sin `If-Match` y con su
 * clave (ADR-0068 §7); descartar compara el `If-Match`, y una descartada deja de existir: `404`.
 */
const servidorDeLaTransferencia = {
  fichas: new Map<string, FichaDeTransferenciaDto>(),
  version: 3,
  rechazo: null as { estado: number; codigo: string } | null,
  lecturas: [] as string[],
  envios: [] as Escritura[],
  recepciones: [] as Escritura[],
  anulaciones: [] as Escritura[],
  descartes: [] as Escritura[],
};

function ficha(cambios: Partial<FichaDeTransferenciaDto> = {}): FichaDeTransferenciaDto {
  return {
    id: TRANSFERENCIA,
    serieId: '5e5e5e5e-0000-0000-0000-000000000003',
    numero: null,
    almacenOrigenId: CENTRAL.id,
    almacenDestinoId: NAVE_DEL_NORTE.id,
    fechaDeEnvio: ENVIO,
    fechaDeRecepcion: null,
    estado: 'Borrador',
    divisa: 'EUR',
    anulaAId: null,
    motivo: null,
    lineas: lasDeSiempre(),
    ...cambios,
  };
}

/** Ya enviada: con su número y el valor que viaja en cada línea. */
function enviada(cambios: Partial<FichaDeTransferenciaDto> = {}): FichaDeTransferenciaDto {
  const [unidades, cajas] = lasDeSiempre() as [
    LineaDeFichaDeTransferenciaDto,
    LineaDeFichaDeTransferenciaDto,
  ];

  return ficha({
    numero: 16,
    estado: 'Enviada',
    lineas: [
      { ...unidades, valor: 12.5 },
      { ...cajas, valor: 90 },
    ],
    ...cambios,
  });
}

function resumen(dto: FichaDeTransferenciaDto): TransferenciaDto {
  return {
    id: dto.id,
    serieId: dto.serieId,
    numero: dto.numero,
    almacenOrigenId: dto.almacenOrigenId,
    almacenDestinoId: dto.almacenDestinoId,
    fechaDeEnvio: dto.fechaDeEnvio,
    fechaDeRecepcion: dto.fechaDeRecepcion,
    estado: dto.estado,
    lineas: dto.lineas.length,
    anulaAId: dto.anulaAId,
    motivo: dto.motivo,
    divisa: dto.divisa,
  };
}

function rechazo(estado: number, codigo: string): Response {
  return HttpResponse.json({ type: `/errors/${codigo}`, status: estado }, { status: estado });
}

async function escritura(request: Request): Promise<Escritura> {
  const texto = await request.text();

  return {
    ifMatch: request.headers.get('If-Match'),
    clave: request.headers.get('Idempotency-Key'),
    cuerpo: texto === '' ? null : (JSON.parse(texto) as unknown),
  };
}

beforeEach(() => {
  // Solo la fecha: hoy es el día con el que empieza la recepción, y su tope.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));

  Object.assign(servidorDeLaTransferencia, {
    fichas: new Map([[TRANSFERENCIA, ficha()]]),
    version: 3,
    rechazo: null,
    lecturas: [],
    envios: [],
    recepciones: [],
    anulaciones: [],
    descartes: [],
  });

  const s = servidorDeLaTransferencia;
  const base = '/api/v1/inventario/transferencias/:id';

  /** La transición de la original, si el servidor no la rechaza. */
  const transitar = (cambios: Partial<FichaDeTransferenciaDto>): Response => {
    if (s.rechazo !== null) {
      return rechazo(s.rechazo.estado, s.rechazo.codigo);
    }

    const hecha = { ...s.fichas.get(TRANSFERENCIA)!, ...cambios };
    s.fichas.set(TRANSFERENCIA, hecha);
    s.version += 1;

    return HttpResponse.json(resumen(hecha));
  };

  servidor.use(
    http.get(base, ({ params }) => {
      const id = String(params['id']);
      const una = s.fichas.get(id);
      s.lecturas.push(id);

      return una === undefined
        ? rechazo(404, 'transferencia-no-encontrada')
        : HttpResponse.json(una, { headers: { ETag: `"${String(s.version)}"` } });
    }),

    http.post(`${base}/envio`, async ({ request }) => {
      s.envios.push(await escritura(request));

      return transitar({ numero: 16, estado: 'Enviada', lineas: enviada().lineas });
    }),

    http.post(`${base}/recepcion`, async ({ request }) => {
      const recibida = await escritura(request);
      s.recepciones.push(recibida);
      const { fechaDeRecepcion } = recibida.cuerpo as { fechaDeRecepcion: string };

      return transitar({ estado: 'Recibida', fechaDeRecepcion });
    }),

    http.post(`${base}/anulacion`, async ({ request }) => {
      const recibida = await escritura(request);
      s.anulaciones.push(recibida);

      if (s.rechazo !== null) {
        return rechazo(s.rechazo.estado, s.rechazo.codigo);
      }

      // La inversa nace recibida, con el motivo y las cantidades al revés, y la original queda
      // anulada (ADR-0068 §5).
      const original = { ...s.fichas.get(TRANSFERENCIA)!, estado: 'Anulada' };
      const inversa = ficha({
        id: INVERSA,
        numero: 17,
        estado: 'Recibida',
        fechaDeEnvio: HOY_SIMULADO,
        fechaDeRecepcion: HOY_SIMULADO,
        motivo: (recibida.cuerpo as { motivo: string }).motivo,
        anulaAId: TRANSFERENCIA,
        lineas: original.lineas.map((l) => ({
          ...l,
          cantidadIntroducida: -Number(l.cantidadIntroducida),
          cantidadEnUnidadBase: -Number(l.cantidadEnUnidadBase),
          valor: -Number(l.valor),
        })),
      });
      s.fichas.set(TRANSFERENCIA, original);
      s.fichas.set(INVERSA, inversa);
      s.version += 1;

      return HttpResponse.json({ original: resumen(original), inverso: resumen(inversa) });
    }),

    http.delete(base, async ({ request }) => {
      const recibida = await escritura(request);
      s.descartes.push(recibida);

      if (recibida.ifMatch !== `"${String(s.version)}"`) {
        return rechazo(412, 'version-obsoleta');
      }

      s.fichas.delete(TRANSFERENCIA);

      return new HttpResponse(null, { status: 204 });
    }),

    http.get('/api/v1/organizacion/almacenes/:id', ({ params }) => {
      const nave = NAVES.find((n) => n.id === params['id']);

      return nave === undefined ? new HttpResponse(null, { status: 404 }) : HttpResponse.json(nave);
    }),
    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      const articulo = [TORNILLO, TUERCA].find((a) => a.id === params['id']);

      return articulo === undefined
        ? rechazo(404, 'articulo-no-encontrado')
        : HttpResponse.json(articulo);
    }),
    http.get('/api/v1/organizacion/ubicaciones/:id', ({ params }) =>
      HttpResponse.json(params['id'] === EN_EL_NORTE.id ? EN_EL_NORTE : EN_LA_CENTRAL),
    ),
    http.get('/api/v1/organizacion/unidades-de-medida/:id', ({ params }) =>
      HttpResponse.json(params['id'] === CAJA.id ? CAJA : UD),
    ),
  );
});

afterEach(() => {
  vi.useRealTimers();
});

function montar(
  permisos: readonly string[] = PUEDE_TODO,
  ruta = `/transferencias/${TRANSFERENCIA}`,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDeLaTransferencia />, ruta, 'es', PATRON, [
    { path: '/transferencias', element: <p>El listado de transferencias</p> },
  ]);
}

function tablaDeLineas(): HTMLElement {
  return screen.getByRole('table', { name: 'Líneas de la transferencia' });
}

/** Las filas de las líneas, celda a celda, sin la cabecera ni el número, que es la de su fila. */
function filas(): string[][] {
  return within(tablaDeLineas())
    .getAllByRole('row')
    .slice(1)
    .map((fila) =>
      within(fila)
        .getAllByRole('cell')
        .map((celda) => celda.textContent),
    );
}

/** Las columnas de la tabla de líneas. */
function columnas(): string[] {
  return within(tablaDeLineas())
    .getAllByRole('columnheader')
    .map((cabecera) => cabecera.textContent);
}

/** Lo que dice la cabecera, término a término. */
function cabecera(): Record<string, string> {
  const terminos = screen.getAllByRole('term');

  return Object.fromEntries(
    terminos.map((termino) => [termino.textContent, termino.nextElementSibling?.textContent ?? '']),
  );
}

const CENTRAL_LARGO = 'ALM-ALFA · Nave central de Alfa';
const NORTE_LARGO = 'ALM-NORTE · Nave del norte';
const BORRADOR = 'Borrador de ALM-ALFA a ALM-NORTE del 8 sept 2026';
const UUID = /^[0-9a-f-]{36}$/;

describe('La ficha de una transferencia', { timeout: 15_000 }, () => {
  it('dice la cabecera y cada línea, con sus dos huecos y el factor del servidor; el borrador no lleva valor', async () => {
    montar();

    expect(await screen.findByRole('heading', { name: BORRADOR })).toBeInTheDocument();
    await waitFor(() => {
      expect(cabecera()).toEqual({
        Estado: 'Borrador',
        Origen: CENTRAL_LARGO,
        Destino: NORTE_LARGO,
        'Fecha de envío': '8 sept 2026',
      });
    });
    expect(columnas()).toEqual([
      'Nº',
      'Ubicación de origen',
      'Ubicación de destino',
      'Artículo',
      'Cantidad',
      'Factor de conversión',
      'En unidad base',
      'Lote o serie',
    ]);
    await waitFor(() => {
      expect(filas()).toEqual([
        ['A-01', 'N-01', 'TOR-M6 · Tornillo M6 zincado', '5 UD', '1', '5', ''],
        ['A-01', 'N-01', 'TUE-M6 · Tuerca M6 zincada', '2 CAJA', '100', '200', 'L-24'],
      ]);
    });
    expect(screen.getByText(/El valor se calcula al enviarla\.$/)).toBeInTheDocument();
    // Muchas columnas: la tabla corre dentro de su contenedor, y no la página.
    expect(tablaDeLineas().parentElement).toHaveClass('overflow-x-auto');
  });

  it('exportar se lleva las líneas de la ficha', async () => {
    const exportaciones = capturarExportaciones();
    montar(PUEDE_VER);

    expect(await screen.findByRole('heading', { name: BORRADOR })).toBeInTheDocument();
    await exportarYComprobar(
      exportaciones,
      `/api/v1/inventario/transferencias/${TRANSFERENCIA}/lineas/exportacion`,
    );
  });

  it('enviar se pregunta, manda su clave y no la versión, y el aviso dice el número con el foco', async () => {
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Enviar la transferencia' }));
    const pregunta = screen.getByRole('group', {
      name: 'Al enviarla recibe su número y la mercancía sale del origen.',
    });
    expect(within(pregunta).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await usuario.click(within(pregunta).getByRole('button', { name: 'Sí, enviar' }));

    const aviso = await screen.findByText('Transferencia enviada con el número 16.');
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDeLaTransferencia.envios).toEqual([
      { ifMatch: null, clave: expect.stringMatching(UUID) as unknown, cuerpo: null },
    ]);

    // La ficha se lee otra vez: ya con su número, su valor y lo que se puede hacer ahora.
    expect(await screen.findByRole('heading', { name: 'Transferencia 16' })).toBeInTheDocument();
    expect(columnas().at(-1)).toBe('Valor');
    expect(filas().map((fila) => fila.at(-1))).toEqual(['12,50 €', '90,00 €']);
    expect(screen.getByRole('button', { name: 'Recibir la transferencia' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Anular la transferencia' })).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Enviar la transferencia' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Descartar el borrador' })).not.toBeInTheDocument();
  });

  it('descartar se pregunta, manda la versión de la ficha, y el borrador deja de existir', async () => {
    montar();
    const usuario = userEvent.setup();
    const descartar = await screen.findByRole('button', { name: 'Descartar el borrador' });

    // Escape cierra la pregunta sin mandar nada, y el foco vuelve al botón.
    await usuario.click(descartar);
    expect(screen.getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await usuario.keyboard('{Escape}');
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Descartar el borrador' })).toHaveFocus();
    });
    expect(servidorDeLaTransferencia.descartes).toEqual([]);

    await usuario.click(screen.getByRole('button', { name: 'Descartar el borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));

    const aviso = await screen.findByText('Borrador descartado.');
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDeLaTransferencia.descartes).toEqual([
      { ifMatch: '"3"', clave: null, cuerpo: null },
    ]);
    // No se vuelve a pedir: contestaría un 404. Queda el camino de vuelta, y nada más.
    expect(servidorDeLaTransferencia.lecturas).toEqual([TRANSFERENCIA]);
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Volver a las transferencias' })).toHaveAttribute(
      'href',
      '/transferencias',
    );
  });

  it('volver atrás a una descartada da su 404, sin pintar antes lo que había', async () => {
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Descartar el borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));
    await usuario.click(await screen.findByRole('link', { name: 'Volver a las transferencias' }));
    expect(await screen.findByText('El listado de transferencias')).toBeInTheDocument();

    // Atrás, con el navegador. Lo que guardara la caché se pintaría al montar, antes de leer nada:
    // por eso la primera aserción va sin esperar.
    await act(async () => {
      await enrutador.navigate(-1);
    });
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Esa transferencia ya no existe. Vuelve al listado y actualiza.',
    );
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(servidorDeLaTransferencia.lecturas).toEqual([TRANSFERENCIA, TRANSFERENCIA]);
  });

  it('recibir pide el día, que empieza en hoy y no va antes del envío, y el aviso dice cuándo llegó', async () => {
    servidorDeLaTransferencia.fichas.set(TRANSFERENCIA, enviada());
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Recibir la transferencia' }));
    const formulario = screen.getByRole('form', {
      name: 'Al recibirla, la mercancía entra en el destino el día que digas.',
    });
    const dia = within(formulario).getByLabelText('Fecha de recepción');
    await waitFor(() => {
      expect(dia).toHaveFocus();
    });
    expect(dia).toHaveValue(HOY_SIMULADO);
    expect(dia).toHaveAttribute('min', ENVIO);
    expect(dia).toHaveAttribute('max', HOY_SIMULADO);

    await usuario.clear(dia);
    await usuario.type(dia, '2026-09-10');
    await usuario.click(within(formulario).getByRole('button', { name: 'Sí, recibir' }));

    const aviso = await screen.findByText('Transferencia recibida el 10 sept 2026.');
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDeLaTransferencia.recepciones).toEqual([
      {
        ifMatch: null,
        clave: expect.stringMatching(UUID) as unknown,
        cuerpo: { fechaDeRecepcion: '2026-09-10' },
      },
    ]);
    await waitFor(() => {
      expect(cabecera()).toMatchObject({
        Estado: 'Recibida',
        'Fecha de recepción': '10 sept 2026',
      });
    });
    expect(
      screen.queryByRole('button', { name: 'Recibir la transferencia' }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Anular la transferencia' })).toBeInTheDocument();
  });

  it('el día de mañana, el de antes del envío y el que rechaza el servidor van en su campo, con el foco', async () => {
    servidorDeLaTransferencia.fichas.set(TRANSFERENCIA, enviada());
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Recibir la transferencia' }));
    const dia = screen.getByLabelText('Fecha de recepción');
    const recibir = screen.getByRole('button', { name: 'Sí, recibir' });

    // Lo que el formulario ya sabe no sale: ni sin día, ni mañana, ni antes de salir.
    await usuario.clear(dia);
    await usuario.click(recibir);
    await waitFor(() => {
      expect(dia).toHaveAccessibleDescription('Escribe el día en que llega.');
    });
    await usuario.type(dia, '2026-09-12');
    await usuario.click(recibir);
    await waitFor(() => {
      expect(dia).toHaveAccessibleDescription(/^Esa fecha es posterior a hoy/);
    });
    await usuario.clear(dia);
    await usuario.type(dia, '2026-09-07');
    await usuario.click(recibir);
    await waitFor(() => {
      expect(dia).toHaveAccessibleDescription(/^La fecha de recepción es anterior a la del envío/);
    });
    expect(servidorDeLaTransferencia.recepciones).toEqual([]);

    // Lo que solo sabe el servidor, también va en el día: es lo que hay que cambiar.
    servidorDeLaTransferencia.rechazo = {
      estado: 409,
      codigo: 'transferencia-en-ejercicio-cerrado',
    };
    await usuario.clear(dia);
    await usuario.type(dia, '2026-09-10');
    await usuario.click(recibir);

    await waitFor(() => {
      expect(servidorDeLaTransferencia.recepciones).toHaveLength(1);
    });
    await waitFor(() => {
      expect(dia).toHaveFocus();
    });
    expect(dia).toHaveAttribute('aria-invalid', 'true');
    expect(dia).toHaveAccessibleDescription(/^Esa fecha cae en un ejercicio cerrado/);
    // Sigue abierto, con lo escrito, y arriba no se dice nada.
    expect(dia).toHaveValue('2026-09-10');
    expect(screen.getAllByRole('alert').map((aviso) => aviso.id)).toEqual([
      'fecha-de-la-recepcion-error',
    ]);
  });

  it('anular pide el motivo, y el aviso lleva a la inversa, que nace recibida y no se anula', async () => {
    servidorDeLaTransferencia.fichas.set(
      TRANSFERENCIA,
      enviada({ estado: 'Recibida', fechaDeRecepcion: '2026-09-09' }),
    );
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Anular la transferencia' }));
    const motivo = screen.getByRole('textbox', { name: 'Motivo' });
    await waitFor(() => {
      expect(motivo).toHaveFocus();
    });
    await usuario.type(motivo, 'Iba a la nave equivocada');
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));

    const aviso = await screen.findByText(
      /^Transferencia anulada\. La compensa la transferencia 17\./,
    );
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDeLaTransferencia.anulaciones).toEqual([
      {
        ifMatch: null,
        clave: expect.stringMatching(UUID) as unknown,
        cuerpo: { motivo: 'Iba a la nave equivocada' },
      },
    ]);
    await waitFor(() => {
      expect(cabecera()['Estado']).toBe('Anulada');
    });

    await usuario.click(within(aviso).getByRole('link', { name: 'Ver la inversa' }));

    expect(await screen.findByRole('heading', { name: 'Transferencia 17' })).toBeInTheDocument();
    expect(enrutador.state.location.pathname).toBe(`/transferencias/${INVERSA}`);
    await waitFor(() => {
      expect(cabecera()).toMatchObject({
        Estado: 'Recibida',
        'Fecha de recepción': '11 sept 2026',
        Motivo: 'Iba a la nave equivocada',
      });
    });
    // Otra ficha, con su estado: ni el aviso de la original ni un botón para anular la inversa.
    expect(screen.queryByText(/^Transferencia anulada/)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver la transferencia original' })).toHaveAttribute(
      'href',
      `/transferencias/${TRANSFERENCIA}`,
    );
    // El único botón es el de exportar sus líneas, que lee y no cambia nada (ítem 3.2).
    expect(screen.getAllByRole('button').map((boton) => boton.textContent)).toEqual([
      'Exportar a CSV',
    ]);
  });

  it('el motivo que falta o que rechaza el servidor va en su campo, con el foco', async () => {
    servidorDeLaTransferencia.fichas.set(TRANSFERENCIA, enviada());
    servidorDeLaTransferencia.rechazo = { estado: 400, codigo: 'transferencia-motivo-no-valido' };
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Anular la transferencia' }));
    const motivo = screen.getByRole('textbox', { name: 'Motivo' });

    // Vacío no sale: lo para el formulario.
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));
    await waitFor(() => {
      expect(motivo).toHaveAccessibleDescription(/^Escribe por qué anulas la transferencia/);
    });
    expect(servidorDeLaTransferencia.anulaciones).toEqual([]);

    await usuario.type(motivo, 'Iba a la nave equivocada');
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));

    await waitFor(() => {
      expect(servidorDeLaTransferencia.anulaciones).toHaveLength(1);
    });
    await waitFor(() => {
      expect(motivo).toHaveFocus();
    });
    expect(motivo).toHaveAttribute('aria-invalid', 'true');
    expect(motivo).toHaveAccessibleDescription(/^Escribe por qué anulas la transferencia/);
    expect(motivo).toHaveValue('Iba a la nave equivocada');
    expect(screen.getAllByRole('alert').map((aviso) => aviso.id)).toEqual([
      'motivo-de-la-anulacion-error',
    ]);
  });

  it('cualquier otro rechazo va arriba con el foco, y la ficha se vuelve a leer', async () => {
    servidorDeLaTransferencia.rechazo = {
      estado: 409,
      codigo: 'transferencia-no-esta-en-borrador',
    };
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Enviar la transferencia' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, enviar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/^Esa transferencia ya no está en borrador/);
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    await waitFor(() => {
      expect(servidorDeLaTransferencia.lecturas).toEqual([TRANSFERENCIA, TRANSFERENCIA]);
    });
    // La ficha sigue: lo que ya se enseñaba no se retira.
    expect(screen.getByRole('heading', { name: BORRADOR })).toBeInTheDocument();
  });

  it('quien solo ve lee la ficha entera y no encuentra más botón que el de exportar', async () => {
    servidorDeLaTransferencia.fichas.set(TRANSFERENCIA, enviada());
    montar(PUEDE_VER);

    await screen.findByRole('heading', { name: 'Transferencia 16' });
    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
    // Exportar lee, con el mismo permiso que la ficha (ítem 3.2): ninguno de los que cambian algo.
    expect(screen.getAllByRole('button').map((boton) => boton.textContent)).toEqual([
      'Exportar a CSV',
    ]);
  });
});
