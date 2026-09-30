import { useTranslation } from 'react-i18next';

import type { Trazabilidad } from '../model/articulo.ts';

/**
 * El nombre de una trazabilidad, y qué se dice cuando llega una que esta versión no conoce.
 *
 * Lo mismo que el tipo en el listado: lo que no se puede interpretar se dice, con su explicación en
 * el título, en vez de dejar la celda en blanco. Lo usan el listado y la pantalla de la
 * trazabilidad, que tienen que llamarla igual.
 */
export function NombreDeTrazabilidad({
  trazabilidad,
}: {
  trazabilidad: Trazabilidad;
}): React.JSX.Element {
  const { t } = useTranslation();

  if (trazabilidad === 'desconocida') {
    return (
      <span
        title={t('catalogo.articulos.trazabilidades.desconocidaDetalle')}
        className="rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-xs text-amber-900"
      >
        {t('catalogo.articulos.trazabilidades.desconocida')}
      </span>
    );
  }

  return <>{t(`catalogo.articulos.trazabilidades.${trazabilidad}`)}</>;
}
