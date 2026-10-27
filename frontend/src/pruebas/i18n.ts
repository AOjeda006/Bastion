import { catalogo as catalogoEn } from '@/app/i18n/catalogo.en.ts';
import { catalogo as catalogoEs } from '@/app/i18n/catalogo.es.ts';
import { crearI18n } from '@/app/i18n/index.ts';
import { en } from '@/app/i18n/en.ts';
import { es, type Diccionario, type TextosDeLasFuncionalidades } from '@/app/i18n/es.ts';
import { identidad as identidadEn } from '@/app/i18n/identidad.en.ts';
import { identidad as identidadEs } from '@/app/i18n/identidad.es.ts';
import type { Idioma } from '@/app/i18n/idioma.ts';
import { inventario as inventarioEn } from '@/app/i18n/inventario.en.ts';
import { inventario as inventarioEs } from '@/app/i18n/inventario.es.ts';
import { organizacion as organizacionEn } from '@/app/i18n/organizacion.en.ts';
import { organizacion as organizacionEs } from '@/app/i18n/organizacion.es.ts';
import { terceros as tercerosEn } from '@/app/i18n/terceros.en.ts';
import { terceros as tercerosEs } from '@/app/i18n/terceros.es.ts';
import { tiposDeError as tiposEn } from '@/app/i18n/tiposDeError.en.ts';
import { tiposDeError as tiposEs } from '@/app/i18n/tiposDeError.es.ts';

/**
 * El i18n de los tests: **síncrono**, con el diccionario traído por import estático.
 *
 * La aplicación lo trae con `crearI18nDelArranque`, que descarga el del idioma elegido y por tanto
 * espera. Aquí no se quiere esa espera, y no por comodidad: montar pasaría a ser asíncrono, cada
 * montaje abriría un turno más fuera de `act()` y el arnés dejaría de tener CERO avisos, que es la
 * propiedad que hace que el primer aviso signifique algo. Lo que se prueba del arranque asíncrono
 * es otra cosa —que el diccionario está antes de que haya árbol— y tiene sus propios casos.
 *
 * Los dos diccionarios entran por import estático a propósito: un test no se empaqueta, así que
 * aquí no cuesta nada tenerlos los dos, y tenerlos deja el montaje síncrono. Con los textos de los
 * `type` dentro, por lo mismo: en la aplicación llegan después de pintar (ADR-0063 §9), y lo que
 * pasa mientras no han llegado tiene su caso en `ElTextoDeUnFallo`, con su propia instancia.
 *
 * Y con los textos de TODAS las funcionalidades, por lo mismo otra vez: en la aplicación cada una
 * llega con su primera ruta (ADR-0071), y una pantalla montada sola no pasa por el enrutador. Lo
 * que pasa mientras no han llegado tiene sus casos en `LosTextosViajanConSusRutas`, que monta con
 * el diccionario del armazón solo.
 *
 * El idioma va SIEMPRE dicho a mano. Si se tomara el detectado, los tests dependerían del
 * `navigator.language` de la máquina que los corre, y el mismo test pasaría aquí y fallaría en la
 * CI.
 */
const TEXTOS: Record<Idioma, TextosDeLasFuncionalidades> = {
  es: {
    catalogo: catalogoEs,
    identidad: identidadEs,
    inventario: inventarioEs,
    organizacion: organizacionEs,
    terceros: tercerosEs,
  },
  en: {
    catalogo: catalogoEn,
    identidad: identidadEn,
    inventario: inventarioEn,
    organizacion: organizacionEn,
    terceros: tercerosEn,
  },
};

/** El diccionario ENTERO de cada idioma: el del armazón, más los textos de cada funcionalidad. */
export const DICCIONARIOS_ENTEROS: Record<Idioma, Diccionario> = {
  es: { ...es, ...TEXTOS.es },
  en: { ...en, ...TEXTOS.en },
};

/** Los textos de los `type` de cada idioma, los mismos objetos que descarga la aplicación. */
export const TIPOS_DE_ERROR = { es: tiposEs, en: tiposEn };

export function crearI18nDePrueba(idioma: Idioma): ReturnType<typeof crearI18n> {
  return crearI18n(idioma, DICCIONARIOS_ENTEROS[idioma], TIPOS_DE_ERROR[idioma]);
}
