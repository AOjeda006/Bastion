---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, concurrencia, transacciones]
tags: [adr, r9, r11, ejercicio, periodo, cerrojo, for-share, for-update, transacciones, adr-0041]
revisado: 2026-09-26
---

# ADR-0042: La escritura que espera no protege lo que ya se decidió, y por eso mover y borrar el ejercicio toman el cerrojo antes de preguntar

- **Estado:** aceptado
- **Fecha:** 2026-09-26
- **Amplía el [ADR-0041](adr-0041-el-cerrojo-del-ejercicio-lo-pide-una-lectura-cruda.md)** en dos
  sitios. En su §2 el exclusivo era el de cerrar; pasa a ser el de cerrar, mover y borrar. Y en su §8
  el orden «cerrojo antes de leer» compraba el error correcto y no la seguridad. En mover y borrar
  compra la seguridad, y aquí está por qué la diferencia no es un matiz.
- **Sale del cierre del ítem 2.6**, en su propia rama y a petición del usuario, antes de abrir el 2.7.

## Contexto

Cerrar, mover y borrar un ejercicio deciden por la misma pregunta: qué documentos tienen los módulos
con fecha dentro del intervalo. El ítem 2.6 serializó cerrar contra confirmar con un cerrojo
exclusivo sobre la fila del ejercicio, tomado antes de la pregunta. Mover y borrar se quedaron sin
él, y la intuición decía que no les hacía falta, porque ellos **sí escriben la fila**: su `UPDATE`
o su `DELETE` chocan con el `FOR SHARE` de una confirmación en vuelo, y esperan.

Esperan, sí. Y eso es lo que no protege nada.

## Decisión

### 1. La espera llega tarde

El orden de mover sin cerrojo es: leer la fila, preguntar a los módulos, decidir, escribir. La
pregunta es una lectura en `READ COMMITTED`, así que no ve lo que otra transacción está escribiendo
todavía. Si en ese momento hay un documento a medio confirmar dentro del intervalo, la respuesta es
«no hay nada». El `UPDATE` de después sí se topa con el `FOR SHARE` de esa confirmación y espera a
que suelte. Pero cuando suelta, el `UPDATE` **no vuelve a preguntar**: escribe lo que ya había
decidido con una respuesta vieja.

**Y la R11 no lo recoge por detrás**, que es lo que en el cierre sí pasaba (ADR-0041 §8). El testigo
de concurrencia es `xmin`, y un `FOR SHARE` escribe en `xmax` sin tocarlo: la versión que el cliente
trajo sigue valiendo después de la confirmación, así que el `UPDATE … AND xmin = testigo` encuentra
su fila y pasa. Es la misma medición que el ADR-0041 §2 usó a su favor —el cerrojo no mueve el ETag,
y por eso cerrar con `If-Match` no falla por confirmaciones ajenas— vista del otro lado.

### 2. El cerrojo va antes de la pregunta

Mover y borrar toman `ICerrojoDeEjercicios.TomarEnExclusivaAsync` **lo primero**, dentro de
`IUnidadTrabajoDeOrganizacion.EnTransaccionAsync`, igual que cerrar. El exclusivo espera a que
suelten los compartidos que ya estaban dentro, así que la pregunta a los módulos se hace cuando esas
confirmaciones ya han llegado a su `COMMIT` y ninguna nueva puede entrar.

### 3. El documento que lo distingue es uno que el puerto no puede ver

La carrera solo existe con un documento que **no esté a la vista** cuando mover pregunta. Un
borrador ya creado no sirve: el puerto de mover y borrar cuenta los documentos en cualquier estado,
así que lo ve, y un caso montado con él sale verde sin el cerrojo. Hoy el que sirve es **el inverso
de una anulación**: nace confirmado dentro de su propia transacción, que es la que toma el
`FOR SHARE`, y no existe para nadie hasta su `COMMIT`.

Con eso, los cuatro casos de `ElEjercicioRigeElAjusteTests` —mover y borrar, cada uno en los dos
órdenes— se reparten así, **medido contra el código sin el arreglo**:

| Caso | Sin el cerrojo | Con él |
|---|---|---|
| `Mover_el_ejercicio_espera_a_la_anulacion_que_ya_estaba_dentro_y_ve_su_inverso` | rojo: `200`, el inverso sin ejercicio | `409` |
| `Borrar_el_ejercicio_espera_a_la_anulacion_que_ya_estaba_dentro_y_ve_su_inverso` | rojo: `204`, el inverso sin ejercicio | `409` |
| `La_anulacion_espera_al_movimiento_que_ya_estaba_dentro_y_luego_lo_obedece` | verde | verde |
| `La_anulacion_espera_al_borrado_que_ya_estaba_dentro_y_luego_lo_obedece` | verde | verde |

Los dos verdes no son la prueba del arreglo, y se dice: en ese orden el `UPDATE` o el `DELETE` ya
tienen la fila cogida cuando la anulación pide el compartido, así que la anulación espera con
cerrojo o sin él. Están para que siga siendo así y para afirmar la otra mitad: que la anulación, al
soltarse, obedece lo que quedó escrito.

Los dos rojos **no llevan plazo de cerrojo**, al revés que los del cierre: lo que afirman no es que
mover espere —espera también sin el arreglo, en su `UPDATE`— sino lo que decide después de esperar.
Así que la petición corre entera, y el caso suelta la anulación solo cuando el motor dice que la
petición ya la está esperando (`pg_blocking_pids`). Soltarla antes convertiría la carrera en dos
operaciones seguidas, que salen bien sin cerrojo.

## Consecuencias

- **Mover y borrar un ejercicio esperan** a las confirmaciones que ya estaban dentro, como cerrar.
  Entre confirmaciones del mismo ejercicio no cambia nada: los compartidos conviven.
- **El criterio para el siguiente caso de uso que decida por lo que hay dentro de un periodo** es
  éste, y no «¿escribe la fila?»: si decide por una lectura de otro módulo, toma el exclusivo antes
  de leer. Que su escritura vaya a esperar después no cuenta.
- **Reabrir no lo toma**, y no por olvido: reabrir no pregunta a nadie qué hay dentro, y lo único que
  lee es el estado de la propia fila, que su `UPDATE` protege con la R11.

## Procedencia

La carrera la señaló el usuario al revisar el cierre del 2.6: mover y borrar no tomaban el cerrojo.
Que un borrador no la ejercía y que los órdenes «mover primero» ya estaban cubiertos lo midió el
agente antes de escribir el arreglo, y los rojos de la tabla se vieron contra el código de `main`.
La mutación que quita el cerrojo de mover, y lo que puso rojo, en el *Estado actual* de
`docs/PLAN.md`.
