import type { Diccionario } from './es.ts';
import type { Idioma } from './idioma.ts';

/**
 * De dónde sale el diccionario de cada idioma, y por qué vive en un fichero aparte.
 *
 * **El tipo viaja por `import type` y el valor por `import()`.** No es un adorno de estilo. Hasta el
 * ADR-0036 era, además, lo ÚNICO que sostenía esto: un comentario. Ya no — el paso «Presupuesto de
 * tamaño» exige que exista un fragmento por idioma y se pone rojo si no lo encuentra, así que
 * deshacer esto ahora cuesta un rojo y no 18 kB en silencio. Sigue escrito aquí porque el rojo dice
 * QUÉ pasa y este comentario dice POR QUÉ. Un import mezclado —`import { es, type Diccionario } from
 * './es.ts'`, que es lo que había— deja el módulo ENTERO en el fragmento de entrada en cuanto ese
 * valor se use en algo alcanzable desde ella, y entonces la importación dinámica de abajo no mueve
 * un solo byte: el empaquetador ya lo tiene dentro y se limita a apuntar ahí. Y la forma mezclada no
 * es una casualidad que se escribió una vez: es la que escribe el arreglo automático de
 * `consistent-type-imports` (`fixStyle: 'inline-type-imports'`). Por eso el tipo se importa en su
 * propia línea, con `import type`, que la emisión borra.
 *
 * MEDIDO, no supuesto, y mirando el FRAGMENTO EMITIDO en vez del fuente, donde las dos formas se
 * parecen: devolviendo el valor al import mezclado y usándolo desde `index.ts`, `assets/es-*.js`
 * DESAPARECE —no encoge: deja de existir—, y el fragmento de entrada pasa de 384,8 kB a 403,3 kB.
 * Es decir: se queda exactamente como estaba, con la importación dinámica escrita y sin efecto. Esa
 * misma mutación salía VERDE, con 409 sobre un tope de 450, mientras el presupuesto midió solo lo
 * que `index.html` referencia; hoy es el rojo del que habla el párrafo de arriba.
 *
 * Y el que la escribiría sin querer no es un despistado: `consistent-type-imports` está como
 * `error` con `fixStyle: 'inline-type-imports'`, así que el día que alguien importe un valor de
 * `es.ts` en un módulo donde también se use el tipo, el arreglo automático escribe la forma
 * mezclada.
 *
 * **Los especificadores son literales**, uno por idioma, y no un especificador compuesto. Con una
 * plantilla el empaquetador tiene que adivinar qué ficheros pueden casar y acaba metiendo de más —o
 * avisando y no partiendo nada—. Escritos a mano son dos líneas y no hay nada que adivinar; y el
 * `Record<Idioma, …>` obliga a que estén los dos: añadir un idioma a `IDIOMAS` sin traerlo aquí es
 * un error de compilación, no un fragmento que falta al ejecutar.
 */
const CARGADORES: Record<Idioma, () => Promise<Diccionario>> = {
  es: async () => (await import('./es.ts')).es,
  en: async () => (await import('./en.ts')).en,
};

/**
 * Trae el diccionario de un idioma. La primera vez cuesta una descarga; a partir de ahí el módulo
 * ya está evaluado y la promesa se resuelve sin volver a pedir nada.
 */
export function cargarDiccionario(idioma: Idioma): Promise<Diccionario> {
  return CARGADORES[idioma]();
}
