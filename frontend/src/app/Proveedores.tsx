import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';

import { ProveedorDeSesion } from '@/shared/sesion/ProveedorDeSesion.tsx';

/** Lo global, montado una sola vez: la caché de servidor y la recuperación de la sesión. */
export function Proveedores({
  cache,
  children,
}: {
  cache: QueryClient;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <QueryClientProvider client={cache}>
      <ProveedorDeSesion>{children}</ProveedorDeSesion>
    </QueryClientProvider>
  );
}
