import { readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

/**
 * EL RELOJ SOLO SE LEE EN `dias.ts` — la regla de `eslint.config.js`, probada por el efecto.
 *
 * El 2.16 dejó un solo «hoy» en el frontal, `hoyEnElServidor`, y la regla impide que vuelva un
 * segundo. Como las fronteras y los diccionarios, no se lee la configuración: se le pide a ESLint
 * que linte cada forma de leer el reloj desde ficheros de verdad, uno por zona, y se exige que la
 * marque. Y lo que construye una fecha sin leer el reloj, que no.
 *
 * **La pareja es el sitio donde el reloj se lee de verdad.** El contenido de `dias.ts`, tal como
 * está en disco, no sale marcado en su sitio, que es la excepción, y sí desde cualquier otro. Los
 * selectores de la regla son cadenas tecleadas: con una letra cambiada, la prohibición podría
 * seguir marcando las formas de aquí y no la de verdad, y es la pareja la que se pondría roja.
 *
 * **Lo que no ve**, y no existe en el frontal: `globalThis.Date` o `window.Date`, un `Date`
 * guardado con otro nombre, `performance.now()`, que mide duraciones y no da días, y el `format()`
 * de `Intl.DateTimeFormat` sin argumento. Ninguno aparece con
 * `git grep -n -E "globalThis\.Date|window\.Date|performance\.now|\.format\(\)" -- frontend/src`,
 * y prohibirlos sería una regla sin contraejemplo.
 */
const RAIZ = process.cwd();
const REGLA = 'no-restricted-syntax';
const DIAS = join(RAIZ, 'src', 'shared', 'lib', 'dias.ts');

/** Un fichero de verdad por zona: el armazón, lo compartido, una funcionalidad, un test y las pruebas. */
const ZONAS = [
  join(RAIZ, 'src', 'app', 'rutas.tsx'),
  join(RAIZ, 'src', 'shared', 'lib', 'parametrosDeUrl.ts'),
  join(RAIZ, 'src', 'features', 'catalogo', 'tarifas', 'ui', 'PaginaDeTarifas.tsx'),
  join(RAIZ, 'src', 'shared', 'lib', 'dias.test.ts'),
  join(RAIZ, 'src', 'pruebas', 'zona.ts'),
];

/** Las formas que leen el reloj. La última es la de `dias.ts`, como valor por omisión. */
const PROHIBIDAS = [
  'const ahora = new Date();\nexport { ahora };\n',
  'const ahora = new Date;\nexport { ahora };\n',
  'const ahora = Date.now();\nexport { ahora };\n',
  'const reloj = Date.now;\nexport { reloj };\n',
  'const ahora = Date();\nexport { ahora };\n',
  'export function hoy(momento: Date = new Date()): string {\n  return momento.toISOString();\n}\n',
];

/** Las que construyen una fecha a partir de algo, sin leer el reloj. */
const PERMITIDAS = [
  "const dia = new Date('2026-10-18T00:30:00Z');\nexport { dia };\n",
  'const origen = new Date(0);\nexport { origen };\n',
  'const dia = Date.UTC(2026, 9, 18);\nexport { dia };\n',
  "const dia = Date.parse('2026-10-18');\nexport { dia };\n",
  'export function dia(momento: Date): string {\n  return momento.toISOString();\n}\n',
];

/** Un `any` que la configuración del proyecto marca siempre: si calla, ESLint no mira el fichero. */
const CANARIO = 'const loQueSea: any = 1;\nexport { loQueSea };\n';

describe('El reloj solo se lee en dias.ts', () => {
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
        ` — en '${relative(RAIZ, fichero)}', ${JSON.stringify(codigo.slice(0, 80))}; ESLint ` +
        'dijo: ' +
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

  it('marca cada lectura del reloj desde el armazón, lo compartido, una funcionalidad, un test y las pruebas', async () => {
    let comprobadas = 0;

    for (const zona of ZONAS) {
      await exigirQueMira(zona);

      for (const forma of PROHIBIDAS) {
        const visto = await avisos(forma, zona);

        expect(visto.regla, 'no ha marcado una lectura del reloj' + visto.todos).not.toEqual([]);
        comprobadas += 1;
      }
    }

    // El conjunto, contado: si una lista se quedara vacía, lo de arriba sería verde sin mirar.
    expect(comprobadas).toBe(5 * 6);
  }, 60_000);

  it('no marca lo que construye una fecha sin leer el reloj', async () => {
    let comprobadas = 0;

    for (const zona of ZONAS) {
      for (const forma of PERMITIDAS) {
        const visto = await avisos(forma, zona);

        expect(visto.regla, 'ha marcado una fecha que no lee el reloj' + visto.todos).toEqual([]);
        comprobadas += 1;
      }
    }

    expect(comprobadas).toBe(5 * 5);
  }, 60_000);

  it('la pareja: dias.ts lee el reloj; en su sitio no se marca, y desde cualquier otro sí', async () => {
    const contenido = readFileSync(DIAS, 'utf8');

    await exigirQueMira(DIAS);
    const enSuSitio = await avisos(contenido, DIAS);

    expect(enSuSitio.regla, 'ha marcado dias.ts, que es la excepción' + enSuSitio.todos).toEqual(
      [],
    );

    for (const zona of ZONAS) {
      const fuera = await avisos(contenido, zona);

      expect(
        fuera.regla,
        'el contenido de dias.ts no sale marcado fuera de su sitio: la regla no ve la lectura ' +
          'de verdad' +
          fuera.todos,
      ).not.toEqual([]);
    }
  }, 60_000);
});
