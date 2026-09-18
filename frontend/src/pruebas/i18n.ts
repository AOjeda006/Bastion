import { crearI18n } from '@/app/i18n/index.ts';
import { en } from '@/app/i18n/en.ts';
import { es } from '@/app/i18n/es.ts';
import type { Idioma } from '@/app/i18n/idioma.ts';

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
 * aquí no cuesta nada tenerlos los dos, y tenerlos deja el montaje síncrono.
 *
 * El idioma va SIEMPRE dicho a mano. Si se tomara el detectado, los tests dependerían del
 * `navigator.language` de la máquina que los corre, y el mismo test pasaría aquí y fallaría en la
 * CI.
 */
const DICCIONARIOS = { es, en };

export function crearI18nDePrueba(idioma: Idioma): ReturnType<typeof crearI18n> {
  return crearI18n(idioma, DICCIONARIOS[idioma]);
}
