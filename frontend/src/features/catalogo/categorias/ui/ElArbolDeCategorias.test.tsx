import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, categoriasDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { PaginaDeCategorias } from './PaginaDeCategorias.tsx';
import type { components } from '@/shared/api/esquema.ts';

type CategoriaDto = components['schemas']['CategoriaDto'];

/** Una página de categorías con lo que se le diga, para los casos que fabrican su propio árbol. */
function pagina(elementos: CategoriaDto[]): components['schemas']['PaginaDeCategoriaDto'] {
  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}

/**
 * La pantalla del árbol de categorías.
 *
 * Lo que aquí se prueba y no se prueba en ningún otro sitio es **la composición**: la API devuelve
 * la lista plana con el padre de cada una, y el árbol lo monta el frontal. Eso trae tres casos que
 * no existen cuando el servidor sirve el árbol montado —el orden, el padre que se queda en otra
 * página y el ciclo guardado— y los tres están abajo.
 */
describe('El árbol de categorías', () => {
  it('mientras llegan, dice que está cargando', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/categorias', async () => {
        await delay(80);
        return HttpResponse.json(categoriasDe(ALFA.id));
      }),
    );

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    expect(await screen.findByText('Cargando las categorías…')).toBeVisible();
    expect(await screen.findByText('Ferretería')).toBeVisible();
  });

  it('si el servidor falla, lo dice y deja volver a intentarlo', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorSimulado.falloDeCategorias = 500;

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'El servidor no ha podido responder. Inténtalo de nuevo.',
    );

    servidorSimulado.falloDeCategorias = null;
    await usuario.click(screen.getByRole('button', { name: 'Volver a intentarlo' }));

    expect(await screen.findByText('Ferretería')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin categorías, dice QUÉ está vacío y no «sin datos»', async () => {
    abrirSesionYaRecuperada();

    servidor.use(http.get('/api/v1/catalogo/categorias', () => HttpResponse.json(pagina([]))));

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    expect(
      await screen.findByText('Todavía no hay ninguna categoría dada de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('se pinta como ÁRBOL y no en el orden en que llega, con el nivel de cada rama', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    await screen.findByText('Ferretería');

    // Llegan ordenadas por código —FERR, SERV, TORN, TUER—, que NO es el orden del árbol. Una
    // pantalla que pintara la lista tal cual pondría «Servicios» la segunda; componer el árbol la
    // manda al final, detrás de la rama entera de Ferretería.
    const filas = screen.getAllByRole('row').slice(1) as HTMLTableRowElement[];
    expect(filas.map((fila) => fila.cells[1]?.textContent.trim())).toEqual([
      'Ferretería',
      'Tornillería',
      'Tuercas',
      'Servicios',
    ]);

    // Y la profundidad va en su columna, no solo en la sangría: un lector de pantalla no ve el
    // relleno de la izquierda, así que sin esta columna el árbol solo existiría para quien mira.
    expect(filas.map((fila) => fila.cells[2]?.textContent)).toEqual(['0', '1', '2', '0']);
  });

  it('cada rama enlaza a SUS artículos, con el filtro ya puesto en la dirección', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    const enlace = await screen.findByRole('link', {
      name: 'Ver los artículos de Tornillería',
    });

    // Es la única forma de poner el filtro por rama, así que este enlace es lo que sostiene que la
    // pantalla de artículos no necesite un desplegable con el catálogo entero.
    expect(enlace).toHaveAttribute(
      'href',
      '/articulos?categoria=eeeeeee1-0000-0000-0000-000000000002',
    );
  });

  it('una categoría cuyo padre se quedó en otra página se pinta IGUALMENTE, marcada', async () => {
    abrirSesionYaRecuperada();

    // Pasa en cuanto hay más categorías que sitio en una página, que en un árbol real es lo
    // normal. Descartar la fila dejaría fuera una categoría que existe, y una categoría que existe
    // y no sale es una que alguien da de alta por segunda vez.
    servidor.use(
      http.get('/api/v1/catalogo/categorias', () =>
        HttpResponse.json(
          pagina(
            categoriasDe(ALFA.id).elementos.filter((categoria) => categoria.codigo !== 'FERR'),
          ),
        ),
      ),
    );

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    const tornilleria = (await screen.findByText('Tornillería')).closest('tr');
    const tuercas = screen.getByText('Tuercas').closest('tr');

    expect(tornilleria).toHaveTextContent('Sin su sitio');

    // Y solo la que ha perdido el padre: lo que cuelga de ella sigue colgando, y marcarlo también
    // convertiría la marca en decoración.
    expect(tuercas).not.toHaveTextContent('Sin su sitio');
    expect(tuercas?.cells[2]).toHaveTextContent('1');
  });

  it('un ciclo YA GUARDADO no cuelga la pantalla: la cadena se rompe por un sitio y se dice', async () => {
    abrirSesionYaRecuperada();

    // Por la API no se puede llegar a esto —lo impide `ElArbolSigueSiendoUnArbol`, en el
    // servidor—, pero sí con un `UPDATE` a mano, una importación o una migración de datos de otro
    // sistema. Y aquí el precio de no contemplarlo no es un dato mal pintado: es un descenso que
    // no termina, o sea una pestaña colgada, que además en esta suite se manifestaría como «el
    // test tarda» y no señalaría a nadie.
    servidor.use(
      http.get('/api/v1/catalogo/categorias', () =>
        HttpResponse.json(
          pagina([
            {
              id: 'eeeeeee9-0000-0000-0000-000000000001',
              empresaId: ALFA.id,
              codigo: 'UNA',
              nombre: 'Una',
              padreId: 'eeeeeee9-0000-0000-0000-000000000002',
            },
            {
              id: 'eeeeeee9-0000-0000-0000-000000000002',
              empresaId: ALFA.id,
              codigo: 'OTRA',
              nombre: 'Otra',
              padreId: 'eeeeeee9-0000-0000-0000-000000000001',
            },
          ]),
        ),
      ),
    );

    montarPantalla(<PaginaDeCategorias />, '/categorias');

    // Las dos salen, y salen una sola vez: eso es lo que dice que el descenso ha terminado.
    const filas = (await screen.findAllByRole('row')).slice(1) as HTMLTableRowElement[];
    expect(filas.map((fila) => fila.cells[1]?.textContent.trim())).toEqual([
      'Una Sin su sitio',
      'Otra',
    ]);

    // La cadena se rompe por UN sitio y se dice cuál: «Una» no está debajo de su padre —está
    // debajo de nada— y se marca; «Otra» sí cuelga de quien dice colgar, ahí mismo encima, así que
    // marcarla también convertiría la marca en decoración.
    expect(filas.map((fila) => fila.cells[2]?.textContent)).toEqual(['0', '1']);
  });
});
