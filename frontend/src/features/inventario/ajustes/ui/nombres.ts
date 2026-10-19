import { useTranslation } from 'react-i18next';

import { useNombre } from '../../maestros/ui/nombres.ts';
import type { ResumenDeAjuste } from '../model/ajuste.ts';
import { diaLegible } from '@/shared/lib/dias.ts';

/**
 * Cómo se nombra un ajuste: por su número si lo tiene, y si no, por su almacén y su día.
 *
 * El número lo recibe al confirmarse. **Un borrador no tiene un nombre único**: puede haber dos del
 * mismo almacén y el mismo día, y entonces los distingue su motivo, que el listado enseña al lado.
 */
export function useNombreDelAjuste(ajuste: ResumenDeAjuste): string {
  const { t, i18n } = useTranslation();
  const almacen = useNombre('almacen', ajuste.almacenId);

  return ajuste.numero === null
    ? t('inventario.ajustes.sinNumero', {
        almacen: almacen.corto,
        dia: diaLegible(ajuste.fechaDeOperacion, i18n.language),
      })
    : t('inventario.ajustes.numerado', { numero: ajuste.numero });
}
