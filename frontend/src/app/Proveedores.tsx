import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { I18nextProvider } from 'react-i18next';
import type { i18n as InstanciaDeI18n } from 'i18next';

import { ProveedorDeSesion } from '@/shared/sesion/ProveedorDeSesion.tsx';

/**
 * Lo global, montado una sola vez: el idioma, la caché de servidor y la recuperación de la sesión.
 *
 * El idioma va MÁS AFUERA que todo lo demás porque todo lo demás puede necesitar traducir, incluido
 * el mensaje de que la sesión no se ha podido recuperar.
 */
export function Proveedores({
  cache,
  i18n,
  children,
}: {
  cache: QueryClient;
  i18n: InstanciaDeI18n;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={cache}>
        <ProveedorDeSesion>{children}</ProveedorDeSesion>
      </QueryClientProvider>
    </I18nextProvider>
  );
}
