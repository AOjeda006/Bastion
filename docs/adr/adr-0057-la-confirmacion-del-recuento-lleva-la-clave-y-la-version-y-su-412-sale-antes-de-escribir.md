---
tipo: referencia
stack: [dotnet, efcore, postgresql, aspnetcore]
aplica_a: [api-rest, concurrencia, idempotencia, inventario]
tags: [adr, recuento, idempotencia, if-match, etag, concurrencia, ejercicio, numeracion, adr-0014, adr-0041, adr-0055]
revisado: 2026-10-07
---

# ADR-0057: La confirmación del recuento lleva la clave y la versión, y su `412` sale antes de escribir

- **Estado:** aceptado
- **Fecha:** 2026-10-07
- **Sale del 2.12**, del paso que confirma el recuento.
- **Enmienda el ADR-0014 §4**, en su último párrafo, y la línea del §9 que lo repite: ninguna acción
  pedía los dos mecanismos a la vez, y desde aquí los pide la que contesta a los dos motivos.
- **Desarrolla el ADR-0055 §3 y §4**, que pusieron la confirmación bajo los dos mecanismos y la
  versión antes que el estado, sin decir cómo se contestaba a los motivos del ADR-0014. No cambia
  nada de lo que decidió.

## Contexto

El ADR-0014 §4 dejó escrita una prohibición con dos motivos. Una acción no podía pedir a la vez
`If-Match` e `Idempotency-Key`:

1. **La repetición devolvería el `ETag` de entonces.** El filtro de idempotencia guarda la respuesta
   y la devuelve tal cual, y la versión que llevaba ya no sería la de la fila.
2. **Un choque de concurrencia saldría como un `500`.** La transacción de la idempotencia va sin
   puntos de guardado, así que un `SaveChanges` que choca contra el testigo la deja abortada. El
   manejador del `412` consulta entonces la versión actual para ponerla en la respuesta, y esa
   consulta fallaría. El cliente recibiría un `500` donde tocaba un `412`, y lo reintentaría.

El barrido funcional lo mantenía: `Ninguna_accion_pide_los_dos_mecanismos_a_la_vez` exigía la lista
vacía. El ADR-0041 §6 se apoyó en él para que el cierre del ejercicio abriera su transacción con la
unidad de trabajo y no con el filtro.

La confirmación del recuento necesita los dos, y el ADR-0055 lo dejó dicho:

- **la clave**, porque toma dos correlativos, el del recuento y el de su ajuste, y los dos tienen
  que quedar escritos en la transacción del documento (R5). Esa transacción la abre el filtro;
- **la versión**, porque lo que se confirma es un papel que varias personas escriben a la vez. Un
  conteo de otra persona deja el recuento en curso y con otra cifra. Ni la máquina de estados ni el
  testigo bastan: confirmar sin haber visto ese conteo daría por buena una diferencia que nadie ha
  mirado.

## Decisión

### 1. La confirmación contesta a los dos motivos, y por eso puede pedir los dos mecanismos

- **Al primero, respondiendo sin `ETag`.** La versión nueva se lee con la ficha, como la de una
  línea tras contarla. La respuesta guardada tampoco lo lleva, así que la repetición no puede citar
  una versión vieja.
- **Al segundo, devolviendo el `412` como un resultado, antes de escribir nada.** El caso de uso
  bloquea la fila del recuento (`FOR NO KEY UPDATE`), la lee y compara su versión con la del
  `If-Match`. Nadie puede cambiar esa fila entre la comparación y el `COMMIT`, así que el `UPDATE` de
  la cabecera no choca nunca contra el testigo, y la transacción no llega a abortarse.

**La regla funcional pasa de prohibir a exigir un motivo.** Se llama ahora
`Solo_piden_los_dos_mecanismos_las_acciones_que_dicen_por_que`:

- `s_conLosDos` nombra cada acción con los dos mecanismos y su motivo;
- la lista se compara entera en los dos sentidos con las acciones que los piden;
- y no puede salir vacía: su contraejemplo es la confirmación.

La partición de `El_barrido_encuentra_el_inventario_entero` resta las de `s_conLosDos`, porque esas
caen en dos cajones a la vez.

**Lo afirman cuatro casos de integración** (`LaConfirmacionDelRecuentoTests` y
`LasCarrerasDeLaConfirmacionDelRecuentoTests`):

- la respuesta y su repetición, sin `ETag` y con el mismo cuerpo, y sin gastar otro número;
- el `If-Match` de antes de contar, que da `412` y no escribe nada;
- dos confirmaciones seguidas con la misma versión: la segunda es un `412`;
- dos confirmaciones a la vez: la segunda espera en la cabecera y sale con `412`, no con `500`.

**El ADR-0041 §6 no cambia.** El cierre del ejercicio no contesta a los dos motivos: su `412` sale
del testigo, al guardar. Sigue sin la clave y con la transacción de la unidad de trabajo.

### 2. Ese `412` no lleva `versionActual`

Sale de `ErroresDeConcurrencia.Obsoleta(actual)`, cuyo mensaje nombra la versión de ahora. Pero no
pasa por el manejador del testigo, que es quien pone la extensión `versionActual`. Es la misma
diferencia que el `412` de `ManejadorDeCarreraPerdidaEnLaBase`, que tampoco la lleva: el código es el
mismo (`version-obsoleta`), y lo que el cliente tiene que hacer también, releer la ficha.

### 3. La versión va antes que el estado

Quien confirma dos veces con la misma versión recibe el `412`, que es su causa. Mirar antes el
estado le daría un `409` `recuento-no-esta-en-curso` por algo que su `If-Match` ya decía. Con la
versión de ahora, sí es ese `409`: ha visto el recuento confirmado y aun así lo confirma.

`ElRecuentoEnCurso.BloquearYLeerEnSuVersionAsync` bloquea, lee y compara la versión sin mirar el
estado. El estado lo mira cada acción, porque cada una pide el suyo. Las escrituras en las líneas
siguen con `BloquearYLeerAsync`, que mira el estado y no la versión de la cabecera, porque traen la
de la línea.

### 4. `ix_ajustes_recuento_id` no se declara como carrera perdida

El índice impide un segundo ajuste del mismo recuento. No entra en
`IndicesQueDelatanUnaCarreraPerdida`, porque una violación suya no puede ser una carrera legítima:

- el único camino que escribe `ajustes.recuento_id` es la confirmación;
- la confirmación lo hace con la fila del recuento bloqueada y después de comprobar que sigue en
  curso.

Dos confirmaciones del mismo recuento se ponen en fila en la cabecera, y la segunda sale con `412`
mucho antes de llegar al índice. Un `23505` ahí querría decir que un camino escribió sin el cerrojo,
y eso es un defecto. El `500` es la respuesta honrada.

### 5. El cambio de año: la serie se elige en el alta, y la fecha la pone la confirmación

El ADR-0055 §1.3 pidió las dos series en el alta, validadas al abrir y otra vez al confirmar. Al
confirmar, la guarda que decide es la del numerador: la fecha de hoy tiene que caer en el ejercicio
de la serie.

**Así que un recuento abierto con las series de este año no se confirma el 2 de enero.** El
numerador contesta `fecha-fuera-del-ejercicio-de-la-serie`, y no se gasta nada. El recuento que se
abre el 31 de diciembre para confirmarse en enero se abre con las series del año nuevo. Esas series
existen en cuanto existe su ejercicio, y el alta no compara fechas: solo mira que la serie sea del
tipo y esté abierta. Lo afirma
`Al_cambiar_de_anio_confirma_el_abierto_con_las_series_del_nuevo_y_no_el_de_las_del_viejo`, con el
reloj del módulo en el 2 de enero del año siguiente.

**Queda abierto, con su disparador, cambiar las series de un recuento en curso.** Hoy no se hace. Un
recuento abierto con las series equivocadas solo sale descartándolo, y eso pierde lo contado. El
disparador es el primer recuento que haya que descartar solo por eso.

### 6. Un recuento sin líneas se confirma

Un almacén vacío también se cuenta. Su recuento se confirma con su número, sin ajuste, y la serie
del ajuste ni se consulta. Prohibirlo dejaría sin documento la afirmación «aquí no había nada», que
es justo la que un auditor pide.

### 7. El cierre del ejercicio pregunta por el recuento confirmado

`LosDocumentosDeInventarioEnUnPeriodo.HayDocumentosEnAsync` cuenta el recuento por su fecha de
confirmación, en cualquier estado. Uno en curso o descartado la tiene nula, y no cae en ningún
intervalo. `HayBorradoresEnAsync` no lo mira: en curso no es un borrador del ejercicio en el que se
abrió (ADR-0055 §1.5).

No basta con preguntar por los ajustes, porque un recuento confirmado sin diferencias no tiene
ajuste que hable por él. Lo afirma
`Un_recuento_vacio_se_confirma_con_su_numero_y_cuenta_para_su_ejercicio_por_la_fecha_de_confirmacion`.
Ahí el recuento es el único documento de la empresa:

- en curso, el ejercicio se mueve;
- confirmado, el mismo movimiento es un `409` `ejercicio-dejaria-documentos-fuera`.

### 8. La misma huella pide el mismo ajuste, y si no, se denuncia

El número del ajuste se toma en el paso 9, antes de las valoraciones, y solo si el teórico sin
cerrojo da diferencias. El que decide es el del paso 11. Si los dos tienen la misma huella, son el
mismo teórico y piden el mismo ajuste, así que el número tomado es justo el que hace falta. Si uno
pide ajuste y el otro no con la misma huella, lo roto es la huella: el caso de uso lanza, y no lo
contesta como un error de negocio.

## Consecuencias

- **La primera acción de la casa con los dos mecanismos** es `POST …/recuentos/{id}/confirmacion`.
  La anulación y el descarte del recuento llegan en el mismo ítem, entran en `s_conLosDos` con su
  motivo y contestan igual: sin `ETag` y con el `412` antes de escribir.
- **Una acción nueva con los dos mecanismos** que no conteste a los dos motivos pone rojo el carril
  funcional: no está en `s_conLosDos`, y para entrar tiene que escribir por qué.
- **El ADR-0014 §4** lleva su nota de enmienda, y el §9 también.
- **Lo que queda abierto, con su disparador:** cambiar las series de un recuento en curso, el
  primer recuento que haya que descartar solo por haberlo abierto con las del año anterior.

## Procedencia

Lo decidió el agente al escribir la confirmación, dentro de lo que fijaron el encargo del 2026-10-05
y la puerta del 2026-10-06 (ADR-0055 §1). La regla del ADR-0014 no admitía la combinación, así que
había que contestar a sus dos motivos antes de cambiarla.
