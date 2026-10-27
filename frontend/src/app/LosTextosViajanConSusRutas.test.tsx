import { readFileSync, readdirSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

import { QueryClient } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { i18n as InstanciaDeI18n } from 'i18next';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { Proveedores } from './Proveedores.tsx';
import { crearRutas } from './enrutador.tsx';
import type * as Diccionarios from './i18n/diccionarios.ts';
import { es } from './i18n/es.ts';
import type { Idioma } from './i18n/idioma.ts';
import { crearI18n, textosCargados } from './i18n/index.ts';
import { tiposDeError } from './i18n/tiposDeError.es.ts';
import { RUTAS } from './rutas.tsx';
import { FUNCIONALIDADES, type Funcionalidad } from '@/features/funcionalidades.ts';
import { ALFA } from '@/pruebas/datos.ts';
import { DICCIONARIOS_ENTEROS } from '@/pruebas/i18n.ts';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';

/**
 * LOS TEXTOS VIAJAN CON SUS RUTAS — ítem 3.1, ADR-0071.
 *
 * El diccionario del arranque solo lleva el armazón, y los textos de cada funcionalidad llegan con
 * la primera ruta de la suya. Que salgan del fragmento del arranque lo mide el presupuesto, en
 * `dist/`. Lo que la medida no puede decir es lo que se ve: que una pantalla no se pinta antes que
 * sus textos —con las claves en crudo—, que cambiar de idioma trae los de lo ya visitado, y que las
 * dos cargas, la de la ruta y la del idioma, se pueden cruzar sin dejar nada a medias.
 *
 * **Se monta con el armazón solo**, y no con `montarAplicacion`, que trae el diccionario entero
 * para montar sin esperar (`pruebas/i18n.ts`). Aquí lo que se prueba es justo la espera.
 *
 * **Las descargas se retienen a propósito**, como en `ElArranqueConUnIdioma`: se sustituye el
 * CARGADOR y no los textos, así que lo que llega es lo de verdad, y la ventana en la que se mira
 * dura lo que el caso quiera. Cada caso suelta lo que retiene antes de acabar: un caso no cierra su
 * árbol con una carga en vuelo.
 *
 * **Y si un caso se pone rojo a medias, lo que dejó retenido NO se suelta.** Soltarlo al acabar lo
 * haría correr dentro del caso siguiente: un cambio de idioma del caso muerto pedía sus textos con
 * el siguiente ya montado, y el siguiente los veía en su lista y se ponía rojo sin culpa. Lo
 * enseñó la mutación 767, que puso tres casos rojos con un fallo de uno. Lo retenido se olvida y
 * se queda esperando, que no cuesta nada: no hay temporizador ni red detrás.
 *
 * **Y la regla que hace que esto baste**: los textos de una funcionalidad solo los lee el código
 * de esa funcionalidad. El armazón, o una pantalla de otro módulo, los encontraría sin cargar. El
 * último par de casos la comprueba sobre el fuente.
 */
const descargas = vi.hoisted(() => {
  const retenidas = new Map<string, { espera: Promise<void>; soltar: () => void }>();
  const pedidas: string[] = [];
  const entregadas: string[] = [];

  return {
    pedidas,
    entregadas,
    retener(clave: string): void {
      let soltar = (): void => undefined;
      const espera = new Promise<void>((resolver) => {
        soltar = resolver;
      });
      retenidas.set(clave, { espera, soltar });
    },
    soltar(clave: string): void {
      retenidas.get(clave)?.soltar();
      retenidas.delete(clave);
    },
    // Sin soltar nada: lo que siga retenido se queda esperando para siempre.
    olvidarlo(): void {
      retenidas.clear();
      pedidas.length = 0;
      entregadas.length = 0;
    },
    async pasar(clave: string): Promise<void> {
      pedidas.push(clave);
      await retenidas.get(clave)?.espera;
    },
  };
});

vi.mock('@/app/i18n/diccionarios.ts', async (importarDeVerdad) => {
  const deVerdad = await importarDeVerdad<typeof Diccionarios>();

  return {
    ...deVerdad,
    cargarDiccionario: async (idioma: Idioma) => {
      await descargas.pasar(`armazon/${idioma}`);
      const diccionario = await deVerdad.cargarDiccionario(idioma);
      descargas.entregadas.push(`armazon/${idioma}`);

      return diccionario;
    },
    cargarTextos: async (funcionalidad: Funcionalidad, idioma: Idioma) => {
      await descargas.pasar(`${funcionalidad}/${idioma}`);
      const textos = await deVerdad.cargarTextos(funcionalidad, idioma);
      descargas.entregadas.push(`${funcionalidad}/${idioma}`);

      return textos;
    },
  };
});

afterEach(() => {
  descargas.olvidarlo();
});

/** La aplicación entera, con una instancia que arranca como la de verdad: el armazón y nada más. */
function montarConElArmazonSolo(ruta: string): {
  i18n: InstanciaDeI18n;
  enrutador: ReturnType<typeof createMemoryRouter>;
} {
  const i18n = crearI18n('es', es, tiposDeError);
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const enrutador = createMemoryRouter(crearRutas(Promise.resolve(i18n)), {
    initialEntries: [ruta],
  });

  render(
    <Proveedores cache={cache} i18n={i18n}>
      <RouterProvider router={enrutador} />
    </Proveedores>,
  );

  return { i18n, enrutador };
}

/** Ninguna clave de una funcionalidad a la vista: es lo que `t()` pinta cuando no la encuentra. */
function exigirSinClavesEnCrudo(): void {
  const texto = document.body.textContent;

  for (const funcionalidad of FUNCIONALIDADES) {
    expect(texto, `se ve una clave de ${funcionalidad} en crudo`).not.toContain(
      `${funcionalidad}.`,
    );
  }
}

/** Entra en `/almacenes` desde el inicio, por el menú, sin esperar a que se pinte. */
async function irAAlmacenes(usuario: ReturnType<typeof userEvent.setup>): Promise<void> {
  expect(await screen.findByRole('heading', { level: 1, name: 'Inicio' })).toBeInTheDocument();
  await usuario.click(screen.getByRole('link', { name: 'Almacenes' }));
}

describe('Los textos viajan con sus rutas', () => {
  it('una ruta trae los textos de su funcionalidad, y solo los suyos', async () => {
    abrirSesionSimulada(ALFA.id);
    const { i18n } = montarConElArmazonSolo('/almacenes');

    expect(
      await screen.findByRole('table', { name: 'Almacenes de la empresa activa' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Código' })).toBeInTheDocument();
    exigirSinClavesEnCrudo();

    // Los de su funcionalidad, en el idioma activo; los de las demás, no, y el otro idioma, tampoco.
    expect(textosCargados(i18n, 'organizacion')).toBe(true);
    for (const otra of FUNCIONALIDADES.filter(
      (funcionalidad) => funcionalidad !== 'organizacion',
    )) {
      expect(textosCargados(i18n, otra), otra).toBe(false);
    }
    expect(textosCargados(i18n, 'organizacion', 'en')).toBe(false);
    expect(descargas.pedidas).toEqual(['organizacion/es']);
  });

  it('mientras sus textos viajan, la pantalla no se pinta: no hay ninguna clave en crudo', async () => {
    // La pantalla, cargada ANTES: así lo único que puede retener la ruta son sus textos. Sin esto,
    // la mutación 768 —que dejaba de esperarlos— salía verde con el caso solo, porque la pantalla
    // tardaba más en compilarse que los textos en llegar, y la ventana miraba antes de que hubiera
    // nada que ver.
    await RUTAS.find((declaracion) => declaracion.ruta === '/almacenes')?.cargar();

    abrirSesionSimulada(ALFA.id);
    descargas.retener('organizacion/es');
    const { i18n, enrutador } = montarConElArmazonSolo('/almacenes');

    // La carga está pedida y retenida: la ventana no se acaba sola. Una pantalla que se pintara sin
    // esperar a sus textos dejaría aquí `organizacion.almacenes.cargando`, o la tabla con sus
    // rótulos en crudo, y el enrutador habría terminado su primera carga.
    await waitFor(() => {
      expect(descargas.pedidas).toContain('organizacion/es');
    });
    expect(enrutador.state.initialized).toBe(false);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    exigirSinClavesEnCrudo();
    expect(textosCargados(i18n, 'organizacion')).toBe(false);

    descargas.soltar('organizacion/es');

    expect(
      await screen.findByRole('table', { name: 'Almacenes de la empresa activa' }),
    ).toBeInTheDocument();
    exigirSinClavesEnCrudo();
  });

  it('cambiar de idioma trae los textos de lo visitado, a la vez que el diccionario y antes del cambio', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    const { i18n } = montarConElArmazonSolo('/almacenes');

    expect(
      await screen.findByRole('table', { name: 'Almacenes de la empresa activa' }),
    ).toBeInTheDocument();

    descargas.retener('armazon/en');
    descargas.retener('organizacion/en');
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');

    // A la vez y no después: con el diccionario todavía retenido, los textos ya están pedidos. Y
    // los de lo que no se ha visitado, no.
    await waitFor(() => {
      expect(descargas.pedidas).toContain('organizacion/en');
    });
    expect(descargas.pedidas).toContain('armazon/en');
    expect(descargas.pedidas.filter((clave) => clave.endsWith('/en'))).toHaveLength(2);

    // Y antes del cambio: con el diccionario ya entregado y los textos aún no, sigue TODO en
    // castellano. Un cambio que no los esperara pintaría la tabla en crudo.
    descargas.soltar('armazon/en');
    await waitFor(() => {
      expect(descargas.entregadas).toContain('armazon/en');
    });
    expect(textosCargados(i18n, 'organizacion', 'en')).toBe(false);
    expect(
      screen.getByRole('table', { name: 'Almacenes de la empresa activa' }),
    ).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('es');

    descargas.soltar('organizacion/en');

    expect(
      await screen.findByRole('table', { name: 'Warehouses of the active company' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Code' })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('en');
    exigirSinClavesEnCrudo();
  });

  it('si el idioma cambia mientras los textos de la ruta viajan, la ruta trae también los del nuevo', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    const { i18n } = montarConElArmazonSolo('/');

    descargas.retener('organizacion/es');
    await irAAlmacenes(usuario);
    await waitFor(() => {
      expect(descargas.pedidas).toContain('organizacion/es');
    });

    // El cambio no ve a `organizacion` cargada —todavía no lo está— y se completa sin ella.
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeInTheDocument();
    expect(descargas.pedidas).not.toContain('organizacion/en');

    // Llegan los castellanos, que ya no sirven: la carga de la ruta tiene que darse cuenta de que el
    // idioma es otro y traer los ingleses antes de dejar pintar la pantalla.
    descargas.soltar('organizacion/es');

    expect(
      await screen.findByRole('table', { name: 'Warehouses of the active company' }),
    ).toBeInTheDocument();
    expect(textosCargados(i18n, 'organizacion', 'en')).toBe(true);
    exigirSinClavesEnCrudo();
  });

  it('si los textos de la ruta llegan mientras el idioma cambia, el cambio trae también los del nuevo', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    const { i18n } = montarConElArmazonSolo('/');

    descargas.retener('organizacion/es');
    descargas.retener('armazon/en');
    await irAAlmacenes(usuario);
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');
    await waitFor(() => {
      expect(descargas.pedidas).toContain('armazon/en');
    });

    // El cambio empezó sin `organizacion`, y los castellanos llegan con él a medias: la pantalla se
    // pinta en castellano, que es el idioma que todavía manda.
    descargas.soltar('organizacion/es');
    expect(
      await screen.findByRole('table', { name: 'Almacenes de la empresa activa' }),
    ).toBeInTheDocument();
    expect(descargas.pedidas).not.toContain('organizacion/en');

    // Y el cambio, justo antes de cambiar, tiene que ver lo que llegó mientras tanto.
    descargas.soltar('armazon/en');

    expect(
      await screen.findByRole('table', { name: 'Warehouses of the active company' }),
    ).toBeInTheDocument();
    expect(textosCargados(i18n, 'organizacion', 'en')).toBe(true);
    exigirSinClavesEnCrudo();
  });
});

/** Todos los caminos de un objeto de textos, los de las hojas y los de los nodos. */
function caminosDe(textos: object, prefijo: string): string[] {
  return Object.entries(textos).flatMap(([clave, valor]) => {
    const camino = `${prefijo}.${clave}`;

    return typeof valor === 'object' && valor !== null
      ? [camino, ...caminosDe(valor as object, camino)]
      : [camino];
  });
}

const CAMINOS = new Map(
  FUNCIONALIDADES.map((funcionalidad) => [
    funcionalidad,
    new Set([funcionalidad, ...caminosDe(DICCIONARIOS_ENTEROS.es[funcionalidad], funcionalidad)]),
  ]),
);

/**
 * Una cadena que empieza por el nombre de una funcionalidad y un punto, y que acaba en su comilla
 * o en el `${` de una plantilla: `t('inventario.saldos.consultar')` o `` `inventario.ajustes.${x}` ``.
 */
const LITERAL = /['"`]((?:[a-z]+)\.[A-Za-z0-9_.]*?)\.?(?=['"`]|\$\{)/g;

interface Lectura {
  readonly fichero: string;
  readonly duenio: Funcionalidad | 'armazon';
  readonly clave: string;
  readonly de: Funcionalidad;
}

/** Las claves de una funcionalidad que aparecen en un fuente, con quién es su dueño. */
function lecturasDe(fichero: string, fuente: string): Lectura[] {
  const partes = fichero.split(sep).join('/').split('/');
  const enFeatures = partes.indexOf('features');
  const carpeta = enFeatures >= 0 ? partes[enFeatures + 1] : undefined;
  const duenio = FUNCIONALIDADES.find((funcionalidad) => funcionalidad === carpeta) ?? 'armazon';

  return [...fuente.matchAll(LITERAL)].flatMap(([, clave = '']) => {
    const de = FUNCIONALIDADES.find((funcionalidad) => CAMINOS.get(funcionalidad)?.has(clave));

    return de === undefined ? [] : [{ fichero, duenio, clave, de }];
  });
}

const RAIZ = join(process.cwd(), 'src');

/** El fuente de la aplicación: ni los tests, ni el arnés, ni los propios textos. */
const FUENTES = readdirSync(RAIZ, { recursive: true, encoding: 'utf8' })
  .filter((fichero) => /\.tsx?$/.test(fichero) && !/\.test\.tsx?$/.test(fichero))
  .map((fichero) => join(RAIZ, fichero))
  .filter((fichero) => {
    const camino = relative(RAIZ, fichero).split(sep).join('/');

    return !camino.startsWith('pruebas/') && !camino.startsWith('app/i18n/');
  });

describe('Los textos de una funcionalidad solo los lee ella', () => {
  it('ningún fichero del armazón ni de otra funcionalidad nombra una clave suya', () => {
    const lecturas = FUENTES.flatMap((fichero) =>
      lecturasDe(fichero, readFileSync(fichero, 'utf8')),
    );
    const ajenas = lecturas
      .filter((lectura) => lectura.duenio !== lectura.de)
      .map((lectura) => `${relative(RAIZ, lectura.fichero)}: '${lectura.clave}'`);

    expect(ajenas).toEqual([]);

    // La pareja: el barrido encuentra claves, y de todas las funcionalidades, en su sitio. Si la
    // expresión dejara de casar, o la lista de ficheros saliera vacía, lo de arriba sería verde sin
    // haber mirado nada.
    expect(FUENTES.length).toBeGreaterThan(50);
    for (const funcionalidad of FUNCIONALIDADES) {
      expect(
        lecturas.filter((lectura) => lectura.de === funcionalidad).length,
        `no se ha encontrado ninguna lectura de '${funcionalidad}'`,
      ).toBeGreaterThan(0);
    }
  });

  it('y la ve si alguien la nombra: desde el armazón, desde otra, y en una plantilla', () => {
    const desdeElArmazon = lecturasDe(
      join(RAIZ, 'app', 'Disposicion.tsx'),
      "t('organizacion.almacenes.tabla');",
    );
    const desdeOtra = lecturasDe(
      join(RAIZ, 'features', 'inventario', 'saldos', 'ui', 'PaginaDeSaldos.tsx'),
      'const clave = `catalogo.articulos.trazabilidades.${valor}`;',
    );
    const desdeLaSuya = lecturasDe(
      join(RAIZ, 'features', 'catalogo', 'articulos', 'ui', 'PaginaDeArticulos.tsx'),
      'const clave = `catalogo.articulos.trazabilidades.${valor}`;',
    );
    // Un permiso tiene la misma forma, y no es un texto: `inventario.ajuste` no es un nodo.
    const unPermiso = lecturasDe(
      join(RAIZ, 'shared', 'sesion', 'permisos.ts'),
      "ajusteVer: 'inventario.ajuste.ver',",
    );

    expect(desdeElArmazon).toEqual([
      expect.objectContaining({ duenio: 'armazon', de: 'organizacion' }),
    ]);
    expect(desdeOtra).toEqual([
      expect.objectContaining({
        duenio: 'inventario',
        de: 'catalogo',
      }),
    ]);
    expect(desdeLaSuya).toEqual([expect.objectContaining({ duenio: 'catalogo', de: 'catalogo' })]);
    expect(unPermiso).toEqual([]);
  });
});
