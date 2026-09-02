import { Link } from 'react-router';

import { useSesion } from '@/shared/sesion/useSesion.ts';

/** Una URL que no existe. Se dice qué ha pasado y se da una salida, no un callejón. */
export function PaginaNoEncontrada(): React.JSX.Element {
  const sesion = useSesion();

  return (
    <div className="mt-4 max-w-prose space-y-3 text-sm">
      <p>Esta dirección no corresponde a ninguna pantalla de Bastion.</p>
      <p>
        <Link to={sesion === null ? '/acceso' : '/'} className="underline">
          {sesion === null ? 'Ir a la pantalla de acceso' : 'Volver al inicio'}
        </Link>
      </p>
    </div>
  );
}
