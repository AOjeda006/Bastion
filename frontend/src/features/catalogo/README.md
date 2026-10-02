# `catalogo` — qué se vende y cómo está clasificado

Espeja el módulo **Catálogo** del backend (`Bastion.Catalogo.*`, `/api/v1/catalogo/`). Dentro, una
carpeta por recurso; entre funcionalidades, nada: `catalogo` no importa de `organizacion` ni al
revés, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

Son **tres recursos y no uno** porque son tres cosas: un artículo es lo que se factura, una
categoría es dónde está colocado, y una tarifa es a cuánto se vende. Dentro de la funcionalidad sí
se importan entre ellos —`articulos/ui` pide a `categorias/api` el nombre de la rama por la que se
está filtrando—, que es exactamente lo que distingue «una funcionalidad» de «una carpeta».

## `articulos` — los artículos de la empresa activa

**Propósito.** Enseñar los artículos con los que se opera, paginados y filtrados, cambiar la
trazabilidad de uno mientras todavía se puede (ítem 2.9), y llevar los códigos de barras que lo
identifican (ítem 2.10).

### Rutas

| Ruta                          | Exigencia                             | Título                         |
| ----------------------------- | ------------------------------------- | ------------------------------ |
| `/articulos`                  | permiso `catalogo.articulo.ver`       | Artículos                      |
| `/articulos/:id/trazabilidad` | permiso `catalogo.articulo.modificar` | Trazabilidad del artículo      |
| `/articulos/:id/gtin`         | permiso `catalogo.articulo.ver`       | Códigos de barras del artículo |

Las dos últimas no salen en la navegación: se llega desde la fila del artículo. Al cambio de la
trazabilidad, con un enlace que solo ve quien puede modificar; a los códigos de barras, con uno que
ve todo el que ve el listado. Dentro, el formulario de alta pide `catalogo.codigo-barras.agregar` y
los botones de quitar, `catalogo.codigo-barras.quitar`.

Parámetros de URL: `?pagina=`, `?tamanio=`, `?busqueda=` y `?categoria=`. En la URL y no en un
`useState` por lo de siempre: el listado acotado se puede pegar en un correo, la flecha de atrás
deshace el filtro y una recarga no pierde el sitio. **Ninguno de los dos criterios es sensible**
(`docs/adr/adr-0025`): uno es un trozo de código o descripción —lo que sale impreso en una
factura— y el otro es el identificador de una rama del árbol de la propia empresa.

### Claves de consulta

`articulos/api/claves.ts`, jerárquicas:

```
['articulos']                                                    → clavesDeArticulos.todo
['articulos', 'lista']                                           → clavesDeArticulos.listas()
['articulos', 'lista', { pagina, tamanio, busqueda, categoriaId }] → clavesDeArticulos.lista(listado)
['articulos', 'una', id]                                         → clavesDeArticulos.una(id)
['articulos', 'codigosDeBarras', id]                             → clavesDeArticulos.codigosDeBarras(id)
```

`staleTime` de cinco minutos en el listado: un artículo es dato maestro, se da de alta y se queda
ahí. La ficha (`una`) no lo lleva: se lee para escribir sobre ella, y su versión tiene que ser la de
ahora. Los códigos de barras van aparte de la ficha porque no la cambian ni cambian su versión: el
alta y la baja invalidan solo los suyos.

## `categorias` — el árbol de clasificación

**Propósito.** Enseñar la forma del árbol y dejar saltar de una rama a sus artículos.

### Rutas

| Ruta          | Exigencia                        | Título     |
| ------------- | -------------------------------- | ---------- |
| `/categorias` | permiso `catalogo.categoria.ver` | Categorías |

Parámetros de URL: `?pagina=` y `?tamanio=`.

### Claves de consulta

```
['categorias']                              → clavesDeCategorias.todo
['categorias', 'lista']                     → clavesDeCategorias.listas()
['categorias', 'lista', { pagina, tamanio }] → clavesDeCategorias.lista(paginacion)
['categorias', 'una', id]                   → clavesDeCategorias.una(id)
```

## `tarifas` — a cuánto se vende, y desde cuándo

**Propósito.** Enseñar los tramos de tarifa de la empresa activa y **cuál de ellos rige hoy**.

### Rutas

| Ruta       | Exigencia                     | Título  |
| ---------- | ----------------------------- | ------- |
| `/tarifas` | permiso `catalogo.tarifa.ver` | Tarifas |

Parámetros de URL: `?pagina=`, `?tamanio=`, `?busqueda=` y `?codigo=`. Los dos filtros son **dos
preguntas distintas**, igual que en la API: `?busqueda=` viaja como `q` y busca texto parcial en el
código y el nombre; `?codigo=` es igualdad exacta y responde «enséñame los tramos de ÉSTA».
Ninguno es sensible (`docs/adr/adr-0025`): el código de una lista de precios de la propia empresa
—`PVP`, `MAYORISTA`— no es un dato de ninguna persona, y ese nombre **tuvo que añadirse** a la lista
declarada de `NingunCriterioSensibleViajaEnLaUrlTests`.

### Claves de consulta

```
['tarifas']                                                   → clavesDeTarifas.todo
['tarifas', 'lista']                                          → clavesDeTarifas.listas()
['tarifas', 'lista', { pagina, tamanio, busqueda, codigo }]    → clavesDeTarifas.lista(listado)
```

`staleTime` de cinco minutos, como los otros dos: una tarifa es dato maestro.

## Lo que no es evidente

- **El árbol lo compone el frontal, no el servidor.** La API devuelve la lista **plana** con el
  padre de cada categoría; `categorias/model/arbol.ts` la recorre una vez y saca las filas con su
  profundidad. Es la otra cara de haber modelado el árbol como lista de adyacencia en la base de
  datos (el porqué, con su alternativa costeada, está en `docs/PLAN.md`): un árbol de una empresa
  cabe entero en una o dos páginas, y servirlo montado obligaría al servidor a recorrerlo entero en
  cada lectura para devolver justo lo que se recompone sin él.
- **Y esa composición termina siempre.** Lleva un conjunto de ya visitadas, así que unos datos con
  un ciclo —imposibles por la API, posibles por un `UPDATE` a mano o una importación— salen como
  filas sueltas en vez de colgar la pestaña. Si alguien quita ese conjunto, la función revienta con
  una frase que lo dice: un descenso sin cota que se manifiesta como «la pantalla no responde» es
  un síntoma que no señala a nadie.
- **Una categoría a la que no se le ve el padre se pinta igualmente**, marcada. Pasa en cuanto hay
  más categorías que sitio en una página. Descartar la fila dejaría fuera una categoría que existe,
  y eso es una categoría que alguien da de alta por segunda vez.
- **La sangría del árbol es decoración; el nivel es un dato y tiene su columna.** Un lector de
  pantalla no ve el relleno de la izquierda.
- **El filtro por categoría no se elige en `/articulos`: se llega a él desde `/categorias`.** Un
  desplegable con todas las ramas obligaría a traerse el catálogo de categorías en cada visita a la
  pantalla de artículos y a quedarse corto —sin decirlo— en la empresa que tuviera más de las que
  caben en una página. Lo que sí hay en `/articulos` es el filtro **puesto**, con el nombre de la
  rama y la salida para quitarlo.
- **Ese filtro acota por la categoría dicha y NO por su subárbol.** Es consecuencia directa del
  modelo de árbol elegido, así que se dice en la pantalla: el mensaje de «ninguno en esta
  categoría» avisa de que los de las ramas que cuelgan de ella no salen ahí. Sin esa frase, una
  rama padre con hijos llenos parece un fallo.
- **El nombre de la rama se pide solo si la sesión concede `catalogo.categoria.ver`**, y si no
  llega, el filtro se anuncia igual pero sin nombre. Ni la tabla de artículos depende de que ese
  nombre se resuelva —sería cambiar lo importante por lo accesorio— ni se lanza una petición que se
  sabe que va a contestar `403`.
- **`z.guid()` y no `z.uuid()`** al leer `?categoria=` de la URL. `z.uuid()` exige la versión y los
  bits de variante de la RFC 9562, y un `Guid` de .NET no tiene por qué cumplirlos: con el esquema
  estricto, un enlace legítimo se abriría sin filtro y sin decir por qué.
- **El tipo de un artículo tiene un tercer valor que la API no emite**, `desconocido`. No es defensa
  por si acaso: el enumerado viaja como texto y el frontal se despliega aparte del backend, así que
  un valor nuevo llega antes de que `api/consultas.ts` lo conozca. Sin ese caso, la traducción
  devolvería `undefined` y la celda saldría vacía —que es lo que se ve cuando algo se rompe— en vez
  de decir que no se sabe.
- **La pantalla de la trazabilidad cambia UNA cosa y manda la ficha entera.** El `PUT` sustituye la
  ficha, así que lo demás —descripción, tipo, impuesto, categoría— viaja tal como llegó en la
  lectura (`FichaDeArticulo.intacto`), con el `If-Match` de esa lectura. Si alguien la ha tocado
  entre medias, el servidor contesta `412` en vez de pisarla, y la pantalla ofrece cargar la versión
  actual. El `200` del `PUT` no trae `ETag`, así que guardar invalida la ficha y la vuelve a leer: el
  formulario lleva la versión como `key` y se monta de nuevo con lo guardado.
- **Si el artículo ya tiene movimientos, eso lo dice el servidor, en el campo.** El formulario no
  puede saberlo —es cosa de Inventario (ADR-0048)—, así que la pista lo avisa antes y el rechazo
  (`articulo-trazabilidad-con-movimientos`) sale después en el mismo grupo, anunciado y en su
  descripción. Los rechazos que no son de la trazabilidad elegida —la versión obsoleta, el artículo
  que ya no existe— van arriba, fuera del campo.
- **La trazabilidad tiene su `desconocida`**, por lo mismo que el tipo. En la pantalla de cambio, una
  guardada que no se reconoce no se marca: hay que elegir una, y guardar sin elegir lo para el
  esquema sin ir al servidor.
- **El formulario de los códigos de barras replica la cuenta y deja la tabla al servidor.** Largo y
  dígito de control se dicen sin ir a la red; la tabla de prefijos que no son de un artículo
  (ADR-0051 §4) y el duplicado, solo allí, porque copiarla obligaría a seguir la del dominio línea a
  línea. Los mensajes del esquema son los `type` de la API, así que el texto sale de
  `errores.tipos` y es el mismo lo diga quien lo diga, en el campo del GTIN, de las unidades o del
  nivel. Los GTIN se enseñan en catorce cifras, como los guarda y los compara el servidor.
- **La clave de idempotencia del alta va con el intento**, como en la importación de terceros:
  repetir el mismo cuerpo repite la clave, cambiarlo la estrena, y tras un alta que sale bien se
  olvida (`model/intentoDeAlta.ts`).
- **Quitar un código se confirma en la fila**, no en una ventana: la baja borra de verdad y el
  proyecto no tiene un diálogo propio que reutilizar. La pregunta sale en el sitio de los botones,
  con el foco en «Cancelar», y Escape la cierra. El listado no trae la versión de cada fila, así que
  la baja la lee justo antes y la manda en el `If-Match`; si la fila ya no está, esa lectura da el
  `404`, que se dice arriba, y la lista se vuelve a leer.
- **La unidad y el impuesto de un artículo no se pintan.** Son identificadores de dos maestros de
  otro módulo, y esta funcionalidad no importa de aquélla: enseñar el `uuid` no le dice nada a
  nadie. Que existan, que estén vigentes y que una unidad retirada no valga para un alta lo decide
  el servidor por sus puertos; aquí solo se traduce el `type` del error a una frase (ADR-0030).
- **Una tarifa son VARIAS filas, y eso es lo que la pantalla de tarifas existe para enseñar.** El
  código se repite —una fila por periodo de vigencia— y los periodos no se solapan nunca, porque lo
  impide una restricción de exclusión de la base. Por eso el acotado por código no es un filtro más:
  es la vista en la que la sucesión de una tarifa se lee entera y se ve si algún día se quedó sin
  cubrir. Se llega a ella desde la propia fila, igual que al filtro por rama se llega desde el árbol.
- **Los dos extremos de la vigencia están incluidos, y el segundo es el que se olvida:** el último
  día de vigencia todavía rige. Es la misma convención que el `daterange(…, '[]')` de la restricción
  de exclusión y que el `<=` de `Tarifa.RigeEl` — tres sitios y una sola convención. Con `<`, la
  pantalla diría «ya no rige» el día en que un tramo acaba mientras la API sigue devolviendo su
  precio, y no habría ningún error: habría dos versiones de la verdad.
- **Las fechas se comparan como CADENAS, no como `Date`.** Llegan como días sueltos (`format: date`)
  y `new Date('2026-09-11')` es medianoche **UTC**: al oeste de Greenwich cae en el día anterior, y
  un tramo que empieza hoy se pintaría como futuro durante un día entero y solo para parte del
  mundo. Los días en ISO-8601 se ordenan igual como texto que como fechas. Por lo mismo, el «hoy» se
  compone de `getFullYear`/`getMonth`/`getDate` y no de `toISOString()`, que es el mismo error por el
  otro lado.
- **La pantalla de tarifas no pinta ni precios ni líneas, y no es un recorte.** Una línea nombra su
  destino por identificador —un artículo o una categoría—, y resolver cuarenta nombres serían
  cuarenta peticiones: exactamente el N+1 que el servidor se niega a hacer para resolver un precio, y
  no sale más barato por hacerlo desde el navegador. Un precio, además, sin su divisa al lado es un
  número que invita a leerse en euros, y la divisa es un maestro de **otro módulo** que esta
  funcionalidad no puede nombrar (ADR-0022, el mismo criterio que dejó fuera la unidad y el impuesto
  de un artículo). Quien necesita un precio lo pide donde se resuelve con su divisa pegada:
  `GET /api/v1/catalogo/tarifas/{codigo}/precio`.
- Los DTO salen de `shared/api/esquema.ts`, que se **genera** (`npm run api`), y se traducen al
  modelo de vista en la capa `api`: los tipos del contrato no salen de ahí.
- La empresa **no** forma parte de ninguna clave de consulta: va dentro del testigo y quien filtra
  es el servidor (R8). Por eso el cambio de empresa no invalida esto a mano — vacía la caché entera.
