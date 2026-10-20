import { useTranslation } from 'react-i18next';

import type { EstadoDeTransferencia } from '../model/transferencia.ts';

/**
 * El estado, escrito y no solo pintado: quien no distinga los colores tiene que poder leerlo.
 *
 * Lo que esta versión no conoce lleva la marca de siempre, y su explicación va aparte, debajo,
 * señalada por la celda (`ExplicacionDeMarca`).
 */
export function Estado({ estado }: { estado: EstadoDeTransferencia }): React.JSX.Element {
  const { t } = useTranslation();

  const pinta: Record<EstadoDeTransferencia, string> = {
    borrador: 'border-sky-300 bg-sky-50 text-sky-900',
    enviada: 'border-violet-300 bg-violet-50 text-violet-900',
    recibida: 'border-emerald-300 bg-emerald-50 text-emerald-900',
    anulada: 'border-neutral-300 bg-neutral-50 text-neutral-700',
    desconocido: 'border-amber-300 bg-amber-50 text-amber-900',
  };

  return (
    <span className={`rounded border px-1.5 py-0.5 text-xs ${pinta[estado]}`}>
      {t(`inventario.transferencias.estados.${estado}`)}
    </span>
  );
}
