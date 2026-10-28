import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { expect, onTestFinished, vi } from 'vitest';

import { servidor } from './servidor.ts';

/**
 * Las exportaciones de un test: lo que se ha pedido al servidor y lo que el navegador ha guardado.
 *
 * **Se dobla en las dos fronteras, y en ninguna más.** La de red, con un manejador de MSW que
 * contesta a toda ruta `…/exportacion` con un CSV y su `Content-Disposition`, como la API. Y la del
 * navegador, que es la que jsdom no tiene: `URL.createObjectURL` no existe, y pulsar un enlace
 * `download` no guarda nada. Entre las dos corre la pantalla de verdad, con su cliente y su caché.
 *
 * **El día del nombre es uno que el navegador no puede saber**, el 1999-12-31: así, que el fichero
 * se guarde con él demuestra que el nombre sale de la cabecera, y no de un reloj del navegador que
 * acertaría el mismo día en que corre el test.
 *
 * El manejador se pone con `servidor.use`, que manda sobre los demás: una ruta como
 * `/api/v1/inventario/recuentos/:id` también casa con `…/recuentos/exportacion`, y sin eso la
 * exportación le llegaría a la ficha.
 */
export interface Exportaciones {
  /** Lo que se ha pedido a una exportación, el camino y la consulta, una entrada por petición. */
  readonly pedidas: string[];
  /** Lo que el navegador ha guardado, una entrada por fichero. */
  readonly guardadas: Guardada[];
  /** Las URL `blob:` que se han revocado: la memoria del fichero, devuelta. */
  readonly revocadas: string[];
}

/** Un fichero guardado: con qué nombre, con qué URL y lo que llevaba. */
export interface Guardada {
  readonly nombre: string;
  readonly url: string;
  readonly contenido: Blob | undefined;
  /** Si el enlace estaba en la página al pulsarlo, que es lo que algunos navegadores exigen. */
  readonly enLaPagina: boolean;
}

/** El día que lleva el nombre de todos los ficheros del servidor simulado. */
export const DIA_DEL_SERVIDOR = '1999-12-31';

/** Lo que manda el servidor simulado en cada fichero: el BOM y una cabecera. */
export const CSV_DEL_SERVIDOR = '﻿codigo;nombre\r\n';

/**
 * Cómo contesta el servidor simulado: bien, tarde o con un fallo. Se lee en cada petición, así que
 * un test puede cambiarlo entre un intento y el siguiente.
 */
export interface Contestacion {
  /** El estado del fallo, o `null` para mandar el fichero. */
  fallo?: number | null;
  /** Cuánto tarda en contestar, en milisegundos. */
  espera?: number;
}

/**
 * El nombre que el servidor simulado le pone al fichero de una ruta: el recurso, que es el segmento
 * de antes de `exportacion`, y {@link DIA_DEL_SERVIDOR}.
 */
export function nombreDelServidor(camino: string): string {
  const segmentos = camino.split('/');

  return `${segmentos.at(-2) ?? ''}-${DIA_DEL_SERVIDOR}.csv`;
}

function restaurar(nombre: string, descriptor: PropertyDescriptor | undefined): void {
  if (descriptor === undefined) {
    Reflect.deleteProperty(URL, nombre);
  } else {
    Object.defineProperty(URL, nombre, descriptor);
  }
}

/** Pone el servidor de las exportaciones y el navegador que guarda, solo para este test. */
export function capturarExportaciones(contestacion: Contestacion = {}): Exportaciones {
  const exportaciones: Exportaciones = { pedidas: [], guardadas: [], revocadas: [] };
  const ficheros = new Map<string, Blob>();
  // Lo que hubiera, para dejarlo como estaba: jsdom no los tiene, pero el `URL` de Node sí.
  const antes = {
    crear: Object.getOwnPropertyDescriptor(URL, 'createObjectURL'),
    revocar: Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL'),
  };

  servidor.use(
    http.get('/api/v1/*/exportacion', async ({ request }) => {
      const url = new URL(request.url);
      exportaciones.pedidas.push(url.pathname + url.search);

      if (contestacion.espera !== undefined) {
        await delay(contestacion.espera);
      }

      if (contestacion.fallo !== undefined && contestacion.fallo !== null) {
        return new HttpResponse(null, { status: contestacion.fallo });
      }

      const nombre = nombreDelServidor(url.pathname);

      return new HttpResponse(CSV_DEL_SERVIDOR, {
        headers: {
          'Content-Type': 'text/csv; charset=utf-8',
          'Content-Disposition': `attachment; filename=${nombre}; filename*=UTF-8''${nombre}`,
        },
      });
    }),
  );

  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    writable: true,
    value: (contenido: Blob): string => {
      const url = `blob:prueba/${String(ficheros.size)}`;
      ficheros.set(url, contenido);

      return url;
    },
  });
  Object.defineProperty(URL, 'revokeObjectURL', {
    configurable: true,
    writable: true,
    value: (url: string): void => {
      exportaciones.revocadas.push(url);
    },
  });

  const pulsar = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ): void {
    const url = this.getAttribute('href') ?? '';

    exportaciones.guardadas.push({
      nombre: this.download,
      url,
      contenido: ficheros.get(url),
      enLaPagina: this.isConnected,
    });
  });

  onTestFinished(() => {
    pulsar.mockRestore();
    restaurar('createObjectURL', antes.crear);
    restaurar('revokeObjectURL', antes.revocar);
  });

  return exportaciones;
}

/**
 * Pulsa «Exportar a CSV» y comprueba lo que tiene que pasar, entero.
 *
 * Una sola petición, a `camino` y con `consulta` exactamente: ni un filtro de menos, ni `page` ni
 * `size`. Un fichero guardado con el nombre que le puso el servidor, desde un enlace que estaba en
 * la página, con los bytes que mandó —el BOM incluido, que es lo que lee Excel para saber que es
 * UTF-8—. Y su URL, revocada.
 *
 * La consulta se compara como objeto y no como cadena: el orden de los parámetros no es contrato.
 *
 * @param exportaciones - Lo que ha capturado {@link capturarExportaciones}.
 * @param camino - La ruta de la exportación que tiene que pedirse, sin consulta.
 * @param consulta - Los parámetros que tiene que llevar, todos.
 */
export async function exportarYComprobar(
  exportaciones: Exportaciones,
  camino: string,
  consulta: Record<string, string> = {},
): Promise<void> {
  await userEvent.click(await screen.findByRole('button', { name: 'Exportar a CSV' }));

  await waitFor(() => {
    expect(exportaciones.guardadas).toHaveLength(1);
  });

  const pedidas = exportaciones.pedidas.map((pedida) => {
    const url = new URL(pedida, 'http://localhost');

    return { camino: url.pathname, consulta: Object.fromEntries(url.searchParams) };
  });
  expect(pedidas).toEqual([{ camino, consulta }]);

  const [guardada] = exportaciones.guardadas;
  expect(guardada).toMatchObject({ nombre: nombreDelServidor(camino), enLaPagina: true });

  const bytes = await guardada?.contenido?.arrayBuffer();
  expect(Array.from(new Uint8Array(bytes ?? new ArrayBuffer(0)))).toEqual(
    Array.from(new TextEncoder().encode(CSV_DEL_SERVIDOR)),
  );
  expect(exportaciones.revocadas).toEqual([guardada?.url]);
}
