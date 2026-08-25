import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App.tsx';
import './index.css';

const contenedor = document.getElementById('root');
if (!contenedor) {
  throw new Error('No existe el elemento #root: revisa index.html.');
}

// StrictMode siempre activo en desarrollo (`stacks/react/convenciones.md`). Que un efecto
// se ejecute dos veces no es un fallo del modo estricto: es tu efecto sin función de limpieza.
createRoot(contenedor).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
