---
tipo: referencia
stack: [dotnet, postgresql]
aplica_a: [ddd, ef-core, sql, migraciones, lopdgdd, sepa]
revisado: 2026-08-26
tags: [adr, esquema, multiempresa, direcciones, bloqueo, numeracion, migraciones]
---

# ADR-0007: Lo irreversible del esquema de Organización

- **Estado:** aceptado
- **Fecha:** 2026-08-26

## Contexto

El ítem 0.4 crea las cuatro primeras tablas del sistema: `empresas`, `ejercicios`, `series` y
`almacenes`. Son las primeras filas que va a tener la base de datos de un cliente, y todo lo que
venga después cuelga de ellas.

El plan maestro reparte el trabajo de esquema entre varios ítems —el filtro global multiempresa es
el 0.6, el tipo base de entidad es el 0.10— pero ese reparto es de **código**, no de **columnas**.
Una columna que no está el primer día no se añade después sin una migración sobre datos de
producción, y la regla de oro del §6 lo dice sin rodeos: *las cuatro reglas nuevas son decisiones de
esquema que salen gratis el primer día y cuestan una migración manual el segundo*.

De ahí el criterio con el que se ha trabajado, y que quedó acordado con el usuario antes de empezar:

> **Lo irreversible de esquema entra en el 0.4 para las entidades que el 0.4 crea.** El 0.10
> formaliza el tipo base y no migra nada.

Esto no adelanta fases: el 0.6 sigue teniendo que escribir el filtro global y demostrarlo con un
test, y el 0.10 sigue teniendo que sacar el tipo base. Lo que no puede pasar es que esas fases
lleguen y se encuentren con que hay que tocar tablas con datos dentro.

## Decisión

### 1. Direcciones en seis columnas, con las longitudes de ISO 20022 (R17)

`domicilio_fiscal_*` en `empresas` y `direccion_*` en `almacenes` son **seis columnas cada una**:
calle, número, código postal, población, subdivisión y país. Nunca una o dos líneas de texto libre.

Las longitudes no son redondeos cómodos, son las del `PostalAddress` de ISO 20022, que es el formato
que va a exigir el SEPA Credit Transfer Rulebook a partir del **15 de noviembre de 2026**:

| Campo | Columna | Longitud |
|---|---|---:|
| Calle | `…_calle` | 70 |
| Número | `…_numero` | 16 |
| Código postal | `…_codigo_postal` | 16 |
| Población | `…_poblacion` | 35 |
| Subdivisión | `…_subdivision` | 35 |
| País (ISO 3166-1 alfa-2) | `…_pais` | 2 |

Elegir 100 o `text` «por si acaso» parece más generoso y es peor: el día de la remesa habría que
descubrir cuáles de las direcciones ya guardadas no caben, y truncarlas a mano. El límite que
importa es el del sistema al que hay que entregar el dato.

La representación en una línea —la que se imprime en una factura— **es una función que compone**, no
una columna. Componer es reversible; partir texto libre no lo es.

### 2. `empresa_id` en las tres entidades transaccionales, desde la primera tabla (R8)

`ejercicios`, `series` y `almacenes` llevan `empresa_id` **no nulo** con clave ajena a `empresas`.
`empresas` no lo lleva: es la raíz, no un dato de otra.

El **filtro global de EF Core es del ítem 0.6**, y aquí no se ha escrito. Lo que se ha escrito es la
columna, la clave ajena y el índice que la acompaña. La razón es literal: añadir `empresa_id`
después obliga a tocar todas las tablas *y* a inventarse qué empresa era la dueña de cada fila
existente, que es una pregunta sin respuesta.

Consecuencia visible en 0.4: `series` tiene **dos** claves ajenas, `empresa_id` y `ejercicio_id`, y
las dos pueden ser válidas por separado apuntando a **contabilidades distintas**. Eso no lo para
ninguna restricción de la base, así que lo comprueba el caso de uso y devuelve un `400` del campo
`ejercicioId`.

### 3. `Bloqueado` es un estado propio, y lleva su fecha (R16)

`empresas.estado` y `almacenes.estado` admiten `Bloqueada`/`Bloqueado` como tercer valor, con su
columna `bloqueada_en` / `bloqueado_en`. `DELETE /api/v1/organizacion/empresas/{id}` responde `204`
y **la fila sigue ahí**, con su estado cambiado.

Por qué alcanza a `Empresa`, que a primera vista es una persona jurídica y no un dato personal: un
**empresario individual** tributa con su DNI y es persona física. Su razón social puede ser su
nombre y apellidos, y su domicilio fiscal, su casa. El artículo 32 de la LOPDGDD le alcanza igual
que a cualquier tercero, y ese artículo no dice «ocúltalo de los listados»: dice **identificar y
reservar**, impidiendo el tratamiento *incluida la visualización*, durante el plazo de prescripción,
y destruir después. Un `activo = false` que sigue apareciendo en un informe es justo lo que el
artículo prohíbe.

La fecha de bloqueo es la que hace calculable el plazo de prescripción. Sin ella, el proceso de
destrucción del R16 no se puede escribir nunca.

`Ejercicio` y `Serie` **no** tienen `Bloqueado`: tienen `Cerrado`, que es otra cosa —una regla
contable del R9, no una obligación de protección de datos— y no lleva fecha porque el cierre lo va
a registrar la auditoría.

Lo que el 0.4 **no** trae: `Desbloquear`, `Reabrir` y `Cerrar` existen en el dominio pero no tienen
puerta HTTP. Abrir esas puertas sin permisos (fase 1) sería publicar la operación con la que se
deshace un bloqueo legal.

### 4. Las fechas de negocio son `date`; los instantes, `timestamptz`

`ejercicios.fecha_de_inicio` y `fecha_de_fin` son `date`. El 1 de enero de 2026 es el 1 de enero en
Madrid y en Canarias; guardarlo como instante obliga a elegir una zona, y en UTC-1 el mismo valor se
lee como 31 de diciembre —el año contable equivocado—.

`bloqueada_en` y `bloqueado_en` sí son `timestamp with time zone`, porque un bloqueo **sí** es un
instante: pasó a una hora concreta y esa hora es la misma se mire desde donde se mire.

La regla, para no volver a discutirla: *si la respuesta a «¿cuándo?» es una casilla de calendario,
es `date`; si es un punto en la línea del tiempo, es `timestamptz`.*

### 5. El contador de una serie es una columna, jamás una secuencia de PostgreSQL

`series.contador` es un `bigint` de la fila, y se sube llamando a `Serie.RegistrarNumeroAsignado`.

Una `SEQUENCE` sería más cómoda y está terminantemente descartada: las secuencias **no son
transaccionales**. Un `nextval` consumido en una transacción que luego se deshace deja el número
gastado, y la numeración sale con huecos. Una numeración de facturas con huecos no es un defecto
estético: es un incumplimiento del artículo 6.1.a del RD 1619/2012, que exige series **correlativas**.
El mismo argumento vale para `IDENTITY` y para cualquier generador del servidor.

Que el contador viva en la fila también es lo que permite que el `409` de «esta serie ya ha numerado»
sea comprobable: `SePuedeSuprimir` es `Contador == 0`.

### 6. Cada módulo escribe su historial de migraciones en su propio esquema

El esquema es **`org`** (Anexo A.1), y el historial de migraciones va a
`org.__historial_de_migraciones`, dicho explícitamente en `OrganizacionDbContext.Configurar`.

Por omisión EF Core lo pondría en `public.__EFMigrationsHistory`, que es un sitio **compartido**. Con
un módulo funciona; con el segundo, el que migre después encuentra allí las migraciones del primero,
se cree al día y **no aplica las suyas**. No sale ningún error: sale un esquema incompleto en
producción.

Y se comprueba **mirando la tabla**, no la configuración. Un test de integración consulta
`information_schema` y verifica dónde ha quedado el historial de verdad.

### 7. Los enumerados se guardan como texto, no como ordinal

`estado`, `regimen_de_iva`, `tipo`, `tipo_de_documento`: todos `text`. Un ordinal es un contrato que
se rompe **solo con reordenar el enumerado en C#**, y quien lo reordena no ve que está rompiendo
nada: el código compila, los tests pasan y las filas viejas pasan a significar otra cosa.

El mismo criterio se aplica en el borde HTTP con `JsonStringEnumConverter`, por la misma razón y
frente al mismo tipo de cliente.

### 8. Ningún borrado en cascada

Las cuatro claves ajenas son `ON DELETE RESTRICT`. En un ERP, un borrado en cascada es la forma más
rápida de perder un histórico: borrar una empresa se llevaría por delante sus ejercicios, sus series
y con ellas la numeración fiscal. Que la base se niegue es la respuesta correcta.

## Consecuencias

- **El 0.6 y el 0.10 llegan a tablas que ya tienen lo que necesitan.** El 0.6 escribe el filtro
  global sobre una columna que ya existe; el 0.10 saca el tipo base a partir de propiedades que ya
  están. Ninguno de los dos migra datos.
- **El 0.4 es más caro de lo que su enunciado sugiere**, y eso es deliberado. El criterio de
  aceptación dice «CRUD con migraciones propias»; lo que se ha construido es ese CRUD más las ocho
  decisiones de arriba, que son las que no admiten segunda oportunidad.
- **Hay comprobaciones que la base de datos no puede hacer y quedan en el caso de uso**: la serie
  cruzada del punto 2, el almacén físico que exige dirección y el virtual que no, y la normalización
  de códigos antes de preguntar por duplicados. Cada una tiene su test de contrato por HTTP.
- **Falta lo que el plan reserva a otros ítems, y sigue faltando a propósito**: `xmin` como `ETag` y
  el juego `412`/`428` (0.9), la idempotencia (0.10 del R10), los permisos (fase 1) y el proceso de
  destrucción al vencer el plazo de prescripción (R16), que necesita un planificador que todavía no
  existe. Nada de eso obliga a tocar estas tablas después.
- **`Serie` y `Almacen` normalizan su código —recortado y en mayúsculas— antes de comprobar
  duplicados.** Sin eso, `«  central  »` habría pasado el filtro de duplicados y habría chocado
  contra el índice único: un `500` donde tocaba un `409`.

## Procedencia

Ítem 0.4 del checklist de `docs/PLAN.md`. El esquema está en
`db/migraciones/Organizacion/20260825233619_EsquemaInicialDeOrganizacion.cs` y las reglas que lo
justifican, en el §6 del plan maestro (R8, R9, R11, R16, R17) y en el §7.2 del modelo de dominio.

La decisión sobre **dónde vive el historial de migraciones** tiene además una segunda mitad —que las
migraciones estén *compiladas* en el ensamblado, o no existen para EF— que se descubrió con la CI en
rojo y está anotada en el propio `Bastion.Organizacion.Infrastructure.csproj`, donde muerde.
