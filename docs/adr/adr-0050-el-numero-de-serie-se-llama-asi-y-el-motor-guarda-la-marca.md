---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, sql, inventario, migraciones, particionado]
tags: [adr, numero-de-serie, lote, check, rename, particion, cuadre, r3, adr-0047, adr-0048]
revisado: 2026-10-02
---

# ADR-0050: El número de serie se llama así, y el motor guarda la marca

- **Estado:** aceptado
- **Fecha:** 2026-10-02
- **Sale del epílogo del 2.9**, que el usuario encargó el 2026-10-02 tras verificar el ítem (PLAN,
  *Traídas por el encargo del 2026-10-02*). Los criterios son del usuario; los medios, del agente, y
  el punto 7 dice cuáles.
- **Enmienda el ADR-0048 §1**: la exclusividad entre lote y número de serie ya no es solo del
  dominio. **Y su §3**, en los nombres: la columna, el índice y el `CHECK`.
- **Cierra lo que el ADR-0047 dejó abierto** en sus *Consecuencias*: el cuadre compara
  `ultima_fecha` con el libro.

## Contexto

En Inventario, «serie» nombraba dos cosas. `Ajuste.SerieId` es la **serie de numeración** del
documento (R5), que vive en Organización. `MovimientoStock.SerieId` y `Existencia.SerieId` son el
**número de serie** de una unidad, que vive en este módulo. Los tests de arquitectura tenían que
declarar las dos últimas como excepción, porque por su nombre casaban con la `Serie` de Organización.
Un nombre que hay que explicar en cada sitio es un nombre equivocado.

El ADR-0048 dejó además dos cosas en el dominio o sin mirar:

- **la exclusividad entre lote y número de serie** la sostenía solo `MovimientoStock`. El 2.11 y el
  2.12 traen más documentos que escriben en el libro, y cada uno sería un camino más que la
  respete o no;
- **el cuadre no miraba `ultima_fecha`**, y es una copia que decide: el dominio rechaza un documento
  atrasado contra ella (ADR-0047 §2). Una copia que decide y no se cuadra puede mentir sin que nadie
  lo vea.

## Decisión

### 1. El número de serie es `NumeroDeSerieId` y `numero_de_serie_id`

En el dominio, en la existencia, en el libro, en el cuadre y en el contrato `MovimientoDto`. La
entidad ya se llamaba `NumeroDeSerie`, así que el identificador casa con ella por nombre, como
`LoteId` con `Lote`, y **las dos declaraciones de los tests de arquitectura se borran**.

`Ajuste.SerieId` se queda como está: es la serie de numeración, y su nombre es el bueno.

### 2. En el motor se renombra, no se recrea

Lo que EF Core propone para un cambio de nombre es renombrar la columna, pero borrar y crear otra vez
las claves ajenas, el índice parcial y el `CHECK`, porque sus nombres y sus expresiones cambian. Crear
una clave ajena o un `CHECK` recorre la tabla para validarla, y en el libro eso es leerlo entero.
Así que la migración lo renombra todo:

- **las dos columnas**, con `RENAME COLUMN`. En el libro particionado llega a cada partición sola;
- **el `CHECK`**: `ck_existencias_numero_de_serie_como_mucho_una`;
- **el índice parcial**: `ix_existencias_numero_de_serie_en_un_sitio`;
- **los índices y las claves ajenas de la columna**, con los nombres que da la convención de EF Core.

**Ninguna fila se reescribe, y las expresiones se ponen al día solas.** PostgreSQL guarda las del
`CHECK` y las del filtro del índice ya analizadas, apuntando a la columna y no a su nombre. La
documentación de `ALTER TABLE` de PostgreSQL 17
(<https://www.postgresql.org/docs/17/sql-altertable.html>, consultada el 2026-10-02) dice de
`RENAME` que «no tiene efecto sobre los datos guardados». Y dice que una columna de una tabla con
descendientes no se renombra sin renombrarla en todos, y un `CHECK`, igual.

**Lo que la documentación no dice, medido** en un PostgreSQL 17.6 aparte, con una tabla particionada,
una clave ajena, un índice y un `CHECK`:

- la clave ajena **no** se renombra en las particiones que ya existen, aunque sí en las que nacen
  después;
- el índice de cada partición conserva el nombre que le puso el motor, que lleva el de la columna
  vieja;
- las dos cosas se pueden renombrar partición a partición.

Así que la migración recorre las particiones del libro y les renombra las dos. Sin eso, el libro
tendría dos nombres para la misma clave ajena según el año de la partición.

**Las listas cerradas se ponen al día en el mismo commit**: la de las restricciones que guardan una
regla (ADR-0048 §3) y la del recorrido de las migraciones sobre tablas con filas. Las dos tienen un
barrido que exige que cada nombre exista en la base, así que un nombre viejo que se quedara en la
lista las pondría en rojo. El `type`, `numero-de-serie-en-existencias`, ya decía «número de serie», y
no cambia.

### 3. La exclusividad entre lote y número de serie, también en el motor

*Enmienda el ADR-0048 §1, su última frase: «Es el dominio el que hoy no deja escribirlas juntas».*

`CHECK (num_nonnulls(lote_id, numero_de_serie_id) <= 1)`, en la existencia
(`ck_existencias_lote_o_numero_de_serie`) y en el libro (`ck_movimiento_stock_lote_o_numero_de_serie`).
`num_nonnulls` devuelve cuántos de sus argumentos no son nulos (documentación de PostgreSQL 17,
*Comparison Functions*, tabla 9.3).

- **No se traduce.** Si salta, algún camino se saltó el dominio, y eso es un defecto: un `500`, como
  los `CHECK` de la valoración. No hay nada que el usuario pueda corregir.
- **El esquema sigue pudiendo guardar las dos cosas**, que es lo que la biblioteca pide. Lo que
  cambia es quién lo impide hoy: el dominio y, detrás, el motor.
- **El disparador del ADR-0048 §1 no cambia**: el primer artículo que necesite lote y número de serie.
  Ese día la marca gana su cuarto valor, y la migración borra estos dos `CHECK`. Borrar un `CHECK` no
  toca la clave ni recorre ninguna tabla.
- **Se añade validando, en la misma sentencia.** En el libro eso recorre todas las particiones con el
  cerrojo de la tabla tomado. Mientras el libro sea pequeño, es lo sencillo. Cuando no lo sea, el
  camino es el de la convención de SQL: `NOT VALID` y después `VALIDATE CONSTRAINT`, que PostgreSQL
  17 admite para un `CHECK` sobre una tabla particionada. Se midió: la marca de «sin validar» llega a
  cada partición.
- **El orden de comprobación no cambia ningún error.** PostgreSQL comprueba los `CHECK` por orden
  alfabético de nombre (ADR-0048 §3), y `lote_o_numero_de_serie` va antes que
  `numero_de_serie_como_mucho_una` y después de `fisico_no_negativo`. Pero la fila de la existencia
  nace a cero, con un `INSERT … ON CONFLICT DO NOTHING`, antes de sumarle nada. Una fila con las dos
  marcas se para ahí, donde el físico todavía no puede saltar.

### 4. El cuadre compara `ultima_fecha` con el libro

*Cierra el punto que el ADR-0047 dejó abierto en sus Consecuencias.*

El cuadre gana un descuadre más, `valoracion-fecha`, por artículo y almacén: la fecha de operación
más alta del libro hasta hoy, contra la `ultima_fecha` de la valoración.

- **Se comparan con `IS DISTINCT FROM`**, porque las dos pueden ser nulas: la valoración que nace en
  el cerrojo y una clave sin filas en el libro. Con `<>`, una fecha nula contra una que no lo es no
  saldría, y es justo la de una valoración que falta.
- **El libro, hasta hoy**, igual que en las otras comparaciones. Una fila con fecha de mañana deja la
  `ultima_fecha` en mañana y el máximo hasta hoy en otro día, y es un descuadre.
- **El descuadre de la fecha lleva fechas, no cantidades.** `Descuadre` gana `FechaEsperada` y
  `FechaGuardada`, y su `Esperado` y su `Guardado` pasan a admitir nulo, que es lo que valen en esa
  fila. Las demás no cambian.

### 5. Los casos y las mutaciones

- **El `CHECK` del número de serie, visto en rojo.** En el mismo hueco, el índice parcial ve una sola
  fila, así que el `CHECK` es la única guarda. La mutación lo quita en la base, dentro de la
  transacción del caso, que se deshace. El caso es
  `La_misma_serie_dos_veces_en_el_mismo_hueco_la_para_el_check_y_sale_422`.
- **La exclusividad**: una fila con lote y número de serie, escrita en crudo en la existencia y en el
  libro, recibe un `23514` con el nombre de su `CHECK`. La mutación quita el `CHECK` dentro de la
  transacción del caso, que se deshace.
- **La fecha del cuadre**: el caso estropea `ultima_fecha` hacia delante, hacia atrás y a nulo, en una
  transacción que se deshace, y exige los tres descuadres. Dos mutaciones: una que deja de mirar la
  fecha, y otra que la compara con `<>`. La segunda solo la ve la fecha nula.

Los números van en la tabla del PLAN, siguiendo la numeración única.

### 6. Lo que no cambia

- **La clave de la existencia y la del libro**, salvo el nombre de una columna.
- **La traducción**: los mismos dos nombres, renombrados, al mismo `422`.
- **El orden de los cerrojos** del ADR-0046 §2, enmendado por el ADR-0048.
- **El libro**: ninguna sentencia de cambio sobre sus filas.

### 7. Qué decidió el usuario y qué el agente

El usuario fijó el renombre, el `CHECK` de la exclusividad en las dos tablas y sin traducir, la
fecha en el cuadre, y la mutación del `CHECK` del número de serie dentro de una transacción que se
deshace. El agente decidió:

- renombrar también el contrato `MovimientoDto`;
- renombrar las claves ajenas y los índices de las particiones;
- los nombres de los dos `CHECK` nuevos;
- añadirlos validando, y no con `NOT VALID`;
- la forma del descuadre de la fecha, y la segunda mutación del cuadre.

## Consecuencias

- **Dos migraciones de Inventario**, una por tema: la del renombre, que no reescribe nada, y la de
  la exclusividad, que añade los dos `CHECK`.
- **El glosario** distingue «Serie» de «Número de serie», y gana el lote y la marca de trazabilidad.
- **Los tests que nombraban las restricciones** usan los nombres nuevos. El recorrido de las
  migraciones sobre tablas con filas conserva el `CHECK` con el nombre viejo, porque existe entre la
  migración del 2.9 y la del renombre.
- **El cuadre tiene una comparación más**, y los casos que lo estropean a propósito ven un descuadre
  más donde la valoración falta.

## Procedencia

Lo fijó el usuario en el encargo del 2026-10-02, y el punto 7 dice qué decidió el agente. La
documentación de PostgreSQL 17 se consultó el 2026-10-02, y lo que no dice se midió ese día en un
contenedor `postgres:17.6-alpine` aparte.
