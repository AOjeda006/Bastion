import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router';

import type { Exigencia } from './rutas.tsx';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesion } from '@/shared/sesion/useSesion.ts';

/**
 * Lo que se interpone entre una ruta y su pantalla.
 *
 * **No es un control de acceso.** Es la interfaz no enseñando lo que no toca. Quien escriba la URL
 * a mano llega hasta aquí, y si se saltara esto llegaría a la pantalla — y la pantalla pediría
 * datos que el servidor le negaría igual. La autorización está en la API y solo ahí
 * (`stacks/react`: «la interfaz oculta por permiso, el servidor autoriza»).
 *
 * Sirve para dos cosas de verdad: mandar a la pantalla de acceso a quien no ha entrado, guardándole
 * a dónde iba; y explicar a quien ha entrado por qué no ve algo, en vez de enseñarle una pantalla
 * rota llena de errores 403.
 */
export function Guarda({
  exigencia,
  children,
}: {
  exigencia: Exigencia;
  children: React.ReactNode;
}): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesion();
  const ubicacion = useLocation();

  if (exigencia.clase === 'publica') {
    return <>{children}</>;
  }

  if (sesion === null) {
    // El destino viaja en el estado de la navegación y no en la URL: no tiene por qué verse, y así
    // no se queda pegado en el historial cuando el acceso ya se ha completado.
    return (
      <Navigate
        to="/acceso"
        replace
        state={{ destino: `${ubicacion.pathname}${ubicacion.search}` }}
      />
    );
  }

  if (exigencia.clase === 'permiso' && !concede(sesion, exigencia.permiso)) {
    return (
      <div
        role="alert"
        className="my-6 max-w-prose rounded border border-amber-300 bg-amber-50 p-4"
      >
        <p className="text-sm text-amber-900">{t('sesion.sinPermiso')}</p>
      </div>
    );
  }

  return <>{children}</>;
}
