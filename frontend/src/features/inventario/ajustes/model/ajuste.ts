/**
 * Un ajuste de inventario, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce
 * `api/ajustes.ts` (ADR-0065).
 *
 * El almacén, la ubicación, el artículo y las unidades llegan como identificadores, porque son
 * maestros de otros módulos: la pantalla los nombra preguntando a sus dueños, como el recuento.
 */

/**
 * En qué punto de su vida está.
 *
 * Los tres que la API emite, más `desconocido` para lo que llegue mañana. Un borrador descartado no
 * es un estado: deja de existir para la API (ADR-0065 §5).
 */
export type EstadoDeAjuste = 'borrador' | 'confirmado' | 'anulado' | 'desconocido';

/** Los tres por los que se puede acotar el listado, en el orden en que se ofrecen. */
export const ESTADOS_DE_AJUSTE = ['borrador', 'confirmado', 'anulado'] as const;

/** Un estado por el que se puede acotar: todos menos `desconocido`, que solo se lee. */
export type EstadoConocido = (typeof ESTADOS_DE_AJUSTE)[number];

/** Un ajuste como sale en el listado: sin sus líneas. */
export interface ResumenDeAjuste {
  readonly id: string;
  /** El correlativo, que solo existe desde que se confirma. */
  readonly numero: number | null;
  readonly almacenId: string;
  /** El día en que mueve el libro, `AAAA-MM-DD`. */
  readonly fechaDeOperacion: string;
  readonly estado: EstadoDeAjuste;
  readonly motivo: string;
}

/**
 * Una línea, tal como quedó en el alta.
 *
 * **El factor y la cantidad en unidad base los puso el servidor** (ADR-0065 §2): la unidad base del
 * artículo, o la conversión del día del alta. Es lo que explica la cantidad que mueve el libro.
 */
export interface LineaDeAjuste {
  readonly numero: number;
  readonly ubicacionId: string;
  readonly articuloId: string;
  /** Lo que se escribió, con su signo: positivo entra, negativo sale. */
  readonly cantidadIntroducida: number;
  readonly unidadIntroducidaId: string;
  readonly factorAUnidadBase: number;
  readonly cantidadEnUnidadBase: number;
  /** El coste de una entrada, o nulo: una salida se valora al precio medio. */
  readonly costeUnitario: number | null;
  readonly codigoDeLote: string | null;
  readonly numeroDeSerie: string | null;
  /** Lo que movió en la valoración, o nulo mientras sea un borrador. */
  readonly valor: number | null;
}

/** Un ajuste como se enseña en su ficha, con todas sus líneas. */
export interface Ajuste extends ResumenDeAjuste {
  readonly divisa: string;
  /** El original, si este es su inverso. */
  readonly anulaAId: string | null;
  /** El recuento que lo generó, si viene de uno. */
  readonly recuentoId: string | null;
  readonly lineas: readonly LineaDeAjuste[];
}

/** La ficha tal como se leyó, con su versión: la que cita el descarte en el `If-Match`. */
export interface FichaDeAjuste {
  readonly ajuste: Ajuste;
  readonly version: string;
}

/** Una página de algo, con el total para el paginador. */
export interface Pagina<T> {
  readonly elementos: readonly T[];
  readonly total: number;
}

/** Lo que devuelve la anulación: el original, ya anulado, y el inverso que lo deshace. */
export interface Anulacion {
  readonly original: ResumenDeAjuste;
  readonly inverso: ResumenDeAjuste;
}
