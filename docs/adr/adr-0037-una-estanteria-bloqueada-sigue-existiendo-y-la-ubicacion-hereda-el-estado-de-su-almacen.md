---
tipo: referencia
stack: [csharp, dotnet, efcore]
aplica_a: [ddd, dominio, clean-architecture, proteccion-datos, testing]
tags: [adr, bloqueo, r16, articulo-32, lopdgdd, puertos, contracts, inventario, adr-0016, adr-0020, adr-0023]
revisado: 2026-09-18
---

# ADR-0037: Una estantería bloqueada sigue existiendo, y la ubicación hereda el estado de su almacén

- **Estado:** aceptado
- **Fecha:** 2026-09-18
- **Cumple una promesa del** [ADR-0016](adr-0016-el-bloqueo-es-uno-y-tapa-a-las-tres.md), §2: «cuando
  la fase 3 necesite leer el almacén de un movimiento histórico, abrirá un ámbito declarado, que es
  el mecanismo previsto». Lo necesita la **fase 2**, una antes de lo escrito allí, y el mecanismo es
  el previsto. No se enmienda nada del ADR-0016: el filtro sigue tapando **sin excepciones** a
  todas las bloqueables —que ya no son las tres de entonces, sino cinco: empresa, usuario, almacén,
  ubicación y tercero—, y esto no es una excepción sino una apertura declarada más.
- Se implementa en el **ítem 2.2**, y se decide **antes de escribir su código**.

## Contexto

El ítem 2.2 publica tres puertos para que Inventario pregunte por los maestros a los que va a
apuntar: el artículo, el almacén y la ubicación. Los dos de Organización contestan
`EstadoDeMaestro`, y ahí aparece una pregunta que ningún puerto anterior había tenido que contestar:
**qué contesta un maestro bloqueado**.

Hay ya una respuesta en el proyecto, y es la contraria de la que hace falta aquí. `IConsultaDeTerceros`
contesta `NoExiste` a un tercero bloqueado, y no por descuido: el ítem 1.10 quitó el valor
`Bloqueado` que se había diseñado, porque la ficha no llega siquiera al `switch` —el filtro de
repositorio del art. 32 la esconde antes— y porque distinguirla «diría que alguien tiene sus datos
reservados». Esa decisión es correcta y se queda.

Si el almacén copiara esa respuesta, el histórico se rompería. Un movimiento de existencias de hace
tres años apunta a su almacén y a su ubicación **para siempre**, y la doble flecha del ítem 2.3
exige que todo movimiento resuelva el suyo. Un almacén bloqueado que contestara `NoExiste` dejaría
el libro señalando a la nada el día que alguien dé de baja una nave, que es el día más normal del
mundo.

Y hay una segunda pregunta, que el criterio del ítem no traía y que sale de mirar el código:
`BloquearAlmacen` escribe la fila del almacén **y ninguna más**. Bloquear un almacén no toca sus
ubicaciones. Así que una **ubicación activa dentro de un almacén bloqueado** no es una hipótesis: es
una fila que existe en la base hoy, y alguien tiene que decir qué contesta el puerto cuando le
preguntan por ella.

## Decisión

### 1. Lo que el bloqueo reserva es la privacidad de una persona, no la existencia de una estantería

Los dos bloqueos comparten mecanismo desde el 0.10 y **no comparten motivo**, y es el motivo el que
decide la respuesta del puerto:

| Qué se bloquea | Por qué | Qué contesta el puerto |
|---|---|---|
| Un tercero, una empresa, un usuario | Art. 32 LOPDGDD: sus datos se reservan | `NoExiste` |
| Un almacén, una ubicación | No romper el histórico de valoración (`CeseDeUso`) | `SoloResuelveLoViejo` |

De un tercero bloqueado, **que exista ya es un dato sobre una persona**: decir «existe, pero solo
resuelve lo viejo» revela que alguien con ese identificador está fichado y que pidió la supresión.
De un almacén bloqueado no se revela nada de nadie: se dice que la nave 3 dejó de admitir mercancía,
que es justamente lo que el albarán impreso de hace tres años ya cuenta.

Esto no relaja el art. 32 para el almacén: la fila **sigue** fuera de todos los caminos ordinarios,
y el `GET` sigue contestando 404. Lo único que cambia es que un módulo que pregunta por un
identificador que ya tiene escrito recibe «esto no se usa para lo nuevo» en vez de «esto no es
nada».

### 2. El invariante 2 habla de la **respuesta**, no del puerto

Que el puerto distinga entre `NoExiste` y `SoloResuelveLoViejo` **no autoriza a que la respuesta
HTTP lo distinga**. El alta de un ajuste contra un almacén bloqueado y contra un almacén inventado
contestan el mismo `400` con el mismo código de error.

No es una regla nueva y se cita en vez de reinventarse: está escrita en `EstadoDelTercero` —«estos
valores son para que la regla decida, no para que la respuesta HTTP los cuente»— y es lo mismo que
hizo el ítem 1.5 con `tercero-duplicado`, uno solo para el activo y para el bloqueado. El puerto es
un mecanismo interno; el `400` es lo que sale por el cable.

### 3. Ver lo bloqueado desde el puerto es una apertura declarada, con un motivo **nuevo**

Para contestar `SoloResuelveLoViejo` el adaptador tiene que **ver** la fila bloqueada, y el filtro
de repositorio no la trae. La única forma admitida es `IAccesoALoBloqueado.ViendoLoBloqueado(...)`
con un motivo de la lista cerrada — nunca un `IgnoreQueryFilters`, que además apagaría de paso el
filtro de empresa.

Ninguno de los tres motivos de hoy sirve, y **no se estira ninguno**:

- `AdministracionDelBloqueo` lo abren los desbloqueos, por una necesidad mecánica: leer la fila que
  van a escribir. Aquí no se escribe nada.
- `AccesoReservadoDelArticulo32` es la vía nominativa y trazada de jueces y Administraciones. Usarla
  para que Inventario resuelva una estantería llenaría de ruido justo la traza que existe para
  contestar quién ha mirado datos reservados.
- `ComprobacionDeUnicidadDeIdentificador` es de otro camino y de otra pregunta.

Entra un cuarto valor, y entra **ahora porque ahora aparece el primer camino que lo necesita**, que
es la regla de esa lista desde que se escribió. Lo que se ve por él no sale por la respuesta, y eso
**no lo garantiza el enumerado: lo garantiza la forma del puerto** —`EstadoDeAsync` devuelve un
estado, no una ficha—, que es el mismo argumento con el que entró el tercero.

### 4. La ubicación **hereda** el estado de su almacén, y el suyo propio solo puede **empeorarlo**

Las cuatro combinaciones, y **las cuatro llevan caso**:

| Almacén | Ubicación | `IConsultaDeUbicaciones` contesta |
|---|---|---|
| Activo | Activa | `SeOfreceParaLoNuevo` |
| Activo | **Bloqueada** | `SoloResuelveLoViejo` |
| **Bloqueado** | Activa | `SoloResuelveLoViejo` ← la que decide |
| **Bloqueado** | **Bloqueada** | `SoloResuelveLoViejo` |

La tercera fila es la decisión, y sale de lo que la ubicación **es**: un hueco dentro de una nave.
Una estantería no sigue admitiendo mercancía porque nadie se acordara de bloquearla cuando se cerró
la nave que la contiene — el género tendría que entrar físicamente por una puerta que está cerrada.
Que su fila propia siga activa es un hecho de la base de datos, no una autorización.

La alternativa —que la ubicación conteste por su fila y punto— deja la coherencia en manos de que
**quien pregunta se acuerde de preguntar también por el almacén**. Eso es una regla no escrita en el
llamante, en un módulo distinto del que la conoce; y el día que un camino nuevo pregunte solo por la
ubicación, entra mercancía en una nave bloqueada sin que nada se ponga rojo. La composición se hace
donde está el conocimiento, que es el adaptador de Organización.

**Que el almacén no puede aportar `NoExiste` es un hecho del esquema, no un olvido.** Hay clave
ajena `ubicaciones.almacen_id → almacenes.id` con `Restrict`, así que una ubicación sin almacén no
existe. Por eso son cuatro casos y no seis: un quinto caso para «el almacén no está» sería un caso
que ningún productor produce, que es el defecto que el ítem 1.10 quitó y que la matriz de puerto ×
estado existe para denunciar. `NoExiste` sale solo de que no haya ubicación.

**Y la composición no es un `Math.Min`.** El orden de «peor a mejor» es `NoExiste` <
`SoloResuelveLoViejo` < `SeOfreceParaLoNuevo`, y el orden de los valores del enumerado es 0, 1, 2
con `SeOfreceParaLoNuevo = 1` y `SoloResuelveLoViejo = 2`: el mínimo numérico de un almacén
bloqueado (2) y una ubicación activa (1) da **1**, que es exactamente la respuesta equivocada. La
composición se escribe por casos, no por aritmética sobre los números de un enumerado que no está
ordenado por severidad.

### 5. El puerto de ubicaciones recibe **también el almacén**, porque nadie más puede comprobarlo

`IConsultaDeUbicaciones.EstadoDeAsync(almacenId, ubicacionId, …)`, y no solo la ubicación. El motivo
no es comodidad: cada movimiento apunta a un almacén **y** a una ubicación, y que la segunda esté
dentro del primero es una condición que **Inventario no puede comprobar por su cuenta** —los dos
datos viven en `organizacion`, y ninguna consulta cruza esquemas (§5, regla 4)—. O lo contesta este
puerto, o no lo contesta nadie y el libro admite movimientos con la mercancía en una nave y el hueco
en otra.

Una ubicación que existe pero **cuelga de otro almacén** contesta `NoExiste`, y es la respuesta
correcta y no una simplificación: la pregunta es «qué hay en este almacén con este identificador», y
la respuesta es que ahí no hay nada. No añade valor al enumerado ni casilla a la matriz.

## Lo que este ADR **no** es

**No es una excepción al filtro del art. 32.** El ADR-0016 rechazó por escrito una excepción «solo
para el almacén, que no es un dato personal», y ese rechazo sigue en pie: aquí no se exime a nadie
del filtro, se abre un ámbito declarado, con su motivo de una lista cerrada, anotado en el registro
y comparado entero por `ElFiltroNoSeSaltaPorAhiTests`.

**No es un valor nuevo en `EstadoDeMaestro`.** «Bloqueado» no entra como cuarto valor, por lo mismo
que salió del enumerado de terceros: sus tres valores son **dos preguntas** colapsadas —¿existe?,
¿vale para un alta?— y un almacén bloqueado ya tiene su casilla en ese par. Lo que el bloqueo
produce es un estado existente, no uno nuevo.

## Alternativas descartadas

**Que el almacén bloqueado conteste `NoExiste`, como el tercero.** Es la respuesta uniforme y no
necesita ni motivo nuevo ni ámbito. Rompe la doble flecha del ítem 2.3 el primer día que alguien dé
de baja una nave: los movimientos que apuntan a ella dejan de resolver su almacén, y el histórico de
valoración es lo único que no se reconstruye.

**Que la ubicación conteste por su fila y el llamante componga.** El puerto queda más simple y la
tabla de arriba desaparece. A cambio, la regla «una ubicación de un almacén bloqueado no admite
mercancía» pasa a vivir en cada llamante, sin nada que la haga cumplir, en el módulo que **no** sabe
qué es un almacén.

**Cascadear el bloqueo del almacén a sus ubicaciones al bloquear.** Deja las cuatro combinaciones en
dos y la composición sobra. Se descarta por dos motivos: bloquear dejaría de ser una escritura de
una fila para ser una de N, con su transacción y su cuenta de filas a la vista; y **desbloquear no
sabría qué deshacer** —qué ubicaciones estaban bloqueadas antes por su cuenta y cuáles solo por
arrastre—, así que levantar el bloqueo de la nave desbloquearía estanterías que nadie quiso
desbloquear. Es el mismo argumento por el que `Usuario` mantiene sus dos bloqueos separados
(ADR-0016 §4).

**Pasar solo la ubicación y que el llamante valide la pertenencia.** No puede: el almacén de una
ubicación vive en `organizacion` y el llamante es `inventario`. Sería un `JOIN` entre esquemas, que
es la regla 4 del §5.

## Consecuencias

- `MotivoParaVerLoBloqueado` gana su cuarto valor, y `s_aperturasDeBloqueoPermitidas` —en
  `ElFiltroNoSeSaltaPorAhiTests`— gana el fichero donde se abre, con su cuenta y su argumento
  propio. Esa lista se compara entera y en los dos sentidos, así que el motivo no puede entrar sin
  que nadie lo use ni usarse sin estar declarado.
- **El adaptador que abre el ámbito no puede emitir un testigo de versión**, y la regla mira por
  fichero: `Ningun_camino_que_ve_lo_bloqueado_emite_un_testigo_de_version` exige que los dos
  conjuntos sean disjuntos. Un puerto que contesta un `EstadoDeMaestro` no produce ningún
  `ConVersion<T>`, así que se cumple sin esfuerzo —y queda escrito para que nadie meta luego una
  lectura con versión en ese mismo fichero y descubra el rojo sin saber de dónde sale—.
- El adaptador de ubicaciones lee **dos** filas de `organizacion` —el almacén y la ubicación— dentro
  del mismo ámbito. Es un `JOIN` dentro de un esquema, que sí está permitido.
- La matriz de puerto × estado gana seis casillas —dos puertos por tres valores—, y viven en
  `LaMatrizDePuertoYEstadoTests`, que es la que cubre la familia de `EstadoDeMaestro`. El artículo,
  con enumerado propio, va a la otra. Escribirlas en la matriz equivocada las dejaría sin dueño y en
  verde.
- Las **cuatro** combinaciones de almacén × ubicación llevan caso propio contra PostgreSQL real, y
  la tercera —ubicación activa dentro de almacén bloqueado— es la que se rompe sola si algún día
  alguien «simplifica» la composición. Sin ella, las otras tres salen verdes con el puerto mirando
  solo su fila.
- Un almacén bloqueado se deja leer por este camino y **solo** por este: el `GET` del recurso sigue
  contestando 404, y el listado sigue sin enseñarlo.
