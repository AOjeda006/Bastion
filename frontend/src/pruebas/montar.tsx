import { render, type RenderResult } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';

import { Proveedores } from '@/app/Proveedores.tsx';
import { crearRutas } from '@/app/enrutador.tsx';
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
}

export function montarAplicacion(rutaInicial = '/'): AplicacionMontada {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  const enrutador = createMemoryRouter(crearRutas(), { initialEntries: [rutaInicial] });

  const pintado = render(
    <Proveedores cache={cache}>
      <RouterProvider router={enrutador} />
    </Proveedores>,
  );

  return { ...pintado, enrutador, cache };
}
