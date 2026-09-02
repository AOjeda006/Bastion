import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { ALFA, sesionDto } from '@/pruebas/datos.ts';
import { abrirSesionSimulada, servidor } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

/**
 * La pantalla de acceso: lo que valida antes de salir a la red, lo que dice cuando le dicen que no,
 * y lo que hace mientras espera.
 *
 * Todo se busca por rol y por etiqueta. Ni una clase de CSS, ni un `data-testid`: si el campo deja
 * de tener su `<label>` asociada estos tests se caen, y eso es exactamente lo que tienen que hacer,
 * porque sin etiqueta asociada el campo no existe para un lector de pantalla.
 */
describe('La pantalla de acceso', () => {
  it('valida antes de molestar al servidor', async () => {
    const usuario = userEvent.setup();

    let intentos = 0;
    servidor.use(
      http.post('/api/v1/identidad/sesiones', () => {
        intentos += 1;
        return new HttpResponse(null, { status: 401 });
      }),
    );

    montarAplicacion('/acceso');
    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText('Escribe tu correo.')).toBeVisible();
    expect(screen.getByText('Escribe tu contraseña.')).toBeVisible();

    // El error no está solo en rojo: está en texto, y está ATADO al campo. Sin `aria-describedby` un
    // lector de pantalla anuncia «Correo, cuadro de edición» y se calla.
    const correo = screen.getByLabelText('Correo');
    expect(correo).toHaveAttribute('aria-invalid', 'true');
    expect(correo).toHaveAccessibleDescription('Escribe tu correo.');

    expect(intentos).toBe(0);
  });

  it('un correo mal escrito se dice aquí, sin ir y volver', async () => {
    const usuario = userEvent.setup();
    montarAplicacion('/acceso');
    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.type(screen.getByLabelText('Correo'), 'ana-arroba-ejemplo');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la-buena');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText('Eso no parece un correo electrónico.')).toBeVisible();
  });

  it('unas credenciales malas no dicen CUÁL de las dos falla', async () => {
    const usuario = userEvent.setup();
    montarAplicacion('/acceso');
    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.type(screen.getByLabelText('Correo'), 'ana@ejemplo.es');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la-mala');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('El correo o la contraseña no son correctos.');

    // Lo que NO puede decir. Un mensaje distinto para cada caso le regala a quien prueba correos la
    // lista de los que existen, y eso se decide en el frontal aunque el servidor conteste lo mismo.
    expect(aviso).not.toHaveTextContent(/no existe|no está registrado|contraseña incorrecta/i);

    // Y se sigue en la pantalla de acceso, con lo escrito donde estaba.
    expect(screen.getByRole('heading', { level: 1, name: 'Iniciar sesión' })).toBeVisible();
    expect(screen.getByLabelText('Correo')).toHaveValue('ana@ejemplo.es');
  });

  it('mientras se envía, el botón no deja mandarlo dos veces', async () => {
    const usuario = userEvent.setup();

    let intentos = 0;
    servidor.use(
      http.post('/api/v1/identidad/sesiones', async () => {
        intentos += 1;
        await delay(120);
        return HttpResponse.json(sesionDto(ALFA.id));
      }),
    );

    montarAplicacion('/acceso');
    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.type(screen.getByLabelText('Correo'), 'ana@ejemplo.es');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la-buena');

    const boton = screen.getByRole('button', { name: 'Entrar' });
    await usuario.click(boton);

    // Desactivado y además diciendo por qué: un botón que se apaga sin explicación parece roto.
    const esperando = await screen.findByRole('button', { name: 'Entrando…' });
    expect(esperando).toBeDisabled();

    expect(await screen.findByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();
    expect(intentos).toBe(1);
  });

  it('estando ya dentro, la pantalla de acceso no se enseña otra vez', async () => {
    abrirSesionSimulada();
    montarAplicacion('/acceso');

    // Volver a la puerta con la llave puesta: entrar otra vez emitiría una familia de testigos nueva
    // dejando la anterior viva, así que se redirige en vez de ofrecer el formulario.
    expect(await screen.findByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();
    expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument();
  });
});
