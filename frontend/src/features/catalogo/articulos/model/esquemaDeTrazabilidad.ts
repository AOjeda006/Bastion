import { z } from 'zod';

import { TRAZABILIDADES } from './articulo.ts';

/**
 * La regla del formulario de la trazabilidad: una de las tres.
 *
 * Solo hace falta cuando la guardada es una que esta versión no reconoce: entonces no hay ninguna
 * marcada al abrir, y guardar sin elegir se dice aquí sin ir al servidor. Si el artículo ya tiene
 * movimientos, eso no lo sabe el formulario: lo dice el servidor, y va en el mismo campo.
 *
 * El mensaje es una clave, por lo mismo que en `esquemaDeAcceso`: el esquema se evalúa fuera de
 * React y antes de que haya idioma.
 */
export const esquemaDeTrazabilidad = z.object({
  trazabilidad: z.enum(TRAZABILIDADES, { error: 'catalogo.articulos.cambioDeTrazabilidad.elige' }),
});

export type DatosDeTrazabilidad = z.infer<typeof esquemaDeTrazabilidad>;
