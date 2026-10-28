import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { Fallo } from './Estados.tsx';
import { guardarFichero } from './guardarFichero.ts';
import { useTextoDeFallo } from './useTextoDeFallo.ts';
import type { Fichero } from '@/shared/api/exportacion.ts';

/**
 * El botón que descarga el listado que se está viendo, entero, en CSV (ítem 3.2).
 *
 * **Exporta lo que la pantalla ve, y no lo que tiene en memoria**: quien lo pone le pasa la
 * exportación con los mismos filtros con los que pide el listado, y el servidor escribe todas las
 * páginas, no la que se está enseñando. Por eso no recibe filas.
 *
 * Mientras llega, el botón se apaga y una línea lo dice (`role="status"`); si falla, lo dice con la
 * frase del fallo, como el listado, y el botón vuelve a estar ahí para intentarlo otra vez. Se pone
 * solo cuando el listado tiene filas: exportar uno vacío daría una cabecera sola.
 *
 * @param exportar - Pide la exportación, con los filtros del listado, y devuelve el fichero.
 */
export function BotonDeExportar({
  exportar,
}: {
  exportar: () => Promise<Fichero>;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();

  const exportacion = useMutation({ mutationFn: exportar, onSuccess: guardarFichero });

  return (
    <div className="mt-4">
      <button
        type="button"
        disabled={exportacion.isPending}
        onClick={() => {
          exportacion.mutate();
        }}
        className="rounded border border-neutral-300 px-3 py-1.5 text-sm disabled:opacity-50"
      >
        {t('comun.exportar')}
      </button>

      {exportacion.isPending && (
        <p role="status" className="py-2 text-sm text-neutral-500">
          {t('comun.exportando')}
        </p>
      )}

      {exportacion.isError && <Fallo mensaje={textoDeFallo(exportacion.error)} />}
    </div>
  );
}
