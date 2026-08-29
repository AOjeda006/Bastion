---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, seguridad, auditoria, multiempresa, testing]
revisado: 2026-08-27
tags: [adr, r3, auditoria, append-only, interceptor, ef-core, transaccion, triggers]
---

# ADR-0012: La traza va en la misma transacción que el cambio, y la tabla no se puede tocar

- **Estado:** aceptado
- **Fecha:** 2026-08-27

## Contexto

El criterio del ítem 0.7 es corto: *«tabla append-only de quién cambió qué; un cambio en un maestro
deja su rastro»*. Lo que decide no lo es. Esta es la **primera tabla de solo añadido del sistema**,
y los libros de la R3 —asientos, movimientos, la cadena de la R15— van a copiar la forma que se
fije aquí. Lo que se decida mal se decide para todos ellos.

Hay además una asimetría que conviene nombrar antes de empezar: una auditoría rota **no da error**.
Sigue habiendo una tabla, sigue llenándose, y el día que alguien la consulte para responder «quién
cambió este NIF» va a obtener una respuesta. Que sea la verdadera no lo garantiza que exista.

## 1. Atomicidad: la traza entra en el mismo `SaveChanges` que el cambio

**Decisión.** Un `SaveChangesInterceptor` recorre las entradas pendientes en `SavingChanges` y
**añade las filas de traza al mismo `DbContext`**, antes de que EF Core envíe nada. Van, por tanto,
en el mismo `SaveChanges` —y en la misma transacción— que el cambio que las produjo: o entran las
dos cosas o no entra ninguna.

**Las dos rutas descartadas, escritas descartadas:**

- **Escribir la traza después de que `SaveChanges` haya ido bien.** Deja el cambio confirmado y su
  traza pendiente de un segundo viaje que puede no ocurrir: un proceso que muere, una conexión que
  se cae, un error de red. El resultado es una tabla que dice «esto no pasó» sobre algo que pasó.
- **Escribirla desde un contexto aparte con su propia transacción.** Falla en la otra dirección:
  la traza se confirma por su cuenta y el cambio se revierte, y queda el rastro de un alta que
  nunca existió.

«Mejor esfuerzo» no es una propiedad que esta tabla pueda tener. No hay una tercera opción con las
ventajas de las dos: lo que hace atómico un par de escrituras es que las escriba la misma
transacción, y eso obliga a que las escriba el mismo `DbContext`.

**Cómo se prueba, sin creerse nada.** Un guardado que revienta a medias no deja ni la fila ni su
traza, y uno que va bien deja las dos. Pero esos dos casos los pasa **también** la primera ruta
descartada —si la traza se escribe después de que el guardado fuera bien, un guardado que revienta
tampoco deja traza—, así que hay un tercer test que no se puede fingir: PostgreSQL guarda en cada
fila, en la columna de sistema `xmin`, el número de la transacción que la insertó. **El `xmin` de la
fila y el de su traza tienen que ser el mismo número.** No hace falta instrumentar nada: lo cuenta
la propia base.

**Consecuencia de frontera, y es la que hace viable la ruta.** El tipo `RegistroDeAuditoria` no
puede vivir en el `Domain` de un módulo: el bloque común no puede referenciar a un módulo, y un
módulo no puede referenciar la `Infrastructure` de otro (§4). Vive en
**`BuildingBlocks/Infrastructure/Auditoria`**, que es donde el §12 coloca la infraestructura de
auditoría, y **cada contexto de módulo lo mapea** con `ToTable("registros", "auditoria")` explícito
—cada contexto tiene su propio `HasDefaultSchema`, así que el esquema hay que nombrarlo—. El módulo
`Auditoria` es el único que lo **migra**; los demás lo declaran con `ExcludeFromMigrations`, que lo
saca del comparador de modelos y evita el problema que se temía: **no hacen falta migraciones
vacías** en Organización ni en Identidad, y `scripts/comprobar-migraciones.sh` da verde para los
tres.

**No hace falta SQL crudo para nada de esto.** La tabla de prohibiciones del 0.6 sigue como estaba:
ni una llamada nueva que rodee el filtro. La única sentencia escrita a mano está **en la migración**
—el disparador del punto 3—, que es donde se escribe el esquema, no en un caso de uso.

## 2. Una sola fase, porque las claves se conocen antes de guardar

La receta canónica de auditoría con EF Core es de dos fases: recoger en `SavingChanges`, dejar
pendientes las filas cuya clave todavía no existe, y completarlas en `SavedChanges` con un segundo
guardado. Existe por una razón concreta: en el caso general, la clave de un `INSERT` la pone la base
—un `IDENTITY`, una secuencia, un `DEFAULT`— y no se sabe hasta después.

**Aquí sí se sabe, y se ha comprobado.** No se ha copiado del ADR-0010 la frase de que las claves
salen del constructor del dominio. `LasClavesSeConocenAntesDeGuardarTests` recorre los modelos ya
construidos de Organización y de Identidad y comprueba dos cosas:

1. **Ninguna propiedad** —sea o no clave— viene del servidor, en las **cinco** formas que tiene de
   venir: un `DEFAULT`, una columna calculada, una estrategia de generación de Npgsql (`IDENTITY` o
   `serial`), un valor que se regenera en cada `UPDATE`, o un testigo de concurrencia —que en
   PostgreSQL suele ser `xmin`, y que llega con el 0.9—.
2. **Toda clave primaria está completa antes de guardar**: o la pone el dominio, o la pone el
   generador del lado del cliente. Un `Guid` marcado `OnAdd` no es un valor del servidor y el test
   lo dice explícitamente, para que la distinción no se confunda con un descuido.

El día que alguien añada una columna con `DEFAULT now()`, una clave `IDENTITY` o el testigo de
concurrencia del 0.9, ese test se pone rojo **antes** de que la traza empiece a escribir claves
vacías, y la segunda fase deja de ser opcional. Y lo que hay que hacer entonces no es añadir la
propiedad a una lista de excepciones: es reabrir esta decisión.

Es la diferencia entre una simplificación justificada y una que funciona por casualidad.

## 3. Solo añadido: lo impide el motor, no una promesa

**Decisión.** `auditoria.registros` tiene una función `plpgsql` y **dos disparadores**: uno de fila
`BEFORE UPDATE OR DELETE` y otro de sentencia `BEFORE TRUNCATE`. Los dos levantan una excepción con
`ERRCODE = 'restrict_violation'` (SQLSTATE `23001`).

**Por qué no un `REVOKE`.** Los permisos los da y los quita el dueño de la tabla, que es el usuario
con el que se conecta la aplicación. Un permiso que el interesado puede devolverse a sí mismo es una
frase, no una guarda. Lo que impide un `UPDATE` es algo que lo rechace **en el motor**, para todo el
mundo y venga por donde venga.

**Y hay que decirlo con estas palabras, porque el antipatrón está a mano:** *esto no es lógica de
negocio, es una restricción de integridad, de la misma familia que un `CHECK`*. No decide nada, no
calcula nada, no depende de ningún dato de la fila: mira la **operación** y la rechaza. La única
diferencia con un `CHECK` es que PostgreSQL no sabe expresar «esta fila no se puede cambiar» de otra
manera. Quien lea esto con la regla «nada de lógica en disparadores» en la mano, que lea también
esta línea.

**Dos disparadores y no uno** porque los de fila no ven un `TRUNCATE`, que es justamente la orden
con la que se vaciaría la tabla de un golpe.

**Leer la migración no es la prueba.** `LaTrazaEsDeSoloAnadidoTests` lanza el `UPDATE`, el `DELETE`
y el `TRUNCATE` contra PostgreSQL de verdad y exige el error del motor con su SQLSTATE. Dos casos
más comprueban el `CHECK` que impide una fila sin empresa y sin motivo, y otra con las dos cosas.

## 4. Los secretos: lista de permitidos, y falla cerrado

Una tabla de solo añadido que guarda el valor viejo y el nuevo de cada propiedad es, sin querer, el
**historial completo de resúmenes de contraseña** de todo el mundo, en un sitio que por diseño no se
puede limpiar. Dos propiedades no pueden acabar ahí por ningún camino: `Usuario.HashDeContrasena` y
`TokenDeRefresco.Hash`.

**Decisión.** La selección de lo que se audita es una **lista de permitidos**, no de prohibidos:
cada entidad y cada propiedad declara su clasificación (`Auditada`, `NoAuditada`, `Secreta`) en su
`IEntityTypeConfiguration`, y lo que no está clasificado **no se audita**. Mejor todavía: falla
cerrado en la revisión, porque `CadaEntidadDeclaraSuAuditoriaTests` recorre el modelo ya construido
y pone en rojo cualquier entidad o propiedad `SinClasificar`. Una columna nueva no se cuela en la
traza por olvido; el olvido es lo que da el rojo.

Eso es la **forma**. El **efecto** lo prueban dos tests de integración:

- Cambiar la contraseña de una cuenta y exigir que ni el resumen viejo ni el nuevo —ni las dos
  contraseñas en claro— aparezcan en ninguna fila de traza, habiendo comprobado antes que ese cambio
  **sí** dejó traza. Sin esa comprobación, un interceptor que no auditara nada pasaría el test.
- La forma fuerte, que no nombra ninguna columna: se le pregunta al modelo qué propiedades ha
  declarado `Secreta`, se leen **todos** sus valores de la base y se exige que ninguno esté en
  ninguna traza. Sigue valiendo el día que alguien marque como secreta una propiedad que hoy no
  existe.

## 5. El inquilinato de la propia traza

`RegistroDeAuditoria` **no** implementa `IDeInquilino`: su empresa es **anulable**, y la interfaz la
exige. Hay escrituras legítimas sin inquilino —la semilla de arranque corre antes de que exista
nadie— y esas filas no se quedan con un hueco:

- **`empresa_id` nulo obliga a `sin_inquilino` con el motivo**, y al revés. Lo garantiza un `CHECK`
  en la tabla: `(empresa_id IS NULL) <> (sin_inquilino IS NULL)`. No es `Guid.Empty`: un valor por
  omisión rellena el hueco y lo esconde, que es exactamente lo que el 0.6 se negó a hacer.
- **Filtra igual que todo lo demás.** Una traza dice qué NIF tenía antes una empresa y quién lo
  cambió; sin filtro, la tabla de auditoría sería la fuga que el 0.6 vino a cerrar.

**De quién es la traza de un cambio en una entidad global.** Un `Rol` no es «de» ninguna empresa
(ADR-0011). Su traza **sí** lo es, y lleva la empresa **desde la que se actuó**: la que estaba
activa cuando se hizo el cambio. La consecuencia se asume y se escribe: **un mismo rol acumula
trazas de varias empresas, y cada una solo ve las suyas**. Nadie ve la historia completa de un rol
global desde dentro de una empresa. Se prefiere eso a lo contrario —que la traza de un rol sea
visible desde cualquier empresa—, que convertiría la auditoría en el camino por el que se descubre
qué otras empresas existen y quién trabaja en ellas.

## 6. Qué es «un maestro»: las diez entidades de hoy, una por una

El criterio dice «un maestro» sin definirlo. Se define aquí, entidad por entidad, sobre las diez que
existen hoy. La tabla lleva una fila más, la undécima: la propia traza, porque «el interceptor no se
audita a sí mismo» también hay que escribirlo en algún sitio. La tabla está también en `docs/PLAN.md`, y no la vigila la buena voluntad: la vigila
`CadaEntidadDeclaraSuAuditoriaTests`.

| Entidad | ¿Se audita? | Por qué |
|---|---|---|
| `Empresa` | **Sí** | El maestro raíz. Su NIF y su razón social salen impresos en cada factura; cambiarlos cambia un documento con validez fiscal. |
| `Ejercicio` | **Sí** | Su apertura y su cierre son la frontera de la R14: qué se puede seguir tocando y qué no. |
| `Serie` | **Sí** | La numeración fiscal. Tocar una serie es tocar la correlatividad de las facturas. |
| `Almacen` | **Sí** | Dónde está el stock. Su alta, su código y su bloqueo. |
| `Usuario` | **Sí**, sin el resumen | Alta, correo, nombre, bloqueo y último acceso. `HashDeContrasena` va marcado `Secreta` y no entra (punto 4). |
| `Rol` | **Sí** | Un rol es un juego de poderes: cambiarlo cambia lo que puede hacer todo el que lo tenga. |
| `PermisoDeRol` | **Sí** | Conceder o retirar un permiso es *el* cambio que hay que poder reconstruir. No tiene ninguna propiedad que clasificar: sus dos columnas **son** la clave, así que el alta y la baja de la fila son el cambio entero. |
| `Membresia` | **Sí** | Quién pertenece a qué empresa: la frontera del inquilinato del 0.6 escrita en filas. Un alta aquí da acceso a los datos de una empresa entera. |
| `RolDeMembresia` | **Sí** | Qué rol tiene alguien en una empresa: la otra mitad de «quién puede qué». Como `PermisoDeRol`, sus dos columnas son la clave. |
| `TokenDeRefresco` | **No** | El «no» de la lista, por dos motivos que se suman. Rota cada quince minutos —una fila por acceso y otra por renovación—, así que auditarla llenaría de ruido una tabla que no se puede limpiar; y lleva `Hash`, un resumen de credencial. Lo que de ella interesa a una auditoría es «quién entró y cuándo», y eso ya deja traza en `Usuario.UltimoAccesoEn`. |
| `RegistroDeAuditoria` | **No** | Es la traza. Auditarla sería recursión, no información. **El interceptor no se audita a sí mismo.** |

**`Direccion` no está en la lista porque no es una entidad**, es un objeto de valor poseído. EF Core
lo sigue como una entrada aparte del rastreador —cambiar solo la calle deja el `Almacen` como
`Unchanged` y su `Direccion` como cambiada—, y tomarlo al pie de la letra daría trazas de un
«Direccion» sin identidad propia y ninguna del almacén, que es de lo que se está hablando. El
interceptor **pliega** cada entrada poseída en la fila de su dueño, con el nombre de la navegación
por delante: `Direccion.Calle`. Sustituir el objeto de valor entero —que es lo que hace un
`Modificar` de dominio— aparece además como dos entradas con la misma clave, una baja y un alta;
plegadas, vuelven a ser lo que de verdad son: el antes y el después de las mismas propiedades.

**Y solo lo que cambió.** Una modificación lista únicamente las propiedades cuyo valor es distinto,
comparando el valor **tal como va a la columna**. No se usa `IsModified`, que es una bandera del
rastreador y se enciende para todas las columnas de un objeto de valor sustituido aunque vuelvan a
llevar lo mismo. Repetir en cada fila el valor de las diez columnas intactas convertiría «qué
cambió» en un ejercicio de comparar dos listas, en una tabla que no se puede limpiar. Una
modificación que no cambia ningún valor auditado **no deja fila**; un alta y una baja sí la dejan
aunque no tengan valores que enseñar, porque hay entidades cuyas columnas son todas la clave.

## 7. El cabo que el 0.6 dejó suelto: `HasQueryFilter` no interviene en un `INSERT`

Un filtro global protege lo que pasa por el traductor de consultas, y nada más. Un `INSERT` no pasa
por ahí. Hasta el 0.7, que una fila naciera con la empresa buena dependía de que cada caso de uso se
acordara de escribir su `usuarioActual.EmpresaId` a mano —hoy son tres sitios; con dieciséis módulos
serán cientos—, y el fallo no da error: da una fila en la empresa equivocada, que se lee como un
dato correcto.

**Decisión.** El interceptor, que ya recorre las entradas pendientes para escribir la traza,
comprueba de paso que toda fila `IDeInquilino` añadida o modificada lleve **la empresa del ámbito
actual**, y lanza `EscrituraEnOtraEmpresaException` **antes** de que se confirme nada.

**Su límite, escrito y no escondido:** dentro de un ámbito sin inquilino no hay contra qué comparar,
así que no se comprueba. La semilla de arranque y la administración de pertenencias escriben filas
de otra empresa **a propósito**, y quién puede hacerlo lo decide `PuedeAdministrarAsync`, no esto.
Se comprobó caso por caso que los cuatro casos de uso de `Pertenencias` guardan **dentro** de su
ámbito, así que la guarda no rompe la administración de pertenencias; y que `CrearEmpresa` guarda
**fuera** del suyo, que es lo que hace que crear una empresa desde la empresa A deje una traza con
`empresa_id = A` —«actuó desde», que es el criterio del punto 5—.

## 8. Lo que este ítem NO trae

No por falta de tiempo: porque son de otros ítems, y adelantarlos sería decidir su forma sin su
criterio.

- **Ningún endpoint de consulta ni API de lectura.** Leer la auditoría es de la fase 10. La
  evidencia de que un cambio deja rastro es la tabla, y los tests la leen directamente — que además
  tiene la ventaja de que ninguna capa de presentación puede maquillar lo que hay.
- **Ningún trabajo de retención ni de purga.**
- **Ninguna bandeja de salida.** Es el 0.8, y ahí tendrá su motivo nuevo en `MotivoSinInquilino`.
- **Ni idempotencia ni concurrencia optimista.** Son el 0.9.
- **`tests/Arquitectura.Tests/` sigue vacío.** Es el 0.12.

**Y una línea que hay que dejar escrita:** la traza guarda **datos personales** —correos, nombres,
quién hizo qué y cuándo— en una tabla que por diseño no se puede borrar. **Su periodo de
conservación es una decisión que aquí no se toma**, y hay que tomarla antes de que esto vea datos
reales: es lo que enfrenta el derecho de supresión con una tabla de solo añadido, y la respuesta
—anonimizar, particionar por fecha y desprender particiones, o conservar con base legal— no es una
decisión técnica.

## Consecuencias

- **Se acepta** que cada `SaveChanges` de un módulo con persistencia haga trabajo extra: recorrer el
  rastreador y serializar un objeto por entidad cambiada. Es el precio de que la traza no pueda
  perderse.
- **Se acepta** que un módulo sin el interceptor registrado siga funcionando y siga pasando sus
  tests de negocio: lo único que cambia es que deja de haber rastro, y eso no se nota mirando la
  pantalla. Lo nota `UnCambioEnUnMaestroDejaSuRastroTests`, y por eso ese test existe.
- **Se acepta** que un `RegistroDeAuditoria` escrito en un ámbito sin inquilino no se vea desde
  ninguna empresa. Son las trazas de la semilla y de la autenticación; consultarlas es tarea de
  administración de la instalación, no de una empresa.
- **Cambia una cosa de lo que ya estaba:** `CerrarSesion` abre ahora el mismo ámbito
  `AutenticacionYSesion` que `IniciarSesion` y `RenovarSesion`. Le faltaba desde el 0.5 y no se
  notaba porque nada preguntaba; desde el 0.7 pregunta el interceptor, porque una escritura sin
  empresa activa tiene que decir por qué no la tiene.
- **Queda pendiente para la R3:** los libros —asientos, movimientos, la cadena de la R15— copian de
  aquí la forma de solo añadido y la atomicidad, pero **no** copian el interceptor: un asiento no es
  la traza de un cambio, es el cambio.
