import { QueryClient } from '@tanstack/react-query';

import { FalloDeApi } from '@/shared/api/errores.ts';

/** Cuántas veces se vuelve a pedir una consulta que ha fallado por algo que no es un `4xx`. */
const REINTENTOS = 1;

/**
 * Si una consulta que ha fallado se vuelve a pedir.
 *
 * Un `4xx`, no: es la API diciendo que la petición está mal, y pedirla otra vez da lo mismo. El
 * recorrido del 2.14 vio el `400` del día futuro pedido dos veces, y el repaso de la fase 2 lo llevó
 * a la respuesta 7. Lo demás —un `5xx`, un corte de red, un error que no es de la API— se reintenta
 * una vez, que es para lo que estaba el reintento.
 *
 * `fallos` es lo que TanStack Query pasa: cuántos ha habido antes de este, empezando en 0. Así que
 * `fallos < REINTENTOS` es lo mismo que el `retry: 1` de antes.
 */
export function debeReintentar(fallos: number, error: unknown): boolean {
  if (error instanceof FalloDeApi && error.estado >= 400 && error.estado < 500) {
    return false;
  }

  return fallos < REINTENTOS;
}

/**
 * La caché de servidor.
 *
 * Es una FÁBRICA y no una instancia de módulo porque cada test monta la suya: una caché compartida
 * entre tests es un intermitente esperando a que dos se ejecuten en cierto orden.
 *
 * `staleTime` NO se pone aquí: lo declara cada consulta, porque un catálogo de almacenes y un saldo
 * no envejecen igual y un valor único para todo siempre está mal para alguien.
 */
export function crearCache(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Un reintento para el corte de red de un segundo, y ninguno para un `4xx`. El 401 no
        // llega hasta aquí: lo resuelve el cliente HTTP renovando el testigo y repitiendo la
        // petición una vez.
        retry: debeReintentar,
        // Volver a la pestaña no es motivo para pedirlo todo otra vez en un ERP que se queda
        // abierto toda la jornada.
        refetchOnWindowFocus: false,
      },
    },
  });
}
