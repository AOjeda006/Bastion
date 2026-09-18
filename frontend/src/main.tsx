import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App.tsx';
import { crearI18nDelArranque } from './app/i18n/index.ts';
import './index.css';

const contenedor = document.getElementById('root');
if (!contenedor) {
  throw new Error('No existe el elemento #root: revisa index.html.');
}

/**
 * El arranque ESPERA al diccionario del idioma elegido, y por eso es asíncrono desde el ítem 2.1.
 *
 * Lo que se gana es que el diccionario del OTRO idioma —el que esta visita no va a usar— deja de
 * descargarse. Lo que cuesta es esta espera, que es la de un fichero pequeño desde el mismo origen.
 * Pintar antes no era una opción: sin diccionario, la primera pantalla saldría con las claves en
 * crudo y se traduciría un instante después, que se ve peor que tardar ese instante en aparecer.
 */
async function arrancar(raiz: HTMLElement): Promise<void> {
  const i18n = await crearI18nDelArranque();

  // StrictMode siempre activo en desarrollo (`stacks/react/convenciones.md`). Que un efecto
  // se ejecute dos veces no es un fallo del modo estricto: es tu efecto sin función de limpieza.
  createRoot(raiz).render(
    <StrictMode>
      <App i18n={i18n} />
    </StrictMode>,
  );
}

void arrancar(contenedor);
