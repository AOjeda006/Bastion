import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PaginaDeLaNuevaTransferencia } from './PaginaDeLaNuevaTransferencia.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import {
  ALFA,
  HOY_SIMULADO,
  NAVE_DEL_NORTE,
  PERMISOS_DE_LECTURA,
  almacenesDe,
} from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type AlmacenDto = components['schemas']['AlmacenDto'];
type ArticuloDto = components['schemas']['ArticuloDto'];
type SerieDto = components['schemas']['SerieDto'];
type TransferenciaDto = components['schemas']['TransferenciaDto'];
type UbicacionDto = components['schemas']['UbicacionDto'];
type UnidadMedidaDto = components['schemas']['UnidadMedidaDto'];

const PUEDE_ABRIR = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.transferenciaVer,
  PERMISOS.transferenciaAbrir,
  PERMISOS.serieVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
];

/** Las dos naves de Alfa, y un muelle, que es el tercero: el que se elige al cambiar de idea. */
const CENTRAL = almacenesDe(ALFA.id).elementos[0]!;
const MUELLE: AlmacenDto = {
  ...CENTRAL,
  id: 'aaaaaaa1-0000-0000-0000-000000000009',
  codigo: 'ALM-MUE',
  nombre: 'Muelle',
};

const CENTRAL_LARGO = 'ALM-ALFA · Nave central de Alfa';
const NORTE_LARGO = 'ALM-NORTE · Nave del norte';
const MUELLE_LARGO = 'ALM-MUE · Muelle';

function ubicacion(
  n: number,
  almacenId: string,
  codigo: string,
  descripcion: string,
): UbicacionDto {
  return {
    id: `abab0000-0000-0000-0000-${String(n).padStart(12, '0')}`,
    empresaId: ALFA.id,
    almacenId,
    codigo,
    pasillo: null,
    estante: null,
    hueco: null,
    descripcion,
  };
}

const A01 = ubicacion(1, CENTRAL.id, 'A-01', 'Pasillo A');
const A02 = ubicacion(2, CENTRAL.id, 'A-02', 'Pasillo A, al fondo');
const N01 = ubicacion(3, NAVE_DEL_NORTE.id, 'N-01', 'Nave norte, entrada');
const M01 = ubicacion(4, MUELLE.id, 'M-01', 'Muelle de carga');

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

function articulo(
  n: number,
  codigo: string,
  descripcion: string,
  trazabilidad: string,
): ArticuloDto {
  return {
    id: `fffffff1-0000-0000-0000-${String(n).padStart(12, '0')}`,
    empresaId: ALFA.id,
    codigo,
    descripcion,
    tipo: 'Bien',
    trazabilidad,
    unidadBaseId: UD.id,
    impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
    categoriaId: null,
  };
}

/** Uno sin seguir y uno por lote: el que abre su celda. */
const TORNILLO = articulo(1, 'TOR-M6', 'Tornillo M6 zincado', 'Ninguna');
const PINTURA = articulo(4, 'PIN-RJ', 'Pintura roja', 'PorLote');
const ARTICULOS = [TORNILLO, PINTURA];

function serie(n: number, tipoDeDocumento: string, codigo: string, estado: string): SerieDto {
  return {
    id: `5e5e5e5e-0000-0000-0000-${String(n).padStart(12, '0')}`,
    empresaId: ALFA.id,
    ejercicioId: 'e1e1e1e1-0000-0000-0000-000000000001',
    tipoDeDocumento,
    codigo,
    formato: '{SERIE}-{NUMERO}',
    contador: 0,
    estado,
  };
}

/** La de transferencias activa, que es la única que se ofrece. */
const TRA = serie(4, 'TransferenciaDeInventario', 'TRA', 'Activa');
const SERIES = [
  serie(2, 'AjusteDeInventario', 'AJU', 'Activa'),
  TRA,
  serie(5, 'TransferenciaDeInventario', 'TRA-VIEJA', 'Cerrada'),
];

const NUEVA = '7a7a7a7a-0000-0000-0000-000000000099';

/** Lo que ha llegado a cada alta, y con qué contesta el servidor la siguiente. */
const servidorDelAlta = {
  altas: [] as { clave: string | null; cuerpo: Record<string, unknown> }[],
  rechazo: null as { estado: number; codigo: string; aparte?: Record<string, unknown> } | null,
};

beforeEach(() => {
  // Solo la fecha: la espera del buscador es un `setTimeout` de verdad.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));

  servidorDelAlta.altas = [];
  servidorDelAlta.rechazo = null;

  servidor.use(
    http.get('/api/v1/organizacion/almacenes', () =>
      HttpResponse.json({
        elementos: [CENTRAL, NAVE_DEL_NORTE, MUELLE],
        pagina: 1,
        tamanio: 200,
        total: 3,
      }),
    ),
    http.get('/api/v1/organizacion/series', () =>
      HttpResponse.json({ elementos: SERIES, pagina: 1, tamanio: 200, total: SERIES.length }),
    ),
    http.get('/api/v1/organizacion/ubicaciones', () =>
      HttpResponse.json({ elementos: [A01, A02, N01, M01], pagina: 1, tamanio: 200, total: 4 }),
    ),
    http.get('/api/v1/organizacion/unidades-de-medida', () =>
      HttpResponse.json({ elementos: [UD, CAJA], pagina: 1, tamanio: 200, total: 2 }),
    ),
    http.get('/api/v1/catalogo/articulos', ({ request }) => {
      const q = new URL(request.url).searchParams.get('q') ?? '';
      const casan = ARTICULOS.filter((a) =>
        `${a.codigo} ${a.descripcion}`.toLocaleLowerCase('es').includes(q.toLocaleLowerCase('es')),
      );

      return HttpResponse.json({ elementos: casan, pagina: 1, tamanio: 10, total: casan.length });
    }),
    http.post('/api/v1/inventario/transferencias', async ({ request }) => {
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDelAlta.altas.push({ clave: request.headers.get('Idempotency-Key'), cuerpo });

      if (servidorDelAlta.rechazo !== null) {
        const { estado, codigo, aparte } = servidorDelAlta.rechazo;
        return HttpResponse.json(
          { type: `/errors/${codigo}`, status: estado, ...aparte },
          { status: estado },
        );
      }

      const abierta: TransferenciaDto = {
        id: NUEVA,
        serieId: String(cuerpo['serieId']),
        numero: null,
        almacenOrigenId: String(cuerpo['almacenOrigenId']),
        almacenDestinoId: String(cuerpo['almacenDestinoId']),
        fechaDeEnvio: String(cuerpo['fechaDeEnvio']),
        fechaDeRecepcion: null,
        estado: 'Borrador',
        lineas: (cuerpo['lineas'] as unknown[]).length,
        anulaAId: null,
        motivo: null,
        divisa: 'EUR',
      };

      return HttpResponse.json(abierta, {
        status: 201,
        headers: { Location: `/api/v1/inventario/transferencias/${NUEVA}` },
      });
    }),
  );
});

afterEach(() => {
  vi.useRealTimers();
});

function montar(permisos: readonly string[] = PUEDE_ABRIR): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(
    <PaginaDeLaNuevaTransferencia />,
    '/transferencias/nueva',
    'es',
    undefined,
    [{ path: '/transferencias/:id', element: <p>La ficha de la transferencia abierta</p> }],
  );
}

const ORIGEN = 'Ubicación de origen';
const DESTINO = 'Ubicación de destino';

/** Las columnas que se eligen de una lista; el resto se escriben. */
const DESPLEGABLES: ReadonlySet<string> = new Set([ORIGEN, DESTINO, 'Artículo', 'Unidad']);

/** Una celda por su nombre entero, que es el que oye un lector de pantalla al entrar en ella. */
function celda(columna: string, linea: number): HTMLElement {
  return screen.getByRole(DESPLEGABLES.has(columna) ? 'combobox' : 'textbox', {
    name: `${columna} de la línea ${String(linea)}`,
  });
}

function lista(nombre: 'Origen' | 'Destino' | 'Serie'): HTMLElement {
  return screen.getByRole('combobox', { name: nombre });
}

/** Lo que ofrece un desplegable, en su orden. */
function opciones(elemento: HTMLElement): string[] {
  return within(elemento)
    .getAllByRole('option')
    .map((opcion) => opcion.textContent);
}

/** Espera a que lleguen las opciones de la cabecera. */
async function cargada(): Promise<void> {
  await screen.findByRole('option', { name: 'TRA' });
  await within(lista('Destino')).findByRole('option', { name: NORTE_LARGO });
}

/** La cabecera: de la central al norte, con la serie de transferencias. El día es hoy. */
async function escribirLaCabecera(usuario: UserEvent): Promise<void> {
  await cargada();
  await usuario.selectOptions(lista('Origen'), CENTRAL_LARGO);
  await usuario.selectOptions(lista('Destino'), NORTE_LARGO);
  await usuario.selectOptions(lista('Serie'), 'TRA');
}

/** Elige el artículo de una línea por lo que se escribe, con la opción que se nombra. */
async function elegirArticulo(
  usuario: UserEvent,
  linea: number,
  escrito: string,
  opcion: string,
): Promise<void> {
  await usuario.type(celda('Artículo', linea), escrito);
  await usuario.click(await screen.findByRole('option', { name: opcion }));
}

/** Una línea entera: de dónde sale, a dónde entra, el artículo y la cantidad. */
async function escribirLinea(
  usuario: UserEvent,
  linea: number,
  {
    sale,
    entra,
    escrito,
    opcion,
    cantidad,
  }: Record<'sale' | 'entra' | 'escrito' | 'opcion' | 'cantidad', string>,
): Promise<void> {
  await usuario.selectOptions(celda(ORIGEN, linea), sale);
  await usuario.selectOptions(celda(DESTINO, linea), entra);
  await elegirArticulo(usuario, linea, escrito, opcion);
  await usuario.type(celda('Cantidad', linea), cantidad);
}

const DEL_TORNILLO = {
  sale: 'A-01 · Pasillo A',
  entra: 'N-01 · Nave norte, entrada',
  escrito: 'TOR-M6',
  opcion: 'TOR-M6 · Tornillo M6 zincado',
  cantidad: '5',
};

const ABRIR = { name: 'Abrir la transferencia' };

// Con plazo propio, como el alta del ajuste: dos líneas enteras son unas cuarenta acciones de
// user-event y dos esperas del buscador.
describe('El alta de una transferencia', { timeout: 15_000 }, () => {
  it('abre un borrador con dos líneas, cada una de un hueco del origen a uno del destino', async () => {
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    // Solo la serie de transferencias que está activa.
    expect(opciones(lista('Serie'))).toEqual(['Sin elegir', 'TRA']);
    await escribirLinea(usuario, 1, { ...DEL_TORNILLO, cantidad: '1,5' });
    // Elegir el artículo pone su unidad base; se puede cambiar por otra.
    expect(celda('Unidad', 1)).toHaveValue(UD.id);
    await usuario.selectOptions(celda('Unidad', 1), 'CAJA · Caja de 100');

    // Intro en la última celda que se puede usar añade la línea, con el foco en su origen. El lote
    // y la serie no cuentan: el tornillo no se sigue así.
    expect(celda('Lote', 1)).toBeDisabled();
    expect(celda('Número de serie', 1)).toBeDisabled();
    await usuario.keyboard('{Enter}');
    await waitFor(() => {
      expect(celda(ORIGEN, 2)).toHaveFocus();
    });

    await escribirLinea(usuario, 2, {
      sale: 'A-02 · Pasillo A, al fondo',
      entra: 'N-01 · Nave norte, entrada',
      escrito: 'pintura',
      opcion: 'PIN-RJ · Pintura roja',
      cantidad: '2',
    });
    // El lote se escribe a mano (respuesta 2 de la puerta), y solo si el artículo lo lleva.
    expect(celda('Lote', 2)).toBeEnabled();
    expect(celda('Número de serie', 2)).toBeDisabled();
    await usuario.type(celda('Lote', 2), 'L-07');
    await usuario.click(screen.getByRole('button', ABRIR));

    expect(await screen.findByText('La ficha de la transferencia abierta')).toBeInTheDocument();
    expect(enrutador.state.location.pathname).toBe(`/transferencias/${NUEVA}`);
    expect(servidorDelAlta.altas).toHaveLength(1);
    // Sin factor: lo pone el servidor (ADR-0068 §2). Lo vacío, nulo, y las cifras con punto.
    expect(servidorDelAlta.altas[0]?.cuerpo).toEqual({
      serieId: TRA.id,
      almacenOrigenId: CENTRAL.id,
      almacenDestinoId: NAVE_DEL_NORTE.id,
      fechaDeEnvio: HOY_SIMULADO,
      lineas: [
        {
          ubicacionOrigenId: A01.id,
          ubicacionDestinoId: N01.id,
          articuloId: TORNILLO.id,
          cantidadIntroducida: '1.5',
          unidadIntroducidaId: CAJA.id,
          codigoDeLote: null,
          numeroDeSerie: null,
        },
        {
          ubicacionOrigenId: A02.id,
          ubicacionDestinoId: N01.id,
          articuloId: PINTURA.id,
          cantidadIntroducida: '2',
          unidadIntroducidaId: UD.id,
          codigoDeLote: 'L-07',
          numeroDeSerie: null,
        },
      ],
    });
    // La clave de idempotencia es obligatoria (ADR-0068 §7).
    expect(servidorDelAlta.altas[0]?.clave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('el mismo almacén en las dos puntas se dice en el destino, y no se manda nada', async () => {
    montar();
    const usuario = userEvent.setup();

    await cargada();
    await usuario.selectOptions(lista('Origen'), CENTRAL_LARGO);
    await usuario.selectOptions(lista('Destino'), CENTRAL_LARGO);
    await usuario.click(screen.getByRole('button', ABRIR));

    await waitFor(() => {
      expect(lista('Destino')).toHaveFocus();
    });
    expect(lista('Destino')).toHaveAccessibleDescription(
      /^El origen y el destino son el mismo almacén/,
    );
    expect(lista('Origen')).toHaveAttribute('aria-invalid', 'false');
    // Aunque falte lo demás: lo que falta se dice a la vez, cada cosa en su campo.
    expect(lista('Serie')).toHaveAccessibleDescription('Elige una serie.');
    expect(servidorDelAlta.altas).toEqual([]);
  });

  it('cada punta ofrece los huecos de su almacén, y cambiarlo solo vacía los de esa punta', async () => {
    montar();
    const usuario = userEvent.setup();

    await cargada();
    expect(opciones(celda(ORIGEN, 1))).toEqual(['Elige antes el almacén']);
    expect(opciones(celda(DESTINO, 1))).toEqual(['Elige antes el almacén']);

    await usuario.selectOptions(lista('Origen'), CENTRAL_LARGO);
    await usuario.selectOptions(lista('Destino'), NORTE_LARGO);
    expect(opciones(celda(ORIGEN, 1))).toEqual([
      'Sin elegir',
      'A-01 · Pasillo A',
      'A-02 · Pasillo A, al fondo',
    ]);
    expect(opciones(celda(DESTINO, 1))).toEqual(['Sin elegir', 'N-01 · Nave norte, entrada']);
    await usuario.selectOptions(celda(ORIGEN, 1), 'A-02 · Pasillo A, al fondo');
    await usuario.selectOptions(celda(DESTINO, 1), 'N-01 · Nave norte, entrada');

    await usuario.selectOptions(lista('Origen'), MUELLE_LARGO);
    expect(opciones(celda(ORIGEN, 1))).toEqual(['Sin elegir', 'M-01 · Muelle de carga']);
    expect(celda(ORIGEN, 1)).toHaveValue('');
    expect(celda(DESTINO, 1)).toHaveValue(N01.id);

    // Lo que cuenta es lo que se manda, no lo que pinta el desplegable: sin su opción, el navegador
    // enseña la primera aunque el formulario guarde todavía el hueco del otro almacén.
    await usuario.selectOptions(lista('Serie'), 'TRA');
    await elegirArticulo(usuario, 1, 'TOR-M6', 'TOR-M6 · Tornillo M6 zincado');
    await usuario.type(celda('Cantidad', 1), '1');
    await usuario.click(screen.getByRole('button', ABRIR));

    await waitFor(() => {
      expect(celda(ORIGEN, 1)).toHaveFocus();
    });
    expect(celda(ORIGEN, 1)).toHaveAccessibleDescription(/Elige la ubicación\./);
    expect(celda(DESTINO, 1)).toHaveAttribute('aria-invalid', 'false');
    expect(servidorDelAlta.altas).toEqual([]);
  });

  it.each([
    { punta: 'Origen', columna: ORIGEN, otra: DESTINO, campo: 'ubicacionOrigenId' },
    { punta: 'Destino', columna: DESTINO, otra: ORIGEN, campo: 'ubicacionDestinoId' },
  ])(
    'la ubicación bloqueada en el $punta va a la celda de esa punta, con el foco',
    async ({ punta, columna, otra, campo }) => {
      servidorDelAlta.rechazo = {
        estado: 409,
        codigo: 'transferencia-ubicacion-bloqueada',
        aparte: { actual: { linea: 1, punta } },
      };
      montar();
      const usuario = userEvent.setup();

      await escribirLaCabecera(usuario);
      await escribirLinea(usuario, 1, DEL_TORNILLO);
      await usuario.click(screen.getByRole('button', ABRIR));

      await waitFor(() => {
        expect(celda(columna, 1)).toHaveFocus();
      });
      expect(celda(columna, 1)).toHaveAccessibleDescription(/^Esa ubicación está bloqueada/);
      expect(celda(otra, 1)).toHaveAttribute('aria-invalid', 'false');
      // Es de una línea: arriba no se dice nada, y el único aviso es el de la celda.
      expect(screen.getAllByRole('alert').map((aviso) => aviso.id)).toEqual([
        `linea-0-${campo}-error`,
      ]);

      // Lo mismo otra vez es la misma operación, con la misma clave.
      await usuario.click(screen.getByRole('button', ABRIR));
      await waitFor(() => {
        expect(servidorDelAlta.altas).toHaveLength(2);
      });
      expect(servidorDelAlta.altas[1]?.clave).toBe(servidorDelAlta.altas[0]?.clave);
    },
  );

  it('un 400 dice la punta en la clave de `errors`, y su error va en esa celda', async () => {
    servidorDelAlta.rechazo = {
      estado: 400,
      codigo: 'transferencia-ubicacion-no-encontrada',
      aparte: { errors: { 'lineas[0].ubicacionDestinoId': ['No es de ese almacén.'] } },
    };
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, DEL_TORNILLO);
    await usuario.click(screen.getByRole('button', ABRIR));

    await waitFor(() => {
      expect(celda(DESTINO, 1)).toHaveFocus();
    });
    expect(celda(DESTINO, 1)).toHaveAccessibleDescription(
      /^Esa ubicación no existe en ese almacén/,
    );
  });

  it('lo que no es de una línea va arriba del formulario', async () => {
    servidorDelAlta.rechazo = { estado: 422, codigo: 'transferencia-almacen-bloqueado' };
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, DEL_TORNILLO);
    await usuario.click(screen.getByRole('button', ABRIR));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/^Ese almacén está bloqueado/);
    // Arriba, en el formulario y no en un campo, y ninguno se marca.
    expect(aviso.parentElement).toBe(screen.getByRole('form', { name: 'Nueva transferencia' }));
    expect(document.querySelectorAll('[aria-invalid="true"]')).toHaveLength(0);
  });

  it('sin poder ver los maestros que ofrece, lo dice y no enseña el formulario', async () => {
    montar(PUEDE_ABRIR.filter((permiso) => permiso !== PERMISOS.ubicacionVer));

    expect(
      await screen.findByText(/^Para abrir una transferencia hace falta poder ver los almacenes/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('form')).toBeNull();
  });
});
