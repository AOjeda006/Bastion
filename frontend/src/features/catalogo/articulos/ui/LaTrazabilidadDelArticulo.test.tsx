import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDeTrazabilidad } from './PaginaDeTrazabilidad.tsx';
import { clavesDeArticulos } from '../api/claves.ts';
import { leerListado } from '../model/listado.ts';
import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { ALFA, PERMISOS_DE_LECTURA, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type ArticuloDto = components['schemas']['ArticuloDto'];

const PATRON = '/articulos/:id/trazabilidad';

/** El primero de Alfa: TOR-M6, un bien sin trazabilidad y con categoría. */
function tornillo(): ArticuloDto {
  const [primero] = articulosDe(ALFA.id).elementos;

  if (primero === undefined) {
    throw new Error('Los datos de prueba se han quedado sin el tornillo de Alfa.');
  }

  return { ...primero };
}

/**
 * La ficha del lado del servidor, con su versión, y lo que ha llegado a cada `PUT`.
 *
 * Aparte del servidor simulado común porque solo la usa esta pantalla, y con la versión como número
 * que sube: la `ETag` que se sirve es la de ahora, y el `If-Match` que vuelve tiene que ser el de
 * la lectura. Es lo único que distingue una pantalla que guarda sobre lo que leyó de una que pisa
 * lo que haya.
 */
const ficha = {
  articulo: tornillo(),
  version: 7,
  /** El rechazo con el que contesta el próximo `PUT`, o `null` para aceptarlo. */
  rechazo: null as { estado: number; codigo: string } | null,
  lecturas: 0,
  escrituras: [] as { ifMatch: string | null; cuerpo: Record<string, unknown> }[],
};

function etiqueta(version: number): string {
  return `"${String(version)}"`;
}

/** Otra persona guarda entre la lectura y el `PUT`: cambia la ficha y sube la versión. */
function guardaOtraPersona(trazabilidad: string): void {
  ficha.articulo = { ...ficha.articulo, trazabilidad };
  ficha.version += 1;
}

beforeEach(() => {
  ficha.articulo = tornillo();
  ficha.version = 7;
  ficha.rechazo = null;
  ficha.lecturas = 0;
  ficha.escrituras = [];

  servidor.use(
    http.get('/api/v1/catalogo/articulos/:id', ({ params }) => {
      ficha.lecturas += 1;

      return params['id'] === ficha.articulo.id
        ? HttpResponse.json(ficha.articulo, { headers: { ETag: etiqueta(ficha.version) } })
        : HttpResponse.json(
            { type: '/errors/articulo-no-encontrado', status: 404 },
            { status: 404 },
          );
    }),

    http.put('/api/v1/catalogo/articulos/:id', async ({ request }) => {
      const cuerpo = (await request.json()) as Record<string, unknown>;
      ficha.escrituras.push({ ifMatch: request.headers.get('If-Match'), cuerpo });

      if (ficha.rechazo !== null) {
        return HttpResponse.json(
          { type: `/errors/${ficha.rechazo.codigo}`, status: ficha.rechazo.estado },
          { status: ficha.rechazo.estado },
        );
      }

      // Como la API: el 200 lleva la ficha y NO lleva `ETag`.
      ficha.articulo = { ...ficha.articulo, trazabilidad: String(cuerpo['trazabilidad']) };
      ficha.version += 1;

      return HttpResponse.json(ficha.articulo);
    }),
  );
});

function montar(idioma: Idioma = 'es'): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...PERMISOS_DE_LECTURA, 'catalogo.articulo.modificar']);

  return montarPantalla(
    <PaginaDeTrazabilidad />,
    `/articulos/${ficha.articulo.id}/trazabilidad`,
    idioma,
    PATRON,
  );
}

/**
 * La pantalla de la trazabilidad (ítem 2.9, ADR-0048): leer la ficha con su versión, cambiar solo
 * la marca, y decir cada rechazo donde toca.
 *
 * Lo que el servidor decide —si el artículo ya tiene movimientos— no se puede comprobar aquí, y por
 * eso lo que se prueba es que el rechazo llega a su campo, con su texto, en los dos idiomas.
 */
describe('La trazabilidad del artículo', () => {
  it('enseña el artículo y la guardada, y la deja marcada', async () => {
    montar();

    expect(await screen.findByText('Tornillo M6 zincado', { exact: false })).toBeVisible();
    expect(screen.getByText('TOR-M6')).toBeVisible();

    const grupo = screen.getByRole('group', { name: 'Trazabilidad' });
    expect(within(grupo).getByRole('radio', { name: 'Ninguna' })).toBeChecked();
    expect(within(grupo).getByRole('radio', { name: 'Por lote' })).not.toBeChecked();
    expect(within(grupo).getByRole('radio', { name: 'Por número de serie' })).not.toBeChecked();

    // La pista va en la descripción del grupo, antes de intentarlo: es lo que para el cambio.
    expect(grupo).toHaveAccessibleDescription(
      'Solo se puede cambiar mientras el artículo no tenga movimientos de stock.',
    );
  });

  it('guarda la marca sobre la versión leída, con el resto de la ficha tal como llegó', async () => {
    const usuario = userEvent.setup();
    const { cache } = montar();

    // Un listado en la caché, para ver que guardar lo deja por viejo: enseña la trazabilidad.
    const sinFiltro = new URLSearchParams();
    const lista = clavesDeArticulos.lista(leerListado(sinFiltro, leerPaginacion(sinFiltro)));
    cache.setQueryData(lista, articulosDe(ALFA.id));

    await usuario.click(await screen.findByRole('radio', { name: 'Por lote' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Guardado. La trazabilidad es ahora «Por lote».',
    );

    const antes = tornillo();
    expect(ficha.escrituras).toEqual([
      {
        ifMatch: etiqueta(7),
        cuerpo: {
          descripcion: antes.descripcion,
          tipo: antes.tipo,
          trazabilidad: 'PorLote',
          impuestoPorDefectoId: antes.impuestoPorDefectoId,
          categoriaId: antes.categoriaId,
        },
      },
    ]);

    // Vuelve a leer la ficha, que trae la versión nueva: guardar otra vez no puede salir con la 7.
    await waitFor(() => {
      expect(ficha.lecturas).toBe(2);
    });
    expect(
      await screen.findByText('Por lote', { selector: 'dd' }),
      'la guardada no se ha repintado con la ficha nueva',
    ).toBeVisible();
    expect(screen.getByRole('radio', { name: 'Por lote' })).toBeChecked();
    expect(cache.getQueryState(lista)?.isInvalidated).toBe(true);

    // Y el aviso de guardado sobrevive a que el formulario se monte de nuevo con la versión 8.
    expect(screen.getByRole('status')).toHaveTextContent('«Por lote»');

    await usuario.click(screen.getByRole('radio', { name: 'Por número de serie' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => {
      expect(ficha.escrituras).toHaveLength(2);
    });
    expect(ficha.escrituras[1]?.ifMatch).toBe(etiqueta(8));
  });

  it.each<[Idioma, string, string]>([
    [
      'es',
      'Trazabilidad',
      'Este artículo ya tiene movimientos de stock, así que su trazabilidad no se puede cambiar.',
    ],
    [
      'en',
      'Traceability',
      'This item already has stock movements, so its traceability can no longer change.',
    ],
  ])(
    '(%s) si ya tiene movimientos, el rechazo va en su campo y en su idioma',
    async (idioma, leyenda, texto) => {
      const usuario = userEvent.setup();
      ficha.rechazo = { estado: 409, codigo: 'articulo-trazabilidad-con-movimientos' };
      montar(idioma);

      const grupo = await screen.findByRole('group', { name: leyenda });
      await usuario.click(within(grupo).getAllByRole('radio')[1]!);
      await usuario.click(
        screen.getByRole('button', { name: idioma === 'es' ? 'Guardar' : 'Save' }),
      );

      // En el campo: dentro del grupo, anunciado y en su descripción. Y SOLO ahí: el aviso general
      // de arriba es para lo que no es de ningún campo.
      const alerta = await within(grupo).findByRole('alert');
      expect(alerta).toHaveTextContent(texto);
      expect(screen.getAllByRole('alert')).toEqual([alerta]);
      expect(grupo).toHaveAccessibleDescription(expect.stringContaining(texto));

      // Lo elegido no se pierde, y no se ha guardado nada.
      expect(within(grupo).getAllByRole('radio')[1]).toBeChecked();
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    },
  );

  it('si otra persona ha guardado antes, lo dice arriba y deja cargar la versión actual', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(await screen.findByRole('radio', { name: 'Por lote' }));

    guardaOtraPersona('PorNumeroSerie');
    ficha.rechazo = { estado: 412, codigo: 'version-obsoleta' };
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(
      'Alguien ha guardado antes que tú. Vuelve a abrir el formulario para no pisar sus cambios.',
    );
    // Arriba y no en el campo: no es de la trazabilidad elegida.
    expect(
      within(screen.getByRole('group', { name: 'Trazabilidad' })).queryByRole('alert'),
    ).not.toBeInTheDocument();

    ficha.rechazo = null;
    await usuario.click(within(aviso).getByRole('button', { name: 'Cargar la versión actual' }));

    // La de la otra persona, marcada, y el aviso fuera.
    expect(await screen.findByText('Por número de serie', { selector: 'dd' })).toBeVisible();
    expect(screen.getByRole('radio', { name: 'Por número de serie' })).toBeChecked();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('radio', { name: 'Por lote' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByRole('status')).toHaveTextContent('«Por lote»');
    expect(ficha.escrituras.map((escritura) => escritura.ifMatch)).toEqual([
      etiqueta(7),
      etiqueta(8),
    ]);
  });

  it('una guardada que no reconoce se dice, no se marca, y sin elegir no se manda', async () => {
    const usuario = userEvent.setup();
    ficha.articulo = { ...ficha.articulo, trazabilidad: 'PorPeso' };
    montar();

    // La explicación no vive en un `title`, que no llega ni al teclado ni al tacto: está escrita
    // debajo de la ficha, y es la descripción accesible del valor.
    const porQue =
      'Esta versión de la pantalla no sabe interpretar la trazabilidad que ha llegado. Avisa a ' +
      'quien administre Bastion.';
    const actual = await screen.findByRole('definition', { description: porQue });
    expect(actual).toHaveTextContent('Sin reconocer');
    expect(actual).not.toHaveTextContent('PorPeso');
    expect(screen.getByText(porQue)).toBeVisible();

    const grupo = screen.getByRole('group', { name: 'Trazabilidad' });
    for (const opcion of within(grupo).getAllByRole('radio')) {
      expect(opcion).not.toBeChecked();
    }

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await within(grupo).findByRole('alert')).toHaveTextContent('Elige una de las tres.');
    expect(ficha.escrituras).toEqual([]);
  });

  it('sin la versión de la ficha no enseña el formulario: no habría con qué guardar', async () => {
    servidor.use(
      http.get('/api/v1/catalogo/articulos/:id', () => HttpResponse.json(ficha.articulo)),
    );
    montar();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se han podido cargar los datos. Inténtalo de nuevo.',
    );
    expect(screen.queryByRole('group')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument();
  });

  it('un artículo que no está dice por qué, y deja volver al listado', async () => {
    abrirSesionYaRecuperada(ALFA.id, [...PERMISOS_DE_LECTURA, 'catalogo.articulo.modificar']);
    montarPantalla(
      <PaginaDeTrazabilidad />,
      '/articulos/fffffff9-0000-0000-0000-000000000009/trazabilidad',
      'es',
      PATRON,
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ese artículo ya no existe. Vuelve al listado y actualiza.',
    );
    expect(screen.getByRole('link', { name: 'Volver a los artículos' })).toHaveAttribute(
      'href',
      '/articulos',
    );
  });
});
