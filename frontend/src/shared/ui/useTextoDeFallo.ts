import { useTranslation } from 'react-i18next';

import { es } from '@/app/i18n/es.ts';
import { motivoDeFallo, tipoDeFallo, trazaDeFallo } from '@/shared/api/errores.ts';

/** Un código de error de los que TIENEN texto escrito, ya estrechado a las claves del diccionario. */
type CodigoConTexto = keyof typeof es.errores.tipos;

/**
 * Si un código trae texto, y —de paso— estrechado al tipo que `t()` acepta.
 *
 * <b>Se pregunta al diccionario y no a `i18n.exists()`</b>, aunque lo segundo parezca más
 * dinámico. Dos razones, y la segunda es la que decide:
 *
 *   · `es` es LA FORMA: el tipo `Diccionario` es `typeof es`, y `en.ts` no compila si no la
 *     cumple. Así que «tiene texto en castellano» equivale a «tiene texto en todos los idiomas»,
 *     y lo garantiza el compilador, no una comprobación en ejecución.
 *   · `t()` está tipado contra las claves literales del diccionario. Una clave compuesta a mano
 *     no es asignable, y la salida sería un `as` — o sea, apagar al compilador justo en el sitio
 *     donde decide qué lee una persona. Con esta guarda de tipo, la clave se estrecha sola.
 */
function tieneTexto(codigo: string): codigo is CodigoConTexto {
  return Object.hasOwn(es.errores.tipos, codigo);
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
  const { t } = useTranslation();

  return (error: unknown): string => {
    const tipo = tipoDeFallo(error);

    if (tipo === null) {
      return t(`errores.${motivoDeFallo(error)}`);
    }

    if (tieneTexto(tipo)) {
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
