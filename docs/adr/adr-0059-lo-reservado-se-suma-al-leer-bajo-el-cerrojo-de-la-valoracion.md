---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, sql, inventario, concurrencia]
tags: [adr, reserva, disponible, caducidad, cerrojo, consumo, albaran, r1, r12, r13, adr-0044, adr-0046, adr-0048, adr-0053, adr-0055]
revisado: 2026-10-09
---

# ADR-0059: Lo reservado se suma al leer, bajo el cerrojo de la valoración, y el consumo escribe su salida

- **Estado:** aceptado
- **Fecha:** 2026-10-09
- **Sale del 2.13.** El encargo del 2026-10-08 fijó las reservas hasta su puerta (PLAN, *El 2.13:
  las reservas y el disponible, hasta su puerta*). La puerta preguntó ocho cosas, y el usuario
  contestó el 2026-10-09: «todas (R), con cinco precisiones» (PLAN, *La puerta del 2.13,
  contestada*). Las respuestas van en el punto 1 tal como las dio. Lo demás lo decide el agente, y
  lo dice el punto 12.
- **Enmienda el §8 del
  [ADR-0044](adr-0044-la-existencia-se-mueve-en-la-sentencia-que-anota-el-libro.md)**:
  `existencias.disponible` deja de ser una columna que calcula el motor, y `existencias.reservado`
  deja de existir. La lista de calculadas que el ADR-0044 abrió en el ADR-0015 se queda vacía, y se
  queda: la siguiente columna calculada entra declarándose, sin reabrir nada.
- **No añade ningún eslabón al orden de cerrojos** del ADR-0046 §2, con los del ADR-0048 y del
  ADR-0055 §3: el cerrojo de una reserva es la fila de la valoración de su clave (punto 2).

## Contexto

Una reserva aparta stock de un almacén para la línea de un pedido. Lo apartado no sale todavía,
pero ya no está libre: **el disponible es el físico menos lo reservado** (§7.4). Desde el 2.7 la
fila de existencias llevaba una columna `reservado`, que valía cero en todas las filas porque nada
la escribía, y un `disponible` que calculaba el motor.

Lo difícil es lo de siempre en este módulo, **la concurrencia**:

- dos pedidos a la vez por las últimas unidades no pueden llevárselas los dos;
- una transferencia no puede sacar del almacén lo que un pedido ya tiene apartado;
- y una reserva caduca sola, sin que nadie haga nada, sin un proceso que la libere.

Y la llamada es de otro módulo que todavía no existe. La reserva la pide un pedido de venta (§7.6) y
la consume un albarán, los dos de la fase 4. En el 2.13 el estado se sabe producir, y **lo que falta
es el llamante**.

## Decisión

### 1. La puerta, contestada: todas (R), con cinco precisiones

Va tal como la dio el usuario.

1. **(R) Lo reservado se suma al leer**, de las reservas activas y vigentes.
   - El ADR dice si la fila de cerrojo es la de la valoración. Tiene la misma clave —empresa,
     artículo y almacén— y ya la bloquea cada movimiento con `INSERT … ON CONFLICT` (ADR-0046 §2).
   - Si es una fila nueva, dice por qué, en qué eslabón del orden va (ADR-0046, 0048 y 0055 §3) y
     por qué una reserva y un envío de la misma clave no se interbloquean.
   - **La lectura de las existencias sigue dando «reservada» y «disponible»**, como pide el §7.4:
     la proyección es esa lectura.
   - Las columnas se quitan con su migración, y el censo de lo que genera el servidor pierde
     `Existencia.Disponible`.
2. **(R) El ajuste, el recuento y las anulaciones no se frenan.** La transferencia da `422` bajo el
   cerrojo.
3. **(R) `caduca_el` es un instante opcional, y no hay proceso periódico.**
   - **El estado de una reserva se lee siempre con una función que aplica el reloj**: nadie lee
     «Activa» a secas.
   - **Una reserva caducada que se libera lleva como fecha de liberación `caduca_el`**, no el
     momento en que se nota.
   - El ADR dice qué escrituras la liberan. Recomendación del usuario: solo las de reservas
     —reservar, consumir y liberar sobre la clave—, para no tocar la sentencia del libro.
4. **(R) La salida y el consumo van en el mismo commit**: se puede consumir una parte, y una reserva
   caducada da `409`. El ADR escribe dos cosas:
   - por qué `Albaran` en `TipoDeDocumentoOrigen` no es la casilla sin productor del 1.10 que evita
     el ADR-0055 §10: tiene productor, el caso de uso de consumir, y lo que falta es el llamante,
     que es la situación que nombra el criterio;
   - cómo se cierra la doble flecha (R13) cuando el documento origen vive en otro módulo.
     `LaDobleFlechaDelLibroTests` lo nombra como caso propio, sin una excepción general que pueda
     tapar otros.
5. **(R) Un solo estado, `Liberada`, con su causa**: a mano con motivo, o por caducidad.
6. **(R) Tipo, identificador y línea del origen, con índice único en todos los estados.** Volver a
   reservar una línea liberada, o cambiar la cantidad, queda para la fase 4, con su disparador: el
   primer pedido que lo necesite.
7. **(R) Lo que manda el plan maestro.**
   - Reservar, consumir y liberar son casos de uso de `Inventario.Application`, y un rechazo es un
     resultado.
   - En `Contracts`, solo la lectura del disponible, y nada por HTTP.
   - Qué hace la venta con un rechazo —un evento de vuelta— es de la fase 4, con su disparador
     escrito.
8. **(R) La serie se comprueba al abrir el recuento**, en un commit propio antes de las reservas. Es
   un defecto contra el ADR-0055 §1, que prometía validarla al abrir. Lleva caso y mutación.

Y lo que la puerta daba por decidido sin preguntar, que queda como se propuso:

- **Reservar comprueba el disponible bajo el cerrojo**, y el disponible no cuenta el tránsito
  (ADR-0053 §1).
- **La cantidad va en la unidad base del artículo**, que Catálogo publica desde el 2.12.
- **Reservar exige un artículo apto para moverse y un almacén activo**, como el ajuste.
- **Una reserva no toca el libro**, así que no tiene fecha contable ni se le aplica la R9. El
  consumo sí la toca, y a él sí (punto 6).

### 2. El cerrojo es la fila de la valoración

**Sí: la fila de cerrojo de una reserva es la de `inventario.valoraciones` de su clave.** No hay
fila nueva, así que no hay eslabón nuevo en el orden.

- **Un movimiento la toma** con el `INSERT … ON CONFLICT DO UPDATE SET cantidad = v.cantidad` del
  ADR-0046 §2. La rama del conflicto bloquea la fila como un `UPDATE` que no toca la clave, en modo
  `FOR NO KEY UPDATE`.
- **Reservar y liberar la toman con `SELECT … FOR NO KEY UPDATE`**, que choca con ese modo y
  consigo mismo, y no crea la fila. Una clave sin fila tiene el físico a cero, así que no hay nada
  que reservar: el `422` sale sin haber escrito nada. Liberar siempre la encuentra, porque una
  reserva solo nace donde había físico.
- **Consumir la toma como cualquier salida**, con `BloquearYLeerAsync`, porque valora lo que saca.

**La regla que lo sostiene**: toda escritura sobre las reservas de una clave tiene antes la
valoración de esa clave. Así, quien tiene la valoración bloqueada lee la suma de las reservas con
un `SELECT` sin cerrojo, y la suma no puede cambiar mientras la lee. Es lo que hace la transferencia
(punto 9).

**Por qué una reserva y un envío de la misma clave no se interbloquean.** Un interbloqueo necesita
que cada una espere algo que tiene la otra.

- La reserva toma **un solo cerrojo**, la valoración de su clave, y no espera nada antes de él.
- Después de él solo escribe filas de `reservas`, que nadie bloquea: la transferencia las lee sin
  cerrojo. Lo único que podría esperar es el índice único del origen (punto 5), y ahí solo puede
  estar otra reserva, que tampoco espera nada.
- El envío toma el ejercicio, la marca, el contador y las valoraciones en orden de clave
  (ADR-0053 §10), y la reserva no tiene ninguno de los tres primeros.

Así que la que llega segunda a la valoración espera a la primera, y la primera no espera a nadie.

**El orden de consumir** es el de una salida del ajuste, sin contador, porque el albarán lo numera
Ventas:

| Paso | Qué toma | Si falla |
|---|---|---|
| 1. El ejercicio de la fecha de la salida | compartido | `409` |
| 2. La marca del artículo | compartida | `409` |
| 3. La valoración de la clave | la fila, como una salida | — |
| 4. Las reservas caducadas de la clave | se escriben al confirmar | — |
| 5. La reserva | se escribe al confirmar | `409` · `422` |
| 6. El físico de cada hueco | — | `422` |
| 7. El valor y el impedimento | — | `422` |
| 8. Las existencias, la valoración y el libro | — | — |

### 3. Lo reservado se suma al leer, y el disponible no cuenta el tránsito

- **Lo reservado de una clave, ahora**, es la suma de lo pendiente de sus reservas `Activa` y
  vigentes: las que no tienen `caduca_el` o lo tienen después de ahora. Lo pendiente es la cantidad
  menos lo consumido. **«Ahora» es el del `TimeProvider`**, que viaja como parámetro, y nunca el
  `now()` del motor: así el reloj congelado de los tests vale también dentro del SQL.
- **El disponible es `valoraciones.cantidad` menos lo reservado.** Esa cantidad es el físico de la
  clave en el almacén, la suma de su libro. El tránsito vive aparte, en `en_transito`, y no cuenta
  (ADR-0053 §1).
- **El índice parcial** `ix_reservas_activas_por_clave`, sobre la empresa, el artículo y el almacén
  con `estado = 'Activa'`, es el que lee esa suma.
- **La migración quita `existencias.reservado` y `existencias.disponible`.** Valen cero en todas las
  filas, así que no hay nada que trasladar. La sentencia de la proyección deja de escribir
  `reservado`, el censo de lo que genera el servidor pierde `Existencia.Disponible`, y el caso que
  intentaba escribir `disponible` se va con su columna.
- **La lectura** es `IConsultaDeExistencias.DisponibleDeAsync`, en `Contracts`: por almacén y por
  lotes de artículos, da el físico, lo reservado y el disponible de cada uno. Es **una sola
  sentencia**, así que las tres cifras salen de la misma foto, y un artículo sin fila contesta tres
  ceros. Es la proyección de la precisión 1.
- **El disponible puede salir negativo**, y la lectura no lo recorta. El ajuste y el recuento
  registran la realidad sin frenarse (precisión 2), y si se rompen cinco unidades de las diez
  reservadas, el disponible es −5: faltan cinco para servir lo apartado. Cortarlo a cero escondería
  justo eso.

### 4. La caducidad se aplica al leer, y se escribe al pasar

- **El estado se lee con `Reserva.EstadoEn(ahora)`.** Una reserva guardada `Activa` cuyo
  `caduca_el` no es posterior a `ahora` se lee `Liberada`. El estado guardado no es público: el
  único que lo lee es `EstadoEn`, y EF lo mapea por su nombre, como propiedad privada.
- **Qué escrituras la liberan: solo las de reservas sobre su clave**, como recomendó el usuario.
  Reservar, consumir y liberar, con la valoración ya bloqueada, cargan las reservas `Activa` de la
  clave con `caduca_el` vencido y las pasan a `Liberada`, con la causa `Caducidad` y
  `liberada_el = caduca_el`. La sentencia del libro no las toca.
- **Las liberan después del último rechazo, justo antes de confirmar.** Un rechazo no las toca: se
  quedan como estaban hasta la siguiente escritura de su clave que salga bien. No importa: el
  disponible y el estado ya no las cuentan. Si se liberaran antes de decidir, un rechazo las dejaría
  liberadas en el rastreador, y la siguiente confirmación del mismo ámbito, sobre otra clave, las
  escribiría sin el cerrojo de la suya. Así estaba hasta la revisión del 2.13, que lo encontró.
- **El hueco, dicho**: entre que caduca y la siguiente escritura sobre su clave, una reserva sigue
  guardada `Activa`. Quien la lee la ve `Liberada`, y el disponible ya no la cuenta.

### 5. Reservar

| Paso | Qué hace | Si falla |
|---|---|---|
| 1. La petición | origen completo, cantidad positiva con seis decimales | `400` |
| 2. La valoración de la clave | `FOR NO KEY UPDATE` | — |
| 3. El origen | la reserva que ya tenga | `409` |
| 4. La caducidad | posterior a ahora | `400` |
| 5. Los maestros | artículo apto, almacén activo, unidad base | `400` · `409` |
| 6. El disponible | con la valoración bloqueada | `422` |
| 7. Las caducadas de la clave | a `Liberada` | — |
| 8. La reserva | `INSERT` | — |

- **La idempotencia por el origen** (precisión 6). Si el origen ya tiene reserva, con el mismo
  artículo, almacén, cantidad y caducidad se devuelve esa, esté como esté; con cualquier otra cosa,
  `409` `reserva-origen-con-otra-reserva`. Va después del cerrojo: dos peticiones iguales a la vez
  esperan en la misma valoración, y la segunda encuentra la primera.
- **La caducidad va después del origen, y no con la forma de la petición**, porque depende de
  «ahora». El reintento de una petición que salió bien, cuando su caducidad ya pasó, encuentra su
  reserva, liberada por caducidad, y no un `400`. Con la caducidad en el paso 1, ese reintento
  contestaba `reserva-caducidad-no-valida`: lo encontró el caso de la caducidad de
  `LasReservasTests` la primera vez que corrió.
- **Dos peticiones del mismo origen sobre claves distintas** no comparten cerrojo. La segunda
  espera en el índice único y revienta cuando la primera confirma. **Es una excepción, no un
  resultado**, y es lo que se quiere: la bandeja de salida reintenta, y el reintento encuentra la
  reserva y contesta el `409`.
- **Los rechazos son resultados** (precisión 7): ningún camino de fallo lanza.

### 6. Consumir escribe su salida

- **Recibe el origen de la reserva**, no su identificador: el albarán sabe de qué línea de pedido
  sale, y el origen es único en todos los estados (precisión 6). Recibe también el documento que
  sale —tipo e identificador—, la fecha de la salida y las líneas: hueco, cantidad en la unidad
  base, lote y serie. El artículo y el almacén son los de la reserva.
- **La fecha** no puede ser futura (`422`, como la transferencia), cae en un ejercicio abierto (R9,
  `409`) y no es anterior al último movimiento de la clave (ADR-0047, `422`).
- **Lo que se mira de la reserva, con su clave bloqueada:**
  - si ese documento ya la consumió, `409` `reserva-documento-ya-la-consumio`. Va antes que el
    estado: el reintento del albarán que la dejó consumida oye que ese albarán ya salió, y no
    que la reserva no está activa;
  - si no está `Activa` ahora, `409`: `reserva-caducada` si es porque caducó, y
    `reserva-no-esta-activa` si se consumió o se liberó;
  - si las líneas suman más de lo pendiente, `422`.
- **El físico de cada hueco se mira antes de escribir**, con la clave bloqueada: si una línea saca
  más de lo que hay, `422` `reserva-consumo-sin-stock`. Hace falta porque la unidad de trabajo
  confirma aunque el caso diga que no (`UnidadDeTrabajoDeInventario`), y la restricción de
  existencias solo se traduce a `422` en el borde HTTP. Sin esto, el rechazo llegaría como una
  excepción de la base. Los lotes y las series se buscan sin crearlos: uno que no existe no tiene
  nada que sacar.
- **La salida** es una fila del libro por línea, con el tipo `Albaran` y el identificador del
  albarán, en la unidad base con factor 1, y valorada al precio medio, como la del ajuste.
- **Se puede consumir una parte.** La reserva sigue `Activa` con lo que queda, y pasa a `Consumida`
  cuando no queda nada.
- **Dobla la R12, como el ajuste**: la reserva, su consumo, las filas del libro, las existencias y
  la valoración van en el mismo `COMMIT`. Con dos, habría un instante en que lo que sale cuenta dos
  veces, en el físico y en lo reservado. La fila de la R12 de `docs/dominio/reglas-duras.md` lo
  cuenta.

### 7. `Albaran` no es la casilla sin productor

El ADR-0055 §10 no le dio al recuento un valor en `TipoDeDocumentoOrigen` porque nada escribiría
filas con él: un valor sin productor es el defecto del 1.10. **`Albaran` sí tiene productor en el
2.13**: el caso de uso de consumir escribe filas del libro con ese tipo, y los tests lo llaman. Lo
que falta es quien lo llame en producción, el albarán de la fase 4, y esa es la situación que nombra
el criterio del ítem.

El valor nombra el documento cuyo identificador va en `documento_origen_id`, y ese documento vive en
Ventas.

### 8. La doble flecha, cuando el documento vive en otro módulo

Inventario no puede leer la tabla de albaranes: ninguna consulta cruza esquemas (§5). **La flecha de
una fila `Albaran` se cierra contra `consumos_de_reserva`**, que guarda el identificador del albarán
que consumió cada reserva:

- **la ida**: toda fila del libro de tipo `Albaran` tiene un consumo con ese documento, de una
  reserva de su mismo artículo y almacén;
- **la vuelta**: todo consumo tiene al menos una fila del libro con su documento, su artículo y su
  almacén.

Son **dos casos con nombre propio** en `LaDobleFlechaDelLibroTests`, junto a los del ajuste y la
transferencia. No hay una regla general para «los documentos de otro módulo»: una excepción así
taparía el próximo tipo que llegara sin su caso.

Lo que no cierra, dicho: que el albarán exista en Ventas. Eso lo cierra la fase 4 desde su lado,
cuando exista el albarán. Y el día que Ventas saque mercancía sin reserva, la ida tendrá que contar
también con esa otra salida.

### 9. La transferencia respeta el disponible de su origen

- **Al enviar, con las valoraciones ya bloqueadas**: si lo que sale de una clave del origen, en la
  unidad base, pasa del disponible de esa clave, es un `422`
  `transferencia-por-encima-del-disponible`. La suma de las reservas se lee después del cerrojo, y
  por eso no puede cambiar.
- **Solo entre el disponible y el físico de la clave.** Lo que pasa del físico de la clave sigue
  siendo el `422` `stock-insuficiente` del hueco, que es más preciso: dice que no hay, y no que está
  apartado. La guarda va la última antes de escribir, detrás de la valoración, así que lo que
  rechaza la valoración se sigue rechazando con su código. La regla es de dominio,
  `ElDisponibleDeLaSalida`, y el envío lee lo reservado con el mismo puerto que la reserva.
- **La guarda mira la clave, no el hueco.** Con 8 en un hueco, 2 en otro y 5 reservados, enviar 9
  del primero es `transferencia-por-encima-del-disponible`; sin las reservas era
  `stock-insuficiente`. Las dos cosas son verdad, y contesta la que no se arregla moviendo
  mercancía de un hueco a otro. Mirar cada hueco pediría leer sus existencias en el envío, que hoy
  las mueve la sentencia del libro sin leerlas (ADR-0044). Este punto decía que lo que ya se
  rechazaba se seguía rechazando con el mismo código, y aquí no es así: lo encontró la revisión del
  2.13.
- **Recibir no mira nada**: suma.
- **El ajuste, el recuento y las anulaciones no se frenan** (precisión 2). Registran la realidad o
  corrigen un error.

### 10. La reserva no es un `DocumentoBase`

La R1 dice que todo documento es una máquina de estados explícita. **La reserva lo es**: su estado
es privado, cambia solo por sus transiciones con nombre —consumir, liberar y liberar por
caducidad— y cada una comprueba su estado de partida y lanza si no es el suyo. Pero no hereda de
`DocumentoBase`, por tres cosas:

- **`DocumentoBase` publica su estado**, y la precisión 3 dice que nadie lee «Activa» a secas.
- **`DocumentoBase` pide un evento por transición.** Los eventos de la reserva son los de vuelta
  hacia la venta, y son de la fase 4 con su consumidor (precisión 7).
- **No es un documento del periodo**: no tiene número, ni fecha contable, ni escribe en el libro.
  El cierre del ejercicio no le pregunta, y `LosModulosConDocumentosSeInscribenTests` no cambia.

Hereda de `EntidadBase`, se audita y lleva testigo de concurrencia, como cualquier cosa que se
modifica, aunque cada escritura ya pase por la valoración de su clave. Su consumo es un hijo de solo
inserción, sin testigo, como la línea del ajuste.

### 11. Lo que no hay en el 2.13, con su disparador

- **Volver a reservar una línea liberada, o cambiar su cantidad**: fase 4, el primer pedido que lo
  necesite (precisión 6).
- **Los eventos de vuelta**, reserva hecha o rechazada: fase 4, el manejador del pedido de venta
  (precisión 7).
- **Una salida del albarán sin reserva**: fase 4, el primer albarán que sirva sin reservar. Trae su
  lado de la doble flecha (punto 8).
- **El *backorder*** del §7.4, reservar más de lo disponible: fase 4, como ya estaba.
- **Un proceso que libere las caducadas**: ninguno (precisión 3). El del 2.14 sigue siendo suyo.

### 12. Qué decidió el agente

- **Las tablas**: `reservas` y `consumos_de_reserva`. El índice único
  `ix_reservas_una_por_origen`, sobre la empresa y el tipo, el identificador y la línea del origen,
  sin filtro, porque vale en todos los estados. Y `ix_consumos_de_reserva_uno_por_documento`, sobre
  la reserva y el documento, que respalda el `409` del documento que ya consumió.
- **Los enumerados**, guardados como texto: `EstadoDeReserva` (`Activa`, `Consumida`, `Liberada`),
  `CausaDeLiberacion` (`AMano`, `Caducidad`) y `TipoDeOrigenDeReserva`, que en el 2.13 solo conoce
  `PedidoDeVenta`, el valor que necesitan los tests. La orden de fabricación entra con su fase.
- **Las restricciones de la fila**, que el dominio ya respeta y que no se traducen a ningún error:
  la cantidad y la línea, positivas; `Liberada` si y solo si hay causa y fecha de liberación; el
  motivo solo con la causa `AMano`, y obligatorio con ella; y la fecha de una liberación por
  caducidad, la de `caduca_el`.
- **La unidad base la copia la reserva** al nacer, preguntada a Catálogo, y la salida va en ella con
  factor 1. Si el artículo cambiara de unidad base, la reserva seguiría hablando en la suya.
- **La consulta del disponible**, `IConsultaDeExistencias.DisponibleDeAsync`, devuelve un registro
  con tres cifras y ningún enumerado, así que la matriz de los puertos de estado no la mira.
- **Los nombres**: el prefijo `reserva-` en los códigos de error; los casos de uso `IReservar`,
  `IConsumirReserva` e `ILiberarReserva`. Sin permisos, porque no hay ruta.

## Consecuencias

- **La migración de Inventario** crea las dos tablas de las reservas y quita dos columnas de
  `existencias`. Hoy no hay ninguna reserva, y las columnas valen cero, así que no hay nada que
  rellenar.
- **La transferencia tiene un rechazo nuevo**, el `422` del disponible, y su caso de carrera con una
  reserva.
- **La propiedad gana reservar, consumir con su salida, liberar y caducar**, con el reloj congelado.
  El invariante gana que lo reservado de cada clave es la suma de sus reservas activas y vigentes, y
  que ninguna se creó por encima del disponible.
- **El estado se sabe producir, y lo que falta es el llamante**: el manejador de eventos de la fase
  4, que llamará a reservar al confirmar un pedido y a consumir al expedir un albarán.

## Procedencia

Lo fijó el usuario en el encargo del 2026-10-08. En la puerta del 2026-10-09 contestó las ocho
preguntas con las recomendaciones y cinco precisiones, y aceptó lo que este ADR decidía sin
preguntar. Lo que decidió el agente está en el punto 12.
