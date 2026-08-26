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

### Tomadas por el agente de desarrollo — ítem 0.3 (2026-08-25)

- **`tests/BuildingBlocks.UnitTests` creado** siguiendo el §12 (decisión del usuario: trivial y
  reversible, se decide y se anota). Referencia **solo** a `BuildingBlocks.Domain`: si algún día
  estos tests necesitan un doble de algo, será señal de que al dominio base se le ha colado una
  dependencia. Con él, **21 proyectos**.
- **Dos escalas son dos TIPOS: `Importe` (4 decimales) y `PrecioUnitario` (6).** Y la reducción de
  escala ocurre en **un solo sitio con nombre**, `PrecioUnitario.Por(cantidad)`, con un único
  redondeo sobre el producto exacto. Detalle y caso dorado en el **ADR-0005**.
- **El modo de redondeo va escrito (`AwayFromZero`) en cada llamada**, porque el de .NET es
  `ToEven` y omitirlo cambia el resultado. Hay un test que lo demuestra con 0,125.
- **La unidad mínima por divisa no tiene valor por omisión: una divisa desconocida lanza.** Suponer
  dos decimales acertaría con el dólar y fallaría en silencio con el yen y con el dinar.
- **NO se ha construido el servicio de cálculo de impuestos.** El §12 lo sitúa en su propio módulo
  de dominio y el plan maestro en la fase de facturación. El bloque común aporta la primitiva
  `Importe.Cuota` (un redondeo, por par base/tipo) y el caso dorado como referencia que ese servicio
  tendrá que reproducir; la agrupación vive **en el test**, como una de tres estrategias comparadas.
- **La frontera entre `Resultado` y excepción está escrita en el ADR-0004**, con la regla, la tabla
  de qué capa hace qué y cuatro ejemplos. Resumen: `Resultado` cruza **una sola costura** —de
  Aplicación al borde— y el dominio **siempre lanza**.
- **`ErrorDeOperacion` lleva código estable + mensaje**, y el código se valida al construirlo
  (minúsculas y guiones): se publica dentro de un URI, así que es contrato.
- **Las fábricas de `Resultado<T>` viven en `Resultado`, que no es genérico.** CA1000 está activo en
  `latest-recommended` y tiene razón: `Resultado<int>.Correcto(...)` obliga a escribir el tipo,
  `Resultado.Correcto(42)` lo infiere. Se corrigió el diseño en vez de suprimir la regla.
- **`BuildingBlocks.Infrastructure` pasa a conocer ASP.NET Core** (`FrameworkReference`). Es
  inevitable: la política de `ProblemDetails` que el §12 pone ahí *es* HTTP. Efecto colateral, con
  `NU1510`: `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` se vuelve redundante
  —viene en el marco compartido— y se ha quitado del `.csproj` y de `Directory.Packages.props`.
- **Las rutas que fallan a propósito viven en el proyecto de tests**, inyectadas con un
  `IStartupFilter`, no en la API. La política es middleware central e independiente de la ruta;
  publicar rutas de diagnóstico en producción para poder probarla sería pagar en superficie real lo
  que en el host de pruebas no cuesta nada.
- **Paquetes nuevos declarados: `Serilog` 4.3.0 y `Serilog.Formatting.Compact` 3.0.0** en
  `tests/Api.FunctionalTests` (ya llegaban por transitividad de la API; un paquete que se usa se
  declara). **Ambos Apache-2.0**, comprobado en su `.nuspec`. Ninguno de los comerciales de 2025.
- **Pruebas basadas en propiedades: descartadas por ahora.** Eran opcionales. Meter una biblioteca
  nueva y un idioma de test nuevo en el mismo ítem que estrena el dominio compra menos de lo que
  cuesta; los casos dorados de la fase 5 son mejor sitio, y allí sí. Anotado en *Notas / riesgos*.

### Tomadas por el agente de desarrollo — ítem 0.4 (2026-08-26)

- **El esquema del módulo se llama `org`, no `organizacion`.** ~~El encargo hablaba de un esquema
  `organizacion`; el **Anexo A.1** del plan maestro fija `org`, y `CLAUDE.md` prohíbe contradecirlo.
  Se ha ido con `org`.~~ **RESUELTO el 2026-08-26, antes del 0.5:** el usuario eligió `organizacion`
  y se corrigió el Anexo A.1. Ver *Convención de esquemas* más abajo.
- **Lo irreversible de esquema entra en el 0.4 para las entidades que el 0.4 crea**, según acordado
  con el usuario antes de empezar. Las ocho decisiones —direcciones en seis columnas con longitudes
  de ISO 20022, `empresa_id` desde la primera tabla, `Bloqueado` con su fecha, `date` frente a
  `timestamptz`, contador en columna y nunca secuencia, historial de migraciones en el esquema del
  módulo, enumerados como texto y cero cascadas— están en el
  **`docs/adr/adr-0007-lo-irreversible-del-esquema-de-organizacion.md`**, con el porqué de cada una.
  El 0.6 y el 0.10 llegan a tablas que ya tienen lo que necesitan; ninguno migra datos.
- **Un caso de uso por operación, sin bus en memoria** (§4): interfaz pública + implementación
  `internal`, y `AgregarCasosDeUsoDeOrganizacion()` registra los veinte. Como quien registra tipos
  `internal` tiene que vivir en el mismo ensamblado, la capa de Aplicación adopta
  **`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9 — MIT**, comprobado en su
  `.nuspec`. Son las abstracciones, no el contenedor. MediatR está descartado por el plan maestro
  y además es comercial desde 2025.
- **`IUnidadTrabajo` vive en `src/BuildingBlocks/Application`** (carpeta nueva). Un repositorio no
  confirma por su cuenta: quien decide dónde acaba la transacción es el caso de uso. Es del bloque
  común porque lo van a necesitar los cinco módulos.
- **El reloj es `TimeProvider`, no un `IReloj` propio.** Es el tipo de la BCL para esto desde .NET 8
  y un puerto con la misma forma solo añadiría una capa que traducir. Se registra con `TryAdd` para
  que un test que ya haya puesto un reloj falso conserve el suyo.
- **Los listados devuelven `PaginaDe<T>` directamente, no `Resultado<PaginaDe<T>>`.** Un listado no
  tiene modo de fallo de negocio: o hay elementos o la página viene vacía. Envolverlo obligaría a
  desenvolver algo que nunca trae error.
- **El tope de paginación lo aplica el enlace de modelo, no el controlador.** `page`/`size` (§9, en
  inglés) se enlazan a `ConsultaPaginada` con sus `[Range]`; un objeto de paginación construido a
  mano dentro de la acción se salta la validación entera y el tope no existe. Es un defecto que se
  encontró y se corrigió dentro del ítem.
- **`Endpoints` referencia `BuildingBlocks.Infrastructure` para reutilizar la política de errores del
  0.3, no para duplicarla.** `ErrorDeOperacion.AResultadoDeAccion()` es el único puente nuevo: un
  `IActionResult` que delega en el `IResult` que ya existía.
- **El error por campo del §9 es una extensión `errors` del `ProblemDetails`**, alimentada por
  `ErrorDeOperacion.Campos`. La forma es la MISMA la produzca el enlace de modelo o el caso de uso,
  para que un cliente no tenga que distinguir quién detectó el fallo. Los campos malos salen
  **todos de una vez**: corregir, reenviar y descubrir el siguiente es como se pierde la paciencia
  con un formulario.
- **`Desbloquear`, `Reabrir` y `Cerrar` existen en el dominio y NO tienen puerta HTTP.** Abrirlas sin
  permisos (fase 1) sería publicar la operación con la que se deshace un bloqueo legal. Anotado aquí
  para que no parezca un olvido.
- **Las URL generadas van en minúsculas (`LowercaseUrls`) y los `Obtener` no declaran nombre de
  ruta.** Los nombres de ruta son **globales**, no por controlador: los cuatro `Obtener` colisionaban
  y la aplicación **no arrancaba**. `CreatedAtAction` resuelve por nombre de acción y no los
  necesitaba. Sin `LowercaseUrls`, el token `[controller]` publicaba el nombre de la clase de C# en
  el `Location`.
- **Los tests de contrato de la API son de integración, por HTTP y contra PostgreSQL de verdad.**
  `WebApplicationFactory<Program>` sustituye **solo** la cadena de conexión, por el sitio por el que
  la configuración entra de verdad: en cuanto se reemplaza un registro del contenedor, lo que se
  prueba deja de ser el sistema que se despliega. Paquete
  **`Microsoft.AspNetCore.Mvc.Testing` 10.0.9 — MIT**, ya en uso desde el 0.2, más un
  `InternalsVisibleTo` en `Bastion.Api` porque con instrucciones de nivel superior `Program` es
  `internal`.
- **`Serie` y `Almacen` normalizan su código antes de preguntar por duplicados** (recorte +
  mayúsculas). Sin eso `«  central  »` pasaba el filtro y chocaba contra el índice único: un `500`
  donde tocaba un `409`.
- **`ARREGLADO` · las migraciones del módulo no se compilaban.** Viven en `db/migraciones/` (§14),
  fuera del proyecto, y el glob por omisión del SDK no las recoge: `Migrate()` aplicaba **cero**
  migraciones y creaba **cero** tablas, sin error y sin aviso. Se arregló con un `<Compile Include>`
  explícito. Y el guardián `scripts/comprobar-migraciones.sh` tenía la lógica de códigos de salida
  **invertida** —`has-pending-model-changes` sale **0 cuando está limpio** y **1 cuando hay
  deriva**—; solo parecía funcionar porque los dos defectos se cancelaban. Ahora comprueba también
  que haya al menos una migración **en el ensamblado**, y se ha demostrado su rojo con una propiedad
  de sombra.
- **`ARREGLADO` · el puerto de empresas pide el `Nif`, no su cadena.** `empresa.Nif.Valor == cadena`
  no se traduce a SQL —el NIF va con conversor de valor, y para EF la columna es un escalar—, así
  que **toda alta de empresa devolvía 500** mientras las lecturas funcionaban. Con el tipo en la
  firma, el error no se puede volver a escribir. Lo encontró el primer paso de los tests de
  integración; un doble en memoria lo habría dado por bueno.
- **`ARREGLADO` · fuera `[Produces("application/json")]` del controlador base.** No documenta:
  **sustituye** los tipos de contenido de todo `ObjectResult`, y con él puesto el `400` del enlace
  de modelo salía como `application/json` en vez de `application/problem+json`. Lo que documenta,
  sin efecto en ejecución, es `[ProducesResponseType]`.

### Convención de esquemas de PostgreSQL — los dieciséis módulos

> Enumerada aquí **entera y de una vez** para que ningún ítem la redescubra por su cuenta. El 0.4
> estrenó el primer esquema, el 0.5 estrena el segundo y el 0.7 el tercero; tres redescubrimientos
> son tres criterios distintos en la misma base de datos.

**La regla, sin excepciones: el esquema es el nombre del módulo (§5) en minúsculas y sin acentos.**

| Módulo (§5) | Esquema | Fase | Estado |
|---|---|---|---|
| Identidad | `identidad` | 0 | **0.5**, en curso |
| Organización | `organizacion` | 0 | creado en el 0.4 |
| Auditoría | `auditoria` | 0 | 0.7 |
| Terceros | `terceros` | 1 | — |
| Catálogo | `catalogo` | 1 | — |
| Inventario | `inventario` | 2 | — |
| Compras | `compras` | 3 | — |
| Ventas | `ventas` | 4 | — |
| Facturación | `facturacion` | 5 | — |
| Tesorería | `tesoreria` | 6 | — |
| Contabilidad | `contabilidad` | 7 | — |
| Producción | `produccion` | 8 | — |
| CRM | `crm` | 9 | — |
| RRHH | `rrhh` | 10 | — |
| Informes | `informes` | transversal | — |
| Notificaciones | `notificaciones` | transversal | sin carpeta en `src/Modules/` todavía |

Dos correcciones al **Anexo A.1**, decididas por el usuario el 2026-08-26 y ya aplicadas al plan
maestro para que no vuelva a divergir:

1. **`org` → `organizacion`.** En la lista del propio anexo, trece de los catorce esquemas ya eran
   el nombre del módulo en minúsculas y sin acentos; `org` era la única abreviatura, y los once
   encabezados del §7 tampoco la usaban. Escrita como estaba, la convención necesitaba un «salvo».
2. **Faltaban dos.** El anexo nombraba catorce esquemas y el §5 lista **dieciséis** módulos: no
   había esquema para **Informes** ni para **Notificaciones**, los dos `genérico / transversal`.
   Ahora son `informes` y `notificaciones`. Los dos acabarán teniendo tablas propias —Informes
   materializa proyecciones de lectura, Notificaciones guarda la cola de avisos en aplicación—, así
   que no tener nombre era un hueco, no una ausencia deliberada.

**El historial de migraciones de cada módulo vive dentro de su propio esquema**, nunca en `public`
(ADR-0007, punto 6). Se comprueba mirando `information_schema`, no la configuración.

### Tomadas por el agente de desarrollo — ítem 0.5 (2026-08-26)

**Respondidas por el usuario** en la puerta de clarificación, antes de escribir una línea:

- **Esquema de Organización: `organizacion`.** Ver el bloque de arriba. Hecho en su propio commit
  antes de empezar el 0.5.
- **Esquemas de Informes y Notificaciones: `informes` y `notificaciones`.** Ver el bloque de arriba.
- **De ASP.NET Core Identity se adopta el *hasher* y su bloqueo, no los almacenes.** `Usuario`,
  `Rol`, `Permiso` y la pertenencia a empresas son agregados propios del módulo, con sus invariantes
  y sus tablas en el esquema `identidad`. Motivo: `IdentityUser`/`IdentityRole` son entidades
  anémicas con setters públicos dentro de un módulo cuyas fronteras vigila el compilador (§4), y su
  `LockoutEnd` es bloqueo **temporal por intentos**, que no es el `Bloqueado` de R16 (baja lógica):
  con Identity entero harían falta los dos conceptos igual. La contraseña se sigue hasheando con «el
  algoritmo por defecto del framework» que exige el §11, así que esto **no** contradice el stack del
  §3; el algoritmo y sus parámetros quedan escritos en su ADR.
- **El registro es solo por invitación, con semilla de arranque.** Crear un usuario exige el permiso
  `identidad.usuario.crear` **en la empresa del *claim***: sin credenciales `401`, con credenciales
  sin ese permiso `403`. El primer usuario y su pertenencia nacen de una semilla que solo se aplica
  si la tabla de usuarios está vacía, parametrizada por variables de entorno. Motivo: esto no es un
  SaaS público; con auto-registro abierto, cualquiera con acceso de red se crea cuenta en la
  instalación de la empresa.

**Decididas por el agente** (o ya fijadas por el plan maestro, y anotadas para no volver a
preguntarlas):

- **Cómo se cambia de empresa: reemitiendo el token.** No es decisión abierta — el §9 del plan
  maestro (línea 850) ya lo fija: «la activa se selecciona al iniciar sesión y se refleja en el
  token, nunca en un parámetro manipulable». El *claim* lleva la empresa **activa**, no la lista;
  cambiar de empresa valida la pertenencia y **reemite el par de tokens**. Si la empresa fuese un
  parámetro de la petición, el *claim* sería decorativo.
- **El renombrado del esquema se hizo regenerando la migración inicial, no con una migración de
  renombrado.** El encargo pedía lo segundo y no funciona: EF Core resuelve dónde está la tabla de
  historial con la configuración vigente, así que contra una base con el esquema viejo buscaría el
  historial en `organizacion`, no lo encontraría y volvería a aplicar la migración 1 — falla justo
  en el único escenario para el que existiría. Y contra una base nueva crearía `org` para vaciarlo
  acto seguido, en cada base, para siempre. Como no hay ni una fila en ninguna parte, la primera
  migración nace ya con el nombre bueno. El razonamiento entero, y el `ALTER SCHEMA` de una línea
  para quien tenga un volumen de desarrollo viejo, en el **punto 9 del ADR-0007**.
- **El segundo factor (TOTP) del §11 no entra en el 0.5.** El criterio del ítem no lo menciona
  —registro, login, permisos por acción, pertenencia y *claim*— y el §11 lo declara «opcional para
  roles con poder». Anotado aquí para que no parezca un olvido.

**Decididas al montarlo** (durante la implementación, 2026-08-26):

- **`Desbloquear`, `Cerrar` y `Reabrir` estrenan puerta HTTP.** En el 0.4 se dejaron sin ella con un
  motivo escrito: no había a quién exigirle un permiso, y una operación de recuperación abierta a
  cualquiera es peor que no tenerla. Ese motivo desaparece hoy. Se abren detrás de
  `organizacion.empresa.desbloquear`, `organizacion.almacen.desbloquear`,
  `organizacion.ejercicio.cerrar` y `organizacion.ejercicio.reabrir`, cada una con su `401` y su
  `403` ejercitados. **No es ampliación de alcance encubierta:** son las mismas operaciones de
  dominio que ya existían y ya estaban probadas; lo que faltaba era la puerta.
- **Crear y modificar NO comparten permiso, aunque los escriba el mismo código.** Hay perfiles que
  dan de alta y no corrigen, y al revés; un permiso compartido hace imposible expresarlo. Lo vigila
  `Escribir_y_modificar_no_comparten_permiso_aunque_los_escriba_el_mismo_codigo`, que barre la tabla
  de rutas y agrupa por permiso todas las acciones de escritura. Las **lecturas sí** comparten:
  `ver` es una sola facultad, la liste quien la liste.
- **Los tests de HTTP se mudan a `tests/Api.IntegrationTests`, nuevo.** Desde hoy **toda** petición
  necesita un token que emite Identidad, así que un test de contrato de Organización ya no puede
  vivir en un proyecto que solo ve Organización. `Organizacion.IntegrationTests` se queda **solo con
  persistencia** (y pierde la referencia a `src/Api` y a `Mvc.Testing`), porque su
  `EsquemaDelModuloTests` afirma que hay **un** historial de migraciones, y eso solo es cierto en un
  contenedor con un módulo migrado. El nuevo proyecto migra los **dos**, que es lo que convierte esa
  afirmación del 0.4 en una prueba de verdad.
- **Ningún test fabrica un principal ni sustituye un servicio del contenedor.** Todos los clientes
  autenticados entran por `POST /api/v1/identidad/sesiones` con la contraseña de la semilla.
  `TokenForjado` existe solo para fabricar los que el borde **tiene que rechazar** (caducado, otro
  emisor, otra audiencia, firma tocada, ninguno): no hay manera de comprobar que la caducidad se
  valida sin presentar uno caducado.
- **Los casos de uso no llevan tests unitarios con dobles.** Se ejercitan por su camino real —HTTP
  contra PostgreSQL de verdad— igual que en el 0.4. Un doble del repositorio habría dado verde al
  defecto del 0.4 (`ExisteConNifAsync` con un conversor de valor por medio), que es exactamente el
  motivo. El **dominio** sí va con TDD y tests unitarios: `Identidad.UnitTests` pasa de 0 a 58.
- **`OpcionesDeJwt.DelEntorno()` se retira.** La configuración entra por `IConfiguration`, que ya lee
  las variables de entorno; un segundo camino que lee `Environment.GetEnvironmentVariable` a mano
  era código muerto y, peor, un camino que los tests no podían configurar.

## Estado actual

**Ítem 0.5 escrito y en verde en local; falta la confirmación de la CI.** GitHub Actions está en
**caída mayor** (incidencia abierta a las 15:11 UTC del 2026-08-26) y **no ha creado el *run*** del
commit `7d5f3c3`, ya empujado a `main`. En cuanto Actions vuelva, se apunta aquí el *run* con sus
cuatro conclusiones y los dos recuentos, y el ítem se marca en el checklist. **Hasta entonces el 0.5
no está cerrado**, por mucho que el árbol esté verde en esta máquina.

Las cinco capas de `src/Modules/Identidad/` dejan de estar vacías: `Usuario`, `Rol`, `Permiso`,
`Membresia` y `TokenDeRefresco`, con el esquema `identidad` y su migración propia. Y lo que cambia
para **todo lo demás**: la API deja de ser pública.

- **Se deniega por omisión** (`SetFallbackPolicy(RequireAuthenticatedUser)`) y cada acción se abre
  con `[ExigePermiso]`. **46 acciones**: 41 detrás de su permiso, 3 anónimas (`Iniciar`, `Renovar`,
  `Cerrar` sesión) y 2 autenticadas sin permiso (cambiar de empresa y cambiarse la contraseña de
  uno mismo, que no pueden depender de una facultad que se pueda retirar).
- **34 permisos** en dos catálogos (`identidad.*` 14, `organizacion.*` 20), con el formato
  `modulo.recurso.accion` del §11. Los roles los agrupan; no hay ni un `[Authorize(Roles = …)]`.
- **La empresa activa viaja en el *claim*** y se cambia reemitiendo el par de tokens. No hay ni un
  camino que la lea del cuerpo o de la consulta.
- **Refresco rotatorio** en cookie `__Host-bastion-refresco` (`HttpOnly` + `Secure` +
  `SameSite=Lax`), 14 días; el acceso, 15 minutos, en memoria. Reutilizar un refresco ya canjeado
  **tumba la familia entera**.
- **Semilla de arranque** en el *composition root* —cruza dos módulos, así que no puede vivir en
  ninguno—, solo si no hay ningún usuario y solo con sus siete variables de entorno puestas.

Recuento: `dotnet test` pasa de **226** casos a **298 rápidos** + los de integración (que aquí no se
pueden ejecutar: sigue sin haber Docker en esta máquina).

Verificado en local, con la salida real y **enseñando el rojo de cada bloque**:

- **Dominio de Identidad (TDD)** — rojo: no compilaba, las entidades no existían. Verde:
  `Identidad.UnitTests` **58/58** (de 0).
- **La denegación por omisión** — el rojo más útil del ítem, y no lo buscaba nadie: al poner la
  política de respaldo, **11 de los 19** tests funcionales que ya estaban en verde se pusieron en
  rojo con `401`. Ninguno hablaba de autorización: eran los de la política de errores, que piden
  rutas inventadas a propósito. Causa: el middleware aplica la política de respaldo **también
  cuando la petición no casa con ningún endpoint**. Está en el **ADR-0009**, con las tres
  consecuencias que arrastra (sondas con `AllowAnonymous` explícito, rutas de prueba publicadas
  como `Endpoint`, y `UseAuthentication` antes de `UseAuthorization`).
- **Los seis tests de forma de la autorización** (`CadaAccionDeclaraSuPermisoTests`) — probados con
  **cinco mutaciones** de `EmpresasController`, restaurando el fichero tras cada una: quitar el
  atributo → 2 rojos; exigir el permiso de otro módulo → 3 rojos; que `Crear` y `Modificar`
  compartan permiso → 2 rojos; `[Authorize(Policy = "inventada")]` → 3 rojos; exigir un permiso que
  no está en el catálogo → 2 rojos.
- **El resumen de contraseñas** (`ElResumenDeContrasenasTests`, 5 casos) — probado con **tres
  mutaciones** del hasher: bajar a 10 000 iteraciones → rojo *«iteraciones should be 100000 but was
  10000»*; que el resumen de relleno salga de una contraseña adivinable → rojo *«should be
  Incorrecta but was Correcta»*; memorizar el resumen → rojo *«primero should not be "AQAAAAIAAYag…"»*.
  Los parámetros del ADR-0008 viven **en el test**, no en el documento: si el paquete cambia sus
  valores por omisión, sale en rojo y el ADR se corrige con él.
- `dotnet restore --locked-mode` → `rc=0`. `dotnet build` Release → **0 advertencias / 0 errores**.
  `dotnet format --verify-no-changes` → `rc=0` (14 `IDE1006` corregidos a mano por el camino: el
  `NamingStyleCodeFixProvider` no admite «corregir todo en la solución», así que `dotnet format` los
  señala y no los arregla).
- `bash scripts/comprobar-migraciones.sh` → `rc=0`, y ahora dice **dos** líneas: *«Organizacion: 1
  migración(es)…»* y *«Identidad: 1 migración(es)…»*.
- `dotnet test --filter "Category!=Integracion"` en Release → **298 correctos, 0 con error**
  (94 bloques comunes + 116 Organización + 58 Identidad + 30 funcionales).

### Cada regla de autorización, la petición que la ejerce y lo que devuelve

Si una fila no tiene petición, esa regla no está probada. Las tres primeras **no son una fila por
acción: son un barrido sobre las 46** que el host publica, sacadas de
`IActionDescriptorCollectionProvider` —la misma tabla que usa el enrutado para servir— y no de una
lista escrita a mano.

| Regla | Petición que la ejerce | Respuesta |
|---|---|---|
| Sin credenciales no se atiende **ninguna** acción protegida | las 41, sin cabecera `Authorization` | `401` |
| Con un permiso que no es el suyo no se abre **ninguna** | las 41, con un token que solo trae `identidad.rol.ver` | `403` |
| Con su permiso se abre **cada una**, y solo con el suyo | las 41, cada una con un token que trae exactamente su permiso | ni `401` ni `403` |
| Las tres anónimas se alcanzan sin credenciales | `POST /sesiones` con cuerpo vacío | `400` (ha entrado) |
| | `DELETE /sesiones/actual` sin cookie | `204` |
| Cambiar la contraseña propia no exige permiso | `PUT /usuarios/actual/contrasena` con una cuenta **sin ni un permiso** | ni `401` ni `403` |
| Elegir empresa no exige permiso | `PUT /sesiones/actual/empresa` con esa misma cuenta | ni `401` ni `403` |
| Una ruta que no existe, para quien se ha identificado | `GET /api/v1/esto-no-existe` con token | `404` + `problem+json` |
| Una ruta que no existe, para el anónimo | la misma, sin token | `401` (ADR-0009) |
| No se pasa a una empresa a la que uno no pertenece | `PUT /sesiones/actual/empresa` con otra empresa | `403` `/errors/empresa-no-pertenece` |
| El correo que no existe y la contraseña mala son indistinguibles | dos `POST /sesiones`, uno con cada cosa | misma respuesta, mismo cuerpo |
| Cinco fallos rechazan la cuenta | 5 × `POST /sesiones` mal + 1 con la contraseña **buena** | `401` también el sexto |
| El acceso caducado, de otro emisor, de otra audiencia, con la firma tocada o sin token | `[Theory]` de 5 casos con `TokenForjado` | `401` |
| Reutilizar un refresco ya canjeado | renovar dos veces con la misma cookie | `401` y la familia entera muerta |
| El refresco no viaja en el cuerpo | leer la respuesta de `POST /sesiones` | solo en `Set-Cookie`, `HttpOnly` |
| Nada del interior sale en un error | NIF hostil, paginación imposible, cuerpo roto, tipo de contenido malo, cadena de 100 000 caracteres, correo hostil | `400`/`404`/`415`, y **ni uno** de los 23 rastros prohibidos |

Los 23 rastros —`Npgsql`, `SELECT `, `relation "`, `C:\`, `/home/runner`, `.cs:line`…— están
**escritos en el test** (`EntradaHostilTests`), no en la cabeza de nadie, más los dos secretos que
cambian en cada ejecución (la cadena de conexión y la clave de firma).

### Lo que el criterio del ítem pedía, y dónde está probado

| Comprobante | Dónde |
|---|---|
| Registro (por invitación) | `Crear` de `UsuariosController`, tras `identidad.usuario.crear` |
| Login | `SesionesYTokensTests`, entrando de verdad por `POST /sesiones` |
| Roles y permisos **por acción** | `LaPuertaDeCadaAccionTests` (barrido) + `CadaAccionDeclaraSuPermisoTests` (forma) |
| Pertenencia a empresas | `Escenario.EntrarEnAsync`, que concede pertenencia y rol por sus endpoints |
| El identificador de empresa viaja en el *claim* | `El_token_de_acceso_lleva_dentro_la_empresa_activa_el_usuario_y_los_permisos` |
| El historial por esquema del 0.4, probado de verdad | `Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema`: **exactamente 2** |
| Sin claves ajenas entre esquemas (§4, regla 4) | `La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena` |
| `Bloqueado` como tercer estado (R16 / LOPDGDD art. 32) | `El_usuario_se_bloquea_y_no_se_borra_asi_que_tiene_donde_apuntarlo` |
| `timestamptz` para los instantes | teoría de 6 columnas sobre `information_schema` |

---

**Del ítem 0.4:**

**Ítem 0.4 TERMINADO.**

> **Entre medias (2026-08-26), un commit propio que no es de ningún ítem:** el esquema de
> Organización pasó de `org` a `organizacion` y el Anexo A.1 quedó corregido y completo (dieciséis
> esquemas). Se hizo **antes** del 0.5 a propósito: es el ítem que estrena el segundo esquema, y
> renombrar con tablas vacías cuesta una migración regenerada; con datos, cuesta otra cosa.

Las cinco capas de `src/Modules/Organizacion/` dejan de estar vacías. `Empresa`, `Ejercicio`,
`Serie` y `Almacen` se crean, consultan, modifican y suprimen por `/api/v1/organizacion/*`, sobre
las migraciones propias del módulo y su esquema `organizacion`. Con ellas llegan los **veinte casos de uso**
(un tipo por operación), los cuatro repositorios, la unidad de trabajo y los cuatro controladores.

**El hito del ítem:** `--filter "Category=Integracion"` **deja de ser un no-op declarado**. Desde el
0.2 ese paso salía con `rc=0` diciendo «Ninguna prueba coincide con el filtro»; ahora ejecuta **50
casos contra PostgreSQL 17.6 de verdad** —la misma imagen del compose— levantado por Testcontainers
en la CI. Nada de EF Core InMemory.

Recuento: `dotnet test` pasa de **55** casos a **226** (176 rápidos + 50 de integración).

Verificado en local, con la salida real y enseñando el rojo de cada bloque:

- **Dominio (TDD, el test antes que el código)** — rojo por bloque: no compilaba, porque las
  entidades no existían. Verde: `Organizacion.UnitTests` **105/105**.
- **Bloque común (`ErrorDeOperacion.Campos`, `Divisas.EsConocida`)** — rojo: no compilaba. Verde:
  `BuildingBlocks.UnitTests` **57/57** (venía de 41).
- **Colisión de nombres de ruta** — rojo real y bien feo: los catorce tests de
  `Api.FunctionalTests` en rojo a la vez con `Attribute routes with the same name 'Obtener' must
  have the same template`. La aplicación no arrancaba. Verde tras quitar los nombres: **14/14**.
- `dotnet build` Debug y Release → **0 advertencias / 0 errores**. `dotnet format --verify-no-changes`
  → `rc=0` (once `IDE0007` corregidos por el camino). `dotnet restore --locked-mode` → `rc=0`.
- `bash scripts/comprobar-migraciones.sh` → `rc=0`: *«Organizacion: 1 migración(es) en el ensamblado,
  y el modelo coincide con ellas.»*
- `dotnet test --filter "Category!=Integracion"` en Release → **176 correctos, 0 con error**
  (57 + 105 + 14), con el recuento sacado del `.trx`.
- Los **50 de integración no se pueden ejecutar aquí**: esta máquina sigue **sin Docker**. Se
  ejecutan en la CI, que es el bucle que el usuario avaló.

Y **el rojo que solo podía ver PostgreSQL**, que es la razón entera de que estos tests existan: en
su primera ejecución, **16 de los 50 fallaron**, y los dos defectos eran invisibles para cualquier
test de dominio.

- **Toda escritura respondía `500`.** `ExisteConNifAsync` comparaba `empresa.Nif.Valor` contra una
  cadena, y el NIF está mapeado con un **conversor de valor**: para EF Core la columna es un escalar
  y no hay ningún `.Valor` en el que entrar, así que la consulta no se traducía. Las **lecturas**
  funcionaban —ninguna toca el NIF—, de ahí que solo cayera lo que escribe. Un doble en memoria
  habría evaluado ese `.Valor` en LINQ-to-Objects y habría dado **verde**. El puerto pide ahora el
  `Nif` entero, para que el error no se pueda volver a escribir.
- **El `400` del enlace de modelo salía como `application/json`.** `[Produces("application/json")]`
  en el controlador base no documenta: **sustituye** los tipos de contenido de todo `ObjectResult`,
  y el `400` automático de `[ApiController]` es uno. Un cliente que ramifique por el tipo de
  contenido —lo que manda la RFC 9457— no reconocía como problema el error más frecuente de todos.
  Quien documenta es `[ProducesResponseType]`, que ya estaba en cada acción.

Los cuatro *jobs* en verde — **[run 32929808259](https://github.com/AOjeda006/Bastion/actions/runs/32929808259)**: `Frontal` ✅, `Backend` ✅,
`Imágenes` ✅ y `Humo` ✅, con los dos recuentos publicados como anotación: *«Dominio y arquitectura:
176 casos (176 correctos, 0 con error, 0 omitidos)»* e *«Integración (Testcontainers): 50 casos (50
correctos, 0 con error, 0 omitidos)»*.

Lo que el criterio pedía y dónde está probado:

| Comprobante | Dónde |
|---|---|
| CRUD de las cuatro entidades por HTTP | `ContratoDeLaApiTests`, `201`+`Location` seguido hasta el recurso |
| Migraciones propias del módulo | `EsquemaDelModuloTests`, mirando `information_schema`, no la configuración |
| El historial vive en el esquema del módulo, no en `public` | `El_historial_de_migraciones_vive_en_el_esquema_del_modulo…` |
| Suprimir es bloquear (R16) | `Borrar_una_empresa_la_bloquea_pero_no_la_borra` |
| Direcciones estructuradas (R17) | `La_direccion_va_y_vuelve_en_los_seis_campos_de_R17` |
| Fechas de negocio sin zona | `Las_fechas_de_un_ejercicio_van_y_vuelven_como_fechas_de_calendario` |
| Error por campo del §9 | `Varios_campos_malos_se_devuelven_todos_de_una_vez` |
| `409` y no excepción de PostgreSQL | `Dos_empresas_con_el_mismo_NIF_es_409…` |
| Serie colgada de otra contabilidad | `Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400…` |
| El tope de paginación se aplica de verdad | `Pedir_una_pagina_gigante_no_se_lleva_la_tabla` |

---

**Del ítem 0.3:**

`src/BuildingBlocks/Domain` deja de estar vacío: estrena `Dinero/` (`Importe`, `PrecioUnitario`,
`Divisas`) y `Resultados/` (`Resultado`, `Resultado<T>`, `ErrorDeOperacion`, `TipoDeError`), y sigue
**sin una sola referencia**, que es justo lo que debe ser. `BuildingBlocks.Infrastructure` estrena
`Errores/` con la política central de `ProblemDetails`, y `Program.cs` la cablea lo primero de la
tubería. Con `tests/BuildingBlocks.UnitTests`, la solución va por **21 proyectos** y `dotnet test`
pasa de 3 casos a **55**.

Verificado en local, con la salida real de cada comando y enseñando el rojo de cada bloque:

- **`Importe`/R6** — rojo 1: no compilaba. Rojo 2, ya con recuento: **22 con error, 0 superados**.
  Verde: **22/22**.
- **`Resultado`** — rojo: no compilaba (`Resultados` no existía). Verde: **41/41** acumulado.
- **`ProblemDetails`** — rojo: no compilaba (`Infrastructure.Errores` no existía). Verde: **14/14**
  en `Api.FunctionalTests`, once de ellos de la política de errores.
- `dotnet build` en Release sobre la solución entera → **0 advertencias / 0 errores**.
  `dotnet format --verify-no-changes` → `rc=0`. `dotnet restore` con los 21 `packages.lock.json`.
- `dotnet test` en Release → **55 superadas, 0 con error** (41 + 14).
- `dotnet test --filter "Category=Integracion"` → `rc=0` diciendo «Ninguna prueba coincide con el
  filtro» en los **dos** ensamblados: un no-op **declarado**, no un verde mudo. Sigue sin haber
  pruebas de integración porque **sigue sin haber Docker** en esta máquina.
- Y fuera del host de pruebas, con la API arrancada de verdad (`dotnet run` + `curl`): un `404` de
  enrutado devuelve `application/problem+json` con `"traceId":"4bf92f3577b34da6a3ce929d0e0e4736"` —el
  mismo `traceparent` que se mandó— y esa misma traza aparece como `@tr` en la línea del registro.

Lo que el criterio pedía y dónde está probado:

| Comprobante | Dónde |
|---|---|
| Redondeo de R6 por par (base, tipo) | `ReglaDeRedondeoR6Tests`, con las **tres** estrategias dando 10,00 / 10,02 / 9,99 |
| `AwayFromZero` y no el `ToEven` de .NET | `ImporteTests.Cuota_EnElPuntoMedioDeLaUnidadMinima_…` |
| Las dos escalas y dónde baja la escala | `PrecioUnitarioTests.Por_…` |
| Operar entre divisas lanza | `ImporteTests.Suma_ConDivisasDistintas_Lanza` |
| `type` estable por clase de error | `PoliticaDeErroresTests.CadaClaseDeError_…` (los cinco) |
| `traceId` de la respuesta = `@tr` del registro | `PoliticaDeErroresTests.ElTraceIdDeLaRespuesta_…` |
| Nada del interior sale en la respuesta | `…_RespondeQuinientosSinNadaDelInterior` (entrada hostil) |
| Los dos destinatarios no comparten texto | `ElDetalleInterno_ViveEnElRegistroYNoEnLaRespuesta` |

Y en la CI — **[run 32882753628](https://github.com/AOjeda006/Bastion/actions/runs/32882753628), los cuatro *jobs* en verde**: `Frontal` ✅,
`Backend` ✅, `Imágenes` ✅ y `Humo` ✅, a la primera y sin ningún paso en rojo.

---

**Del ítem 0.2:**

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

**Dónde retomar exactamente:** ítem **0.5**, el módulo Identidad. Criterio: registro y login; roles
y permisos por acción; pertenencia a empresas; el identificador de empresa viaja en el *claim*.

El sitio del 0.5 son las cinco capas de `src/Modules/Identidad/`, que existen desde el 0.1 y siguen
vacías. Organización ya está montado y sirve de plantilla: mismo reparto por capas, mismo
`Modulo…` en el *composition root*, mismo `DbContext` con su esquema y su historial propios, y los
mismos dos ficheros de test (`…UnitTests` para el dominio, `…IntegrationTests` para el esquema y el
contrato de la API). La carpeta `Arquitectura.Tests` sigue vacía y es del **0.12**.

**Lo que el 0.5 va a necesitar de lo que dejó el 0.4:** el *claim* de empresa que exige R8 se apoya
en la columna `empresa_id` que ya existe en las tres entidades transaccionales; el filtro global que
la usa es el **0.6**, no el 0.5.

**Docker en local sigue sin estar, y sigue sin bloquear.** El *runner* `ubuntu-latest` trae demonio
de Docker —el *job* `Humo` levanta el compose entero y el paso `Category=Integracion` ejecuta ya 50
casos con Testcontainers—, así que la cobertura existe. Lo que falta en local es solo el **bucle
rápido**: cada iteración de un repositorio cuesta un *run* de CI (unos cuatro minutos) en vez de
segundos. Los dos defectos del 0.4 costaron dos *runs* completos por eso. Sigue mereciendo la pena
instalarlo (ver *Notas / riesgos*), y la comprobación no es `docker --version`: es **una ejecución
real de Testcontainers**.

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
- [x] **0.3 · Bloque común (*shared kernel*)** — criterio de aceptación: objeto de valor
  `Importe(cantidad, divisa)` con la regla de redondeo de R6 probada; resultado de operación;
  `ProblemDetails` según **RFC 9457**.
  Decisiones en `docs/adr/adr-0004-frontera-entre-resultado-y-excepcion.md` y
  `docs/adr/adr-0005-dinero-dos-escalas-y-la-regla-de-redondeo.md`.
  Los cuatro *jobs* de la CI en verde — [run 32882753628](https://github.com/AOjeda006/Bastion/actions/runs/32882753628).
- [x] **0.4 · Módulo Organización** — criterio de aceptación: CRUD de `Empresa`, `Ejercicio`, `Serie`
  y `Almacen`, con migraciones propias del módulo.
  Decisiones de esquema en `docs/adr/adr-0007-lo-irreversible-del-esquema-de-organizacion.md`.
  Primer ítem con tests de integración de verdad: **50 casos** contra PostgreSQL 17.6 con
  Testcontainers, y `Category=Integracion` deja de ser un no-op declarado.
  Los cuatro *jobs* de la CI en verde — [run 32929808259](https://github.com/AOjeda006/Bastion/actions/runs/32929808259).
- [ ] **0.5 · Módulo Identidad** — criterio de aceptación: registro y login; roles y permisos por
  acción; pertenencia a empresas; el identificador de empresa viaja en el *claim*.
  Escrito y en verde en local; decisiones en
  `docs/adr/adr-0008-contrasenas-bloqueo-y-la-respuesta-unica-del-acceso.md`,
  `docs/adr/adr-0009-la-denegacion-por-omision-tambien-cubre-lo-que-no-es-una-ruta.md` y
  `docs/adr/adr-0010-una-entidad-hija-con-clave-propia-no-se-da-de-alta-sola.md`.
  **Sin marcar a propósito:** la CI lo ha tumbado tres veces —`7d5f3c3` no llegó a ejecutarse por
  la caída mayor de Actions del 2026-08-26; `5314ffc` y `772ddec` salieron rojos por defectos
  reales que solo PostgreSQL veía— y se arregla hacia delante. Se marca cuando haya cuatro
  conclusiones en verde que leer, no antes.
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

- **ABIERTO (2026-08-26) · el *compose* no aplica las migraciones, así que la semilla no llega a
  aplicarse ahí.** Nadie ejecuta `dotnet ef database update` ni al arrancar la API ni en el
  `docker-compose.yml`: la base del entorno local no tiene tablas. Consecuencia práctica de hoy:
  las siete variables `BASTION_SEMILLA_*` están **declaradas en el compose y vacías por omisión**,
  así que la semilla se salta con un aviso en el registro y el entorno levanta igual que antes. Si
  se rellenan sin haber migrado, el arranque **revienta a propósito** (la semilla no se calla). Lo
  que falta es decidir **quién** aplica las migraciones en un despliegue —un paso del compose, un
  `initContainer`, o el propio arranque de la API— y eso es materia del **0.13**, no del 0.5. Los
  tests de integración sí migran: lo hace su fixture antes de levantar el host.
- **ABIERTO (2026-08-26) · la CI solo se dispara en `main` en este repositorio.** El *workflow*
  declara `push: branches: ['**']`, pero de los **19** *runs* que existen, los 19 son de `main`.
  Empujar `feature/0.5-identidad` (commit `7d5f3c3`) **no creó ningún *run***, ni siquiera un
  `check-run`. Consecuencia: la regla de trabajo *«rama por ítem, verificar en verde y solo entonces
  `main` avanza por fast-forward»* no se puede cumplir tal cual — la única manera de que la CI mire
  el trabajo es empujar `main`, que es lo que han hecho de hecho todos los ítems anteriores
  (incluidos dos rojos, `ab9009b` y `2e991ac`, arreglados hacia delante). Pendiente de decidir con
  el usuario: o se acepta el «arreglar hacia delante en `main`» y se escribe así en el plan, o se
  averigua por qué Actions no atiende a las ramas (permisos del repositorio, no del *workflow*).
- **INCIDENCIA EXTERNA (2026-08-26) · GitHub Actions en caída mayor.** Incidencia abierta a las
  15:11 UTC. El *push* de `7d5f3c3` a `main` llegó (el `HEAD` remoto es ese) y **no se creó el
  *run***. No es un fallo del repositorio ni del *workflow*: `githubstatus.com` da `Actions ->
  major_outage` con `Git Operations`, `API Requests` y `Webhooks` operativos. El 0.5 queda **sin
  cerrar** hasta que haya *run* que leer.
- **ABIERTO · los mínimos del recuento de tests siguen en 1 y 1.** `scripts/ci/recuento-de-tests.sh`
  falla si un paso ejecuta menos casos de los exigidos, y hoy exige **uno** en cada paso mientras se
  ejecutan 298 y unos cuantos de integración. Con ese suelo, perder un ensamblado entero del barrido
  seguiría saliendo verde. Se suben a un suelo realista **en cuanto la CI publique las dos cuentas
  de verdad** — no antes, para no poner un número inventado.
- **ARREGLADO (2026-08-26) · el verde del *job* `Backend` no decía cuántos casos ejecutaba.** Los
  registros de un *job* devuelven **403** sin autenticar, así que desde fuera un `dotnet test` que
  dejara de encontrar ensamblados —o un `--filter` que no casara con nada— se veía igual de verde que
  un verde de verdad. Los dos pasos de test escriben ahora en **su propio** directorio y sin
  `LogFileName` fijo, y `scripts/ci/recuento-de-tests.sh` publica el recuento como `::notice::`, que
  sí es público, y **falla** si baja del mínimo declarado (dominio 1; integración 0 hasta el 0.4, y 1
  desde el 0.4). De paso salió un defecto que no se veía: con un `LogFileName` fijo, `dotnet test`
  sobre la solución escribe un `.trx` **por ensamblado** y el segundo pisaba al primero, así que el
  artefacto `test-results` solo traía la mitad de los resultados. El recuento se saca del `.trx`, que
  es XML, y no del resumen de consola, que va **traducido** al idioma del CLI.
- **ARREGLADO (2026-08-26) · un rojo de test no decía QUÉ había fallado.** Continuación de la nota
  anterior, y descubierto en el 0.4: con los registros del *job* devolviendo **403** sin autenticar
  y la descarga del artefacto `test-results` pidiendo credenciales (**401**), un rojo se veía desde
  fuera como *«Process completed with exit code 1»* y nada más — ni el nombre del caso ni la
  aserción. `recuento-de-tests.sh` saca ahora de cada `.trx` los casos en rojo con su nombre y su
  mensaje y los emite como `::error::`, que sí es público; y los dos pasos de test dejan de decidir
  el desenlace (`dotnet test` con `set +e`, el recuento habla el último, el paso sale con el código
  original). Sin esto, los dos defectos del 0.4 no se habrían podido diagnosticar sin credenciales.
  **Dos límites que conviene saber:** GitHub conserva **10 anotaciones por nivel y paso**, así que
  el script emite como mucho quince y dice cuántas quedan fuera; y las anotaciones llegan en
  Latin-1 por la API, de modo que los acentos se ven rotos al leerlas con `curl` —en la web salen
  bien—.
- **Un test que solo se ejecuta aislado no está probado.** Dos tests de la política de errores
  **pasaban en aislado y fallaban con la suite entera**: `AddSerilog(configurar)` deja por omisión el
  registro en el `Log.Logger` **estático** y ata el contenedor a ese estático, así que con dos hosts
  de prueba levantándose en paralelo el último le pisaba el registro al otro. Arreglado con
  `preserveStaticLogger: true` en el host de pruebas. Regla que se queda: **cualquier host de prueba
  que capture registro lo usa**, y una verificación en aislado no cuenta como verificación. Es una
  trampa de *toolchain*, transversal y reutilizable, así que vive en su propio
  **`docs/adr/adr-0006-un-test-que-solo-se-ejecuta-aislado-no-esta-probado.md`** y no dentro del
  ADR-0004, que va de otra cosa.
- **Pruebas basadas en propiedades: pendientes, y con una condición.** Descartadas en el 0.3 a
  propósito (ver *Decisiones*). El sitio natural son los **casos dorados de impuestos y valoración**
  de la fase 5. La regla que más cuesta cumplir, anotada aquí para que no se pierda: **no se escribe
  la función para que encaje con la propiedad**, y si la propiedad se pone en rojo, la primera
  pregunta es **qué afirma la propiedad**, no qué falla en el código. Comprobar la licencia de la
  biblioteca antes de adoptarla.
- **El servicio de cálculo de impuestos es de la fase de facturación, no del bloque común.** El 0.3
  deja la primitiva (`Importe.Cuota`, un redondeo por par base/tipo) y el **caso dorado** del
  ADR-0005. Criterio de aceptación de aquel servicio: reproducir esa tabla. Si da 10,02 está
  redondeando línea a línea; si da 9,99, al final.
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
  pueden ejecutarse en local. Lo que esto cuesta es el **bucle rápido**, no el trabajo: el *runner*
  `ubuntu-latest` sí tiene demonio de Docker, así que los tests con Testcontainers se ejecutan en la
  CI desde el 0.4 y el criterio del ítem se puede cumplir y verificar sin Docker aquí. Lo que no se
  puede es iterar en segundos: cada vuelta de un repositorio cuesta un *run* completo. Los tests de
  integración con PostgreSQL real son la mitad de la pirámide del §13, así que esa diferencia se nota.
  **Recomendación:** instalar Docker Desktop (o un demonio equivalente) cuanto antes, **en paralelo**
  al 0.4, no como paso previo que lo bloquee.

  **Intento del 2026-08-25, fallido, y lo que se aprendió:** el prerrequisito ya está —WSL 2 con
  Ubuntu como distribución predeterminada—, y `winget` ofrece `Docker.DockerDesktop` 4.88.0. El
  intento se quedó **parado en la descarga**: doce minutos con 0,58 s de CPU y **cero bytes**
  escritos. No es la red —la misma máquina descarga el instalador de 631 MB por HTTPS sin problema,
  `200 OK` con `curl -I`—, sino que la sesión **no es administradora** y winget se queda esperando
  la elevación. Se detuvo el proceso; **no quedó nada a medias** (`C:\Program Files\Docker` no
  existe). Lo tiene que lanzar el usuario desde una terminal **elevada**:

  ```powershell
  winget install --id Docker.DockerDesktop --exact --accept-package-agreements --accept-source-agreements
  ```

  Después hacen falta dos cosas más que tampoco puede hacer el agente: estar en el grupo
  `docker-users` (cerrar sesión y volver a entrar) y arrancar Docker Desktop una vez. Y la
  comprobación no es `docker --version`: es **una ejecución real de Testcontainers**, que es lo que
  demuestra que el demonio acepta contenedores y que el ciclo del 0.4 es viable.
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
