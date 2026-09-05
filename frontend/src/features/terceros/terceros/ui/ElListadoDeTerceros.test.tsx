import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, tercerosDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { PaginaDeTerceros } from './PaginaDeTerceros.tsx';
import type { components } from '@/shared/api/esquema.ts';

/**
 * La pantalla de terceros, por sus cuatro situaciones y por lo que este ítem estrena.
 *
 * Las cuatro de siempre —cargando, error con salida, vacío con motivo y la página en la URL— y dos
 * más que son de aquí: **el filtro, que también vive en la URL**, y **el sello de verificación**,
 * que es la pieza por la que este listado no puede ser el de almacenes con otras columnas.
 *
 * Lo que NO se prueba aquí, y no por olvido: que el identificador fiscal no viaje por la cadena de
 * consulta. Eso no es una propiedad de esta pantalla sino del servidor, y lo vigila
 * `NingunCriterioSensibleViajaEnLaUrl` en el carril de la API (ADR-0025). Lo que sí es de aquí es
 * que este recuadro no ofrezca buscar por él, y eso se ve en su etiqueta.
 */
describe('El listado de terceros', () => {
  it('mientras llegan, dice que está cargando', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/terceros/terceros', async () => {
        await delay(80);
        return HttpResponse.json(tercerosDe(ALFA.id));
      }),
    );

    montarPantalla(<PaginaDeTerceros />, '/terceros');

    expect(await screen.findByText('Cargando los terceros…')).toBeVisible();
    expect(await screen.findByText('Ferretería Industrial del Sur SL')).toBeVisible();
  });

  it('si el servidor falla, lo dice y deja volver a intentarlo', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorSimulado.falloDeTerceros = 500;

    montarPantalla(<PaginaDeTerceros />, '/terceros');

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('El servidor no ha podido responder. Inténtalo de nuevo.');

    // Y la salida existe de verdad: no es un botón decorativo, vuelve a pedir. El recuadro de
    // filtro sigue en pie mientras tanto, que es lo que permite reintentar con otro criterio en vez
    // de volver a entrar por la URL.
    expect(screen.getByRole('searchbox')).toBeVisible();

    servidorSimulado.falloDeTerceros = null;
    await usuario.click(screen.getByRole('button', { name: 'Volver a intentarlo' }));

    expect(await screen.findByText('Ferretería Industrial del Sur SL')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin terceros, dice QUÉ está vacío y no «sin datos»', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/terceros/terceros', () =>
        HttpResponse.json({ elementos: [], pagina: 1, tamanio: 20, total: 0 }),
      ),
    );

    montarPantalla(<PaginaDeTerceros />, '/terceros');

    expect(
      await screen.findByText('Todavía no hay ningún tercero dado de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('vacío POR EL FILTRO no se dice igual que vacío del todo, y nombra el filtro', async () => {
    abrirSesionYaRecuperada();

    // La distinción no es cosmética: «no hay ninguno» manda a dar de alta un tercero que sí existe
    // —con el identificador ocupado, y por tanto con un 409 esperándole— mientras que «ninguno
    // coincide con esto» manda a probar otra palabra.
    montarPantalla(<PaginaDeTerceros />, '/terceros?busqueda=Nombre%20Que%20No%20Existe');

    expect(
      await screen.findByText('Ningún tercero coincide con «Nombre Que No Existe».'),
    ).toBeVisible();
    expect(
      screen.queryByText('Todavía no hay ningún tercero dado de alta en esta empresa.'),
    ).not.toBeInTheDocument();
  });

  it('filtrar lo escribe EN LA URL, y el criterio llega al servidor', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(<PaginaDeTerceros />, '/terceros');

    expect(await screen.findByText('Outillage Girondin SARL')).toBeVisible();

    await usuario.type(screen.getByRole('searchbox'), 'Ferrisur');
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }));

    // Ferrisur es el NOMBRE COMERCIAL de la primera ficha, no su razón social: el filtro mira los
    // dos, que es lo que hace falta para encontrar a alguien por como se le llama de verdad.
    expect(await screen.findByText('Ferretería Industrial del Sur SL')).toBeVisible();
    await waitFor(() => {
      expect(screen.queryByText('Outillage Girondin SARL')).not.toBeInTheDocument();
    });

    // Lo que distingue esto de un `useState`: el filtro está en la ubicación, así que el listado
    // filtrado se puede pegar en un correo y la flecha de atrás lo deshace.
    expect(enrutador.state.location.search).toBe('?busqueda=Ferrisur');

    // Y llega al servidor. Sin esto, una pantalla que se trajera todo y filtrara en el navegador
    // pintaría exactamente lo mismo — hasta el día en que hay tres mil terceros.
    expect(servidorSimulado.busquedasDeTerceros).toEqual(['', 'Ferrisur']);
  });

  it('entrando por un enlace con ?busqueda=, se abre ya filtrado', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeTerceros />, '/terceros?busqueda=Girondin');

    expect(await screen.findByText('Outillage Girondin SARL')).toBeVisible();
    expect(screen.queryByText('Ferretería Industrial del Sur SL')).not.toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toHaveValue('Girondin');
    expect(servidorSimulado.busquedasDeTerceros).toEqual(['Girondin']);
  });

  it('cambiar el filtro vuelve a la primera página', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const pedidas: { pagina: string; busqueda: string }[] = [];
    servidor.use(http.get('/api/v1/terceros/terceros', tramo(pedidas)));

    const { enrutador } = montarPantalla(<PaginaDeTerceros />, '/terceros?pagina=2');

    expect(await screen.findByText('Tercero 21')).toBeVisible();

    await usuario.type(screen.getByRole('searchbox'), 'Tercero');
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }));

    // Quedarse en la séptima página al cambiar el filtro enseña una página vacía de un resultado
    // que sí tiene filas, y quien lo ve entiende que no hay ninguno.
    expect(await screen.findByText('Tercero 1')).toBeVisible();
    expect(enrutador.state.location.search).toBe('?busqueda=Tercero');
    expect(pedidas).toEqual([
      { pagina: '2', busqueda: '' },
      { pagina: '1', busqueda: 'Tercero' },
    ]);
  });

  it('el sello se pinta SIEMPRE, comprobado o no, y el papel dice las dos cosas cuando son dos', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeTerceros />, '/terceros');

    // El sello enseñado solo cuando algo va mal deja sin saber si su ausencia significa
    // «comprobado» o «esta versión todavía no lo enseñaba». Aquí están los dos estados, en la misma
    // tabla y a la vez, que es la única forma de que la comparación signifique algo.
    const conNif = (await screen.findByText('ES 00000001R')).closest('tr');
    const extranjero = screen.getByText('FR PRUEBAFR0001').closest('tr');

    expect(conNif).toHaveTextContent('Comprobado');
    expect(extranjero).toHaveTextContent('Sin comprobar');

    // Y el papel: cliente, proveedor, o las dos cosas. Las dos cosas es UNA ficha, no dos, y por eso
    // la columna no puede ser un booleano pintado.
    expect(conNif).toHaveTextContent('Cliente');
    expect(extranjero).toHaveTextContent('Proveedor');
    expect(screen.getByText('ES 00000002W').closest('tr')).toHaveTextContent('Cliente y proveedor');
  });

  it('un estado de verificación que esta versión no conoce NO se da por bueno', async () => {
    abrirSesionYaRecuperada();

    // El enumerado viaja como texto y el frontal se despliega aparte del backend, así que un valor
    // nuevo llega antes de que esta pantalla lo conozca. Lo que no se puede validar se marca como
    // no validado: ni «Comprobado», ni la celda en blanco —que es lo que se ve cuando algo se
    // rompe— sino dicho.
    servidor.use(
      http.get('/api/v1/terceros/terceros', () => {
        const pagina = tercerosDe(ALFA.id);
        const [primero, ...resto] = pagina.elementos;

        return HttpResponse.json({
          ...pagina,
          elementos: [
            {
              ...primero!,
              identificacion: {
                ...primero!.identificacion,
                verificacion: 'VerificadoContraElVies',
              },
            },
            ...resto,
          ],
        });
      }),
    );

    montarPantalla(<PaginaDeTerceros />, '/terceros');

    const fila = (await screen.findByText('ES 00000001R')).closest('tr');

    // Las dos mitades de «no se da por bueno»: se DICE que no está comprobado —ni la celda en
    // blanco, ni el texto crudo que llegó— y no se cuela como comprobado.
    expect(fila).toHaveTextContent('Sin comprobar');
    expect(fila).not.toHaveTextContent('Comprobado');
  });
});

type PaginaDeTerceroDto = components['schemas']['PaginaDeTerceroDto'];

/**
 * Veinticinco terceros servidos de veinte en veinte, que anotan qué página y qué filtro les piden.
 *
 * Veinticinco y no dos: con menos de una página el paginador nunca se activa, y el test de volver a
 * la primera pasaría sin haber estado nunca en la segunda.
 */
function tramo(pedidas: { pagina: string; busqueda: string }[]) {
  return ({ request }: { request: Request }): Response => {
    const consulta = new URL(request.url).searchParams;
    const numero = Number(consulta.get('page') ?? '1');
    const tamanio = Number(consulta.get('size') ?? '20');

    pedidas.push({ pagina: String(numero), busqueda: consulta.get('q') ?? '' });

    const desde = (numero - 1) * tamanio;
    const [molde] = tercerosDe(ALFA.id).elementos;

    if (molde === undefined) {
      throw new Error('Los datos de prueba se han quedado sin terceros de los que sacar el molde.');
    }

    const elementos = Array.from(
      { length: Math.max(0, Math.min(tamanio, 25 - desde)) },
      (_, i) => ({
        ...molde,
        id: `ccccccc9-0000-0000-0000-${String(desde + i + 1).padStart(12, '0')}`,
        razonSocial: `Tercero ${String(desde + i + 1)}`,
        nombreComercial: null,
      }),
    );

    const cuerpo: PaginaDeTerceroDto = { elementos, pagina: numero, tamanio, total: 25 };

    return HttpResponse.json(cuerpo);
  };
}
