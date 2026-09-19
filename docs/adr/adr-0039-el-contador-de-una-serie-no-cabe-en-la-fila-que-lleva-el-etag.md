---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, migraciones, concurrencia]
tags: [adr, r5, r11, numeracion, series, xmin, etag, esquema, adr-0007, adr-0015]
revisado: 2026-09-19
---

# ADR-0039: El contador de una serie no cabe en la fila que lleva el `ETag`

- **Estado:** aceptado
- **Fecha:** 2026-09-19
- **Enmienda el [ADR-0007](adr-0007-lo-irreversible-del-esquema-de-organizacion.md), punto 5**, que
  es de las decisiones casi irreversibles del esquema de Organización. Por eso va en un ADR propio y
  no dentro del de la numeración: quien mañana lea el 0007 tiene que encontrar aquí qué parte de él
  sigue en pie y cuál no.
- **Sale del ítem 2.4**, al tomar la decisión de diseño antes de escribir su código.

## Contexto

Dos decisiones correctas, tomadas con cinco ítems de diferencia, se contradicen sobre la **misma
fila** — y la contradicción no se veía porque hasta hoy nadie numera:

- **ADR-0007 §5 (ítem 0.4):** `series.contador` es un `bigint` **de la fila**, jamás una secuencia
  de PostgreSQL, porque `nextval` no se revierte y la numeración saldría con huecos —lo que el
  artículo 6.1.a del RD 1619/2012 no permite—.
- **ADR-0015 y el ítem 0.9:** `Serie` lleva testigo de concurrencia, y el testigo **es `xmin`**, la
  columna de sistema en la que PostgreSQL apunta la transacción que escribió la fila. Ese testigo es
  el `ETag` que publica `GET /series/{id}` y el que `PUT /series/{id}` exige en `If-Match` (R11).

`xmin` **cambia en cada escritura de la fila**. No hay forma de decirle a PostgreSQL «esta columna
no cuenta»: el testigo es de la fila entera, no de un subconjunto de sus columnas. Así que con el
contador dentro de `series`:

> Cada documento confirmado invalida el `ETag` de todo el que tenga esa serie abierta en pantalla.
> Guardar el formato contesta `412 Precondition Failed` **sin que nadie la haya editado**.

Y el reparto de frecuencias es el peor posible: una serie se edita una vez al año y se confirman
documentos contra ella todo el día. Sobre una serie en uso, esa pantalla no se puede guardar.

**Por qué no lo vio ningún test, y por qué eso no es un descuido reparable con un caso más.** La
suite entera de `Serie` es correcta y seguiría en verde: ningún caso abre el editor de una serie
**y confirma un documento en medio**, porque hasta el ítem 2.3 no había nada que confirmar y hasta
el 2.4 nada que numerara. El defecto no está en lo que los tests miran, está en que la combinación
que lo produce no existía todavía. Es exactamente el hueco que la
[puerta de clarificación](../PLAN.md) del `CLAUDE.md` §2 tiene que cerrar **antes** de escribir el
código, y no después.

## Decisión

### 1. El contador se muda a `organizacion.contadores_de_serie`

Una tabla con `serie_id` de **clave primaria** y clave ajena a `organizacion.series`, y una sola
columna de datos, `ultimo_numero`. Una fila por serie, **creada con la serie** y borrada con ella.

Con eso, numerar toca la fila de `contadores_de_serie` y no la de `series`:

- el `xmin` de la serie se queda quieto y su `ETag` sobrevive a la jornada;
- el cerrojo se sigue tomando **sobre una fila**, que es lo que el mecanismo de numeración necesita
  y lo que el ADR-0007 §5 quería decir con «bloquear esta fila»;
- y la `SEQUENCE` sigue igual de descartada, que es el punto siguiente.

### 2. Qué parte del ADR-0007 §5 se enmienda, y qué parte no

**Sigue en pie, y es lo que ese punto decidía de verdad:** *el contador es una **columna de una
fila**, jamás una secuencia de PostgreSQL ni un `IDENTITY` ni ningún generador del servidor.* El
argumento no se toca ni una coma: las secuencias no son transaccionales, un `nextval` consumido en
una transacción que luego se deshace deja el número gastado, y una numeración con huecos incumple
el artículo 6.1.a del RD 1619/2012. Lo que cambia es **en qué fila** vive esa columna, no de qué
clase de cosa es.

**Se enmienda la frase literal** «`series.contador` es un `bigint` de la fila, y se sube llamando a
`Serie.RegistrarNumeroAsignado`». Las dos mitades caen, y por motivos distintos:

- La columna ya no está en `series`.
- `Serie.RegistrarNumeroAsignado` **se borra**. No porque estorbe: porque **nadie puede llamarlo**.
  `Serie` vive en `Organizacion.Domain` y ningún módulo ve el interior de otro, así que ni
  Inventario ni Facturación podrían usarlo nunca. El porqué entero está en el ADR de la numeración.

**Y se enmienda la última frase del punto**, «que el contador viva en la fila también es lo que
permite que el `409` de *esta serie ya ha numerado* sea comprobable: `SePuedeSuprimir` es
`Contador == 0`». Sigue siendo `Contador == 0` y sigue siendo comprobable —ver el punto 4—, pero ya
no es «la fila» la que lo permite, sino que la fila hija se **carga siempre** con la serie.

### 3. `Serie.Contador` sigue existiendo, y el contrato de la API no se mueve

La mudanza es **de tabla, no de modelo**. `Serie` gana una navegación a su fila de contador,
configurada con carga automática, y `Serie.Contador` la lee. Por tanto `SerieDto.Contador`, el mapeo
a DTO, el orden por `contador` del listado y `SePuedeSuprimir` **no cambian de forma**: ningún
cliente de la API se entera de nada.

Es el mismo criterio que `Divisa.Decimales`, que sale del catálogo del código y no de una columna:
*quien lee el DTO no tiene por qué saber de dónde viene cada campo*.

**Lo que no puede pasar es que la fila hija falte.** Un contador ausente que se leyera como cero
sería `SePuedeSuprimir` diciendo que sí sobre una serie que ya ha numerado, en silencio y con
`DELETE` por delante. Por eso la navegación es **requerida y de carga automática**, y la propiedad
**lanza** si no está cargada en vez de contestar cero: el modo de fallo pasa de «borra lo que no
debía» a «no contesta», que es el único de los dos que se puede depurar.

### 4. Nada en C# puede mover el contador, y eso es más fuerte que el método que sustituye

`ContadorDeSerie` no tiene ningún `set` accesible ni ningún método que suba el valor. Lo único que
lo incrementa es la sentencia del mecanismo de numeración, que **incrementa sobre lo que hay**
(`ultimo_numero = ultimo_numero + 1`), condicionada por su `WHERE`.

Donde `RegistrarNumeroAsignado` era la última defensa contra un llamante que pasara el número
equivocado —comprobaba `numero == Contador + 1` y lanzaba—, **aquí no hay número que un llamante
pueda pasar**. La invariante de R5 no viaja ya en un método que alguien puede no llamar: viaja en la
única sentencia que escribe esa columna.

### 5. La fila hija lleva **su propio** testigo de concurrencia, y eso no es simetría

Es lo que sostiene la carrera **suprimir-contra-confirmar**, que hasta hoy sostenía el `xmin` de la
serie sin ayuda. Antes: `EliminarSerie` exige `If-Match`, y como cualquier `UPDATE` movía el `xmin`
de `series`, quien numerara en medio hacía fallar el borrado. Al sacar el contador de esa fila **ese
argumento se queda sin base**: numerar ya no la toca, así que una serie leída antes conserva su
versión buena y `SePuedeSuprimir` se calcularía sobre un contador que otro acaba de subir.

Con testigo en la fila hija, el borrado de la serie arrastra el de su contador **en el mismo
`SaveChanges`**, y ese `DELETE` lleva dentro la versión que se leyó. Quien numeró en medio la movió,
el borrado no casa y la operación falla. **La respuesta al cliente sigue siendo la misma `412`**: no
cambia el comportamiento, cambia quién lo produce.

Y se **comprueba**, en vez de darse por hecho: que un `DELETE` de una entidad dependiente arrastrada
por su principal lleve su testigo dentro es un hecho del ORM, no una intención nuestra. El caso
numera entre la lectura y el borrado, contra PostgreSQL de verdad.

### 6. La clave ajena es `RESTRICT`, y quien borra la hija es el ORM

`ON DELETE RESTRICT`, como las cuatro claves ajenas del ADR-0007 §8, y por el mismo motivo que allí
—en un ERP, un borrado en cascada es la forma más rápida de perder un histórico—. La cascada es
**del lado del cliente**: el ORM marca la fila hija cargada como borrada y la borra **antes** que su
principal, en la misma transacción y con su testigo puesto.

Con cascada en la base pasarían dos cosas que no interesan: un `DELETE` a mano sobre `series` se
llevaría el contador sin que nadie lo comprobara, y el borrado de la hija no llevaría testigo, que
es justo lo que el punto 5 necesita.

### 7. La fila hija **no se audita**, y el motivo no es de gusto

El incremento es **SQL crudo**, así que no pasa por el interceptor de auditoría: un `SeAudita()` en
esa columna sería una promesa que el mecanismo no puede cumplir, y una traza que dijera lo que no
pasa por ella es peor que ninguna. El ADR-0007 §5 anotaba esto mismo como algo a revisar «cuando
llegue la fase 5, con el número delante»; llegó antes, en el 2.4, y esta es la revisión.

Lo que queda del número en el rastro no es la columna: es **el documento que lo lleva**, que sí se
audita y sí apunta a su serie.

### 8. `empresa_id` no se copia a la fila hija

El inquilinato de esa fila es el de su serie. La sentencia cruda comprueba la empresa **sobre la
fila de `series`** que lee para condicionar el incremento, que es exactamente la fila que el filtro
global habría protegido — así que la comprobación no es una imitación del filtro, es la misma.

Copiar la columna daría un segundo sitio donde guardar el mismo dato, con su posibilidad de
divergir, para no ganar nada: no hay ninguna consulta que pueda empezar por la fila del contador.
Que siga sin haberla lo vigila `ElFiltroNoSeSaltaPorAhiTests`, que prohíbe su `Set<>` por nombre.

### 9. La migración rellena antes de tirar

La migración **crea la tabla, copia `id, contador` de cada serie existente y solo entonces quita la
columna**, en ese orden y en la misma migración. Contra una base recién creada da igual; contra una
con filas dentro es la diferencia entre mudar el contador y perderlo.

No es una precaución teórica: `LasMigracionesSobreTablasConFilasTests` aplica la historia entera de
migraciones **una a una, inventando una fila en cada tabla antes de cada paso**, que es lo que verá
una instalación de verdad a partir del segundo despliegue.

## Consecuencias

- **`series` deja de moverse por numerar.** Su `ETag` solo cambia cuando cambia la serie, que es lo
  que R11 prometía y lo que la ficha necesita para poder guardarse.
- **Una tabla más en `organizacion`**, con una fila por serie. El coste es una unión en cada lectura
  de serie; se paga siempre, porque la carga es automática, y es lo que evita el cero silencioso.
- **Dos filas que borrar en vez de una** al suprimir una serie que no ha numerado. Las dos caen en
  el mismo `SaveChanges`, y la segunda lleva el testigo que sostiene la carrera.
- **La auditoría de la numeración deja de existir como columna**, y se dice dónde está en su lugar.
  Si algún día hiciera falta la traza del contador aparte del documento, habría que pasar el
  incremento por el modelo — y eso es incompatible con tomar el cerrojo en la sentencia.
- **El ADR-0007 sigue siendo el sitio donde se lee lo irreversible del esquema de Organización**, y
  su punto 5 hay que leerlo con este delante. Lo que no se toca de él: los ocho puntos restantes,
  incluido el §8 —ninguna cascada en la base— del que este ADR es un caso y no una excepción.

## Procedencia

Ítem 2.4 del checklist de `docs/PLAN.md`, decisión 1 de las tomadas **antes** de escribir su código.
El mecanismo que se apoya en esta tabla —la sentencia, el cerrojo, la segunda excepción al ADR-0013
y la corrección de lo que `Serie.cs` prometía— está en el ADR de la numeración, no aquí.
