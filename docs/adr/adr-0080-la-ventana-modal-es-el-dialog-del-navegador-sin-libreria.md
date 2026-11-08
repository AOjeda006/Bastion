---
tipo: referencia
stack: [react, typescript, vitest]
aplica_a: [frontal, accesibilidad, testing, cache]
tags: [adr, dialog, showmodal, modal, inert, foco, escape, scroll, strict-mode, jsdom, vitest-browser, radix, tanstack-query, adr-0079]
revisado: 2026-11-08
---

# ADR-0080: La ventana modal es el `<dialog>` del navegador, sin librería de primitivas

- **Estado:** aceptado.
- **Fecha:** 2026-11-08
- **Sale del ítem 3.5**, el punto 7 de su puerta: la zona de cada almacén y de cada empresa se
  cambia en una ventana, desde su fila del listado. Es la primera ventana modal del frontal.
- **No enmienda ningún ADR.** Responde a una nota de la fase 0 que no es un ADR (*Contexto*), y lee
  `stacks/react/convenciones.md` de una manera que conviene dejar escrita (punto 1).

## Contexto

`stacks/react/convenciones.md` pide «primitivas accesibles sin estilo (Radix u equivalente)» para
un diálogo, y lo dice con una razón: «no reimplementes un diálogo modal: foco atrapado, `Escape`,
*scroll* bloqueado y `aria-modal` no salen bien a la primera».

La fase 0 decidió no tener librería de primitivas, porque todos los controles del armazón tenían
elemento nativo, y anotó que una «llegará cuando haga falta un diálogo modal, un menú o un combo con
autocompletado, que es donde lo nativo se acaba» (`docs/historia/fase-0.md`, *Sin librería de
primitivas accesibles*). Es una nota del PLAN, no un ADR, así que no hay nada que enmendar: este
dice por qué, para el diálogo, no llegó.

Lo nativo no se acaba en el diálogo. El navegador tiene `<dialog>`, y `showModal()` lo abre en la
capa de arriba, con el resto de la página inerte.

## Decisión

### 1. El `<dialog>` del navegador, abierto con `showModal()`

`shared/ui/Dialogo.tsx` es un `<dialog>` con su título, sin ninguna dependencia. De las cuatro
cosas que nombra la convención, la plataforma da tres, y la cuarta es una línea de CSS (punto 4):

- **El foco atrapado**: el resto de la página queda inerte, y un `focus()` por programa sobre un
  botón de fuera no saca el foco de la ventana. **Medido** en Chromium. El tabulador no se mide
  aparte: lo inerte no se enfoca por ningún camino, y el `focus()` es el que menos se deja.
- **`Escape`** la cierra, y al cerrarse **el foco vuelve a quien la abrió**. **Medido** en Chromium.
- **`aria-modal`**: la plataforma expone como modal un `<dialog>` abierto con `showModal()`. **No se
  afirma**: la prueba no lee el árbol de accesibilidad. Lo que `aria-modal` promete al lector de
  pantalla, que lo de fuera no se alcanza, es la inercia, que sí se mide.

Así que la convención se lee con el `<dialog>` como el «equivalente» de Radix, y no se reimplementa
nada: lo hace el navegador, sin descargar nada. Es una lectura, no una excepción, y se escribe aquí
para que el día que se enriquezca la biblioteca se decida si la convención lo dice ella.

### 2. Se abre en un efecto que depende del elemento, y se desmonta con `close`

- **El elemento va en el estado, no en una referencia**, y lo abre el efecto que depende de él. En
  el montaje el estado todavía no tiene elemento, y el efecto no hace nada; la apertura llega en el
  render siguiente, que no es un montaje.
- **El modo estricto no la cierra, y está medido.** Monta los efectos dos veces: lo de dentro de la
  ventana se monta dos veces (`montajes === 2`), y en 50 ms no llega ningún `close`
  (`cierres === 0`). Una sonda, ya borrada, contó además una llamada a `showModal()` y ninguna a
  `close()`. Un primer borrador descartaba un `close` con la ventana todavía abierta, contra un
  cierre del doble montaje que el comentario daba por hecho; la medida dijo que no llega, y la
  guarda y su comentario se quitaron.
- **Se desmonta con el evento `close`, y no antes.** Cerrar es lo que devuelve el foco, así que para
  cerrarla se cierra, con `cerrarElDialogo` desde dentro, y `alCerrar` llega después. El padre la
  desmonta entonces.

### 3. El foco entra por el título

`showModal()` lleva el foco al primer control. Aquí va al título, que no se tabula
(`tabIndex={-1}`): lo primero que se lee es qué se pregunta, y los avisos de la zona, que van en la
descripción del grupo, se leen antes de elegir.

### 4. La página de debajo no corre

`showModal()` deja la página inerte, pero no la bloquea: la rueda la sigue moviendo por detrás.
`index.css` lo cierra con `html:has(dialog:modal) { overflow: hidden; }`, sin JavaScript y sin
contar ventanas: rige mientras haya una modal abierta.

### 5. En jsdom, abrir y cerrar, y nada más

jsdom trae el elemento, pero no `showModal()` ni `close()`. `setupTests.ts` pone los dos: abrir pone
`open`, y cerrar, si estaba abierta, lo quita y avisa con `close`. **Lo que no se imita no se prueba
allí**: el `close` del navegador llega en otra tarea y el de jsdom en el acto, y jsdom no tiene capa
de arriba, ni inercia, ni `Escape`, ni foco que vuelva. Eso, y el modo estricto, lo prueba el carril
del navegador.

### 6. Tras cambiar una zona, la caché entera se invalida

La zona de una empresa cambia el día de los almacenes que la heredan, y el día está en muchas
consultas: la de `['hoy']`, la ficha de cada almacén y los listados. Así que cambiar una zona
invalida la caché entera (`invalidateQueries()` sin clave), como el cambio de empresa: lo que está
en pantalla se vuelve a leer, y lo demás queda viejo. **Invalidar, no reiniciar**: `resetQueries`
vaciaría el listado mientras se vuelve a leer, y el botón al que vuelve el foco desaparecería con
él.

## Alternativas descartadas

- **Radix, o react-aria.** Una dependencia y una descarga para lo que ya da la plataforma, y una
  capa más entre el rol y el marcado.
- **Una ventana hecha a mano**, un `<div role="dialog" aria-modal="true">` con su trampa de foco. Es
  lo que la convención prohíbe.
- **Un `<dialog>` abierto con `open`**, sin `showModal()`. No es modal: ni capa de arriba, ni
  inercia, ni `Escape`.
- **Una página para la zona**, con su ruta. Pierde el listado de vista, y el punto 7 de la puerta
  eligió la ventana.

## Lo que no cubre, dicho

- **Solo se mide en Chromium**, que es el navegador del carril. Firefox y WebKit no se prueban. Un
  motor sin `:has()` dejaría correr la página por detrás, y nada más.
- **Ningún lector de pantalla** ha leído la ventana; `aria-modal` se deja a la plataforma (punto 1).
- **Pulsar fuera no la cierra.** Se sale con `Escape`, con *Cancelar* o guardando.
- **Un menú, un combo con autocompletado o un *tooltip*** siguen sin primitiva. Para ellos la nota
  de la fase 0 sigue en pie, y lo decidirá la puerta del ítem que los necesite.

## Consecuencias

- **La próxima ventana modal usa `Dialogo`**, con su título, y se cierra con `cerrarElDialogo`.
- **Un test de jsdom que abre una ventana depende del sustituto de `setupTests.ts`**: el `close`
  llega en el acto, y el foco no vuelve. Lo que dependa de eso se prueba en el navegador.
- **El primer modal no añade ninguna dependencia**, ni al `package.json` ni a la descarga.

## Cómo se prueba

| Lo que se fija | Dónde |
|---|---|
| Modal, con el foco en el título, y el modo estricto no la cierra | `Dialogo.navegador.test.tsx` |
| La página no corre con la ventana abierta, y vuelve a correr al cerrarla | `Dialogo.navegador.test.tsx` |
| Lo de fuera, inerte: un `focus()` no saca el foco | `Dialogo.navegador.test.tsx` |
| `Escape` y la salida de dentro la cierran una vez, y el foco vuelve a quien la abrió | `Dialogo.navegador.test.tsx` |
| Las dos ventanas de la zona caben en 320 px, con su pareja | `LasVentanasDeLaZonaCabenEn320.navegador.test.tsx` |
| La del almacén: textos, opciones, avisos, el `PUT` entero con su `If-Match`, el `412` | `LaZonaDelAlmacen.test.tsx` |
| La caché invalidada, `['hoy']` incluida | `LaZonaDelAlmacen.test.tsx` |
| La de la empresa: tres opciones, su aviso y el `PUT` entero | `LaZonaDeLaEmpresa.test.tsx` |

La tanda de mutaciones del cierre del ítem, con sus dos listas, está en el PLAN y, al cerrarse la
fase, en `docs/historia/fase-3.md`.

## Procedencia

La ventana, del usuario, en el punto 7 de la puerta del 3.5 (2026-11-05). Lo demás lo decidió el
agente al escribirla, y está en el PLAN, *Tomadas por el agente de desarrollo — ítem 3.5*.
