import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { ALFA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

/**
 * Lo que hace la guarda, y —tan importante como eso— lo que NO hace.
 *
 * No es un control de acceso: es la interfaz no enseñando lo que no toca. Por eso aquí se prueba
 * que redirige, que explica y que esconde, y no se prueba «que impide»: impedir es cosa del
 * servidor, y eso está probado en `Api.IntegrationTests`.
 */
describe('Las rutas protegidas', () => {
  it('sin sesión, cualquier ruta protegida lleva a la pantalla de acceso', async () => {
    montarAplicacion('/almacenes');

    expect(await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' })).toBeVisible();
    expect(screen.getByLabelText('Correo')).toBeInTheDocument();
  });

  it('tras entrar se vuelve a donde se iba, no al inicio', async () => {
    const usuario = userEvent.setup();
    montarAplicacion('/almacenes');

    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.type(screen.getByLabelText('Correo'), 'ana@ejemplo.es');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la-buena');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    // El destino viajaba en el estado de la navegación desde que la guarda desvió. Perderlo obliga
    // a volver a buscar la pantalla a mano, que es la clase de fricción que nadie reporta y todo el
    // mundo sufre.
    expect(await screen.findByRole('heading', { level: 1, name: 'Almacenes' })).toBeVisible();
  });

  it('con sesión pero sin el permiso, se explica en vez de enseñar una pantalla rota', async () => {
    abrirSesionSimulada(ALFA.id, []);
    montarAplicacion('/almacenes');

    // El encabezado sigue estando —la ruta existe y se ha llegado a ella—, lo que no está son los
    // datos. Y en su sitio hay una frase, no una tabla vacía ni una cascada de errores 403.
    expect(await screen.findByRole('heading', { level: 1, name: 'Almacenes' })).toBeVisible();
    expect(screen.getByRole('alert')).toHaveTextContent(/no tiene permiso/i);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('la navegación no ofrece lo que no se puede ver', async () => {
    abrirSesionSimulada(ALFA.id, ['organizacion.almacen.ver']);
    montarAplicacion('/');

    await screen.findByRole('heading', { level: 1, name: 'Inicio' });

    const principal = screen.getByRole('navigation', { name: 'Principal' });
    expect(principal).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Almacenes' })).toBeInTheDocument();
    // Sin `organizacion.empresa.ver` el enlace no se pinta. Esconderlo no protege nada —la URL
    // sigue ahí— pero evita ofrecer una puerta que se va a cerrar en la cara.
    expect(screen.queryByRole('link', { name: 'Empresas' })).not.toBeInTheDocument();
  });

  it('una dirección que no existe se dice, y se da una salida', async () => {
    abrirSesionSimulada();
    montarAplicacion('/lo-que-sea');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' }),
    ).toBeVisible();
    expect(screen.getByRole('link', { name: 'Volver al inicio' })).toBeInTheDocument();
  });
});
