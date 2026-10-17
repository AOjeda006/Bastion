import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ALFA } from '@/pruebas/datos.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';

/**
 * LA CABECERA CABE EN 320 PX (WCAG 1.4.10, *Reflow*, AA; respuesta 7 del repaso de la fase 2).
 *
 * Lo que NO afirma: que quepa. jsdom no maqueta —todo mide 0—, así que aquí no hay ancho que medir.
 * Se midió con playwright-cli contra la pila local, a 320 y a 375 px, en siete pantallas y en los
 * dos idiomas: antes del 2.15 el documento medía 815 px; después, lo que mide la ventana. La medida
 * está en `docs/PLAN.md`, en el 2.15.
 *
 * Lo que SÍ afirma son las dos reglas que esa medida dejó, sobre la cabecera de verdad y con dos
 * empresas, para que el selector de empresa sea un `<select>` y no el párrafo de la empresa única:
 *
 *   1. Toda fila flexible de la cabecera envuelve. Deciden dos: la navegación, que sin envolver
 *      llega a 720 px, y el bloque de la derecha, que sin envolver cabe, pero aprieta el selector
 *      de idioma a 18 px y el de empresa a 84: eso es perder información, que el 1.4.10 tampoco
 *      admite. Las otras tres envuelven para que cada rótulo baje a su línea.
 *   2. Toda caja entre la fila de la cabecera y un `<select>` lleva `min-w-0`, que es lo que le deja
 *      estrecharse por debajo de su contenido. Deciden las tres del selector de empresa: quitarlo
 *      de una sola deja el documento en 480 px. En las del de idioma no decide hoy, porque sus dos
 *      opciones son cortas; está para que el próximo `<select>` de la cabecera herede la regla.
 *
 * Y cada regla afirma que encontró lo que mira: una regla que deja de mirar no se pone roja.
 */

/** Las clases de un elemento, como fichas sueltas: `flex` no es `flex-wrap` ni `inline-flex`. */
function clases(elemento: Element): string[] {
  return (elemento.getAttribute('class') ?? '').split(/\s+/);
}

/** Un nombre que se lee en el mensaje de un rojo: la etiqueta, el `id` o la etiqueta accesible. */
function nombre(elemento: Element): string {
  const etiqueta = elemento.getAttribute('aria-label');

  return [
    elemento.tagName.toLowerCase(),
    elemento.id === '' ? '' : `#${elemento.id}`,
    etiqueta === null ? '' : `[${etiqueta}]`,
  ].join('');
}

/** Las cajas desde el `<select>`, incluido, hasta la fila de la cabecera, sin incluirla. */
function cajasHastaLaFila(selector: Element, fila: Element): Element[] {
  const cajas: Element[] = [];

  for (
    let caja: Element | null = selector;
    caja !== null && caja !== fila;
    caja = caja.parentElement
  ) {
    cajas.push(caja);
  }

  return cajas;
}

async function cabeceraConDosEmpresas(): Promise<HTMLElement> {
  abrirSesionSimulada(ALFA.id);
  montarAplicacion('/');

  return screen.findByRole('banner');
}

describe('La cabecera a 320 px', () => {
  it('toda fila flexible de la cabecera envuelve', async () => {
    const cabecera = await cabeceraConDosEmpresas();

    const filas = [...cabecera.querySelectorAll('*')].filter((e) => clases(e).includes('flex'));

    // La fila de la cabecera, la navegación, el bloque de la derecha y los dos selectores.
    expect(filas.map(nombre)).toEqual(['div', 'nav[Principal]', 'div', 'div', 'div']);
    expect(filas.filter((e) => !clases(e).includes('flex-wrap')).map(nombre)).toEqual([]);
  });

  it('toda caja entre la fila de la cabecera y un <select> se puede estrechar', async () => {
    const cabecera = await cabeceraConDosEmpresas();
    const fila = cabecera.firstElementChild;

    expect(fila).not.toBeNull();

    const selectores = within(cabecera).getAllByRole('combobox');
    const cajas = selectores.flatMap((selector) => cajasHastaLaFila(selector, fila!));

    // Por cada `<select>`, él mismo, su envoltorio y el bloque de la derecha. Si el camino no
    // llegara a la fila, subiría hasta la raíz del documento y saldrían muchas más.
    expect(selectores.map(nombre)).toEqual(['select#idioma', 'select#empresa-activa']);
    expect(cajas.map(nombre)).toEqual([
      'select#idioma',
      'div',
      'div',
      'select#empresa-activa',
      'div',
      'div',
    ]);
    expect(cajas.filter((e) => !clases(e).includes('min-w-0')).map(nombre)).toEqual([]);
  });
});
