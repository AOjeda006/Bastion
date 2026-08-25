// `defineConfig` de `vitest/config` (no de `vite`): es la que conoce la clave `test`.
import { defineConfig } from 'vitest/config';
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
    sourcemap: true,
    // Presupuesto de tamaño: la CI avisa si un fragmento se dispara. Un ERP acaba
    // cargando tablas, gráficas y un cliente de API generado; sin un tope, el arranque
    // se degrada un poco en cada sprint y nadie lo nota hasta que molesta.
    chunkSizeWarningLimit: 600,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    css: false,
  },
});
