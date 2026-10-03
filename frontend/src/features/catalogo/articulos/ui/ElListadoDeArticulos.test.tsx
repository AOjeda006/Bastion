import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, PERMISOS_DE_LECTURA, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { PaginaDeArticulos } from './PaginaDeArticulos.tsx';

/** La rama «Tornillería», que en los datos de prueba tiene un artículo y cuelga de «Ferretería». */
const TORNILLERIA = 'eeeeeee1-0000-0000-0000-000000000002';

/** Por qué sale «Sin reconocer» en cada columna: escrito debajo de la tabla, y descripción de la celda. */
const POR_QUE_EL_TIPO =
  'Esta versión de la pantalla no sabe interpretar el tipo que ha llegado. Avisa a quien ' +
  'administre Bastion.';
const POR_QUE_LA_TRAZABILIDAD =
  'Esta versión de la pantalla no sabe interpretar la trazabilidad que ha llegado. Avisa a quien ' +
  'administre Bastion.';

/**
 * La pantalla de artículos: sus tres estados, sus dos filtros y lo que este ítem estrena.
 *
 * Lo de siempre —cargando, error con salida, vacío con motivo y los filtros en la URL— y dos cosas
 * que son de aquí: **el filtro por rama**, que se pone desde el árbol y se anuncia con su nombre, y
 * **el tipo del artículo**, que viaja como texto y por tanto puede llegar con un valor que esta
 * versión no conozca. Y desde el ítem 2.9, **la trazabilidad**, con el mismo cuidado y con el enlace
 * a su pantalla para quien puede cambiarla. Desde el 2.10, el enlace a los códigos de barras.
 */
describe('El listado de artículos', () => {
  it('mientras llegan, dice que está cargando', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/articulos', async () => {
        await delay(80);
        return HttpResponse.json(articulosDe(ALFA.id));
      }),
    );

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    expect(await screen.findByText('Cargando los artículos…')).toBeVisible();
    expect(await screen.findByText('Tornillo M6 zincado')).toBeVisible();

    // Con todo reconocido, no se explica nada.
    expect(screen.queryByText(/no sabe interpretar/)).not.toBeInTheDocument();
  });

  it('si el servidor falla, lo dice y deja volver a intentarlo', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorSimulado.falloDeArticulos = 500;

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('El servidor no ha podido responder. Inténtalo de nuevo.');

    // Y la salida existe de verdad: no es un botón decorativo, vuelve a pedir. El recuadro de
    // filtro sigue en pie mientras tanto, que es lo que permite reintentar con otro criterio.
    expect(screen.getByRole('searchbox')).toBeVisible();

    servidorSimulado.falloDeArticulos = null;
    await usuario.click(screen.getByRole('button', { name: 'Volver a intentarlo' }));

    expect(await screen.findByText('Tornillo M6 zincado')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin artículos, dice QUÉ está vacío y no «sin datos»', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/articulos', () =>
        HttpResponse.json({ elementos: [], pagina: 1, tamanio: 20, total: 0 }),
      ),
    );

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    expect(
      await screen.findByText('Todavía no hay ningún artículo dado de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('vacío POR EL FILTRO no se dice igual que vacío del todo, y nombra el filtro', async () => {
    abrirSesionYaRecuperada();

    // La distinción no es cosmética: «no hay ninguno» manda a dar de alta un artículo que sí
    // existe —con el código ocupado, y por tanto con un 409 esperándole— mientras que «ninguno
    // coincide con esto» manda a probar otra palabra.
    montarPantalla(<PaginaDeArticulos />, '/articulos?busqueda=Loquesea');

    expect(await screen.findByText('Ningún artículo coincide con «Loquesea».')).toBeVisible();
    expect(
      screen.queryByText('Todavía no hay ningún artículo dado de alta en esta empresa.'),
    ).not.toBeInTheDocument();
  });

  it('vacío POR LA RAMA avisa de que el subárbol no cuenta', async () => {
    abrirSesionYaRecuperada();

    // «Ferretería» no tiene artículos suyos: los tiene «Tornillería», que cuelga de ella. El
    // acotado es por la categoría dicha y NO por su subárbol —consecuencia directa de la lista de
    // adyacencia—, así que la pantalla lo dice en vez de dejar que parezca un fallo.
    montarPantalla(
      <PaginaDeArticulos />,
      '/articulos?categoria=eeeeeee1-0000-0000-0000-000000000001',
    );

    expect(
      await screen.findByText(
        'Esta categoría no tiene ningún artículo. Los de las categorías que cuelgan de ella no ' +
          'salen aquí: míralas una a una.',
      ),
    ).toBeVisible();
  });

  it('filtrar lo escribe EN LA URL, y el criterio llega al servidor', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(<PaginaDeArticulos />, '/articulos');

    expect(await screen.findByText('Mano de obra de taller')).toBeVisible();

    await usuario.type(screen.getByRole('searchbox'), 'TOR-M6');
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByText('Tornillo M6 zincado')).toBeVisible();
    await waitFor(() => {
      expect(screen.queryByText('Mano de obra de taller')).not.toBeInTheDocument();
    });

    // Lo que distingue esto de un `useState`: el filtro está en la ubicación, así que el listado
    // filtrado se puede pegar en un correo y la flecha de atrás lo deshace.
    expect(enrutador.state.location.search).toBe('?busqueda=TOR-M6');

    // Y llega al servidor. Sin esto, una pantalla que se trajera todo y filtrara en el navegador
    // pintaría exactamente lo mismo — hasta el día en que hay tres mil artículos.
    expect(servidorSimulado.articulosPedidos).toEqual([
      { busqueda: '', categoria: null },
      { busqueda: 'TOR-M6', categoria: null },
    ]);
  });

  it('entrando por un enlace del árbol, se abre filtrado por la rama Y CON SU NOMBRE', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeArticulos />, `/articulos?categoria=${TORNILLERIA}`);

    expect(await screen.findByText('Tornillo M6 zincado')).toBeVisible();
    expect(screen.queryByText('Mano de obra de taller')).not.toBeInTheDocument();

    // El nombre y no el identificador: un `uuid` en pantalla no le dice a nadie por qué está
    // viendo tres filas en vez de treinta.
    expect(await screen.findByText('Filtrando por la categoría «Tornillería».')).toBeVisible();
    expect(servidorSimulado.articulosPedidos).toEqual([{ busqueda: '', categoria: TORNILLERIA }]);
  });

  it('quitar el filtro de rama lo borra de la URL y devuelve el listado entero', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(
      <PaginaDeArticulos />,
      `/articulos?categoria=${TORNILLERIA}&pagina=2`,
    );

    await screen.findByText('Filtrando por la categoría «Tornillería».');

    await usuario.click(screen.getByRole('button', { name: 'Quitar el filtro de categoría' }));

    expect(await screen.findByText('Mano de obra de taller')).toBeVisible();

    // La página también se va: quedarse en la segunda al quitar el filtro enseña una página vacía
    // de un resultado que sí tiene filas.
    expect(enrutador.state.location.search).toBe('');
  });

  it('una categoría con forma inválida en la URL se ignora, no se le manda al servidor', async () => {
    abrirSesionYaRecuperada();

    // Lo que viene de la barra de direcciones viene de fuera. Un identificador mal escrito no es un
    // error que enseñar, es ruido: se cae a «sin filtro», igual que un `?pagina=-4`.
    montarPantalla(<PaginaDeArticulos />, '/articulos?categoria=no-es-un-identificador');

    expect(await screen.findByText('Mano de obra de taller')).toBeVisible();
    expect(servidorSimulado.articulosPedidos).toEqual([{ busqueda: '', categoria: null }]);
    expect(screen.queryByText('Filtrando por una categoría.')).not.toBeInTheDocument();
  });

  it('sin permiso para ver categorías, el filtro se anuncia igual pero sin pedir el nombre', async () => {
    let preguntas = 0;
    servidor.use(
      http.get('/api/v1/catalogo/categorias/:id', () => {
        preguntas += 1;
        return new HttpResponse(null, { status: 403 });
      }),
    );

    abrirSesionYaRecuperada(ALFA.id, ['catalogo.articulo.ver']);

    montarPantalla(<PaginaDeArticulos />, `/articulos?categoria=${TORNILLERIA}`);

    // Las dos mitades. Que la tabla salga —no depende de resolver un nombre— y que el filtro se
    // diga aunque no se pueda nombrar: callarlo dejaría menos filas de las que hay sin explicación.
    expect(await screen.findByText('Tornillo M6 zincado')).toBeVisible();
    expect(screen.getByText('Filtrando por una categoría.')).toBeVisible();

    // Y no se ha pedido lo que se sabía que iba a contestar 403.
    expect(preguntas).toBe(0);
  });

  it('un tipo de artículo que esta versión no conoce NO se da por bueno', async () => {
    abrirSesionYaRecuperada();

    // El enumerado viaja como texto y el frontal se despliega aparte del backend, así que un valor
    // nuevo llega antes de que esta pantalla lo conozca. Lo que no se puede interpretar se dice: ni
    // «Bien», ni la celda en blanco —que es lo que se ve cuando algo se rompe— sino dicho.
    servidor.use(
      http.get('/api/v1/catalogo/articulos', () => {
        const pagina = articulosDe(ALFA.id);
        const [primero, ...resto] = pagina.elementos;

        return HttpResponse.json({
          ...pagina,
          elementos: [{ ...primero!, tipo: 'Kit' }, ...resto],
        });
      }),
    );

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const fila = (await screen.findByText('TOR-M6')).closest('tr');

    expect(fila).not.toHaveTextContent('Kit');

    // La explicación no vive en un `title`, que no llega ni al teclado ni al tacto: está escrita
    // debajo de la tabla, y es la descripción accesible de la celda.
    expect(
      within(fila!).getByRole('cell', { name: 'Sin reconocer', description: POR_QUE_EL_TIPO }),
    ).toBeVisible();
    expect(screen.getByText(POR_QUE_EL_TIPO)).toBeVisible();
    expect(screen.queryByText(POR_QUE_LA_TRAZABILIDAD)).not.toBeInTheDocument();
  });

  it('el tipo y la trazabilidad sin reconocer: cada celda, con su explicación', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/articulos', () => {
        const pagina = articulosDe(ALFA.id);
        const [tornillo, tuerca, ...resto] = pagina.elementos;

        return HttpResponse.json({
          ...pagina,
          elementos: [
            { ...tornillo!, tipo: 'Kit' },
            { ...tuerca!, trazabilidad: 'PorPeso' },
            ...resto,
          ],
        });
      }),
    );

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const tornillo = (await screen.findByText('TOR-M6')).closest('tr')!;
    const tuerca = screen.getByText('TUE-M6').closest('tr')!;

    // Dos explicaciones, una por clase de valor, y cada celda señala la suya y no la otra.
    expect(
      within(tornillo).getByRole('cell', { name: 'Sin reconocer', description: POR_QUE_EL_TIPO }),
    ).toBeVisible();
    expect(
      within(tuerca).getByRole('cell', {
        name: 'Sin reconocer',
        description: POR_QUE_LA_TRAZABILIDAD,
      }),
    ).toBeVisible();
    expect(screen.getByText(POR_QUE_EL_TIPO)).toBeVisible();
    expect(screen.getByText(POR_QUE_LA_TRAZABILIDAD)).toBeVisible();

    // Las celdas reconocidas no llevan descripción.
    for (const celda of within(tuerca).getAllByRole('cell')) {
      if (celda.textContent !== 'Sin reconocer') {
        expect(celda).not.toHaveAccessibleDescription();
      }
    }
  });

  it('la trazabilidad sale en su columna, también la que esta versión no conoce', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/articulos', () => {
        const pagina = articulosDe(ALFA.id);
        const [tornillo, tuerca, ...resto] = pagina.elementos;

        return HttpResponse.json({
          ...pagina,
          elementos: [
            { ...tornillo!, trazabilidad: 'PorPeso' },
            { ...tuerca!, trazabilidad: 'PorLote' },
            ...resto,
          ],
        });
      }),
    );

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const columna = await screen.findByRole('columnheader', { name: 'Trazabilidad' });
    const indice = within(columna.closest('tr')!).getAllByRole('columnheader').indexOf(columna);
    const celda = (codigo: string): HTMLElement =>
      within(screen.getByText(codigo).closest('tr')!).getAllByRole('cell')[indice]!;

    expect(celda('TUE-M6')).toHaveTextContent('Por lote');
    expect(celda('MO-TALLER')).toHaveTextContent('Ninguna');
    expect(celda('TOR-M6')).not.toHaveTextContent('PorPeso');
    expect(celda('TOR-M6')).toBe(
      screen.getByRole('cell', { name: 'Sin reconocer', description: POR_QUE_LA_TRAZABILIDAD }),
    );
    expect(screen.getByText(POR_QUE_LA_TRAZABILIDAD)).toBeVisible();
    expect(celda('TUE-M6')).not.toHaveAccessibleDescription();

    // Sin el permiso de modificar, el enlace no está en ninguna fila.
    expect(screen.queryByRole('link', { name: /trazabilidad/ })).not.toBeInTheDocument();
  });

  it('a quien puede modificar, cada fila le enlaza su trazabilidad y dice de qué artículo', async () => {
    abrirSesionYaRecuperada(ALFA.id, [...PERMISOS_DE_LECTURA, 'catalogo.articulo.modificar']);

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const enlace = await screen.findByRole('link', {
      name: 'Cambiar la trazabilidad de TOR-M6',
    });
    expect(enlace).toHaveTextContent('Cambiar');
    expect(enlace).toHaveAttribute(
      'href',
      '/articulos/fffffff1-0000-0000-0000-000000000001/trazabilidad',
    );

    // Uno por fila, cada uno con su nombre: en la lista de enlaces del lector de pantalla, tres
    // «Cambiar» iguales no se distinguen.
    expect(
      screen
        .getAllByRole('link', { name: /^Cambiar la trazabilidad de / })
        .map((e) => e.textContent),
    ).toEqual(['Cambiar', 'Cambiar', 'Cambiar']);
  });

  it('a quien ve el listado, cada fila le enlaza sus códigos de barras y dice de qué artículo', async () => {
    abrirSesionYaRecuperada(ALFA.id, PERMISOS_DE_LECTURA);

    montarPantalla(<PaginaDeArticulos />, '/articulos');

    const enlace = await screen.findByRole('link', {
      name: 'Ver los códigos de barras de TOR-M6',
    });
    expect(enlace).toHaveTextContent('Ver');
    expect(enlace).toHaveAttribute('href', '/articulos/fffffff1-0000-0000-0000-000000000001/gtin');
    expect(screen.getByRole('columnheader', { name: 'Códigos de barras' })).toBeVisible();

    // Con el permiso de ver basta: es el que pide la pantalla, que dentro esconde lo demás.
    expect(
      screen
        .getAllByRole('link', { name: /^Ver los códigos de barras de / })
        .map((e) => e.textContent),
    ).toEqual(['Ver', 'Ver', 'Ver']);
  });
});
