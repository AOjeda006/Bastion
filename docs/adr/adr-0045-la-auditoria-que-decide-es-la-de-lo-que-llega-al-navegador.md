---
tipo: referencia
stack: [typescript, react, github-actions]
aplica_a: [seguridad, entrega-continua, dependencias]
tags: [adr, npm-audit, ci, eslint, dependencias, frontal]
revisado: 2026-09-28
---

# ADR-0045: La auditoría que decide es la de lo que llega al navegador

- **Estado:** aceptado. **El disparador de `vitest` se cumplió, y se subió en `4f0e055` y `5edf198`
  (2026-10-09).** Es el de *Queda, con su disparador*, al final del §3. Lo demás de este ADR sigue
  entero.
  - La anotación de la auditoría entera pasó de dos moderadas a **2 críticas, 1 alta y 1
    moderada**, en `@vitest/mocker`, `source-map-js`, `tinypool` y `vitest`. Es la del run de
    `main` 37916215471, sobre `b2c5021`. Las críticas eran `tinypool`, que vitest 3 usaba para
    repartir los ficheros, y `vitest`, que depende de él; la alta, `source-map-js`. El encargo del
    2026-10-08 lo dio por cumplido.
  - `4f0e055` lleva `source-map-js` a 1.2.2 con `npm update`, sin salir de los rangos que se
    piden. `5edf198` lleva `vitest` a 4.1.11, la que nombraba este ADR, sin `--force`. Desde ahí,
    `npm audit` da **0**, entera y con `--omit=dev`. `5654fb9` le devuelve a la CI el informe
    `github-actions`, que el primero había apagado sin querer (ADR-0058).
- **Fecha:** 2026-09-28
- Sale del segundo de los cinco puntos pequeños del encargo del 2026-09-28, el epílogo del 2.7.

## Contexto

`npm ci` avisaba en cada run de la CI de dos cosas: **4 vulnerabilidades (2 altas)** y que
**`eslint@9.39.5` ya no tiene soporte**. El aviso solo sale en el registro del job, que sin testigo
contesta 403, así que nadie lo leía. Y nada en la CI decidía sobre él.

Medido en local el 2026-09-28, con `npm audit` y `npm ls` en `frontend/`:

| Paquete | Gravedad | Por dónde llega | Clase |
|---|---|---|---|
| `js-yaml` 4.3.1 | alta | `@eslint/eslintrc` (ESLint 9) y `@redocly/openapi-core` 1.34.19 (`openapi-typescript`) | herramienta |
| `@redocly/openapi-core` 1.34.19 | alta | depende de ese `js-yaml` | herramienta |
| `@vitest/mocker` 3.2.7 | moderada | `vitest` | herramienta |
| `vitest` 3.2.7 | moderada | depende de ese `@vitest/mocker` | herramienta |

**`npm audit --omit=dev` daba 0.** Las cuatro son de la herramienta: el linter, el ejecutor de los
tests y el generador del cliente. Corren en la CI y en el equipo de desarrollo, y ninguna viaja en
el paquete que se sirve.

## Decisión

### 1. La CI falla con `npm audit --omit=dev --audit-level=high`

Es un paso del job *Frontal*, justo después de `npm ci`.

- **`--omit=dev` porque eso es lo que llega al navegador**: las `dependencies` de `package.json`
  —React, el enrutador, TanStack Query, i18next, react-hook-form, Zod y `openapi-fetch`, con sus
  adaptadores—. Una
  vulnerabilidad ahí es un defecto del producto, aunque la publique un aviso de madrugada sobre un
  commit que no ha tocado nada. El rojo dice justo eso: lo que se sirve es vulnerable desde hoy.
- **`high` y no `moderate`**, porque la puerta tiene que poder quedarse cerrada sin que cada aviso
  menor ponga rojo el trabajo de otro. Lo moderado de lo que se sirve no queda escondido: lo cuenta
  el paso siguiente.
- **Si el registro no contesta, el paso sale rojo.** Se ha medido: con `--registry` apuntando a un
  puerto cerrado, `npm audit` sale con 1. Se relanza, y no se salta: un verde sin haber preguntado
  sería el falso verde de siempre.

**El canario**, en un directorio de usar y tirar: `lodash@4.17.20`, con una vulnerabilidad alta, pone
la puerta en **1** como dependencia de ejecución y la deja en **0** como dependencia de desarrollo.
Así que la puerta separa lo que dice separar.

### 2. La herramienta se cuenta, pero no decide

El paso siguiente pasa `npm audit` entero a JSON y deja **una anotación** con las cuentas por
gravedad y los nombres, que es lo único del job que se lee desde fuera sin testigo. No falla nunca.
Si el JSON no trae el resumen, deja un aviso en su lugar.

**Por qué no decide.** Una vulnerabilidad de la herramienta no alcanza a quien usa el producto. Se
arregla subiendo la herramienta, y a veces eso pide un salto de versión mayor, como la de `vitest`,
que no se da por sorpresa en el commit de otro.

### 3. Lo que se ha arreglado, y cómo, sin `--force`

- **ESLint 10**, en su propio commit. El `eslint-plugin-jsx-a11y` original no admite ESLint 10
  como par y no se publica desde 2024, así que se sustituye por el bifurcado de es-tooling,
  **`eslint-plugin-jsx-a11y-x`**. Sus 33 reglas de `strict` se compararon una a una con las del
  original: mismos niveles y mismas opciones, y solo cambia el prefijo. Un canario con una
  infracción de cada complemento sale marcado por los cuatro: a11y, *hooks*, i18next y
  typescript-eslint. Las otras dos salidas se descartaron:
  - forzar el par con `overrides` deja una combinación que nadie mantiene;
  - quedarse en ESLint 9 es quedarse sin soporte.
- **`js-yaml` 4.3.2.** Con ESLint 10 desaparece `@eslint/eslintrc`, y el camino que queda es
  `openapi-typescript`. `npm update @redocly/openapi-core` la lleva a 1.34.20, dentro del `^1.34.6`
  que declara `openapi-typescript`, y con ella entra `js-yaml` 4.3.2. `npm audit fix` a secas decía
  «up to date», porque la 1.34.19 fija `js-yaml` exacto. `esquema.ts` sale idéntico.

**Queda, con su disparador**: las dos moderadas de `vitest` (GHSA-82fw-gwwq-j7x9, lectura de
ficheros por un *mock* redirigido). El arreglo es `vitest` 4.1.11, una versión mayor. Solo afecta a
quien ejecuta los tests, y no se sube con `--force` ni de rebote. Se sube en su propio commit el día
que se toque la herramienta de tests a propósito, o antes si la anotación la da como alta.

## Lo que no cubre, dicho

**`--omit=dev` no es exactamente «lo que llega al navegador».** Vite es de desarrollo y, aun así,
mete en el paquete unos pocos ayudantes suyos: el *polyfill* de `modulepreload` y el cargador de los
fragmentos diferidos. La auditoría no los ve. Se acepta porque los avisos de Vite, hasta hoy, son del
servidor de desarrollo y no del código que emite. **El disparador**: un aviso de Vite, o de
`@vitejs/plugin-react`, que afecte a lo que se construye. Ese día la regla se revisa aquí.

## Consecuencias

- **La CI puede ponerse roja sin que el commit haya tocado nada**, cuando se publica un aviso alto
  de lo que se sirve. Es a propósito.
- **Los avisos de la herramienta se leen en las anotaciones del run**, y no en un registro que pide
  testigo.
- **Un complemento de ESLint que no admita la versión se cambia o se espera**, no se fuerza.
