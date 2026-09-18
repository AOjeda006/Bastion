import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type * as Diccionarios from './i18n/diccionarios.ts';
import { crearI18nDelArranque } from './i18n/index.ts';
import type { Idioma } from './i18n/idioma.ts';
import { ALFA } from '@/pruebas/datos.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';

/**
 * EL ARRANQUE SE QUEDA CON UN IDIOMA — ítem 2.1.
 *
 * Que el diccionario del idioma no activo salga del arranque se mide en `dist/`, no aquí: lo dice
 * el paso «Presupuesto de tamaño» de la CI. Lo que este fichero comprueba es lo que esa medida NO
 * puede decir —qué se ve mientras el diccionario viaja— y lo que la medida da por hecho: que la
 * instancia con la que arranca la aplicación trae UN idioma y no dos.
 *
 * **La descarga se retiene a propósito.** Sin retenerla, «mientras llega, la pantalla sigue en el
 * idioma viejo» sería una carrera que se gana casi siempre: la promesa se resuelve en un turno de
 * microtareas y la aserción llegaría tarde o pronto según el día. Con el cargador detenido, la
 * ventana en la que se mira dura exactamente lo que el test quiera, y lo que se afirma es una
 * propiedad y no una suerte. Se sustituye el CARGADOR y no el diccionario: lo que se quiere
 * gobernar es cuándo llega, y el texto que llega es el de verdad.
 */
const cargaDelIngles = vi.hoisted(() => {
  let soltar: (() => void) | undefined;

  const espera = new Promise<void>((resolver) => {
    soltar = resolver;
  });

  return {
    espera,
    soltar: (): void => {
      soltar?.();
    },
  };
});

vi.mock('@/app/i18n/diccionarios.ts', async (importarDeVerdad) => {
  const deVerdad = await importarDeVerdad<typeof Diccionarios>();

  return {
    ...deVerdad,
    cargarDiccionario: async (idioma: Idioma) => {
      if (idioma === 'en') {
        await cargaDelIngles.espera;
      }

      return deVerdad.cargarDiccionario(idioma);
    },
  };
});

describe('El arranque con un idioma', () => {
  it('la instancia del arranque trae el diccionario del idioma activo, y solo ese', async () => {
    const i18n = await crearI18nDelArranque('es');

    // El que se usa está ANTES de que haya árbol: es lo que sostiene el `useSuspense: false`.
    expect(i18n.hasResourceBundle('es', 'traduccion')).toBe(true);
    expect(i18n.t('comun.salir')).toBe('Salir');

    // Y el que no se usa no está. Esto es la contrapartida en comportamiento de los bytes que el
    // presupuesto ve desaparecer: si alguien devolviera el import estático, aquí saldría `true`
    // aunque la cifra de la CI tardara otra fase en delatarlo.
    expect(i18n.hasResourceBundle('en', 'traduccion')).toBe(false);
  });

  it('mientras el diccionario nuevo viaja, la pantalla sigue ENTERA en el idioma anterior', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/almacenes', 'es');

    expect(await screen.findByRole('heading', { level: 1, name: 'Almacenes' })).toBeInTheDocument();

    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');

    // La descarga está retenida, así que esta ventana no se acaba sola. Lo que se mira son tres
    // sitios distintos y el `lang`: un cambio que se adelantara al diccionario dejaría aquí las
    // claves en crudo —`rutas.almacenes` en vez de «Almacenes»—, que es el parpadeo que se evita.
    expect(screen.getByRole('heading', { level: 1, name: 'Almacenes' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Almacenes' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salir' })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('es');

    cargaDelIngles.soltar();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Warehouses' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('en');
  });
});
