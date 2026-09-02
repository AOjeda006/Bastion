import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { abrirSesionSimulada } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

/**
 * LOS TRES PASOS DEL CAMBIO DE RUTA ACCESIBLE (`ux-ipo`, sección SPA).
 *
 * Un enrutador de cliente cambia de pantalla sin recargar: para un lector de pantalla eso es
 * silencio y el foco se queda en un enlace que ya no existe. Se comprueba como se comprueba la
 * accesibilidad —consultando por ROL y por NOMBRE ACCESIBLE—, no mirando clases ni marcado: si el
 * test encuentra las cosas por su rol, un lector de pantalla también.
 */
describe('El cambio de ruta', () => {
  it('anuncia, retitula y mueve el foco al navegar', async () => {
    abrirSesionSimulada();
    const usuario = userEvent.setup();
    montarAplicacion('/');

    // El armazón aparece cuando la sesión se ha recuperado con la cookie.
    expect(await screen.findByRole('heading', { level: 1, name: 'Inicio' })).toBeInTheDocument();
    await waitFor(() => {
      expect(document.title).toBe('Inicio · Bastion');
    });

    await usuario.click(screen.getByRole('link', { name: 'Almacenes' }));

    const encabezado = await screen.findByRole('heading', { level: 1, name: 'Almacenes' });

    // 2. El título, único por vista.
    await waitFor(() => {
      expect(document.title).toBe('Almacenes · Bastion');
    });

    // 1. El mensaje de estado. Se busca POR SU ROL: si `getByRole` lo encuentra es que está en el
    //    árbol de accesibilidad, o sea que no se ha escondido con `display:none` ni
    //    `visibility:hidden` — que es lo que lo escondería también del lector de pantalla.
    const anunciador = screen.getByRole('status', { name: 'Estado de la navegación' });
    expect(anunciador).toHaveTextContent('La página Almacenes se ha cargado.');
    expect(anunciador).toHaveAttribute('aria-live', 'polite');

    // 3. El foco, en el `<h1>` del `<main>`.
    await waitFor(() => {
      expect(document.activeElement).toBe(encabezado);
    });
  });

  it('el destino del foco no entra en el orden de tabulación', async () => {
    abrirSesionSimulada();
    montarAplicacion('/');

    const encabezado = await screen.findByRole('heading', { level: 1, name: 'Inicio' });

    // `tabindex="-1"` es enfocable por programación pero NO tabulable. Con `0` el foco también
    // llegaría —el test anterior seguiría verde— y a cambio todo el mundo se comería una parada de
    // más con el tabulador en cada pantalla, para caer en un encabezado que no hace nada.
    expect(encabezado).toHaveAttribute('tabindex', '-1');
  });

  it('el primer enlace de la página es el salto al contenido', async () => {
    abrirSesionSimulada();
    montarAplicacion('/');

    await screen.findByRole('heading', { level: 1, name: 'Inicio' });

    const enlaces = screen.getAllByRole('link');
    expect(enlaces[0]).toHaveAccessibleName('Saltar al contenido');
    expect(enlaces[0]).toHaveAttribute('href', '#contenido');
    expect(document.querySelector('main')).toHaveAttribute('id', 'contenido');
  });
});
