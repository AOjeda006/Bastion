import { useTranslation } from 'react-i18next';

import { useNombre } from '../../maestros/ui/nombres.ts';
import type { ResumenDeRecuento } from '../model/recuento.ts';
import { diaLegible } from '@/shared/lib/dias.ts';

/**
 * Cómo se nombra un recuento: por su número si lo tiene, y si no, por su almacén y su día de
 * apertura (ADR-0055 §1.4). Con un solo recuento en curso por almacén, no hay otro igual.
 */
export function useNombreDelRecuento(recuento: ResumenDeRecuento): string {
  const { t, i18n } = useTranslation();
  const almacen = useNombre('almacen', recuento.almacenId);

  return recuento.numero === null
    ? t('inventario.recuentos.sinNumero', {
        almacen: almacen.corto,
        dia: diaLegible(recuento.fechaDeApertura, i18n.language),
      })
    : t('inventario.recuentos.numerado', { numero: recuento.numero });
}
