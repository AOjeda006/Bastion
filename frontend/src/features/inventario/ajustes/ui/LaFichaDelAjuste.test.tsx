import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDelAjuste } from './PaginaDelAjuste.tsx';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { ALFA, PERMISOS_DE_LECTURA, almacenesDe, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type AjusteDto = components['schemas']['AjusteDto'];
type ArticuloDto = components['schemas']['ArticuloDto'];
type FichaDeAjusteDto = components['schemas']['FichaDeAjusteDto'];
type LineaDeFichaDeAjusteDto = components['schemas']['LineaDeFichaDeAjusteDto'];
type UbicacionDto = components['schemas']['UbicacionDto'];
type UnidadMedidaDto = components['schemas']['UnidadMedidaDto'];

const PATRON = '/ajustes/:id';

const PUEDE_VER = [
  ...PERMISOS_DE_LECTURA,
  PERMISOS.ajusteVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
];

const PUEDE_TODO = [
  ...PUEDE_VER,
  PERMISOS.ajusteConfirmar,
  PERMISOS.ajusteAnular,
  PERMISOS.ajusteDescartar,
];

/** ALM-ALFA, y los dos artículos de Alfa: TOR-M6 y TUE-M6. */
const ALMACEN = almacenesDe(ALFA.id).elementos[0]!;
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

const AJUSTE = 'adadadad-0000-0000-0000-000000000015';
const INVERSO = 'adadadad-0000-0000-0000-000000000017';
const RECUENTO = '7e7e7e7e-0000-0000-0000-000000000008';

/** Una línea en la unidad base, que entra con su coste, y otra en cajas, que sale. */
function lasDeSiempre(): LineaDeFichaDeAjusteDto[] {
  return [
    {
      numero: 1,
      ubicacionId: UBICACION.id,
      articuloId: TORNILLO.id,
      cantidadIntroducida: 5,
      unidadIntroducidaId: UD.id,
      factorAUnidadBase: 1,
      cantidadEnUnidadBase: 5,
      costeUnitario: 2.5,
      codigoDeLote: null,
      numeroDeSerie: null,
      valor: null,
    },
    {
      numero: 2,
      ubicacionId: UBICACION.id,
      articuloId: TUERCA.id,
      cantidadIntroducida: -1,
      unidadIntroducidaId: CAJA.id,
      // Lo pone el servidor, con la conversión del día (ADR-0065 §2).
      factorAUnidadBase: 100,
      cantidadEnUnidadBase: -100,
      costeUnitario: null,
      codigoDeLote: null,
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
 * Los ajustes del lado del servidor y lo que ha llegado a cada acción.
 *
 * Responde como la API: la ficha lleva su `ETag`; confirmar y anular, sin `If-Match` y con su clave
 * (ADR-0065 §7); descartar compara el `If-Match`, y un descartado deja de existir: `404` (§5).
 */
const servidorDelAjuste = {
  fichas: new Map<string, FichaDeAjusteDto>(),
  version: 3,
  rechazo: null as { estado: number; codigo: string } | null,
  lecturas: [] as string[],
  confirmaciones: [] as Escritura[],
  anulaciones: [] as Escritura[],
  descartes: [] as Escritura[],
};

function ficha(cambios: Partial<FichaDeAjusteDto> = {}): FichaDeAjusteDto {
  return {
    id: AJUSTE,
    serieId: '5e5e5e5e-0000-0000-0000-000000000002',
    numero: null,
    almacenId: ALMACEN.id,
    fechaDeOperacion: '2026-11-05',
    motivo: 'Rotura en el muelle',
    estado: 'Borrador',
    divisa: 'EUR',
    anulaAId: null,
    recuentoId: null,
    lineas: lasDeSiempre(),
    ...cambios,
  };
}

/** Ya confirmado: con su número y lo que movió cada línea en la valoración. */
function confirmada(cambios: Partial<FichaDeAjusteDto> = {}): FichaDeAjusteDto {
  const [entrada, salida] = lasDeSiempre() as [LineaDeFichaDeAjusteDto, LineaDeFichaDeAjusteDto];

  return ficha({
    numero: 16,
    estado: 'Confirmado',
    lineas: [
      { ...entrada, valor: 12.5 },
      { ...salida, valor: -45 },
    ],
    ...cambios,
  });
}

function resumen(dto: FichaDeAjusteDto): AjusteDto {
  return {
    id: dto.id,
    serieId: dto.serieId,
    numero: dto.numero,
    almacenId: dto.almacenId,
    fechaDeOperacion: dto.fechaDeOperacion,
    motivo: dto.motivo,
    estado: dto.estado,
    lineas: dto.lineas.length,
    anulaAId: dto.anulaAId,
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
  Object.assign(servidorDelAjuste, {
    fichas: new Map([[AJUSTE, ficha()]]),
    version: 3,
    rechazo: null,
    lecturas: [],
    confirmaciones: [],
    anulaciones: [],
    descartes: [],
  });

  const s = servidorDelAjuste;
  const base = '/api/v1/inventario/ajustes/:id';

  servidor.use(
    http.get(base, ({ params }) => {
      const id = String(params['id']);
      const una = s.fichas.get(id);
      s.lecturas.push(id);

      return una === undefined
        ? rechazo(404, 'ajuste-no-encontrado')
        : HttpResponse.json(una, { headers: { ETag: `"${String(s.version)}"` } });
    }),

    http.post(`${base}/confirmacion`, async ({ request }) => {
      s.confirmaciones.push(await escritura(request));

      if (s.rechazo !== null) {
        return rechazo(s.rechazo.estado, s.rechazo.codigo);
      }

      const hecho = confirmada();
      s.fichas.set(AJUSTE, hecho);
      s.version += 1;

      return HttpResponse.json(resumen(hecho));
    }),

    http.post(`${base}/anulacion`, async ({ request }) => {
      s.anulaciones.push(await escritura(request));

      if (s.rechazo !== null) {
        return rechazo(s.rechazo.estado, s.rechazo.codigo);
      }

      // El inverso nace confirmado, con las cantidades al revés, y el original queda anulado.
      const original = { ...s.fichas.get(AJUSTE)!, estado: 'Anulado' };
      const inverso = ficha({
        id: INVERSO,
        numero: 17,
        estado: 'Confirmado',
        motivo: 'Anulación: mal contado',
        anulaAId: AJUSTE,
        lineas: original.lineas.map((l) => ({
          ...l,
          cantidadIntroducida: -Number(l.cantidadIntroducida),
          cantidadEnUnidadBase: -Number(l.cantidadEnUnidadBase),
          valor: -Number(l.valor),
        })),
      });
      s.fichas.set(AJUSTE, original);
      s.fichas.set(INVERSO, inverso);
      s.version += 1;

      return HttpResponse.json({ original: resumen(original), inverso: resumen(inverso) });
    }),

    http.delete(base, async ({ request }) => {
      const recibida = await escritura(request);
      s.descartes.push(recibida);

      if (recibida.ifMatch !== `"${String(s.version)}"`) {
        return rechazo(412, 'version-obsoleta');
      }

      s.fichas.delete(AJUSTE);

      return new HttpResponse(null, { status: 204 });
    }),

    http.get('/api/v1/organizacion/almacenes/:id', () => HttpResponse.json(ALMACEN)),
    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      const articulo = [TORNILLO, TUERCA].find((a) => a.id === params['id']);

      return articulo === undefined
        ? rechazo(404, 'articulo-no-encontrado')
        : HttpResponse.json(articulo);
    }),
    http.get('/api/v1/organizacion/ubicaciones/:id', () => HttpResponse.json(UBICACION)),
    http.get('/api/v1/organizacion/unidades-de-medida/:id', ({ params }) =>
      HttpResponse.json(params['id'] === CAJA.id ? CAJA : UD),
    ),
  );
});

function montar(
  permisos: readonly string[] = PUEDE_TODO,
  ruta = `/ajustes/${AJUSTE}`,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(<PaginaDelAjuste />, ruta, 'es', PATRON, [
    { path: '/ajustes', element: <p>El listado de ajustes</p> },
    { path: '/recuentos/:id', element: <p>La ficha del recuento</p> },
  ]);
}

/** Las filas de las líneas, celda a celda, sin la cabecera ni el número, que es la de su fila. */
function filas(): string[][] {
  const tabla = screen.getByRole('table', { name: 'Líneas del ajuste' });

  return within(tabla)
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
  const tabla = screen.getByRole('table', { name: 'Líneas del ajuste' });

  return within(tabla)
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

describe('La ficha de un ajuste', () => {
  it('dice la cabecera y cada línea, con el factor que puso el servidor; el borrador no lleva valor', async () => {
    montar();

    expect(
      await screen.findByRole('heading', { name: 'Borrador de ALM-ALFA del 5 nov 2026' }),
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(cabecera()).toEqual({
        Estado: 'Borrador',
        Almacén: 'ALM-ALFA · Nave central de Alfa',
        'Fecha de operación': '5 nov 2026',
        Motivo: 'Rotura en el muelle',
      });
    });
    expect(columnas()).toEqual([
      'Nº',
      'Ubicación',
      'Artículo',
      'Cantidad',
      'Factor de conversión',
      'En unidad base',
      'Coste unitario',
      'Lote o serie',
    ]);
    await waitFor(() => {
      expect(filas()).toEqual([
        ['A-01', 'TOR-M6 · Tornillo M6 zincado', '5 UD', '1', '5', '2,50 €', ''],
        ['A-01', 'TUE-M6 · Tuerca M6 zincada', '-1 CAJA', '100', '-100', '', ''],
      ]);
    });
    expect(screen.getByText(/El valor se calcula al confirmarlo\.$/)).toBeInTheDocument();
  });

  it('confirmar se pregunta, manda su clave y no la versión, y el aviso dice el número con el foco', async () => {
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar el ajuste' }));
    const pregunta = screen.getByRole('group', {
      name: 'Al confirmarlo recibe su número y mueve el stock.',
    });
    expect(within(pregunta).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await usuario.click(within(pregunta).getByRole('button', { name: 'Sí, confirmar' }));

    const aviso = await screen.findByText('Ajuste confirmado con el número 16.');
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDelAjuste.confirmaciones).toEqual([
      { ifMatch: null, clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown, cuerpo: null },
    ]);

    // La ficha se lee otra vez: ya con su número, su valor y lo que se puede hacer ahora.
    expect(await screen.findByRole('heading', { name: 'Ajuste 16' })).toBeInTheDocument();
    expect(columnas().at(-1)).toBe('Valor');
    expect(filas().map((fila) => fila.at(-1))).toEqual(['12,50 €', '-45,00 €']);
    expect(screen.getByRole('button', { name: 'Anular el ajuste' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar el ajuste' })).not.toBeInTheDocument();
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
    expect(servidorDelAjuste.descartes).toEqual([]);

    await usuario.click(screen.getByRole('button', { name: 'Descartar el borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));

    const aviso = await screen.findByText('Borrador descartado.');
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDelAjuste.descartes).toEqual([{ ifMatch: '"3"', clave: null, cuerpo: null }]);
    // No se vuelve a pedir: contestaría un 404. Queda el camino de vuelta, y nada más.
    expect(servidorDelAjuste.lecturas).toEqual([AJUSTE]);
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Volver a los ajustes' })).toHaveAttribute(
      'href',
      '/ajustes',
    );
  });

  it('volver atrás a un descartado da su 404, sin pintar antes lo que había', async () => {
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Descartar el borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, descartar' }));
    await usuario.click(await screen.findByRole('link', { name: 'Volver a los ajustes' }));
    expect(await screen.findByText('El listado de ajustes')).toBeInTheDocument();

    // Atrás, con el navegador. Lo que guardara la caché se pintaría al montar, antes de leer nada:
    // por eso la primera aserción va sin esperar.
    await act(async () => {
      await enrutador.navigate(-1);
    });
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ese ajuste ya no existe. Vuelve al listado y actualiza.',
    );
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(servidorDelAjuste.lecturas).toEqual([AJUSTE, AJUSTE]);
  });

  it('anular pide el motivo, y el aviso lleva al inverso, que se abre sin el aviso', async () => {
    servidorDelAjuste.fichas.set(AJUSTE, confirmada());
    const { enrutador } = montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Anular el ajuste' }));
    const motivo = screen.getByRole('textbox', { name: 'Motivo' });
    await waitFor(() => {
      expect(motivo).toHaveFocus();
    });
    await usuario.type(motivo, 'Mal contado');
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));

    const aviso = await screen.findByText(/^Ajuste anulado\. Lo compensa el ajuste 17\./);
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    expect(servidorDelAjuste.anulaciones).toEqual([
      {
        ifMatch: null,
        clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown,
        cuerpo: { motivo: 'Mal contado' },
      },
    ]);
    await waitFor(() => {
      expect(cabecera()['Estado']).toBe('Anulado');
    });

    await usuario.click(within(aviso).getByRole('link', { name: 'Ver el inverso' }));

    expect(await screen.findByRole('heading', { name: 'Ajuste 17' })).toBeInTheDocument();
    expect(enrutador.state.location.pathname).toBe(`/ajustes/${INVERSO}`);
    // Otra ficha, con su estado: ni el aviso del original ni un botón para anular el inverso.
    expect(screen.queryByText(/^Ajuste anulado/)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver el ajuste original' })).toHaveAttribute(
      'href',
      `/ajustes/${AJUSTE}`,
    );
    expect(screen.queryByRole('button', { name: 'Anular el ajuste' })).not.toBeInTheDocument();
  });

  it('el motivo que falta o que rechaza el servidor va en su campo, con el foco', async () => {
    servidorDelAjuste.fichas.set(AJUSTE, confirmada());
    servidorDelAjuste.rechazo = { estado: 400, codigo: 'ajuste-motivo-no-valido' };
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Anular el ajuste' }));
    const motivo = screen.getByRole('textbox', { name: 'Motivo' });

    // Vacío no sale: lo para el formulario.
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));
    await waitFor(() => {
      expect(motivo).toHaveAccessibleDescription(/^Escribe el motivo, en 300 caracteres o menos/);
    });
    expect(servidorDelAjuste.anulaciones).toEqual([]);

    await usuario.type(motivo, 'Mal contado');
    await usuario.click(screen.getByRole('button', { name: 'Sí, anular' }));

    await waitFor(() => {
      expect(servidorDelAjuste.anulaciones).toHaveLength(1);
    });
    await waitFor(() => {
      expect(motivo).toHaveFocus();
    });
    expect(motivo).toHaveAttribute('aria-invalid', 'true');
    expect(motivo).toHaveAccessibleDescription(/^Escribe el motivo, en 300 caracteres o menos/);
    // Sigue abierto, con lo escrito, y arriba no se dice nada.
    expect(motivo).toHaveValue('Mal contado');
    expect(screen.getAllByRole('alert').map((aviso) => aviso.id)).toEqual([
      'motivo-de-la-anulacion-error',
    ]);
  });

  it('cualquier otro rechazo va arriba con el foco, y la ficha se vuelve a leer', async () => {
    servidorDelAjuste.rechazo = { estado: 409, codigo: 'ajuste-en-ejercicio-cerrado' };
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar el ajuste' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, confirmar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/^La fecha de ese ajuste cae en un ejercicio cerrado/);
    await waitFor(() => {
      expect(aviso).toHaveFocus();
    });
    await waitFor(() => {
      expect(servidorDelAjuste.lecturas).toEqual([AJUSTE, AJUSTE]);
    });
    // La ficha sigue: lo que ya se enseñaba no se retira.
    expect(
      screen.getByRole('heading', { name: 'Borrador de ALM-ALFA del 5 nov 2026' }),
    ).toBeInTheDocument();
  });

  it('el ajuste de un recuento lleva a su recuento a quien puede verlo, y no se anula desde aquí', async () => {
    servidorDelAjuste.fichas.set(AJUSTE, confirmada({ recuentoId: RECUENTO }));
    const { unmount } = montar([...PUEDE_TODO, PERMISOS.recuentoVer]);

    expect(await screen.findByRole('link', { name: 'Ver su recuento' })).toHaveAttribute(
      'href',
      `/recuentos/${RECUENTO}`,
    );
    expect(screen.queryByRole('button', { name: 'Anular el ajuste' })).not.toBeInTheDocument();
    unmount();

    montar();
    await screen.findByRole('heading', { name: 'Ajuste 16' });
    expect(cabecera()['Viene de']).toBe('Un recuento');
    expect(screen.queryByRole('link', { name: 'Ver su recuento' })).not.toBeInTheDocument();
  });

  it('quien solo ve lee la ficha entera y no encuentra ni un botón', async () => {
    montar(PUEDE_VER);

    await screen.findByRole('heading', { name: 'Borrador de ALM-ALFA del 5 nov 2026' });
    await waitFor(() => {
      expect(filas()).toHaveLength(2);
    });
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
