import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useSesion } from '@/shared/sesion/useSesion.ts';

/** Una URL que no existe. Se dice qué ha pasado y se da una salida, no un callejón. */
export function PaginaNoEncontrada(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesion();

  return (
    <div className="mt-4 max-w-prose space-y-3 text-sm">
      <p>{t('inicio.noEncontrada')}</p>
      <p>
        <Link to={sesion === null ? '/acceso' : '/'} className="underline">
          {sesion === null ? t('inicio.irAlAcceso') : t('inicio.volverAlInicio')}
        </Link>
      </p>
    </div>
  );
}
