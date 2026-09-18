import { RouterProvider, createBrowserRouter } from 'react-router';
import type { i18n as InstanciaDeI18n } from 'i18next';

import { Proveedores } from './Proveedores.tsx';
import { crearCache } from './cache.ts';
import { crearRutas } from './enrutador.tsx';

const cache = crearCache();
const enrutador = createBrowserRouter(crearRutas());

/**
 * Raíz de la aplicación: proveedores fuera, enrutador dentro.
 *
 * El `i18n` entra por parámetro y no se crea aquí porque crearlo es asíncrono desde el ítem 2.1
 * —hay que descargar el diccionario del idioma elegido—, y un componente no espera: lo hace
 * `main.tsx` antes de montar nada.
 */
export function App({ i18n }: { i18n: InstanciaDeI18n }): React.JSX.Element {
  return (
    <Proveedores cache={cache} i18n={i18n}>
      <RouterProvider router={enrutador} />
    </Proveedores>
  );
}
