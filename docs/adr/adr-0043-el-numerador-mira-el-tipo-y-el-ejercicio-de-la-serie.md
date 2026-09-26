---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, numeracion, multiempresa]
tags: [adr, r2, r5, r8, r9, serie, ejercicio, numeracion, anulacion, rectificativa, adr-0040]
revisado: 2026-09-26
---

# ADR-0043: El numerador mira el tipo y el ejercicio de la serie, y el inverso numera con la fecha del original

- **Estado:** aceptado
- **Fecha:** 2026-09-26
- **Amplía el [ADR-0040](adr-0040-el-numero-lo-toma-una-sentencia-en-el-esquema-de-otro-modulo.md)**:
  la sentencia que numera ya no comprueba solo la empresa y el estado de la serie. Ahora comprueba
  también **de qué documento es** y **de qué ejercicio**.
- **Cierra dos preguntas abiertas del PLAN**: la del 2.4 (nadie comprueba el tipo) y la del 2.6 (en
  qué serie numera el inverso). Sale del segundo punto del encargo del 2026-09-26, y las dos
  decisiones de fondo las tomó el usuario.

## Contexto

La R5 dice que la numeración es por serie y ejercicio. Hasta aquí esa cláusula se cumplía por la
forma: cada serie cuelga de un ejercicio y tiene su contador. La forma dice de qué ejercicio es una
serie. **No impide que un documento numere en otra.** La sentencia del ADR-0040 casaba con
cualquier serie activa de la empresa. Un ajuste abierto sobre una serie de facturas se confirmaba y
gastaba un número de facturas. Un ajuste de este año, sobre la serie del año pasado, tomaba el
correlativo del año pasado.

El ítem 2.6 no lo cerró porque su pregunta es otra. La R9 pregunta si el ejercicio **de la fecha**
admite el documento. No pregunta si la serie es **de ese ejercicio**. Y apareció un caso en que las
dos no coinciden: el inverso de una anulación lleva la fecha de hoy y hereda la serie del original.

## Decisión

### 1. La sentencia que numera comprueba las dos cosas

El incremento une la serie con su ejercicio y añade dos condiciones al `WHERE`. La serie tiene que
ser del tipo del documento, con `s.tipo_de_documento` contra un parámetro. Y su ejercicio tiene que
comprender la fecha que se le pasa, con los dos extremos dentro. Si una de las dos no se cumple, no
casa ninguna fila, y el contador no se mueve. Es la misma forma de fallo que la del ADR-0040: sin
lectura previa y sin ventana entre comprobar y escribir.

### 2. Un código por cláusula, sin abrir un oráculo

Cuando no casa nada, una **tercera sentencia** lee por qué: `serie-de-otro-documento`,
`fecha-fuera-del-ejercicio-de-la-serie` o, si no es ninguna de esas, el `serie-no-numera` de
siempre. Hay dos códigos porque se arreglan de maneras distintas: cambiar de serie o cambiar de
fecha.

Esa lectura **filtra por empresa y estado igual que el incremento**. Sin eso, una serie ajena diría
de qué documentos es, y los dos códigos nuevos serían el oráculo que `serie-no-numera` cierra.
Una serie ajena de otro documento contesta lo mismo que una que no existe, y lo afirma un caso.

La lectura solo corre después de fallar y no decide nada. Por eso no necesita cerrojo: si la serie
cambia entre el incremento y la lectura, lo peor es un código menos preciso, nunca un número.

**Devuelve un escalar, el código mismo, con un `CASE`.** La primera versión devolvía una fila con
dos columnas booleanas, y la primera pasada del carril de integración la tumbó con un `42703`. Una
fila de `SqlQueryRaw` se lee con la convención de nombres del contexto que numera. El de Inventario
lo pasa todo a `snake_case` y buscaba `cae_en_su_ejercicio`, no el alias que escribía la cadena. El
bloque común no sabe qué convención tiene cada módulo. El escalar se lee por la columna `Value`, que
es de EF Core y no pasa por ninguna convención. Las ramas del `CASE` van en orden de prioridad. Si
la serie es de otro documento, se dice eso aunque además sea de otro ejercicio: cambiar de serie
arregla las dos cosas.

### 3. El tipo lo dice cada módulo, no quien llama

El puerto pasa a ser genérico, `INumeradorDeSerie<TDocumento>`, sobre el enumerado de documentos
**del propio módulo**. Cada módulo implementa en su numerador el mapa entre sus documentos y el tipo
de serie. Quien confirma dice qué documento numera, no en qué tipo de serie: ese dato no es suyo, y
dejárselo elegir sería la misma comprobación con el dato de la petición en los dos lados.

Inventario no ve `TipoDeDocumento`, que es de `Organizacion.Domain` (ADR-0013). Por eso escribe el
nombre del valor como cadena. Que esa cadena exista en el enumerado lo afirma el carril rápido desde
el único proyecto que ve los dos lados. Que la columna guarde ese nombre, y no su número, también.
El mapa **no tiene rama por defecto**: un documento nuevo sin su línea lanza, y no numera en las
series de ajustes «porque sí».

### 4. La fecha la elige quien llama, y ahí está la puerta de las excepciones

El puerto recibe `fechaQueDecideElEjercicio`. Confirmar pasa la fecha de operación del documento.
Es la misma que usa la R9, así que en el caso normal la R9 y la R5 miran el mismo ejercicio.

**La excepción, escrita:** el inverso de una anulación pasa la fecha **del original**. Así numera en
la serie del original aunque su ejercicio esté cerrado. La R9 le sigue preguntando por la fecha de
hoy, y el inverso cae en el ejercicio abierto (ADR-0041). La R2 promete que anular siempre se
puede. Sin esta excepción, anular un documento de un ejercicio cerrado obligaría a cambiarle la
serie al inverso, y el inverso dejaría de numerar en la serie del documento que anula.

### 5. Lo de hoy vale para ajustes y no es regla para todos

Una **factura rectificativa exige serie propia** (Reglamento de facturación, art. 6). Así que su
inverso no podrá quedarse en la serie del original. El mecanismo no lo impide: cada llamante elige
la serie y la fecha con la que numera su inverso. Lo que se decide aquí es la regla de los ajustes.

**Disparador, para la fase 5:** al escribir la anulación de una factura, se decide su serie
rectificativa y la fecha que pasa al numerador. Antes, se contrasta el artículo con la biblioteca.
No se copia la excepción del ajuste.

## Consecuencias

- **La cláusula «por serie y ejercicio» de la R5 tiene rojo propio.** Una serie de otro documento,
  una fecha fuera del ejercicio de la serie y los dos extremos del ejercicio se ejercen contra
  PostgreSQL. De extremo a extremo, un ajuste con la serie equivocada no se confirma y no gasta
  correlativo.
- **Abrir el borrador no lo comprueba.** La guarda es la de confirmar, igual que en la R9: lo que se
  protege es lo definitivo. Avisar ya al abrir sería una cortesía, y no forma parte de este cambio.
- **Un módulo nuevo con documentos** trae su enumerado, su numerador con el mapa, y su caso de
  carril rápido que recorre el enumerado contra `TipoDeDocumento`.
- **La tabla de verdad de las sentencias crece:** tres tablas, tres sentencias y cinco columnas más,
  todas contra el modelo de EF Core.

## Procedencia

Las dos preguntas las dejó abiertas el agente, en los cierres del 2.4 y del 2.6. Las decidió el
usuario en el encargo del 2026-09-26:

- el tipo, en el `WHERE`;
- el ejercicio, por la fecha del documento;
- el inverso, en la serie del original, con la excepción escrita;
- el disparador de la rectificativa, para la fase 5.

Qué pone rojo cada mutación del `WHERE` está en el *Estado actual* de `docs/PLAN.md`.
