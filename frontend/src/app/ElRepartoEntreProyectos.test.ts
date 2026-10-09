import { existsSync, readdirSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * EL REPARTO DE `src` ENTRE LOS PROYECTOS DE TYPESCRIPT (ítem 2.13).
 *
 * `npm run typecheck` es `tsc -b`, que compila los proyectos a los que apunta `tsconfig.json`, y
 * ninguno más. Desde el 2.13, `src` se reparte entre dos: el de la aplicación, que solo ve los tipos
 * del navegador, y el de los tests, que ve los de Node. Ese reparto se puede romper sin que nada se
 * ponga rojo: quitar de `tsconfig.json` la referencia a los tests dejó el typecheck y el lint en
 * verde con un error de tipos dentro de un test (la mutación 394).
 *
 * Así que aquí no se lee la configuración como texto. Se le pregunta a TypeScript qué raíces tiene
 * cada proyecto, se compara con el disco y se exige:
 *
 * - que todo fichero de `src` sea raíz de algún proyecto al que apunte `tsconfig.json`;
 * - que ningún test sea raíz del de la aplicación, que pediría los tipos de Node;
 * - que todo test sea raíz del de los tests, y que ese no tenga más que tests y declaraciones.
 *
 * Qué es «de los tests» lo dicen tres patrones tecleados, los mismos del `exclude` de
 * `tsconfig.app.json`. Cada uno tiene que casar con algún fichero: uno que no casara con nada no
 * clasificaría nada, y su mitad del reparto saldría verde sin mirar.
 */

/** La raíz del frontal. Sale del directorio de trabajo por lo mismo que en el barrido de las fronteras. */
const RAIZ = process.cwd();

const PATRONES_DE_LOS_TESTS = {
  'un *.test.ts o un *.test.tsx': (fichero: string) => /\.test\.tsx?$/.test(fichero),
  'src/setupTests.ts': (fichero: string) => fichero === 'src/setupTests.ts',
  'lo que hay bajo src/pruebas': (fichero: string) => fichero.startsWith('src/pruebas/'),
};

function esDeLosTests(fichero: string): boolean {
  return Object.values(PATRONES_DE_LOS_TESTS).some((casa) => casa(fichero));
}

/** El camino relativo a la raíz, con barras normales, sea cual sea el sistema. */
function relativo(camino: string): string {
  return relative(RAIZ, camino).split(sep).join('/');
}

function ficherosDe(directorio: string): string[] {
  return readdirSync(directorio, { withFileTypes: true })
    .flatMap((entrada) => {
      const camino = join(directorio, entrada.name);

      if (entrada.isDirectory()) {
        return ficherosDe(camino);
      }

      return /\.tsx?$/.test(entrada.name) ? [relativo(camino)] : [];
    })
    .sort();
}

function leerProyecto(camino: string): ts.ParsedCommandLine {
  const leido = ts.readConfigFile(camino, (fichero) => ts.sys.readFile(fichero));

  if (leido.error !== undefined) {
    throw new Error(ts.flattenDiagnosticMessageText(leido.error.messageText, '\n'));
  }

  return ts.parseJsonConfigFileContent(leido.config, ts.sys, RAIZ, undefined, camino);
}

/** Las raíces de un proyecto que están dentro de `src`. */
function raicesEnSrc(proyecto: ts.ParsedCommandLine): string[] {
  return proyecto.fileNames.map(relativo).filter((fichero) => fichero.startsWith('src/'));
}

describe('El reparto de src entre los proyectos de TypeScript', () => {
  const enDisco = ficherosDe(join(RAIZ, 'src'));
  const deLosTests = enDisco.filter(esDeLosTests);
  const deLaAplicacion = enDisco.filter((fichero) => !esDeLosTests(fichero));

  it('mira ficheros de verdad, y cada patrón de los tests casa con alguno', () => {
    expect(existsSync(join(RAIZ, 'tsconfig.json')), `${RAIZ} no es la raíz del frontal`).toBe(true);
    expect(deLaAplicacion.length).toBeGreaterThan(0);
    expect(deLosTests.length).toBeGreaterThan(0);

    for (const [nombre, casa] of Object.entries(PATRONES_DE_LOS_TESTS)) {
      expect(enDisco.some(casa), `el patrón «${nombre}» no casa con ningún fichero`).toBe(true);
    }
  });

  it('todo fichero de src es raíz de algún proyecto al que apunta tsconfig.json', () => {
    const referencias = leerProyecto(join(RAIZ, 'tsconfig.json')).projectReferences ?? [];
    const compilados = new Set(
      referencias.flatMap((referencia) => raicesEnSrc(leerProyecto(referencia.path))),
    );

    expect(enDisco.filter((fichero) => !compilados.has(fichero))).toEqual([]);
  });

  it('ningún test es raíz del proyecto de la aplicación, y todo lo demás sí', () => {
    const raices = raicesEnSrc(leerProyecto(join(RAIZ, 'tsconfig.app.json')));

    expect(raices.filter(esDeLosTests)).toEqual([]);
    expect(deLaAplicacion.filter((fichero) => !raices.includes(fichero))).toEqual([]);
  });

  it('todo test es raíz del proyecto de los tests, y este no tiene más que tests y declaraciones', () => {
    const raices = raicesEnSrc(leerProyecto(join(RAIZ, 'tsconfig.test.json')));

    expect(deLosTests.filter((fichero) => !raices.includes(fichero))).toEqual([]);
    expect(
      raices.filter((fichero) => !esDeLosTests(fichero) && !fichero.endsWith('.d.ts')),
    ).toEqual([]);
  });
});
