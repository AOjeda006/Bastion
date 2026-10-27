// `defineConfig` de `vitest/config` (no de `vite`): es la que conoce la clave `test`.
import { configDefaults, defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // La API se sirve por el mismo origen en desarrollo: así la cookie `httpOnly` del
    // refresh viaja sin CORS y sin `SameSite=None`, que es lo que se quiere en producción.
    proxy: {
      '/api': {
        target: process.env.VITE_API_URL ?? 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    // `'hidden'` y no `true`: el mapa se genera —hace falta para leer una traza de producción—
    // pero el fragmento no lo anuncia con su comentario `sourceMappingURL`, así que ningún
    // navegador se lo descarga solo. Y la imagen de nginx no lo sirve: `Dockerfile.web` lo borra
    // del raíz servido, porque un mapa publicado es el código fuente publicado.
    sourcemap: 'hidden',
    // El manifiesto, `.vite/manifest.json`, es lo que lee el presupuesto por ruta (ADR-0071): dice
    // qué importa cada fragmento, y eso en el `dist` no está escrito. Como el mapa, no se sirve:
    // `Dockerfile.web` lo borra, porque nombra cada fichero del fuente y el navegador no lo pide.
    manifest: true,
    // Presupuesto de tamaño: la CI avisa si un fragmento se dispara. Un ERP acaba
    // cargando tablas, gráficas y un cliente de API generado; sin un tope, el arranque
    // se degrada un poco en cada sprint y nadie lo nota hasta que molesta.
    chunkSizeWarningLimit: 600,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    // Los del carril del navegador miden la disposición, y aquí no hay ninguna: van con
    // `vitest.navegador.config.ts` (ítem 2.20).
    exclude: [...configDefaults.exclude, 'src/**/*.navegador.test.tsx'],
    css: false,
    // El informe, fijado, y no el que elija vitest según quién lo lance. Sin esta línea, vitest 4
    // detecta un agente de IA por el entorno (`AI_AGENT`, `CLAUDECODE`…) y cambia al informe
    // `agent`, que se calla la consola de los casos en verde: los avisos de `act()` dejan de salir
    // en el registro, y el cero de la batería ya no mide nada. Medido con un canario que provoca
    // uno: 1 con esta línea, 0 sin ella. Fijarlo apaga también lo otro que vitest pone cuando no se
    // configura nada: `github-actions`, que en la CI anota los casos rojos y escribe el resumen del
    // job. Por eso se pide aquí, con la misma condición con la que lo pide vitest.
    reporters: process.env.GITHUB_ACTIONS === 'true' ? ['default', 'github-actions'] : ['default'],
  },
});
