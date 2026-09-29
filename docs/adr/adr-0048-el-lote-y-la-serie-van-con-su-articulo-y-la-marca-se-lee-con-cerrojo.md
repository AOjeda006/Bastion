---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, sql, inventario, catalogo, concurrencia, gs1]
tags: [adr, lote, numero-de-serie, trazabilidad, cerrojo, cruce-mutuo, adr-0042, adr-0046]
revisado: 2026-09-29
---

# ADR-0048: El lote y la serie van con su artículo, y la marca se lee con cerrojo

- **Estado:** aceptado
- **Fecha:** 2026-09-29
- **Sale del 2.9**, con las seis decisiones que el usuario fijó en el encargo del 2026-09-29 (PLAN,
  *El 2.9: lotes y números de serie*). Los medios, y lo que el encargo dejó por decidir, son del
  agente, y el punto 9 dice cuáles.
- **Enmienda el ADR-0046 §2**, su lista del orden de los cerrojos. Entran dos eslabones: la marca
  del artículo, después del ejercicio, y los lotes y las series, después de la valoración. El resto
  del orden no cambia.

## Contexto

El 2.9 le da al artículo su trazabilidad, `Ninguna`, `PorLote` o `PorNumeroSerie`, y hace que el
stock trazable se lleve por **(artículo, lote)** o **(artículo, serie)**. Hasta aquí, la existencia
tenía una columna `lote_id` siempre nula y sin clave ajena, y el libro no tenía ninguna.

Tres cosas del ítem no se arreglan después sin rehacer datos:

- la forma de la clave, en una tabla particionada y de solo añadido;
- la guarda de «un número de serie no está en dos sitios», que un `CHECK` de fila no ve;
- la carrera entre cambiar la marca y confirmar el primer movimiento, que es la del ADR-0042 con
  otros dos módulos.

## Decisión

### 1. Lote y serie van en dos columnas, aunque la marca sea excluyente

La marca tiene tres valores, y un artículo lleva lote **o** serie, nunca los dos. Lo acordó la
puerta de la fase, y se queda así por decisión del usuario.

Aun así, **la existencia y el libro llevan `lote_id` y `serie_id` por separado.**
`negocio/identificacion-articulos` pone como antipatrón una trazabilidad por serie que no pueda
guardar también el lote, o al revés. El esquema no cae en él: puede guardar las dos cosas. Es el
dominio el que hoy no deja escribirlas juntas.

- **La desviación**, respecto de la biblioteca, es solo del dominio, y es deliberada.
- **Su disparador:** el primer artículo que necesite las dos, por ejemplo un producto sanitario con
  lote y número de serie en su etiqueta. Ese día la marca gana un cuarto valor, que es un cambio
  del dominio. La clave del libro no se toca, y el libro está particionado y es de solo añadido.

### 2. El lote es (empresa, artículo, código), y la serie igual

- **Cada uno tiene su tabla**: `inventario.lotes` y `inventario.numeros_de_serie`, con índice único
  por `(empresa_id, articulo_id, codigo)` y `(empresa_id, articulo_id, numero)`. El código suelto no
  identifica nada: dos fabricantes pueden usar el mismo texto de lote.
- **La existencia y el libro los apuntan con clave ajena**, dentro del esquema. Las filas de lote y
  de serie no se borran nunca, así que la clave ajena no le quita nada al libro.
- **Se crean en la primera entrada**, con `INSERT … ON CONFLICT DO NOTHING` dentro de la transacción
  que confirma, y una lectura después trae sus identificadores. `DO NOTHING` y no `DO UPDATE`: la
  fila del lote no se escribe nunca, así que no hace falta bloquearla. La serialización la ponen la
  valoración y la existencia. Dos primeras entradas del mismo lote a la vez se ordenan solas en el
  índice único: la segunda espera, y cuando la primera confirma, su lectura ve la fila.
- **Una salida con un lote que no existe no tiene camino propio.** Pasa por la misma sentencia, que
  crea el lote, y la guarda del stock la para con el `422` `stock-insuficiente`, porque la fila de
  la existencia de ese lote está a cero. El `ROLLBACK` se lleva el lote.
- **El borrador guarda el código, y el libro, el identificador.** La línea de un ajuste lleva el
  texto que escribió el usuario, y la confirmación lo resuelve.

**El límite de 20 caracteres de GS1 es regla de negocio, y también su juego de caracteres.** Un lote
o un número de serie tiene de 1 a 20 caracteres del conjunto 82 de GS1. Es lo que admiten el AI 10
(lote) y el AI 21 (serie), los dos con formato `X..20` y la expresión `[!%-?A-Z_a-z\x22]{1,20}`.

- **El motivo:** estos códigos son los de la etiqueta. El GTIN del 2.10 y el analizador de GS1-128,
  cuando llegue, los leen de un código de barras, donde no cabe más. Un lote que el sistema admite y
  no se puede codificar aparece al imprimir la primera etiqueta o al mandar el primer aviso de
  expedición, con los datos ya guardados.
- **Se empieza estricto porque es lo reversible.** Aflojar después es ensanchar una columna y quitar
  una validación. Apretar después es limpiar datos sucios.
- **Se recortan los espacios de los extremos, y nada más.** El espacio no está en el conjunto 82, así
  que uno de dentro se rechaza.
- **No se pasa a mayúsculas**, al revés que el código de artículo. El conjunto 82 tiene las dos cajas
  y GS1 las distingue: `a1` y `A1` son dos lotes distintos del proveedor, y juntarlos mezclaría su
  trazabilidad.
- **La fuente:** el conjunto de datos oficial de los AI,
  <https://ref.gs1.org/ai/GS1_Application_Identifiers.jsonld> (entradas `10` y `21`), y las
  *GS1 General Specifications*, figura 7.11-1, para el conjunto 82. Consultados el 2026-09-29.

### 3. «Un número de serie no está en dos sitios» lo sostiene el motor

Un `CHECK` de fila no ve las demás filas, así que hacen falta dos piezas sobre la existencia:

- **`ck_existencias_serie_como_mucho_una CHECK (serie_id IS NULL OR fisico <= 1)`.** El encargo
  decía `BETWEEN 0 AND 1`, y es lo mismo. El límite de abajo ya lo pone
  `ck_existencias_fisico_no_negativo`. Repetirlo haría que las dos restricciones saltaran a la vez
  con un −1, y PostgreSQL las comprueba en orden alfabético de nombre. Qué error recibe el usuario
  dependería de cómo se llaman.
- **`ix_existencias_serie_en_un_sitio`**, un índice único parcial `(empresa_id, articulo_id,
  serie_id) WHERE fisico > 0`.

Con las dos sentencias del 2.8, el índice se comprueba en el `UPDATE` que suma. La perdedora espera
a la ganadora y recibe un `23505`.

**Se traduce por su nombre, con el mecanismo del ADR-0046 §4**, a un `422`
`numero-de-serie-en-existencias`. No es el `412` de la carrera perdida: quien llega segundo no tiene
nada que recargar. La serie ya está dentro, y reintentar da lo mismo.

- **La lista cerrada de `RestriccionesQueGuardanUnaRegla` pasa a admitir `23505`**, además de
  `23514`, cada una con su nombre. Un nombre no puede estar a la vez en esa lista y en la de
  `IndicesQueDelatanUnaCarreraPerdida`, y un barrido lo comprueba.
- **El `CHECK` se traduce al mismo error.** Salta cuando la misma serie entra dos veces en la misma
  ubicación, y el índice cuando entra en otra.

**Una línea con serie mueve ±1 en unidad base.** Y **una serie sale una sola vez por documento.** El
índice único se comprueba fila a fila, no al final de la sentencia, así que mover una serie de una
estantería a otra dentro del mismo `UPDATE` chocaría o no según en qué orden recorriera las filas el
motor. Un ajuste dice que una serie apareció o desapareció, y la reubicación es otro documento.
Un índice parcial no puede ser una restricción diferible, que es lo que lo arreglaría en el motor.

**Las dos cosas del motor en que se apoya este punto**, en la documentación de `CREATE TABLE` de
PostgreSQL 17 (<https://www.postgresql.org/docs/17/sql-createtable.html>, consultada el 2026-09-29):
los `CHECK` de una tabla «se comprueban en cada fila en orden alfabético de nombre», y una unicidad
no diferible se comprueba «inmediatamente cada vez que se inserta o modifica una fila», no al final
de la sentencia (apartado *Non-Deferred Uniqueness Constraints*).

**El caso:** la misma serie entra en dos almacenes a la vez, con dos transacciones de verdad. Una
confirma, y la otra recibe el `422`.

### 4. Cambiar la marca con el primer movimiento en vuelo es la carrera del ADR-0042

Catálogo pregunta «¿tiene movimientos?» y después cambia, mientras Inventario confirma contra la
marca vieja. Se cierra como el cierre del ejercicio:

- **El cambio toma la fila del artículo en exclusiva y pregunta después**, dentro de una transacción
  de Catálogo. Pide `FOR NO KEY UPDATE` y no `FOR UPDATE`, porque es el cerrojo que toma el propio
  `UPDATE` del artículo, y ya choca con el `FOR SHARE` de Inventario. `FOR UPDATE` bloquearía además
  las claves ajenas que apuntan al artículo, como una línea de tarifa nueva, y el cambio de marca no
  tiene nada que decir sobre ellas.
- **La pregunta va por un puerto de Inventario hacia Catálogo.** `Inventario.Contracts` publica
  `IMovimientosDeArticulos.TieneMovimientosAsync(Guid)`, Inventario la contesta y Catálogo la
  consume. Un borrador no es un movimiento: la marca se puede cambiar con borradores pendientes.
- **La confirmación lee la marca con `FOR SHARE`** desde su propio adaptador, con el contexto de
  Inventario y en su transacción, como el ejercicio en el ADR-0041. El puerto,
  `IConsultaDeTrazabilidad`, lo publica `Catalogo.Contracts` y lo implementa
  `Inventario.Infrastructure`, con SQL crudo sobre
  `catalogo.articulos`. La empresa se compara a mano, y la tabla y la columna se comprueban contra el
  modelo. La sentencia entra en la lista cerrada de `ElFiltroNoSeSaltaPorAhiTests`.
- **La confirmación valida sus líneas contra lo que leyó.** Un borrador con líneas que ya no casan
  con la marca se rechaza al confirmar, con el `409` `ajuste-trazabilidad-no-casa`, que dice qué
  línea y qué falta o sobra. Abrir el ajuste hace la misma comprobación sin cerrojo, que es la
  cortesía. La guarda es la de confirmar.
- **Anular no lee la marca.** El original tiene movimientos, así que su marca ya no puede cambiar.

**El orden de los cerrojos**, que enmienda el ADR-0046 §2:

1. el ejercicio, `FOR SHARE`;
2. **la marca de los artículos del documento, `FOR SHARE`**;
3. el contador de la serie;
4. la valoración;
5. **los lotes y las series, `INSERT … ON CONFLICT DO NOTHING`**;
6. la fila del documento;
7. las filas de la existencia.

- **La marca va antes del contador** porque puede esperar a un cambio de Catálogo, y porque puede
  rechazar. El cerrojo del contador serializa todas las confirmaciones de una serie de numeración,
  así que todo lo que espera o rechaza va delante, para que la fila más disputada se tenga el menor
  tiempo posible.
- **Los lotes van después de la valoración** porque la existencia los necesita y la valoración no:
  el lote no entra en su clave (punto 5). Van en una sola sentencia, en orden de código, que es lo
  que impide el interbloqueo entre dos documentos con los mismos lotes nuevos.
- **Catálogo no puede cerrar un ciclo.** Toma una sola fila, la del artículo, y su pregunta a
  Inventario es una lectura sin cerrojos.

**Los casos:** los dos órdenes, con dos transacciones de verdad. Si Catálogo bloquea antes, la
confirmación espera, lee la marca nueva y se rechaza. Si la confirmación bloquea antes, el cambio
espera, y su pregunta ve el movimiento ya confirmado y contesta el `409`
`articulo-trazabilidad-con-movimientos`. **Las mutaciones:** preguntar antes de tomar el cerrojo, y
leer la marca sin `FOR SHARE`.

### 5. El inverso copia el lote y la serie

La línea inversa copia los dos códigos, y la confirmación los resuelve a las mismas filas, porque la
clave es la misma.

- **Anular la entrada de una serie que ya salió es el `422` de la excepción de la R2** (ADR-0046
  §1): el inverso dejaría la serie en −1, y la guarda del stock lo para.
- **La clave de valoración no cambia.** Es empresa, artículo y almacén (ADR-0046 §3), y el lote no
  entra: un precio medio por lote sería el coste de cada lote, que es FIFO. La serie tampoco.

### 6. La propiedad y el cuadre se extienden a la clave trazable

- **El cuadre** agrupa por artículo, almacén, ubicación, lote y serie, y compara la existencia con el
  libro en cada una de esas claves.
- **La propiedad** genera artículos con las tres marcas, y líneas con lotes y series. Su modelo
  rechaza lo que el sistema tiene que rechazar, y su invariante gana que ninguna serie tenga más de
  una unidad ni esté en dos sitios.

### 7. El segundo cruce mutuo

`Catalogo.Application` referencia `Inventario.Contracts`, por la pregunta del punto 4.
`Inventario.Application` ya referenciaba `Catalogo.Contracts`, y ahora también
`Inventario.Infrastructure`, por el adaptador de la marca. Es el segundo cruce mutuo del proyecto,
después del de Catálogo y Terceros, y se declara en los tests de arquitectura con su motivo.

- **Los dos `Contracts` no se ven entre sí.** Si se vieran, sería un ciclo de proyectos.
- **Por los puertos cruzan solo `Guid`, primitivos y los enumerados que publica el propio
  `Contracts`**, como `AptitudParaMoverExistencias`. Nada del dominio de nadie.

### 8. Lo que no cambia

- **La clave de valoración y `ultima_fecha`** (ADR-0047). La fecha se mira por artículo y almacén,
  sin lote.
- **La guarda del stock**, que es la de la existencia, ahora por lote y serie.
- **El resto del orden de los cerrojos.**

### 9. Qué decidió el usuario y qué el agente

Las seis decisiones, sus casos y sus mutaciones las fijó el usuario en el encargo. El agente decidió:

- que el límite de GS1 es regla, con su juego de caracteres y sin cambiar la caja;
- el `CHECK` de la serie solo por arriba;
- que una serie sale una vez por documento;
- `FOR NO KEY UPDATE` en lugar de `FOR UPDATE`;
- que los lotes van después de la valoración;
- y que `23505` entra en la lista de las reglas guardadas por la base.

## Consecuencias

- **La migración de Inventario** crea las dos tablas, añade `serie_id` a la existencia, `lote_id` y
  `serie_id` al libro, y el código de lote y de serie a la línea del ajuste. También pone las claves
  ajenas, el `CHECK`, el índice parcial, y la serie en el índice de la clave de la existencia. Hoy no
  hay ningún lote ni ninguna serie, así que no hay nada que rellenar.
- **La de Catálogo** añade la marca a todos los artículos como `Ninguna`, que es lo que eran.
- **El formulario del artículo** gana la marca, con el error del servidor en su campo y en los dos
  idiomas.
- **Un artículo con movimientos ya no cambia de marca.** Si se equivocó, el camino es otro artículo.
- **Lo que se deja abierto:** la caducidad del lote, que no pide ningún ítem de la fase. Su
  disparador es el primer artículo con fecha de caducidad.

## Procedencia

Lo fijó el usuario en el encargo del 2026-09-29:

- las dos columnas;
- la clave con el artículo;
- el `CHECK` y el índice parcial;
- la carrera de la marca, con sus casos y sus mutaciones;
- el inverso;
- la propiedad y el cuadre.

Lo que decidió el agente está en el punto 9. Las fuentes de GS1 y la de PostgreSQL se consultaron el
2026-09-29.
