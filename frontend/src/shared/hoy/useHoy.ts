import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { vidaDeHoy, type Hoy } from './hoy.ts';
import { consultarHoy } from '@/shared/api/hoy.ts';

/**
 * La clave de «hoy». Es de todos, porque lo leen formularios de varias funcionalidades, y nada la
 * invalida a mano: caduca cuando acaba el primer día de sus zonas, y el cambio de empresa, que es
 * cuando cambian las zonas, vacía la caché entera.
 */
export const CLAVE_DE_HOY = ['hoy'] as const;

/**
 * Hoy, como lo dice el servidor (ADR-0079 §12), preguntado otra vez en cuanto acaba un día.
 *
 * **La vida del dato es la del día**, y no un `staleTime` fijo: lo que dijo el servidor vale hasta
 * que acaba el primero de los días de sus zonas, y entonces se vuelve a preguntar, también con la
 * pantalla quieta. Un formulario abierto a medianoche cambia de tope sin recargar.
 */
export function useHoy(): UseQueryResult<Hoy> {
  return useQuery({
    queryKey: CLAVE_DE_HOY,
    queryFn: consultarHoy,
    staleTime: (consulta) =>
      consulta.state.data === undefined ? 0 : vidaDeHoy(consulta.state.data),
    refetchInterval: (consulta) =>
      consulta.state.data === undefined ? false : vidaDeHoy(consulta.state.data),
  });
}
