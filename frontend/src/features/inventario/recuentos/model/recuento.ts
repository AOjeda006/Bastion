/**
 * Un recuento, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce `api/recuentos.ts`.
 *
 * El almacén, el artículo, la ubicación y la unidad llegan como identificadores, porque son maestros
 * de otros módulos (ADR-0055 §12). La pantalla los nombra preguntando a sus dueños por la API; aquí
 * solo viajan los identificadores.
 */

/**
 * En qué punto de su vida está.
 *
 * Los cuatro que la API emite hoy, más `desconocido` para lo que llegue mañana: el estado viaja como
 * texto y el frontal se despliega aparte del backend.
 */
export type EstadoDeRecuento = 'enCurso' | 'confirmado' | 'anulado' | 'descartado' | 'desconocido';

/** Los cuatro por los que se puede acotar el listado, en el orden en que se ofrecen. */
export const ESTADOS_DE_RECUENTO = ['enCurso', 'confirmado', 'anulado', 'descartado'] as const;

/** Un estado por el que se puede acotar: todos menos `desconocido`, que solo se lee. */
export type EstadoConocido = (typeof ESTADOS_DE_RECUENTO)[number];

/** Un recuento como sale en el listado: sin líneas y sin teórico. */
export interface ResumenDeRecuento {
  readonly id: string;
  /** El correlativo, que solo existe desde que se confirma. */
  readonly numero: number | null;
  readonly almacenId: string;
  /** Día del alta, `AAAA-MM-DD`. */
  readonly fechaDeApertura: string;
  /** Día de la confirmación, `AAAA-MM-DD`, o nulo. */
  readonly fechaDeConfirmacion: string | null;
  readonly estado: EstadoDeRecuento;
  readonly motivo: string;
}

/** Un recuento como se enseña en su ficha, con las cuentas de sus líneas. */
export interface Recuento extends ResumenDeRecuento {
  readonly motivoDelDescarte: string | null;
  readonly motivoDeLaAnulacion: string | null;
  /** El ajuste que movió la diferencia, o nulo si todo cuadraba o si no se ha confirmado. */
  readonly ajusteId: string | null;
  readonly lineas: number;
  readonly lineasSinContar: number;
  /** Nulo si ya no está en curso: el teórico ya no se mueve. */
  readonly lineasConElTeoricoCambiado: number | null;
  /** Nulo si ya no está en curso. */
  readonly lineasConTransito: number | null;
  /**
   * La huella del teórico de todas las líneas, tal como estaba al leer la ficha (ADR-0055 §2). La
   * confirmación la manda: si el almacén se ha movido desde entonces, el servidor contesta `409`.
   */
  readonly huellaDelTeorico: string | null;
}

/**
 * La ficha tal como se leyó, con su versión.
 *
 * Confirmar, anular y descartar citan la versión que se vio. Si alguien ha contado o ha cambiado el
 * recuento entre medias, la versión ya no es la misma y el servidor contesta `412`.
 */
export interface FichaDeRecuento {
  readonly recuento: Recuento;
  /** El `ETag` de la lectura, que vuelve en el `If-Match`. */
  readonly version: string;
}

/**
 * Una línea: una clave de existencia del almacén, con lo contado y el teórico.
 *
 * Las cantidades van en la unidad base del artículo, y se leen como números para pintarlas. Las
 * escribe el servidor y tienen seis decimales como mucho.
 */
export interface LineaDeRecuento {
  readonly id: string;
  readonly numero: number;
  readonly ubicacionId: string;
  readonly articuloId: string;
  readonly codigoDeLote: string | null;
  readonly numeroDeSerie: string | null;
  readonly unidadBaseId: string;
  /** Nulo mientras nadie la cuente: una línea sin contar no es un cero. */
  readonly contado: number | null;
  /** El teórico de la clave cuando se contó, o nulo. */
  readonly teoricoAlContar: number | null;
  /** El teórico de ahora, o el que se congeló al confirmar; nulo si se descartó. */
  readonly teorico: number | null;
  /** Lo que viaja hacia la clave, o nulo si ya no está en curso. */
  readonly enTransito: number | null;
  /** Lo contado menos el teórico, o nulo si falta alguno de los dos. */
  readonly diferencia: number | null;
  /** Si el teórico de ahora ya no es el de cuando se contó. */
  readonly teoricoCambiado: boolean;
}

/** Una página de algo, con el total para el paginador. */
export interface Pagina<T> {
  readonly elementos: readonly T[];
  readonly total: number;
}

/** Una línea leída sola, con su versión: la que cita el `PUT` que la cuenta. */
export interface FichaDeLinea {
  readonly linea: LineaDeRecuento;
  readonly version: string;
}
