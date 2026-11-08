import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { Idioma } from '@/app/i18n/idioma.ts';
import { ALFA, PERMISOS_DE_LECTURA } from '@/pruebas/datos.ts';
import { capturarExportaciones, exportarYComprobar } from '@/pruebas/exportaciones.ts';
import { montarPantalla, type PantallaMontada } from '@/pruebas/montar.tsx';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { PaginaDeArticulos } from './PaginaDeArticulos.tsx';

type ArticuloDto = components['schemas']['ArticuloDto'];

const TORNILLO: ArticuloDto = {
  id: 'fffffff1-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  codigo: 'TOR-M6',
  descripcion: 'Tornillo M6 zincado',
  tipo: 'Bien',
  trazabilidad: 'Ninguna',
  unidadBaseId: 'aaaa0001-0000-0000-0000-000000000001',
  impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
  categoriaId: null,
  retirada: false,
};

const ARANDELA: ArticuloDto = {
  ...TORNILLO,
  id: 'fffffff1-0000-0000-0000-000000000004',
  codigo: 'ARA-M5',
  descripcion: 'Arandela M5, de un proveedor que ya no la sirve',
  retirada: true,
};

/**
 * Los artículos del lado del servidor, con su versión, y lo que ha llegado a cada escritura.
 *
 * Como en la API: el listado deja fuera los retirados salvo con `?retiradas=true`, la ficha sirve la
 * `ETag` de ahora, y un `If-Match` que no es el de la última lectura sale con el `412`. Retirar y
 * reincorporar son idempotentes, y cada uno sube la versión.
 */
const servidorDeArticulos = {
  articulos: [] as ArticuloDto[],
  versiones: new Map<string, number>(),
  lecturas: 0,
  listados: [] as string[],
  escrituras: [] as { metodo: string; id: string; ifMatch: string | null }[],
};

function etiqueta(version: number): string {
  return `"${String(version)}"`;
}

function versionDe(id: string): number {
  return servidorDeArticulos.versiones.get(id) ?? 0;
}

/** Cambia un artículo en el servidor, como la API: lo dicho, y una versión más. */
function cambiar(id: string, cambio: Partial<ArticuloDto>): void {
  servidorDeArticulos.articulos = servidorDeArticulos.articulos.map((articulo) =>
    articulo.id === id ? { ...articulo, ...cambio } : articulo,
  );
  servidorDeArticulos.versiones.set(id, versionDe(id) + 1);
}

function escribir(metodo: string, id: string, ifMatch: string | null): Response {
  servidorDeArticulos.escrituras.push({ metodo, id, ifMatch });

  if (ifMatch !== etiqueta(versionDe(id))) {
    return HttpResponse.json({ type: '/errors/version-obsoleta', status: 412 }, { status: 412 });
  }

  cambiar(id, { retirada: metodo === 'POST' });

  return new HttpResponse(null, { status: 204 });
}

beforeEach(() => {
  servidorDeArticulos.articulos = [{ ...TORNILLO }, { ...ARANDELA }];
  servidorDeArticulos.versiones = new Map([
    [TORNILLO.id, 3],
    [ARANDELA.id, 7],
  ]);
  servidorDeArticulos.lecturas = 0;
  servidorDeArticulos.listados = [];
  servidorDeArticulos.escrituras = [];

  servidor.use(
    http.get('/api/v1/catalogo/articulos', ({ request }) => {
      const consulta = new URL(request.url).searchParams;
      servidorDeArticulos.listados.push(consulta.toString());
      const elementos = servidorDeArticulos.articulos.filter(
        ({ retirada }) => !retirada || consulta.get('retiradas') === 'true',
      );

      return HttpResponse.json({ elementos, pagina: 1, tamanio: 20, total: elementos.length });
    }),

    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      servidorDeArticulos.lecturas += 1;
      const articulo = servidorDeArticulos.articulos.find(({ id }) => id === params['id']);

      return articulo === undefined
        ? HttpResponse.json(
            { type: '/errors/articulo-no-encontrado', status: 404 },
            { status: 404 },
          )
        : HttpResponse.json(articulo, { headers: { ETag: etiqueta(versionDe(articulo.id)) } });
    }),

    http.post('/api/v1/catalogo/articulos/:id/retirada', ({ params, request }) =>
      escribir('POST', String(params['id']), request.headers.get('If-Match')),
    ),

    http.delete('/api/v1/catalogo/articulos/:id/retirada', ({ params, request }) =>
      escribir('DELETE', String(params['id']), request.headers.get('If-Match')),
    ),
  );
});

const LOS_DOS = [PERMISOS.articuloRetirar, PERMISOS.articuloReincorporar];

function montar(
  url = '/articulos',
  permisos: readonly string[] = LOS_DOS,
  idioma: Idioma = 'es',
): PantallaMontada {
  abrirSesionYaRecuperada(ALFA.id, [...PERMISOS_DE_LECTURA, ...permisos]);

  return montarPantalla(<PaginaDeArticulos />, url, idioma);
}

/** La fila de un artículo, por su código. */
async function filaDe(codigo: string): Promise<HTMLElement> {
  const fila = (await screen.findByRole('cell', { name: codigo })).closest('tr');

  if (fila === null) {
    throw new Error(`${codigo} no está en ninguna fila.`);
  }

  return fila;
}

/**
 * La retirada del artículo en su listado (ítem 3.6, ADR-0082).
 *
 * El listado no trae los retirados salvo que se pidan, y los marca cuando los trae; cada fila se
 * retira o se reincorpora en una ventana que lee la ficha por su versión y dice lo que pasa antes
 * del botón que lo hace. Lo que hace la ventana en un navegador —modal, el foco, Escape— lo prueba
 * `Dialogo.navegador.test.tsx`, y que quepa en 320 px, `LaVentanaDeLaRetiradaCabeEn320`: jsdom no
 * dispone nada (ADR-0081).
 */
describe('La retirada del artículo', () => {
  it('sin pedirlos, los retirados no se piden; con el conmutador, la URL lo dice y salen marcados', async () => {
    const usuario = userEvent.setup();
    const { enrutador } = montar('/articulos?pagina=2', []);

    // Desde la página 2: el conmutador es un filtro más, y devuelve a la primera.
    expect(await filaDe('TOR-M6')).not.toHaveTextContent('Retirado');
    expect(screen.queryByRole('cell', { name: 'ARA-M5' })).not.toBeInTheDocument();
    const conmutador = screen.getByRole('checkbox', { name: 'Ver también los retirados' });
    expect(conmutador).not.toBeChecked();
    expect(servidorDeArticulos.listados).toEqual(['page=2&size=20']);

    await usuario.click(conmutador);

    expect(await filaDe('ARA-M5')).toHaveTextContent('Retirado');
    expect(await filaDe('TOR-M6')).not.toHaveTextContent('Retirado');
    expect(enrutador.state.location.search).toBe('?retiradas=true');
    expect(screen.getByRole('checkbox', { name: 'Ver también los retirados' })).toBeChecked();
    expect(servidorDeArticulos.listados.at(-1)).toBe('page=1&size=20&retiradas=true');

    // Y se quita igual: la URL lo olvida y la fila retirada se va.
    await usuario.click(screen.getByRole('checkbox', { name: 'Ver también los retirados' }));
    await waitFor(() => {
      expect(screen.queryByRole('cell', { name: 'ARA-M5' })).not.toBeInTheDocument();
    });
    expect(enrutador.state.location.search).toBe('');
  });

  it('en inglés también: el conmutador, la marca, la columna y el nombre de cada botón', async () => {
    montar('/articulos?retiradas=true', LOS_DOS, 'en');

    expect(await filaDe('ARA-M5')).toHaveTextContent('Withdrawn');
    expect(screen.getByRole('checkbox', { name: 'Also show withdrawn items' })).toBeChecked();
    expect(screen.getByRole('columnheader', { name: 'Withdrawal' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Withdraw TOR-M6' })).toHaveTextContent('Withdraw');
    expect(screen.getByRole('button', { name: 'Reinstate ARA-M5' })).toHaveTextContent('Reinstate');
  });

  it('cada fila ofrece lo que su permiso deja; sin ninguno de los dos, ni columna, y la marca sí', async () => {
    const soloRetirar = montar('/articulos?retiradas=true', [PERMISOS.articuloRetirar]);

    expect(await screen.findByRole('button', { name: 'Retirar TOR-M6' })).toHaveTextContent(
      'Retirar',
    );
    expect(
      within(await filaDe('ARA-M5')).queryByRole('button', { name: /Reincorporar/ }),
    ).not.toBeInTheDocument();
    soloRetirar.unmount();

    const soloReincorporar = montar('/articulos?retiradas=true', [PERMISOS.articuloReincorporar]);
    expect(await screen.findByRole('button', { name: 'Reincorporar ARA-M5' })).toBeVisible();
    expect(screen.queryByRole('button', { name: /^Retirar/ })).not.toBeInTheDocument();
    soloReincorporar.unmount();

    montar('/articulos?retiradas=true', []);
    expect(await filaDe('ARA-M5')).toHaveTextContent('Retirado');
    expect(screen.queryByRole('columnheader', { name: 'Retirada' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Retirar|Reincorporar/ })).not.toBeInTheDocument();
  });

  it('retirar dice lo que pasa antes del botón, escribe con la versión leída, lo avisa e invalida la caché entera', async () => {
    const usuario = userEvent.setup();
    const { cache } = montar();
    const ajena = ['otra-funcionalidad', 'articulos-del-buscador'];
    cache.setQueryData(ajena, ['TOR-M6']);

    await usuario.click(await screen.findByRole('button', { name: 'Retirar TOR-M6' }));

    const ventana = await screen.findByRole('dialog', { name: 'Retirar TOR-M6' });
    const confirmar = await within(ventana).findByRole('button', { name: 'Retirar el artículo' });
    expect(within(ventana).getByText('Tornillo M6 zincado', { exact: false })).toBeVisible();
    // Las dos mitades del ADR-0023, en la descripción del botón: el lector las dice al llegar a él.
    expect(confirmar).toHaveAccessibleDescription(
      expect.stringContaining('Deja de ofrecerse para lo nuevo'),
    );
    expect(confirmar).toHaveAccessibleDescription(
      expect.stringContaining('Lo que ya lo lleva sigue su camino'),
    );
    expect(confirmar).toHaveAccessibleDescription(expect.stringContaining('sale con un recuento'));

    await usuario.click(confirmar);

    const aviso = await screen.findByText('TOR-M6 queda retirado: ya no se ofrece para lo nuevo.');
    expect(aviso.closest('[role="status"]')).not.toBeNull();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(servidorDeArticulos.escrituras).toEqual([
      { metodo: 'POST', id: TORNILLO.id, ifMatch: etiqueta(3) },
    ]);

    // Sin los retirados, sale del listado; y lo que otra funcionalidad tenía guardado, también.
    await waitFor(() => {
      expect(screen.queryByRole('cell', { name: 'TOR-M6' })).not.toBeInTheDocument();
    });
    expect(cache.getQueryState(ajena)?.isInvalidated).toBe(true);
  });

  it('reincorporar dice lo suyo y borra la retirada con la versión leída', async () => {
    const usuario = userEvent.setup();
    montar('/articulos?retiradas=true');

    await usuario.click(await screen.findByRole('button', { name: 'Reincorporar ARA-M5' }));
    const ventana = await screen.findByRole('dialog', { name: 'Reincorporar ARA-M5' });
    const confirmar = await within(ventana).findByRole('button', {
      name: 'Reincorporar el artículo',
    });
    expect(confirmar).toHaveAccessibleDescription(
      expect.stringContaining('Vuelve a ofrecerse para lo nuevo'),
    );
    expect(confirmar).not.toHaveAccessibleDescription(expect.stringContaining('recuento'));

    await usuario.click(confirmar);

    expect(await screen.findByText('ARA-M5 vuelve a ofrecerse para lo nuevo.')).toBeVisible();
    expect(servidorDeArticulos.escrituras).toEqual([
      { metodo: 'DELETE', id: ARANDELA.id, ifMatch: etiqueta(7) },
    ]);
    await waitFor(async () => {
      expect(await filaDe('ARA-M5')).not.toHaveTextContent('Retirado');
    });
  });

  it('cancelar cierra sin escribir, y la vez siguiente se vuelve a leer', async () => {
    const usuario = userEvent.setup();
    montar();

    const retirar = await screen.findByRole('button', { name: 'Retirar TOR-M6' });
    await usuario.click(retirar);
    await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    await usuario.click(retirar);
    await screen.findByRole('button', { name: 'Retirar el artículo' });

    expect(servidorDeArticulos.escrituras).toEqual([]);
    expect(servidorDeArticulos.lecturas).toBe(2);
    expect(screen.queryByText(/queda retirado/)).not.toBeInTheDocument();
  });

  it('al volver a abrirla espera a la lectura de ahora, y no confirma sobre la de antes mientras llega', async () => {
    const usuario = userEvent.setup();
    montar();

    const retirar = await screen.findByRole('button', { name: 'Retirar TOR-M6' });
    await usuario.click(retirar);
    await screen.findByRole('button', { name: 'Retirar el artículo' });
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // Con la ventana cerrada, otra persona cambia el artículo, y la lectura siguiente se retiene.
    cambiar(TORNILLO.id, { descripcion: 'Tornillo M6 zincado, caja de 100' });
    let abrirLaPuerta = (): void => undefined;
    const puerta = new Promise<void>((resolver) => {
      abrirLaPuerta = resolver;
    });
    servidor.use(
      http.get('/api/v1/catalogo/articulos/:id', async () => {
        servidorDeArticulos.lecturas += 1;
        await puerta;
        const articulo = servidorDeArticulos.articulos.find(({ id }) => id === TORNILLO.id)!;

        return HttpResponse.json(articulo, { headers: { ETag: etiqueta(versionDe(articulo.id)) } });
      }),
    );

    await usuario.click(retirar);
    const ventana = await screen.findByRole('dialog');
    await waitFor(() => {
      expect(servidorDeArticulos.lecturas).toBe(2);
    });

    // Con la lectura de antes guardada, aquí ya estaría el botón, con la versión 3.
    expect(within(ventana).queryByRole('button', { name: 'Retirar el artículo' })).toBeNull();

    abrirLaPuerta();
    await usuario.click(
      await within(ventana).findByRole('button', { name: 'Retirar el artículo' }),
    );
    expect(await screen.findByText(/TOR-M6 queda retirado/)).toBeVisible();
    expect(servidorDeArticulos.escrituras.map(({ ifMatch }) => ifMatch)).toEqual([etiqueta(4)]);
  });

  it('si otra persona ha cambiado el artículo, lo dice y deja cargar la versión actual', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Retirar TOR-M6' }));
    const ventana = await screen.findByRole('dialog');
    await within(ventana).findByRole('button', { name: 'Retirar el artículo' });

    cambiar(TORNILLO.id, { descripcion: 'Tornillo M6 zincado, caja de 100' });
    await usuario.click(within(ventana).getByRole('button', { name: 'Retirar el artículo' }));

    const rechazo = await within(ventana).findByRole('alert');
    expect(rechazo).toHaveTextContent(
      'Alguien ha guardado antes que tú. Vuelve a abrir el formulario para no pisar sus cambios.',
    );

    await usuario.click(within(rechazo).getByRole('button', { name: 'Cargar la versión actual' }));

    // La de ahora, con lo que cambió la otra persona, y el rechazo fuera.
    expect(
      await within(ventana).findByText('Tornillo M6 zincado, caja de 100', { exact: false }),
    ).toBeVisible();
    expect(within(ventana).queryByRole('alert')).not.toBeInTheDocument();

    await usuario.click(within(ventana).getByRole('button', { name: 'Retirar el artículo' }));

    expect(await screen.findByText(/TOR-M6 queda retirado/)).toBeVisible();
    expect(servidorDeArticulos.escrituras.map(({ ifMatch }) => ifMatch)).toEqual([
      etiqueta(3),
      etiqueta(4),
    ]);
  });

  it('sin la versión del artículo no ofrece confirmar: no habría con qué escribir', async () => {
    const usuario = userEvent.setup();
    servidor.use(http.get('/api/v1/catalogo/articulos/:id', () => HttpResponse.json(TORNILLO)));
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Retirar TOR-M6' }));

    const ventana = screen.getByRole('dialog');
    expect(await within(ventana).findByRole('alert')).toHaveTextContent(
      'No se han podido cargar los datos. Inténtalo de nuevo.',
    );
    expect(within(ventana).queryByRole('button', { name: 'Retirar el artículo' })).toBeNull();
    await usuario.click(within(ventana).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  it('retirar el último deja el listado vacío, que lo dice sin los retirados, y el aviso sigue', async () => {
    const usuario = userEvent.setup();
    servidorDeArticulos.articulos = [{ ...TORNILLO }];
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Retirar TOR-M6' }));
    await usuario.click(await screen.findByRole('button', { name: 'Retirar el artículo' }));

    expect(
      await screen.findByText(
        'Esta empresa no tiene ningún artículo en el catálogo, sin contar los retirados.',
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        'Los retirados no salen en el listado: para verlos, marca «Ver también los retirados».',
      ),
    ).toBeVisible();
    expect(screen.getByText('TOR-M6 queda retirado: ya no se ofrece para lo nuevo.')).toBeVisible();

    // Con ellos, vacío sí es «ninguno», y no se explica cómo verlos.
    servidorDeArticulos.articulos = [];
    await usuario.click(screen.getByRole('checkbox', { name: 'Ver también los retirados' }));
    expect(
      await screen.findByText('Todavía no hay ningún artículo dado de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByText(/Los retirados no salen/)).not.toBeInTheDocument();
  });

  it('exportar se lleva los retirados cuando el listado los trae', async () => {
    const exportaciones = capturarExportaciones();
    montar('/articulos?retiradas=true&busqueda=m');

    expect(await filaDe('ARA-M5')).toBeVisible();
    await exportarYComprobar(exportaciones, '/api/v1/catalogo/articulos/exportacion', {
      q: 'm',
      retiradas: 'true',
    });
  });
});
