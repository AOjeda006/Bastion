---
tipo: referencia
stack: [react, vitest, github-actions, bash]
aplica_a: [testing, entrega-continua]
tags: [adr, act, canal, consola, centinela, selector, contraejemplo, react-19, adr-0024, adr-0060]
revisado: 2026-10-19
---

# ADR-0066: El canal de la consola cuenta todos los avisos de `act()`, y su lista se mira contra el React instalado

- **Estado:** aceptado
- **Fecha:** 2026-10-19
- **Sale de la tanda del 2.17**, en la que la mutación 641 salió verde en los dos carriles. Es un
  hallazgo, y `AGENTS.md` pide cubrirlo en su commit y volver a medir.
- **Enmienda el ADR-0060 §1**, en el tercer caso en que falla el guion, «hay un solo aviso de
  `act()`». El guion lo contaba con una sola cadena.

## Contexto

**La 641 quita el `flushSync` de la rejilla del alta.** Sin él, `append` de react-hook-form lleva
el foco a la línea nueva desde un efecto, y el `change` que user-event emula al salir de la celda
abre un `act()` dentro de otro. React lo avisa una vez, en el caso «abre un borrador con una línea
en la unidad base…»:

> A component suspended inside an \`act\` scope, but the \`act\` call was not awaited.

Los casos siguen en verde, porque el foco acaba donde tiene que estar. **Y el canal también**,
porque `canal-de-la-consola.sh` contaba una sola cadena, `not wrapped in act`. Esa cadena sale en dos
de las ocho que escribe React 19.2.8 sobre `act`:

| Cadena de React | Paquete | ¿La contaba? |
|---|---|---|
| An update to %s inside a test was not wrapped in act(...) | `react-dom-client` | sí |
| A suspended resource finished loading inside a test, but the event was not wrapped in act(...) | `react-dom-client` | sí |
| A component suspended inside an \`act\` scope, but the \`act\` call was not awaited | `react` | no |
| You seem to have overlapping act() calls, this is not supported | `react` | no |
| You called act(async () => ...) without await | `react` | no |
| The current testing environment is not configured to support act(...) | `react-dom-client` | no |
| This browser does not have a MessageChannel implementation, so enqueuing tasks via await act(async () => ...) will fail | `react` | no |
| \`ReactDOMTestUtils.act\` is deprecated in favor of \`React.act\` | `react-dom-test-utils` | no |

La cadena del guion era un selector tecleado y sin contraejemplo. Encontraba lo que buscaba, pero
nadie había comprobado que eso fuera todo lo que había que buscar.

## Decisión

### 1. El guion cuenta siete trozos

Cada trozo es una parte de la primera línea de su mensaje, que es la que llega entera a una línea
del registro. El de `not wrapped in act` cubre dos cadenas. Un aviso es una línea del registro con
cualquiera de los siete, y el fondo sigue a cero: uno es un hallazgo.

```bash
AVISOS=(
  'not wrapped in act'
  'suspended inside an `act` scope'
  'overlapping act() calls'
  'act(async () => ...) without await'
  'not configured to support act'
  'does not have a MessageChannel implementation'
  '`ReactDOMTestUtils.act` is deprecated'
)
```

### 2. La lista se compara con las cadenas del React instalado, en los dos sentidos

Antes de leer el registro, el guion saca de `frontend/node_modules` las cadenas que nombran `act`.
Son los literales de `react/cjs/*.development.js` y `react-dom/cjs/*.development.js` que casan con
`"[^"]*\bact\b[^"]*"`. Comparar listas enteras pide las afirmaciones del ADR-0024:

- **ningún trozo sin su cadena**: cada trozo está dentro de alguna. Una letra cambiada no estaría;
- **ninguna cadena sin su trozo**: un aviso nuevo de React, al subir de versión, pone el paso en
  rojo hasta que su trozo entra en la lista. Es lo que la 641 encontró por casualidad, y ahora se
  encuentra por construcción;
- **la lista de React no sale vacía**: sin `npm ci`, o si los paquetes cambian de sitio, no habría
  nada con qué comparar, y las dos de arriba saldrían verdes sin mirar nada.

Va dentro del mismo paso, y no en un caso de vitest, porque lo que comprueba es el guion: la lista
que cuenta tiene que ser la que React escribe.

### 3. Lo demás del ADR-0060 no cambia

Siguen igual la marca del centinela, el registro en un fichero y `grep -c` sobre él, `AI_AGENT` en
el paso «Tests» de la CI y su `pipefail`.

## Medido

Los registros son los de `mutar641-canal.py`, un guion del *scratchpad*. Lanza el centinela y el
alta del ajuste con el informe fijado en `vite.config.ts`, el que imprime la consola de los casos en
verde. Con `--reporter=json` la consola no sale, y contar avisos ahí no mide nada. Las mutaciones
del guion van en una copia junto a él, en `scripts/ci/`, porque busca los paquetes desde su sitio, y
la copia se borra al acabar. La porcelana sale limpia.

```bash
python -X utf8 mutar641-canal.py base|641     # escribe canal-<n>.log
bash scripts/ci/canal-de-la-consola.sh canal-<n>.log
```

| # | Mutación | Registro | Paso | Lo que dice |
|---|---|---|---|---|
| — | ninguna, la base | `base` | **0** | marca 1, 7 trozos para 8 cadenas, ningún aviso |
| 641 | la rejilla sin `flushSync` (del 2.17) | `641` | **1** | «Avisos de act() en el registro: 1», el de «suspended inside an \`act\` scope» |
| 641 | la misma, con el guion de antes | `641` | 0 | era el hallazgo |
| 652 | un trozo con una letra cambiada: `insid` | `base` | **1** | el trozo no está en React, y la cadena del suspendido se queda sin contar |
| 653 | sin el trozo de los `act()` solapados | `base` | **1** | React escribe «You seem to have overlapping act() calls…», y el canal no lo cuenta |
| 654 | ARNÉS: la expresión busca `acto` | `base` | **1** | no hay ninguna cadena sobre act en los paquetes |

- **La 652 se ve dos veces**, una por cada sentido de la comparación: el trozo sin cadena y la
  cadena sin trozo.
- **La 654 rompe el arnés sin tocar el sujeto.** La lista de trozos está entera, y sin la tercera
  afirmación el paso saldría verde. Habría comparado la lista contra nada.

La suite entera da **342 de 342**, con el canal nuevo en verde:

```bash
(set -o pipefail; npm --prefix frontend run test 2>&1 | tee artifacts/vitest.log)
bash scripts/ci/canal-de-la-consola.sh artifacts/vitest.log
```

## Lo que no cubre, dicho

- **Un aviso que React compusiera** concatenando literales, o con una plantilla, no saldría como
  una sola cadena. Tampoco uno que hablara de `act` sin nombrarlo. En la 19.2.8 los ocho son
  literales sencillos.
- **Solo se mira la primera línea de cada mensaje.** Basta, porque cada trozo está en ella.
- **Los avisos de React que no son de `act()`** no entran en el canal: la clave que falta en una
  lista o el cambio de estado en mitad del pintado. Serían otro canal, con su propio fondo medido.
- **Sin `frontend/node_modules`, el paso falla.** En la CI corre después de `npm ci`, y en la
  batería, después de los tests.

## Consecuencias

- **El canal cuenta lo que React escribe sobre `act`, y nada se queda fuera sin que se vea.** Subir
  React con un aviso nuevo es un rojo en el canal hasta que su trozo está en la lista.
- **Un trozo mal tecleado es un rojo**, no un canal sordo para ese aviso.
- **Para otro proyecto**: una cadena que busca lo que escribe otra herramienta se compara con lo
  que esa herramienta puede escribir, en los dos sentidos, y esa lista no puede salir vacía.
