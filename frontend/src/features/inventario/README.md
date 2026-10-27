# `inventario` — lo que hay, dónde y cuánto

Espeja el módulo **Inventario** del backend (`Bastion.Inventario.*`, `/api/v1/inventario/`). Dentro,
una carpeta por recurso, y dos más: `maestros`, con lo que leen todas, y `lineas`, con la rejilla de
las altas que llevan líneas. Entre funcionalidades, nada: `inventario` no importa de `catalogo` ni
de `organizacion`, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

## `maestros` — los nombres de lo que es de otro módulo

**Propósito.** Preguntar a sus dueños por los almacenes, las series, las ubicaciones, los artículos y
las unidades que nombran u ofrecen las pantallas del inventario (ADR-0055 §12). No tiene rutas. Vivía
en `recuentos`, y salió de ahí cuando lo empezaron a leer dos documentos (ADR-0065 §8).

### Claves de consulta

`maestros/api/claves.ts`:

```
['maestros-del-inventario']                        → clavesDeMaestros.todo
['maestros-del-inventario', 'maestro', clase, id]  → clavesDeMaestros.maestro(clase, id)
['maestros-del-inventario', 'almacenes']           → clavesDeMaestros.almacenes()
['maestros-del-inventario', 'series']              → clavesDeMaestros.series()
['maestros-del-inventario', 'ubicaciones']         → clavesDeMaestros.ubicaciones()
['maestros-del-inventario', 'unidades']            → clavesDeMaestros.unidades()
['maestros-del-inventario', 'articulos', busqueda] → clavesDeMaestros.articulos(busqueda)
```

Nada las invalida, porque nada del inventario escribe un maestro: duran cinco minutos
(`VIDA_DE_UN_MAESTRO`). Son de todo el inventario, así que lo que nombró una pantalla no se vuelve a
pedir en otra.

## `lineas` — la rejilla de las altas con líneas

**Propósito.** Lo que comparten las altas de los documentos con líneas, el ajuste y la
transferencia: el marco de la rejilla, sus celdas, el buscador del artículo y los campos de la
cabecera (ADR-0068 §8). No tiene rutas, y su única consulta es la del buscador, que cuelga de
`clavesDeMaestros.articulos(busqueda)`. Vivía en `ajustes`, y salió de ahí cuando lo empezó a
necesitar la transferencia. El ajuste hace lo mismo que antes, y lo dicen sus casos, que salieron
sin tocar. Sus textos van en el espacio `inventario.lineas`.

- **El marco pone lo que es de toda rejilla** (`MarcoDeLineas`): la cabecera, el número de cada
  línea, quitarla, añadir otra, el foco, Intro en la última celda y el tope de 200 líneas. **Cada
  documento pone sus columnas** y sus celdas, el nombre de su tabla y la frase de su tope.
- **Las celdas son tres**, de texto, de lista y del artículo, y cada una lleva su nombre entero,
  con la línea («Cantidad de la línea 2»), y su error debajo (`useCeldasDeLaLinea`). El de un
  rechazo del servidor va al mismo sitio.
- **El artículo se busca**, con un _combobox_ que pregunta al listado del catálogo (`?q=`, que mira
  el código y la descripción) 250 ms después de la última tecla, y ofrece los diez primeros. Elegirlo
  pone su unidad base, que no necesita conversión, y abre el lote o el número de serie si se sigue
  así. Escribir otra vez deja la línea sin artículo hasta que se elige otro: lo escrito es una
  búsqueda, no un artículo.
- **La rejilla es de teclado** (§10 del plan maestro). El tabulador va de celda en celda, e Intro en
  la última celda que se puede usar añade una línea, con el foco en su primera celda, o pasa a la
  siguiente. La línea nueva se pinta con `flushSync` y el foco se mueve desde el manejador, no desde
  el efecto de `append`: movido en mitad del pintado, el `blur` de la celda que se deja corría dentro
  de él. El cuerpo de la tabla llega del manejador, y no de la referencia, porque las celdas las
  pinta el documento, y el compilador de React no puede saber que no llamarán a Intro al pintar.

## `recuentos` — contar el almacén y ajustar la diferencia

**Propósito.** Abrir el recuento de un almacén entero, contar sus líneas, confirmar el ajuste de lo
que no cuadra, y anularlo o descartarlo (ítem 2.12, `docs/adr/adr-0055`). Es la primera pantalla del
inventario.

### Rutas

| Ruta             | Exigencia                         | Título             |
| ---------------- | --------------------------------- | ------------------ |
| `/recuentos`     | permiso `inventario.recuento.ver` | Recuentos          |
| `/recuentos/:id` | permiso `inventario.recuento.ver` | Ficha del recuento |

Las dos van **diferidas** (`lazy` en `app/rutas.tsx`): su código no entra en el arranque. La ficha
no sale en la navegación: se llega desde la fila del listado o desde el alta.

Dentro, cada acción pide la suya: el alta, `inventario.recuento.abrir`; contar una línea,
`inventario.recuento.contar`; y confirmar, anular y descartar, `inventario.recuento.confirmar`,
`.anular` y `.descartar`. Quien solo ve lee la ficha entera y no encuentra ni un botón. La interfaz
esconde, el servidor autoriza.

Parámetros de URL:

- en el listado, `?pagina=`, `?tamanio=`, `?estado=` y `?almacen=`;
- en la ficha, `?pagina=`, `?tamanio=` y `?solo=` (`sin-contar` o `teorico-cambiado`).

Llevan **los nombres y los valores de la API** (`?estado=EnCurso`, no `enCurso`): un enlace copiado
de la barra de direcciones vale para probar la API a mano. Ninguno es sensible (`docs/adr/adr-0025`).
Lo que no se reconoce se ignora: ni se manda ni se enseña.

### Claves de consulta

`recuentos/api/claves.ts`, jerárquicas:

```
['recuentos']                                    → clavesDeRecuentos.todo
['recuentos', 'lista']                           → clavesDeRecuentos.listas()
['recuentos', 'lista', listado]                  → clavesDeRecuentos.lista(listado)
['recuentos', 'una', id]                         → clavesDeRecuentos.una(id)
['recuentos', 'una', id, 'ficha']                → clavesDeRecuentos.ficha(id)
['recuentos', 'una', id, 'lineas', listado]      → clavesDeRecuentos.lineas(id, listado)
```

`una(id)` alcanza la ficha **y** sus líneas, que es lo que cambia al contar, confirmar, anular y
descartar: cada escritura invalida `una(id)` y `listas()`, y no los maestros, que no han cambiado.
El recuento no lleva `staleTime`: se lee para escribir sobre él, y su versión tiene que ser la de
ahora. Los maestros cuelgan de `clavesDeMaestros`, con sus cinco minutos.

## Lo que no es evidente

- **Los nombres salen de sus dueños, y con caché** (ADR-0055 §12). El recuento identifica el
  almacén, el artículo, la ubicación y la unidad por su `uuid`. Esta funcionalidad no puede importar
  las del catálogo ni las de la organización, así que pregunta a su API pública, una petición por
  maestro distinto, y la clave es suya. Es un N+1 a sabiendas: en una página de veinte líneas de un
  mismo almacén salen como mucho veinte artículos y unas pocas ubicaciones y unidades, y la segunda
  vez ya están en caché. Sin el permiso de ver ese maestro no se pregunta —el servidor contestaría
  `403` a cada fila— y se pinta el identificador: una celda en blanco parecería un dato que falta.
- **Un recuento sin número se nombra por su almacén y su día de apertura.** El número lo recibe al
  confirmarse (ADR-0055 §1.4). Con un solo recuento en curso por almacén, no hay dos iguales.
- **Vacío no es cero.** El campo de lo contado vacío pide que se escriba algo; el cero se escribe.
  Una pieza con número de serie se cuenta 0 o 1. Lo escrito se valida con la misma regla que lo
  convierte en lo que viaja (`model/cantidad.ts`), con coma o con punto, y viaja con punto.
- **La versión que viaja al contar es la de lo que se ve.** El listado de líneas no trae el `ETag`
  de cada una, así que abrir el campo lee la línea, y mientras está abierto la fila y el campo
  enseñan lo que trajo esa lectura. Guardar manda esa versión en el `If-Match`, sin volver a leerla:
  leerla justo antes de mandar tomaría la de quien acabe de contar, y el `PUT` pisaría su cifra. Si
  otra persona la contó con el campo abierto, el `412` cierra el campo, el aviso dice lo que contó, y
  la ficha se vuelve a leer.
- **La confirmación lleva la versión del recuento y la huella del teórico** (ADR-0057). Si el stock
  se ha movido desde que se leyó la ficha, el servidor contesta `409` y la pantalla lleva a la vista
  `?solo=teorico-cambiado`, que enseña, línea a línea, lo que había al contar, lo que hay ahora y el
  cambio. Confirmar otra vez manda la huella nueva **con otra clave de idempotencia**: es otra
  operación. Con líneas sin contar, el `422` lleva a `?solo=sin-contar`.
- **El tránsito se dice por línea mientras se cuenta.** Lo que está en camino entre almacenes no está
  en ninguno de los dos, y explica una diferencia que no es una pérdida. Cerrado el recuento, la
  columna no sale: ya no se mueve.
- **Las vistas parciales son de un recuento en curso.** Cerrado, el teórico ya no cambia y no queda
  nada por contar, así que un enlace viejo con `?solo=` enseña todas las líneas, y confirmar,
  anular o descartar vuelve a la vista entera desde la primera página.
- **La clave de idempotencia va con el intento** (`shared/api/intento.ts`): repetir el mismo cuerpo
  repite la clave, cambiarlo la estrena, y tras una escritura que sale bien se olvida. Vale para
  abrir, confirmar, anular y descartar.
- **El rechazo que es de un campo va en ese campo, con el foco.** El almacén que ya se está contando,
  el bloqueado o el que ya no existe, en el almacén; un motivo vacío o largo, en el motivo; una
  cifra que no vale, en lo contado. Una serie cerrada o sin ejercicio va arriba, en un `alert`. Los
  mensajes del esquema son los `type` de la API, así que el texto sale de `errores.tipos` y es el
  mismo lo diga el formulario o el servidor (ADR-0030).
- **Los desplegables del alta empiezan en «Sin elegir»**, no en la primera serie: abrir con la que el
  navegador eligió por su cuenta numeraría el recuento donde nadie lo pidió. Solo se ofrecen las
  series **activas** de cada clase (`RecuentoDeInventario` y `AjusteDeInventario`).
- **Confirmar, anular y descartar se preguntan en el sitio**, no en una ventana, con el foco en
  «Cancelar»; Escape cierra y devuelve el foco al botón. Anular y descartar piden el motivo.
- **El aviso de lo hecho y el del fallo comparten región.** La región `status` está siempre montada
  para que el lector de pantalla la oiga cambiar; el fallo va en un `alert` y se lleva el foco.
- **El estado tiene un quinto valor que la API no emite**, `desconocido`, por lo mismo que el tipo de
  un artículo: el enumerado viaja como texto y el frontal se despliega aparte del backend.
- **El alta va siempre en el mismo sitio del árbol.** Si cambiara de rama al llegar el listado, React
  la montaría otra vez y se llevaría lo escrito y los avisos de sus campos.

### Límites conocidos

- **Los desplegables del alta piden una página de 200**, que es el tope del servidor. Una empresa con
  más almacenes o series no verá los siguientes. El disparador es la primera que se acerque a ese
  número: entonces el almacén se busca en vez de elegirse de una lista.
- **Dos series activas con el mismo código en ejercicios distintos salen iguales en el alta.** La
  serie trae su `ejercicioId`, y nombrar el ejercicio sería preguntar a otra API más. Cerrar un
  ejercicio no cierra sus series, así que puede pasar en cuanto se abre el segundo ejercicio: ese es
  el disparador, y entonces la opción tiene que llevar el año.
- **Añadir y quitar líneas no están en la pantalla**, aunque la API los tiene
  (`POST /recuentos/{id}/lineas` y `DELETE /recuentos/{id}/lineas/{lineaId}`). El recuento se abre
  con sus líneas precargadas; lo que aparece sin línea solo se puede añadir hoy por la API.
- Los DTO salen de `shared/api/esquema.ts`, que se **genera** (`npm run api`), y se traducen al
  modelo de vista en la capa `api`: los tipos del contrato no salen de ahí.
- La empresa **no** forma parte de ninguna clave de consulta: va dentro del testigo y quien filtra es
  el servidor (R8). El cambio de empresa vacía la caché entera.

## `saldos` — cuánto había al final de un día

**Propósito.** Enseñar el stock de la empresa al final de un día, hoy o uno pasado, por artículo y
almacén, con su valor y con lo que viajaba hacia cada almacén (ítem 2.14, `docs/adr/adr-0063`). Solo
lee: no tiene ningún botón que esconder.

### Rutas

| Ruta      | Exigencia                      | Título         |
| --------- | ------------------------------ | -------------- |
| `/saldos` | permiso `inventario.saldo.ver` | Saldo de stock |

Va **diferida**, como las del recuento. Parámetros de URL: `?fecha=` (`AAAA-MM-DD`), `?almacen=`,
`?articulo=`, `?pagina=` y `?tamanio=`. Los tres primeros con los nombres de la API, y ninguno es
sensible (`docs/adr/adr-0025`). Lo que no se reconoce se ignora; **un día futuro, no**: tiene forma
de día, y quien dice que el saldo del futuro no existe es el servidor, con `saldo-con-fecha-futura`.

### Claves de consulta

`saldos/api/claves.ts`:

```
['saldos']                   → clavesDeSaldos.todo
['saldos', 'lista']          → clavesDeSaldos.listas()
['saldos', 'lista', listado] → clavesDeSaldos.lista(listado)
['saldos', 'articulos']      → clavesDeSaldos.articulos()
```

Nada escribe, así que nada invalida: el saldo de un día pasado no cambia, y el de hoy se vuelve a
pedir al volver a la pantalla. Los nombres del almacén y del artículo de cada fila, y la lista de
almacenes del filtro, **no cuelgan de aquí**: salen por el camino del recuento, con sus claves
(`clavesDeMaestros.maestro` y `.almacenes()`), así que lo que nombró una pantalla no se vuelve a
pedir en la otra.

### Lo que no es evidente

- **El día que se pinta es el que contesta el servidor**, no el del campo. Sin `?fecha=` no se manda
  ninguno, el servidor contesta con su hoy y la respuesta lo dice: la tabla lleva ese día en su
  título.
- **Hoy es el día UTC** (ADR-0063 §3, nota abierta para el cierre de la fase). El campo no deja
  elegir un día posterior al de hoy del servidor, y por omisión enseña ese día, no el del calendario
  de quien mira: entre la medianoche de aquí y la de Greenwich, ofrecer el local sería ofrecer un
  `400` seguro.
- **Lo que viaja no está sumado.** Va en sus dos columnas, y las dos señalan la frase de debajo de la
  tabla que lo dice. Es lo que salió de otro almacén hacia este y al final del día aún no había
  llegado ni vuelto.
- **Un día futuro no se reintenta.** El fallo lo dice con el texto del `type`, que dice qué día
  elegir, y no ofrece «Volver a intentarlo»: daría el mismo `400`.
- **Un importe lleva los decimales de su divisa y hasta cuatro**, los del libro. Una divisa que `Intl`
  no admite se pinta con su código detrás, en vez de romper la pantalla.
- **El almacén y el artículo se ofrecen solo a quien puede verlos.** Sin ese permiso no hay filtro ni
  se pregunta su nombre, y la celda lleva el identificador.

### Límites conocidos

- **El filtro de artículos pide una página de 200**, como los desplegables del alta del recuento.
  Una empresa con más artículos no verá los siguientes en la lista, aunque sí en la tabla. El
  disparador es el mismo: la primera que se acerque a ese número, y entonces el artículo se busca.

## `ajustes` — corregir el stock a mano

**Propósito.** Abrir un ajuste a mano —el almacén, la serie, el día, el motivo y sus líneas—, y
confirmarlo, anularlo o descartarlo (ítem 2.17, `docs/adr/adr-0065`). Los ajustes que genera un
recuento salen también aquí, y se anulan desde su recuento.

### Rutas

| Ruta             | Exigencia                         | Título                |
| ---------------- | --------------------------------- | --------------------- |
| `/ajustes`       | permiso `inventario.ajuste.ver`   | Ajustes de inventario |
| `/ajustes/nuevo` | permiso `inventario.ajuste.abrir` | Nuevo ajuste          |
| `/ajustes/:id`   | permiso `inventario.ajuste.ver`   | Ficha del ajuste      |

Las tres van **diferidas**. Solo el listado sale en la navegación: el alta se abre desde él, y la
ficha, desde su fila o al acabar el alta. Confirmar, anular y descartar piden
`inventario.ajuste.confirmar`, `.anular` y `.descartar`. El alta pide además poder ver lo que
ofrece —los almacenes, las series, las ubicaciones, las unidades y los artículos—, y sin uno de esos
permisos lo dice en vez de enseñar listas vacías, que parecerían una empresa sin almacenes.

Parámetros de URL del listado: `?pagina=`, `?tamanio=`, `?estado=` y `?almacen=`, con los nombres y
los valores de la API, como en el recuento; `?busqueda=`, que busca en el motivo y viaja como `q`; y
`?orden=`, con el valor de la API (`fechaDeOperacion`, `-numero` o `numero`; sin él, la fecha, del
último día al primero). Ninguno es sensible (`docs/adr/adr-0025`), y lo que no se reconoce se ignora.

### Claves de consulta

`ajustes/api/claves.ts`:

```
['ajustes']                   → clavesDeAjustes.todo
['ajustes', 'lista']          → clavesDeAjustes.listas()
['ajustes', 'lista', listado] → clavesDeAjustes.lista(listado)
['ajustes', 'una', id]        → clavesDeAjustes.una(id)
```

Abrir invalida `listas()`; confirmar, anular y un fallo de cualquiera de las dos, `una(id)` y
`listas()`. Descartar **quita** `una(id)` de la caché, porque el ajuste deja de existir y volver a
pedirlo daría un `404`, e invalida `listas()`. Lo que el alta ofrece cuelga de `clavesDeMaestros`.

### Lo que no es evidente

- **El factor no se escribe**: lo pone el servidor, con la conversión del día del alta (ADR-0065
  §2), y la ficha lo enseña en cada línea junto a la cantidad en unidad base. Una unidad sin
  conversión a la base del artículo vuelve como `ajuste-sin-conversion`, en la unidad de esa línea.
- **El rechazo de una línea va en su campo, con el foco** (ADR-0065 §3). Un `400` dice la línea en
  la clave de `errors` (`lineas[2].numeroDeSerie`, desde cero), y un `409` o un `422`, en
  `actual.linea` (desde uno); el campo sale del `type`. Lo demás va arriba, en un `alert`: el
  almacén, la serie, el día, el motivo y el coste que rechaza el servidor, que no los ata a ninguna
  línea. El coste de una línea que saca existencias sí lo dice el formulario en su campo, antes de
  mandar nada.
- **Las ubicaciones que se ofrecen son las del almacén elegido.** La API de la organización no
  acota por almacén: se piden las de la empresa y se filtran aquí. Cambiar de almacén deja sin
  elegir la ubicación que era de otro, en vez de mandar una que el servidor rechazaría.
- **La rejilla y el buscador del artículo son los de `lineas`**, con las columnas del ajuste: una
  ubicación, que es la primera celda y la que recibe el foco de la línea nueva, y el coste. Un
  ajuste lleva como mucho 200 líneas, y el botón de añadir lo dice al llegar.
- **El coste lleva cuatro decimales como mucho.** El servidor redondea a cuatro lo que traiga más;
  el formulario no lo deja escribir, para que lo que se escribe sea lo que se guarda.
- **Un borrador se nombra por su almacén y su día**, como un recuento, pero aquí puede haber dos
  iguales: los distingue el motivo.
- **Confirmar y anular mandan su `Idempotency-Key` y no la versión; descartar manda la versión** en
  el `If-Match`, y se pregunta antes (ADR-0065 §7 y §8). La clave va con el intento, como en el
  recuento, y sobrevive a un fallo: el servidor solo guarda las respuestas que salen bien.
- **Descartado, el borrador deja de existir**: la ficha no se vuelve a pedir, y queda el aviso con
  el camino de vuelta al listado.
- **Una ficha por ajuste.** La ruta monta la ficha con la clave del identificador, así que el
  inverso, al que lleva el aviso de la anulación, se abre sin el aviso ni el estado del original.
- **Un inverso no se anula, y el ajuste de un recuento se anula desde su recuento**
  (`ajuste-de-un-recuento-no-se-anula`): la ficha no lo ofrece, y lleva al recuento a quien puede
  verlo.

### Límites conocidos

- **Las ubicaciones se piden en una página de 200**, como los desplegables del recuento, y de toda
  la empresa. Una empresa con más no verá las siguientes en el alta. El disparador es la primera que
  se acerque a ese número: entonces la ubicación se busca, o la API de la organización acota por
  almacén.
- **Una unidad base retirada sale «Sin elegir»** al elegir el artículo, porque el desplegable solo
  ofrece las vigentes. El servidor rechazaría esa unidad igual (`ajuste-unidad-retirada`).
- **El valor de una línea no se comprueba al abrir.** La cantidad y el coste caben cada uno en su
  columna, pero su producto, que se calcula al confirmar, puede no caber en la del valor
  (`numeric(18,4)`) con los dos cerca de sus topes.
- **El tope de 200 líneas no tiene caso de pantalla**: añadir 199 líneas en jsdom tarda 15 s, porque
  cada una repinta la rejilla entera. Lo afirman el esquema (`model/formularios.test.ts`) y el
  servidor, y el recorrido en el navegador del cierre del ítem.

## `transferencias` — llevar stock de un almacén a otro

**Propósito.** Abrir una transferencia a mano —los dos almacenes, la serie, el día en que sale y sus
líneas—, listar las de la empresa, acotarlas por su estado y por sus dos puntas, y leer cada una con
todas sus líneas, enviarla, recibirla, anularla o descartarla (ítem 2.19, `docs/adr/adr-0068`).
Una transferencia sale del origen al enviarla, viaja, y entra en el destino al recibirla.

### Rutas

| Ruta                    | Exigencia                                | Título                    |
| ----------------------- | ---------------------------------------- | ------------------------- |
| `/transferencias`       | permiso `inventario.transferencia.ver`   | Transferencias            |
| `/transferencias/nueva` | permiso `inventario.transferencia.abrir` | Nueva transferencia       |
| `/transferencias/:id`   | permiso `inventario.transferencia.ver`   | Ficha de la transferencia |

Las tres van **diferidas**, como las del ajuste. Solo el listado sale en la navegación: el alta se
abre desde él, la ficha, desde su fila o al acabar el alta, y la de una inversa, desde el aviso de
la anulación. Enviar, recibir, anular y descartar piden `inventario.transferencia.enviar`,
`.recibir`, `.anular` y `.descartar`. El alta pide además poder ver lo que ofrece, como la del
ajuste, y sin uno de esos permisos lo dice.

Parámetros de URL del listado: `?pagina=`, `?tamanio=`, `?estado=`, `?origen=` y `?destino=`, con
los nombres y los valores de la API; y `?orden=`, con el valor de la API (`fechaDeEnvio`,
`fechaDeRecepcion` o `numero`, con un guion delante si es de mayor a menor; sin él, el envío, del
último día al primero). Ninguno es sensible (`docs/adr/adr-0025`), y lo que no se reconoce se
ignora.

### Claves de consulta

`transferencias/api/claves.ts`:

```
['transferencias']                   → clavesDeTransferencias.todo
['transferencias', 'lista']          → clavesDeTransferencias.listas()
['transferencias', 'lista', listado] → clavesDeTransferencias.lista(listado)
['transferencias', 'una', id]        → clavesDeTransferencias.una(id)
```

Abrir invalida `listas()`; enviar, recibir, anular y un fallo de cualquiera, `una(id)` y
`listas()`. Descartar **quita**
`una(id)` de la caché, porque la transferencia deja de existir y volver a pedirla daría un `404`, e
invalida `listas()`. Los almacenes del filtro y los nombres de cada fila cuelgan de
`clavesDeMaestros`.

### Lo que no es evidente

- **Dos filtros de almacén y no uno** (ADR-0068 §1): quien recibe busca lo que llega a su almacén, y
  quien envía, lo que sale del suyo. Juntos dan lo que va de uno a otro. Sin el permiso de ver los
  almacenes no salen, y cada fila lleva sus identificadores.
- **No hay búsqueda.** El alta no tiene ningún texto libre, como el motivo del ajuste, y la API no
  filtra por texto: un `?q=` con algo escrito es un `400` (ADR-0072).
- **Por la fecha de recepción, las que no han llegado van primero de la última a la primera, y
  últimas al revés**, que es lo que hace PostgreSQL con un nulo. Las dos fechas van en su columna, y
  la de recepción está vacía mientras no llega.
- **La tabla corre dentro de su contenedor**, y no la página: con dos almacenes y dos fechas es más
  ancha que un móvil (WCAG 1.4.10, ADR-0068 §8). La de las líneas de la ficha, también.
- **La ficha trae todas sus líneas** (ADR-0068 §1), cada una con sus dos huecos, el que deja y el
  que ocupa, y con el factor y la cantidad en unidad base que puso el servidor (§2). El valor es
  del envío, así que un borrador no lo enseña.
- **Recibir pide el día**, que empieza en hoy, el del servidor, y no va antes del envío. Lo que se
  arregla cambiando el día va en el día, con el foco: un día de mañana, uno de antes del envío, y
  los rechazos del servidor que dependen del día —sin ejercicio, ejercicio cerrado, por detrás del
  último movimiento—. Lo demás va arriba.
- **Enviar, recibir y anular mandan su `Idempotency-Key` y no la versión; descartar manda la
  versión** en el `If-Match`, y se pregunta antes (ADR-0068 §7). La clave va con el intento, como en
  el ajuste.
- **La inversa nace recibida, con el motivo, y no se anula**: deshacerla sería volver a hacer la
  transferencia. Su ficha no ofrece nada, y lleva a la original.
- **Descartada, la transferencia deja de existir**, y **una ficha por transferencia**: lo mismo que
  en el ajuste, por lo mismo.
- **Una ubicación por punta en cada línea**: la de origen ofrece las del almacén de origen, y la de
  destino, las del de destino. Cambiar el almacén de una punta deja sin elegir las ubicaciones de
  esa punta que eran del otro, y la otra punta no se toca. La rejilla es la de `lineas`, sin coste:
  lo que viaja se valora al enviarlo.
- **El mismo almacén en las dos puntas lo dice el formulario en el destino**, en cuanto están los
  dos, aunque falte lo demás: `zod` mira la regla del objeto aunque falle un campo. La salta tras
  un error de tipo, que el formulario no produce, y con más líneas que el tope, que se dice solo,
  desde la 4.6.6. Dos sin elegir no son el mismo almacén, y la regla lo dice ella misma. El mismo
  rechazo, si lo dice el servidor, va arriba.
- **El rechazo de una línea va en su campo, con el foco** (ADR-0068 §6), como en el ajuste: un `400`
  dice la línea en la clave de `errors`, desde cero, y un `409` o un `422`, en `actual.linea`, desde
  uno. El de una ubicación dice además su punta: la que no existe, en la ruta
  (`lineas[0].ubicacionDestinoId`), y la bloqueada, en `actual.punta`. Una ubicación sin punta va
  arriba, porque elegir una de las dos celdas sería inventárselo.
- **La cantidad no lleva signo**: lo negativo es del inverso, que se construye anulando.

### Límites conocidos

- **Un borrador se nombra por sus dos almacenes y el día en que sale**, y puede haber dos iguales:
  la fila no tiene nada más que los distinga. El número lo recibe al enviarse.
- **El lote y el número de serie se escriben a mano** (respuesta 2 de la puerta del 2.19). Elegirlos
  de lo que hay en el origen espera a la expedición de la fase 4, que es su disparador.
- **Las ubicaciones se piden en una página de 200, y de toda la empresa**, como en el alta del
  ajuste, con el mismo disparador.
- **El tope de 200 líneas no tiene caso de pantalla**, por lo mismo que en el ajuste: lo afirman el
  esquema (`model/formularios.test.ts`) y el servidor.
