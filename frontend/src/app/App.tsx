import { RouterProvider, createBrowserRouter } from 'react-router';

import { Proveedores } from './Proveedores.tsx';
import { crearCache } from './cache.ts';
import { crearRutas } from './enrutador.tsx';

const cache = crearCache();
const enrutador = createBrowserRouter(crearRutas());

/** Raíz de la aplicación: proveedores fuera, enrutador dentro. */
export function App(): React.JSX.Element {
  return (
    <Proveedores cache={cache}>
      <RouterProvider router={enrutador} />
    </Proveedores>
  );
}
