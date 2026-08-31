---
tipo: referencia
stack: [dotnet, efcore, postgresql, aspnetcore]
aplica_a: [api-rest, persistencia, multiempresa, seguridad, testing]
tags: [adr, r10, r11, idempotencia, concurrencia, etag, if-match, xmin, upsert]
revisado: 2026-08-31
---

# ADR-0014: La clave del cliente y la versión del recurso son dos mecanismos, no dos niveles de uno

- **Estado:** aceptado
- **Fecha:** 2026-08-31

## Contexto

El criterio del ítem 0.9 los junta en una frase —*«la misma `Idempotency-Key` devuelve el mismo
recurso; `If-Match` obsoleto → 412; estado incorrecto → 409; sin cabecera → 428»*— y compartir ítem
es lo único que comparten:

- **R10, la `Idempotency-Key`,** protege de que **una** persona repita su propia petición. El móvil
  del comercial pierde la cobertura al enviar un alta y la aplicación reintenta sola: sin clave hay
  dos almacenes.
- **R11, el `If-Match`,** protege de que **dos** personas pisen el mismo recurso. Ana y Luis abren el
  mismo almacén, Ana guarda, Luis guarda encima y lo de Ana desaparece sin que nadie vea un error.

Un alta no tiene versión previa que citar —no existía— así que solo la primera la protege; una
modificación ya trae la versión de su lectura, así que le basta la segunda. **Ninguna acción de hoy
pide las dos**, y hay un barrido que lo mantiene así (§8 de este ADR).

## 1. La identidad de una petición repetible es la tupla entera

**Decisión.** La clave es `(EmpresaId, UsuarioId, Metodo, Ruta, Clave)`, y esa es también la clave
primaria de `auditoria.claves_de_idempotencia`.

La clave la elige el cliente, y dos clientes eligen la misma antes o después: `1`, `test`, el UUID
de una plantilla copiada. Con la clave sola por identidad, el segundo recibiría **la respuesta
guardada del primero** —el almacén de otra empresa, presentado como suyo— y el fallo se leería como
un dato correcto. Es lo peor que puede pasar en un sistema multiempresa: no hay error que mirar.

El método y la ruta están dentro porque la misma clave contra `POST /almacenes` y contra
`POST /series` son dos operaciones y nadie dijo lo contrario.

**Se comprueba en los dos planos.** `LaClaveDeIdempotenciaEsLaTuplaEnteraTests` compara la sentencia
SQL contra el modelo ya construido —columnas, objetivo del conflicto, huecos de parámetro y columnas
obligatorias—, y `La_misma_clave_desde_otra_empresa_hace_su_propio_trabajo` lo comprueba por el
efecto, contra PostgreSQL, con dos empresas mandando literalmente la misma clave.

## 2. La huella se calcula sobre los bytes del cuerpo, y el cuerpo no se guarda

**Decisión.** SHA-256 en hexadecimal de **los bytes tal como llegaron**, antes de deserializar nada.
La misma clave con otro cuerpo es un `409`, no una repetición.

Sobre el objeto ya deserializado, la huella dependería del serializador y de sus opciones, y cambiar
una opción cambiaría la identidad de peticiones **ya guardadas**.

**La contrapartida, dicha para que nadie la descubra depurando:** dos cuerpos que solo se diferencian
en espacios en blanco tienen huellas distintas, así que el segundo intento se rechaza con un `409` en
vez de repetir la respuesta. Se prefiere ese error a devolver el desenlace de una petición que no es
exactamente la que se hizo.

El cuerpo **no se guarda**: se guarda su huella. Es lo único que hace falta para decir «esto no es lo
que pediste antes», y evita que una tabla de servicio acabe siendo una segunda copia de todo lo que
ha entrado por la API.

## 3. Reclamar la clave es un `INSERT … ON CONFLICT`, y es la única sentencia cruda del mecanismo

**Decisión.**

```sql
INSERT INTO auditoria.claves_de_idempotencia
  (empresa_id, usuario_id, metodo, ruta, clave, huella, creada_en)
VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})
ON CONFLICT (empresa_id, usuario_id, metodo, ruta, clave) DO NOTHING
```

**Las dos alternativas que no son SQL crudo son peores, y no por poco:**

- **Mirar y luego insertar** deja una ventana entre las dos consultas. Dos peticiones con la misma
  clave la cruzan a la vez, las dos ven «no está» y las dos hacen el trabajo: **es el fallo que el
  mecanismo entero viene a impedir, reintroducido en su propia implementación**, y solo bajo carga.
- **Insertar y atrapar la violación del índice** usa una excepción como flujo de control, y una que
  llega desde dentro de una llamada transaccional: en PostgreSQL un error dentro de una transacción
  la deja **abortada**, así que el `catch` no puede seguir trabajando.

**La excepción al barrido del 0.6 es estrecha, y lo es por su argumento:** esta sentencia **no lee
ninguna tabla**. Escribe una fila cuya clave primaria completa se le entrega, y esa clave lleva
`empresa_id` dentro, tomado del *claim* —nunca de la petición—, exactamente igual que haría el
filtro. No hay ninguna fila que un filtro de empresa hubiera protegido y esta sentencia alcance.
Todo lo que se **lee** de esa tabla pasa por EF Core con su filtro global puesto. Quien quiera SQL
crudo para leer no puede acogerse a esto, porque el argumento entero es que aquí no se lee.

Es el mismo listón que el cerrojo consultivo del 0.8, y por eso `LaClaveDeIdempotenciaEsLaTuplaEnteraTests`
existe: comprueba que la sentencia sigue diciendo lo que su excepción dice que dice.

## 4. El recibo cae en la misma transacción que el trabajo, y la transacción va sin puntos de guardado

**Decisión.** El filtro es el **dueño de la transacción** de la petición: la abre antes de reclamar
la clave, deja pasar el trabajo, guarda la respuesta y confirma. La invariante de la tabla es
**«la fila existe si y solo si el trabajo ocurrió»**.

De ahí salen tres cosas que parecen detalles y no lo son:

- **Las columnas de la respuesta son anulables**, porque la fila nace antes de que exista la
  respuesta. Quien garantiza que toda fila **confirmada** está completa es la transacción, no la
  columna: un `NOT NULL` se comprueba en el `INSERT`, no al confirmar.
- **Solo se guarda la respuesta de un `2xx`.** Un fallo se devuelve tal cual y **no se apunta**, para
  que la clave quede libre. Guardar el recibo de un `409` dejaría al cliente atrapado: corrige el
  dato, reintenta con la misma clave —que es lo que manda hacer— y recibe para siempre el error de la
  primera vez.
- **La transacción va con `AutoSavepointsEnabled = false`.** Ver el punto siguiente.

### Los puntos de guardado automáticos, y por qué se apagan

Con una transacción abierta, EF Core pone un `SAVEPOINT` delante de cada `SaveChanges` y vuelve a él
si falla. Eso deja la transacción **viva** después de un fallo, con la clave ya reclamada dentro:
alguien que la confirmara —hoy nadie, mañana cualquiera— dejaría el recibo de un trabajo que se
deshizo, y el reintento devolvería un `201` que no creó nada. Sin puntos de guardado, un
`SaveChanges` que falla aborta la transacción entera: **la invariante deja de depender de que el
filtro se acuerde de deshacerla**.

**Y además la hace visible, que es como se ha descubierto.** Cada punto de guardado abre una
subtransacción, que en PostgreSQL toma su propio identificador, así que las filas de un mismo trabajo
salían con `xmin` distintos —medido: **759** la de negocio, **760** el recibo, consecutivos— y la
prueba que usaron el 0.7 y el 0.8 no valía aquí. Sin ellos, todas llevan el mismo número, y
`El_recibo_y_el_almacen_llevan_el_mismo_xmin` es la tercera vez que este proyecto prueba la
atomicidad de la misma manera: preguntándoselo al motor.

**El precio, dicho:** dentro de una petición idempotente ya no se puede seguir trabajando tras un
fallo de guardado. Nadie lo hace, y hay un caso que lo notaría —un choque de concurrencia, porque el
manejador del `412` consulta la fila actual y la transacción abortada se lo impediría—. Por eso
ninguna acción pide los dos mecanismos a la vez, y el barrido del §8 lo mantiene así.

## 5. Ninguna respuesta con un secreto dentro entra en esa tabla

**Decisión.** Las rutas que admiten la cabecera son una **lista de permitidos** explícita: el
atributo `[AdmiteIdempotencia]`, hoy sobre las **seis** altas. Una ruta no marcada que reciba la
cabecera responde `400`, no la ignora.

Ignorarla sería lo peor de los dos mundos: el cliente cree que está protegido, reintenta con
confianza y duplica.

**Y el argumento de por qué ningún secreto puede acabar guardado es estructural, no una lista de
nombres.** La clave de la tabla exige la tupla `(empresa, usuario)`; una acción **anónima** no tiene
ni lo uno ni lo otro, así que no se puede marcar sin pedir una identidad que no existe. Y en este
sistema **las respuestas que llevan credenciales dentro son precisamente las de los caminos
anónimos** —identificarse, renovar, cerrar—. La coincidencia no se deja al azar:
`Ninguna_accion_que_admite_idempotencia_es_anonima` la comprueba por reflexión.

## 6. Los tres códigos de R11 no son grados de la misma cosa

| Situación | Código | Quién lo arregla |
|---|---|---|
| No viene `If-Match` | **428** | El cliente: tiene que leer el recurso y citar su versión |
| Viene y no es una versión concreta (`*`, débil, lista, sin comillas) | **400** | El cliente: la manda mal |
| Viene bien y ya no es la actual | **412** | Nadie: otro guardó primero. Hay que releer y decidir |
| El recurso está en un estado que no admite la operación | **409** | Depende del caso; no es concurrencia |

**El comodín `*` no se admite**, aunque el RFC lo defina: significa «me vale cualquier versión con
tal de que el recurso exista», que es saltarse el control entero sin dejar de cumplir el protocolo.

**El `412` sale de la política central**, de un `IExceptionHandler` que traduce
`DbUpdateConcurrencyException`, y no de un `catch` por acción: quince sitios que hay que acordarse de
escribir, y el que falte devolverá un `500` que el cliente reintentará tal cual, machacando lo que el
otro escribió. Es la actualización perdida con un mensaje que despista.

**`ExecuteUpdate`/`ExecuteDelete` siguen prohibidos** (barrido del 0.6): saltan el rastreador, así
que el testigo no entraría en el `WHERE` y no habría choque que detectar.

### El estado actual del conflicto se sirve como versión, y en el cuerpo

La convención pide devolver el estado actual para que el cliente pueda enseñar la diferencia. Lo que
sale es **la versión de ahora**, en la extensión `versionActual` del ProblemDetails. Volcar los
valores de la fila publicaría columnas que no pasan por ningún DTO —entre ellas las clasificadas
como secretas— y en el sitio con menos contexto para decidirlo.

**Y va en el cuerpo y no en una cabecera `ETag`, que era el primer diseño.** No es una preferencia:
el middleware de excepciones de ASP.NET Core registra un `OnStarting` que **borra el `ETag`** de toda
respuesta de error. Comprobado poniendo a la vez el `ETag` y una cabecera cualquiera: la segunda
llegó y el `ETag` no. Y el borrado tiene razón, que es lo que impide buscarle la vuelta con otro
nombre: el `ETag` de una respuesta es el de **la representación que va en esa respuesta** (RFC 9110,
§8.8.3), y la que va en un `412` es un documento de problema, no el recurso.

## 7. El testigo es `xmin`, y la trampa está en el camino de lectura

**Decisión.** El testigo se declara como propiedad de sombra `Version` de tipo `uint`, marcada
`ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`; el proveedor la mapea a la columna de sistema
`xmin`. No es un contador nuestro, así que no hay que mantenerlo ni puede desincronizarse.

**La trampa, comprobada contra PostgreSQL antes de diseñar nada:** el testigo vive en el rastreador
de cambios, no en la entidad. Si la entidad viene de una consulta con `AsNoTracking()`,
`Entry(entidad).Property<uint>("Version").CurrentValue` **devuelve `0` sin lanzar nada** —EF la
adjunta en ese momento y la propiedad de sombra nace a cero—. Medido: **756** por el camino
rastreado, **756** proyectando con `EF.Property`, **0** por este. Ese cero compila, pasa los tests
rápidos y sale a producción dentro de un `ETag`, donde convierte **todo `If-Match` en un `412`
perpetuo**.

Por eso `Versiones.De` comprueba el rastreo antes de preguntar y lanza con el mensaje que dice cómo
proyectarlo. Fallar ruidosamente es lo único que distingue esto de un cero silencioso.

**La migración existe y no emite SQL.** El diferenciador ve una columna nueva y escribe un
`AddColumn`, pero `xmin` ya está en toda tabla de PostgreSQL: `dotnet ef migrations script` sobre esa
migración produce **solo** el `INSERT` en `__historial_de_migraciones`. Se conserva para que el
modelo y las migraciones sigan cuadrando, que es lo que comprueba `comprobar-migraciones.sh`.

## 8. Qué recurso lleva qué, decidido con un barrido y no de memoria

**Decisión.** `TodaEscrituraDiceComoSeProtegeTests` recorre por reflexión los dos ensamblados de
*endpoints* y exige que **toda acción que cambia estado** exija `If-Match`, admita
`Idempotency-Key`, o esté en una lista de exentas **con su motivo escrito**. Las listas se comparan
en los dos sentidos: una exención que sobra es un permiso que sigue concedido sobre una acción que ya
cambió.

Hoy: **46 acciones**, de ellas **32** cambian estado — **16** exigen `If-Match`, **6** admiten
`Idempotency-Key` y **10** están exentas. Los números están fijados en el propio test, porque un
barrido cuya enumeración devuelva nada sale verde por la peor de las razones.

Las tablas con el detalle están en `docs/PLAN.md`, en la sección del ítem 0.9.

**Las diez exenciones, resumidas** (el motivo entero está en el test): las cuatro de sesiones son
anónimas o repetibles por naturaleza y sus respuestas llevan credenciales; las dos de contraseñas
dejan el mismo estado al repetirse y una de ellas trae su propia precondición en el cuerpo; las
cuatro de pertenencias y roles chocan con su clave al repetirse, y un `If-Match` **sobre el usuario**
no protegería la fila que se toca — una cabecera que parece proteger sin proteger es peor que no
tenerla.

## 9. Lo que este ítem NO trae

- **Ninguna política de retención** para `auditoria.claves_de_idempotencia`. La tabla crece sin
  límite y eso está **decidido así, no olvidado**: borrar recibos es exactamente lo que reabre la
  ventana que la tabla cierra, y decidir cuánto tiempo se conserva uno es una decisión de producto
  que necesita saber cuánto reintenta un cliente. Queda anotado como pendiente en `docs/PLAN.md`.
- **Ningún estado intermedio en la fila.** No hay columna de «en curso» porque no hay un estado
  intermedio que representar: la fila se reclama dentro de la transacción del trabajo.
- **La combinación de los dos mecanismos en una misma acción**, por lo dicho en el §4.

## Consecuencias

- **La transacción de una petición idempotente la abre el filtro**, así que ningún caso de uso puede
  abrir la suya sin chocar: `AbrirTransaccionAsync` lanza si ya hay una. Es a propósito — si alguien
  más la abre, la clave y el trabajo pueden acabar en transacciones distintas.
- **Los almacenes se registran con clave** (`AddKeyedScoped`), y la clave es el segmento de módulo de
  la ruta, que es también el nombre de su esquema. Registrados bajo el tipo a secas, el último módulo
  desplazaría a los demás y las claves de Organización se apuntarían en la transacción de Identidad.
- **El cuerpo se guarda como `text` y no como `jsonb`**, al revés que la bandeja de salida: `jsonb`
  normaliza —reordena claves, se come los espacios— y lo que hay que devolver en la repetición son
  **los mismos bytes**. La bandeja guarda un hecho que se vuelve a serializar; esto guarda una
  respuesta que se vuelve a emitir tal cual.
- **Solo dos cabeceras se guardan**, `ETag` y `Location`, cada una en su columna. Con un saco de
  cabeceras, cualquiera que la tubería añadiera el día de mañana —una cookie de sesión, una
  autorización renovada— entraría en la tabla sin que nadie lo decidiera.
- **La clave que manda el cliente no entra en el registro**, y el resto de la tupla sí: la elige quien
  llama, y hay quien mete dentro el número de pedido o el correo de alguien.
- **Una cabecera presente y vacía es un `400`**, no una petición sin proteger. «Sin cabecera» es que
  no venga, y se pregunta por el número de valores, no por si el texto está en blanco.
