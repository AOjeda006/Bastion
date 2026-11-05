import { useTranslation } from 'react-i18next';

import type { Hoy } from './hoy.ts';
import { useHoy } from './useHoy.ts';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Lo que necesita saber qué día es, cuando ya lo sabe: un formulario con fecha, que la lleva por
 * omisión y la usa de tope.
 *
 * Mientras llega, «Cargando el día de hoy…», y si falla, el fallo con su reintento. **Lo que ya se
 * sabía se sigue pintando aunque falle la pregunta de medianoche**: desmontar el formulario por un
 * reintento en segundo plano se llevaría lo escrito.
 */
export function ConElDiaDeHoy({
  children,
}: {
  children: (hoy: Hoy) => React.ReactNode;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const hoy = useHoy();

  if (hoy.data !== undefined) {
    return <>{children(hoy.data)}</>;
  }

  if (hoy.isError) {
    return (
      <Fallo
        mensaje={textoDeFallo(hoy.error)}
        alReintentar={() => {
          void hoy.refetch();
        }}
      />
    );
  }

  return <Cargando que={t('comun.elDiaDeHoy')} />;
}
