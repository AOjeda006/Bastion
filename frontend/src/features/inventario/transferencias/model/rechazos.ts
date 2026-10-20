import { actualDeFallo, camposDeFallo, tipoDeFallo } from '@/shared/api/errores.ts';

/**
 * Los rechazos que van en un campo, y en cuál (ADR-0068 §6).
 *
 * Son los `type` de la API (ADR-0030), y el texto es el de `errores.tipos`: lo diga el esquema del
 * formulario o lo diga el servidor, el campo enseña la misma frase. Los tipos literales son lo que
 * deja escribir `t(`errores.tipos.${rechazo}`)` sin un `as`: una errata aquí no compila.
 */

export const RECHAZO_DEL_MOTIVO = 'transferencia-motivo-no-valido';
export const RECHAZO_DE_LA_FECHA = 'transferencia-con-fecha-futura';
export const RECHAZO_DE_LA_RECEPCION = 'transferencia-recepcion-antes-del-envio';
export const RECHAZO_DE_LA_CANTIDAD = 'transferencia-cantidad-no-valida';
export const RECHAZO_DEL_MISMO_ALMACEN = 'transferencia-mismo-almacen';

/**
 * Los que dicen algo de la fecha de la recepción, y van en ella: los dos que también dice su
 * esquema, y los que solo sabe el servidor, que son del ejercicio en que cae y del último
 * movimiento de algún artículo en el destino. Todos se arreglan cambiando el día.
 */
const DE_LA_RECEPCION = [
  RECHAZO_DE_LA_FECHA,
  RECHAZO_DE_LA_RECEPCION,
  'transferencia-sin-ejercicio',
  'transferencia-en-ejercicio-cerrado',
  'transferencia-fecha-anterior-al-ultimo-movimiento',
] as const;

export type RechazoDeLaRecepcion = (typeof DE_LA_RECEPCION)[number];

/** En un conjunto y no leyendo un objeto: el `type` viene de la red, y un objeto hereda `constructor`. */
const RECHAZOS_DE_LA_RECEPCION: ReadonlySet<string> = new Set(DE_LA_RECEPCION);

export function esRechazoDeLaRecepcion(tipo: string | null): tipo is RechazoDeLaRecepcion {
  return tipo !== null && RECHAZOS_DE_LA_RECEPCION.has(tipo);
}

/** Los campos de una línea, con el nombre que llevan en el cuerpo y en el formulario. */
export type CampoDeLaLinea =
  | 'ubicacionOrigenId'
  | 'ubicacionDestinoId'
  | 'articuloId'
  | 'cantidadIntroducida'
  | 'unidadIntroducidaId'
  | 'codigoDeLote'
  | 'numeroDeSerie';

/** Lo que dice el rechazo de una ubicación: su punta, y con ella, cuál de las dos celdas. */
const PUNTA = 'punta';

/**
 * El campo de cada rechazo de una línea. La trazabilidad que no casa va al artículo, porque es su
 * marca la que no casa; la serie que no es unitaria, a la cantidad, que es lo que se corrige. Las
 * dos de la ubicación van a la de su punta, que el rechazo dice.
 */
const CAMPOS = {
  'transferencia-ubicacion-no-encontrada': PUNTA,
  'transferencia-ubicacion-bloqueada': PUNTA,
  'transferencia-articulo-no-encontrado': 'articuloId',
  'transferencia-articulo-no-se-almacena': 'articuloId',
  'transferencia-trazabilidad-no-casa': 'articuloId',
  'transferencia-unidad-no-encontrada': 'unidadIntroducidaId',
  'transferencia-unidad-retirada': 'unidadIntroducidaId',
  'transferencia-sin-conversion': 'unidadIntroducidaId',
  'transferencia-conversion-retirada': 'unidadIntroducidaId',
  'transferencia-lote-no-valido': 'codigoDeLote',
  'transferencia-numero-de-serie-no-valido': 'numeroDeSerie',
  'transferencia-serie-repetida': 'numeroDeSerie',
  'transferencia-serie-no-unitaria': 'cantidadIntroducida',
  [RECHAZO_DE_LA_CANTIDAD]: 'cantidadIntroducida',
} as const satisfies Record<string, CampoDeLaLinea | typeof PUNTA>;

export type RechazoDeLaLinea = keyof typeof CAMPOS;

/** En un `Map` y no leyendo el objeto: el `type` viene de la red, y un objeto hereda `constructor`. */
const CAMPO_DEL_RECHAZO: ReadonlyMap<string, CampoDeLaLinea | typeof PUNTA> = new Map(
  Object.entries(CAMPOS),
);

/** La celda de cada punta, con el nombre que le da la API (`UbicacionDeLaLineaDto`). */
const CELDA_DE_LA_PUNTA: ReadonlyMap<unknown, CampoDeLaLinea> = new Map([
  ['Origen', 'ubicacionOrigenId'],
  ['Destino', 'ubicacionDestinoId'],
]);

/** Los que el formulario puede enseñar en un campo: los de la línea y los que dice su esquema. */
export type RechazoDeCampo =
  | RechazoDeLaLinea
  | RechazoDeLaRecepcion
  | typeof RECHAZO_DEL_MOTIVO
  | typeof RECHAZO_DEL_MISMO_ALMACEN;

const DE_CAMPO: ReadonlySet<string> = new Set<string>([
  ...CAMPO_DEL_RECHAZO.keys(),
  ...RECHAZOS_DE_LA_RECEPCION,
  RECHAZO_DEL_MOTIVO,
  RECHAZO_DEL_MISMO_ALMACEN,
]);

export function esRechazoDeCampo(mensaje: string): mensaje is RechazoDeCampo {
  return DE_CAMPO.has(mensaje);
}

function esDeLaLinea(tipo: string | null): tipo is RechazoDeLaLinea {
  return tipo !== null && CAMPO_DEL_RECHAZO.has(tipo);
}

/** Dónde va un rechazo de una línea: su posición en el formulario, desde cero, y su campo. */
export interface RechazoEnLaLinea {
  readonly indice: number;
  readonly campo: CampoDeLaLinea;
  readonly tipo: RechazoDeLaLinea;
}

const RUTA_DE_UNA_LINEA = /^lineas\[(\d+)\]\.(\w+)$/;

/** La primera ruta de `errors` que nombra un campo de una línea: su índice, desde cero, y su campo. */
function rutaDe(error: unknown): { readonly indice: number; readonly campo: string } | null {
  for (const ruta of camposDeFallo(error)) {
    const [, indice, campo] = RUTA_DE_UNA_LINEA.exec(ruta) ?? [];

    if (indice !== undefined && campo !== undefined) {
      return { indice: Number(indice), campo };
    }
  }

  return null;
}

/**
 * La línea de la que habla un rechazo, desde cero, o nula si no dice ninguna.
 *
 * Un `400` la lleva en la ruta de `errors` (`lineas[2].numeroDeSerie`, desde cero), y un `409` o un
 * `422`, en `actual.linea`, desde uno, que es el número que enseña la ficha.
 */
function indiceDe(error: unknown): number | null {
  const ruta = rutaDe(error);

  if (ruta !== null) {
    return ruta.indice;
  }

  const linea = actualDeFallo(error)?.['linea'];

  return typeof linea === 'number' && Number.isInteger(linea) && linea >= 1 ? linea - 1 : null;
}

/**
 * La celda de la ubicación de la que habla un rechazo, o nula si no dice cuál.
 *
 * La que no existe es un `400`, y su punta va en la ruta (`lineas[0].ubicacionDestinoId`); la
 * bloqueada es un `409`, y la dice `actual.punta`.
 */
function celdaDeLaPunta(error: unknown): CampoDeLaLinea | null {
  const campo = rutaDe(error)?.campo;

  if (campo === 'ubicacionOrigenId' || campo === 'ubicacionDestinoId') {
    return campo;
  }

  return CELDA_DE_LA_PUNTA.get(actualDeFallo(error)?.['punta']) ?? null;
}

/**
 * En qué campo de qué línea va un rechazo, o nulo si va arriba del formulario: lo que no es de una
 * línea, y lo que dice ser de una línea sin decir cuál, o de una ubicación sin decir su punta.
 */
export function rechazoEnLaLinea(error: unknown): RechazoEnLaLinea | null {
  const tipo = tipoDeFallo(error);
  const indice = indiceDe(error);

  if (!esDeLaLinea(tipo) || indice === null) {
    return null;
  }

  const delRechazo = CAMPOS[tipo];
  const campo = delRechazo === PUNTA ? celdaDeLaPunta(error) : delRechazo;

  return campo === null ? null : { indice, campo, tipo };
}
