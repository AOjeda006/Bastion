import { render, type RenderResult } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { I18nextProvider } from 'react-i18next';
import { QueryClientProvider } from '@tanstack/react-query';

import { Proveedores } from '@/app/Proveedores.tsx';
import { crearRutas } from '@/app/enrutador.tsx';
import { crearI18n } from '@/app/i18n/index.ts';
import type { Idioma } from '@/app/i18n/idioma.ts';
import { QueryClient } from '@tanstack/react-query';

/**
 * Monta la aplicación ENTERA en un enrutador de memoria, en la ruta que se pida.
 *
 * Entera y no la pantalla suelta: lo que este ítem tiene que probar —el anuncio de cambio de ruta,
 * el foco, la guarda, el selector de empresa— solo existe cuando el armazón está montado. Probar
 * una pantalla aislada dejaría fuera todo eso.
 *
 * Caché nueva en cada llamada y sin reintentos: con reintentos, un test de error tarda segundos en
 * fallar y otro test acaba viendo datos que pidió el anterior.
 *
 * Devuelve también el enrutador, y no por comodidad: es la única forma de comprobar que el filtro o
 * la página están EN LA URL. Un `useState` daría exactamente la misma pantalla, y la diferencia
 * —que el enlace se puede compartir y la vuelta atrás funciona— solo se ve mirando la ubicación.
 *
 * Y devuelve la caché por un motivo parecido pero distinto: lo que se pinta prueba el EFECTO, y la
 * caché dice la CAUSA. Un test que solo mire la pantalla sí caza que se han quedado filas del
 * inquilino anterior, pero se entera cinco segundos más tarde y por plazo agotado, con un mensaje
 * que no menciona ni la caché ni la empresa. Mirando la caché, el mismo fallo se nombra en
 * milisegundos. Las dos cosas y no una: quien mire solo la caché estará probando la implementación
 * de hoy en vez de la promesa.
 */
export interface AplicacionMontada extends RenderResult {
  readonly enrutador: ReturnType<typeof createMemoryRouter>;
  readonly cache: QueryClient;
  readonly i18n: ReturnType<typeof crearI18n>;
}

export function montarAplicacion(rutaInicial = '/', idioma: Idioma = 'es'): AplicacionMontada {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  // Instancia de i18n NUEVA en cada montaje, y con el idioma dicho a mano. Si se tomara el idioma
  // detectado, los tests dependerían del `navigator.language` de la máquina que los corre, y el
  // mismo test pasaría aquí y fallaría en la CI.
  const i18n = crearI18n(idioma);

  const enrutador = createMemoryRouter(crearRutas(), { initialEntries: [rutaInicial] });

  const pintado = render(
    <Proveedores cache={cache} i18n={i18n}>
      <RouterProvider router={enrutador} />
    </Proveedores>,
  );

  return { ...pintado, enrutador, cache, i18n };
}

/** Lo que devuelve `montarPantalla`. La sesión no está: la escribe el test antes de montar. */
export interface PantallaMontada extends RenderResult {
  readonly enrutador: ReturnType<typeof createMemoryRouter>;
  readonly cache: QueryClient;
  readonly i18n: ReturnType<typeof crearI18n>;
}

/**
 * Monta UNA PANTALLA sola, en un enrutador de memoria con su ruta y nada más.
 *
 * **Por qué existe, si ya hay `montarAplicacion`.** Porque el armazón se asienta en varios turnos
 * posteriores al montaje —el enrutador termina su navegación inicial y los tres componentes
 * suscritos a la sesión se enteran— y React deja cuatro avisos «not wrapped in act» por cada
 * montaje. Medido: cuatro, con carga perezosa de la ruta y sin ella. No son ruido inofensivo:
 * mientras el fondo sea de cuatro por test, un aviso NUEVO —el de una actualización de verdad sin
 * esperar, que es la que deja un test comprobando una pantalla a medias— no se distingue de él.
 * Montando solo la pantalla, la cuenta es cero, y entonces el primer aviso que aparezca significa
 * algo.
 *
 * **Lo que esto NO prueba, y quién lo prueba.** Que la ruta exista y esté detrás de su permiso, lo
 * dicen `ElBarridoDeRutas` y `LasRutasProtegidas`; el anuncio de cambio de ruta y el foco, `El
 * cambio de ruta`; el vaciado de la caché al cambiar de empresa, `El selector de empresa`. Nada de
 * eso es de esta pantalla, y volver a montarlo aquí no lo comprobaría otra vez: lo comprobaría el
 * mismo número de veces con cuatro avisos más.
 *
 * La sesión se escribe ANTES, en el depósito (`abrirSesionYaRecuperada`), que es donde la deja la
 * recuperación de verdad: el cliente HTTP lee de ahí el testigo, así que la petición sale con la
 * misma cabecera que en la aplicación entera.
 */
export function montarPantalla(
  pantalla: React.JSX.Element,
  rutaInicial: string,
  idioma: Idioma = 'es',
): PantallaMontada {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const i18n = crearI18n(idioma);

  // La ruta es el camino de la entrada inicial: así el test escribe una sola vez a dónde entra, con
  // sus parámetros, y no puede montar una ruta distinta de la que dice estar visitando.
  const camino = rutaInicial.split('?')[0] ?? rutaInicial;

  const enrutador = createMemoryRouter([{ path: camino, element: pantalla }], {
    initialEntries: [rutaInicial],
  });

  // Los proveedores a mano y no `Proveedores`: el que sobra es exactamente `ProveedorDeSesion`, que
  // es quien pide la sesión con la cookie al arrancar. Aquí ya está puesta.
  const pintado = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={cache}>
        <RouterProvider router={enrutador} />
      </QueryClientProvider>
    </I18nextProvider>,
  );

  return { ...pintado, enrutador, cache, i18n };
}
