---
tipo: referencia
stack: [typescript, react, vitest, eslint]
aplica_a: [testing, desarrollo-con-ia]
tags: [adr, tsconfig, extends, exclude, project-references, typescript-eslint, tipos-de-node, reparto]
revisado: 2026-10-09
---

# ADR-0061: Los tipos de Node son de los tests, y el reparto de `src` se mira contra el disco

- **Estado:** aceptado
- **Fecha:** 2026-10-09
- **Sale del encargo del 2026-10-09**, que pide, antes de las reservas del 2.13, «el `tsconfig` de
  los tests, con los tipos de Node, y el de la aplicación, sin ellos y sin los ficheros `*.test.*`»,
  con un canario: «un `process.env` en un componente no compila». Lo aplica `a68b3ea`, y el caso
  del reparto, `157bc56`.
- **Va después del ADR-0060 y antes del ADR-0059**, que es el de las reservas y lleva su número
  desde la puerta contestada.

## Contexto

**Vitest 3 traía los tipos de Node sin pedirlos, y vitest 4 no.** Al subir, en `5edf198`, los tests
dejaron de compilar, porque usan `node:fs`, `node:path`, `node:url`, `node:util` y `process`, y
`tsconfig.app.json` pasó a pedir `node` en sus `types`.

**Ese `tsconfig` compilaba la aplicación y sus tests juntos.** Con `node` dentro, un componente con
`process.env['NODE_ENV']` pasaba `tsc -b --force` con salida 0, y en el navegador `process` no
existe. No era nuevo: con vitest 3 pasaba igual, sin que nadie lo hubiera pedido.

## Decisión

### 1. Dos proyectos para `src`, y el de los tests hereda el de la aplicación

- **`tsconfig.app.json`** pide solo `vite/client`, y su `exclude` deja fuera los `*.test.ts`, los
  `*.test.tsx`, `src/setupTests.ts` y `src/pruebas`.
- **`tsconfig.test.json`** hace `extends` del de la aplicación, así que las opciones estrictas no
  pueden divergir. Cambia los `types` (los de Vite, Node, vitest y jest-dom), el `include` y el
  `exclude`.
- **`tsconfig.json`** referencia los dos y el de Node, y el `project` de ESLint lleva los tres.

**La trampa de `extends`: el `exclude` se hereda.** La referencia de `tsconfig` lo dice: `files`,
`include` y `exclude` del fichero que hereda **sustituyen** a los del base, y si no los escribe, se
quedan los del base. El `exclude` de la aplicación deja fuera justo los tests, así que el de los
tests lo escribe vacío. Sin él, el proyecto de los tests se queda sin tests, y el typecheck sale en
verde (la 400).

### 2. Ni `tsc -b` ni ESLint vigilan el reparto

- **`tsc -b` compila los proyectos que referencia `tsconfig.json`, y ninguno más.** Quitar la
  referencia a los tests deja el typecheck en verde con un error de tipos dentro de un test (la 394).
- **ESLint solo falla con un fichero que no está en ningún programa.** Lo que un test importa entra
  en el programa de los tests aunque no esté en su `include`, y ESLint lo analiza con tipos sin
  quejarse. Sacar `src/pruebas` del `include` deja el lint y el typecheck en verde (la 396): lo que
  hay ahí se sigue compilando porque los tests lo importan, y lo que no importara ninguno se quedaría
  sin compilar.

### 3. Un caso le pregunta a TypeScript, y lo compara con el disco

`frontend/src/app/ElRepartoEntreProyectos.test.ts` lee cada proyecto con
`ts.parseJsonConfigFileContent`, que resuelve el `extends`, el `include` y el `exclude` igual que
el compilador. Toma sus **raíces** (`fileNames`), no el cierre de lo que importan, y exige:

- que todo `.ts` y `.tsx` de `src` sea raíz de algún proyecto que referencie `tsconfig.json`;
- que ningún test sea raíz del de la aplicación, y que todo lo demás sí lo sea;
- que todo test sea raíz del de los tests, y que este no tenga más que tests y declaraciones.

Qué es «de los tests» lo dicen tres patrones tecleados, los del `exclude` de la aplicación. Cada uno
tiene que casar con algún fichero del disco, porque un patrón que no casa con nada no clasifica nada.
El primer caso afirma además que el barrido del disco encuentra ficheros.

## Medido

**Primera tanda, de la 387 a la 395**, sobre `a68b3ea`, con `python -X utf8 tanda-tsconfig.py`: cada
mutación se mide con `tsc -b --force` y con `eslint .`. `tsc -b` sale con **2** cuando hay errores.

| # | Mutación | Typecheck | Lint |
|---|---|---|---|
| — | Base | 0 | 0 |
| 387 | Un componente con `process.env` | **2** | **1** |
| 388 | Un componente que importa `node:fs` | **2** | **1** |
| 389 | Pareja: el mismo `process.env` en un test | 0 | 0 |
| 390 | `tsconfig.test.json` sin los tipos de Node | **2** | **1** |
| 391 | `tsconfig.app.json` sin su `exclude` | **2** | **1** |
| 392 | `tsconfig.test.json` sin los `*.test.tsx` | 0 | **1** |
| 393 | El `project` de ESLint sin `tsconfig.test.json` | 0 | **1** |
| 394a | Base, con un error de tipos en un test | **2** | 0 |
| 394 | `tsconfig.json` sin la referencia a los tests, con el mismo error | 0 | 0 |
| 395 | **Arnés**: el canario de la 387, en `src/pruebas` | 0 | 0 |

**La 394 salió verde en todo, y la espera lo decía**: estaba escrita para enseñar que la referencia
es lo que deja ver los tests. Una mutación verde en todos los carriles es un hallazgo, así que se
cubrió con el caso del punto 3, en su commit, y se volvió a medir.

**Segunda tanda**, sobre `157bc56`, con `python -X utf8 tanda-reparto.py` (y `tanda-reparto.py 400`).
Suma el caso nuevo solo, `vitest run src/app/ElRepartoEntreProyectos.test.ts`, con 4 casos:

| # | Mutación | Reparto, de 4 | Typecheck | Lint |
|---|---|---|---|---|
| — | Base | 0 | 0 | 0 |
| 394 | `tsconfig.json` sin la referencia a los tests, sin error plantado | **1** | 0 | 0 |
| 391 | `tsconfig.app.json` sin su `exclude` | **1** | **2** | **1** |
| 392 | `tsconfig.test.json` sin los `*.test.tsx` | **2** | 0 | **1** |
| 396 | `tsconfig.test.json` sin `src/pruebas` | **2** | 0 | 0 |
| 397 | `tsconfig.test.json` sin `src/setupTests.ts` | **2** | 0 | **1** |
| 398 | **Arnés**: el barrido del disco no encuentra nada | **1** | **2** | **1** |
| 399 | El patrón de `setupTests`, con una letra cambiada | **3** | 0 | 0 |
| 400 | `tsconfig.test.json` sin su `exclude` vacío | **2** | 0 | **1** |

- **Los rojos del reparto, por nombre.** La 394, «todo fichero de src es raíz de algún proyecto».
  La 391, «ningún test es raíz del proyecto de la aplicación». La 392, la 396, la 397 y la 400, esas
  dos: «todo fichero de src…» y «todo test es raíz del proyecto de los tests». La 398, «mira
  ficheros de verdad». La 399, las tres que no son «todo fichero de src…».
- **La 394 y la 396 son las que solo ve el reparto.**
- **El typecheck y el lint de la 398 son de la forma de la mutación**, que deja sin usar la función
  que barre el disco. El rojo que se buscaba es el del reparto.
- La porcelana sale vacía al terminar las dos tandas.

Los casos del frontal pasan de 252 a **256**, en 26 ficheros (`npm --prefix frontend run test`, su
línea `Tests`), y el canal de la consola sigue abierto y sin avisos.

## Lo que no cubre, dicho

- **Los tres patrones de «es de los tests» están escritos dos veces**, en el `exclude` y en el caso.
  Si dejan de coincidir, el caso sale rojo: es el lado bueno del error.
- **El caso mira `src`.** `tsconfig.node.json` compila la configuración de la raíz, y eso no lo
  reparte nadie.
- **Que la aplicación no importe nada de los tests no lo mira el caso.** Si lo hiciera, ese fichero
  entraría en el programa de la aplicación, sin los tipos de Node, y el typecheck lo vería si usa
  alguno.

## Consecuencias

- **Un componente con un tipo de Node es un rojo del typecheck**, y también del lint con tipos.
- **Romper el reparto es un rojo de los tests del frontal**, aunque el typecheck y el lint sigan en
  verde.
- **Para otro proyecto con Vite y vitest**: los tipos de Node no se ponen en el `tsconfig` de la
  aplicación, el de los tests escribe su `exclude` aunque sea vacío, y lo que el compilador y el
  linter no miran del reparto se le pregunta a TypeScript.
