---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, inventario, concurrencia, multiempresa]
tags: [adr, r3, r8, r9, existencias, instantanea, cuadre, proyeccion, adr-0015, adr-0040]
revisado: 2026-09-29
---

# ADR-0044: La existencia se mueve en la sentencia que anota el libro, y la instantánea mensual se puede tirar

- **Estado:** aceptado. **Enmendado por el ADR-0046 (§2 y §4); §5 corregido en `d9dd1e1`.**
  - El [ADR-0046](adr-0046-el-valor-es-la-verdad-y-el-precio-medio-se-deduce.md) enmienda, en sus
    puntos 2 y 4, el §2 de este: una proyección que necesita leer lo que suma lo lee con la fila
    bloqueada, y la sentencia de la existencia se parte en dos.
  - El §5 no se enmendó desde un ADR nuevo, como pide `principios/git-workflow.md`, sino que se
    reescribió por dentro en `d9dd1e1` (2026-09-28), por encargo del usuario: el cerrojo del
    recálculo es de todas las empresas. No se deshace; esta línea lo declara, y el mensaje de ese
    commit dice qué cambió y por qué.
- **Fecha:** 2026-09-27
- **Enmienda el [ADR-0015](adr-0015-lo-unico-que-genera-el-servidor-son-los-testigos-de-concurrencia.md)**,
  puntos 2 y 3: lo que genera el servidor ya no son solo los testigos de concurrencia. También lo
  son las columnas calculadas que se declaran, cada una en su lista y comprobada por lo que la hace
  ser lo que dice.
- **Amplía el [ADR-0040](adr-0040-el-numero-lo-toma-una-sentencia-en-el-esquema-de-otro-modulo.md)**:
  su criterio de cuatro cláusulas —con la segunda ya reescrita por el ADR-0041— también rige el SQL
  crudo que escribe tablas de negocio en el esquema **del propio módulo**. La cláusula 2 cambia su
  «una sola fila» por lo que la sustituye aquí (punto 9).
- **Sale del ítem 2.7** y de las cuatro decisiones que trajo el encargo del 2026-09-26. La (c) y
  el disponible generado venían decididos. La (a), la (b) y la (d) las contestó el usuario el
  2026-09-27.

## Contexto

La R3 dice que el stock es un libro mayor y no un contador. El libro existe desde el 2.3:
`inventario.movimiento_stock`, particionada por mes y de solo añadido en el motor. Pero la regla no
se podía romper, porque no había ningún contador que pudiera discrepar del libro. El 2.7 trae ese
contador —la existencia—, y con él la posibilidad de que diga otra cosa. El ítem consiste en que
no pueda decirla y en que, si un día la dijera, algo la encuentre.

Tres fuerzas tiran en direcciones distintas:

- **Sumar el libro cada vez es correcto y no escala.** El libro solo crece, y la pregunta del saldo
  es la más frecuente del módulo.
- **Una copia es rápida y puede mentir.** Una suma se pierde en una carrera. Un movimiento con fecha
  atrasada no toca lo que ya estaba calculado. Una fila de otra empresa suma donde no debe.
- **El 2.14 pedirá el saldo a una fecha pasada**, y ése no sale de la fila de hoy.

## Decisión

### 1. Tres tablas, y ninguna es la verdad

- **`inventario.existencias`**, la fila viva: una por empresa, artículo, almacén, ubicación y lote,
  con el físico, lo reservado y lo disponible.
- **`inventario.instantaneas_mensuales`**, el saldo de cada existencia al acabar cada mes. Clave
  (existencia, mes), con el mismo límite de mes que la partición del libro. **Sin huecos**, desde
  el primer mes en que la clave se movió hasta el corte de su empresa.
- **`inventario.cortes_de_la_instantanea`**, hasta qué mes tiene instantáneas cada empresa. Sin
  corte no hay ninguna.

**La verdad sigue siendo el libro**, y el saldo se define como su suma. Las tres tablas son
proyecciones. Se aceptan porque cada una se comprueba contra el libro (punto 6), y la instantánea
además se puede rehacer desde él (punto 5).

La migración rellena la fila viva de toda clave que ya tuviera movimientos, sumando el libro
entero. No crea instantáneas, porque ninguna empresa tiene corte todavía.

### 2. La fila viva se mueve en la misma sentencia que anota el libro — decisión (d)

> **Enmendado por el ADR-0046 (2026-09-28), puntos 2 y 4.** Una proyección que necesita leer lo que
> suma lo lee con la fila bloqueada, y la sentencia de la existencia se parte en dos, porque el
> `CHECK` del stock mira la fila propuesta y no la sumada.

Confirmar y anular ya no añaden filas al libro a secas: las **anotan**
(`IRepositorioDeAjustes.AnotarEnElLibroAsync`), y anotar mueve la proyección con **una sentencia**
(`LaProyeccionDelLibro.MoverAsync`). Corre en la transacción que abre el filtro de idempotencia,
antes de que la unidad de trabajo guarde las filas. No hay una firma que añada filas sin mover la
suma, ni una que mueva la suma sin filas: cualquiera de las dos dejaría escribir la discrepancia
que la R3 prohíbe.

- **La aplicación nunca lee lo que suma.** La sentencia es un
  `INSERT … ON CONFLICT (clave) DO UPDATE SET fisico = e.fisico + excluded.fisico`, que suma sobre
  la fila que hay en el motor con la fila bloqueada. Leer el saldo, sumarle y escribirlo dejaría una
  ventana entre las dos cosas, y dos confirmaciones del mismo artículo se llevarían el mismo saldo
  de partida: una de las dos cantidades se perdería sin error.
- **Las cantidades van agrupadas por clave**, porque PostgreSQL rechaza actualizar dos veces la
  misma fila en una sentencia. Un documento con dos líneas de la misma clave daría dos.
- **Y ordenadas por clave**, para que dos confirmaciones que tocan las mismas claves las bloqueen
  en el mismo orden y la segunda espere a la primera en vez de interbloquearse.
- **Revienta si no hay transacción**, porque EF Core abriría una implícita y la proyección se
  confirmaría sola. **Y revienta si alguna fila es de otra empresa**: sumaría en las existencias de
  otra sociedad (R8).

### 3. La instantánea se mueve en la misma sentencia, desde su mes hasta el corte — decisión (a)

Un movimiento con fecha atrasada deja viejas todas las instantáneas de su clave desde su mes. Una
reapertura no es un caso aparte: lo que entra en un ejercicio reabierto es un movimiento atrasado
más. **No se invalidan: se corrigen.** La misma sentencia del punto 2 suma la cantidad a cada
instantánea de la clave desde el mes del movimiento hasta el corte, y crea las que falten, con un
`generate_series` que lee el corte de la empresa.

- **Una sentencia y no dos.** La fila viva y las instantáneas se mueven juntas o no se mueven. Con
  dos, nada quedaría a medias —la transacción es la misma—, pero quien leyera entre medias vería una
  cosa movida y la otra no.
- **Por eso no hay huecos.** Un mes que faltara no recibiría la suma, y al crearlo después no se
  sabría cuánto le toca sin volver al libro.
- **Un movimiento posterior al corte no escribe ninguna, y sin corte no se escribe ninguna.** Las
  dos cosas son correctas: esos meses todavía no se han materializado, y quien los materialice
  leerá el libro.

### 4. Lo futuro no se confirma — decisión (b)

**El saldo es la suma del libro con fecha menor o igual que hoy.** Para que la fila viva y esa
definición digan lo mismo, confirmar rechaza una fecha posterior a hoy con **su propio código**,
`ajuste-con-fecha-futura` (`409`). Lo rechaza antes de preguntar por el ejercicio: no toma cerrojos,
y una fecha futura no se arregla reabriendo nada. Hasta aquí, un ajuste con fecha futura dentro del
ejercicio abierto se confirmaba, porque nada lo miraba.

Anular no necesita la guarda: el inverso lleva la fecha de hoy (ADR-0043). Y el cuadre suma el
libro **hasta hoy**, así que una fila futura escrita antes de esta regla sale como descuadre, que
es donde tiene que salir.

**«Hoy» es el día UTC.** En España eso deja dos horas, de 00:00 a 02:00 en verano (una en
invierno), en las que aquí ya ha empezado el día y en UTC todavía no: un ajuste fechado hoy a esa
hora se rechaza como futuro. La zona horaria de la empresa queda como nota abierta en el PLAN, con
su disparador.

### 5. El recálculo, y el cerrojo que lo hace seguro

> **Corregido por dentro en `d9dd1e1` (2026-09-28), por encargo del usuario.** El párrafo del modo
> del cerrojo decía que era el más débil que choca con quien escribe, y del modo es verdad; faltaba
> que la tabla es de todas las empresas. Lo que sigue es el texto corregido, no el del 2026-09-27.

`LasInstantaneasMensuales.RecalcularAsync(hastaElMes)` es lo que hace de la instantánea una
optimización y no una segunda verdad: si se puede tirar y rehacer sin que cambie un número, no dice
nada que el libro no diga.

1. **Abre su propia transacción**, y revienta si ya hay una.
2. **Toma lo primero `LOCK TABLE inventario.instantaneas_mensuales IN SHARE ROW EXCLUSIVE MODE`.**
3. Pone el corte de la empresa en el mes que se le pide.
4. Borra las instantáneas de la empresa.
5. Las repone desde el libro, con el fragmento `LasDebidas`.

**Por qué el cerrojo, aunque solo toque una empresa.** Una confirmación en vuelo de una clave nueva
no se ve, porque su fila viva todavía no está confirmada. El recálculo no le crearía instantáneas,
y ella las habría creado con el corte de antes: a esa clave le faltarían los meses que el recálculo
acaba de añadir. Y al revés: una confirmación que empezara durante el recálculo leería el corte
viejo.

**El modo es el más débil que choca con quien escribe, pero el alcance es el problema.** La
sentencia del punto 2 toma `ROW EXCLUSIVE` sobre la tabla al analizarse, aunque no llegue a escribir
ninguna instantánea, y lo suelta en su `COMMIT`. `SHARE ROW EXCLUSIVE` choca con él y consigo mismo,
así que dos recálculos se esperan. No choca con las lecturas, y no hay interbloqueo, porque el
recálculo no necesita nada que una confirmación tenga cogido.

Hasta aquí es cierto, y no basta. **`LOCK TABLE` cierra la tabla entera, y la tabla es de todas las
empresas**: mientras se recalcula la empresa A, se paran las confirmaciones y las anulaciones de la
B, de cualquier almacén y de cualquier artículo. Este párrafo decía que el modo era «el más débil»
como si eso lo hiciera barato. Del modo es verdad, pero del cerrojo engaña, y ningún modo más débil
lo arregla, porque lo que sobra es el alcance. *(Corregido en el epílogo del 2.7, el 2026-09-28, por
el encargo del usuario.)*

**El arreglo va en el 2.14: un cerrojo por empresa.** Hasta el 2.14 el recálculo no lo llama nadie
en producción (el párrafo siguiente), así que el defecto todavía no alcanza a nadie. El 2.14 es el
que lo llamará cada mes para todas las empresas, y lo cambia antes de llamarlo:

- **Quien anota toma `pg_advisory_xact_lock_shared(clave, empresa)`** antes de la sentencia que
  mueve la proyección, cuando todavía no tiene cogida ninguna fila de la proyección. Ya tiene el
  contador y el ejercicio, pero el recálculo no los necesita, así que esperar no forma un ciclo.
  **El recálculo toma `pg_advisory_xact_lock(clave, empresa)`** en lugar del `LOCK
  TABLE`.
  - Los compartidos no se esperan entre sí, así que dos confirmaciones de la misma empresa siguen
    sin esperarse.
  - El exclusivo espera a los compartidos de su empresa y detiene a los que lleguen después, pero no
    toca los de otra empresa.
  - Los dos se sueltan solos al acabar la transacción.
- **`clave` es una constante del módulo** que nombra las instantáneas de Inventario. **`empresa` es
  un entero sacado del identificador**, `hashtext(empresa_id::text)`, porque el identificador es un
  `uuid` y el cerrojo pide dos `int4`. Dos empresas con el mismo resumen se esperarían entre sí: se
  pierde paralelismo, no corrección.
- **La fila del corte no vale como cerrojo**, aunque sea la otra forma evidente de ponerlo por
  empresa. Una empresa sin corte no tiene fila, y el primer recálculo es justo el que la crea, así
  que la confirmación en vuelo no tendría nada que bloquear.
- **Con un caso de dos empresas y dos transacciones de verdad** que demuestre que la confirmación de
  B no espera mientras el recálculo de A sigue abierto. El caso de hoy, el de la clave nueva en
  vuelo, sigue demostrando que dentro de una misma empresa sí se espera.

**En el 2.7 no lo llama nadie en producción, a propósito.** Lo llaman los casos, que es lo que pide
el criterio. El primer lector de la instantánea es el 2.14, y con él llega quien avanza el corte
cada mes (punto 10).

### 6. El cuadre dice cuánto ha comparado

`ElCuadreDeLasExistencias.CuadrarAsync` compara, en **una sola lectura**:

- cada fila viva con la suma del libro hasta hoy, y cuántas filas vivas tiene cada clave —dos para
  la misma es un descuadre aunque sumen lo que deben—;
- cada instantánea con las que el libro dice que tiene que haber, por clave y mes, y cuántas hay
  de cada una, así que una que falte, una que sobre, una que diga otra cosa y dos para la misma
  clave y mes salen las cuatro.

**La cuenta de filas es la defensa del cuadre contra el índice.** Si el índice único dejara de
tratar el lote nulo como un valor (punto 7), las filas repetidas que eso deja sumarían, entre
todas, lo que dice el libro: la suma no las delata, y la cuenta sí.

**Una sola lectura** porque así el libro, las filas vivas y las instantáneas salen de la misma foto
del motor, y una confirmación a medias no puede aparecer en una y no en la otra. **Se junta con
`UNION ALL` y se agrupa**, en vez de cruzar, porque el lote es nulo y una igualdad con nulos no
casa; el `GROUP BY` los trata como iguales. **Las debidas salen del mismo fragmento que usa el
recálculo**: con dos ideas distintas de lo que tiene que haber, un cuadre limpio podría esconder un
recálculo equivocado.

**Devuelve cuántas existencias y cuántas instantáneas comparó**, además de los descuadres. Un
cuadre que no encuentra nada que comparar sale limpio, y quien lo llama afirma que el conjunto
comparado no era vacío (ADR-0020).

### 7. El lote va en la clave desde ya, y la unicidad no distingue nulos — decisión (c)

`lote_id uuid NULL`, **sin clave ajena hasta el 2.9**, que es el ítem que trae el lote. El índice
único `ix_existencias_una_por_clave` es `NULLS NOT DISTINCT`: con uno normal conviven dos filas
«sin lote», y el `ON CONFLICT` inserta una tercera en vez de sumar. Lo midió el usuario en
PostgreSQL 16, y aquí lo mide una mutación (en el PLAN).

**El libro todavía no lleva lote**, así que la sentencia, el recálculo y el cuadre lo escriben
nulo. Cuando el 2.9 añada la columna al libro, las tres cambian a la vez.

### 8. Lo disponible lo calcula el motor

`disponible` es `GENERATED ALWAYS AS (fisico - reservado) STORED`. `reservado` no tiene valor por
defecto: la sentencia lo escribe a cero, y lo moverá el 2.13. Escribir `disponible` es un error del
motor, el `428C9`, y hay un caso que lo intenta.

**Y es la primera columna que genera el servidor sin ser un testigo.** El ADR-0015 pedía, para ese
día, reabrir la decisión y no añadir una excepción. Reabierta, la premisa que sostiene la fase
única del interceptor de auditoría **sigue en pie**: la columna no es clave, la existencia no se
audita y ninguna escritura del rastreador la toca —la fila la mueven sentencias crudas, y el motor
rechaza cualquier `INSERT` o `UPDATE` que la nombre—. Lo que cambia es la lista:

- **Punto 2 del ADR-0015.** Lo que genera el servidor son los testigos **y** las calculadas
  declaradas, cada una en su lista y por su nombre, y se comparan enteras contra el modelo
  (`LasClavesSeConocenAntesDeGuardarTests.Lo_unico_que_genera_el_servidor_es_lo_declarado`, antes
  `Lo_unico_que_genera_el_servidor_son_los_testigos_de_concurrencia`).
- **Punto 3 del ADR-0015.** Cada una se comprueba por lo que la hace ser lo que se declara: un
  testigo es `uint`, se regenera en cada escritura y está marcado como tal; una calculada lleva su
  expresión, **guardada**, y nada más —ni un `DEFAULT`, ni un `IDENTITY`, ni el papel de testigo—
  (`LasClavesSeConocenAntesDeGuardarTests.Cada_cosa_que_genera_el_servidor_es_de_verdad_lo_que_se_declaro`,
  antes `Todo_lo_que_genera_el_servidor_es_de_verdad_un_testigo_de_concurrencia`).

### 9. SQL crudo en el esquema propio, y la cláusula 2 que lo acota

Las tres piezas escriben en SQL crudo, y son las primeras de la lista cerrada de
`ElFiltroNoSeSaltaPorAhiTests` que escriben tablas de negocio **en el esquema de su propio
módulo**. No cruzan ninguna frontera, así que no son una excepción al «único camino» del ADR-0040.
Lo que sí las alcanza es la prohibición del SQL crudo, que es lo que esa lista vigila. Se defienden
con el mismo criterio de cuatro cláusulas, porque es el único escrito, y cada una se dice aquí:

1. **Atómica con el documento.** La sentencia del punto 2 va en la transacción que confirma: con
   dos transacciones habría un instante con el documento confirmado y la existencia sin mover, y
   durante ese instante la R3 sería falsa. El recálculo y el cuadre no acompañan a ningún documento:
   el recálculo lleva su propia transacción y el cuadre una sola lectura.
2. **Ningún valor lo decide el llamante**, y la sentencia o no lee para decidir, o lee con el
   cerrojo que impide que lo leído se quede viejo. Es la cláusula del ADR-0041, **con una
   sustitución**: «una sola fila y una sola columna» no se puede cumplir, porque una proyección
   toca tantas filas como claves y meses. Lo que la sustituye es esto:

   > **Cada fila se mueve solo sumando sobre lo que hay, y lo que se reescribe entero se reescribe
   > bajo un cerrojo que excluye a los que suman.**

   La sentencia del libro suma sobre lo que hay y lee una sola cosa, el corte de la empresa. El corte
   no puede cambiar mientras ella está en vuelo, porque el cerrojo del recálculo choca con el suyo.
   El recálculo borra y repone, pero **después** de tomar ese cerrojo. El cuadre solo lee, en una
   foto.
3. **No hay otra forma de escribirlo.** Aquí el dueño es el propio módulo, así que la cláusula no
   pregunta por otro módulo, sino por el ORM. EF Core no traduce el `ON CONFLICT DO UPDATE`, ni el
   incremento sobre lo que hay, ni un `LOCK TABLE`, ni una función de ventana dentro de un
   `INSERT … SELECT`. Leer, sumar y guardar por el ORM es justo la ventana del punto 2.
4. **La empresa, comprobada dentro, con un caso que se pone rojo.** El filtro global no alcanza al
   SQL crudo. Cada sentencia compara `empresa_id` con el valor de `IInquilinoActual` en cada tabla
   que toca, y la de anotar comprueba además que cada fila del libro es de esa empresa. Qué pone
   rojo quitar cada comparación está en el PLAN, mutación a mutación.

Las tres entran en la lista cerrada por su ruta y con su motivo:
`LaProyeccionDelLibro.cs` y `LasInstantaneasMensuales.cs` usan `.ExecuteSql`, y
`ElCuadreDeLasExistencias.cs` usa `.SqlQuery`.

### 10. Lo que llega después, con su disparador

- **El 2.9 trae el lote al libro**, y con él la sentencia, el recálculo y el cuadre cambian a la
  vez (punto 7).
- **El 2.13 trae las reservas**, y con ellas el primer `reservado` distinto de cero.
- **El 2.14 trae el primer lector de la instantánea**, y con él un trabajo periódico: al empezar
  cada mes avanza el corte —con el recálculo— y cuadra cada empresa. Hasta entonces ninguna empresa
  tiene corte en producción, y la sentencia del punto 2 no escribe ninguna instantánea.

## Consecuencias

- **La R3 está viva.** Hay un contador que puede discrepar y varias cosas que lo pondrían en rojo:
  los casos de `LasExistenciasSonLaSumaDelLibroTests` y la propiedad de
  `ElSaldoEsLaSumaDelLibroPorPropiedadTests`, que tras cada paso compara el libro con un modelo,
  cada fila viva con la suma del libro y cada instantánea con una segunda cuenta escrita en C#.
- **Confirmar escribe dos tablas más en la misma transacción**, con una sentencia más por documento.
- **Todo documento que escriba en el libro lo hará por `AnotarEnElLibroAsync`.** No queda un camino
  para añadir filas sin mover la existencia.
- **Una fecha futura ya no se confirma**, y quien la escriba recibe un código que no es el del
  ejercicio.
- **Lo que no tiene caso, dicho.** El orden de las claves, que es lo que evita el interbloqueo entre
  dos confirmaciones con las mismas claves, lo sostiene el razonamiento del punto 2 y no un caso que
  se ponga rojo: provocarlo pide un tercero que retenga una fila mientras las otras dos se cruzan.
- **La tabla de la lista cerrada crece en tres entradas**, y cada una lleva su motivo.

## Procedencia

La (c) y el disponible generado venían decididos en el encargo del 2026-09-26. La (a), la (b) y la
(d) las preguntó el agente y las contestó el usuario el 2026-09-27; en las tres eligió la opción
recomendada. El lote sin clave ajena hasta el 2.9 y el generador propio del test de propiedad los
decidió el agente, y están en el PLAN porque son reversibles.

Qué pone rojo cada mutación —la suma de la fila viva, la de la instantánea, su límite inferior, la
unicidad sin nulos, el cerrojo del recálculo, las comparaciones de la empresa, la guarda de lo
futuro y la cuenta de filas del cuadre— está en el *Estado actual* de `docs/PLAN.md`.
