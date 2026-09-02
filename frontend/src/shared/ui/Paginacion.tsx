import { useTranslation } from 'react-i18next';

import type { Paginacion } from '@/shared/lib/parametrosDeUrl.ts';

/**
 * Anterior / siguiente, con el sitio donde se está dicho en texto.
 *
 * En `<nav>` con nombre accesible, porque es navegación. Los botones se desactivan en los extremos
 * en vez de desaparecer: un control que aparece y desaparece cambia de sitio a los de al lado
 * (`ux-ipo`: prevención de errores, consistencia).
 */
export function Paginador({
  paginacion,
  total,
  alCambiar,
}: {
  paginacion: Paginacion;
  total: number;
  alCambiar: (pagina: number) => void;
}): React.JSX.Element {
  const { t } = useTranslation();

  const ultima = Math.max(1, Math.ceil(total / paginacion.tamanio));
  const primero = total === 0 ? 0 : (paginacion.pagina - 1) * paginacion.tamanio + 1;
  const ultimo = Math.min(total, paginacion.pagina * paginacion.tamanio);

  return (
    <nav aria-label={t('paginacion.nombre')} className="mt-4 flex items-center gap-3 text-sm">
      <button
        type="button"
        disabled={paginacion.pagina <= 1}
        onClick={() => {
          alCambiar(paginacion.pagina - 1);
        }}
        className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-40"
      >
        {t('paginacion.anterior')}
      </button>
      <span className="text-neutral-600">
        {total === 0
          ? t('paginacion.sinResultados')
          : t('paginacion.rango', { primero, ultimo, total })}
      </span>
      <button
        type="button"
        disabled={paginacion.pagina >= ultima}
        onClick={() => {
          alCambiar(paginacion.pagina + 1);
        }}
        className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-40"
      >
        {t('paginacion.siguiente')}
      </button>
    </nav>
  );
}
