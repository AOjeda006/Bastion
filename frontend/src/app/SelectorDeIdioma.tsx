import { useTranslation } from 'react-i18next';

import { cambiarIdioma } from './i18n/index.ts';
import { IDIOMAS, recordarIdioma, type Idioma } from './i18n/idioma.ts';

/** Cómo se llama cada idioma **en ese idioma**: quien busca «English» no sabe buscar «Inglés». */
const NOMBRES: Record<Idioma, string> = {
  es: 'Español',
  en: 'English',
};

/**
 * El cambio de idioma.
 *
 * `<select>` nativo con su `<label>`, por lo mismo que el selector de empresa: `stacks/react` manda
 * elemento nativo antes que ARIA, y aquí no hay nada que un `<select>` no haga ya bien.
 *
 * Cambiar el idioma **no** toca la caché de consultas, y esa es la diferencia con el selector de
 * empresa: allí cambian las FILAS que se pueden ver y quedarse con las viejas es una fuga entre
 * inquilinos; aquí cambia el idioma de los rótulos, que no viven en la caché. Los datos del
 * servidor son los mismos en los dos idiomas.
 */
export function SelectorDeIdioma(): React.JSX.Element {
  const { t, i18n } = useTranslation();

  return (
    <div className="flex items-center gap-2">
      <label htmlFor="idioma" className="sr-only">
        {t('comun.idioma')}
      </label>
      <select
        id="idioma"
        value={i18n.language}
        onChange={(evento) => {
          const elegido = evento.target.value as Idioma;

          // La elección se recuerda YA, antes de la descarga y no después: es del usuario en el
          // momento en que la expresa, y un diccionario que no llegue no tiene por qué borrarla.
          recordarIdioma(elegido);

          // Y el cambio es asíncrono desde el ítem 2.1: `cambiarIdioma` trae el diccionario y solo
          // entonces cambia. Mientras llega, esta pantalla sigue entera en el idioma anterior.
          void cambiarIdioma(i18n, elegido);
        }}
        className="rounded border border-neutral-300 px-2 py-1 text-sm"
      >
        {IDIOMAS.map((idioma) => (
          <option key={idioma} value={idioma}>
            {NOMBRES[idioma]}
          </option>
        ))}
      </select>
    </div>
  );
}
