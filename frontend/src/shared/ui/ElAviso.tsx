import type { Aviso } from './avisoSiguiente.ts';

/**
 * Lo que dice cómo ha quedado un cambio hecho en una ventana, en una región que existe antes de
 * tener texto: una que entra en el árbol de accesibilidad con el texto ya puesto no se anuncia.
 * Cada aviso es un párrafo nuevo (`numero`), y el mismo texto dos veces se dice dos veces.
 *
 * Nació con la zona (ítem 3.5), en `organizacion`, y subió aquí con la retirada del artículo (ítem
 * 3.6): lo que piden dos funcionalidades no se importa de una a otra.
 */
export function ElAviso({ aviso }: { aviso: Aviso | null }): React.JSX.Element {
  return (
    <div role="status">
      {aviso !== null && (
        <p
          key={aviso.numero}
          className="mt-4 rounded border border-green-300 bg-green-50 p-3 text-sm text-green-900"
        >
          {aviso.frase}
        </p>
      )}
    </div>
  );
}
