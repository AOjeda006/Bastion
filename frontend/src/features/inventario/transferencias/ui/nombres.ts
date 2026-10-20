import { useTranslation } from 'react-i18next';

import { useNombre } from '../../maestros/ui/nombres.ts';
import type { ResumenDeTransferencia } from '../model/transferencia.ts';
import { diaLegible } from '@/shared/lib/dias.ts';

/**
 * Cómo se nombra una transferencia: por su número si lo tiene, y si no, por sus dos almacenes y el
 * día en que sale.
 *
 * El número lo recibe al enviarse. **Un borrador no tiene un nombre único**: puede haber dos con
 * los mismos almacenes y el mismo día, y la fila no tiene nada más que los distinga. Queda en el
 * README de la funcionalidad.
 */
export function useNombreDeLaTransferencia(transferencia: ResumenDeTransferencia): string {
  const { t, i18n } = useTranslation();
  const origen = useNombre('almacen', transferencia.almacenOrigenId);
  const destino = useNombre('almacen', transferencia.almacenDestinoId);

  return transferencia.numero === null
    ? t('inventario.transferencias.sinNumero', {
        origen: origen.corto,
        destino: destino.corto,
        dia: diaLegible(transferencia.fechaDeEnvio, i18n.language),
      })
    : t('inventario.transferencias.numerada', { numero: transferencia.numero });
}
