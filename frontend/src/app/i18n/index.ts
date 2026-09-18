import i18next, { type i18n as InstanciaDeI18n } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { cargarDiccionario } from './diccionarios.ts';
import { idiomaInicial, marcarIdiomaDelDocumento, type Idioma } from './idioma.ts';
// El TIPO del diccionario, en su propia línea y con `import type`: la emisión la borra. Junto al
// valor —`import { es, type Diccionario }`— arrastraría `es.ts` entero al fragmento de entrada y
// dejaría sin efecto la carga por idioma. El porqué entero, en `diccionarios.ts`.
import type { Diccionario } from './es.ts';

/** El único espacio de nombres. Se nombra una vez para que no haya dos cadenas que cuadrar. */
const ESPACIO = 'traduccion';

/**
 * El motor de traducción.
 *
 * ESPACIO DE NOMBRES ÚNICO. i18next admite varios y aquí hay uno solo, `traduccion`. Repartir el
 * diccionario en espacios por módulo suena ordenado y trae carga perezosa por partes; el precio es
 * que `t()` deja de comprobarse contra UN tipo y que una clave puede vivir en dos sitios. Lo que se
 * paga por no repartir es UN diccionario, y desde el ítem 2.1 solo el del idioma activo: **18,5 kB**
 * construido el castellano y **18,0 kB** el inglés —49,5 kB de fuente entre los dos—. El precio del
 * reparto por espacios seguiría siendo el mismo de siempre y el ahorro sería una fracción de esos
 * 18; cuando una sola pantalla traiga más texto que todo lo demás junto, volverá a merecer la pena.
 * Hoy no.
 *
 * LICENCIAS, comprobadas antes de adoptarlos (ítem 0.14): `i18next` **MIT** y `react-i18next`
 * **MIT**. Se anotan aquí, junto a la adopción, y no en el mensaje del commit: un dato que solo
 * vive en el historial hay que ir a buscarlo sabiendo ya que existe.
 *
 * SIN `Suspense`, y desde el ítem 2.1 el motivo es otro. Antes los dos diccionarios venían dentro
 * del fragmento de entrada y no había nada que esperar. Ahora el del idioma activo SÍ se descarga
 * —es lo que saca al otro del arranque—, y lo que sostiene el `useSuspense: false` es CUÁNDO se
 * espera: la descarga se hace ANTES de que exista árbol (`crearI18nDelArranque`) y antes de cambiar
 * de idioma (`cambiarIdioma`), nunca durante un renderizado. Así que en el primer renderizado —el
 * del idioma que sí se descarga— el diccionario ya está, y en el cambio la pantalla se queda entera
 * en el idioma viejo hasta que el nuevo está listo. Con `useSuspense` activado, esa misma espera se
 * pagaría en pantalla: un `fallback` en cada cambio, que es el parpadeo que esto evita.
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
 *
 * Recibe el diccionario YA CARGADO, y es síncrona a propósito: así quien la llama decide cuándo se
 * paga la descarga —`crearI18nDelArranque` la paga antes de montar nada— y los tests pueden
 * construir una instancia sin esperar a nada.
 *
 * Se registra **un solo idioma**. El otro entra por `cambiarIdioma` cuando alguien lo pide, que es
 * justo lo que hace que su diccionario no esté en el arranque.
 */
export function crearI18n(idioma: Idioma, diccionario: Diccionario): InstanciaDeI18n {
  const instancia = i18next.createInstance();

  void instancia.use(initReactI18next).init({
    lng: idioma,
    fallbackLng: false,
    defaultNS: ESPACIO,
    ns: [ESPACIO],
    resources: { [idioma]: { [ESPACIO]: diccionario } },
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

/**
 * El i18n con el que arranca la aplicación: se elige el idioma, se descarga SU diccionario y solo
 * entonces hay instancia.
 *
 * Es asíncrona porque el arranque de verdad lo es, y esconderlo detrás de una instancia vacía que
 * se rellena luego sería exactamente el parpadeo que se quiere evitar: la primera pantalla saldría
 * con las claves en crudo y se traduciría un instante después.
 */
export async function crearI18nDelArranque(
  idioma: Idioma = idiomaInicial(),
): Promise<InstanciaDeI18n> {
  return crearI18n(idioma, await cargarDiccionario(idioma));
}

/**
 * Cambia el idioma, trayendo su diccionario si es la primera vez.
 *
 * El orden es la única parte que importa: **primero la descarga, después el cambio**. Al revés
 * —cambiar y que el diccionario llegue luego— la pantalla enseñaría sus claves en crudo mientras
 * tanto. Así, mientras se descarga, lo que se ve sigue siendo la pantalla anterior entera.
 *
 * Volver a un idioma ya traído no descarga nada: `hasResourceBundle` lo corta.
 */
export async function cambiarIdioma(instancia: InstanciaDeI18n, idioma: Idioma): Promise<void> {
  if (!instancia.hasResourceBundle(idioma, ESPACIO)) {
    instancia.addResourceBundle(idioma, ESPACIO, await cargarDiccionario(idioma));
  }

  await instancia.changeLanguage(idioma);
}
