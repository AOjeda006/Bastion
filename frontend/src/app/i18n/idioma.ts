/**
 * Qué idiomas hay, cuál toca y dónde se recuerda.
 *
 * `stacks/react` cuenta la identidad, el tema y **el idioma** entre lo poco que legítimamente es
 * estado global de cliente. Aquí solo vive la parte que no es React: la lista, la detección y el
 * `localStorage`. Quién lo cambia y quién repinta es cosa de `i18next`.
 */

/** Los dos que fija el §3 del plan maestro. El primero es el de por defecto. */
export const IDIOMAS = ['es', 'en'] as const;

export type Idioma = (typeof IDIOMAS)[number];

export const IDIOMA_POR_OMISION: Idioma = 'es';

const CLAVE = 'bastion.idioma';

function esIdioma(valor: string | null): valor is Idioma {
  return valor !== null && (IDIOMAS as readonly string[]).includes(valor);
}

/**
 * El idioma con el que arrancar: lo que se eligió la última vez, si no lo que pide el navegador, y
 * si no el de por omisión.
 *
 * Todo va en `try`. `localStorage` **lanza** —no devuelve `null`— en una ventana privada con las
 * cookies bloqueadas, y una excepción aquí dejaría la aplicación sin arrancar por no poder leer una
 * preferencia. Que no se acuerde del idioma es un incordio; que no cargue, no.
 */
export function idiomaInicial(): Idioma {
  try {
    const guardado = window.localStorage.getItem(CLAVE);

    if (esIdioma(guardado)) {
      return guardado;
    }
  } catch {
    // Sin depósito: se sigue por el navegador.
  }

  // `navigator.language` viene como 'es-ES' o 'en-GB': interesa la parte de antes del guion.
  const delNavegador = navigator.language.split('-')[0];

  return esIdioma(delNavegador ?? null) ? (delNavegador as Idioma) : IDIOMA_POR_OMISION;
}

/** Recuerda la elección. Si no se puede, no pasa nada: se vuelve a detectar en la próxima visita. */
export function recordarIdioma(idioma: Idioma): void {
  try {
    window.localStorage.setItem(CLAVE, idioma);
  } catch {
    // Sin depósito. Ver `idiomaInicial`.
  }
}

/**
 * El `lang` del documento tiene que decir la verdad.
 *
 * No es cosmético: WCAG 3.1.1 lo exige, y de él dependen la voz que elige un lector de pantalla, la
 * separación silábica y el corrector del navegador. Un documento con `lang="es"` leído en inglés se
 * pronuncia mal, y eso no se ve en ninguna captura de pantalla.
 */
export function marcarIdiomaDelDocumento(idioma: Idioma): void {
  document.documentElement.lang = idioma;
}
