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

  public constructor(estado: number, motivo: MotivoDeFallo) {
    super(`La API ha respondido ${String(estado)} (${motivo}).`);
    this.name = 'FalloDeApi';
    this.estado = estado;
    this.motivo = motivo;
  }
}

/**
 * De código de estado a motivo.
 *
 * El 401 no llega aquí casi nunca: el cliente HTTP renueva el testigo y repite la petición. Si
 * llega, es que la renovación tampoco valió, y entonces lo que toca no es un mensaje sino volver a
 * la pantalla de acceso — de eso se encarga la guarda en cuanto la sesión se queda en nada.
 */
export function fallo(estado: number): FalloDeApi {
  if (estado === 403) {
    return new FalloDeApi(estado, 'sinPermiso');
  }

  if (estado === 401) {
    return new FalloDeApi(estado, 'sesionCaducada');
  }

  if (estado >= 500) {
    return new FalloDeApi(estado, 'servidor');
  }

  return new FalloDeApi(estado, 'carga');
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
