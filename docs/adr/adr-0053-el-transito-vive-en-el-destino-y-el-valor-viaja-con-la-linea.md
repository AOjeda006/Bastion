---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, sql, inventario, concurrencia]
tags: [adr, transferencia, transito, valoracion, numero-de-serie, cerrojo, anulacion, adr-0043, adr-0046, adr-0047, adr-0048]
revisado: 2026-10-03
---

# ADR-0053: El tránsito vive en el destino, y el valor viaja con la línea

- **Estado:** aceptado. **Enmendado por el ADR-0054 (§5 y §6, lo que se lleva el inverso que vacía
  el destino).**
  - El [ADR-0054](adr-0054-vaciar-la-clave-se-lleva-todo-su-valor-tambien-en-el-inverso.md) dice lo que estas dos frases callaban:
    si la salida del destino vacía la clave, se lleva todo lo que queda, que puede ser más de lo
    que entró, y eso es lo que recibe el origen.
- **Fecha:** 2026-10-03
- **Sale del 2.11.** Los diez puntos los fijó el usuario en el encargo del 2026-10-03 (PLAN, *El
  2.11: la transferencia y el stock en tránsito*). Tres cosas quedaron abiertas, y el usuario las
  contestó en la puerta de ese mismo día: dónde vive el tránsito, la forma del inverso y el valor
  con el que vuelve una recibida cuando el tope toca. El punto 12 dice qué decidió el agente.
- **Enmienda el ADR-0048 §3**: las dos expresiones que sostienen «un número de serie en un solo
  sitio» pasan a contar el tránsito. Los nombres son los del ADR-0050, y no cambian.
- **Cumple el ADR-0046 §3**, que dejaba al 2.11 «cómo se cuenta el tránsito», y aplica a la
  transferencia el orden de cerrojos del ADR-0046 §2 con los eslabones del ADR-0048, sin cambiarlo.

## Contexto

La transferencia es el segundo documento que escribe en el libro, y el primero con **dos momentos**.
Al enviar sale del origen, y al recibir entra en el destino. Entre los dos, la mercancía no está en
ningún almacén, pero sigue siendo de la empresa.

El plan maestro lo dice en dos sitios. El §7.4 pone «en tránsito» entre los campos de la
existencia, y dice que la transferencia tiene «stock en tránsito mientras vuela». El §8.3 dibuja el
ciclo: «enviar ► SALIDA origen + stock EN TRÁNSITO ► recibir ► ENTRADA destino».

Tres cosas del ítem no se arreglan después sin rehacer datos:

- **dónde vive el tránsito**, que es esquema;
- **la guarda de la serie**, que hoy es un índice parcial sobre la existencia y no ve lo que vuela;
- **el valor**, que viaja con la línea y tiene que volver entero, o decir por qué no.

## Decisión

### 1. El tránsito vive en el destino, en la existencia y en la valoración

Lo eligió el usuario en la puerta.

- **`existencias.en_transito`**, del mismo tipo que `fisico`. Va en la fila del **destino**: su
  almacén, su ubicación, su lote y su serie. Es el campo «en tránsito» del §7.4, y se ve donde se
  espera la mercancía.
- **`valoraciones.en_transito` y `valoraciones.valor_en_transito`**, en la clave del destino
  (empresa, artículo, almacén). El precio medio del destino no los mira: se deduce de `cantidad` y
  `valor`, que siguen siendo la suma del libro.
- **La línea lleva la ubicación de destino desde el alta.** La fila de la existencia la necesita, y
  la recepción, que es entera, no tiene que preguntar nada.
- **Cuatro restricciones nuevas**: `ck_existencias_en_transito_no_negativo`,
  `ck_valoraciones_en_transito_no_negativo`, `ck_valoraciones_valor_en_transito_no_negativo` y
  `ck_valoraciones_sin_transito_no_hay_valor_en_transito`. Son las de la cantidad y el valor,
  repetidas para lo que vuela.

**El tránsito no está en el libro.** Cada línea escribe dos filas: la salida del origen al enviar y
la entrada en el destino al recibir. Así que la R3 se sigue cumpliendo clave a clave: `fisico` es la
suma del libro, y `valor` también. El tránsito es la proyección de **otra** verdad, los documentos:
las líneas de las transferencias que están `Enviada`. Por eso tiene su propio cuadre (punto 11).

- **El disponible no lo cuenta.** Sigue siendo `fisico − reservado`, que es lo que el 2.13 reserva.
- **Las instantáneas mensuales tampoco.** Son del libro. El tránsito a una fecha pasada sale de los
  documentos y de sus dos fechas, y es del 2.14.

### 2. El valor viaja con la línea

Lo fijó el ADR-0046 §3, y el encargo lo concreta.

- **Al enviar**, la salida del origen se valora como cualquier salida: a su precio medio, como mucho
  lo que hay, y todo el valor si vacía la clave. La línea guarda ese importe en `Valor`.
- **Ese importe exacto queda en tránsito**, en `valor_en_transito` del destino.
- **Al recibir**, la entrada en el destino entra con él, como `ValorQueCompensa`: sin coste y sin el
  precio medio del destino. El tránsito baja lo mismo.

**Casos:** el precio medio del destino se mezcla con lo que llega, y vaciar el origen se lleva todo
el valor.

### 3. Dos momentos y dos fechas

- **La fecha de envío** se pone al abrir el documento, como la del ajuste.
- **La fecha de recepción** la trae el cuerpo de la recepción, y es obligatoria.

| | Envío | Recepción |
|---|---|---|
| Fecha futura | `422` | `422` |
| Ejercicio de su fecha (R9) | abierto | abierto |
| `ultima_fecha` de su clave (ADR-0047) | la del origen | la del destino |
| Contra la otra fecha | — | no anterior al envío, `422` |
| Número (R5) | en su serie, con su fecha (ADR-0043) | ninguno |

**El tránsito no mueve `ultima_fecha`.** No escribe ninguna fila del libro, y esa fecha es la del
último movimiento de la clave.

**Casos:** el cambio de año, con el envío el 30/12 en la serie de un año y la recepción el 3/1, y
recibir en un ejercicio cerrado.

### 4. La recepción es entera

La recomendó el usuario. Recibir mueve todas las líneas, y una diferencia se regulariza después con
un ajuste en el destino. **Disparador:** la primera recepción con faltas.

### 5. El inverso es otra transferencia, con las líneas negadas

> **Enmendado por el ADR-0054 (2026-10-04).** La salida del destino se lleva el valor que entró,
> como mucho el que queda, y **todo el que queda si vacía la clave**, aunque sea más.

Lo eligió el usuario en la puerta. Es la forma del 2.5.

- **Una transferencia nueva**, con `AnulaAId`, el mismo origen, destino, serie y divisa, y cada
  línea con la cantidad negada. Copia las ubicaciones, el lote y la serie, y su `ValorQueCompensa`
  es el `Valor` de la línea original, negado.
- **Niega cada pata que el original escribió**:
  - de una `Enviada`, la salida del origen. Vuelve al origen el valor exacto que salió, y el
    tránsito baja a cero;
  - de una `Recibida`, las dos, en la misma transacción. La salida del destino lleva el valor que
    entró, y como mucho el que queda. La entrada en el origen lleva lo que esa salida se llevó
    (punto 6).
- **Nace `Recibida`**: lo que mueve lo mueve de una vez, y no deja nada en vuelo. Sus dos fechas son
  la de hoy, en UTC, y la R9 pregunta por hoy.
- **Numera en la serie del original, con la fecha de envío del original.** Es la excepción del
  ADR-0043 §4, por el mismo motivo: el inverso pertenece a su original, no a su fecha.
- **Sus filas del libro son de tipo `Transferencia`**, como las del original, y el par se suma de
  una vez.
- **El original pasa a `Anulada`**, venga de `Enviada` o de `Recibida`. Un índice único sobre
  `anula_a_id` impide un segundo inverso, como en los ajustes.
- **Si las unidades ya salieron del destino**, es la excepción de la R2 del ADR-0046 §1: `422`
  `stock-insuficiente`, y no se anula.

**Casos:** anular en los dos estados, y la carrera de recibir y anular a la vez, con dos
transacciones de verdad.

### 6. Una transferencia no crea ni destruye valor

> **Enmendado por el ADR-0054 (2026-10-04).** Con tope, el origen recibe menos de lo que salió de
> él. Si la salida del destino vacía la clave, puede recibir más. El invariante de abajo no
> cambia.

Lo eligió el usuario en la puerta. Al anular una `Recibida`, **el origen recibe lo que la salida del
destino se llevó de verdad**. Sin tope, es exactamente lo que salió del origen. Con tope, es menos,
y la línea del inverso guarda las dos cifras, `ValorQueCompensa` y `Valor`, como el ADR-0046 §6.

De ahí sale el invariante que la propiedad comprueba: **el valor de la empresa es la suma de
`valor` y `valor_en_transito` de todas sus valoraciones, y solo lo mueven los ajustes.** Ni enviar,
ni recibir, ni anular una transferencia lo cambian.

### 7. Una serie en tránsito sigue estando en un solo sitio

**Enmienda el ADR-0048 §3**, en sus dos expresiones:

- el índice `ix_existencias_numero_de_serie_en_un_sitio` filtra por
  `(fisico > 0 OR en_transito > 0) AND numero_de_serie_id IS NOT NULL`;
- el `CHECK` `ck_existencias_numero_de_serie_como_mucho_una` pasa a ser
  `numero_de_serie_id IS NULL OR fisico + en_transito <= 1`.

Sin eso, una serie que vuela no está en ninguna existencia. Un ajuste podría darla de alta en otro
sitio mientras viaja, y la recepción chocaría después.

**El orden de las sentencias importa, porque el índice se comprueba fila a fila:**

- **al enviar, primero la salida del origen y después el tránsito.** Al revés, la serie estaría un
  instante en dos filas;
- **al recibir, primero el tránsito y después la entrada.** Al revés, la fila del destino pasaría
  por `fisico = 1` y `en_transito = 1`, y el `CHECK` la rechazaría.

**El caso:** una serie en tránsito, y un ajuste que la da de alta en otro almacén a la vez, con dos
transacciones de verdad. El ajuste recibe el `422` `numero-de-serie-en-existencias`.

### 8. Dos almacenes distintos, de la misma empresa

- **El origen no puede ser el destino**, y se rechaza en el alta.
- **Los dos almacenes se preguntan al puerto**, y tienen que ofrecerse para lo nuevo. Uno de otra
  empresa contesta lo mismo que uno que no existe: no hay oráculo.
- **Cada ubicación es de su almacén**, por el puerto de ubicaciones, como en el ajuste.

**El caso:** una transferencia hacia el almacén de otra empresa recibe lo mismo que una hacia un
almacén inventado.

### 9. La superficie es la del ajuste

- **Tres acciones**: `POST /api/v1/inventario/transferencias/{id}/envio`, `…/recepcion` y
  `…/anulacion`. Las tres llevan la `Idempotency-Key` **obligatoria**, porque el filtro de
  idempotencia es el dueño de la transacción.
- **Tres permisos**, uno por acción: `inventario.transferencia.enviar`, `…recibir` y `…anular`.
  Quien envía y quien recibe suelen ser personas distintas, en almacenes distintos.
- **El alta, el listado y la ficha no tienen acción ni pantalla.** El alta es un caso de uso, como
  el del ajuste. La pregunta de las pantallas del inventario, en el cierre de la fase, incluye
  ahora la transferencia.

### 10. El orden de los cerrojos

Es el del ADR-0046 §2 con los eslabones del ADR-0048, en cada transacción:

| Paso | Envío | Recepción | Anulación |
|---|---|---|---|
| Ejercicio, compartido | fecha de envío | fecha de recepción | hoy |
| Marca del artículo, compartida | sí | no | no |
| Contador de la serie | sí | no | sí, la del original |
| Valoraciones, en orden de clave | origen y destino | destino | origen y destino |
| Lotes y series | sí | sí | sí |
| Fila del documento | sí | sí | la del inverso y la del original |
| Existencias | salida, luego tránsito | tránsito, luego entrada | según lo que niegue |

- **La marca no se lee al recibir ni al anular.** El artículo ya tiene movimientos, los del envío,
  así que su marca no puede cambiar (ADR-0048 §4).
- **El documento se guarda antes que las existencias**, como en el ajuste. Así, quien pierde una
  carrera sobre el mismo documento choca con el testigo de la R11 y recibe el `412`, en vez de un
  `422` de stock que no le corresponde.

**Por qué dos transferencias cruzadas no se interbloquean.** A→B y B→A del mismo artículo bloquean
las dos valoraciones, la de A y la de B, **en el mismo orden de clave** y antes de tocar ninguna
existencia. La segunda espera en la primera clave que comparten, y no tiene nada que la primera
necesite. Las existencias se tocan con su valoración ya bloqueada en exclusiva, así que su orden ya
no importa. Y el contador de la serie va antes que las valoraciones en las dos. Como en el ADR-0044,
lo sostiene el razonamiento, y no un caso que se ponga rojo.

**Recibir y anular a la vez** también se ordenan así. Las dos bloquean la valoración del destino, y
la que llega segunda espera. Cuando sigue, choca con el testigo de la fila del documento, que la
primera ya cambió, y recibe el `412`. Su transacción entera se deshace, número incluido.

### 11. El cuadre del tránsito y la propiedad

- **El cuadre del tránsito** compara dos cosas, y dice cuántas filas comparó:
  - `existencias.en_transito`, por artículo, almacén, ubicación, lote y serie del destino, contra
    la suma de las líneas de las transferencias `Enviada`;
  - `valoraciones.en_transito` y `valor_en_transito`, por artículo y almacén del destino, contra la
    suma de la cantidad en unidad base y del `Valor` de esas mismas líneas.
- **La propiedad** gana transferencias en su generador: abrir y enviar, recibir, y anular en los dos
  estados. Su modelo rechaza lo que el sistema rechaza, y su invariante gana tres cosas:
  - el cuadre del tránsito;
  - el valor de la empresa del punto 6;
  - una serie en un solo sitio, contando el tránsito.

### 12. Qué decidió el agente

Lo que el encargo y las tres respuestas dejaron por decidir:

- **los estados**: `Borrador`, `Enviada`, `Recibida` y `Anulada`, en un enumerado propio, como pide
  el de los ajustes. El borrador es el del ajuste: se abre, se le ponen líneas y no mueve nada;
- **que el inverso nace `Recibida`**, con las dos fechas en hoy;
- **que la fecha de recepción va en el cuerpo de la recepción**, y la de envío en el alta;
- **que el motivo solo lo lleva el inverso**, que es por qué se anula. Una transferencia no
  necesita justificarse como un ajuste;
- **la cantidad en tránsito también en la valoración**, para que el motor sostenga «sin tránsito no
  hay valor en tránsito» con un `CHECK` de fila;
- **los nombres**: de las restricciones, de los tres permisos y de los `type` de error, con el
  prefijo `transferencia-`;
- **lo que el cierre pregunta**: un borrador cuenta por su fecha de envío, y un documento cuenta si
  cualquiera de sus dos fechas cae dentro del intervalo;
- **los tres eventos**: `TransferenciaEnviada`, `TransferenciaRecibida` y `TransferenciaAnulada`.

## Consecuencias

- **El libro tiene dos documentos que escriben en él.** `TipoDeDocumentoOrigen` gana
  `Transferencia`, y la serie numera en el tipo `TransferenciaDeInventario`, que ya estaba en el
  enumerado de Organización desde el 2.4.
- **La migración de Inventario** crea las dos tablas de la transferencia, añade las tres columnas
  del tránsito y cambia las dos expresiones de la serie. Hoy no hay ninguna transferencia, así que
  las columnas nacen a cero y no hay nada que rellenar.
- **Lo que se deja abierto, con su disparador:**
  - la recepción parcial: la primera recepción con faltas;
  - el tránsito a una fecha pasada: el 2.14;
  - las pantallas: el cierre de la fase 2.

## Procedencia

Lo fijó el usuario en el encargo del 2026-10-03: los diez puntos, con sus casos. En la puerta de ese
mismo día eligió tres cosas: el tránsito en el destino, el inverso como transferencia negada y el
valor de vuelta igual a lo que sale del destino. Lo que decidió el agente está en el punto 12.
