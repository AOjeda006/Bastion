---
tipo: referencia
stack: [dotnet, efcore, postgresql, react]
aplica_a: [ddd, sql, inventario, concurrencia, api-rest]
tags: [adr, recuento, teorico, huella, ajuste, anulacion, inverso, cerrojo, paginacion, adr-0043, adr-0046, adr-0047, adr-0048, adr-0053]
revisado: 2026-10-06
---

# ADR-0055: El recuento congela el teórico al confirmar, y su ajuste es el que mueve

- **Estado:** aceptado
- **Fecha:** 2026-10-06
- **Sale del 2.12.** El encargo del 2026-10-05 fijó el recuento en nueve puntos (PLAN, *El 2.12: el
  recuento*). La puerta del 2.12 preguntó ocho cosas, y el usuario contestó el 2026-10-06: «todas
  (R), con cuatro precisiones» (PLAN, *La puerta del 2.12, contestada*). Las respuestas van en el
  punto 1 tal como las dio. Lo demás lo decide el agente, y lo dice el punto 13.
- **No enmienda ninguno.** El 2.5 no tiene ADR propio y ningún ADR dice nada de anular un inverso:
  `grep -n -i "inverso de un inverso\|inverso del inverso\|anular el inverso\|anular un inverso\|no se anula" docs/adr/*.md`
  solo encuentra la línea del ADR-0053 sobre el stock que ya salió. Este ADR lo deja escrito.
- **Aplica el orden de cerrojos del ADR-0046 §2 con los eslabones del ADR-0048**, y le añade un
  eslabón delante: la fila del recuento.

## Contexto

El recuento es el tercer documento del inventario, y el primero que **no escribe en el libro**. Una
persona cuenta lo que hay en un almacén, y al confirmar el sistema compara lo contado con lo que el
libro dice que hay. La diferencia la mueve un ajuste del 2.3, con su número y su doble flecha, que es
lo que el §15 llama «ajustes trazables».

Lo difícil no es la resta. Es **cuándo** se resta:

- contar un almacén entero lleva horas, y mientras tanto el almacén sigue vivo;
- si el teórico que se resta es el del momento en que se contó, una salida entre medias se
  convierte en stock que no existe;
- y si es el de la confirmación, el usuario tiene que haberlo visto antes de confirmar.

Un almacén entero son miles de claves, así que la ficha va paginada, y el usuario nunca ve todas las
líneas a la vez.

## Decisión

### 1. La puerta, contestada: todas (R), con cuatro precisiones

Va tal como la dio el usuario.

1. **(R) El almacén entero.** El recuento por ubicación, el recuento cíclico, va a la pregunta del
   cierre de fase, con su disparador: el primer almacén que no se pueda contar de una vez.
2. **(R) La unidad base.**
   - La consulta nueva va en el `Contracts` de Catálogo, por lotes, como `MarcasDeAsync`.
   - Lleva su caso: un artículo de otra empresa contesta lo mismo que uno que no existe.
   - Los tests de fronteras siguen en verde sin tocarlos.
   - La línea del ajuste que genera va en la base, con factor 1, y la pantalla dice en qué unidad
     se cuenta.
3. **(R) Las dos series en el alta**, validadas al abrir y otra vez al confirmar.
4. **(R) Se numera al confirmar.** Mientras está en curso, la pantalla lo nombra por su almacén y su
   fecha de apertura: con la 1 y la 7, no hay otro.
5. **(R) Un recuento en curso no cuenta para el ejercicio.** La consecuencia:
   - el ajuste lleva la fecha de la confirmación;
   - así que un recuento de fin de año confirmado en enero deja su ajuste en el año nuevo;
   - y no se puede fechar el 31/12, porque el teórico es el de la confirmación y el ADR-0047 lo
     rechaza en cuanto haya un movimiento posterior;
   - por eso **el recuento de cierre se confirma antes del primer movimiento del año nuevo**.
6. **(R) `Descartado`**, con su motivo, sin número y con el almacén libre. La acción es
   `/descarte`, con `Idempotency-Key` e `If-Match`.
7. **(R) Uno en curso por almacén**, con un índice único parcial traducido por su nombre a `409`.
   - Lleva la carrera de dos altas a la vez, con dos transacciones de verdad. Aquí la espera en el
     índice es lo que se prueba.
   - Lleva la mutación del índice sin su `WHERE`.
8. **(R) El inverso de un ajuste no se anula: `409`.** Va junto con «el ajuste de un recuento no se
   anula por separado», en `AnularAjuste`.
   - La anulación del recuento es el único camino que anula el ajuste de un recuento. Se prueban los
     dos caminos: el público, que da `409`, y el del recuento, que pasa. Cada uno con su mutación.
   - Los casos que hoy anulan un inverso cambian en el mismo commit que la regla. No hay ninguno:
     el generador de la propiedad solo anula originales (`Anulables` no recibe inversos), y ningún
     caso llama a la anulación con el identificador de un inverso.

Y las tres cosas que este ADR decide sin preguntar, que quedan como se propusieron:

- **Las acciones se nombran con un sustantivo**, como en el ajuste y en la transferencia:
  `POST /api/v1/inventario/recuentos/{id}/confirmacion`, `/anulacion` y `/descarte`.
- **El evento hacia el asiento de regularización** del §8.3 es el `AjusteConfirmado` del ajuste que
  genera, como el de cualquier otro ajuste. El recuento no publica uno propio hacia el asiento. Sus
  transiciones sí se cuentan, porque la R1 no deja transitar sin evento (`DocumentoBase`):
  `RecuentoConfirmado`, `RecuentoAnulado` y `RecuentoDescartado`. Ninguno lleva importes, así que
  nadie puede asentar con ellos.
- **El teórico que vio el usuario** viaja en la confirmación, y si ha cambiado se contesta un `409`
  con el actual. El punto 2 dice cómo viaja.

### 2. El teórico viaja como una huella de todas las líneas

La ficha va paginada, así que «el teórico que vio el usuario» no puede ser el de cada línea: el
cliente tendría que haber leído todas las páginas para mandarlas, y el cuerpo de la confirmación
serían miles de pares. **Viaja una huella.**

- **El teórico de una línea** es el físico de su clave ahora: `existencias.fisico` de su artículo,
  su almacén, su ubicación, su lote y su número de serie, o cero si la fila no existe. No es el
  disponible: las reservas son del 2.13, y un recuento cuenta lo que hay, no lo que está libre.
- **La huella** es el SHA-256, en hexadecimal, de las líneas del recuento ordenadas por su
  identificador, cada una escrita como `{id}:{teórico con seis decimales}` y terminada en `;`. La
  calcula una función pura del dominio, así que se prueba en el carril rápido y no hay dos
  implementaciones que puedan discrepar.
- **La ficha la devuelve** en su cabecera, con tres cuentas: las líneas sin contar, las que tienen
  el teórico cambiado desde que se contaron, y las que tienen tránsito hacia su clave.
- **La confirmación la lleva en el cuerpo** (`huellaDelTeorico`). Si la de ahora no es la misma, la
  respuesta es `409` `recuento-teorico-cambiado`, y en la extensión `actual` va la huella de ahora
  y las líneas cuyo teórico ya no es el de cuando se contaron, hasta cincuenta, con el total.

**Lo que la huella no cubre lo cubre la versión.** Las líneas que se añaden o se quitan, y lo
contado por otra persona, cambian el recuento, y cualquier escritura en una línea mueve la versión
de la cabecera (punto 4). La confirmación exige esa versión en `If-Match`. Así, la huella dice si el
mundo cambió, y la versión dice si el documento cambió: el `409` y el `412` de `api-rest.md`, cada
uno con su causa.

**Lo que ve el usuario de cada línea:** lo contado, el teórico de cuando se contó, el de ahora y la
diferencia entre los dos si la hay, y la diferencia que movería el ajuste. Un recuento contado antes
de una salida enseña esa línea marcada en la pantalla mucho antes de confirmar.

### 3. El orden de los cerrojos al confirmar

Es el del ADR-0046 §2 con los eslabones del ADR-0048, con la fila del recuento delante:

| Paso | Qué toma | Si falla |
|---|---|---|
| 1. La fila del recuento | `FOR NO KEY UPDATE`, antes de leerla | — |
| 2. Su versión y su estado | se comparan con la fila ya bloqueada | `412` · `409` |
| 3. Las líneas sin contar | — | `422` |
| 4. El ejercicio de hoy | compartido | `409` |
| 5. La marca de todos sus artículos | compartida | — |
| 6. El teórico, **sin cerrojo** | — | `409` si la huella no casa |
| 7. La forma de las líneas que se moverán | — | `409` |
| 8. El número del recuento | el contador de su serie | lo dice el numerador |
| 9. El número del ajuste, si hay diferencias | el contador de la otra serie | lo dice el numerador |
| 10. Las valoraciones de todas sus claves | en orden de clave | — |
| 11. El teórico, **con las valoraciones bloqueadas** | — | `409` si la huella no casa |
| 12. El tránsito de las líneas que suben | — | `409` |
| 13. El valor y el impedimento | — | `422` |
| 14. Lotes, series, documentos y libro | — | — |

**Por qué el teórico se lee dos veces.** El número del ajuste va antes que las valoraciones, como en
cualquier ajuste: al revés, un recuento y un ajuste de la misma serie se esperarían el uno al otro,
cada uno con lo que el otro necesita. Pero si hay ajuste o no lo dice el teórico, y el teórico que
decide se lee con las valoraciones bloqueadas. La primera lectura, sin cerrojo, solo sirve para
saber si hay que pedir el segundo número: si su huella no es la del usuario, la confirmación para
ahí, más barata. La segunda es la que decide las cantidades, y si su huella ya no es la del usuario,
la transacción se deshace con sus dos números.

**Por qué la fila del recuento va primero.** Dos confirmaciones del mismo recuento: la segunda
espera en el paso 1, y cuando sigue lee la versión que dejó la primera y recibe el `412`. Sin el
cerrojo llegaría hasta el paso 11, vería el teórico que movió el ajuste de la primera y contestaría
un `409` que no es su causa.

La mutación que lee el teórico antes de bloquear las valoraciones —que el paso 6 decida y el 11 no
exista— tiene que ponerse roja en la carrera con un ajuste sobre la misma clave.

### 4. Las líneas llevan su versión, y escribir en una mueve la de la cabecera

- **Contar** es `PUT /api/v1/inventario/recuentos/{id}/lineas/{lineaId}` con `If-Match` **de la
  línea**, y **quitar** una línea es `DELETE` sobre la misma ruta, con la misma cabecera. **Añadir**
  una clave es `POST …/lineas`, con `Idempotency-Key`.
- **Toda escritura en una línea bloquea antes la fila del recuento**, comprueba que siga en curso y
  la toca, así que su versión cambia. Dos personas pueden contar a la vez líneas distintas: se
  esperan en esa fila un instante, y ninguna recibe un `412` por lo que hizo la otra.
- **Confirmar, anular y descartar** llevan `If-Match` **de la cabecera**. Es la primera acción de la
  casa con los dos mecanismos, la versión y la clave: la clave es la del filtro, que es el dueño de la
  transacción cuando la acción numera.

### 5. La línea sin contar no es un cero

- **Al abrir se precargan** las claves del almacén con físico mayor que cero, sin contar.
- **Confirmar con una línea sin contar** es un `422` `recuento-con-lineas-sin-contar`, con el total
  y hasta cincuenta líneas en `actual`.
- **Una línea que no se va a contar se quita a propósito**, y su clave queda como está. El recuento
  solo dice algo de las claves que lleva.

### 6. Las claves nuevas, y su coste

- **Se pueden añadir** claves que el sistema no tiene. La ubicación tiene que ser del almacén del
  recuento, el artículo tiene que almacenarse, y el lote o el número de serie tienen que casar con
  su marca, como en el alta del ajuste.
- **El coste solo lo lleva una clave añadida**, y solo se usa si la línea sube. Si sube sin coste,
  entra al precio medio de su clave, y si no lo hay, la confirmación da el `422`
  `ajuste-entrada-sin-coste-ni-precio-medio`, que ya existe.
- **Un número de serie se cuenta en cero o en uno.**

### 7. El tránsito

- **La ficha enseña el tránsito de cada clave**, el de `existencias.en_transito` (ADR-0053 §1).
- **La confirmación rechaza una línea que sube en una clave con tránsito hacia ella**: `409`
  `recuento-sube-con-transito`, con las líneas en `actual`. Si la mercancía ya llegó y no se ha
  recibido, contarla y después recibirla la sumaría dos veces. Se confirma después de recibir.

### 8. El ajuste que genera, y su doble flecha

- **Un ajuste por recuento, solo con las líneas que difieren**: cantidad, lo contado menos el
  teórico; unidad, la base con factor 1; fecha, la de la confirmación; serie, la del alta; motivo,
  el del recuento; divisa, la del recuento, que es la base de la empresa al abrirlo.
- **Si todas cuadran, no hay ajuste** y el libro no se mueve. El recuento se confirma igual, con su
  número.
- **La doble flecha (R13):** el ajuste apunta a su recuento (`ajustes.recuento_id`, única entre los
  que no son nulos) y cada línea del recuento a la del ajuste que la mueve
  (`lineas_recuento.linea_de_ajuste_id`). El recuento no apunta a su ajuste: se encuentra por la
  flecha contraria, y así no hay un ciclo de claves ajenas entre las dos tablas.
- **Se confirma en la misma transacción que el recuento**, así que no queda ningún borrador suelto.

### 9. Anular el recuento es anular su ajuste

- **La anulación del recuento anula su ajuste**, en la misma transacción, con el inverso del 2.5.
  Si no tiene ajuste, solo cambia de estado: no hay nada en el libro que compensar.
- **El ajuste de un recuento no se anula por separado:** `409` `ajuste-de-un-recuento-no-se-anula`.
- **El inverso de un ajuste no se anula:** `409` `ajuste-inverso-no-se-anula`. Deshacerlo sería
  volver a hacer el ajuste, y eso es otro ajuste. Es la regla de la transferencia, que ya lo impedía.
- **Las dos guardas viven en el caso de uso público**, y el núcleo de la anulación lo comparten los
  dos caminos. El dominio lanza además en los dos casos, como invariante.

### 10. El numerador tiene su propia lista

`TipoDeDocumentoOrigen` es la lista de los documentos que **escriben en el libro**, y el recuento no
escribe. Ponerle un valor sería la casilla sin productor del ítem 1.10. Así que el numerador del
módulo se teclea con otra lista, `DocumentoQueNumera` —ajuste, transferencia y recuento—, y el
recuento numera en las series `RecuentoDeInventario`, que Organización tiene desde el 2.4.

### 11. El estado actual del conflicto viaja en `actual`

`api-rest.md` pide devolver el estado actual en el conflicto, y hasta ahora un `ErrorDeOperacion`
solo sabía llevar los errores por campo. Gana un dato opcional, que se publica como la extensión
`actual` del `ProblemDetails`. Lo usan el `409` del teórico, el del tránsito y el `422` de las
líneas sin contar.

### 12. La pantalla nombra lo que la ficha identifica

- **La ficha devuelve identificadores** de artículo, ubicación y unidad, como todo el módulo. El
  puerto de Catálogo no publica ni el código ni la descripción, y esto no lo cambia.
- **La pantalla los nombra preguntando a sus dueños** por su API pública
  (`/catalogo/articulos/{id}`, `/organizacion/ubicaciones/{id}`,
  `/organizacion/unidades-de-medida/{id}`), con caché, como dato maestro que son. Una
  funcionalidad no importa de otra: llama a la API.
- **La ruta va diferida**, como todas las del frontal.

### 13. Qué decidió el agente

- **los estados**: `EnCurso`, `Confirmado`, `Anulado` y `Descartado`, en un enumerado propio;
- **el índice**: `ix_recuentos_uno_en_curso_por_almacen`, sobre la empresa y el almacén, con
  `estado = 'EnCurso'`; y `ix_lineas_recuento_una_por_clave`, sobre el recuento, el artículo, la
  ubicación, el lote y la serie, con los nulos iguales, traducido a `409`
  `recuento-clave-repetida`;
- **el motivo en el alta**, obligatorio, porque es el que lleva el ajuste;
- **la fecha de apertura** es el día UTC del alta, y la de confirmación, el de la confirmación;
- **las líneas se ordenan por número**, que la precarga reparte por ubicación, artículo, lote y
  serie. La página se corta en el servidor sobre la lista entera, que el servidor ya lee para la
  huella; el filtro deja ver solo las sin contar o solo las de teórico cambiado;
- **la serie del ajuste no se consulta al confirmar si no hay ajuste**: no se escribe nada en ella;
- **lo que el cierre pregunta**: un recuento en curso no cuenta (punto 1.5), y uno confirmado o
  anulado cuenta por su fecha de confirmación, como documento;
- **los nombres**: el prefijo `recuento-` en los `type`, y los permisos `inventario.recuento.ver`,
  `…abrir`, `…contar`, `…confirmar`, `…anular` y `…descartar`.

## Consecuencias

- **El inventario tiene su primera pantalla**, `/recuentos`, con el listado y la ficha.
- **La migración de Inventario** crea las dos tablas del recuento y añade `ajustes.recuento_id` y
  `lineas_recuento.linea_de_ajuste_id`. Hoy no hay ningún recuento, así que no hay nada que
  rellenar.
- **El cuadre y la propiedad** ganan el recuento: tras confirmar, el físico de cada clave contada es
  lo contado, y el valor de la empresa solo lo mueven los ajustes, también los del recuento.
- **Lo que se deja abierto, con su disparador:**
  - el recuento cíclico: el primer almacén que no se pueda contar de una vez;
  - el recuento a ciegas, sin enseñar el teórico a quien cuenta: la primera auditoría que lo pida;
  - las pantallas del ajuste y de la transferencia: el cierre de la fase 2.

## Procedencia

Lo fijó el usuario en el encargo del 2026-10-05, con sus nueve puntos. En la puerta del 2026-10-06
contestó las ocho preguntas con las recomendaciones y cuatro precisiones, y aceptó las tres cosas que
este ADR decidía sin preguntar. Que la huella sea de todas las líneas lo pidió decidir aquí el mismo
encargo. Lo que decidió el agente está en el punto 13.
