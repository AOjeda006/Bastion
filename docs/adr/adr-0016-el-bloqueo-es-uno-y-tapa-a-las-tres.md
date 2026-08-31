---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [dominio, persistencia, testing]
tags: [adr, r14, r16, r17, lopdgdd, bloqueo, filtro-global, tipo-complejo, ef-core]
revisado: 2026-08-31
---

# ADR-0016: El bloqueo es uno, tapa a las tres, y la dirección deja de fingir identidad

- **Estado:** aceptado
- **Fecha:** 2026-08-31
- **Relacionado:** amplía el
  [ADR-0011](adr-0011-el-filtro-global-de-empresa-y-los-caminos-que-lo-rodean.md), que estableció el
  filtro global de empresa y la doctrina de la apertura declarada. Aquí se aplica la misma forma a
  un segundo filtro.

## Contexto

Hasta el 0.10 el mismo concepto estaba escrito tres veces: `EstadoDeEmpresa`, `EstadoDeAlmacen` y
`EstadoDeUsuario`. Tres enumerados de dos valores, tres `BloqueadoEn` sueltos al lado, tres
`Bloquear`/`Desbloquear` copiados — y **ninguno con motivo**. Tres copias de una regla legal son
tres sitios donde la regla puede divergir; que no hubieran divergido todavía era cuestión de que
nadie las había tocado por separado.

Y ninguna de las tres tapaba nada: una empresa «bloqueada» seguía saliendo en las consultas, con
sus datos a la vista. El artículo 32 de la LOPDGDD no dice eso. Dice que los datos se **reservan**,
impidiendo su tratamiento —y menciona la visualización expresamente— salvo para ponerlos a
disposición de jueces, Fiscalía y Administraciones competentes durante el plazo de prescripción.
Un registro que se lista, se lee y se exporta como cualquier otro no está reservado: está
etiquetado.

## Decisión

### 1. Un solo `Bloqueo`, objeto de valor, con las tres piezas juntas

`Bloqueo` vive en `BuildingBlocks/Domain/Bloqueos` y lleva **si está bloqueado, desde cuándo y por
qué**. Las tres juntas y no un enumerado con dos columnas sueltas al lado: con un enumerado, nada
obliga a que la fecha esté puesta cuando el estado lo dice ni vacía cuando no, y de esa fecha
cuelga el plazo de prescripción. Aquí las combinaciones imposibles no se pueden construir.

El motivo es una **lista cerrada** (`SupresionSolicitada`, `CeseDeUso`) y no un texto libre. Es lo
que permite contestar años después por qué esos datos siguen guardados, y las dos razones no
caducan igual: la supresión del art. 32 conserva durante el plazo de prescripción; el cese de uso
de un almacén conserva mientras exista su histórico de valoración.

**La transición es comportamiento, no asignación.** `Bloquear(motivo, momento)` y `Desbloquear()`
llevan dentro las dos respuestas incómodas, escritas y probadas:

- **Bloquear lo ya bloqueado devuelve el bloqueo de antes, entero.** No mueve la fecha —moverla
  alargaría la conservación de datos personales sin que nadie lo hubiera decidido— ni pisa el
  motivo. Y hace que el `DELETE` de la API sea idempotente, que es lo que exige el verbo.
- **Desbloquear lo que no está bloqueado no es un error:** devuelve lo mismo. Lanzar obligaría a
  todo el que llama a preguntar antes, y la pregunta y la acción no son atómicas.

No hay *setters* públicos. Abrirlos deja pasar exactamente esto: reasignar la fecha a mano en un
segundo bloqueo, treinta días de conservación de más y ningún síntoma.

### 2. El filtro tapa a las tres. Sin lista de excepciones.

`Empresa`, `Usuario` y `Almacen` llevan un filtro global llamado `"Bloqueo"`, hermano del
`"Inquilinato"` del ADR-0011. Una fila bloqueada **no sale por ningún camino ordinario**: ni en la
lista, ni por identificador, ni al modificar.

**Las tres, sin excepciones**, y en particular el almacén. El motivo de bloquear un almacén no es
el art. 32 —un almacén no es una persona— sino no romper el histórico de valoración; pero el
mecanismo es el mismo y no lleva excepciones a propósito. Una excepción «solo para el almacén, que
no es un dato personal» sería el primer sitio donde mirar para saber si el filtro tapa de verdad, y
la segunda excepción llegaría con menos discusión que la primera. Cuando la fase 3 necesite leer el
almacén de un movimiento histórico, abrirá un ámbito declarado, que es el mecanismo previsto.

**La consecuencia visible es que `GET` y `PUT` sobre lo bloqueado contestan 404**, y no un 409
«está bloqueada». No es una simplificación: un 409 explicando el bloqueo revela a la vez que el
registro existe y en qué estado está, que es precisamente el tratamiento —la visualización— que el
bloqueo impide. Los códigos de error `empresa-bloqueada` y `almacen-bloqueado` se han borrado del
catálogo en vez de quedarse sin quien los emita.

### 3. Ver lo bloqueado es una apertura declarada, enumerada y con motivo

Igual que `SinInquilino`: `IAccesoALoBloqueado.ViendoLoBloqueado(MotivoParaVerLoBloqueado)` abre un
ámbito `AsyncLocal`, deja rastro en el registro y **el sitio donde se abre está en una lista
comparada entera** (`El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado`). Hoy la lista
son los tres desbloqueos y nada más, por una razón de lógica: para levantar un bloqueo hay que poder
leer lo que está bloqueado.

`.IgnoreQueryFilters(` sigue prohibido, y ahora con un argumento más fuerte: **apaga los dos
filtros**. Quien lo escribiera para ver una fila bloqueada abriría de paso el de empresa sin
enterarse.

### 4. Lo que el bloqueo de R16 **no** es

`Usuario` tiene dos bloqueos y siguen separados: el `Bloqueo` del art. 32 —lo decide una persona
con permiso, lleva fecha y motivo, no caduca solo, y saca la fila de todas las consultas— y
`RechazadoHasta`, el rechazo temporal por intentos fallidos, que se levanta solo a los pocos
minutos y no oculta nada. Fundirlos haría que fallar la contraseña cinco veces diera de baja la
cuenta para siempre.

### 5. `EntidadBase`: las dos marcas de tiempo, y de dónde sale cada hora

Toda entidad que es un recurso por sí misma lleva `CreadoEn` y `ModificadoEn`, las dos
`DateTimeOffset` sobre `timestamptz` porque son **instantes** y no fechas de calendario (R14).

- `CreadoEn` **la pone el dominio**, en la fábrica de cada entidad. Se pone una vez y en un solo
  sitio, así que el dominio la puede sostener; y sosteniéndola, la entidad nace completa incluso en
  una prueba unitaria que nunca ve una base de datos.
- `ModificadoEn` **la pone un interceptor** al guardar. Cambia en todos los métodos que tocan algo,
  presentes y futuros: sostenerla a mano significaría que el día que alguien escriba un método
  nuevo y no se acuerde, la marca deja de moverse **sin que nada falle**.

**Ninguna de las dos es un `DEFAULT now()`**, y esto no es estética. Ataría las columnas al reloj
del servidor de base de datos —el único que una prueba no puede adelantar— y metería una forma
nueva de valor generado por el servidor en un modelo donde lo único que lo genera son los seis
testigos de concurrencia del [ADR-0015](adr-0015-lo-unico-que-genera-el-servidor-son-los-testigos-de-concurrencia.md).
La hora sale del `TimeProvider` inyectado, y que salga de ahí está comprobado con un reloj parado en
2019: un instante que `now()` no puede devolver.

`EntidadBase` **no aporta identidad ni bloqueo**. La identidad la declara cada entidad, como antes,
porque no era lo que estaba escrito tres veces. Y bloquearse no le pasa a todo el mundo —un
ejercicio se cierra, no se bloquea—, así que eso es `IBloqueable` y no un miembro heredado que la
mitad de las entidades tendrían que ignorar.

### 6. `Direccion` deja de fingir que tiene identidad

Pasa de **tipo poseído** a **tipo complejo**. Un objeto de valor no tiene identidad, y un tipo
poseído sí: EF Core le sintetiza una clave, lo sigue como una entidad más y lo saca en
`GetEntityTypes()`. El mapeo decía de la dirección algo que el dominio niega.

El cambio resultó **neutro para el esquema**: las mismas seis columnas, los mismos topes, ninguna
migración pendiente en los tres módulos. Lo único que estaba en juego era decir la verdad sobre el
modelo… y una cosa más, que es lo que de verdad merece estar escrito aquí.

## El hallazgo: los barridos no cubrían lo que parecía

Antes de tocar el mapeo se midió la mutación: **`Direccion` como tipo complejo con los barridos sin
ampliar**. El resultado:

| Qué | Poseída | Compleja |
|---|---:|---:|
| Propiedades escalares en el modelo | 152 | **138** |
| Tipos de entidad | 20 | 18 |
| Tipos poseídos | 2 | 0 |
| **Casos de barrido de modelo en rojo** | — | **0 de 14** |

**Doce propiedades se fueron de la clasificación de auditoría y los catorce barridos siguieron en
verde.** La causa es una sola línea de EF Core: las propiedades de un tipo complejo **no salen** en
`IEntityType.GetProperties()` ni en `EntityEntry.Properties`. Todo barrido escrito sobre esas dos
APIs deja de mirar lo que hay dentro de un tipo complejo, y no avisa: devuelve menos y da verde.

El único rojo fue de comportamiento y en la suite de integración
(`La_direccion_de_un_almacen_viaja_DENTRO_de_la_traza_de_su_dueno`), porque el interceptor de
auditoría recorría `entrada.Properties`. Es decir: **el mecanismo que iba a avisar no avisó, y lo
que salvó el cambio fue un test de efecto, escrito para otra cosa.**

De ahí el orden que se siguió, y que es la parte reutilizable de este ADR:

1. **Primero** se amplían los barridos y el interceptor, con `PropiedadesConCamino()` —un recorrido
   recursivo de `GetComplexProperties()` que devuelve el camino con puntos, `Empresa.DomicilioFiscal.Calle`—.
2. **Se comprueba que la ampliación se pone roja** con el mapeo todavía poseído (`should be
   "Almacen.Direccion: 6, Empresa.DomicilioFiscal: 6" but was ""`). Un barrido nuevo que nace verde
   no ha demostrado que mire.
3. **Después** se cambia el mapeo.

Al revés, el cambio habría entrado en verde con doce propiedades fuera de la clasificación.

## Consecuencias

- Los tres enumerados de estado, sus columnas y sus códigos de error desaparecen. La migración de
  cada módulo **deriva** el bloqueo de la columna vieja antes de tirarla, y el `Down` lo rehace:
  deshacer una migración no puede ser una forma de desbloquear en silencio.
- Las filas anteriores al 0.10 estrenan `creado_en` con el instante en que corrió la migración, que
  es la cota superior más ajustada que se puede afirmar. Todas comparten el mismo valor al
  milisegundo, y esa coincidencia es la señal de que el dato está derivado y no observado.
- Los tres `POST /desbloqueo` pierden el `If-Match`. Eso es consecuencia de la decisión 2 y tiene su
  propio ADR: el [ADR-0017](adr-0017-el-desbloqueo-no-puede-pedir-una-llave-que-el-bloqueo-esconde.md).
- Cualquier barrido nuevo sobre el modelo se escribe con `PropiedadesConCamino()` y no con
  `GetProperties()`. El de fechas (`LasFechasDicenDeQueTipoSonTests`) ya nace así, y por eso ve
  `Bloqueo.Desde`.
