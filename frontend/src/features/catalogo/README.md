# `catalogo` — qué se vende y cómo está clasificado

Espeja el módulo **Catálogo** del backend (`Bastion.Catalogo.*`, `/api/v1/catalogo/`). Dentro, una
carpeta por recurso; entre funcionalidades, nada: `catalogo` no importa de `organizacion` ni al
revés, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

Son **tres recursos y no uno** porque son tres cosas: un artículo es lo que se factura, una
categoría es dónde está colocado, y una tarifa es a cuánto se vende. Dentro de la funcionalidad sí
se importan entre ellos —`articulos/ui` pide a `categorias/api` el nombre de la rama por la que se
está filtrando—, que es exactamente lo que distingue «una funcionalidad» de «una carpeta».

## `articulos` — los artículos de la empresa activa

**Propósito.** Enseñar los artículos con los que se opera, paginados y filtrados.

### Rutas

| Ruta         | Exigencia                       | Título    |
| ------------ | ------------------------------- | --------- |
| `/articulos` | permiso `catalogo.articulo.ver` | Artículos |

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
```

`staleTime` de cinco minutos: un artículo es dato maestro, se da de alta y se queda ahí.

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
