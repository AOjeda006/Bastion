---
tipo: referencia
stack: [react, typescript, vitest]
aplica_a: [frontal, accesibilidad, testing]
tags: [adr, dialog, showmodal, foco, tabindex, mutacion, jsdom, vitest-browser, adr-0080]
revisado: 2026-11-08
---

# ADR-0081: El foco de la ventana lo pone `showModal()`, en lo primero que se puede enfocar

- **Estado:** aceptado.
- **Fecha:** 2026-11-08
- **Sale del ítem 3.5**, de la tanda de mutaciones de su F2: la 1199 quitó el `focus()` del título,
  y el carril del navegador siguió en verde.
- **Enmienda el ADR-0080** en su punto 3, en el 5 y en una línea de sus *Consecuencias*.

## Contexto

El ADR-0080 §3 decía que «`showModal()` lleva el foco al primer control», y que aquí va al título.
`Dialogo.tsx` lo llevaba con un `focus()` sobre el título, justo después de `showModal()`.

La mutación 1199 quitó esa línea. Se puso rojo un caso de jsdom, «abre la ventana con la zona de
ahora marcada…» (`LaZonaDelAlmacen.test.tsx`), que afirmaba el foco en el título. El del navegador
que lo fija, «se abre modal, con el foco en el título…» (`Dialogo.navegador.test.tsx`), siguió en
verde.

Las dos cosas tienen la misma causa. `showModal()` no lleva el foco al primer *control*, sino a lo
primero que se puede enfocar dentro de la ventana: son los pasos de enfoque del diálogo del estándar
HTML, cuando nada lleva `autofocus`. Y el título lo es, porque su `tabIndex={-1}` lo hace enfocable
sin meterlo en el tabulador, y va el primero. En Chromium, el `focus()` repetía lo que ya había
hecho la plataforma. En jsdom, cuyo `showModal()` de `setupTests.ts` solo pone `open`, era lo único
que ponía el foco: el caso de jsdom probaba la línea, y no lo que pasa en un navegador.

## Decisión

1. **Sin `focus()`.** El título lleva el foco porque es lo primero que se puede enfocar en la
   ventana, y eso lo deciden dos cosas del marcado: su `tabIndex={-1}`, y que va antes que lo de
   dentro. Es lo que el ADR-0080 hace con todo lo demás: lo hace la plataforma, y no se rehace.
2. **El foco que entra se prueba en el navegador**, como el que vuelve. jsdom no lo afirma, y el
   caso de `LaZonaDelAlmacen.test.tsx` deja de hacerlo.
3. **La línea que decide tiene su mutación.** La 1211 quita el `tabIndex={-1}`: el título deja de
   poder enfocarse, y el foco ya no puede ir a él.

## Lo que enmienda

- **ADR-0080 §3.** Donde decía «`showModal()` lleva el foco al primer control», dice «a lo primero
  que se puede enfocar dentro», y lo que pone ahí el título es su `tabIndex={-1}`, sin `focus()`.
- **ADR-0080 §5 y *Consecuencias*.** A lo que jsdom no tiene se suma el foco que entra al abrir: su
  `showModal()` no lo mueve. Un test de jsdom que abre una ventana tiene el foco donde estaba.

## Consecuencias

- **Una ventana que quiera el foco en otro sitio no lo pide con `focus()`.** Lo pide con el marcado,
  y la plataforma lo respeta: un `autofocus` dentro se lo llevaría, porque los pasos de enfoque del
  diálogo lo prefieren a lo primero enfocable. Ninguna ventana lo usa hoy.
- **Medido en Chromium**, que es el carril del navegador. Firefox y Safari siguen el mismo estándar,
  y no se miden.

## Cómo se prueba

| Lo que se fija | Dónde |
|---|---|
| Al abrirse, el foco va al título, sin `focus()` | `Dialogo.navegador.test.tsx`, «se abre modal, con el foco en el título…» |
| Sin el `tabIndex={-1}`, el foco no va al título (la 1211) | El mismo caso |

La tanda de la F2 del 3.5, con la 1199 y la 1211 por nombre, está en el PLAN, en su punto 40, y al
cerrarse la fase, en `docs/historia/fase-3.md`.

## Procedencia

Del agente, al medir la tanda de la F2 del 3.5 (2026-11-08): la 1199 era una línea que nada
probaba en un navegador. El ADR-0080, que enmienda, es del mismo ítem.
