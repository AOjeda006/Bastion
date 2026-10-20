import type { UseFormRegisterReturn } from 'react-hook-form';

import { Celda } from './Celda.tsx';
import type { CeldaDeLaLinea } from './useCeldasDeLaLinea.ts';

/** Una celda que se escribe: la cantidad, el coste, el lote o la serie. */
export function CeldaDeTexto({
  celda,
  registro,
  ancho,
  deshabilitada = false,
}: {
  celda: CeldaDeLaLinea;
  registro: UseFormRegisterReturn;
  /** La clase del ancho, que es de cada columna. */
  ancho: string;
  /** Si no se puede escribir, como el lote de un artículo que no se sigue por lote. */
  deshabilitada?: boolean;
}): React.JSX.Element {
  return (
    <Celda id={celda.propiedades.id} error={celda.error}>
      <input
        type="text"
        autoComplete="off"
        disabled={deshabilitada}
        {...celda.propiedades}
        {...registro}
        className={`${ancho} rounded border border-neutral-300 px-2 py-1 disabled:bg-neutral-100`}
      />
    </Celda>
  );
}
