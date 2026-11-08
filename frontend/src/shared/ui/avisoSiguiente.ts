/** Lo que dice cómo ha quedado algo, con su número: dos avisos iguales son dos avisos. */
export interface Aviso {
  readonly frase: string;
  readonly numero: number;
}

/**
 * El aviso que sigue a `antes`, con `frase`. Vive aparte de `ElAviso`, como `cerrarElDialogo` de
 * `Dialogo`: un fichero de componentes que exporta otra cosa deja sin recarga en caliente al
 * componente.
 */
export function avisoSiguiente(antes: Aviso | null, frase: string): Aviso {
  return { frase, numero: (antes?.numero ?? 0) + 1 };
}
