import i18next, { type i18n as InstanciaDeI18n } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { cargarDiccionario, cargarTiposDeError } from './diccionarios.ts';
import { idiomaInicial, marcarIdiomaDelDocumento, type Idioma } from './idioma.ts';
// El TIPO del diccionario, en su propia línea y con `import type`: la emisión la borra. Junto al
// valor —`import { es, type Diccionario }`— arrastraría `es.ts` entero al fragmento de entrada y
// dejaría sin efecto la carga por idioma. El porqué entero, en `diccionarios.ts`. Lo mismo con los
// textos de los `type`.
import type { Diccionario } from './es.ts';
import type { TiposDeError } from './tiposDeError.es.ts';

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
    resources: { traduccion: DiccionarioConLosTipos };
  }
}

/**
 * El diccionario con los textos de los `type` ya dentro, que es la forma contra la que `t()` se
 * comprueba.
 *
 * Los dos viajan por separado desde el 2.14 (ADR-0063 §9) y se juntan en el MISMO espacio de
 * nombres, en `errores.tipos`, como estaban. No en un espacio propio: así `t('errores.tipos.…')`
 * sigue siendo la misma clave en las cinco pantallas que la escriben, y no hay dos sitios donde
 * buscar un texto. Lo que el espacio propio daría gratis —saber si ya han llegado— lo da
 * `tiposCargados`.
 */
export type DiccionarioConLosTipos = Diccionario & { errores: { tipos: TiposDeError } };

/** Junta el diccionario y los textos de los `type`, sin tocar ninguno de los dos. */
export function conLosTipos(diccionario: Diccionario, tipos: TiposDeError): DiccionarioConLosTipos {
  return { ...diccionario, errores: { ...diccionario.errores, tipos } };
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
 *
 * Los textos de los `type` son opcionales: el arranque no los trae —llegan después, por
 * `completarConLosTipos`— y los tests sí, para que montar siga siendo síncrono.
 */
export function crearI18n(
  idioma: Idioma,
  diccionario: Diccionario,
  tipos?: TiposDeError,
): InstanciaDeI18n {
  const instancia = i18next.createInstance();

  void instancia.use(initReactI18next).init({
    lng: idioma,
    fallbackLng: false,
    defaultNS: ESPACIO,
    ns: [ESPACIO],
    resources: {
      [idioma]: { [ESPACIO]: tipos === undefined ? diccionario : conLosTipos(diccionario, tipos) },
    },
    // `false` porque React ya escapa todo lo que pinta. Dejarlo activado escaparía dos veces, y una
    // razón social con `&` saldría como `&amp;`.
    interpolation: { escapeValue: false },
    // `bindI18nStore: 'added'`: cada componente que traduce se vuelve a pintar cuando entra un
    // texto nuevo. Es lo que hace que los textos de los `type`, que llegan DESPUÉS de pintar, se
    // vean en cuanto llegan: el fallo que se tradujo antes con el genérico pasa a decir el suyo, y
    // el mensaje de un campo, el que le toca. Sin esto, se quedarían con lo de antes hasta el
    // siguiente repintado, que puede no llegar. El precio es un repintado más al cambiar de idioma,
    // el de añadir el diccionario nuevo, que todavía se pinta en el viejo.
    react: { useSuspense: false, bindI18nStore: 'added' },
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
 *
 * Espera al diccionario y NO a los textos de los `type` (ADR-0063 §9): esos son el 68 % de lo que
 * pesaba el fragmento y solo se leen cuando algo falla. Los trae `completarConLosTipos`, que el
 * arranque llama después de montar el árbol.
 */
export async function crearI18nDelArranque(
  idioma: Idioma = idiomaInicial(),
): Promise<InstanciaDeI18n> {
  return crearI18n(idioma, await cargarDiccionario(idioma));
}

/**
 * Si los textos de los `type` de un idioma ya están, que es lo que separa «este `type` no tiene
 * texto» de «todavía no ha llegado». Por omisión, el idioma activo.
 */
export function tiposCargados(instancia: InstanciaDeI18n, idioma = instancia.language): boolean {
  return instancia.getResource(idioma, ESPACIO, 'errores.tipos') !== undefined;
}

/**
 * Los mete en el diccionario de su idioma, con `errores` rehecho entero: los genéricos que ya
 * estaban, más los textos. Ni a lo ancho a secas ni con mezcla profunda, y las dos cosas se
 * midieron:
 *
 * - a lo ancho, `{ errores: { tipos } }` sustituye `errores` entero y se lleva los genéricos, que
 *   son justo los que se dicen mientras los textos no han llegado;
 * - la mezcla profunda escribe DENTRO del objeto que ya está, y el que se registró al crear la
 *   instancia es el propio módulo `es.ts`: i18next guarda por referencia lo que recibe al iniciar.
 *   Los textos acababan en el diccionario importado, y la siguiente instancia que lo usaba los
 *   traía sin haberlos pedido. En los tests, eso dejaba verde un cambio de idioma que no los
 *   descargaba (la mutación 456 del 2.14).
 *
 * `addResourceBundle` copia lo que recibe, así que aquí no se toca ni el módulo ni lo que se pasa.
 */
function anadirLosTipos(instancia: InstanciaDeI18n, idioma: Idioma, tipos: TiposDeError): void {
  const actual = instancia.getResourceBundle(idioma, ESPACIO) as Diccionario;

  instancia.addResourceBundle(
    idioma,
    ESPACIO,
    { errores: { ...actual.errores, tipos } },
    false,
    true,
  );
}

/**
 * Trae los textos de los `type` de un idioma cuyo diccionario ya está, y los mete en él. Es lo que
 * el arranque llama DESPUÉS de montar el árbol (ADR-0063 §9): el arranque espera al diccionario y
 * no a ellos, y hasta que llegan quien traduce un `type` dice el genérico por código de estado.
 *
 * Si el idioma no tiene todavía su diccionario, no hace nada: sus textos entran con él, por
 * `cambiarIdioma`. Meterlos antes dejaría un diccionario con solo `errores.tipos`, que
 * `hasResourceBundle` daría por cargado.
 */
export async function completarConLosTipos(
  instancia: InstanciaDeI18n,
  idioma: Idioma,
): Promise<void> {
  if (!instancia.hasResourceBundle(idioma, ESPACIO) || tiposCargados(instancia, idioma)) {
    return;
  }

  anadirLosTipos(instancia, idioma, await cargarTiposDeError(idioma));
}

/**
 * Cambia el idioma, trayendo su diccionario y los textos de sus `type` si es la primera vez.
 *
 * El orden es la única parte que importa: **primero la descarga, después el cambio**. Al revés
 * —cambiar y que el diccionario llegue luego— la pantalla enseñaría sus claves en crudo mientras
 * tanto. Así, mientras se descarga, lo que se ve sigue siendo la pantalla anterior entera.
 *
 * Aquí los textos de los `type` SÍ se esperan, al contrario que en el arranque: la pantalla ya está
 * pintada en el idioma viejo, así que esperar no retrasa ningún primer pintado, y el idioma nuevo
 * llega entero. Las dos descargas van a la vez.
 *
 * Volver a un idioma ya traído no descarga nada: `hasResourceBundle` y `tiposCargados` lo cortan.
 */
export async function cambiarIdioma(instancia: InstanciaDeI18n, idioma: Idioma): Promise<void> {
  const [diccionario, tipos] = await Promise.all([
    instancia.hasResourceBundle(idioma, ESPACIO) ? null : cargarDiccionario(idioma),
    tiposCargados(instancia, idioma) ? null : cargarTiposDeError(idioma),
  ]);

  if (diccionario !== null) {
    instancia.addResourceBundle(idioma, ESPACIO, diccionario);
  }

  if (tipos !== null) {
    anadirLosTipos(instancia, idioma, tipos);
  }

  await instancia.changeLanguage(idioma);
}
