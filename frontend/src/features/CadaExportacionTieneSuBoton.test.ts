import { readFileSync, readdirSync } from 'node:fs';
import { join, relative } from 'node:path';

import { describe, expect, it } from 'vitest';

/**
 * CADA EXPORTACIÓN DEL CONTRATO TIENE SU BOTÓN, Y CADA BOTÓN SU CASO (ítem 3.2).
 *
 * Tres listas que tienen que ser la misma, y las tres salen de disco:
 *
 * 1. **Las del contrato**: los `GET` de `docs/api/openapi.json` que acaban en `/exportacion`.
 * 2. **Las que pide el frontal**: los `api.GET('…/exportacion'` de las funcionalidades, fuera de
 *    los tests.
 * 3. **Las que un caso pulsa**: el camino que cada test le pasa a `exportarYComprobar`, que monta
 *    la pantalla, pulsa «Exportar a CSV» y comprueba el fichero. Que un camino esté aquí es que una
 *    pantalla de verdad tiene el botón y que el botón pide esa exportación.
 *
 * Una exportación nueva en la API sin botón pone esto rojo, y un botón sin su caso, también. Los
 * caminos se comparan con los parámetros vaciados: el contrato dice `{articuloId}` y el caso,
 * `${ARTICULO.id}`, y los dos son `{}`.
 *
 * **Nada se queda sin clasificar.** Las expresiones son selectores, y un selector que deja de casar
 * no se pone rojo: devuelve menos. Por eso cada lista se compara también con un recuento que no
 * depende de la expresión: cada `/exportacion'` escrito en una fuente está dentro de un
 * `api.GET(`, y cada `exportarYComprobar(` de un caso lleva un camino que la expresión ha leído.
 * Un camino guardado en una variable, o una llamada partida de otra manera, sale aquí.
 *
 * Este fichero es un test de `src/features` y escribe las dos marcas, así que se encontraría a sí
 * mismo: se deja fuera de los casos que lee, por su nombre.
 */
const RAIZ = process.cwd();
const FUNCIONALIDADES = join(RAIZ, 'src', 'features');
const CONTRATO = join(RAIZ, '..', 'docs', 'api', 'openapi.json');

/** Lo que dice la entrega: las trece pantallas del ítem 3.2. Una exportación nueva lo sube. */
const EXPORTACIONES = 13;

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

/** El camino con sus parámetros vaciados, se escriban como en el contrato o como en un caso. */
function sinParametros(camino: string): string {
  return camino.replaceAll(/\$\{[^}]+\}/g, '{}').replaceAll(/\{[^}]*\}/g, '{}');
}

function delContrato(): string[] {
  const contrato = JSON.parse(readFileSync(CONTRATO, 'utf8')) as {
    paths: Record<string, Record<string, unknown>>;
  };

  return Object.entries(contrato.paths)
    .filter(([camino, operaciones]) => camino.endsWith('/exportacion') && 'get' in operaciones)
    .map(([camino]) => sinParametros(camino));
}

const FICHEROS = ficherosDe(FUNCIONALIDADES);
const FUENTES = FICHEROS.filter((fichero) => !/\.test\.tsx?$/.test(fichero));
const ESTE = join(FUNCIONALIDADES, 'CadaExportacionTieneSuBoton.test.ts');
const CASOS = FICHEROS.filter((fichero) => /\.test\.tsx?$/.test(fichero) && fichero !== ESTE);

/** Lo que encuentra una expresión en unos ficheros, y cuántas veces aparece su marca. */
function leer(
  ficheros: string[],
  expresion: RegExp,
  marca: string,
): { caminos: string[]; marcas: number } {
  const caminos: string[] = [];
  let marcas = 0;

  for (const fichero of ficheros) {
    const texto = readFileSync(fichero, 'utf8');

    marcas += texto.split(marca).length - 1;

    for (const coincidencia of texto.matchAll(expresion)) {
      const camino = coincidencia[1];

      if (camino !== undefined) {
        caminos.push(sinParametros(camino));
      }
    }
  }

  return { caminos, marcas };
}

const PEDIDAS = leer(FUENTES, /api\.GET\(\s*'([^']+\/exportacion)'/g, "/exportacion'");
const PULSADAS = leer(
  CASOS,
  /exportarYComprobar\(\s*exportaciones,\s*['`]([^'`]+)['`]/g,
  'exportarYComprobar(',
);

describe('Cada exportación tiene su botón', () => {
  it('la raíz es la del frontal, y el contrato y las funcionalidades están donde se leen', () => {
    // Un directorio equivocado dejaría las tres listas vacías, y vacías serían iguales.
    expect(relative(RAIZ, FUNCIONALIDADES)).toBe(join('src', 'features'));
    expect(FUENTES.length).toBeGreaterThan(0);
    expect(CASOS.length).toBeGreaterThan(0);
    expect(FICHEROS).toContain(ESTE);
    // La pareja del filtro del contrato: un recurso que existe y no es una exportación, fuera.
    const contrato = JSON.parse(readFileSync(CONTRATO, 'utf8')) as { paths: object };
    expect(Object.keys(contrato.paths)).toContain('/api/v1/organizacion/almacenes');
    expect(delContrato()).not.toContain('/api/v1/organizacion/almacenes');
  });

  it('el contrato tiene las exportaciones que dice la entrega, sin repetir ninguna', () => {
    const contrato = delContrato();

    expect(contrato).toHaveLength(EXPORTACIONES);
    expect(new Set(contrato).size).toBe(contrato.length);
  });

  it('el frontal pide todas las del contrato, y ninguna más', () => {
    expect(PEDIDAS.caminos.length).toBeGreaterThan(0);
    expect(new Set(PEDIDAS.caminos)).toEqual(new Set(delContrato()));
  });

  it('cada exportación del contrato la pulsa un caso, desde su pantalla', () => {
    expect(PULSADAS.caminos.length).toBeGreaterThan(0);
    expect(new Set(PULSADAS.caminos)).toEqual(new Set(delContrato()));
  });

  it('ninguna exportación escrita se queda fuera de lo que leen las expresiones', () => {
    expect(PEDIDAS.marcas).toBe(PEDIDAS.caminos.length);
    expect(PULSADAS.marcas).toBe(PULSADAS.caminos.length);
  });
});
