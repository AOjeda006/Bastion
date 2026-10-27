import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App.tsx';
import { crearEnrutador } from './app/enrutador.tsx';
import { completarConLosTipos, crearI18nDelArranque } from './app/i18n/index.ts';
import { idiomaInicial } from './app/i18n/idioma.ts';
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
 *
 * Los textos de los `type` de error, en cambio, NO se esperan (ADR-0063 §9): se piden después de
 * pintar. Hasta que llegan, un fallo se dice con el genérico por código de estado, y en cuanto
 * llegan se repinta con el suyo. Si no llegan —la red se cae justo ahí—, se queda el genérico, que
 * es exactamente lo que pasaba antes con un `type` sin texto; no hay nada más que hacer con el
 * rechazo, y dejarlo suelto solo ensuciaría la consola con algo que la persona ya ve.
 *
 * El enrutador se crea con la PROMESA del i18n, antes de esperarla: empieza a cargar la pantalla
 * de la URL en cuanto existe, así que la pantalla viaja a la vez que el diccionario. Los textos de
 * su funcionalidad (ADR-0071) esperan a la instancia, y la pantalla, a sus textos.
 */
async function arrancar(raiz: HTMLElement): Promise<void> {
  const idioma = idiomaInicial();
  const i18nDelArranque = crearI18nDelArranque(idioma);
  const enrutador = crearEnrutador(i18nDelArranque);
  const i18n = await i18nDelArranque;

  // StrictMode siempre activo en desarrollo (`stacks/react/convenciones.md`). Que un efecto
  // se ejecute dos veces no es un fallo del modo estricto: es tu efecto sin función de limpieza.
  createRoot(raiz).render(
    <StrictMode>
      <App i18n={i18n} enrutador={enrutador} />
    </StrictMode>,
  );

  completarConLosTipos(i18n, idioma).catch(() => undefined);
}

void arrancar(contenedor);
