import { render, screen, waitFor } from '@testing-library/react';
import { StrictMode, useEffect, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';

import { Dialogo } from './Dialogo.tsx';
import { cerrarElDialogo } from './cerrarElDialogo.ts';

/** Cuántas veces se han montado los efectos de lo de dentro: dos, si el modo estricto está puesto. */
let montajes = 0;

function LoDeDentro(): React.JSX.Element {
  useEffect(() => {
    montajes += 1;
  }, []);

  return (
    <button
      type="button"
      onClick={(evento) => {
        cerrarElDialogo(evento.currentTarget);
      }}
    >
      Salir
    </button>
  );
}

/** Una página con quien abre la ventana, algo fuera de ella, y una salida dentro. */
function ConSuVentana({ alCerrar }: { alCerrar: () => void }): React.JSX.Element {
  const [abierta, setAbierta] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => {
          setAbierta(true);
        }}
      >
        Abrir
      </button>
      <button type="button">Fuera</button>
      {abierta && (
        <Dialogo
          titulo="La pregunta"
          alCerrar={() => {
            alCerrar();
            setAbierta(false);
          }}
        >
          <LoDeDentro />
        </Dialogo>
      )}
    </>
  );
}

/**
 * Cuántos `close` ha avisado el navegador. El evento no sube, así que se escucha al bajar: en la
 * fase de captura del documento, que lo ve antes que la ventana.
 */
let cierres = 0;
const contarCierre = (): void => {
  cierres += 1;
};

beforeEach(() => {
  montajes = 0;
  cierres = 0;
  document.addEventListener('close', contarCierre, true);
});

afterEach(() => {
  document.removeEventListener('close', contarCierre, true);
});

/** Monta la página en modo estricto, como `main.tsx` en desarrollo, y abre la ventana. */
async function abrir(): Promise<{ alCerrar: () => void; abrirla: HTMLElement }> {
  const alCerrar = vi.fn<() => void>();
  render(
    <StrictMode>
      <ConSuVentana alCerrar={alCerrar} />
    </StrictMode>,
  );

  const abrirla = screen.getByRole('button', { name: 'Abrir' });
  await userEvent.click(abrirla);
  await screen.findByRole('dialog', { name: 'La pregunta' });

  return { alCerrar, abrirla };
}

/**
 * LA VENTANA MODAL, EN UN CHROMIUM DE VERDAD (ítem 3.5, ADR-0080).
 *
 * Es lo que jsdom no tiene: `setupTests.ts` le pone un `showModal()` y un `close()` que cambian el
 * atributo y nada más. Aquí se prueba lo que hace el navegador con ellos —la ventana modal, el resto
 * de la página inerte, Escape, el foco que entra y el que vuelve— y el modo estricto, que monta los
 * efectos dos veces y no tiene que cerrarla. El foco que entra lo pone `showModal()`, sin `focus()`,
 * y por eso solo se prueba aquí (ADR-0081).
 */
describe('La ventana modal', () => {
  it('se abre modal, con el foco en el título, y el modo estricto no la cierra', async () => {
    const { alCerrar } = await abrir();
    const ventana = screen.getByRole('dialog');

    expect(ventana.matches(':modal')).toBe(true);
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'La pregunta' }));

    // El modo estricto está puesto: lo de dentro se ha montado dos veces. Y un cierre de ese doble
    // montaje llegaría en otra tarea: se espera a que hayan pasado unas cuantas, y no ha llegado.
    expect(montajes).toBe(2);
    await new Promise((listo) => {
      setTimeout(listo, 50);
    });
    expect(cierres).toBe(0);
    expect(ventana.isConnected).toBe(true);
    expect(ventana.matches(':modal')).toBe(true);
    expect(alCerrar).not.toHaveBeenCalled();
  });

  it('la página de debajo no corre mientras está abierta, y al cerrarla vuelve a correr', async () => {
    const corre = (): string => getComputedStyle(document.documentElement).overflowY;
    expect(corre(), 'la página ya no corría antes de abrir').not.toBe('hidden');

    await abrir();
    expect(corre()).toBe('hidden');

    await userEvent.keyboard('{Escape}');
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { hidden: true })).toBeNull();
    });
    expect(corre()).not.toBe('hidden');
  });

  it('lo de fuera queda inerte: el foco no sale de la ventana', async () => {
    await abrir();
    const titulo = screen.getByRole('heading', { name: 'La pregunta' });

    // Por programa, que es lo que ni el ratón ni el tabulador pueden saltarse.
    screen.getByRole('button', { name: 'Fuera' }).focus();

    expect(document.activeElement).toBe(titulo);
  });

  it.each([
    [
      'Escape',
      async (): Promise<void> => {
        await userEvent.keyboard('{Escape}');
      },
    ],
    [
      'la salida de dentro',
      async (): Promise<void> => {
        await userEvent.click(screen.getByRole('button', { name: 'Salir' }));
      },
    ],
  ])('%s la cierra una vez, y el foco vuelve a quien la abrió', async (_, cerrar) => {
    const { alCerrar, abrirla } = await abrir();

    await cerrar();

    await waitFor(() => {
      expect(screen.queryByRole('dialog', { hidden: true })).toBeNull();
    });
    expect(cierres).toBe(1);
    expect(alCerrar).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(abrirla);
  });
});
