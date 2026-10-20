/**
 * Una transferencia entre almacenes, tal como la pinta esta funcionalidad. No es el DTO: lo traduce
 * `api/transferencias.ts` (ADR-0068).
 *
 * Los dos almacenes llegan como identificadores, porque son maestros de otro módulo: la pantalla
 * los nombra preguntando a su dueño, como el ajuste.
 */

/**
 * En qué punto de su vida está.
 *
 * Los cuatro que la API emite, más `desconocido` para lo que llegue mañana. Un borrador descartado
 * no es un estado: deja de existir para la API (ADR-0068 §5).
 */
export type EstadoDeTransferencia = 'borrador' | 'enviada' | 'recibida' | 'anulada' | 'desconocido';

/** Los cuatro por los que se puede acotar el listado, en el orden de su vida. */
export const ESTADOS_DE_TRANSFERENCIA = ['borrador', 'enviada', 'recibida', 'anulada'] as const;

/** Un estado por el que se puede acotar: todos menos `desconocido`, que solo se lee. */
export type EstadoConocido = (typeof ESTADOS_DE_TRANSFERENCIA)[number];

/** Una transferencia como sale en el listado: sin sus líneas. */
export interface ResumenDeTransferencia {
  readonly id: string;
  /** El correlativo, que solo existe desde que se envía. */
  readonly numero: number | null;
  readonly almacenOrigenId: string;
  readonly almacenDestinoId: string;
  /** El día en que sale, `AAAA-MM-DD`. */
  readonly fechaDeEnvio: string;
  /** El día en que entra, o nulo si no ha llegado. */
  readonly fechaDeRecepcion: string | null;
  readonly estado: EstadoDeTransferencia;
  /** La que esta compensa, si es un inverso. */
  readonly anulaAId: string | null;
}

/** Una página de algo, con el total para el paginador. */
export interface Pagina<T> {
  readonly elementos: readonly T[];
  readonly total: number;
}

/**
 * Una línea, tal como quedó en el alta.
 *
 * **El factor y la cantidad en unidad base los puso el servidor** (ADR-0068 §2): la unidad base del
 * artículo, o la conversión del día del alta. Es lo que explica lo que sale de una ubicación y
 * entra en la otra.
 */
export interface LineaDeTransferencia {
  readonly numero: number;
  readonly ubicacionOrigenId: string;
  readonly ubicacionDestinoId: string;
  readonly articuloId: string;
  /** Lo que se escribió, siempre en positivo: el inverso lleva su signo al revés. */
  readonly cantidadIntroducida: number;
  readonly unidadIntroducidaId: string;
  readonly factorAUnidadBase: number;
  readonly cantidadEnUnidadBase: number;
  readonly codigoDeLote: string | null;
  readonly numeroDeSerie: string | null;
  /** Lo que llevó al tránsito, o nulo mientras sea un borrador. */
  readonly valor: number | null;
}

/** Una transferencia como se enseña en su ficha, con todas sus líneas. */
export interface Transferencia extends ResumenDeTransferencia {
  readonly divisa: string;
  /** El de la anulación, que lo lleva el inverso. */
  readonly motivo: string | null;
  readonly lineas: readonly LineaDeTransferencia[];
}

/** La ficha tal como se leyó, con su versión: la que cita el descarte en el `If-Match`. */
export interface FichaDeTransferencia {
  readonly transferencia: Transferencia;
  readonly version: string;
}

/** Lo que devuelve la anulación: el original, ya anulado, y el inverso que lo compensa. */
export interface Anulacion {
  readonly original: ResumenDeTransferencia;
  readonly inverso: ResumenDeTransferencia;
}
