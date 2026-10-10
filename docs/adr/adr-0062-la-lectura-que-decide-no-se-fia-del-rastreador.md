---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [concurrencia, testing, inventario]
tags: [adr, efcore, change-tracker, identity-resolution, cerrojo, reserva, mutaciones, testigo, adr-0059]
revisado: 2026-10-10
---

# ADR-0062: La lectura que decide no se fía del rastreador, y su prueba lleva un testigo que nadie relee

- **Estado:** aceptado
- **Fecha:** 2026-10-10
- **Sale de la revisión del 2.13**, de su hallazgo 8: `ObtenerPorOrigenAsync` rastreaba, y si el
  contexto ya tenía la reserva la devolvía sin releerla. Lo arregla `3517919`. La segunda mitad, la
  de las pruebas, sale de la tanda del mismo ítem: lo endurece `988ff44`.
- **Precisa el [ADR-0059](adr-0059-lo-reservado-se-suma-al-leer-bajo-el-cerrojo-de-la-valoracion.md)**
  sin enmendarlo: el cerrojo de la reserva sigue siendo la fila de la valoración de su clave, y la
  lectura que va detrás sigue siendo la que decide. Este ADR dice qué tiene que hacer esa lectura
  para decidir con lo que hay en la base.

## Contexto

**Una consulta con rastreo devuelve la instancia que el contexto ya tenía, con sus valores.** Lo
dice la documentación de EF Core, en *Tracking vs. No-Tracking Queries*: «If EF Core finds an
existing entity, then the same instance is returned […]. EF Core doesn't overwrite current and
original values of the entity's properties in the entry with the database values.» La sentencia sí
va a la base, y espera al cerrojo si lo hay; lo que lee se tira.

**Así que un cerrojo no basta si el contexto vive más que una operación.** En la API cada petición
abre su ámbito y la lectura es la primera. Pero un ámbito que hace varias operaciones —el módulo
cableado a mano de los tests, Ventas en proceso desde la fase 4, un trabajo en segundo plano— carga
la reserva en la primera y, en la segunda, toma el cerrojo y lee lo de antes. Con una liberación de
otra transacción por medio, consumía una reserva liberada. El caso que lo ve es
`La_reserva_se_lee_despues_del_cerrojo_aunque_el_modulo_ya_la_tuviera`, con la semilla 854.

**Y la relectura que lo arregla tiene un efecto en las pruebas.** Suelta la instancia que el
contexto tenía, con lo que una operación anterior le hubiera cambiado sin guardar. Si una operación
rechazada deja algo en el rastreador —las caducadas liberadas antes de decidir, el hallazgo 5—, la
relectura de la siguiente se lo lleva, y la prueba que mira si ese resto llega a la base sale verde.

## Decisión

### 1. La lectura que decide suelta antes lo que el contexto tenía de esa clave

`ObtenerPorOrigenAsync` busca en el rastreador la reserva de ese origen, suelta sus consumos y la
suelta a ella, y después lee. La lectura trae el agregado entero, porque los consumos son
`AutoInclude`.

- **Solo esa.** Las demás que rastree el contexto no deciden nada en esa operación, y soltarlas
  tiraría lo que otra parte del mismo caso de uso les hubiera hecho.
- **No `AsNoTracking`.** La reserva que se lee es la que se cambia y se guarda.
- **No `ReloadAsync`.** Recarga los valores de las propiedades de la entidad, según su referencia,
  y no los consumos, que son una colección: un consumo de otra transacción no aparecería.

### 2. Rastrear sí vale donde lo viejo en memoria no puede decidir otra cosa

`CaducadasDeLaClaveAsync` rastrea, y puede devolver una instancia que el contexto ya tenía. No es el
mismo caso:

- **la base filtra**: la consulta pide las guardadas activas y vencidas, así que solo vuelve una
  fila que la base tiene activa;
- **el estado solo avanza**: una reserva no vuelve a activa, así que una instancia cargada antes
  también estaba activa;
- **lo que se escribe sale de lo inmutable**: la liberación por caducidad pone el estado, la causa y
  la fecha de la caducidad, que no cambia, y no toca los consumos.

La regla se queda en eso: **una lectura con rastreo detrás de un cerrojo se justifica por escrito**,
con las tres razones o con las que valgan, o suelta antes lo que el contexto tenía.

### 3. La prueba de que un rechazo no deja nada lleva un testigo que nadie relee

`La_caducidad_suelta_sola_y_la_escribe_la_siguiente_escritura_que_sale` mira que un rechazo no deja
las caducadas liberadas en el rastreador. Desde `988ff44`:

- **detrás de cada rechazo va una escritura de otra clave**, con el mismo módulo, que es la que
  guardaría el resto;
- **hay una segunda caducada en la clave, la vecina**, que no es la del origen y no la relee nadie.
  Si las caducadas se liberaran antes de releer el origen, la relectura limpiaría la del origen y
  solo la vecina lo delataría;
- **cada aserción dice cuál de las dos mira**, para que el rojo diga cuál fue.

En general: cuando el código relee lo que una prueba vigila, la prueba vigila además algo que no se
relee.

## Medido

Con el caso solo, `dotnet test tests/Api.IntegrationTests --filter
"FullyQualifiedName~La_caducidad_suelta_sola_y_la_escribe_la_siguiente_escritura_que_sale"`, sobre
el código de `fdaf42f`, que `988ff44` no toca, con la prueba de entonces y con la de `988ff44`:

| Mutación | La prueba de antes | La de `988ff44` |
|---|---|---|
| 430: reservar libera las caducadas antes de mirar el disponible | verde | rojo, al reservar 11 |
| 431: consumir las libera antes de mirar la reserva | rojo | rojo, al consumir |
| 432: liberar las libera antes de mirar la reserva | verde | rojo, al liberar |
| 439: liberar las libera antes de releer el origen | verde | rojo, al liberar, y por la vecina |
| 440: consumir las libera antes de releer el origen | verde | rojo, al consumir, y por la vecina |

La 432 salía verde por la relectura: la prueba de antes escribía una sola vez, detrás de liberar y
de consumir, y la relectura de consumir se llevaba lo que liberar había dejado. La 430, porque no
había ningún rechazo de reservar. Y la 433, que quita
la relectura, solo la ve `La_reserva_se_lee_despues_del_cerrojo_aunque_el_modulo_ya_la_tuviera`.
Las cinco y la 433 están en la tanda del 2.13, en el PLAN, con sus listas por nombre.

## Consecuencias

- **Un ámbito que hace varias operaciones decide con lo que hay en la base**, y no con lo que cargó
  la operación anterior.
- **Para otro proyecto con EF Core**: el cerrojo protege la fila de la base, no la instancia del
  contexto. La lectura que decide detrás de un cerrojo, si el contexto puede haberla cargado antes,
  suelta antes lo que tenía o lee sin rastreo. Y una prueba que vigila lo que el código relee
  necesita un testigo que no se relea.

## Procedencia

Lo encontró la revisión del 2.13, con `/code-review high 2.13-las-reservas` sobre `9b4ba9a`, en su
hallazgo 8. Lo decide el agente, y el usuario no lo ha visto todavía.
