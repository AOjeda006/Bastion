/**
 * Por qué ha fallado una llamada, en cuatro casos. Cada uno tiene su frase en `errores.*` del
 * diccionario, con el mismo nombre.
 *
 * ES UN MOTIVO Y NO UNA FRASE, y ese es el cambio del ítem 0.14. Antes esta capa —que es red, no
 * pantalla— escribía castellano y se lo daba pintado a los componentes. Con dos idiomas eso obliga
 * a una de dos cosas malas: o la capa de red llama a `t()` —y entonces sabe de presentación, y
 * además tiene que resolver el idioma fuera de React, donde no hay contexto—, o el texto sale
 * siempre en el idioma en que se escribió. Devolviendo el motivo, quien traduce es quien pinta,
 * que es el único que está dentro de React y sabe en qué idioma se está.
 */
export type MotivoDeFallo = 'sinPermiso' | 'sesionCaducada' | 'servidor' | 'carga';

/**
 * El fallo de una llamada a la API.
 *
 * Se lanza —no se devuelve— porque quien lo consume es la caché de consultas, que distingue el
 * camino feliz del que no lo es por la excepción. `estado` y `message` son para el registro y
 * nunca se le enseñan a nadie; lo que se pinta sale de `motivo`.
 */
export class FalloDeApi extends Error {
  public readonly estado: number;
  public readonly motivo: MotivoDeFallo;

  /**
   * El código del `type` del ProblemDetails, ya sin la base `/errors/`, o `null` si la respuesta
   * no traía ninguno (un fallo de red, un 502 de un intermediario, un cuerpo vacío).
   *
   * Se guarda el CÓDIGO y no el `type` entero porque es lo que sirve de clave: el artefacto
   * `docs/api/errores.json` lleva los dos, y el diccionario se indexa por el código. Quitar la
   * base aquí, en un sitio, evita que cada pantalla la recorte a su manera.
   */
  public readonly tipo: string | null;

  /**
   * El identificador de traza que toda respuesta de error de esta API lleva, o `null`.
   *
   * No se le enseña a nadie salvo cuando el `type` es desconocido: entonces es lo único que
   * permite ir al registro y ver qué pasó de verdad. Es el mismo valor que Serilog escribe.
   */
  public readonly traza: string | null;

  /**
   * Los errores por campo de una validación (`errors`), por la ruta del campo tal como viaja en el
   * cuerpo —`lineas[2].numeroDeSerie`—, o vacío si no los trae (ADR-0065 §3).
   *
   * El texto de cada uno es del servidor y no se enseña: lo que se pinta sale del `type`. Lo que
   * sirve es la clave, que dice en qué campo poner el error.
   */
  public readonly campos: Readonly<Record<string, readonly string[]>>;

  /**
   * El estado actual del recurso (`actual`, ADR-0055 §11), o `null` si no lo trae. Un conflicto
   * de una línea lleva aquí su número, desde uno (ADR-0065 §3).
   */
  public readonly actual: Readonly<Record<string, unknown>> | null;

  public constructor(
    estado: number,
    motivo: MotivoDeFallo,
    tipo: string | null = null,
    traza: string | null = null,
    campos: Readonly<Record<string, readonly string[]>> = {},
    actual: Readonly<Record<string, unknown>> | null = null,
  ) {
    super(`La API ha respondido ${String(estado)} (${motivo}).`);
    this.name = 'FalloDeApi';
    this.estado = estado;
    this.motivo = motivo;
    this.tipo = tipo;
    this.traza = traza;
    this.campos = campos;
    this.actual = actual;
  }
}

/** La base de los `type` que emite la API, tal como la publica `docs/api/errores.json`. */
const BASE_DE_TIPOS = '/errors/';

/**
 * Lo que se puede leer del cuerpo de una respuesta de error.
 *
 * No se tipa desde `esquema.ts` a propósito: esto tiene que sobrevivir a un cuerpo que no sea el
 * que se esperaba —una página de error de un intermediario, un JSON de otra forma—, así que se
 * comprueba campo a campo en vez de fiarse del contrato.
 */
interface CuerpoDeProblema {
  type?: unknown;
  traceId?: unknown;
  errors?: unknown;
  actual?: unknown;
}

function esUnObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

/** Los errores por campo, con las entradas que tienen la forma esperada y sin las que no. */
function camposDe(problema: CuerpoDeProblema | undefined): Record<string, readonly string[]> {
  const errores = problema?.errors;

  if (!esUnObjeto(errores)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errores).filter(
      (entrada): entrada is [string, string[]] =>
        Array.isArray(entrada[1]) && entrada[1].every((texto) => typeof texto === 'string'),
    ),
  );
}

function codigoDe(problema: CuerpoDeProblema | undefined): string | null {
  const tipo = problema?.type;

  if (typeof tipo !== 'string' || !tipo.startsWith(BASE_DE_TIPOS)) {
    return null;
  }

  const codigo = tipo.slice(BASE_DE_TIPOS.length);

  return codigo === '' ? null : codigo;
}

/**
 * De código de estado a motivo.
 *
 * El 401 no llega aquí casi nunca: el cliente HTTP renueva el testigo y repite la petición. Si
 * llega, es que la renovación tampoco valió, y entonces lo que toca no es un mensaje sino volver a
 * la pantalla de acceso — de eso se encarga la guarda en cuanto la sesión se queda en nada.
 */
export function fallo(estado: number, problema?: unknown): FalloDeApi {
  const cuerpo =
    typeof problema === 'object' && problema !== null ? (problema as CuerpoDeProblema) : undefined;

  const tipo = codigoDe(cuerpo);
  const traza = typeof cuerpo?.traceId === 'string' ? cuerpo.traceId : null;
  const campos = camposDe(cuerpo);
  const actual = esUnObjeto(cuerpo?.actual) ? cuerpo.actual : null;

  const motivo: MotivoDeFallo =
    estado === 403
      ? 'sinPermiso'
      : estado === 401
        ? 'sesionCaducada'
        : estado >= 500
          ? 'servidor'
          : 'carga';

  return new FalloDeApi(estado, motivo, tipo, traza, campos, actual);
}

/**
 * El motivo de un error cualquiera, venga de donde venga.
 *
 * La caché de consultas tipa su error como `Error`, así que en la pantalla llega sin el motivo a
 * la vista. Lo que NO se hace aquí es suponer: un error que no es de la API —un fallo al traducir
 * la respuesta, un bug— cae en `carga`, que es la frase genérica, en vez de inventarle una causa.
 */
export function motivoDeFallo(error: unknown): MotivoDeFallo {
  return error instanceof FalloDeApi ? error.motivo : 'carga';
}

/**
 * El código del `type` de un error cualquiera, o `null` si no lo trae.
 *
 * Mismo criterio que `motivoDeFallo`: lo que no es un fallo de la API no tiene `type`, y no se le
 * inventa uno.
 */
export function tipoDeFallo(error: unknown): string | null {
  return error instanceof FalloDeApi ? error.tipo : null;
}

/** El identificador de traza de un error cualquiera, o `null` si no lo trae. */
export function trazaDeFallo(error: unknown): string | null {
  return error instanceof FalloDeApi ? error.traza : null;
}

/** Las rutas de los campos que un error cualquiera dice que fallan, o ninguna si no las trae. */
export function camposDeFallo(error: unknown): readonly string[] {
  return error instanceof FalloDeApi ? Object.keys(error.campos) : [];
}

/** El estado actual que trae un error cualquiera, o `null` si no lo trae. */
export function actualDeFallo(error: unknown): Readonly<Record<string, unknown>> | null {
  return error instanceof FalloDeApi ? error.actual : null;
}
