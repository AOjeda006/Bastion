import { useTranslation } from 'react-i18next';

import { useNombreDeZona } from './nombres.ts';
import type { Zona } from '../model/zona.ts';

/**
 * La zona en una fila de los dos listados: cuál es, de dónde sale en un almacén —«de la empresa» o
 * «propia», que las pantallas distinguen (ADR-0079 §12)—, y «Cambiar», si se puede.
 *
 * El texto visible es «Cambiar», uno por fila; el nombre accesible dice de cuál, como el «Cambiar»
 * de la trazabilidad en el listado de artículos.
 */
export function LaZonaEnLaFila({
  zona,
  propia,
  nombre,
  alCambiar,
}: {
  zona: Zona;
  /** Solo en un almacén: si la zona es suya, o la hereda de su empresa. */
  propia?: boolean;
  /** Cómo se llama la fila, para el nombre accesible del botón. */
  nombre: string;
  /** Nulo sin el permiso de modificar: entonces no hay botón. */
  alCambiar: (() => void) | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const nombreDeZona = useNombreDeZona();

  return (
    <>
      {nombreDeZona(zona)}
      {propia !== undefined && (
        <span className="text-neutral-600">
          {' · '}
          {propia ? t('organizacion.zonas.propia') : t('organizacion.zonas.deLaEmpresa')}
        </span>
      )}
      {alCambiar !== null && (
        <button
          type="button"
          onClick={alCambiar}
          aria-label={t('organizacion.zonas.cambiarLaDe', { nombre })}
          className="ml-2 text-xs underline"
        >
          {t('organizacion.zonas.cambiar')}
        </button>
      )}
    </>
  );
}

/**
 * Lo que dice cómo ha quedado una zona, en una región que existe antes de tener texto: una que entra
 * en el árbol de accesibilidad con el texto ya puesto no se anuncia. Cada aviso es un párrafo nuevo
 * (`numero`), y el mismo texto dos veces se dice dos veces.
 */
export function ElAvisoDeLaZona({
  aviso,
}: {
  aviso: { readonly frase: string; readonly numero: number } | null;
}): React.JSX.Element {
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
