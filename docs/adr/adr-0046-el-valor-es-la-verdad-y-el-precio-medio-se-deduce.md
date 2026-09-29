---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, inventario, concurrencia, dinero]
tags: [adr, r2, r3, r6, pmp, valoracion, existencias, cerrojo, adr-0004, adr-0040, adr-0044]
revisado: 2026-09-29
---

# ADR-0046: El valor es la verdad y el precio medio se deduce, por artículo y almacén y bajo cerrojo

- **Estado:** aceptado. **Enmendado por el ADR-0047 (§5, último punto) y por el ADR-0048 (§2, el
  orden de los cerrojos).**
  - El [ADR-0047](adr-0047-ninguna-fecha-anterior-al-ultimo-movimiento-de-su-clave.md) prohíbe
    confirmar o anular con una fecha anterior al último movimiento de alguna clave del documento.
    El orden de valoración sigue siendo el de confirmación, y dentro de cada clave coincide con el
    de la fecha.
  - El [ADR-0048](adr-0048-el-lote-y-la-serie-van-con-su-articulo-y-la-marca-se-lee-con-cerrojo.md)
    mete dos eslabones en el orden de los cerrojos: la marca del artículo, después del ejercicio, y
    los lotes y las series, después de la valoración.
- **Fecha:** 2026-09-28
- **Sale del ítem 2.8** y de las cinco decisiones que el encargo del 2026-09-28 pidió tomar y
  escribir **antes del código**. Las toma el agente. Cada una lleva aquí su motivo, y en el PLAN la
  entrada que la resume.
- **Enmienda el ADR-0044 §2** en dos cosas. Una proyección que necesita leer lo que suma lo lee
  **con la fila bloqueada** (punto 2). Y la sentencia de la existencia se parte en dos, porque el
  `CHECK` del stock mira la fila propuesta y no la sumada (punto 4).
- **Sustituye el mecanismo de la decisión 4 del ítem 2.5** («el coste del inverso se copia»), y
  conserva su motivo: el par suma cero también en valor (punto 6).

## Contexto

El criterio del 2.8 pide cinco cosas:

- que el PMP se recalcule en cada entrada y se guarde **en el movimiento**;
- que una salida congele el vigente en su fila, para que valorar el pasado no exija reproducir la
  historia;
- que el stock negativo se rechace;
- que un ajuste positivo sin coste tome el PMP vigente;
- y que pasen los casos dorados.

La decisión 8 de la puerta de la fase 2 ya fijó dos cosas: el coste es un `Importe` en la divisa
base de la empresa, y el disparador del invariante 8 no se cruza en esta fase.

Hay tres cosas que el 2.7 dejó hechas y que el PMP pone en tensión:

- **La proyección no lee lo que suma** (ADR-0044 §2). El PMP sí necesita leer: la fila del libro
  guarda el precio medio, y para calcularlo hay que conocer el saldo y el valor **antes** de
  escribirla. Como el libro es de solo añadido, la fila no se puede corregir después.
- **La R2 se cumple oponiendo un inverso**, y el inverso de una entrada es una salida. Si las
  unidades ya salieron, deja el stock por debajo de cero.
- **La existencia va por ubicación y lote**, y cambiar una caja de estantería no puede cambiar su
  valor.

## Decisión

### 1. Anular no siempre se puede: el inverso que deja el stock negativo se rechaza

**Se rechaza, con el mismo `422` `stock-insuficiente` que cualquier salida.** La excepción está en
la R2, que gana una: anular una entrada cuyas unidades ya han salido no se puede hasta que
vuelvan. Hay dos maneras de arreglarlo:

- anular antes las salidas que se las llevaron;
- o registrar la entrada que falta.

La otra opción era admitir el negativo solo cuando lo pide una anulación, y se descarta por tres
motivos:

- **El inverso es un documento como otro cualquiera**: numera, respeta el ejercicio y mueve el
  libro. Una salida que deja negativo es la misma salida la firme quien la firme.
- **El motor no distingue quién escribe** (punto 4). La excepción no cabe en un `CHECK`, así que
  habría que quitarle la guarda al motor, y la guarda es lo único que dos salidas simultáneas no se
  saltan.
- **Un negativo no tiene precio medio.** Con cantidad cero o por debajo, el valor por unidad no
  existe, y la entrada siguiente tendría que promediar contra un saldo que no es un saldo.

**El caso:** entran 10, salen 6 y se anula la entrada. La anulación contesta `422`, el original sigue
`Confirmado`, el contador de la serie no se mueve y el libro no gana ninguna fila.

### 2. Se lee con la fila bloqueada: una sentencia bloquea y la siguiente lee lo bloqueado

> **Enmendado por el ADR-0048 (2026-09-29), en el orden de los cerrojos.** Entre el ejercicio y el
> contador va la marca de los artículos del documento, con `FOR SHARE`; entre la valoración y el
> documento, los lotes y las series, con `INSERT … ON CONFLICT DO NOTHING`. Lo demás del punto sigue
> igual.

**Antes de valorar**, para cada clave del documento y **en orden de clave**, una sentencia toma la
fila de la valoración:

```sql
INSERT INTO inventario.valoraciones AS v (empresa_id, articulo_id, almacen_id, cantidad, valor, divisa)
SELECT {empresa}, c.articulo_id, c.almacen_id, 0, 0, {divisa} FROM unnest(…) AS c …
ORDER BY c.articulo_id, c.almacen_id
ON CONFLICT (empresa_id, articulo_id, almacen_id) DO UPDATE SET cantidad = v.cantidad
```

La siguiente sentencia lee esas filas, que ya son de esta transacción. Es lo que hace el numerador
con el contador: incrementar y luego leer. No se usa `RETURNING`, porque `SqlQueryRaw` mete el texto
dentro de un `SELECT … FROM (…)`, y PostgreSQL no admite un `INSERT` ahí.

- **El `DO UPDATE` que no cambia nada bloquea la fila** igual que un `FOR UPDATE`, y hace además lo
  que un `FOR UPDATE` no hace: **bloquea también la clave que todavía no existe**. Dos primeras
  entradas simultáneas de un artículo nuevo no encuentran nada que bloquear con un `SELECT … FOR
  UPDATE`, y las dos valorarían desde cero. Con el `INSERT … ON CONFLICT`, la segunda se choca con
  la fila sin confirmar de la primera y espera. Cuando la primera confirma, la segunda bloquea la
  versión nueva, y su lectura, que en `READ COMMITTED` toma foto por sentencia, ve lo que dejó la
  primera.
- **El orden de clave es lo que impide el interbloqueo**, igual que en la sentencia de la
  existencia (ADR-0044 §2).
- **El orden de los cerrojos es el mismo en las dos acciones que valoran**, confirmar y anular:
  primero el ejercicio, luego el contador de la serie, luego la valoración, luego la fila del
  documento (punto 4) y, por último, las filas de la existencia. Toda transacción que toca la existencia de una clave ha tomado antes su
  valoración, así que dos confirmaciones del mismo artículo y almacén se ordenan en la valoración y
  no llegan a cruzarse en la existencia.

**Después, el valor lo calcula el dominio, y no la sentencia.** Depende de las líneas **en
secuencia**: dentro de un documento, la segunda línea del mismo artículo valora contra lo que dejó
la primera. Y el redondeo es el de la R6, en un solo punto: `PrecioUnitario.Por`. Escribirlo en
SQL sería una segunda regla de redondeo, y la R6 dice que solo hay una.

**Y se escribe sumando**, como la existencia. `valor = valor + Δ` y `cantidad = cantidad + Δ` van
en una sentencia propia, **después** de la de la existencia (punto 4), sobre filas que ya son de
esta transacción. La sentencia exige haber tocado una fila por clave: si una clave no se hubiera
bloqueado antes, no tendría fila, y eso es un defecto de quien llama, no un saldo nuevo.

**El caso:** dos entradas simultáneas del mismo artículo, a costes distintos, en dos transacciones
de verdad. La segunda espera, y el precio medio de su fila **incluye** a la primera. La mutación
que lo pone rojo es leer sin el cerrojo.

### 3. La clave de valoración es la empresa, el artículo y el almacén

**Ni la ubicación ni el lote.** Mover entre estanterías del mismo almacén no cambia el valor, porque
la clave no las ve. Y un precio medio por lote no es un medio: sería el coste de cada lote, que es
lo que haría FIFO.

**Y con el almacén, no solo por empresa y artículo**, por tres motivos:

- **La clave fina se agrega y la gruesa no se parte.** El valor de la empresa es la suma del de sus
  almacenes, y su precio medio es ΣV / ΣQ, exacto, porque se guarda el valor (punto 5). Al revés
  no se puede: un valor por empresa no dice cuánto hay en cada almacén, y pasar un día al almacén
  obligaría a reconstruir la historia. Es tan difícil de deshacer como las cinco decisiones del
  ADR-0007, y ante la duda se elige la que se puede deshacer.
- **El coste de poner la mercancía en un almacén no es el mismo en todos.** Portes, aranceles y
  manipulación hacen que la misma caja cueste distinto en Madrid que en Las Palmas. Un medio por
  empresa lo mezclaría.
- **Menos contención.** El cerrojo del punto 2 es por clave, así que dos almacenes que mueven el
  mismo artículo no se esperan.

Además, el §7.4 del plan maestro pone el «coste medio» en la existencia, que lleva almacén.

**Lo que decide para el 2.11:** una transferencia mueve valor entre almacenes. La salida del origen
se valora a su precio medio, y **ese importe exacto** —el de la fila, no el precio— es el coste de
la entrada en el destino. El valor que sale del origen es el que llega al destino, y mientras viaja
es el que queda en tránsito. Cómo se cuenta el tránsito lo decide el 2.11.

### 4. El negativo lo rechaza el motor, y el borde lo traduce por el nombre

**`ck_existencias_fisico_no_negativo CHECK (fisico >= 0)`** va sobre la fila viva de la existencia,
que es por ubicación. Así se rechaza también sacar de una estantería vacía lo que está en la de al
lado, que es lo que pasa en el almacén. Como la cantidad de la valoración es la suma de las filas de
su clave, tampoco puede bajar de cero.

**No hay comprobación previa en el dominio.** Leer el saldo, compararlo y escribir es la ventana que
dos salidas simultáneas se saltan juntas. El `CHECK` se evalúa con la fila ya bloqueada y el valor
ya sumado, y es la única guarda que las dos no pueden cruzar a la vez.

**Para que eso sea verdad, la sentencia de la existencia se parte en dos.** Se midió, no se supuso.
En un `INSERT … ON CONFLICT DO UPDATE`, PostgreSQL comprueba los `CHECK` sobre la fila **propuesta**
antes de mirar si choca. La sentencia única del 2.7 proponía una fila nueva con la cantidad del
documento, así que una salida de 2 sobre una existencia de 5 chocaba con el −2 de una fila que nunca
se iba a escribir. Lo destapó `Borrar_las_instantaneas_y_recalcularlas_no_cambia_ningun_numero`, que
confirma 5 y después −2 en la misma clave: con la sentencia única, el segundo contestaba `23514` con
el saldo en 3. Ahora son dos sentencias:

1. **La primera crea a cero y bloquea**, en orden de clave, las filas vivas del documento. Es el
   mismo `DO UPDATE` que no cambia nada del punto 2, y propone cero, que cumple el `CHECK` tanto si
   la fila entra como si choca.
2. **La segunda suma y escribe.** Es un `UPDATE` sobre filas que ya son de esta transacción, y en la
   misma sentencia escribe las instantáneas del mes. El `CHECK` ve la fila ya sumada.

La fila viva y las instantáneas siguen moviéndose juntas, que es lo que el ADR-0044 §2 exige. Lo que
cambia es que el cerrojo va en una sentencia aparte, como en la valoración.

**Y el documento se guarda antes de mover la existencia.** Dos transacciones que confirman o anulan
el **mismo** documento a la vez se separan en las guardas del documento: el testigo de la R11 y el
índice único del inverso. La guarda del stock separa a dos documentos **distintos** que no caben
juntos. Si la existencia se moviera antes, la perdedora de una carrera sobre el mismo documento
chocaría con el stock que la ganadora ya se llevó. Contestaría `422` `stock-insuficiente` a quien
solo llegó tarde, en vez del `412` que le dice que recargue.

- **Por eso `AnotarEnElLibroAsync` guarda lo pendiente del documento antes de la sentencia.** Antes
  comprueba la transacción y la empresa, porque ese guardado, sin transacción, se confirmaría solo.
- **Lo destapó `Dos_anulaciones_simultaneas_dejan_un_solo_inverso`.** Su perdedora chocó con
  `ck_existencias_fisico_no_negativo` y no con `ix_ajustes_anula_a_id`, que es lo que el caso
  afirma.

**La traducción va en el borde, por el nombre de la restricción, como la del índice del inverso.**
La excepción del motor sube sin traducir, que es lo que el ADR-0004 manda a la infraestructura. Un
manejador nuevo, registrado entre el de la carrera perdida y el general, la reconoce **solo** si es
un `23514` con ese nombre. Entonces contesta con el error que el módulo declaró para ella: un `422`
`stock-insuficiente`. Es literalmente el ejemplo 2 del ADR-0004, «reservar stock cuando no hay
existencias», que ese ADR da como el caso canónico de la regla de negocio. Cualquier otro `23514`
sigue siendo un `500`.

- **El manejador es nuevo y no el del índice**, porque el del índice traduce siempre a un `412` sin
  código de negocio, y aquí cada restricción declarada lleva su propio error. El mecanismo es el
  mismo:
  - lista cerrada;
  - declaración en el módulo dueño de la restricción, con su motivo;
  - un barrido que compara la lista con el modelo.
- **No en el repositorio.** Atrapar la excepción ahí y devolver un `Resultado` sería traducir en la
  infraestructura, y el ADR-0004 dice que la infraestructura lanza y que el `Resultado` solo cruza
  de Aplicación al borde.
- **La transacción está abortada, y eso da igual.** La excepción la deshace el filtro de
  idempotencia, que libera la clave para el reintento, como con cualquier otro fallo.

**Las instantáneas no llevan `CHECK`.** Un movimiento con fecha atrasada puede dejar un mes pasado
por debajo de cero sin que el saldo de hoy lo esté, y eso ya lo admitía el 2.7.

**La valoración lleva tres `CHECK`**, que no se traducen, porque saltar significa un defecto:

- la cantidad no baja de cero;
- el valor no baja de cero;
- sin cantidad no hay valor.

Que ninguna salte antes que la de la existencia lo garantiza el orden: la sentencia de la valoración
va **después** de la de la existencia. Si la cantidad fuera a quedar negativa, la de la existencia
ya habría saltado y la de la valoración no llegaría a correr. En una sola sentencia, cuál de las dos
restricciones salta primero no estaría definido.

**El caso:** dos salidas simultáneas que caben una a una y no juntas. Una confirma y la otra
contesta `422`, y el físico no baja de cero en ningún momento.

### 5. Se guarda el valor total y se deduce el precio medio

> **Enmendado por el ADR-0047 (2026-09-29), en su último punto.** Un documento ya no puede llevar
> una fecha anterior al último movimiento de alguna de sus claves. Así, el valor de una fecha pasada
> es un estado que la clave tuvo de verdad, y el aviso que el último punto le pedía al 2.14 deja de
> hacer falta para lo que se confirme desde entonces.

**La verdad es `valoraciones.valor`**, un importe de escala 4. El precio medio es `valor /
cantidad` redondeado a la escala de `PrecioUnitario` (6), y solo existe con cantidad mayor que cero.
Si se guardara el precio medio redondeado y el valor se recalculara como precio por cantidad, el
valor se desviaría un poco en cada movimiento, y la desviación no la vería nadie. Así, el valor es
exacto y el precio medio se deduce de él.

**Cada fila del libro gana dos columnas.** `valor` es un importe con signo: lo que la fila sumó o
restó a la valoración. `precio_medio` es el precio medio que la fila **congela**. `p` es el precio
medio vigente antes de la fila, y `V` el valor de la clave en ese momento:

| Fila | `valor` | `precio_medio` |
|---|---|---|
| Entrada con coste `c` | `+c.Por(q)`: el importe redondeado una vez, en `PrecioUnitario.Por` | el de después |
| Entrada sin coste | `+p.Por(q)`. Sin cantidad en la clave no hay `p`, y se rechaza con `ajuste-entrada-sin-coste-ni-precio-medio` | el de después, que es `p` |
| Salida | `−min(p.Por(q), V)`. Si deja la clave a cero, `−V` entero | el de antes, que es `p` |
| Inverso de una salida | `+` el valor de la línea original, exacto | el de después |
| Inverso de una entrada | `−min(v, V)`, con `v` el valor de la línea original. Si deja la clave a cero, `−V` entero | el de antes |

- **`q` es siempre la cantidad en unidad base, y el coste también es por unidad base**, que es lo
  que dice el contrato de la línea desde el 2.3. Así, el precio medio sale en la misma unidad que
  el coste.
- **La salida resta exactamente el importe redondeado de su fila** (R6), y no un valor calculado
  aparte. Lo que queda es lo que había menos lo que la fila dice.
- **Vaciar la clave se lleva todo el valor**, aunque el redondeo diga otra cosa. Con 1000 € en 300
  unidades, el precio medio es 3,333333, y 300 por él son 999,9999. Si la salida restara eso, quedaría
  una diezmilésima sin unidades que la sostengan. Se lleva 1000,0000, y sin cantidad no hay valor.
- **El tope `V` solo lo toca el redondeo.** El precio medio redondeado hacia arriba, por una cantidad
  que casi vacía la clave, puede pasar del valor por una diezmilésima. Se resta como mucho lo que hay,
  y la clave se queda con unas milésimas de unidad y valor cero.
- **Una salida puede mover el precio medio en la sexta decimal.** Restar un importe redondeado a
  cuatro deja un cociente que no siempre coincide con el precio de antes. Con 1000 € en 300
  unidades, sacar 100 resta 333,3333 y deja 666,6667 en 200, que es 3,333334 y no 3,333333. El
  criterio dice que la salida posterior «ya no lo mueve», y eso es verdad en el precio con el que
  se valora, que es el de antes. El que se deduce después puede diferir en la última cifra. Hay un
  caso dorado para cada una de las dos cosas.
- **Dentro de un documento, primero lo que sube y después lo que baja**, cada grupo en el orden de
  sus líneas. Si no, un documento con una salida y una entrada del mismo artículo pasaría a mitad de
  camino por un saldo negativo que no ha existido, y valoraría contra él. El libro y la existencia
  suman igual en cualquier orden. La valoración no, y por eso se fija este.
- **El orden de las líneas es su número, y se guarda.** El que devuelve la base al leer el documento
  no es el que se escribió: EF Core no ordena las filas de una colección dentro de su documento, y
  los identificadores de la versión 7 no ordenan dentro del mismo milisegundo. Y el orden no solo
  cambia el precio que se congela: con una entrada con coste y otra sin él del mismo artículo, cambia
  el valor del documento. Cada línea guarda su posición, la del inverso copia la de la suya, y el
  agregado recorre sus líneas por ella, las lea de donde las lea.
- **El orden de valoración es el de confirmación, no el de la fecha de operación.** Un movimiento con
  fecha atrasada entra en el libro en su mes, pero se valora con el precio medio del momento en que
  se confirma. Es el PMP perpetuo de siempre, y lo que el criterio pide: el valor de una fecha pasada
  es la suma del `valor` de las filas hasta esa fecha, sin reproducir nada. Esa suma usa precios de
  cuando se confirmaron, y **el 2.14 tendrá que decirlo en su pantalla**, si enseña valor.

**Los casos dorados**, en el carril rápido y con sus cifras escritas:

- 10 a 2 €, 5 a 3,50 € (el precio medio pasa a 2,50), salen 4 (−10,0000, y el precio medio no se
  mueve), entran 2 sin coste (+5,0000, a 2,50) y salen 13, que vacían la clave (−32,5000);
- 1000 € en 300 unidades: salen 100 (−333,3333, y el precio medio pasa a 3,333334) y luego las 200
  que quedan (−666,6667, todo el valor);
- 1000 € en 300 unidades, salen las 300 de golpe: −1000,0000 y no −999,9999;
- 666,6667 € en 200 unidades, salen 199,999999: el precio por la cantidad da 666,6668, el tope
  resta 666,6667, y quedan 0,000001 unidades con valor cero.

**La propiedad y el cuadre se extienden al valor.** Tras cada paso de la secuencia al azar:

- el valor de cada clave es la suma del `valor` de sus filas del libro;
- su cantidad es la suma del libro de esa clave;
- sin cantidad no hay valor, y el valor no es negativo;
- y una segunda implementación, escrita en C# dentro del test, valora la misma secuencia y tiene que
  dar las mismas cifras fila a fila.

El cuadre compara lo mismo en una sola lectura, y dice cuántas valoraciones comparó.

### 6. El inverso compensa el valor de la línea del original

La decisión 4 del 2.5 copiaba el **coste** en el inverso, para que el par sumara cero también en
valor el día que hubiera valor. Ese día es hoy, y el coste no basta. `v` es el importe **redondeado**
de la fila, y un coste por unidad no siempre lo reproduce. Y una salida no tiene coste: tiene el
precio medio de su momento.

**Así que la línea confirmada guarda su valor** (`LineaDeAjuste.Valor`, el de su fila del libro), y
el inverso lo copia, con el signo cambiado, en `LineaDeAjuste.ValorQueCompensa`. Al confirmar, el
inverso se valora contra eso, con las dos reglas de la tabla. El coste del inverso queda vacío.

- **El par suma cero en valor siempre que el tope no toque.** Lo toca cuando, entre el original y la
  anulación, otras salidas se han llevado parte del valor que la entrada trajo. Entonces el inverso
  se lleva lo que queda y la diferencia se dice: `ValorQueCompensa` y `Valor` difieren en la línea
  del inverso, y las dos cifras quedan escritas.
- **Por qué en la línea y no en el libro.** La fila del libro no sabe de qué línea sale, y
  emparejarlas por orden no es fiable: los identificadores se crean en el mismo milisegundo y la
  versión 7 no ordena dentro de él. Una columna de línea en el libro serviría para las filas nuevas,
  pero no para las ya escritas, que no se pueden tocar. La línea sí se puede rellenar en la
  migración (punto 8).

### 7. La divisa va en la cabecera del documento, y una vez por fila en el libro

**El documento lleva una divisa**, `Ajuste.Divisa`, que se pone al abrirlo con la divisa base de la
empresa, por un método nuevo de `IConsultaDeEmpresas`, como fijó la decisión 8 de la puerta. Las
líneas guardan importes sin divisa propia, en la del documento, como las líneas de una factura. La
línea del contrato deja de traer divisa. Si la trajera, la línea podría llevar una distinta de la
del documento.

**Una línea que baja stock no admite coste**, porque su valor es el precio medio y un coste escrito
ahí no se usaría. Tampoco lo admite negativo. Las dos cosas se rechazan con el mismo `400`,
`ajuste-coste-no-valido`, en vez de ignorarlas en silencio. No hay `CHECK` en el motor: las salidas
confirmadas antes del 2.8 llevan coste, y sus filas del libro no se pueden tocar.

**El libro lleva una columna `divisa` por fila**, y no una por importe como el resto del proyecto
(`limite_credito_cantidad` y `limite_credito_divisa`, por ejemplo). Todos los importes de una fila
están en la misma divisa, y con una sola columna no se puede escribir una fila que las mezcle. Pero
el motivo que decide es otro: la columna es `coste_unitario_divisa` **renombrada**. Una pareja
nueva por importe exigiría rellenar la divisa del valor en las filas que ya están, y el libro no
admite un `UPDATE`. El dominio sigue viendo `Importe` y `PrecioUnitario` con su divisa (R6): la
fila los compone al leerse.

**La valoración lleva su divisa**, y una valoración en una divisa no se mezcla con un documento en
otra. Si la empresa cambia de divisa base teniendo existencias, la primera confirmación en la nueva
se rechaza con `ajuste-valoracion-en-otra-divisa`, un `422`. Convertir exige un tipo de cambio con
fecha, y `Importe` ya se niega a sumar sin él. El tipo de cambio es de la fase 6.

### 8. Lo que la migración hace con lo que ya hay

**Las filas del libro anteriores al 2.8 valen cero.** `valor` y `precio_medio` entran con un valor
por defecto de cero, que PostgreSQL guarda en el catálogo sin reescribir ninguna fila, y el valor
por defecto se quita después. Así, el libro sigue sin una sola sentencia de cambio. No se revaloran
porque revalorar es reproducir la historia, y las salidas de antes del 2.8 no congelaron ningún
precio.

**Las valoraciones nacen de la existencia.** Cada clave con existencias recibe una fila con la
cantidad que suman sus ubicaciones, valor cero y la divisa de sus filas del libro. Con valor cero y
cantidad, el precio medio vigente es cero, así que **una entrada sin coste en una clave de antes del
2.8 se valora a cero**, y no se rechaza. Es lo que vale lo que ya estaba, y la primera entrada con
coste empieza a darle precio.

**La cabecera del ajuste recibe la divisa de sus líneas**, que la línea ya guardaba. Después, la
columna de la línea se quita. Las líneas confirmadas reciben `valor` cero, que es lo que valen sus
filas. Así, anular un ajuste de antes del 2.8 compensa cero y no necesita un camino propio. Las
líneas en borrador que bajan stock pierden el coste que traían, que ya no se usaría. Los documentos
confirmados conservan el suyo.

**El euro, cuando no hay de dónde sacar la divisa.** Un ajuste sin líneas no llega ni a abrirse, y
una existencia sin filas del libro es un descuadre. Ninguna de las dos cosas existe en una
instalación, pero la migración no puede dejar nulo lo que es obligatorio. En esos dos casos usa
`EUR`, que es lo que Bastion factura hoy. Si en una base de desarrollo una clave tuviera filas en
dos divisas, se queda con la primera por orden alfabético, y la próxima confirmación en la otra se
rechaza con su código.

**Las líneas que ya están se numeran por su identificador**, dentro de cada documento, en una
segunda migración, `ElOrdenDeLasLineas`. No hay otro orden guardado y no hace falta: las líneas de
antes del 2.8 valen cero, así que su orden no cambia ningún valor. El número entra con un valor por
defecto que se quita después, como el valor del libro, y el índice único de `(ajuste_id, numero)` va
detrás del relleno.

**El `CHECK` de la existencia falla si la base ya tiene negativos.** La migración no los arregla:
una base de desarrollo con un negativo se rehace o se le da la entrada que falta, y el error lleva
el nombre de la restricción.

### 9. El criterio del SQL crudo, en las sentencias nuevas

Van en un fichero nuevo, que entra en la lista cerrada de `ElFiltroNoSeSaltaPorAhiTests` con su
motivo, y se defienden con las cuatro cláusulas del ADR-0040, como las del 2.7 (ADR-0044 §9):

1. **Atómicas con el documento.** Van en la transacción del filtro de idempotencia, y fuera de ella
   revientan.
2. **Leen con el cerrojo que impide que lo leído se quede viejo.** Es la otra mitad de la cláusula
   del ADR-0041, que el ADR-0044 no necesitó. La sentencia que bloquea va antes que la que lee, y
   la que escribe suma sobre filas que ya son de esta transacción. Ningún valor lo decide el
   llamante: la cantidad y el valor salen de las filas del libro.
3. **No hay otra forma.** EF Core no traduce el `ON CONFLICT`, y leer por el ORM y escribir después
   es la ventana del punto 2.
4. **La empresa, dentro y comprobada.** Las tres sentencias filtran por el inquilino, que es parte de
   la clave del `ON CONFLICT`, y antes se comprueba que cada fila es de esa empresa. Un caso de dos
   empresas con el mismo artículo y almacén comprueba que valorar una no toca la otra.

### 10. El dominio valora, y no devuelve `Resultado`

El plan maestro pide la valoración con la interfaz preparada para FIFO, y FIFO no se implementa.
`IValoracionDeExistencias` es la costura: recibe lo que el documento quiere valorar y devuelve, por
cada línea, su valor y el precio que congela. `ElPrecioMedioPonderado` la implementa a partir de los
saldos bloqueados. Un FIFO la implementaría a partir de sus capas (`CapaValoracion`, §7.4), con su
propia lectura bloqueada. **Lo que no se prepara**, a propósito, es la tabla de capas: no se escribe
un esquema para un método que nadie ha pedido todavía.

**Tiene dos métodos y no uno**, por el ADR-0004: el dominio lanza y nunca devuelve `Resultado`.
`LoQueImpide` dice, sin lanzar, si hay una línea que no se puede valorar y por qué (una entrada sin
coste ni precio medio, o una valoración en otra divisa). `Valorar` lanza si se le llama igualmente.
El caso de uso pregunta primero y devuelve el `422` con su código, como ya hace con el estado del
documento antes de transitar.

### 11. El invariante 8, sin cruzar

La decisión 8 de la puerta lo dejó escrito, y aquí se confirma sobre el código. Ninguna línea de este
ítem convierte un `PrecioResueltoDto` en un `Importe`. Los costes llegan del documento como
decimales, y la divisa, de la empresa. El disparador sigue armado para la fase 3.

## Consecuencias

- **La R2 tiene una excepción escrita en su fila**: anular una entrada ya consumida no se puede.
- **La R3 se extiende al valor**: la valoración es la suma del valor del libro, y lo comprueban la
  propiedad, el cuadre y la segunda implementación.
- **Confirmar y anular escriben una tabla más y ejecutan tres sentencias más**: la que bloquea, la
  que lee y la que suma el valor.
- **Todo documento que mueva el libro se valora.** No hay forma de confirmar sin valoración: el
  dominio exige una por cada línea, y la sentencia que suma exige la fila que dejó el cerrojo.
- **Los tests que sacaban stock de un almacén vacío dejan de valer.** Ahora contestan `422`, y cada
  uno se arregla dando antes la entrada, no quitando la guarda. Los de la anulación con
  contradocumento reciben una entrada previa en otra serie, y el generador de la propiedad modela
  el rechazo.
- **La línea de ajuste lleva número**, y no estaba en el encargo: lo destapó la propiedad. Con un
  coste distinto por entrada, un inverso leído de la base valoraba sus salidas en otro orden que el
  que tenía el original en memoria, y el precio congelado no casaba con el modelo.
- **Lo que no tiene caso, dicho.** El interbloqueo entre dos documentos con las mismas dos claves en
  orden inverso lo sostiene el orden de las claves, como en el 2.7 (ADR-0044, *Consecuencias*).

## Procedencia

Las cinco decisiones las pidió el usuario en el encargo del 2026-09-28, con sus alternativas, y le
dejó al agente la elección. Los puntos 6 a 11 salen de aplicarlas al código que ya había, y los
decidió el agente.
