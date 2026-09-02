---
tipo: referencia
stack: [typescript, react, testing]
aplica_a: [frontend, api-rest, testing]
tags: [adr, contrato, openapi, asercion-de-tipo, mutacion, msw]
revisado: 2026-09-02
---

# ADR-0019: La aserción de tipo es el punto ciego del contrato, y necesita un test que la fije

- **Estado:** aceptado
- **Fecha:** 2026-09-02
- **Relacionado:** completa el ADR-0018 por el lado del consumidor. Salió de la sexta mutación del
  ítem 0.11.

## Contexto

El ADR-0018 dejó el contrato generado, versionado y comprobado por la CI: `docs/api/openapi.json`
sale de la API, `src/shared/api/esquema.ts` sale del documento, y un paso del flujo vuelve a generar
los dos y falla si difieren. La regla que lo acompaña es **nunca se escribe a mano un tipo del
contrato**, y la pregunta que este ADR contesta es quién la hace cumplir.

La respuesta cómoda —«el compilador»— es cierta casi siempre y por eso engaña. Se comprobó
escribiendo a mano un tipo del contrato en los tres sitios donde cabe hacerlo:

1. **En un traductor de funcionalidad** (`features/*/api/consultas.ts`). El valor llega tipado por
   `openapi-fetch`, así que el tipo generado tiene que ser asignable al escrito a mano. No lo es, y
   `tsc` lo dice donde ocurre, con el campo que falta por su nombre.
2. **En la traducción de sesión** (`shared/api/traduccion.ts`, la función que recibe el DTO ya
   tipado). Lo mismo, y además se caen tres tests.
3. **Detrás de una aserción de tipo.** Aquí no dice nada nadie.

El tercer caso no es hipotético ni evitable: la renovación de sesión va con `fetch` pelado —el
cliente tipado no puede renovarse a sí mismo sin morderse la cola, porque su propio 401 dispararía
otra renovación— y el cuerpo de esa respuesta llega como `unknown`. Convertirlo exige un `as`, y
**un `as` es exactamente la instrucción de dejar de comprobar**. Con `token` escrito donde el
contrato dice `tokenDeAcceso`: `tsc` limpio, `eslint` limpio, y cada sesión recuperada de la cookie
sale con el testigo en `undefined`.

## El hallazgo que importa no es que no se cazara

Los tests **sí** se ponían rojos. El problema era **cómo**: se caían dos listados —el del selector de
empresa y el del botón de reintento— por agotar su plazo de espera cinco segundos después, en tests
que no hablan de sesiones ni de testigos. Ese es el rojo que se archiva como «test intermitente» y
se vuelve a lanzar. Y los cuatro tests que sí hablaban del testigo seguían verdes, porque sus
manejadores simulados devolvían lo mismo mirasen o no la cabecera.

Un fallo que aparece lejos de su causa, tarde, y en un test cuyo nombre no tiene nada que ver, es
peor que un fallo que no aparece: el que no aparece se descubre en producción una vez; el que
aparece mal enseña al equipo a desconfiar de la suite entera.

## Decisión

**Toda aserción de tipo sobre un cuerpo de respuesta lleva un test que fije el campo del contrato
que se está leyendo, y ese test mira el efecto observable, no el valor intermedio.**

En concreto, para la renovación de sesión: un test comprueba que la cabecera `Authorization` que
sale hacia el servidor es exactamente `Bearer <el testigo del contrato>`. No comprueba que
`traducirCuerpoDeSesion` devuelva un objeto con la forma esperada —eso lo daría por bueno un doble
mal escrito—, sino qué acaba viajando por la red.

Tres consecuencias que se siguen de la decisión:

- **Las aserciones se cuentan.** `shared/api/traduccion.ts` tiene una, con su motivo escrito encima.
  Si aparece una segunda, aparece con su test.
- **El servidor simulado responde SEGÚN LA CABECERA**, no siempre lo mismo. Un manejador que
  devuelve la misma página mire quien mire es un manejador que no puede notar un testigo roto. El de
  almacenes decide qué filas devuelve a partir del `Authorization` que recibe, igual que la API de
  verdad decide a partir del inquilinato que va dentro del token.
- **Un test cuyo único modo de fallo es el plazo agotado está a medio escribir.** Sirve, pero no
  diagnostica; y cuando es el único que cubre algo, ese algo está peor cubierto de lo que parece.

## Alternativas descartadas

**Validar el cuerpo con Zod en vez de asertarlo.** Es la respuesta ortodoxa y resuelve el problema
de verdad: un esquema de Zod sí comprueba en ejecución. Se descarta **por ahora** y con fecha de
caducidad, no por principio. El esquema habría que escribirlo a mano, que es exactamente lo que este
ADR persigue: sería un tipo del contrato tecleado, con la ventaja de que falla ruidosamente y el
inconveniente de que hay que mantenerlo sincronizado a mano. Cambiar una fuente de verdad duplicada
silenciosa por una ruidosa es una mejora, pero sigue siendo duplicada. **Cuando haya un generador de
esquemas de Zod desde el OpenAPI en el que se confíe** —o cuando las aserciones dejen de ser una—,
se revisa: entonces el esquema se genera y deja de estar escrito a mano.

**Prohibir la aserción y hacer que la renovación pase por el cliente tipado.** No se puede sin
romper otra cosa: el cliente reintenta al recibir un 401, así que renovarse a través de él es un
bucle. La alternativa sería un segundo cliente tipado sin reintento, y eso son dos clientes con dos
configuraciones que hay que mantener iguales —otra fuente de verdad duplicada, en otro sitio—.

**Dejarlo como estaba, apoyándose en que los tests se caían.** Se descarta por lo dicho: se caían
mal. Y el coste de arreglarlo fue una línea.

## Consecuencias

- La mutación «escribir a mano un tipo del contrato» pasa de fallar en cinco segundos en dos tests
  ajenos, a fallar en 194 ms en el test que lleva el nombre de lo que se ha roto.
- Queda escrito dónde está el punto ciego, que es lo que importa el día que alguien añada la segunda
  aserción: no es un descuido de este código, es una propiedad de `as` en TypeScript.
- La decisión de Zod queda tomada como pendiente, con la condición que la desbloquea escrita. No es
  un `TODO`: está aquí y en *Notas / riesgos* de `docs/PLAN.md`.
