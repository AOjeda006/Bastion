import { RouterProvider, createBrowserRouter } from 'react-router';

import { Proveedores } from './Proveedores.tsx';
import { crearCache } from './cache.ts';
import { crearRutas } from './enrutador.tsx';
import { crearI18n } from './i18n/index.ts';

const cache = crearCache();
const i18n = crearI18n();
const enrutador = createBrowserRouter(crearRutas());

/** Raíz de la aplicación: proveedores fuera, enrutador dentro. */
export function App(): React.JSX.Element {
  return (
    <Proveedores cache={cache} i18n={i18n}>
      <RouterProvider router={enrutador} />
    </Proveedores>
  );
}
