import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PaginaDelAlta } from './PaginaDelAlta.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, HOY_SIMULADO, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type AjusteDto = components['schemas']['AjusteDto'];
type AlmacenDto = components['schemas']['AlmacenDto'];
type ArticuloDto = components['schemas']['ArticuloDto'];
type SerieDto = components['schemas']['SerieDto'];
type UbicacionDto = components['schemas']['UbicacionDto'];
type UnidadMedidaDto = components['schemas']['UnidadMedidaDto'];

const PUEDE_ABRIR = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.ajusteVer,
  PERMISOS.ajusteAbrir,
  PERMISOS.serieVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
];

/** La nave de Alfa, y otra que no tiene, para ver que se ofrecen las ubicaciones de la elegida. */
const NAVE = almacenesDe(ALFA.id).elementos[0]!;
const MUELLE: AlmacenDto = {
  ...NAVE,
  id: 'aaaaaaa1-0000-0000-0000-000000000009',
  codigo: 'ALM-MUE',
  nombre: 'Muelle',
};

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

const A01 = ubicacion(1, NAVE.id, 'A-01', 'Pasillo A');
const A02 = ubicacion(2, NAVE.id, 'A-02', 'Pasillo A, al fondo');
const M01 = ubicacion(3, MUELLE.id, 'M-01', 'Muelle de carga');

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

/** Uno sin seguir, dos que se buscan juntos, y uno por lote: el que abre su celda. */
const TORNILLO = articulo(1, 'TOR-M6', 'Tornillo M6 zincado', 'Ninguna');
const TUERCA = articulo(2, 'TUE-M6', 'Tuerca M6 zincada', 'Ninguna');
const TORNILLO_LARGO = articulo(3, 'TOR-M8', 'Tornillo M8 largo', 'Ninguna');
const PINTURA = articulo(4, 'PIN-RJ', 'Pintura roja', 'PorLote');
const ARTICULOS = [TORNILLO, TUERCA, TORNILLO_LARGO, PINTURA];

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

/** La de ajustes activa, que es la única que se ofrece: no la cerrada ni la de otro documento. */
const AJU = serie(2, 'AjusteDeInventario', 'AJU', 'Activa');
const SERIES = [
  serie(1, 'RecuentoDeInventario', 'REC', 'Activa'),
  AJU,
  serie(3, 'AjusteDeInventario', 'AJU-VIEJA', 'Cerrada'),
];

const NUEVO = 'adadadad-0000-0000-0000-000000000099';

/** Lo que ha llegado a cada alta, y con qué contesta el servidor la siguiente. */
const servidorDelAlta = {
  altas: [] as { clave: string | null; cuerpo: Record<string, unknown> }[],
  rechazo: null as { estado: number; codigo: string; aparte?: Record<string, unknown> } | null,
  busquedas: [] as string[],
};

beforeEach(() => {
  // Solo la fecha: la espera del buscador es un `setTimeout` de verdad.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00Z`));

  servidorDelAlta.altas = [];
  servidorDelAlta.rechazo = null;
  servidorDelAlta.busquedas = [];

  servidor.use(
    http.get('/api/v1/organizacion/almacenes', () =>
      HttpResponse.json({ elementos: [NAVE, MUELLE], pagina: 1, tamanio: 200, total: 2 }),
    ),
    http.get('/api/v1/organizacion/series', () =>
      HttpResponse.json({ elementos: SERIES, pagina: 1, tamanio: 200, total: SERIES.length }),
    ),
    http.get('/api/v1/organizacion/ubicaciones', () =>
      HttpResponse.json({ elementos: [A01, A02, M01], pagina: 1, tamanio: 200, total: 3 }),
    ),
    http.get('/api/v1/organizacion/unidades-de-medida', () =>
      HttpResponse.json({ elementos: [UD, CAJA], pagina: 1, tamanio: 200, total: 2 }),
    ),
    http.get('/api/v1/catalogo/articulos', ({ request }) => {
      const q = new URL(request.url).searchParams.get('q') ?? '';
      servidorDelAlta.busquedas.push(q);
      const casan = ARTICULOS.filter((a) =>
        `${a.codigo} ${a.descripcion}`.toLocaleLowerCase('es').includes(q.toLocaleLowerCase('es')),
      );

      return HttpResponse.json({ elementos: casan, pagina: 1, tamanio: 10, total: casan.length });
    }),
    http.post('/api/v1/inventario/ajustes', async ({ request }) => {
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDelAlta.altas.push({ clave: request.headers.get('Idempotency-Key'), cuerpo });

      if (servidorDelAlta.rechazo !== null) {
        const { estado, codigo, aparte } = servidorDelAlta.rechazo;
        return HttpResponse.json(
          { type: `/errors/${codigo}`, status: estado, ...aparte },
          { status: estado },
        );
      }

      const abierto: AjusteDto = {
        id: NUEVO,
        serieId: String(cuerpo['serieId']),
        numero: null,
        almacenId: String(cuerpo['almacenId']),
        fechaDeOperacion: String(cuerpo['fechaDeOperacion']),
        motivo: String(cuerpo['motivo']),
        estado: 'Borrador',
        lineas: (cuerpo['lineas'] as unknown[]).length,
        anulaAId: null,
        divisa: 'EUR',
      };

      return HttpResponse.json(abierto, {
        status: 201,
        headers: { Location: `/api/v1/inventario/ajustes/${NUEVO}` },
      });
    }),
  );
});

afterEach(() => {
  vi.useRealTimers();
});

function montar(permisos: readonly string[] = PUEDE_ABRIR): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDelAlta />, '/ajustes/nuevo', 'es', undefined, [
    { path: '/ajustes/:id', element: <p>La ficha del ajuste abierto</p> },
  ]);
}

/** Las columnas que se eligen de una lista; el resto se escriben. */
const DESPLEGABLES: ReadonlySet<string> = new Set(['Ubicación', 'Artículo', 'Unidad']);

/** Una celda por su nombre entero, que es el que oye un lector de pantalla al entrar en ella. */
function celda(columna: string, linea: number): HTMLElement {
  return screen.getByRole(DESPLEGABLES.has(columna) ? 'combobox' : 'textbox', {
    name: `${columna} de la línea ${String(linea)}`,
  });
}

/** La cabecera: el almacén, la serie y el motivo. El día es hoy, que es con lo que se abre. */
async function escribirLaCabecera(usuario: UserEvent): Promise<void> {
  await screen.findByRole('option', { name: 'AJU' });
  await screen.findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
  await usuario.selectOptions(
    screen.getByRole('combobox', { name: 'Almacén' }),
    'ALM-ALFA · Nave central de Alfa',
  );
  await usuario.selectOptions(screen.getByRole('combobox', { name: 'Serie' }), 'AJU');
  // Pegado y no tecleado: cada tecla repinta la rejilla, y el motivo no es lo que se prueba.
  await usuario.click(screen.getByRole('textbox', { name: 'Motivo' }));
  await usuario.paste('Rotura en el muelle');
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

/** Una línea entera: la ubicación, el artículo, la cantidad y, si se dice, el coste. */
async function escribirLinea(
  usuario: UserEvent,
  linea: number,
  {
    ubicacion,
    escrito,
    opcion,
    cantidad,
  }: Record<'ubicacion' | 'escrito' | 'opcion' | 'cantidad', string>,
): Promise<void> {
  await usuario.selectOptions(celda('Ubicación', linea), ubicacion);
  await elegirArticulo(usuario, linea, escrito, opcion);
  await usuario.type(celda('Cantidad', linea), cantidad);
}

// Con plazo propio: dos líneas enteras son unas cuarenta acciones de user-event y dos esperas del
// buscador. Medido en esta máquina, el primer caso tarda 3 s solo y 5,1 s con la suite entera, que
// son ya los 5 s de siempre.
describe('El alta de un ajuste', { timeout: 15_000 }, () => {
  it('abre un borrador con una línea en la unidad base y otra en una unidad con conversión', async () => {
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR-M6',
      opcion: 'TOR-M6 · Tornillo M6 zincado',
      cantidad: '5',
    });
    // Elegir el artículo pone su unidad base, que no necesita conversión.
    expect(celda('Unidad', 1)).toHaveValue(UD.id);
    await usuario.type(celda('Coste por unidad base', 1), '2,5');

    // Intro en la última celda que se puede usar añade la línea, con el foco en su ubicación. El
    // lote y la serie no cuentan: el tornillo no se sigue así.
    expect(celda('Lote', 1)).toBeDisabled();
    await usuario.keyboard('{Enter}');
    await waitFor(() => {
      expect(celda('Ubicación', 2)).toHaveFocus();
    });

    await escribirLinea(usuario, 2, {
      ubicacion: 'A-02 · Pasillo A, al fondo',
      escrito: 'TUE',
      opcion: 'TUE-M6 · Tuerca M6 zincada',
      cantidad: '-1',
    });
    await usuario.selectOptions(celda('Unidad', 2), 'CAJA · Caja de 100');
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    expect(await screen.findByText('La ficha del ajuste abierto')).toBeInTheDocument();
    expect(enrutador.state.location.pathname).toBe(`/ajustes/${NUEVO}`);
    expect(servidorDelAlta.altas).toHaveLength(1);
    // Sin factor: lo pone el servidor (ADR-0065 §2). Lo vacío, nulo, y las cifras con punto.
    expect(servidorDelAlta.altas[0]?.cuerpo).toEqual({
      serieId: AJU.id,
      almacenId: NAVE.id,
      fechaDeOperacion: HOY_SIMULADO,
      motivo: 'Rotura en el muelle',
      lineas: [
        {
          ubicacionId: A01.id,
          articuloId: TORNILLO.id,
          cantidadIntroducida: '5',
          unidadIntroducidaId: UD.id,
          costeUnitario: '2.5',
          codigoDeLote: null,
          numeroDeSerie: null,
        },
        {
          ubicacionId: A02.id,
          articuloId: TUERCA.id,
          cantidadIntroducida: '-1',
          unidadIntroducidaId: CAJA.id,
          costeUnitario: null,
          codigoDeLote: null,
          numeroDeSerie: null,
        },
      ],
    });
    // La clave de idempotencia es obligatoria (ADR-0065 §6).
    expect(servidorDelAlta.altas[0]?.clave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('el rechazo de una línea sin conversión va a la unidad de esa línea, con el foco', async () => {
    servidorDelAlta.rechazo = {
      estado: 422,
      codigo: 'ajuste-sin-conversion',
      aparte: { actual: { linea: 2 } },
    };
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR-M6',
      opcion: 'TOR-M6 · Tornillo M6 zincado',
      cantidad: '5',
    });
    // Con el botón, el foco va también a la línea nueva.
    await usuario.click(screen.getByRole('button', { name: 'Añadir una línea' }));
    expect(celda('Ubicación', 2)).toHaveFocus();
    await escribirLinea(usuario, 2, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TUE',
      opcion: 'TUE-M6 · Tuerca M6 zincada',
      cantidad: '3',
    });
    await usuario.selectOptions(celda('Unidad', 2), 'CAJA · Caja de 100');
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    await waitFor(() => {
      expect(celda('Unidad', 2)).toHaveFocus();
    });
    expect(celda('Unidad', 2)).toHaveAttribute('aria-invalid', 'true');
    expect(celda('Unidad', 2)).toHaveAccessibleDescription(
      /^La unidad de esa línea no tiene conversión a la unidad base del artículo/,
    );
    expect(celda('Unidad', 1)).toHaveAttribute('aria-invalid', 'false');
    // Es de una línea: arriba no se dice nada, y el único aviso es el de la celda.
    expect(screen.getAllByRole('alert').map((aviso) => aviso.id)).toEqual([
      'linea-1-unidadIntroducidaId-error',
    ]);

    // Lo mismo otra vez es la misma operación, con la misma clave; con la unidad cambiada, otra.
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));
    await waitFor(() => {
      expect(servidorDelAlta.altas).toHaveLength(2);
    });
    servidorDelAlta.rechazo = null;
    await usuario.selectOptions(celda('Unidad', 2), 'UD · Unidad');
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    expect(await screen.findByText('La ficha del ajuste abierto')).toBeInTheDocument();
    const [primera, segunda, tercera] = servidorDelAlta.altas.map((alta) => alta.clave);
    expect(segunda).toBe(primera);
    expect(tercera).not.toBe(primera);
  });

  it('un 400 dice la línea en la clave de `errors`, y su error va en ese campo', async () => {
    servidorDelAlta.rechazo = {
      estado: 400,
      codigo: 'ajuste-cantidad-no-valida',
      aparte: { errors: { 'lineas[0].cantidadIntroducida': ['No cabe.'] } },
    };
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR-M6',
      opcion: 'TOR-M6 · Tornillo M6 zincado',
      cantidad: '0,000001',
    });
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    await waitFor(() => {
      expect(celda('Cantidad', 1)).toHaveFocus();
    });
    expect(celda('Cantidad', 1)).toHaveAccessibleDescription(/^Esa cantidad no cabe en el libro/);
  });

  it('lo que no es de una línea va arriba del formulario', async () => {
    servidorDelAlta.rechazo = { estado: 422, codigo: 'ajuste-almacen-bloqueado' };
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR-M6',
      opcion: 'TOR-M6 · Tornillo M6 zincado',
      cantidad: '5',
    });
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/^Ese almacén está bloqueado/);
    // Arriba, en el formulario y no en una celda, y ningún campo se marca.
    expect(aviso.parentElement).toBe(screen.getByRole('form', { name: 'Nuevo ajuste' }));
    expect(document.querySelectorAll('[aria-invalid="true"]')).toHaveLength(0);
  });

  it('lo que el formulario ya sabe no sale: una línea que baja con coste, en el coste', async () => {
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR-M6',
      opcion: 'TOR-M6 · Tornillo M6 zincado',
      cantidad: '-2',
    });
    await usuario.type(celda('Coste por unidad base', 1), '3');
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    await waitFor(() => {
      expect(celda('Coste por unidad base', 1)).toHaveFocus();
    });
    expect(celda('Coste por unidad base', 1)).toHaveAccessibleDescription(
      /^Una línea que saca existencias no lleva coste/,
    );
    expect(servidorDelAlta.altas).toEqual([]);
  });

  it('las ubicaciones son las del almacén elegido, y cambiarlo deja sin elegir la de otro', async () => {
    montar();
    const usuario = userEvent.setup();

    await screen.findByRole('option', { name: 'ALM-ALFA · Nave central de Alfa' });
    const opciones = (): string[] =>
      within(celda('Ubicación', 1))
        .getAllByRole('option')
        .map((opcion) => opcion.textContent);

    expect(opciones()).toEqual(['Elige antes el almacén']);
    await usuario.selectOptions(
      screen.getByRole('combobox', { name: 'Almacén' }),
      'ALM-ALFA · Nave central de Alfa',
    );
    expect(opciones()).toEqual(['Sin elegir', 'A-01 · Pasillo A', 'A-02 · Pasillo A, al fondo']);
    await usuario.selectOptions(celda('Ubicación', 1), 'A-02 · Pasillo A, al fondo');

    await usuario.selectOptions(
      screen.getByRole('combobox', { name: 'Almacén' }),
      'ALM-MUE · Muelle',
    );
    expect(opciones()).toEqual(['Sin elegir', 'M-01 · Muelle de carga']);
    expect(celda('Ubicación', 1)).toHaveValue('');
  });

  it('el lote se escribe solo si el artículo se sigue por lote, y entonces es la última celda', async () => {
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await usuario.selectOptions(celda('Ubicación', 1), 'A-01 · Pasillo A');
    expect(celda('Lote', 1)).toBeDisabled();
    // Con una sola opción, Intro elige esa.
    await usuario.type(celda('Artículo', 1), 'pintura');
    await screen.findByRole('option', { name: 'PIN-RJ · Pintura roja' });
    await usuario.keyboard('{Enter}');
    expect(celda('Artículo', 1)).toHaveValue('PIN-RJ · Pintura roja');
    expect(celda('Lote', 1)).toBeEnabled();
    expect(celda('Número de serie', 1)).toBeDisabled();

    // Intro en el coste ya no añade: la última es el lote.
    await usuario.click(celda('Coste por unidad base', 1));
    await usuario.keyboard('{Enter}');
    expect(screen.queryByRole('combobox', { name: 'Ubicación de la línea 2' })).toBeNull();
    await usuario.type(celda('Lote', 1), 'L-07{Enter}');
    await waitFor(() => {
      expect(celda('Ubicación', 2)).toHaveFocus();
    });
  });

  it('escribir otra vez en el artículo deja la línea sin él, hasta que se elige otro', async () => {
    montar();
    const usuario = userEvent.setup();

    await escribirLaCabecera(usuario);
    await escribirLinea(usuario, 1, {
      ubicacion: 'A-01 · Pasillo A',
      escrito: 'TOR',
      opcion: 'TOR-M8 · Tornillo M8 largo',
      cantidad: '1',
    });
    // La búsqueda espera a que se deje de escribir: una petición por palabra, no por letra.
    expect(servidorDelAlta.busquedas).toEqual(['TOR']);
    await usuario.type(celda('Artículo', 1), 'x');
    await usuario.click(screen.getByRole('button', { name: 'Abrir el ajuste' }));

    await waitFor(() => {
      expect(celda('Artículo', 1)).toHaveFocus();
    });
    expect(celda('Artículo', 1)).toHaveAccessibleDescription(/Elige un artículo de la lista\./);
    expect(servidorDelAlta.altas).toEqual([]);
  });

  it('sin poder ver los maestros que ofrece, lo dice y no enseña el formulario', async () => {
    montar(PUEDE_ABRIR.filter((permiso) => permiso !== PERMISOS.serieVer));

    expect(
      await screen.findByText(/^Para abrir un ajuste hace falta poder ver los almacenes/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('form')).toBeNull();
  });
});
