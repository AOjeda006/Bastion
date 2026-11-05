import { useQuery } from '@tanstack/react-query';

import { VIDA_DE_UN_MAESTRO } from './nombres.ts';
import { clavesDeMaestros } from '../api/claves.ts';
import { consultarAlmacen, type Almacen } from '../api/maestros.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/**
 * La zona del almacén `id` de la lista de un alta, o nula si no está: sin elegir, o con la lista
 * todavía por llegar. Sin zona, el formulario lleva el día de la empresa.
 */
export function zonaDelAlmacen(
  almacenes: readonly Almacen[] | undefined,
  id: string,
): string | null {
  return almacenes?.find((almacen) => almacen.id === id)?.zona ?? null;
}

/**
 * La zona horaria del almacén `id`, con la que un formulario pide su día de hoy (ADR-0079 §12).
 *
 * Se pregunta con la clave de su nombre, así que en una ficha que ya lo nombra no cuesta nada.
 * `undefined` mientras llega. **`null` si no se puede leer**, sin permiso para verlo o porque ya no
 * existe: entonces el formulario lleva el día de la empresa, y quien decide es el servidor, cuyo
 * rechazo vuelve al campo.
 */
export function useZonaDelAlmacen(id: string): string | null | undefined {
  const sesion = useSesionAbierta();
  const puede = concede(sesion, PERMISOS.almacenVer);

  const consulta = useQuery({
    queryKey: clavesDeMaestros.maestro('almacen', id),
    queryFn: () => consultarAlmacen(id),
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: puede,
  });

  if (consulta.data !== undefined) {
    return consulta.data.zona;
  }

  return puede && !consulta.isError ? undefined : null;
}
