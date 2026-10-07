import { useTranslation } from 'react-i18next';

import type { EstadoDeRecuento } from '../model/recuento.ts';

/**
 * El estado, escrito y no solo pintado: quien no distinga los colores tiene que poder leerlo.
 *
 * Lo que esta versión no conoce lleva la marca de siempre, y su explicación va aparte, debajo,
 * señalada por la celda (`ExplicacionDeMarca`).
 */
export function Estado({ estado }: { estado: EstadoDeRecuento }): React.JSX.Element {
  const { t } = useTranslation();

  const pinta: Record<EstadoDeRecuento, string> = {
    enCurso: 'border-sky-300 bg-sky-50 text-sky-900',
    confirmado: 'border-emerald-300 bg-emerald-50 text-emerald-900',
    anulado: 'border-neutral-300 bg-neutral-50 text-neutral-700',
    descartado: 'border-neutral-300 bg-neutral-50 text-neutral-700',
    desconocido: 'border-amber-300 bg-amber-50 text-amber-900',
  };

  return (
    <span className={`rounded border px-1.5 py-0.5 text-xs ${pinta[estado]}`}>
      {t(`inventario.recuentos.estados.${estado}`)}
    </span>
  );
}
