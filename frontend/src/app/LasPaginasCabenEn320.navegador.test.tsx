import { screen, waitFor } from '@testing-library/react';
import { HttpResponse, http, type RequestHandler } from 'msw';
import { describe, expect, it } from 'vitest';
import { userEvent } from 'vitest/browser';

import { RUTAS } from '@/app/rutas.tsx';
import {
  SERIE_DE_AJUSTES,
  SERIE_DE_TRANSFERENCIAS,
} from '@/features/inventario/maestros/api/maestros.ts';
import { ALFA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/manejadores.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { usarEnElNavegador } from '@/pruebas/navegador.ts';
import type { components } from '@/shared/api/esquema.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

type Esquemas = components['schemas'];

/**
 * LAS PÁGINAS CABEN EN 320 PX (ítem 2.20). Es el 1.4.10 de WCAG (*Reflow*, AA): a 320 px de ancho,
 * nada obliga a desplazarse en horizontal, salvo lo que no se puede leer de otra manera. Una tabla
 * de datos es de eso, y por eso corre ella, dentro de su contenedor; la página que la lleva, no.
 *
 * **Se mide, en un Chromium de verdad**, con la hoja de estilos entera: el ancho del documento es
 * el de la ventana, en cada ruta de `RUTAS`. `LaCabeceraCabeEn320` afirma las clases de la cabecera
 * en jsdom, que no dispone nada; este carril mide lo que esas clases hacen.
 *
 * **Y lleva su pareja en cada tabla**: con el contenedor quitado —su `overflow-x` a `visible`—, la
 * página se sale. Sin ella, una página que cabe porque sus datos son cortos pasaría igual el día que
 * se le quitara el contenedor. Así que los datos de aquí son largos a propósito, como los de una
 * empresa de verdad: códigos que no se parten, descripciones de catálogo y nombres de población.
 * Ninguno es de nadie; los NIF se han fabricado con el algoritmo, como los de `pruebas/datos.ts`.
 *
 * **Y lo que va en absoluto dentro de la tabla corre con ella.** Un `sr-only` es `position:
 * absolute`, y una caja con `overflow` solo recorta lo absoluto si es su bloque contenedor o lo
 * contiene. Sin `relative` en el contenedor, el rótulo oculto de la última columna de `/tarifas` se
 * quedaba en su sitio, a la derecha de la tabla, y empujaba la página: 322 px en la CI, con las
 * fuentes de Linux (run 40064764889), y 320 aquí, con las de Windows. Lo que depende de la fuente
 * no se mide con los datos. Por eso cada tabla lleva una sonda: un `sr-only` en su última celda,
 * con la tabla a 2000 px, que no puede mover la página; y su pareja, con el contenedor sin
 * posición, que sí la mueve, y que el diagnóstico tiene que nombrar.
 */

const ANCHO = 320;

const TODOS_LOS_PERMISOS = Object.values(PERMISOS);

const UNIDAD: Esquemas['UnidadMedidaDto'] = {
  id: 'aaaa0001-0000-0000-0000-000000000001',
  codigo: 'UD',
  nombre: 'Unidad',
  decimales: 0,
  retirada: false,
};

const DIRECCION: Esquemas['DireccionDto'] = {
  calle: 'Avenida de la Industria',
  numero: '27',
  codigoPostal: '41703',
  poblacion: 'Dos Hermanas',
  subdivision: 'SE',
  pais: 'ES',
};

const PLATAFORMA: Esquemas['AlmacenDto'] = {
  id: 'aaaaaaa1-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  codigo: 'ALM-PLATAFORMA-SUR',
  nombre: 'Plataforma logística de Dos Hermanas',
  direccion: DIRECCION,
  tipo: 'Fisico',
};

const MUELLE: Esquemas['AlmacenDto'] = {
  ...PLATAFORMA,
  id: 'aaaaaaa2-0000-0000-0000-000000000002',
  codigo: 'ALM-MUELLE-NORTE',
  nombre: 'Muelle de carga del polígono norte',
  direccion: { ...DIRECCION, poblacion: 'Alcalá de Guadaíra', codigoPostal: '41500' },
};

const UBICACION: Esquemas['UbicacionDto'] = {
  id: 'abababab-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  almacenId: PLATAFORMA.id,
  codigo: 'P03-E12-H04',
  pasillo: '03',
  estante: '12',
  hueco: '04',
  descripcion: 'Estantería de picking',
};

const ARTICULO: Esquemas['ArticuloDto'] = {
  id: 'fffffff1-0000-0000-0000-000000000001',
  empresaId: ALFA.id,
  codigo: 'TOR-DIN933-M8X40',
  descripcion: 'Tornillo hexagonal DIN 933 M8x40 de acero cincado',
  tipo: 'Bien',
  trazabilidad: 'PorLote',
  unidadBaseId: UNIDAD.id,
  impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
  categoriaId: null,
};

const SERIES: Esquemas['SerieDto'][] = [SERIE_DE_AJUSTES, SERIE_DE_TRANSFERENCIAS].map(
  (tipoDeDocumento, n) => ({
    id: `5e5e5e5e-0000-0000-0000-00000000000${String(n + 1)}`,
    empresaId: ALFA.id,
    ejercicioId: 'e1e1e1e1-0000-0000-0000-000000000001',
    tipoDeDocumento,
    codigo: `S${String(n + 1)}`,
    formato: '{SERIE}-{NUMERO}',
    contador: 0,
    estado: 'Activa',
  }),
);

function pagina<T>(elementos: T[]): {
  elementos: T[];
  pagina: number;
  tamanio: number;
  total: number;
} {
  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}

/** Los maestros, por su identificador y en listado: los piden casi todas las páginas. */
const MAESTROS: RequestHandler[] = [
  http.get('/api/v1/organizacion/almacenes', () => HttpResponse.json(pagina([PLATAFORMA, MUELLE]))),
  http.get('/api/v1/organizacion/almacenes/:id', ({ params }) =>
    HttpResponse.json(params['id'] === MUELLE.id ? MUELLE : PLATAFORMA),
  ),
  http.get('/api/v1/organizacion/ubicaciones', () => HttpResponse.json(pagina([UBICACION]))),
  http.get('/api/v1/organizacion/ubicaciones/:id', () => HttpResponse.json(UBICACION)),
  http.get('/api/v1/organizacion/unidades-de-medida', () => HttpResponse.json(pagina([UNIDAD]))),
  http.get('/api/v1/organizacion/unidades-de-medida/:id', () => HttpResponse.json(UNIDAD)),
  http.get('/api/v1/organizacion/series', () => HttpResponse.json(pagina(SERIES))),
  http.get('/api/v1/catalogo/articulos/:id', () =>
    HttpResponse.json(ARTICULO, { headers: { ETag: '"1"' } }),
  ),
];

const AJUSTE = 'adadadad-0000-0000-0000-000000000001';
const RECUENTO = 'cececece-0000-0000-0000-000000000001';
const TRANSFERENCIA = '7f7f7f7f-0000-0000-0000-000000000001';

/**
 * Lo que necesita cada ruta para pintarse con sus datos: qué contesta la API, qué hay que hacer
 * antes de medir, y el texto que dice que ya está. Una por cada ruta de `RUTAS`, ni una más: el
 * primer caso lo comprueba, así que una ruta nueva no se queda sin medir.
 */
interface Escenario {
  readonly camino: string;
  readonly manejadores: readonly RequestHandler[];
  /** El texto que sale cuando la página ya tiene sus datos. */
  readonly listo: string | RegExp;
  /** Cuántas tablas tiene la página con esos datos. */
  readonly tablas: number;
  readonly sinSesion?: true;
  readonly antes?: () => Promise<void>;
}

const ESCENARIOS: Record<string, Escenario> = {
  '/acceso': { camino: '/acceso', manejadores: [], listo: 'Entrar', tablas: 0, sinSesion: true },
  '/': { camino: '/', manejadores: [], listo: 'Ana Contable', tablas: 0 },
  '/ajustes': {
    camino: '/ajustes',
    manejadores: [
      http.get('/api/v1/inventario/ajustes', () =>
        HttpResponse.json(
          pagina<Esquemas['AjusteResumenDto']>([
            {
              id: AJUSTE,
              numero: 1042,
              almacenId: PLATAFORMA.id,
              fechaDeOperacion: '2026-09-30',
              estado: 'Confirmado',
              motivo: 'Regularización tras el inventario de septiembre',
            },
          ]),
        ),
      ),
    ],
    listo: 'Regularización tras el inventario de septiembre',
    tablas: 1,
  },
  '/ajustes/nuevo': { camino: '/ajustes/nuevo', manejadores: [], listo: /S1/, tablas: 1 },
  '/ajustes/:id': {
    camino: `/ajustes/${AJUSTE}`,
    manejadores: [
      http.get('/api/v1/inventario/ajustes/:id', () =>
        HttpResponse.json(
          {
            id: AJUSTE,
            serieId: SERIES[0]!.id,
            numero: 1042,
            almacenId: PLATAFORMA.id,
            fechaDeOperacion: '2026-09-30',
            motivo: 'Regularización tras el inventario de septiembre',
            estado: 'Confirmado',
            divisa: 'EUR',
            anulaAId: null,
            recuentoId: null,
            lineas: [
              {
                numero: 1,
                ubicacionId: UBICACION.id,
                articuloId: ARTICULO.id,
                cantidadIntroducida: 125,
                unidadIntroducidaId: UNIDAD.id,
                factorAUnidadBase: 1,
                cantidadEnUnidadBase: 125,
                costeUnitario: 0.0875,
                codigoDeLote: 'L-2026-09-000123',
                numeroDeSerie: null,
                valor: 10.9375,
              },
            ],
          } satisfies Esquemas['FichaDeAjusteDto'],
          { headers: { ETag: '"1"' } },
        ),
      ),
    ],
    listo: 'L-2026-09-000123',
    tablas: 1,
  },
  '/almacenes': {
    camino: '/almacenes',
    manejadores: [],
    listo: 'Plataforma logística de Dos Hermanas',
    tablas: 1,
  },
  '/articulos': { camino: '/articulos', manejadores: [], listo: 'Tornillo M6 zincado', tablas: 1 },
  '/articulos/:id/trazabilidad': {
    camino: `/articulos/${ARTICULO.id}/trazabilidad`,
    manejadores: [],
    listo: /TOR-DIN933-M8X40/,
    tablas: 0,
  },
  '/articulos/:id/gtin': {
    camino: `/articulos/${ARTICULO.id}/gtin`,
    manejadores: [
      http.get('/api/v1/catalogo/articulos/:articuloId/gtins', () =>
        HttpResponse.json<Esquemas['CodigoBarrasDto'][]>([
          {
            id: 'baba0001-0000-0000-0000-000000000001',
            empresaId: ALFA.id,
            articuloId: ARTICULO.id,
            gtin: '18412345678904',
            nivel: 'Caja',
            unidades: 100,
          },
        ]),
      ),
    ],
    listo: '18412345678904',
    tablas: 1,
  },
  '/categorias': {
    camino: '/categorias',
    manejadores: [
      http.get('/api/v1/catalogo/categorias', () =>
        HttpResponse.json(
          pagina<Esquemas['CategoriaDto']>([
            {
              id: 'eeeeeee1-0000-0000-0000-000000000001',
              empresaId: ALFA.id,
              codigo: 'FIJACIONES',
              nombre: 'Fijaciones',
              padreId: null,
            },
            {
              id: 'eeeeeee1-0000-0000-0000-000000000002',
              empresaId: ALFA.id,
              codigo: 'FIJ-TORN-INOX-A2',
              nombre: 'Tornillería inoxidable A2',
              padreId: 'eeeeeee1-0000-0000-0000-000000000001',
            },
          ]),
        ),
      ),
    ],
    listo: /Tornillería inoxidable A2/,
    tablas: 1,
  },
  '/empresas': {
    camino: '/empresas',
    manejadores: [
      http.get('/api/v1/organizacion/empresas', () =>
        HttpResponse.json(
          pagina<Esquemas['EmpresaDto']>([
            {
              id: ALFA.id,
              nif: 'B99999997',
              razonSocial: 'Distribuciones Ferreteras del Guadalquivir SL',
              domicilioFiscal: { ...DIRECCION, poblacion: 'Alcalá de Guadaíra' },
              divisaBase: 'EUR',
              regimenDeIva: 'General',
            },
          ]),
        ),
      ),
    ],
    listo: 'Distribuciones Ferreteras del Guadalquivir SL',
    tablas: 1,
  },
  '/recuentos': {
    camino: '/recuentos',
    manejadores: [
      http.get('/api/v1/inventario/recuentos', () =>
        HttpResponse.json(
          pagina<Esquemas['RecuentoResumenDto']>([
            {
              id: RECUENTO,
              numero: 87,
              almacenId: PLATAFORMA.id,
              fechaDeApertura: '2026-09-28',
              fechaDeConfirmacion: '2026-09-30',
              estado: 'Confirmado',
              motivo: 'Inventario de cierre del tercer trimestre',
            },
          ]),
        ),
      ),
    ],
    listo: 'Inventario de cierre del tercer trimestre',
    tablas: 1,
  },
  '/recuentos/:id': {
    camino: `/recuentos/${RECUENTO}`,
    manejadores: [
      http.get('/api/v1/inventario/recuentos/:id', () =>
        HttpResponse.json(
          {
            id: RECUENTO,
            serieId: SERIES[0]!.id,
            serieDelAjusteId: SERIES[0]!.id,
            numero: null,
            almacenId: PLATAFORMA.id,
            fechaDeApertura: '2026-09-28',
            fechaDeConfirmacion: null,
            estado: 'EnCurso',
            motivo: 'Inventario de cierre del tercer trimestre',
            divisa: 'EUR',
            motivoDelDescarte: null,
            motivoDeLaAnulacion: null,
            ajusteId: null,
            lineas: 1,
            lineasSinContar: 0,
            lineasConElTeoricoCambiado: 0,
            lineasConTransito: 0,
            huellaDelTeorico: 'a'.repeat(64),
          } satisfies Esquemas['RecuentoDto'],
          { headers: { ETag: '"1"' } },
        ),
      ),
      http.get('/api/v1/inventario/recuentos/:id/lineas', () =>
        HttpResponse.json(
          pagina<Esquemas['LineaDeRecuentoDto']>([
            {
              id: 'cececece-0000-0000-0001-000000000001',
              numero: 1,
              ubicacionId: UBICACION.id,
              articuloId: ARTICULO.id,
              codigoDeLote: 'L-2026-09-000123',
              numeroDeSerie: null,
              unidadBaseId: UNIDAD.id,
              origen: 'Precargada',
              costeUnitario: 0.0875,
              contado: 1250,
              teoricoAlContar: 1248,
              teorico: 1248,
              enTransito: 0,
              diferencia: 2,
              teoricoCambiado: false,
              lineaDeAjusteId: null,
            },
          ]),
        ),
      ),
    ],
    listo: 'L-2026-09-000123',
    tablas: 1,
  },
  '/saldos': {
    camino: '/saldos',
    manejadores: [
      // La lista de artículos del filtro, con el de la descripción de catálogo larga.
      http.get('/api/v1/catalogo/articulos', () => HttpResponse.json(pagina([ARTICULO]))),
      http.get('/api/v1/inventario/saldos', () =>
        HttpResponse.json<Esquemas['SaldosAUnaFechaDto']>({
          fecha: '2026-09-30',
          saldos: pagina([
            {
              articuloId: ARTICULO.id,
              almacenId: PLATAFORMA.id,
              cantidad: 12480,
              valor: 1092,
              cantidadEnTransito: 250,
              valorEnTransito: 21.875,
              divisa: 'EUR',
            },
          ]),
        }),
      ),
    ],
    listo: /TOR-DIN933-M8X40/,
    tablas: 1,
  },
  '/tarifas': { camino: '/tarifas', manejadores: [], listo: /MAYORISTA/, tablas: 1 },
  '/terceros': { camino: '/terceros', manejadores: [], listo: /SL/, tablas: 1 },
  '/terceros/importacion': {
    camino: '/terceros/importacion',
    manejadores: [
      http.post('/api/v1/terceros/terceros/importacion', () =>
        HttpResponse.json<Esquemas['InformeDeImportacionDto']>({
          leidas: 6,
          importadas: 1,
          rechazadas: 5,
          rechazos: [
            { columna: 'domicilio_codigo_postal', motivo: 'formato-no-valido', lineas: [2, 3, 7] },
            { columna: 'sujeto_a_retencion_irpf', motivo: 'no-valido', lineas: [4] },
            { columna: null, motivo: 'numero-de-campos-distinto', lineas: [5] },
          ],
        }),
      ),
    ],
    listo: 'sujeto_a_retencion_irpf',
    tablas: 1,
    antes: async () => {
      const selector = await waitFor(() => {
        const encontrado = document.querySelector<HTMLInputElement>('input[type=file]');
        if (encontrado === null) {
          throw new Error('La importación no tiene su selector de fichero');
        }
        return encontrado;
      });
      await userEvent.upload(
        selector,
        new File(['identificacion_pais;identificacion_numero\r\n'], 'clientes.csv', {
          type: 'text/csv',
        }),
      );
      await userEvent.click(screen.getByRole('button', { name: 'Importar' }));
    },
  },
  '/transferencias': {
    camino: '/transferencias',
    manejadores: [
      http.get('/api/v1/inventario/transferencias', () =>
        HttpResponse.json(
          pagina<Esquemas['TransferenciaResumenDto']>([
            {
              id: TRANSFERENCIA,
              numero: 311,
              almacenOrigenId: PLATAFORMA.id,
              almacenDestinoId: MUELLE.id,
              fechaDeEnvio: '2026-09-29',
              fechaDeRecepcion: '2026-09-30',
              estado: 'Recibida',
              anulaAId: null,
            },
          ]),
        ),
      ),
    ],
    listo: /ALM-MUELLE-NORTE/,
    tablas: 1,
  },
  '/transferencias/nueva': {
    camino: '/transferencias/nueva',
    manejadores: [],
    listo: /S2/,
    tablas: 1,
  },
  '/transferencias/:id': {
    camino: `/transferencias/${TRANSFERENCIA}`,
    manejadores: [
      http.get('/api/v1/inventario/transferencias/:id', () =>
        HttpResponse.json(
          {
            id: TRANSFERENCIA,
            serieId: SERIES[1]!.id,
            numero: 311,
            almacenOrigenId: PLATAFORMA.id,
            almacenDestinoId: MUELLE.id,
            fechaDeEnvio: '2026-09-29',
            fechaDeRecepcion: '2026-09-30',
            estado: 'Recibida',
            divisa: 'EUR',
            anulaAId: null,
            motivo: null,
            lineas: [
              {
                numero: 1,
                ubicacionOrigenId: UBICACION.id,
                ubicacionDestinoId: UBICACION.id,
                articuloId: ARTICULO.id,
                cantidadIntroducida: 250,
                unidadIntroducidaId: UNIDAD.id,
                factorAUnidadBase: 1,
                cantidadEnUnidadBase: 250,
                codigoDeLote: 'L-2026-09-000123',
                numeroDeSerie: null,
                valor: 21.875,
              },
            ],
          } satisfies Esquemas['FichaDeTransferenciaDto'],
          { headers: { ETag: '"1"' } },
        ),
      ),
    ],
    listo: 'L-2026-09-000123',
    tablas: 1,
  },
  '*': {
    camino: '/una-ruta-que-no-existe',
    manejadores: [],
    listo: 'Página no encontrada',
    tablas: 0,
  },
};

function anchoDeLaPagina(): number {
  return document.documentElement.scrollWidth;
}

/** Si una caja corta lo que se sale de ella: lo de dentro ya no empuja la página. */
function recortaLoDeDentro(elemento: Element): boolean {
  return getComputedStyle(elemento).overflowX !== 'visible';
}

/**
 * Lo que pasa del borde de la ventana, solo lo más hondo de cada rama, para que el rojo diga qué
 * es. Lo que está dentro de una caja que lo recorta no cuenta: corre ella, y no la página.
 *
 * **Lo absoluto solo lo recorta su cadena de bloques contenedores**: la primera caja con posición
 * por encima, y las que la contienen. Las de en medio, aunque lleven `overflow`, lo dejan salir.
 * Contarlas como recorte es lo que dejó vacío el rojo de `/tarifas` en el run 40064764889.
 */
function loQueSeSale(): string[] {
  const fuera = (elemento: Element): boolean =>
    elemento.getBoundingClientRect().right > ANCHO + 0.5;
  const recortado = (elemento: Element): boolean => {
    let posicion = getComputedStyle(elemento).position;
    for (let caja = elemento.parentElement; caja !== null; caja = caja.parentElement) {
      if (posicion === 'fixed') {
        return false;
      }
      const estilo = getComputedStyle(caja);
      if (posicion === 'absolute' && estilo.position === 'static') {
        continue;
      }
      if (recortaLoDeDentro(caja)) {
        return true;
      }
      posicion = estilo.position;
    }
    return false;
  };

  return [...document.body.querySelectorAll('*')]
    .filter((elemento) => fuera(elemento) && !recortado(elemento))
    .filter((elemento) => ![...elemento.children].some(fuera))
    .map(
      (elemento) =>
        `<${elemento.tagName.toLowerCase()}> «${elemento.textContent.slice(0, 50)}» ` +
        `hasta ${String(Math.round(elemento.getBoundingClientRect().right))} px`,
    );
}

describe(`Las páginas a ${String(ANCHO)} px`, () => {
  it('cada ruta tiene su escenario, y ninguno sobra', () => {
    expect(Object.keys(ESCENARIOS).sort()).toEqual(RUTAS.map(({ ruta }) => ruta).sort());
  });

  for (const { ruta } of RUTAS) {
    it(`${ruta} cabe, y cada tabla corre dentro de su contenedor`, async () => {
      const escenario = ESCENARIOS[ruta];
      if (escenario === undefined) {
        throw new Error(`${ruta} no tiene escenario`);
      }

      // El ambiente, fijado y no heredado: es el ancho de la configuración del carril.
      expect(window.innerWidth).toBe(ANCHO);

      if (escenario.sinSesion !== true) {
        abrirSesionSimulada(ALFA.id, TODOS_LOS_PERMISOS);
      }
      usarEnElNavegador(...escenario.manejadores, ...MAESTROS);
      montarAplicacion(escenario.camino);

      await escenario.antes?.();
      await screen.findAllByText(escenario.listo, {}, { timeout: 5000 });
      await waitFor(() => {
        expect(document.querySelectorAll('table')).toHaveLength(escenario.tablas);
      });

      const medidas = [...document.querySelectorAll('table')].map((tabla) => {
        const contenedor = tabla.parentElement!;
        const desplaza = getComputedStyle(contenedor).overflowX;
        contenedor.style.overflowX = 'visible';
        const sinContenedor = anchoDeLaPagina();
        contenedor.style.overflowX = '';

        // La sonda, al fondo de una tabla que no cabe con ninguna fuente.
        const ultima = tabla.querySelector('tr > :last-child');
        if (ultima === null) {
          throw new Error(`${ruta}: una tabla sin celdas no tiene dónde poner la sonda`);
        }
        const sonda = document.createElement('span');
        sonda.className = 'sr-only';
        sonda.textContent = 'sonda';
        ultima.append(sonda);
        tabla.style.width = '2000px';
        const conSonda = anchoDeLaPagina();
        contenedor.style.position = 'static';
        const sinPosicion = { ancho: anchoDeLaPagina(), seSale: loQueSeSale().join('; ') };
        contenedor.style.position = '';
        tabla.style.width = '';
        sonda.remove();

        return { desplaza, sinContenedor, conSonda, sinPosicion };
      });

      expect(anchoDeLaPagina(), `${ruta} se sale: ${loQueSeSale().join('; ')}`).toBe(ANCHO);
      for (const medida of medidas) {
        expect(medida.desplaza, `${ruta}: el contenedor de la tabla no corre`).toBe('auto');
        expect(medida.sinContenedor, `${ruta}: sin el contenedor, la página cabe`).toBeGreaterThan(
          ANCHO,
        );
        expect(medida.conSonda, `${ruta}: lo que va en absoluto en la tabla empuja la página`).toBe(
          ANCHO,
        );
        expect(
          medida.sinPosicion.ancho,
          `${ruta}: sin posición en el contenedor, la sonda no llega a mover la página`,
        ).toBeGreaterThan(ANCHO);
        expect(medida.sinPosicion.seSale, `${ruta}: el diagnóstico no nombra la sonda`).toContain(
          '«sonda»',
        );
      }
    });
  }
});
