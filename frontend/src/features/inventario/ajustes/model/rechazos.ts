import { actualDeFallo, camposDeFallo, tipoDeFallo } from '@/shared/api/errores.ts';

/**
 * Los rechazos del alta que van en un campo, y en cuál (ADR-0065 §3).
 *
 * Son los `type` de la API (ADR-0030), y el texto es el de `errores.tipos`: lo diga el esquema del
 * formulario o lo diga el servidor, el campo enseña la misma frase. Los tipos literales son lo que
 * deja escribir `t(`errores.tipos.${rechazo}`)` sin un `as`: una errata aquí no compila.
 */

/** Los campos de una línea, con el nombre que llevan en el cuerpo y en el formulario. */
export type CampoDeLaLinea =
  | 'ubicacionId'
  | 'articuloId'
  | 'cantidadIntroducida'
  | 'unidadIntroducidaId'
  | 'costeUnitario'
  | 'codigoDeLote'
  | 'numeroDeSerie';

/**
 * El campo de cada rechazo de una línea. La trazabilidad que no casa va al artículo, porque es su
 * marca la que no casa; la serie que no es unitaria, a la cantidad, que es lo que se corrige.
 */
const CAMPOS = {
  'ajuste-ubicacion-no-encontrada': 'ubicacionId',
  'ajuste-ubicacion-bloqueada': 'ubicacionId',
  'ajuste-articulo-no-encontrado': 'articuloId',
  'ajuste-articulo-no-se-almacena': 'articuloId',
  'ajuste-trazabilidad-no-casa': 'articuloId',
  'ajuste-unidad-no-encontrada': 'unidadIntroducidaId',
  'ajuste-unidad-retirada': 'unidadIntroducidaId',
  'ajuste-sin-conversion': 'unidadIntroducidaId',
  'ajuste-conversion-retirada': 'unidadIntroducidaId',
  'ajuste-lote-no-valido': 'codigoDeLote',
  'ajuste-numero-de-serie-no-valido': 'numeroDeSerie',
  'ajuste-serie-repetida': 'numeroDeSerie',
  'ajuste-serie-no-unitaria': 'cantidadIntroducida',
  'ajuste-cantidad-no-valida': 'cantidadIntroducida',
} as const satisfies Record<string, CampoDeLaLinea>;

export type RechazoDeLaLinea = keyof typeof CAMPOS;

/** En un `Map` y no leyendo el objeto: el `type` viene de la red, y un objeto hereda `constructor`. */
const CAMPO_DEL_RECHAZO: ReadonlyMap<string, CampoDeLaLinea> = new Map(Object.entries(CAMPOS));

export const RECHAZO_DEL_MOTIVO = 'ajuste-motivo-no-valido';
export const RECHAZO_DE_LA_CANTIDAD = 'ajuste-cantidad-no-valida';
export const RECHAZO_DEL_COSTE = 'ajuste-coste-no-valido';
export const RECHAZO_DE_LA_FECHA = 'ajuste-con-fecha-futura';

/** Los que el formulario puede enseñar en un campo: los de la línea y los que dice su esquema. */
export type RechazoDeCampo =
  | RechazoDeLaLinea
  | typeof RECHAZO_DEL_MOTIVO
  | typeof RECHAZO_DEL_COSTE
  | typeof RECHAZO_DE_LA_FECHA;

const DE_CAMPO: ReadonlySet<string> = new Set<string>([
  ...CAMPO_DEL_RECHAZO.keys(),
  RECHAZO_DEL_MOTIVO,
  RECHAZO_DEL_COSTE,
  RECHAZO_DE_LA_FECHA,
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

const RUTA_DE_UNA_LINEA = /^lineas\[(\d+)\]\./;

/**
 * La línea de la que habla un rechazo, desde cero, o nula si no dice ninguna.
 *
 * Un `400` la lleva en la ruta de `errors` (`lineas[2].numeroDeSerie`, desde cero), y un `409` o un
 * `422`, en `actual.linea`, desde uno, que es el número que enseña la ficha.
 */
function indiceDe(error: unknown): number | null {
  for (const ruta of camposDeFallo(error)) {
    const partes = RUTA_DE_UNA_LINEA.exec(ruta);

    if (partes !== null) {
      return Number(partes[1]);
    }
  }

  const linea = actualDeFallo(error)?.['linea'];

  return typeof linea === 'number' && Number.isInteger(linea) && linea >= 1 ? linea - 1 : null;
}

/**
 * En qué campo de qué línea va un rechazo, o nulo si va arriba del formulario: lo que no es de una
 * línea, y lo que dice ser de una línea sin decir cuál.
 */
export function rechazoEnLaLinea(error: unknown): RechazoEnLaLinea | null {
  const tipo = tipoDeFallo(error);
  const indice = indiceDe(error);

  if (!esDeLaLinea(tipo) || indice === null) {
    return null;
  }

  return { indice, campo: CAMPOS[tipo], tipo };
}
