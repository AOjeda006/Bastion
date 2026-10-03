import { useTranslation } from 'react-i18next';

import { SinReconocer } from './SinReconocer.tsx';
import type { Trazabilidad } from '../model/articulo.ts';

/**
 * El nombre de una trazabilidad, y la marca cuando llega una que esta versión no conoce.
 *
 * Lo usan el listado y la pantalla de la trazabilidad, que tienen que llamarla igual. La
 * explicación de la marca no va aquí: la pone cada pantalla, una vez, y la celda la señala
 * (`SinReconocer`).
 */
export function NombreDeTrazabilidad({
  trazabilidad,
}: {
  trazabilidad: Trazabilidad;
}): React.JSX.Element {
  const { t } = useTranslation();

  if (trazabilidad === 'desconocida') {
    return <SinReconocer texto={t('catalogo.articulos.trazabilidades.desconocida')} />;
  }

  return <>{t(`catalogo.articulos.trazabilidades.${trazabilidad}`)}</>;
}
