import { useQueryClient } from '@tanstack/react-query';
import { Suspense, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, Outlet, useLocation } from 'react-router';

import { LimiteDeError } from './LimiteDeError.tsx';
import { SelectorDeEmpresa } from './SelectorDeEmpresa.tsx';
import { SelectorDeIdioma } from './SelectorDeIdioma.tsx';
import { navegablesPara } from './rutas.tsx';
import { useDeclaracionDeRuta } from './useDeclaracionDeRuta.ts';
import { cerrarSesion } from '@/shared/api/sesiones.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';
import { useSesion } from '@/shared/sesion/useSesion.ts';
import { Cargando } from '@/shared/ui/Estados.tsx';

/**
 * El armazón de la aplicación, y los tres pasos del cambio de ruta accesible.
 *
 * Un enrutador de cliente cambia de pantalla sin recargar: para un lector de pantalla eso es
 * SILENCIO, y el foco del teclado se queda donde estaba —normalmente en el enlace que se acaba de
 * pulsar, que ya no existe—. Cumplir las WCAG al pie de la letra no basta, porque no obligan a
 * emitir mensajes de estado; en una SPA hay que emitirlos (`ux-ipo`, sección SPA).
 *
 *   1. `role="status" aria-live="polite"` con «La página X se ha cargado». Está SIEMPRE en el DOM
 *      y se esconde con la técnica de recorte (`sr-only`), nunca con `display:none` ni
 *      `visibility:hidden` — eso lo escondería también del lector, que es justo a quien va dirigido.
 *   2. `<title>` distinto en cada vista. Sale de la misma declaración que el `<h1>` y que el
 *      mensaje, así que no pueden desalinearse.
 *   3. El foco, a mano, al `<h1>` del `<main>`, con `tabIndex={-1}` — **nunca `0`**, que lo metería
 *      en el orden de tabulación y le daría a todo el mundo una parada de más en cada pantalla.
 */
export function Disposicion(): React.JSX.Element {
  const { t } = useTranslation();
  const declaracion = useDeclaracionDeRuta();
  const ubicacion = useLocation();
  const sesion = useSesion();
  const cache = useQueryClient();

  const encabezado = useRef<HTMLHeadingElement>(null);
  const yaSeHaNavegado = useRef(false);

  const titulo = t(`rutas.${declaracion.claveDeTitulo}`);

  useEffect(() => {
    document.title = t('comun.tituloDeDocumento', { titulo });

    // En la primera pintada NO se mueve el foco: eso no es una navegación interna, es la carga de
    // la página, y de ella ya se encarga el navegador. Robarle el foco ahí desplazaría la vista
    // sin que nadie haya pedido ir a ninguna parte.
    if (yaSeHaNavegado.current) {
      encabezado.current?.focus();
    }

    yaSeHaNavegado.current = true;
    // `t` entra en las dependencias porque cambia al cambiar de idioma: sin ella, el `<title>` se
    // quedaría en el idioma anterior hasta la siguiente navegación.
  }, [t, titulo, ubicacion.key]);

  const anuncio = t('comun.paginaCargada', { titulo });

  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        {t('comun.saltarAlContenido')}
      </a>

      <div
        role="status"
        aria-live="polite"
        aria-label={t('comun.estadoDeLaNavegacion')}
        className="sr-only"
      >
        {anuncio}
      </div>

      {sesion !== null && (
        <header className="border-b border-neutral-200 bg-white">
          <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-4 px-4 py-3">
            <span className="text-lg font-semibold tracking-tight">Bastion</span>

            <nav aria-label={t('comun.navegacionPrincipal')} className="flex gap-1">
              {navegablesPara(sesion.permisos).map((ruta) => (
                <NavLink
                  key={ruta.ruta}
                  to={ruta.ruta}
                  end={ruta.ruta === '/'}
                  className={({ isActive }) =>
                    `rounded px-3 py-1.5 text-sm ${
                      isActive
                        ? 'bg-neutral-900 text-white'
                        : 'text-neutral-700 hover:bg-neutral-100'
                    }`
                  }
                >
                  {t(`rutas.${ruta.claveDeTitulo}`)}
                </NavLink>
              ))}
            </nav>

            <div className="ml-auto flex items-center gap-4">
              <SelectorDeIdioma />
              <SelectorDeEmpresa />
              <span className="text-sm text-neutral-500">{sesion.nombre}</span>
              <button
                type="button"
                onClick={() => {
                  void cerrarSesion().finally(() => {
                    // Al salir se vacía con `clear()` y NO se reinicia: reiniciar volvería a pedir
                    // lo que hubiera en pantalla, y lo que hay que hacer con ello es tirarlo.
                    escribirSesion(null);
                    cache.clear();
                  });
                }}
                className="rounded border border-neutral-300 px-3 py-1.5 text-sm hover:bg-neutral-100"
              >
                {t('comun.salir')}
              </button>
            </div>
          </div>
        </header>
      )}

      <main id="contenido" className="mx-auto max-w-5xl px-4 py-6">
        <h1 ref={encabezado} tabIndex={-1} className="text-2xl font-semibold tracking-tight">
          {titulo}
        </h1>

        {/*
          La llave por ruta reinicia el límite de error al navegar: sin ella, una pantalla rota deja
          el mensaje de error puesto para siempre, también en la pantalla siguiente.
        */}
        <LimiteDeError key={ubicacion.pathname}>
          <Suspense fallback={<Cargando que={t('comun.laPantalla')} />}>
            <Outlet />
          </Suspense>
        </LimiteDeError>
      </main>
    </div>
  );
}
