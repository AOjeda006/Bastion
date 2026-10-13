import type { i18n as InstanciaDeI18n } from 'i18next';
import { useTranslation } from 'react-i18next';

// Por su TIPO, y en su propia línea: un import de valor ataría el fragmento de los textos a cada
// página que traduce un fallo, que es lo que hacía este fichero con `es.ts` hasta el 2.14
// (ADR-0063 §9). El *linter* lo prohíbe.
import type { TiposDeError } from '@/app/i18n/tiposDeError.es.ts';
import { motivoDeFallo, tipoDeFallo, trazaDeFallo } from '@/shared/api/errores.ts';

/** Un código de error de los que TIENEN texto escrito, ya estrechado a las claves del diccionario. */
type CodigoConTexto = keyof TiposDeError;

/**
 * Si un código trae texto, y —de paso— estrechado al tipo que `t()` acepta.
 *
 * <b>Se pregunta a `i18n.exists()` desde el 2.14</b>, y antes se preguntaba al diccionario
 * castellano importado por su valor. Lo que aquel import compraba —que «tiene texto en castellano»
 * equivale a «tiene texto en todos los idiomas»— lo sigue garantizando el compilador:
 * `tiposDeError.en.ts` no compila si no cumple la forma del castellano. Lo que costaba era el
 * diccionario entero dentro de once páginas. Y `exists` pregunta además lo que el import no podía
 * saber: si los textos han llegado ya.
 *
 * La guarda de tipo se queda por lo mismo que antes: `t()` está tipado contra las claves
 * literales, una clave compuesta a mano no es asignable, y la salida sería un `as` — apagar al
 * compilador justo donde decide qué lee una persona.
 */
function tieneTexto(i18n: InstanciaDeI18n, codigo: string): codigo is CodigoConTexto {
  return i18n.exists(`errores.tipos.${codigo}`);
}

/**
 * La frase que se le enseña a una persona cuando una llamada a la API ha fallado.
 *
 * ES LA PIEZA (c) DEL ADR-0030. El backend no manda texto para leer: manda un `type` estable, y
 * el texto lo escribe este lado, en el idioma en el que se esté. Aquí se decide cuál:
 *
 *   1. Si la respuesta traía un `type` y **hay texto para él**, ese. Es el caso normal, y el que
 *      hace que un error de negocio se lea como una instrucción («ya hay un almacén con ese
 *      código») en vez de como una categoría («conflicto»).
 *   2. Si traía un `type` y **no hay texto**, la frase genérica CON EL IDENTIFICADOR DE TRAZA. No
 *      la genérica a secas: un `type` desconocido significa que el backend ha estrenado un código
 *      que este frontal no conoce, o sea que las dos partes se han desincronizado. La traza es lo
 *      único que convierte «no ha funcionado» en algo que alguien puede buscar en el registro — y
 *      es el MISMO valor que Serilog escribe como `@tr`.
 *   3. Si no traía `type` —un fallo de red, un 502 de un intermediario, una respuesta sin
 *      cuerpo—, el motivo de siempre por código de estado. No hay nada que mapear.
 *   4. Si traía un `type` y **los textos todavía no han llegado** (ADR-0063 §9), también el motivo
 *      por código de estado, y SIN la traza: el `type` no es desconocido, solo está en camino, y
 *      decir otra cosa mandaría a alguien a buscar en el registro un fallo que no existe. Cuando
 *      llegan, el componente se repinta (`bindI18nStore`) y pasa al caso 1.
 *
 * <b>Por qué el caso 2 no es teórico y por qué no basta con el barrido.</b> El barrido de
 * diccionarios de `ElCambioDeIdioma` compara el artefacto contra los textos y se pone rojo en
 * cuanto falta uno, así que en el repositorio el caso 2 no debería existir nunca. Pero el frontal
 * desplegado y la API desplegada son dos artefactos que se despliegan por separado: durante un
 * despliegue escalonado, o con una pestaña abierta desde antes, la versión vieja del frontal
 * recibe códigos de la versión nueva de la API. Eso no se arregla con una regla; se aguanta con
 * elegancia, que es lo que hace el caso 2.
 */
export function useTextoDeFallo(): (error: unknown) => string {
  const { t, i18n } = useTranslation();

  return (error: unknown): string => {
    const tipo = tipoDeFallo(error);

    if (tipo === null || !i18n.exists('errores.tipos')) {
      return t(`errores.${motivoDeFallo(error)}`);
    }

    if (tieneTexto(i18n, tipo)) {
      return t(`errores.tipos.${tipo}`);
    }

    // La traza puede no venir —una respuesta con `type` pero sin `traceId` es rara pero posible—,
    // y entonces se dice el genérico de siempre en vez de enseñar un hueco donde iba la
    // referencia.
    const traza = trazaDeFallo(error);

    return traza === null
      ? t(`errores.${motivoDeFallo(error)}`)
      : t('errores.desconocido', { traza });
  };
}
