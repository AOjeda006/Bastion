/**
 * Las dieciocho columnas de la plantilla, en su orden y tal cual.
 *
 * Están escritas aquí porque la pantalla tiene que enseñarlas —el error de cabecera dice «cópiala tal
 * cual»— y el contrato no las publica como dato. Una copia que se separa del servidor manda a copiar
 * una cabecera que el servidor rechaza, así que no se queda en una promesa:
 * `LaPlantillaDelFrontalEsLaDeLaApiTests`, en el carril rápido de la API, lee este fichero y lo compara
 * con `ImportacionDeTerceros.Cabecera`.
 */
export const CABECERA_DE_LA_PLANTILLA = [
  'identificacion_pais',
  'identificacion_numero',
  'razon_social',
  'nombre_comercial',
  'domicilio_calle',
  'domicilio_numero',
  'domicilio_codigo_postal',
  'domicilio_poblacion',
  'domicilio_subdivision',
  'domicilio_pais',
  'es_cliente',
  'es_proveedor',
  'territorio',
  'recargo_de_equivalencia',
  'criterio_de_caja',
  'sujeto_a_retencion_irpf',
  'limite_credito',
  'limite_credito_divisa',
] as const;

/**
 * Por qué no ha entrado una fila, de una lista cerrada.
 *
 * `desconocido` no es defensa por si acaso, por lo mismo que la verificación del listado: el motivo
 * viaja como texto y el frontal se despliega aparte de la API, así que un motivo nuevo llega antes de
 * que esta lista lo conozca. Sin él, la celda saldría vacía, que es lo que se ve cuando algo se rompe.
 */
export type MotivoDeRechazo =
  | 'numeroDeCamposDistinto'
  | 'comillasMalColocadas'
  | 'obligatorio'
  | 'demasiadoLargo'
  | 'formatoNoValido'
  | 'noValido'
  | 'niClienteNiProveedor'
  | 'yaExiste'
  | 'repetidaEnElFichero'
  | 'desconocido';

/** Un motivo en una columna, y las líneas de la hoja en las que se da. */
export interface Rechazo {
  /** La columna tal como va en la cabecera, o `null` si el motivo es de la fila entera. */
  readonly columna: string | null;
  readonly motivo: MotivoDeRechazo;
  /** Contadas como las enseña la hoja de cálculo: la cabecera es la 1. */
  readonly lineas: readonly number[];
}

/** Lo que ha pasado con un fichero. Nunca trae un valor del fichero: solo dónde y por qué. */
export interface InformeDeImportacion {
  readonly leidas: number;
  readonly importadas: number;
  readonly rechazadas: number;
  readonly rechazos: readonly Rechazo[];
}

/**
 * El fichero elegido y la clave de idempotencia que le toca.
 *
 * **La clave va con la elección, no con el envío.** Volver a mandar el mismo fichero —tras un fallo de
 * red, o con un doble clic— es la misma operación, y el servidor contesta el informe guardado sin volver a
 * importar. Elegir otro fichero es otra operación, y estrena clave: con la anterior, el servidor
 * contestaría un 409 por cuerpo distinto.
 */
export interface Eleccion {
  readonly fichero: File;
  readonly clave: string;
}

/** Una elección nueva, con su clave recién generada. */
export function elegir(fichero: File): Eleccion {
  return { fichero, clave: crypto.randomUUID() };
}
