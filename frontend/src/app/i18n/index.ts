import i18next, { type i18n as InstanciaDeI18n } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { cargarDiccionario, cargarTextos, cargarTiposDeError } from './diccionarios.ts';
import { idiomaInicial, marcarIdiomaDelDocumento, type Idioma } from './idioma.ts';
// El TIPO del diccionario, en su propia línea y con `import type`: la emisión la borra. Junto al
// valor —`import { es, type Diccionario }`— arrastraría `es.ts` entero al fragmento de entrada y
// dejaría sin efecto la carga por idioma. El porqué entero, en `diccionarios.ts`. Lo mismo con los
// textos de los `type` y con los de cada funcionalidad.
import type { Diccionario, DiccionarioDelArmazon, TextosDeLasFuncionalidades } from './es.ts';
import type { TiposDeError } from './tiposDeError.es.ts';
import { FUNCIONALIDADES, type Funcionalidad } from '@/features/funcionalidades.ts';

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
 * Desde el 3.1 el diccionario SÍ llega por partes (ADR-0071), y sigue siendo un espacio: el del
 * armazón con el arranque, y los de cada funcionalidad con su primera ruta (`cargarTextosDe`). Lo
 * que se partió es la DESCARGA, no el tipo: todas las partes entran en `traduccion`, cada una en
 * su espacio de primer nivel, y `t()` se sigue comprobando contra el `Diccionario` entero. Así el
 * arranque deja de crecer con cada documento, que era lo que costaba no partir, y lo que costaba
 * repartir en espacios no se paga.
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

/**
 * Junta el diccionario y los textos de los `type`, sin tocar ninguno de los dos. Vale para el del
 * armazón y para el entero, y devuelve el mismo que recibe, con los textos dentro.
 */
export function conLosTipos<D extends DiccionarioDelArmazon>(
  diccionario: D,
  tipos: TiposDeError,
): D & { errores: { tipos: TiposDeError } } {
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
 * `completarConLosTipos`— y los tests sí, para que montar siga siendo síncrono. Y el diccionario
 * puede ser el del armazón, que es el del arranque, o el entero, con los textos de todas las
 * funcionalidades dentro, que es el de los tests, por lo mismo.
 */
export function crearI18n(
  idioma: Idioma,
  diccionario: DiccionarioDelArmazon,
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
  const actual = instancia.getResourceBundle(idioma, ESPACIO) as DiccionarioDelArmazon;

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

/** Si los textos de una funcionalidad ya están en un idioma. Por omisión, el activo. */
export function textosCargados(
  instancia: InstanciaDeI18n,
  funcionalidad: Funcionalidad,
  idioma = instancia.language,
): boolean {
  return instancia.getResource(idioma, ESPACIO, funcionalidad) !== undefined;
}

/** Las funcionalidades cuyos textos ya están en un idioma: las que alguna ruta ha pedido. */
function funcionalidadesCargadas(instancia: InstanciaDeI18n, idioma: string): Funcionalidad[] {
  return FUNCIONALIDADES.filter((funcionalidad) =>
    textosCargados(instancia, funcionalidad, idioma),
  );
}

/** Unos textos traídos, con la funcionalidad de la que son. */
type TextosTraidos = readonly [Funcionalidad, TextosDeLasFuncionalidades[Funcionalidad]];

/** Descarga, a la vez, los textos de las funcionalidades que falten en un idioma. No mete nada. */
async function descargarLosTextos(
  instancia: InstanciaDeI18n,
  idioma: Idioma,
  funcionalidades: readonly Funcionalidad[],
): Promise<TextosTraidos[]> {
  return Promise.all(
    funcionalidades
      .filter((funcionalidad) => !textosCargados(instancia, funcionalidad, idioma))
      .map(
        async (funcionalidad) =>
          [funcionalidad, await cargarTextos(funcionalidad, idioma)] as const,
      ),
  );
}

/**
 * Los mete en el diccionario de su idioma, A LO ANCHO: lo que había, más el espacio de la
 * funcionalidad. Aquí lo ancho es lo correcto, al revés que con los `type`, porque ese espacio es
 * de primer nivel y entero suyo: no hay nada dentro que pisar. Y la mezcla profunda escribiría
 * dentro del objeto que se registró al crear la instancia, que es el módulo `es.ts`
 * (`anadirLosTipos` cuenta qué pasaba). `addResourceBundle` copia lo que recibe.
 *
 * Solo en un idioma cuyo diccionario ya está, por lo mismo que `completarConLosTipos`: con solo
 * los textos, `hasResourceBundle` daría el idioma por cargado. Los dos que llaman lo cumplen: el
 * enrutador, en el idioma activo, y `cambiarIdioma`, después de meter el diccionario.
 */
function anadirLosTextos(
  instancia: InstanciaDeI18n,
  idioma: Idioma,
  traidos: TextosTraidos[],
): void {
  for (const [funcionalidad, textos] of traidos) {
    instancia.addResourceBundle(idioma, ESPACIO, { [funcionalidad]: textos }, false, true);
  }
}

/**
 * Los textos de la funcionalidad de una ruta, en el idioma activo, antes de pintarla (ADR-0071).
 * Lo llama el enrutador a la vez que carga la pantalla, así que la primera visita espera a las dos
 * descargas juntas, y las siguientes no esperan nada.
 *
 * **Se repite si el idioma cambia mientras viajan.** Lo traído sería del idioma de antes, y la
 * pantalla se pintaría en el nuevo sin sus textos: las claves en crudo. `cambiarIdioma` no puede
 * verlos, porque cuando mira todavía no han llegado.
 */
export async function cargarTextosDe(
  instancia: InstanciaDeI18n,
  funcionalidad: Funcionalidad,
): Promise<void> {
  let idioma: Idioma;

  do {
    idioma = instancia.language as Idioma;
    anadirLosTextos(
      instancia,
      idioma,
      await descargarLosTextos(instancia, idioma, [funcionalidad]),
    );
  } while (instancia.language !== idioma);
}

/**
 * Cambia el idioma, trayendo su diccionario, los textos de sus `type` y los de las funcionalidades
 * que ya se han visitado, si es la primera vez.
 *
 * El orden es la única parte que importa: **primero la descarga, después el cambio**. Al revés
 * —cambiar y que el diccionario llegue luego— la pantalla enseñaría sus claves en crudo mientras
 * tanto. Así, mientras se descarga, lo que se ve sigue siendo la pantalla anterior entera.
 *
 * Aquí los textos de los `type` SÍ se esperan, al contrario que en el arranque: la pantalla ya está
 * pintada en el idioma viejo, así que esperar no retrasa ningún primer pintado, y el idioma nuevo
 * llega entero. Las descargas van a la vez.
 *
 * **Las funcionalidades se vuelven a mirar justo antes del cambio**, y hasta que no falte ninguna
 * (ADR-0071). Una ruta que se abrió mientras se descargaba lo demás ha metido sus textos en el
 * idioma viejo, y sin esta vuelta la pantalla pasaría al nuevo sin ellos. Entre la última mirada y
 * el `changeLanguage` no hay ninguna espera, y con los recursos ya dados i18next cambia el idioma
 * sin ninguna, así que no queda hueco por el que se cuele otra.
 *
 * Volver a un idioma ya traído no descarga nada: `hasResourceBundle`, `tiposCargados` y
 * `textosCargados` lo cortan.
 */
export async function cambiarIdioma(instancia: InstanciaDeI18n, idioma: Idioma): Promise<void> {
  const [diccionario, tipos, textos] = await Promise.all([
    instancia.hasResourceBundle(idioma, ESPACIO) ? null : cargarDiccionario(idioma),
    tiposCargados(instancia, idioma) ? null : cargarTiposDeError(idioma),
    descargarLosTextos(instancia, idioma, funcionalidadesCargadas(instancia, instancia.language)),
  ]);

  if (diccionario !== null) {
    instancia.addResourceBundle(idioma, ESPACIO, diccionario);
  }

  if (tipos !== null) {
    anadirLosTipos(instancia, idioma, tipos);
  }

  anadirLosTextos(instancia, idioma, textos);

  const pendientes = (): Funcionalidad[] =>
    funcionalidadesCargadas(instancia, instancia.language).filter(
      (funcionalidad) => !textosCargados(instancia, funcionalidad, idioma),
    );

  for (let faltan = pendientes(); faltan.length > 0; faltan = pendientes()) {
    anadirLosTextos(instancia, idioma, await descargarLosTextos(instancia, idioma, faltan));
  }

  await instancia.changeLanguage(idioma);
}
