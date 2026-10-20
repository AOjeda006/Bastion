---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [inventario, concurrencia, observabilidad, rendimiento]
tags: [adr, r3, instantanea, corte, trabajo-periodico, avance-incremental, cuadre, xmin, adr-0064, adr-0063, adr-0044]
revisado: 2026-10-20
---

# ADR-0067: El avance del corte va un mes por transacción, y el cuadre guarda en el corte hasta dónde llega

- **Estado:** aceptado
- **Fecha:** 2026-10-19
- **Sale del ítem 2.18**, que es la respuesta 5 de la puerta del 2.17: el 2026-10-18 el usuario
  aceptó el avance incremental que el ADR-0064 §5 proponía, y fijó su criterio. Está en
  `docs/PLAN.md`, en la casilla del 2.18.
- **Enmienda el [ADR-0064](adr-0064-el-avance-del-corte-se-mide-contra-el-plazo-de-cada-orden.md)**
  en cuatro sitios:
  - **§5**, la propuesta, que pasa a decisión con tres cambios: el recálculo a mano también va mes a
    mes; el cuadre de cada vuelta no es «el mes nuevo y las existencias», sino todo lo que quede por
    cuadrar, anotado en la fila del corte; y el cuadre entero tampoco lee el libro entero (puntos 1,
    3, 5, 6 y 7 de este);
  - **§3**, el histograma, que mide cada mes y no la puesta al día entera (punto 4);
  - **§4**, el último punto: el cuadre que falla ya se repite (punto 5);
  - **las *Consecuencias***: una empresa de 10⁵ claves avanza (*Medido*).
- **Enmienda el [ADR-0063](adr-0063-el-saldo-a-una-fecha-es-la-instantanea-mas-su-tramo-y-el-cerrojo-es-de-la-empresa.md)**
  en dos puntos:
  - **§1**, el tercer punto: el valor de la instantánea ya no se suma del libro con el fragmento
    `LasDebidas`, que desaparece, sino del mes anterior y del libro de su mes (punto 1);
  - **§7**, el cuarto y el quinto puntos: el avance no rehace las instantáneas desde el libro hasta
    el mes que toca, sino que avanza un mes por transacción hasta llegar; y el cuadre no va solo en
    la empresa que avanza, sino en toda la que tenga algo por cuadrar (puntos 1 y 5).
- **Enmienda el [ADR-0044](adr-0044-la-existencia-se-mueve-en-la-sentencia-que-anota-el-libro.md)**
  en dos puntos:
  - **§5**, los pasos 3 a 5 del recálculo: ya no pone el corte, borra y repone de una vez, sino que
    vuelve atrás un mes por transacción y avanza otra vez (punto 3);
  - **§6**: el cuadre entero sigue siendo una sola lectura, pero lo que espera de cada instantánea
    ya no sale del fragmento del recálculo, sino del mes anterior y del libro de su mes; y el del
    trabajo va un mes por transacción (puntos 5 y 6).

## Contexto

**El ADR-0064 midió el avance de entonces y lo encontró sin techo.** Rehacía todas las
instantáneas de la empresa desde el libro entero, en una sola sentencia. Su coste subía cada mes
aunque no se moviera nada, porque cada mes suma una instantánea por clave, y la junta de los meses
de cada clave crecía con su cuadrado. Una empresa de 10⁵ claves y 10⁶ movimientos, la C, ya no
cabía en el plazo de la orden, 30 s, y fallaba cada hora; mientras fallaba, sus confirmaciones
esperaban en el compartido. Y el cuadre, que iba después del `COMMIT` del avance, no se repetía
nunca si fallaba.

**El usuario aceptó la propuesta el 2026-10-18**, con un criterio más largo que la propuesta: el
recálculo a mano también va mes a mes; el estado del cuadre queda en disco, y la vuelta siguiente
lo repite mientras quede pendiente; las existencias se cuadran contra la instantánea del corte más
el libro desde el corte, y no contra el libro entero; tres casos nuevos, cada uno con su mutación;
y A, B y C medidas otra vez, con C avanzando.

## Decisión

### 1. Un mes por transacción, el del corte más uno

`LasInstantaneasMensuales.AvanzarUnMesAsync(empresa, hastaElMes)` hace **un solo mes** y confirma:

1. **Abre su transacción y toma el exclusivo de la empresa**, lo primero, como hasta ahora (ADR-0063
   §5). Revienta si ya hay una transacción abierta.
2. **Lee el corte.** Sin corte, el mes que toca es el del primer movimiento de la empresa. Si no
   tiene libro, o el primer movimiento cae después de `hastaElMes`, pone el corte en `hastaElMes` y
   no escribe ninguna instantánea: todo es posterior. Si el corte ya llega, no hace nada.
3. **Escribe las instantáneas del mes M**, el del corte más uno: las del mes anterior más el libro
   de M, por clave. Una clave que no se ha movido arrastra su saldo, y una que se mueve por primera
   vez aparece. El libro se acota por su fecha por los dos lados, así que solo se lee la partición
   de M, y se junta con su fila viva por la clave entera, con el lote y la serie comparados con
   `IS NOT DISTINCT FROM`.
4. **Pone el corte en M y confirma.** Lo cuadrado no se toca (punto 2).

**El exclusivo se suelta entre un mes y el siguiente.** Una confirmación que llega a mitad de una
puesta al día espera un mes, no la puesta al día entera, y entra entre dos: lee el corte que el mes
anterior acaba de confirmar y deja sus instantáneas hasta él, y el mes siguiente ya la ve y la
arrastra. Lo comprueba el caso de la confirmación entre dos meses.

**Sin `ON CONFLICT`.** Por encima del corte no escribe nadie: la sentencia que anota el libro llega
hasta el corte y no pasa, y volver atrás borra el mes antes de bajar el corte. Una instantánea ahí
es un descuadre, y el paso que choca con ella falla con su clave duplicada en vez de pisarla.

**Quien quiere llegar más lejos lo llama otra vez.** El trabajo mensual lo llama hasta que no hace
nada, y mira la cancelación entre un mes y el siguiente. Un mes que falla no se lleva los
anteriores, que ya están confirmados: la hora siguiente sigue desde el último.

**El fragmento `LasDebidas` desaparece.** Era lo que decía, desde el libro entero, qué instantáneas
tenía que haber. Ya no lo usa nadie: ni el avance, ni el recálculo, ni el cuadre.

### 2. Lo cuadrado, en la fila del corte

La fila del corte gana **`cuadrado_hasta`**, `date` nulable: el primer día del último mes que el
cuadre ha comparado, incluido, o nulo si no ha comparado ninguno desde que el corte se rehízo. Dos
`CHECK`: es un primer día, y no pasa del corte.

- **El avance no lo toca.** Sigue sin pasar del corte, que sube.
- **Volver atrás lo pone a nulo, y no en el mes de antes.** Volver atrás es el principio de un
  recálculo, y lo que el cuadre había comparado ya no es lo que va a haber.
- **Va en la fila del corte y no en una tabla aparte** porque la fila ya existe siempre que hay algo
  que cuadrar, y porque cada paso del avance y de la vuelta atrás la escribe en la misma
  transacción que sus instantáneas: su versión dice si algo ha cambiado (punto 5).

### 3. El recálculo a mano, mes a mes

`RecalcularAsync(empresa, hastaElMes)` ya no es una sentencia, sino dos series de pasos, cada uno en
su transacción y con el exclusivo de la empresa:

- **hacia atrás**, mientras a la empresa le quede alguna instantánea: lee su último mes, lo borra,
  baja el corte al mes anterior y deja lo cuadrado a nulo. Sin instantáneas, quita la fila del
  corte;
- **hacia delante**, los pasos del punto 1, desde el primer mes del libro hasta `hastaElMes`.

**Entre un paso y el siguiente, las confirmaciones de la empresa entran.** Una que entra mientras
vuelve atrás deja sus instantáneas hasta el corte de ese momento, y el paso siguiente las tira con
su mes; una que entra mientras avanza las deja hasta el mes recién hecho, y el siguiente las
arrastra. Las dos acaban en el libro, que es de donde sale todo lo demás.

**Sigue sin entrada en producción**, como desde el ADR-0044 §5: lo llaman los casos, y en *Medido*,
un programa aparte que llama a los mismos pasos en el mismo orden. Pone el corte donde se le diga,
también por detrás del que había.

### 4. Lo que se mide es cada mes

- **El histograma `bastion.inventario.corte.duracion_del_avance` mide cada mes**, de abrir su
  transacción al `COMMIT`, que es lo que dura el exclusivo y lo que espera una confirmación. Es lo
  que se compara con el disparador de 10 s del ADR-0064 §2. Conserva el nombre y los cubos, porque
  cambiar el nombre de una métrica rompe a quien la lea, y su descripción dice «un mes».
- **El `8701` es uno por mes**, con la empresa, el mes, las instantáneas escritas y los
  milisegundos.
- **El cuadre tiene su propia línea, el `8705`**: desde qué mes y hasta cuál, cuántas existencias e
  instantáneas comparó, cuánto tardó y cuántos descuadres encontró. El `8703` sigue avisando de los
  descuadres, ahora con los dos meses.
- **La empresa cuenta como avanzada si avanzó un mes por lo menos**, y antes del cuadre: si lo que
  falla es el cuadre, cuenta en las avanzadas y en las fallidas.
- **El indicador del retraso no cambia** (ADR-0064 §3).

### 5. El cuadre de cada vuelta es el de lo pendiente, y sigue donde se quedó

`ElCuadreDeLasExistencias.CuadrarLoPendienteAsync(empresa)` cuadra **desde el mes siguiente al
último cuadrado hasta el corte**. Con lo cuadrado a nulo, desde el mes de la primera instantánea o
del primer movimiento, el que vaya antes, porque una clave del libro sin fila viva no tiene
instantánea, y solo la ve la cadena del mes en que se movió.

- **Cada mes en su propia transacción con `REPEATABLE READ`, que solo lee**: la fila del corte con
  su versión primero, y el mes después, con la misma foto. Los meses de antes del corte comparan su
  cadena (punto 6); el del corte, su cadena y lo demás (punto 7).
- **Después de cada mes, fuera de esa transacción, la marca**: `cuadrado_hasta` pasa a ese mes si
  la fila del corte sigue en la versión que se leyó. La versión es su `xmin`, el mismo testigo que
  usa el resto del proyecto. Si un paso del avance o de la vuelta atrás ha escrito el corte entre
  medias, la marca no toca ninguna fila y el cuadre para: lo que comparó era de antes, y la vuelta
  siguiente lo compara otra vez sobre lo que haya. Lo comprueba el caso del cuadre que se cruza con
  un recálculo.
- **No toma el cerrojo de las instantáneas**: solo lee, y no para ninguna confirmación. Una que
  entra a mitad mueve a la vez su mes y los siguientes hasta el corte, y cada lectura la ve entera
  o no la ve.
- **Anota también lo que descuadra.** Lo que hace el cuadre es decirlo, y lo dice una vez: repetir
  cada hora el mismo aviso no lo arregla. Lo arregla recalcular, que deja lo cuadrado a nulo.
- **Si falla a mitad, lo anotado se queda**, y la vuelta siguiente sigue desde ahí. Por eso el
  trabajo cuadra **toda empresa con algo pendiente, haya avanzado o no**: la que se cortó entre su
  último mes y su cuadre lo termina en la vuelta siguiente, con el corte ya puesto. Es lo que el
  ADR-0064 §4 decía que no pasaba nunca, y lo comprueba el caso del cuadre que falla a medias.

**El cuadre entero se queda**, `CuadrarAsync`, para los casos y para quien recalcula a mano. Sigue
siendo una sola lectura, sin mirar ni anotar lo cuadrado: la cadena de todos los meses hasta el
corte, y lo demás.

### 6. Cada mes, contra el anterior más el libro de su mes

La cadena compara **cada instantánea con la del mes anterior más el libro de su mes**, por clave y
mes, que es lo mismo que la escribió. Lo esperado viene del mes anterior y del libro, y lo guardado,
de la tabla, con su cuenta de filas, así que una que falte, una que sobre, una que diga otra cosa y
dos para la misma clave y mes salen las cuatro. Se junta con `UNION ALL` y se agrupa, como el cuadre
del ADR-0044 §6, porque el lote y la serie son nulos en casi todas las claves. Y en sus dos
columnas, la cantidad y el valor.

- **Una instantánea mal descuadra su mes y el siguiente**, porque es lo esperado del siguiente.
- **Una que está bien en un mes que se escribió mal no se ve aquí**: se ve en el mes en que dejó de
  estar bien, que es donde hay que buscar.
- **Los límites del mes van en subconsultas escalares**, que el motor resuelve antes de leer: son
  los que podan las particiones del libro y acotan el índice del punto 8.

### 7. Las existencias y la valoración, contra el corte más el libro desde el corte

Lo de antes del corte ya está en la instantánea del corte, y la cadena comprueba que lo esté. Así
que **cada existencia se compara con su instantánea del corte más el libro desde el corte hasta
hoy**, y la cantidad y el valor de la valoración, igual, por artículo y almacén. Sin corte, el libro
entero.

- **Una instantánea del corte que diga otra cosa descuadra también su existencia y su
  valoración**, porque se comparan contra ella. Un error arrastrado mes a mes hasta el corte se ve
  aquí aunque la cadena lo encuentre en orden, que es lo que comprueba el caso del avance que no
  rehace lo que ya llega.
- **Y una existencia que mienta lo mismo que su instantánea del corte no descuadra**: cuadra con
  ella, y lo que se ve es la cadena, en la instantánea, una vez. Hasta el 2.18 salían las dos. El
  caso del lote y la serie (ADR-0048 §6) pone desde entonces una unidad de diferencia entre las
  dos copias, para que cada una salga con su lote.
- **La fecha de la valoración no está en la instantánea.** Si el par se ha movido desde el corte, la
  más alta está en lo leído. Si no, la guardada se da por buena cuando el libro tiene un movimiento
  del par ese día y ninguno después hasta el corte: dos búsquedas por el índice, cada una en las
  particiones de sus fechas. Solo cuando no se da por buena se recorre el par entero, y es un
  descuadre.
- **Las instantáneas de después del corte**, cada una un descuadre, y el tránsito, como hasta
  ahora (ADR-0053 §1 y §11).

### 8. El índice, por empresa y mes

El índice de las instantáneas por empresa pasa a ser **por empresa y mes**,
`ix_instantaneas_mensuales_empresa_id_mes`. El avance lee el mes anterior, la cadena lee dos meses
seguidos y la vuelta atrás borra el último: todo por empresa y mes. El mes va detrás porque es el
rango, y la empresa sola sigue sirviéndose del mismo índice.

## Medido

**El entorno.** La pila local en su propio proyecto de *compose*, `bastion-humo-218`, sobre
`8ef03b3`, con PostgreSQL 17.6 (`postgres:17.6-alpine`) en Docker Desktop, en esta máquina, y los
ajustes del motor por omisión: entre ellos, `jit = on` y un `max_wal_size` de 1 GB. Las mismas tres
empresas del ADR-0064, con la misma semilla: A, 10⁵ movimientos y 10⁴ claves; B, 10⁶ y 10⁴; C, 10⁶
y 10⁵. El libro, en las particiones mensuales que deja el migrador desde octubre de 2025, y las
instantáneas, 118 493, 120 000 y 992 332.

**Las órdenes.** Las del ADR-0064, cambiadas para el mes a mes. `python -X utf8 medir.py . <empresa>
primera|mensual` lee del registro el `8701` de cada mes y el `8705` del cuadre, y la sesión que hace
de confirmación espera al exclusivo de cada mes, pide el compartido y vuelve a esperar al siguiente.
`python -X utf8 explicar.py . <empresa> <salida> [sinjit]` explica el mes del avance de septiembre,
el cuadre de mayo y el del mes del corte, septiembre; con `sinjit`, con `SET LOCAL jit = off`
delante. Y el recálculo de C, `python -X utf8 recalcular.py <empresa> 2026-09-01`: un programa
aparte que llama a los dos pasos de `LasInstantaneasMensuales` en el orden de `RecalcularAsync`, y
cronometra cada uno. Los cuatro viven fuera del repositorio, en el *scratchpad* de la sesión.

**Lo medido**, en milisegundos y en el orden en que se midió. El mes más largo es el que más tiempo
tiene el exclusivo, y la espera, la más larga de las confirmaciones de cada mes:

| Ronda | Meses | El mes más largo | Suma de los meses | Cuadre | La espera |
|---|---|---|---|---|---|
| A, la primera | 12 | 281 | 3 138 | 2 045 | 280 |
| A, mensual ×3 | 1 | 285 · 293 · 289 | — | 923 · 864 · 6 222 | 269 · 274 · 275 |
| B, la primera | 12 | 12 154 | 16 796 | 13 106 | 12 150 |
| B, mensual ×3 | 1 | 310 · 313 · 335 | — | 11 056 · 11 107 · 11 032 | 302 · 304 · 326 |
| B, la primera, tras un `CHECKPOINT` | 12 | 482 | 4 190 | 8 927 | 475 |
| B, mensual, tras el mismo | 1 | 371 | — | 6 563 | 362 |
| C, la primera | 12 | 2 729 | 27 570 | 18 762 | 2 727 |
| C, mensual ×3 | 1 | 3 348 · 4 326 · 3 033 | — | 9 719 · 9 845 · 9 802 | 3 346 · 4 326 · 3 032 |

Ninguna ronda dejó un descuadre ni una vuelta fallida.

- **El mes más largo de las rondas mensuales es de 4 326 ms, el de C**, contra el disparador de
  10 s del ADR-0064 §2: el de B, 371, y el de A, 293. En el 2.15, B tenía el exclusivo 6,2 s cada
  mes, y C fallaba por plazo cada hora. Un mes cuesta lo que sus claves y el libro de ese mes: en
  la primera de A, los doce entre 244 y 281; en la de C, crecen con las claves, de 45 480 a
  100 000, y desde mayo, con las 100 000, se quedan entre 2 595 y 2 729.
- **El mes de 12 s de B es un punto de control del WAL, no el avance.** Su `INSERT` tardó 270 ms;
  el resto fue su `COMMIT`, que esperó al punto de control que había dejado en marcha la carga de
  10⁶ movimientos: el registro del motor lo da de las 01:13:53 a las 01:16:35, con 162 s de
  escritura. La misma ronda, repetida tras un `CHECKPOINT`, da 482 ms el mes más largo.
- **El cuadre no tiene el exclusivo**, así que no para ninguna confirmación: su techo es el plazo
  de la orden, 30 s. Lo que más tarda es el mes del corte, y en B y en C, casi todo es la
  compilación del JIT del motor. Sus planes cuestan 589 865 y 5 048 823, por encima del umbral de
  optimización, 500 000 por omisión, y el motor compila 1 252 y 1 345 funciones en 6 248 y 6 689 ms.
  El de A cuesta 317 817 y compila sin optimizar, en 542. Las tres mensuales de B antes del
  `CHECKPOINT`, 11 s, no se han explicado; la de después, 6 563, es lo que dice su plan.
- **La tercera mensual de A, 6 222, tampoco está explicada.** Su plan compila sin optimizar, y una
  estimación de esa ronda por encima del umbral daría esa cifra, pero no se ha comprobado.
- **El recálculo de C avanza**, en 46 202 ms. Hacia atrás, trece pasos: el primero, 1 403; los
  demás, de 69 a 34, y el último quita el corte en 11. Hacia delante, los doce meses y las 992 332
  instantáneas, con el más largo en julio, 7 373. Es más que en la primera ronda; lo probable es
  que sean las filas muertas que acaba de dejar la vuelta atrás, sin recoger todavía, pero no se ha
  comprobado.
- **Los planes**, en milisegundos de ejecución:

  | Sentencia | A | B | B sin JIT | C | C sin JIT |
  |---|---|---|---|---|---|
  | El mes del avance, septiembre | 320 | 392 | 335 | 2 879 | 2 974 |
  | El cuadre de un mes, mayo | 83 | 273 | 245 | 922 | 781 |
  | El cuadre del mes del corte | 794 | 6 687 | 410 | 9 587 | 3 032 |

- **El índice nuevo** se construye en 528, 564 y 524 ms sobre las 1 230 825 instantáneas de las
  tres empresas, con una tabla de 170 MB y un índice de 18 MB: tres veces `DROP INDEX` y `CREATE
  INDEX` en una transacción que se deshace, con `\timing`. Es lo que la migración para las
  escrituras de las instantáneas en una instalación de ese tamaño.
- **El segundo arranque** (ADR-0035) sale con 0, en 17 s, con el esquema en orden. Encontró a C con
  lo cuadrado a nulo, que es como lo deja el recálculo, y su primera vuelta la iba cuadrando mes a
  mes: al leer los cortes, ya llegaba a agosto. Es el camino del punto 5 sin un caso que lo
  fabrique.
- **Contra el 2.15**, el cuadre de la vuelta mensual pasa de 5,3 s a 0,9 en A, salvo la ronda de
  6,2; de 7,2 a 6,6 en B tras el `CHECKPOINT`, y a 11 antes de él; y de 15,3, la única vez que
  llegó, a 9,8 en C. Lo que queda en B y en C es el JIT del tercer punto.

## Lo que no cubre, dicho

- **Un mes ya cuadrado que se estropea después no lo vuelve a mirar el cuadre de lo pendiente.** Si
  el avance lo arrastra hasta el corte, lo ven las existencias (punto 7). Si no, solo el cuadre
  entero o un recálculo.
- **Una confirmación con fecha de un mes ya cuadrado** mueve ese mes y todos los siguientes hasta el
  corte, en la misma sentencia (ADR-0044 §3): la cadena sigue en orden y no hay nada que ver, pero
  el cuadre de lo pendiente tampoco lo vuelve a mirar.
- **Ninguna métrica dice que un cuadre falla una y otra vez.** Lo dicen el `8702` de cada hora y el
  contador de fallidos; lo cuadrado no se exporta.
- **El índice se crea sin `CONCURRENTLY`**, dentro de la transacción de la migración, así que para
  las escrituras de las instantáneas mientras se construye: medio segundo con 1,23·10⁶
  instantáneas (*Medido*).
- **El cuadre del mes del corte paga la compilación del JIT del motor**, unos 6 s en B y en C, que
  dependen de lo grande que es el plan y no de las filas que lee. No tiene el exclusivo y cabe en
  el plazo, así que el 2.18 no lo toca: se propone, en el PLAN, un `SET LOCAL jit = off` en la
  transacción de cada mes del cuadre, que lo deja en 0,4 s en B y en 3,0 en C. Mientras no se
  decida, el cuadre de C se queda en 9,8 s de los 30 del plazo.
- **Con dos instancias**, los pasos de una empresa se ponen en fila en el exclusivo, y los dos
  cuadres pueden comparar el mismo mes a la vez: los dos marcan lo mismo, y no hace daño.

## Consecuencias

- **El coste de un mes ya no depende de cuántos lleve la empresa**: depende de sus claves y del
  libro de ese mes. El techo del ADR-0064 deja de acercarse con el tiempo.
- **Una confirmación espera un mes como mucho**, una vez al mes, y no la puesta al día entera.
- **El cuadre que falla se repite**, desde el último mes que anotó, hasta que llega al corte.
- **Para otro proyecto con un libro *append-only* y fotos periódicas**: cada foto se escribe desde
  la anterior y el libro de su periodo, un periodo por transacción, y se comprueba igual; y el
  progreso de un trabajo que comprueba se guarda en la misma fila que su paso escribe, con la
  versión de esa fila como guarda.

## Procedencia

El avance un mes por transacción, el recálculo mes a mes, el cuadre con su estado en disco y contra
el corte, los tres casos y las medidas los fijó el usuario en el criterio del 2.18, el 2026-10-18.
Lo demás lo decide el agente al montarlo: la columna en la fila del corte y su vuelta a nulo, la
guarda del `xmin`, la marca también de lo que descuadra, el `REPEATABLE READ` de cada mes, el
índice nuevo, conservar el nombre de la métrica, que el recálculo siga sin entrada en producción,
un cuarto caso, el del cuadre que se cruza con un recálculo, para la guarda del `xmin`, y dejar
el JIT del cuadre como propuesta.
El usuario no los ha visto todavía.
