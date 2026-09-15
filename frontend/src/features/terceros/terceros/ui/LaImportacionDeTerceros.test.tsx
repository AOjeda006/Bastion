import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { clavesDeTerceros } from '../api/claves.ts';
import { CABECERA_DE_LA_PLANTILLA } from '../model/importacion.ts';
import { PaginaDeImportacion } from './PaginaDeImportacion.tsx';
import { PaginaDeTerceros } from './PaginaDeTerceros.tsx';
import { PERMISOS_DE_LECTURA } from '@/pruebas/datos.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { abrirSesionYaRecuperada, servidor } from '@/pruebas/servidor.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

type InformeDto = components['schemas']['InformeDeImportacionDto'];

const RUTA = '/api/v1/terceros/terceros/importacion';

/** Lo que llegó al servidor simulado en cada envío. */
interface Recibido {
  readonly bytes: Uint8Array;
  readonly tipo: string | null;
  readonly clave: string | null;
}

/**
 * Un CSV como lo guarda Excel en español con «CSV (delimitado por comas)»: Windows-1252.
 *
 * La «ñ» va como el byte 0xF1, que en UTF-8 no es un carácter sino el principio de uno roto. Es el
 * byte que se estropearía si la pantalla leyera el fichero como texto antes de mandarlo, y por eso
 * está aquí. Los datos de la fila no son de nadie: el servidor es simulado y no los lee.
 */
function csvDeExcel(razonSocial: string): Uint8Array<ArrayBuffer> {
  const texto = `${CABECERA_DE_LA_PLANTILLA.join(';')}\r\nZZ;SIN-IDENTIFICADOR;${razonSocial};\r\n`;

  return Uint8Array.from(texto, (caracter) => {
    const codigo = caracter.codePointAt(0) ?? 0;

    // Solo lo que cabe en un byte: el test no necesita más, y un carácter que no quepa rompería la
    // premisa en silencio.
    if (codigo > 0xff) {
      throw new Error(`«${caracter}» no cabe en Windows-1252 tal como lo escribe este test`);
    }

    return codigo;
  });
}

function ficheroCsv(nombre: string, bytes: Uint8Array<ArrayBuffer>): File {
  return new File([bytes], nombre, { type: 'text/csv' });
}

const INFORME_LIMPIO: InformeDto = { leidas: 1, importadas: 1, rechazadas: 0, rechazos: [] };

/** Registra lo que llega y contesta, por orden, lo que se le dé. La última respuesta se repite. */
function servidorDeImportacion(...respuestas: (() => Response)[]): Recibido[] {
  const recibidos: Recibido[] = [];

  servidor.use(
    http.post(RUTA, async ({ request }) => {
      recibidos.push({
        bytes: new Uint8Array(await request.arrayBuffer()),
        tipo: request.headers.get('Content-Type'),
        clave: request.headers.get('Idempotency-Key'),
      });

      const responder = respuestas[Math.min(recibidos.length, respuestas.length) - 1];

      if (responder === undefined) {
        throw new Error('servidorDeImportacion necesita al menos una respuesta');
      }

      return responder();
    }),
  );

  return recibidos;
}

function selectorDeFichero(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Fichero CSV');
}

/**
 * La pantalla de importación de terceros.
 *
 * Lo que decide el servidor —el dialecto, las filas, los permisos— lo prueba el carril de la API con
 * ficheros de verdad escritos por Excel. Aquí se prueba lo que solo puede romper la pantalla: que los
 * bytes salgan sin tocar, que la clave de idempotencia vaya con la elección y no con el envío, que el
 * informe diga dónde y por qué sin inventar, y que el listado se entere.
 */
describe('La importación de terceros', () => {
  it('manda los bytes del fichero tal cual, como text/csv y con clave', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    const recibidos = servidorDeImportacion(() => HttpResponse.json(INFORME_LIMPIO));
    const bytes = csvDeExcel('Cañas de Prueba SL');

    // La premisa, comprobada y no supuesta: si el fichero no llevara el byte de Windows-1252, este
    // test pasaría también con una pantalla que decodifica como UTF-8.
    expect(bytes).toContain(0xf1);

    montarPantalla(<PaginaDeImportacion />, '/terceros/importacion');

    // La cabecera que se enseña es la que se manda copiar: una sola línea, con punto y coma.
    expect(screen.getByText(CABECERA_DE_LA_PLANTILLA.join(';'))).toBeVisible();

    await usuario.upload(selectorDeFichero(), ficheroCsv('proveedores.csv', bytes));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));

    expect(await screen.findByText('Han entrado todas las filas.')).toBeVisible();
    expect(recibidos).toHaveLength(1);
    expect(recibidos[0]?.bytes).toEqual(bytes);
    expect(recibidos[0]?.tipo).toBe('text/csv');
    expect(recibidos[0]?.clave).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/,
    );
  });

  it('el informe dice cuántas, y de cada rechazo la columna, el motivo y las líneas', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorDeImportacion(() =>
      HttpResponse.json({
        leidas: 5,
        importadas: 2,
        rechazadas: 3,
        rechazos: [
          { columna: 'identificacion_numero', motivo: 'no-valido', lineas: [2, 6] },
          { columna: null, motivo: 'numero-de-campos-distinto', lineas: [4] },
          // Un motivo que esta versión no conoce: llega antes que el despliegue que lo traduce.
          { columna: 'territorio', motivo: 'motivo-de-una-version-futura', lineas: [5] },
        ],
      }),
    );

    montarPantalla(<PaginaDeImportacion />, '/terceros/importacion');

    await usuario.upload(selectorDeFichero(), ficheroCsv('proveedores.csv', csvDeExcel('Alta SL')));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));

    const informe = await screen.findByRole('region', { name: 'Resultado de la importación' });

    expect(within(informe).getByText('Filas leídas').nextElementSibling).toHaveTextContent('5');
    expect(within(informe).getByText('Importadas').nextElementSibling).toHaveTextContent('2');
    expect(within(informe).getByText('Rechazadas').nextElementSibling).toHaveTextContent('3');
    expect(within(informe).queryByText('Han entrado todas las filas.')).not.toBeInTheDocument();

    const filas = within(within(informe).getByRole('table')).getAllByRole('row').slice(1);

    expect(
      filas.map((fila) =>
        within(fila)
          .getAllByRole('cell')
          .map((c) => c.textContent),
      ),
    ).toEqual([
      [
        'identificacion_numero',
        'Se lee, pero no es un valor válido: por ejemplo, un NIF con la letra que no le toca.',
        '2, 6',
      ],
      ['La fila entera', 'La fila no tiene tantas columnas como la cabecera.', '4'],
      [
        'territorio',
        'Esta versión de la pantalla no sabe explicar este motivo. Avisa a quien administre Bastion.',
        '5',
      ],
    ]);
  });

  it('reintentar tras un fallo sin nombre repite la clave, y otro fichero estrena otra', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    const recibidos = servidorDeImportacion(
      () => new HttpResponse(null, { status: 503 }),
      () => HttpResponse.json(INFORME_LIMPIO),
    );

    montarPantalla(<PaginaDeImportacion />, '/terceros/importacion');

    await usuario.upload(selectorDeFichero(), ficheroCsv('proveedores.csv', csvDeExcel('Uno SL')));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));
    await usuario.click(
      within(await screen.findByRole('alert')).getByRole('button', { name: 'Volver a intentarlo' }),
    );

    expect(await screen.findByText('Han entrado todas las filas.')).toBeVisible();

    // La misma operación: el servidor, si la primera sí llegó a escribir, contesta el informe
    // guardado en vez de importar dos veces.
    expect(recibidos.map((r) => r.clave)).toEqual([recibidos[0]?.clave, recibidos[0]?.clave]);

    // Elegir otro fichero deja fuera el informe anterior, que ya no habla de lo elegido.
    await usuario.upload(selectorDeFichero(), ficheroCsv('clientes.csv', csvDeExcel('Dos SL')));
    expect(screen.queryByText('Han entrado todas las filas.')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Importar' }));
    expect(await screen.findByText('Han entrado todas las filas.')).toBeVisible();

    expect(recibidos).toHaveLength(3);
    expect(recibidos[2]?.clave).not.toBeNull();
    expect(recibidos[2]?.clave).not.toBe(recibidos[0]?.clave);
  });

  it('un error con nombre dice qué cambiar y no ofrece repetir lo mismo', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorDeImportacion(() =>
      HttpResponse.json(
        { type: '/errors/importacion-separador-no-admitido', status: 400 },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    );

    montarPantalla(<PaginaDeImportacion />, '/terceros/importacion');

    await usuario.upload(selectorDeFichero(), ficheroCsv('proveedores.csv', csvDeExcel('Tres SL')));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));

    const aviso = await screen.findByRole('alert');

    expect(aviso).toHaveTextContent(
      'El fichero no separa las columnas con punto y coma. Guárdalo desde Excel con la configuración regional de España.',
    );
    expect(within(aviso).queryByRole('button')).not.toBeInTheDocument();
  });

  it('si entra alguno, el listado de terceros queda por volver a pedir; si no entra ninguno, no', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorDeImportacion(
      () => HttpResponse.json({ leidas: 1, importadas: 0, rechazadas: 1, rechazos: [] }),
      () => HttpResponse.json(INFORME_LIMPIO),
    );

    const { cache } = montarPantalla(<PaginaDeImportacion />, '/terceros/importacion');
    const listado = [...clavesDeTerceros.listas(), 'la-que-hubiera-abierta'];
    cache.setQueryData(listado, { elementos: [], pagina: 1, tamanio: 20, total: 0 });

    await usuario.upload(selectorDeFichero(), ficheroCsv('proveedores.csv', csvDeExcel('Cero SL')));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));

    // El informe se pinta cuando la mutación ya ha terminado su `onSuccess`: lo que haya hecho con
    // la caché está hecho, y lo que no, no va a pasar después.
    expect(
      await screen.findByRole('region', { name: 'Resultado de la importación' }),
    ).toBeVisible();
    expect(cache.getQueryState(listado)?.isInvalidated).toBe(false);

    await usuario.upload(selectorDeFichero(), ficheroCsv('clientes.csv', csvDeExcel('Uno SL')));
    await usuario.click(screen.getByRole('button', { name: 'Importar' }));

    expect(await screen.findByText('Han entrado todas las filas.')).toBeVisible();
    expect(cache.getQueryState(listado)?.isInvalidated).toBe(true);
  });

  it('el listado enseña el enlace a importar solo a quien puede importar', async () => {
    abrirSesionYaRecuperada(undefined, [...PERMISOS_DE_LECTURA, PERMISOS.terceroImportar]);
    const conPermiso = montarPantalla(<PaginaDeTerceros />, '/terceros');

    const enlace = await screen.findByRole('link', { name: 'Importar desde un CSV' });
    expect(enlace).toHaveAttribute('href', '/terceros/importacion');
    conPermiso.unmount();

    abrirSesionYaRecuperada(undefined, PERMISOS_DE_LECTURA);
    montarPantalla(<PaginaDeTerceros />, '/terceros');

    // Esperando a que el listado esté pintado: antes de eso, la ausencia del enlace no dice nada.
    expect(await screen.findByText('Ferretería Industrial del Sur SL')).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Importar desde un CSV' })).not.toBeInTheDocument();
  });
});
