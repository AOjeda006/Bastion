---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, concurrencia, multiempresa, transacciones]
tags: [adr, r2, r5, r9, r11, r16, ejercicio, periodo, sql-crudo, cerrojo, for-share, for-update, transacciones, adr-0013, adr-0014, adr-0040]
revisado: 2026-09-25
---

# ADR-0041: El cerrojo del ejercicio lo pide una lectura cruda, y la cláusula 2 del criterio admite leer si la lectura trae el cerrojo

- **Estado:** aceptado
- **Fecha:** 2026-09-25
- **Enmienda el [ADR-0040](adr-0040-el-numero-lo-toma-una-sentencia-en-el-esquema-de-otro-modulo.md)**
  en la cláusula 2 de su criterio (§2): «una sentencia que **no lee nada para decidir**». Las dos
  sentencias de este ítem leen para decidir —es para lo que existen—, así que o no entraban o el
  criterio cambiaba. El propio ADR-0040 dice cuál de las dos cosas toca: quien traiga una excepción
  más «tiene que defender las cuatro cláusulas o traer un ADR que las cambie». Éste es ese ADR.
- **Sale del ítem 2.6**, que es el que pone viva la R9.

## Contexto

La R9 dice que los periodos se abren y se cierran. Hasta el ítem 2.5 eso eran dos estados en una
fila: cerrar un ejercicio no impedía nada, y un ajuste con fecha dentro de un ejercicio cerrado se
confirmaba igual. Para que el cierre signifique algo, **confirmar tiene que preguntar por el
ejercicio de la fecha del documento y negarse si está cerrado**.

Preguntar es fácil; lo que no es fácil es que la respuesta siga siendo cierta cuando el documento se
escribe. Sin más, la carrera es ésta: una confirmación lee «abierto», un cierre entra y confirma, y la
confirmación escribe después un documento definitivo dentro de un periodo que ya estaba cerrado.
Nada falla, ningún reintento lo destapa, y el documento queda donde la R9 dice que no puede estar.

**La R11 no lo separa, y ese es el argumento entero.** El testigo de concurrencia separa dos
escrituras sobre **la misma fila**. Aquí las dos operaciones no escriben la misma fila: cerrar
escribe la del ejercicio; confirmar escribe un documento y ni toca el ejercicio. No hay versión que
chocar.

Lo que sí las separa es un **cerrojo de fila** sobre el ejercicio: compartido al confirmar,
exclusivo al cerrar. Y EF Core no tiene traducción para ninguna cláusula de bloqueo —ni `FOR SHARE`
ni `FOR UPDATE`—, así que la única forma de pedirlo es SQL crudo. Que hoy está prohibido salvo en
los sitios de la lista cerrada de `ElFiltroNoSeSaltaPorAhiTests`, y cuya entrada decide el criterio
del ADR-0040.

## Decisión

### 1. La guarda es confirmar; la pregunta del cierre es la cortesía

Son dos mecanismos distintos y conviene no confundirlos, porque solo uno de ellos sostiene la regla.

- **La guarda** es la pregunta que hace **confirmar** (y anular, punto 7), con el cerrojo puesto:
  `IConsultaDeEjercicios.ParaEscribirEnAsync(fecha)`. Es la que decide si un documento definitivo
  entra en un periodo, y la única que no se puede saltar.
- **La cortesía** es la pregunta que hace **cerrar** a cada módulo con documentos —¿te quedan
  borradores con fecha dentro?—, que este mismo ítem añade. Sirve para que un cierre no deje
  borradores huérfanos sin avisar, y el `409` nombra al módulo. Pero no sostiene la regla: un borrador que nace
  **después** del cierre no lo vio ningún barrido, y aun así no se confirma. El caso que lo afirma
  (`Cerrado_el_ejercicio_el_borrador_ya_no_se_confirma_y_no_queda_nada_a_medias`) monta la escena en
  ese orden a propósito.

**Se pregunta al confirmar y no al crear el borrador** porque lo que la R9 protege es lo definitivo:
el borrador sí nace, y se queda en borrador hasta que haya un periodo que lo admita.

### 2. Compartido al confirmar, exclusivo al cerrar, y el estado en la misma lectura

La sentencia de confirmar toma `FOR SHARE` sobre la fila del ejercicio que comprende la fecha; la de
cerrar toma `FOR UPDATE` sobre la fila del ejercicio que se cierra. **Las dos traen el estado en la
misma lectura que pide el cerrojo**, y eso es lo que hace que la lectura no se pueda quedar vieja:
desde ese instante y hasta el `COMMIT`, nadie más puede cambiar esa fila.

El reparto no es simétrico por capricho. Muchas confirmaciones a la vez sobre el mismo ejercicio son
la operación normal, y los cerrojos compartidos conviven entre sí; un cierre no convive con ninguna
—el exclusivo espera a que suelten los compartidos que ya estaban dentro, y los que lleguen después
esperan al cierre y luego lo obedecen—. Por eso se descartó también la alternativa sin SQL crudo de
tomar el cerrojo con un `UPDATE` tonto sobre la propia fila: escribiría —y auditaría— un cambio que
nadie ha pedido, y además es exclusivo, así que pondría en fila a todas las confirmaciones de la
empresa.

Los dos sentidos se ejercen con **dos transacciones de verdad** contra PostgreSQL
(`El_cierre_espera_a_la_confirmacion_que_ya_estaba_dentro`,
`La_confirmacion_espera_al_cierre_que_ya_estaba_dentro_y_luego_lo_obedece`), y el reparto se afirma
por su nombre desde el carril rápido
(`LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests.El_compartido_es_el_de_confirmar_y_el_exclusivo_el_de_cerrar`):
intercambiar las dos cláusulas dejaría el mecanismo de adorno sin que nada más fallara. Quitar
cualquiera de las dos pone rojos esos tres casos; está medido.

**Y las dos revientan si no hay transacción abierta**, por lo mismo que el numerador (ADR-0040 §6):
un cerrojo solo dura hasta el final de su transacción, y sin una que abarque la lectura y la
escritura se soltaría al acabar la propia lectura. Parecería protegido sin estarlo.

### 3. La cláusula 2 del criterio, dicha de nuevo

El ADR-0040 exigía, en su cláusula 2, «una sola fila y una sola columna, con una sentencia que no lee
nada para decidir», y explicaba el motivo: que no hubiera «un valor que el llamante pueda traer ni una
lectura previa que se pueda quedar vieja». **El motivo sigue en pie; lo que cambia es la frase**, que
solo admitía una forma de cumplirlo. Queda así:

> 2. **Es una sola fila y una sola columna, y ningún valor lo decide el llamante.** La sentencia o
>    no lee nada para decidir —opera sobre lo que hay, como el incremento del contador—, **o lo que
>    lee lo trae con el cerrojo que impide que se quede viejo hasta el `COMMIT`**, en la misma
>    sentencia y no en una anterior.

Las dos de este ítem cumplen la segunda forma. **Una sola fila**: la de cerrar va por clave; la de
confirmar va por fecha, y que una fecha caiga en un solo ejercicio lo sostiene la restricción de
exclusión del motor (R9, *se abren*) —y si alguna vez devolviera más de una, el puerto lanza en vez
de elegir—. **Una sola columna**: el estado, que tiene dos valores. **Ningún valor del llamante**:
los dos puertos reciben la fecha o el identificador, nunca la empresa (punto 5).

Lo que la frase nueva **no** admite es una lectura por el ORM seguida de un cerrojo, ni un cerrojo
seguido de una decisión tomada sobre otra lectura. Las dos cosas dejan dos versiones de la misma fila
en la misma operación.

### 4. Las otras tres cláusulas, una a una

1. **Atómica con el documento.** El compartido tiene que durar hasta el `COMMIT` **del que escribe
   el documento**, así que va en la transacción de Inventario —la que abre el filtro de idempotencia
   al confirmar, ADR-0040 §6—. En otra transacción se soltaría antes de tiempo.
2. *(La del punto 3.)*
3. **El dueño no puede exponerlo.** Organización **declara** la pregunta —`IConsultaDeEjercicios`,
   la duodécima puerta pública— pero **no puede contestarla**: contestarla desde
   `OrganizacionDbContext` sería otra conexión y otra transacción, y el cerrojo se soltaría al acabar
   esa lectura. Por eso la implementa **cada módulo con documentos**, sobre su propia transacción
   (`LosEjerciciosDesdeInventario`), igual que el numerador deriva el suyo sobre su contexto. El
   exclusivo, en cambio, vive en el esquema de su propio módulo (`CerrojoDeEjercicios`); para él la
   cláusula no aplica, y está en la lista por la otra razón: el ORM no sabe pedir el cerrojo.
4. **La empresa, comprobada dentro y con un caso que se pone rojo.** Las dos sentencias comparan
   `empresa_id` contra el valor de `IInquilinoActual` —de donde lo toma el filtro global, nunca de la
   petición—, **también la del propio esquema**: el filtro no alcanza al SQL crudo y el identificador
   viene de la ruta.

**La cláusula 4 no estaba cubierta, y lo dijo una mutación, no una lectura.** Quitada la comparación
de la sentencia de Organización, **ningún caso de ningún carril** se puso rojo: el cerrojo encontraba
la fila de otra empresa, la bloqueaba en exclusiva y contestaba su estado antes de que la lectura por
el ORM —que sí lleva el filtro— dijera que no existía. Quitada la de Inventario se pusieron rojos
dieciocho casos, y **ninguno por diseño**: caían en la guarda de «más de una fila» porque el carril de
integración comparte la base entre cientos de empresas con ejercicio del año en curso; con una sola
empresa en la base, verde. Ahora hay un caso por sentencia en el carril rápido
(`LaSentenciaDelEjercicioMiraLaEmpresaTests`) y uno de extremo a extremo
(`ElEjercicioRigeElAjusteTests.Cerrar_el_ejercicio_cerrado_de_otra_empresa_es_el_mismo_404_que_uno_inventado`),
y cada una de las dos mutaciones pone roja exactamente la suya.

### 5. Dos entradas en la lista cerrada, por su ruta

`ElFiltroNoSeSaltaPorAhiTests` las lleva con su motivo al lado:

- `src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/Repositorios/LosEjerciciosDesdeInventario.cs`
  — el compartido, desde el esquema de Inventario sobre `organizacion.ejercicios`.
- `src/Modules/Organizacion/Bastion.Organizacion.Infrastructure/Persistencia/Repositorios/CerrojoDeEjercicios.cs`
  — el exclusivo, en el esquema propio y en un fichero aparte, como `CerrojoDeLaBandeja`, para que la
  excepción se lea de una vez.

**Son dos y no una** porque son los dos lados del mismo cerrojo y viven en módulos distintos: cada
lado tiene que ir en la transacción de quien escribe. Y son **las primeras de la lista que no
escriben nada**: lo que las trae no es lo que escriben, es la cláusula de bloqueo que llevan pegada.

Como la numeración, quedan atadas a la forma de una tabla que no es suya —esquema, tabla y
columnas, escritas a mano—, y eso se paga igual:
`LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests` compara las cadenas contra el modelo de EF Core, y
que el estado se guarde como texto con los nombres del `enum` se afirma aparte, porque la sentencia
lo lee como texto.

### 6. La transacción del cierre la abre la unidad de trabajo

Cerrar exige `If-Match`, y ninguna acción pide a la vez `If-Match` e `Idempotency-Key`: está prohibido
con su motivo escrito, y el duro es que la transacción de la idempotencia va sin puntos de guardado,
así que un choque de concurrencia la dejaría abortada y el `412` saldría convertido en `500`. Así que
el filtro de idempotencia —que es quien abre la transacción en las demás escrituras (ADR-0014)— aquí
**no puede**, y la abre `IUnidadTrabajoDeOrganizacion.EnTransaccionAsync`. Fue decisión del usuario
entre las opciones que había, y no contradice al ADR-0014, que habla de las peticiones idempotentes.

Si ya hay una transacción abierta, no abre otra: anidar lanza en EF Core, y confirmar dentro soltaría
los cerrojos que la de fuera todavía necesita. Quien la abrió es quien la cierra.

### 7. Anular pregunta por la fecha de hoy, porque el inverso es un documento

Anular no quita: añade un contra-documento (R2), y ese contra-documento es un documento como
cualquier otro, así que la guarda le vale igual. **Nace con la fecha de hoy y no con la del
original**: con la del original, anular un documento de un periodo cerrado sería escribir dentro de
él, y la R2 dejaría de poder aplicarse justo a los documentos por los que existe —los viejos—. Así
que anular pregunta por el ejercicio de **hoy**, y el cerrado se queda como estaba (R3).

Con el inverso cayendo en el ejercicio abierto la pregunta se contesta que sí, así que **la guarda
de anular no la ejerce el cierre sino el hueco**: hoy fuera de todo ejercicio. Una mutación que la
anulaba no puso nada rojo; el caso que la cubre
(`Anular_con_hoy_fuera_de_todo_ejercicio_no_escribe_el_inverso`) salió de ahí.

### 8. El orden dentro del cierre compra el error correcto, no la seguridad

Cerrar toma el cerrojo **antes** de leer el ejercicio por el ORM. Invertirlo no pone roja ninguna
prueba, y está medido: la R11 lo recoge por detrás —la entidad leída antes del cerrojo trae un
testigo viejo, y el `UPDATE` final no encuentra fila—. Lo que el orden compra es **el error que se
devuelve**: un `ejercicio-ya-cerrado` que dice lo que pasa, en vez de un `412` sobre una versión que
el cliente tenía bien cuando la pidió. El comentario del caso de uso lo dice así desde que la
medición corrigió lo que decía antes.

## Consecuencias

- **El criterio del ADR-0040 sigue siendo de cuatro cláusulas**, con la segunda reescrita. Quien
  traiga la siguiente excepción la defiende contra esta redacción, y leer el ADR-0040 exige tener
  éste delante.
- **La confirmación de un documento serializa contra el cierre de su ejercicio**, además de contra
  las demás confirmaciones de su serie (ADR-0040). Entre confirmaciones del mismo ejercicio no hay
  contención nueva: los compartidos conviven.
- **Cerrar un ejercicio espera** a que terminen las confirmaciones que ya estaban dentro. Es el
  precio de que el cierre signifique algo.
- **Cada módulo con documentos implementa `IConsultaDeEjercicios`** sobre su propia transacción y
  entra en la lista cerrada con su ruta, como el numerador. Hoy solo Inventario.
- **Lo que este ADR no trae, dicho:** que un documento vaya a la serie **de su ejercicio** —lo que el
  ADR-0040 dejaba para este ítem— sigue sin comprobarse, y ahora se sabe por qué no es trivial: el
  inverso hereda la serie del original, que cuelga del ejercicio del original, mientras que su fecha
  es la de hoy. Está en las preguntas abiertas de `docs/PLAN.md`, con la del factor a unidad base:
  `CrearInverso` copia el factor del original, y así tiene que seguir si algún día se valida contra
  `ConversionUM` al confirmar.

## Procedencia

Ítem 2.6 del checklist de `docs/PLAN.md`. El reparto guarda/cortesía, las dos transacciones reales y
el SQL crudo con su entrada en la lista cerrada venían en el encargo del ítem; quién abre la
transacción del cierre lo decidió el usuario; las dos lagunas de la cláusula 4 y la del hueco al
anular las encontró la tanda de mutaciones, cuyo detalle está en el *Estado actual* del PLAN.
