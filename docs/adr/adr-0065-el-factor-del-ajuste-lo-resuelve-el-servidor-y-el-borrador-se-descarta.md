---
tipo: referencia
stack: [dotnet, efcore, postgresql, aspnetcore, react]
aplica_a: [inventario, organizacion, api-rest, frontal]
tags: [adr, ajuste, conversion, factor, unidad-base, descarte, borrado-logico, r3, r10, r11, adr-0014, adr-0023, adr-0048, adr-0055, adr-0057]
revisado: 2026-10-18
---

# ADR-0065: El factor del ajuste lo resuelve el servidor, y el borrador se descarta con un borrado lógico

- **Estado:** aceptado
- **Fecha:** 2026-10-18
- **Sale del ítem 2.17**, el alta del ajuste. El criterio está en `docs/PLAN.md`, en su casilla, y
  la puerta, con sus cuatro respuestas, en *Decisiones tomadas → La puerta del 2.17* y *Traídas por
  el encargo del 2026-10-18*.
- **No enmienda ningún ADR.** Desarrolla el ADR-0023, que puso la conversión como un par exacto que
  ni se invierte ni se encadena, y el ADR-0048, que puso la forma de las líneas trazables. Y deja
  escrita, con su disparador, una desviación de `api-rest.md` que ya existía desde el 2.4 (punto 7).

## Contexto

### Lo que dice el plan maestro, primero

- **El §7.4** pone el ajuste como «cabecera con estado, líneas […] y generación de los movimientos
  al confirmar». **El §7.1** pone `UnidadMedida` y `ConversionUM` (origen, destino, factor) en
  **Organización**, y **el §7.3**, la unidad base en el artículo, en Catálogo.
- **El §9**: `201` con `Location` al crear, `Idempotency-Key` obligatoria en lo que crea o confirma
  (R10), `ETag` e `If-Match` en los documentos (R11), paginación con tope, `422` para la regla de
  negocio y `403` para el permiso. Y «`204` al borrar un borrador».
- **El §10**: el listado, con la paginación y el orden en el servidor; la ficha, con la barra de
  acciones que sale de `accionesDisponibles(documento, permisos)`; y la entrada de líneas, pensada
  para teclado.
- **El §11**: el permiso es `modulo.recurso.accion`. La interfaz oculta lo que no se puede hacer, y
  el que decide es el servidor.
- **La R3**: la cantidad del libro va en la unidad base, y la línea guarda la cantidad y la unidad
  que escribió la persona, con el factor que las une.

### Lo que había escrito

- `AbrirAjuste` existía sin borde HTTP. **Tomaba el factor del cliente** en cada línea
  (`LineaDeAjusteDto.FactorAUnidadBase`), y nadie lo comparaba con la conversión. Un cliente podía
  mandar 2 donde la caja tenía 12, y el libro sumaba 2.
- `ResolverConversionUm`, en Organización, contestaba el par exacto, y también el retirado, con
  `EstaRetirada`. Ningún puerto de `Organizacion.Contracts` lo contestaba hacia fuera.
- El borde del ajuste tenía dos acciones, confirmar y anular, **sin `GET`**. Por eso ninguna de las
  dos pedía `If-Match`: el cliente no tenía de dónde sacar una versión.
- Un borrador no se podía modificar ni tirar.

### Las cuatro respuestas de la puerta

1. El puerto va en `Organizacion.Contracts`, que es el dueño del dato, y no en Catálogo.
2. La conversión retirada se rechaza en el alta. Un borrador abierto antes se confirma con el factor
   que guardó. El inverso copia el factor del original. Y la ficha enseña en cada línea el factor y
   la cantidad en unidad base.
3. El borrador equivocado se descarta: `inventario.ajuste.descartar`, borrado lógico y `204`.
   Modificar las líneas se queda fuera, con su disparador.
4. La exportación del listado se queda fuera, en una nota abierta para todos los listados.

## Decisión

### 1. El puerto: `IConsultaDeConversiones`, en `Organizacion.Contracts/Unidades`

```csharp
Task<IReadOnlyDictionary<ParDeUnidades, ConversionResuelta>> DeLosParesAsync(
    IReadOnlyCollection<ParDeUnidades> pares, CancellationToken cancelacion);

public readonly record struct ParDeUnidades(Guid OrigenId, Guid DestinoId);
public sealed record ConversionResuelta(decimal Factor, bool EstaRetirada);
```

- **Todos los pares en una consulta**, como `UnidadesBaseDeAsync` de Catálogo. El alta pregunta
  una vez por documento, no una vez por línea.
- **El par exacto, sin invertir ni encadenar**, como dijo el ADR-0023. Si existe kilo → gramo y se
  pide gramo → kilo, no hay respuesta. Inventa una conversión que nadie ha escrito, y su factor
  sería la inversa redondeada a seis decimales.
- **Un par sin conversión no está en el diccionario.** No hay un «factor nulo» que alguien pueda
  leer como cero.
- **La retirada se contesta, con su marca.** Decide quien pregunta: el alta la rechaza, y un uso
  futuro que lea lo viejo, no. Es lo mismo que hace `EstadoDeMaestro.SoloResuelveLoViejo` con los
  demás maestros.
- **No escribe, y no publica nada más de la conversión**: ni su identificador ni su versión.
- La implementación va en `Organizacion.Infrastructure`, junto a la de las unidades.

### 2. El factor lo resuelve el servidor, y el cliente ya no lo manda

`LineaDeAjusteDto` pierde `FactorAUnidadBase`. La línea del dominio lo conserva, porque es lo que
explica su cantidad en unidad base (R3). El alta lo resuelve así, después de comprobar los maestros
de cada línea:

1. **La unidad base de cada artículo distinto**, en una consulta a Catálogo
   (`IConsultaDeArticulos.UnidadesBaseDeAsync`).
2. **Si la unidad de la línea es la base, el factor es 1**, sin preguntar a Organización. Una
   conversión de una unidad a sí misma no puede existir: la rechazan el alta de la conversión y el
   dominio.
3. **Si no, el par (unidad introducida → unidad base)**, con todos los pares distintos del
   documento en una consulta.
   - Un par que no está es un **`422` `ajuste-sin-conversion`**. Es una regla de negocio y no una
     validación: el cuerpo está bien escrito, y lo que falta es un maestro que se puede dar de alta.
   - Un par retirado es un **`409` `ajuste-conversion-retirada`**, como la unidad retirada
     (`ajuste-unidad-retirada`).
   - Los dos dicen **la primera línea que falla**, por su orden, con
     `actual = { linea, unidadIntroducidaId, unidadBaseId }`. Con los dos identificadores, quien
     lo lee sabe qué conversión falta sin volver a preguntar.

**La serie unitaria se mira después del factor.** Una línea con número de serie mueve una unidad
base (ADR-0048 §3), y eso ya no se sabe antes de resolver el factor. La forma de las líneas se mira
en dos pasadas:

- la primera, antes que los maestros, como antes: el lote, el número de serie y la serie repetida;
- la segunda, con el factor resuelto: la serie unitaria.

Lo barato sigue yendo primero. Lo único que cambia es el orden entre dos fallos del mismo
documento: si una línea no es unitaria y otra repite una serie, antes salía la primera, y ahora sale
la repetida. La transferencia sigue con su factor del cliente, en una sola pasada, hasta el 2.19.

### 3. Cada rechazo de una línea dice cuál es

La pantalla pone el error en su campo, y para eso necesita la línea. Se usa la forma que la casa ya
tiene para cada clase de error:

- **Un `400` lleva `errors`**, con la ruta del campo tal como viaja en el cuerpo:
  `lineas[2].numeroDeSerie`, con el índice desde cero. Es la misma ruta que da un esquema de `zod`
  sobre el formulario, así que el error del servidor y el de la pantalla caen en el mismo campo.
  Son los de la forma (el lote, el número de serie, la serie unitaria y la repetida) y los
  maestros que no existen (la ubicación, el artículo y la unidad).
- **Un `409` o un `422` lleva `actual = { linea }`**, con el número de la línea, desde uno, que es
  el que enseña la ficha. `ErrorDeOperacion` solo deja poner campos en una validación: un conflicto
  es del recurso, no de un campo. Son la ubicación bloqueada, el artículo que no se almacena, la
  unidad retirada, la trazabilidad que no casa y las dos conversiones.

El campo de cada `type` lo decide la pantalla, con una tabla en `model/`. La trazabilidad que no
casa va al artículo, porque es su marca la que no casa.

Lo que no es de una línea va arriba del formulario: el almacén, la serie, la fecha, el motivo y las
líneas que faltan o que sobran.

### 4. Lo que no cambia

- **Confirmar no vuelve a preguntar.** El factor es el que la línea guardó en el alta, aunque la
  conversión se haya retirado o cambiado después. Lo que se confirma es lo que la ficha enseñaba.
- **El inverso copia el factor del original**, línea a línea, sin pasar por ningún puerto
  (`Ajuste.Inverso`). Dos casos lo afirman: uno con la conversión retirada después de confirmar, y
  otro con su factor cambiado.
- **El ajuste de un recuento** mueve en la unidad base, con factor 1, y no pregunta nada.

### 5. El descarte: `DELETE`, borrado lógico y `204`

- **`DELETE /api/v1/inventario/ajustes/{id}`**, con `If-Match` y el permiso nuevo
  `inventario.ajuste.descartar`. Contesta `204` sin cuerpo. No lleva `Idempotency-Key`: repetirlo
  da `404`, que es lo que es.
- **Solo un borrador.** Un ajuste confirmado ya escribió el libro, y se corrige con su inverso
  (R2). Con otro estado, `409` `ajuste-no-esta-en-borrador`.
- **La versión va antes que el estado** (ADR-0057 §3): quien descarta con una versión vieja recibe
  el `412`, aunque el ajuste ya no esté en borrador.
- **El borrado es una marca de tiempo, `descartado_en`**, y un filtro global con nombre,
  `Descarte`, junto al de la empresa. Un ajuste descartado no sale en el listado, ni en la ficha, ni
  en confirmar, ni en anular, y tampoco en la pregunta del cierre del ejercicio
  (`HayBorradoresEnAsync`): un borrador tirado no tiene por qué parar un cierre. La fila se queda,
  y la auditoría guarda quién la tiró y cuándo.
- **Por qué no es un estado, como el del recuento.** El recuento descartado sigue en su listado,
  con su motivo, porque bloqueó un almacén mientras estuvo en curso y alguien querrá saber por qué
  se soltó. Un borrador de ajuste no ha numerado, no ha movido nada y no ha bloqueado nada. El §9 lo
  llama borrar, y `AGENTS.md` dice que el borrado lógico es para los borradores y los maestros. Así
  que no lleva motivo ni evento.

### 6. Las rutas, bajo `/api/v1/inventario/ajustes`

| Ruta | Respuesta | Permiso | Mecanismos |
|---|---|---|---|
| `POST` | `201` con `Location` y el ajuste | `inventario.ajuste.abrir` | `Idempotency-Key` obligatoria |
| `GET` | una página | `inventario.ajuste.ver` | — |
| `GET {id}` | la ficha, con su `ETag` | `inventario.ajuste.ver` | — |
| `DELETE {id}` | `204` | `inventario.ajuste.descartar` | `If-Match` |

- **El alta exige la clave.** El alta del recuento la admite sin exigirla, porque el índice de uno
  en curso por almacén para el reintento. Aquí no hay nada que lo pare, y un reintento sin clave
  dejaría dos borradores iguales.
- **El alta no lleva `ETag`.** La puerta lo decía, y se corrige: ningún alta de la casa lo lleva, y
  con la clave obligatoria la repetición devolvería la versión de entonces, que es el primer motivo
  del ADR-0057 §1. La versión se lee con la ficha.
- **El listado** acepta `?page=`, `?size=`, `?sort=`, `?q=`, `?estado=` y `?almacen=`, como el
  del recuento. Por omisión va por la fecha de operación, de la última a la primera, con el
  desempate por el identificador, y también se ordena por el número. `q` mira el motivo. Las filas
  no llevan las líneas.
- **La ficha lleva las líneas enteras**, sin paginar, porque el alta las trae en una petición. El
  alta acepta como mucho 200, el tope de una página del servidor (`Paginacion.TamanioMaximo`), y
  una más es un `400`.
- **Los permisos pasan de 109 a 112.** Entran en `PermisosDeInventario.Todos`, y con ellos en el
  rol del sistema. El humo los cuenta en los dos arranques.

### 7. Confirmar y anular siguen sin `If-Match`, y se escribe por qué

`api-rest.md` pide `If-Match` en las acciones que cambian el estado de un documento. Confirmar y
anular el ajuste no lo pedían desde el 2.4 y el 2.5, y el motivo escrito era que el ajuste no tenía
`GET`. Desde aquí lo tiene, así que el motivo hay que cambiarlo, o cambiar las dos acciones.

**Se quedan como están, con otro motivo.** `If-Match` protege de pisar un cambio que no se ha visto.
Lo que la ficha enseña de un borrador solo cambia por una transición de estado (confirmar, anular o
descartar), porque las líneas no se modifican, y cada acción comprueba el estado que pide. Quien
confirma un borrador que otro acaba de confirmar recibe el `409`. Si ya lo habían descartado,
recibe el `404`. La pregunta que contestaría el `If-Match` ya la contesta la máquina de estados.
Contra la carrera, está el testigo `xmin`, con su `412`.

**El disparador es el mismo que el de las líneas editables** (punto 9). El día que una línea se
pueda cambiar, confirmar tendría que haberla visto, y pasa a pedir los dos mecanismos, como la
confirmación del recuento (ADR-0057). El comentario de `TodaEscrituraDiceComoSeProtegeTests` lo
dice con este motivo.

**El descarte sí lleva `If-Match`**, aunque por el mismo argumento le bastaría el estado. Es un
`DELETE`, que la casa protege siempre con la versión, como el de una línea del recuento. Y no
cuesta nada: la ficha ya trae el `ETag`.

### 8. La ficha y la pantalla

- **La ficha** lleva la cabecera, con el recuento de origen si lo hay, y cada línea con su número,
  su ubicación, su artículo, la cantidad y la unidad introducidas, **el factor y la cantidad en
  unidad base**, el coste, el lote, la serie y el valor, cuando ya se ha confirmado.
- **`accionesDisponibles(ajuste, permisos)`**, en `model/`, decide la barra de acciones:
  - confirmar y descartar, en un borrador;
  - anular, en uno confirmado que no es un inverso ni el ajuste de un recuento.
- **Las rutas del frontal son tres, y las tres se cargan en diferido**:
  - `/ajustes`, el listado, con `inventario.ajuste.ver`;
  - `/ajustes/nuevo`, el alta, con `inventario.ajuste.abrir`. Va en su propia ruta porque la
    rejilla de líneas es lo que más pesa, y el listado no tiene por qué cargarla;
  - `/ajustes/:id`, la ficha, con `inventario.ajuste.ver`.
- **La rejilla de líneas, para teclado** (§10): tabulador entre celdas, Intro en la última añade una
  línea, y el artículo se busca por el filtro de texto del listado de Catálogo, que mira el código y
  la descripción.
- **Los maestros que el recuento ya leía** (almacenes, series, ubicaciones, artículos, unidades y
  sus nombres) pasan de `inventario/recuentos` a `inventario/maestros`, porque ahora los leen dos
  documentos. El cambio va en su propio commit, sin cambiar nada de lo que hacen.
- **Confirmar y anular** mandan su `Idempotency-Key`. **Descartar** manda el `If-Match` de la
  ficha, y pide confirmación antes, porque no se puede deshacer desde la pantalla.

### 9. Lo que se queda fuera

- **Modificar las líneas de un borrador.** Es un ítem por sí solo: lleva `If-Match` en cada línea,
  como el recuento, y cambia confirmar (punto 7). **Su disparador**: el primer documento de la fase
  3 con líneas editables, el pedido de compra, que necesitará lo mismo; o alguien que pida corregir
  un borrador de ajuste sin tirarlo. Hasta entonces, un borrador equivocado se descarta y se escribe
  otro.
- **La exportación del listado** (§10), en la nota abierta de los listados que no exportan.
- **El factor de la transferencia**, que sigue llegando del cliente hasta el 2.19. Ese ítem usa este
  mismo puerto.

## Consecuencias

- **Un cliente ya no puede escribir un factor**, y el libro suma lo que dice la conversión del día
  del alta. Una conversión corregida después no cambia las cantidades de ayer (R3).
- **Un borrador equivocado se puede tirar**, y deja de estar en el listado y de parar el cierre del
  ejercicio.
- **Es el primer borrado lógico de la casa.** Los maestros se bloquean o se retiran, y los
  documentos cambian de estado. Un filtro global con nombre, al lado del de la empresa, es lo que la
  convención pide. Ningún módulo se lo salta, porque `IgnoreQueryFilters` sigue prohibido.
- **Una desviación de `api-rest.md` queda escrita con su disparador** (punto 7), en vez de quedar
  apoyada en un motivo que ya no es verdad.
- **Para otro proyecto**: lo que puede calcular el servidor no lo manda el cliente. Si un cálculo
  depende de un maestro, el cliente lo resuelve contra la copia que tiene en caché, y esa copia
  puede ser vieja.

## Procedencia

Las cuatro respuestas son del usuario, el 2026-10-18, y el resto lo decide el agente con ellas: el
puerto, las rutas, la forma de los rechazos, el descarte y la desviación del punto 7. Dos cosas se
apartan de lo que la puerta dijo que decidiría:

- **el alta sin `ETag`** (punto 6);
- **la ruta del alta en el frontal**, que la puerta no nombraba (punto 8).
