import { join, relative } from 'node:path';

import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

import { IDIOMAS } from './i18n/idioma.ts';

/**
 * UN DICCIONARIO SE IMPORTA POR SU TIPO — ADR-0063 §9.
 *
 * La regla vive en `eslint.config.js`, y existe porque el presupuesto solo ve la mitad del fallo.
 * Un import de valor desde el arranque hace desaparecer el fragmento del idioma, y eso el
 * presupuesto lo dice. Uno desde una página no cambia ningún tamaño: solo QUÉ se descarga con
 * ella, que es lo que pasó con `useTextoDeFallo` y once páginas hasta el 2.14.
 *
 * Se prueba por el efecto, como las fronteras: se le pide a ESLint que linte cada forma prohibida
 * desde ficheros de verdad, uno por zona, y se exige que la marque. Y cada forma permitida, que
 * no. El patrón de la regla es una expresión regular tecleada, así que sus contraejemplos salen
 * del proyecto: cada idioma de `IDIOMAS`, con su diccionario y con sus textos de los `type`.
 */
const RAIZ = process.cwd();
const REGLA = '@typescript-eslint/no-restricted-imports';

/** Un fichero de verdad por zona, y cómo se escribe desde él el camino a un diccionario. */
const ZONAS = [
  {
    fichero: join(RAIZ, 'src', 'app', 'rutas.tsx'),
    caminos: (nombre: string) => [`./i18n/${nombre}.ts`, `@/app/i18n/${nombre}.ts`],
  },
  {
    fichero: join(RAIZ, 'src', 'shared', 'ui', 'useTextoDeFallo.ts'),
    caminos: (nombre: string) => [`@/app/i18n/${nombre}.ts`, `../../app/i18n/${nombre}.ts`],
  },
  {
    fichero: join(
      RAIZ,
      'src',
      'features',
      'catalogo',
      'articulos',
      'model',
      'esquemaDeAltaDeGtin.ts',
    ),
    caminos: (nombre: string) => [`@/app/i18n/${nombre}.ts`],
  },
  {
    // La forma del defecto que midió el ADR-0036: el valor, desde `index.ts`, que es arranque.
    fichero: join(RAIZ, 'src', 'app', 'i18n', 'index.ts'),
    caminos: (nombre: string) => [`./${nombre}.ts`],
  },
];

/** El único fichero de la aplicación que los importa por su valor, y un test, que no se empaqueta. */
const EXENTOS = [
  join(RAIZ, 'src', 'app', 'i18n', 'diccionarios.ts'),
  join(RAIZ, 'src', 'app', 'ElCambioDeIdioma.test.tsx'),
];

/** Los dos módulos de cada idioma: el diccionario y los textos de los `type`. */
const NOMBRES = IDIOMAS.flatMap((idioma) => [idioma, `tiposDeError.${idioma}`]);

/** Las formas que traen el VALOR. La mezclada es la que escribe el arreglo automático. */
const PROHIBIDAS = [
  (camino: string) => `import { es } from '${camino}';\nexport { es };\n`,
  (camino: string) => `import { es, type Diccionario } from '${camino}';\nexport { es };\n`,
  (camino: string) => `import '${camino}';\n`,
  (camino: string) => `export { es } from '${camino}';\n`,
];

/** Las formas que solo traen el TIPO, que la emisión borra. */
const PERMITIDAS = [
  (camino: string) =>
    `import type { Diccionario } from '${camino}';\nexport type { Diccionario };\n`,
  (camino: string) =>
    `import { type Diccionario } from '${camino}';\nexport type { Diccionario };\n`,
];

/** Un `any` que la configuración del proyecto marca siempre: si calla, ESLint no mira el fichero. */
const CANARIO = 'const loQueSea: any = 1;\nexport { loQueSea };\n';

describe('Un diccionario se importa por su tipo', () => {
  // Sin `disallowAutomaticSingleRunInference`, con `CI=true` ESLint linta el fichero de disco y
  // no el texto: el porqué entero, en `ElBarridoDeLasFronteras`.
  const eslint = new ESLint({
    cwd: RAIZ,
    overrideConfig: {
      languageOptions: { parserOptions: { disallowAutomaticSingleRunInference: true } },
    },
  });

  async function avisos(
    codigo: string,
    fichero: string,
  ): Promise<{ regla: string[]; todos: string }> {
    const [resultado] = await eslint.lintText(codigo, { filePath: fichero });

    expect(resultado, `ESLint no ha devuelto nada para '${fichero}': lo ignora`).toBeDefined();

    const mensajes = resultado?.messages ?? [];

    return {
      regla: mensajes.filter((aviso) => aviso.ruleId === REGLA).map((aviso) => aviso.message),
      todos:
        ` — en '${relative(RAIZ, fichero)}', ${JSON.stringify(codigo)}; ESLint dijo: ` +
        JSON.stringify(
          mensajes.map((aviso) => `[${aviso.ruleId ?? 'sin regla'}] ${aviso.message}`),
        ),
    };
  }

  async function exigirQueMira(fichero: string): Promise<void> {
    const canario = await avisos(CANARIO, fichero);

    expect(
      canario.todos.includes('no-explicit-any'),
      `ESLint no marca un 'any' descarado: no está mirando el fichero` + canario.todos,
    ).toBe(true);
  }

  it('marca el valor desde el armazón, lo compartido, una funcionalidad y el propio i18n', async () => {
    let comprobadas = 0;

    for (const zona of ZONAS) {
      await exigirQueMira(zona.fichero);

      for (const camino of NOMBRES.flatMap(zona.caminos)) {
        for (const forma of PROHIBIDAS) {
          const visto = await avisos(forma(camino), zona.fichero);

          expect(
            visto.regla,
            `no ha marcado un import de valor de '${camino}'` + visto.todos,
          ).not.toEqual([]);
          comprobadas += 1;
        }

        for (const forma of PERMITIDAS) {
          const visto = await avisos(forma(camino), zona.fichero);

          expect(visto.regla, `ha marcado un import de tipo de '${camino}'` + visto.todos).toEqual(
            [],
          );
        }
      }
    }

    // El conjunto, contado: dos idiomas, dos módulos, seis caminos y cuatro formas. Si `IDIOMAS`
    // se quedara vacío, lo de arriba sería verde sin haber comprobado nada.
    expect(comprobadas).toBe(IDIOMAS.length * 2 * 6 * PROHIBIDAS.length);
    expect(IDIOMAS.length).toBeGreaterThan(1);
  }, 60_000);

  it('deja el valor a `diccionarios.ts` y a los tests, y no marca lo que no es un diccionario', async () => {
    for (const fichero of EXENTOS) {
      await exigirQueMira(fichero);

      for (const nombre of NOMBRES) {
        const visto = await avisos(
          `import { es } from './i18n/${nombre}.ts';\nexport { es };\n`,
          fichero,
        );

        expect(visto.regla, `ha marcado un fichero exento` + visto.todos).toEqual([]);
      }
    }

    // La expresión no es más ancha de lo que dice: sus vecinos de carpeta y de nombre pasan.
    const indice = ZONAS[3]!.fichero;
    for (const vecino of ['./idioma.ts', './diccionarios.ts', '@/shared/api/errores.ts']) {
      const visto = await avisos(
        `import { loQueSea } from '${vecino}';\nexport { loQueSea };\n`,
        indice,
      );

      expect(visto.regla, `ha marcado '${vecino}', que no es un diccionario` + visto.todos).toEqual(
        [],
      );
    }
  }, 60_000);
});
