import type { UseFormRegisterReturn } from 'react-hook-form';

import { Celda } from './Celda.tsx';
import type { CeldaDeLaLinea } from './useCeldasDeLaLinea.ts';
import type { Maestro } from '../../maestros/api/maestros.ts';
import { textoDeLaOpcion } from '../model/lineas.ts';

/**
 * Una celda que se elige de una lista: una ubicación o una unidad.
 *
 * La primera opción es la vacía, y no la primera de la lista: una línea que el navegador hubiera
 * llenado por su cuenta movería stock de donde nadie lo eligió.
 */
export function CeldaDeLista({
  celda,
  registro,
  opciones,
  vacia,
  ancho,
}: {
  celda: CeldaDeLaLinea;
  registro: UseFormRegisterReturn;
  opciones: readonly Maestro[];
  /** El texto de la opción vacía. */
  vacia: string;
  /** La clase del ancho, que es de cada columna. */
  ancho: string;
}): React.JSX.Element {
  return (
    <Celda id={celda.propiedades.id} error={celda.error}>
      <select
        {...celda.propiedades}
        {...registro}
        className={`${ancho} rounded border border-neutral-300 px-2 py-1`}
      >
        <option value="">{vacia}</option>
        {opciones.map((opcion) => (
          <option key={opcion.id} value={opcion.id}>
            {textoDeLaOpcion(opcion)}
          </option>
        ))}
      </select>
    </Celda>
  );
}
