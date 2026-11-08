import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { consultarHoy } from '@/shared/api/hoy.ts';
import { CLAVE_DE_HOY } from '@/shared/hoy/useHoy.ts';
import { ALFA, NAVE_DE_CANARIAS, PERMISOS_DE_LECTURA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionSimulada, servidor } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

type AlmacenDto = components['schemas']['AlmacenDto'];

/** La central de Alfa, que hereda la zona de su empresa. */
function central(): AlmacenDto {
  const [primero] = almacenesDe(ALFA.id).elementos;

  if (primero === undefined) {
    throw new Error('Los datos de prueba se han quedado sin la central de Alfa.');
  }

  return { ...primero };
}

/**
 * Los almacenes del lado del servidor, con su versión, y lo que ha llegado a cada `PUT`.
 *
 * Como la ficha de la trazabilidad: la `ETag` que se sirve es la de ahora, y un `If-Match` que no es
 * el de la última lectura sale con el `412`, como en la API. La zona que rige la calcula el servidor:
 * la propia, o la de la empresa, que aquí es la de la península.
 */
const servidorDeAlmacenes = {
  almacenes: [] as AlmacenDto[],
  versiones: new Map<string, number>(),
  lecturas: 0,
  escrituras: [] as { id: string; ifMatch: string | null; cuerpo: Record<string, unknown> }[],
};

function etiqueta(version: number): string {
  return `"${String(version)}"`;
}

function versionDe(id: string): number {
  return servidorDeAlmacenes.versiones.get(id) ?? 0;
}

/** Guarda en el servidor una zona, como lo haría la API: la que rige y una versión más. */
function guardarZona(id: string, zonaHoraria: AlmacenDto['zonaHoraria']): AlmacenDto {
  const indice = servidorDeAlmacenes.almacenes.findIndex((almacen) => almacen.id === id);
  const queda: AlmacenDto = {
    ...servidorDeAlmacenes.almacenes[indice]!,
    zonaHoraria,
    zonaEfectiva: zonaHoraria ?? 'Europe/Madrid',
  };

  servidorDeAlmacenes.almacenes[indice] = queda;
  servidorDeAlmacenes.versiones.set(id, versionDe(id) + 1);

  return queda;
}

beforeEach(() => {
  servidorDeAlmacenes.almacenes = [central(), { ...NAVE_DE_CANARIAS }];
  servidorDeAlmacenes.versiones = new Map([
    [central().id, 3],
    [NAVE_DE_CANARIAS.id, 5],
  ]);
  servidorDeAlmacenes.lecturas = 0;
  servidorDeAlmacenes.escrituras = [];

  servidor.use(
    http.get('/api/v1/organizacion/almacenes', () => {
      const elementos = servidorDeAlmacenes.almacenes;

      return HttpResponse.json({ elementos, pagina: 1, tamanio: 20, total: elementos.length });
    }),

    http.get('/api/v1/organizacion/almacenes/:id', ({ params }) => {
      servidorDeAlmacenes.lecturas += 1;
      const almacen = servidorDeAlmacenes.almacenes.find(({ id }) => id === params['id']);

      return almacen === undefined
        ? HttpResponse.json({ type: '/errors/almacen-no-encontrado', status: 404 }, { status: 404 })
        : HttpResponse.json(almacen, { headers: { ETag: etiqueta(versionDe(almacen.id)) } });
    }),

    http.put('/api/v1/organizacion/almacenes/:id', async ({ params, request }) => {
      const id = String(params['id']);
      const ifMatch = request.headers.get('If-Match');
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDeAlmacenes.escrituras.push({ id, ifMatch, cuerpo });

      if (ifMatch !== etiqueta(versionDe(id))) {
        return HttpResponse.json(
          { type: '/errors/version-obsoleta', status: 412 },
          { status: 412 },
        );
      }

      // Como la API: el 200 lleva el almacén y NO lleva `ETag`.
      return HttpResponse.json(guardarZona(id, cuerpo['zonaHoraria'] as AlmacenDto['zonaHoraria']));
    }),
  );
});

function montar(idioma: Idioma = 'es', modificar = true): ReturnType<typeof montarAplicacion> {
  abrirSesionSimulada(
    ALFA.id,
    modificar ? [...PERMISOS_DE_LECTURA, 'organizacion.almacen.modificar'] : PERMISOS_DE_LECTURA,
  );

  return montarAplicacion('/almacenes', idioma);
}

/** La celda de la zona de un almacén, por su código. */
async function zonaDe(codigo: string): Promise<HTMLElement> {
  const fila = (await screen.findByRole('cell', { name: codigo })).closest('tr');

  if (fila === null) {
    throw new Error(`${codigo} no está en ninguna fila.`);
  }

  return within(fila).getAllByRole('cell').at(-1)!;
}

/**
 * La zona de cada almacén, en su listado (ítem 3.5, ADR-0079 §12 y ADR-0080).
 *
 * El listado dice cuál rige y de dónde le viene; el diálogo la cambia por una de las tres o la
 * devuelve a la de su empresa, con el almacén entero sobre la versión leída. Lo que hace la ventana
 * en un navegador —modal, el foco que entra y el que vuelve, Escape— lo prueba
 * `Dialogo.navegador.test.tsx`: jsdom no dispone nada (ADR-0081).
 */
describe('La zona del almacén', () => {
  it.each<[Idioma, string, string, string]>([
    ['es', 'Zona horaria', 'Península y Baleares · de la empresa', 'Canarias · propia'],
    [
      'en',
      'Time zone',
      "Mainland Spain and Balearic Islands · the company's",
      'Canary Islands · its own',
    ],
  ])(
    '(%s) el listado dice la zona y de dónde le viene',
    async (idioma, columna, hereda, propia) => {
      montar(idioma, false);

      expect(await zonaDe('ALM-ALFA')).toHaveTextContent(hereda);
      expect(await zonaDe('ALM-CANARIAS')).toHaveTextContent(propia);
      expect(screen.getByRole('columnheader', { name: columna })).toBeVisible();

      // Sin el permiso de modificar, nada que cambiar. El servidor también lo diría, pero no se ofrece.
      expect(screen.queryByRole('button', { name: /^(Cambiar|Change)/ })).not.toBeInTheDocument();
    },
  );

  it('abre la ventana con la zona de ahora marcada, y lo que avisa antes de las opciones', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' }));

    const ventana = await screen.findByRole('dialog', { name: 'Zona horaria de ALM-ALFA' });
    const grupo = await within(ventana).findByRole('group', { name: 'Zona horaria' });
    expect(
      within(grupo)
        .getAllByRole('radio')
        .map((opcion) => opcion.closest('label')?.textContent),
    ).toEqual(['La de la empresa', 'Península y Baleares', 'Canarias', 'Ceuta y Melilla']);
    expect(within(grupo).getByRole('radio', { name: 'La de la empresa' })).toBeChecked();

    // Los avisos van en la descripción del grupo: el lector de pantalla los dice al entrar.
    expect(grupo).toHaveAccessibleDescription(expect.stringContaining('Lo ya fechado no cambia'));
    expect(grupo).toHaveAccessibleDescription(
      expect.stringContaining('Durante la hora que sigue al cambio'),
    );
    expect(grupo).not.toHaveAccessibleDescription(expect.stringContaining('almacenes'));
  });

  it('guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera', async () => {
    const usuario = userEvent.setup();
    const { cache } = montar();
    const cambiar = await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' });

    // Un «hoy» ya leído, de los que piden los formularios de Inventario: es de otra funcionalidad,
    // y la zona nueva lo mueve. Con la pantalla ya pintada: la sesión que se recupera al montar
    // contestaría dentro de esta espera, fuera de `act()`.
    await cache.query({ queryKey: CLAVE_DE_HOY, queryFn: consultarHoy });

    await usuario.click(cambiar);
    await usuario.click(await screen.findByRole('radio', { name: 'Canarias' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar la zona' }));

    expect(await screen.findByText('La zona de ALM-ALFA es ahora Canarias.')).toBeVisible();
    expect(
      screen.getByText('La zona de ALM-ALFA es ahora Canarias.').closest('[role="status"]'),
    ).not.toBeNull();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    const antes = central();
    expect(servidorDeAlmacenes.escrituras).toEqual([
      {
        id: antes.id,
        ifMatch: etiqueta(3),
        cuerpo: {
          nombre: antes.nombre,
          direccion: antes.direccion,
          tipo: antes.tipo,
          zonaHoraria: 'Atlantic/Canary',
        },
      },
    ]);

    // El listado se vuelve a pedir, y la fila dice la nueva.
    await waitFor(async () => {
      expect(await zonaDe('ALM-ALFA')).toHaveTextContent('Canarias · propia');
    });
    expect(cache.getQueryState(CLAVE_DE_HOY)?.isInvalidated).toBe(true);
  });

  it('volver a la de la empresa manda la zona nula, y dice cuál rige ahora', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(
      await screen.findByRole('button', { name: 'Cambiar la zona de ALM-CANARIAS' }),
    );
    expect(await screen.findByRole('radio', { name: 'Canarias' })).toBeChecked();
    await usuario.click(screen.getByRole('radio', { name: 'La de la empresa' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar la zona' }));

    expect(
      await screen.findByText(
        'ALM-CANARIAS toma ahora la zona de su empresa: Península y Baleares.',
      ),
    ).toBeVisible();
    expect(servidorDeAlmacenes.escrituras.map(({ cuerpo }) => cuerpo['zonaHoraria'])).toEqual([
      null,
    ]);
    await waitFor(async () => {
      expect(await zonaDe('ALM-CANARIAS')).toHaveTextContent(
        'Península y Baleares · de la empresa',
      );
    });
  });

  it('guardar la que ya tenía cierra sin escribir, y cancelar también', async () => {
    const usuario = userEvent.setup();
    montar();

    const cambiar = await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' });
    await usuario.click(cambiar);
    await usuario.click(await screen.findByRole('button', { name: 'Guardar la zona' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    await usuario.click(cambiar);
    await usuario.click(await screen.findByRole('radio', { name: 'Ceuta y Melilla' }));
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(servidorDeAlmacenes.escrituras).toEqual([]);
    expect(screen.queryByText(/es ahora|toma ahora/)).not.toBeInTheDocument();

    // Y la vez siguiente se vuelve a leer.
    expect(servidorDeAlmacenes.lecturas).toBe(2);
  });

  // Lo pide la 1205 de la F2 del 3.5: sin el `gcTime: 0`, salió verde en los dos carriles, porque
  // ningún caso volvía a abrir la ventana con la lectura todavía en camino.
  it('al volver a abrirla espera a la lectura de ahora, y no ofrece la de antes mientras llega', async () => {
    const usuario = userEvent.setup();
    montar();

    const cambiar = await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' });
    await usuario.click(cambiar);
    expect(await screen.findByRole('radio', { name: 'La de la empresa' })).toBeChecked();
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // Con la ventana cerrada, otra persona la pone en Ceuta, y la lectura siguiente se retiene.
    guardarZona(central().id, 'Africa/Ceuta');
    let abrirLaPuerta = (): void => undefined;
    const puerta = new Promise<void>((resolver) => {
      abrirLaPuerta = resolver;
    });
    servidor.use(
      http.get('/api/v1/organizacion/almacenes/:id', async () => {
        servidorDeAlmacenes.lecturas += 1;
        await puerta;
        const almacen = servidorDeAlmacenes.almacenes.find(({ id }) => id === central().id)!;

        return HttpResponse.json(almacen, { headers: { ETag: etiqueta(versionDe(almacen.id)) } });
      }),
    );

    await usuario.click(cambiar);
    const ventana = await screen.findByRole('dialog');
    await waitFor(() => {
      expect(servidorDeAlmacenes.lecturas).toBe(2);
    });

    // Con la lectura de antes guardada, aquí ya estaría «La de la empresa», marcada.
    expect(within(ventana).queryByRole('radio')).not.toBeInTheDocument();

    abrirLaPuerta();
    expect(await within(ventana).findByRole('radio', { name: 'Ceuta y Melilla' })).toBeChecked();
  });

  it('si otra persona ha guardado antes, lo dice y deja cargar la versión actual', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' }));
    await usuario.click(await screen.findByRole('radio', { name: 'Canarias' }));

    guardarZona(central().id, 'Africa/Ceuta');
    await usuario.click(screen.getByRole('button', { name: 'Guardar la zona' }));

    const ventana = screen.getByRole('dialog');
    const aviso = await within(ventana).findByRole('alert');
    expect(aviso).toHaveTextContent(
      'Alguien ha guardado antes que tú. Vuelve a abrir el formulario para no pisar sus cambios.',
    );

    await usuario.click(within(aviso).getByRole('button', { name: 'Cargar la versión actual' }));

    // La de la otra persona, marcada, y el aviso fuera.
    await waitFor(() => {
      expect(within(ventana).getByRole('radio', { name: 'Ceuta y Melilla' })).toBeChecked();
    });
    expect(within(ventana).queryByRole('alert')).not.toBeInTheDocument();

    await usuario.click(within(ventana).getByRole('radio', { name: 'Canarias' }));
    await usuario.click(within(ventana).getByRole('button', { name: 'Guardar la zona' }));

    expect(await screen.findByText('La zona de ALM-ALFA es ahora Canarias.')).toBeVisible();
    expect(servidorDeAlmacenes.escrituras.map(({ ifMatch }) => ifMatch)).toEqual([
      etiqueta(3),
      etiqueta(4),
    ]);
  });

  it('sin la versión del almacén no enseña el formulario: no habría con qué guardar', async () => {
    const usuario = userEvent.setup();
    servidor.use(
      http.get('/api/v1/organizacion/almacenes/:id', () => HttpResponse.json(central())),
    );
    montar();

    await usuario.click(await screen.findByRole('button', { name: 'Cambiar la zona de ALM-ALFA' }));

    const ventana = screen.getByRole('dialog');
    expect(await within(ventana).findByRole('alert')).toHaveTextContent(
      'No se han podido cargar los datos. Inténtalo de nuevo.',
    );
    expect(within(ventana).queryByRole('radio')).not.toBeInTheDocument();

    await usuario.click(within(ventana).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });
});
