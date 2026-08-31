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
  El 0.6 llegó a tablas que ya tenían lo que necesitaba y no migró datos. **El 0.10 sí**, y la
  previsión de 2026-08-26 se quedó corta por un sitio que entonces no se veía: las columnas
  estaban puestas, pero el bloqueo pasó de un enumerado por tabla a las tres columnas del tipo
  compartido, y ese cambio de forma **sí** obliga a derivar el dato de las filas existentes
  antes de tirar la columna vieja (ADR-0016). La previsión acertó en lo irreversible —ninguna
  de las ocho decisiones del ADR-0007 hubo que rehacerla— y erró en que unificar tres formas
  de lo mismo también mueve datos.
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
| Identidad | `identidad` | 0 | creado en el 0.5 |
| Organización | `organizacion` | 0 | creado en el 0.4 |
| Auditoría | `auditoria` | 0 | creado en el 0.7; desde el 0.8 acoge además las dos tablas de la bandeja de salida (ADR-0013) y desde el 0.9 la de claves de idempotencia (ADR-0014) |
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

### Tomadas por el agente de desarrollo — ítem 0.6 (2026-08-27)

**Respondidas por el usuario** en la puerta de clarificación, antes de escribir una línea:

- **`Empresa` se filtra, y solo se ve la activa.** La alternativa era «se ven las empresas a las que
  uno pertenece», que exigía cruzar de Organización a Identidad en una consulta y eso lo prohíbe el
  §4. Consecuencia asumida: `GET /empresas` trae una sola fila y el `Location` de un `POST` no se
  puede seguir hasta entrar en la empresa nueva.
- **Un usuario con el que no se comparte empresa da `404`, no `403`.** Un `403` confirmaría que la
  cuenta existe, que es exactamente lo que sirve para enumerar correos.

**El inquilinato, entidad por entidad.** Esta tabla se decidió **antes** que el código, porque
decidirla mal no se nota: una entidad global de más es una fuga permanente y una de menos es una
funcionalidad que no se puede construir. Va aquí y en el **ADR-0011**, y no la vigila la buena
voluntad: la vigila `CadaEntidadDeclaraSuInquilinatoTests`, que recorre el modelo ya construido y la
compara **en los dos sentidos**.

| Entidad | ¿De inquilino? | Por qué |
|---|---|---|
| `Empresa` | Sí, **por su propia clave** | Es la raíz: no lleva `empresa_id` porque ella *es* el inquilino. Filtra por `Id`. |
| `Ejercicio` | Sí | Un ejercicio contable es de una empresa; dos empresas tienen su 2026 cada una. |
| `Serie` | Sí | La numeración de facturas es de una empresa y un ejercicio. Compartirla es un problema fiscal, no de privacidad. |
| `Almacen` | Sí | El dato de negocio más obvio. |
| `Membresia` | Sí — **es el puente** | Lleva `empresa_id` y dice quién está en qué empresa. Sobre ella se apoya el filtro de `Usuario`. |
| `Usuario` | Global, con **consulta acotada** | Una cuenta es una y puede pertenecer a varias empresas: no puede llevar `empresa_id`. Filtra por la pertenencia. |
| `Rol` | Global | Un rol es un catálogo de permisos de la instalación. **Consecuencia asumida y escrita:** un rol creado desde una empresa se ve y se asigna desde las demás. |
| `PermisoDeRol` | Global | Es parte del rol; no tiene `DbSet` ni consulta propia. |
| `RolDeMembresia` | Global **de hecho** | Depende de la pertenencia, que sí filtra, y no tiene navegación de vuelta con la que escribir un filtro. Seguro **mientras no se consulte por su cuenta**, y eso no se confía: se comprueba. |
| `TokenDeRefresco` | Global | Se busca por su resumen **antes** de que haya empresa activa. La empresa con la que se operaba va dentro de la fila (`EmpresaActivaId`) y la comprueba `RenovarSesion`. |

**Decididas por el agente** al montarlo:

- **El filtro falla cerrado.** `IInquilinoActual.EmpresaDelFiltro` **lanza** cuando no hay empresa
  activa; no devuelve nulo ni `Guid.Empty`. Cualquiera de esas dos sería un valor por omisión que
  rellena el hueco y lo esconde, y el síntoma —«no tienes almacenes», o peor, «aquí están los de
  todos»— no se distingue de un dato correcto.
- **Los caminos legítimos sin principal van por un ámbito explícito, con nombre y anotado en el
  registro**, no por `IgnoreQueryFilters`. Los motivos son un **enumerado cerrado**
  (`MotivoSinInquilino`), no una cadena libre: añadir uno obliga a tocar el tipo, que es un cambio
  que se ve en la revisión. Son cuatro: `SemillaDeArranque`, `AutenticacionYSesion`,
  `UnicidadGlobal` (el NIF y el correo son únicos en toda la instalación: filtrada, la comprobación
  diría «libre» sobre un valor ocupado y el alta se estrellaría contra el índice único — un `500`
  donde toca un `409`) y `AdministracionDePertenencias`. Diez aperturas en siete ficheros, y la
  lista se compara entera. **El 0.8 tiene aquí su sitio**: el trabajo de fondo de la bandeja de
  salida corre sin petición, y lo que necesita es un motivo nuevo en el enumerado, no un contexto
  sin filtro.
- **El ámbito vive en un `AsyncLocal`, no en un `static` a secas.** El host atiende varias
  peticiones a la vez en el mismo proceso, y un estático convertiría «la semilla está sembrando» en
  «nadie filtra, para todos». Anidar está permitido y al cerrarse recupera el de fuera.
- **Los filtros se escriben a mano, uno por entidad, en el `OnModelCreating` de cada contexto.** Un
  barrido por reflexión sería más corto y peor: para armar la expresión habría que meter la
  instancia dentro (`Expression.Constant(this)`) y el modelo, que EF Core cachea, se quedaría con el
  inquilino del **primer** contexto. Lo que la reflexión iba a garantizar —que no falte ninguna— lo
  garantiza el test que recorre el modelo.
- **Los contextos siguen con `AddDbContext`, no con `AddDbContextPool`.** Y el filtro lee una
  **propiedad de instancia** evaluada en cada consulta, no un campo copiado en el constructor. Hoy
  las dos cosas se comportan igual; la propiedad es lo que hace que sigan igual el día que alguien
  active la agrupación buscando rendimiento.
- **La forma fuerte de «el identificador del cuerpo se ignora» es que no exista.** Ningún DTO tiene
  por dónde recibir la empresa. Lo comprueba `NingunaPeticionNombraLaEmpresaTests` sobre la tabla de
  rutas que el host construye de verdad, con seis excepciones —las acciones cuyo **sujeto** es la
  empresa— y el guardián nombrado en cada una.
- **Una fila ajena y una inexistente devuelven exactamente lo mismo: `404`, con el mismo
  `ProblemDetails` y el mismo `type`.** No contradice el `403 /errors/empresa-ajena` del 0.5: allí
  la petición nombra una **empresa** y se niega la operación —quien la recibe ya sabía qué empresa
  había escrito—; aquí la petición nombra una **fila** y no se dice si existe. La distinción está
  escrita en el ADR-0011, §6.
- **`Microsoft.EntityFrameworkCore` entra como referencia de
  `Bastion.BuildingBlocks.Infrastructure`** (EF Core, no el proveedor: quién elige PostgreSQL sigue
  siendo la `Infrastructure` de cada módulo). No es un paquete nuevo en la solución —la versión
  10.0.9 ya estaba en `Directory.Packages.props` desde el 0.4—, es una referencia nueva. Licencia
  **MIT**.

### Tomadas por el agente de desarrollo — ítem 0.7 (2026-08-27)

**Apuntada por el usuario** al abrir el ítem: la ruta 1 —la entidad de traza **mapeada en el
contexto de cada módulo**, apuntando al esquema `auditoria` con `ToTable(nombre, "auditoria")`
explícito— era su apuesta. Se ha montado, y sale. Lo que se temía de ella —que Organización e
Identidad necesitaran una migración vacía cada una para un modelo que no cambia— **no ocurre**:
`ExcludeFromMigrations` saca la tabla del comparador de modelos, y `scripts/comprobar-migraciones.sh`
dice «el modelo coincide con ellas» para los tres módulos. La tabla la migra un solo dueño:
`AuditoriaDbContext`.

Todo lo de abajo está razonado en
`docs/adr/adr-0012-la-traza-va-en-la-misma-transaccion-que-el-cambio.md`.

- **La traza entra en el mismo `SaveChanges` que el cambio.** Un `SaveChangesInterceptor` añade las
  filas en `SavingChanges`, al mismo `DbContext`, antes de que EF Core mande nada. **Las dos rutas
  descartadas se escriben descartadas:** ni escribir la traza *después* de que `SaveChanges` haya
  ido bien —cambio confirmado, traza que puede no llegar—, ni desde un contexto aparte con su propia
  transacción —traza confirmada de un cambio que se revirtió—. «Mejor esfuerzo» no es una propiedad
  que esta tabla pueda tener.
- **La prueba de la atomicidad es el `xmin`.** Los dos tests obvios —un guardado que revienta no
  deja ni fila ni traza; uno que va bien deja las dos— los pasa **también** la ruta descartada de
  escribir después. Así que hay un tercero: PostgreSQL guarda en cada fila el número de la
  transacción que la insertó, y el de la fila y el de su traza tienen que ser **el mismo**. Es el
  único de los tres que la mutación «escribir después del guardado» pone en rojo.
- **Una sola fase, y comprobado en vez de copiado.** La receta canónica es de dos porque en el caso
  general la clave de un `INSERT` la pone la base. Aquí no la pone: lo verifica
  `LasClavesSeConocenAntesDeGuardarTests` sobre los modelos ya construidos, en las **cinco** formas
  que tiene un valor de venir del servidor (`DEFAULT`, columna calculada, `IDENTITY`/`serial` de
  Npgsql, regeneración en `UPDATE`, testigo de concurrencia — el `xmin` que llegó con el 0.9). Si se
  pone rojo no se añade a una lista de excepciones: se reabre el ADR. **Y así ocurrió**: el 0.9
  declaró `xmin` como testigo, el test se puso rojo como estaba escrito que haría, y la premisa se
  reenunció en el **ADR-0015**, que sustituye al punto 2 del ADR-0012 — una decisión aceptada no se
  edita—. La fase única sigue en pie: el testigo no va a la traza.
- **Solo añadido lo impide el motor.** Una función `plpgsql` y **dos** disparadores sobre
  `auditoria.registros`: uno de fila `BEFORE UPDATE OR DELETE` y otro de sentencia `BEFORE TRUNCATE`
  —los de fila no ven un `TRUNCATE`, que es justo la orden con la que se vaciaría la tabla de un
  golpe—, los dos con `ERRCODE = 'restrict_violation'`. Un `REVOKE` no vale: los permisos los da y
  los quita el dueño de la tabla, que es el usuario con el que se conecta la aplicación, y un
  permiso que el interesado puede devolverse a sí mismo es una frase, no una guarda. **Y queda
  escrito en el ADR con estas palabras:** esto no es lógica de negocio, es una restricción de
  integridad, de la misma familia que un `CHECK`. Leer la migración no es la prueba — la prueba es
  el `UPDATE`, el `DELETE` y el `TRUNCATE` lanzados contra PostgreSQL exigiendo el SQLSTATE.
- **Lo que se audita es una lista de permitidos, y falla cerrado.** Cada entidad y cada propiedad
  declara su clasificación (`Auditada`, `NoAuditada`, `Secreta`) en su configuración; lo que no está
  clasificado no se audita, y `CadaEntidadDeclaraSuAuditoriaTests` pone en rojo cualquier
  `SinClasificar`. Las dos propiedades que no pueden acabar ahí por ningún camino son
  `Usuario.HashDeContrasena` y `TokenDeRefresco.Hash`. Eso es la forma; el efecto lo prueban dos
  tests de integración, uno de ellos en su **forma fuerte**: no nombra ninguna columna, le pregunta
  al modelo qué ha declarado secreto, lee esos valores de la base y exige que ninguno esté en
  ninguna traza.
- **La traza tiene su propio inquilinato, y no admite huecos.** `RegistroDeAuditoria` **no**
  implementa `IDeInquilino` porque su empresa es **anulable**: hay escrituras legítimas sin
  inquilino. Esas filas llevan el motivo en su propia columna, y un `CHECK` de la tabla
  —`(empresa_id IS NULL) <> (sin_inquilino IS NULL)`— hace imposible tener una cosa sin la otra. No
  es `Guid.Empty`: un valor por omisión rellena el hueco y lo esconde. Y filtra por empresa como
  todo lo demás, porque una traza dice qué NIF tenía antes una empresa y quién lo cambió.
- **La traza de una entidad global lleva la empresa DESDE LA QUE se actuó.** Un `Rol` no es de
  ninguna empresa (ADR-0011); su traza sí, y es la que estaba activa al hacer el cambio.
  **Consecuencia asumida y escrita:** un mismo rol acumula trazas de varias empresas y cada una solo
  ve las suyas — nadie ve su historia completa desde dentro de una empresa. Lo contrario convertiría
  la auditoría en el camino por el que se descubre qué otras empresas existen.
- **Se cierra el cabo que dejó suelto el 0.6: `HasQueryFilter` no interviene en un `INSERT`.** El
  interceptor, que ya recorre las entradas pendientes, comprueba que toda fila `IDeInquilino`
  añadida o modificada lleve la empresa del ámbito actual, y lanza antes de confirmar nada. **Su
  límite está escrito:** dentro de un ámbito sin inquilino no hay contra qué comparar, así que no se
  comprueba — la semilla y la administración de pertenencias escriben filas de otra empresa a
  propósito, y quién puede hacerlo lo decide `PuedeAdministrarAsync`.
- **`CerrarSesion` abre ahora el ámbito `AutenticacionYSesion`**, el mismo que `IniciarSesion` y
  `RenovarSesion`. Le faltaba desde el 0.5 y no se notaba porque nada preguntaba; desde el 0.7
  pregunta el interceptor. Es el único cambio de comportamiento que el ítem hace sobre lo que ya
  estaba, y la lista de aperturas del 0.6 pasa de diez a **once**.
- **`Microsoft.EntityFrameworkCore.Relational` entra como referencia de
  `Bastion.BuildingBlocks.Infrastructure`.** No es un paquete nuevo en la solución —la versión
  10.0.9 ya estaba en `Directory.Packages.props`—, es una referencia nueva. Licencia **MIT**.
- **`jsonb` es la única palabra de PostgreSQL en el bloque común, y se acepta a sabiendas.** La
  alternativa era `text`, que no obliga a nada y pierde lo único que justifica guardar una fila por
  entidad cambiada: poder preguntar por dentro de los valores sin leer la tabla entera. Va en un
  `HasColumnType` de la configuración compartida, no en una consulta.

**Qué es «un maestro», entidad por entidad.** El criterio del ítem dice «un maestro» sin definirlo,
así que se define aquí sobre las diez entidades que existen hoy — y sobre la undécima fila de la
tabla, que es la propia traza, porque «el interceptor no se audita a sí mismo» también hay que
escribirlo en algún sitio. Va también en el **ADR-0012**, y no
la vigila la buena voluntad: la vigila `CadaEntidadDeclaraSuAuditoriaTests`, que recorre el modelo ya
construido y exige que no quede nada sin clasificar.

| Entidad | ¿Se audita? | Por qué |
|---|---|---|
| `Empresa` | **Sí** | El maestro raíz. Su NIF y su razón social salen impresos en cada factura: cambiarlos cambia un documento con validez fiscal. |
| `Ejercicio` | **Sí** | Su apertura y su cierre son la frontera de la R14: qué se puede seguir tocando y qué no. |
| `Serie` | **Sí** | La numeración fiscal. Tocar una serie es tocar la correlatividad de las facturas. |
| `Almacen` | **Sí** | Dónde está el stock. Su alta, su código y su bloqueo. |
| `Usuario` | **Sí**, sin el resumen | Alta, correo, nombre, bloqueo, último acceso. `HashDeContrasena` va marcado `Secreta` y no entra. |
| `Rol` | **Sí** | Un rol es un juego de poderes: cambiarlo cambia lo que puede hacer todo el que lo tenga. |
| `PermisoDeRol` | **Sí** | Conceder o retirar un permiso es *el* cambio que hay que poder reconstruir. No tiene ninguna propiedad que clasificar: sus dos columnas **son** la clave, así que el alta y la baja de la fila son el cambio entero. |
| `Membresia` | **Sí** | Quién pertenece a qué empresa: la frontera del inquilinato del 0.6 escrita en filas. Un alta aquí da acceso a los datos de una empresa entera. |
| `RolDeMembresia` | **Sí** | Qué rol tiene alguien en una empresa: la otra mitad de «quién puede qué». Como `PermisoDeRol`, sus dos columnas son la clave. |
| `TokenDeRefresco` | **No** | El «no» de la lista, por dos motivos que se suman: rota cada quince minutos —una fila por acceso y otra por renovación—, así que llenaría de ruido una tabla que no se puede limpiar; y lleva `Hash`, un resumen de credencial. Lo que de ella interesa a una auditoría es «quién entró y cuándo», y eso ya deja traza en `Usuario.UltimoAccesoEn`. |
| `RegistroDeAuditoria` | **No** | Es la traza. **El interceptor no se audita a sí mismo:** sería recursión, no información. |

`Direccion` no está en la lista porque **no es una entidad**, es un objeto de valor poseído. EF Core
la sigue como una entrada aparte del rastreador, y el interceptor la **pliega** en la fila de su
dueño con el nombre de la navegación por delante (`Direccion.Calle`). Sustituirla entera —que es lo
que hace un `Modificar` de dominio— aparece como dos entradas con la misma clave, una baja y un
alta; plegadas, vuelven a ser el antes y el después de las mismas propiedades.

**Y solo lo que cambió.** Una modificación lista únicamente las propiedades cuyo valor es distinto,
comparando el valor **tal como va a la columna** y no la bandera `IsModified` del rastreador, que se
enciende para todas las columnas de un objeto de valor sustituido aunque vuelvan a llevar lo mismo.
Una modificación que no cambia nada auditado **no deja fila**; un alta y una baja sí la dejan aunque
no tengan valores que enseñar.

### La prueba fuerte del 0.7: seis mutaciones, y las dos que hay que contar enteras

Cada una se aplicó sobre el árbol verde, se compiló, se corrió la tanda de auditoría (20 casos) y se
revirtió comprobando que el fichero volvía byte a byte al original.

| Mutación | Qué se rompió | Resultado |
|---|---|---|
| Quitar `AddInterceptors` del cableado de Organización | El módulo deja de escribir traza | **5 rojos**, todos de `UnCambioEnUnMaestroDejaSuRastroTests` |
| Escribir la traza **después** de que el guardado fuera bien (capturar en `SavingChanges`, volcar en `SavedChanges` con un segundo `SaveChanges`) | La ruta no atómica que el usuario nombró | **1 rojo**, y solo uno: `La_fila_y_su_traza_las_escribe_LA_MISMA_transaccion` |
| Escribir la traza en **otra conexión**, con su propia transacción | La otra ruta no atómica | **2 rojos**: el del `xmin` y `Un_guardado_que_revienta_no_deja_ni_la_fila_ni_su_traza` |
| Quitar la función y los dos disparadores de la migración | La tabla deja de ser de solo añadido | **3 rojos**: `UPDATE`, `DELETE` y `TRUNCATE` |
| Cambiar `.EsSecreta(…)` por `.SeAudita()` en `Usuario.HashDeContrasena` | El resumen de credencial entra en la traza | **2 rojos** en integración, y la tanda rápida **en verde** |
| Quitar la guarda de escritura del interceptor | Se puede escribir en la empresa de otro | **2 rojos**, y el control positivo sigue verde |

**Las dos que hay que contar enteras:**

1. **Una mutación salió verde y no cuenta como superviviente: no llegó a mutar nada.** Poner
   `AutoTransactionBehavior.Never` en el contexto —para que `SaveChanges` no abriera transacción— no
   cambió ni un test. El motivo es que EF Core manda los dos `INSERT` en **un solo comando**, y
   PostgreSQL envuelve un comando con varias sentencias en una transacción implícita: quitar la
   explícita no quita la atomicidad. Es un intento de mutación fallido, no una prueba de nada, y por
   eso se repitió por el camino de la conexión aparte, que sí muta lo que se quería mutar.
2. **La mutación del secreto deja la tanda rápida en verde, y eso es información.**
   `CadaEntidadDeclaraSuAuditoriaTests` comprueba que **todo** esté clasificado, no que lo esté
   *bien*: `SeAudita()` sobre un resumen de contraseña es una clasificación perfectamente válida
   para él. Lo que la caza es el test de **efecto**, que lee el resumen de la base y lo busca dentro
   de las filas de traza. Por eso el punto de los secretos pedía las dos cosas, y por eso no bastaba
   con el barrido del modelo.
3. **Y la primera fila de la tabla y la segunda dicen lo mismo desde dos lados:** la ruta «escribir
   después» pasa los dos tests de atomicidad que uno escribiría de primeras. Sin el test del `xmin`,
   esa ruta habría sobrevivido a esta batería entera.


### Tomadas por el agente de desarrollo — ítem 0.8 (2026-08-29)

Todo lo de abajo está razonado en
`docs/adr/adr-0013-el-evento-va-en-la-misma-transaccion-y-el-efecto-ocurre-una-vez.md`.

- **El evento entra en el mismo `SaveChanges` que el cambio, por la ruta 1 del 0.7.** Las dos tablas
  se mapean en el contexto de **cada** módulo apuntando al esquema `auditoria`, y un
  `SaveChangesInterceptor` vuelca en `SavingChanges` los eventos que los agregados llevan en la
  mano. **Las dos alternativas se escriben descartadas:** un contexto aparte alistado en la
  transacción del módulo —obliga a que cada caso de uso de cada módulo abra y comparta una
  transacción explícita, y el que se olvide no falla: pierde el evento— y un `INSERT` a mano sobre
  la conexión —SQL crudo esquivando el barrido del 0.6—.
- **La prueba de la atomicidad vuelve a ser el `xmin`,** y con una condición escrita en el fichero:
  **ahí no corre el publicador**, porque marcar una fila es un `UPDATE` y un `UPDATE` le cambia el
  `xmin` a la fila. Los otros dos tests —un guardado que revienta no deja ni la empresa ni su
  evento; uno que va bien deja las dos— los pasa **también** la ruta de volcar después del guardado.
- **Los eventos viajan en el agregado (`RaizAgregado`), no en un recolector de ámbito.** La
  atomicidad la darían los dos; lo que solo da esta es que **un evento no pueda existir sin su
  escritura**: a la bandeja solo llega el evento de un agregado que se está guardando. Con una lista
  suelta por petición, un evento registrado sin guardar nada se colaría en el `SaveChanges`
  siguiente, que puede ser el de otra cosa. Y el agregado olvida sus eventos **cuando el guardado ya
  ha ido bien**, no al volcarlos.
- **`Bastion.Organizacion.Contracts` pasa a referenciar `Bastion.BuildingBlocks.Domain`.** Es la
  única frontera que este ítem mueve, y es de fondo y no de grado: ese bloque es el **núcleo
  compartido** (`Nif`, `Direccion`, `Resultado`, `EventoDeIntegracion`), no el dominio de ningún
  módulo, y todos los módulos lo ven ya por sus propias capas. Sin ella un evento sería un `object`.
  El motivo de fondo es que el `Domain` de un módulo no ve su `Contracts` —las dependencias apuntan
  hacia dentro—, así que el evento lo construye la capa de aplicación y se lo entrega a la raíz.
- **El nombre con el que viaja un evento lo declara el propio contrato** (`EmpresaCreada.Nombre`), y
  lo usan igual el cableado de producción y los tests. Escrito dos veces, se separa el día que
  alguien renombra uno de los dos, y lo que se rompe no es un test: es la cola, con filas cuyo
  nombre ya no declara nadie.
- **Las dos tablas viven en el esquema `auditoria` y las migra el módulo Auditoría.** No es que la
  bandeja sea auditoría: el §5 lista dieciséis módulos y **ninguno es la bandeja**, así que no hay
  esquema del que pudiera ser —la convención dice que el esquema es el nombre del módulo—, inventar
  un decimoséptimo módulo reabriría el §5, y un esquema sin módulo que lo migre no lo puede crear
  nadie. Queda el dueño que ya tiene la otra tabla que escriben todos los contextos dentro de su
  transacción. Los demás contextos la declaran con `ExcludeFromMigrations`: sin migraciones vacías,
  y `scripts/comprobar-migraciones.sh` en verde para los tres módulos.
- **Un despachador propio de unas decenas de líneas, no un bus en memoria.** En un monolito modular
  un bus no aporta nada sobre una interfaz y sí quita algo: quién atiende deja de decirlo el
  compilador. Y la interfaz del manejador **no es genérica** a propósito: cerrarla por reflexión
  (`MakeGenericType`) y resolver del contenedor un tipo construido es la clase de código que falla
  en el arranque de producción y en ningún test. **Ningún paquete NuGet nuevo** entra con este ítem.
- **La cola la vacía un `BackgroundService`: cada dos segundos, cien filas por vuelta, ordenadas por
  `id`** —versión 7, o sea, orden de escritura—. **Un solo lector**, garantizado con un cerrojo
  consultivo de PostgreSQL. Las otras dos formas se miraron: suponer una sola instancia desplegada
  es gratis y falla publicando dos veces —dos correos, dos asientos, dos remesas— en silencio el día
  que alguien escale a dos réplicas; y `FOR UPDATE SKIP LOCKED` con varios lectores es **más** SQL
  crudo, este sí sobre filas, y **pierde el orden** — que pesa, porque la R15 va a necesitar
  exactamente un consumidor serializado.
- **La excepción a la prohibición de SQL crudo del 0.6 es una, está nombrada por su ruta y no se
  extiende.** `CerrojoDeLaBandeja.cs`, listado en `ElFiltroNoSeSaltaPorAhiTests`. El argumento: la
  prohibición existe porque el SQL a mano no pasa por el traductor y devuelve filas de otros
  inquilinos sin fallar; **estas dos órdenes no leen ninguna tabla** —toman y sueltan un cerrojo del
  motor con una clave constante—, así que no hay fila que filtrar. Se justifica por lo que la hace
  inútil para cualquier otro caso: quien quiera SQL crudo para leer *filas* tendrá que traer su
  propio argumento.
- **Se marca DESPUÉS de despachar: la entrega es «al menos una vez» por decisión.** Marcar antes
  haría que el fallo de un manejador se **tragara** el evento: cola limpia, nadie lo reintenta, el
  efecto no ocurre jamás y **no hay ningún error que mirar**. Lo que convierte «al menos una» en
  «exactamente una» es la huella del par **(evento, consumidor)** — con solo el evento, el segundo
  consumidor de un mismo hecho no llegaría a ejecutarse nunca.
- **La ventana que queda abierta está escrita:** la huella se graba en su propia transacción, no en
  la del efecto del manejador, así que una caída entre «el manejador terminó» y «la huella está
  grabada» repite ese manejador. Cerrarla exige que el manejador escriba efecto y huella en el mismo
  `SaveChanges`, y por eso `EventoProcesado` está mapeado también en los contextos de módulo: la
  puerta queda abierta y sin usar. La fase 0 no tiene ningún manejador con efecto de negocio; se
  decide el día que haya uno delante.
- **La unidad de aislamiento del fallo es la fila, y a los cinco intentos se aparca.** Uno
  confundiría un corte de red con un evento imposible; cien serían ocho minutos de vueltas y cien
  excepciones iguales en el registro. Aparcar cuesta el orden **de ese evento** y salva el de todos
  los demás. Y no se aparca en silencio: queda dicho una sola vez, con el motivo dentro de la fila.
- **El publicador no tiene petición detrás, y por eso abre su ámbito sin inquilino con un motivo
  nuevo: `PublicacionDeEventos`** — el que el 0.6 dejó reservado por escrito. **La lista de
  aperturas pasa de once a doce**, y se compara entera. La alternativa —un contexto sin filtro para
  el trabajo de fondo— sería un segundo mecanismo para saltarse el inquilinato, sin lista cerrada y
  sin quedar anotado.
- **`EventoDeLaBandeja` no implementa `IDeInquilino`** —su empresa es anulable, y la cola es de
  todas las empresas a la vez: un publicador por empresa sería un publicador por cada una de las que
  existan—, así que lleva **o** la empresa desde la que se actuó **o** el motivo, nunca las dos y
  nunca ninguna: constructor y `CHECK` de la tabla. **`EventoProcesado` es global a propósito.** Las
  dos están clasificadas en los **dos** barridos del modelo, el de inquilinato y el de auditoría.
- **Se vigila con una métrica y NO con una sonda**, que es la lección del 0.2 otra vez: una cola
  atrasada no significa que el proceso esté colgado —la sonda de vida reiniciaría la API en bucle, y
  reiniciar no vacía la cola— ni que no pueda atender tráfico. Y la métrica es **la edad del
  pendiente más viejo**, no cuántos hay: el tamaño sube y baja con el tráfico y no distingue mil
  eventos que salen en dos segundos de uno atascado desde ayer. Más dos contadores separados,
  publicados y aparcados, porque sobre uno se pone alerta y sobre el otro no. La lista de sondas
  registradas se compara entera.
- **Contra una base sin migrar el publicador se para y lo dice una vez.** Es el riesgo abierto del
  compose hasta el 0.13; hasta entonces, un error por vuelta desde el arranque no es información, es
  donde se esconden los errores de verdad. Que la API siga sirviendo es correcto: lo que falta es la
  cola, no la API.
- **Hangfire se aplaza, y se dice por qué.** El §3 lo nombra para trabajos de fondo. Aquí no hace
  falta nada de lo que aporta —horarios, reintentos con almacén propio, panel, trabajos encolados
  por el usuario—: esto es un bucle que mira una tabla cada dos segundos y su almacén **es** la
  tabla que mira. Envolver un `while` en un programador de tareas con su propio esquema añadiría un
  mecanismo de reintentos encima del que la bandeja ya tiene, y dos sitios donde mirar cuando algo
  falla. Entra cuando aparezca su caso: trabajos con horario —cierres, remesas, envíos periódicos—.

### La prueba fuerte del 0.8: seis mutaciones, y la que salió verde contada entera

Cada una se aplicó sobre el árbol verde, se compiló, se corrió la tanda que le tocaba y se revirtió
comprobando que el fichero volvía al original.

| Mutación | Qué se rompió | Resultado |
|---|---|---|
| Volcar los eventos en `SavedChangesAsync`, después de que el guardado fuera bien | La ruta no atómica | **1 rojo, y solo uno:** `La_empresa_y_su_evento_los_escribe_LA_MISMA_transaccion`. Los otros tres de la clase, verdes |
| Quitar el `AddHostedService` del publicador | Nadie vacía la cola | **2 rojos**, los dos de `ElAltaDeUnaEmpresaSePublicaTests` |
| Marcar la fila `Publicado` **antes** de despachar | «Al menos una vez» se convierte en «como mucho una vez» | **4 rojos** en `ElTrabajoDeFondoVaciaLaColaTests` |
| Que la comprobación del duplicado mire, avise y **no** se salte al manejador | Reprocesar duplica el efecto | **2 rojos de 4** en `ReprocesarNoDuplicaTests`; los otros dos son los controles que tienen que seguir verdes |
| Quitar el ámbito `SinInquilino` del publicador | El trabajo de fondo se queda sin empresa y sin motivo | **4 rojos**: sin ámbito, `EmpresaDelFiltro` lanza y no se publica nada |
| Que una tabla ausente caiga por la rama genérica en vez de reconocerse | El publicador deja de pararse | **2 rojos**, los dos de `SinLaTablaElPublicadorSeParaTests` |

**La que salió verde, entera, porque desmiente algo que yo mismo había escrito en el código.** La
sexta mutación iba a ser otra: quitar de la clasificación de errores la rama de
`invalid_schema_name` (3F000), dejando solo `undefined_table` (42P01). El comentario del publicador
decía —lo escribí yo— que *«son dos códigos, y el segundo es el que de verdad pasa»*, y que ese
detalle **lo había encontrado el test**. Con la mutación puesta, los dos casos de
`SinLaTablaElPublicadorSeParaTests` siguieron **verdes**.

Lo que hay detrás, comprobado lanzando las dos consultas contra `postgres:17.6-alpine`, la misma
imagen de los tests: **un `SELECT` sobre una tabla cuyo esquema no existe responde 42P01**
—«relation ... does not exist»—, no 3F000. El 3F000 lo dan las órdenes que **crean** algo dentro de
un esquema ausente, y el publicador no crea nada. O sea: la rama del 3F000 era **código muerto** y
el comentario que la justificaba era falso. Se ha quitado la rama, se han reescrito el comentario
del publicador y el de la cabecera del test con lo que sí se ha verificado, y la sexta mutación se
ha rehecho por el camino que sí muta algo —hacer que la tabla ausente no se distinga—, que pone los
dos casos en rojo.

**Y una que hubo que repetir porque el primer intento no mutaba lo que decía mutar.** Mover el
volcado a `SavedChangesAsync` a secas no escribía **nada**, porque el volcado filtra las entradas
por «se está guardando» y después del guardado las entidades están `Unchanged`: tres tests en rojo,
pero por el motivo equivocado —un interceptor que no vuelca no es la ruta no atómica, es ningún
interceptor—. Con el filtro quitado también, la mutación es la que quería ser y deja **un solo**
rojo: el del `xmin`. Sin ese test, la ruta de escribir después habría sobrevivido a la batería
entera.


### Tomadas por el agente de desarrollo — ítem 0.9 (2026-08-31)

Todo lo de abajo está razonado en
`docs/adr/adr-0014-la-clave-del-cliente-y-la-version-del-recurso-son-dos-mecanismos.md`.

- **Son dos mecanismos, no dos niveles de uno.** La `Idempotency-Key` (R10) protege de que **una**
  persona repita su propia petición —el móvil que pierde cobertura al enviar y reintenta solo—; el
  `If-Match` (R11) protege de que **dos** personas pisen el mismo recurso —Ana guarda, Luis guarda
  encima y lo de Ana desaparece sin un solo error—. Un alta no tiene versión previa que citar, así
  que solo la protege la primera; una modificación ya la trae de su lectura, así que le basta la
  segunda. **Ninguna acción pide las dos**, y hay un barrido que lo mantiene así.
- **La identidad de una petición repetible es la tupla entera**: `(empresa, usuario, método, ruta,
  clave)`, que es también la clave primaria de la tabla. Con la clave sola, dos clientes que
  eligieran la misma —`1`, `test`, el UUID de una plantilla copiada— se cruzarían las respuestas, y
  el segundo leería el recurso de otra empresa **como si fuera suyo**: no hay error que mirar, hay
  un dato de otro presentado como correcto.
- **La huella del cuerpo se calcula sobre los BYTES tal como llegaron**, antes de deserializar, y es
  un SHA-256. Sobre el objeto ya deserializado dependería del serializador, y cambiar una opción
  cambiaría la identidad de peticiones ya guardadas. **La contrapartida se escribe**: dos cuerpos que
  solo difieren en espacios en blanco tienen huellas distintas, así que el segundo intento sale
  `409` en vez de repetirse. El cuerpo **no se guarda**; se guarda su huella.
- **Reclamar la clave es un `INSERT … ON CONFLICT DO NOTHING`,** la única sentencia cruda del
  mecanismo. Las dos alternativas son peores: *mirar y luego insertar* reintroduce, dentro de la
  propia implementación, la carrera que el mecanismo viene a impedir; *insertar y atrapar la
  violación del índice* usa una excepción como flujo de control dentro de una transacción que
  PostgreSQL deja **abortada**, así que el `catch` no puede seguir trabajando. **La excepción al
  barrido del 0.6 se gana por su argumento**, igual que el cerrojo del 0.8: esa sentencia **no lee
  ninguna tabla**, y la fila que escribe lleva `empresa_id` dentro de su clave primaria completa,
  tomado del *claim*. `LaClaveDeIdempotenciaEsLaTuplaEnteraTests` comprueba que sigue siendo verdad.
- **El recibo cae en la misma transacción que el trabajo, y el filtro es el dueño de esa
  transacción.** La invariante es «la fila existe si y solo si el trabajo ocurrió»: solo se guarda
  la respuesta de un `2xx`, y un fallo deja la clave libre para que el mismo reintento pueda salir
  bien. Guardar el recibo de un `409` dejaría al cliente atrapado con la clave quemada.
- **Y la transacción va SIN puntos de guardado automáticos** (`AutoSavepointsEnabled = false`). No es
  afinar: con ellos, un `SaveChanges` que falla vuelve al punto de guardado y **deja viva** la
  transacción con la clave ya reclamada dentro. Sin ellos, aborta entera y no hay nada que confirmar:
  la invariante deja de depender de que el filtro se acuerde. El precio está dicho en el ADR.
- **Los almacenes se registran con clave** (`AddKeyedScoped`), y la clave es el segmento de módulo de
  la ruta —que es también el nombre de su esquema—. Registrados bajo el tipo a secas, el último
  módulo desplazaría a los demás y las claves de Organización se apuntarían en la transacción de
  Identidad: dos transacciones para un trabajo que tenía que ser uno, sin error y sin rastro.
- **El testigo de concurrencia es `xmin`**, declarado como propiedad de sombra `Version`. No es un
  contador nuestro, así que no hay que mantenerlo. **La trampa se comprobó antes de diseñar**: por un
  camino con `AsNoTracking()`, leer el testigo devuelve **`0` sin lanzar nada** —EF adjunta la
  entidad en ese momento y la sombra nace a cero—, y ese cero saldría dentro de un `ETag`
  convirtiendo todo `If-Match` en un `412` perpetuo. `Versiones.De` comprueba el rastreo antes de
  preguntar y falla ruidosamente.
- **Las migraciones del testigo existen y no emiten SQL.** El diferenciador escribe un `AddColumn`
  porque ve una columna nueva, pero `xmin` ya está en toda tabla de PostgreSQL:
  `dotnet ef migrations script` sobre ellas produce **solo** el `INSERT` en el historial. Se conservan
  para que modelo y migraciones sigan cuadrando.
- **El `412` sale de la política central**, de un `IExceptionHandler` que traduce
  `DbUpdateConcurrencyException`, y no de un `catch` por acción: el que faltara devolvería un `500`
  que el cliente reintentaría tal cual, machacando lo que el otro escribió.
- **El estado actual del conflicto se sirve como versión y en el CUERPO**, en la extensión
  `versionActual`. La cabecera `ETag` era el primer diseño y **no llega**: el middleware de
  excepciones de ASP.NET Core la borra de toda respuesta de error. Y hace bien —el `ETag` etiqueta
  la representación que va en esa respuesta, que en un `412` es un documento de problema—, así que
  no se le busca la vuelta con otro nombre de cabecera.
- **El comodín `If-Match: *` no se admite**, aunque el RFC lo defina: significa «me vale cualquier
  versión con tal de que el recurso exista», que es saltarse el control entero sin dejar de cumplir
  el protocolo.
- **Una cabecera presente y vacía es un `400`, no una petición sin proteger.** «Sin cabecera» es que
  no venga, y se pregunta por el número de valores, no por si el texto está en blanco. Lo encontró
  un test: el filtro trataba una clave en blanco como ausente y atendía la petición sin protección
  ninguna, que es el cliente creyéndose protegido y duplicando al reintentar.

#### Qué recurso lleva qué, decidido con un barrido y no de memoria

`TodaEscrituraDiceComoSeProtegeTests` recorre por reflexión los dos ensamblados de *endpoints*.
Hoy: **46 acciones**, de ellas **32** cambian estado — **13** exigen `If-Match`, **6** admiten
`Idempotency-Key` y **13** están exentas con su motivo escrito. Los números están fijados en el
propio test: un barrido cuya enumeración devuelva nada saldría verde por la peor de las razones.

> **Movido en el 0.10** (eran 16 y 10). Los tres `POST /{id}/desbloqueo` pasaron de exigir
> `If-Match` a estar exentos con su motivo: R16 hace que un recurso bloqueado no emita `ETag`, así
> que la precondición pedía una llave que el propio mecanismo esconde. El argumento entero está en
> el [ADR-0017](adr/adr-0017-el-desbloqueo-no-puede-pedir-una-llave-que-el-bloqueo-esconde.md). El
> total de acciones que cambian estado **no se mueve**, y eso es lo que dice que fue una mudanza y
> no una acción nueva colada sin protección. Las tablas de abajo están ya con los números de hoy.

**Los seis recursos que emiten `ETag` en su lectura por identificador:**

| Recurso | Ruta del `GET` que emite el `ETag` |
|---|---|
| Almacén | `GET /api/v1/organizacion/almacenes/{id}` |
| Ejercicio | `GET /api/v1/organizacion/ejercicios/{id}` |
| Empresa | `GET /api/v1/organizacion/empresas/{id}` |
| Serie | `GET /api/v1/organizacion/series/{id}` |
| Rol | `GET /api/v1/identidad/roles/{id}` |
| Usuario | `GET /api/v1/identidad/usuarios/{id}` |

Los listados **no** lo emiten: un `ETag` sobre una página sería el de la página, no el de cada
elemento, y un cliente que lo devolviera en un `If-Match` estaría citando una versión que no es la
del recurso que escribe.

**Las trece operaciones que exigen `If-Match`:**

| Recurso | Operaciones |
|---|---|
| Almacén | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Ejercicio | `PUT /{id}`, `DELETE /{id}`, `POST /{id}/cierre`, `DELETE /{id}/cierre` |
| Empresa | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Serie | `PUT /{id}`, `DELETE /{id}` |
| Rol | `PUT /{id}` |
| Usuario | `PUT /{id}`, `DELETE /{id}` (bloqueo) |

Las subrutas —el bloqueo, el cierre— citan la versión **del recurso**, no una suya: no son otro
recurso, son otra puerta al mismo. Es lo que hace que bloquear un almacén y modificarlo compitan por
la misma versión, que es lo que se quiere.

**Las seis rutas que admiten `Idempotency-Key`** — las seis altas, y solo ellas:

| Ruta | Módulo | Almacén que la atiende |
|---|---|---|
| `POST /api/v1/organizacion/almacenes` | `organizacion` | `AlmacenDeIdempotenciaDeOrganizacion` |
| `POST /api/v1/organizacion/ejercicios` | `organizacion` | ídem |
| `POST /api/v1/organizacion/empresas` | `organizacion` | ídem |
| `POST /api/v1/organizacion/series` | `organizacion` | ídem |
| `POST /api/v1/identidad/roles` | `identidad` | `AlmacenDeIdempotenciaDeIdentidad` |
| `POST /api/v1/identidad/usuarios` | `identidad` | ídem |

**Y las trece exentas, con el motivo resumido** (el entero está en el test):

| Acción | Por qué |
|---|---|
| `Sesiones.Iniciar` | Anónima por definición: no hay tupla (empresa, usuario) con la que formar clave, y su respuesta lleva credenciales dentro |
| `Sesiones.Renovar` | Lo mismo, y encima el refresco YA es de un solo uso: la protección está en el dominio |
| `Sesiones.Cerrar` | Cerrar una sesión cerrada es cerrarla; y es anónima a propósito, para poder cerrar con un token caducado |
| `Sesiones.CambiarEmpresa` | Emite un token nuevo; repetirlo emite otro igual de válido y no acumula nada |
| `Usuarios.CambiarContrasenaPropia` | Mismo estado al repetirse, y su precondición ya viaja en el cuerpo: hay que presentar la contraseña de ahora |
| `Usuarios.Restablecer` | Mismo estado al repetirse; y su cuerpo ES la contraseña nueva, así que no se le invita a reintentar desde donde sea |
| `Usuarios.Conceder` / `Usuarios.AsignarRol` | Repetirlo choca con su clave: `409`, no un segundo efecto. Y un `If-Match` **sobre el usuario** no protegería la fila que se toca — una cabecera que parece proteger sin proteger es peor que no tenerla |
| `Usuarios.Retirar` / `Usuarios.RetirarRol` | Retirar lo que ya no está es un `404`, no un segundo efecto; mismo motivo para el `If-Match` |
| `Empresas.Desbloquear` (0.10) | Una empresa bloqueada contesta `404` a su propio `GET`, así que no hay `ETag` que citar. Y no hace falta: mientras está bloqueada ninguna otra escritura llega a la fila, y desbloquear dos veces deja el mismo estado |
| `Almacenes.Desbloquear` (0.10) | Lo mismo. El testigo de concurrencia **sigue** comparándose dentro de la petición; lo que desaparece es la precondición que cita el cliente, no la protección |
| `Usuarios.Desbloquear` (0.10) | Igual que las dos de Organización. Levanta el bloqueo del art. 32, **no** el rechazo temporal por intentos fallidos, que vive en `rechazado_hasta` y se levanta solo |

#### La prueba fuerte del 0.9: siete mutaciones, y la que no llegó a mutar

Cada una se aplicó sobre el árbol verde, se compiló, se corrió la tanda que le tocaba y se revirtió.

| Mutación | Qué se rompió | Resultado |
|---|---|---|
| Quitar `empresa_id` del `ON CONFLICT` de la sentencia cruda | La excepción al barrido del 0.6 deja de ser cierta | **3 rojos** en `LaClaveDeIdempotenciaEsLaTuplaEnteraTests`; los otros 5 verdes |
| Guardar el recibo también cuando el trabajo falla (`exito = true`) | La clave se quema con un intento rechazado | **1 rojo, y solo uno:** `Un_alta_rechazada_deja_la_clave_libre_para_el_reintento` |
| Devolver los puntos de guardado automáticos de EF Core | El recibo y el trabajo dejan de compartir transacción visible | **1 rojo:** `El_recibo_y_el_almacen_llevan_el_mismo_xmin` |
| Aceptar el comodín `If-Match: *` | Se puede saltar el control cumpliendo el protocolo | **1 rojo:** la fila `*` de la teoría del `400`; las otras cuatro verdes |
| Que `Versiones.Exigir` no meta la versión en el `WHERE` | La actualización perdida vuelve entera | **3 rojos:** el `412`, los dos escritores simultáneos y «tras un 412, ni traza ni evento» |
| Comparar la **longitud** de la huella en vez de la huella | La segunda vez se atiende como si fuera la primera | **1 rojo:** `La_misma_clave_con_otro_cuerpo_es_409` |
| Quitar `[AdmiteIdempotencia]` de `SeriesController.Crear` | El barrido deja de ver una ruta que sí estaba protegida | **2 rojos** en `TodaEscrituraDiceComoSeProtegeTests`, incluido el que fija el inventario |

**Ninguna sobrevivió. Y una hubo que rehacerla porque no llegó a mutar**, que es la distinción que
separa una batería de un ritual. La sexta iba a ser `CoincideElCuerpo(huella) => true`, y **no
compiló**: CA1822 —«el miembro no accede a datos de instancia y puede marcarse como `static`»— está
tratado como error en este proyecto. Sin mirar la salida de la compilación, esa mutación se habría
apuntado como «cero rojos, sobrevive» y habría acusado a los tests de un agujero que no tenían.
Rehecha como `Huella.Length == huella.Length` —que sí compila, y siempre es verdad porque las dos
son SHA-256 en hexadecimal— mata el test que tenía que matar.


### Tomadas por el agente de desarrollo — ítem 0.10 (2026-08-31)

El ítem **no añade una función: unifica lo que estaba escrito tres veces.** `EstadoDeEmpresa`,
`EstadoDeAlmacen` y `EstadoDeUsuario` eran tres enumerados de dos valores, con tres `BloqueadoEn`
sueltos al lado, tres `Bloquear`/`Desbloquear` copiados y **ningún motivo**. Se borra la copia de
dentro y se llama a la compartida; nunca al revés.

Decisiones en `docs/adr/adr-0016-el-bloqueo-es-uno-y-tapa-a-las-tres.md` y
`docs/adr/adr-0017-el-desbloqueo-no-puede-pedir-una-llave-que-el-bloqueo-esconde.md`.

- **Un solo `Bloqueo`, objeto de valor, con las tres piezas juntas** (si está bloqueado, desde
  cuándo y por qué) en `BuildingBlocks/Domain/Bloqueos`. Un enumerado deja la fecha y el motivo
  sueltos al lado, donde nada obliga a que estén puestos cuando el estado lo dice ni vacíos cuando
  no; y de esa fecha cuelga el plazo de prescripción del art. 32 de la LOPDGDD. El motivo es
  **lista cerrada** —`SupresionSolicitada`, `CeseDeUso`— porque es lo que permite contestar años
  después por qué esos datos siguen guardados, y las dos razones no caducan igual.

- **La transición es comportamiento, y las dos preguntas incómodas tienen respuesta escrita.**
  *Bloquear lo ya bloqueado devuelve el bloqueo de antes, entero*: no mueve la fecha —moverla
  alargaría la conservación de datos personales sin que nadie lo decidiera— ni pisa el motivo, y de
  paso hace idempotente el `DELETE`. *Desbloquear lo que no está bloqueado no es un error*: la
  postcondición ya se cumple, y lanzar obligaría a preguntar antes, cosa que no es atómica. Las once
  respuestas están en `BloqueoTests`.

- **El filtro tapa a las tres: empresa, usuario y almacén. Sin lista de excepciones.** Filtro global
  `"Bloqueo"`, hermano del `"Inquilinato"` del ADR-0011. El motivo de bloquear un almacén no es el
  art. 32 —un almacén no es una persona— sino no romper el histórico de valoración; pero el
  mecanismo es el mismo **a propósito**: una excepción «solo para el almacén» sería el primer sitio
  donde mirar para saber si el filtro tapa de verdad, y la segunda llegaría con menos discusión que
  la primera. Cuando la fase 3 necesite leer el almacén de un movimiento histórico, abrirá un ámbito
  declarado.

- **`GET` y `PUT` sobre lo bloqueado contestan 404, no 409.** Un 409 que dijera «esa empresa está
  bloqueada» revela a la vez que el registro existe y en qué estado está, que es exactamente el
  tratamiento —la visualización— que el bloqueo impide. Los códigos `empresa-bloqueada` y
  `almacen-bloqueado` se **borran del catálogo** en vez de quedarse sin quien los emita.

- **Ver lo bloqueado es una apertura declarada, enumerada y con motivo.** `ViendoLoBloqueado(...)`,
  ámbito `AsyncLocal`, rastro en el registro, y **la lista de sitios donde se abre se compara
  entera**. Hoy: los tres desbloqueos y nada más. `.IgnoreQueryFilters(` sigue prohibido y ahora con
  un argumento más fuerte — **apaga los dos filtros**, así que quien lo escribiera para ver una fila
  bloqueada abriría de paso el de empresa sin enterarse.

- **Los dos bloqueos del usuario siguen separados.** El `Bloqueo` del art. 32 lo decide una persona,
  lleva fecha y motivo, no caduca solo y saca la fila de todas las consultas; `RechazadoHasta` es el
  rechazo temporal por intentos fallidos, se levanta solo y no oculta nada. Fundirlos haría que
  fallar la contraseña cinco veces diera de baja la cuenta para siempre.

- **`EntidadBase` aporta las dos marcas de tiempo y nada más.** No aporta identidad —cada entidad
  declara su `Id`, que es como estaba y no era lo que se había escrito tres veces— ni bloqueo
  —bloquearse no le pasa a todo el mundo: un ejercicio se cierra, no se bloquea—.

- **Dos marcas, dos mecanismos, y ninguno es un `DEFAULT now()`.** `CreadoEn` la pone el dominio en
  la fábrica: ocurre en un solo sitio por entidad, así que se puede sostener, y así la entidad nace
  completa incluso en una prueba unitaria. `ModificadoEn` la pone un interceptor: cambia en todos
  los métodos que tocan algo, presentes y futuros, y sostenerla a mano significa que el día que
  alguien escriba un método nuevo y no se acuerde, la marca deja de moverse **sin que nada falle**.
  Un `DEFAULT now()` ataría las dos al reloj del servidor de base de datos —el único que una prueba
  no puede adelantar— y metería una sexta forma de valor generado por el servidor en un modelo donde
  lo único que lo genera son los testigos del ADR-0015: se comprobó mutándolo, y pone rojo el
  ADR-0015 en tres casos.

- **Los tres `POST .../desbloqueo` pierden el `If-Match` (ADR-0017).** No es aflojar R11: la
  etiqueta se obtiene leyendo el recurso, y desde este ítem un recurso bloqueado contesta 404 a su
  propio `GET`. Una precondición cuya llave no hay manera de conseguir no es una precondición, es un
  muro. Y no hay nada que perder: mientras está bloqueado ninguna otra escritura llega a la fila
  —todas la piden al repositorio y el filtro no se la da—, y desbloquear dos veces deja el mismo
  estado. **El testigo `xmin` sigue comparándose dentro de la petición**; lo que desaparece es la
  cabecera que cita el cliente, no la protección. Las firmas pierden el parámetro de versión: no es
  que no se pida, es que no se puede pedir, y la firma lo dice.

- **`Direccion` pasa de tipo poseído a tipo complejo.** Un objeto de valor no tiene identidad y un
  tipo poseído sí: EF Core le sintetiza una clave y lo saca en `GetEntityTypes()`. El cambio resultó
  **neutro para el esquema** —las mismas seis columnas, ninguna migración— así que lo único en juego
  era decir la verdad sobre el modelo… y lo que se descubrió al medirlo, que está abajo.

- **Las migraciones derivan los datos, no los inventan.** Lo generado por el andamiaje tiraba
  `estado` antes de leerlo y creaba `bloqueado` a `false`: sobre una tabla con filas, eso es
  desbloquear a todo el mundo en silencio, y el dato que se pierde ahí es justo el que el art. 32
  obliga a conservar. Escritas a mano, con el patrón **añadir anulable → `UPDATE` que deriva →
  cerrar a `NOT NULL`**: un `NOT NULL` directo necesita un `DEFAULT` para las filas existentes, y
  ese `DEFAULT` **se queda en la tabla** aunque el modelo no lo declare — una divergencia que ningún
  barrido ve. El `Down` rehace `estado` a partir de `bloqueado` antes de tirarlo: deshacer una
  migración no puede ser una forma de desbloquear en silencio.

- **A las filas viejas se les elige un valor y se escribe el motivo.** `creado_en` de una fila
  anterior al 0.10 no lo sabe nadie; lo único cierto es que ya existía cuando la migración corrió, y
  ese instante es la cota superior más ajustada que se puede afirmar. Todas comparten el mismo valor
  al milisegundo, y esa coincidencia es la señal de que el dato está derivado y no observado. El
  `0001-01-01` del andamiaje no dice «no se sabe»: dice que la empresa se dio de alta en el año uno.
  `usuarios.modificado_en` sí se sabe —se pone a `creado_en`, que existe desde el 0.5—.

- **Las propiedades nuevas del tipo base se clasificaron una a una.** `CreadoEn` **se audita**: no
  cambia nunca después del alta, así que un cambio suyo es exactamente lo que la traza existe para
  contar. `ModificadoEn` **no se audita**, con motivo escrito: cambia en todas las modificaciones y
  el instante de cada una ya viaja en el `ocurrido_en` de la propia fila de traza. El barrido de
  clasificación se puso rojo al nacer `Bloqueo`, como estaba anunciado, y ese rojo es el mecanismo
  funcionando.

#### Ninguna dependencia nueva

No se ha añadido ni un paquete. En particular **no** se trajo
`Microsoft.Extensions.TimeProvider.Testing` por su `FakeTimeProvider`: lo único que hacía falta era
que `GetUtcNow()` devolviera un instante elegido, y eso son tres líneas (`RelojParado`). Una
dependencia se justifica por lo que ahorra.

#### La prueba fuerte del 0.10: seis mutaciones, y la que hay que leer entera

Cada una aplicada sobre el árbol verde, compilada, ejecutada y revertida.

| Mutación | Qué se rompía si nadie miraba | Resultado |
|---|---|---|
| `DEFAULT now()` en `CreadoEn` | El servidor genera un valor auditado y la fase única del interceptor deja de estar justificada | **3 rojos** en `LasClavesSeConocenAntesDeGuardarTests`; las seis entidades a la vez, porque el `DEFAULT` se pone una vez en el tipo base |
| Quitar `Serie.Version` de la lista **declarada** del ADR-0015 | La lista deja de describir el modelo | **2 rojos** |
| Quitar el testigo de `Serie` en el **modelo** | Un recurso se queda sin control de concurrencia | **2 rojos**, los mismos: comparar listas enteras caza los dos sentidos |
| Que `ModificarEmpresa` abra `ViendoLoBloqueado` | Un caso de uso ordinario ve lo bloqueado y nadie se entera | **1 rojo:** `El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado` |
| Fundir el rechazo temporal con el bloqueo de R16 | Fallar la contraseña cinco veces da de baja la cuenta para siempre | **2 rojos**, y el que lo nombra: `ElRechazoPorIntentos_SeLevantaSolo_YNoTocaElEstadoDeLaCuenta` |
| *Setter* público en vez del método de transición | El segundo bloqueo mueve la fecha del primero | **1 rojo:** `Bloquear_dos_veces_no_mueve_la_fecha_del_primer_bloqueo`, con treinta días de conservación de más |

**Y la sexta, la que había que leer entera salga como salga: `Direccion` a tipo complejo con los
barridos SIN ampliar.** Se midió antes de tocar nada:

| | Poseída | Compleja |
|---|---:|---:|
| Propiedades escalares en el modelo | 152 | **138** |
| Tipos de entidad | 20 | 18 |
| Tipos poseídos | 2 | 0 |
| **Barridos de modelo en rojo** | — | **0 de 14** |

**Doce propiedades se fueron de la clasificación de auditoría y los catorce barridos siguieron en
verde.** La causa es una línea de EF Core: las propiedades de un tipo complejo **no salen** en
`GetProperties()` ni en `EntityEntry.Properties`. Todo barrido escrito sobre esas dos APIs deja de
mirar dentro de un tipo complejo y **no avisa**: devuelve menos y da verde.

El único rojo fue de comportamiento y en integración
(`La_direccion_de_un_almacen_viaja_DENTRO_de_la_traza_de_su_dueno`), porque el interceptor de
auditoría recorría `entrada.Properties`. Es decir: **el mecanismo que iba a avisar no avisó, y lo
que salvó el cambio fue un test de efecto escrito para otra cosa.** Ese es el hallazgo del ítem.

De ahí el orden que se siguió, que es la parte reutilizable: **(1)** ampliar barridos e interceptor
con `PropiedadesConCamino()` —recorrido recursivo de `GetComplexProperties()` con el camino en
puntos—; **(2)** comprobar que la ampliación se pone **roja** con el mapeo todavía poseído (`should
be "Almacen.Direccion: 6, Empresa.DomicilioFiscal: 6" but was ""`), porque un barrido nuevo que nace
verde no ha demostrado que mire; **(3)** después, cambiar el mapeo.

## Estado actual

**Ítem 0.10 cerrado, con la CI en verde:**
[run 33436800074](https://github.com/AOjeda006/Bastion/actions/runs/33436800074) sobre `1ba9669`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **388** casos rápidos y **202** de integración contra
PostgreSQL 17.6.

Cierra la fase 0 por el lado del **modelo base**: a partir de aquí toda entidad que es un recurso
nace sabiendo cuándo se creó y cuándo se tocó, el bloqueo de R16 es uno solo y tapa de verdad, y la
dirección de R17 se mapea como lo que es. Nada de esto se añade después: ese era el criterio.

Lo que llega con él: `EntidadBase` y `InterceptorDeMarcasDeTiempo` en el bloque común; `Bloqueo`,
`MotivoDeBloqueo` e `IBloqueable` en `BuildingBlocks/Domain/Bloqueos`; `IAccesoALoBloqueado`,
`MotivoParaVerLoBloqueado` y el filtro global `"Bloqueo"` en las tres entidades bloqueables;
`ConfiguracionDeEntidadBase` y `ConfiguracionDeBloqueo`; `Direccion` mapeada como **tipo complejo**;
**dos migraciones nuevas** —Organización e Identidad— escritas a mano; y **dieciocho casos más** que
al cerrar el 0.9 —22 de integración y 18 rápidos, menos los que desaparecieron con los tres
enumerados—.

Y **tres ficheros menos**: `EstadoDeEmpresa`, `EstadoDeAlmacen` y `EstadoDeUsuario`.

### Lo que el criterio del 0.10 pedía, y dónde está probado

El criterio es una sola frase —*el tipo base de entidad y las direcciones ya nacen con lo que exigen
R14, R16 y R17; no se añade después*— y son tres reglas.

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| **R14** · un instante lleva zona horaria, una fecha de calendario no | `LasFechasDicenDeQueTipoSonTests`, 4 casos sobre los tres modelos: todo `DateTimeOffset` es `timestamptz`, todo `DateOnly` es `date`, **no hay ni un `DateTime`** —el tipo que no contesta la pregunta— y el barrido encuentra fechas de las dos clases, que es lo que impide que los otros tres salgan verdes recorriendo una lista vacía. |
| …y en el esquema de verdad, columna a columna | `EsquemaDelModuloTests.Los_instantes_llevan_zona_horaria_porque_son_momentos` (10 filas) y `EsquemaDeIdentidadTests`, contra `information_schema`. |
| …y sin `DEFAULT` ninguno | `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT`: `(is_nullable, column_default)` tiene que ser `("NO", null)`. Un `DEFAULT` puesto por una migración se queda en la tabla aunque el modelo no lo declare, y este es el único sitio desde donde se ve. |
| …y que la hora la ponga el reloj inyectado | `LasMarcasDeTiempoLasPoneElRelojInyectadoTests`, 3 casos. Uno pasa **por la API entera** —un `PUT` mueve `modificado_en` y deja `creado_en`—, que es lo único que nota si se borra la línea que engancha el interceptor. Los otros dos usan un reloj **parado en marzo de 2019**: un instante que `now()` no puede devolver, así que el valor de la columna solo lo pudo escribir el reloj que se inyectó. El tercero comprueba que el alta **no** pasa por el interceptor: sus dos marcas son la del dominio, en otro año. |
| **R16** · suprimir es bloquear, y lo bloqueado deja de verse | `LaFilaBloqueadaSigueEnLaBaseTests`, 2 casos con **SQL en crudo**, que es la única lectura que no le pregunta al acusado. Tras el `DELETE`: el `GET` da 404 **y** la fila sigue entera, con su bandera, su motivo, su fecha y su razón social. |
| …y desbloquear devuelve la misma fila, no una copia | El segundo caso: `id` y `creado_en` idénticos antes y después, y las tres columnas del bloqueo de vuelta a `["false", "", ""]` — sin cuarto estado. |
| …y que el ámbito que ve lo bloqueado esté enumerado | `El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado`: la lista de ficheros que llaman a `ViendoLoBloqueado(` se compara **entera** contra los tres desbloqueos. |
| …y las dos respuestas de la transición | `BloqueoTests`, 11 casos: bloquear dos veces devuelve el bloqueo de antes **entero**, desbloquear lo no bloqueado no es un error, desbloquear borra las tres cosas, los motivos son dos y están enumerados. |
| …y que los dos bloqueos del usuario sigan siendo dos | `EsquemaDeIdentidadTests`: `rechazado_hasta` sigue existiendo, en su columna, al lado de las tres del bloqueo. |
| **R17** · la dirección son seis campos y no tiene identidad | `Las_propiedades_de_un_tipo_complejo_entran_en_este_barrido`, con el inventario fijado: `Almacen.Direccion: 6`, `Empresa.DomicilioFiscal: 6`, más los tres `Bloqueo: 3`. |
| Que el tipo base nazca sabiendo del testigo de concurrencia | `Las_entidades_del_tipo_base_y_las_que_llevan_testigo_son_las_MISMAS`: tres listas enteras —las que heredan de `EntidadBase`, las que llevan `xmin` y la del ADR-0015— comparadas entre sí. No prohíbe la divergencia: la hace visible, y obliga a explicarla en el ADR. |
| Que toda escritura siga diciendo cómo se protege | `TodaEscrituraDiceComoSeProtegeTests`, con el inventario movido en el mismo cambio: **13 / 6 / 13** de 32. |

### Verificado en local, con la salida real

- `dotnet build Bastion.sln` → **0 advertencias / 0 errores**.
- `dotnet format Bastion.sln --verify-no-changes` → limpio. **No lo estaba**: encontró treinta y un
  incumplimientos en código de este ítem —diecisiete `CHARSET` (BOM sobrante, efecto de editar los
  ficheros con un script), diez `IMPORTS` de orden de `using` y cuatro `IDE0007`—. Se aplicó el
  formateador y se volvieron a pasar los dos carriles enteros después.
- `dotnet test --filter "Category!=Integracion"` → **388** correctos, 0 con error
  (111 comunes + 116 Organización + 58 Identidad + **103** funcionales, de 96).
- `dotnet test --filter "Category=Integracion"` → **202** correctos, 0 con error
  (**42** Organización, de 28, + **160** de API, de 152), contra PostgreSQL 17.6 con Testcontainers.
- Los **dos carriles otra vez con `GITHUB_ACTIONS=true`** y el recuento de la CI ejecutado en local:
  `::notice title=Dominio y arquitectura::… 388 casos (388 correctos, 0 con error, 0 omitidos)` y
  `::notice title=Integración con PostgreSQL::… 202 casos (202 correctos, 0 con error, 0 omitidos)`.
- `scripts/comprobar-migraciones.sh` → *«el modelo coincide con ellas»* en los tres módulos:
  **Auditoría 3**, **Organización 3** e **Identidad 3** —las dos nuevas son las del bloqueo y las
  marcas de tiempo; el cambio de `Direccion` a tipo complejo **no emite SQL**—.
- **Seis mutaciones**, cada una aplicada, compilada, ejecutada y revertida. La tabla está arriba.
  Ninguna sobrevivió… salvo la sexta, que sobrevivió **a propósito y antes de tiempo**, y por eso
  existe `PropiedadesConCamino()`.

### Lo que se ha decidido dejar pendiente, a propósito

- **La retención de `auditoria.claves_de_idempotencia`** sigue abierta, tal como quedó en el 0.9. No
  se ha tocado.
- **El ámbito declarado para leer el almacén de un movimiento histórico.** Es de la fase 3: hoy no
  hay movimientos, así que no hay camino que abrir, y abrirlo antes sería una excepción sin caso.

### Lo que se encontró por el camino y no estaba previsto

1. **R16 rompió el `If-Match` de los tres desbloqueos**, y no se vio al diseñarlo sino al ver dos
   tests de contrato en rojo **dentro del ayudante que lee la etiqueta**. El `ETag` se obtiene
   con un `GET`, y un recurso bloqueado ya no se deja leer. Se resolvió como decisión y con ADR (0017), no
   aflojando el barrido: se descartó devolver el `ETag` en el `DELETE` —solo funcionaría justo
   después de bloquear— y se descartó abrir un camino de lectura para lo bloqueado, que es el
   agujero que este mismo ítem acababa de cerrar.
2. **El barrido de escrituras se puso rojo por ello, y eso es el mecanismo funcionando.** Los tres
   desbloqueos dejaron de declarar cómo se protegen. Se movieron al cajón de exentas con su motivo
   escrito, y los tres números del inventario se movieron en el mismo cambio: **16 → 13** y
   **10 → 13**, con el total de acciones que cambian estado quieto en 32.
3. **El barrido de clasificación de auditoría se puso rojo al nacer `Bloqueo`**, exactamente como
   estaba previsto. Las tres propiedades **ya venían clasificadas**; lo que estaba sin actualizar
   era el inventario escrito a mano, que es justo lo que el barrido existe para obligar a tocar.
4. **Dos clases de test nuevas se pisaban con `LaVersionViajaDeLaLecturaALaEscritura` por el NIF.**
   Cinco NIF estaban usados dos veces en el mismo ensamblado, que comparte la base de datos: la
   segunda alta se llevaba un `409 empresa-ya-registrada`, y **qué caso caía dependía del orden de
   ejecución**. En aislado, verde; juntos, cinco rojos. Es la trampa del ADR-0006 otra vez y por el
   camino contrario: allí el test que solo pasaba aislado, aquí el que solo pasa aislado. Corregido
   con NIF libres (`00000076F`…`00000080B`), calculados con su carácter de control y no inventados.
5. **`comprobar-migraciones.sh` acusó a Identidad de tener cambios pendientes, y no los tenía.** El
   script usa `--no-build`, así que estaba leyendo el ensamblado anterior a `migrations add`. Un
   `dotnet build src/Api` antes y los tres módulos en verde.

### Lo que la CI encontró y en local no se veía

**Nada, por cuarta vez, y por el mismo motivo que en el 0.7, el 0.8 y el 0.9**: los dos carriles se
ejecutan también en local con `GITHUB_ACTIONS=true`, así que el *build* es el mismo, y los dos
recuentos publicados coinciden **exactamente** con los de esta máquina, ensamblado por ensamblado:

```
Dominio y arquitectura: 388 casos (388 correctos, 0 con error, 0 omitidos) en 6 ensamblados ·
  Organizacion.UnitTests 116, BuildingBlocks.UnitTests 111, Api.FunctionalTests 103, Identidad.UnitTests 58
Integración (Testcontainers): 202 casos (202 correctos, 0 con error, 0 omitidos) en 6 ensamblados ·
  Organizacion.IntegrationTests 42, Api.IntegrationTests 160
```

—el título del aviso lo pone la etiqueta que se le pasa al script, por eso allí el segundo carril se
llama `Integración (Testcontainers)` y aquí se lanzó como `Integración con PostgreSQL`; los números
y el reparto son los mismos—. Los suelos de recuento (300 / 100) siguen haciendo su trabajo.

Y un tercer aviso, que en este ítem es el que más pesaba: `Modelo y migraciones coinciden en todos
los módulos con persistencia`. Las dos migraciones del 0.10 están **escritas a mano**, así que la
garantía de que siguen describiendo el modelo no la da el andamiaje: la da este paso, y la da
también allí y no solo aquí.

Los cuatro *jobs* ejercieron lo que dicen ejercer —se comprobó paso a paso, no por el color—:
`Formato` 40 s, `Tests de dominio y de arquitectura` 11 s, `Migraciones` 15 s y `Tests de
integración (Testcontainers)` 72 s en `Backend`; y en `Humo`, las cuatro sondas del entorno levantado con
`docker compose` —vida, disponibilidad contra PostgreSQL, la API rechazando a quien no se identifica
y el frontal cargando— más las trazas llegando al visor.

Lo único que la CI dice y aquí no se ve sigue siendo el mismo aviso ajeno a este ítem, arrastrado
desde el 0.7 y todavía sin tocar: `actions/upload-artifact@v4` (en `Backend` y en `Frontal`) y
`docker/build-push-action@v6` con `docker/setup-buildx-action@v3` (en `Imágenes de contenedor`)
apuntan a Node.js 20, en desuso, y el ejecutor los fuerza a Node.js 24. No es un fallo; es del 0.13.

**Dónde retomar exactamente:** ítem **0.11**, *shell* de React. Criterio: login, selector de
empresa, *layout*, rutas protegidas y cliente de API **generado desde el OpenAPI**; cambio de ruta accesible
(`<title>`, `role="status"`, foco). Tres cosas que hereda de aquí:

1. **El contrato que va a consumir ya no distingue «no existe» de «está bloqueado»**, y no debe
   hacerlo: los códigos `empresa-bloqueada` y `almacen-bloqueado` están borrados del catálogo, y lo
   bloqueado contesta **404** por los caminos ordinarios (ADR-0016). Una interfaz que dijera «esta
   empresa está bloqueada» desharía en la pantalla lo que el filtro protege en la base.
2. **Los tres `POST .../desbloqueo` son las únicas escrituras sobre un recurso existente que no
   piden `If-Match`** (ADR-0017). El cliente generado no debe inventarles una cabecera de versión;
   las otras trece sí la exigen, y el barrido lo mantiene así.
3. **`docs/api/openapi.json` todavía no está en el repositorio** —solo hay un `.gitkeep`—, así que
   el paso `Publicar el OpenAPI` del *job* `Backend` lleva saliendo `skipped` desde que existe. No
   es una regresión de este ítem; es exactamente el hueco que el 0.11 viene a llenar, y el sitio
   donde se notará si se llena de verdad.

---

**Ítem 0.9 cerrado, con la CI en verde:**
[run 33347235943](https://github.com/AOjeda006/Bastion/actions/runs/33347235943) sobre `c6327b2`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **370** casos rápidos y **180** de integración contra
PostgreSQL 17.6.

Cierra la fase 0 por el lado del **protocolo de escritura**: a partir de aquí, toda modificación de
un maestro dice sobre qué versión escribe, y toda alta se puede reintentar sin duplicar. Las dos
cosas son requisitos duros —R10 y R11— y ninguna tiene síntoma en desarrollo: un sistema sin ellas
funciona perfectamente hasta el día que dos personas trabajan a la vez o a alguien se le cae la
cobertura.

Lo que llega con él: `VersionDeRecurso`, `IVersiones` y `ErroresDeConcurrencia` en la aplicación
común; `TestigoDeConcurrencia`, `Versiones`, `RespuestasConVersion` y `ManejadorDeVersionObsoleta`
en la infraestructura común; el mecanismo de idempotencia entero —clave, huella, registro, almacén,
filtro y respuesta repetida— en `BuildingBlocks/{Application,Infrastructure}/Idempotencia`; **una
tabla nueva** en el esquema `auditoria`, `claves_de_idempotencia`; el testigo `xmin` en las **seis**
entidades con lectura por identificador; y **treinta y ocho casos más** que al cerrar el 0.8
—20 de integración y 18 rápidos—.

### Lo que el criterio del 0.9 pedía, y dónde está probado

Cuatro cláusulas de dos mecanismos distintos.

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| *La misma `Idempotency-Key` devuelve el mismo recurso* | `El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro`: los **mismos bytes**, el mismo `Location`, la cabecera `Idempotent-Replayed` solo en la segunda — y, por el efecto, **un** almacén y no dos. |
| …y la misma clave con otro cuerpo **no** | `La_misma_clave_con_otro_cuerpo_es_409`, más la comprobación de que el segundo no se creó. |
| …y la misma clave de otra empresa es otra clave | `La_misma_clave_desde_otra_empresa_hace_su_propio_trabajo`, con dos empresas mandando literalmente la misma cadena. |
| …y dos peticiones **simultáneas** con la misma clave | `De_dos_peticiones_simultaneas_con_la_misma_clave_solo_una_hace_el_trabajo`, asertado sobre el efecto: exactamente una lleva la cabecera de repetición y hay una sola fila. Es el test que justifica el `ON CONFLICT`. |
| …y que la clave no se queme con un intento fallido | `Un_alta_rechazada_deja_la_clave_libre_para_el_reintento`: el cliente corrige y reintenta **con la misma clave**, como manda la cabecera. |
| …y que el recibo caiga en la transacción del trabajo | `El_recibo_y_el_almacen_llevan_el_mismo_xmin`. Tercera vez que se prueba así en este proyecto, y la única que no se puede fingir desde el código. |
| *`If-Match` obsoleto → **412*** | `Una_version_obsoleta_es_412_y_trae_la_actual` y `De_dos_que_leyeron_lo_mismo_solo_guarda_el_primero`: el segundo se lleva el `412` **y lo del primero sigue ahí**, que es la mitad que de verdad importa. |
| …y que el `412` traiga el estado actual | El mismo test: `versionActual` en el cuerpo coincide con el `ETag` que emite una relectura. Y comprueba que la cabecera `ETag` **no** viene, que es lo contrario de lo que se diseñó primero. |
| *Sin cabecera → **428*** | `Sin_la_cabecera_es_428_y_no_toca_nada`, con la segunda mitad: la fila sigue como estaba. |
| Y la cabecera ilegible → **400**, que no es lo mismo | `Una_cabecera_que_no_es_una_version_concreta_es_400`, cinco filas: el comodín `*`, una etiqueta débil, una lista, una sin comillas y una que no es un número. |
| *Estado incorrecto → **409*** | Ya estaba desde el 0.4 y sigue verde: `Suprimir_una_serie_que_ya_ha_numerado_es_409`. Lo que el 0.9 añade es que ahora llegan **después** del `If-Match`, no en vez de él. *(El otro caso que sostenía esta fila, `Una_empresa_bloqueada_no_se_puede_modificar_y_da_409`, es hoy `…_y_da_404`: con R16 puesta, modificar algo bloqueado no da 409 sino 404, y ese código de error se borró del catálogo — ADR-0016.)* |
| Que el `ETag` que emite la lectura sea el que acepta la escritura | `La_etiqueta_que_emite_la_lectura_es_la_que_acepta_la_escritura` y `La_version_cambia_cuando_el_recurso_cambia`. El segundo no sobra: con una versión constante, todos los demás saldrían verdes sin comprobar nada. |
| Tras un `412`, ni traza ni evento | `Tras_un_412_ni_traza_ni_evento`, que ata este ítem con el 0.7 y el 0.8: los tres viajan en el mismo `SaveChanges`, así que o se deshacen los tres o queda una traza de un cambio que no ocurrió. |
| Que ninguna escritura se quede sin decir cómo se protege | `TodaEscrituraDiceComoSeProtegeTests`, 6 casos, con el inventario fijado en números. |
| Que la sentencia cruda siga mereciendo su excepción | `LaClaveDeIdempotenciaEsLaTuplaEnteraTests`, 8 casos contra el modelo ya construido. |

### Verificado en local, con la salida real

- `dotnet build Bastion.sln` → **0 advertencias / 0 errores**.
- `dotnet format Bastion.sln --verify-no-changes` → limpio. **No lo estaba**: encontró cuatro
  incumplimientos en código de este ítem (orden de `using` en `Program.cs`, dos `IDE0270` y un
  `IDE0078`). Se aplicó el formateador y se volvieron a pasar los dos carriles enteros después.
- `dotnet test --filter "Category!=Integracion"` → **370** correctos, 0 con error
  (100 comunes + 116 Organización + 58 Identidad + **96** funcionales, de 78).
- `dotnet test --filter "Category=Integracion"` → **180** correctos, 0 con error
  (28 Organización + **152** de API, de 132), contra PostgreSQL 17.6 con Testcontainers.
- Los **dos carriles otra vez con `GITHUB_ACTIONS=true`**: 370 y 180, en verde.
- `scripts/comprobar-migraciones.sh` → *«el modelo coincide con ellas»* en los tres módulos:
  **Auditoría 3** migraciones —la nueva es la de `claves_de_idempotencia`—, **Organización 2** e
  **Identidad 2** —las nuevas son las del testigo, que **no emiten SQL** y se conservan para que
  modelo y migraciones cuadren—.
- **Siete mutaciones**, cada una aplicada, compilada, ejecutada y revertida. La tabla está arriba.
  Ninguna sobrevivió, y la que hay que leer es la que **no llegó a compilar** y por tanto no llegó a
  mutar: apuntada sin mirar, habría pasado por superviviente.

### Lo que se ha decidido dejar pendiente, a propósito

- **La retención de `auditoria.claves_de_idempotencia`.** La tabla crece sin límite. Está fuera de
  este ítem por decisión, no por olvido: borrar recibos es exactamente lo que reabre la ventana que
  la tabla cierra, y decidir cuánto se conserva uno necesita saber cuánto reintenta un cliente. Se
  decidirá con datos, no ahora.

### Lo que se encontró por el camino y no estaba previsto

1. **La cabecera `ETag` en un `412` no llega nunca.** El middleware de excepciones de ASP.NET Core
   registra un `OnStarting` que la borra de toda respuesta de error. Se vio poniendo a la vez el
   `ETag` y una cabecera cualquiera: la segunda llegó y el `ETag` no. Y **el borrado tiene razón**
   —el `ETag` etiqueta la representación que va en esa respuesta, y ahí va un documento de problema,
   no el recurso—, así que no se le buscó la vuelta: se quitó la cabecera, la versión actual viaja
   en el cuerpo, y hay un test que comprueba que la cabecera **no** está para que nadie la reponga
   «porque parece que falta».
2. **Los puntos de guardado automáticos de EF Core rompían la prueba de atomicidad, y algo más.**
   Con una transacción abierta, EF pone un `SAVEPOINT` delante de cada `SaveChanges`; cada uno abre
   una subtransacción con su propio identificador, así que las filas de un mismo trabajo salían con
   `xmin` distintos —medido: **759** la de negocio, **760** el recibo—. Apagarlos no era solo para
   que el test pasara: con ellos, un guardado que falla deja la transacción **viva** con la clave ya
   reclamada dentro.
3. **Una `Idempotency-Key` en blanco se atendía sin protección.** El filtro preguntaba si el texto
   estaba en blanco, y una cabecera presente y vacía cuenta como ausente en esa pregunta. El cliente
   se creía protegido y duplicaría al reintentar. Ahora se pregunta por el número de valores.

### Lo que la CI encontró y en local no se veía

**Nada, por tercera vez, y por el mismo motivo que en el 0.7 y el 0.8**: los dos carriles se
ejecutan también en local con `GITHUB_ACTIONS=true`, así que el *build* es el mismo, y los dos
recuentos publicados coinciden **exactamente** con los de esta máquina — **370** y **180**, con el
mismo reparto por ensamblado (`BuildingBlocks` 100, Organización 116, Identidad 58, funcionales 96;
Organización de integración 28 y API 152). Los suelos de recuento (300 / 100) siguen haciendo su
trabajo, y `Modelo y migraciones coinciden en todos los módulos con persistencia` sale también allí,
que es lo que da valor a las dos migraciones vacías: si algún día dejaran de estar, este aviso se
volvería rojo en la CI y no solo aquí.

Lo único que la CI dice y aquí no se ve sigue siendo el mismo aviso ajeno a este ítem, arrastrado
desde el 0.7 y todavía sin tocar: `actions/upload-artifact@v4` (en `Backend` y en `Frontal`) y
`docker/build-push-action@v6` con `docker/setup-buildx-action@v3` (en `Imágenes de contenedor`)
apuntan a Node.js 20, en desuso, y el ejecutor los fuerza a Node.js 24. No es un fallo; es del 0.13.

**Dónde retomar exactamente:** ítem **0.10**, estados `Bloqueado` y fechas de R14–R17 en el modelo
base. Criterio: el tipo base de entidad y las direcciones ya nacen con lo que exigen R14, R16 y R17
—no se añade después—. Lo que hereda de aquí: las seis entidades con lectura por identificador ya
llevan testigo de concurrencia, así que el tipo base del 0.10 tiene que nacer sabiéndolo, y la lista
de seis testigos del **ADR-0015** se compara entera — una entidad nueva con `xmin` obliga a
escribirla ahí, que es lo que mantiene «cuáles son» como una decisión.

---

**Ítem 0.8 cerrado, con la CI en verde:**
[run 33263044577](https://github.com/AOjeda006/Bastion/actions/runs/33263044577) sobre `ac7b9a4`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **352** casos rápidos y **160** de integración contra
PostgreSQL 17.6.

Estrena **el único camino por el que un módulo escribe en otro** (§4, regla 5). A partir de aquí,
«Contabilidad se entera de que se emitió una factura» es una fila de `auditoria.bandeja_de_salida`,
escrita en la misma transacción que la factura.

Lo que llega con él: `EventoDeIntegracion` y `RaizAgregado` en el dominio común;
`IDespachadorDeEventos` e `IManejadorDeEvento` en la aplicación común; la bandeja entera
—entidades, contexto, interceptor, despachador, cerrojo, publicador y métricas— en
`BuildingBlocks/Infrastructure/BandejaDeSalida`; el primer evento de verdad,
`Organizacion.Contracts.Empresas.EmpresaCreada`; **dos tablas nuevas** en el esquema `auditoria`, que
las migra; y **treinta casos nuevos** —18 de integración y 12 rápidos—.

### Lo que el criterio del 0.8 pedía, y dónde está probado

Tres cláusulas, y ninguna cubre a las otras.

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| *Un evento y su escritura de negocio caen en la misma transacción* | `ElEventoVaEnLaMismaTransaccionTests`, 4 casos. El que no se puede fingir es el del `xmin`: el de `organizacion.empresas` y el de `auditoria.bandeja_de_salida` tienen que ser el mismo número. Los otros dos —el guardado que revienta y el que va bien— los pasa también la ruta de escribir después. |
| …y que guardar dos veces el mismo agregado no lo encole dos veces | El cuarto caso del mismo fichero: el agregado olvida sus eventos cuando el guardado ya ha ido bien. |
| *El trabajo de fondo lo publica* | `ElTrabajoDeFondoVaciaLaColaTests`, 4 casos con el cableado de producción: publica; reintenta al que falló una vez —el que distingue «al menos una vez» de «como mucho una vez»—; aísla el envenenado y **sigue vivo después**; y aparca al que su manejador nunca podrá atender. |
| …y con un evento **real**, de punta a punta | `ElAltaDeUnaEmpresaSePublicaTests`, 2 casos por la API de verdad: un `POST /empresas` deja su fila y el publicador **del propio host** la publica; y el alta de la semilla, que no tiene empresa, sale igual con su motivo. |
| *Reprocesar no duplica* | `ReprocesarNoDuplicaTests`, 4 casos, asertados sobre **el efecto** —el contador del manejador— y no sobre la fila de la huella: dos entregas del mismo evento dejan un solo efecto, cada consumidor tiene su turno, y los dos controles que tienen que seguir verdes. |
| Que la cola no se cuele por los barridos del 0.6 y del 0.7 | `CadaEntidadDeclaraSuInquilinatoTests` y `ElFiltroNoSeSaltaPorAhiTests`, con las dos entidades nuevas clasificadas y la excepción del cerrojo nombrada por su ruta. La lista de aperturas pasa de once a **doce** y se compara entera. |
| Que todo evento esté declarado | `CadaEventoEstaDeclaradoTests`, 3 casos sobre el catálogo que construye el host de verdad. |
| Que se vigile con una métrica y **no** con una sonda | `LaBandejaSeMideYNoSeSondeaTests` (la lista de sondas registradas, entera) y `LaEdadDelMasViejoSeMideTests`, que comprueba que el instrumento **mide lo que dice medir**: ~600 s con un evento de hace diez minutos, y 0 con la cola vacía. |
| Qué hace contra una base sin migrar | `SinLaTablaElPublicadorSeParaTests`, 2 casos: avisa **una** vez y se para, por los dos caminos —sin esquema y con esquema pero sin tabla—. |

### Verificado en local, con la salida real

- `dotnet build Bastion.sln` → **0 advertencias / 0 errores**.
- `dotnet format Bastion.sln --verify-no-changes` → `rc=0`, sin una línea de salida.
- `dotnet test --filter "Category!=Integracion"` → **352** correctos, 0 con error
  (100 comunes + 116 Organización + 58 Identidad + **78** funcionales, de 66).
- `dotnet test --filter "Category=Integracion"` → **160** correctos, 0 con error
  (28 Organización + **132** de API, de 114), contra PostgreSQL 17.6 con Testcontainers.
- Los **dos carriles otra vez con `GITHUB_ACTIONS=true`**: 352 y 160, en verde.
- `scripts/comprobar-migraciones.sh` → *«el modelo coincide con ellas»* en los tres módulos:
  **Auditoría 2** migraciones —la nueva es la de la bandeja—, Organización 1, Identidad 1. Ninguna
  migración vacía, que es lo que hace viable la ruta 1.
- **Seis mutaciones**, cada una aplicada, compilada, ejecutada y revertida. La tabla completa está
  arriba; las dos que hay que leer son la que salió **verde** —y desmintió un comentario que estaba
  escrito en el código— y la que hubo que repetir porque no mutaba lo que decía mutar.

### Lo que la CI encontró y en local no se veía

**Nada, otra vez, y por el mismo motivo que en el 0.7**: los dos carriles se ejecutan también en
local con `GITHUB_ACTIONS=true`, así que el *build* es el mismo, y los dos recuentos publicados
coinciden **exactamente** con los de esta máquina — 352 y 160. Los suelos de recuento (300 / 100)
siguen haciendo su trabajo.

Lo único que la CI dice y aquí no se ve sigue siendo el aviso ajeno a este ítem, ya anotado en el
0.7 y todavía sin tocar: `actions/upload-artifact@v4`, `docker/build-push-action@v6` y
`docker/setup-buildx-action@v3` apuntan a Node.js 20, que está en desuso, y el ejecutor los fuerza a
Node.js 24. No es un fallo; es del 0.13.

**Dónde retomar exactamente:** ítem **0.9**, idempotencia (R10) y concurrencia optimista (R11).
Criterio: la misma `Idempotency-Key` devuelve el mismo recurso; `If-Match` obsoleto → **412**;
estado incorrecto → **409**; sin cabecera → **428**. Hereda de este ítem dos cosas que conviene no
confundir: lo de aquí es idempotencia **del consumidor** —nadie repite una petición, se repite una
entrega—, y el testigo de concurrencia de PostgreSQL es `xmin`, el mismo que aquí se usa para probar
la atomicidad; en cuanto el 0.9 lo declare como testigo, `LasClavesSeConocenAntesDeGuardarTests`
—del 0.7— se pondrá rojo a propósito y habrá que reabrir el ADR-0012, punto 2.

---

**Ítem 0.7 cerrado, con la CI en verde:**
[run 33249546052](https://github.com/AOjeda006/Bastion/actions/runs/33249546052) sobre `7556951`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **340** casos rápidos y **142** de integración contra
PostgreSQL 17.6.

Estrena el tercer esquema, `auditoria`, y con él **la primera tabla de solo añadido del sistema**.
La forma que se fija aquí no se vuelve a discutir: los libros de la R3 —asientos, movimientos, la
cadena de la R15— la copian.

Lo que llega con él: `RegistroDeAuditoria`, su configuración compartida y el interceptor en
`BuildingBlocks/Infrastructure/Auditoria` (que es donde el §12 coloca la infraestructura de
auditoría, y lo que hace posible que los tres contextos mapeen la misma tabla sin cruzar ninguna
frontera del §4); el módulo `Auditoria`, único dueño de la migración; y **veinticinco casos nuevos**
—19 de integración y 6 rápidos— repartidos entre los que prueban el efecto contra PostgreSQL de
verdad y los que barren el modelo ya construido.

### Lo que el criterio del 0.7 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| *Tabla append-only* | `LaTrazaEsDeSoloAnadidoTests`: `UPDATE`, `DELETE` y `TRUNCATE` lanzados contra PostgreSQL exigiendo el SQLSTATE `23001` del motor. Leer la migración no cuenta. |
| …y que no admita una fila incoherente | Dos casos más del mismo: un `INSERT` sin empresa y sin motivo, y otro con las dos cosas, contra el `CHECK` `ck_registros_empresa_o_motivo` (`23514`). |
| *De quién cambió qué* | `UnCambioEnUnMaestroDejaSuRastroTests`: quién (`usuario_id` de la sesión), dónde (`empresa_id`), qué (entidad + clave) y el antes y el después de cada propiedad. |
| *Un cambio en un maestro deja su rastro* | El mismo, **por la API de verdad y con sesión de verdad**: se hace un `POST` y un `PUT` de almacén y un `POST` de rol, y se mira la tabla. Que el interceptor esté registrado no se lee en ningún sitio: se nota aquí o no se nota en ninguna parte. |
| Y que la traza **no** sobreviva a un cambio revertido | `LaTrazaVaEnLaMismaTransaccionTests`, tres casos, incluido el del `xmin` — el único que distingue la ruta buena de la de «escribir después de que el guardado fuera bien». |
| Y que no guarde secretos | `LaTrazaNoGuardaSecretosTests`, dos casos, uno de ellos en forma fuerte: no nombra ninguna columna, se las pregunta al modelo. |
| Y el cabo del 0.6 | `NadieEscribeEnLaEmpresaDeOtroTests`, tres casos con su control positivo. |

### Verificado en local, con la salida real

- `dotnet build Bastion.sln` → **0 advertencias / 0 errores**.
- `dotnet format --verify-no-changes` → `rc=0` (pasó por un `dotnet format` antes: orden de
  importaciones e `IDE0007` en cuatro sitios de los tests nuevos).
- `dotnet test --filter "Category!=Integracion"` → **340** correctos, 0 con error
  (100 comunes + 116 Organización + 58 Identidad + **66** funcionales, de 58).
- `dotnet test --filter "Category=Integracion"` → **142** correctos, 0 con error
  (28 Organización + **114** de API, de 94), contra PostgreSQL 17.6 con Testcontainers.
- Los **dos carriles otra vez con `GITHUB_ACTIONS=true`**: 340 y 142, en verde.
- `scripts/comprobar-migraciones.sh` → *«el modelo coincide con ellas»* en los **tres** módulos, y
  ninguna migración vacía en Organización ni en Identidad.
- El rojo de partida más interesante no fue el del interceptor sino el de `CerrarSesion`: un `500`
  en un test del 0.5 que llevaba meses en verde. La traza le preguntó a una escritura en nombre de
  qué empresa se hacía, y resultó que aquel caso de uso nunca había contestado.
### Lo que la CI encontró y en local no se veía

**Nada, y esta vez es un dato y no una frase hecha.** Los cuatro *jobs* en `success` a la primera, y
los dos recuentos publicados coinciden **exactamente** con los de esta máquina: 340 y 142. Es la
primera vez que pasa, y la explicación no es la suerte — es que el ítem 0.6 dejó dos costumbres: los
dos carriles se ejecutan también con `GITHUB_ACTIONS=true`, que es lo que hace que el *build* sea el
mismo, y el recuento se publica como anotación porque los registros del *job* devuelven `403` sin
autenticar.

Lo único que la CI dice y aquí no se ve es un aviso ajeno a este ítem:
`actions/upload-artifact@v4` apunta a Node.js 20, que está en desuso, y el ejecutor lo fuerza a
Node.js 24. No es un fallo y no rompe nada; queda anotado para cuando toque el 0.13.

**Ítem 0.6 cerrado, con la CI en verde:**
[run 33022811597](https://github.com/AOjeda006/Bastion/actions/runs/33022811597) sobre `9391f0b`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **332** casos rápidos y **122** de integración contra PostgreSQL 17.6.

El filtro de empresa deja de ser algo que cada repositorio tiene que recordar: es **global**, va en
el modelo de EF Core y **falla cerrado**. Seis filtros, dos contextos, y la empresa sale del *claim*
en cada consulta.

Lo que llega con él: `IInquilinoActual` y `MotivoSinInquilino` en el bloque común, la base de los
`DbContext` de módulo con la propiedad por la que filtran, y **diecisiete casos nuevos** repartidos
entre los que prueban el efecto (integración, contra PostgreSQL de verdad) y los que cierran los
caminos que rodean al filtro (funcionales, sin base de datos).

### Cada camino que podría saltarse el filtro, y qué lo impide

Un filtro de consulta protege lo que pasa por el traductor de consultas y nada más. La lista es
corta y es conocida. **Un camino sin test que lo ejerza ni prohibición comprobada es un camino
abierto**, así que aquí no hay ninguna fila sin la una o la otra.

| Camino | Qué lo impide |
|---|---|
| Listado sin filtro explícito | **Test** `Un_listado_sin_filtro_explicito_no_devuelve_datos_de_otra_empresa` |
| El total de la paginación (es **otra** consulta, no la de los elementos) | **Test** `El_total_de_la_pagina_tampoco_cuenta_las_filas_de_otra_empresa` |
| Lectura por identificador de una fila ajena | **Test** `Una_fila_de_otra_empresa_no_se_distingue_de_una_que_no_existe` (mismo `404` y mismo `type` que un `Guid` inventado) |
| **Escritura** por identificador contra una fila ajena (`PUT`) | **Test** `Una_escritura_por_identificador_contra_una_fila_de_otra_empresa_es_404`, que además comprueba que la fila **no cambió** |
| **Borrado** por identificador contra una fila ajena (`DELETE`) | **Test** `Un_borrado_por_identificador_contra_una_fila_de_otra_empresa_es_404`, que además comprueba que sigue `Activo` |
| El identificador de empresa colado en la petición | **Test** `El_identificador_de_empresa_que_venga_en_la_peticion_se_ignora` (cuerpo + `?empresaId=` + cabecera `X-Empresa-Id`, los tres a la vez) y **prohibición** `NingunaPeticionNombraLaEmpresaTests`: ningún DTO tiene el campo |
| La raíz: leer el padrón de empresas desde otra empresa | **Test** `El_padron_de_empresas_no_se_lee_desde_otra_empresa` |
| Un usuario con el que no se comparte empresa | **Test** `Un_usuario_que_no_comparte_empresa_no_se_ve` |
| Navegaciones y claves que apuntan fuera (`Include`, `ejercicioId` ajeno) | **Test** `Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400_del_campo_ejercicioId` (del 0.4; desde hoy el ejercicio ajeno además **ya no se ve**, así que esa comprobación pasa a ser la segunda línea, no la única) |
| Una entidad **nueva** a la que se le olvide el filtro | **Test** `CadaEntidadDeclaraSuInquilinatoTests`, en los dos sentidos. Es lo único de esta tabla que **escala a los dieciséis módulos** |
| El filtro congelado en el primer contexto | **Test** `ElFiltroSeLeeEnCadaConsultaTests`, 3 casos, incluido el del **mismo contexto reutilizado** |
| No tener empresa y que pase desapercibido | **Test** `SinEmpresaNoSeConsultaTests`, 4 casos sobre el `IInquilinoActual` que resuelve el contenedor de verdad |
| `IgnoreQueryFilters()` | **Prohibición comprobada.** Se puede prohibir del todo porque el ámbito auditado cubre lo que hacía falta |
| SQL crudo: `FromSql*`, `ExecuteSql*`, `SqlQuery` | **Prohibición comprobada**: no pasan por el traductor, así que el filtro no se les aplica |
| `ExecuteUpdate` / `ExecuteDelete` | **Prohibición comprobada**: respetan el filtro, pero saltan el rastreador y la unidad de trabajo, así que ni la auditoría (0.7) ni la concurrencia (0.9) los verían pasar |
| `Find` / `FindAsync` y el rastreador | **Prohibición comprobada**: pueden contestar desde el rastreador **sin consultar**, y entonces no hay consulta que filtrar |
| Consultar una dependiente global por su cuenta (`Set<RolDeMembresia>`, `Set<PermisoDeRol>`) | **Prohibición comprobada, con una excepción anotada**: `RepositorioDeRoles.PermisosDeAsync`. Los identificadores de rol no vienen de la petición, los pone `ConstructorDeSesion` desde la membresía, que sí filtra |
| Abrir un ámbito sin inquilino donde no toca | **Prohibición comprobada**: los siete ficheros y el número de aperturas de cada uno se comparan **enteros**, en los dos sentidos |
| Definir un filtro global fuera de los contextos de módulo | **Prohibición comprobada**: repartirlos no rompería nada, solo haría imposible contestar «¿qué filtra y qué no?» leyendo un sitio |

El barrido de prohibiciones lee `src/**/*.cs` **con los comentarios quitados** y solo los ficheros
que ven EF Core: lo que se prohíbe es **llamar**, no nombrar, y un `.Find(` en el dominio es el de
`List<T>` —lo fue: `Usuario.EnEmpresa` dio el primer falso positivo—.

### La prueba fuerte: ocho mutaciones, y lo que cada una puso en rojo

Un test que no distingue el código bueno del malo no prueba nada. Cada mutación se aplicó, se
ejecutó y se revirtió; `grep -rn "MUTACION" src tests` no devuelve nada.

| # | Mutación | Rojo |
|---|---|---|
| 1 | Los seis `HasQueryFilter` comentados | 4 rápidos + **los 8 casos de integración de R8** |
| 2 | El inquilino copiado una vez, no leído en cada consulta | **verde** con los dos casos que había → se escribió el tercero (mismo contexto reutilizado) y entonces rojo |
| 3 | `EmpresaDelFiltro` deja de lanzar y de mirar el ámbito | **27** casos de integración |
| 3b | Solo se quita el `throw` | 3 casos de `SinEmpresaNoSeConsultaTests` |
| 4 | Sin filtro en `Empresa` | `CadaEntidadDeclaraSuInquilinato` + `El_padron_de_empresas_no_se_lee_desde_otra_empresa` |
| 5 | Sin filtro en `Usuario` | `CadaEntidadDeclaraSuInquilinato` + `Un_usuario_que_no_comparte_empresa_no_se_ve` |
| 6 | Sin filtro en `Ejercicio` | `CadaEntidadDeclaraSuInquilinato` ×2 — **integración se queda verde** |
| 7 | `.IgnoreQueryFilters()` inyectado en `RepositorioDeAlmacenes` | `ElFiltroNoSeSaltaPorAhi.Ninguna_llamada…` |
| 8 | Quitado el ámbito de `IniciarSesion` | `ElFiltroNoSeSaltaPorAhi.El_ambito_sin_inquilino_solo_se_abre_donde_esta_declarado` |

**Las dos mutaciones que hay que contar sin adornarlas:**

- **La 2 salió verde**, y no era un fallo del código sino de los tests: con `AddDbContext` cada
  consulta abre su propio ámbito de servicios, así que un identificador copiado en el constructor se
  comporta **igual** que leerlo cada vez y ningún test lo distinguía. El caso nuevo reutiliza la
  misma instancia de contexto con dos empresas —lo que haría `AddDbContextPool`— y ese sí lo
  distingue: rojo bajo la mutación, verde al restaurar.
- **La 6 dejó la integración verde**: `Ejercicio` y `Serie` **no tienen test de efecto propio**. Lo
  que los cubre es la red de completitud, que es justamente la parte que escala. Queda dicho para
  que no se lea como cobertura que no está.

### Verificado en local, con la salida real

Con Docker ya instalado, **por primera vez los dos carriles se ejecutan aquí**. El ítem se hizo
test-first caso a caso, enseñando el rojo antes de cada verde.

- `dotnet build -c Release` → **0 advertencias / 0 errores**.
- `dotnet format --verify-no-changes` → `rc=0`.
- `dotnet test --filter "Category!=Integracion"` → **332** correctos, 0 con error
  (100 comunes + 116 Organización + 58 Identidad + **58** funcionales, de 41).
- `dotnet test --filter "Category=Integracion"` → **122** correctos, 0 con error
  (28 Organización + **94** de API, de 86), contra PostgreSQL 17.6 con Testcontainers.
- Y los **dos carriles otra vez con `GITHUB_ACTIONS=true`**, que es lo que hace que el build sea
  el mismo que el de la CI: 332 y 122, en verde. No es una repetición decorativa — ver abajo.
- El rojo de partida más grande: al cablear el filtro que falla cerrado, **70 casos de integración
  en rojo a la vez**. Setenta no eran setenta defectos: eran los caminos legítimos sin principal
  saliendo a la luz de golpe, que es exactamente para lo que sirve fallar cerrado. Diez ámbitos
  después, cuatro. Y esos cuatro eran la consecuencia de «`Empresa`: solo la activa».

### Lo que la CI encontró y en local no se veía

**Un rojo, y de los que enseñan algo**
([run 33022273428](https://github.com/AOjeda006/Bastion/actions/runs/33022273428), 4 en rojo de 332).
Los cuatro casos de `ElFiltroNoSeSaltaPorAhiTests` fallaron con *«no se ha encontrado Bastion.sln
subiendo desde el fichero del test»*. Localizaban la raíz del repositorio desde `[CallerFilePath]`,
y eso **no sobrevive a la CI**: `Directory.Build.props` activa `ContinuousIntegrationBuild` cuando
corre en GitHub Actions, con él llega `DeterministicSourcePaths`, y las rutas de los fuentes se
reescriben a `/_/tests/…` para que dos máquinas produzcan el mismo binario. La ruta deja de apuntar
a un sitio que existe.

Se busca ahora desde el directorio del ensamblado, con el fichero del test de segundo intento. Y
**lo que el test hizo bien se conserva**: si no encuentra la raíz por ninguno de los dos caminos,
revienta. Un barrido de prohibiciones que no encuentra qué barrer y da verde es peor que no tenerlo
—sería un `IgnoreQueryFilters` colado con la CI aplaudiendo—.

Dos cosas que se quedan de aquí:

- **El rojo se reprodujo en local antes de tocar nada**, con `GITHUB_ACTIONS=true dotnet build`:
  misma aserción, mismos cuatro casos. Desde ahora ese es el modo de reproducir un rojo de la CI en
  esta máquina, y los dos carriles se ejecutan también así.
- **Las anotaciones del recuento pagaron su precio.** Los registros del *job* devuelven `403` sin
  autenticar —se comprobó, no se supuso—, así que el nombre de los cuatro casos y su aserción
  salieron de `GET /check-runs/{id}/annotations`, que sí responde `200`. Sin el `::notice::` que
  publica `recuento-de-tests.sh`, el rojo se habría visto desde fuera como *«Process completed with
  exit code 1»* y nada más.

### Lo que cambia para lo que ya estaba

- **Cuatro tests de contrato del 0.5 pasaron a entrar en la empresa antes de operar sobre ella.** No
  se relajó ni una aserción: cambió el contrato, no la prueba. Quien crea una empresa recibe un
  `201` con un `Location` que **todavía no puede seguir** hasta cambiarse a ella.
- **Diez aperturas de ámbito sin inquilino en siete ficheros de producción**, cada una con su motivo
  escrito al lado y todas anotadas en el registro al abrirse.
- **`tests/Arquitectura.Tests/` sigue vacía**: es del 0.12. Las prohibiciones de este ítem se
  comprueban hoy leyendo los fuentes desde `Api.FunctionalTests`; cuando exista ese proyecto será el
  momento de decidir si alguna se expresa mejor allí. **No se ha adelantado el 0.12.**
- El comentario de `.github/workflows/ci.yml` que decía «el ensamblado más pequeño de este paso
  aporta 41 casos» quedó desfasado (son 58) y se corrige con este ítem. Los suelos de recuento
  (300 / 100) siguen haciendo su trabajo: 332 y 122.

**Dónde retomar exactamente:** ítem **0.7**, módulo Auditoría. Criterio: tabla *append-only* de
quién cambió qué; un cambio en un maestro deja su rastro. Estrena el tercer esquema
(`auditoria`), y hereda de este ítem dos cosas: que `ExecuteUpdate`/`ExecuteDelete` están prohibidos
—si pasaran, la auditoría no los vería— y que el ámbito sin inquilino se anota en el registro, que
es el precedente de «una operación deliberada deja rastro».

---

**Del ítem 0.5:**

**Ítem 0.5 cerrado, con la CI en verde de verdad:**
[run 32999845303](https://github.com/AOjeda006/Bastion/actions/runs/32999845303) sobre `369c24d`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor— y
los dos recuentos publicados: **315** casos rápidos y **114** de integración contra PostgreSQL 17.6.

Y llegar ahí costó **tres rojos de la CI**, que es la parte que importa: el árbol llevaba días verde
en esta máquina y escondía tres defectos que solo se veían con una base de datos delante. Están
contados abajo.

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

Recuento: `dotnet test` pasa de **226** casos a **315 rápidos** + **114 de integración**. Los de
integración no se pueden ejecutar aquí —sigue sin haber Docker en esta máquina—, así que quien los
ejecuta es la CI y por eso su conclusión es la única que cierra el ítem.

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
- `dotnet test --filter "Category!=Integracion"` en Release → **315 correctos, 0 con error**
  (100 bloques comunes + 116 Organización + 58 Identidad + 41 funcionales).

### Lo que la CI encontró y en local no se veía

Tres rojos, tres defectos reales, ninguno visible sin PostgreSQL delante. Se arreglaron hacia
delante sobre `main`, que es como han ido todos los ítems de esta fase.

1. **La empresa recién creada era inalcanzable para siempre**
   ([run 32993448112](https://github.com/AOjeda006/Bastion/actions/runs/32993448112), 11 rojos).
   Con la regla «solo se administra la empresa del *claim*», la **segunda** empresa del sistema no
   la puede usar nadie: para entrar hay que pertenecer, y para pertenecer hay que estar dentro. No
   es un fallo del test, es un fallo del producto, y no lo enseña ningún test de dominio. La
   excepción es la mínima que lo desatasca —se admite nombrar otra empresa **mientras no haya nadie
   más dentro**— y está escrita en `ErroresDePertenencia.PuedeAdministrarAsync`. Se descartó la
   alternativa obvia (que crear la empresa dé de alta a quien la crea) porque sería una escritura
   de Organización sobre Identidad, y eso solo va por eventos (§4, regla 5), que son el **0.8**.
   Del mismo *run* salieron un código de rol de 42 caracteres contra un tope de 40, un `\0` en un
   correo que llegaba vivo hasta el 500, y un barrido que daba por cerrada una puerta que en
   realidad había abierto (el `403` de negocio confundido con el de la política).
2. **El 400 del enlace de modelo publicaba el tipo de C#**
   ([run 32994795968](https://github.com/AOjeda006/Bastion/actions/runs/32994795968)). Mandar `[]`
   a una acción que espera un objeto contestaba *«The JSON value could not be converted to
   Bastion.Organizacion.Contracts.Empresas.CrearEmpresaDto»*. No es una traza, así que no lo paraba
   ningún manejador de excepciones: es el camino **previsto** para un cuerpo mal formado, y MVC lo
   componía por su cuenta, también sin identificador de traza (§9). Lo arregla `EntradaNoValida`, y
   el rojo se enseñó **sin Docker** por la única puerta anónima que recibe un cuerpo: 6 de 7 casos
   en rojo, 7 de 7 en verde.
3. **Dar de alta en una empresa era un `UPDATE` que no tocaba ninguna fila**
   ([run 32997004433](https://github.com/AOjeda006/Bastion/actions/runs/32997004433), 10 rojos, y
   los diez el mismo `500`). EF Core decide si una entidad hija es nueva **mirando si tiene clave**;
   `Membresia` la tiene desde el constructor —un `Guid` v7, a propósito—, así que la daba por
   existente. Lo tapaba que el otro camino que crea pertenencias es la semilla, donde el hijo hereda
   el alta del padre. Está en el **ADR-0010**, con las tres alternativas descartadas, y fijado en
   `LasPertenenciasNuevasSeInsertanTests`: cuatro casos, **sin base de datos**, porque el estado en
   que EF Core deja la entidad se decide antes de abrir ninguna conexión.

De ahí salieron además dos cosas que se quedan: `RegistroDeFallos`, que engancha el registro de la
API al mensaje de la aserción para que un `500` de la CI diga **qué** ha reventado —los registros de
un *job* devuelven 403 sin autenticar, así que no hay otra forma de leerlos—, y un cuarto barrido,
`Ninguna_accion_contesta_con_un_fallo_del_servidor_al_sondeo`: el barrido de «se abre con su
permiso» da por buena cualquier respuesta que no sea 401 ni 403, **y eso incluye un 500**.

**Honestidad sobre el método:** los tres agregados de Identidad (`Usuario`, `Rol`, `TokenDeRefresco`)
se escribieron **a la vez** que sus tests, no estrictamente test-first. El rojo de partida fue real
—no compilaba, las entidades no existían— pero no fue caso a caso. Donde sí se hizo por el efecto,
caso a caso y enseñando el rojo, es en todo lo que vino después: la denegación por omisión, la forma
de la autorización, el resumen de contraseñas y los tres defectos de arriba.

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
| Y ninguna **estalla** al abrirse | las mismas 41 respuestas, miradas otra vez | nunca `5xx` |
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
- [x] **0.5 · Módulo Identidad** — criterio de aceptación: registro y login; roles y permisos por
  acción; pertenencia a empresas; el identificador de empresa viaja en el *claim*.
  La API deja de ser pública: se deniega por omisión y cada una de las **46 acciones** se abre con
  su `[ExigePermiso]` —41 tras su permiso, 3 anónimas, 2 autenticadas sin permiso— sobre un catálogo
  de **34 permisos**. Decisiones en
  `docs/adr/adr-0008-contrasenas-bloqueo-y-la-respuesta-unica-del-acceso.md`,
  `docs/adr/adr-0009-la-denegacion-por-omision-tambien-cubre-lo-que-no-es-una-ruta.md` y
  `docs/adr/adr-0010-una-entidad-hija-con-clave-propia-no-se-da-de-alta-sola.md`.
  Segundo módulo con persistencia, o sea la primera prueba **de verdad** del historial de
  migraciones por esquema del 0.4: se comprueba mirando las tablas y salen exactamente dos.
  Los cuatro *jobs* de la CI en verde — [run 32999845303](https://github.com/AOjeda006/Bastion/actions/runs/32999845303),
  con **315** casos rápidos y **114** de integración.
- [x] **0.6 · Filtro global multiempresa (R8)** — criterio de aceptación: un test demuestra que una
  consulta sin filtro explícito **no** devuelve datos de otra empresa, y que el identificador del
  cuerpo de la petición se ignora.
  Las dos mitades del criterio, por el efecto y sobre HTTP con dos empresas sembradas:
  `Un_listado_sin_filtro_explicito_no_devuelve_datos_de_otra_empresa` y
  `El_identificador_de_empresa_que_venga_en_la_peticion_se_ignora`, que lo manda por el cuerpo, por
  la cadena de consulta y por una cabecera a la vez. Y su forma fuerte: ningún DTO tiene por dónde
  recibir una empresa. Decisiones en
  `docs/adr/adr-0011-el-filtro-global-de-empresa-y-los-caminos-que-lo-rodean.md`, con la tabla de
  inquilinato, los cuatro motivos del ámbito auditado y el `404` que no distingue una fila ajena de
  una que no existe. Probado con **ocho mutaciones**, dos de ellas contadas sin adornarlas.
  Los cuatro *jobs* de la CI en verde — [run 33022811597](https://github.com/AOjeda006/Bastion/actions/runs/33022811597),
  con **332** casos rápidos y **122** de integración.
- [x] **0.7 · Módulo Auditoría** — criterio de aceptación: tabla *append-only* de quién cambió qué;
  un cambio en un maestro deja su rastro.
  Las dos mitades del criterio, por el efecto: lo *append-only* lo rechaza **el motor** —una función
  `plpgsql` y dos disparadores, uno de fila para `UPDATE` y `DELETE` y otro de sentencia para
  `TRUNCATE`—, y se prueba lanzando las tres órdenes contra PostgreSQL, no leyendo la migración; y
  el rastro se comprueba por la API de verdad, con sesión de verdad, mirando la tabla. La traza va
  **dentro del mismo `SaveChanges`** que el cambio, y eso lo prueba el `xmin`: el de la fila y el de
  su traza son el mismo número. Estrena el tercer esquema, `auditoria`, y con él la primera tabla de
  solo añadido del sistema. Decisiones en
  `docs/adr/adr-0012-la-traza-va-en-la-misma-transaccion-que-el-cambio.md`, con las dos rutas no
  atómicas escritas descartadas, la tabla de las diez entidades —más la propia traza, que no se audita a sí
  misma— y la línea sobre datos personales y
  conservación, que es una decisión que aquí no se toma. De paso cierra el cabo del 0.6:
  `HasQueryFilter` no interviene en un `INSERT`. Probado con **seis mutaciones**, dos de ellas
  contadas enteras.
  Los cuatro *jobs* de la CI en verde — [run 33249546052](https://github.com/AOjeda006/Bastion/actions/runs/33249546052),
  con **340** casos rápidos y **142** de integración.
- [x] **0.8 · Outbox transaccional (R12)** — criterio de aceptación: un evento y su escritura de
  negocio caen en la misma transacción; el trabajo de fondo lo publica; reprocesar no duplica.
  Las tres cláusulas, probadas por separado y ninguna cubriendo a las otras: la atomicidad por el
  `xmin` —el de la empresa y el de su evento son el mismo número—, la publicación con el cableado de
  producción y también con un evento **real** de punta a punta (`EmpresaCreada` por la API), y la no
  duplicación asertada sobre **el efecto** y no sobre la fila de la huella. Se marca DESPUÉS de
  despachar, así que la entrega es «al menos una vez» y quien la convierte en una sola es la huella
  del par (evento, consumidor); la ventana que queda abierta está escrita. Dos tablas nuevas en el
  esquema `auditoria`, que las migra, por la ruta 1 del 0.7. El publicador es lo primero que corre
  sin petición detrás: abre su ámbito sin inquilino con el motivo `PublicacionDeEventos` y la lista
  de aperturas pasa de once a doce. Se vigila con una métrica —la edad del pendiente más viejo— y no
  con una sonda. Decisiones en
  `docs/adr/adr-0013-el-evento-va-en-la-misma-transaccion-y-el-efecto-ocurre-una-vez.md`, con el
  aplazamiento razonado de Hangfire y la ventana de la huella. Probado con **seis mutaciones**, una
  de ellas verde y contada entera: desmintió un comentario que estaba escrito en el código.
  Los cuatro *jobs* de la CI en verde — [run 33263044577](https://github.com/AOjeda006/Bastion/actions/runs/33263044577),
  con **352** casos rápidos y **160** de integración.
- [x] **0.9 · Idempotencia (R10) y concurrencia optimista (R11)** — criterio de aceptación: la misma
  `Idempotency-Key` devuelve el mismo recurso; `If-Match` obsoleto → **412**; estado incorrecto →
  **409**; sin cabecera → **428**.
  **Son dos mecanismos y se han diseñado por separado**, aunque el criterio los junte: la clave del
  cliente protege de que UNA persona repita su petición; el `If-Match`, de que DOS pisen el mismo
  recurso. Ninguna acción pide las dos, y un barrido lo mantiene así. El testigo es `xmin` —no un
  contador nuestro—, con la trampa del `AsNoTracking` cerrada con un fallo ruidoso. La clave de
  idempotencia es **la tupla entera** (empresa, usuario, método, ruta, clave) y se reclama con un
  `INSERT … ON CONFLICT DO NOTHING`, la única sentencia cruda del mecanismo, con su excepción al
  barrido del 0.6 ganada por el mismo argumento que el cerrojo del 0.8: **no lee ninguna tabla**.
  El recibo cae en la transacción del trabajo, y para que eso sea *comprobable* hubo que apagar los
  puntos de guardado automáticos de EF Core —abren subtransacciones con `xmin` propio—. Una tabla
  nueva en el esquema `auditoria`; **la retención de esa tabla queda fuera a propósito** y anotada.
  Decisiones en `docs/adr/adr-0014-la-clave-del-cliente-y-la-version-del-recurso-son-dos-mecanismos.md`
  y, para la premisa reenunciada del 0.7, en
  `docs/adr/adr-0015-lo-unico-que-genera-el-servidor-son-los-testigos-de-concurrencia.md`.
  Probado con **siete mutaciones**, ninguna superviviente y una que hubo que rehacer porque no
  llegó a compilar. Los cuatro *jobs* de la CI en verde — [run 33347235943](https://github.com/AOjeda006/Bastion/actions/runs/33347235943),
  con **370** casos rápidos y **180** de integración publicados como `::notice::`.
- [x] **0.10 · Estados `Bloqueado` y fechas de R14–R17 en el modelo base** — criterio de aceptación:
  el tipo base de entidad y las direcciones ya nacen con lo que exigen R14, R16 y R17 — no se añade
  después.
  **No añade una función: unifica lo que estaba escrito tres veces.** `EstadoDeEmpresa`,
  `EstadoDeAlmacen` y `EstadoDeUsuario` eran tres enumerados de dos valores, con tres `BloqueadoEn`
  sueltos al lado y **ningún motivo**; ahora son un solo `Bloqueo` —bandera, fecha y motivo de lista
  cerrada, juntos, con la transición como comportamiento y sin *setters* públicos—. Y ninguno de los
  tres tapaba nada: el filtro global `"Bloqueo"`, hermano del `"Inquilinato"` del ADR-0011, **tapa a
  las tres sin excepciones**, así que `GET` y `PUT` sobre lo bloqueado contestan **404** y no un 409
  que revelaría a la vez que la fila existe y en qué estado está — que es el tratamiento que el
  art. 32 de la LOPDGDD manda impedir. Ver lo bloqueado es una **apertura declarada** con motivo, y
  la lista de sitios donde se abre se compara entera: hoy son los tres desbloqueos y nada más.
  `EntidadBase` aporta **las dos marcas de tiempo y nada más** —ni identidad ni bloqueo, porque no
  era eso lo que estaba escrito tres veces—: `CreadoEn` la pone el dominio en la fábrica,
  `ModificadoEn` un interceptor al guardar, y **ninguna es un `DEFAULT now()`**, comprobado con un
  reloj parado en 2019 —un instante que `now()` no puede devolver—. `Direccion` deja de fingir que
  tiene identidad y pasa de tipo poseído a **tipo complejo**, sin cambio de esquema. Las **dos
  migraciones nuevas están escritas a mano**: derivan el bloqueo de la columna vieja antes de
  tirarla, y el `Down` lo rehace — deshacer una migración no puede ser una forma de desbloquear en
  silencio. Consecuencia con ADR propio: los tres `POST .../desbloqueo` **pierden el `If-Match`**,
  porque la etiqueta se obtiene leyendo y lo bloqueado ya no se deja leer; el testigo `xmin` se
  sigue comparando dentro de la petición.
  Decisiones en `docs/adr/adr-0016-el-bloqueo-es-uno-y-tapa-a-las-tres.md` y
  `docs/adr/adr-0017-el-desbloqueo-no-puede-pedir-una-llave-que-el-bloqueo-esconde.md`.
  Probado con **seis mutaciones**, ninguna superviviente salvo la sexta, que sobrevivió **a
  propósito y antes de tiempo**: `Direccion` a tipo complejo con los barridos sin ampliar dejó
  **catorce barridos en verde y doce propiedades fuera** de la clasificación de auditoría, porque
  las de un tipo complejo no salen en `GetProperties()`. De ahí `PropiedadesConCamino()`, y de ahí
  el orden barridos → rojo → mapeo.
  Los cuatro *jobs* de la CI en verde — [run 33436800074](https://github.com/AOjeda006/Bastion/actions/runs/33436800074),
  con **388** casos rápidos y **202** de integración publicados como `::notice::`.
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

- **ABIERTO (2026-08-31) · `auditoria.claves_de_idempotencia` crece sin límite, y es a propósito.**
  El 0.9 no trae política de retención, y no por olvido. Un recibo de idempotencia es lo que impide
  que un reintento duplique un alta: **borrarlo reabre exactamente la ventana que la tabla cierra**,
  así que una limpieza mal calibrada no deja la tabla más pequeña, deja el sistema sin la garantía.
  Lo que hace falta para decidirlo no está en el código sino en el uso: **cuánto tarda un cliente
  real en reintentar**. Hasta que haya ese dato, borrar sería adivinar. Dos cosas que ya se saben y
  ahorran trabajo el día que toque: la fila es pequeña y anónima —identidad, huella, código y bytes
  de la respuesta, nada del contenido de negocio (ADR-0014 §5)—, y la clave primaria empieza por
  `empresa_id`, así que un borrado por antigüedad dentro de una empresa recorre un rango contiguo
  del índice. El momento natural de decidirlo es el **0.13**, junto con quién aplica las migraciones
  en un despliegue; no antes.
- **ABIERTO (2026-08-26) · el *compose* no aplica las migraciones, así que la semilla no llega a
  aplicarse ahí.** Nadie ejecuta `dotnet ef database update` ni al arrancar la API ni en el
  `docker-compose.yml`: la base del entorno local no tiene tablas. Consecuencia práctica de hoy:
  las siete variables `BASTION_SEMILLA_*` están **declaradas en el compose y vacías por omisión**,
  así que la semilla se salta con un aviso en el registro y el entorno levanta igual que antes. Si
  se rellenan sin haber migrado, el arranque **revienta a propósito** (la semilla no se calla). Lo
  que falta es decidir **quién** aplica las migraciones en un despliegue —un paso del compose, un
  `initContainer`, o el propio arranque de la API— y eso es materia del **0.13**, no del 0.5. Los
  tests de integración sí migran: lo hace su fixture antes de levantar el host.
- **CORREGIDO (2026-08-27) · la CI SÍ se dispara en las ramas; lo que falló fue la observación.**
  Esta nota decía que empujar `feature/0.5-identidad` (commit `7d5f3c3`) no había creado ningún
  *run* y dejaba a decisión del usuario si investigar los permisos de Actions. **La premisa era
  falsa.** El *run* existe: `32993726736`, `event: push`, `head_branch: feature/0.5-identidad`,
  `head_sha: 7d5f3c3`, conclusión **failure**. El `on: push: branches: ['**']` funciona —`**` casa
  con las barras— y la consulta que lo resuelve, `GET /actions/runs?branch=feature/0.5-identidad`,
  responde **sin autenticar** y devuelve `total_count: 1`.
  Lo que hubo fue una conclusión sacada de una ausencia que produjo la propia herramienta: las
  consultas a la API de Actions sin credenciales devuelven **403** en varios caminos —los registros
  de un *job*, sin ir más lejos, como ya dice la nota de más abajo— y un 403 se leyó como «no hay
  *run*». **Ausencia de evidencia con una herramienta que devuelve 403 no es evidencia de
  ausencia**; cuando una consulta no devuelva nada, lo primero es comprobar si tenía permiso para
  devolver algo. Es el mismo error contra el que llevan cuatro ítems peleando los tests, solo que
  apuntando al andamiaje en vez de al producto.
  **Corolario, vigente desde el 0.6:** «rama por ítem, verde ahí, y solo entonces `main` avanza por
  avance rápido» **sí se puede cumplir**, y se cumple. El 0.5 se arregló hacia delante sobre `main`
  por esta nota equivocada; no vuelve a pasar. `feature/0.5-identidad` se borró del remoto el
  2026-08-27, ya superada por `main`.
- **CERRADA (2026-08-26) · GitHub Actions estuvo en caída mayor.** Incidencia abierta a las 15:11
  UTC: el *push* de `7d5f3c3` a `main` llegó (el `HEAD` remoto era ese) y **no se creó el *run***.
  No fue un fallo del repositorio ni del *workflow* —`githubstatus.com` daba `Actions ->
  major_outage` con `Git Operations`, `API Requests` y `Webhooks` operativos— y el servicio
  volvió esa misma tarde. Queda apuntada porque explica el hueco entre `7d5f3c3` y el primer *run*
  del 0.5, y porque la lección es que **no haber *run* no es lo mismo que *run* en verde**: lo que
  tocaba era esperar, no dar el ítem por cerrado.
- **ARREGLADO (2026-08-26) · los mínimos del recuento de tests estaban en 1 y 1.**
  `scripts/ci/recuento-de-tests.sh` falla si un paso ejecuta menos casos de los exigidos, y exigía
  **uno** en cada paso mientras se ejecutaban cientos: con ese suelo, perder un ensamblado entero
  del barrido seguía saliendo verde. Con las dos cuentas ya publicadas por la CI —**315** y
  **114**— el suelo pasa a **300** y **100**. El criterio no es el número de hoy sino aquel por
  debajo del cual se ha perdido algo gordo: el ensamblado más pequeño de cada paso aporta 41 y 28
  casos, así que perder cualquiera de ellos rompe el suelo y borrar un test de más no.
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
- **RESUELTO (2026-08-27) · Docker ya está instalado, y el bucle rápido existe.** Esta nota llevaba
  desde el 0.2 diciendo que no había demonio en la máquina de desarrollo y que cada vuelta de un
  repositorio costaba un *run* de CI. Ya no. `docker info` responde `29.7.2 · linux/x86_64`, y la
  comprobación que este plan exigía —**no** `docker --version`, sino **una ejecución real de
  Testcontainers**— sale en verde: `dotnet test --filter "Category=Integracion"` ejecuta en local
  los **114** casos contra PostgreSQL 17.6 en **35 s**, con el mismo reparto que publica la CI
  (28 de `Organizacion.IntegrationTests` + 86 de `Api.IntegrationTests`).
  Consecuencia práctica, y es grande: **el rojo de un test de integración ya se puede enseñar
  aquí**. Los tres defectos del 0.5 costaron tres *runs* de CI porque no había otra forma de
  verlos; desde el 0.6, un ítem se desarrolla test-first contra PostgreSQL de verdad en segundos y
  la CI vuelve a ser lo que debe ser: la segunda opinión, en un entorno limpio, no el único sitio
  donde se ejecuta la mitad de la pirámide del §13.
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
