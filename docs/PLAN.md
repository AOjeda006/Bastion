# PLAN — Bastion · Fase 0 (Cimientos)

> Plan de registro (**fuente de verdad del estado del trabajo**). Es lo que hace segura la
> resumabilidad: el agente lo lee al arrancar y tras cada `/compact`, y lo actualiza al avanzar.
> Mantenlo siempre coherente con la realidad del repo.
>
> La especificación del producto es **`ERP-PLAN-MAESTRO.md`** (fuera del repositorio, lo aporta el
> usuario). Este fichero **no la duplica**: solo lleva el estado del trabajo. Cuando cambie una
> decisión de producto, se cambia allí y se anota el porqué en un ADR de `docs/adr/`.

## Objetivo

Dejar la **fase 0 (Cimientos)** terminada: solución modular en .NET 10, bloques comunes, los módulos
Identidad, Organización y Auditoría, outbox transaccional, Docker Compose, CI y el shell de React con
login.

**Criterio de salida de la fase 0 (§15 del plan maestro):** un usuario inicia sesión, elige empresa y
opera el CRUD de almacenes viendo **solo** los datos de su empresa, con CI en verde y los tests de
arquitectura activos.

## Alcance / No-objetivos

- **Dentro:** los trece ítems del checklist de abajo (Anexo A.3 del plan maestro), y nada más.
- **Fuera:**
  - **Las fases 1 a 11.** Terceros, Catálogo, Inventario, Compras, Ventas, Facturación, Tesorería,
    Contabilidad, Producción, CRM, RRHH e Informes tienen ya su carpeta de módulo creada y vacía;
    **no se empiezan ahora**, ni "de paso" ni "para dejarlo encaminado".
  - **Editar `../BibliotecaDocumentacion`.** Es de solo lectura durante todo el proyecto. Los
    aprendizajes transversales se dejan como ADR en `docs/adr/`; la destilación a la biblioteca es
    lo último, al terminar el proyecto.
  - **El workflow de despliegue (`cd.yml`).** Solo integración continua por ahora; el despliegue se
    decide cuando haya algo que desplegar (§16 del plan maestro lo deja aplazado).

## Decisiones tomadas

> Cada respuesta del usuario a una pregunta de clarificación se anota aquí (fecha + decisión), para
> que sobreviva a `/compact` y no se vuelva a preguntar.

### Heredadas del plan maestro (no se reabren — Anexo A.4)

El stack (§2 y §3), el estilo arquitectónico (§4), el mapa de módulos (§5), las diecisiete reglas
duras (§6), el modelo de dominio (§7) y el orden de las fases (§15) **están cerrados**. Si algo
resulta inviable al montarlo, se **pregunta**; no se cambia por iniciativa propia.

### Tomadas por el agente de configuración (2026-08-25)

Ninguna de estas toca lo cerrado en A.4. Todas son reversibles y están aquí para no volver a
discutirlas.

- **2026-08-25 — El repositorio es `AOjeda006/Bastion`, no `AOjeda006/bastion-erp`.** El Anexo A.1
  nombra `bastion-erp`, pero el repositorio que existe y al que se tiene acceso de escritura se llama
  `Bastion`. Se ha montado ahí. Consecuencia práctica: **ninguna**, porque los imports de `CLAUDE.md`
  son relativos a la raíz del proyecto (`@../BibliotecaDocumentacion/…`) y no dependen del nombre de
  la carpeta. Solo afecta a la URL de clonado del `README.md`. Si el usuario prefiere el nombre del
  anexo, renombrar el repositorio en GitHub es una operación de un minuto y no rompe nada de lo aquí
  montado.
- **2026-08-25 — El *workflow* de CI vive en `.github/workflows/`, no en `deploy/.github/workflows/`.**
  El §12 del plan maestro lo dibuja dentro de `deploy/`, pero GitHub Actions **solo** ejecuta lo que
  encuentra en `.github/workflows/` de la raíz del repositorio: en `deploy/` sería un fichero muerto.
  El resto de `deploy/` (compose y Dockerfiles) sí sigue el §12.
- **2026-08-25 — `docker-compose.yml` se queda en `deploy/`**, como dice el §12. Por tanto **todos**
  los comandos llevan `-f deploy/docker-compose.yml`; están escritos así en `AGENTS.md` y en el
  `README.md`. No se añade un segundo compose en la raíz: dos ficheros que se desincronizan es peor
  que un comando más largo.
- **2026-08-25 — Gestión centralizada de versiones de paquetes** (`Directory.Packages.props` con
  `ManagePackageVersionsCentrally`). Es el mecanismo que hace cumplir el "una sola versión por
  solución" de `stacks/dotnet/convenciones.md` en una solución que va a tener del orden de setenta
  proyectos. El fichero está creado y **vacío a propósito**: cada paquete que se adopte añade ahí su
  `PackageVersion`, y los `.csproj` lo referencian **sin** atributo `Version`.
- **2026-08-25 — `InvariantGlobalization` explícitamente en `false`** en `Directory.Build.props`.
  Es lo contrario del valor que traen varias plantillas de contenedor de .NET, y es deliberado: un
  ERP español formatea importes y fechas en `es-ES` y ordena texto con acentos y `ñ`. Con
  globalización invariante eso se rompe **en silencio**, y el síntoma aparece en un informe, no en un
  test.
- **2026-08-25 — Los tests relajan las reglas de documentación XML.** `tests/Directory.Build.props`
  apaga `GenerateDocumentationFile` y `CS1591`. Documentar cada método de test con `///` es ruido; la
  regla de `stacks/csharp/convenciones.md` habla de la **API pública**.
- **2026-08-25 — Versiones de las herramientas del frontal fijadas por rango mayor** en
  `frontend/package.json`, con `package-lock.json` commiteado. El *lockfile* es lo que hace la
  construcción reproducible; el rango permite parches de seguridad sin un commit por chincheta.

### Tomadas por el agente de desarrollo — ítem 0.1 (2026-08-25)

Respuestas del usuario a la puerta de clarificación, y decisiones triviales o reversibles que se
anotan para no volver a discutirlas.

**Respondidas por el usuario:**

- **Modo git: `commit+push`.** Commits firmados (SSH, `commit.gpgsign=true`) y **solo** con las
  credenciales del usuario: ningún `Co-Authored-By` ni trailer de sesión. El aviso de arranque sobre
  `SIGNING_KEY_B64` se refiere al entorno de nube y **no aplica en local**: la firma estaba activa y
  el primer commit verificó `G` (*Good signature*).
- **Alcance de la solución: solo los proyectos de la fase 0 — 19 de los 74 posibles.** `Bastion.Api`,
  los tres bloques comunes, y las cinco capas de Identidad, Organización y Auditoría. Los otros once
  módulos (fases 1-11) conservan su carpeta con `.gitkeep` y **cada fase crea los suyos al empezar**.
  Motivo: «no adelantes fases», y no pagar `restore`/`build`/`format`/CI sobre cincuenta y cinco
  ensamblados vacíos durante meses. Consecuencia: cada fase futura añade sus proyectos al `.sln`.
- **`ERP-PLAN-MAESTRO.md` vive en la raíz del repositorio pero NO se versiona.** Se renombró desde
  `ERPPLANMAESTRO.md` al nombre canónico que usan `CLAUDE.md` y `README.md`, y se añadió a
  `.gitignore`. Los dos ficheros dicen que la especificación la aporta el usuario **fuera del
  repositorio**; ignorarlo cumple eso y a la vez lo deja a mano en local. Si se prefiere versionarlo,
  basta con quitar la línea del `.gitignore`.

**Decididas por el agente (triviales o reversibles):**

- **Rama por unidad de trabajo, y `main` avanza por *fast-forward*.** `principios/git-workflow.md`
  prohíbe trabajar sobre `main`, pero el usuario no quiere PR. El apaño: se trabaja en
  `feature/<item>`, se verifica en verde y solo entonces `main` avanza sin *merge commit*. Así `main`
  nunca ve trabajo a medias y el historial sigue siendo lineal y legible para retomar.
- **La solución se crea con `--format sln` explícito.** El SDK de .NET 10 crea `.slnx` por omisión y
  eso dejaría el *job* `backend` de la CI saltándose todos sus pasos para siempre. Está en
  **`docs/adr/adr-0001-formato-de-fichero-de-solucion.md`** porque el aprendizaje es transversal.
- **Los bloques comunes se llaman `Bastion.BuildingBlocks.<Capa>`.** Las carpetas del §12 son
  `src/BuildingBlocks/{Domain,Application,Infrastructure}` (sin prefijo, al contrario que las de
  módulo); el nombre del proyecto se deriva de la ruta y respeta la raíz `Bastion` del Anexo A.1.
- **`Contracts` no referencia nada, ni siquiera los bloques comunes.** Es lo único que un módulo
  puede ver de otro (§4, regla 1): si arrastrase `Domain`, cualquier módulo vería el dominio ajeno
  por transitividad y la frontera sería decorativa.
- **`Endpoints` es una biblioteca de clases con `FrameworkReference Microsoft.AspNetCore.App`**, no
  un proyecto de SDK Web. El host es uno solo (`src/Api`); los módulos solo aportan controladores. Al
  ser referencia al framework compartido y no un paquete, no lleva versión ni entra en
  `Directory.Packages.props`.
- **`Bastion.Api` referencia dos capas de cada módulo de fase 0**, y por motivos distintos:
  `Endpoints` para que el host descubra los controladores, e `Infrastructure` para registrar su
  `DbContext` y sus adaptadores. Es el *composition root*: el único sitio donde eso está permitido.
- **`RestorePackagesWithLockFile` activado** en `Directory.Build.props`. No es adorno: la CI usa
  `cache-dependency-path: '**/packages.lock.json'` y `actions/setup-dotnet` **falla** si ese patrón
  no casa con ningún fichero. De paso fija también las versiones transitivas, que la gestión
  centralizada por sí sola no cubre. Los 19 `packages.lock.json` se commitean.
- **`Directory.Packages.props` sigue vacío: el 0.1 no adopta ni un solo paquete de NuGet.** Un host
  mínimo y diecinueve bibliotecas sin código no necesitan ninguno, y ASP.NET Core entra por
  referencia al framework. El primer `PackageVersion` llegará cuando el 0.2 o el 0.3 adopte algo — y
  entonces se comprueba su licencia primero.
- **Todavía no hay proyectos de test.** El 0.1 no crea comportamiento: no hay nada que probar, y TDD
  exige el test *antes* del código, no un proyecto vacío antes del test. `tests/Arquitectura.Tests/` y
  `tests/Api.FunctionalTests/` siguen como carpeta con `.gitkeep`; los `<Modulo>.UnitTests` y
  `<Modulo>.IntegrationTests` del §12 nacen con su primera prueba (0.3 en adelante; el de
  arquitectura, en el 0.12). `dotnet test` sobre una solución sin proyectos de test devuelve 0:
  comprobado en los dos filtros, no hay rojo ni verde falso.
- **Discrepancia del plan maestro, resuelta a favor del §5:** el árbol del §12 no lista **Auditoría**
  bajo `src/Modules/`, pero el §5 la asigna a la fase 0 y el Anexo A.3 le dedica el ítem 0.7. La
  carpeta ya existía; se le han creado sus cinco proyectos. Es una omisión del árbol del §12, no una
  decisión.
- **Los `.gitkeep` de las diecinueve carpetas que ya tienen `.csproj` se han borrado.** Existían para
  que git conservara la carpeta vacía; con un proyecto dentro sobran. Quedan 60, los de las carpetas
  que siguen vacías.

### Tomadas por el agente de desarrollo — ítem 0.2 (2026-08-25)

- **El criterio del 0.2 se verifica con un *job* de humo en la CI, no en local** (decisión del
  usuario). **Docker no está instalado en la máquina de desarrollo** —ni en el `PATH` de PowerShell
  ni en el de Git Bash, sin `C:\Program Files\Docker`, sin el servicio `com.docker.service`—, así
  que aquí `docker compose up` no se puede ejecutar. El *job* `Humo` levanta el compose entero en un
  *runner* limpio y comprueba las cuatro cosas del criterio por su efecto: `/health/live` responde
  `Healthy`, `/health/ready` ve PostgreSQL, el frontal sirve su `index.html` y Jaeger conoce el
  servicio `bastion-api`. Ventaja sobre comprobarlo en local: se comprueba **siempre**, y no «la vez
  que lo probé».
- **La API se instrumenta ya, en el 0.2** (decisión del usuario), en lugar de dejarlo para más
  adelante: Serilog para el registro estructurado y OpenTelemetry para trazas y métricas. El
  `docker-compose.yml` ya trae recolector y visor, y una observabilidad que se añade después obliga a
  reescribir el arranque cuando ya hay módulos colgando de él.
- **Dos sondas con semántica distinta, y la de vida no mira NADA.** `/health/live` se publica con
  `Predicate = _ => false`: cero comprobaciones, 200 si y solo si el proceso atiende peticiones.
  `/health/ready` agrega las comprobaciones etiquetadas `disponibilidad`. Como agrega por etiqueta,
  añadir la deriva del reloj de R15 en su fase será una línea, no un rediseño.
- **La correlación entre traza y registro sale del estándar, no de una cabecera propia.** Serilog 4
  arrastra el `TraceId` y el `SpanId` de la actividad en curso y `CompactJsonFormatter` los escribe
  como `@tr` y `@sp`; ASP.NET Core ya honra `traceparent` de entrada. No se inventa ningún
  `X-Correlation-Id`: sería un segundo identificador que mantener, y peor, uno que los visores de
  trazas no entienden.
- **El exportador OTLP solo se registra si hay recolector configurado.** Sin esa guarda, un
  `dotnet run` a pelo o un test funcional convierten cada intento de exportar en un error de red
  repetido que ensucia el registro. La instrumentación se registra siempre; lo condicional es a
  dónde se manda.
- **La comprobación de PostgreSQL vive en `BuildingBlocks.Infrastructure`, no en `src/Api`.** El
  *composition root* **cablea** componentes; no los aloja. `ComprobacionDeBaseDeDatos` solo depende
  de las *abstracciones* de sondas y de Npgsql, así que ese proyecto sigue sin conocer ASP.NET Core.
  Ejecuta `SELECT 1` con margen propio de 3 s: mirar solo si el puerto acepta conexiones daría
  «sano» con una base recuperándose.
- **`/health/ready` responde JSON con el detalle por dependencia**, no el `Unhealthy` a secas que
  escribe el formateador por omisión. Un cuerpo que dice qué comprobación ha fallado y por qué es la
  diferencia entre diagnosticar y bucear en los registros.
- **Primer proyecto de test: `tests/Api.FunctionalTests`, por el camino VSTest.** xUnit 2.9.3 +
  `xunit.runner.visualstudio` 3.1.4 + `Microsoft.NET.Test.Sdk` 17.14.1 — que es exactamente lo que
  genera `dotnet new xunit` con el SDK de `global.json`, **no** lo último de nuget.org. Las versiones
  mayores (xunit v3, Test.Sdk 18) son el camino Microsoft.Testing.Platform, con otra gramática de
  filtros que rompería el `--filter "Category!=Integracion"` y el `--logger trx` de la CI. Aserciones
  con **Shouldly** (BSD-3-Clause), no FluentAssertions 8 (comercial desde 2025).
- **`CA1707` (nada de guiones bajos) silenciado SOLO bajo `tests/`.** La convención de nombres de
  test es `Metodo_Escenario_ResultadoEsperado`, y el `.editorconfig` ya lo declaraba para la regla de
  *estilo*; faltaba hacerlo para el analizador, que sí rompe el build. En `src/` sigue vigente.
- **Registro en JSON compacto también en desarrollo.** Un registro que se lee distinto en local que
  en producción deja de ser el que se depura de verdad. Las sondas se registran a nivel `Verbose`
  para que consultarlas cada pocos segundos no ahogue la salida.
- **Sin `appsettings.json`.** La configuración llega por entorno, que es lo que hace el compose. Un
  fichero de ajustes con valores por omisión duplicados es una fuente de verdad de más.
- **Paquetes del framework fijados en `10.0.9`**, que es el runtime de ASP.NET instalado aquí. Un
  paquete más nuevo que el runtime es el sentido que rompe; al revés rueda hacia delante.

## Estado actual

**Ítem 0.2 TERMINADO. Siguiente: ítem 0.3 (bloque común / *shared kernel*), sin empezar.**

`src/Api/Program.cs` deja de ser un host vacío: publica las dos sondas del §14, registra con Serilog
en JSON compacto y exporta trazas y métricas por OTLP al recolector. `BuildingBlocks.Infrastructure`
estrena su primer componente (`Salud/ComprobacionDeBaseDeDatos`) y la solución su **primer proyecto
de test**, `tests/Api.FunctionalTests` — **20 proyectos**. Con eso, `dotnet test` deja de ser el
verde vacío que era en el 0.1: ejecuta **3 casos** y los pasa.

Verificado en local, con la salida real de cada comando:

- `dotnet build` en Debug y en Release, `dotnet format --verify-no-changes` y
  `dotnet restore --locked-mode` → **0 advertencias / 0 errores**, `rc=0` los tres.
- `dotnet test --filter "Category!=Integracion"` → **3 superadas, 0 con error**. Antes de escribir el
  código esos mismos 3 casos fallaban (404 donde se esperaba 200 y 503): el rojo fue real.
- `dotnet test --filter "Category=Integracion"` → `rc=0` diciendo «Ninguna prueba coincide con el
  filtro»: un no-op **declarado**, no un verde mudo.
- API arrancada de verdad (`dotnet run` + `curl`): `/health/live` → `200 Healthy`; `/health/ready` →
  `503` con el detalle por dependencia. En el registro, cada evento con su `@tr` y su `@sp`, la sonda
  de vida sin generar línea y el 503 subido a nivel `Error`.

Y verificado donde importa, en la CI — **[run 32873076287](https://github.com/AOjeda006/Bastion/actions/runs/32873076287), los cuatro *jobs* en verde**:
`Frontal` ✅, `Backend` ✅, `Imágenes` ✅ y `Humo` ✅. Los cuatro comprobantes del criterio, cada uno
su propio paso: `/health/live` devuelve `Healthy`; `/health/ready` responde `Healthy` contra un
PostgreSQL de verdad; el frontal sirve su `index.html`; y Jaeger conoce el servicio `bastion-api`,
que es la prueba de que la instrumentación exporta y no solo está declarada en un `.csproj`.

Costó **tres *runs***, y los dos primeros están contados en *Notas / riesgos*: el andamiaje traía dos
sondas escritas con `wget` que no podían funcionar, por dos motivos distintos y en dos imágenes
distintas.

Del ítem 0.1: `Bastion.sln` compila con los 19 proyectos de entonces (`Bastion.Api`, los tres
bloques comunes y las cinco capas de Identidad, Organización y Auditoría), con las referencias de proyecto ya cableadas
según el §4. Verificado con la batería completa de `AGENTS.md` en verde — `dotnet build` en Debug y
en Release (0 advertencias / 0 errores), `dotnet format --verify-no-changes`, `dotnet test` en sus
dos filtros, y `npm run typecheck | lint | format:check | test | build`.

A partir de ahora **el *job* `backend` de la CI se ejecuta de verdad**: su condición
`if [ -f Bastion.sln ]` ya se cumple, sin tocar el *workflow*. Un fallo suyo es un fallo real.

**CI en verde de punta a punta desde `801837d`** — [run 32866580496](https://github.com/AOjeda006/Bastion/actions/runs/32866580496):
`Frontal` ✅, `Backend` ✅ e `Imágenes` ✅. El *job* `Imágenes` corrió por primera vez (antes se
saltaba porque `hashFiles('Bastion.sln')` estaba vacío) y construyó `Dockerfile.api` y
`Dockerfile.web` sin tocarlos. En aquel *run* `dotnet test` salía 0 **sin ejecutar nada**, porque no
había proyectos de test: no era evidencia. Desde el 0.2 sí ejecuta casos.

**Dónde retomar exactamente:** ítem **0.3**, el bloque común. Criterio: objeto de valor
`Importe(cantidad, divisa)` con la regla de redondeo de R6 probada, resultado de operación y
`ProblemDetails` según RFC 9457. Es el primer ítem con **dominio de verdad**, así que es el primero
donde el TDD del contrato manda: el test antes que el código, sin excepción. El sitio es
`src/BuildingBlocks/Domain` (hoy vacío y sin una sola referencia, que es justo lo que debe ser) y su
proyecto de tests hay que crearlo — `tests/` solo tiene `Api.FunctionalTests` y la carpeta vacía
`Arquitectura.Tests`.

### Lo que el agente de configuración NO pudo dejar hecho, y por qué

Explica por qué faltaban cosas que parecerían obvias. Los puntos **1 y 2 ya están
resueltos** por el ítem 0.1 y se conservan por trazabilidad; **3 y 4 siguen vigentes**.

1. ~~**No existe `Bastion.sln` ni ningún `.csproj`.**~~ **RESUELTO en el 0.1** — existen la
   solución y 19 proyectos (los de la fase 0). El entorno donde se montó esto **no tenía el SDK
   de .NET**, así que no había forma de comprobar que la solución compilaba. La regla era explícita:
   un esqueleto que no compila es peor que ninguno. Están las **carpetas** del §12 (una por módulo y
   capa, con el nombre del proyecto ya puesto: `src/Modules/Ventas/Bastion.Ventas.Domain/`), los
   `.gitkeep` y toda la configuración de MSBuild que los proyectos heredarán, que es exactamente
   de lo que partió el 0.1.
2. ~~**La CI está escrita pero su mitad de backend no se ejecuta todavía.**~~ **RESUELTO en el
   0.1** — al existir `Bastion.sln`, el *job* compila, formatea y ejecuta tests de verdad, sin que
   se tocara el *workflow*. Mientras `Bastion.sln` no existía, el *job* se saltaba los pasos y lo
   declaraba en el resumen del *run*: ni rojo ni verde falso, sino una CI diciendo lo que no había
   podido comprobar. **A partir de ahora un fallo suyo es un fallo de verdad.** El *job* `frontend`
   corría de verdad desde el primer commit.
3. **El frontal es solo la cadena de herramientas.** React 19, TypeScript estricto, Vite, ESLint
   (con `react-hooks` y `jsx-a11y`), Prettier, Vitest y Tailwind v4, con la estructura de carpetas del
   §10 y un `App.tsx` de relleno que no hace nada. **No hay enrutador, ni caché de servidor, ni
   formularios, ni i18n, ni cliente de API:** todo eso es el ítem 0.11, y sus librerías se instalan
   cuando se use cada una, no antes. El `App.tsx` de relleno **se sustituye** en el 0.11; no se
   construye encima de él.
4. **`db/migraciones/` y `db/semillas/` están vacías.** Las migraciones las genera EF Core (una por
   módulo, cada `DbContext` con su propio historial); las semillas (PGC, tipos de IVA, unidades,
   países) son **configuración**, no esquema, y no van dentro de una migración.

## Checklist

> Un ítem = una unidad de trabajo pequeña, con criterio de aceptación verificable.
> `[ ]` pendiente · `[x]` hecho (cumple criterio + verificado + commiteado si procede).
>
> Literal del **Anexo A.3** del plan maestro. No se reordena ni se amplía: si algo falta, se propone.

- [x] **0.1 · Andamiaje del repositorio y solución modular** — criterio de aceptación: `Bastion.sln`
  compila; la estructura coincide con el §12; `dotnet build` y `npm run build` en verde.
- [x] **0.2 · `docker compose up`** — criterio de aceptación: levanta PostgreSQL, API, frontal y
  observabilidad; la API responde a su sonda de vida y el frontal carga.
  Verificado en el *job* `Humo` de la CI — [run 32873076287](https://github.com/AOjeda006/Bastion/actions/runs/32873076287).
- [ ] **0.3 · Bloque común (*shared kernel*)** — criterio de aceptación: objeto de valor
  `Importe(cantidad, divisa)` con la regla de redondeo de R6 probada; resultado de operación;
  `ProblemDetails` según **RFC 9457**.
- [ ] **0.4 · Módulo Organización** — criterio de aceptación: CRUD de `Empresa`, `Ejercicio`, `Serie`
  y `Almacen`, con migraciones propias del módulo.
- [ ] **0.5 · Módulo Identidad** — criterio de aceptación: registro y login; roles y permisos por
  acción; pertenencia a empresas; el identificador de empresa viaja en el *claim*.
- [ ] **0.6 · Filtro global multiempresa (R8)** — criterio de aceptación: un test demuestra que una
  consulta sin filtro explícito **no** devuelve datos de otra empresa, y que el identificador del
  cuerpo de la petición se ignora.
- [ ] **0.7 · Módulo Auditoría** — criterio de aceptación: tabla *append-only* de quién cambió qué;
  un cambio en un maestro deja su rastro.
- [ ] **0.8 · Outbox transaccional (R12)** — criterio de aceptación: un evento y su escritura de
  negocio caen en la misma transacción; el trabajo de fondo lo publica; reprocesar no duplica.
- [ ] **0.9 · Idempotencia (R10) y concurrencia optimista (R11)** — criterio de aceptación: la misma
  `Idempotency-Key` devuelve el mismo recurso; `If-Match` obsoleto → **412**; estado incorrecto →
  **409**; sin cabecera → **428**.
- [ ] **0.10 · Estados `Bloqueado` y fechas de R14–R17 en el modelo base** — criterio de aceptación:
  el tipo base de entidad y las direcciones ya nacen con lo que exigen R14, R16 y R17 — no se añade
  después.
- [ ] **0.11 · Shell de React** — criterio de aceptación: login, selector de empresa, layout, rutas
  protegidas y cliente de API **generado desde el OpenAPI**; cambio de ruta accesible (`<title>`,
  `role="status"`, foco).
- [ ] **0.12 · Tests de arquitectura** — criterio de aceptación: NetArchTest con las reglas de
  frontera del §4; **fallan** si un módulo cruza una frontera.
- [ ] **0.13 · Integración continua** — criterio de aceptación: *workflow* que compila, pasa linter,
  tests de dominio, tests con Testcontainers y tests de arquitectura; verde de punta a punta.

## Imports pendientes de `CLAUDE.md`

`CLAUDE.md` ya trae el **núcleo permanente** (A.2.1) y los de **fase 0** (A.2.2). Los de abajo
**no están puestos a propósito**: cada import cuesta contexto en **cada** turno, así que se añaden
al empezar su fase y se quedan (Anexo A.2.3).

| Al empezar la fase | Añadir a `CLAUDE.md` |
|---|---|
| **1 · Maestros** | `@../BibliotecaDocumentacion/herramientas/proteccion-datos.md`<br>`@../BibliotecaDocumentacion/patrones/soft-delete.md` |
| **2 · Inventario** | `@../BibliotecaDocumentacion/negocio/identificacion-articulos/convenciones.md` |
| **5 · Facturación** | `@../BibliotecaDocumentacion/negocio/facturacion-espana/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/verifactu/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/iva-espana/convenciones.md` |
| **6 · Tesorería** | `@../BibliotecaDocumentacion/negocio/pagos-y-cobros/convenciones.md` |
| **7 · Contabilidad** | `@../BibliotecaDocumentacion/negocio/contabilidad/convenciones.md` |

Los `referencia.md` **no se importan nunca**: son para que los lea una persona. Se consultan a mano
cuando hace falta el porqué.

> **Aviso para la fase 5:** `negocio/verifactu/convenciones.md` no es material de consulta, es
> **lectura obligatoria entera antes de la primera línea** de esa fase.

## Notas / riesgos

- **`autoCompactWindow: 300000` en `.claude/settings.json` solo sirve si el agente corre un modelo de
  ventana de 1M.** En un modelo de 200K no hace nada útil: la frontera por omisión ya está donde toca
  y bajarla solo quita sitio. Se deja el valor de la plantilla; si el agente local es de 200K,
  **quita la línea**. Para afinarlo: mide con `/context` lo que cuesta una tarea típica (`U`) y deja
  la ventana en ≈ `2·U`.
- **ARREGLADO (2026-08-25) · `nuget.config` mandaba la caché de paquetes dentro del repositorio, y
  tumbó la CI el mismo día.** El bloque `<config>` fijaba `globalPackagesFolder` a
  `%NUGET_PACKAGES%`; con la variable sin definir, NuGet no falla: toma el literal y lo resuelve
  relativo al propio fichero. **La predicción anterior de esta nota era falsa**: decía que no había
  mordido y que mordería «con el primer `PackageVersion`, en el 0.2 o el 0.3». Mordió en el primer
  *run* con solución, sin ningún paquete adoptado — [run 32845328258](https://github.com/AOjeda006/Bastion/actions/runs/32845328258):

  ```
  Post .NET: Cache folder path is retrieved for .NET CLI but doesn't exist on disk:
             /home/runner/work/Bastion/Bastion/%NUGET_PACKAGES%
  ```

  Quien toca esa carpeta no es `restore`, es el **post-step** de `actions/setup-dotnet`, que la pide
  para guardar la caché. Por eso el *job* `Backend` tuvo **sus doce pasos reales en verde y acabó en
  rojo**. Arreglado en dos mitades, porque borrar el `<config>` **no bastaba**: los 19
  `packages.lock.json` solo tienen entradas `"type": "Project"`, así que la carpeta no se crea ni en
  la ruta correcta. Se borró el bloque **y** se añadió `mkdir -p ~/.nuget/packages` a la CI. Detalle
  en **`docs/adr/adr-0002-cache-de-paquetes-y-post-step-de-la-ci.md`**. El `mkdir` se retira en el
  0.13, no antes.
- **ARREGLADO (2026-08-25) · el presupuesto de tamaño del frontal medía el sourcemap.** El paso
  «Presupuesto de tamaño» hacía `du -sk dist` con tope 1024 kB. En el *runner* dio **1104 kB** y puso
  el *job* `Frontal` en rojo con todo lo demás en verde — [run 32845328258](https://github.com/AOjeda006/Bastion/actions/runs/32845328258):
  `El frontal supera el presupuesto de tamaño (1104 kB > 1024 kB)`. (En disco local daban 1097 kB: el
  `du` del *runner* redondea distinto, así que **la cifra local no es la que decide**.) El culpable no
  era el *bundle* sino los ~911 kB del *sourcemap*, que el navegador no descarga. La comprobación
  contradecía su propio comentario, que habla del «arranque del usuario». Arreglado midiendo
  `du -sk --exclude='*.map' dist`.
- **El presupuesto del frontal ha quedado flojo, y hay que apretarlo en el 0.11.** Con el
  `--exclude='*.map'`, lo que el navegador descarga son ~205 kB contra un tope de 1024 kB: un margen
  de cinco veces no señala nada. **Recomendación:** al terminar el shell de React (0.11), cuando el
  frontal tenga enrutador, caché de servidor, formularios, i18n y cliente de API, medir lo que pesa
  de verdad y dejar el tope en ese valor más un margen corto (~20 %). Un presupuesto que nunca puede
  saltar es peor que no tenerlo: ocupa el sitio del que sí avisaría.
- **`sourcemap: true` publica el código fuente del frontal a cualquiera que abra las herramientas de
  desarrollo.** `frontend/vite.config.ts` fija `build.sourcemap: true` y `deploy/Dockerfile.web` copia
  el `dist` entero, *sourcemaps* incluidos, a la imagen que sirve nginx. No es un problema hoy —no hay
  nada que proteger en un `App.tsx` de relleno— pero sí lo será el día que el frontal lleve lógica de
  negocio, nombres de endpoints internos y reglas de permisos. **Recomendación:** `sourcemap: 'hidden'`,
  que genera el mapa pero **no** deja el comentario `//# sourceMappingURL=` que lo enlaza: el navegador
  no lo pide y un rastreador de errores sí puede consumirlo si algún día se sube. **No se toca ahora**
  (fuera del alcance); es decisión del 0.11 o del despliegue, y conviene tomarla antes de que la imagen
  se publique en algún sitio.
- **ARREGLADO (2026-08-25) · las sondas del andamiaje no podían ejecutarse: las imágenes de .NET no
  traen ningún cliente HTTP.** Tanto el `HEALTHCHECK` de `deploy/Dockerfile.api` como el del servicio
  `api` en `docker-compose.yml` invocaban `wget`, y `mcr.microsoft.com/dotnet/aspnet:10.0-noble` es
  una imagen mínima: sin `wget`, sin `curl`, sin nada. El síntoma habría sido **mudo** —el contenedor
  quedándose en `starting` para siempre, y `web` sin arrancar por su `depends_on: service_healthy`—,
  no un error legible. Arreglado instalando `curl` en la etapa final (como `root`, volviendo a `app`)
  y usando `curl --fail --silent` en ambas sondas. Detalle, junto con el diseño de las dos sondas, en
  **`docs/adr/adr-0003-sondas-de-vida-y-de-disponibilidad.md`**.
- **ARREGLADO (2026-08-25) · la sonda del frontal apuntaba a `localhost`, y eso no es una dirección.**
  Segundo defecto de la misma familia, en la otra imagen. Con el `curl` de la API ya puesto, el *job*
  `Humo` seguía en rojo — [run 32872440626](https://github.com/AOjeda006/Bastion/actions/runs/32872440626):
  `postgres`, `api`, `otel-collector` y `jaeger` sanos, y `container bastion-web-1 is unhealthy`.
  Dentro de un contenedor, `localhost` resuelve a **127.0.0.1 y a ::1**; el `wget` de BusyBox se queda
  con la primera dirección que le devuelve el sistema, y `nginx` hace `listen 8080` a secas, que es
  **solo IPv4**. Cuando salía `::1`, la sonda daba «connection refused» contra un servidor
  perfectamente vivo. El `curl` de la API no lo sufría porque prueba todas las direcciones. Las tres
  sondas pasan a `127.0.0.1` explícito.
- **Los logs de un *run* de Actions NO son públicos; las anotaciones sí.** Con el repositorio público,
  `GET /actions/runs/{id}` y `/jobs` se leen sin autenticación, pero `/jobs/{id}/logs` devuelve **403**.
  La primera versión del *job* `Humo` era un solo `up --build`, así que su fallo llegó como un
  `Process completed with exit code 1` y nada más — imposible de diagnosticar sin bajarse el log.
  Arreglado en el propio *workflow*, y conviene mantenerlo así: los pasos están **partidos por fase**
  (validar / construir / levantar PostgreSQL / levantar el resto), de modo que la conclusión de cada
  paso ya localiza el fallo, y el paso `Diagnóstico` emite como **anotación** (`::error::`) el final de
  cada salida y, para cada servicio que no esté sano, su `docker inspect .State.Health` — que es lo
  único que dice por qué falla una sonda, porque no aparece en los registros del contenedor.
- **`Dockerfile.api` tenía que copiar `tests/` entero, no solo su `Directory.Build.props`.** Desde
  que la solución referencia un proyecto de test, `dotnet restore Bastion.sln` dentro de la imagen
  falla si no encuentra su `.csproj`. Se restaura la solución **completa** a propósito: así la imagen
  resuelve el mismo grafo de paquetes que la CI y no pueden divergir en silencio.
- **`Imágenes` y `Humo` construyen las mismas dos imágenes, cada uno por su lado.** `Imágenes` usa
  `docker/build-push-action` con caché de *runner*; `Humo` las reconstruye vía compose porque
  `load: false` no las deja en el demonio. Son unos minutos de más por *run*. **Recomendación:**
  consolidarlo en el **0.13**, que es el ítem de integración continua — o bien `Humo` absorbe a
  `Imágenes`, o `Imágenes` publica con `load: true` y `Humo` reutiliza. No se toca ahora: el 0.13
  tiene que rehacer el *workflow* de todas formas y hacerlo dos veces sería trabajo tirado.
- **Docker no está instalado en la máquina de desarrollo.** Ni `docker compose` ni Testcontainers
  pueden ejecutarse en local. Hoy lo cubre el *job* `Humo` de la CI, pero **a partir del 0.4 esto
  duele de verdad**: los tests de integración con PostgreSQL real son la mitad de la pirámide del
  §13, y esperar un *run* completo de CI para cada iteración de un repositorio no es un ciclo de
  trabajo viable. **Recomendación:** instalar Docker Desktop (o un demonio equivalente) antes de
  empezar el 0.4.
- **La firma de commits depende del secreto `SIGNING_KEY_B64`.** El hook `SessionStart` de
  `.claude/settings.json` la activa si existe y avisa si no. Sin él, **no se commitea** (política de
  `CLAUDE.md` §3). El montaje de la clave está en `plantillas/README.md` de la biblioteca.
- **Antes del 0.1, comprobar cuál es la LTS vigente de .NET.** `stacks/dotnet/convenciones.md` es
  explícita: "LTS vigente se comprueba, no se recuerda". El plan maestro dice .NET 10 y así está
  puesto en `Directory.Build.props`; si al arrancar hubiera cambiado, se corrige ahí, en un sitio.
- **Antes de adoptar cualquier dependencia, mirar su licencia.** MediatR, AutoMapper y
  FluentAssertions 8 pasaron a comercial en 2025; las versiones antiguas que circulan en tutoriales
  siguen pareciendo libres. Alternativas libres: Shouldly o AwesomeAssertions, Mapperly, y un
  despachador propio de unas cuarenta líneas en lugar de un bus en memoria (que en un monolito
  modular no aporta nada sobre una interfaz).
- **Testcontainers necesita un Docker accesible.** Los tests de integración no corren donde no lo
  haya. Por eso los comandos de `AGENTS.md` separan la batería rápida (dominio + arquitectura, sin
  Docker) de la completa, y la CI las ejecuta en pasos distintos.
- **R15 tiene una decisión de diseño que hay que tomar antes de escribir el caso de uso de la fase 5**,
  no durante: la cadena de registros de facturación es **una sola por (obligado tributario, sistema
  informático)** y por tanto **serializa la emisión**. Cerrojo pesimista sobre la fila de la cadena, o
  cola de un solo consumidor. No es un detalle de implementación: es un cuello de botella estructural.
  No afecta a la fase 0, pero condiciona cómo se diseña el outbox del 0.8 — conviene tenerlo delante.
- **El reloj del servidor forma parte de la corrección fiscal** (R15): las validaciones detectan
  registros con fecha-hora posterior a la del sistema. NTP obligatorio en el despliegue y comprobación
  de deriva en la sonda de disponibilidad. Se apunta ya para que el 0.2 no cierre la sonda de salud
  sin dejar sitio a esto.
