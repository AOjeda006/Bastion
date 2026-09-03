import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, relative, resolve, sep } from 'node:path';

import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

import { FUNCIONALIDADES } from './funcionalidades.ts';
import { en } from '@/app/i18n/en.ts';
import { es } from '@/app/i18n/es.ts';

/**
 * EL SEXTO BARRIDO — una funcionalidad no importa de otra, y el disco manda.
 *
 * La regla la aplica ESLint (`eslint.config.js`), que es donde tiene que estar: falla mientras se
 * escribe, no media hora después en la CI. Lo que este barrido añade es lo que ESLint no sabe
 * decir de sí mismo.
 *
 * **Una regla de ESLint cuyo patrón no case con nada PASA.** Un glob mal escrito, una carpeta
 * renombrada, un descubrimiento que devuelve cero — y la regla sigue ahí, se lee perfectamente y no
 * prohíbe nada. Es la misma trampa que los barridos del backend persiguen desde el 0.7: la
 * comprobación que mira cero elementos y sale verde. Contra eso hacen falta dos cosas, y aquí están
 * las dos:
 *
 * 1. **Que alguien AFIRME cuántas hay.** `funcionalidades.ts` lo dice a mano; aquí se compara esa
 *    lista entera contra `src/features/`, en los dos sentidos.
 * 2. **Que la regla se pruebe por el efecto.** No se lee la configuración: se le pide a ESLint que
 *    linte un import prohibido entre CADA PAR de funcionalidades y se exige que lo marque. Si el
 *    patrón se rompe, este test se pone rojo aunque no haya cambiado ni un import del proyecto.
 *
 * Y una tercera, que cubre el punto ciego de la regla: `no-restricted-imports` mira los `import`
 * estáticos, no los dinámicos. El barrido de abajo lee TODOS los especificadores escritos bajo
 * `src/features/` —estáticos, de tipo y dinámicos— y los resuelve.
 */

/**
 * La raíz del frontal, y **no** sale de `import.meta.url`: los tests corren en el entorno de jsdom,
 * donde `import.meta.url` no es una URL de fichero y `fileURLToPath` revienta. Sale del directorio
 * de trabajo, que vitest fija en la raíz del proyecto, y el primer test comprueba que ahí están de
 * verdad `eslint.config.js` y `src/features/` — un directorio equivocado dejaría todo lo de abajo
 * mirando cero ficheros, que es exactamente el fallo que este barrido persigue.
 */
const RAIZ = process.cwd();
const FUNCIONALIDADES_EN_DISCO = join(RAIZ, 'src', 'features');

/** Los espacios de nombres del diccionario que NO son de ninguna funcionalidad. */
const ESPACIOS_DEL_ARMAZON = ['comun', 'paginacion', 'rutas', 'sesion', 'errores', 'inicio'];

const DICCIONARIOS = { es, en };

function carpetasDe(directorio: string): string[] {
  return readdirSync(directorio, { withFileTypes: true })
    .filter((entrada) => entrada.isDirectory())
    .map((entrada) => entrada.name)
    .sort();
}

function ficherosDe(directorio: string): string[] {
  return readdirSync(directorio, { withFileTypes: true })
    .flatMap((entrada) => {
      const camino = join(directorio, entrada.name);

      if (entrada.isDirectory()) {
        return ficherosDe(camino);
      }

      return /\.tsx?$/.test(entrada.name) ? [camino] : [];
    })
    .sort();
}

/** A qué funcionalidad pertenece un fichero, o `null` si no está dentro de ninguna. */
function funcionalidadDe(rutaAbsoluta: string): string | null {
  const dentro = relative(FUNCIONALIDADES_EN_DISCO, rutaAbsoluta);

  if (dentro.startsWith('..')) {
    return null;
  }

  const [primera] = dentro.split(sep);

  return primera !== undefined && primera !== '' && primera.includes('.')
    ? null
    : (primera ?? null);
}

/**
 * Todo lo que un fichero importa: `from '…'`, `import '…'` y `import('…')` en la misma pasada.
 *
 * Es una expresión regular sobre el texto y no un árbol de sintaxis a propósito: lo que se persigue
 * es un especificador ESCRITO, y una regex no puede olvidarse de una forma de import que el
 * recorrido del árbol no contemplara. Que también pesque alguno dentro de un comentario solo la
 * hace más estricta, nunca más laxa.
 */
function especificadoresDe(fichero: string): string[] {
  const texto = readFileSync(fichero, 'utf8');
  const encontrados: string[] = [];

  for (const coincidencia of texto.matchAll(/(?:from|import)\s*\(?\s*['"]([^'"]+)['"]/g)) {
    const especificador = coincidencia[1];

    if (especificador !== undefined) {
      encontrados.push(especificador);
    }
  }

  return encontrados;
}

/** Dónde apunta un especificador, si apunta a un fichero nuestro. Un paquete de npm da `null`. */
function destinoDe(fichero: string, especificador: string): string | null {
  if (especificador.startsWith('@/')) {
    return resolve(RAIZ, 'src', especificador.slice('@/'.length));
  }

  if (especificador.startsWith('.')) {
    return resolve(dirname(fichero), especificador);
  }

  return null;
}

describe('El barrido de las fronteras', () => {
  const enDisco = carpetasDe(FUNCIONALIDADES_EN_DISCO);

  it('la lista declarada y las carpetas del disco son la misma, entera y en los dos sentidos', () => {
    // Antes que nada, que el sitio sea el sitio: si el directorio de trabajo no fuera la raíz del
    // frontal, `readdirSync` habría reventado o —peor— habría leído otra cosa, y el resto de los
    // tests estaría comparando listas vacías entre sí.
    expect(existsSync(join(RAIZ, 'eslint.config.js')), `${RAIZ} no es la raíz del frontal`).toBe(
      true,
    );

    // Dos funcionalidades es el mínimo para que exista una frontera. Con menos, la regla de ESLint
    // no generaría ningún patrón y el lint saldría verde sin prohibir nada; `eslint.config.js`
    // revienta en ese caso, y esto lo dice también desde este lado.
    expect(enDisco.length, 'src/features/ no tiene al menos dos funcionalidades').toBeGreaterThan(
      1,
    );

    expect(
      enDisco,
      'La lista de src/features/funcionalidades.ts no coincide con las carpetas que hay. Una ' +
        'carpeta nueva sin declarar no está vallada por la regla de fronteras; una declaración ' +
        'sin carpeta es una regla que no mira nada.',
    ).toEqual([...FUNCIONALIDADES].sort());
  });

  it('ESLint prohíbe de verdad cada par, escrito con alias y escrito con camino relativo', async () => {
    const eslint = new ESLint({ cwd: RAIZ });

    async function restringidos(codigo: string, fichero: string): Promise<string[]> {
      const [resultado] = await eslint.lintText(codigo, { filePath: fichero });

      expect(resultado?.messages.filter((aviso) => aviso.fatal === true) ?? []).toEqual([]);

      return (resultado?.messages ?? [])
        .filter((aviso) => aviso.ruleId === 'no-restricted-imports')
        .map((aviso) => aviso.message);
    }

    let paresComprobados = 0;

    for (const funcionalidad of enDisco) {
      // Un fichero DE VERDAD de la funcionalidad: así el linteo pasa por el mismo proyecto de
      // TypeScript que el lint normal, y la ruta con la que se compara el patrón es la real.
      const [testigo] = ficherosDe(join(FUNCIONALIDADES_EN_DISCO, funcionalidad));

      expect(
        testigo,
        `${funcionalidad} no tiene ni un fichero .ts con el que probar`,
      ).toBeDefined();

      for (const otra of enDisco.filter((cual) => cual !== funcionalidad)) {
        const conAlias = `import '@/features/${otra}/loQueSea.ts';\n`;
        const subiendo = relative(dirname(testigo!), join(FUNCIONALIDADES_EN_DISCO, otra))
          .split(sep)
          .join('/');
        const conRelativo = `import '${subiendo}/loQueSea.ts';\n`;

        expect(
          await restringidos(conAlias, testigo!),
          `${funcionalidad} puede importar de ${otra} con el alias, y no debería`,
        ).not.toEqual([]);

        expect(
          await restringidos(conRelativo, testigo!),
          `${funcionalidad} puede importar de ${otra} subiendo por caminos relativos ` +
            `('${conRelativo.trim()}'), y no debería`,
        ).not.toEqual([]);

        paresComprobados += 1;
      }

      // La otra mitad de la regla, y la que se rompe cuando alguien la escribe demasiado ancha:
      // la frontera va de funcionalidad a funcionalidad. `shared/` es de todos —ahí vive la
      // sesión, que es lo que dice con qué empresa se opera— y del armazón se puede leer el tipo
      // del diccionario. Si prohibir lo de al lado obligara a bajar la sesión dentro de una
      // funcionalidad, la regla estaría mal, no la estructura.
      for (const permitido of [
        `import '@/shared/sesion/sesion.ts';\n`,
        `import '@/app/i18n/es.ts';\n`,
        `import '@/features/${funcionalidad}/loQueSea.ts';\n`,
      ]) {
        expect(
          await restringidos(permitido, testigo!),
          `${funcionalidad} no puede escribir ${permitido.trim()}, y sí debería`,
        ).toEqual([]);
      }
    }

    // El conjunto no vacío, contado: con dos funcionalidades son dos pares ordenados. Si el bucle
    // se quedara sin qué recorrer, todo lo de arriba sería verde sin haber comprobado nada.
    expect(paresComprobados, 'no se ha comprobado ningún par de funcionalidades').toBe(
      enDisco.length * (enDisco.length - 1),
    );
  }, 60_000);

  it('ningún fichero de una funcionalidad importa de otra, ni siquiera dinámicamente', () => {
    const ficheros = enDisco.flatMap((funcionalidad) =>
      ficherosDe(join(FUNCIONALIDADES_EN_DISCO, funcionalidad)),
    );

    let especificadoresMirados = 0;
    const cruces: string[] = [];

    for (const fichero of ficheros) {
      const suya = funcionalidadDe(fichero);

      for (const especificador of especificadoresDe(fichero)) {
        const destino = destinoDe(fichero, especificador);

        if (destino === null) {
          continue;
        }

        especificadoresMirados += 1;
        const ajena = funcionalidadDe(destino);

        if (ajena !== null && ajena !== suya) {
          cruces.push(`${relative(RAIZ, fichero)} → ${especificador} (${ajena})`);
        }
      }
    }

    expect(ficheros.length, 'no hay ningún fichero dentro de las funcionalidades').toBeGreaterThan(
      0,
    );
    expect(
      especificadoresMirados,
      'ningún import de las funcionalidades apunta a código nuestro: el barrido no está mirando nada',
    ).toBeGreaterThan(0);

    expect(cruces, 'una funcionalidad importa de otra').toEqual([]);
  });

  it('ningún nombre de funcionalidad choca con una carpeta de shared/ o de app/', () => {
    // El patrón ancho de la regla («que el nombre aparezca como carpeta, a cualquier profundidad»)
    // es lo que atrapa los caminos relativos que suben y vuelven a bajar. El precio es que una
    // carpeta de `shared/` o de `app/` que se llamara igual que una funcionalidad quedaría
    // prohibida sin querer, y el diagnóstico sería incomprensible. Aquí eso deja de ser una
    // posibilidad silenciosa y pasa a ser un test rojo.
    function carpetasRecursivas(directorio: string): string[] {
      return carpetasDe(directorio).flatMap((nombre) => [
        nombre,
        ...carpetasRecursivas(join(directorio, nombre)),
      ]);
    }

    const vecinas = [
      ...carpetasRecursivas(join(RAIZ, 'src', 'shared')),
      ...carpetasRecursivas(join(RAIZ, 'src', 'app')),
    ];

    expect(
      vecinas.length,
      'no se ha encontrado ninguna carpeta en shared/ ni en app/',
    ).toBeGreaterThan(0);
    expect(
      vecinas.filter((nombre) => enDisco.includes(nombre)),
      'hay una carpeta de shared/ o de app/ con el nombre de una funcionalidad: el patrón ancho ' +
        'de la regla de fronteras la prohibiría sin querer',
    ).toEqual([]);
  });

  it('los espacios de nombres del diccionario son el armazón más las funcionalidades del disco', () => {
    // Aquí es donde un renombrado se rompe callado. Las carpetas se mueven, los diccionarios se
    // quedan describiendo una estructura que ya no existe, y el compilador no dice nada porque una
    // clave es una cadena. Se comparan las listas ENTERAS y en los dos sentidos: un espacio de
    // nombres sin carpeta y una carpeta sin espacio de nombres salen los dos.
    expect(ESPACIOS_DEL_ARMAZON.length, 'no queda ningún espacio del armazón').toBeGreaterThan(0);

    for (const [idioma, diccionario] of Object.entries(DICCIONARIOS)) {
      const espacios: Record<string, unknown> = diccionario;

      expect(
        Object.keys(espacios).sort(),
        `${idioma}: los espacios de nombres de primer nivel no son el armazón más las ` +
          'funcionalidades que hay en src/features/',
      ).toEqual([...ESPACIOS_DEL_ARMAZON, ...enDisco].sort());

      for (const funcionalidad of enDisco) {
        const dentro = espacios[funcionalidad] as Record<string, unknown> | undefined;

        expect(dentro, `${idioma}: ${funcionalidad} no tiene espacio de nombres`).toBeDefined();
        expect(
          Object.keys(dentro ?? {}).sort(),
          `${idioma}: los espacios dentro de ${funcionalidad} no son sus recursos en disco`,
        ).toEqual(carpetasDe(join(FUNCIONALIDADES_EN_DISCO, funcionalidad)));
      }
    }
  });
});
