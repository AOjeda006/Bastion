import type { UseFormRegisterReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

/**
 * Un desplegable de la cabecera del alta, con su error debajo.
 *
 * La primera opción es «Sin elegir» y no la primera serie: abrir con la que el navegador eligió por
 * su cuenta numeraría el documento donde nadie lo pidió.
 */
export function ListaDeLaCabecera({
  campo,
  etiqueta,
  error,
  registro,
  opciones,
  sinOpciones = null,
}: {
  campo: string;
  etiqueta: string;
  error: string | null;
  registro: UseFormRegisterReturn;
  opciones: readonly { id: string; texto: string }[];
  /** Lo que se dice si la lista llega vacía, o nulo si no hay nada que decir. */
  sinOpciones?: string | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const vacia = sinOpciones !== null && opciones.length === 0;
  const descripciones = [vacia && `${campo}-vacia`, error !== null && `${campo}-error`]
    .filter(Boolean)
    .join(' ');

  return (
    <div
      // Un `<select>` mide lo que su opción más larga, y un maestro se llama como quiera la
      // empresa. `min-w-0` deja a la caja estrecharse hasta el ancho de la pantalla, y la lista
      // con ella (WCAG 1.4.10).
      className="flex min-w-0 flex-col gap-1 text-sm"
    >
      <label htmlFor={campo} className="font-medium">
        {etiqueta}
      </label>
      <select
        id={campo}
        aria-invalid={error !== null}
        aria-describedby={descripciones === '' ? undefined : descripciones}
        {...registro}
        className="rounded border border-neutral-300 px-2 py-1.5"
      >
        <option value="">{t('inventario.lineas.sinElegir')}</option>
        {opciones.map((opcion) => (
          <option key={opcion.id} value={opcion.id}>
            {opcion.texto}
          </option>
        ))}
      </select>
      {vacia && (
        <p id={`${campo}-vacia`} className="text-neutral-600">
          {sinOpciones}
        </p>
      )}
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}
