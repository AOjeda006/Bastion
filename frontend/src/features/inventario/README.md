# `inventario` — lo que hay, dónde y cuánto

Espeja el módulo **Inventario** del backend (`Bastion.Inventario.*`, `/api/v1/inventario/`). Dentro,
una carpeta por recurso; entre funcionalidades, nada: `inventario` no importa de `catalogo` ni de
`organizacion`, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

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
['recuentos', 'maestro', clase, id]              → clavesDeRecuentos.maestro(clase, id)
['recuentos', 'almacenes']                       → clavesDeRecuentos.almacenes()
['recuentos', 'series']                          → clavesDeRecuentos.series()
```

`una(id)` alcanza la ficha **y** sus líneas, que es lo que cambia al contar, confirmar, anular y
descartar: cada escritura invalida `una(id)` y `listas()`, y no los maestros, que no han cambiado.
El recuento no lleva `staleTime`: se lee para escribir sobre él, y su versión tiene que ser la de
ahora. Los maestros, cinco minutos (`VIDA_DE_UN_MAESTRO`).

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
