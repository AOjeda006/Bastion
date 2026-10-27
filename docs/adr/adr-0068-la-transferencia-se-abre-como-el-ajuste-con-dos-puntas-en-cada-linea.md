---
tipo: referencia
stack: [dotnet, efcore, postgresql, aspnetcore, react]
aplica_a: [inventario, organizacion, api-rest, frontal]
tags: [adr, transferencia, conversion, factor, unidad-base, descarte, borrado-logico, listado, rejilla, r3, r10, r11, adr-0048, adr-0053, adr-0057, adr-0065]
revisado: 2026-10-27
---

# ADR-0068: La transferencia se abre como el ajuste, con dos puntas en cada línea

- **Estado:** aceptado. **Enmendado por el ADR-0069 (§1) y por el ADR-0072 (§1).**
  - El [ADR-0069](adr-0069-un-q-sin-filtro-declarado-llega-y-no-filtra-nada.md) enmienda, el
    2026-10-22, el punto «Sin `?q=`» del §1: el borde no contesta que el recurso no admite filtro
    de texto. El `?q=` llega y no filtra nada.
  - El [ADR-0072](adr-0072-un-listado-enlaza-tres-piezas-y-el-texto-solo-quien-lo-mira.md)
    enmienda, el 2026-10-27, el mismo punto: el borde vuelve a contestar, con un `400`
    `filtro-no-admitido`, que el recurso no admite filtro de texto.
- **Fecha:** 2026-10-20
- **Sale del ítem 2.19**, el alta de la transferencia. El criterio está en `docs/PLAN.md`, en su
  casilla, y la puerta, con sus cuatro respuestas, en *Decisiones tomadas → La puerta del 2.19* y
  *Traídas por el encargo del 2026-10-20*.
- **Enmienda el [ADR-0053](adr-0053-el-transito-vive-en-el-destino-y-el-valor-viaja-con-la-linea.md)**
  en dos sitios:
  - **§9**, la superficie: el alta, el listado y la ficha tienen ruta y pantalla, y el borrador se
    descarta (puntos 1 y 5 de este);
  - **§12**, lo que el cierre pregunta: un borrador descartado ya no cuenta (punto 5).
- **Cumple lo que el ADR-0065 dejó para este ítem**, en su §2 y su §9: el factor de la
  transferencia lo resuelve el servidor, con el mismo puerto. No lo enmienda.

## Contexto

### Lo que dice el plan maestro, primero

Lo mismo que en el ADR-0065, y no se repite entero:

- **el §9**: `201` con `Location`, `Idempotency-Key` obligatoria en lo que crea (R10), `ETag` e
  `If-Match` en los documentos (R11), paginación con tope, y «`204` al borrar un borrador»;
- **el §10**: el listado con la paginación y el orden en el servidor, la barra de la ficha que sale
  de `accionesDisponibles(documento, permisos)`, y la entrada de líneas pensada para teclado;
- **el §11**: el permiso es `modulo.recurso.accion`, la interfaz oculta lo que no se puede hacer, y
  el que decide es el servidor;
- **la R3**: la línea guarda la cantidad y la unidad que se escribieron, con el factor que las une a
  la unidad base.

### Lo que había escrito

- **`AbrirTransferencia` existía sin borde HTTP** (ADR-0053 §9), y tomaba el factor del cliente en
  cada línea (`LineaDeTransferenciaDto.FactorAUnidadBase`). Miraba la forma de las líneas en una
  sola pasada, y sus rechazos no decían la línea en `errors` ni en `actual`.
- **Una cantidad que no cabía en su columna era un `500`**, como en el ajuste hasta `b1db83e`: el
  dominio miraba el cero, y lo demás lo decía la base al guardar.
- **El borde tenía tres acciones**, enviar, recibir y anular, sin `GET`, y por eso ninguna pedía
  `If-Match`.
- **Un borrador no se podía tirar**, y contaba para el cierre del ejercicio por su fecha de envío
  (ADR-0053 §12).
- **La rejilla de líneas y el buscador del artículo eran del ajuste**, en
  `features/inventario/ajustes/ui`.

### Las cuatro respuestas de la puerta

1. El borrador equivocado se descarta, como el del ajuste: `inventario.transferencia.descartar`,
   borrado lógico y `204`.
2. El lote y la serie se escriben a mano, como en el ajuste. El selector de lo que hay en la
   ubicación de origen se queda pendiente, con su disparador: la expedición de la fase 4.
3. El cierre de la fase va en este mismo encargo, después del 2.20, y se para en la puerta de la
   fase 3.
4. Las tres propuestas abiertas van en un ítem nuevo, el 2.20, con el indicador de los meses sin
   cuadrar y una medida nueva del cuadre sin JIT.

## Decisión

### 1. Las rutas, bajo `/api/v1/inventario/transferencias`

| Ruta | Respuesta | Permiso | Mecanismos |
|---|---|---|---|
| `POST` | `201` con `Location` y la transferencia | `inventario.transferencia.abrir` | `Idempotency-Key` obligatoria |
| `GET` | una página | `inventario.transferencia.ver` | — |
| `GET {id}` | la ficha, con su `ETag` | `inventario.transferencia.ver` | — |
| `DELETE {id}` | `204` | `inventario.transferencia.descartar` | `If-Match` |
| `POST {id}/envio`, `…/recepcion` y `…/anulacion` | como hasta ahora | los tres de siempre | `Idempotency-Key` obligatoria |

- **El alta exige la clave y no lleva `ETag`**, por los motivos del ADR-0065 §6: nada para el
  reintento de un borrador, y la versión se lee con la ficha.
- **El listado** acepta `?page=`, `?size=`, `?sort=`, `?estado=`, `?origen=` y `?destino=`. **Dos
  filtros de almacén y no uno**: quien recibe busca lo que llega a su almacén, y quien envía, lo que
  sale del suyo. Juntos, dan lo que va de uno a otro. Por omisión va por la fecha de envío, de la
  última a la primera, con el desempate por el identificador. También se ordena por la fecha de
  recepción y por el número. Las filas no llevan las líneas.
- > **Enmendado por el ADR-0069 (2026-10-22).** El borde no contesta nada: un `?q=` llega y no
  > filtra nada, porque el paginador no aplica ningún filtro si el repositorio no declara uno. La
  > frase de las líneas de tarifa tampoco era cierta, y se reescribió con el ADR-0069.
  >
  > **Enmendado por el ADR-0072 (2026-10-27).** Y desde el ítem 3.2 vuelve a ser cierto lo que
  > decía este punto: el listado ya no publica `q`, y el borde contesta con un `400`
  > `filtro-no-admitido` a un `?q=` con algo escrito.

  **Sin `?q=`.** El alta de una transferencia no tiene un campo que escriba una persona: el motivo
  es del inverso, y se escribe al anular (ADR-0053 §12). Como en las líneas de tarifa, el borde
  contesta que este recurso no admite filtro de texto. Un `?q=` sobre identificadores sería pedir
  que se teclee un `uuid`.
- **La ficha lleva las líneas enteras**, sin paginar. El alta acepta como mucho 200, el tope de una
  página (`Paginacion.TamanioMaximo`), y una más es un `400`.
- **Los permisos pasan de 112 a 115**: `ver`, `abrir` y `descartar`. Entran en
  `PermisosDeInventario.Todos`, y con ellos en el rol del sistema. El humo los cuenta en los dos
  arranques.

### 2. El factor lo resuelve el servidor, con el puerto del 2.17

`LineaDeTransferenciaDto` pierde `FactorAUnidadBase`, y el alta lo resuelve como el del ajuste
(ADR-0065 §2), después de comprobar los maestros:

1. la unidad base de cada artículo distinto, en una consulta a Catálogo;
2. el factor 1 si la unidad de la línea es la base, sin preguntar a Organización;
3. si no, el par (unidad introducida → unidad base), con todos los pares distintos del documento en
   una consulta a `IConsultaDeConversiones`.

Un par que no está es un **`422` `transferencia-sin-conversion`**, y uno retirado, un **`409`
`transferencia-conversion-retirada`**. Los dos dicen la primera línea que falla, con
`actual = { linea, unidadIntroducidaId, unidadBaseId }`.

**La forma de las líneas, en dos pasadas**, como en el ajuste:

- la primera, antes que los maestros: la cantidad escrita, el lote, el número de serie y la serie
  repetida;
- la segunda, con el factor resuelto: la cantidad en unidad base y la serie unitaria.

### 3. La conversión retirada, como en el ajuste

- **Se rechaza en el alta**, con el `409` del punto 2.
- **Enviar no vuelve a preguntar.** Un borrador abierto antes de que se retirara la conversión se
  envía con el factor que la línea guardó. Lo que se envía es lo que la ficha enseñaba.
- **El inverso copia el factor del original**, línea a línea (`LineaDeTransferencia.Compensada`), y
  tampoco pregunta. Dos casos lo afirman: uno con la conversión retirada después de enviar, y otro
  con su factor cambiado.
- **La ficha enseña en cada línea el factor y la cantidad en unidad base.**

### 4. Lo que no cabe en su columna es un `400`

Como en el ajuste desde `b1db83e`, y por lo mismo: una cantidad que la base rechaza al guardar es
un `500`, y quien la escribió no sabe qué corregir.

- **La cantidad escrita**: positiva, con seis decimales como mucho y doce cifras enteras como mucho
  (`LineaDeTransferencia.LaCantidadCabe`). Se mira en la primera pasada.
- **La cantidad en unidad base**, con el factor puesto: ni cero ni de más de doce cifras enteras
  (`LineaDeTransferencia.SePuedeTransferir`). Se mira en la segunda.
- **Las dos son `transferencia-cantidad-no-valida`**, con el campo `lineas[i].cantidadIntroducida`,
  que es lo que se corrige, también cuando lo que no cabe es la base. El dominio las vuelve a mirar
  al crear la línea, y lanza.

### 5. El descarte: `DELETE`, borrado lógico y `204`

Como el del ajuste (ADR-0065 §5), punto por punto:

- **`DELETE /api/v1/inventario/transferencias/{id}`**, con `If-Match` y el permiso nuevo. Contesta
  `204` sin cuerpo, sin `Idempotency-Key`, y repetirlo da `404`.
- **Solo un borrador.** Una enviada ya escribió el libro, y se corrige con su inverso (R2). Con otro
  estado, `409` `transferencia-no-esta-en-borrador`.
- **La versión va antes que el estado** (ADR-0057 §3): con una versión vieja, `412`, aunque ya no
  sea un borrador.
- **Una marca de tiempo, `descartado_en`, auditada**, y un filtro global con nombre, `Descarte`,
  junto al de la empresa. Una transferencia descartada no sale en el listado, ni en la ficha, ni en
  enviar, recibir o anular, ni en la pregunta del cierre del ejercicio (`HayBorradoresEnAsync`).
  Eso enmienda el ADR-0053 §12: un borrador tirado no para un cierre. Los dos índices por fecha
  pasan a ser parciales, con `descartado_en IS NULL`, como el del ajuste.
- **Las dos lecturas en SQL escrito a mano** que tocan `transferencias`, el cuadre del tránsito y
  el saldo a una fecha, no ven el filtro, y no lo necesitan: miran `estado = 'Enviada'` y
  `estado <> 'Borrador'`, y solo un borrador se descarta.
- **Sin motivo ni evento**: un borrador no ha numerado, no ha movido nada y no ha bloqueado nada.

### 6. Cada rechazo de una línea dice cuál es, y en qué punta

La forma es la del ajuste (ADR-0065 §3): un `400` lleva `errors` con la ruta del campo,
`lineas[2].numeroDeSerie`, con el índice desde cero; y un `409` o un `422` lleva `actual` con el
número de la línea, desde uno.

**Las dos ubicaciones son dos campos**, `ubicacionOrigenId` y `ubicacionDestinoId`, y cada una se
pregunta con el almacén de su punta:

- **la que no existe en ese almacén** es un `400` en su campo. Una ubicación del otro almacén
  tampoco existe en este, y contesta lo mismo;
- **la bloqueada** es un `409`, y su `actual` dice la punta: `{ linea, punta }`, con `"Origen"` o
  `"Destino"`. Sin ella, la pantalla no sabría en cuál de las dos celdas ponerlo.

**Los cuerpos de `actual` pasan a `Inventario.Contracts.Lineas`**: `LineaQueFallaDto`,
`ConversionDeLaLineaDto` y el nuevo `UbicacionDeLaLineaDto`. Los usan los dos documentos, y ninguno
se publica en el OpenAPI, porque `actual` no tiene esquema.

Lo que no es de una línea va arriba del formulario, como en el ajuste: los dos almacenes, la serie,
la fecha y las líneas que faltan o que sobran.

### 7. Enviar, recibir y anular siguen sin `If-Match`

Con el motivo del ADR-0065 §7, que aquí vale igual. Las líneas de un borrador no se modifican, así
que lo que la ficha enseña solo cambia por una transición, y cada acción comprueba el estado que
pide. Quien envía un borrador que otro acaba de enviar recibe el `409`, y si ya lo habían
descartado, el `404`. Contra la carrera está el testigo `xmin`, con su `412`. El disparador es el
mismo, las líneas editables. El comentario de `TodaEscrituraDiceComoSeProtegeTests` cambia de
motivo.

### 8. La ficha y la pantalla

- **La ficha** lleva la cabecera y cada línea con su número, sus dos ubicaciones, su artículo, la
  cantidad y la unidad escritas, el factor, la cantidad en unidad base, el lote, la serie y el
  valor, cuando ya se ha enviado.
- **`accionesDisponibles(transferencia, permisos)`**, en `model/`:
  - enviar y descartar, en un borrador;
  - recibir, en una enviada, con la fecha, que empieza en hoy, el del servidor;
  - anular, en una enviada o una recibida que no es un inverso, con el motivo.

  Un inverso enlaza a la que compensa.
- **Las rutas del frontal**: `/transferencias`, `/transferencias/nueva` y `/transferencias/:id`, las
  tres diferidas, con su entrada en la navegación de Inventario. El alta va en su propia ruta por lo
  mismo que la del ajuste: la rejilla es lo que más pesa.
- **La rejilla, para teclado**, con dos columnas de ubicación, cada una con las del almacén de su
  punta. Cambiar un almacén vacía las ubicaciones de esa punta.
- **La rejilla y el buscador se comparten, no se copian.** Pasan a `features/inventario/lineas`, en
  su propio commit y antes de la pantalla, sin cambiar lo que hace el ajuste: el marco (cabecera,
  añadir y quitar, Intro en la última celda, el foco, el tope de líneas), las celdas, y el buscador
  del artículo. Sus textos van al espacio `inventario.lineas`. Cada documento pone sus columnas.
- **La tabla del listado va desde el principio en un contenedor que corre**, que es lo que el 2.20
  pondrá en las demás.

### 9. Lo que se queda fuera

- **Modificar las líneas de un borrador**, con el disparador del ADR-0065 §9.
- **Elegir el lote y la serie de lo que hay en el origen** (respuesta 2). Necesita una lectura de
  las existencias por ubicación, lote y serie, con su permiso. Su disparador es la expedición de la
  fase 4, que elegirá lo mismo.
- **La exportación del listado**, en la nota abierta de los listados que no exportan.
- **La zona de cada almacén**, antes de la fase 5: el «hoy» de la recepción es el del servidor.
- **La recepción con faltas** (ADR-0053 §4).

## Consecuencias

- **Un cliente ya no puede escribir el factor de una transferencia**, y los dos documentos que
  escriben en el libro desde una pantalla lo resuelven igual.
- **Una transferencia equivocada se tira antes de enviarla**, y deja de parar el cierre.
- **El segundo documento con pantalla de líneas no copia la primera.** Lo común vive en una
  carpeta, y el tercero, el pedido de compra, la encuentra hecha.
- **Para otro proyecto**: cuando una línea tiene dos campos del mismo tipo, el rechazo que no es de
  un campo tiene que decir a cuál se refiere. El número de la línea no basta.

## Procedencia

Las cuatro respuestas son del usuario, el 2026-10-20. El resto lo decide el agente con ellas, y casi
todo lo anunció la puerta: las rutas, el factor, la conversión retirada, los dos filtros y la
rejilla compartida. Tres cosas no estaban en la puerta:

- **el `400` de lo que no cabe en su columna** (punto 4), que el ajuste tiene desde `b1db83e` y la
  transferencia no tenía;
- **la punta en el `actual` de la ubicación bloqueada** (punto 6);
- **el listado sin `?q=`**, y ordenado también por la fecha de recepción (punto 1).
