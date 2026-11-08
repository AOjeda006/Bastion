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
