import { QueryClient } from '@tanstack/react-query';

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
        // Un reintento para el corte de red de un segundo. El 401 no llega hasta aquí: lo resuelve
        // el cliente HTTP renovando el testigo y repitiendo la petición una vez.
        retry: 1,
        // Volver a la pestaña no es motivo para pedirlo todo otra vez en un ERP que se queda
        // abierto toda la jornada.
        refetchOnWindowFocus: false,
      },
    },
  });
}
