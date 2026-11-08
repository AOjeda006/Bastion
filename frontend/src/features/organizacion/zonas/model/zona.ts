import { z } from 'zod';

/**
 * Las tres zonas horarias de la lista cerrada (ADR-0079 §1), por el nombre con el que las pinta la
 * pantalla: la península y Baleares, Canarias, y Ceuta y Melilla. Ampliar la lista es una pregunta
 * para el usuario, y entrar aquí, un cambio del contrato: el traductor de `zonas/api` deja de
 * compilar hasta que se le da nombre.
 */
export const ZONAS = ['peninsula', 'canarias', 'ceuta'] as const;

export type Zona = (typeof ZONAS)[number];

/** Lo que se elige para un almacén: una de las tres, o la de su empresa, que hereda en vivo. */
export type ZonaDeAlmacen = Zona | 'deLaEmpresa';

/** Si lo elegido es una de las tres, y no la de la empresa. */
export function esUnaZona(zona: ZonaDeAlmacen): zona is Zona {
  return zona !== 'deLaEmpresa';
}

/** Lo que ofrece el diálogo de un almacén: la de su empresa primero, y después las tres. */
export const ZONAS_DE_ALMACEN = ['deLaEmpresa', ...ZONAS] as const;

/**
 * Las reglas de los dos formularios de la zona: una de las que ofrecen. Siempre hay una marcada, la
 * de ahora, así que no hay mensaje que dar: lo que no se puede elegir no llega aquí.
 */
export const esquemaDeZonaDeAlmacen = z.object({ zona: z.enum(ZONAS_DE_ALMACEN) });

export const esquemaDeZonaDeEmpresa = z.object({ zona: z.enum(ZONAS) });
