// EL CARRIL DEL NAVEGADOR (ítem 2.20): los casos que miden la disposición de verdad, en un
// Chromium sin cabeza. jsdom no dispone nada, y un ancho medido allí sale a cero. Corre con
// `npm run test:navegador`, aparte de `npm run test`, que excluye estos ficheros.
//
// Es el Playwright que el §3 del plan maestro pone para los tests del frontal, por debajo de vitest:
// el mismo `describe`, los mismos manejadores de msw y la misma `montarAplicacion` que el carril de
// jsdom, en otra caja. Lo de extremo a extremo sobre el *compose* sigue siendo otra cosa.
//
// LICENCIAS, comprobadas antes de adoptarlos: `@vitest/browser-playwright` **MIT**, y lo que trae,
// `@vitest/browser` **MIT** y `@vitest/mocker` **MIT**; `playwright` y `playwright-core` 1.64.0,
// **Apache-2.0**. Lo demás que entra en el lockfile, todo **MIT**: `@blazediff/core`, `@polka/url`,
// `mrmime`, `pngjs`, `sirv`, `totalist` y `ws`. Anotadas aquí, junto a la adopción, y no en el
// mensaje del commit.
//
// Y LA AUDITORÍA DE LA HERRAMIENTA, leída al adoptarlos: los dos avisos moderados de
// `@vitest/mocker` 4.1.11 (GHSA-qxg6-89mg-rqv8 y GHSA-xp26-3454-mjgp) ya estaban en la base, porque
// lo trae `vitest`, pero tocan justo a este modo: el registro de *mocks* del servidor del navegador.
// Se corrigen en 4.1.12, y la subida está propuesta en el PLAN, no hecha. El servidor escucha solo
// en `localhost`, y la auditoría que decide, la de lo que llega al navegador, no los ve.
import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';

import base from './vite.config.ts';

export default defineConfig({
  plugins: base.plugins,
  resolve: base.resolve,
  test: {
    include: ['src/**/*.navegador.test.tsx'],
    setupFiles: ['./src/pruebas/navegador.ts'],
    browser: {
      enabled: true,
      provider: playwright(),
      headless: true,
      // El ancho, fijado aquí y afirmado en el caso: es el del 1.4.10 de WCAG.
      instances: [{ browser: 'chromium', viewport: { width: 320, height: 800 } }],
      // Sin capturas: en la CI nadie las sube, y el rojo ya dice qué se sale y hasta dónde.
      screenshotFailures: false,
    },
    // El mismo informe fijado que el carril de jsdom (ADR-0058). Aquí no hay canal de `act()`: sin
    // `globals`, Testing Library enciende el entorno de `act()` de React solo dentro de sus propias
    // llamadas, y React no avisa de lo que pasa fuera. El registro del carril no lleva consola.
    reporters: base.test?.reporters,
  },
});
