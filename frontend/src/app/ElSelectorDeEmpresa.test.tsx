import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { ALFA, BETA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { leerSesion } from '@/shared/sesion/deposito.ts';

/**
 * EL SELECTOR DE EMPRESA — la R8 vista desde el navegador.
 *
 * Este es el test que importa del ítem, y está escrito a propósito para que MIRE LO QUE SE PINTA.
 * Uno que comprobara que se ha llamado a `clear()` probaría que se ha llamado a `clear()`: seguiría
 * verde el día que alguien lo sustituyera por una invalidación de tres claves elegidas a mano, que
 * es exactamente el fallo que hay que cazar.
 *
 * Dos empresas de verdad, con un almacén distinto cada una. Se pinta la lista de Alfa, se cambia a
 * Beta, y se exige que lo que se ve sea de Beta y que lo de Alfa ya no esté.
 */
describe('El selector de empresa', () => {
  it('al cambiar de empresa, la lista pasa a ser la de la otra', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/almacenes');

    expect(await screen.findByText('Nave central de Alfa')).toBeInTheDocument();

    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Empresa' }), BETA.id);

    expect(await screen.findByText('Nave central de Beta')).toBeInTheDocument();

    // Y lo de Alfa YA NO ESTÁ. Sin esta línea el test pasaría con la caché sin vaciar mientras la
    // consulta nueva se resuelve por detrás: se verían las dos, que en un ERP multiempresa no es
    // un parpadeo sino filas de otro inquilino en pantalla.
    expect(screen.queryByText('Nave central de Alfa')).not.toBeInTheDocument();
  });

  it('el testigo nuevo lleva la empresa nueva', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/');

    await screen.findByRole('heading', { level: 1, name: 'Inicio' });
    expect(leerSesion()?.empresaActivaId).toBe(ALFA.id);

    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Empresa' }), BETA.id);

    // La empresa activa no es un estado del frontal: viaja dentro del testigo. Cambiarla es
    // cambiar de testigo, y por eso la sesión entera se sustituye.
    await waitFor(() => {
      expect(leerSesion()?.empresaActivaId).toBe(BETA.id);
    });
    expect(leerSesion()?.testigo).toBe(`testigo-de-${BETA.id}`);
  });

  it('el selector se llama por su etiqueta y trae las empresas de la sesión', async () => {
    abrirSesionSimulada(ALFA.id);
    montarAplicacion('/');

    await screen.findByRole('heading', { level: 1, name: 'Inicio' });

    // Consultado por rol y nombre accesible: es un `<select>` nativo con su `<label>`, así que no
    // hace falta ninguna primitiva de ARIA para que se pueda usar con teclado y lector de pantalla.
    const selector = screen.getByRole('combobox', { name: 'Empresa' });

    expect(selector).toHaveValue(ALFA.id);
    expect(screen.getByRole('option', { name: 'Alfa Materiales SL' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Beta Suministros SL' })).toBeInTheDocument();
  });
});
