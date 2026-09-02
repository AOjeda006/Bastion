import i18next, { type i18n as InstanciaDeI18n } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { en } from './en.ts';
import { es, type Diccionario } from './es.ts';
import { idiomaInicial, marcarIdiomaDelDocumento, type Idioma } from './idioma.ts';

/**
 * El motor de traducción.
 *
 * ESPACIO DE NOMBRES ÚNICO. i18next admite varios y aquí hay uno solo, `traduccion`. Repartir el
 * diccionario en espacios por módulo suena ordenado y trae carga perezosa por partes; el precio es
 * que `t()` deja de comprobarse contra UN tipo y que una clave puede vivir en dos sitios. El
 * diccionario entero pesa unos pocos kilobytes: no hay nada que repartir todavía.
 *
 * SIN `Suspense`. Los diccionarios se importan, no se descargan: al primer renderizado ya están.
 * Dejar `useSuspense` activado montaría una espera para algo que nunca espera, y esa espera sí se
 * nota — es un parpadeo en cada pantalla.
 */
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'traduccion';
    resources: { traduccion: Diccionario };
  }
}

/**
 * Una instancia por arranque, igual que la caché de consultas.
 *
 * Es una FÁBRICA y no la instancia global de `i18next` por el mismo motivo que `crearCache`: un
 * test que cambia el idioma no puede dejárselo cambiado al siguiente. La instancia global es
 * estado compartido entre tests, y eso es un intermitente esperando a que dos se ejecuten en
 * cierto orden.
 */
export function crearI18n(idioma: Idioma = idiomaInicial()): InstanciaDeI18n {
  const instancia = i18next.createInstance();

  void instancia.use(initReactI18next).init({
    lng: idioma,
    fallbackLng: false,
    defaultNS: 'traduccion',
    ns: ['traduccion'],
    resources: {
      es: { traduccion: es },
      en: { traduccion: en },
    },
    // `false` porque React ya escapa todo lo que pinta. Dejarlo activado escaparía dos veces, y una
    // razón social con `&` saldría como `&amp;`.
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

  marcarIdiomaDelDocumento(idioma);

  // El `lang` del documento se mantiene al día desde AQUÍ y no desde el componente que cambia el
  // idioma: así sigue siendo cierto aunque el idioma se cambie desde otro sitio. Un invariante
  // que depende de que alguien se acuerde de llamarlo no es un invariante.
  instancia.on('languageChanged', (nuevo) => {
    marcarIdiomaDelDocumento(nuevo as Idioma);
  });

  return instancia;
}
