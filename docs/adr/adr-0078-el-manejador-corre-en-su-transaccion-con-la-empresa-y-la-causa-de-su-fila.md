---
tipo: referencia
stack: [dotnet, efcore, postgresql, opentelemetry]
aplica_a: [mensajeria, persistencia, multiempresa, observabilidad, concurrencia, testing]
tags: [adr, r12, r8, outbox, bandeja-de-salida, manejador, idempotencia, xmin, traceparent, correlacion, concurrencia-optimista, metrica, adr-0013, adr-0011]
revisado: 2026-11-03
---

# ADR-0078: El manejador corre en su transacción, con la empresa y la causa de su fila, y un choque no lo aparca

- **Estado:** aceptado
- **Fecha:** 2026-11-03
- **Sale del ítem 3.4**, la bandeja lista para su primer manejador. Su criterio son los cinco
  huecos de `docs/PLAN.md`, *El orden de trabajo, C*, y su puerta (*La puerta del 3.4,
  contestada*) fija cuatro cosas: la transacción es la del contexto del módulo, un choque se
  reintenta en el acto y si no a la vuelta, la causa va en tres columnas de la fila, y una fila sin
  empresa no se entrega.
- **Enmienda el [ADR-0013](adr-0013-el-evento-va-en-la-misma-transaccion-y-el-efecto-ocurre-una-vez.md)**
  en siete sitios:
  - **§3**, el último párrafo: la huella mapeada en los contextos de módulo ya se usa (punto 1);
  - **§4**: el despachador resuelve cada manejador por su tipo, `EventoQueAtiende` desaparece, y
    la interfaz recibe la fila y vive en Infrastructure (puntos 1 y 2);
  - **§6**, el último párrafo: el hueco entre el efecto y la huella está cerrado (punto 1);
  - **§7**: un choque de versión no cuenta intento y no aparca (punto 7);
  - **§8**: el publicador sigue sin inquilino, y su manejador ya no (puntos 3 y 4);
  - **§9**: hay un tercer instrumento, el de los aparcados sin resolver (punto 8);
  - ***Consecuencias***, la tercera: la ventana del §6 ya no queda abierta (punto 1).
- **Amplía el [ADR-0011](adr-0011-el-filtro-global-de-empresa-y-los-caminos-que-lo-rodean.md)** en
  su §2: además del *claim* y de los ámbitos sin inquilino, hay un tercer origen de la empresa
  activa, la fila de la bandeja, en un ámbito que solo abre el despachador (punto 3).
- **Se apoya en** `fundamentos/datos-distribuidos/convenciones.md`, que se leyó antes del primer
  manejador: *Outbox* más consumo idempotente, y reintentos con *backoff* exponencial y *jitter*
  solo sobre operaciones idempotentes.

## Contexto

El ADR-0013 dejó la bandeja publicando y deduplicando, y lo hizo sin ningún manejador con efecto
de negocio delante. Lo que se decidió en abstracto tenía cinco huecos, y los cinco aparecían el día
que hubiera uno de verdad:

1. Todo el lote compartía un ámbito y un contexto. Lo que un manejador dejaba a medias en el
   rastreador se lo guardaba el siguiente, que podía ser de otro evento.
2. El manejador corría dentro del ámbito sin inquilino de la vuelta. Lo que leía por EF veía todas
   las empresas, y la guarda de la auditoría no podía comparar contra nada.
3. La fila no decía quién había causado su evento. El efecto se auditaba sin usuario y con una
   correlación suya, y la traza se cortaba en el borde del módulo.
4. Un aparcado salía de la métrica de la edad y solo lo contaba un contador que se reinicia con el
   proceso.
5. La huella se guardaba en su propia transacción (ADR-0013 §6), y nadie abría la del manejador.
   Un choque de versión con otra escritura contaba como un fallo y podía aparcar un documento
   disputado.

El primer manejador real es el de la saga del 3.8, cuyo ADR se escribe en su puerta. El 3.4 deja el
despachador listo y lo ejerce con un manejador de prueba, que vive en los tests de integración, con
su tabla en una base nueva por caso y con una carrera de verdad.

## Decisión

### 1. Cada manejador, en su ámbito y en la transacción del contexto de su módulo

**El ámbito es nuevo por manejador y por intento.** Lo que deja a medias un manejador no lo guarda
ni el siguiente evento ni el siguiente manejador del mismo evento, y un reintento no hereda el
rastreador del intento que chocó.

**La transacción es la del contexto de su módulo**, el mismo que reciben sus repositorios en ese
ámbito. Dentro van, sin nada fuera:

- la mirada a la huella, el par (evento, consumidor);
- el efecto del manejador;
- su traza de auditoría, que escribe el interceptor de ese contexto (ADR-0012);
- la huella;
- y el evento de respuesta, que escribe el interceptor de la bandeja en el mismo `SaveChanges`.

**El contexto lo declara el registro, y no el manejador.** La puerta decía
`ManejadorDeEvento<TEvento, TContexto>`, pero el manejador vive en Application, que no ve EF Core.
Lo declara el cableado del módulo, en Infrastructure:
`AgregarManejadorDeEvento<TManejador, TContexto>()`. A qué evento atiende lo dice la base,
`ManejadorDeEvento<T>`, leída al registrarlo y sin construirlo; un manejador que no la hereda falla
en el arranque y no en el primer evento.

**La huella se añade antes de llamar al manejador**, para que entre en el primer `SaveChanges` de
su efecto. Es un refinamiento del orden literal de la puerta, que la añadía después. Si el
manejador termina sin haber guardado nada, el contexto se vacía y la huella se guarda sola: lo que
no confirma no lo guarda nadie, como al acabar una petición que no confirma, y un caso de uso que
dice que no y deja cambios en memoria se comporta igual que detrás de HTTP.

**Sin puntos de guardado.** Con ellos, cada `SaveChanges` dentro de la transacción abre una
subtransacción, y lo que se escribe en ella lleva otro `xmin`. La prueba de la transacción única es
la de siempre, el `xmin` de las cuatro filas, que es uno. Con un solo `SaveChanges` por manejador,
quitar la línea que los apaga no lo distinguía (la mutación 1082 salió verde); por eso hay un caso
con un manejador que confirma dos veces.

**El publicador marca cada fila en un ámbito propio**, releyéndola. El de la vuelta guarda el
cerrojo, que es de su conexión, y lee el lote sin rastrearlo. Con el contexto de la vuelta
compartido, un fallo al guardar la marca habría dejado la fila publicada y con error a la vez.

Así se cierra el hueco que el ADR-0013 §6 dejó escrito: si el proceso se cae antes de confirmar, no
queda ni el efecto ni la huella, y el evento se vuelve a entregar entero.

### 2. El despachador recibe la fila, y vive en Infrastructure

`IDespachadorDeEventos` pasa de Application a Infrastructure y recibe la fila de la cola, no el
evento. Reconstruir el evento pasa del publicador al despachador. El motivo es el punto 3: la
empresa sale de la fila, y con un evento y una empresa sueltos quien llamara elegiría la empresa.
En Infrastructure ningún caso de uso lo alcanza, así que ninguno puede cambiar de empresa por este
camino. El despachador pasa a *singleton*, porque los ámbitos los abre él.

### 3. El manejador corre con la empresa de su fila

Dentro del ámbito de cada manejador, el despachador abre el de la empresa de su fila,
`InquilinoActual.ConLaEmpresaDelEvento`. Dentro, la empresa activa es esa, como la del *claim* en
una petición: el filtro global la usa, la traza y la respuesta la llevan, y escribir en otra la
para la guarda de la auditoría con `EscrituraEnOtraEmpresa`. Un ámbito sin inquilino que se abra
dentro funciona igual que en una petición.

**La R8 se sigue cumpliendo.** La empresa de la fila es la empresa desde la que se actuó (ADR-0013
§8), y esa salió del *claim* de quien actuó. El manejador no recibe una empresa nueva: recibe la de
la operación que causó su evento.

**Es `internal` y no está en `IInquilinoActual`.** Solo lo abre el despachador; ningún caso de uso
puede cambiar de empresa. Al cerrarse vuelve lo de fuera, que en la vuelta del publicador es el
ámbito sin inquilino de `PublicacionDeEventos`.

### 4. Una fila sin empresa no se entrega

Una fila escrita bajo un ámbito sin inquilino, como las de la semilla, no lleva empresa. Si su
evento tiene manejadores, el despachador lanza con su motivo, la fila cuenta un intento, y a la
quinta se aparca. Cero manejadores sigue siendo cero, con empresa o sin ella. Qué hacer con una así
se decide con el primer manejador que la necesite delante.

### 5. Quién lo causó: tres columnas de la fila

La fila lleva tres columnas nuevas, nulas porque las de antes del 3.4 no las tienen, con su
migración en Auditoría (`LaCausaDeCadaEvento`):

| Columna | Qué guarda |
|---|---|
| `usuario_id` | Quien causó el evento, si fue alguien con sesión. |
| `correlacion_id` | La correlación de la auditoría de la operación que lo escribió. |
| `contexto_de_traza` | El `traceparent` W3C de la actividad en la que se escribió: 55 caracteres exactos. |

Las llena el interceptor de la bandeja desde `CausaDelAmbito`, que es *scoped* y que también lee el
interceptor de la auditoría. El despachador las restaura en el ámbito de cada manejador con
`CausaDelAmbito.ConLaDelEvento`, `internal` por lo mismo que el ámbito de la empresa: ningún caso de
uso puede firmar en nombre de otro. El efecto se audita a nombre de quien causó el evento y con su
correlación, y la respuesta hereda las tres sin que el manejador copie nada, así que una cadena de
eventos se sigue entera desde la petición que la empezó. A una fila de antes del 3.4, sin
correlación, se le da una nueva, la misma para todos sus manejadores.

**La correlación de la auditoría pasa de una por `SaveChanges` a una por ámbito.** Era un
`Guid.CreateVersion7()` en cada guardado, y una operación con dos guardados dejaba dos
correlaciones. Con eso, la fila de la bandeja no habría podido llevar la de su operación: solo la de
su guardado. Ahora todo lo que escribe una operación, en uno o en varios `SaveChanges`, se junta
por ella.

### 6. La actividad del manejador es hija de la traza de la fila, no un enlace

Cada manejador abre una actividad `Consumer` de la fuente `Bastion.BandejaDeSalida`, con el
`traceparent` de la fila como padre remoto, los atributos `messaging.*` y el estado de error si el
manejador lanza. La API registra la fuente. La causa que hereda lo que escribe el manejador lleva la
traza de esa actividad.

**Hija, porque la pregunta de quien mira la traza es «¿qué pasó por culpa de esto?»**: la petición
que da de alta una empresa y lo que hacen después sus manejadores se leen como una sola traza. Un
enlace contesta a otra pregunta, la de un lote que junta varias causas, y la bandeja entrega de una
en una.

**Sin nadie escuchando no hay actividad**, y lo guardado es la traza de la fila: lo que se escribe
no depende de que haya un oyente.

### 7. Un choque de versión se reintenta en el acto, y no aparca nunca

Si el manejador choca con otra escritura —un `DbUpdateConcurrencyException`—, su transacción ya se
ha deshecho, y el despachador se lo vuelve a entregar con un ámbito y un contexto nuevos. El
contexto que chocó no se reutiliza: guarda lo que leyó antes del choque y volvería a chocar con lo
mismo.

- **Hasta tres reintentos**, `EventoDeLaBandeja.ReintentosPorChoque`.
- **La espera se dobla y lleva azar**: 50 ms por 2^(n−1), menos hasta la mitad al azar. Quedan en
  (25, 50], (50, 100] y (100, 200] ms, y como mucho 350 ms en total, menos que el intervalo de la
  vuelta, que es de dos segundos. El azar solo separa en el tiempo a los que chocan, así que el de
  `Random.Shared` basta. La espera va por el `TimeProvider` inyectado.
- **Cada reintento se anota** con el `EventId` 8201, en `Warning`.
- **Reintentar no duplica**, que es la condición que pone la convención de datos distribuidos: la
  transacción se ha deshecho, y la mirada a la huella va dentro.

Si sigue chocando, el choque sale del despachador y el publicador lo atrapa: la fila se queda
pendiente como estaba, sin intento y sin error, con una línea `EventId` 8307 en `Warning`, y la
vuelta siguiente lo vuelve a probar. Un manejador que chocara siempre, por un error suyo, no se
aparcaría: lo dirían ese registro y la edad del más viejo, que sube.

**Cualquier otro fallo cuenta como hasta ahora.** También una violación de un índice único, que es
un `DbUpdateException` y no un choque de versión: el caso del ADR-0059 de dos peticiones del mismo
origen sobre claves distintas cuenta un intento, y la vuelta siguiente encuentra la reserva.

**La captura del publicador cubre también la marca.** Un choque al marcar la fila es casi
imposible: la fila de la bandeja no tiene testigo de concurrencia, se relee con `Single`, y el
publicador tiene el cerrojo. Por eso no se guardó ninguna guarda de «ya despachado» para ese caso:
ningún caso la podría ejercer.

**La carrera de los tests es de verdad.** El manejador de prueba lee su documento por EF, y antes
de guardar abre otra conexión de Npgsql y le cambia la fila en otra transacción. Su `xmin` se
mueve, el `UPDATE … WHERE xmin = …` de EF Core no toca ninguna fila, y el guardado sale por
`DbUpdateConcurrencyException`, como saldría contra otra petición.

### 8. Cuántos aparcados hay sin resolver

Un indicador observable, `bastion.bandeja_de_salida.aparcados_sin_resolver`, de tipo `long` y en
`{evento}`. El publicador cuenta los aparcados de la base al leer cada lote, dentro de la misma
guarda que la lectura —así una base sin migrar se trata igual—, y por el índice de estado que ya
tenía la cola. Son los de todas las empresas, porque la vuelta corre sin inquilino. Lo que se
aparca en una vuelta lo cuenta la siguiente.

El contador de aparcados dice cuántos se han aparcado desde que arrancó el proceso, y se reinicia
con él: tras un despliegue marca cero aunque la cola tenga diez esperando a alguien. Este dice
cuántos hay, y no baja hasta que alguien resuelve uno. **La alerta va sobre este.** El valor lo
rellena el publicador, y el medidor solo lo lee, por lo mismo que el de la edad: la devolución de
llamada de un instrumento observable la recolecta el exportador en su hilo, y consultar la base
desde ahí lo bloquearía contra la red. Sigue sin haber sonda de la bandeja (ADR-0013 §9).

## Alternativas descartadas

- **Una conexión compartida en la que se enganchan todos los contextos del ámbito**, con
  `UseTransaction`. Es más general, y deja sin vigilar que un manejador escriba solo en su módulo.
- **Esperar siempre a la vuelta siguiente ante un choque.** Deja el evento detrás de los que vienen
  después en el lote.
- **Contar el choque como un fallo después de los tres reintentos.** Puede aparcar un documento muy
  disputado, que no tiene nada de malo.
- **Llevar la causa en `EventoDeIntegracion`, dentro del cuerpo**, con el manejador encargado de
  pasarla a su respuesta. Es una línea que alguien se olvida en el manejador catorce, y el contrato
  de cada evento pasaría a llevar datos que no son del hecho.
- **Un motivo nuevo, `ManejoDeEventos`**, para que el manejador corriera sin inquilino. Vería todas
  las empresas, que es justo lo que cierra el hueco 2.

## Lo que no cubre, dicho

- **Ningún manejador real.** El primero es el de la saga del 3.8, y su ADR se escribe en la puerta
  de la recepción.
- **Dentro de un manejador, `IUsuarioActual` no tiene token.** Su `EmpresaId` y su `UsuarioId`
  lanzan, y `Tiene` no concede nada. La empresa activa sí está, por `IInquilinoActual`, y quién lo
  causó, por la causa del ámbito. Un caso de uso que lea `IUsuarioActual` no se puede llamar
  desde un manejador tal cual. Se decide con el primer manejador real o en el ADR de la saga.
- **Un caso de uso que convirtiera un `DbUpdateConcurrencyException` en un `Resultado`** se saltaría
  el reintento en el acto: el manejador recibiría un fallo, y si lanza, contaría un intento. Hoy no
  lo hace ninguno: el choque lo traduce el borde HTTP, `ManejadorDeVersionObsoleta`, y los casos de
  uso lo dejan salir. Se decide con el primer manejador real.
- **El `EventId` 8200 lo usan dos registros**, el de un evento ya procesado del despachador y el de
  `AccesoALoBloqueado`. Es anterior al 3.4 y no se cambia aquí: renumerar un `EventId` cambia lo que
  filtran los paneles, y eso se decide aparte.
- **La retención de la bandeja** sigue pendiente, como dijo el ADR-0013.

## Consecuencias

- **Se acepta** un ámbito, un contexto y una transacción por manejador y por intento. La vuelta
  sigue siendo una sola y entrega de uno en uno, así que son dos conexiones a la vez: la de la
  vuelta, con el cerrojo, y la del manejador.
- **Se acepta** que un manejador que choque siempre no se aparque. Lo dicen la línea 8307 y la edad
  del más viejo.
- **Se acepta** que una fila sin empresa con manejadores acabe aparcada, con su motivo en el error.
- **La correlación de la auditoría es la de la operación**, no la del guardado. Lo que la lea para
  agrupar lee una operación entera, y el efecto de un manejador lleva la de la operación que causó
  su evento.
- **Un evento se sigue entero**, de la petición al efecto del último manejador, por la correlación
  en la auditoría y por la traza en el visor.

## Cómo se prueba

| Lo que se fija | Dónde |
|---|---|
| Un ámbito por manejador, la transacción única por el `xmin`, dos confirmaciones, el evento al que no se atiende | `ElManejadorVaEnSuTransaccionTests` |
| La empresa de la fila, lo que ve el manejador, la guarda, la fila sin empresa y su aparcamiento | `ElManejadorVaConLaEmpresaDeSuFilaTests` |
| La causa restaurada, la fila sin correlación, la actividad en verde y en error | `ElManejadorVaConQuienLoCausoTests` |
| La causa en una petición, y el alta por la API con su `traceparent` | `LaCausaDelAmbitoTests`, `ElAltaDeUnaEmpresaSePublicaTests` |
| Tres choques en el acto, la espera, el cuarto que sale, otro fallo, y el choque que no cesa | `UnChoqueDeVersionSeReintentaTests` |
| Los aparcados de la base, de todas las empresas, y su bajada | `LosAparcadosSinResolverSeCuentanTests` |

**El espía de medidas lee el medidor de su bandeja, y no el mayor de todos.** En el proceso de los
tests de integración está también el host de la API, con su propio publicador sobre la base
compartida, y los aparcados de esa base son los de cualquiera. El espía apunta los medidores que ya
estaban cuando empieza a escuchar, y lee solo el que nace después; si nacen dos, lo dice en vez de
elegir uno.
