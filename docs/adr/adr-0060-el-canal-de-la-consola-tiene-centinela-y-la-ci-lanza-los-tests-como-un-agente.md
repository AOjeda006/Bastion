---
tipo: referencia
stack: [typescript, react, vitest, github-actions, bash]
aplica_a: [testing, desarrollo-con-ia, entrega-continua]
tags: [adr, vitest, informe, act, agente, ci, centinela, canario, pipefail, adr-0058]
revisado: 2026-10-09
---

# ADR-0060: El canal de la consola tiene centinela, y la CI lanza los tests como un agente

- **Estado:** aceptado
- **Fecha:** 2026-10-09
- **Sale del encargo del 2026-10-09**, que pide el centinela del canal de `act()` antes del 2.13,
  en su propio commit y con su mutación: «borrar el informe fijado en `vite.config.ts` pone el paso
  en rojo». Lo aplica `c112817`.
- **Enmienda el ADR-0058**, en el punto de *Lo que no cubre* que decía que no había centinela.
- **Va antes que el ADR-0059.** El encargo da el 0059 a las reservas, y el centinela se hace antes.

## Contexto

**Los avisos de `act()` del frontal son un canal con el fondo a cero desde el 1.8** (`AGENTS.md`).
React los escribe con `console.error`, y llegan al registro porque el informe `default` de vitest
imprime la consola de cada caso, también la de los que salen en verde.

**El ADR-0058 dejó abierto quién vigila eso.** Vitest 4, si no se le fija el informe, elige `agent`
cuando detecta un agente por el entorno, y ese informe se calla la consola de los casos en verde.
`vite.config.ts` fija el informe, y nada vigilaba esa línea:

- la CI no buscaba avisos de `act()`: el cero solo lo leía la batería del agente;
- y en la CI no hay agente, así que sin la línea vitest elegiría `default` igual. Borrarla no
  cambiaba nada allí.

## Decisión

### 1. Un caso escribe una marca, y un paso la busca

`frontend/src/app/ElCanalDeLaConsola.test.ts` escribe `CENTINELA DEL CANAL DE LA CONSOLA` con
`console.error`, desde un caso en verde, que es el camino de un aviso de `act()`.

`scripts/ci/canal-de-la-consola.sh` lee el registro de `npm run test` y falla en tres casos:

- el registro no existe o está vacío;
- **la marca no está**: el canal está mudo, y el cero de abajo no mediría nada;
- **hay un solo aviso de `act()`**. No se cuenta contra un umbral, como dice `AGENTS.md`.

Cuenta sobre el fichero, con `grep -c`, y no con `grep -q` al final de una tubería: bajo `pipefail`,
el `grep -q` corta la tubería y da rojos por azar en Linux (ítem 1.12).

### 2. La CI lanza los tests con `AI_AGENT` puesto

```yaml
- name: Tests
  env:
    AI_AGENT: ci
  run: |
    set -o pipefail
    mkdir -p ../artifacts
    npm run test 2>&1 | tee ../artifacts/vitest.log
```

**Sin la variable, la mutación que pide el encargo sale verde en la CI.** Sin agente, vitest elige
el informe que imprime la consola, con la línea de `vite.config.ts` o sin ella. Con la variable, la
CI elige el informe como lo elige el agente, y lo único que mantiene la marca en el registro es la
línea fijada. La medida está en la tabla de abajo, en las filas de la 383.

La variable va solo en el paso «Tests». `std-env` reconoce `AI_AGENT` antes que ninguna otra, y
vitest, además del informe, apaga los colores. El registro sale sin códigos de escape, que es lo que
se quiere de un fichero que se va a leer con `grep`.

### 3. El paso «Tests» escribe `set -o pipefail`

El `bash` por omisión de Actions es `bash -e {0}`, sin `pipefail`. Con el registro saliendo por
`tee`, el paso daría el código de `tee`, que es 0: **un caso rojo pasaría por verde**. Medido en la
386.

### 4. La batería de `AGENTS.md` lleva los dos pasos

Con el registro a `artifacts/vitest.log`, como la CI. Allí no hace falta poner `AI_AGENT`, porque
quien la lanza ya es un agente.

## Medido

Con `canal.sh`, un guion del *scratchpad*: lanza los tests en el entorno pedido, con el registro a
un fichero como la CI, y pasa el guion del paso sobre ese registro.

| Entorno | Variables |
|---|---|
| `ci` | `GITHUB_ACTIONS=true`, `CI=true`, `AI_AGENT=ci`, sin `CLAUDECODE` ni `CLAUDE_CODE` |
| `ci-sin-agente` | el mismo, sin `AI_AGENT` |
| `agente` | el del agente, que trae `AI_AGENT` y `CLAUDECODE` |

| # | Mutación | Entorno | Tests | Paso | Marcas | Avisos |
|---|---|---|---|---|---|---|
| — | ninguna, la base | `ci` | 0 | **0** | 1 | 0 |
| 383 | sin la línea `reporters` de `vite.config.ts` | `ci` | 0 | **1**, mudo | 0 | 0 |
| 383 | la misma | `ci-sin-agente` | 0 | **0** | 1 | 0 |
| 383 | la misma | `agente` | 0 | **1**, mudo | 0 | 0 |
| 384 | ARNÉS: el centinela no escribe la marca | `ci` | 0 | **1**, mudo | 0 | 0 |
| 385 | un canario temporal con un aviso de `act()` | `ci` | 0 | **1**, un aviso | 1 | 1 |
| 386 | un caso rojo, con `pipefail` | `ci` | **1** | 0 | 1 | 0 |
| 386 | el mismo, sin `pipefail` | `ci` | **0** | 0 | 1 | 0 |

- **La fila `ci-sin-agente` de la 383 es la razón del punto 2.** Es la CI de antes de este ADR: la
  línea fuera, y el paso en verde.
- **La 384 rompe el arnés sin tocar el sujeto.** La línea de `vite.config.ts` sigue ahí y el paso
  sale rojo igual, que es lo que tiene que pasar si alguien quita o cambia la marca.
- **El canario de la 385** es un componente que cambia su estado en un `setTimeout` después del
  `render`, fuera de `act()`. React escribe «An update to Tardio inside a test was not wrapped in
  act(...)», y el paso lo cuenta.
- **En la 386 el paso del canal sale verde**, y está bien: lo que pone rojo el job es el paso
  «Tests». Sin `pipefail` no lo pone, y vitest dice «1 failed».

Los casos del frontal son **252** en la base (`npm --prefix frontend run test`, su línea `Tests`),
251 de antes y el centinela. Con los canarios de la 385 y la 386, 253. La porcelana sale vacía al
terminar la tanda.

## Lo que no cubre, dicho

- **Un `--reporter` en la línea de órdenes sigue mandando sobre la configuración**, como dice el
  ADR-0058. Ahora, si alguien lo pasara en la CI, el paso lo vería: un informe que se calla la
  consola de los verdes se calla también la marca.
- **La marca está escrita dos veces**, en el caso y en el guion. Si dejan de coincidir, el paso no
  la encuentra y falla. Es el lado bueno del error.
- **El paso mira el registro del frontal.** Un canal de avisos en otro proyecto de tests necesitaría
  su propia marca.

## Consecuencias

- **Borrar la línea que fija el informe es un rojo en la CI**, y no solo en la batería del agente.
- **La CI y el agente lanzan los tests en el mismo modo**, el de agente. Lo que vitest cambie al
  detectar un agente, lo verá primero la CI.
- **Un caso rojo ya no puede pasar por verde** porque el registro salga por una tubería.
