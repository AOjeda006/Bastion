import type { Categoria } from './categoria.ts';

/**
 * De la lista plana al árbol, aquí y no en el servidor.
 *
 * El árbol de una empresa cabe entero en una o dos páginas —diez niveles de profundidad como
 * máximo, y en la práctica dos o tres— y componerlo es esta vuelta por la lista. Servirlo montado
 * obligaría al servidor a recorrerlo entero en cada lectura para devolver justo lo que se puede
 * recomponer sin él.
 */

/** Una fila del árbol ya compuesto: la categoría, a qué altura cuelga y si le falta el padre. */
export interface FilaDelArbol {
  readonly categoria: Categoria;
  /** Cero para una raíz de lo que se está viendo; uno más por cada nivel hacia abajo. */
  readonly profundidad: number;
  /**
   * Si esta fila NO está pintada debajo de su padre.
   *
   * Tiene dos causas y las dos se pintan igual porque para quien mira significan lo mismo —«su
   * sitio en el árbol no se ve desde aquí»—: que su padre se haya quedado en otra página, que es
   * lo normal en cuanto hay más categorías que sitio en una página; o que los datos traigan un
   * ciclo, y entonces la cadena tiene que romperse por algún sitio y esta fila es por donde se ha
   * roto. Lo que NO se hace en ninguno de los dos casos es dejar de pintar la fila: una categoría
   * que existe y no sale es una que alguien da de alta por segunda vez.
   */
  readonly huerfana: boolean;
}

/**
 * Compone el árbol de una página, en el orden en que llega y con los hijos debajo de su padre.
 *
 * <b>Termina siempre</b>, y esa es la mitad interesante. El descenso se lleva un conjunto de ya
 * visitadas, así que cada categoría se emite exactamente una vez: unos datos con un ciclo no dan
 * vueltas, salen como huérfanas. La comprobación de abajo es el equivalente al tope del backend —
 * si alguien quita el conjunto, esto revienta con una frase que dice qué ha pasado en vez de
 * colgar la pestaña, que es un síntoma que no señala a nadie.
 */
export function componerElArbol(categorias: readonly Categoria[]): readonly FilaDelArbol[] {
  const porId = new Map(categorias.map((categoria) => [categoria.id, categoria]));

  const hijosDe = new Map<string, Categoria[]>();
  for (const categoria of categorias) {
    if (categoria.padreId !== null && porId.has(categoria.padreId)) {
      const hermanos = hijosDe.get(categoria.padreId) ?? [];
      hermanos.push(categoria);
      hijosDe.set(categoria.padreId, hermanos);
    }
  }

  const filas: FilaDelArbol[] = [];
  const visitadas = new Set<string>();

  const bajar = (categoria: Categoria, profundidad: number, huerfana: boolean): void => {
    if (visitadas.has(categoria.id)) {
      return;
    }

    if (filas.length >= categorias.length) {
      throw new Error(
        `El árbol de categorías ha emitido ${String(filas.length)} filas para ` +
          `${String(categorias.length)} categorías. Eso solo puede pasar si el descenso ha dejado ` +
          'de llevar cuenta de por dónde ha pasado, y sobre datos con un ciclo eso no es un fallo ' +
          'de más: es una pestaña colgada.',
      );
    }

    visitadas.add(categoria.id);
    filas.push({ categoria, profundidad, huerfana });

    for (const hijo of hijosDe.get(categoria.id) ?? []) {
      bajar(hijo, profundidad + 1, false);
    }
  };

  // Las raíces de verdad primero, en el orden en que vienen —que es el que pidió el servidor—, y
  // solo después lo que se ha quedado sin padre visible. Al revés, una página en la que casi todo
  // es huérfano empezaría por lo que no se entiende.
  for (const categoria of categorias) {
    if (categoria.padreId === null) {
      bajar(categoria, 0, false);
    }
  }

  for (const categoria of categorias) {
    if (!visitadas.has(categoria.id)) {
      bajar(categoria, 0, true);
    }
  }

  return filas;
}
