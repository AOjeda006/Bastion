/**
 * Los tres estados que toda pantalla tiene que contemplar: cargando, error y vacío.
 *
 * Están aquí y no repetidos en cada pantalla para que digan lo mismo en todas. Una pantalla que
 * solo pinta el camino feliz está a medias (`stacks/react`), y las tres situaciones son igual de
 * normales que la lista llena: la primera carga, el servidor que no contesta y el listado que
 * todavía no tiene nada.
 */

/** Mientras se espera. `role="status"` para que el lector de pantalla lo diga sin interrumpir. */
export function Cargando({ que }: { que: string }): React.JSX.Element {
  return (
    <p role="status" className="py-8 text-sm text-neutral-500">
      Cargando {que}…
    </p>
  );
}

/**
 * Cuando algo falla.
 *
 * `role="alert"` porque esto sí interrumpe: el usuario está esperando datos que no van a llegar.
 * El mensaje es una frase accionable, no el error técnico —ese va al registro— y siempre hay una
 * salida: volver a intentarlo (`ux-ipo`: recuperación de errores, control y libertad).
 */
export function Fallo({
  mensaje,
  alReintentar,
}: {
  mensaje: string;
  alReintentar?: (() => void) | undefined;
}): React.JSX.Element {
  return (
    <div role="alert" className="my-6 rounded border border-red-300 bg-red-50 p-4">
      <p className="text-sm text-red-900">{mensaje}</p>
      {alReintentar !== undefined && (
        <button
          type="button"
          onClick={alReintentar}
          className="mt-3 rounded border border-red-400 px-3 py-1.5 text-sm text-red-900 hover:bg-red-100"
        >
          Volver a intentarlo
        </button>
      )}
    </div>
  );
}

/** Cuando no hay nada que enseñar. Dice qué está vacío, no «sin datos». */
export function Vacio({ mensaje }: { mensaje: string }): React.JSX.Element {
  return <p className="py-8 text-sm text-neutral-500">{mensaje}</p>;
}
