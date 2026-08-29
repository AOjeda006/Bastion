---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, mensajeria, multiempresa, observabilidad, testing]
tags: [adr, r12, outbox, bandeja-de-salida, eventos, idempotencia, trabajo-de-fondo, ef-core]
revisado: 2026-08-29
---

# ADR-0013: El evento va en la misma transacción que el cambio, y su efecto ocurre una sola vez

- **Estado:** aceptado
- **Fecha:** 2026-08-29

## Contexto

El criterio del ítem 0.8 tiene tres cláusulas y ninguna cubre a las otras: *«un evento y su
escritura de negocio caen en la misma transacción; el trabajo de fondo lo publica; reprocesar no
duplica»*. La primera es atomicidad, la segunda es entrega y la tercera es idempotencia del efecto.
Un mecanismo puede tener dos y fallar en la tercera sin dar un solo error.

Esto es además **el único camino por el que un módulo escribe en otro** (§4, regla 5): a partir de
aquí, «Contabilidad se entera de que se emitió una factura» es una fila de esta tabla. Lo que se
decida mal se decide para los dieciséis módulos, y el síntoma de casi todos los fallos posibles es
el mismo: **no hay error que mirar**. Un evento perdido no lanza nada; un efecto duplicado tampoco.
Por eso la forma de probar cada cláusula importa tanto como la cláusula.

## 1. El evento entra en el mismo `SaveChanges` que el cambio

**Decisión.** La bandeja se mapea **en el contexto de cada módulo**, apuntando al esquema
`auditoria` con `ToTable(tabla, esquema)` explícito, y un `SaveChangesInterceptor` vuelca en
`SavingChanges` los eventos que los agregados llevan en la mano. Van, por tanto, en el mismo
`SaveChanges` —y en la misma transacción— que el cambio que los produjo. Es la **ruta 1 del 0.7**
(ADR-0012, punto 1) reutilizada tal cual, y por el mismo motivo.

**Las dos alternativas descartadas:**

- **Un contexto aparte alistado en la transacción del módulo.** Obliga a que cada caso de uso de
  cada módulo abra una transacción explícita y la comparta. Son todos los caminos de escritura de
  todos los módulos, y el día que uno se olvide no falla nada: solo se pierde el evento.
- **Un `INSERT` a mano sobre la conexión del contexto.** Es SQL crudo escrito en una capa de
  escritura, justo lo que el barrido del 0.6 prohíbe, y por un motivo que aquí también aplica: lo
  que se escribe a mano no pasa por el modelo, así que ni el filtro ni las invariantes de la tabla
  lo vigilan.

**La prueba que no se puede fingir es el `xmin`.** Los dos tests que uno escribe de primeras —un
guardado que revienta no deja ni la empresa ni su evento; uno que va bien deja las dos cosas— los
pasa **también** la ruta de volcar el evento después de que `SaveChanges` haya ido bien. Lo que esa
ruta no puede es compartir transacción: PostgreSQL guarda en cada fila, en la columna de sistema
`xmin`, el número de la transacción que la insertó, y el de la empresa y el de su evento tienen que
ser **el mismo**. Ese test tiene una condición que hay que respetar y está escrita en el fichero:
**ahí no corre el publicador**, porque marcar una fila es un `UPDATE` y un `UPDATE` le cambia el
`xmin`.

## 2. Los eventos viajan en el agregado, no en un recolector de ámbito

**Decisión.** `RaizAgregado` lleva sus `EventosPendientes`, el caso de uso los registra sobre el
agregado y el interceptor los recoge **del rastreador de cambios**.

Un recolector inyectado por petición —«apunta este evento en la lista»— daría la misma atomicidad y
perdería la propiedad que de verdad importa: así, **un evento no puede existir sin su escritura**.
Solo llega a la bandeja el evento de un agregado que se está guardando. Un caso de uso que registre
un evento y luego no guarde nada no publica nada, y eso es correcto: no ha pasado nada que contar.
Con una lista suelta, ese evento se colaría en el `SaveChanges` siguiente, que puede ser el de otra
cosa y de otra petición.

**Y el agregado los olvida cuando el guardado ya ha ido bien**, no al volcarlos: si se vaciara antes
y el guardado reventase, el agregado seguiría vivo en el rastreador con la lista limpia y el evento
se habría perdido sin que nada fallara. Guardar dos veces el mismo agregado no encola el hecho dos
veces, y hay un test que lo fija.

**La asimetría de capas, escrita.** El tipo concreto de cada evento vive en el `Contracts` del
módulo que lo emite, que es lo único público de un módulo (§12). El `Domain` de un módulo **no ve**
su `Contracts` —las dependencias apuntan hacia dentro—, así que quien construye el evento es la capa
de aplicación, que ve las dos, y se lo entrega a la raíz con `Registrar`. Eso obliga a una
referencia nueva: `Bastion.Organizacion.Contracts` pasa a ver `Bastion.BuildingBlocks.Domain`. Es el
**núcleo compartido** —`Nif`, `Direccion`, `Resultado`, `EventoDeIntegracion`—, no el dominio de
ningún módulo, y todos los módulos lo ven ya por sus propias capas; sin esa línea un evento sería un
`object` y el mecanismo entero perdería el tipo.

**El nombre con el que un evento viaja lo declara el propio contrato** (`EmpresaCreada.Nombre`), no
el cableado. Un nombre escrito dos veces —una en el módulo y otra en el test— se separa el día que
alguien renombra uno de los dos, y lo que se rompe entonces no es un test: es la cola en producción,
con filas cuyo nombre ya no declara nadie.

## 3. Dónde viven las dos tablas: en el esquema `auditoria`, y las migra el módulo Auditoría

**Decisión.** `auditoria.bandeja_de_salida` y `auditoria.eventos_procesados`. Las **mapean todos**
los contextos; solo `AuditoriaDbContext` las **migra**, y los demás las declaran con
`ExcludeFromMigrations`, que las saca del comparador de modelos —así no hacen falta migraciones
vacías y `scripts/comprobar-migraciones.sh` sigue en verde para los tres módulos—.

**Por qué no un esquema propio.** El §5 lista dieciséis módulos y **ninguno es la bandeja**. La
convención de `docs/PLAN.md` dice que el esquema es el nombre del módulo, así que un esquema
`bandeja` exigiría inventar un decimoséptimo módulo y reabrir el §5, que es de lo que no se reabre
(Anexo A.4). Y un esquema sin módulo que lo migre no lo puede crear nadie: quien crea una tabla
tiene que ser dueño de un esquema. Queda el mismo dueño que ya tiene la otra tabla que escriben
todos los contextos dentro de su transacción — el módulo Auditoría.

**No es que la bandeja sea auditoría**, y conviene no confundirlas: la traza dice qué cambió y quién
lo cambió, la bandeja dice qué hay que contarle a otro módulo. Comparten el esquema por la razón de
arriba, no por parentesco. `RegistroDeAuditoria` y `EventoDeLaBandeja` son entidades distintas con
reglas distintas: la traza no se puede tocar —disparadores—, la bandeja se marca y se reintenta.

**`EventoProcesado` está mapeado también en los contextos de módulo**, y hoy nadie lo usa desde ahí.
Es una puerta dejada a propósito, y su motivo está en el punto 6.

## 4. Un despachador de unas decenas de líneas, no un bus en memoria

**Decisión.** `IDespachadorDeEventos` con una implementación propia que resuelve del contenedor
todos los `IManejadorDeEvento` registrados y llama a los que atienden ese tipo.

En un monolito modular un bus no aporta nada sobre una interfaz, y sí quita algo: quién atiende un
evento deja de decirlo el compilador. A eso se suma la nota de licencias del plan maestro —las
bibliotecas clásicas de este hueco pasaron a licencia comercial en 2025—, que convierte una
dependencia gratuita en una decisión de coste. **Ningún paquete NuGet nuevo entra en la solución con
este ítem.**

**La interfaz del manejador no es genérica**, y es a propósito. El despachador recibe del almacén un
evento del que solo conoce el tipo en ejecución; con `IManejadorDeEvento<T>` habría que cerrarla por
reflexión (`MakeGenericType`) y resolver del contenedor un tipo construido — la clase de código que
falla en el arranque de producción y en ningún test. Aquí cada manejador dice a qué evento atiende y
cómo se llama; la comodidad del tipo concreto la da la base `ManejadorDeEvento<T>`.

**El nombre del consumidor es estable y no se deriva del nombre del tipo.** Renombrar la clase haría
que dejara de reconocer todo lo que ya había procesado y lo volviera a procesar entero, en silencio
y una sola vez: el peor momento para descubrirlo.

**Cero manejadores no es un error.** Un hecho que hoy no le interesa a nadie se publica igual: es el
emisor quien decide contar lo que le ha pasado, no el receptor quien decide qué se cuenta.

## 5. Cómo se vacía la cola: sondeo, un solo lector y un cerrojo consultivo

**Decisión.** Un `BackgroundService` alojado en la API mira la cola **cada dos segundos**, se lleva
**cien** filas por vuelta ordenadas por `id` —versión 7, o sea, orden de escritura— y las despacha
una a una. Antes de cada vuelta toma un **cerrojo consultivo de PostgreSQL**
(`pg_try_advisory_lock`) con una clave constante; si no lo consigue, esta vuelta no hace nada y
vuelve en la siguiente.

**Las tres formas de garantizar un solo lector, y por qué esta:**

1. **Cerrojo consultivo** — la elegida.
2. **Suponer que solo hay una instancia desplegada.** Es gratis y falla publicando dos veces, en
   silencio, el día que alguien escale la API a dos réplicas. El fallo no da error: da dos correos,
   dos asientos o dos remesas.
3. **`FOR UPDATE SKIP LOCKED` con varios lectores.** Es **más** SQL crudo, este sí sobre filas, y
   además **pierde el orden**. Eso pesa más de lo que parece: la R15 obliga a que la cadena de
   registros de facturación sea una sola por (obligado tributario, sistema informático), o sea, un
   consumidor serializado. Repartir hoy la cola entre varios lectores haría imposible mañana ese
   consumidor sin rehacer el mecanismo. Un lector único ordenado ya es la forma que la R15 va a
   necesitar.

**La excepción a la prohibición de SQL crudo del 0.6: una, nombrada, por su ruta, y con su motivo.**
`CerrojoDeLaBandeja.cs` está listado en `ElFiltroNoSeSaltaPorAhiTests` y no se extiende a nada más.
El argumento entero, porque de él depende que no sirva de precedente: la prohibición existe porque
el SQL a mano no pasa por el traductor de consultas, así que el filtro de empresa no se le aplica y
devuelve filas de otros inquilinos sin fallar. Las dos órdenes de este fichero **no leen ninguna
tabla**: toman y sueltan un cerrojo del propio motor con una clave numérica constante y devuelven un
booleano. No hay fila que filtrar, así que no hay nada que el filtro pudiera haber protegido. La
excepción se justifica precisamente por lo que la hace inútil para cualquier otro caso: quien mañana
quiera SQL crudo para leer **filas** tendrá que traer su propio argumento.

**El cerrojo es de sesión, así que la conexión se abre y se cierra a mano.** Un contexto de EF Core
abre y cierra la conexión por orden; dejarlo así soltaría el cerrojo al devolver la conexión a la
reserva o —peor— lo dejaría tomado en una conexión que reutilizará otro. Tomar abre la conexión;
soltar suelta el cerrojo y **luego** cierra, siempre, también cuando la vuelta ha reventado.

**Sondeo y no `LISTEN`/`NOTIFY`.** Un aviso del motor bajaría el retraso a milisegundos y añadiría
una conexión permanente y un camino de recuperación propio para cuando ese aviso se pierda —que se
pierde: `NOTIFY` no sobrevive a una desconexión—, así que **haría falta el sondeo igual** como red
de seguridad. Dos mecanismos donde uno basta, para ganar dos segundos en una consistencia que es
eventual por definición.

**Y por qué no Hangfire, que el §3 nombra para trabajos de fondo.** Aquí no hace falta nada de lo
que Hangfire aporta —planificación por horario, reintentos con almacén propio, panel, trabajos
encolados por el usuario—: esto es un bucle que mira una tabla cada dos segundos, y su almacén ya es
la tabla que mira. Meter un programador de tareas con su propio esquema y sus propias tablas para
envolver un `while` sería añadir un mecanismo de reintentos **encima** del que la bandeja ya tiene,
con dos sitios donde mirar cuando algo no sale. Hangfire entra cuando aparezca su caso —trabajos con
horario: cierres, remesas, envíos periódicos—, no antes.

## 6. Marcar DESPUÉS de despachar: al menos una vez, y el efecto una sola

**Decisión.** La fila se marca `Publicado` **después** de que el despacho haya terminado bien. La
entrega es, por tanto, **al menos una vez**. Lo que convierte «al menos una» en «exactamente una» es
la otra tabla: antes de llamar a un manejador se mira si el par **(evento, consumidor)** ya está
apuntado, y en cuanto el manejador termina se apunta.

**Marcar antes se descartó por lo que hace cuando algo falla:** si la fila se marcara publicada
antes de despachar, el fallo de un manejador se tragaría el evento —la cola quedaría limpia, nadie
volvería a intentarlo y el efecto no ocurriría jamás—. El síntoma en producción es el peor de todos,
porque no hay ningún error que mirar. Con «marcar después», el precio es el contrario y es asumible:
el mismo evento puede llegar dos veces, y para eso está la huella.

**La clave de la huella es el par, no el evento.** Con solo el identificador del evento, el segundo
consumidor de un mismo hecho no llegaría a ejecutarse nunca porque el primero ya lo habría marcado
como visto. Cada consumidor tiene su propio turno.

**El hueco que queda, escrito y no escondido.** La huella se graba en su propia transacción, no en
la del efecto del manejador: si el proceso se cae entre «el manejador terminó» y «la huella está
grabada», ese manejador se ejecutará otra vez. Cerrarlo del todo exige que el manejador escriba su
efecto y su huella en el **mismo** `SaveChanges`, y por eso `EventoProcesado` está mapeado también
en los contextos de módulo: la puerta está abierta y sin usar. La fase 0 no tiene ningún manejador
con efecto de negocio, así que esto se decide el día que haya uno delante, no en abstracto.

## 7. La unidad de aislamiento del fallo es la fila, y a la quinta se aparca

**Decisión.** El fallo de **un** evento no es el fallo de la vuelta: se apunta en su fila —intentos
y último error, recortado— y los demás del lote siguen saliendo. A los **cinco** intentos fallidos
seguidos la fila pasa a `Aparcado` y deja de intentarse.

Cinco, no uno y no cien: uno confundiría un corte de red de un segundo con un evento imposible; cien
serían ocho minutos de vueltas antes de que nadie se entere, y un registro con cien excepciones
iguales dentro. **Aparcar cuesta el orden de ese evento y salva el de todos los demás**, que es
exactamente el intercambio que hay que hacer: sin ello, un evento que su manejador nunca podrá
atender —un dato que no existe, un error de programación— bloquea la cola para siempre o se
reintenta en bucle hasta que alguien mire el registro.

**Y no se aparca en silencio:** queda dicho al nivel que se mira, una sola vez y con el motivo
dentro. Reintentarlo eternamente sería ruido; aparcarlo sin decirlo sería una pérdida.

**El caso que hay que probar y que no es obvio:** que después del desastre el trabajo de fondo
**siga vivo**. Un publicador muerto en silencio a las tres de la mañana pasa los asertos de «el
bueno salió» y «el envenenado se aparcó»; lo que lo caza es encolar uno nuevo **después** y exigir
que también salga.

## 8. El publicador no tiene petición detrás

Es el único de los tres pedazos del R12 que corre solo: sin usuario, sin empresa y sin
`HttpContext`. De ahí salen sus formas de fallar en silencio y de ahí sale su inquilinato.

**Decisión.** Cada vuelta abre su propio ámbito de servicios y, dentro, un ámbito **sin inquilino**
con un motivo nuevo: `MotivoSinInquilino.PublicacionDeEventos` — el que el 0.6 dejó reservado por
escrito y que se estrena aquí. La lista de aperturas del 0.6 pasa de **once a doce**, y se compara
entera: añadir una obliga a venir a decidirla.

La alternativa —darle al trabajo de fondo un contexto sin filtro— no se descartó por gusto: sería un
**segundo** mecanismo para saltarse el inquilinato, sin lista cerrada, sin quedar anotado y sin
aparecer en la columna que distingue «no tenía empresa porque publica eventos» de «alguien perdió la
empresa por el camino».

**La cola es de todas las empresas a la vez**, y tiene que serlo: un publicador por empresa sería un
publicador por cada una de las que existan. Por eso `EventoDeLaBandeja` **no** implementa
`IDeInquilino` —su empresa es anulable— y su fila lleva **o** la empresa desde la que se actuó **o**
el motivo por el que no había ninguna, nunca las dos y nunca ninguna: lo comprueba el constructor y
lo vuelve a comprobar un `CHECK` de la tabla. `EventoProcesado` es **global a propósito**: la huella
de que un consumidor ya procesó un evento no es de ninguna empresa. Las dos entidades están
clasificadas en los **dos** barridos del modelo —el de inquilinato y el de auditoría—, que es lo que
impide que una entidad nueva se cuele sin decidir nada.

**La empresa de la fila es desde la que se actuó, no la de la que habla el evento.** Crear una
empresa desde la empresa A deja una fila con `empresa_id = A` y un cuerpo que habla de B. Es el
mismo criterio que la traza del 0.7 (ADR-0012, punto 5), y conviene no confundir las dos cosas al
leer la tabla.

## 9. Se vigila con una métrica, y la métrica es la edad del más viejo

**Decisión.** El publicador publica, en cada vuelta, **los segundos que lleva esperando el evento
pendiente más antiguo**, más dos contadores separados: publicados y aparcados. Y **no** hay ninguna
sonda de la bandeja: ni en la de vida ni en la de disponibilidad.

Es la lección del 0.2 otra vez, y por escrito: una cola atrasada **no** significa que el proceso
esté colgado —meterla en la sonda de vida haría que el orquestador reiniciara la API en bucle, y
reiniciar la API no vacía la cola— ni significa que la API no pueda atender tráfico —meterla en la
de disponibilidad convertiría un retraso de fondo en una caída de servicio—. Se vigila con una
alerta sobre la métrica, que es la herramienta que existe para eso. La lista de comprobaciones
registradas se compara **entera** en un test: si alguien añade una de la bandeja, se pone rojo y hay
que venir aquí a decidirlo.

**La edad y no el tamaño.** Cuántos hay sube y baja con el tráfico y no distingue mil eventos que se
van a publicar en dos segundos de uno atascado desde ayer. La edad del más viejo distingue
exactamente eso, que es lo único sobre lo que se puede poner un umbral que signifique algo.

**Dos contadores y no uno con etiqueta:** sobre el de aparcados se pone una alerta que salta con el
primero, y sobre el de publicados no se pone ninguna. Mezclarlos obligaría a filtrar en cada
consulta, y la que se olvide de filtrar avisará por lo que va bien.

**Y se prueba que mide lo que dice medir**, no solo que existe: con un evento de hace diez minutos
que no puede salir, el instrumento tiene que marcar unos seiscientos segundos; y con la cola vacía,
cero — porque un instrumento que se quedara con el último valor grande diría que hay un atasco
cuando ya no lo hay, y la alerta no bajaría nunca.

## 10. Contra una base sin migrar: se para, y lo dice una vez

**Decisión.** Si al leer la cola PostgreSQL contesta que la tabla no existe, el publicador **se
detiene** y deja una línea de aviso con el esquema, la tabla y qué hacer.

No es un caso teórico: el `docker-compose` de desarrollo levanta la base vacía y nadie aplica las
migraciones — el riesgo que quedó abierto y que cierra el 0.13. Lo que se decide aquí es qué pasa
**mientras**: un error por vuelta desde el arranque hasta que alguien apague el contenedor no es
información, es el sitio donde se esconden los errores de verdad. Que el proceso siga sirviendo la
API es correcto: lo que falta es la cola, no la API.

**Un solo código de PostgreSQL, y comprobado en vez de supuesto.** Contra una base a la que nadie ha
aplicado nada no falta solo la tabla: falta el esquema entero. Aun así, un `SELECT` sobre un esquema
que no existe responde `undefined_table` (42P01) —«relation … does not exist»—, **no**
`invalid_schema_name` (3F000): el 3F000 lo dan las órdenes que **crean** algo dentro de un esquema
ausente, y aquí no se crea nada. Se verificó lanzando las dos consultas contra la misma imagen de
PostgreSQL que usan los tests. Los dos caminos —sin esquema, y con esquema pero sin tabla— tienen su
caso, para que el día que dejaran de responder igual salga rojo.

## 11. Lo que este ítem NO trae

No por falta de tiempo: son de otros ítems, y adelantarlos sería decidir su forma sin su criterio.

- **Ninguna idempotencia de API.** `Idempotency-Key` es el 0.9. Lo de aquí es idempotencia del
  **consumidor**, que es otra cosa: nadie repite una petición, se repite una entrega.
- **Ninguna concurrencia optimista.** El 0.9. La bandeja no la necesita: un solo lector.
- **Ningún `Bloqueado` ni fechas de R14–R17** (0.10), **ninguna interfaz** (0.11).
- **`tests/Arquitectura.Tests/` sigue con su `.gitkeep`** (0.12). Que un módulo solo vea el
  `Contracts` de otro lo sujeta hoy el compilador; que lo vigile NetArchTest es del 0.12.
- **Quién aplica las migraciones en un despliegue** sigue sin decidirse: es el 0.13. Lo que este
  ítem aporta es que, hasta entonces, el publicador no llena el registro de ruido.
- **Ningún endpoint de consulta de la bandeja.** Ver la cola es de la fase 10; los tests la leen
  directamente, que además impide que ninguna capa de presentación maquille lo que hay.

## Consecuencias

- **Se acepta** que cada `SaveChanges` de un módulo con persistencia recorra el rastreador buscando
  agregados con eventos y serialice uno por evento. Es el precio de que el evento no pueda perderse.
- **Se acepta** un retraso de hasta dos segundos entre el cambio y su efecto en otro módulo. Es una
  consistencia eventual y se nombra: el módulo que reacciona **no** ve el cambio en la misma
  petición que lo provocó, y ningún endpoint puede prometer lo contrario.
- **Se acepta** que un evento pueda entregarse dos veces y que el efecto lo desduplique el
  consumidor. La ventana concreta que queda abierta está en el punto 6.
- **Se acepta** que un evento aparcado se resuelva a mano. No hay reintento manual ni panel: es de
  la fase 10, y hasta entonces la fila lleva el motivo dentro y el registro tiene la línea.
- **Queda pendiente para la R15:** la cadena de registros de facturación necesita un consumidor
  serializado por (obligado tributario, sistema informático). El lector único ordenado que se fija
  aquí es la forma que eso va a necesitar; repartir la cola entre varios lectores obligaría a
  rehacer el mecanismo.
- **Queda pendiente:** la retención de la bandeja. Las filas publicadas se quedan para siempre, y
  eso es una tabla que crece con cada cambio de negocio del sistema. Purgar lo publicado a partir de
  cierta edad es una decisión de operación que no se toma aquí, pero hay que tomarla antes de que
  esto vea volumen real.
