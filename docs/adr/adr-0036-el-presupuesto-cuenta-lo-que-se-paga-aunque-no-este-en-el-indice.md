---
tipo: referencia
stack: [typescript, react, vite]
aplica_a: [entrega-continua, ux-ipo, frontend]
tags: [adr, presupuesto, rendimiento, carga-diferida, code-splitting, ci, vacuidad, adr-0028, adr-0020]
revisado: 2026-09-18
---

# ADR-0036: El presupuesto cuenta lo que se paga antes de pintar, aunque no esté en el índice

- **Estado:** aceptado
- **Fecha:** 2026-09-18
- **Enmienda** el [ADR-0028](adr-0028-el-presupuesto-mide-el-arranque-no-la-suma-de-los-fragmentos.md):
  sustituye su **definición de arranque**. Lo demás de aquel ADR —las dos métricas, los topes y su
  cálculo, los bytes en vez de bloques, y la disciplina de afirmar que se ha mirado algo— sigue
  entero y en pie.
- Se implementa **entre los ítems 2.1 y 2.2**, en su propio commit.

## Contexto

El ADR-0028 definió el **arranque** como «los ficheros referenciados por `index.html` (el módulo de
entrada, la hoja de estilo y cualquier `modulepreload`), más el propio `index.html`», y lo hizo
diciendo para qué: es «lo que el navegador pide **antes de poder pintar nada**». Las dos frases
describían el mismo conjunto, así que la segunda se podía medir con la primera.

El **ítem 2.1** las separó. El diccionario del idioma pasó a importación dinámica, el arranque lo
**espera** —`main.tsx` no monta nada hasta tenerlo— y Vite no lo anuncia en el `index.html`, porque
un `import()` que se resuelve en tiempo de ejecución no tiene sitio ahí. Desde ese día:

| | bytes | KiB |
|---|---:|---:|
| Lo que `index.html` referencia | 399 657 | **391** |
| Lo que el navegador descarga antes de pintar (castellano) | 418 129 | **409** |
| Tope | | **450** |

Dieciocho kilobytes que el usuario paga y que la medida no veía. Y el guion seguía diciendo, en su
propia cabecera, que lo que `index.html` referencia «es lo que se paga antes de pintar nada» — que
es exactamente la frase que dejó de ser cierta.

**No es un tope laxo: es un tope que ha dejado de vigilar una parte.** Y la parte que no vigila es
justo la que el ítem 2.1 acaba de crear, así que la primera regresión posible es deshacer el 2.1:
devolver el diccionario al fragmento de entrada deja lo que el usuario paga en los mismos 409, y
sube la cifra publicada de 391 a 409 **en verde**, porque 409 cabe bajo 450. El ahorro se habría
perdido entero sin que ningún paso se pusiera rojo.

## Decisión

### 1. Arranque = lo que el índice referencia **más** lo declarado

```text
arranque = index.html
         + lo que index.html referencia (entrada, hoja de estilo, modulepreload)
         + los fragmentos DECLARADOS que se pagan siempre antes del primer pintado
```

La primera parte se **lee** del artefacto. La tercera se **declara**, y la diferencia no es
comodidad: en `dist`, un fragmento diferido de una ruta y uno que el arranque espera son el mismo
tipo de fichero con el mismo tipo de nombre. No hay nada en el artefacto que los distinga, así que
deducirlo sería adivinar. Lo que sí se puede hacer es obligar a que alguien lo diga, y comprobar
después que lo dicho sigue siendo verdad.

### 2. La lista es cerrada y se compara **en los dos sentidos**

La declaración vive en `scripts/ci/presupuesto-del-frontal.sh`, junto a la regla que la usa. Y no se
compara contra sí misma: se compara contra la fuente que manda sobre cuántos idiomas hay, que es
`IDIOMAS` en `frontend/src/app/i18n/idioma.ts`.

- Un fragmento **declarado que no está** en `dist` es rojo. Es el caso de la regresión de arriba, y
  también el de un renombrado: sin esta dirección el guardián envejece apuntando a un fichero que ya
  no existe, y sigue verde.
- Un idioma **que está y nadie declaró** es rojo. Un tercer idioma entraría con su diccionario
  descargándose antes de pintar y sin que nadie lo contara.

### 3. Entre alternativas excluyentes se cuenta **la mayor**

Los diccionarios de idioma son alternativas: un usuario paga **uno**. Sumarlos mediría un caso que
no le ocurre a nadie; contar el menor prometería algo que no se cumple para la mitad. El tope es una
promesa sobre el **peor caso que alguien puede llegar a pagar**, así que se cuenta el mayor del
grupo.

Esto va **escrito en el guion**, y no implícito en un máximo, porque el día que entre un tercer
idioma más gordo que los de hoy, lo que significa el número cambiaría en silencio. Escrito, sube
solo y se ve por qué.

### 4. La parte nueva hereda la disciplina del ADR-0020 entera

El ADR-0028 ya hacía fallar el paso si el conjunto de arranque salía vacío, si no llevaba ningún
`.js` o si el arranque salía mayor que el total. La parte declarada añade tres afirmaciones más, y
hacen falta: **una enmienda que se midiera a sí misma midiendo la nada sería peor que no haberla
escrito**, porque el número volvería a ser el de antes con una explicación nueva encima. El paso
falla si la lista declarada está vacía, si alguno de sus miembros no resuelve en **exactamente un**
fichero, o si al terminar no se ha resuelto ni uno.

### 5. El tope se queda en **450**

La cifra publicada sube de 391 a 409 KiB **sin que el frontal engorde un solo byte**: lo que ha
cambiado es la medida. Quedan 41 KiB de margen real —margen de verdad, no el ficticio de antes—, y
mover el tope en el mismo commit que cambia lo que se mide sería cambiar dos cosas a la vez y no
poder atribuir la siguiente sorpresa a ninguna.

## Lo que este ADR NO es

**No es un cambio de criterio.** Es el criterio que el ADR-0028 ya escribió —«se mide lo que el
navegador descarga»— aplicado donde volvió a dejar de aplicarse solo. Es la tercera vez: la primera
fueron los `.map` (ítem 0.1), la segunda los fragmentos de ruta diferidos (ítem 1.1), y esta la de
los fragmentos que el arranque **espera**. Las tres, el mismo error con otra cara: contar lo que no
se descarga, o no contar lo que sí.

**Y no es una cifra peor.** 409 KiB es lo que el usuario paga hoy y pagaba antes del 2.1 —cuando
eran 426—, así que el ítem quitó un diccionario de verdad. Lo único que cambia aquí es que la cifra
publicada lo dice.

## Alternativas descartadas

**Dejarlo como estaba y anotar la diferencia.** Es lo que el ítem 2.1 dejó escrito en *Estado
actual*, como pregunta. Sostiene un número que la propia cabecera del guion contradice, y su
caducidad no se nota: el día que el margen se agote, el paso dejará pasar 18 kB de más sin avisar.

**Medir con un navegador sin cabeza y contar los bytes de red hasta el primer pintado.** Es la
medida exacta y no admite discusión. Trae un navegador entero a la CI, un número que cambia entre
ejecuciones y una dependencia que mantener, para un artefacto de veinte ficheros cuya composición
cabe en dos líneas declaradas.

**Deducir los fragmentos del `manifest.json` de Vite.** El manifiesto sí dice qué importa
dinámicamente el fragmento de entrada, pero no cuáles de esos se esperan antes de pintar: las rutas
diferidas están en la misma lista. Deduciría de más y convertiría el presupuesto de arranque en el
de total, que es de lo que venimos.

**Hacer estático el diccionario del idioma por omisión y diferir solo el otro.** Mediría bien sin
declarar nada, porque todo lo del arranque volvería al `index.html`. Y cuesta lo que el 2.1 acababa
de ganar a quien no lee castellano: pagaría los dos diccionarios. Además deja de escalar en cuanto
entra un tercer idioma.

## Consecuencias

- La cifra del paso pasa de **391/450** a **409/450**, con el desglose nombrando el fragmento
  contado y el grupo del que sale. El total servido no cambia: 593/900.
- **Deshacer la partición del ítem 2.1 es ahora un rojo**, y con mensaje. Comprobado por el efecto:
  devolviendo el diccionario al fragmento de entrada, el paso falla diciendo que el diccionario
  declarado `es` resuelve en cero ficheros. Esa misma mutación salía **verde** antes de este ADR.
- Lo que hasta hoy defendía esa partición era un comentario en `diccionarios.ts`. Sigue ahí, porque
  el rojo dice qué pasa y el comentario dice por qué, pero ya no está solo.
- Un idioma nuevo obliga a tocar el guion. Es deliberado: es el sitio donde se decide si su
  diccionario se paga en el arranque, y son dos líneas.
- El presupuesto de arranque queda acoplado a un fichero de `src/`. Es un acoplamiento con nombre y
  con rojo: si `IDIOMAS` se renombra o se mueve, el paso falla diciendo que el patrón ha dejado de
  casar, en vez de comparar contra una lista vacía y salir verde.
