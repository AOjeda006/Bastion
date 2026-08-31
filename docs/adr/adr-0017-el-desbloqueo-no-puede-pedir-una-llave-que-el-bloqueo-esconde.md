---
tipo: referencia
stack: [dotnet, aspnetcore, http]
aplica_a: [api-rest, concurrencia, seguridad]
tags: [adr, r11, r16, if-match, etag, idempotencia, lopdgdd]
revisado: 2026-08-31
---

# ADR-0017: El desbloqueo no puede pedir una llave que el bloqueo esconde

- **Estado:** aceptado
- **Fecha:** 2026-08-31
- **Relacionado:** consecuencia directa del
  [ADR-0016](adr-0016-el-bloqueo-es-uno-y-tapa-a-las-tres.md) (decisión 2). Matiza, sin sustituirlo,
  el [ADR-0014](adr-0014-la-clave-del-cliente-y-la-version-del-recurso-son-dos-mecanismos.md).

## Contexto

R11 exige que **toda escritura sobre un recurso existente cite la versión que cree estar pisando**,
en la cabecera `If-Match`. Los tres `POST .../desbloqueo` lo hacían desde el 0.9, y el barrido
`TodaEscrituraDiceComoSeProtegeTests` lo comprobaba junto con las otras quince operaciones.

El ADR-0016 hizo que una fila bloqueada dejara de salir por los caminos ordinarios. Y la etiqueta
que `If-Match` exige **se obtiene leyendo el recurso**: el cliente hace `GET`, se queda con el
`ETag` de la respuesta y lo devuelve en la escritura. Ese es el ida y vuelta que el proyecto
ejercita a propósito, sin fabricar etiquetas.

El rojo apareció en el sitio exacto, en dos tests de contrato, dentro del ayudante que lee la
etiqueta:

```
Versiones.EtiquetaDeAsync
  lectura.StatusCode should be HttpStatusCode.OK but was HttpStatusCode.NotFound
```

Un recurso bloqueado ya no emite `ETag`, porque no se deja leer. **La precondición pedía una llave
que el propio mecanismo esconde.**

## Alternativas descartadas

**Devolver el `ETag` en la respuesta del `DELETE`.** Funcionaría justo después de bloquear, y solo
ahí: el cliente que quiera desbloquear mañana, o desde otra sesión, o tras reiniciar, no tiene de
dónde sacarla. Es optimizar para el test que se estaba mirando.

**Abrir un camino de lectura declarado para lo bloqueado, solo para emitir la etiqueta.** Es
exactamente el agujero que el ADR-0016 acaba de cerrar: la primera excepción al filtro, y la más
difícil de argumentar de todas, porque su motivo sería «para que funcione una cabecera».

**Aflojar el barrido** para que el desbloqueo no tenga que decir cómo se protege. Descartada por lo
mismo que en el ADR-0015: una excepción sin argumento es la puerta por la que entra la siguiente.

## Decisión

**Los tres `POST .../desbloqueo` —empresa, almacén y usuario— dejan de exigir `If-Match`, y quedan
en la lista de acciones exentas del barrido, cada una con su motivo escrito.**

Dos argumentos, y el segundo es el que de verdad sostiene la decisión.

### 1. Una precondición cuya llave no se puede obtener no es una precondición: es un muro

`If-Match` existe para que el cliente diga sobre qué versión escribe. Si no hay ninguna manera
legítima de conocerla, la cabecera no protege de nada: solo hace que la operación sea imposible o
que el cliente se invente un valor. Un `If-Match: *` la dejaría pasar siempre, que es peor que no
tenerla, porque parece que protege.

### 2. No hay nada que perder, porque no hay con quién competir

`If-Match` protege de que dos escrituras se pisen. Mientras el recurso está bloqueado, **ninguna
otra escritura llega a la fila**: todos los casos de uso la piden al repositorio y el filtro no se
la da. La única operación que puede tocar una fila bloqueada es el propio desbloqueo, y desbloquear
dos veces deja el mismo estado. La ventana que `If-Match` cerraría está cerrada por construcción.

### 3. Lo que NO se pierde: el testigo sigue comprobándose

Desaparece la precondición **que el cliente cita**, no el control de concurrencia. El desbloqueo
sigue leyendo la fila y guardándola en la misma transacción, con su `xmin` en el `WHERE` del
`UPDATE`: si alguien la tocara entremedias, el guardado falla. Lo que se retira es la cabecera, no
la protección.

## Consecuencias

- Las tres firmas pierden el parámetro de versión: `EjecutarAsync(Guid id, CancellationToken)`. No
  es que no se pida, es que **no se puede pedir**, y la firma lo dice.
- Los tres endpoints declaran `204` y `404`, y ya no `412` ni `428`.
- El inventario del barrido se mueve entero y en el mismo cambio: **16 → 13** operaciones que
  exigen `If-Match`, **10 → 13** exentas con motivo. El total de acciones que cambian estado no se
  mueve (32), y eso es lo que dice que fue una mudanza y no una acción nueva colada sin protección.
- **Estas son las únicas escrituras del sistema sin `If-Match` sobre un recurso existente.** El día
  que aparezca una cuarta, este ADR es el sitio donde argumentar por qué, y el barrido obliga a
  pasar por aquí.
- El ADR-0014 sigue entero: `Idempotency-Key` y `If-Match` siguen siendo dos mecanismos distintos y
  ninguna acción pide los dos. Lo que este ADR añade es que hay un tercer caso —la acción
  naturalmente idempotente cuya llave no existe— y que se declara, no se supone.
