---
tipo: referencia
stack: [dotnet, aspnetcore, http]
aplica_a: [api-rest, seguridad, rgpd]
tags: [adr, rgpd, lopdgdd, datos-personales, paginacion, cursor, idempotencia, r10, r11]
revisado: 2026-09-03
---

# ADR-0025: Un dato sensible no viaja en la URL — ni de ida, ni en el enlace de vuelta

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** aplica `herramientas/api-rest.md` y `herramientas/proteccion-datos.md` (que entra
  con la fase 1). Matiza el barrido de **ADR-0014** / `TodaEscrituraDiceComoSeProtegeTests`. Se
  implementa en el **ítem 1.3**.

## Contexto

El §15 pide para la fase 1 «listados paginados y **filtrados en servidor**». El primer filtro que un
usuario real va a querer sobre Terceros es **por NIF**, y un NIF es un dato personal de una persona
física identificada — de hecho el ejemplo que `proteccion-datos.md` usa para explicar por qué un
hash no anonimiza.

`herramientas/api-rest.md` es explícito: **un dato sensible no viaja en la cadena de consulta**. Los
motivos no son teóricos: la cadena de consulta se registra en el servidor y en cada proxy del
camino, se queda en el historial del navegador, viaja en el `Referer` a terceros, y aparece en la
analítica del cliente. Nada de eso es un incidente: es el funcionamiento normal de la web.

Hoy la decisión se toma **limpia**. Los listados solo llevan `?page=&size=` (`Paginacion`,
`ConsultaPaginada`), `PaginaDe<T>` **no devuelve enlaces**, y el vocabulario de filtrado **todavía no
existe**. No hay nada que deshacer.

## Decisión

**1. La búsqueda por criterio va en el cuerpo, no en la URL.** `POST /api/v1/{modulo}/{recurso}/buscar`,
con el criterio y la paginación dentro. El listado sin criterio sigue siendo un `GET` con `page` y
`size`, que no llevan nada personal.

**2. La comprobación no termina en la entrada.** La respuesta pagina con un **cursor opaco**, no con
una URL a la página siguiente. Un servidor que responde «la siguiente página está en
`…/buscar?nif=12345678Z&page=2`» ha vuelto a meter el dato en una URL él solo, y encima con su
firma: el cliente la seguirá, quedará en su historial y viajará en el `Referer`. **El cursor no
lleva el criterio en claro**, y esa es su razón de ser aquí; que además pagine mejor sobre conjuntos
grandes es una ventaja, no el motivo.

**3. La exención del barrido se escribe con el endpoint, no cuando el carril se ponga rojo.**
`TodaEscrituraDiceComoSeProtegeTests` reparte las acciones **por el verbo HTTP**
(`EsDeEscritura: POST, PUT, PATCH, DELETE`), así que `POST .../buscar` cae del lado de las que
cambian estado y el barrido pedirá `If-Match` o `Idempotency-Key` para una búsqueda. La respuesta
correcta es **una línea en `s_exentas` con su motivo**, y el motivo es que **la acción no cambia
estado pese al verbo**: repetirla no acumula nada, no hay recurso previo cuya versión citar, y
guardar su respuesta metería datos personales en `auditoria.claves_de_idempotencia`.

Lo que **no** se hace es ensanchar la partición del barrido para que las búsquedas dejen de contar
como escrituras. La partición por verbo es correcta y no tiene falsos negativos: prefiere pedir de
más y que cada excepción se argumente. Aflojarla para acomodar un caso es cambiar una lista de
excepciones razonadas por una regla que ya no distingue.

## Alternativas descartadas

**Mitigar en el proxy** (no registrar la cadena de consulta). Confía la protección a un componente
que no vive en este repositorio y que cualquier despliegue puede sustituir; y **no toca** el
historial del navegador, el `Referer` ni la analítica del cliente, que es la mitad del problema.

**`GET` con el criterio en la URL para todo menos el NIF.** Parte el listado en dos formas según qué
se filtre — la clase de asimetría que nadie recuerda seis meses después, y que hace que el siguiente
campo sensible (un IBAN, un teléfono) entre por el camino equivocado sin que nada avise.

**`GET` con el criterio en una cabecera.** Evita el registro por URL pero rompe la caché, no se puede
enlazar ni depurar, y sorprende a cualquier cliente — todo el coste de un `POST` sin su claridad.

## Consecuencias

- **Un `POST` que no crea nada choca con la lectura ingenua de REST.** Es el precio, se paga a
  sabiendas y queda escrito aquí para que nadie lo «arregle» dentro de tres meses. No es una
  invención: es lo que hacen las APIs que buscan sobre datos personales.
- **La respuesta de búsqueda no lleva enlaces**, así que el cliente pagina mandando el cursor que
  recibió. `PaginaDe<T>` ya no devuelve enlaces hoy, o sea que no hay que quitar nada.
- El vocabulario de filtro nace **con tope**: número máximo de criterios y de valores por criterio,
  por lo mismo que `TamanioMaximo = 200` existe en la paginación.
- La regla se aplica **a todo dato sensible**, no solo al NIF. El día que se busque por IBAN o por
  teléfono, el camino ya está y no hay que volver a decidir.
