---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [inventario, concurrencia, observabilidad, rendimiento]
tags: [adr, r3, instantanea, corte, trabajo-periodico, plazo-de-la-orden, metricas, histograma, indicador, adr-0063]
revisado: 2026-10-17
---

# ADR-0064: El avance del corte se mide contra el plazo de cada orden, y su disparador es de 10 s

- **Estado:** aceptado
- **Fecha:** 2026-10-17
- **Sale del ítem 2.15**, de su tercer tema, «el coste del trabajo mensual, medido contra el plazo
  que de verdad decide». El criterio está en `docs/PLAN.md`, en la casilla del 2.15.
- **Enmienda el [ADR-0063](adr-0063-el-saldo-a-una-fecha-es-la-instantanea-mas-su-tramo-y-el-cerrojo-es-de-la-empresa.md)**
  en dos sitios de su §7:
  - **el disparador**, que decía «si el avance de una empresa pasa de un minuto». Un minuto no llega
    nunca: la orden se corta a los 30 s (puntos 1 y 2 de este);
  - **las métricas**, que eran tres contadores y pasan a ser cinco instrumentos: los tres, un
    histograma de la duración y un indicador del retraso (punto 3).
- **Propone, y no decide,** cambiar el avance por uno incremental (punto 5). Lo decide el usuario:
  la pregunta está en el PLAN.

## Contexto

**El ADR-0063 §7 puso el disparador en un minuto sin medir nada.** El avance rehace todas las
instantáneas de la empresa desde el libro entero, y el ADR lo llamó «lineal en el libro, una vez al
mes». Las dos cosas estaban por comprobar: cuánto tarda de verdad, y qué lo corta antes de que
llegue al disparador.

**Lo que lo corta es el plazo de la orden.** La reposición es una sola sentencia, y Npgsql la
cancela cuando pasa su plazo. El avance falla, se deshace, cuenta en `avances_fallidos`, y la hora
siguiente lo vuelve a intentar con el mismo libro. Con el disparador en un minuto, una empresa así
no lo alcanzaría nunca: fallaría cada hora a los 30 s.

## Decisión

### 1. El techo es el plazo de cada orden, 30 s

Se comprobó en cuatro sitios, y en ninguno se cambia:

- **la documentación de Npgsql**, *Connection String Parameters*: «Command Timeout (default: 30).
  The time to wait (in seconds) while trying to execute a command before terminating the attempt
  and generating an error»;
- **el código de `NpgsqlCommand`**: sin plazo propio, la orden toma el de la conexión, y sin él,
  `DefaultTimeout`, que vale 30;
- **la de EF Core**, `RelationalDbContextOptionsBuilder.CommandTimeout`: «Configures the wait time
  (in seconds) before terminating the attempt to execute a command and generating an error». Es una
  opción: si no se pone, manda la del proveedor;
- **el registro de la API**, que en cada sentencia del avance dice `CommandTimeout='30'`.

`ctx7` contestó lo mismo, 30, con un fragmento viejo en la misma respuesta que decía 20. Manda la
documentación oficial, y el registro lo confirma.

**Y nada del proyecto lo cambia.** Esta orden no devuelve nada: ni la cadena de conexión, ni las
opciones de los contextos, ni la base ponen otro plazo.

```bash
git grep -n -i -E "CommandTimeout|Command Timeout|statement_timeout|lock_timeout" -- src deploy
```

**El plazo es de cada sentencia, y el exclusivo dura toda la transacción.** El exclusivo de la
empresa se toma con `pg_advisory_xact_lock` y se suelta en el `COMMIT` o en el `ROLLBACK`. Una
confirmación de esa empresa pide el compartido con su propia sentencia y su propio plazo de 30 s.
Así que un avance que tarda T tiene paradas las confirmaciones de su empresa durante T, y si T pasa
de 30 s, fallan por plazo. Y mientras espera, la confirmación ya tiene los seis primeros eslabones
del orden de cerrojos del ADR-0063 §6, entre ellos el contador de su serie, así que las de su misma
serie esperan detrás de ella.

### 2. El disparador: 10 s, o un fallo por plazo

**Si el avance de una empresa pasa de 10 s, o falla por plazo una sola vez, se cambia por el
incremental** (punto 5). Diez segundos por dos razones:

- **es lo más que una confirmación debería esperar**, una vez al mes, a que su empresa avance;
- **deja margen para el caso frío.** El primer avance de B, justo después de cargar el libro, tardó
  26,3 s, y los tres siguientes, 6,2. Con el disparador en 10 s, un avance tres veces más lento que
  el normal todavía cabe en el techo.

**Se lee en dos sitios**: en el histograma del punto 3, el cubo de 10 s; y en el `8702` del
registro, cuya excepción dice que se agotó el plazo, junto con el contador de fallidos.

### 3. Las dos métricas, sin la empresa como etiqueta

Van en el medidor del corte, `Bastion.Inventario.Corte`, con los tres contadores del ADR-0063 §7:

- **`bastion.inventario.corte.duracion_del_avance`**, un histograma en segundos de lo que tarda el
  avance de cada empresa, de abrir su transacción al `COMMIT`. Recomienda sus cubos con
  `InstrumentAdvice`: `1, 2.5, 5, 7.5, 10, 15, 20, 25, 30, 60`. Rodean lo que decide, el disparador
  y el techo; los del SDK están pensados para milisegundos. El SDK de OpenTelemetry los respeta
  desde la 1.10.0, y el proyecto usa la 1.18.0. Solo se anota el avance que termina: el que no
  termina ya cuenta en los fallidos.
- **`bastion.inventario.corte.meses_de_retraso`**, un indicador: los meses que le faltan al corte
  más viejo para llegar al mes que toca. Se mide al acabar cada vuelta, porque lo que importa es
  dónde se han quedado los cortes, no dónde estaban. Una empresa sin corte cuenta desde el mes
  anterior a su primer movimiento: es una empresa que el avance no ha conseguido avanzar nunca, y
  es la que más tiene que verse. Un corte por delante del mes que toca no resta.

**El indicador vale cero tras cada vuelta buena.** Uno o más dice que alguna empresa no ha llegado.
Si dura más de un día, es que falla cada hora. Es la alerta que le faltaba al ADR-0063: un
contador de fallidos que sube a uno por hora se puede leer como ruido, y un retraso que no baja, no.

**Para medirlo, el avance lee el corte de cada empresa**, `IAvanceDelCorte.CortesAsync`, en un
ámbito sin inquilino con el mismo motivo que la lista, `AvanceDelCorte`. La fecha del primer
movimiento solo se busca para las empresas sin corte. Buscarla cuesta 0,66 s con 10⁶ movimientos, y
hacerlo cada hora para todas no diría nada que el corte no diga.

### 4. Lo que cuesta, y cómo crece

Con las cifras del apartado *Medido*:

- **La reposición cuesta unos 2,5 µs por movimiento y 31,4 µs por instantánea.** Es un ajuste
  lineal sobre A y B. Para C da 33,6 s; la única vez que C terminó, tardó 27,7 s, y las otras cuatro
  pasó de 30.
- **Las instantáneas son las claves por los meses desde el primer movimiento.** Cada mes la empresa
  suma una instantánea por clave aunque no se mueva nada, así que el coste sube cada mes sin
  techo. Con el ajuste, B sube unos 0,5 s al mes y cruza los 10 s en unos siete meses; A, unos
  0,33 s al mes, en año y medio.
- **El ajuste lineal es optimista.** La reposición junta los meses de cada clave con los meses en
  que se movió, en un *merge join* por tres columnas —artículo, almacén y ubicación—, y deja el
  lote, la serie y el mes en el filtro de la junta. Así que por cada clave compara cada mes con
  cada mes: 1,08 millones de filas descartadas en A, 1,32 en B y 4,87 en C. Crece con el cuadrado
  de los meses.
- **La clave foránea de la instantánea a la existencia es de un tercio a dos tercios del coste.** Su
  disparador tardó 2,07 s en A (el 54 %), 2,01 s en B (el 32 %) y 18,7 s en C (el 63 %), una llamada
  por instantánea. No se propone quitarla: es lo que impide una instantánea de una existencia que
  no existe.
- **El cuadre crece igual**, porque recorre todas las instantáneas: 5,3 s en A, 7,2 en B y 15,3 en
  C. Y va después del `COMMIT` del avance: si falla, el corte ya está puesto, la hora siguiente no
  avanza nada, y ese cuadre no se repite nunca.

### 5. La propuesta: el avance incremental, un mes por transacción

**Lo que se propone.** Cada avance hace un solo mes: el del corte más uno. Sus instantáneas son las
del mes anterior más el libro de ese mes, por clave. Pone el corte en ese mes y confirma. Una
empresa con varios meses pendientes los hace uno a uno, cada uno en su transacción, y suelta el
exclusivo entre uno y otro. El coste de cada mes es de las claves y del libro de ese mes, y deja de
depender de cuántos meses lleve la empresa. Con el ajuste, un mes de C cuesta unos 3 s, y uno de B,
unos 0,5.

**El cuadre, a juego.** El de cada avance compara solo el mes nuevo y las existencias. El entero se
queda para recalcular a mano, que es cuando se quiere.

**Lo que no se propone**:

- **subir el plazo**: mueve el techo sin cambiar la pendiente, y alarga el exclusivo, así que las
  confirmaciones de la empresa fallarían por su propio plazo;
- **quitar la clave foránea**: es integridad, no rendimiento;
- **subir `work_mem`**: las ordenaciones van a disco (87 MB en B y en C), pero es una fracción
  pequeña del coste, y cambia la base entera.

**Por qué se pregunta y no se hace.** Cambia la forma del recálculo, que el ADR-0044 y el ADR-0063
decidieron. Y el techo no está igual de cerca para todas las empresas: a B, con 10⁴ claves, le
quedan unos siete meses hasta el disparador; C, con 10⁵, ya ha pasado el techo.

## Medido

**El entorno.** La pila local en su propio proyecto de *compose*, `bastion-humo-215`, con
PostgreSQL 17.6 (`postgres:17.6-alpine`) en Docker Desktop, en esta máquina. Las particiones del
libro de los doce meses de octubre de 2025 a septiembre de 2026, como las habría dejado el migrador
en una instalación con un año en marcha. Tres empresas sintéticas, una a una, cada una con su libro,
sus existencias y sus valoraciones, sacadas del propio libro para que el cuadre salga limpio:

| Empresa | Movimientos | Claves | Instantáneas |
|---|---|---|---|
| A | 10⁵ | 10⁴ (2 000 artículos × 5 almacenes) | 118 493 |
| B | 10⁶ | 10⁴ (2 000 × 5) | 120 000 |
| C | 10⁶ | 10⁵ (20 000 × 5) | 992 332 |

**Las órdenes.** La semilla, por empresa, con `psql -v empresa=<uuid> -v etiqueta=<X> -v n=<N>
-v articulos=<K> -v almacenes=5 < sembrar.sql`. La medida, con `python -X utf8 medir.py . <empresa>
primera|mensual`: reinicia la API, cuya primera vuelta corre al arrancar, y lee del registro el
`8701` o el `8702` de la empresa y cada sentencia con su duración y su plazo. A la vez, otra sesión
espera a ver concedido el exclusivo de la empresa y pide el compartido, como una confirmación. La
ronda `mensual` lleva antes el corte a agosto, que es el estado de cada mes. El plan, con `python -X
utf8 explicar.py . <empresa> <salida>`: `EXPLAIN (ANALYZE, BUFFERS)` de las dos sentencias largas,
con el texto que registró la API, dentro de una transacción que se deshace. Los tres guiones viven
fuera del repositorio, en el *scratchpad* de la sesión.

**Lo medido**, en milisegundos:

| Ronda | Avance | Cuadre | Espera del compartido |
|---|---|---|---|
| A, la primera | 3 822 | 5 252 | 3 805 |
| A, mensual ×3 | 3 860 · 4 075 · 3 970 | 5 233 · 5 298 · 5 268 | 3 843 · 4 058 · 3 949 |
| B, la primera | 26 272 | 7 349 | 26 261 |
| B, mensual ×3 | 6 219 · 6 234 · 6 302 | 7 127 · 7 221 · 7 157 | 6 207 · 6 225 · 6 295 |
| C, la primera | falla por plazo a los 30 026 | — | 30 011 |
| C, la primera, otra vez | 27 693 | 15 260 | 27 691 |
| C, mensual ×3 | fallan por plazo: 30 031 · 30 024 · 30 025 | — | 30 396 · 43 712 · 30 578 |

- **La primera de B fue la fría.** Llegó detrás de la carga: el *autovacuum* recorrió todas las
  particiones entre las 18:21:26 y las 18:21:34, y había un punto de control del WAL en curso.
- **En C, el borrado de las instantáneas viejas tardó 387, 13 709 y 569 ms**, fuera de la
  reposición, que es la que llega al plazo.
- **La espera del compartido es lo que esperaría una confirmación.** La sesión que la mide no tiene
  plazo; una confirmación de verdad habría fallado a los 30 s en las cuatro rondas de C que pasan.
- **Los planes**: la reposición, 3,81 s en A, 6,24 en B y 29,47 en C, y el cuadre, 5,29, 7,79 y
  14,71. Las cifras de la clave foránea y del filtro de la junta del punto 4 salen de ahí.

## Consecuencias

- **El disparador se puede ver**: está en el histograma, con su cubo, y en el indicador del retraso,
  que deja de ser cero cuando una empresa se queda atrás.
- **Una empresa de 10⁵ claves no avanza con el avance de hoy.** Falla cada hora, a los 30 s, y
  durante esos 30 s sus confirmaciones esperan. Hasta que el usuario conteste a la propuesta, es lo
  que pasaría con una empresa así.
- **El cuadre que falla después del `COMMIT` no se repite.** Lo arregla la propuesta del punto 5, si
  se acepta; si no, queda como pregunta abierta.
- **Para otro proyecto con Npgsql**: un trabajo de fondo que hace una sola sentencia grande tiene
  como techo el plazo de la orden, 30 s por omisión, y no el que diga su disparador. Y un cerrojo
  de transacción alarga ese plazo a todo lo que espera detrás.

## Procedencia

La medida, el disparador, las métricas y la propuesta los decide el agente, en el 2.15, contra el
criterio que acordó el usuario en el repaso de la fase 2. El usuario no los ha visto todavía. La
pregunta del avance incremental está en el PLAN, sin contestar.
