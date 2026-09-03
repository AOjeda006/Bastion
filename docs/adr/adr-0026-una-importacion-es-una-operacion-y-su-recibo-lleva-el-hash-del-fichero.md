---
tipo: referencia
stack: [csharp, dotnet, aspnetcore]
aplica_a: [api-rest, manejo-errores, idempotencia]
tags: [adr, r10, idempotencia, importacion, csv, manejo-errores, aislamiento]
revisado: 2026-09-03
---

# ADR-0026: Una importación es una operación, su unidad de aislamiento es la fila, y su recibo lleva el hash del fichero

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** aplica R10 y el **ADR-0014** (la clave del cliente y la versión del recurso son
  dos mecanismos). Aplica `principios/manejo-errores.md`. Toca la nota abierta sobre el crecimiento
  de `auditoria.claves_de_idempotencia`. Se implementa en el **ítem 1.11**.

## Contexto

El §15 pide **importación CSV** en la fase 1. Un CSV real de un cliente que migra trae miles de
filas y un puñado de ellas malas: un NIF con la letra cambiada, una unidad que no existe, un código
repetido. Hay que decidir dos cosas que se parecen y no lo son.

`principios/manejo-errores.md` decide la primera casi entera: **en un bucle que procesa muchos
elementos, el fallo de uno no puede ser el fallo de la vuelta**, y hay que **escribir cuál es la
unidad de aislamiento**. La segunda —la idempotencia— no la decide nadie, y R10 obliga a
contestarla: si el mismo fichero llega dos veces, ¿reimporta, no hace nada, o devuelve el resultado
guardado?

## Decisión

### 1. La unidad de aislamiento es la **fila**

Un fichero con filas malas **importa las buenas** y devuelve un informe. Cada fila se procesa en su
propia unidad de trabajo; el fallo de una no arrastra a las demás ni a la operación.

El informe dice, por cada fila rechazada, **el número de línea, el motivo con nombre y el valor que
lo provocó**. No es cosmética: es lo único que permite corregir el fichero y reimportar solo esas.
Un «no» sin diagnóstico sobre tres mil filas es, en la práctica, un producto que no sirve para
migrar datos — y migrar datos es para lo que existe la importación.

### 2. Una importación es **una** operación idempotente

Con `Idempotency-Key`, el reenvío del mismo fichero **devuelve el resultado guardado sin volver a
importar**. Es la misma máquina que ya protege las quince escrituras del sistema (ADR-0014), sin
inventar nada.

### 3. El recibo guarda el **hash del contenido**, y esa es la parte que hace segura a la 2

Junto al resultado se guarda una huella del fichero importado. **Misma clave con fichero distinto es
un error explícito**, no el informe del fichero anterior devuelto con cara de acierto.

Sin este cerrojo, la decisión 2 tiene un modo de fallo silencioso y realista: un cliente que
reutiliza la clave —porque su script la deriva del día, o del nombre del fichero— recibe el informe
de otra importación y cree que la suya entró. Con él, recibe un error que dice exactamente qué pasa.

### 4. La reimportación de las filas corregidas es una operación **nueva**

Fichero distinto → hash distinto → clave nueva. Es coherente y **se dice en voz alta**, para que
nadie lo lea como una fuga de la idempotencia: lo que la clave protege es «esta petición ya la
hice», y un fichero corregido no es la misma petición.

## Alternativas descartadas

**Todo o nada (el fichero como unidad de aislamiento).** Contradice `manejo-errores.md` y convierte
un CSV de tres mil filas en un rechazo sin diagnóstico. Se descarta por el producto, no por la
teoría.

**Lotes de N filas.** Coge lo malo de los dos: sigue arrastrando filas buenas al fallo de una mala, y
además el informe deja de poder señalar la línea exacta con precisión. La única virtud sería el
rendimiento, y el rendimiento aquí no es el problema.

**N operaciones idempotentes, una por fila con clave derivada.** Es la alternativa seria, y se
descarta con un número: multiplicaría por tres mil las filas de
`auditoria.claves_de_idempotencia` por cada importación — una tabla que **ya crece sin política de
retención y está anotada como riesgo abierto** precisamente porque borrar de ella reabre la ventana
que cierra. Empeorar en tres órdenes de magnitud un problema abierto para ganar un grano más fino
del que nadie ha pedido no sale a cuenta.

**No ser idempotente (reimportar y actualizar lo que exista).** Convierte cada reintento de red en
una segunda pasada sobre datos maestros. R10 existe para esto.

## Consecuencias

- El caso de uso de importación devuelve un **informe**, no un `201`: la respuesta útil es qué entró
  y qué no.
- El recibo de idempotencia de una importación es algo mayor que los actuales, porque guarda el
  informe. Sigue sin llevar **contenido de negocio de las filas**: número de línea, código de motivo
  y recuentos, nada de datos personales (ADR-0014 §5, y `proteccion-datos.md`: nunca identificadores
  personales en trazas ni mensajes).
- La huella del fichero se calcula sobre los **bytes recibidos**, no sobre el contenido interpretado:
  es una comprobación de identidad de la petición, no de equivalencia semántica.
- El patrón vale para cualquier importación futura (artículos, tarifas, y las de fases posteriores).
  Se decide una vez.
