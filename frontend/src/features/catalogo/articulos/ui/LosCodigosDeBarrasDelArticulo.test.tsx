import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { PaginaDeCodigosDeBarras } from './PaginaDeCodigosDeBarras.tsx';
import type { Idioma } from '@/app/i18n/idioma.ts';
import type { components } from '@/shared/api/esquema.ts';
import { ALFA, PERMISOS_DE_LECTURA, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';

type ArticuloDto = components['schemas']['ArticuloDto'];
type CodigoBarrasDto = components['schemas']['CodigoBarrasDto'];

const PATRON = '/articulos/:id/gtin';

const PUEDE_TODO = [
  ...PERMISOS_DE_LECTURA,
  'catalogo.codigo-barras.agregar',
  'catalogo.codigo-barras.quitar',
];

/** El primero de Alfa: TOR-M6. */
function tornillo(): ArticuloDto {
  const [primero] = articulosDe(ALFA.id).elementos;

  if (primero === undefined) {
    throw new Error('Los datos de prueba se han quedado sin el tornillo de Alfa.');
  }

  return { ...primero };
}

const ARTICULO = tornillo();

function codigo(
  numero: number,
  gtin: string,
  nivel: string,
  unidades: number | string,
): CodigoBarrasDto {
  return {
    id: `cccccccc-0000-0000-0000-${String(numero).padStart(12, '0')}`,
    empresaId: ALFA.id,
    articuloId: ARTICULO.id,
    gtin,
    nivel,
    unidades,
  };
}

/** La base y una caja de doce. */
function losDeSiempre(): CodigoBarrasDto[] {
  return [
    codigo(1, '00036000291452', 'Base', 1),
    // Como texto, que el contrato lo admite: el número sale igual.
    codigo(2, '10036000291459', 'Caja', '12'),
  ];
}

/** Como la API: por unidades, y a igualdad, por GTIN y por identificador. */
function enSuOrden(codigos: readonly CodigoBarrasDto[]): CodigoBarrasDto[] {
  const comparar = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

  return [...codigos].sort(
    (a, b) =>
      Number(a.unidades) - Number(b.unidades) || comparar(a.gtin, b.gtin) || comparar(a.id, b.id),
  );
}

/**
 * Los códigos del lado del servidor, y lo que ha llegado a cada alta y a cada baja.
 *
 * Aparte del servidor simulado común porque solo los usa esta pantalla. La lista es la de su
 * artículo y en el orden de la API; el alta rellena a catorce cifras, como la API, y la baja pide el
 * `If-Match` de la lectura de la fila.
 */
const servidorDeCodigos = {
  codigos: [] as CodigoBarrasDto[],
  /** Si la lista contesta un 500, para ver qué pasa cuando volver a leerla falla. */
  falloDeLaLista: false,
  /** El rechazo con el que contesta la próxima alta, o `null` para aceptarla. */
  rechazoDelAlta: null as { estado: number; codigo: string } | null,
  altas: [] as { clave: string | null; cuerpo: Record<string, unknown> }[],
  lecturasDeUno: [] as string[],
  bajas: [] as { id: string; ifMatch: string | null }[],
  siguiente: 10,
};

beforeEach(() => {
  servidorDeCodigos.codigos = losDeSiempre();
  servidorDeCodigos.falloDeLaLista = false;
  servidorDeCodigos.rechazoDelAlta = null;
  servidorDeCodigos.altas = [];
  servidorDeCodigos.lecturasDeUno = [];
  servidorDeCodigos.bajas = [];
  servidorDeCodigos.siguiente = 10;

  servidor.use(
    http.get('/api/v1/catalogo/articulos/:id', ({ params }) =>
      params['id'] === ARTICULO.id
        ? HttpResponse.json(ARTICULO, { headers: { ETag: '"7"' } })
        : HttpResponse.json(
            { type: '/errors/articulo-no-encontrado', status: 404 },
            { status: 404 },
          ),
    ),

    http.get('/api/v1/catalogo/articulos/:articuloId/gtins', ({ params }) => {
      if (params['articuloId'] !== ARTICULO.id) {
        return HttpResponse.json(
          { type: '/errors/articulo-no-encontrado', status: 404 },
          { status: 404 },
        );
      }

      return servidorDeCodigos.falloDeLaLista
        ? HttpResponse.json({ type: '/errors/error-interno', status: 500 }, { status: 500 })
        : HttpResponse.json(enSuOrden(servidorDeCodigos.codigos));
    }),

    http.post('/api/v1/catalogo/articulos/:articuloId/gtins', async ({ request }) => {
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDeCodigos.altas.push({ clave: request.headers.get('Idempotency-Key'), cuerpo });

      if (servidorDeCodigos.rechazoDelAlta !== null) {
        const { estado, codigo: tipo } = servidorDeCodigos.rechazoDelAlta;
        return HttpResponse.json({ type: `/errors/${tipo}`, status: estado }, { status: estado });
      }

      const nivel = String(cuerpo['nivel']);
      const nuevo = codigo(
        servidorDeCodigos.siguiente,
        String(cuerpo['gtin']).padStart(14, '0'),
        nivel,
        nivel === 'Base' ? 1 : Number(cuerpo['unidades']),
      );
      servidorDeCodigos.siguiente += 1;
      servidorDeCodigos.codigos = [...servidorDeCodigos.codigos, nuevo];

      // Como la API: un 201 con su `Location` y sin ETag, que la da la lectura de la fila.
      return HttpResponse.json(nuevo, {
        status: 201,
        headers: { Location: `/api/v1/catalogo/articulos/gtins/${nuevo.id}` },
      });
    }),

    http.get('/api/v1/catalogo/articulos/gtins/:id', ({ params }) => {
      const id = String(params['id']);
      servidorDeCodigos.lecturasDeUno.push(id);
      const fila = servidorDeCodigos.codigos.find((c) => c.id === id);

      return fila === undefined
        ? HttpResponse.json(
            { type: '/errors/codigo-barras-no-encontrado', status: 404 },
            { status: 404 },
          )
        : HttpResponse.json(fila, { headers: { ETag: '"3"' } });
    }),

    http.delete('/api/v1/catalogo/articulos/gtins/:id', ({ params, request }) => {
      const id = String(params['id']);
      servidorDeCodigos.bajas.push({ id, ifMatch: request.headers.get('If-Match') });
      servidorDeCodigos.codigos = servidorDeCodigos.codigos.filter((c) => c.id !== id);

      return new HttpResponse(null, { status: 204 });
    }),
  );
});

function montar(
  idioma: Idioma = 'es',
  permisos: readonly string[] = PUEDE_TODO,
): ReturnType<typeof montarPantalla> {
  abrirSesionYaRecuperada(ALFA.id, [...permisos]);

  return montarPantalla(
    <PaginaDeCodigosDeBarras />,
    `/articulos/${ARTICULO.id}/gtin`,
    idioma,
    PATRON,
  );
}

/** Las filas de la tabla, celda a celda y sin la cabecera. */
function filas(): string[][] {
  const tabla = screen.getByRole('table', { name: 'Códigos de barras de TOR-M6' });

  return within(tabla)
    .getAllByRole('row')
    .slice(1)
    .map((fila) =>
      within(fila)
        .getAllByRole('cell')
        .slice(0, 3)
        .map((celda) => celda.textContent),
    );
}

/**
 * La pantalla de los códigos de barras (ítem 2.10, ADR-0051): los que tiene el artículo, el alta con
 * lo que el formulario puede decir solo, y la baja confirmada.
 *
 * Lo que solo sabe el servidor —la tabla de prefijos, el duplicado— no se puede comprobar aquí: lo
 * que se prueba es que su rechazo llega al campo, con su texto, en los dos idiomas.
 */
describe('Los códigos de barras del artículo', () => {
  it('enseña el artículo y sus códigos, con el nivel y las unidades', async () => {
    montar();

    expect(await screen.findByText('Tornillo M6 zincado', { exact: false })).toBeVisible();
    expect(filas()).toEqual([
      ['00036000291452', 'Unidad base', '1'],
      ['10036000291459', 'Caja', '12'],
    ]);
    expect(screen.getByRole('link', { name: 'Volver a los artículos' })).toHaveAttribute(
      'href',
      '/articulos',
    );
  });

  it('sin ninguno, lo dice en vez de enseñar una tabla vacía', async () => {
    servidorDeCodigos.codigos = [];
    montar();

    expect(
      await screen.findByText('Este artículo todavía no tiene ningún código de barras.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  // `constructor` porque un objeto lo hereda: un registro consultado con `??` lo daría por bueno.
  it.each(['Bandeja', 'constructor'])(
    'un nivel que esta versión no conoce («%s») se dice, con su explicación',
    async (nivel) => {
      servidorDeCodigos.codigos = [codigo(1, '00036000291452', nivel, 6)];
      montar();

      // La explicación no vive en un `title`, que no llega ni al teclado ni al tacto: está
      // escrita debajo de la tabla, y es la descripción accesible de la celda.
      const porQue =
        'Esta versión de la pantalla no sabe interpretar el nivel que ha llegado. Avisa a quien ' +
        'administre Bastion.';
      expect(
        await screen.findByRole('cell', { name: 'Sin reconocer', description: porQue }),
      ).toBeVisible();
      expect(screen.getByText(porQue)).toBeVisible();
      expect(screen.queryByText(nivel)).not.toBeInTheDocument();
    },
  );

  it('da de alta una base sin mandar unidades, con su clave, y la enseña rellenada', async () => {
    const usuario = userEvent.setup();
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    expect(campo).toHaveAccessibleDescription('Las 8, 12, 13 o 14 cifras que van bajo las barras.');
    // La base viene elegida, y las unidades no se preguntan.
    expect(screen.getByRole('group', { name: 'Nivel' })).toHaveAccessibleDescription(
      'Dónde va impreso: en la unidad que se vende suelta, en la caja o en el palé.',
    );
    expect(screen.getByRole('radio', { name: 'Unidad base' })).toBeChecked();
    expect(
      screen.queryByRole('textbox', { name: 'Unidades base que contiene' }),
    ).not.toBeInTheDocument();

    await usuario.type(campo, ' 4006381333931 ');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Dado de alta el GTIN 04006381333931.');
    });
    expect(servidorDeCodigos.altas).toEqual([
      {
        clave: expect.stringMatching(/^[0-9a-f-]{36}$/) as unknown,
        cuerpo: { gtin: '4006381333931', nivel: 'Base' },
      },
    ]);

    // La lista se lee otra vez, y el formulario se monta de nuevo: vacío y con el foco para el
    // siguiente.
    await waitFor(() => {
      expect(filas()).toContainEqual(['04006381333931', 'Unidad base', '1']);
    });
    const siguiente = screen.getByRole('textbox', { name: 'GTIN' });
    expect(siguiente).toHaveValue('');
    await waitFor(() => {
      expect(siguiente).toHaveFocus();
    });

    // Y sin el «ya se envió» del alta anterior: la primera cifra del siguiente no se valida todavía.
    await usuario.type(siguiente, '4');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(siguiente).toHaveAttribute('aria-invalid', 'false');
  });

  it('una caja pide sus unidades, y las manda como número', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.type(await screen.findByRole('textbox', { name: 'GTIN' }), '10012345678902');
    await usuario.click(screen.getByRole('radio', { name: 'Caja' }));

    const unidades = screen.getByRole('textbox', { name: 'Unidades base que contiene' });
    expect(unidades).toHaveAccessibleDescription('Dos o más. Si cambian, es otro GTIN.');

    // Sin unidades, o con una, no sale: lo dice el campo.
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));
    expect(await screen.findByText(/Una caja o un palé llevan las unidades base/)).toBeVisible();
    expect(unidades).toHaveAttribute('aria-invalid', 'true');

    await usuario.type(unidades, '1');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));
    expect(servidorDeCodigos.altas).toEqual([]);

    await usuario.clear(unidades);
    await usuario.type(unidades, '24');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('10012345678902');
    });
    expect(servidorDeCodigos.altas.map((alta) => alta.cuerpo)).toEqual([
      { gtin: '10012345678902', nivel: 'Caja', unidades: 24 },
    ]);
  });

  it.each([
    ['4006381333932', 'La última cifra no cuadra con las demás'],
    ['40063813339', 'Un GTIN tiene 8, 12, 13 o 14 cifras'],
    ['', 'Un GTIN tiene 8, 12, 13 o 14 cifras'],
    ['4006381-333931', 'Un GTIN son solo cifras'],
    ['40063813339３１', 'Un GTIN son solo cifras'],
  ])('«%s» lo para el formulario, en el campo, sin ir al servidor', async (gtin, texto) => {
    const usuario = userEvent.setup();
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    if (gtin !== '') {
      await usuario.type(campo, gtin);
    }
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(texto);
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo).toHaveAccessibleDescription(expect.stringContaining(texto));
    expect(campo).toHaveFocus();
    expect(servidorDeCodigos.altas).toEqual([]);
  });

  it.each<[Idioma, string, string, string]>([
    [
      'es',
      'Dar de alta',
      'GTIN',
      'Ese GTIN ya lo lleva un artículo de esta empresa. Si ahora es de otro, quítalo antes del que lo lleva.',
    ],
    [
      'en',
      'Add',
      'GTIN',
      'An item at this company already carries that GTIN. If it now belongs to another one, remove it from the first.',
    ],
  ])(
    '(%s) el duplicado lo dice el servidor, y va en el campo y en su idioma',
    async (idioma, boton, nombreDelCampo, texto) => {
      const usuario = userEvent.setup();
      servidorDeCodigos.rechazoDelAlta = { estado: 409, codigo: 'codigo-barras-duplicado' };
      montar(idioma);

      const campo = await screen.findByRole('textbox', { name: nombreDelCampo });
      await usuario.type(campo, '4006381333931');
      await usuario.click(screen.getByRole('button', { name: boton }));

      const alerta = await screen.findByRole('alert');
      expect(alerta).toHaveTextContent(texto);
      // En el campo y SOLO ahí: el aviso de arriba es para lo que no es de ningún campo.
      expect(screen.getAllByRole('alert')).toEqual([alerta]);
      expect(campo).toHaveAccessibleDescription(expect.stringContaining(texto));
      expect(campo).toHaveFocus();
      expect(campo).toHaveValue('4006381333931');
      expect(screen.getByRole('status')).toBeEmptyDOMElement();
    },
  );

  // Los números son los de `ElGtinTests`: cuadran, y es la tabla de prefijos la que los para.
  it.each([
    ['2012345678903', 'gtin-circulacion-restringida', 'Es un número de circulación restringida'],
    ['9901234567899', 'gtin-cupon', 'Es el número de un cupón o de un vale de devolución'],
    ['9511234567890', 'gtin-sin-asignar', 'Ese prefijo está reservado'],
    ['98412345678908', 'gtin-medida-variable', 'Es un código de peso o medida variable'],
  ])('«%s» lo para el servidor (%s), y va en el campo', async (gtin, tipo, texto) => {
    const usuario = userEvent.setup();
    servidorDeCodigos.rechazoDelAlta = { estado: 400, codigo: tipo };
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    await usuario.type(campo, gtin);
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent(texto);
    expect(screen.getAllByRole('alert')).toEqual([alerta]);
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo).toHaveAccessibleDescription(expect.stringContaining(texto));
    expect(campo).toHaveFocus();
    expect(servidorDeCodigos.altas).toHaveLength(1);
  });

  it('las unidades que rechaza el servidor van en su campo', async () => {
    const usuario = userEvent.setup();
    servidorDeCodigos.rechazoDelAlta = { estado: 400, codigo: 'codigo-barras-unidades-no-validas' };
    montar();

    await usuario.type(await screen.findByRole('textbox', { name: 'GTIN' }), '10012345678902');
    await usuario.click(screen.getByRole('radio', { name: 'Caja' }));
    const unidades = screen.getByRole('textbox', { name: 'Unidades base que contiene' });
    await usuario.type(unidades, '24');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /Una caja o un palé llevan las unidades base/,
    );
    expect(unidades).toHaveAttribute('aria-invalid', 'true');
    expect(unidades).toHaveAccessibleDescription(
      expect.stringContaining('Una caja o un palé llevan las unidades base'),
    );
    expect(unidades).toHaveFocus();
  });

  it('el nivel que rechaza el servidor va en la descripción del grupo, con el foco en él', async () => {
    const usuario = userEvent.setup();
    servidorDeCodigos.rechazoDelAlta = { estado: 400, codigo: 'codigo-barras-nivel-no-valido' };
    montar();

    await usuario.type(await screen.findByRole('textbox', { name: 'GTIN' }), '4006381333931');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    const texto = 'Un código de barras va en la unidad base, en una caja o en un palé.';
    expect(await screen.findByRole('alert')).toHaveTextContent(texto);
    expect(screen.getByRole('group', { name: 'Nivel' })).toHaveAccessibleDescription(
      expect.stringContaining(texto),
    );
    expect(screen.getByRole('radio', { name: 'Unidad base' })).toHaveFocus();
  });

  it('lo que no es de un campo va arriba, y el campo queda como estaba', async () => {
    const usuario = userEvent.setup();
    servidorDeCodigos.rechazoDelAlta = { estado: 409, codigo: 'idempotencia-cuerpo-distinto' };
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    await usuario.type(campo, '4006381333931');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Se ha repetido una operación con los mismos datos de envío pero distinto contenido.',
    );
    expect(campo).toHaveAttribute('aria-invalid', 'false');
    expect(campo).toHaveAccessibleDescription('Las 8, 12, 13 o 14 cifras que van bajo las barras.');
  });

  it('repetir lo mismo repite la clave; cambiarlo la estrena; y tras el alta, otra', async () => {
    const usuario = userEvent.setup();
    servidorDeCodigos.rechazoDelAlta = { estado: 500, codigo: 'error-interno' };
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    const enviar = screen.getByRole('button', { name: 'Dar de alta' });

    await usuario.type(campo, '4006381333931');
    await usuario.click(enviar);
    await screen.findByRole('alert');
    await usuario.click(enviar);
    await waitFor(() => {
      expect(servidorDeCodigos.altas).toHaveLength(2);
    });

    await usuario.clear(campo);
    await usuario.type(campo, '96385074');
    await usuario.click(enviar);
    await waitFor(() => {
      expect(servidorDeCodigos.altas).toHaveLength(3);
    });

    servidorDeCodigos.rechazoDelAlta = null;
    await usuario.click(enviar);
    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Dado de alta el GTIN 00000096385074.');
    });

    // El formulario de después es otro: el campo y el botón se buscan de nuevo.
    await usuario.type(screen.getByRole('textbox', { name: 'GTIN' }), '96385074');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));
    await waitFor(() => {
      expect(servidorDeCodigos.altas).toHaveLength(5);
    });

    const [primera, segunda, tercera, cuarta, quinta] = servidorDeCodigos.altas.map(
      (alta) => alta.clave,
    );
    expect(segunda, 'el reintento de lo mismo no repite la clave').toBe(primera);
    expect(tercera, 'otro cuerpo con la clave del anterior').not.toBe(primera);
    expect(cuarta, 'el reintento que sale bien no repite la clave').toBe(tercera);
    expect(quinta, 'un alta nueva tras la que salió bien repite la clave').not.toBe(cuarta);
  });

  it('quitar se confirma en la fila: cancelar no manda nada y devuelve el foco', async () => {
    const usuario = userEvent.setup();
    montar();

    const quitar = await screen.findByRole('button', { name: 'Quitar el GTIN 10036000291459' });
    expect(quitar).toHaveTextContent('Quitar');
    await usuario.click(quitar);

    const pregunta = screen.getByRole('group', {
      name: '¿Quitar el GTIN 10036000291459 de este artículo?',
    });
    expect(within(pregunta).getByRole('button', { name: 'Cancelar' })).toHaveFocus();

    await usuario.click(within(pregunta).getByRole('button', { name: 'Cancelar' }));
    expect(screen.queryByRole('group', { name: /¿Quitar/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Quitar el GTIN 10036000291459' })).toHaveFocus();

    // Escape también la cierra.
    await usuario.click(screen.getByRole('button', { name: 'Quitar el GTIN 10036000291459' }));
    await usuario.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: /¿Quitar/ })).not.toBeInTheDocument();

    expect(servidorDeCodigos.lecturasDeUno).toEqual([]);
    expect(servidorDeCodigos.bajas).toEqual([]);
  });

  it('confirmado, lee la versión de la fila y la manda en el If-Match', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(
      await screen.findByRole('button', { name: 'Quitar el GTIN 10036000291459' }),
    );
    await usuario.click(screen.getByRole('button', { name: 'Sí, quitarlo' }));

    const aviso = await screen.findByText('Quitado el GTIN 10036000291459.');
    expect(aviso).toHaveFocus();
    expect(screen.getByRole('status')).toContainElement(aviso);

    const id = losDeSiempre()[1]!.id;
    expect(servidorDeCodigos.lecturasDeUno).toEqual([id]);
    expect(servidorDeCodigos.bajas).toEqual([{ id, ifMatch: '"3"' }]);

    await waitFor(() => {
      expect(filas()).toEqual([['00036000291452', 'Unidad base', '1']]);
    });
  });

  it('si alguien la quitó antes, lo dice arriba y la lista deja de ofrecerla', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(
      await screen.findByRole('button', { name: 'Quitar el GTIN 10036000291459' }),
    );
    servidorDeCodigos.codigos = servidorDeCodigos.codigos.slice(0, 1);
    await usuario.click(screen.getByRole('button', { name: 'Sí, quitarlo' }));

    // El foco va al aviso: el botón que lo tenía se ha cerrado, y su fila va a desaparecer.
    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent(
      'Ese código de barras ya no está en este artículo: alguien lo ha quitado antes.',
    );
    expect(alerta).toHaveFocus();
    expect(servidorDeCodigos.bajas).toEqual([]);
    await waitFor(() => {
      expect(filas()).toEqual([['00036000291452', 'Unidad base', '1']]);
    });
  });

  it('si volver a leer la lista falla, lo ya enseñado se queda, con el fallo encima', async () => {
    const usuario = userEvent.setup();
    montar();

    const campo = await screen.findByRole('textbox', { name: 'GTIN' });
    servidorDeCodigos.falloDeLaLista = true;
    await usuario.type(campo, '4006381333931');
    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }));

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('El servidor no ha podido responder. Inténtalo de nuevo.');
    expect(screen.getByRole('status')).toHaveTextContent('Dado de alta el GTIN 04006381333931.');
    expect(filas()).toEqual([
      ['00036000291452', 'Unidad base', '1'],
      ['10036000291459', 'Caja', '12'],
    ]);

    servidorDeCodigos.falloDeLaLista = false;
    await usuario.click(within(alerta).getByRole('button', { name: 'Volver a intentarlo' }));

    await waitFor(() => {
      expect(filas()).toEqual([
        ['00036000291452', 'Unidad base', '1'],
        ['04006381333931', 'Unidad base', '1'],
        ['10036000291459', 'Caja', '12'],
      ]);
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('quien solo ve, ve la lista y nada más', async () => {
    montar('es', PERMISOS_DE_LECTURA);

    expect(await screen.findByText('00036000291452')).toBeVisible();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Quitar/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Acciones' })).not.toBeInTheDocument();
  });

  it('cada permiso da lo suyo: el de dar de alta no deja quitar, ni al revés', async () => {
    const { desmontar } = montarConDesmontaje([
      ...PERMISOS_DE_LECTURA,
      'catalogo.codigo-barras.agregar',
    ]);

    expect(await screen.findByRole('textbox', { name: 'GTIN' })).toBeVisible();
    expect(screen.queryByRole('button', { name: /Quitar/ })).not.toBeInTheDocument();
    desmontar();

    montarConDesmontaje([...PERMISOS_DE_LECTURA, 'catalogo.codigo-barras.quitar']);

    expect(
      await screen.findByRole('button', { name: 'Quitar el GTIN 00036000291452' }),
    ).toBeVisible();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('un artículo que no está dice por qué, y deja volver al listado', async () => {
    abrirSesionYaRecuperada(ALFA.id, PUEDE_TODO);
    montarPantalla(
      <PaginaDeCodigosDeBarras />,
      '/articulos/fffffff9-0000-0000-0000-000000000009/gtin',
      'es',
      PATRON,
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ese artículo ya no existe. Vuelve al listado y actualiza.',
    );
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Volver a los artículos' })).toBeVisible();
  });
});

function montarConDesmontaje(permisos: readonly string[]): { desmontar: () => void } {
  const montaje = montar('es', permisos);

  return { desmontar: montaje.unmount };
}
