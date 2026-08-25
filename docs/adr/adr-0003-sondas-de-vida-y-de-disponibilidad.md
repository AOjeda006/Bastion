---
tipo: referencia
stack: [dotnet, docker]
aplica_a: [csharp, aspnetcore]
revisado: 2026-08-25
tags: [adr, salud, healthcheck, docker, observabilidad, opentelemetry, serilog]
---

# ADR-0003: Dos sondas de salud con semántica distinta, y qué hace falta para que se puedan ejecutar

- **Estado:** aceptado
- **Fecha:** 2026-08-25

## Contexto

El ítem 0.2 pide que `docker compose up` levante el entorno y que **la API responda a su sonda de
vida**. El andamiaje ya había escrito, antes de que existiera una sola línea de la API, tres sitios
que dan por hechas dos rutas concretas: el `HEALTHCHECK` de `deploy/Dockerfile.api`, el `healthcheck`
del servicio `api` en `deploy/docker-compose.yml` y el `README.md`. Las rutas son `/health/live` y
`/health/ready`.

Al implementarlas aparecieron dos cuestiones que no son de estilo.

## Decisión

### 1. Dos sondas, y la de vida no mira ninguna dependencia

`/health/live` se publica con `Predicate = _ => false`: ejecuta **cero** comprobaciones y responde
200 si y solo si el proceso está en pie y atendiendo peticiones. `/health/ready` agrega solo las
comprobaciones etiquetadas `disponibilidad`, que hoy es una —PostgreSQL— y mañana serán más.

La tentación es tener una sola sonda «completa» y apuntar todo a ella. Es un error con consecuencia
concreta: el orquestador **reinicia** lo que falla la sonda de vida. Si esa sonda mirase la base de
datos, un corte de PostgreSQL se convertiría en un bucle de reinicios de la API — que no arregla la
base, tira las conexiones que sí quedaban y sustituye un `503 Service Unavailable` legible por un
servicio que nunca termina de arrancar. La sonda de disponibilidad, en cambio, solo saca la instancia
del balanceo, que es exactamente lo que se quiere mientras la dependencia vuelve.

Agregar **por etiqueta** y no por lista explícita tiene un motivo de plazo: R15 exige comprobar la
deriva del reloj del servidor, porque el reloj forma parte de la corrección fiscal. Cuando llegue esa
fase, añadirla será registrar una comprobación más con la etiqueta `disponibilidad`; no hay que tocar
el *endpoint*.

`/health/ready` escribe además un cuerpo JSON con el estado y la descripción **por comprobación**. El
formateador por omisión escribe `Unhealthy` a secas, que obliga a bucear en los registros para saber
qué ha fallado.

### 2. La imagen necesita un cliente HTTP, y las de .NET no lo traen

Las dos sondas del andamiaje invocaban `wget`. La imagen de ejecución es
`mcr.microsoft.com/dotnet/aspnet:10.0-noble`, que es **mínima**: no trae `wget`, ni `curl`, ni ningún
otro cliente HTTP. Su capa base (`runtime-deps`) instala lo justo para ejecutar .NET —
`ca-certificates`, `libicu`, `libssl`, `tzdata` y poco más.

Lo grave no es que falte, sino **cómo falla**: el `HEALTHCHECK` no puede ejecutarse, el contenedor no
pasa nunca de `starting`, y `web` —que declara `depends_on: api: condition: service_healthy`— no
arranca. No hay error que leer; hay un compose que se queda colgado hasta agotar el tiempo de espera.
Es el mismo patrón que el defecto del ADR-0002: **una configuración que no puede funcionar, fallando
en silencio en un sitio distinto de donde está escrita.**

Se instala `curl` en la etapa final (como `root`, volviendo después a `app`) y ambas sondas pasan a
`curl --fail --silent`. Es la opción aburrida; las alternativas —añadir un modo sonda al propio
ejecutable, o renunciar al `HEALTHCHECK` en la imagen— cuestan más y compran menos.

### 2 bis. Y la sonda apunta a `127.0.0.1`, no a `localhost`

Arreglado lo anterior, el *job* de humo seguía en rojo, ahora con `container bastion-web-1 is
unhealthy` ([run 32872440626](https://github.com/AOjeda006/Bastion/actions/runs/32872440626)), con
todo lo demás sano.

Dentro de un contenedor, `localhost` resuelve a **dos** direcciones: `127.0.0.1` y `::1`. El `wget`
de BusyBox se queda con la primera que le devuelve el sistema; `curl`, en cambio, prueba todas. Y
`nginx.conf` declara `listen 8080;` a secas, que es **solo IPv4**. Cuando la resolución devolvía
`::1` primero, la sonda daba «connection refused» contra un servidor que estaba sirviendo
perfectamente.

Las tres sondas —la de la imagen del frontal, la del frontal en compose y la de la API— pasan a
`127.0.0.1` explícito. `deploy/Dockerfile.web` no necesita instalar nada: es Alpine y su BusyBox sí
trae `wget`; lo que necesitaba era una dirección en vez de un nombre.

### 3. La instrumentación se registra siempre; el exportador, solo si hay a dónde exportar

Serilog escribe JSON compacto a consola —en contenedor, la salida estándar *es* el transporte de
logs— y OpenTelemetry instrumenta ASP.NET Core, `HttpClient` y el *runtime*. Pero el exportador OTLP
solo se registra si `OTEL_EXPORTER_OTLP_ENDPOINT` viene definido. Sin esa guarda, un `dotnet run` a
pelo o un test funcional convierten cada ciclo de exportación en un error de red repetido que ensucia
el registro sin aportar nada.

La correlación entre traza y registro **no** usa una cabecera propia. Serilog 4 arrastra el `TraceId`
y el `SpanId` de la actividad en curso y `CompactJsonFormatter` los escribe como `@tr` y `@sp`;
ASP.NET Core ya honra el `traceparent` de entrada. Un `X-Correlation-Id` propio sería un segundo
identificador que mantener y que ningún visor de trazas entiende.

Las sondas se **excluyen de las trazas** (se consultan cada pocos segundos y no cuentan nada) pero
**no de las métricas**, que son agregados y no un evento por petición.

## Consecuencias

- La sonda que usan el `HEALTHCHECK` de la imagen y el `healthcheck` del compose es la de **vida**.
  La de disponibilidad es para el balanceador o el orquestador, y **no** debe cablearse a un
  reinicio.
- Cualquier imagen futura que declare un `HEALTHCHECK` sobre una base de .NET tiene que instalar su
  cliente HTTP o quedarse sin sonda. Conviene recordarlo al escribir el `Dockerfile` de cualquier
  servicio nuevo.
- Añadir una dependencia a la sonda de disponibilidad es registrar una comprobación con la etiqueta
  `disponibilidad`. Añadirla a la de vida es, casi siempre, un error.

### 4. La CI tiene que poder explicar su propio fallo

Con el repositorio público, la API de Actions deja leer *runs*, *jobs* y pasos sin autenticación,
pero **`GET /actions/jobs/{id}/logs` devuelve 403**. La primera versión del *job* de humo era un
único `docker compose up --build`, así que su fallo llegó como `Process completed with exit code 1`
y nada más.

De ahí dos medidas que se quedan: los pasos van **partidos por fase** —validar, construir, levantar
PostgreSQL, levantar el resto—, de modo que la conclusión de cada paso ya localiza el fallo sin leer
una sola línea de log; y un paso `Diagnóstico` emite como **anotación** (`::error::`, que sí es
pública) el final de cada salida y, para cada servicio que no esté sano, su
`docker inspect --format '{{json .State.Health}}'`. Eso último es lo decisivo: la salida de una sonda
fallida **no** aparece en los registros del contenedor, la guarda el demonio.

## Aprendizaje transversal

Se repite el del ADR-0002, ahora en otra herramienta: **una configuración escrita antes que el código
al que apunta no está verificada, por muy razonable que se lea.** El `wget` de las dos sondas llevaba
escrito desde el andamiaje y nadie podía haberlo ejecutado nunca, porque no había API que sondear. Lo
que lo detecta no es releerlo: es ejecutarlo.

Y un corolario sobre el propio diagnóstico: entre el primer rojo y el verde hubo dos hipótesis
razonables y **falsas** —que fallaba la creación del clúster de PostgreSQL por la configuración ICU
en Alpine, y que fallaba la construcción de una imagen—, ambas descartadas en cuanto los pasos se
partieron por fase y PostgreSQL apareció sano en 9 s. Instrumentar para que el fallo se localice
solo cuesta menos que razonar sobre qué pudo pasar.
