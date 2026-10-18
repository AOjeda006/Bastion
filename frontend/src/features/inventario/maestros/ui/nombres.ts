import { useQuery } from '@tanstack/react-query';

import { clavesDeMaestros } from '../api/claves.ts';
import {
  consultarAlmacen,
  consultarArticulo,
  consultarUbicacion,
  consultarUnidad,
  type Maestro,
} from '../api/maestros.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/** Un maestro cambia poco: minutos, no segundos (`stacks/react`). */
export const VIDA_DE_UN_MAESTRO = 5 * 60 * 1000;

type Clase = 'almacen' | 'articulo' | 'ubicacion' | 'unidad';

const CONSULTAS: Record<Clase, (id: string) => Promise<Maestro>> = {
  almacen: consultarAlmacen,
  articulo: consultarArticulo,
  ubicacion: consultarUbicacion,
  unidad: consultarUnidad,
};

const PERMISO: Record<Clase, string> = {
  almacen: PERMISOS.almacenVer,
  articulo: PERMISOS.articuloVer,
  ubicacion: PERMISOS.ubicacionVer,
  unidad: PERMISOS.unidadMedidaVer,
};

/** Cómo se llama un maestro: su código solo, y su código con el nombre. */
export interface Nombre {
  readonly corto: string;
  readonly largo: string;
}

/**
 * El nombre de un maestro de otro módulo, preguntado a su dueño (ADR-0055 §12).
 *
 * Mientras llega, «…». **Si no se puede leer** —sin permiso para verlo, o porque ya no existe— se
 * pinta su identificador, que es lo único que la ficha sabe de él: una celda en blanco parecería un
 * dato que falta, y un texto inventado, uno que no es. Sin el permiso no se pregunta: el servidor
 * contestaría `403` a cada fila.
 */
export function useNombre(clase: Clase, id: string): Nombre {
  const sesion = useSesionAbierta();

  const consulta = useQuery({
    queryKey: clavesDeMaestros.maestro(clase, id),
    queryFn: () => CONSULTAS[clase](id),
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: concede(sesion, PERMISO[clase]),
  });

  if (consulta.data !== undefined) {
    const { codigo, nombre } = consulta.data;

    return { corto: codigo, largo: nombre === null ? codigo : `${codigo} · ${nombre}` };
  }

  const provisional = consulta.fetchStatus === 'fetching' ? '…' : id;

  return { corto: provisional, largo: provisional };
}
