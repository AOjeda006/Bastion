---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, ddd, testing]
revisado: 2026-08-26
tags: [adr, ef-core, change-tracker, guid-v7, agregados, 500]
---

# ADR-0010: Una entidad hija con clave propia no se da de alta sola

- **Estado:** aceptado
- **Fecha:** 2026-08-26

## Contexto

Dar de alta a alguien en una empresa es una línea de dominio:

```csharp
Membresia Conceder(Guid empresaId)
{
    var membresia = new Membresia(Id, empresaId);   // el constructor pone Id = Guid.CreateVersion7()
    _membresias.Add(membresia);
    return membresia;
}
```

El caso de uso lee el usuario, llama a `Conceder` y confirma. Los tests de dominio pasan, el
usuario queda con su pertenencia y no hay nada que revisar.

Contra PostgreSQL, **todas** las altas contestaban `500`.

## El mecanismo

Cuando el usuario se ha leído de la base, EF Core lo tiene en estado `Unchanged`. Al detectar
cambios encuentra en su colección una `Membresia` que no seguía, y para decidir qué es **mira si
tiene clave puesta**: si la tiene, concluye que la fila ya existe y la marca `Modified`; si no, la
marca `Added`.

La tiene. El constructor le pone un `Guid` v7 el primer día —a propósito: la identidad de una
entidad no depende de que se haya grabado—. Así que EF Core emite

```sql
UPDATE identidad.membresias SET empresa_id = …, usuario_id = … WHERE id = …
```

que no encuentra ninguna fila, y eso es un `DbUpdateConcurrencyException`. Al borde le llega una
excepción no controlada y sale por donde tiene que salir: `500`.

**Por qué no lo vio nadie antes.** El otro camino que crea pertenencias es la semilla de arranque,
y ahí el usuario es nuevo: el hijo hereda el `Added` del padre y el `INSERT` sale bien. Ese camino
se ejecuta en cada arranque de cada test, así que la impresión era que aquello funcionaba. Y el
hermano `RolDeMembresia` tampoco falla, porque su clave es **compuesta** y con esa EF Core acierta
solo. Un único tipo, por un único camino, y solo contra una base de datos de verdad.

## Decisión

**Quien crea una entidad hija con clave propia la apunta explícitamente en su repositorio.**

```csharp
usuarios.Registrar(usuario.Conceder(peticion.EmpresaId));
```

`Registrar` hace `contexto.Membresias.Add(membresia)`, que fuerza el `Added`. El dominio no se
toca: `Membresia` sigue teniendo su identidad desde el constructor, que es lo correcto.

Las alternativas se descartaron por lo que costaban:

- **No poner el `Id` en el constructor** y dejar que lo genere EF Core. Resuelve el síntoma y
  rompe otra cosa: `AsignarRol` construye `RolDeMembresia(Id, rolId)`, así que una pertenencia sin
  identidad hasta el `SaveChanges` deja roles colgando de `Guid.Empty` en cuanto alguien escriba
  el caso de uso que hace las dos cosas seguidas.
- **`ValueGeneratedNever()` en el mapeo.** No sirve: la regla de EF Core es «la clave está puesta»,
  y con generación desactivada está puesta siempre.
- **Clave compuesta `(usuario_id, empresa_id)`**, que es la identidad real de una pertenencia y
  haría desaparecer el problema por construcción (es lo que salva a `RolDeMembresia`). Es
  probablemente lo correcto a largo plazo, pero cambia el esquema, la migración y la clave ajena
  de `roles_de_membresia`: no es un arreglo, es un rediseño, y no se hace dentro del ítem que
  descubre la avería.

## Consecuencias

- Añadir un hijo a una colección de un agregado **no basta** para que se grabe. Cuando el hijo
  tenga clave propia asignada por el constructor, hay que apuntarlo.
- El caso de uso comprueba la idempotencia **antes** (`usuario.PerteneceA(...)`), porque apuntar
  como nueva una pertenencia que ya existía sería un `INSERT` contra su propio índice único.
- Queda fijado en `LasPertenenciasNuevasSeInsertanTests`, **sin base de datos**: el estado en que
  EF Core deja la entidad se decide antes de abrir ninguna conexión. Uno de sus cuatro casos es un
  canario que afirma el comportamiento actual de EF Core; el día que cambie, se pondrá rojo y el
  rodeo podrá desaparecer.
- La lección general, que es la que vale para el resto del ERP: **preguntar por el estado del
  `ChangeTracker` es un test rápido**. La avería parecía necesitar PostgreSQL y no lo necesitaba;
  necesitaba preguntar lo que había que preguntar.
