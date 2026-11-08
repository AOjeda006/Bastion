import { useTranslation } from 'react-i18next';

import type { Zona } from '../model/zona.ts';

/** El nombre de una zona en el idioma de quien mira: «Canarias», y no `Atlantic/Canary`. */
export function useNombreDeZona(): (zona: Zona) => string {
  const { t } = useTranslation();

  return (zona) => t(`organizacion.zonas.nombres.${zona}`);
}
