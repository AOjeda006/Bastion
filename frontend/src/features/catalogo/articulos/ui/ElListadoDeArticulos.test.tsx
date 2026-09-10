import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, articulosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { PaginaDeArticulos } from './PaginaDeArticulos.tsx';

/** La rama «Tornillería», que en los datos de prueba tiene un artículo y cuelga de «Ferretería». */
const TORNILLERIA = 'eeeeeee1-0000-0000-0000-000000000002';

/**
 * La pantalla de artículos: sus tres estados, sus dos filtros y lo que este ítem estrena.
 *
 * Lo de siempre —cargando, error con salida, vacío con motivo y los filtros en la URL— y dos cosas
 * que son de aquí: **el filtro por rama**, que se pone desde el árbol y se anuncia con su nombre, y
 * **el tipo del artículo**, que viaja como texto y por tanto puede llegar con un valor que esta
 * versión no conozca.
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

    expect(fila).toHaveTextContent('Sin reconocer');
    expect(fila).not.toHaveTextContent('Kit');
  });
});
