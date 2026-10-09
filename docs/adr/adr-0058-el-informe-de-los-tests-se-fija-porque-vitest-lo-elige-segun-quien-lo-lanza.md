---
tipo: referencia
stack: [typescript, react, vitest, github-actions]
aplica_a: [testing, desarrollo-con-ia, entrega-continua]
tags: [adr, vitest, informe, act, agente, ci, canario, adr-0045]
revisado: 2026-10-09
---

# ADR-0058: El informe de los tests se fija, porque vitest lo elige según quién lo lanza

- **Estado:** aceptado
- **Fecha:** 2026-10-09
- **Sale de la subida de vitest** del encargo del 2026-10-08, que pedía que los avisos de `act()`
  siguieran en cero. Lo aplican `5edf198` y `5654fb9`.
- **Toma el número que el encargo daba al 2.13.** El ADR de las reservas pasa a ser el 0059.

## Contexto

**Los avisos de `act()` del frontal son un canal desde el 1.8** (`AGENTS.md`): el fondo está a cero,
y un solo aviso es un hallazgo. La batería lo lee con `grep -c 'not wrapped in act'` sobre el
registro de `npm run test`. Funciona porque el informe `default` de vitest imprime la consola de
cada caso, también la de los que salen en verde, y React escribe el aviso con `console.error`.

**Vitest 4.1.11, si no se le configura ningún informe, lo elige según quién lo lanza.** El código
que lo decide es este (`vitest/dist/chunks/coverage.DM_a_rWm.js`):

```js
if (!resolved.reporters.length) {
  resolved.reporters.push([isAgent ? "agent" : "default", {}]);
  // also enable github-actions reporter as a default
  if (process.env.GITHUB_ACTIONS === "true") resolved.reporters.push(["github-actions", {}]);
}
```

`isAgent` sale de `std-env`, que mira variables de entorno como `AI_AGENT` o `CLAUDECODE`. El
informe `agent` es una variante del mínimo, y **se calla la consola de los casos en verde**. Con un
agente al mando de la batería, el registro dejaba de traer los avisos, y el cero dejaba de medir:
un aviso nuevo no habría salido en ninguna parte. La guía oficial de migración a la 4 no lo cuenta
entre sus cambios.

**Lo destapó un canario** de dos casos, uno que provoca un aviso de `act()` y otro que escribe un
`console.error` a mano. En la misma sesión, vitest 3.2.7 saca los dos, y la 4.1.11, ninguno. La
cifra de la batería, 0, era la misma en los dos.

## Decisión

### 1. `vite.config.ts` fija el informe

```ts
reporters: process.env.GITHUB_ACTIONS === 'true' ? ['default', 'github-actions'] : ['default'],
```

Es lo que vitest pone por su cuenta cuando no hay agente, escrito para que no dependa del entorno.
Con esa línea, el canario vuelve a dar **un aviso de `act()` y un `console.error`**, con
`GITHUB_ACTIONS` y sin él. Sin ella, cero.

### 2. Fijar un informe apaga los otros que vitest añadía solo

`5edf198` fijó solo `['default']`, y con eso la CI perdió `github-actions`, que vitest 3 también
añadía por su cuenta: es el que anota cada caso rojo en su fichero y su línea. Lo arregló `5654fb9`,
medido con un canario que falla a propósito:

| Entorno | Informe | Líneas `::error` | Resumen del job |
|---|---|---|---|
| `GITHUB_ACTIONS=true` | el de la configuración | 1 | 6 líneas |
| `GITHUB_ACTIONS=true` | `--reporter=default`, el pin de `5edf198` | 0 | ninguno |
| sin `GITHUB_ACTIONS` | el de la configuración | 0 | — |

**La regla, para lo que venga**: quien fija una opción que la herramienta resuelve por defecto
según el entorno, lee el código que la resuelve y copia todas sus ramas, no solo la que ve en su
equipo.

### 3. El resumen del job es nuevo, y se deja

El `github-actions` de la 4 escribe, además, una tabla con los ficheros y los casos en el resumen
del job (`jobSummary`, encendido por defecto). El de la 3.2.7 no lo hacía. Es lo que vitest 4
haría en la CI sin configurar nada, así que no se apaga.

## Lo que no cubre, dicho

- **Un `--reporter` en la línea de órdenes manda sobre la configuración.**
  `vitest run --reporter=dot` vuelve a callar la consola. Ni la CI ni la batería lo pasan; quien lo pase para leer menos, deja
  de ver el canal.
- **No hay un centinela permanente del canal.** Si alguien borrara la línea, la CI no lo notaría,
  porque allí no hay agente, y la batería local daría cero sin medir nada. Un caso que escribiera
  una marca, y un paso que la buscara en el registro, lo cerraría. Es una propuesta del PLAN, no
  está hecho.

## Consecuencias

- **El cero de `act()` vuelve a medir** con cualquiera que lance la batería, persona o agente.
- **La CI conserva sus anotaciones** de casos rojos, y gana el resumen del job.
- **Una herramienta que cambia de conducta al detectar un agente** es una trampa de esta forma de
  trabajar: lo que mide el agente deja de ser lo que mide la CI. Se busca en la siguiente subida
  de herramienta, con un canario que la herramienta esté obligada a sacar.
