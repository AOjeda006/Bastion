import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, almacenesDe } from '@/pruebas/datos.ts';
import { abrirSesionSimulada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import type { components } from '@/shared/api/esquema.ts';

/**
 * Una pantalla terminada, comprobada por sus cuatro situaciones y no solo por la buena.
 *
 * Cargando, error con salida, vacío con motivo, y la página en la URL. Las tres primeras son las
 * que se olvidan; la cuarta es la que se hace mal: `useState('pagina')` pinta lo mismo y rompe lo
 * que nadie prueba —compartir el enlace, la flecha de atrás, recargar sin perder el sitio—.
 */
describe('El listado de almacenes', () => {
  it('mientras llega, dice que está cargando', async () => {
    abrirSesionSimulada();

    servidor.use(
      http.get('/api/v1/organizacion/almacenes', async () => {
        await delay(80);
        return HttpResponse.json(almacenesDe(ALFA.id));
      }),
    );

    montarAplicacion('/almacenes');

    expect(await screen.findByText('Cargando los almacenes…')).toBeVisible();
    expect(await screen.findByText('Nave central de Alfa')).toBeVisible();
  });

  it('si el servidor falla, lo dice y deja volver a intentarlo', async () => {
    const usuario = userEvent.setup();
    abrirSesionSimulada();
    servidorSimulado.falloDeAlmacenes = 500;

    montarAplicacion('/almacenes');

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('El servidor no ha podido responder. Inténtalo de nuevo.');

    // Y la salida existe de verdad: no es un botón decorativo, vuelve a pedir.
    servidorSimulado.falloDeAlmacenes = null;
    await usuario.click(screen.getByRole('button', { name: 'Volver a intentarlo' }));

    expect(await screen.findByText('Nave central de Alfa')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin almacenes, dice QUÉ está vacío y no «sin datos»', async () => {
    abrirSesionSimulada();

    servidor.use(
      http.get('/api/v1/organizacion/almacenes', () =>
        HttpResponse.json({ elementos: [], pagina: 1, tamanio: 20, total: 0 }),
      ),
    );

    montarAplicacion('/almacenes');

    expect(
      await screen.findByText('Todavía no hay ningún almacén dado de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Paginación' })).toHaveTextContent(
      'Sin resultados',
    );
  });

  it('pasar de página lo escribe EN LA URL, y de la URL sale lo que se pide', async () => {
    const usuario = userEvent.setup();
    abrirSesionSimulada();

    const pedidas: string[] = [];
    servidor.use(http.get('/api/v1/organizacion/almacenes', pagina(pedidas)));

    const { enrutador } = montarAplicacion('/almacenes');

    expect(await screen.findByText('Almacén 1')).toBeVisible();

    await usuario.click(screen.getByRole('button', { name: 'Siguiente' }));

    expect(await screen.findByText('Almacén 21')).toBeVisible();
    expect(screen.queryByText('Almacén 1')).not.toBeInTheDocument();

    // Lo que distingue esto de un `useState`: la página está en la ubicación, así que el enlace se
    // puede pegar en un correo y la flecha de atrás del navegador hace lo que se espera.
    expect(enrutador.state.location.search).toBe('?pagina=2');
    expect(pedidas).toEqual(['1', '2']);
  });

  it('entrando por un enlace con ?pagina=2, se abre en la segunda', async () => {
    abrirSesionSimulada();

    const pedidas: string[] = [];
    servidor.use(http.get('/api/v1/organizacion/almacenes', pagina(pedidas)));

    montarAplicacion('/almacenes?pagina=2');

    expect(await screen.findByText('Almacén 21')).toBeVisible();
    expect(screen.getByRole('navigation', { name: 'Paginación' })).toHaveTextContent('21–25 de 25');
    expect(pedidas).toEqual(['2']);
  });

  it('una página disparatada en la URL no rompe la pantalla', async () => {
    abrirSesionSimulada();

    const pedidas: string[] = [];
    servidor.use(http.get('/api/v1/organizacion/almacenes', pagina(pedidas)));

    montarAplicacion('/almacenes?pagina=-7&tamanio=9999');

    // La URL la escribe cualquiera. Se lee con un esquema que tiene valores por omisión, así que un
    // disparate se convierte en la primera página en vez de en una petición absurda o un fallo.
    expect(await screen.findByText('Almacén 1')).toBeVisible();
    await waitFor(() => {
      expect(pedidas).toEqual(['1']);
    });
  });
});

type PaginaDeAlmacenDto = components['schemas']['PaginaDeAlmacenDto'];

/**
 * Un listado de veinticinco almacenes servido de veinte en veinte, que anota qué página le piden.
 *
 * Veinticinco y no dos: con menos de una página el paginador nunca se activa y el test pasaría sin
 * haber pasado de página.
 */
function pagina(pedidas: string[]) {
  return ({ request }: { request: Request }): Response => {
    const consulta = new URL(request.url).searchParams;
    const numero = Number(consulta.get('page') ?? '1');
    const tamanio = Number(consulta.get('size') ?? '20');

    pedidas.push(String(numero));

    const desde = (numero - 1) * tamanio;
    const [molde] = almacenesDe(ALFA.id).elementos;

    if (molde === undefined) {
      throw new Error(
        'Los datos de prueba se han quedado sin almacenes de los que sacar el molde.',
      );
    }

    const elementos = Array.from(
      { length: Math.max(0, Math.min(tamanio, 25 - desde)) },
      (_, i) => ({
        ...molde,
        id: `aaaaaaa1-0000-0000-0000-${String(desde + i + 1).padStart(12, '0')}`,
        codigo: `ALM-${String(desde + i + 1).padStart(3, '0')}`,
        nombre: `Almacén ${String(desde + i + 1)}`,
      }),
    );

    const cuerpo: PaginaDeAlmacenDto = { elementos, pagina: numero, tamanio, total: 25 };

    return HttpResponse.json(cuerpo);
  };
}
