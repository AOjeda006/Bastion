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

`TodaEscrituraDiceComoSeProtegeTests` recorre la **tabla de enrutado del host** (desde el 1.3; hasta
entonces eran dos `typeof` escritos a mano, y por eso el primer controlador de Terceros habría
quedado fuera sin ponerse nada rojo). Hoy: **110 acciones**, de ellas **72** cambian estado — **40**
exigen `If-Match`, **15** admiten `Idempotency-Key` y **17** están exentas con su motivo escrito.
Los números están fijados en el propio test: un barrido cuya enumeración devuelva nada saldría verde
por la peor de las razones.

> **Rehechas enteras en el 1.6 y otra vez en el 1.8, y esa es la anotación que importa.** Los números del test se han ido
> moviendo ítem a ítem —cada uno los sube y lo ve rojo si no lo hace—, pero **las tablas de aquí
> abajo se quedaron en las del 0.10** (46 acciones, 32 escrituras, 13 · 6 · 13) y así llegaron hasta
> el 1.5. Nada lo puso rojo porque **una tabla en un `.md` no es una fuente que ningún test compare**:
> es prosa, y la prosa no falla. El recorrido real fue 0.9 → 46/32/**16·6·10**, 0.10 → 46/32/**13·6·13**
> (mudanza de los tres desbloqueos, ADR-0017), 1.6 → 93/60/**30·13·17** y hoy 110/72/**40·15·17**.
>
> **Y volvieron a quedarse atrás en el 1.7, que es la segunda vez.** Entre `4960922` y `9f8ff56` el
> test decía **102 acciones, 68 escrituras, 38 · 13 · 17** —lo que el 1.7 añadió con las retiradas y
> los dos cierres— y estas tablas seguían con las del 1.6. Nada se puso rojo, por lo mismo de
> siempre. Se rehacen enteras ahora, con el delta del 1.8 separado del que arrastraban: **+8
> acciones, +4 escrituras, +2 `If-Match`, +2 `Idempotency-Key`, cero exentas**, que son exactamente
> las ocho de Catálogo —cuatro por recurso: listado, lectura por identificador, alta y
> modificación—. Una tabla que se copia mal dos veces seguidas es una tabla en la que ya no se
> puede confiar.

**Los quince recursos que emiten `ETag` en su lectura por identificador** — uno por raíz de agregado
con `GET /{id}`, que es la misma lista de las quince altas de más abajo:

| Recurso | Ruta del `GET` que emite el `ETag` |
|---|---|
| Almacén | `GET /api/v1/organizacion/almacenes/{id}` |
| Conversión de unidades | `GET /api/v1/organizacion/conversiones-de-unidades/{id}` |
| Divisa | `GET /api/v1/organizacion/divisas/{id}` |
| Ejercicio | `GET /api/v1/organizacion/ejercicios/{id}` |
| Empresa | `GET /api/v1/organizacion/empresas/{id}` |
| Impuesto | `GET /api/v1/organizacion/impuestos/{id}` |
| Serie | `GET /api/v1/organizacion/series/{id}` |
| Tipo de cambio | `GET /api/v1/organizacion/tipos-de-cambio/{id}` |
| Ubicación | `GET /api/v1/organizacion/ubicaciones/{id}` |
| Unidad de medida | `GET /api/v1/organizacion/unidades-de-medida/{id}` |
| Rol | `GET /api/v1/identidad/roles/{id}` |
| Usuario | `GET /api/v1/identidad/usuarios/{id}` |
| Tercero | `GET /api/v1/terceros/terceros/{id}` |
| Artículo *(1.8)* | `GET /api/v1/catalogo/articulos/{id}` |
| Categoría *(1.8)* | `GET /api/v1/catalogo/categorias/{id}` |

Los listados **no** lo emiten: un `ETag` sobre una página sería el de la página, no el de cada
elemento, y un cliente que lo devolviera en un `If-Match` estaría citando una versión que no es la
del recurso que escribe.

**Las cuarenta operaciones que exigen `If-Match`:**

| Recurso | Operaciones |
|---|---|
| Almacén | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Conversión de unidades | `PUT /{id}` |
| Divisa | `PUT /{id}` |
| Ejercicio | `PUT /{id}`, `DELETE /{id}`, `POST /{id}/cierre`, `DELETE /{id}/cierre` |
| Empresa | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Impuesto | `PUT /{id}`, `POST /{id}/cierre` |
| Serie | `PUT /{id}`, `DELETE /{id}` |
| Tipo de cambio | `PUT /{id}` |
| Ubicación | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Unidad de medida | `PUT /{id}` |
| Rol | `PUT /{id}` |
| Usuario | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Tercero | `PUT /{id}`, `DELETE /{id}` (bloqueo) |
| Artículo *(1.8)* | `PUT /{id}` |
| Categoría *(1.8)* | `PUT /{id}` |
| Tercero — lo que cuelga *(1.6)* | `POST /{terceroId}/contactos`, `DELETE /{terceroId}/contactos/{contactoId}`, `POST /{terceroId}/cuentas-bancarias`, `DELETE /{terceroId}/cuentas-bancarias/{cuentaId}`, `POST /{terceroId}/cuentas-bancarias/{cuentaId}/preferente`, `PUT /{terceroId}/condiciones-pago/{rol}`, `PUT /{terceroId}/limite-credito` |

Las subrutas —el bloqueo, el cierre— citan la versión **del recurso**, no una suya: no son otro
recurso, son otra puerta al mismo. Es lo que hace que bloquear un almacén y modificarlo compitan por
la misma versión, que es lo que se quiere.

Y las **siete del 1.6** son la misma idea llevada a lo que cuelga: un contacto y una cuenta no tienen
`ETag` propio ni testigo de concurrencia propio, así que **citan el de la ficha**. Colgar un contacto
mientras otro cambia la razón social es un choque de verdad —los dos escriben el mismo agregado— y se
quiere que la segunda escritura se lleve el `412`. Ninguna de las siete admite `Idempotency-Key`, y no
por descuido: no son altas de un recurso nuevo con vida propia, son modificaciones de un agregado que
ya existe, y para eso el mecanismo que protege es el otro.

**Las quince rutas que admiten `Idempotency-Key`** — las quince altas, y solo ellas:

| Ruta | Módulo | Almacén que la atiende |
|---|---|---|
| `POST /api/v1/organizacion/almacenes` | `organizacion` | `AlmacenDeIdempotenciaDeOrganizacion` |
| `POST /api/v1/organizacion/conversiones-de-unidades` | `organizacion` | ídem |
| `POST /api/v1/organizacion/divisas` | `organizacion` | ídem |
| `POST /api/v1/organizacion/ejercicios` | `organizacion` | ídem |
| `POST /api/v1/organizacion/empresas` | `organizacion` | ídem |
| `POST /api/v1/organizacion/impuestos` | `organizacion` | ídem |
| `POST /api/v1/organizacion/series` | `organizacion` | ídem |
| `POST /api/v1/organizacion/tipos-de-cambio` | `organizacion` | ídem |
| `POST /api/v1/organizacion/ubicaciones` | `organizacion` | ídem |
| `POST /api/v1/organizacion/unidades-de-medida` | `organizacion` | ídem |
| `POST /api/v1/identidad/roles` | `identidad` | `AlmacenDeIdempotenciaDeIdentidad` |
| `POST /api/v1/identidad/usuarios` | `identidad` | ídem |
| `POST /api/v1/terceros/terceros` | `terceros` | `AlmacenDeIdempotenciaDeTerceros` |
| `POST /api/v1/catalogo/articulos` | `catalogo` | `AlmacenDeIdempotenciaDeCatalogo` |
| `POST /api/v1/catalogo/categorias` | `catalogo` | ídem |

**Y las diecisiete exentas, con el motivo resumido** (el entero está en el test):

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
| `Ubicaciones.Desbloquear` (1.1) | La cuarta del ADR-0017, por el mismo motivo que las otras tres |
| `Terceros.Desbloquear` (1.5) | La quinta, y la que cierra la lista: los cinco agregados bloqueables del art. 32 tienen hoy su desbloqueo exento por la misma razón |
| `Empresas.Buscar` (1.3) | Es una **lectura** que viaja por `POST` para no poner criterios sensibles en la URL (ADR-0025). No cambia estado aunque el verbo lo sugiera, así que ninguno de los dos mecanismos le corresponde. **DEPENDE DE** que siga sin efectos: el día que una búsqueda guarde algo —un histórico, una preferencia— esta exención deja de valer |
| `Terceros.Buscar` (1.5) | Lo mismo, y con más motivo: sus criterios incluyen el identificador fiscal, que es justo lo que no puede ir en una URL |

> Los **cinco desbloqueos** son el mismo argumento repetido cinco veces, y están escritos uno a uno a
> propósito: cada uno **nombra la condición de la que depende**, para que quien la cambie se
> encuentre con la frase que dice que esa exención caduca. En el 1.4 caducó media frase de las cuatro
> que había entonces —`GET .../bloqueados` es una lectura que sí entrega filas bloqueadas— y se
> reescribieron **diciendo qué las sustituye**: hoy las sostiene que ningún camino de lectura entregue
> un recurso bloqueado **con testigo de versión**, y eso lo afirman dos reglas que se ponen rojas, no
> un párrafo.

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

### Tomadas por el agente de desarrollo — ítem 0.11 (2026-08-31)

**Respuestas de la puerta de clarificación**, preguntadas antes de escribir una línea porque las
tres cambiaban lo que había que construir:

- **El selector de empresa saca los nombres de la propia sesión.** `SesionDto.Empresas` deja de ser
  `IReadOnlyList<Guid>` y pasa a llevar identificador **y razón social**, poblados con un método
  nuevo de `IConsultaDeEmpresas` —el puerto de lectura entre módulos que ya existía y que Identidad
  ya referenciaba—. La alternativa descartada era que el frontal cruzara contra
  `GET /api/v1/organizacion/empresas`: ese endpoint exige `organizacion.empresa.ver`, y **pertenecer
  a varias empresas no implica poder ver la ficha de ninguna** — quien no tuviera el permiso vería
  identificadores pelados en el desplegable con el que se cambia de empresa.

- **Lo bloqueado sigue sin camino de lectura, y el 0.11 no abre ninguno.** El shell no lleva
  pantalla de bloqueados: la lista de sitios que abren `ViendoLoBloqueado` sigue siendo los tres
  desbloqueos y se sigue comparando entera. Queda **ABIERTA** en *Notas / riesgos* con lo que hace
  falta para decidirlo. Abrir un listado de administración habría sido trabajo de dominio y de
  derecho —motivo nuevo, permiso nuevo y un ADR que argumentara por qué el art. 32 admite esa
  visualización— dentro de un ítem cuya disciplina es otra.

- **El shell lleva dos listados de solo lectura: almacenes y empresas.** No es adorno: la mutación
  que quita el vaciado de caché al cambiar de empresa **solo se puede probar por el efecto si hay
  una lista de datos de empresa que mirar**, y un test que compruebe que se llamó a `clear()` prueba
  que se llamó a `clear()`. Ni altas, ni ediciones, ni bloqueos: eso no es shell.

- **Y un hallazgo que salió al hacerlo: el selector es también la lista de empresas operables.**
  Al esconder lo bloqueado del desplegable quedó a la vista una incoherencia que ya existía desde el
  0.10: se podía **abrir sesión, renovarla y cambiarse** a una empresa suprimida al amparo del art.
  32. El bloqueo la tapaba en las pantallas sin echar a nadie de ella, y el token de refresco la
  habría mantenido activa durante días. Los tres casos de uso de sesión calculan ahora el selector
  **antes** de elegir la empresa activa, y `ConstructorDeSesion` comprueba la invariante. La empresa
  bloqueada sale por el error de siempre —`401` de credenciales al entrar, `empresa-no-pertenece` al
  cambiar—: un código propio confirmaría el bloqueo, que es justo lo que el `404` del 0.10 se niega
  a confirmar. Motivo de revocación nuevo, `EmpresaSuprimida`, sin migración porque se guarda con
  `HasConversion<string>()`.

**Decisiones tomadas al construirlo**, todas reversibles menos la primera:

- **Sin librería de primitivas accesibles.** Ni Radix ni equivalente. Todos los controles del
  armazón tienen elemento nativo —`<select>` para el selector de empresa, `<button>`, `<a>`,
  `<table>` con `<caption>` y `<th scope>`, `<label for>`— y `stacks/react` dice «elemento nativo
  antes que ARIA». Una librería de primitivas se paga en descargas y en una capa más entre el rol y
  el marcado, y aquí no compraría nada: llegará cuando haga falta un diálogo modal, un menú o un
  combo con autocompletado, que es donde lo nativo se acaba. **Se anota porque es una desviación de
  lo esperable**, no porque sea barata de cambiar.

- **El cambio de empresa reinicia con `resetQueries()`, no con `clear()`.** Es contraintuitivo y
  costó un test: `clear()` parece lo más contundente y hace menos. Vacía el almacén, pero no toca a
  los observadores ya montados, que se quedan enseñando su último resultado —el de la empresa
  anterior— sin volver a pedir nada, porque su consulta ya no existe. `resetQueries()` los devuelve
  a su estado inicial **y** vuelve a pedir lo que está en pantalla. Al entrar y al salir sí basta
  `clear()`: ahí no hay ninguna pantalla de datos montada. Lo cazó el test que mira lo que se pinta;
  uno de «se ha llamado a `clear()`» lo habría dado por bueno.

- **El testigo no forma parte de ninguna clave de consulta, y la empresa tampoco.** La empresa va
  dentro del testigo, así que las mismas claves devuelven otras filas según con quién se opere. Es
  exactamente por eso que el cambio vacía la caché **entera** en vez de invalidar una lista de
  claves elegidas a mano: esa lista es la que alguien olvidará ampliar al añadir la séptima pantalla.

- **Los permisos son la única parte del contrato escrita a mano** (`shared/sesion/permisos.ts`). No
  hay de dónde generarlos: el catálogo es un endpoint en tiempo de ejecución, no un `enum` del
  OpenAPI. El modo de fallo está elegido: una constante mal escrita **esconde** la pantalla en vez
  de enseñarla, porque la comprobación es «¿está este permiso en la lista?». Queda en *Notas /
  riesgos*.

- **`total` llega del contrato como `integer | string`, y se estrecha en la traducción.** No es un
  fallo del generador: `JsonSerializerDefaults.Web` implica `NumberHandling = AllowReadingFromString`,
  y el documento describe honestamente lo que la API acepta. Se resuelve en un sitio
  (`shared/api/enteros.ts`) y no cambiando el servidor: tocar la serialización para que el documento
  quede más cómodo sería mover el contrato para que encaje el cliente, que es al revés. Mismo
  criterio que el `[Produces]` del ADR-0018.

- **La CI comprueba también el cliente generado.** Que `docs/api/openapi.json` esté al día no dice
  nada de `esquema.ts`: entre uno y otro cabe un contrato nuevo con un cliente viejo, que **compila**
  —el compilador solo ve el fichero generado— y miente en ejecución. Es el mismo cerrojo, un eslabón
  más abajo.

- **La API se sirve por el mismo origen, también en el contenedor.** nginx reenvía `/api/`. No es
  comodidad: la cookie de refresco es `__Host-bastion-refresco` y ese prefijo solo viaja al origen
  exacto que la puso. Por eso el empaquetado no lleva ninguna URL absoluta ni ninguna `VITE_API_URL`
  —que además sería un secreto en el paquete si algún día llevara algo más que un host—.

### Tomadas por el agente de desarrollo — ítem 0.12 (2026-09-02)

El criterio del ítem dice «NetArchTest con las reglas de frontera del §4; **fallan** si un módulo
cruza una frontera», y el verbo es lo que ordena todo lo demás: el entregable no son las reglas
escritas, es **cada regla vista en rojo por una violación deliberada antes de aceptarla en verde**.

- **Toda regla afirma también que su conjunto no está vacío, y ese conteo se compara.** Es la
  decisión de fondo y está entera en
  **`docs/adr/adr-0020-una-regla-sin-afirmacion-de-conjunto-no-vacio-no-es-una-regla.md`**. El
  motivo es cómo funciona la herramienta: si el selector no casa con nada, no hay ningún tipo que
  incumpla, **así que la regla se cumple**. Verdad por vacuidad, verde hoy y verde el día que
  alguien cruce la frontera, y nada en el informe la distingue de una regla que sí protege algo.
  Todas pasan por un único punto de entrada, `Barrido.Exige`, que antes de evaluar nada comprueba
  tres cosas: que el alcance está declarado y no está vacío, que los ensamblados leídos son los
  declarados —comparados enteros— y que el número de tipos seleccionados es el esperado y mayor que
  cero. **Una regla sin esas afirmaciones no cuenta como escrita.**

- **Las reglas se aplican a lo que se descubre, no a una lista de nombres tecleada.** Trece de los
  dieciséis módulos del §5 no existen. Escribirlas por nombre —«`Bastion.Ventas.Domain` no
  referencia EF Core»— daría trece baterías verdes que no miran nada, y un informe que dice
  «dieciséis fronteras comprobadas». `Ensamblados` descubre del disco y de la salida de compilación;
  `Inventario` declara lo que el proyecto dice que hay; y los tests comparan las dos mitades
  **enteras y en los dos sentidos**, que es la misma forma de los cinco barridos anteriores.

- **El proyecto de test referencia SOLO la raíz de composición (`Bastion.Api`).** Referenciar módulo
  a módulo habría sido una segunda declaración de qué módulos hay, escrita a mano en un `.csproj` y
  comparada por nadie. Referenciando la raíz, todos los ensamblados caen en la salida y el conjunto
  se descubre de ahí.

- **Cada cadena escrita a mano lleva su contraejemplo: la capa donde ese espacio de nombres SÍ tiene
  que aparecer.** Las prohibiciones al dominio son de fuera del proyecto —`Microsoft.EntityFrameworkCore`,
  `Microsoft.AspNetCore`, `Npgsql`, y `Bastion.BuildingBlocks.Infrastructure`, que es la que un
  dominio tiene más a mano— y no se pueden derivar de nada. Si la cadena está bien escrita, se
  encuentra donde tiene que estar; si está mal, no se encuentra en ninguna parte y eso es lo que se
  pone rojo. La cuarta mutación demuestra que sin esto la regla 2 no protege nada.

- **`System.Data` se prohibió, se comprobó y se quitó ANTES de existir.** Parecía gratis —ADO.NET es
  la otra puerta a la base de datos— y su contraejemplo salió a **cero** en las tres capas donde
  tendría que aparecer: nadie en el proyecto depende de `System.Data`, porque el acceso a datos
  entra por EF Core y por Npgsql. Habría sumado una regla verde que no protege nada. Queda escrito
  en el fichero, porque es el mecanismo de este ítem funcionando sobre su propio autor.

- **Hay una decimoquinta regla que NO lee IL, sino los `.csproj`.** Salió de la propia batería de
  mutaciones: NetArchTest lee IL, y **una referencia de proyecto que todavía no usa nadie no emite
  IL**. Se le puede poner a `Bastion.Identidad.Application` una referencia a
  `Bastion.Organizacion.Domain` —el cruce que la regla 1 prohíbe— y las catorce reglas anteriores
  siguen verdes; el compilador tampoco avisa, porque una referencia sin usar no es un aviso. Y es el
  orden natural de las cosas: primero se añade la referencia, y la línea que la usa llega en otro
  commit. Un carril que solo mire el IL aprueba el commit que **abre** la frontera y suspende el que
  la **cruza**, que es tarde. Así que el uso se vigila en la IL y el permiso en el `.csproj`, por
  separado, comparando el grafo de aristas entero.

- **Qué regla vive en qué carril, cuando cabe en los dos.** Solo hay una:
  `UnidadDeTrabajoPorModuloTests.NingunCasoDeUso_PideLaUnidadDeTrabajoComun`, que es un hecho de
  tipos y NetArchTest expresaría sin esfuerzo. **Se queda donde está**, en los tests funcionales,
  porque va en pareja con `CadaModulo_DeclaraSuPropiaUnidadDeTrabajo`, que sí necesita el contenedor
  construido y no puede venirse aquí. Partir la pareja por pureza dejaría dos mitades que se leen
  sin la otra. Regla general: **cuando una regla cabe en los dos carriles, va con la regla hermana
  que solo cabe en uno.**

- **El paquete: `NetArchTest.eNhancedEdition` 1.4.5, MIT declarada en el propio `.nuspec`.** Es una
  bifurcación, y coger la bifurcación en vez del original —que tiene 15 millones de descargas y 1772
  estrellas— es la decisión que había que justificar. Tres motivos, comprobados en el `.nuspec` y en
  la API de GitHub el 2026-09-02: **(1) licencia** — `NetArchTest.Rules` 1.3.2 no declara ninguna en
  el paquete (el repositorio lleva un LICENSE MIT, pero el artefacto que se descarga no lo dice, y
  aquí ya está aprendido que «siempre fue gratis» no es una comprobación); la bifurcación la declara
  dentro del paquete. **(2) mantenimiento** — original: 1.3.2 del 2021-05-23, último commit del
  2023-07-03; bifurcación: 1.4.5 del 2025-06-04. **(3) `Mono.Cecil` 0.11.6 frente a 0.11.3**, y aquí
  hay que leer ensamblados de **.NET 10**: el lector de metadatos es justo la pieza que envejece
  mal. Lo que se pierde —53 estrellas frente a 1772, un solo mantenedor— y por qué el riesgo está
  acotado —solo entra en proyectos de test, y la superficie usada es la API fluida de la 1.3.2,
  común a las dos ediciones— está escrito en `Directory.Packages.props`. Va en el **carril rápido**,
  que es donde ya estaba el paso «Tests de dominio y de arquitectura» esperándolo.

- **Los identificadores de permiso escritos a mano se cierran en el 0.13, y NO aquí.** No es una
  regla de arquitectura: este carril lee ensamblados de .NET y `frontend/src/shared/sesion/permisos.ts`
  es TypeScript, así que no hay forma de que entre. Lo que hace falta es un barrido que compare esa
  lista contra el catálogo que la API sirve de verdad, en la forma de `ElFiltroNoSeSaltaPorAhiTests`
  —que ya lee código fuente—, y eso **cabe en el 0.13**. Esto corrige lo que decía la nota abierta,
  que lo mandaba a la fase 1: el test no necesita que haya más permisos para existir, y esperar a la
  fase 1 significa entregar la fase 0 entera con la única cadena del contrato escrita a mano sin
  nadie que la compare.

- **El `.gitkeep` de `tests/Arquitectura.Tests/` se va con este ítem, y se auditaron los demás.**
  Quedan **63**: los **60** del andamio de módulos (doce módulos con carpeta × cinco capas, fases 1
  a 10, deliberados) y tres carpetas legítimamente vacías —`db/semillas/`, `docs/dominio/` y
  `frontend/public/`—, todas de fases posteriores. El único que sobraba era
  **`db/migraciones/.gitkeep`**, con dieciocho ficheros versionados dentro desde hace ítems: **se
  borra también**. Ningún otro andamio de la fase 0 se queda sin llenar.

### Tomadas por el agente de desarrollo — ítem 0.13 (2026-09-02)

- **El criterio de este ítem ya se cumplía el día que empezó, y ese era el problema.** Había
  *workflow*, compilaba, pasaba linter, corría los tres carriles de tests y salía verde de punta a
  punta. Un ítem cuyo criterio literal ya está satisfecho es el que se cierra sin hacer nada. Lo que
  se ha hecho es preguntar qué afirma de verdad cada verde, y el resultado fueron seis cosas que
  daban verde sin comprobar lo que decían.

- **Quién aplica el esquema en un despliegue: un contenedor de un solo uso, no el arranque de la
  API.** La deuda más antigua de la fase, abierta el 26 de agosto con el 0.4. El mismo artefacto
  invocado con `--migrar` aplica las migraciones de los tres contextos en orden y sale; los demás
  servicios esperan a `service_completed_successfully`. Se descarta migrar en el arranque —cómodo
  con una réplica, avería con dos: DDL simultáneo, y el cambio de esquema lo aplica «la réplica que
  arranque primero», o sea nadie— y se descarta un paso con `dotnet ef database update` —necesita el
  SDK en el contenedor y migraría desde el **código fuente**, no desde el artefacto que se
  despliega—. El argumento entero, con el precio de la elegida, en **ADR-0021**.

- **Se pide por argumento y no por variable de entorno.** Una variable se hereda: basta con que
  alguien la ponga en el `.env` compartido para que *todas* las réplicas se conviertan en
  migradoras. Un argumento se escribe servicio a servicio y se ve en `docker compose config`.

- **El `.dockerignore` excluía `db/migraciones`, y eso era una avería muda.** Salió al probar el
  migrador dentro de un contenedor por primera vez: dijo «el esquema ya estaba al día» tres veces y
  salió con **0** sobre una base sin una sola tabla. Las migraciones viven fuera de los proyectos
  (§14) y entran por un `<Compile Include="../../db/migraciones/…" />`; sin la carpeta en el
  contexto de construcción el glob no casaba con nada, y **un glob vacío no da error**: compila
  igual y el ensamblado publicado sale sin migraciones. Dos arreglos, no uno: se quita la exclusión
  **y** el migrador afirma que conoce al menos una migración antes de mirar si hay pendientes. Cero
  migraciones conocidas significa un ensamblado mal construido, no una base al día.

- **El *job* de Humo gana la sonda que le faltaba: una petición que LEE UNA TABLA.** Las cinco que
  había —vida, disponibilidad, el frontal carga, el frontal reenvía `/api`, las trazas llegan—
  **ninguna toca una tabla**; la de disponibilidad pregunta si PostgreSQL acepta conexiones y
  responde, y eso es cierto sobre una base vacía. La nueva inicia sesión con la cuenta sembrada y
  lista empresas con el testigo. Medida contra el despliegue anterior daba **500**, y con los tres
  esquemas tirados vuelve a darlo mientras las otras cinco siguen verdes (mutación 6).

- **Las siete `BASTION_SEMILLA_*` se rellenan en la CI.** Iban vacías, así que la semilla se saltaba
  con un aviso y **nadie ejercía ese camino**. La primera vez que se rellenaron, contra el compose
  anterior, la API ni arrancaba. La contraseña se sortea en el propio *job* y no se imprime.

- **El suelo de casos se acompaña de la lista de ensamblados, en los DOS carriles.** El suelo es una
  red gruesa y protegía menos de lo que parecía: en el carril rápido, de cinco ensamblados cubría
  dos —perder los 103 de `Api.FunctionalTests` deja 300 clavados, y la comparación es «menor que»—;
  en el de integración, de dos cubría uno. Ahora la lista se declara y **se compara entera, en las
  dos direcciones**: el que desaparece se nombra, el que aparece obliga a declararlo, y solo cuenta
  el que ejecuta al menos un caso.

- **Ningún comentario de recuento vuelve a afirmar cuántos casos hay.** El del carril de integración
  decía «142 (114 de la API + 28 de Organización)» cuando llevaba 206, y llevaba ítems mintiendo.
  Un número escrito en un comentario no lo actualiza nadie: la cuenta la publica cada *run* como
  anotación. Donde queda un número —el del carril rápido— está marcado como **medida del 0.13** y
  está ahí como argumento, no como inventario.

- **Las capas de aplicación se descubren; ya no se teclean.** `UnidadDeTrabajoPorModuloTests`
  declaraba dos `typeof(...)` y llevaba desde el 0.9 sin `Bastion.Auditoria.Application`. Ahora
  salen de los `Bastion.*.Application.dll` que arrastra `Bastion.Api`, menos el bloque común, con la
  afirmación de conjunto no vacío del ADR-0020. El módulo de la fase 1 entra solo.

- **El barrido de permisos vive en el carril de INTEGRACIÓN, y esa es la decisión.** Lo que hay que
  comparar no es una constante de C#: es lo que la API sirve por `GET /api/v1/identidad/roles/permisos`,
  que exige `identidad.rol.ver`. Eso pide un inicio de sesión de verdad y, por tanto, una base de
  datos. El carril funcional tiene el host pero no la base, así que allí solo se llegaría **forjando
  un testigo** —lo que este proyecto no hace nunca— y probaría el manejador de permisos, no el
  catálogo. Comparar contra las constantes diría que dos listas escritas a mano coinciden.

- **Las cuatro publicaciones colgadas del aviso de Node.js 20 se resuelven de dos maneras
  distintas.** Leídas las notas de versión antes: `upload-artifact` v5 y v6 pasan a Node 24 y exigen
  runner ≥ 2.327.1 —`ubuntu-latest` va muy por encima—, v7 añade subida directa opcional;
  `download-artifact` v8 pasa a **errar** ante un hash que no cuadra (antes solo avisaba) y deja de
  desempaquetar a ciegas: mira el `Content-Type`. Se suben a v7 y v8. Los dos de docker
  —`build-push-action` y `setup-buildx-action`— **desaparecen con el *job* que los usaba**.

- **Se retira el *job* `imagenes`, que es la salida que `PLAN.md` dejaba abierta.** Construía las
  dos imágenes sin publicarlas, con los mismos `needs` y los mismos dos Dockerfile que `Humo`, que
  las construye, las **arranca** y les hace peticiones. Una afirmación que es subconjunto estricto
  de otra no añade cobertura: añade minutos y una segunda cosa que actualizar. Se pierde la caché
  `type=gha` de buildx, que `compose build` no usa igualmente.

- **Publicar no es haber publicado lo que se dice.** `upload-artifact` sale verde subiendo un zip
  vacío si el `path` no casa con nada. Los tres artefactos se bajan ahora en el mismo *run* y se les
  mira dentro: el `dist` con su `index.html`, el `openapi.json` **comparado byte a byte** con el del
  repositorio, y los `.trx` de los **dos** carriles —que estén los dos es lo que distingue «se
  subieron los resultados» de «se subió la mitad»—.

- **Se retira el `mkdir -p ~/.nuget/packages` del ADR-0002.** Existía porque los `packages.lock.json`
  solo tenían entradas `"type": "Project"` y ningún `restore` creaba la carpeta. Hoy tienen 17
  directas, 102 transitivas y 31 centrales. La retirada estaba prescrita para este ítem, no antes.

- **`frontend/public/` sobraba, y además se estaba publicando.** De las tres carpetas vacías que
  quedaban con `.gitkeep`, dos tienen dueño escrito en el plan maestro: `db/semillas/` recibe los
  datos maestros versionados que un comando carga (§14) y `docs/dominio/` el modelo y el glosario
  del lenguaje ubicuo. La tercera no la nombra nadie —ni el plan maestro, ni `vite.config.ts`, ni el
  `index.html`— y no era inerte: Vite copia `public/` al raíz de `dist`, así que el `.gitkeep`
  viajaba a la imagen y **nginx lo servía con 200**. Se borra.

### Auditoría previa a la fase 1 (2026-09-02)

La fase 0 se cerró cumpliendo su criterio **ítem por ítem**, y eso se re-midió antes de nada: build
`0 Advertencia(s), 0 Errores`; **403** casos rápidos en 5 ensamblados y **207** de integración en 2,
ejecutados de nuevo en local; los **12** *commits* de `a9c54e7..ce4f083` con `%G?` = `G`, autor único
y cero *trailers*; checklist sin pendientes. El informe de cierre no exageraba nada.

Pero el criterio que se cumplió es el del **Anexo A.3**, y A.3 es más estrecho que el plan maestro.
Esta auditoría contrasta lo construido contra el plan maestro entero y contra la biblioteca, no
contra el checklist. Salen **seis hallazgos**. Tres tienen solución objetiva y se aplican; tres se
llevaron al usuario porque cambiaban cómo se estructura la fase 1, y los decidió él.

#### F1 · La internacionalización nunca se instaló, y el plan la daba por hecha

El §3 la fija: `react-i18next`, es/en, con el motivo escrito al lado — *«Los textos fuera del código
**desde el primer día**: meterlos después es un refactor global»*. El §10 le da sitio: `app/`.

No es que se decidiera no hacerla: **es que nadie la echó de menos**. Este mismo PLAN se la asignó al
0.11 dos veces —*«No hay enrutador, ni caché de servidor, ni formularios, **ni i18n**, ni cliente de
API: todo eso es el ítem 0.11»*, y *«cuando el frontal tenga enrutador, caché de servidor,
formularios, **i18n** y cliente de API, medir lo que pesa»*—. El 0.11 entregó enrutador, caché,
formularios y cliente; i18n no, y su criterio de aceptación tampoco la nombraba, así que el ítem se
cerró en verde sin que nada lo señalara. Es la forma de error que persigue toda la fase: **una
comprobación que no cubre lo que se creía que cubría**, y aquí el hueco estaba en el criterio mismo.

Hoy el frontal son **49 ficheros** y unos **diez** llevan texto visible. La fase 1 —Terceros con su
ficha, direcciones, contactos y condiciones de pago; Catálogo con artículos, categorías, tarifas y
códigos de barras— los multiplica. El aviso del §3 no es retórico: el coste crece con cada pantalla
escrita antes de ponerlo.

**Decidido por el usuario:** se hace **antes** de la fase 1, como ítem **0.14**.

#### F2 · Organización quedó a medias, y el criterio de la fase 1 depende justo de lo que falta

El §7.1 le da a Organización siete agregados: Empresa, Ejercicio, SerieDocumental, **Impuesto**,
**Divisa + TipoCambio**, **UnidadMedida + ConversionUM**, y Almacen + **Ubicacion**. El A.3 · 0.4
pidió solo cuatro: *«CRUD de `Empresa`, `Ejercicio`, `Serie` y `Almacen`»*. Se hizo exactamente eso,
sin ampliar alcance —que era lo correcto— y los otros tres **no se anotaron en ninguna parte**:

```
grep -rn "Impuesto|UnidadMedida|TipoCambio|Ubicacion"  src/         ->  0
grep -n  "Impuesto|UnidadMedida|TipoCambio"            docs/PLAN.md ->  0
```

El problema es que el criterio de aceptación de la **fase 1** (§15) es *«alta de cliente/proveedor
con NIF validado y de artículo con **unidad, impuesto** y tarifa»*, y el Artículo del §7.3 lleva
«unidad base, impuesto por defecto». Por la **regla de frontera 4**, Catálogo no puede definirse los
suyos: guarda el id y **valida contra el contrato de Organización** — un contrato que hoy no expone
ninguno de los dos. **La fase 1 no puede cumplir su propio criterio con lo que hay.**

No es un fallo de ejecución: es una inconsistencia del plan maestro entre §5 («Organización = fase
0»), A.3 (que pidió cuatro agregados) y §15 (que necesita los otros). Por el §2 de `CLAUDE.md`, eso
es exactamente un caso de «resulta inviable al montarlo → se pregunta».

**Decidido por el usuario:** se completa Organización **entera** —los cuatro agregados que faltan,
con sus semillas y su cargador— antes de la fase 1, como ítem **0.15**.

#### F3 · `db/semillas` estaba en `.dockerignore`: la misma avería muda que el 0.13 acababa de quitar

`.dockerignore` excluía `db/semillas` en la línea de al lado del comentario que explica por qué se
retiró `db/migraciones` (ADR-0021). Hoy era inofensivo porque la carpeta está vacía; el §12 dice que
las semillas —**PGC, tipos de IVA, unidades, países**— son ficheros versionados «que un comando
carga», y ese comando será el mismo contenedor migrador.

El día que exista: la imagen se construye sin los ficheros, el cargador encuentra cero, y **«cero
semillas» es indistinguible de «no había nada que cargar»**. Es la avería del ADR-0021 replantada en
el sitio de al lado, y **F2 la activa**: los tipos de IVA y las unidades *son* semillas.

**Solución objetiva, aplicada.** La línea se retira. Su efecto no se puede comprobar hoy —no hay
semillas—, así que la comprobación se ata al **0.15**, donde el cargador afirmará conjunto no vacío
y la CI mirará dentro de la imagen. Un `.dockerignore` corregido y sin nada que copiar no prueba
nada por sí solo.

#### F4 · `features/` no espejaba los módulos, y había un conflicto normativo sin resolver

Dos fuentes normativas se contradicen, y nadie lo había notado:

- **§10:** *«Las carpetas de `features/` **espejan los módulos del backend**»*, con el árbol
  `features/ventas/`, `features/compras/`, `features/inventario/`…
- **`stacks/react/convenciones.md`:36:** *«Organiza por **funcionalidad**, no por tipo técnico:
  `src/features/<funcionalidad>/{api,model,ui}`»*.

El 0.11 siguió la biblioteca: `acceso`, `almacenes`, `empresas`, `inicio` — que son **recursos**, no
módulos (`almacenes` y `empresas` son ambos de `organizacion`; `acceso` es `identidad`). Sin decisión
registrada, porque el conflicto no se vio.

Hoy da igual. En la fase 1 deja de dar igual, y por una razón concreta: el §7.2 dice que *«un tercero
puede ser cliente y proveedor a la vez»* — es **un solo agregado**. Repartiendo por recurso salen
`features/clientes` y `features/proveedores` compartiendo ficha, direcciones, contactos y condiciones
de pago; y como la regla —del §10 **y** de la biblioteca, línea 39— es que **«una funcionalidad nunca
importa de otra»**, todo eso tendría que subir a `shared/`: código de dominio dentro del sistema de
componentes, que es justo lo que `shared/` no debe ser. Con Catálogo es peor.

**Decidido por el usuario: gana el §10.** `features/` espeja los módulos del backend. Son **4
carpetas y 14 ficheros** hoy; después de la fase 1, veinte y pico. Se reordena como ítem **0.16**.
Las dos fuentes dejan de estar en conflicto porque el §10 es la más específica: la biblioteca dice
«por funcionalidad y no por tipo técnico» —contra `components/`, `hooks/`, `pages/`—, y un módulo del
backend **es** una funcionalidad; el plan maestro solo fija cuál es el corte.

#### F5 · La «batería mínima» de `AGENTS.md` ya no era la que ejecuta la CI

`AGENTS.md` decía: *«la batería mínima es `dotnet build` + `dotnet test` + `dotnet format
--verify-no-changes` + `npm run build` + `npm run lint`. **Es exactamente lo que ejecuta la CI**»*.
Había dejado de serlo. La CI ejecuta además **«Contrato»** (regenera el cliente y compara),
**«Migraciones»** (modelo sin cambios pendientes), **«OpenAPI»** (contrato versionado al día),
`npm run typecheck`, `npm run format:check` y `npm run test`.

Seis comprobaciones que un agente siguiendo `AGENTS.md` al pie de la letra **no correría antes de dar
un ítem por hecho** — y las tres primeras son las que más rojos causaron en el 0.11 y el 0.13. Es la
instrucción que gobierna cuándo se declara algo terminado, así que estaba mintiendo justo donde más
caro sale.

**Solución objetiva, aplicada.** La batería se reescribe con lo que la CI ejerce de verdad, marcando
cuál es el orden barato (lo que falla en segundos, primero) y qué exige Docker.

#### F6 · `docs/dominio/` estaba vacío

El §12 lo define como *«glosario del lenguaje ubicuo + diagramas E-R y de estados»*, y
`principios/ddd.md` pone el lenguaje ubicuo en su lista de «siempre». Hay tres módulos construidos,
21 ADR y 3.374 líneas de PLAN, y ahí solo había un `.gitkeep`.

Importa **ahora** y no antes porque la fase 1 es donde el vocabulario se dispara y donde más se paga
no tenerlo fijado: *tercero* / *cliente* / *proveedor*, *artículo* / *referencia*, *tarifa* /
*precio*, *unidad base* / *unidad de compra*. Palabras que, sin acordarse antes, se acuerdan tres
veces distintas en tres pantallas.

**Solución objetiva, aplicada.** Se siembra `docs/dominio/glosario.md` con el vocabulario **ya
construido** —lo que existe en código hoy, no lo que vendrá— y se amplía al abrir cada módulo. Un
glosario que inventa términos por adelantado es peor que no tenerlo: fija nombres antes de conocer
el dominio.

#### Lo que se auditó decisión a decisión, y aguanta

Se revisaron todas las decisiones registradas de los trece ítems, las tomadas por el agente y las
consultadas al usuario. Estas parecían candidatas a replantearse y **no lo son**; queda escrito para
no volver a abrirlas:

| Decisión | Por qué se sostiene |
|---|---|
| **El contador de serie es una columna, no una `SEQUENCE`** | El §6 pide «secuencia dedicada con bloqueo», pero una `SEQUENCE` de PostgreSQL **no es transaccional** y deja huecos al hacer *rollback*, y R5 exige «correlativa y **sin huecos**». La regla se contradecía; ADR-0007 §5 lo resuelve. Anulación correcta. |
| **El *outbox* con cerrojo consultivo y un solo lector** | `FOR UPDATE SKIP LOCKED` se evaluó y se descartó **por escrito porque pierde el orden**, y R15 necesitará un consumidor serializado. Decisión de la 0.8 que ya miraba a la fase 5. |
| **No existe ningún `Activo`** | `grep "bool Activo" src/` → cero. R16 exige que `Bloqueo` no se confunda con borrado lógico, y no hay dónde confundirlos. |
| **La deriva del reloj (R15), aparcada** | ADR-0003 la aparca **con su mecanismo ya construido**: las sondas se agregan por etiqueta, así que añadirla es registrar una comprobación más. Aparcar con el enchufe puesto no es deuda. |
| **`Context` y no Zustand para la sesión** | No es desviación: `stacks/react/convenciones.md`:83 dice «con **Context** si cambia poco; Zustand si cambia a menudo». La sesión cambia al entrar y al cambiar de empresa. |
| **Sin EF Core InMemory; solo el *hasher* de ASP.NET Core Identity** | Ambas son las que manda `testing.md` y `herramientas/autenticacion.md`. Adoptar el resto de Identity habría traído su modelo de usuario, que R11 y R12 contradicen. |
| **`Notificaciones` sin carpeta, pero declarado** | `ElInventarioDeModulosTests` lo marca `Presencia.SinCarpeta` y **compara la lista entera** contra los dieciséis del §5. Ausente y declarado no es lo mismo que olvidado. |

### Tomadas por el agente de desarrollo — ítem 0.14 (2026-09-02)

- **Los diccionarios son módulos de TypeScript, no `.json`.** Es la decisión de la que cuelgan las
  demás. `es.ts` exporta el objeto **sin `as const`** —así `typeof es` tiene las claves literales y
  los valores como `string`— y de ahí sale el tipo `Diccionario`, que `en.ts` cumple entero. La
  consecuencia: **una clave que falte en un idioma no compila**. Con `.json` no habría comprobación
  ninguna; con `.json` y un test, la habría el día que alguien ejecutara el test. Medido con la
  mutación 4: añadir una clave solo al castellano da `TS2741` nombrando la que falta.

- **Un solo espacio de nombres, `traduccion`.** i18next admite repartir el diccionario por módulos y
  cargarlos por separado. Se descarta hoy: partirlo hace que `t()` deje de comprobarse contra UN
  tipo y permite que la misma clave viva en dos sitios, y el diccionario entero son unos pocos
  kilobytes. Cuando pese, se parte; no antes.

- **Fábrica `crearI18n()`, no la instancia global de `i18next`.** Exactamente el mismo motivo que
  `crearCache()` en el 0.11: la instancia global es estado compartido entre tests, y un test que
  cambia el idioma se lo dejaría cambiado al siguiente. `montarAplicacion` recibe además el idioma
  **explícito**; tomarlo del detectado ataría los tests al `navigator.language` de la máquina que
  los corre, y el mismo test pasaría en local y fallaría en la CI.

- **La tabla de rutas guarda `claveDeTitulo`, no `titulo`, y su tipo es
  `keyof Diccionario['rutas']`.** Es la doctrina del contraejemplo aplicada al frontal: no basta con
  que la clave se traduzca, hay que hacer que escribir una frase ahí **no compile**. El título se
  usa en tres sitios —`<title>`, el `<h1>` y el anuncio de `aria-live`—, así que un literal en la
  tabla no se quedaría sin traducir en uno: se quedaría sin traducir en los tres.

- **La capa de red deja de escribir frases y pasa a devolver MOTIVOS.** Es el cambio de forma del
  ítem, y no estaba previsto: `shared/api/errores.ts` y `shared/api/sesiones.ts` —que son red, no
  pantalla— escribían castellano y se lo daban pintado a los componentes. Con dos idiomas eso obliga
  a una de dos cosas malas: o la capa de red llama a `t()` —y entonces sabe de presentación, y
  además tiene que resolver el idioma fuera de React, donde no hay contexto—, o el texto sale
  siempre en el idioma en que se escribió. Ahora devuelven uniones exactas —`MotivoDeFallo`,
  `MotivoDeAcceso`, `MotivoDeCambioDeEmpresa`— y traduce quien pinta. Cada operación devuelve **su**
  unión y no una común: así la pantalla de acceso no tiene que contemplar «no se ha podido cambiar
  de empresa», que ahí no puede pasar, y el compilador la obliga a cubrir los dos que sí.

- **Los mensajes de Zod son claves.** `esquemaDeAcceso` es una constante de módulo: se evalúa una
  vez al importarlo, fuera de React y antes de que haya idioma. Una frase escrita ahí quedaría
  fijada en el idioma de ese instante para toda la vida de la pestaña y **no cambiaría al cambiar de
  idioma**. Guardando la clave, traduce el componente en cada pintada.

- **El `lang` del documento lo mantiene el motor, no el componente que cambia el idioma.**
  `crearI18n` se suscribe a `languageChanged` y lo actualiza ahí. Ponerlo en el selector sería un
  invariante que depende de que alguien se acuerde de llamarlo, y dejaría de ser cierto en cuanto el
  idioma se cambiara desde otro sitio. No es cosmético: **WCAG 3.1.1**, y de él dependen la voz que
  elige un lector de pantalla y la separación silábica. Es justo lo que no se ve en una captura.

- **La prohibición de texto suelto es una regla que se ejecuta: `i18next/no-literal-string`.** Un
  barrido escrito a mano habría sido una prohibición tecleada —lo que la doctrina del contraejemplo
  desaconseja— y habría que mantenerle la expresión regular. La regla va en modo `jsx-text-only` y
  cubre el texto de JSX; `className`, `id`, `to` y compañía quedan fuera adrede, porque incluirlos
  obligaría a una excepción por línea que acabaría apagando la regla de hecho. Corre en el paso
  «Linter» que ya existía, así que no añade tiempo a la CI.

- **La marca se excluye de la regla; no entra en el diccionario.** «Bastion» se escribe igual en los
  dos idiomas —lo dice el glosario y el Anexo A.1—, y meterla como clave sería invitar a que alguien
  la traduzca. Va en `words.exclude` con el motivo escrito al lado.

- **`<Trans>` solo donde el énfasis está DENTRO de la frase.** Las dos primeras líneas de la portada
  llevan `<strong>` en medio, y dónde cae el énfasis cambia con el idioma. La alternativa —partir la
  frase en tres cachos y concatenarlos— deja al traductor sin la frase entera y se rompe en cuanto
  un idioma pone el nombre al final. Por lo mismo hay dos claves enteras para «estás operando con
  X» —con y sin la coletilla del selector— en vez de una frase más un trozo cosido.

- **Sin `i18next-browser-languagedetector`.** La detección son veinte líneas: `localStorage`, si no
  `navigator.language`, si no castellano. Traer un paquete para eso es lo que `AGENTS.md` llama
  adoptar algo «porque siempre fue gratis». Y todos los accesos al depósito van en `try`:
  `localStorage` **lanza** —no devuelve `null`— en una ventana privada con las cookies bloqueadas, y
  una excepción ahí dejaría la aplicación sin arrancar por no poder leer una preferencia.

- **`MensajeDePantallaRota` sale a su propio fichero.** Un límite de error tiene que ser una clase
  —`getDerivedStateFromError` no existe en *hooks*— y una clase no puede llamar a `useTranslation`.
  Se sacó el mensaje a un componente de función, y eso encendió `react-refresh/only-export-components`
  —**0 avisos antes, 1 después**, medido—, porque el fichero pasaba a mezclar clase y función. Por
  eso vive aparte y no dentro.

#### Las cinco mutaciones, cada una aplicada, ejecutada y revertida

| # | Qué se rompió | Qué se puso rojo |
|---|---|---|
| 1 | Un `<p>` con texto escrito a mano en `PaginaNoEncontrada` | `npm run lint` — `i18next/no-literal-string`, nombrando el fichero y la línea |
| 2 | `crearI18n` deja de seguir a `languageChanged` para el `lang` | `ElCambioDeIdioma` — `expected 'es' to be 'en'` |
| 3 | El título de la ruta sale del diccionario castellano en vez de `t()` | `ElCambioDeIdioma` — dos casos: el encabezado no llega a «Warehouses» y el anuncio se queda en castellano |
| 4 | Una clave solo en `es.ts`, sin pareja en `en.ts` | **La compilación** — `TS2741`, nombrando la clave que falta |
| 5 | Cuatro textos de `en.ts` copiados del castellano | `ElCambioDeIdioma` — «Hay 5 textos idénticos en los dos idiomas», con la lista |

La 4 es la que más protege y la única que no es un test: el error llega antes de ejecutar nada.
La 5 es la que cubre el agujero que la 4 deja abierto —copiar el castellano cumple el tipo, cumple
la comparación de claves y deja media aplicación sin traducir—, y por eso compara **valores** y no
solo claves. Su umbral es «menos de 4 iguales» y no «ninguno»: `{{titulo}} · Bastion` es idéntico en
los dos idiomas legítimamente, y exigir cero obligaría a inventarle una diferencia.

#### Lo que se encontró por el camino y no estaba previsto

- **El frontal no pinta nunca `ProblemDetails.detail`.** Se comprobó antes de empezar, porque de
  eso dependía si la i18n tocaba también a la API: si los mensajes del servidor llegaran a pantalla,
  o la API tendría que traducir por `Accept-Language`, o devolver códigos. No hace falta ninguna de
  las dos: todo lo que lee una persona está escrito en el frontal. **Pero la decisión vuelve** el
  día que un error de validación se le enseñe al usuario con el texto del servidor, y ese día es la
  fase 1. Queda anotado como riesgo.

- **El presupuesto de tamaño se queda corto.** Medido a los dos lados: **490 kB antes, 554 kB
  después**, con el tope de la CI en **600 kB**. Los 64 kB son `i18next` más `react-i18next` más los
  dos diccionarios. Quedan 46 kB de margen y la fase 1 trae Terceros y Catálogo enteros: el tope hay
  que revisarlo, y con un número razonado, no subiéndolo cuando salte. Anotado como riesgo.

- **Los avisos de `act(...)` de los tests son de antes.** Salían 92 y seguían saliendo 92, contados a
  los dos lados con el mismo comando. No los trae la i18n, así que no se arreglan aquí; queda dicho
  para que el siguiente que los vea no los persiga en el sitio equivocado. **Corregido el 2026-09-05
  (ítem 1.5):** ese 92 —y el 91 que se anotó al cerrar el 1.4— salieron los dos de un registro
  **truncado**. La cifra reproducible es **109 en siete ficheros**, tres pasadas idénticas, y el
  comando está escrito en la nota de riesgo. Lo que la nota decía sigue en pie: no los trae la i18n.

### Tomadas por el agente de desarrollo — ítem 0.15 (2026-09-03)

- **Los cinco maestros nuevos son globales, y la marca es una entrada escrita.** `Impuesto`,
  `Divisa`, `TipoCambio`, `UnidadMedida` y `ConversionUM` no llevan `EmpresaId`. Es la R8 aplicada
  —«los maestros que se comparten entre sociedades se marcan explícitamente»—, y el §7 lo dice con
  su notación: a `Empresa`, `Ejercicio`, `Serie` y `Almacén` les pone «empresa» delante; a estos,
  no. La marca **no** es la ausencia de la interfaz, que no se distingue de un olvido: es la entrada
  con su motivo en la lista de globales del barrido de inquilinato, comparada en los dos sentidos.
  `Ubicacion` sí lo lleva, y propio, aunque su almacén ya tenga uno: el filtro de la R8 se evalúa
  sobre las columnas de la fila, y sin ella un listado que empezara por ubicaciones enseñaría las de
  otra empresa.

- **Un impuesto no se edita: se sucede.** El general pasó del 18 % al 21 % el 1 de septiembre de
  2012 y una factura de agosto lleva el 18 para siempre. `Modificar` no recibe ni el porcentaje ni
  el tramo, así que reescribir la facturación hacia atrás no es difícil: **no compila**. El código,
  por lo mismo, NO es único —hay una fila por tramo—; lo que no puede haber es solape.

- **Lo que un índice único no sabe decir lo dice un `EXCLUDE USING gist`.** Dos tramos del mismo
  código no pueden pisarse, y el rango va cerrado por los dos lados (`daterange(..., '[]')`), que es
  el que caza el solape de un solo día. La aplicación pregunta antes con `HaySolapeAsync` aunque la
  base ya lo prohíba: un 409 con su motivo es mejor respuesta que un 500, y el cierre lo vuelve a
  preguntar porque cerrar más tarde un tramo ya cerrado lo alarga.

- **La `Divisa` de la tabla y el catálogo de redondeo no se pueden separar.** `Crear` exige que el
  redondeo se conozca y los decimales son una propiedad CALCULADA, no una columna: cuántos tiene el
  euro es una regla fiscal, y en una columna cualquiera escribe un 3 y la facturación redondea mal
  sin que nada falle. El catálogo crece a cinco —el yen con cero decimales, que es el contraejemplo
  que impide «simplificar» esto a una constante 2— y **siguen quedando divisas fuera**, para que la
  puerta que rechaza lo desconocido tenga algo que rechazar.

- **Los decimales de una unidad de medida SÍ son columna, y el contraste está escrito.** No hay
  norma que consultar sobre a cuántos decimales se pesa. Lo que no se puede es **bajarlos** después:
  cada existencia ya registrada de 1,250 kg pasaría a ser un número que la propia unidad dice que no
  existe.

- **Tres controladores escriben su ruta en vez de heredarla.** El host publica las URL en minúsculas,
  así que `[controller]` daría `/tiposdecambio` en un contrato que después no se puede cambiar sin
  romper a quien ya lo use. Para eso se parte la constante en `Prefijo` y `RutaBase`.

- **Las semillas son ficheros de datos en `db/semillas/`, no código.** Un `.json` que se puede leer
  y revisar sin compilar nada, y que la CI enseña en un *diff* legible: un tipo de IVA que cambia es
  una línea en un fichero, no un despliegue de código. Se admiten **comentarios** (JSONC, con
  `ReadCommentHandling = Skip`) porque la mitad del valor del fichero es el porqué de cada fila —de
  dónde sale el 4 %, por qué las retenciones empiezan en 2016—, y ese porqué no cabe en ninguna
  columna. Que los comentarios se parsean está probado, no supuesto.

- **Qué entra en las semillas y qué no.** Entran los doce tramos de impuesto del régimen general
  —IVA general por sus tres saltos (16, 18, 21), reducido por los suyos, superreducido, exento y las
  cuatro retenciones— y quince unidades de medida. **No** entran el IGIC ni los recargos de
  equivalencia ni las conversiones entre unidades: los tres dependen de la instalación —dónde está
  la empresa, a qué régimen está acogida, qué cabe en su caja— y no del país, así que sembrarlos
  sería decidir por el usuario. El motivo está escrito en la cabecera de cada fichero, que es donde
  se lee cuando alguien va a añadir una fila.

- **Las carga el MIGRADOR, no el arranque de la API**, por lo mismo que las migraciones: con dos
  réplicas, dos procesos cargarían a la vez y el segundo se estrellaría contra el índice único. El
  migrador es un contenedor de un solo uso y su código de salida es la señal que mira el *compose*.

- **La carga es repetible y no borra.** El migrador corre en **cada** despliegue. Cada fila se busca
  por su identidad natural —el código en las unidades, el código **más** el primer día de vigencia
  en los impuestos, porque buscarlo solo por el código daría «ya está» a partir del segundo
  despliegue y dejaría fuera todos los tramos menos el primero— y solo se añade si falta. Lo que ya
  está no se toca: una instalación que renombró «Caja» a «Caja de 12» no quiere que el siguiente
  despliegue se lo devuelva. Y quitar una fila del `.json` **no** la borra: un impuesto sembrado
  puede llevar meses en las líneas de una factura (R16).

- **Un `SaveChanges` por fichero**, no uno por fila ni uno para todo: la granularidad es la del
  fallo. Si el `.json` de impuestos trae un tramo solapado, lo que hay que dejar fuera es ese
  fichero, no las unidades, que no tienen nada que ver.

- **El ámbito sin inquilino se abre con un motivo propio, `CargaDeMaestros`.** No se reutiliza
  `SemillaDeArranque` porque el motivo acaba en una columna de la traza y son dos hechos distintos
  —lo mismo que argumentó `PublicacionDeEventos` cuando le tocó—. Y el ámbito **no** está para poder
  consultar los maestros, que no llevan filtro: está para que el interceptor de auditoría pueda
  escribir el alta, porque sin empresa y sin ámbito **lanza**. Es el segundo camino de todo el
  sistema sin petición detrás, y está declarado con su cuenta en `ElFiltroNoSeSaltaPorAhiTests`.

- **La afirmación de conjunto no vacío se dice dos veces, y la segunda es la que cierra.** La
  primera mira el FICHERO —que llegó y que trae filas—; la segunda cuenta **en la base** después de
  guardar. Entre las dos hay sitio para el fallo mudo: un `SaveChanges` sobre el contexto equivocado
  devuelve cero filas sin quejarse, y sin el segundo recuento la carga saldría con 0 habiendo dejado
  el catálogo vacío. El registro se emite **siempre**, también cuando no se añade nada, porque un
  cargador silencioso no distingue «ya estaban» de «no he mirado».

- **F3 tenía dos mitades y solo se veía una.** Quitar `db/semillas` del `.dockerignore` deja que el
  contexto de construcción las copie, pero `Dockerfile.api` publica solo `/publicado`: sin
  `<Content Include ... CopyToPublishDirectory>` en el `.csproj` de Infrastructure no llegan al
  contenedor que las carga. El `Include` es un **glob** —una semilla nueva no se olvida de copiar—,
  y la lista de qué ficheros tienen que estar se escribe **a mano** en `SemillasDeOrganizacion`: si
  fuera también un glob, borrar un fichero dejaría el barrido comparando la nada consigo misma.

- **El hallazgo del ítem, y no estaba previsto: `[Range]` con límites de texto los lee en la cultura
  de la máquina.** `[Range(typeof(decimal), "0.000001", "1000000")]` convierte sus dos cadenas la
  primera vez que valida, con `CurrentCulture`. En es-ES el punto separa millares, y eso rompía las
  cuatro acciones de tipos de cambio y conversiones por dos sitios a la vez: al validar lanzaba
  `ArgumentException` **dentro** del enlazado de modelos —500 a toda petición con cuerpo, también a
  las correctas—, y al generar el contrato el mismo texto sí se leía, como `1`, así que
  `docs/api/openapi.json` llevaba publicado `"minimum": 1` para una tasa que admite un millonésimo.
  Lo encontró `LaPuertaDeCadaAccionTests`, que sondea todas las rutas; no lo habría encontrado
  ningún test de esas acciones, porque no había ninguno. La regla que lo impide,
  `LosLimitesSeLeenEnCulturaInvarianteTests`, **fija la cultura a mano**: el ejecutor de la CI corre
  en en-US, donde el punto sí es el separador decimal, así que un test que se conformara con la
  cultura del que lo ejecuta sería verde justo en la máquina que tiene que avisar.

#### Las tres mutaciones de la rebanada de semillas, aplicadas, ejecutadas y revertidas

| # | Qué se rompió | Qué se puso rojo |
|---|---|---|
| 1 | Se quita el `<ItemGroup Label="Semillas">` del `.csproj` | `LasSemillasLleganDondeSeCarganTests` — **5 de 12** casos, justo los que dependen de lo publicado; los 7 de carpeta temporal siguen verdes, que es como se sabe que el rojo señala lo que dice |
| 2 | Se invierte la condición de deduplicado de impuestos (no se añade ninguno) | Los **5** casos de `LaCargaDeSemillasTests`, y con el diagnóstico del propio cargador: «trae 12 filas y en la base hay 0» — o sea, lo caza la afirmación sobre la BASE, no el test |
| 3 | Se quita `ParseLimitsInInvariantCulture` de un atributo | Las **dos** reglas nuevas: `ArgumentException :: 0.000001 is not a valid value for Decimal`, y `CrearTipoCambioDto.Tasa` nombrado en la lista de límites a merced de la máquina |

La 2 es la que más importa: prueba que la guarda que se afirma es la del recuento **en la base**, y
no la del fichero, que en esa mutación seguía diciendo 12 tan contenta.

### Tomadas por el agente de desarrollo — ítem 0.16 (2026-09-03)

Cierra **F4** (`features/` no espejaba los módulos) y **F6** (`docs/dominio/` estaba vacío) de la
auditoría previa a la fase 1.

- **`features/` espeja los módulos, y un recurso es una subcarpeta.** `identidad/acceso/`,
  `organizacion/almacenes/`, `organizacion/empresas/`. **Dentro de una funcionalidad no hay
  frontera**: `almacenes` y `empresas` son dos recursos del mismo módulo y se ven entre sí. La
  frontera va de funcionalidad a funcionalidad, que es lo que dicen el §10 y la biblioteca.

- **`inicio` no era ninguna de las dos cosas, y se va al armazón.** `PaginaDeInicio` y
  `PaginaNoEncontrada` no son de ningún módulo del backend: no tienen recurso, ni permiso, ni
  contrato. Meterlas en una funcionalidad les presta un dueño que no tienen y ata la portada a un
  módulo que mañana puede desaparecer. Van a **`src/app/paginas/`**, junto a lo demás que se monta
  una vez para todas las pantallas.

- **La frontera la ejecuta ESLint, con `no-restricted-imports` del núcleo. Ningún paquete nuevo, y
  por tanto ninguna licencia nueva que comprobar.** Se descartó un plugin de fronteras: habría
  traído la misma vacuidad —sus capas también se declaran con globs— más una dependencia y una
  licencia, para hacer lo que la regla del núcleo hace con dos patrones.

- **La regla se genera del disco.** `eslint.config.js` lee las carpetas de `src/features/` y emite
  una configuración por funcionalidad. Así una funcionalidad nueva queda vallada **por existir**, y
  no porque alguien recuerde venir a añadir dos líneas.

- **Y revienta si encuentra menos de dos.** Con cero o una no hay frontera que vigilar: el bucle
  generaría cero reglas y `npm run lint` saldría verde sin prohibir nada. Es la afirmación de
  conjunto no vacío (ADR-0020) puesta en el único sitio donde ese fallo ocurre sin que se note.

- **La lista declarada la escribe una persona, y se compara entera contra el disco.**
  `src/features/funcionalidades.ts`. Una lista que se descubre sola no puede desmentir al disco: si
  el barrido se rompe o alguien renombra una carpeta, cambia con ella y todo sigue verde. Se compara
  en los dos sentidos — carpeta sin declarar, roja; declaración sin carpeta, roja.

- **Y la regla se comprueba POR EL EFECTO, no leyendo la configuración.** El barrido instancia
  ESLint y, para **cada par ordenado**, lintea un import prohibido —en las dos formas: alias y
  camino relativo— y exige que lo marque; y lintea tres que NO debe marcar (`@/shared/…`,
  `@/app/…` y la propia funcionalidad). Los pares se cuentan contra `n·(n−1)`. Leer la
  configuración habría comprobado que el fichero pone lo que pone.

- **Un barrido de fuentes aparte, porque `no-restricted-imports` no ve `import()` dinámico.**
  Resuelve todos los especificadores escritos bajo `src/features/` y comprueba a mano que ninguno
  cruza, con su propio recuento de especificadores mirados.

- **Dos patrones por funcionalidad prohibida, y no tres** — comprobado quitando cada uno. Los globs
  se leen con semántica de `.gitignore`, así que `@/features/otra` **a secas ya cubre lo que
  cuelga**: la cola explícita sobraba. El segundo, el ancho, es el que atrapa
  `../../../otra/loQueSea.ts`, donde la palabra `features` ni aparece. Su precio —que una carpeta de
  `shared/` o `app/` llamada como una funcionalidad quedaría prohibida sin querer— es una
  comprobación más del barrido, no una nota.

- **Lo que la regla NO prohíbe, a propósito: lo que cuelga de `shared/` y de `app/`.**
  `organizacion` necesita saber con qué empresa se opera, y eso vive en `shared/sesion/`. Si la
  regla obligara a bajar la sesión dentro de `identidad`, la regla estaría mal, no la estructura. El
  único cruce al armazón que hay hoy es `PaginaDeAcceso.tsx` importando `type { Diccionario }` de
  `@/app/i18n/es.ts`: un tipo, del diccionario, que es del armazón por definición.

- **Los espacios de nombres de los diccionarios son las funcionalidades que hay en disco**, lista
  entera y en los dos sentidos, en `es.ts` y en `en.ts`. Mover carpetas sin mover los espacios de
  nombres dejaría los diccionarios describiendo una estructura que ya no existe, y **TypeScript no
  diría nada, porque una clave es una cadena**.

- **Toda ruta declara de quién es su pantalla**, y el barrido de rutas saca del `cargar` qué módulo
  importa de verdad y comprueba que vive donde su dueño dice. Sin eso, mover una pantalla del
  armazón a dentro de una funcionalidad es un `git mv` y dos imports que compilan, lintan y pasan.

- **Del glosario, solo una lista deja de ser prosa: la de agregados.** Se compara contra el dominio
  compilado, entera, en los dos sentidos, y con el módulo de cada uno. Es la que más caro sale
  desactualizada, porque es la que dice qué cosas hay. El resto —objetos de valor, entidades hijas,
  conceptos— **sigue siendo prosa, y el fichero lo dice de sí mismo**: clasificar un objeto de valor
  no es algo que la reflexión lea sin una lista de excepciones a mano, y esa lista convertiría una
  comprobación en una convención con pasos extra.

- **El glosario no se estrenó: ya existía (`428331f`) y se había quedado atrás.** Reservaba para el
  0.15 seis términos que el 0.15 construyó, y avisaba de un choque de nombres —«Divisa» sería el
  código que acompaña al importe **y** el agregado de Organización— que hay que contar resuelto. Se
  fusionó: no se ha perdido ni un término de los que había, y la columna «y qué **no** es» se queda,
  que es la mitad que hace útil un glosario.

#### Lo que decidió el glosario y se implementa en la FASE 1 (ADR-0023)

Escribir la definición de cuatro términos obligó a decidir tres cosas que no lo estaban. **Están
decididas y NO implementadas**: hoy no protegerían nada, porque no hay todavía una sola operación
transaccional que apunte a una divisa o a una unidad.

1. **Los cuatro maestros de instalación tendrán una retirada.** `Divisa`, `TipoCambio`,
   `UnidadMedida` y `ConversionUM` se crean y se editan y no se puede hacer nada más con ellos: en
   el contrato tienen `GET`/`POST` y `GET`/`PUT`, y **ni `DELETE`, ni `/cierre`, ni `/desbloqueo`**
   —al contrario que `Impuesto`, que sí tiene `/cierre`—. Y `Modificar` es estrecho: solo el nombre
   en `Divisa` y `UnidadMedida`, solo la tasa en `TipoCambio`, solo el factor en `ConversionUM`. Un
   **código** mal escrito, unos **decimales** equivocados o un **par o una fecha** que no eran son
   permanentes, y como son maestros de instalación se ven **desde todas las empresas**. La salida se
   llamará **retirada**: ni bloqueo —el bloqueo es el artículo 32 de la LOPDGDD y habla de datos
   personales, que una divisa no tiene— ni cierre —el cierre es el final de una línea temporal, y
   una unidad no se sucede—. Lo retirado no se ofrece para lo nuevo y **sigue resolviendo** para lo
   que ya apunta a ello. Ningún `DELETE`, nunca.
2. **Si la inversa de una conversión está declarada, tiene que ser la inversa.** Los dos sentidos
   son filas independientes a propósito, pero nada acotaba cuánto podían discrepar: hoy conviven
   `caja→unidad = 12` y `unidad→caja = 0,5` sin un solo error. Se exige
   `|f·g − 1| ≤ 5·10⁻⁷·(f + g)`, que **no es una tolerancia inventada**: es exactamente la que
   impone guardar cada factor redondeado a seis decimales. Admite las dos lecturas razonables de
   1/12 y rechaza 0,5 por seis órdenes de magnitud. Va en la **capa de aplicación**, porque
   relaciona dos instancias del agregado y la R12 dice una transacción, un agregado.
3. **Una conversión encadenada no compone.** Con `kg→g` y `g→mg` pero sin `kg→mg`, se responde un
   **error de negocio con nombre**, nunca un cero, nunca un nulo, nunca el producto. Encadenar
   multiplica el error de redondeo, y con varias cadenas posibles el número dependería de cuál
   eligiera el buscador de caminos: una entrada invisible en un dato de negocio.

#### Las higienes del cierre, y lo que la medición corrigió

- **`CA1304`, `CA1305` y `CA1310` no había que activarlas: ya rompían el build.** Comprobado por el
  efecto con una sonda de tres líneas —`ToUpper()`, `int.ToString()`, `StartsWith(string)`—, que da
  cuatro errores (también `CA1311`). Lo que sí faltaba es de quién dependía: rompían por venir
  dentro de `AnalysisLevel=latest-recommended`, un lote que Microsoft recompone en cada SDK — con
  `latest-default`, la misma sonda da **cero**. Escritas en `.editorconfig`, la garantía la pide
  este repositorio y no un lote. `CA1307` no salta ni forzando la categoría entera: no hay nada que
  encender ahí, y decirlo es parte del resultado.
- **La licencia se escribe donde se lee la decisión, y nunca como última línea del commit.** Una
  línea final con forma `Clave: valor` **es un trailer** —`git log --format='%(trailers)'` la
  devuelve—, y esa forma tiene que quedar vacía para que sirva de comprobación de que no se ha
  colado ninguno de sesión, de herramienta ni de terceros. Los dos commits del 0.14 (`2398889`,
  `309f594`) la llevan así; están publicados y **no se reescriben**. El dato se movió a su sitio
  —i18next y react-i18next (MIT) a la cabecera del motor de traducción, eslint-plugin-i18next (ISC)
  junto a su regla— y la convención quedó escrita en `AGENTS.md`.

#### Lo que se encontró por el camino y no estaba previsto

**El renombrado hizo que una clave de traducción tuviera la forma exacta de un permiso.** Al pasar
los espacios de nombres del diccionario a espejar los módulos, `organizacion.almacenes.tabla` quedó
indistinguible de `organizacion.almacen.ver` para `LosPermisosQueNombraElFrontalTests`, que reconoce
un permiso por «minúsculas con puntos y primer segmento de módulo». **Quince claves entraron como
permisos que la API no sirve**, y el caso se puso rojo en el carril de integración — no en el
frontal, que estaba verde entero.

Importa cómo falló: **rojo, no cero ficheros**. El barrido tiene dos anclas —hay candidatos, y el
fichero que los declara aporta alguno— y por eso el renombrado lo rompió en vez de vaciarlo. Es el
mismo modo de fallo que los siete casos de carpeta temporal del 0.15, resuelto al revés y a tiempo.

La corrección no toca los espacios de nombres, que son la decisión del ítem: **la distinción no es
de forma, es de sitio**. Un literal que el código le entrega al traductor no es un permiso. Se resta
por ocurrencia y no por valor —la misma cadena en otro sitio se sigue comprobando— y es una resta,
así que un permiso escrito en cualquier otro lugar sigue entrando.

**Con `CI=true`, ESLint linteaba el fichero del disco y no el texto inventado — y el barrido por el
efecto se quedaba sin nada que comprobar.** El frontal salió rojo en la CI tres veces con la máquina
de desarrollo verde entera. La causa no era el sistema operativo, ni la versión de Node, ni la de
ESLint, ni la del paquete `ignore`, ni la semántica de los globs, ni el orden de los pasos del
*workflow*: se descartaron las seis. Era que **`typescript-eslint` mira `process.env.CI`** y, si
vale `'true'`, deduce «ejecución única» y no monta el programa de TypeScript en modo vigilancia —
sin el cual `lintText(codigo, { filePath })` analiza **el fichero del disco** en lugar del texto que
se le pasa. El fichero de disco está limpio, así que ESLint no marcaba nada.

Se reprodujo en local con `CI=true` en una sola orden. Arreglo: el motor de este barrido se
construye con `parserOptions.disallowAutomaticSingleRunInference: true`, en la instancia y no en el
entorno, porque `npm run lint` sí debe aprovechar la ejecución única. Y con él, **dos preguntas de
control por testigo**, que son la comprobación por el efecto aplicada a sí misma: que ESLint marque
un `any` descarado en ese fichero (el canario), y que el motor prohíba el mismo import con una
configuración inline que no lleva otra cosa. La primera que falle nombra la capa rota.

**Salió rojo y no verde por la polaridad de la aserción**, y eso no es suerte: el barrido exige que
la lista de avisos **no** esté vacía, así que un ESLint que no mira nada falla. Escrito al revés
—comprobando que lo permitido no se marca— la ejecución única habría dado verde en la CI para
siempre. Está en el ADR-0022.

#### Las mutaciones, cada una aplicada, ejecutada y revertida

| # | Qué se rompió | Qué se puso rojo |
|---|---|---|
| 1 | Un import de `@/features/organizacion/…` dentro de `identidad/acceso/model/` | **Dos veces**: `npm run lint` código 1 con el mensaje de la regla, y el barrido de imports nombrando fichero y destino |
| 2a | El `files:` de la regla apuntando a una carpeta que no existe, **sin tocar ningún import** | **`lint` código 0. Verde total.** Solo el caso que lintea de verdad: «identidad puede importar de organizacion con el alias, y no debería». El barrido de imports siguió verde, y es correcto: ningún import cruzó |
| 2b | Quitar el patrón ancho, dejando solo el del alias | `lint` código 0; rojo **solo** en el caso del camino relativo. El alias se seguía cazando → los globs de `.gitignore` ya cubren los descendientes, y la cola explícita sobraba |
| 3 | Renombrar `features/organizacion/` sin tocar el diccionario | **Dos veces**: lista declarada contra disco, y partición de espacios de nombres del diccionario |
| 4 | Borrar del glosario un término que sí existe (`Serie`) | «Sobran en el glosario: []. Faltan: [Serie]». Y atribuyéndolo a otro módulo: «Serie: el glosario dice «Identidad» y vive en Organizacion» |
| 5 | Mover `PaginaDeInicio` del armazón a dentro de una funcionalidad | `typecheck` 0, `lint` 0, fronteras verde; rojo **solo** en el barrido de rutas |
| 6 | `organizacion.almacen.inventar` en `permisos.ts`, tras arreglar el reconocedor de permisos | `LosPermisosQueNombraElFrontalTests` → rojo, nombrando fichero y valor: la resta no se comió el barrido |
| 7 | El canario convertido en algo que ESLint no marca (`export const x = 1;`) | «ESLint no ha marcado un 'any' descarado en el testigo de identidad: no está mirando ese fichero» |
| 8 | El patrón del motor inline apuntando a `@/nada/…` | «el motor de ESLint no prohíbe '@/features/organizacion/…' ni con una configuración que no lleva otra cosa» |
| 2a *bis* | La 2a otra vez, **con `CI=true`**, que es el modo en el que la CI ejecuta | Rojo, y nombrando la capa: «la configuración que le aplica **NO LLEVA** no-restricted-imports». Antes del arreglo, ese mismo modo dejaba el barrido sin nada que mirar |

**La 2a es el hallazgo del ítem.** Una regla de ESLint cuyo patrón no case con nada **pasa**: ni un
aviso, ni un «0 ficheros comprobados». Y la mitad que suele olvidarse es que la comprobación que
mira el código **no puede** detectarlo, porque el código está bien; y la que mira la regla no puede
detectar un import prohibido, porque la regla ya no existe. Hacen falta las dos.

**La 5 tiene lectura.** La frontera de ESLint no la caza, y no es un fallo: meter una pantalla del
armazón dentro de una funcionalidad no cruza ninguna frontera **entre funcionalidades**; le presta
un dueño que no tiene. Es otra regla, y por eso hay otra regla.


#### El traspaso a la fase 1

**Dónde retomar exactamente: la fase 0 está cerrada; lo siguiente es la FASE 1 (Maestros).** El
0.16 era el último ítem con criterio escrito de antemano. La fase 1 **no tiene Anexo A.3**, así que
**antes de tocar nada pasa por la puerta de clarificación**: se leen el objetivo, este PLAN y las
convenciones, se identifica toda decisión esencial sin especificar que admita varias opciones
viables, y se preguntan **todas juntas, en una sola tanda**, sin empezar a trabajar hasta tener
respuesta.

**Lo que hereda, y que ya no hay que decidir:**

- **Tres cosas decididas y sin implementar, que son suyas** (ADR-0023): la **retirada** de los
  cuatro maestros de instalación —con el primer módulo que los referencie, no antes—, la
  comprobación de que la inversa de una conversión es la inversa, y el resolutor de conversiones que
  responde un error con nombre en vez de componer una cadena.
- **El corte de `features/`**: espeja los módulos del backend. Terceros es **un solo agregado** con
  roles (§7.2), así que es **una sola funcionalidad** `terceros/` con los recursos que haga falta
  dentro — no `clientes/` y `proveedores/`, que era justo lo que F4 vio venir.
- **El glosario**, que ya no se puede quedar atrás en su parte más cara: un agregado nuevo sin
  entrada en `docs/dominio/glosario.md` pone el carril de arquitectura en rojo el día que se
  escribe. Los términos que la fase 1 estrene se añaden **cuando existan**, y la sección
  *Reservado* dice cuáles están ya con dueño: Tercero, Artículo, Categoría, Tarifa.

**Los imports del Anexo A.2.3 que se añaden a `CLAUDE.md` al empezar** (y se quedan):

```
@../BibliotecaDocumentacion/herramientas/proteccion-datos.md
@../BibliotecaDocumentacion/patrones/soft-delete.md
```

`soft-delete.md` no es opcional aquí: la **retirada** del ADR-0023 es un final de vida que **no** es
el bloqueo de la R16, y las dos van a convivir en las mismas tablas.

**Los invariantes de la fase 0 que ahora sostienen peso**, y que romper sale caro:

- **Ninguna regla sin afirmación de conjunto no vacío** (ADR-0020, y ADR-0022 para el frontal). Son
  siete barridos en el backend y dos en el frontal; `LasReglasDeEsteCarrilTests` los cuenta por su
  nombre, así que añadir o quitar una regla obliga a escribir la línea.
- **Toda regla nueva se ve en rojo antes de aceptarse en verde.** No es estilo: es el único modo de
  distinguir una regla que protege de una que se lee bien.
- **El inventario de módulos manda el alcance.** Cuando la fase 1 estrene `Terceros.Domain` con su
  primer tipo, hay que declararlo en `Inventario.EnsambladosConTipos` — si no, sus fronteras salen
  verdes por vacuidad, y el informe dirá que se comprobaron.
- **El contrato se genera y se versiona**, y el cliente del frontal se genera de él: nada de
  `docs/api/openapi.json` ni de `src/shared/api/esquema.ts` se escribe a mano.
- **Los permisos que teclea el frontal los sirve la API**, y desde el 0.16 el reconocedor distingue
  una clave de traducción de un permiso por el **sitio**, no por la forma. Un módulo nuevo entra en
  ese barrido el día que publica su primer permiso, sin tocar nada.
- **`[Range]` con límites de texto se lee en cultura invariante**, y la sensibilidad a la cultura la
  exige `.editorconfig` y no un lote de analizadores.

### Tomadas por el agente de desarrollo — puerta de clarificación de la fase 1 (2026-09-03)

> **Doce preguntas, planteadas todas juntas y contestadas todas juntas**, como manda el `CLAUDE.md`
> §2. No se escribió una línea de código antes de tenerlas resueltas, y ese es el punto: la fase 1
> **no tiene Anexo A.3**. El §15 da el criterio de la fase entera —«alta de cliente/proveedor con NIF
> validado y de artículo con unidad, impuesto y tarifa; listados paginados y filtrados en servidor;
> dominio cubierto por tests»— y nada más. El desglose en ítems es una **decisión**, no un literal
> que copiar, y por eso vive aquí.
>
> **Tres cosas no se preguntaron**, porque el plan maestro ya las decide y reabrirlas sería tocar el
> §7:
>
> - **El modelo de tarifa** lo fija el §7.3: `Tarifa` con vigencia y divisa; `LineaTarifa` por
>   artículo **o** categoría, con precio **o** descuento, escalado por cantidad. Lo que el §7.3 **no**
>   fija —precedencia, exclusividad, solape, ausencia— sí se decidió, y está en P5.
> - **La unicidad del identificador fiscal** la fija el §7.2: «su identidad es única por CIF **y**
>   empresa». Dentro de una empresa no se repite; entre empresas sí. Lo que quedaba abierto está
>   en P4.
> - **`Tercero` es un solo agregado con roles** (§7.2), así que es **una** carpeta `terceros/` y no
>   `clientes/` y `proveedores/`.

#### P1 · El desglose en ítems — once, con dos ajustes sobre lo propuesto

**Decidido:** once ítems, cada uno con criterio verificable, en el orden de la tabla de más abajo.
Dos cambios sobre la propuesta inicial del agente, los dos del usuario y los dos con el mismo
motivo —**decidir barato hoy en vez de urgente mañana**—:

1. **El presupuesto del frontal pasa de último a primero.** El propio P11 demuestra que la métrica
   ya mide mal **hoy**, y que la fase 1 va a añadir pantallas diferidas que engordan el número sin
   empeorar el arranque. Dejarlo al final significa que salta en mitad de Catálogo, con una pantalla
   a medias, y ahí es cuando un tope se sube por prisa — que es exactamente lo que la nota abierta
   del riesgo dice que no se haga.
2. **El contrato de listado absorbe el P12.** Se está tocando ese contrato y están a punto de nacer
   dos módulos más: es el momento más barato que va a existir para consolidar los tipos de
   paginación. Después son cuatro copias.

**Por qué el cruce mutuo es ítem propio.** Del §7 salen dos dependencias en sentidos opuestos:
`Tercero.TarifaAsignada` mira a Catálogo y `ArticuloProveedor` mira a Terceros. Ninguno de los dos
módulos puede hacerse entero primero, así que la mutualidad se hace **visible** en un ítem en vez de
colarse dentro de otro.

**§15 respetado:** la fase termina desplegable y no hay dos fases abiertas a la vez. Nada de la fase
2 en adelante entra aquí — en particular `CodigoBarras`, que arrastra
`negocio/identificacion-articulos/convenciones.md`, y ese import es de la **fase 2**.

#### P2 · La cuarta vía en rojo — regla por reflexión, con dos fuentes

**El problema.** De los cuatro caminos por los que Catálogo puede tocar a Organización, tres se
ponen rojos solos: una clave foránea entre esquemas la rechaza el SQL, tocar `Organizacion.Domain`
lo rechaza el compilador, y referenciar `Organizacion.Contracts` sin declarar el cruce lo rechaza
`LasFronterasEntreModulosTests`. El cuarto —**guardar `unidad_id` sin validarlo contra nadie**— sale
**verde en todas partes**. Es el camino equivocado que no avisa.

**Decidido:** una regla nueva del carril de arquitectura, con **dos fuentes independientes
comparadas enteras y en los dos sentidos**, que es el patrón de `funcionalidades.ts` y el de
`Inventario`:

- **La lista declarada a mano** de las propiedades que son identificadores de otro módulo, con su
  dueño. Entra por aquí lo que el nombre no delata.
- **El descubrimiento por reflexión** sobre el dominio compilado: todo `Guid` de un agregado cuyo
  nombre case con un agregado de otro módulo. Entra por aquí lo que nadie declare.

Y para cada entrada, la exigencia: **tiene que existir un cruce declarado hacia el `Contracts` de
ese módulo**.

**Por qué dos fuentes y no una.** La heurística de nombres sola tiene un agujero que aparece en el
primer caso real: el §7.3 le da al artículo una **unidad base**, y ese campo se llamará
`UnidadBaseId`, no `UnidadMedidaId`. No casa, la regla calla, y el agujero queda abierto justo donde
se abrió para cerrarlo. La lista declarada sola tiene el agujero simétrico: un `DivisaId` que nadie
apunte no existe para la regla. Cada fuente tapa el fallo de la otra, y compararlas enteras es lo
que impide que una envejezca en silencio.

**Mutación que la valida:** añadir `DivisaId` a un agregado que no tenga puerto, y ver el rojo.

**Lo que se descarta, y por qué.** Declarar la línea de `s_crucesDeclarados` **antes** que el código
—para que el rojo llegue el día que Catálogo compile sin usar el puerto— tiene un precio que no se
puede pagar: la comparación es **entera y en los dos sentidos**, así que una línea declarada sin
código que la ejerza deja el carril **en rojo desde que se escribe**. Eso choca de frente con «cada
commit deja el árbol verde» (`AGENTS.md`). El puerto, su primer consumidor y su línea de cruce van
en el **mismo commit**; lo que obliga al siguiente agregado no es el orden de escritura, es la regla
de P2.

#### P3 · El cruce mutuo Terceros ↔ Catálogo — los dos, declarados, en ítem propio

**Decidido:** se aceptan los dos cruces, cada uno por el `Contracts` del módulo dueño y declarado en
`s_crucesDeclarados`, en el ítem 1.10.

No es un ciclo de proyectos —`Contracts` no arrastra el interior de su módulo, así que compila—,
pero sí una dependencia mutua entre módulos, y por eso se hace visible en vez de repartirla.

**Descartado el evento de integración** para uno de los dos sentidos: son **lecturas**, no
reacciones a un cambio, y el §4 ya dice que las lecturas van por la interfaz del `Contracts` del
dueño resuelta en proceso. Un evento pagaría coste de bandeja, de idempotencia y de orden a cambio
de nada.

#### P4 · Qué valida «NIF validado»

**Decidido:** **NIF, NIE y CIF con su letra de control**, validados de verdad en el objeto de valor.
El identificador extranjero se guarda como **identificador opaco con su país**, sin validar y
marcado como tal. **Nada de VIES en el camino del alta**, pero con su hueco previsto: el campo de
estado —*sin verificar · verificado · rechazado*, con su fecha— **existe desde el primer día**, para
que la comprobación diferida sea un caso de uso nuevo y no una migración sobre datos.

**Por qué no VIES ahora.** Es un servicio externo con caída y con latencia: meterlo en el camino del
alta convierte un formulario en una integración, y la primera indisponibilidad ajena impide dar de
alta un cliente. Y el dato que de verdad decide el IVA es el **territorio fiscal**, que el §7.2 ya
hace campo propio y que VIES no aporta.

**Las dos coletillas:**

- **Tercero sin identificador fiscal: sí, con el rol restringido.** Puede ser cliente de contado; no
  puede ser proveedor ni entrar en factura hasta que lo tenga. La regla vive en el dominio, no en el
  formulario.
- **NIF de un tercero bloqueado: son DOS caminos, no uno, y el matiz es del usuario.** Resolverlo con
  el ítem 1.4 no basta: quien da un alta normalmente **no** tendrá el permiso de ver lo bloqueado, y
  un mensaje que diga «está bloqueado» **publica la existencia de un dato del art. 32 a alguien sin
  derecho a saberlo**. Así que:
  - **el alta** devuelve un conflicto que **no revela** —«no se puede dar de alta con ese
    identificador; consulta con administración»—, sin confirmar ni desmentir que exista una fila;
  - **el administrador**, con su permiso y por el camino del 1.4, sí la encuentra y puede levantarla.

  Va escrito así en el **ADR-0027**, porque es la clase de detalle que se decide una vez y se copia
  mal diez veces.

#### P5 · Lo que el §7.3 no fija de las tarifas — cinco decisiones

1. **Precedencia: artículo > categoría más cercana > … > raíz.** No basta con «gana la más
   específica»: como la categoría es **jerárquica con padre** (ver *Lo que decide el agente*), un
   artículo puede casar con una línea de su categoría **y** con otra de la categoría padre, y las
   dos son «por categoría». Sin la precedencia completa, el desempate lo decide **el orden en que
   salgan las filas**, que es un no-determinismo silencioso. El caso que hay que probar es
   exactamente ese: **dos líneas de categoría a distinta profundidad**.
2. **Precio o descuento, excluyentes en el propio objeto de valor.** Los dos juntos son dos maneras
   de decir lo mismo con un orden de aplicación invisible.
3. **Solape de vigencias prohibido con restricción de exclusión**, igual que los tramos de impuesto
   del 0.15. La máquina ya sabe hacerlo; dejarlo a la aplicación es dejarlo a una carrera.
4. **Sin tarifa aplicable: error de negocio con nombre, nunca precio cero.** Mismo criterio que la
   decisión 3 del ADR-0023.
5. **`Tercero.TarifaAsignada` entra en la fase 1**, dentro del ítem de cruces (1.10). Sin él la
   tarifa por cliente no existe y el criterio del §15 se queda a medias.

#### P6 · Hasta dónde llega Terceros en esta fase — la raya entre maestro y movimiento

El §7.2 cuelga seis cosas del tercero. Tres son de fase 1 sin discusión (`Direccion`, `Contacto`,
régimen fiscal). Las otras tres tienen dueño más adelante:

| | Fase 1 | Su fase |
|---|---|---|
| `CuentaBancaria` (IBAN, BIC) | dato maestro del tercero | — |
| `MandatoSEPA` | — | **6 · Tesorería**, con las remesas |
| `CondicionPago` | plazos y tope legal de 60 días | — |
| `LimiteCredito` | el **importe** | el **riesgo vivo** y el bloqueo por impago son **4 · Ventas** |

**El criterio:** en la fase 1 entra lo que es **maestro**; lo que necesita **movimientos** para
significar algo entra con el módulo que los genera. Meter `MandatoSEPA` ahora es escribir una
máquina de estados —incluida la baja automática a los 36 meses— que **nada ejercita**: es la
vacuidad del ADR-0020 en forma de tabla.

#### P7 · Cuándo entra la retirada del ADR-0023 — ítem propio, antes de Catálogo

**Decidido:** ítem 1.7, propio, **antes** del primer ítem de Catálogo, con sus dos hermanos —la
tolerancia de la conversión inversa y el resolutor de conversiones encadenadas con su error con
nombre—.

El disparador escrito en el ADR-0023 es «con el primer módulo que los referencie, no antes», y ese
momento es Catálogo usando `UnidadMedida`. Metido **dentro** del ítem de Catálogo, el criterio de
ese ítem dejaría de ser verificable de un vistazo: son cambios de contrato en cuatro recursos de
Organización más una regla de aplicación. Y dejarlo **para el final** significa que Catálogo nace
pudiendo apuntar a una unidad que ya nadie debería ofrecer.

#### P8 · El NIF no viaja en la cadena de consulta

`herramientas/api-rest.md` es explícito —«un dato sensible no viaja en la cadena de consulta»— y
`proteccion-datos.md` entra con esta fase. Hoy los listados solo llevan `?page=&size=` y `PaginaDe`
no devuelve enlaces, así que la decisión se toma limpia y no hay que deshacer nada.

**Decidido:** la búsqueda va por **`POST .../buscar` con el criterio en el cuerpo**, la paginación
en el cuerpo también, y la respuesta devuelve un **cursor opaco** en lugar de una URL que
reintroduzca el criterio. **La comprobación no termina en la entrada:** el enlace a la página
siguiente que fabrica el servidor tampoco puede llevar el criterio dentro.

**Descartado** mitigar en el proxy (no registrar la cadena de consulta): confía la protección a un
componente que no vive en este repositorio, y el historial del navegador y la analítica del cliente
siguen ahí. **Descartado** partir el listado en dos formas según el filtro: la asimetría que nadie
recuerda seis meses después.

**El efecto secundario, y su exención.** Un `POST` que no crea nada choca con la lectura ingenua de
REST; queda documentado en el **ADR-0025**. Y tiene una consecuencia concreta sobre lo construido:
`TodaEscrituraDiceComoSeProtegeTests` reparte las acciones **por el verbo**, así que
`POST .../buscar` cae del lado de las que cambian estado y el barrido pedirá `If-Match` o
`Idempotency-Key` para una búsqueda. Necesita **su exención con motivo escrito** en `s_exentas`, que
ya se compara entera en los dos sentidos. Es una línea, y se escribe **con el endpoint**, no cuando
el carril se ponga rojo: la tentación entonces sería ensanchar la partición, que es peor.

#### P9 · La importación CSV — aislamiento por fila, idempotencia por fichero, y el hash

**Unidad de aislamiento: la fila.** Es literalmente lo que pide `principios/manejo-errores.md`: en
un bucle que procesa muchos elementos, el fallo de uno no puede ser el fallo de la vuelta. Un CSV de
tres mil filas de un cliente real no puede convertirse en un «no» sin diagnóstico.

**Idempotencia (R10): una importación es UNA operación.** Con `Idempotency-Key`, el reenvío del
mismo fichero devuelve **el resultado guardado** sin reimportar. **Descartado** hacerla N
operaciones —una por fila con clave derivada—: multiplicaría por tres mil las filas de
`auditoria.claves_de_idempotencia`, que **ya crece sin política de retención y es riesgo abierto**.

**El segundo cerrojo, que es lo que hace segura a la decisión anterior:** se guarda el **hash del
contenido** junto al resultado. Misma clave con **fichero distinto** es un **error explícito**, no
el informe equivocado devuelto con cara de acierto.

**En el criterio del ítem:** con aislamiento por fila, el informe dice **qué filas fallaron, por qué
y en qué línea** — es lo único que permite corregir y reimportar solo esas. Y esa reimportación es
**fichero distinto → hash distinto → clave nueva**: coherente, y dicho en voz alta para que nadie lo
lea como una fuga de la idempotencia.

#### P10 · El camino de lectura de lo bloqueado — ítem propio, con dos correcciones de hecho

**Sí lo necesita el desglose, y no por comodidad.** Sin él, el alta de un tercero cuyo NIF está
bloqueado choca contra un índice único sobre una fila **invisible**, y el mensaje sería «duplicado»
señalando a nada (ver P4).

**Dos correcciones al análisis previo, comprobadas en el código:**

1. **El motivo ya existe.** `MotivoParaVerLoBloqueado.AdministracionDelBloqueo` está construido en
   `src/BuildingBlocks/Application/Bloqueos/IAccesoALoBloqueado.cs` y en uso. **No hay que crearlo.**
2. **Son cuatro sitios, no tres.** `s_aperturasDeBloqueoPermitidas` lista `DesbloquearAlmacen`,
   `DesbloquearEmpresa`, `AdministracionDeUsuarios` y `BloquearUbicacion`. Terceros sería el
   **quinto**. Importa porque esa lista **se compara entera y en los dos sentidos**: escribir «tres»
   ahí es escribir un rojo.

Así que el ítem **no construye el motivo**: construye el **camino de listado** —una consulta que
enseñe lo bloqueado a un rol nominativo y trazado—, con su permiso y su ADR.

**Y lo que el `proteccion-datos.md` añade y hoy no existe:** el art. 32 exige que el bloqueo tenga
**fecha de vencimiento y proceso de destrucción**. No entra en el ítem —es materia de retención, y
la fase 1 no tiene con qué decidir el plazo— pero **entra como riesgo anotado con su fecha**, no en
silencio.

#### P11 · El presupuesto del frontal — dos topes, y el criterio que ya estaba escrito

**Lo medido hoy:** `dist` son **554 kB** de un tope de 600. Pero el desglose enseña que el `dist`
está **partido en fragmentos y las rutas se cargan tarde**: `index` 380 kB, `schemas` (zod) 84,
`PaginaDeAcceso` 36, css 12, `Paginacion` 12. El paso de la CI hace
`du -sk --exclude='*.map' dist`, o sea que **suma también los fragmentos que el navegador no
descarga al arrancar**. Su propio comentario dice que mide «lo que el navegador descarga», y con
carga diferida **eso dejó de ser cierto**.

**La consecuencia es al revés de como parece:** la fase 1 va a añadir pantallas **diferidas**, que
engordan el número sin empeorar el arranque, y el presupuesto saltaría por algo que **no es el
problema que existe para vigilar**.

**Decidido: dos topes, con el cálculo escrito.**

- **Arranque ≤ 450 kB** — el fragmento de entrada más sus importaciones estáticas y el CSS. Hoy son
  ≈400 kB; en una 4G de 1,6 Mbps efectivos, ~2 s de descarga, dentro del segundo y medio a dos y
  medio que se considera aceptable para el primer render útil. Margen corto sobre lo medido, que es
  la regla que ya dejó escrita el 0.10.
- **Total ≤ 900 kB** — holgado a propósito: vigila el crecimiento global sin castigar el troceo.

**Y lo importante, que es de dónde sale el criterio y no el número:** el propio paso de la CI ya
excluye los `.map` con este razonamiento escrito —«no se descarga al arrancar, así que contarlo
medía otra cosa distinta de la que dice esta frase»—. Ese argumento vale **palabra por palabra**
para los fragmentos diferidos. Esto **no cambia el criterio: lo aplica donde dejó de aplicarse
solo.** Así queda en el **ADR-0028**.

#### P12 · Los tipos de paginación se consolidan — y la premisa de la pregunta era falsa

La pregunta se planteó como «¿seguimos copiando o tocamos el §4?». **Comprobado en el código, no hay
§4 que tocar.**

**Uno.** `Contracts` **ya** referencia el núcleo común, y solo en un módulo:

```
Bastion.Organizacion.Contracts.csproj  ->  Bastion.BuildingBlocks.Domain.csproj
Bastion.Identidad.Contracts.csproj     ->  (ninguna)
Bastion.Auditoria.Contracts.csproj     ->  (ninguna)
```

O sea que hoy **no hay regla**: hay una **incoherencia entre dos módulos**, con la puerta abierta y
uno solo entrando por ella.

**Dos.** La regla de capas no dice lo que la pregunta suponía. `LasCapasVanHaciaDentroTests`
prohíbe que `Contracts` arrastre **el interior de su propio módulo**, y el motivo está escrito ahí
mismo: «si el contrato arrastrase el `Domain`, cualquier módulo que lo referenciara —que es lo que la
regla 1 le PERMITE hacer— acabaría viendo el dominio ajeno por transitividad». **El bloque común no
es interior de nadie**: todos pueden verlo, así que la transitividad que justifica la regla **no
existe en este caso**.

**Tres.** Dos de los cuatro tipos ni siquiera están en `Contracts`: `ConsultaPaginada` vive en
`Endpoints` y `Paginador` en `Infrastructure`, y las dos capas **ya** referencian
`BuildingBlocks.Infrastructure`. Para esos dos no hay ni discusión.

**Cuatro.** Son duplicados de verdad, no parientes: `diff` entre los dos `Paginacion.cs` devuelve
**una sola línea**, la del `namespace`; los dos `PaginaDe.cs`, lo mismo.

**Con eso, seguir copiando deja de ser la opción barata:** sería institucionalizar **dieciséis
ficheros idénticos** —cuatro tipos por cuatro módulos— más un barrido que los vigile para siempre,
contra un principio que la biblioteca declara sin matices. Un barrido que compara copias no es una
defensa: es la cuota anual de una deuda que no hacía falta contraer.

**Decidido, y cómo:**

- **`Bastion.BuildingBlocks.Contracts`**, nuevo y mínimo, con `Paginacion` y `PaginaDe`. Es su sitio
  semántico: son tipos **de contrato**, no de dominio.
- **`ConsultaPaginada` y `Paginador`** se van a los comunes que sus capas ya referencian.
- **Identidad y Organización pasan a usarlos y sus copias se borran.** Que digan lo mismo **por
  construcción**, no por vigilancia.

**Aviso previsto:** `Inventario.ComunesConTipos` declara tres ensamblados comunes con un comentario
que dice que «son tres y **no van a crecer con las fases**». El cuarto lo pondrá **rojo**, y hay que
actualizar la línea. Eso es el mecanismo funcionando como se diseñó — y la ocasión de corregir el
comentario, que resultó ser **una predicción y no una regla**.

#### Lo que decide el agente, por trivial o reversible

- **Categoría jerárquica con padre y comprobación de ciclos**, no ruta materializada: la ruta
  optimiza consultas que todavía no existen. Es la que obliga a completar la precedencia de P5.
- **Idioma del tercero** restringido a los del diccionario del frontal.
- **Orden por omisión** de cada listado, declarado en el caso de uso y no heredado del motor.
- **Nombres de permisos** siguiendo `modulo.recurso.accion`, como el catálogo ya existente.
- **Dónde vive cada barrido nuevo**, siguiendo el carril que ya le corresponde por lo que lee.

#### Lo que se encontró al comprobar las decisiones contra el código

Cuatro cosas que no estaban en la tanda y que cambian el trabajo. Las dos primeras son **averías
mudas** que la fase 1 activa; las dos últimas, consecuencias del P12.

1. **El ítem 1.4 caduca dos exenciones del ADR-0017, y ellas mismas lo dicen.** Las exenciones de
   `EmpresasController.Desbloquear`, `AlmacenesController.Desbloquear`,
   `UbicacionesController.Desbloquear` y `UsuariosController.Desbloquear` llevan escrita **la
   condición de la que dependen**: «DEPENDE DE que el filtro `Bloqueo` siga tapando la empresa en
   **TODA lectura que llegue por la API**: el día que un endpoint abra `ViendoLoBloqueado(...)` y
   devuelva una empresa bloqueada **con su ETag**, la llave vuelve a existir, esta exención caduca y
   hay que volver a exigir `If-Match` aquí». El ítem 1.4 hace exactamente eso.

   **La cláusula tiene dos mitades y solo una se rompe si el camino es de listado.** Un **listado**
   de lo bloqueado rompe la primera mitad —ya no es «toda lectura»— pero **no entrega ETag**, que es
   lo que resucita la llave; un **`GET` individual** rompe las dos y obliga a devolver el `If-Match`
   a los cuatro desbloqueos. **Decidido: el 1.4 construye SOLO el listado**, que es además lo único
   que hace falta —del listado sale el identificador, y el desbloqueo no pide etiqueta—. Y **entra en
   el criterio del ítem** reescribir la mitad rota de las cuatro cláusulas: una condición que ha
   dejado de ser cierta y sigue ahí es una exención que parece razonada y no lo es.

2. **`TodaEscrituraDiceComoSeProtegeTests` fija sus ensamblados a mano, y la fase 1 lo deja ciego.**
   Su `Todas()` enumera `typeof(ControladorDeOrganizacion).Assembly` y
   `typeof(ControladorDeIdentidad).Assembly`, tecleados. El día que exista `Terceros.Endpoints`, el
   barrido **dejará de mirarlo sin ponerse rojo y sin cambiar de color** — un controlador nuevo
   entero sin `If-Match`, sin `Idempotency-Key` y sin exención, y el informe diciendo que se
   comprobó. Es **la misma avería** que se cerró en el 0.13 para `UnidadDeTrabajoPorModuloTests`, y
   es hoy la última lista de ensamblados tecleada del proyecto.

   **Decidido:** se arregla con la receta ya aceptada aquí —descubrir los ensamblados que arrastra
   `Bastion.Api` filtrando `Bastion.*.Endpoints`, y comparar la lista entera en las dos
   direcciones—, y **va en el 1.3**, o sea **antes** de que exista el primer controlador nuevo. Se
   arregla cuando cuesta una tarde, no cuando ya escondió algo.

3. **`Inventario.ComunesConTipos` se pondrá rojo con el cuarto común**, como estaba previsto (P12).

4. **`Paginador.cs` de Organización arrastra un resto de edición en su comentario de documentación**
   —una etiqueta suelta dentro de una palabra—, que compila porque es XML válido. No se arregla por
   separado: ese fichero **desaparece** con la consolidación del P12.

#### Lo que faltó en la tanda, y por tanto no se supone

**El octavo tema que el usuario puso sobre la mesa —«quién escribe el texto que lee una persona
cuando falla una validación»— se cayó de la tanda al redactarla.** No se contestó porque no se
preguntó, así que **no se hereda un default**: queda como decisión abierta con fecha de caducidad.

- **Dónde muerde:** en el **1.5**, que es el primer ítem con una validación que apetece enseñar con
  el texto del servidor (un NIF con la letra mal). Los ítems 1.1 a 1.4 no la tocan.
- **Las opciones:** que la API traduzca por `Accept-Language`, o que devuelva un `type` estable y lo
  traduzca el frontal.
- **Lo que ya está escrito** en *Notas / riesgos* desde el 0.14: «la respuesta por defecto es la
  segunda, porque mantiene la API sin saber de presentación; **pero se decide, no se hereda**». Y
  encaja con lo construido —el `type` de `ProblemDetails` **ya es contrato**—, pero eso es un
  argumento, no una respuesta.
- **Se pregunta antes de empezar el 1.5**, y sale de ahí un ADR si la respuesta es de arquitectura.

#### El desglose de la fase 1, definitivo

> Once ítems. Entre paréntesis, el número que tenía en la propuesta inicial, para que no se pierda
> nada al leer el historial de esta decisión.

| # | Ítem | Criterio de aceptación |
|---|---|---|
| **1.1** | El presupuesto del frontal, remedido *(era 1.11)* | Dos métricas —arranque frente a total— con topes **450 / 900 kB** razonados con el cálculo escrito; la CI mide **lo que dice medir**, comprobado con la cifra de antes y la de después |
| **1.2** | Los puertos de lectura de Organización, y la cuarta vía en rojo *(1.1)* | `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e `IConsultaDeDivisas` en `Organizacion.Contracts`; el cruce declarado en el mismo commit que su primer consumidor; la **regla de dos fuentes** de P2 vista en rojo con su mutación (`DivisaId` sin puerto) |
| **1.3** | El contrato de listado, y los tipos de paginación consolidados *(1.2 + P12)* | Filtro y orden en servidor con tope; búsqueda por cuerpo, **sin dato sensible en la URL ni en el enlace a la página siguiente**, con su exención escrita en `s_exentas`; `BuildingBlocks.Contracts` con `Paginacion` y `PaginaDe`, `ConsultaPaginada` y `Paginador` en sus comunes, las ocho copias borradas; y `TodaEscrituraDiceComoSeProtegeTests` **descubriendo** sus ensamblados |
| **1.4** | El camino de lectura de lo bloqueado *(1.3)* | **Listado** —no `GET` individual— de lo bloqueado, con su permiso y su ADR; `s_aperturasDeBloqueoPermitidas` comparada entera con **cinco** sitios; y la mitad caducada de las cuatro cláusulas «DEPENDE DE» del ADR-0017 reescrita |
| **1.5** | Terceros: el agregado y su identidad *(1.4)* | Alta con **NIF/NIE/CIF validado de verdad**, extranjero opaco con país y estado de verificación; dirección estructurada (R17); roles; unicidad por (empresa, NIF); el alta contra un bloqueado devuelve un conflicto **que no revela**; listado paginado y filtrado; `features/terceros/`. Y el **ADR-0030**, movido aquí desde el 1.6 en el ítem 1.3: el artefacto de `type` y el barrido, **antes** que el primer texto que los use |
| **1.6** | Terceros: lo que cuelga *(1.5)* | `Contacto`, `CuentaBancaria` con IBAN validado, `CondicionPago` con el tope de 60 días **contado desde la entrega**, `LimiteCredito` (solo el importe) — la raya de P6 |
| **1.7** | La retirada y las dos conversiones *(1.6)* | ADR-0023 implementado entero: retirada en los cuatro maestros de instalación, tolerancia de la conversión inversa, resolutor de encadenadas con **error con nombre**. Antes de que Catálogo los referencie |
| **1.8** | Catálogo: artículo y categoría *(1.7)* | Alta de artículo con unidad e impuesto **validados por los puertos del 1.2**; categoría **jerárquica con ciclos comprobados**; listado paginado y filtrado; `features/catalogo/` |
| **1.9** | Tarifas *(1.8)* | Vigencia y divisa; línea por artículo o categoría; escalado por cantidad; **la precedencia completa de P5 probada con dos líneas de categoría a distinta profundidad**; solape prohibido por restricción de exclusión; sin tarifa aplicable, error con nombre |
| **1.10** | Los dos cruces mutuos *(1.9)* | `ArticuloProveedor` y `Tercero.TarifaAsignada`, cada uno por el `Contracts` del dueño y **declarado** |
| **1.11** | Importación CSV *(1.10)* | Aislamiento **por fila**; idempotencia **por fichero** con `Idempotency-Key` **y hash del contenido** —misma clave con fichero distinto, error explícito—; informe con **línea y motivo** de cada fila rechazada |

**Los ADR que salen de esta puerta**, numerados del 0024 en adelante: la cuarta vía y su regla de dos
fuentes (**0024**), la búsqueda sin dato sensible en la URL (**0025**), la idempotencia de la
importación con el hash (**0026**), la lectura de lo bloqueado y los dos caminos del art. 32
(**0027**), el presupuesto de arranque frente a total (**0028**) y la consolidación de los tipos de
paginación (**0029**) — este último deja escrito **por qué la regla de `Contracts` no era la que
parecía**, porque es la corrección de una creencia y no solo una decisión.


### Tomadas por el agente de desarrollo — ítem 1.2 (2026-09-03)

**1. Los tres puertos contestan un `EstadoDeMaestro`, no un `bool`.** El ADR-0023 dice que una fila
retirada «no se ofrece para operaciones nuevas, pero **sigue resolviendo** para lo que ya apunta a
ella». Eso son **dos preguntas**, y un puerto que solo conteste una deja un agujero en cualquiera de
los dos sentidos: contestando solo «existe» se dan de alta artículos con unidades retiradas;
contestando solo «se puede usar» el histórico se queda sin poder resolver la unidad de un albarán de
hace tres años. `EstadoDeMaestro` tiene `NoExiste = 0`, `SeOfreceParaLoNuevo` y
`SoloResuelveLoViejo`. **Se fija ahora aunque la retirada sea del 1.7**, porque el consumidor es
Catálogo en el **1.8**: descubrir el estado que falta entonces sería cambiar el contrato con una
pantalla a medias. El cero es `NoExiste` a propósito — un `default(EstadoDeMaestro)` que llegue por
un camino que no pasó por el puerto no autoriza nada.

**2. El de impuestos lleva la fecha de devengo como parámetro; los otros dos no.** Un impuesto no
«existe» o «no existe»: rige **entre dos fechas**. El IVA general al 18 % dejó de regir el 31 de
agosto de 2012 y sigue teniendo que resolver una rectificativa de 2011. Preguntar «¿vale este
impuesto?» sin decir para qué fecha no tiene respuesta, así que la fecha va en la firma. Divisa y
unidad no tienen tramos: hoy contestan dos de los tres valores, y el tercero llega con el 1.7.

**3. Los tres maestros no son bloqueables, y está comprobado, no supuesto.** `Impuesto`, `Divisa` y
`UnidadMedida` son `: EntidadBase` y nada más — ni `IBloqueable`, ni filtro de empresa. Importaba
comprobarlo antes de escribir los puertos: si alguno lo fuera, el puerto heredaría el 404 de la R16
sobre lo bloqueado y el consumidor **no podría distinguir «no existe» de «no puedes verlo»**. Lo
prueba `Los_tres_puertos_contestan_sin_empresa_activa_y_sin_ver_lo_bloqueado`, que los ejerce con el
inquilino y el acceso que **lanzan en cuanto alguien les pregunta**: que contesten sin excepción es
la prueba, y el día que alguien les ponga un filtro el test revienta diciendo cuál de los dos ha
sido.

**4. La regla de dos fuentes NO es una igualdad, y aplicarla como tal saldría roja el primer día.**
El descubrimiento por reflexión **infradetecta por diseño**: encuentra un identificador ajeno cuando
el nombre de la propiedad casa con el del módulo dueño, y hay uno que existe desde el 0.5 y no casa
— `TokenDeRefresco.EmpresaActivaId`, que apunta a Organización. (No es un agujero: `ConstructorDeSesion`
y `RenovarSesion` lo comprueban contra el selector que sale de `IConsultaDeEmpresas`. Es la **prueba
viva** de por qué hace falta la lista declarada.) Así que la regla son **cinco afirmaciones** y
ninguna es una igualdad:

| # | Afirmación | Qué agujero tapa |
|---|---|---|
| 1 | Todo lo **descubierto** está en la lista, con su puerto | El identificador ajeno con nombre que casa y nadie declaró |
| 2 | Toda **declaración** sigue correspondiendo a una propiedad del dominio | La entrada que sobrevive al borrado de su propiedad y engorda un inventario muerto |
| 3 | Las **dos fuentes** encuentran algo | La vacuidad: un patrón de descubrimiento roto, o una lista vaciada |
| 4 | Cada módulo de la lista tiene su **cruce declarado y su puerto**, y el puerto existe y es del dueño | La mitad que convierte la regla en protección y no en inventario |
| 5 | **Ningún** `Guid …Id` del dominio se queda sin clasificar | El identificador ajeno con **nombre que no casa** y sin declarar |

**La quinta no estaba en el enunciado y es la que decide si el ADR-0024 protege o solo describe.**
Se comprobó por mutación: un `DivisaPreferidaId` en un agregado de Identidad —cruzado, con nombre que
no casa, sin declarar— lo caza **exactamente un test**, el quinto; los otros veintidós pasan. Con las
cuatro afirmaciones del enunciado esa mutación se habría quedado **verde**.

**5. Con dos entradas en la lista y una en el descubrimiento, la regla se ve trabajar hoy.** No hace
falta esperar a Catálogo. Los sujetos que hay son reales: `Membresia.EmpresaId` (que el
descubrimiento sí encuentra) y `TokenDeRefresco.EmpresaActivaId` (que no), más siete identificadores
del **mismo** módulo que la quinta afirmación obliga a clasificar. Una regla cuyo primer sujeto
llegara en el 1.8 sería una regla que nadie ha visto trabajar.

**6. El texto que lee una persona cuando falla una validación lo escribe el frontal** — la decisión
que quedaba abierta de la puerta de clarificación, contestada: se mapea el **`type` estable** del
`ProblemDetails`, y la API **no** negocia idioma por `Accept-Language`. El motivo, las tres cosas que
obliga a montar y las tres alternativas descartadas están en el **ADR-0030**; los criterios que
genera están en el **ítem 1.6** del checklist.

> **Una nota de orden sobre esto último, resuelta en el ítem 1.3 (2026-09-04).** El criterio se
> escribió en el **1.6** siguiendo la instrucción y **se ha movido al 1.5**, con el mecanismo —(a) el
> artefacto de `type` generado y versionado y (b) el barrido comparándolo contra los diccionarios—
> **antes** que el primer texto que lo use.
>
> El motivo no es que el 1.5 traiga la primera pantalla, que también: es que **el catálogo de `type`
> no está vacío hoy**. `PoliticaDeErrores.BaseDeTipos` y `ErrorDeOperacion.Codigo` ya componen
> `/errors/{codigo}` en los dos módulos montados, así que el artefacto y el barrido tienen sujetos
> desde el primer día y se pueden ver en rojo — que es la condición que el ADR-0020 le pone a
> cualquier regla nueva. Escrito en el 1.6, el 1.5 habría llenado `features/terceros/` de textos a
> mano y el 1.6 habría sido una reescritura, no una implementación; y el barrido habría nacido con
> las excepciones de esos textos ya dentro.
>
> Queda anotado aquí para que dentro de tres ítems nadie lo lea como un descuido: **no es que el 1.6
> perdiera un criterio, es que el 1.5 lo necesita antes.**


### Tomadas por el agente de desarrollo — ítem 1.3 (2026-09-04)

**1. Paginar y buscar son DOS parejas de tipos, no una.** `Paginacion`/`PaginaDe<T>` para el listado
ordinario; `Recorrido`/`TramoDe<T>` para la búsqueda. La alternativa era un solo `PaginaDe` que
llevara número, total y cursor — y entonces `Total = 0` en cada búsqueda y `CursorSiguiente = null`
en cada listado, o sea **la mitad de los campos vacíos en cada respuesta**. Un cliente no puede
distinguir «vacío porque esta forma no lo usa» de «vacío porque no hay», así que ese tipo miente
siempre y obliga a documentar fuera del contrato cuál de sus mitades vale. Dos tipos con nombres
que dicen lo que son cuestan un fichero más y no mienten ninguna vez. `TramoDe<T>` **no lleva
total**, y tampoco por pereza: contar un conjunto filtrado cuesta un recorrido entero en cada tramo,
que es justo lo que un cursor viene a evitar; el listado sí lo lleva porque su total es el de la
tabla y sale barato (§9, «con el total cuando es barato calcularlo»).

**2. La lista de campos por los que se deja ordenar tiene UN dueño, y no hay regla que la compare
con nada.** Se descartó lo que parecía natural —los nombres declarados en `Contracts` y el mapa a
columnas en `Infrastructure`—: son dos listas, divergen, y entonces hace falta una tercera cosa que
las compare. Lo que hay es un solo diccionario, `CriteriosDe<T>.Ordenables`, en el repositorio, y
**sus claves SON la lista blanca**. Sube al borde por el tipo: `IOrdenaPor` → `IListado<TDto>` →
`ConsultaPaginada.APaginacion(camposOrdenables)`, que devuelve `400` con los campos admitidos
nombrados. Un `?sort=` que el borde no rechace y llegue al paginador **lanza**, porque a esas
alturas es un error de programación y no una entrada (ADR-0004).

**3. Las claves de orden son `LambdaExpression` y no `Expression<Func<T, object>>`.** Con `object`,
el compilador inserta un `Convert` en el árbol, y ese `Convert` no revienta al compilar: revienta en
ejecución y **solo** sobre las propiedades con conversor de valor —`Usuario.Correo`, `Empresa.Nif`—,
o sea que la pantalla que lo estrena da un 500 y las demás no. El precio es un `(Expression<Func<T,
K>>)` explícito por entrada del diccionario, y está escrito al lado.

**4. El universo de `Todas()` se descubre; no se escribe.** Sale de
`IActionDescriptorCollectionProvider` —la tabla de enrutado del host, no una reconstrucción suya por
reflexión—, así que un módulo entra en el barrido **en el momento en que se monta**, que es el mismo
momento en que sus acciones empiezan a atender peticiones. Y lleva una segunda fuente que no se
deriva de la primera: los ensamblados `Bastion.<Modulo>.Endpoints` **del disco** con controladores
dentro. Se comparan **enteras**, y la igualdad es legítima porque las dos describen el mismo
conjunto: un módulo con controladores es un módulo que se monta. Añadir un tercer `typeof` habría
arreglado hoy lo mismo que se rompería con Catálogo.

**5. El barrido del criterio sensible mira el explorador de API, y reconoce los listados por lo que
DEVUELVEN.** El explorador es la misma fuente de la que sale `docs/api/openapi.json` y por tanto el
cliente del frontal; una reconstrucción por reflexión que mirase `[FromQuery]` **se saltaría** los
parámetros que en un `GET` se enlazan desde la URL por convenio, que es exactamente la fuga que la
regla existe para ver. Y los listados se reconocen por su tipo de respuesta —`PaginaDe<>` o
`TramoDe<>`— y no por que el método se llame `Listar`: la heurística por nombre es la que el ítem 1.2
tumbó, y se le escapa el primero que se llame `Consultar` **en silencio**. La comparación con los
doce campos sensibles es **por contención y sin distinguir mayúsculas**, para que `nifDelCliente`
cuente igual que `nif`; y la lista de los cuatro parámetros que hoy aceptan los listados
—`page`, `q`, `size`, `sort`— se compara **entera**, de modo que uno nuevo obliga a decidirlo donde
están escritos los motivos.

**6. `POST /api/v1/organizacion/empresas/buscar` nace con su exención escrita**, no cuando el carril
se pone rojo. La partición de `TodaEscrituraDiceComoSeProtegeTests` reparte **por el verbo**, así que
una búsqueda por `POST` cae del lado de las escrituras y el barrido le pide `If-Match` o
`Idempotency-Key`. Lo que **no** se hace es ensanchar la partición: es correcta y no tiene falsos
negativos, y aflojarla para acomodar un caso cambia una lista de excepciones razonadas por una regla
que ya no distingue (ADR-0025). La línea está en `s_exentas` con su cláusula «DEPENDE DE»: el día que
una búsqueda apunte algo —un registro de lo buscado, una lista de recientes—, la exención caduca.

**7. El cursor lleva posición y nada más, y el recorrido va por el identificador.** El `Id` es un
GUID v7: sus primeros bits son el instante de creación, así que ascendente por identificador **es**
por antigüedad de alta —un orden con sentido, no un capricho— y además es único, con lo que el
«después de esto» no necesita desempate ni comparación de tuplas y cae sobre la clave primaria. Se
leen `tamaño + 1` filas para distinguir «no hay más» de «hay más», sin contar el conjunto entero.
«Opaco» aquí significa que el cliente no lo construye ni lo interpreta —lo que permite cambiar la
clave del recorrido sin romper a nadie—, no que esté cifrado: dentro va la posición del último
elemento que el cliente ya tiene delante. **El criterio no entra en el cursor**: lo reenvía el
cliente en el cuerpo del `POST` siguiente. Si entrara, el NIF volvería al cliente en una cadena que
se copia y se comparte, que es la fuga del ADR-0025 entrando por la puerta de al lado.

**8. Que `Contracts` vea el bloque común no es tocar el §4.** La regla de capas prohíbe que un
`Contracts` arrastre el **interior de su propio módulo**; el núcleo común lo ven ya todas las capas
de todos los módulos. Está argumentado en el **ADR-0029** y se comprueba en
`LasFronterasEntreModulosTests.Las_referencias_de_proyecto_son_las_declaradas`, donde las dos aristas
nuevas —`Identidad.Contracts -> BuildingBlocks.Contracts` y `Organizacion.Contracts ->
BuildingBlocks.Contracts`— están declaradas.

**9. El comentario de `Inventario.ComunesConTipos` era una predicción, no una regla.** Decía que los
comunes «no van a crecer con las fases»; la fase 1 los ha hecho crecer en su tercer ítem. Se ha
reescrito para que diga lo que de verdad afirma —qué comunes hay y que todos llevan tipos— y el
nombre del test perdió el número: `El_bloque_comun_tiene_sus_tres_capas…` pasó a
`El_bloque_comun_tiene_las_capas_declaradas…`, porque **un número dentro de un nombre es una
afirmación que nadie comprueba**.

**10. El carril de integración se ejerce en local en la parte que no necesita el carril.**
`LaTraduccionASqlTests` vive en `Organizacion.IntegrationTests` **sin** el rasgo `Integracion`, y es
la única excepción declarada de ese proyecto —su cabecera lo dice—: generar el SQL necesita el
proveedor de Npgsql y el modelo, no un servidor, y ese proyecto es el único que ve los repositorios.
Descubre los diez listados del módulo por el **tipo** de su campo de criterios —no por el nombre del
campo— y traduce el orden de omisión, cada campo ordenable en los dos sentidos y el filtro. Lleva su
pregunta de control —la consulta que entra en el `.Valor` de un valor convertido tiene que
romperse— porque si `ToQueryString` dejara de traducir, el silencio no diría nada. La búsqueda se
sondea aparte y por el efecto: se ejecuta contra un sitio donde no hay nadie y se afirma **de qué**
se queja, mirando si en la cadena de excepciones hay una `DbException` —el tipo, no el mensaje, que
habría dependido del idioma del ejecutor—, con su contraria en el mismo test.

**11. Ninguna dependencia nueva.** El fichero de bloqueo suma **un** proyecto —
`Bastion.BuildingBlocks.Contracts`, `"type": "Project"`— y **cero** entradas `Direct` o
`Transitive`. No hay licencias que comprobar porque no hay paquete que comprobar.

**12. Lo que este ítem NO toca**, dicho para que no se lea como un olvido: ni Terceros, ni Catálogo,
ni tarifas, ni la retirada del ADR-0023. El camino de lectura de lo bloqueado es el **1.4**. El
artefacto de `type` del ADR-0030 es el **1.5** (ver la nota de orden del ítem 1.2, resuelta arriba).

#### Lo que este ítem deja abierto y no arregla

`LasReglasDeEsteCarrilTests` es el **censo de Arquitectura.Tests y solo de ese ensamblado**: se
descubre con `typeof(LasReglasDeEsteCarrilTests).Assembly`. Sus reglas siguen siendo **23** y lo
único que cambió allí fue la línea del test renombrado. Las afirmaciones nuevas de este ítem —tres en
`Api.FunctionalTests` y tres en `Organizacion.IntegrationTests`— viven en carriles **sin censo**, así
que borrar una de ellas dejaría la suite verde, más rápida y con una regla menos que nadie echa de
menos. Es la misma rendija que `LasReglasDeEsteCarrilTests` tapó para su carril en el 0.12, abierta
en otros dos. **No se cierra aquí** porque no está en el criterio del 1.3 y el checklist no se amplía
por iniciativa propia; queda anotado como candidato, con su motivo, para quien decida el alcance del
siguiente ítem.

#### Las cinco mutaciones del 1.3, cada una aplicada, ejecutada y revertida

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | Un controlador de un módulo montado **fuera** del universo que la lista escrita a mano nombraba (`POST /api/v1/terceros/fichas`, sin protección) | **Rojo ×4** | `Toda_accion_que_cambia_estado_dice_como_se_protege`, `El_universo_cubre_a_todos_los_modulos_montados`, `El_barrido_encuentra_el_inventario_entero` y `Toda_accion_o_exige_un_permiso_o_esta_en_la_lista_de_excepciones` |
| 2 | Un `[FromQuery(Name = "nif")]` en el listado de empresas | **Rojo ×2** | `Ningun_listado_recibe_un_criterio_sensible_por_la_url` (con el motivo del campo impreso) y `El_barrido_ve_los_listados_y_sus_parametros` |
| 3 | El descubrimiento del barrido nuevo apuntando a un tipo que ningún listado devuelve | **Rojo ×1** | Solo `El_barrido_ve_los_listados_y_sus_parametros`. La regla de al lado salió **verde recorriendo una lista vacía**, que es exactamente lo que la afirmación de conjunto no vacío existe para cazar (ADR-0020) |
| 4 | Divergir una copia consolidada: `TamanioMaximo = 5000` en un módulo | **Imposible ×3** — no rojo | El compilador, tres veces. Ver abajo |
| 5 | Quitar la exención de `EmpresasController.Buscar` de `s_exentas` | **Rojo ×2** | `Toda_accion_que_cambia_estado_dice_como_se_protege` (nombrando la acción) y `El_barrido_encuentra_el_inventario_entero` (15 esperadas, 14 encontradas) |

#### La primera, entera

Es la que decide si el arreglo es estructural o aplazado, así que se hizo con su contraste. Se añadió
un controlador **montado y enrutado** que vive fuera de `Bastion.Organizacion.Endpoints` y de
`Bastion.Identidad.Endpoints` —los dos ensamblados que el universo escrito a mano nombraba—, con una
escritura sin `If-Match` ni `Idempotency-Key`:

```csharp
[ApiController]
[Route("api/v1/terceros/fichas")]
public sealed class MutacionUnoControladorDeTerceros : ControllerBase
{
    [HttpPost]
    public IActionResult Crear() => Ok();
}
```

Con el universo descubierto, **cuatro rojos**, y cada uno dice algo distinto:

```
sinDecirlo    should be empty but had 1 item and was ["MutacionUnoControladorDeTerceros.Crear"]
sinDeclarar   should be empty but had 1 item and was ["MutacionUnoControladorDeTerceros.Crear"]
enrutados     should be ["identidad", "organizacion"]
              but was    ["identidad", "organizacion", "terceros"]
todas.Count   should be 74 but was 75
```

Después, **con la mutación puesta**, se restauró el universo escrito a mano —los dos `typeof`, sobre
la misma tabla de enrutado— y se volvió a ejecutar solo ese fichero:

```
Correctas! - Con error: 0, Superado: 7, Omitido: 0, Total: 7
```

**Siete verdes con un `POST` sin proteger atendiendo peticiones.** Eso es lo que quedaba en pie: no
que faltara un `typeof`, sino que el barrido no tenía forma de enterarse de que faltaba. Las dos
mitades —el controlador y el universo viejo— se revirtieron y el fichero volvió a 121 de 121.

#### La cuarta, entera — y no sale roja porque no llega a existir

El enunciado pedía comprobar que divergir una copia consolidada **es imposible, no rojo**. Se
intentaron las tres formas que hay, de la más ingenua a la única que de verdad importaría.

**Forma literal: cambiar `TamanioMaximo` en un módulo.** No hay dónde. En todo el repositorio la
constante se **declara una vez**:

```
src/BuildingBlocks/Contracts/Paginacion/Paginacion.cs:31:    public const int TamanioMaximo = 200;
```

y sus dos usos —`ConsultaPaginada` y `BuscarEmpresasDto`— resuelven a esa. La edición no tiene
objetivo.

**Forma 1: recrear la copia en `Bastion.Organizacion.Contracts.Comun` con 5000.** El compilador la
rechaza, y no por una regla que alguien escribió:

```
error CS0104: 'Paginacion' es una referencia ambigua entre
  'Bastion.Organizacion.Contracts.Comun.Paginacion' y
  'Bastion.BuildingBlocks.Contracts.Paginacion.Paginacion'
```

**Forma 2: divergir el borde.** Una `ConsultaPaginadaDeOrganizacion` con tope 5000, enlazada en
`EmpresasController.Listar`:

```
error CS1503: Argumento 1: no se puede convertir de
  'Bastion.Organizacion.Endpoints.Comun.ConsultaPaginadaDeOrganizacion' a
  'Bastion.BuildingBlocks.Infrastructure.Listados.ConsultaPaginada'
```

**Forma 3: saltarse el ayudante común.** Una `Paginacion` propia en un espacio de nombres que no
importa nadie —para que exista sin ambigüedad— pasada **directamente** al caso de uso, sin pasar por
`ResponderListadoAsync`:

```
error CS1503: Argumento 1: no se puede convertir de
  'Bastion.Organizacion.Endpoints.Paginado.Paginacion' a
  'Bastion.BuildingBlocks.Contracts.Paginacion.Paginacion'
```

Las tres son **errores de compilación**, no pruebas en rojo. Y el motivo es el mismo en las tres, y
es lo que distingue consolidar de copiar a un sitio nuevo: el tipo que atraviesa el camino de
listado está fijado por `IListado<TDto>.EjecutarAsync(Paginacion, …)` y por
`ConsultaPaginada.APaginacion(...)`, así que **una copia divergente no tiene por dónde entrar**.
Puede existir como fichero muerto en un rincón que nadie importe; lo que no puede es llegar a un
listado. Que es la diferencia que se pedía comprobar.


### Tomadas por el agente de desarrollo — ítem 1.4 (2026-09-04)

**1. El listado reutiliza el contrato del 1.3 y no estrena un segundo idioma de listado.**
`IListarLoBloqueado : IListado<BloqueadoDto>`, `CriteriosDe<RecursoBloqueado>` con sus ordenables
(`tipo`, `codigo`, `nombre`, `fecha`, por omisión `fecha` descendente y desempate por `Id`) y el
mismo `?page`, `?size`, `?sort`, `?q` que los otros doce. No es pereza: los cuatro parámetros de
consulta que la API acepta están **declarados** en `NingunCriterioSensibleViajaEnLaUrlTests`, y un
listado con su propia forma habría entrado ahí como excepción el primer día.

**2. El motivo nuevo, contra lo que decía el ADR-0027.** Ese ADR dejó escrito que el listado abriría
el ámbito «con el motivo que ya existe». Se decidió lo contrario y el ADR lleva su **corrección
fechada**: `AdministracionDelBloqueo` es el sistema operando sobre un bloqueo y esto es una persona
mirando datos personales reservados, y bajo la misma etiqueta la traza del art. 32 no las distingue
—que es justo para lo que sirve—. Era además la deuda que el propio enumerado tenía escrita en su
comentario: «un segundo valor cuando exista un camino de lectura». Consecuencia:
`MotivoParaVerLoBloqueado` pasa a lista cerrada de dos **con regla propia**, comparada entera y en
los dos sentidos contra las citas del código de producción.

**3. El vencimiento cuelga del MOTIVO, no de un número global.** Los dos motivos son de naturalezas
distintas: `SupresionSolicitada` reserva datos personales y **vence** —pasado el plazo procede
destruirlos—, y `CeseDeUso` **no vence nunca**, porque un almacén retirado se conserva por razón
contable y sus datos no son de nadie. Poner caducidad a los dos por igual pondría a destruir un dato
mercantil que hay que guardar. **Seis años por omisión** con procedencia: el art. 30 del Código de
Comercio, que es el suelo más largo de los que le aplican a una pyme española (cuatro la prescripción
tributaria del art. 66 LGT, cinco las acciones personales del art. 1964 CC); se elige el más largo
porque un bloqueo que venciera antes que la obligación de conservar destruiría lo que todavía hay que
poder enseñar. **Configurable por instalación** (`BASTION_PLAZO_DE_SUPRESION_ANIOS`), a diferencia de
`OpcionesDeLaBandeja`, que dice por escrito que no tiene variable «porque nadie ha necesitado
cambiarlos sin recompilar»: aquí el caso existe y no es hipotético. Ausente vale y significa el de
omisión; **puesta y mal** no vale y **para el arranque**, porque eso es alguien intentando configurar
algo y consiguiendo otra cosa. Y el `switch` sobre el motivo es exhaustivo y lanza: un tercer motivo
tiene que **decidir** si vence, no heredar el «no vence» en silencio.

**4. La traza dice QUIÉN, y eso cambió una línea que ya existía.** Hasta este ítem la anotación del
ámbito llevaba solo el motivo, y bastaba mientras el único camino era desbloquear: la escritura
siguiente dejaba su propia fila de auditoría con el usuario dentro. **Una consulta no escribe nada**,
así que si no lo dice esa línea no lo dice nadie, y el art. 32 no reserva el acceso «a la
administración» sino a personas concretas. El usuario va **anulable y sin lanzar**: el ámbito también
se abre fuera de una petición —arranque, trabajos de fondo— y ahí no hay usuario; un `Guid.Empty` de
relleno pasaría por un usuario de verdad en cuanto alguien leyera el registro.

**5. El rastro de la lectura NO es una fila de auditoría, y eso ya estaba resuelto.** El criterio
pedía que la traza no dependiera de la integridad referencial de lo que audita ni impidiera destruir
lo observado. Se comprobó en vez de reinventarlo: `RegistroDeAuditoria` no tiene **ninguna** clave
foránea —guarda identificadores sueltos— y una lectura no dispara el interceptor, que solo mira
entidades modificadas. O sea que la traza de esta consulta es la línea del `ILogger`, que no ata nada
y no impide destruir la fila el día que su bloqueo venza.

**6. El censo, extendido a los cuatro carriles que tienen reglas —con el coste medido.** El usuario
lo autorizó explícitamente para este ítem. Medido antes de decidir, no estimado: `Api.FunctionalTests`
112 `[Fact]` + 2 `[Theory]`, `Api.IntegrationTests` 124 + 9, `Organizacion.IntegrationTests` 30 + 10
—**287 casos** sin censo— frente a `Arquitectura.Tests` 23 + 0, que sí lo tenía desde el 0.12. El
coste real es **una lista escrita por ensamblado** y un fichero compartido, porque el mecanismo ya
existía: no había que inventar nada. Las tres UnitTests (`BuildingBlocks` 56+16, `Identidad` 43+5,
`Organizacion` 92+28) se quedan **fuera a propósito**: son tests de dominio, no reglas de frontera, y
borrar uno se nota en el comportamiento; borrar una regla de frontera no se nota en nada. Es una raya
movible, no un olvido.

**7. Dos trampas de EF Core que aparecieron en rojo y valen para el próximo listado.**
(a) `Concat`/`Union` **no** se traducen después de una proyección de cliente: las tres ramas
proyectaban a `new RecursoBloqueado(...)` y salieron **diez pruebas en rojo** con «Unable to translate
set operation after client projection has been applied». Se diagnosticó con una sonda temporal que
demostró que las ramas con **tipo anónimo** sí se unen (`UNION ALL`), y la construcción del tipo
propio se movió **detrás** de la unión. (b) El `ORDER BY` a través de esa proyección solo se traduce
si la proyección es un **inicializador de objeto**; con una llamada a constructor, EF la inlinea en la
clave de orden y no sabe traducirla. Por eso `RecursoBloqueado` es un `record` con propiedades
`init` y no un `record` posicional. Las dos las cazó `LaTraduccionASqlTests`, **sin contenedor**.

**8. Lo que este ítem NO toca**, dicho para que no se lea como un olvido: ni Terceros, ni el
artefacto de `type` del ADR-0030 (es el **1.5**), ni Catálogo, ni la retirada del ADR-0023 (el
**1.7**). Y no hay pantalla: el listado existe en la API y el frontal no lo consume todavía, que es
lo que la nota de riesgo de la ficha individual ya decía.

**9. Ninguna dependencia nueva.** `git diff --name-only b75b312..HEAD -- '*packages.lock.json'`
devuelve **cero** ficheros: los cuatro `.csproj` tocados añaden un `<Compile Include>` y ninguna
`PackageReference`, y `frontend/package*.json` no se mueven. No hay licencias que comprobar porque no
hay paquete que comprobar.

**10. Un error de proceso, escrito porque costó trabajo de verdad.** Al revertir la segunda mutación
se usó `git checkout -- <fichero>` sobre un fichero que tenía **trabajo del ítem sin commitear**, y
eso lo devolvió a HEAD: se perdieron las ~128 líneas que el 1.4 había añadido a
`ElFiltroNoSeSaltaPorAhiTests.cs` —la quinta apertura declarada y las **dos reglas nuevas**—. Se
rehicieron, y el propio censo del §4 sirvió de comprobante de que la reconstrucción era completa: su
lista declarada volvió a casar nombre a nombre. **La regla que deja:** antes de una tanda de
mutaciones, o se commitea lo que hay, o se respalda **todo** lo modificado y no solo lo no rastreado;
`git checkout --` no distingue entre «deshaz la mutación» y «deshaz el ítem».

**11. Un método de extensión es un paquete, y ahora hay quien lo comprueba.** El requisito 2 del
enunciado —registrar **quién** pregunta— le dio a `AccesoALoBloqueado` la dependencia `IUsuarioActual`,
que registraba **otro** método de extensión. La API llamaba a los dos y no notó nada; el publicador de
la bandeja, que corre fuera de una petición, llamaba solo a `AgregarInquilinato()` y reventó en la CI
con diez casos de Integración caídos. Se arregla registrando `IUsuarioActual` ahí también, con
`TryAddScoped` para que llamar a las dos extensiones no ponga dos descriptores del mismo servicio y el
orden del `Program.cs` no elija implementación en silencio. **Lo que deja escrito** es
`ElInquilinatoSeConstruyeSoloTests`, en el carril rápido: construye **todo** lo que la extensión
registra —descubriéndolo, no tecleándolo— en un ámbito con `validateScopes: true`. La lección, que no
es sobre Docker: un fallo de composición no necesita base de datos, necesita que alguien intente
construir lo registrado. El relato completo, con el mensaje de la CI y el mismo mensaje reproducido en
local en milisegundos, en *Verificado en local → La sexta, que no puse yo*.


### Tomadas por el agente de desarrollo — ítem 1.5 (2026-09-05)

**1. La unicidad de (empresa, identificador) abarca TAMBIÉN lo bloqueado, y la decisión no la toma
el índice.** El enunciado del ítem cruza dos cosas —unicidad por (empresa, NIF) y bloqueo— y avisa de
que lo que no vale es que decida el índice por omisión. Son dos opciones de verdad y las dos tienen
coste: un índice **parcial** (`WHERE NOT bloqueado`) libera el identificador al bloquear la ficha, y
entonces desbloquear puede encontrarse **dos filas con la misma llave** y alguien tiene que deshacer
a mano un empate con datos personales por medio; un índice **incondicional** deja el identificador
ocupado durante todo el plazo del art. 32, y quien quiera volver a operar con ese tercero tiene que
desbloquear el que hay en vez de dar de alta otro. Se elige el **incondicional**, porque un
desbloqueo que puede fallar por una colisión creada mientras tanto no es reversible, y el art. 32
exige poder devolver la ficha a su sitio. La contrapartida está escrita donde se paga
(`ErroresDeTercero`, `IRepositorioDeTerceros`) y comprobada **por el efecto**, no por la
configuración: `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca` da de
alta, bloquea, vuelve a intentar el alta —409— y luego desbloquea y comprueba que la ficha vuelve
entera con su identificador. Y `La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado`
se lo pregunta a `pg_index` con `indpred IS NULL`, que es la mitad que dice que **no hay predicado
parcial**: sin esa condición el test pasaría igual con el índice contrario.

**2. El índice está escrito a mano en la migración, y eso obliga a que lo vigile otro.** EF Core 10
**no sabe indexar miembros de un tipo complejo**: se intentaron las dos formas y las dos fallan —con
el selector, «no es una expresión de acceso a miembro válida»; con los nombres, «la propiedad
"Identificacion.Pais" no se puede añadir al tipo "Tercero"»—. La alternativa era degradar la
identificación a **tipo poseído**, que sí admite índices, y se descartó: un poseído tiene identidad
sintetizada y EF lo sigue como una entidad más (ADR-0016), o sea que el mapeo mentiría sobre el
modelo para conseguir un índice. Se escribe con `CreateIndex` en la migración. Lo que eso rompe hay
que decirlo: como el modelo **no conoce** el índice, `has-pending-model-changes` no puede echarlo en
falta, así que borrarlo de la migración no pondría roja ninguna comprobación de migraciones. Quien lo
nota es el test de esquema, que lo busca **en la base** — que además es donde de verdad importa.

**3. La indistinguibilidad del conflicto se sostiene en CUATRO decisiones de estructura, no en una
redacción.** El criterio pide que el alta contra un bloqueado devuelva un conflicto que no revela, y
lo que se ha buscado es que la redacción no pueda romperlo:

- (a) el puerto devuelve un **booleano** —`ExisteLaIdentificacionAsync`—, así que no es que el caso
  de uso decida no mirar si el que estorba estaba bloqueado: **no lo tiene**. Un enumerado de tres
  valores habría dejado la propiedad en manos de que nadie escribiera un `if`, y la primera vez que
  alguien quisiera «un mensaje más útil» lo escribiría.
- (b) `ErroresDeTercero.IdentificacionDuplicada()` **no recibe parámetros y no lleva el número
  dentro** —al revés que el almacén duplicado, que sí lo lleva—, porque con el número las dos
  respuestas dejarían de ser comparables enteras y la comparación tendría que normalizar el cuerpo. Un
  test que decide qué trozos no cuenta es un test que decide qué se puede filtrar.
- (c) el **tiempo** es el tercer canal, y los dos desenlaces hacen literalmente la misma consulta —un
  índice, una fila—: no hay un camino que además cargue el bloqueo ni uno que se ahorre la lectura.
  Iguales **por construcción**, no por haberlos medido.
- (d) cuál de los dos era **sí** queda escrito, pero en el registro y desde la implementación, que es
  quien lo sabe: suceso **8400** con `{Bloqueada}`, la empresa y el instante, y **sin el identificador
  fiscal dentro** — un NIF es un dato personal y el registro se agrega, se exporta y se conserva con
  menos ceremonia que la base de datos.

El ámbito de bloqueo se abre **solo alrededor de la pregunta** y no hasta el `ConfirmarAsync`: si
llegara hasta la grabación, se grabaría con el filtro de R16 apagado, que es mucho más de lo que aquí
hace falta.

**4. La fuga que este ítem NO cierra, dicha porque existe.** Quien tenga a la vez
`terceros.tercero.ver` y `terceros.tercero.crear` puede **correlacionar dos respuestas**: la búsqueda
por identificador (`POST .../buscar`) no encuentra la ficha bloqueada —una consulta ordinaria no ve lo
bloqueado— y el alta con ese mismo identificador contesta 409. «No aparece y sin embargo está
ocupado» solo puede significar una cosa. Cerrarlo pide algo que no es de este ítem —separar los dos
permisos, o hacer que la búsqueda mienta igual que el alta— y el checklist no se amplía por iniciativa
propia. Queda anotado con su nombre: lo que este ítem garantiza es que **una sola respuesta** no lo
diga, y eso es lo que estaba en el criterio.

**5. Un tercero bloqueado no aparece en NINGÚN listado, y esto lo estrena el 1.5.** El listado del
art. 32 que construyó el 1.4 recorre `TipoDeRecursoBloqueado`, que tiene exactamente tres valores
—`Empresa`, `Almacen`, `Ubicacion`—. Un tercero bloqueado desaparece del camino ordinario, como debe,
y **no asoma por el camino reservado**, que es donde debería. O sea que hoy la única forma de saber
que existe es intentar darlo de alta otra vez, que es justo el canal que el punto 3 cierra. No se
arregla aquí porque el enumerado, el permiso y el DTO de ese listado son del módulo Organización y
ampliarlos es cambiar el criterio del 1.4, ya cerrado; se anota como **deuda con nombre** para el ítem
que toque la retirada (1.7) o el que abra ese listado a más módulos.

**6. Los datos de prueba del identificador fiscal se GENERAN, y por eso la batería prueba el
algoritmo en las dos direcciones.** Un NIF real es un dato personal, y en una fixture queda en el
repositorio, en el artefacto de resultados y en el registro de la CI, para siempre y sin plazo. No hay
ni uno: `IdentificadoresInventados` calcula el carácter de control de números inventados
—`"TRWAGMYFPDXBNJZSQVHLCKE"[n % 23]` para el NIF, y el doblado de las posiciones pares para el CIF— y
`LaBateriaGeneradaTests` los recorre **en los dos sentidos**: el generado con su control se acepta, y
el mismo número con **cualquiera de los otros 22** controles se rechaza. Eso segundo es lo que
convierte la batería en una prueba del algoritmo y no en una lista de ejemplos: un validador que
aceptara cualquier letra pasaría la mitad de arriba entera. En el frontal, lo mismo: los
identificadores de `datos.ts` son `00000001R`, `00000002W` y `00000003A`, derivados con el mismo
algoritmo y documentados como tales, más uno extranjero opaco.

**7. El artefacto de `type` fue primero, y el orden se cobró.** Es la condición que el 1.3 dejó
escrita —«el mecanismo antes que el primer texto que lo use»— y se cumplió literalmente: los dos
primeros commits del ítem son el generador (`scripts/generar-errores.sh`), `docs/api/errores.json`, su
paso en la CI y el barrido de diccionarios comparándolo; las pantallas de Terceros vinieron después.
La consecuencia práctica es que `tercero-duplicado`, `tercero-no-encontrado` y los demás **nacieron
con texto en los dos idiomas el día que se escribieron**, sin una sola excepción que justificar en el
barrido. Al revés, el ítem habría escrito los textos a mano y el barrido habría nacido con la lista de
lo que no cumple.

**8. `Terceros.UnitTests` nace SIN censo, siguiendo la raya que puso el 1.4.** El censo cubre los
cuatro carriles con **reglas** —afirmaciones sobre un universo descubierto, cuyo borrado no se nota en
nada—; los tres proyectos `*.UnitTests` se quedaron fuera a propósito porque son tests de dominio y
borrar uno sí se nota en el comportamiento. El cuarto proyecto unitario se crea con el mismo criterio,
y no por omisión. Es una raya movible; moverla no es de este ítem.

**9. El frontal entrega el listado del criterio y NO la búsqueda por cuerpo.** El criterio pide
«listado paginado y filtrado» y `features/terceros/`, y eso está: `/terceros` con `?pagina=`,
`?tamanio=` y `?busqueda=` en la URL, el sello de verificación pintado **siempre** —también cuando
está comprobado— y el papel, que dice «cliente y proveedor» cuando son las dos cosas porque es **una**
ficha. El recuadro de filtro **no busca por identificador fiscal**, y es el ADR-0025 aplicado donde se
puede desobedecer: lo que se teclea ahí acaba en el historial del navegador, en el enlace que se copia,
en la cabecera `Referer` y en el registro de acceso del servidor de delante. Buscar por identificador
existe en la API, va por el cuerpo con `POST .../buscar`, y **el frontal todavía no lo usa**: es
alcance que el criterio no pide y que se deja anotado en vez de colarlo. El servidor simulado de los
tests **no filtra por identificador a propósito**, para que ningún test pueda pasar contra un servidor
que sí lo hiciera.

**10. El régimen fiscal del §7.2 no está en el criterio de ningún ítem, y queda anotado.** El modelo
de dominio del plan maestro se lo pone a `Tercero`; el criterio escrito del **1.5** no lo nombra, y el
del **1.6** tampoco —ese enumera `Contacto`, `CuentaBancaria`, `CondicionPago` y `LimiteCredito`—. No
se añade por iniciativa propia (el checklist no se amplía) y no se da por olvidado: si es del 1.6, hay
que decirlo allí; si es de facturación, hay que decir de qué fase. Es una pregunta, no una tarea.

**11. Los tests nuevos montan la PANTALLA, no la aplicación, y la cifra está medida.** Montar el
armazón (`montarAplicacion`) cuesta exactamente **cuatro** avisos de «not wrapped in act» por montaje
—`SelectorDeEmpresa`, `Guarda`, `Disposicion` y `RouterProvider`—, con carga perezosa de la ruta y sin
ella, y saltan **después** de que el ámbito de `act` se cierre, así que envolver no los quita: se
probaron las dos formas, y una de ellas —sondear dentro de un `act` asíncrono— **cuelga**, porque
React no repinta dentro de un ámbito `act` asíncrono. La salida es `montarPantalla`, que monta la
pantalla bajo prueba con su enrutador de memoria, su caché y su i18n: **cero avisos**. Los nueve casos
nuevos del listado y los del texto de fallo entran con cero, y la cifra global se queda en **109 en
siete ficheros**, la misma antes y después. Cerrar esos 109 no es de este ítem; no aumentarlos, sí.

**12. Una desviación entre la batería de `AGENTS.md` y la CI, encontrada al diseñar la mutación 4.**
La CI comprueba desde este ítem que `docs/api/errores.json` no se ha quedado atrás (paso «Catálogo de
errores»), y la batería de aceptación de `AGENTS.md` **no lo nombraba**: quien la pasara entera en
local podía empujar un `type` nuevo sin regenerar el artefacto y descubrirlo en el run. Es la misma
familia que la rendija del recuento que anotó el 1.3 —«el paso de CI no es solo su comando»— y se
arregla igual: la línea en la tabla y en el bloque de la batería. Salió de preguntarse **quién** iba a
poner roja la mutación 4, no de leer el fichero por encima.

**13. Ninguna dependencia nueva, y la frase sale del diff.** Los conjuntos de paquetes resueltos de
todos los `packages.lock.json` a los dos lados: **145 en `main`, 150 en la rama**, y las cinco entradas
de diferencia son los cinco proyectos nuevos del propio módulo (`bastion.terceros.domain`,
`.contracts`, `.application`, `.infrastructure`, `.endpoints`). **Cero paquetes de terceros añadidos,
cero retirados**, `Directory.Packages.props` sin tocar, y `frontend/package.json` y `package-lock.json`
sin mover. No hay licencias que comprobar porque no hay paquete que comprobar.

**14. Los captadores del registro son sumideros de Serilog, y ahora tienen quien los vigile.**
Lo destapó el run del propio ítem, el
[34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496), con **un caso rojo de
271**: la mitad de §1 que dice que *la traza sí registra cuál de los dos era* esperaba dos sucesos
anotados y leyó **cero**. No era el producto. `src/Api/Program.cs` hace `ClearProviders()` y luego
`AddSerilog(...)`, que instala `SerilogLoggerFactory`, y esa fábrica **ignora todo `ILoggerProvider`
ajeno**: `RegistroDeSucesos` y `RegistroDeFallos` eran proveedores, así que nunca recibieron nada.
Cero significaba «no hay cable» y se leía como «la API no anota» — las dos lecturas son
indistinguibles desde el test, que es lo que lo hace peligroso.

Se pasan los dos a `ILogEventSink`. **La decisión de verdad no es esa, es dónde vive el enganche:**
`tests/Comun/CapturaDeRegistro.cs`, compartido por los dos carriles y **no copiado**. Lo que se rompió
fue el enganche, así que un canario con su propia copia habría comprobado su copia; el de ahora
—`ElCaptadorDeRegistroCapturaTests`, en `Api.FunctionalTests`— ejecuta **la misma línea** que ejecuta
`ApiDeVerdad`. Y va al **carril rápido**, no al de integración, porque un cable roto no necesita
PostgreSQL para notarse: donde estaba, avisó en la CI y no en la máquina de quien lo rompió.

**Lo que además se descubrió por el camino y vale la pena no olvidar:** `RegistroDeFallos` llevaba sin
funcionar desde que se escribió, y su motivo era precisamente que los registros de un *job* devuelven
403 sin autenticar. En el [run 33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347)
hay un `500` de verdad y detrás ni una línea de `· registro del servidor:`. Un arnés de pruebas es
código sin nadie detrás: la primera pregunta ante una regla nueva no es «¿el dominio la cumple?» sino
**«¿la regla mira lo que dice mirar?»**. El desarrollo entero está en *Verificado en local* →
*La avería que la CI destapó*, con las dos mutaciones que ponen rojo el canario.


### Tomadas por el agente de desarrollo — ítem 1.6 (2026-09-07)

**1. La correlación buscar/alta no se cierra en este ítem, y deja de ser una nota abierta: es una
decisión con su razonamiento escrito.** El hecho está comprobado y no ha cambiado: `POST
.../terceros/buscar` no encuentra una ficha bloqueada —una consulta ordinaria no ve lo bloqueado— y
`POST .../terceros` contesta `409` si su identificador está ocupado, esté la ficha activa o bloqueada.
Cada respuesta por separado no revela nada; **las dos juntas, sí**: quien tenga a la vez
`terceros.tercero.ver` y `terceros.tercero.crear` aprende que existe una ficha bloqueada con ese
identificador, y «no aparece y sin embargo está ocupado» solo significa una cosa. Las tres salidas
posibles, con lo que cuesta cada una:

  - **Separar los permisos** para que nadie tenga los dos. Es una decisión de **política de
    permisos**, no de código: en una pyme de cinco personas, quien da de alta un cliente es la misma
    persona que lo busca antes para no duplicarlo. Cierra el canal y rompe el trabajo diario.
  - **Que la búsqueda mienta igual que el alta** —que devuelva la ficha bloqueada— rompe R16 y el
    art. 32 a la vez: el bloqueo existe precisamente para que no aparezca.
  - **Que el alta deje de distinguir**: contestar `201` con una ficha nueva sobre un identificador
    ocupado. Rompe la unicidad de (empresa, identificador), que es la decisión 1 del 1.5.

  Ninguna es gratis, así que **no la toma el agente**: es de las de preguntar. Se anota aquí con las
  tres opciones y su coste para que la pregunta llegue entera al día que se conteste, y **no vuelve a
  *Notas / riesgos*** porque no es un riesgo que se descubra: es un cruce conocido, medido y
  documentado. Lo que sí queda cerrado del 1.5 es lo que su criterio pedía —una respuesta que no
  revela—, y eso está comparado entero.

**2. El régimen fiscal del §7.2 entra en el 1.6, y el corte que se sostiene son tres condiciones de
cuatro.** Contesta la pregunta que el 1.5 dejó abierta (decisión 10 de aquel ítem). **No es alcance
nuevo:** está en el modelo de dominio del plan maestro y se quedó sin ítem al desglosar la fase —
alcance comprometido sin casa—, y su sitio es el tercero, porque dejarlo para la fase 3 obliga a
migrar un maestro con datos dentro. El corte es el de `LimiteCredito`: lista cerrada de valores con
su `CHECK` y nada más. Entra `TerritorioFiscal` (cinco valores) y entran los tres booleanos —recargo
de equivalencia, criterio de caja, sujeto a retención de IRPF—, y **se sostiene porque los cuatro son
hechos declarados del tercero**, no resultados de un cálculo: que un minorista esté en recargo es
algo que él dice y que su ficha registra, igual que su domicilio. **Lo que no se sostiene y se deja
fuera es el tipo de retención**: ahí sí es cierto que el valor no significa nada sin la regla que lo
aplica, y además ya tiene dueño desde el 1.2 —`Impuesto` con `TipoDeImpuesto.Retencion`—, así que
meterlo aquí obligaría a declarar un cruce entre módulos (ADR-0024). El booleano dice **si** se le
retiene; **cuánto**, quien ya lo sabe.

**3. `LimiteCredito` lleva su divisa; no la hereda de la empresa.** El criterio del ítem dice «solo el
importe» y no cierra esto, así que hacía falta decidirlo o escribir por qué no. Un importe sin divisa
sería **la única cantidad de dinero del sistema que no dice de qué es**. Heredar `Empresa.DivisaBase`
—que es como se llama; **`DivisaPreferidaId` no existe**, era el nombre que traía la nota— tiene un
modo de fallo mudo: cambiar la divisa base de la empresa reinterpretaría todos los límites ya
guardados sin tocar una fila y sin que nada fallara. Así que el límite es un `Importe?`, con
`numeric(18,4)` —nunca coma flotante, nunca el `money` del motor— y un `CHECK` que obliga a que la
cantidad y la divisa estén o falten **juntas**. Lo que la pantalla ofrezca por omisión es de la
pantalla. Consumo, disponible y bloqueo por exceso no son de este ítem: esa es la raya de P6.

**4. La cuenta preferente por tercero es una restricción, y ve las filas bloqueadas: es la misma
decisión del 1.5, sin matices.** Índice único parcial sobre `(tercero_id)` con filtro `es_preferente`,
más el invariante en el agregado. El índice es de la base y no conoce el filtro global, igual que el
de (empresa, identificador). **Y aquí no podría ser de otra manera**, que es lo interesante: el
bloqueo del art. 32 vive en el **padre**, no en la cuenta, así que dos cuentas que compiten por ser
la preferente están siempre en el mismo estado. La pregunta que en el 1.5 tenía dos respuestas
posibles con costes distintos, aquí solo tiene una — y por eso no se rehace el razonamiento: se cita.

**5. `Contacto` no tiene campo de notas, y es una decisión.** Los cuatro campos —nombre, cargo,
correo, teléfono— pasaron las cuatro preguntas (finalidad, plazo, quién lo ve, categoría especial) y
están contestadas una a una en la cabecera de la entidad, con la base escrita: el art. 19 de la
LOPDGDD ampara los datos de contacto de quien presta servicios en una persona jurídica **siempre que
se refieran a esa actividad**. Eso cubre el teléfono del departamento de compras; **no cubre el móvil
personal de nadie**, y como el modelo no puede distinguir un número de otro, lo que hace es no
invitar a guardarlo —un solo teléfono, el del puesto—. El campo de notas se descarta porque un `text`
libre en la ficha de una persona es donde acaban «está de baja», «es el yerno del dueño» y «no lo
llames los viernes»: categorías especiales que **ninguna** de las cuatro preguntas podría contestar.
Cuando haga falta anotar el trato comercial, su sitio es el CRM del §7.11.

**6. La tabla de longitudes del IBAN es la zona SEPA —37 países—, no el registro entero de la ISO.**
Aceptar los más de ochenta del registro haría creer que esta instalación puede domiciliar un adeudo
contra cualquiera de ellos, y es falso: fuera de SEPA hace falta una transferencia internacional, con
su comisión y su plazo. Añadir un país es cambiar la tabla, con su motivo. Los territorios de ultramar
no llevan código propio y por eso no aparecen.

**7. El barrido de datos con forma de real declara lo que se PERMITE, no lo que se prohíbe.** Es la
decisión de diseño de `NingunDatoConFormaDeRealTests` y sale de la propia restricción del encargo: una
lista de valores prohibidos tendría que llevarlos escritos, y **escribir un IBAN real para prohibirlo
es exactamente el daño que se está evitando** —«tampoco para señalarlos»—. Así que cada forma declara
su espacio inventado y todo lo que tenga la forma y esté fuera es rojo. Por lo mismo, los mensajes de
fallo dan fichero, línea y forma **enmascarada**: un registro de CI que imprime lo que ha cazado
contiene justo lo que la regla existe para sacar del repositorio.

**8. Las siete escrituras nuevas exigen `If-Match` y ninguna admite `Idempotency-Key`.** No son altas
de un recurso con vida propia: son modificaciones de un agregado que ya existe, y citan **el testigo
de la ficha**, no uno suyo. De ahí que las entidades hijas no lleven testigo de concurrencia propio —
colgar un contacto mientras otro cambia la razón social es un choque de verdad y se quiere que la
segunda escritura se lleve el `412`—. El censo se mueve **+11 acciones, +7 escrituras, +7 `If-Match`,
cero idempotencia y cero exenciones**, y esa forma del movimiento es la comprobación de que la
decisión se aplicó: si alguna hubiera acabado en el cajón de las exentas, se vería.

**9. Un controlador para lo que cuelga, no cuatro.** `LoQueCuelgaDelTerceroController` monta las once
rutas bajo `/terceros/{terceroId:guid}/…`. Son subrecursos de **una** ficha y comparten el
`[Route]`, la versión que exigen y el 404 de la ficha inexistente; partirlo en cuatro habría
duplicado esas tres cosas cuatro veces. La regla que vigila que escribir y modificar no compartan
permiso sigue cumpliéndose porque los permisos son **siete**, uno por escritura, no uno por
controlador.

**10. `ObtenerConLoQueCuelgaAsync` es un método aparte del repositorio, no un `Include` metido en el
de siempre.** Cargar las tres colecciones en toda lectura de un tercero son tres consultas por nada
en el caso común —el listado, la ficha, el límite de crédito—. Y al revés, operar sobre el agregado
**sin** sus hijos es peor que lento: `AgregarCuentaBancaria` decidiría sobre una lista vacía y creería
que no hay ninguna preferente. Los dos caminos existen y cada uno dice cuál usa.

**11. Las tres tablas del censo del 0.9 se han rehecho enteras, y el motivo es una lección.** Llevaban
desde el **0.10** con sus números —46 acciones, 13 · 6 · 13— mientras el test iba subiendo hasta 93 y
30 · 13 · 17. Nada se puso rojo porque **una tabla en un `.md` no es una fuente que ningún test
compare**: es prosa, y la prosa no falla. Es la misma familia que el «antes» mal medido de los
`act()` del 1.5, y la salida es la misma que allí: la cifra que se publica va con lo que la produce.
Aquí, con la lista enumerada al lado del número, para que la próxima copia se pueda contrastar.

**12. Ninguna dependencia nueva, y la frase sale del diff.** Conjuntos de paquetes **resueltos** de
todos los `packages.lock.json` a los dos lados: **150 en `main` y 150 en la rama**, sin una sola
entrada de diferencia en ninguno de los dos sentidos. Los `.csproj` que cambian mueven la referencia
a Npgsql a `BuildingBlocks.Infrastructure` —que ya estaba en el grafo por transitividad—, y
`Directory.Packages.props`, `frontend/package.json` y `package-lock.json` no se tocan. **No hay
licencias que comprobar porque no hay paquete que comprobar.**


### Tomadas por el agente de desarrollo — ítem 1.7 (2026-09-07)

**1. La retirada se ve en la colección con un parámetro de listado, `?retiradas=true`, y no con un
extremo aparte.** El ADR-0023 dice «el `GET` de la colección la excluye **por omisión**», y «por
omisión» implica que hay un modo de verlas: quien tecleó `EURO` en vez de `EUR` y la retiró tiene
que poder encontrarla para arreglarla. Las dos formas posibles eran un parámetro o un extremo
nominativo, y el proyecto ya tiene el segundo montado —el listado del artículo 32 del ADR-0027—, así
que la pregunta es **por qué aquel es un extremo aparte y este no**. La respuesta es entera del
ADR-0027 y no una preferencia: aquel camino existe porque lo que enseña son **datos personales
reservados**, y `proteccion-datos.md` exige para el art. 32 «una vía de acceso separada, nominativa
y trazada»; por eso tiene permiso propio, motivo propio de apertura del ámbito y queda anotado en el
registro. Una divisa retirada no tiene nada de eso: **no hay dato personal, no hay art. 32, no hay
nada que trazar**. Copiar allí la forma del bloqueo sería copiar su coste —cuatro permisos nuevos,
cuatro rutas nuevas, cuatro consultas nuevas— para proteger algo que no necesita protección, y
además diría en el contrato que ver una divisa retirada es un acto reservado, que es falso.

  Consecuencias, todas queridas:

  - **Pasa por el contrato de listado del 1.3**: es un `[FromQuery]` de un modelo que hereda de
    `ConsultaPaginada`, con lo que se valida como los otros cuatro y hereda el tope de `size`.
    Construido a mano en el controlador se saltaría la validación entera, que es lo que el propio
    `ConsultaPaginada` explica que no puede pasar.
  - **No es un criterio sensible**, y eso no se afirma de palabra: `NingunCriterioSensibleViajaEnLaUrl`
    compara la lista **entera** de parámetros de todos los listados contra `{page, q, size, sort}`,
    así que el quinto nombre **pone esa regla roja** y obliga a declararlo ahí, junto a los motivos.
    Es exactamente el disparador que esa regla dice tener: «un parámetro nuevo —sensible o no— pone
    esto rojo y obliga a decidirlo aquí».
  - **Solo lo declaran los cuatro maestros retirables.** El parámetro vive en un modelo de consulta
    aparte (`ConsultaDeMaestro`), no en `ConsultaPaginada`: metido ahí, los doce listados —roles,
    usuarios, terceros— publicarían en el OpenAPI un `?retiradas=` que no miran, que es una mentira
    del contrato y de las que no fallan.
  - **Dos valores y no tres.** `?retiradas=true` incluye las retiradas junto a las demás; omitido o
    `false`, no. No hay un tercer «solo las retiradas»: cada elemento publica su `retirada`, así que
    ese caso lo compone el cliente filtrando, y un tercer valor sería una segunda manera de decir lo
    mismo con su propia rama que probar.

**2. Una fila retirada SIGUE restringiendo a su inversa.** Es la pregunta que abre la decisión 2 del
ADR-0023 en cuanto se cruza con la 1, y la respuesta no es obvia, así que va escrita y no puesta por
omisión. Si `A→B` está retirada y alguien da de alta o modifica `B→A`, la tolerancia
`|f·g − 1| ≤ 5·10⁻⁷·(f + g)` **se comprueba igual**, contra el factor de la fila retirada.

  El motivo es literal del ADR: una fila retirada «**sigue resolviendo** para lo que ya apunta a
  ella». Sigue resolviendo significa que **sigue siendo verdad**, y dos filas que se contradicen
  siguen contradiciéndose aunque una de ellas ya no se ofrezca: el albarán de hace tres años que
  resuelve por `A→B` y el que se emita mañana por `B→A` darían cantidades que no cuadran, que es
  exactamente el descuadre sin autor que la decisión 2 existe para impedir. La alternativa —que
  retirar una fila la saque de la comprobación— convierte la retirada en **una manera de saltarse la
  regla**: bastaría retirar el sentido incómodo para poder declarar cualquier número en el otro. Una
  salida que la propia regla ofrece no es una salida: es un agujero con permiso.

  Lo que **sí** cambia con la retirada es la otra mitad, y por el mismo motivo: la fila retirada **no
  se ofrece**, así que no se puede dar de alta una conversión nueva sobre una unidad retirada. Las
  dos frases son la misma —«no se ofrece para lo nuevo, sigue resolviendo lo viejo»— leídas en los
  dos sentidos.

**3. La retirada es un booleano, sin fecha y sin motivo.** Es la tercera decisión, la de forma, y
tiene su porqué. El bloqueo lleva fecha y motivo porque el art. 32 los exige —de la fecha cuelga el
plazo de prescripción y del motivo cuelga si vence—, y nada de eso aplica aquí. Una fecha, además,
tendría un coste que no se ve: invitaría a un `EstadoDeAsync(id, fecha)` en los puertos y convertiría
la retirada en **una línea temporal**, que es justo lo que el ADR-0023 dice que la retirada **no
es** —«no es un cierre; una unidad de medida no se sucede: deja de usarse»—. Quién la retiró y cuándo
ya está guardado: `ModificadoEn` lo pone el interceptor de marcas y la traza de auditoría guarda el
cambio de columna con su autor. Un motivo enumerado sería una lista cerrada más, sin ningún consumidor
que ramifique sobre ella, que es la vacuidad del ADR-0020 en forma de columna.

**4. La retirada se deshace, con `DELETE .../{id}/retirada`.** No lo dice el ADR, y hay que decidirlo:
el motivo por el que la retirada existe es que «el alta equivocada es permanente **y visible desde
todas las empresas de la instalación**», y una retirada irreversible sería un error nuevo con
exactamente el mismo radio. La forma no se inventa: es la del cierre de un ejercicio,
`POST`/`DELETE` sobre el mismo subrecurso, que ya está en el módulo. Son dos permisos por maestro
—`retirar` y `reincorporar`—, como `bloquear`/`desbloquear` y `cerrar`/`reabrir`, porque deshacer una
retirada no es la misma facultad que hacerla.

  Y no es un `DELETE` del recurso: **ninguno de los cuatro tiene `DELETE` sobre su elemento, nunca**,
  que es lo que el ADR prohíbe. La distinción es de ruta y así se comprueba: lo prohibido es
  `DELETE /{recurso}/{id}`; `DELETE /{recurso}/{id}/retirada` es el verbo que levanta un subrecurso,
  igual que `DELETE /ejercicios/{id}/cierre` convive con el `DELETE` del ejercicio sin ser el mismo.

**5. `TipoCambio` y `ConversionUM` no tienen puerto de consumo, y su retirada no es vacía.** Hay que
escribirlo porque es justo la pregunta que el ADR-0020 obliga a hacerse —«¿esto lo mira alguien?»— y
la respuesta no se ve de un vistazo. `Divisa` y `UnidadMedida` tienen `IConsultaDeDivisas` e
`IConsultaDeUnidadesDeMedida`, así que su retirada es observable desde **otro módulo**: Catálogo
pregunta y recibe `SoloResuelveLoViejo`. Los otros dos no tienen puerto: nadie fuera de Organización
pregunta hoy por una cotización ni por una conversión. **Eso no las hace vacías.** Su retirada es
observable por **sus propios `GET`**, que es un camino real y ejercitado: la colección deja de
traerlas, el elemento las sigue devolviendo, y el resolutor de conversiones —que es de este mismo
ítem— deja de ofrecer una conversión retirada para una operación nueva. La diferencia con el caso
vacío del ADR-0020 es exactamente esa: allí la columna existía y **ningún camino la leía**; aquí hay
tres caminos que la leen, y ninguno de ellos necesita que exista un puerto entre módulos.

**6. Dos defectos del factor, encontrados al leer para la decisión 2 y arreglados aquí.** No estaban
en el encargo; salen de que la decisión 2 del ADR se apoya en el rango del factor para afirmar que
«la regla es **total**, no tiene casos en los que se calle».

  - **`FactorValido` redondea DESPUÉS de comprobar el cero.** El orden era: rechazar `factor <= 0`,
    y luego `decimal.Round(factor, 6, AwayFromZero)`. Con eso, `0,0000001` pasa la comprobación
    —es mayor que cero— y se **guarda como cero**, que es literalmente el fallo que el comentario
    de al lado dice impedir: «cero convertiría cualquier existencia en nada, y en silencio». Se
    invierte el orden: se redondea primero y se comprueba el número que se va a guardar.
  - **Y hay que decir hasta dónde llegaba, porque no llegaba a la API.** El `[Range(0.000001,
    1000000)]` de `CrearConversionUmDto` y `ModificarConversionUmDto` rechaza `0,0000001` antes de
    llegar al dominio, así que **por HTTP el defecto no era alcanzable**. Lo era por cualquier otro
    camino que construya la entidad —una semilla, otro módulo, una prueba—, y sobre todo lo era
    como *promesa*: la guarda del dominio decía proteger de algo de lo que no protegía, y quien la
    leyera lo daría por cerrado. Un defecto tapado por una validación de otra capa sigue siendo un
    defecto, y además del tipo peor: el día que alguien llame al dominio desde donde no hay `[Range]`
    aparece sin previo aviso.
  - **El rango `[0,000001, 1000000]` que el ADR cita solo estaba en el borde, y escrito a mano dos
    veces.** El dominio no lo comprobaba: el suelo lo daba de hecho el redondeo a seis decimales una
    vez corregido lo anterior, y el techo no existía en ninguna capa por debajo de la anotación.
    Importa porque el argumento de totalidad de la decisión 2 —«la inversa de cualquier factor
    válido cae también dentro del rango»— se apoya en él: sin techo, un factor de `10¹⁵` tiene una
    inversa que no se puede declarar con seis decimales, y la regla tendría un caso en el que se
    calla. Queda comprobado **en el dominio**, con el rango escrito una sola vez como constantes de
    `ConversionUM` y citado el ADR; y como las dos anotaciones del borde tienen que llevar
    literales de compilación —`[Range]` no admite otra cosa—, hay una regla que compara los cuatro
    literales contra las dos constantes, que es la única manera de que dos fuentes escritas en
    sitios distintos no se separen en silencio.



### Tomadas por el agente de desarrollo — ítem 1.8 (2026-09-10)

**1. El árbol de categorías se modela como LISTA DE ADYACENCIA, y la alternativa está costeada.**
`Categoria` guarda `PadreId` y nada más: una columna que apunta a la misma tabla, con clave ajena a
sí misma y dentro del mismo esquema. La alternativa seria era una *closure table* —una fila por cada
par (antepasado, descendiente) con su distancia—, que es lo que se pone cuando lo que se consulta de
verdad son subárboles.

El criterio para elegir fue **qué va a consultar este módulo**, no qué es más general. El consumidor
real es el **listado de artículos filtrado por categoría**, y ese filtro es `WHERE categoria_id = ?`:
una igualdad sobre una columna indexada, que la lista de adyacencia sirve directamente y para la que
una *closure table* no aporta nada. Lo segundo que se consulta es **el árbol entero de una empresa**
para pintarlo, que en una lista de adyacencia es un `SELECT` plano y se compone en el cliente. La
*closure table* cobra su precio en la escritura: mover una rama reescribe todas las filas de cierre
de su subárbol, y esa escritura hay que mantenerla coherente con la jerarquía en cada alta y cada
modificación. Se pagaría desde el primer día por una consulta que este módulo no hace.

**Y qué pasaría el día que haga falta el subárbol.** Pasaría lo que ya está dicho en pantalla: hoy el
filtro **acota por la categoría dicha y no por lo que cuelga de ella**, y las dos pantallas lo avisan
con esas palabras en vez de dejar que parezca un fallo. El día que el subárbol haga falta de verdad
—el candidato es la **precedencia de tarifas del 1.9**, que asciende de la categoría del artículo
hasta la raíz— hay dos salidas y ninguna obliga a migrar el modelo: un `WITH RECURSIVE` de PostgreSQL
sobre la misma columna, que con un árbol acotado a once niveles recorre como mucho once filas por
consulta; o una *closure table* **derivada**, que se añade al lado sin tocar `PadreId` porque la
jerarquía sigue viviendo ahí. Lo que no se puede hacer barato es al revés: quitar una *closure table*
que ya se está manteniendo. Por eso se empieza por la que no cobra por adelantado.

**2. La cota del recorrido de padres es `Categoria.ProfundidadMaxima`, y vale diez.** Diez niveles
**por debajo** de la raíz, o sea un árbol completo de once categorías desde la raíz hasta la hoja más
honda. El número vive en el dominio, junto a su motivo, y el caso de uso lo lee de allí: repetirlo
sería tener dos fuentes que se separan sin que nada avise.

La cota tiene **dos motivos y hay que decir los dos**. El primero es de negocio: a más de diez
niveles, lo que hay debajo de una categoría casi nunca es una clasificación —es un atributo del
artículo disfrazado de categoría, que es donde se acaban metiendo el color y el tamaño—, y la cota lo
dice con un error con nombre en vez de dejar crecer un árbol que nadie puede recorrer con la vista.
El segundo es que **la cota es lo único que convierte un cuelgue en un error**: el ascenso gasta una
consulta por nivel, y sobre datos que ya tuvieran un ciclo —una restauración a medias, un `UPDATE` a
mano, una importación de otro sistema— un ascenso sin cota no da error, **gira**, dentro de la
petición y con la conexión abierta. Un servidor que no contesta es peor que cualquier rechazo, porque
no dice de qué venía. Con la cota puesta el peor caso son once viajes a la base por alta o
modificación con padre, y el ciclo ya guardado sale como `categoria-demasiado-profunda` con la cadena
escrita.

**Y la cota tiene un caso que la ejerce sin colgar la suite**, que es la otra mitad de la decisión:
`Un_ciclo_YA_GUARDADO_no_deja_el_recorrido_dando_vueltas` monta dos categorías que se apuntan la una
a la otra, comprueba que el recorrido termina **y cuenta cuántos eslabones ha pedido**. El doble de
repositorio lleva un tope del doble —cien— que lanza con una frase que dice qué ha pasado. Sin ese
tope, quitar la cota no daría un caso rojo: daría un caso que no termina, y el síntoma sería «la CI
tarda», que no señala a nadie.

**3. La comprobación de ciclos vive en la capa de APLICACIÓN, y se hace en las DOS puertas.** Dónde
vive, por lo mismo que `LaInversaEsPlausible` del 1.7 —el patrón se reutiliza, no se inventa otro—:
la regla relaciona **varias instancias** del agregado, y la R12 dice una transacción, un agregado. Un
invariante de dominio que tuviera que cargar el resto del árbol sería justo la grieta que la R12
cierra. Tampoco es una restricción de la base: tendría que mirar otras filas, o sea un disparador
recursivo, con el coste y la invisibilidad que eso tiene.

Y en las dos puertas porque **el hueco está en la modificación**. En el alta la rama del ciclo es
estructuralmente inalcanzable —la categoría que nace todavía no está en el árbol, así que ningún
antepasado suyo puede ser ella— y conviene decirlo en voz alta en vez de contar el alta como el sitio
donde se atrapan los ciclos. Lo que el alta sí ejerce son las otras dos ramas: que el padre exista y
que colgar de él no pase de la profundidad máxima. El ciclo lo cierra **reasignar el padre** de una
rama debajo de su propia descendencia, y eso solo se puede hacer modificando. Es la misma forma del
hueco que el 1.7 le encontró a la inversa, por su otra cara.

**4. El caso degenerado —una categoría con ella misma como padre— NO tiene tratamiento aparte, y es
una decisión.** Es un ciclo de longitud uno: sale por la primera vuelta del mismo recorrido, con el
mismo `categoria-ciclo` y el mismo mensaje. Para quien lo provoca es el mismo problema, y una rama
propia solo daría un sitio más donde equivocarse. La mutación 4 lo comprueba por el otro lado:
tratarlo aparte —dejarlo pasar en su propia rama— pone rojos dos casos, uno por cada puerta.

**5. La pregunta del art. 32 se ha hecho, no se ha supuesto: `Articulo` NO es bloqueable.** El
artículo 32 de la LOPDGDD habla del bloqueo de **datos personales**, que es lo que hace bloqueables a
la empresa, al usuario y al tercero. Un artículo guarda código, descripción, tipo y tres
identificadores, y nada más: ni nombre, ni identificador fiscal, ni dirección, ni cuenta. Lo mismo la
categoría. La respuesta está escrita en `Articulo`, en `ArticuloDto` y en `CatalogoDbContext`, y
además **comprobada**: `ElCatalogoNoGuardaDatosDeNadieTests` recorre el modelo y afirma que ninguna
entidad de Catálogo guarda un dato personal, y `ModuloDeCatalogo` **no** registra un aportador al
listado del art. 32 —registrar uno vacío sería peor que no registrarlo: pondría en el informe un
módulo que no tiene nada que aportar—.

**6. Ni la unidad ni el impuesto son claves ajenas, y esa es la §5 entera.** Viven en el esquema de
Organización y aquí se guardan como `uuid` desnudos, validados por los puertos del 1.2
(`IConsultaDeUnidadesDeMedida`, `IConsultaDeImpuestos`). Sin esas dos preguntas las dos columnas
aceptarían cualquier identificador, compilarían, migrarían y pasarían los tests —es la «cuarta vía»
del ADR-0024—, y el fallo aparecería en la factura que los usa, tres fases después. Las mutaciones 6
y 8 son las dos caras de esto: quitar la pregunta, y poner la clave ajena.

**7. Catálogo es el consumidor para el que se construyó la retirada, y las tres casillas de
`EstadoDeMaestro` significan tres cosas distintas en un camino de negocio.** `SeOfreceParaLoNuevo` →
el alta pasa. `SoloResuelveLoViejo` → el alta se rechaza **y el artículo que ya la usaba sigue
resolviéndola**: las dos mitades, que es lo que distingue una retirada de un borrado. `NoExiste` → el
alta se rechaza con un `type` **distinguible** del anterior, porque son dos arreglos distintos: quien
teclea un identificador inventado corrige el identificador, quien apunta a uno retirado elige otro
que sí se ofrezca.

La segunda mitad consiste en algo que **no se hace**: `ModificarArticulo` vuelve a preguntar
únicamente por lo que **ha cambiado**. Revalidar el maestro que nadie ha tocado convertiría la
retirada en un **congelador** —el día que un tramo deja de regir, cada artículo que lo propusiera se
quedaría sin poder corregir ni su propia descripción—. Y la unidad base es el caso extremo: no se
revalida porque **no se puede cambiar**, no está en el DTO ni en `Articulo.Modificar`.

> **Y aquí la tanda de mutaciones destapó una aserción sin escenario.** El caso por HTTP que afirma
> esto —`Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_...`— llegaba a la modificación con
> el tramo del artículo **todavía vigente**, y con eso revalidarlo y no revalidarlo se ven
> exactamente igual desde fuera: la mutación 2 lo dejaba verde. Arreglado en `1eab397`, cerrando el
> tramo del artículo —`POST /impuestos/{id}/cierre` con el último día en ayer— antes de la
> modificación que no lo toca. Es la misma familia que el «antes» mal medido de los `act()` del 1.5:
> una afirmación cuyo escenario no la ejerce sale verde por la peor de las razones.

**8. En `/articulos` no hay desplegable de categorías, y el filtro por rama se pone desde
`/categorias`.** Un desplegable obligaría a traerse el catálogo de categorías entero en cada visita a
la pantalla de artículos, y a quedarse corto —sin decirlo— en la empresa que tuviera más de las que
caben en una página. Lo que sí hay en `/articulos` es el filtro **puesto**, con el nombre de la rama
resuelto por un `GET` por identificador y la salida para quitarlo. Ese `GET` se lanza **solo** si la
sesión concede `catalogo.categoria.ver`: la interfaz esconde y el servidor autoriza, pero pedir lo
que se sabe que va a contestar `403` no es esconder, es hacer ruido. Y si el nombre no llega, el
filtro se anuncia igual sin nombre: callarlo dejaría una tabla con menos filas de las que hay y
ninguna explicación a la vista.

**9. En `/categorias` no hay filtro de texto, y también es una decisión.** Filtrar un árbol por texto
destruye justo lo que esa pantalla enseña: al quitar las filas que no casan, lo que queda son ramas
sueltas con sus niveles descolgados. La pantalla es la forma del árbol; quien busca un artículo lo
busca en `/articulos`.


## Estado actual

**Puerta de clarificación de la fase 1 cerrada — el desglose existe y es una decisión escrita:**
doce preguntas planteadas juntas y contestadas juntas, con su motivo, en *Decisiones tomadas →
puerta de clarificación de la fase 1*; **once ítems** con criterio verificable en el *Checklist* →
*Fase 1 · Maestros*; y **seis ADR** nuevos, del **0024** al **0029**. `CLAUDE.md` estrena los dos
imports del Anexo A.2.3 que entran con la fase y se quedan: `proteccion-datos.md` y
`soft-delete.md`.

Dos cosas que salieron de comprobar las decisiones **contra el código** y que no estaban en la
tanda, las dos averías mudas que la fase 1 activa: el ítem 1.4 **caduca la mitad de las cuatro
cláusulas «DEPENDE DE»** del ADR-0017 (por eso construye un listado y no un `GET` individual), y
`TodaEscrituraDiceComoSeProtegeTests` **fija sus ensamblados a mano**, así que el primer controlador
de Terceros quedaría fuera del barrido sin que nada se pusiera rojo — se arregla en el **1.3**, o
sea antes de que ese controlador exista.

**La decisión que quedaba abierta ya está contestada** (2026-09-03, con el 1.2): quién escribe el
texto que lee una persona cuando falla una validación → **el frontal**, mapeando el `type` estable del
`ProblemDetails`; la API no negocia idioma por `Accept-Language`. **ADR-0030**, con sus tres criterios
en el ítem **1.5** —movidos ahí en el 1.3, y con el mecanismo antes que su primer uso, porque el
catálogo de `type` **no está vacío hoy**— y el motivo del movimiento en *Decisiones tomadas → ítem
1.2*.

**Ítem 1.8 cerrado — el catálogo, y la afirmación que su propio escenario no ejercía:**
run **34424407829** sobre `f90dbed`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `102706467191` ✓ (22 pasos, 0 omitidos),
Frontal `102706467394` ✓ (17 pasos, 0 omitidos) y Humo `102707539606` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 682 casos (682 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Catalogo.UnitTests 25, Arquitectura.Tests 34, Api.IntegrationTests 6
    (651 en el 1.7 sobre `4960922`, en 8 ensamblados: +31 casos y un ensamblado nuevo)

Integración (Testcontainers): 348 casos (348 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 274, y 0 en los otros siete
    (334 en el 1.7 sobre `4960922`: +14 casos)

Frontal: 13 ficheros de prueba, 81 casos, 0 avisos de `act()`
         (109 avisos en 7 ficheros sobre `4960922`: el fondo entero, a cero)
         arranque 410/450 KiB en 3 ficheros · total servido 564/900 KiB  (403/549 en el 1.7)

OpenAPI:  110 operaciones en 64 rutas /api/v1/  (102 en 60 rutas sobre `4960922`)
Catálogo de `type`: 63 tipos, de 68 sitios de llamada  (51 de 56 sobre `4960922`)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 5, Identidad 3, Terceros 2, Catálogo 1
Censo de escrituras: 110 acciones, 72 cambian estado — 40 `If-Match`, 15 `Idempotency-Key`,
                     17 exentas  (102/68/38·13·17 sobre `4960922`)
Artefactos: test-results con 9 .trx y 682 casos (dominio) y 9 con 348 (integración)
            bastion-web-dist con 26 ficheros, 12 .js y 1 .css
Humo: el migrador aplica los CINCO contextos —Auditoría, Organización, Identidad, Terceros y
      Catálogo—, la cuenta sembrada inicia sesión (testigo de 3819 caracteres), el entorno
      desplegado sirve 1 empresa y las semillas cargan 12 tramos de impuesto y 15 unidades
```

> **Toda cifra de «antes y después» de arriba nombra sus dos commits**: el «antes» es `4960922`, que
> es donde el 1.7 dejó las líneas base, y el «después» es `c87a6d2`, que es donde se midieron todas
> en local. Entre `c87a6d2` y `f90dbed` —el commit del run— cambia **un solo fichero,
> `.github/workflows/ci.yml`**, así que ninguna de las cifras se mueve, y el propio run las vuelve a
> publicar idénticas. Las dos medidas de integración se tomaron **con Docker arrancado** —el carril
> corrió entero, 348 de 348— y las del carril rápido con los dos `recuento-de-tests.sh` del workflow,
> que son los que deciden el desenlace del paso y no `dotnet test`.

**Y el ítem destapó un guardián que se había quedado corto dos módulos atrás.** El paso del humo
comprueba **por nombre** que sobre una base recién creada cada contexto con persistencia se migra —por
nombre y no por cuenta, para que el que falte se pueda nombrar—, y su lista seguía siendo
`AuditoriaDbContext, OrganizacionDbContext, IdentidadDbContext` mientras `MigradorDeArranque` había
pasado a **cinco**: Terceros entró en el 1.5 y Catálogo en el 1.8. No fallaba nada; lo que pasaba es
que durante dos módulos **quitar del migrador el contexto de Terceros o el de Catálogo habría dejado
el humo en verde y el contenedor sin su esquema**. Es la misma familia que las tablas del censo y que
la aserción de la mutación 2: una lista escrita a mano solo sabe lo que alguien se acordó de escribir.
Se completa en `f90dbed`, con su propio modo de fallo escrito al lado, y **comprobado por el efecto
antes de tocarlo**: `docker compose -f deploy/docker-compose.yml up --build migraciones` sobre una
base recién creada emite `EsquemaMigrado` para los cinco y el contenedor sale con código 0.

**Las nueve decisiones del ítem están en *Decisiones tomadas → ítem 1.8*, y dos de ellas el enunciado
las pedía nombradas.** El **modelo del árbol** es una **lista de adyacencia** —`PadreId` y nada más—
elegida por lo que este módulo consulta de verdad: el filtro del listado es `WHERE categoria_id = ?`,
una igualdad sobre columna indexada, y el árbol entero de una empresa cabe en una página que el
frontal recompone. La *closure table* cobra su precio en la escritura —mover una rama reescribe las
filas de cierre de todo su subárbol— por una consulta que hoy no se hace; y el día que el subárbol
haga falta (el candidato es la precedencia de tarifas del 1.9) entran un `WITH RECURSIVE` o una
*closure table* **derivada**, ninguna de las dos obliga a migrar el modelo. La consecuencia de la
elección **se dice en pantalla** en vez de esconderse: el filtro acota por la categoría dicha y no
por lo que cuelga de ella, y el mensaje de «ninguno en esta categoría» lo avisa con esas palabras.
La **cota del ascenso** es `Categoria.ProfundidadMaxima = 10` —diez niveles por debajo de la raíz, un
árbol de once— con dos motivos: a más profundidad lo que hay ya no es una clasificación sino un
atributo disfrazado, y sobre todo, la cota es **lo único que convierte un cuelgue en un error** —el
ascenso gasta una consulta por nivel y sobre datos ya cíclicos, sin cota, no falla: gira—.

**Las ocho mutaciones**, cada una sobre árbol verde, aplicada, compilada, corrida y revertida
restaurando una copia. Al terminar, `git status --porcelain` **vacío** y `grep -rn "MUTACION" src
tests` y `grep -rn MUTACION frontend/src` **sin resultados**, los tres comprobados:

| # | Mutación | Dónde | Línea base | Qué se puso rojo |
|---|---|---|---|---|
| 1 | La casilla de `SoloResuelveLoViejo` de la unidad devuelve `Correcto()`: **el alta pasa con una unidad retirada** | `ElMaestroSeOfreceParaLoNuevo.cs` | `9f8ff56` | **Rojo ×2** en `Catalogo.UnitTests` |
| 2 | Se **revalida el maestro que nadie ha tocado** al modificar (fuera la condición `!=`) | `ModificarArticulo.cs` | `9f8ff56` (unidad) · `1eab397` (HTTP) | **Rojo ×1 + ×1** |
| 3 | El ciclo se comprueba **solo en el alta** (fuera la llamada de la modificación) | `ModificarCategoria.cs` | `1eab397` | **Rojo ×2 + ×2** |
| 4 | El **caso degenerado** —padre igual a sí misma— tratado aparte y dejado pasar | `ElArbolSigueSiendoUnArbol.cs` | `1eab397` | **Rojo ×2**: `ElArbolSigueSiendoUnArbolTests.Una_categoria_no_puede_ser_su_propia_madre` y `ElCicloSeCompruebaEnLasDosPuertasTests.Una_categoria_no_puede_pasar_a_colgar_de_si_misma` — uno por puerta |
| 5 | El **ascenso pierde la cota** (`for … <= ProfundidadMaxima` → `while (true)`) | `ElArbolSigueSiendoUnArbol.cs` | `1eab397` | **Rojo ×3, y el del ciclo en 1 ms**: `Un_ciclo_YA_GUARDADO_no_deja_el_recorrido_dando_vueltas` con «*el recorrido de padres ha pedido 101 eslabones, más de los 100 que este doble admite […] lo que hay es un ascenso SIN COTA sobre datos que ya tienen un ciclo*», más `Colgar_un_nivel_mas_abajo_se_rechaza_por_profundidad` y `El_alta_demasiado_honda_se_rechaza_tambien`, que se quedan sin rechazo. **El caso lo demuestra; no cuelga la suite** |
| 6 | El alta **guarda la unidad sin preguntarle a su puerto** | `CrearArticulo.cs` | `1eab397` | **Rojo ×4** en `LaCasillaDeLaRetiradaTests`: las tres casillas de la unidad más `Un_estado_que_esta_casilla_no_conoce_lanza_en_vez_de_dejar_pasar`. Y **`LosIdentificadoresAjenosTests` sigue en verde**, que es lo correcto y hay que decirlo: esa regla vigila que el cruce esté **declarado con su puerto**, no que la respuesta del puerto **decida**. Las dos mitades son de dos sitios distintos |
| 7 | `CatalogoDbContext` **deja de heredar de `ContextoDeModulo`** y se cae del universo descubierto | `CatalogoDbContext.cs` | `1eab397` | **Rojo ×4** en `Api.FunctionalTests`, con `El_universo_de_modelos_es_el_declarado` diciendo lo que encontró: `"ContextoDeLaBandeja"` donde esperaba los seis. Y con él, dos reglas de modelo y —lo que más importa— **el arnés** `ElCatalogoNoGuardaDatosDeNadieTests.El_barrido_ve_catalogo_y_sabe_reconocer_un_dato_personal`: sin ese arnés, las reglas de «cada entidad declara…» habrían seguido verdes mirando cinco módulos de seis |
| 8 | `UnidadBaseId` pasa a ser **clave ajena a `organizacion.unidades_de_medida`** | `CatalogoDbContext.cs` | `1eab397` | **Rojo ×3**, con `Ninguna_clave_foranea_cruza_de_esquema` nombrándola entera: `["Articulo.fk_articulos_unidades_de_medida_unidad_base_id: catalogo -> organizacion"]`. Y en el **carril rápido**, sin Docker: el modelo de EF sabe a qué esquema va cada tabla antes de que exista ninguna |

**La 1, la 2 y la 3, enteras.**

**Mutación 1 — el alta acepta una unidad retirada.** Cambiar la casilla de `SoloResuelveLoViejo` por
`Resultado.Correcto()` pone rojos **dos** casos, y el segundo es el que importa:
`Con_la_unidad_solo_resolviendo_lo_viejo_el_alta_se_rechaza`, que es el directo, y
`Con_la_unidad_inexistente_el_alta_se_rechaza_con_OTRO_error`, que compara los dos `type` y se cae
porque ya no hay dos. Esa segunda es la que sostiene el ADR-0030 aquí: quien teclea un identificador
inventado tiene que corregir el identificador y quien apunta a uno retirado tiene que elegir otra
unidad, son dos arreglos distintos, y con un solo `type` el frontal escribiría un texto que sirve
para uno y despista en el otro. La misma mutación por HTTP la caza
`ContratoDeCatalogoTests.Una_unidad_RETIRADA_no_vale_para_un_alta_y_el_articulo_que_ya_la_usa_sigue_resolviendola`,
que es el caso de las dos mitades contra PostgreSQL.

**Mutación 2 — el artículo que ya usaba el maestro deja de poder corregirse, y aquí la tanda encontró
un agujero de verdad.** Quitar la condición `!=` de `ModificarArticulo` —o sea revalidar el maestro
que nadie ha tocado— puso rojo `LaCasillaDeLaRetiradaTests.El_impuesto_que_no_se_toca_no_se_vuelve_a_preguntar`
y **dejó verde** el caso por HTTP que afirma lo mismo,
`Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_pero_cambiarlo_a_uno_caducado_SI_se_rechaza`.
El motivo: el artículo llegaba a la modificación con su tramo **todavía vigente**, y con eso
revalidarlo y no revalidarlo se ven exactamente igual desde fuera. La aserción afirmaba una decisión
que su propio escenario no ejercía — la misma familia que el «antes» mal medido de los `act()` del
1.5 y que las tablas del censo. Arreglado en `1eab397`: el tramo del artículo se cierra con
`POST /impuestos/{id}/cierre` y el último día en ayer, antes de la modificación que no lo toca. Con
el caso ya endurecido, la mutación lo pone en **409** y el caso en rojo.

> **Y la mitad de la UNIDAD no se puede romper desde `ModificarArticulo`, que es la forma más fuerte
> de la garantía y hay que decirla como es.** La unidad base no está en `ModificarArticuloDto` ni en
> `Articulo.Modificar`, y el puerto de unidades **no está inyectado** en ese caso de uso: unificar la
> comprobación entre el alta y la modificación no es una línea, es cambiar la firma. Por eso la
> mutación se ejerce por el maestro que **sí** se puede cambiar —el impuesto—, que es el mismo
> defecto por el único sitio por donde entra. La mitad de la unidad la sostiene, contra PostgreSQL,
> el caso de las dos mitades del contrato.

**Mutación 3 — el ciclo comprobado solo en el alta.** Quitar la llamada a
`ElArbolSigueSiendoUnArbol.ComprobarAsync` de `ModificarCategoria` pone rojos **dos casos en cada
carril**: en el rápido, `ElCicloSeCompruebaEnLasDosPuertasTests.Mover_una_rama_debajo_de_su_propia_descendencia_se_rechaza`
y `.Una_categoria_no_puede_pasar_a_colgar_de_si_misma`; por HTTP y contra PostgreSQL,
`ContratoDeCatalogoTests.Mover_una_categoria_debajo_de_su_propia_descendencia_es_409_con_type_categoria_ciclo`
y `.Una_categoria_no_puede_colgar_de_si_misma_ni_al_crearla_ni_al_moverla`. **Y el alta se queda
entera en verde**, que es exactamente el punto: en el alta la rama del ciclo es estructuralmente
inalcanzable —la categoría que nace todavía no está en el árbol—, así que una comprobación que solo
mirase ahí dejaría pasar **el único camino por el que un ciclo entra**, y lo dejaría pasar sin un
solo rojo. Es la misma forma del hueco que el 1.7 le encontró a la inversa, por su otra cara.

**Lo que el módulo nuevo tuvo que descubrirse solo, y se descubrió.** Ninguna de las reglas
transversales necesitó que se le apuntara Catálogo a mano: las cinco que recorren el modelo entero
—inquilinato, auditoría, claves, fechas y la de claves ajenas entre esquemas— lo ven por el universo
descubierto de `LosModelosDeCadaModulo`, que es «toda clase concreta que hereda de
`ContextoDeModulo`»; la matriz puerto × estado del 1.7 lo ve porque descubre las interfaces del
`Contracts` que devuelven `Task<EstadoDeMaestro>`; y los tres universos del 1.3 y el 1.4 —fronteras,
censos de carril y rutas del frontal— comparan sus listas contra el disco en los dos sentidos. Lo
único que se escribe a mano es el **censo de casos de `Api.FunctionalTests` y `Api.IntegrationTests`**
y la lista `Declarados`, y las dos son a propósito: son el arnés que impide que un descubrimiento que
deje de encontrar a nadie salga verde recorriendo una lista vacía. La mutación 7 es la prueba de que
ese arnés funciona.

**Ninguna dependencia nueva, y la frase sale del diff.** Conjuntos de paquetes **resueltos** de todos
los `packages.lock.json` a los dos lados: **150 en `main` y 155 en la rama**, y las cinco de
diferencia son `bastion.catalogo.domain`, `.contracts`, `.application`, `.endpoints` e
`.infrastructure`, las cinco con `"type": "Project"` — proyectos de este repositorio, no paquetes.
`Directory.Packages.props` no se toca, y `git diff --stat main..HEAD -- frontend/package.json
frontend/package-lock.json` no devuelve **ni una línea**. **No hay licencias que comprobar porque no
hay paquete de terceros que comprobar.**

**Commits, firmas y trailers.** `git rev-list --count main..HEAD` → **10** sobre `f90dbed`, que es el
commit del run; **11** contando el que escribe este bloque. Todos con `%G?` = `G`. Y el comprobante
que el ítem pedía, `git show -s --format='%(trailers:only)'` recorrido sobre el rango entero:
**ninguno devuelve nada** — ni un trailer, ni de sesión ni de prosa. El aviso de `SIGNING_KEY_B64` que
suelta el enganche de arranque es un falso positivo comprobado: `git config --global commit.gpgsign`
es `true` y las firmas lo confirman.

**Fuera del ítem, y no «de paso»:** `CodigoBarras` (fase 2, con su import), las tarifas (1.9), los dos
cruces mutuos incluido `ArticuloProveedor` (1.10), la importación CSV (1.11). Nada de existencias ni
de movimientos.

**Ítem 1.7 cerrado — la retirada, las dos conversiones, y la lección por cuarta vez:**
run **34177571670** sobre `dd89e9f`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `101909932819` ✓ (22 pasos, 0 omitidos),
Frontal `101909932714` ✓ (17 pasos, 0 omitidos) y Humo `101910637516` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 651 casos (651 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Organizacion.IntegrationTests 21,
    Api.FunctionalTests 140, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Arquitectura.Tests 34, Api.IntegrationTests 6
    (630 en el 1.6, los mismos 8 ensamblados: +21 casos)

Integración (Testcontainers): 334 casos (334 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 260, y 0 en los otros seis
    (318 en el 1.6: +16 casos, y esta vez los 334 se ejercieron TAMBIÉN en local)

Artefacto test-results: 8 ficheros .trx con 651 casos (dominio) y 8 con 334 (integración)

Frontal: arranque 403/450 KiB en 3 ficheros · total servido 549/900 KiB  (548 en el 1.6)
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  102 operaciones, 60 rutas /api/v1/, y el fichero del artefacto idéntico al del
          repositorio  (93 operaciones y 55 rutas en el 1.6)
Catálogo de `type`: 51 tipos, de 56 sitios de llamada  (49 de 54 en el 1.6)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 5, Identidad 3, Terceros 2
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 3601
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

> **El run de arriba es sobre `dd89e9f`, y a `main` va `f9b5825`: hay que decirlo y hay que
> justificarlo.** `f9b5825` es el commit que escribe este mismo bloque, así que no puede citar su
> propio run. Entre los dos, `git diff --stat dd89e9f f9b5825` da **un solo fichero, `docs/PLAN.md`**:
> ni una línea de código, de test, de contrato ni de migración. Y `f9b5825` tiene su propio run en
> verde, el **34178017980**, también con los 3 jobs contados en él — Backend `101911192453` ✓ (22
> pasos, 0 omitidos), Frontal `101911192553` ✓ (17, 0) y Humo `101911925167` ✓ (24, 1 omitido)—. Las
> cifras de arriba valen para el árbol que se lleva a `main` porque el código que las produce es
> idéntico, y la comprobación que lo sostiene está escrita aquí para que se pueda repetir. La
> alternativa —dar el run sin decir sobre qué árbol se midió— es justo lo que la regla del 1.5
> prohíbe.

**La lección, por cuarta vez, y la primera en la que se vio ANTES de escribir el arreglo.** Las tres
anteriores se descubrieron arreglando otra cosa; esta se buscó a propósito, porque el enunciado del
ítem ya la traía: *una obligación transversal enumerada a mano es ciega a lo que llegue después, y en
verde no la mira nadie*. Aquí la obligación era un **enumerado de tres valores** cuyo tercero
—`SoloResuelveLoViejo`— era inalcanzable para dos de sus tres productores, con el comentario de los
dos diciendo «la tercera llega con la retirada» y **nada** comprobando que llegara. La regla se
escribió primero, se commiteó **en rojo** (`cf46416`) nombrando las dos casillas, y se puso verde con
la retirada. Es la diferencia entre arreglar un defecto y **dejar puesto lo que lo habría avisado**.

**Y la forma que se lleva el ítem es la de la matriz, no la de la retirada.** `EstadoDeMaestro` es
una lista cerrada; los puertos que la producen, un conjunto abierto. La regla descubre los dos lados
—los valores del enumerado y las interfaces del `Contracts` que devuelven `Task<EstadoDeMaestro>`— y
exige que cada casilla del producto tenga un caso que la afirme, con la cobertura marcada **sobre el
caso** y no en una lista aparte: borrar el caso borra el cubrimiento, y la matriz se pone roja por la
casilla que se quedó sin nadie. Un puerto nuevo entra con sus tres casillas por cubrir sin que nadie
lo apunte. Es la misma pregunta del 1.6 —¿la lista cerrada y el conjunto que enumera se comparan
enteros?— por su otra cara: allí **faltaban** valores, aquí **sobraba** uno.

**Lo que este ítem añade al catálogo de trampas, y no estaba en el enunciado.** Con el `GET` por
identificador contestando 404 a una fila retirada —la mutación 3—, cae también **reincorporar**: la
puerta de vuelta necesita el `ETag`, y el `ETag` sale del `GET` que acaba de decir que no existe. En
cuatro maestros de instalación (R8), donde retirar por error deja sin esa fila a **todas** las
empresas, tratar la retirada como un bloqueo no solo esconde el dato: **convierte un error reversible
en uno permanente**. La línea que separa las dos cosas no es una cortesía con las facturas viejas —es
lo que sostiene la marcha atrás.

**Y una decisión de sitio que costó más ver que las tres del ADR.** La desigualdad de la inversa y la
negativa a encadenar tenían su **único** testigo detrás de Testcontainers. Ninguna de las dos abre una
conexión: son aritmética y una rama. Con Docker parado, cambiar el `≤` por un `<` compilaba, salía
verde, y el aviso llegaba en el *runner*. **Un caso frontera al que solo se llega levantando un
contenedor es un caso frontera que nadie ejerce mientras escribe el cambio que lo rompe** — y por eso
el testigo bajó al carril rápido, como segunda excepción declarada de `Organizacion.IntegrationTests`.

**Ítem 1.7 — la retirada y las dos conversiones.** El ADR-0023 entero, que llevaba escrito y sin
implementar desde la puerta de clarificación de la fase 1, y **antes** de que Catálogo referencie
`UnidadMedida`: ese era el disparador que el propio ADR dejó puesto.

**La lección del ítem, por cuarta vez, y esta vez se vio antes de escribir el arreglo.** Una
obligación transversal enumerada a mano es ciega a lo que llegue después, y en verde no la mira
nadie. Aquí la obligación era `EstadoDeMaestro`: tres valores, y `SoloResuelveLoViejo` lo contestaba
**solo** `ConsultaDeImpuestos`. Divisas y unidades no podían contestarlo —el comentario de las dos
lo decía, «la tercera llega con la retirada», sin que nada comprobara que llegaba—, así que la lista
cerrada tenía un valor que para dos de sus tres productores era inalcanzable. La regla que lo caza
—`LaMatrizDePuertoYEstadoTests`— se escribió **primero y se commiteó en rojo** (`cf46416`), con las
dos casillas nombradas, y se puso verde con la retirada.

**La forma de esa regla es lo que se lleva el ítem.** Los dos lados se descubren y ninguno es una
lista: los **valores**, del enumerado; los **puertos**, de lo que el `Contracts` publica —toda
interfaz con un método que devuelva `Task<EstadoDeMaestro>`—; y las **casillas cubiertas**, de los
`[CubreEstadoDeMaestro]` puestos **sobre el caso que las afirma**, no en una lista aparte. Un puerto
nuevo entra solo, con sus tres casillas por cubrir; un caso borrado se lleva su casilla por delante y
la matriz se pone roja. Y la matriz se afirma **no vacía** antes de compararla, que es la rendija por
la que un cero contra cero sale verde.

**La retirada, en los cuatro maestros de instalación.** `Divisa`, `TipoCambio`, `UnidadMedida` y
`ConversionUM` implementan `IRetirable` —la marca que hace de lista, igual que `IBloqueable`—. El
comportamiento es el literal del ADR: no se ofrece para operaciones nuevas, sigue resolviendo para lo
que ya apunta a ella; el `GET` de la colección la excluye por omisión y el `GET` por identificador
**la sigue devolviendo**. Y **ningún `DELETE` de la fila, para ninguno de los cuatro, nunca**: la
prohibición la vigila `NingunMaestroRetirableSeBorra`, que descubre los dos universos —las entidades
marcadas en los ensamblados de dominio del disco, y los recursos cuyo listado devuelve una página con
`Retirada`— y compara sus tamaños antes de recorrerlos.

**La exclusión vive en UN sitio.** `Paginador.SinLasRetiradas` monta la expresión `!fila.EstaRetirada`
cuando el elemento es `IRetirable` y no se han pedido las retiradas. No hay cuatro repositorios
acordándose: hay una consulta, y un quinto maestro retirable la hereda sin que nadie lo apunte.

> **Decisión A — cómo se ven las retiradas: un parámetro del listado de siempre,
> `?retiradas=true`, y no un extremo aparte.** Se decide **por contraste con el ADR-0027**, que es
> lo que impide copiarlo por parecido: lo que hace del listado de lo bloqueado un extremo separado,
> nominativo y trazado, **no** es que las filas estén escondidas — es que expone **datos del
> artículo 32**. Una divisa retirada no tiene datos de nadie: verla no es un acceso reservado, es
> mirar el maestro. Pasa por el contrato de listado del 1.3, y `retiradas` **no es un criterio
> sensible** —no lleva identificador, ni nombre, ni nada de una persona—, así que puede viajar en la
> URL; `NingunCriterioSensibleViajaEnLaUrl` declara ahora **cinco** nombres (`page`, `q`,
> `retiradas`, `size`, `sort`) con ese motivo escrito en el propio caso.

> **Decisión B — `TipoCambio` y `ConversionUM` no tienen puerto de consumo, y hay que decirlo.**
> `EstadoDeMaestro` lo contestan `IConsultaDeDivisas`, `IConsultaDeUnidadesDeMedida` e
> `IConsultaDeImpuestos`; los otros dos maestros no exponen puerto para otros módulos porque hoy no
> los consume nadie. **Esto no convierte su retirada en vacuidad del ADR-0020:** es observable por
> sus propios `GET` —el de la colección deja de ofrecerlas y el del identificador las sigue
> devolviendo—, y eso es un camino real, ejercido por HTTP contra PostgreSQL. Pero la frase queda
> escrita, porque el día que Catálogo o Facturación pidan un puerto para conversiones, la matriz
> puerto × estado les exigirá sus tres casillas sin que nadie tenga que acordarse.

**La inversa plausible (decisión 2 del ADR).** Con `f` y `g` ya redondeados a seis decimales,
`|f·g − 1| ≤ 5·10⁻⁷·(f + g)`. En la **capa de aplicación** y al escribir, no como invariante de
dominio: la regla relaciona **dos instancias distintas** del agregado, y un invariante que cargara
otro agregado sería justo la grieta que la R12 cierra. El fallo es un error de negocio con nombre
—`conversion-um-inversa-implausible`, 409—, no una excepción (ADR-0004). Y la tolerancia **se
calcula**: `new decimal(5, 0, 0, false, DecimalesDelFactor + 1)`, para que no queden dos sitios
diciendo lo mismo sin nada que los ate.

**Se comprueba en el alta Y en la modificación**, y lo segundo no es un extra: si se da de alta
`A→B`, luego `B→A`, y después alguien **modifica** la primera, el par vuelve a poder contradecirse.
Un test que solo ejerza el alta deja pasar exactamente el camino por el que se rompe, y lo deja pasar
en verde. La mutación 5 es esa.

> **Decisión C — una fila retirada SIGUE restringiendo a su inversa.** «Sigue resolviendo» significa
> que sigue siendo verdad, así que sigue acotando. Va escrita y no puesta por omisión, porque la
> alternativa tiene una consecuencia concreta: si retirar una fila la sacara de la comprobación,
> **retirar el sentido incómodo sería la manera de declarar cualquier número en el otro** — y una
> salida que la propia regla ofrece no es una salida, es un agujero con permiso. Traducido a
> consulta: `DelParAsync` trae también las retiradas, y sus dos clientes —el resolutor y la
> comprobación de la inversa— la necesitan así por motivos distintos.

> **El enunciado del ítem daba `g = 0,083334` por aceptada para `f = 12`, y la desigualdad la
> rechaza. Manda la desigualdad, que está fijada y no se toca.** Los números: `|12 × 0,083334 − 1| =
> 8·10⁻⁶`, contra un margen de `5·10⁻⁷ × 12,083334 = 6,0416670·10⁻⁶`. Y el rechazo **es lo
> correcto**: `1/12 = 0,08333333…`, así que `0,083334` se separa `6,67·10⁻⁷` del valor real —más de
> media unidad del último decimal— y por tanto **no es** un redondeo de `1/12` a seis decimales;
> `0,083333`, a `3,33·10⁻⁷`, sí lo es y **se acepta**. Ensanchar la tolerancia para que entrara sería
> exactamente el «número elegido por comodidad» que el ADR prohíbe. Los tres casos están escritos con
> su aritmética en `LaAritmeticaDeLaInversaTests`.

**El caso frontera, que es donde una desigualdad se equivoca de signo.** `f = 1,001` y `g = 0,999`:
`f·g = 0,999999`, se separan `10⁻⁶`, y el margen es `5·10⁻⁷ × 2,000 = 10⁻⁶` — **exactamente igual**.
Con `≤` entran; con `<` no. Y hay un caso aparte que **afirma esa igualdad**, porque un frontera que
no esté en la frontera no separa los dos signos: sería un par cualquiera dentro del margen, y la
mutación pasaría en verde. El vecino `g = 0,998999` se separa `2,001·10⁻⁶`, que además cae por debajo
de `10⁻⁵`: por eso el mismo caso caza también la tolerancia relajada un orden de magnitud.

**El resolutor (decisión 3 del ADR).** `GET /api/v1/organizacion/conversiones-um/resolucion` con las
dos unidades. Pedir un par no declarado es `conversion-um-no-declarada`, un **404**. **Nunca un cero,
nunca un nulo, nunca el producto de la cadena**: las tres son las tres peores maneras de equivocarse
en un inventario —un cero convierte las existencias en nada, un nulo se propaga sin ruido, y encadenar
multiplica el error de redondeo y hace que el número dependa de qué camino elija el buscador—. El caso
que importa **no** es el par declarado: es `kg→g` y `g→mg` declarados, `kg→mg` preguntado, y que salga
el error con nombre y **no un 1000000** —comprobado también sobre el cuerpo de la respuesta, que no
puede llevar ese número ni de adorno—. Ni identidad ni simetría: `kg→kg` falla igual, y `B→A` no se
deduce invirtiendo `A→B`.

**La ruta nueva pasa por los tres universos del 1.3 y el 1.4 y por el censo, sin apuntarla a mano en
ninguno**: tabla de enrutado, testigo de versión en el cuerpo, criterio sensible en la URL, y el
censo de reglas de su carril. Su `type` entra en el catálogo del ADR-0030 y el frontal escribe el
texto desde el `type`, en `es.ts` y `en.ts`.

**Nueve puertas nuevas, cada una con su permiso y su prohibición.** Ocho constantes nuevas en
`PermisosDeOrganizacion` —retirar y reincorporar son permisos **distintos**, como `cerrar`/`reabrir`:
los cuatro maestros son de instalación (R8), así que retirar por error deja **a todas las empresas**
sin esa fila, y deshacerlo tiene que poder autorizarse aparte—. `TodaEscrituraDiceComoSeProtege`
cuenta ahora 102 acciones, 68 que cambian estado, con 38 exigiendo versión, 13 admitiendo
idempotencia y 17 exentas declaradas: `38 + 13 + 17 = 68`, y esa suma es la aserción.

**Un defecto que salió de comprobar la decisión 6 contra el código, y que hay que decir entero.** El
`[Range(0.000001, 1000000)]` de los DTO significaba que el redondeo-a-cero del factor **no era
alcanzable por HTTP**. Lo era desde cualquier otro llamante y, sobre todo, era una **promesa falsa**:
una guarda de dominio que decía impedir algo que solo impedía el borde. El rango vive ahora en el
dominio —`FactorMinimo` y `FactorMaximo`, con su porqué escrito— y la validación redondea **primero**
y comprueba el número que se va a guardar; una regla barrida compara los cuatro literales del
contrato contra las dos constantes, sin enumerar DTO.

**Y una decisión de sitio, que es la que más cuesta ver.** La desigualdad de la inversa y la negativa
a encadenar tenían su **único** testigo en `Api.IntegrationTests`, detrás de Testcontainers. Pero ni
una comparación entre dos `decimal` ni la rama que decide no componer una cadena abren una conexión:
son *la parte del carril que no necesita el carril*, exactamente como `LaTraduccionASqlTests` desde el
1.3. Con Docker parado —que es como estaba esta máquina al empezar—, cambiar el `≤` por un `<`
compilaba, salía verde, y el aviso llegaba minutos después y en otro sitio. **Un caso frontera al que
solo se llega levantando un contenedor es un caso frontera que nadie ejerce mientras escribe el cambio
que lo rompe.** Se añade `LaAritmeticaDeLaInversaTests` como **segunda excepción declarada** de
`Organizacion.IntegrationTests`, con `InternalsVisibleTo` de la capa de aplicación hacia ese
ensamblado nombrado. No sustituye a los casos por HTTP —allí se comprueba que el rechazo llega como
409 y que el resolutor contesta 404—: aquí se comprueba el número, y ninguna de las dos afirmaciones
implica a la otra.

**Lo que el ítem no toca**, y se comprueba en el `git diff`: catálogo, artículo, categoría, tarifas,
cruces mutuos, importación. **Nada de `Impuesto`**: su `/cierre` es correcto y el ADR dice
expresamente que se queda como está.

**Ítem 1.6 cerrado — lo que cuelga del tercero, y la misma lección tres veces:**
run **34125157573** sobre `911ba5c`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `101752010394` ✓ (22 pasos, 0 omitidos),
Frontal `101752010729` ✓ (17 pasos, 0 omitidos) y Humo `101753229252` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 630 casos (630 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.UnitTests 182, BuildingBlocks.UnitTests 132, Organizacion.IntegrationTests 4,
    Identidad.UnitTests 58, Api.FunctionalTests 137, Api.IntegrationTests 6,
    Arquitectura.Tests 34, Terceros.UnitTests 77
    (576 en el 1.5, los mismos 8 ensamblados: +54 casos)

Integración (Testcontainers): 318 casos (318 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 246, y 0 en los otros seis
    (271 en el 1.5: +47 casos, que son los que en esta máquina NO se pudieron ejecutar)

Frontal: arranque 403/450 KiB en 3 ficheros · total servido 548/900 KiB  (402 en el 1.5)
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  93 operaciones, 55 rutas /api/v1/, y el fichero del artefacto idéntico al del
          repositorio  (82 operaciones y 47 rutas en el 1.5)
Catálogo de `type`: 49 tipos, de 54 sitios de llamada  (47 de 52 en el 1.5)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 4, Identidad 3, Terceros 2
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 3204
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

**El ítem se cierra con una lección que le salió tres veces, y la tercera ya sin excusa.** Las tres
son la misma: **una obligación transversal enumerada a mano es ciega a lo que llegue después**, y
mientras esté en verde nadie la mira. (1) El listado del artículo 32 enumeraba tres agregados de
cinco, y el enumerado vivía dentro de un módulo. (2) Las cinco reglas que recorren el modelo entero
enumeraban sus contextos a mano y ninguna incluía Terceros, así que cuatro reglas que dicen «cada
entidad declara…» lo estuvieron diciendo de dos módulos de cinco. (3) Los dos ADR del propio ítem
salieron con el mismo número, porque la numeración no la miraba nadie. Las tres se cierran igual:
**el universo se descubre**, con su lista declarada al lado para que un descubrimiento corto salga
rojo en vez de silencioso. Lo que no arregla ninguna de las tres es la cuarta que todavía no se ha
visto — y por eso la forma importa más que los arreglos.

**La primera mitad — el agujero del art. 32, que es un defecto de cumplimiento en código
ya entregado.** Cinco agregados del proyecto se bloquean (`Empresa`, `Almacen`, `Ubicacion`,
`Tercero`, `Usuario`) y el listado del art. 32 veía **tres**. `Usuario` faltaba desde el **1.4** —una
persona física que ejerce su derecho de supresión, el caso más nítido del artículo— y `Tercero` desde
el **1.5**. Está contado entero en **ADR-0031**; lo esencial:

- **La regla va primero y se mira roja.** `LoBloqueadoSeVeEnteroTests` compara
  `TipoDeRecursoBloqueado` contra los tipos que implementan `IBloqueable`, en las dos direcciones y
  con su ancla. Escrita **antes** del arreglo sale roja con **dos supervivientes a la vez**, que es
  la mutación 1 de la tabla del ítem. La doctrina ya estaba escrita —en el comentario de la propia
  `IBloqueable`, desde el día que se escribió— y el barrido que la ejerce no existía: la lista se
  quedó vieja **dos ítems seguidos** con la suite en verde.
- **Y una segunda regla, porque la primera se puede cumplir sin arreglar nada.**
  `Todo_modulo_que_bloquea_contesta_por_lo_suyo`: el enumerado es una lista de nombres y se pone
  verde en cuanto alguien añade el valor. Un valor declarado que ningún módulo contesta es **peor**
  que el agujero — el contrato promete un tipo de recurso que no aparece nunca, así que quien audite
  leerá «no hay usuarios bloqueados» en vez de «nadie los está buscando».
- **El arreglo no inventa arquitectura.** Cada módulo que bloquea expone `IConsultaDeLoBloqueado` y
  el listado compone. El puerto vive en el **bloque común** y no en el `Contracts` de nadie, porque
  aquí hay varios dueños y un consumidor; el consumidor resuelve `IEnumerable<…>`, así que un módulo
  nuevo entra registrando su implementación. Sin `JOIN` entre esquemas y sin llamada HTTP — y **sin
  cruce nuevo que declarar**: al estar el puerto en el bloque común no aparece ninguna referencia de
  proyecto entre módulos.
- **El repositorio anterior llevaba escrito por qué esto «no se podía hacer», y la mitad era falsa.**
  Decía que no hay forma de saber cuántas filas traer de cada fuente sin traerlas todas. Para la
  página *k* bastan las `salto + tamaño` primeras de **cada** fuente: es la cota de una fusión
  k-vías, y no es asintóticamente peor que el `UNION` con `OFFSET` que sustituye.
- **Dos defectos los encontró el barrido nuevo, antes de salir de la rama.** El de traducción a SQL
  se mudó de `Organizacion.IntegrationTests` a `Api.IntegrationTests` —allí «entero» habría pasado a
  significar un tercio, que es el mismo defecto en pequeño— y en cuanto miró los tres módulos puso
  rojas las nueve combinaciones de orden de Identidad: **(a)** una condición de membresía escrita a
  mano duplicaba un invariante que el filtro global de `Usuario` ya impone, y sumada a la navegación
  que ese filtro expande no se traducía; **(b)** `Codigo = usuario.Correo.Valor` tampoco, porque
  `Correo` se mapea con conversor y `.Valor` no es una columna. Habrían sido nueve `?sort=`
  convertidos en un 500 justo en la pantalla que se abre para rectificar un bloqueo por error.
- **En este listado no viaja ningún identificador de persona:** ni el NIF del tercero ni el correo
  del usuario. Para levantar un bloqueo hecho por error bastan el nombre y el identificador de la
  fila, que es lo que el desbloqueo pide.
- **`BuildingBlocks.Infrastructure` estrena el proveedor de Npgsql**, porque el filtro compartido usa
  `ILIKE`. Su `.csproj` decía «EF Core, no el proveedor: este proyecto no sabe contra qué base
  corre», y la frase **ya era falsa cuando se escribió** —dos párrafos más abajo admitía mapear una
  columna `jsonb`—. Se corrige la frase en vez de esconder el hecho detrás de tres copias del filtro.

Carril rápido en local sobre esta mitad, con el comando que lo mide
(`dotnet test Bastion.sln -c Release --no-build --filter "Category!=Integracion" --logger trx` +
`scripts/ci/recuento-de-tests.sh`):

```
Dominio y arquitectura: 582 casos (582 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Terceros.UnitTests 39,
    Organizacion.UnitTests 182, Organizacion.IntegrationTests 4, Arquitectura.Tests 27,
    Api.FunctionalTests 136, Api.IntegrationTests 4
```

Contra los **576 en 8 ensamblados** del 1.5 (`279b8c7`): +1 regla de arquitectura (23→27 incluye las
tres del barrido nuevo del art. 32 más la del puerto), +3 casos en `Api.IntegrationTests` (el barrido
de traducción, que corre sin Docker) y −1 en `Organizacion.IntegrationTests` (el caso que se mudó).
**Integración no se ha podido ejercer en local: no hay Docker en esta máquina.** Lo que sí se ejerció
en su lugar está en el informe del ítem.

Mutaciones de esta mitad, **cada una con su línea base nombrada**, sobre árbol limpio y revertidas
sin `git checkout --`:

| # | Mutación | Línea base | Resultado |
|---|---|---|---|
| 1 | La regla de la lista cerrada, **antes del arreglo** | `e3a9e9e` (main al abrir la rama) | 🔴 `["Tercero", "Usuario"]` — los dos supervivientes a la vez |
| 2 | `ConsultaDeLoBloqueadoDeTerceros` deja de implementar el puerto (y se comenta su `AddScoped`) | `f9913d9` (la rama, con el arreglo) | 🔴 `mudos: ["Terceros"]` |

La **1** se midió en un árbol aparte de `e3a9e9e` con la regla copiada dentro y nada más: es
literalmente «la regla, antes del arreglo». Salieron **2 verdes y 1 roja** de 3 — los verdes son el
ancla (ve tres agregados bloqueables, así que no se cumple en vacío) y la dirección «ningún valor
sobra».

La **2** deja ver por qué hacen falta las dos reglas:
`Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32` se quedó **verde** con la mutación
puesta. El valor `Tercero` seguía declarado y nadie lo contestaba — el falso verde exacto que la
segunda regla existe para cazar.

**El «antes» de los `act()`, medido donde toca.** La regla nueva del ítem dice que toda cifra de
antes y después nombra el commit de cada extremo, y que el «antes» es **main al abrir la rama**:

```
e3a9e9e (main al abrir) — 109 avisos `not wrapped in act`, en 7 ficheros de prueba
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
(11 ficheros, 63 casos, todos verdes)
```

Y eso **corrige el «antes» que se dio en el 1.5**: allí se apuntaron «91 en seis ficheros» como
punto de partida, pero se midieron sobre un árbol que ya llevaba el código del ítem. Medido sobre
main limpio salen **109 en siete**, que es la misma cifra que el «después» de aquel ítem: los
dieciocho avisos **no los trajo el 1.5**. La comparación de entonces no demostraba lo que decía —
que es exactamente el motivo por el que la regla se escribió.

**Ítem 1.6, segunda mitad — lo que cuelga del tercero.** Tres entidades hijas (`Contacto`,
`CuentaBancaria`, `CondicionPago`), un valor opcional en la propia ficha (`LimiteCredito`) y el
régimen fiscal del §7.2. Once casos de uso, un controlador, once rutas nuevas y siete permisos.

- **El IBAN se valida de verdad, y la batería prueba las tres negativas por separado.** `Iban` vive
  en `BuildingBlocks.Domain.Identificacion`, al lado de `Nif`: mod-97-10 sobre la cadena reordenada
  con las letras convertidas a dígitos, **más la longitud por país**, que no es la misma en todos.
  La tabla son los **37 países de la zona SEPA** y no el registro entero de la ISO, con el motivo
  escrito: aceptar los ochenta y pico haría creer que esta instalación puede domiciliar un adeudo
  contra cualquiera de ellos. Los casos se generan **con su control calculado** —`IbanesInventados`—
  y se ejercen en las dos direcciones: que acepte los válidos de los 37 países, y que rechace **por
  cada motivo distinto** con su caso propio (`Un_control_cambiado_se_rechaza`,
  `Una_longitud_de_otro_pais_se_rechaza_aunque_el_control_cuadre`,
  `Un_pais_que_no_existe_se_rechaza_aunque_el_control_cuadre`). Un negativo genérico habría pasado
  con cualquiera de las tres comprobaciones puesta y las otras dos quitadas.
- **El barrido del 1.5 se extiende al IBAN, y cambia de forma para poder hacerlo.**
  `NingunDatoConFormaDeRealTests` es una **lista cerrada de formas** —hoy dos: IBAN e identificador
  fiscal español— comparada entera en los dos sentidos contra los detectores implementados, con su
  afirmación de que cada detector encuentra algo y de que el barrido está leyendo ficheros de verdad.
  La decisión de diseño está escrita en su cabecera: **la lista se declara por lo que se PERMITE**,
  no por lo que se prohíbe, porque una lista de valores prohibidos tendría que llevarlos escritos —y
  escribir un IBAN real para prohibirlo es exactamente el daño—. Cada forma declara su **espacio
  inventado** y todo lo que tenga la forma y esté fuera es rojo. Por lo mismo, **los mensajes no
  publican lo que encuentran**: fichero, línea y forma enmascarada, porque un registro de CI que
  imprime el IBAN cazado contiene justo lo que la regla existe para sacar del repositorio.
- **Los sesenta días son un invariante del dominio, no un valor por omisión.** `CondicionPago`
  guarda **el plazo** —`DiasMaximosDePlazo = 60`— y lo exige el dominio lanzando, la aplicación
  adelantándolo como error de campo para que se pueda explicar, y la base con
  `ck_condiciones_pago_plazo_legal`. Tres sitios, y el que manda es el del dominio: la interfaz que
  no ofrece 90 no impide guardarlo desde una importación de CSV o desde un test. Los dos lados del
  límite se prueban **en el mismo fichero** (`[InlineData(0, 30, 60)]` acepta,
  `[InlineData(61, 90, 120)]` rechaza), para que un `>` escrito donde va un `>=` no se pueda
  esconder. **Y aquí no se calcula ningún vencimiento:** Terceros no tiene entregas.
- **Hasta dónde llega el invariante, dicho sin exagerarlo.** Con `DiaDePagoFijo` puesto, la fecha
  efectiva de pago **puede pasar de 60 días** aunque el plazo guardado sean 30: un plazo de 30 días
  sobre una entrega del día 2, redondeado al día de pago 25 del mes siguiente, se va más allá. Esa
  comprobación es de donde se calcula el vencimiento —la fase 3— y allí tiene que **recortar al día
  60**, no rechazar. Escribirlo aquí sería computar fechas, que es meterse en la fase 3; callarlo
  sería dejar creer que el tope está cerrado por este ítem, y no lo está.
- **`LimiteCredito` lleva su divisa, y no la hereda.** Es un `Importe?` —`numeric(18,4)` más el
  código ISO, nunca coma flotante ni el `money` del motor—, con `ck_terceros_limite_credito_completo`
  para que las dos columnas estén o falten juntas. La alternativa era heredar `Empresa.DivisaBase`
  (el campo se llama así; **`DivisaPreferidaId` no existe**, y era el nombre que traía la nota), y se
  descarta por su **modo de fallo mudo**: el día que alguien cambie la divisa base de la empresa,
  todos los límites guardados cambiarían de significado sin que se toque una sola fila y sin que
  falle nada. Lo que la pantalla ofrezca por omisión es otra cosa y es de la pantalla. Consumo,
  disponible y bloqueo por exceso **no son de este ítem**: esa es la raya de P6.
- **Una sola cuenta preferente por tercero, y es una restricción.** Índice único parcial
  `ix_cuentas_bancarias_una_preferente_por_tercero` sobre `(tercero_id)` con filtro `es_preferente`,
  más el invariante en el agregado: la primera cuenta nace preferente, marcar otra degrada a la
  anterior y quitar la preferente promueve a otra. **¿La restricción ve las filas bloqueadas? Es la
  misma decisión del 1.5** —la de (empresa, NIF)—: el índice es de la base y no conoce el filtro
  global, así que las ve todas. Y aquí, además, **no podría ser de otra manera y eso es lo
  interesante**: el bloqueo del art. 32 vive en el **padre**, no en la cuenta, así que dos cuentas
  que compiten por ser la preferente están siempre en el mismo estado. La pregunta que en el 1.5
  tenía dos respuestas posibles, aquí solo tiene una.
- **El régimen fiscal entra, con el corte que se sostiene — tres de cuatro.** Está en el §7.2 del
  plan maestro y se quedó sin ítem al desglosar la fase: alcance comprometido sin casa, y su sitio es
  el tercero. `RegimenFiscal` es un valor con `TerritorioFiscal` (cinco valores, `CHECK`
  `ck_terceros_territorio_fiscal`) y tres booleanos: recargo de equivalencia, criterio de caja y
  sujeto a retención de IRPF. **El corte se sostiene porque los cuatro son hechos declarados del
  tercero**, no resultados de un cálculo — que un minorista esté en recargo de equivalencia es algo
  que él dice y que su ficha registra, igual que su domicilio. **Lo que NO entra, y se dice: el tipo
  de retención.** Ese sí es un valor sin significado sin la regla que lo aplica, y además ya tiene
  dueño desde el 1.2 (`Impuesto` con `TipoDeImpuesto.Retencion`), así que ponerlo aquí obligaría a
  declarar un cruce entre módulos (ADR-0024) — bastante más que «el campo y nada más». El booleano
  dice *si* se le retiene; *cuánto* es de quien ya lo sabe.
- **Los tres universos del 1.3 y el 1.4 miraron solos, sin que hubiera que apuntarlos**, que era la
  prueba: la tabla de enrutado descubrió las once rutas nuevas (`TodaEscrituraDiceComoSeProtege`
  pasó de 82 a 93 acciones y puso rojo `El_barrido_encuentra_el_inventario_entero`), el testigo de
  versión en el cuerpo siguió verde porque ninguno de los cuatro DTO nuevos lo lleva, y el criterio
  sensible en la URL **no necesitó ni una línea nueva en el censo**: `iban` ya estaba en
  `s_sensibles` desde el 1.3, y **no se ha añadido ninguna ruta que busque por IBAN**. Si algún día
  se busca por él, va por cuerpo.
- **Y una regla del carril que NO se ha podido ver roja en local, dicha como tal.**
  `EsquemaDeTercerosTests` compara la lista de tablas del esquema **entera** y afirma que no hay
  **ninguna** clave ajena; con tres tablas nuevas y sus tres claves en cascada, iba a ponerse roja
  por partida doble en la CI. La vigilancia funcionó —nadie tuvo que apuntarla— pero **está en el
  carril de integración**, así que aquí se detectó leyéndola, no ejecutándola. Aprovechando que es
  el único sitio que ve la base de verdad, se ha extendido de 5 casos a 15: el índice **parcial** de
  la cuenta preferente (contrastado a propósito con el del identificador, que no puede llevar
  predicado), la unicidad de (tercero, IBAN) —y **no** global, porque la misma cuenta puede ser de
  dos terceros—, los tres `CHECK`, el valor 60 del tope escrito en el suyo, las cuatro columnas del
  régimen y las dos del límite con su escala, el `ON DELETE CASCADE` de los tres hijos por el
  derecho de supresión, y la regla de `NOT NULL` **sin `DEFAULT`** sobre las cuatro columnas que
  este ítem añadió a una tabla con filas dentro — que es exactamente la que dice si la migración de
  tres pasos hizo lo que promete. Y se dice lo que **no** son: leen la definición de la restricción,
  no meten una fila para ver si muerde. Cazan que se borre o se escriba mal, no un motor que la
  ignore.
- **Y dos reglas más se pusieron rojas sin que nadie las llamara**, las dos por lo mismo —una
  entidad nueva es un compromiso que hay que declarar en varios sitios—: `ElGlosarioDelDominioTests`
  exigió las tres filas de las entidades hijas, y
  `CadaAccionDeclaraSuPermisoTests.Escribir_y_modificar_no_comparten_permiso` cazó que
  `terceros.tercero.modificar` estaba abriendo **ocho** acciones. De ahí los siete permisos nuevos,
  uno por escritura, cada uno con su motivo escrito.

**Los `Idempotency-Key` no se mueven, y eso es la mitad del resultado.** El ítem suma **+11
acciones**, **+7 que cambian estado**, **+7 `If-Match`**, y **cero** al cajón de idempotencia y cero
al de las exentas. Las siete escrituras nuevas son modificaciones de un agregado que ya existe, no
altas de un recurso nuevo, así que el mecanismo que les toca es el otro —y las tablas del 0.9, que
llevaban desde el 0.10 sin rehacerse, se han rehecho enteras en *Decisiones tomadas → ítem 0.9*.

Carril rápido en local con el ítem entero, con el comando que lo mide —**el mismo del *workflow***,
que es lo que `AGENTS.md` exige desde el 1.3—:

```
dotnet test Bastion.sln --filter "Category!=Integracion" \
  --logger trx --results-directory artifacts/test-results/dominio
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio \
  "Dominio y arquitectura" 300 "<los ocho ensamblados de ci.yml>"

Dominio y arquitectura: 624 casos (624 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Organizacion.UnitTests 182, Organizacion.IntegrationTests 4, Api.FunctionalTests 136,
    Arquitectura.Tests 31, Api.IntegrationTests 4
```

Contra los **576 en 8 ensamblados** de `main` al abrir la rama (`e3a9e9e`): **+48 casos**, y el
reparto dice de dónde sale cada uno — Terceros.UnitTests 39→77 (los 27 de lo que cuelga y los 11 de
la batería del IBAN), Arquitectura.Tests 23→31 (las tres del art. 32, la del puerto y las cuatro del
barrido de datos con forma de real), Api.IntegrationTests 1→4 y Organizacion.IntegrationTests 5→4
(el caso que se mudó de proyecto). Lo demás no se mueve.

**Integración no se ha podido ejercer en local, y esta es la firma:** `docker info` responde, pero
`docker version` contesta `failed to connect to the docker API at
npipe:////./pipe/dockerDesktopLinuxEngine; check if the path is correct and if the daemon is running:
open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.` — el cliente está
instalado y el demonio no arranca en esta máquina.

**Lo que se ejerció en su lugar, y lo que queda para el run.** En lugar de los tests que necesitan
contenedor: `scripts/comprobar-migraciones.sh` (el modelo no tiene cambios pendientes y la migración
cuadra con él), `CadaEntidadDeclaraSuAuditoriaTests` en el carril rápido (las cuatro configuraciones
nuevas declaran la auditoría de **cada** propiedad), los 27 casos de `LoQueCuelgaDelTerceroTests`
sobre los invariantes del agregado, y la compilación del proyecto de integración con sus **veinte
casos nuevos** —los 15 de `EsquemaDeTercerosTests` y los 10 de `ContratoDeLoQueCuelgaTests`, de los
que 5 ya existían—, que al menos garantiza que las consultas están escritas y el ensamblado carga.
**Lo que solo el run puede decir** es lo que de verdad falta: que la migración se **aplique** —los
tres pasos de añadir, rellenar y cerrar—, que los tres `CHECK` y el índice parcial estén en la base
después de aplicarla, que los 15 casos de esquema pasen, y que los 10 de contrato pasen —entre ellos
el que tapa el agujero de la mutación 7, que **ninguna máquina ha visto correr todavía**—. Se dice
antes de empujar, no después.

**Y el run lo dijo: 34097268237 salió ROJO, con dos defectos de producto que ningún carril local
podía ver.** Se anota entero porque es el resultado del párrafo anterior: lo que se dijo que solo el
run podía decir, lo dijo, y dijo que no. Los **tres jobs contados en el run** (`total_count: 3`):
Backend `101663584575` **failure** en 22 pasos, Frontal `101663584755` success en 17, Humo
`101664678865` skipped. Recuentos que publicó: **dominio 624 casos en 8 ensamblados, 0 rojos**;
**integración 313 casos en 8 ensamblados, 7 rojos**. Los 7 son dos defectos, no siete.

- **Defecto A — la migración dejó las cuatro columnas del régimen ANULABLES, y el modelo decía
  `NOT NULL`.** Cuatro filas rojas de
  `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT(tabla: "terceros", columna: …)`, con
  el mensaje `should be (NO, ) but was (YES, )` sobre `territorio_fiscal`,
  `recargo_de_equivalencia`, `criterio_de_caja` y `sujeto_a_retencion_irpf`. **La causa es una
  omisión que no da error ni aviso:** `MigrationBuilder.AlterColumn(..., nullable: false)` **sin**
  `oldNullable: true` construye una operación cuya columna «vieja» tiene los valores por omisión del
  tipo, así que el generador de Npgsql compara `IsNullable` nuevo (`false`) contra
  `OldColumn.IsNullable` (`false`), no ve diferencia y **no emite `SET NOT NULL`**. La migración se
  aplica entera, sin fallar, y deja el tercer paso sin hacer.
- **Y esto es exactamente el hueco que la regla existía para tapar.** El modelo sí decía `NOT NULL`
  —`b1.Property<string>("Territorio").IsRequired()` en el *snapshot*—, así que
  `has-pending-model-changes` no tenía nada que ver, y **ningún barrido sobre el modelo podía
  encontrarlo**: el modelo estaba bien y el esquema estaba mal. Solo lo ve quien mira la base
  después de aplicar la migración, que es el único sitio donde vive la diferencia.
- **Arreglado, y comprobado en local sin contenedor** — que es lo que aquí sí se puede ejercer: las
  cuatro `AlterColumn` llevan ya sus parámetros de columna vieja, y `dotnet ef migrations script`
  —que no se conecta a ninguna base— emite ahora las cuatro sentencias que faltaban:

  ```
  dotnet ef migrations script 20260905044708_EsquemaInicialDeTerceros \
    20260907025348_LoQueCuelgaDelTercero --context TercerosDbContext --no-build

  UPDATE terceros.terceros SET territorio_fiscal = 'PeninsulaYBaleares', … WHERE territorio_fiscal IS NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN territorio_fiscal SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN recargo_de_equivalencia SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN criterio_de_caja SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN sujeto_a_retencion_irpf SET NOT NULL;
  ```

  Y `grep -i default` sobre el guion entero no encuentra nada: los tres pasos de añadir, rellenar y
  cerrar, sin `DEFAULT` que se quede detrás.

- **Defecto B — citar la versión de la ficha para colgarle algo daba 412 SIEMPRE.** Tres filas rojas
  —`Una_segunda_cuenta_preferente_baja_a_la_primera_y_solo_queda_UNA`,
  `El_mismo_IBAN_dos_veces_en_la_misma_ficha_es_409_y_sin_el_numero_dentro` y
  `Sesenta_dias_clavados_se_aceptan_porque_el_tope_es_el_maximo_y_no_un_veto`—, todas con
  `should be HttpStatusCode.OK but was HttpStatusCode.PreconditionFailed` y el cuerpo
  `{"type":"/errors/version-obsoleta","title":"La versión ya no es la actual","status":412,…}`.
- **La atribución sale de una partición limpia, no de una corazonada.** De los diez casos de
  contrato, los tres rojos son **exactamente** los que llegan a `SaveChanges` tocando **solo hijos**;
  los cuatro del límite de crédito pasaron porque `FijarLimiteDeCredito` modifica la propia ficha, y
  los otros tres pasaron porque nunca llegan a guardar. La causa: `Versiones.Exigir` ponía el
  `OriginalValue` del testigo, pero una escritura que solo inserta un hijo deja la fila del agregado
  en `Unchanged`; EF Core arma entonces un mandato sin columnas que asignar, la sentencia no toca
  ninguna fila, el guardado sale por `DbUpdateConcurrencyException` y el borde lo traduce a un 412
  **perpetuo** — la escritura correcta era imposible, y el mensaje decía que otro la había pisado,
  que era mentira.
- **DESMENTIDO por el run siguiente — se deja escrito, no se borra.** Lo que sigue se escribió como el arreglo del defecto B y no lo era; el bloque de abajo lo cuenta. El cambio se queda en el código por otro motivo, que también está abajo.
- **Arreglado en `Versiones.Exigir`, y no en los seis casos de uso.** Quien **exige** una versión
  para escribir está diciendo que escribe: ahora, además del `OriginalValue`, marca `ModificadoEn`
  como modificada cuando la entidad es una `EntidadBase`. El interceptor de marcas de tiempo le pone
  entonces un valor nuevo de verdad, el `UPDATE` tiene algo que asignar y el testigo avanza. Es el
  mismo argumento que ya estaba escrito en `InterceptorDeMarcasDeTiempo`: **sostenerlo a mano
  significa que el día que alguien escriba la séptima ruta que cuelga algo y no se acuerde, la
  versión deja de moverse sin que nada falle.** Para las 27 escrituras que ya modifican el agregado
  no cambia una sola sentencia, y para `EliminarSerie` tampoco, porque `Remove` manda sobre el
  estado. Los treinta llamantes de `Exigir` se han comprobado uno a uno: los treinta son casos de
  uso de escritura, ninguno es una lectura.
- **Y de paso tapa un silencio de la bandeja de salida, que nadie había pedido mirar.**
  `InterceptorDeLaBandeja` solo publica los eventos de un agregado si además `SeEstaGuardando` —es
  decir, si su estado es `Added`, `Modified` o `Deleted`—. Con la raíz en `Unchanged`, una escritura
  que solo tocara hijos y registrara un evento de dominio en la raíz **se habría guardado sin
  publicar nada, y sin fallar**. Hoy ninguna lo hace, así que no es un defecto observado: es un
  filo que estaba puesto y que ya no está. Los otros dos interceptores se han mirado igual:
  `InterceptorDeAuditoria` escribirá ahora una fila de traza para la ficha además de la del hijo
  —que es lo correcto, porque la ficha ha cambiado—, y ninguna prueba existente cuenta filas de
  traza de escrituras de este tipo, porque hasta ahora todas fallaban.
- **Y el caso que faltaba es el que habría ahorrado el run.** Los diez de contrato miraban cada ruta
  por su lado; ninguno miraba el invariante que las cruza. `ContratoDeLoQueCuelgaTests` pasa de 10 a
  **14 casos**: `Colgar_un_contacto_MUEVE_la_version_de_la_ficha_y_la_vieja_ya_no_vale` —que afirma
  las dos direcciones, porque cada una caza un fallo distinto: que la etiqueta cambie caza la versión
  quieta, y que la vieja dé 412 caza un borde que devolviera etiquetas nuevas sin relación con el
  testigo— más los tres recorridos de escritura que nada ejercía (alta y baja de contacto, ascenso a
  preferente, baja de cuenta). Los cuatro, en el censo de su carril.
- **El límite honesto, dicho igual que el de la mutación 7:** los cuatro casos nuevos y el arreglo
  del defecto B viven en el **carril de integración**, que esta máquina no puede ejecutar, y el
  proyecto rechaza a propósito EF Core InMemory y no tiene arnés de SQLite —así que no hay dónde
  ponerlos en el carril rápido sin inventar arquitectura—. Del arreglo A sí hay prueba local (el
  guion SQL de arriba); del B, **hasta que el run siguiente esté leído, lo único que se puede afirmar
  es que la causa está identificada por partición y el arreglo escrito**, no que se haya visto morder.

**El run 34101097530 sobre `57838b2`: el defecto A está cerrado y el B NO, y mi atribución del B era
falsa.** Se escribe entero porque la parte que importa es la segunda. Los **tres jobs contados en el
run** (`total_count: 3`): Frontal `101675496804` success en 17 pasos, Backend `101675497155`
**failure** en 22, Humo `101676750179` skipped. Recuentos que publicó el propio run: **dominio 624
casos en 8 ensamblados, 0 rojos**; **integración 317 casos en 8 ensamblados, 7 rojos**. Los tres
pasos rojos del Backend son uno: «Tests de integración», y detrás «Bajar el artefacto del OpenAPI» y
«El artefacto del OpenAPI contiene el contrato», que caen porque el anterior no llegó a subirlo.

- **Defecto A, CERRADO.** Las cuatro filas de
  `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT` **han desaparecido** del run. Es la
  confirmación por el efecto de lo que el guion SQL ya decía en local: los `oldNullable: true`
  emiten las cuatro sentencias y la migración cierra su tercer paso.
- **Defecto B, ABIERTO — y con la atribución anterior desmentida.** Los 7 rojos son ahora los 3 de
  antes más los 4 casos nuevos, todos con `should be OK but was PreconditionFailed` y **todos en la
  PRIMERA escritura** de su caso. Es decir: el cambio en `Versiones.Exigir` no movió nada. La
  explicación que se escribió —la ficha en `Unchanged`, el mandato sin columnas, el `UPDATE` de cero
  filas— **era falsa**, y conviene decir por qué se creyó: la partición encajaba perfectamente (los
  cuatro casos del límite pasaban, los que solo tocan hijos no), y una partición limpia que encaja
  con una explicación plausible se parece mucho a una prueba sin serlo.
- **Lo que el run SÍ deja probado, y apunta a otro sitio.** El cuerpo del 412 es
  `ObsoletaYSinRecurso` —«Este recurso ha cambiado o ha desaparecido»— y no `Obsoleta(actual)`.
  `ManejadorDeVersionObsoleta.ActualAsync` solo devuelve nulo cuando **ninguna** de las entradas del
  choque tiene la propiedad del testigo. **La ficha la tiene.** Luego el `DbUpdateConcurrencyException`
  no lo levanta la ficha: lo levanta una entrada sin testigo —un hijo, o una fila de traza—. Eso
  descarta la ficha entera, que era donde estaba mirando.
- **Y una hipótesis descartada con su experimento, para que nadie la repita.** Se sospechó que la
  columna de sistema `xmin` no sobreviviera a la subconsulta que EF Core genera al mezclar
  `Include` de colecciones con `FirstOrDefault` —`FROM (SELECT … LIMIT 1) AS t0`—, lo que habría
  hecho que la versión leída fuese basura. Se comprobó **en local y sin base de datos**, con una
  sonda temporal que imprime `ToQueryString()` de las dos consultas del repositorio: `t.xmin` está
  en la proyección de la consulta simple, en la de los tres `LEFT JOIN` y **también dentro de la
  subconsulta con `LIMIT`**. La hipótesis es falsa. La sonda se ha borrado; lo que queda es esto
  escrito.
- **Lo que falta para cerrarlo es el texto de la excepción**, que dice qué entidad esperaba afectar
  a una fila y afectó a cero. Está en el registro del job `101675497155`, y
  `/actions/jobs/{id}/logs` contesta **403** sin credencial. Las anotaciones del *check run* —que sí
  son públicas y de donde sale todo lo anterior— publican el nombre del caso y la aserción, pero no
  el registro de la aplicación. **Pedido al usuario**, que lo había ofrecido — y su respuesta fue que el registro **no contiene ninguna traza del choque**: ni una linea. Lo que sigue sale de ahi.

**Y la partición que se dio por buena estaba mal leída, que es la segunda vez que pasa lo mismo con
distinta cara.** Se dijo que pasaban «los cuatro casos del límite» y fallaban «los que solo tocan
hijos». Falso: `Sesenta_dias_clavados_se_aceptan…` es de condiciones de pago y estaba en rojo. La
partición **real** del run es otra y es más limpia: **los siete rojos son exactamente los siete que
esperan un OK en una ruta de colección hija, y ninguno de los que esperan un rechazo está en rojo**
—no lo están porque fallan la validación y nunca llegan a escribir—. Reenunciada: *todo camino que
llega a escribir un hijo falla; ninguno que no llega, falla*. Y de ahí lo que de verdad duele: **no
queda ni un verde que demuestre que una escritura de hijo funciona.** La lección no es «mira mejor»;
es que **una partición se comprueba enumerando los dos lados, y el lado verde no se enumeró**.

**La enumeración que sí se hizo, y lo que tumba.** Se enumeraron desde el código todos los caminos
que producen el cuerpo `ObsoletaYSinRecurso`, en vez de suponer el primero que encajaba:

| Pregunta | Comando | Resultado |
|---|---|---|
| ¿Quién llama a `ObsoletaYSinRecurso()`? | `grep -rn ObsoletaYSinRecurso src/ tests/ frontend/src` | **Un solo llamante**: `ManejadorDeVersionObsoleta.cs:78` |
| ¿Quién produce ese texto de `detail`? | `grep -rn "ha cambiado o ha desaparecido" src/ tests/ frontend/src` | **Un solo sitio**, el de la constante |
| ¿Quién produce `TipoDeError.VersionObsoleta`? | `grep -rn "VersionObsoleta(" src/` | Solo `ErroresDeConcurrencia`, sus dos fábricas |
| ¿Quién trata `DbUpdateConcurrencyException`? | `grep -rn DbUpdateConcurrencyException src/` | Solo el manejador |

Y ese único llamante está **detrás de un `return false`** que descarta toda excepción que no sea
`DbUpdateConcurrencyException`. Conclusión: **hubo choque**, y ahora está probado por enumeración y
no supuesto. Con eso cae la hipótesis alternativa —«no hubo choque, la ficha no se encontró»—:
una ficha que no aparece devuelve `ErroresDeTercero.NoEncontrado`, que es un **404**, no este cuerpo.
Un filtro global que la escondiera daría 404 igual.

**El agujero de la política de errores, cerrado — y no como instrumentación de este fallo.** El
registro del job no traía ni una línea del choque, y aunque la hubiera traído no habría servido: el
mensaje era *«la versión del cliente ya no era la actual»* y **no nombraba nada**. Un 412 es de las
pocas respuestas que no puede decir en el cuerpo **qué** chocó, así que el único sitio donde eso
puede constar es la traza; y el cuerpo publica un `traceId` precisamente para que alguien lo pegue y
aparezca la petición. Sin la anotación, ese `traceId` era **una promesa que el sistema no cumplía**.
Ahora hay dos sucesos, y son dos porque no son la misma cosa:

- **8500, `Information` — el choque que el diseño espera.** Dos personas editando la misma ficha. El
  cliente tiene en el cuerpo el `versionActual` que necesita para resolverlo.
- **8501, `Warning` — el choque SIN versión actual.** Ninguna entrada lleva testigo, así que el 412
  sale **sin** el `versionActual` que el contrato promete. Eso no es tráfico normal, es una
  anomalía, y por eso no se queda en el nivel de información entre todas las consultas del día.

Los dos llevan método, ruta, **la misma traza que publica el cuerpo** y las entradas del choque como
`Tipo:Estado`. **Nombres y estados, y ningún valor**, enmascarado en el punto de escritura y no
confiando en el destino: entre lo que puede chocar hay tablas con datos personales —un contacto
lleva nombre, correo y teléfono de una persona identificada— y esto va a un registro que se agrega,
se replica y se conserva con menos ceremonia que la base de datos. Por construcción no puede salir un
valor: el ayudante solo lee `Metadata` y `State`. Lo guarda
`Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado`, que afirma **las dos mitades** —que
nombra `Tercero:Modified` y la traza, y que no lleva ni el identificador fiscal ni la razón social—.

**Y dos defectos del arnés, que el usuario señaló y se confirman desde arriba.**

- **La cascada del OpenAPI era cascada, y ya no.** «Publicar el OpenAPI» **no llevaba `always()`**
  —con razón: de un job que falló no hay nada que publicar—, así que un rojo en los tests lo dejaba
  sin ejecutar; pero los dos pasos que lo verifican **sí** llevaban `always()` y se ejecutaban
  igual, buscando un artefacto que nadie había subido. Un rojo de tests fabricaba dos errores más
  que **parecen del contrato y no lo son**. Ahora los dos cuelgan de
  `steps.publicar_openapi.outcome == 'success'`: si no hubo publicación, no hay nada que verificar.
  El arnés que existe para que la CI no mienta no puede ser el que miente.
- **La tubería que se cerraba antes de tiempo.** `grep -o … | head -1`, tres veces en
  `recuento-de-tests.sh`: `head` cierra la tubería y `grep` muere escribiendo en ella —de ahí los
  `grep: write error: Broken pipe` del registro—. Hoy no rompía nada porque de una tubería solo
  cuenta el último estado, pero **este guion decide el rojo y el verde del carril**, y el día que
  alguien le añadiera `set -o pipefail` empezaría a fallar el **contador** en vez de lo contado.
  Sustituido por un `awk` con `exit`, que no abre tubería, se para en la primera coincidencia igual
  y devuelve 0 aunque no encuentre nada. Comprobado con `pipefail` puesto a propósito: mismo
  recuento —**624 casos en 8 ensamblados**—, código 0 y sin una línea de ruido.

**Los `act()` del frontal, con los dos extremos nombrados.** La regla que salió del 1.5 se aplica
desde aquí: toda cifra de «antes y después» dice sobre qué commit se midió cada extremo, y el
«antes» es `main` al abrir la rama.

```
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
```

- **Antes — `e3a9e9e`** (main al abrir): **109 avisos en 7 ficheros**.
- **Después — `57838b2`** (el árbol que se lleva a main al cerrar): **109 avisos en 7 ficheros**,
  con el mismo reparto: `ElCambioDeIdioma` 17, `ElCambioDeRuta` 14, `ElSelectorDeEmpresa` 14,
  `LasRutasProtegidas` 17, `LaPantallaDeAcceso` 10, `ElListadoDeAlmacenes` 24, `ElTestigoDeAcceso` 13.

> **Y el extremo «después» se midió en `c7b9f80`, no en `57838b2`: hay que decirlo y hay que
> justificarlo.** Entre los dos hay dos commits que arreglan lo que destapó el run 34097268237, y
> ninguno toca el frontal — `git diff --stat c7b9f80 57838b2 -- frontend/` sale **vacío**, es decir,
> el árbol de `frontend/` es idéntico byte a byte. La cifra vale para `57838b2` porque el código que
> la produce es el mismo, y la comprobación que lo sostiene está escrita aquí para que se pueda
> repetir. La alternativa —dar el número sin decir en qué commit se midió— es justo lo que la regla
> que salió del 1.5 prohíbe.

**No se mueve, y el motivo está en el `git diff`:** el ítem toca del frontal solo `esquema.ts`
—generado—, dos textos de `i18n` y las *fixtures*. Ni un componente, ni una pantalla. Un cambio en
esta cifra habría sido la señal de que el ítem se metió donde no le tocaba.

**Las ocho primeras mutaciones del ítem, cada una con su línea base nombrada** —las seis
restantes van con la segunda mitad del ítem, más abajo—**.** Todas sobre árbol limpio,
aplicadas con copia de respaldo y revertidas **restaurando esa copia** —nunca con `git checkout --`,
que se llevaría por delante todo lo no commiteado del fichero—, y con `git status --porcelain` vacío
comprobado después de cada una. Cuando la mutación toca el modelo, se **reconstruye Debug** antes de
medir y antes de dar por revertida: `comprobar-migraciones.sh` corre con `--no-build` y lee los
binarios del disco, no el código.

| # | Mutación | Línea base | Resultado |
|---|---|---|---|
| 1 | La regla de la lista cerrada de tipos bloqueables, **escrita antes del arreglo** | `e3a9e9e` (main al abrir la rama) | **Rojo**, con los dos supervivientes nombrados |
| 2 | Un módulo que bloquea y **no expone su puerto** de consulta | `f9913d9` | **Rojo**, con el módulo nombrado |
| 3 | Un IBAN con el control válido y la **longitud de otro país** | `81b44fe` | **Rojo** |
| 4 | Un IBAN **con forma de real** en una *fixture* del frontal | `81b44fe` | **Rojo**, con el sitio y el valor enmascarado |
| 5 | `CondicionPago` aceptando **90 días** | `81b44fe` | **Rojo** en el dominio, en el borde y en el `CHECK` de la migración |
| 6 | La cuenta **por omisión duplicada** | `81b44fe` | **Rojo** por el efecto: dos preferentes en la ficha |
| 7 | `LimiteCredito` **sin divisa**, heredando en silencio la de la empresa | `81b44fe`, y otra vez sobre `c7b9f80` | **VERDE las dos veces en el carril rápido** — el hallazgo del ítem, abajo entero |
| 8 | Una **regla nueva fuera del censo** de su carril | `c7b9f80` | **Rojo**, con la regla nombrada |

**La 1, entera.** No es una mutación del código: es la regla escrita **contra el árbol tal como
estaba**, que es el orden que pedía el encargo —«el trabajo no empieza por añadir dos valores al
enum; empieza por la pregunta de por qué nada se puso rojo»—. Sobre `e3a9e9e`, `TipoDeRecursoBloqueado`
tenía tres valores y cinco agregados implementaban el bloqueo. Con
`dotnet test tests/Arquitectura.Tests/Bastion.Arquitectura.Tests.csproj --filter LoBloqueadoSeVeEntero`:

```
[xUnit.net]     Bastion.Arquitectura.Tests.LoBloqueadoSeVeEnteroTests
                .Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32 [FAIL]
  Shouldly.ShouldAssertException : sinSitio
      should be empty but had
  2
      items and was
  ["Tercero", "Usuario"]

  Additional Info:
      estos agregados se pueden bloquear y NO aparecen en `TipoDeRecursoBloqueado`, así que lo
      que se les reserva no sale en el listado del art. 32 y nadie puede consultarlo. No basta
      con añadir el valor: el enumerado vive en un módulo y la obligación es transversal, así
      que el módulo dueño tiene que exponer su puerto:
  · Tercero
  · Usuario
  Stack Trace:
    tests/Arquitectura.Tests/LoBloqueadoSeVeEnteroTests.cs(108,0)

Con error! - Con error: 1, Superado: 2, Omitido: 0, Total: 3
```

Los dos supervivientes son los que el listado no veía, y el mensaje ya dice que el arreglo **no** es
añadir dos valores al enumerado. Ese rojo doble —el de aquí y el de la mutación 2— es el entregable
del primer trozo del ítem; el arreglo vino después, en `f9913d9`.

**La 4, entera.** Sobre `81b44fe`, se cambió en `frontend/src/pruebas/datos.ts` un IBAN de relleno
por uno con el cuerpo variado y el control calculado —es decir, indistinguible de uno de verdad—.
El valor no se escribe aquí, que es precisamente lo que la regla defiende:

```
MUTACION 4 aplicada en frontend/src/pruebas/datos.ts
  Con error Bastion.Arquitectura.Tests.NingunDatoConFormaDeRealTests
            .Ningun_dato_con_forma_de_real_se_queda_escrito [355 ms]
  Mensaje de error:
   Shouldly.ShouldAssertException : hallazgos
    should be empty but had
1
    item and was
["frontend/src/pruebas/datos.ts:231 · IBAN «ES********************98»"]

Additional Info:
    estos sitios llevan escrito un dato con forma de real que no sale del espacio inventado de
    este proyecto. Un identificador válido es de alguien, y de un repositorio no se borra:
    sustitúyelo por un valor de relleno con su control calculado, como hace el resto de la suite.

Con error! - Con error: 1, Superado: 3, Omitido: 0, Total: 4
```

Tres cosas que este rojo demuestra y que un negativo genérico no habría demostrado: que el barrido
**alcanza al frontal** y no solo a `src/` y `tests/` de C#; que **enmascara lo que encuentra** —el
mensaje enseña el país y dos cifras, no el número—; y que la extensión al IBAN reutiliza el mismo
mecanismo del identificador fiscal en vez de duplicarlo, porque la lista de formas prohibidas se
compara **entera y en los dos sentidos** contra lo que el barrido sabe detectar.

**La 7 salió verde, y ese es el segundo hallazgo del ítem.** La mutación era la del encargo: quitar
la exigencia de divisa en `FijarLimiteCredito.LeerImporte` y devolver `Importe.De(cantidad, "EUR")`
cuando el cuerpo no la trae. Con ella puesta, el carril rápido entero siguió en **624/624**, sin un
solo caso rojo. La decisión «la divisa NO se hereda en silencio» estaba escrita en un `remarks` y en
un comentario, y **no la guardaba ningún test**: `Terceros.UnitTests` no referencia `Application` a
propósito (§13 — «dominio puro, sin infraestructura y sin E/S»), y `Api.FunctionalTests` no puede
llegar al caso de uso porque este carga el agregado **antes** de validar, que es el orden 404-antes-
que-400 que sigue todo el código. Su única casa posible es el contrato con base de verdad.

Escrito en `c7b9f80`: `ContratoDeLoQueCuelgaTests`, diez casos, y el que cierra el agujero afirma
**el efecto** y no solo el código —400, el campo `divisa`, y que al releer el recurso **sigue sin
haber límite**—, porque el modo de fallo que importa no es dejar de contestar 400: es contestar 200
con una divisa que nadie escribió. **Y aquí está el límite honesto: esa regla vive en el carril de
integración, que esta máquina no puede ejecutar.** Reaplicada la mutación sobre `c7b9f80`, el carril
rápido volvió a salir verde —lo esperado, porque los diez casos nuevos no corren en él—. El run de
la CI es el primero que los ejecuta.

**Y leído el run 34097268237, esto es lo que se puede decir de la 7 y no más.** Los cuatro casos del
límite de crédito **corrieron y pasaron** sobre el código bueno, así que la regla existe, se ejecuta
y afirma algo — que es lo que faltaba por saber. Lo que **sigue sin haberse visto** es la regla
mordiendo: para eso habría que reaplicar la mutación en un run, y no se ha hecho. Así que de la 7 se
afirma que **el agujero está identificado, la regla escrita y el caso verde sobre código correcto**;
no que se haya visto rojo con la mutación puesta. La diferencia importa: un caso que pasa sobre
código bueno todavía puede ser un caso que pasa siempre.

**El culpable, nombrado por el registro que faltaba.** El suceso 8501 salió en las anotaciones del
[run 34120375406](https://github.com/AOjeda006/Bastion/actions/runs/34120375406) con lo que hacía
falta y nada más: `Contacto:Modified`, `CuentaBancaria:Modified`, `CondicionPago:Modified`. Los tres
hijos, en estado de **modificación** en el momento del choque. Y el caso permanente que lo escribe
—`Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado`— salió **verde** en ese mismo run:
el desenlace 8500 sobre la raíz, con `Tercero:Modified` y su `versionActual`, funciona.

**Y una corrección antes de que la corrigiera el run: la inferencia tenía tres ramas y se enunció
una.** Que `ActualAsync` devuelva nulo puede salir porque (a) ninguna entrada lleve testigo, porque
(b) lo lleve y `GetDatabaseValuesAsync` devuelva nulo, o porque (c) lo lleve, se lea, y el valor no
case con `is uint`. Se afirmó (a) como si fuera la única —el mismo error de estrechar una
enumeración, tercera vez—. El suceso 8501 las distingue por construcción, y sus entradas dijeron que
era (a): entre las entradas del choque no aparece `Tercero`.

**El mecanismo, y el ADR que ya lo contaba.** EF Core decide si una entidad recién aparecida en la
colección de un padre ya seguido es un alta o una fila que ya existía mirando si su clave **se
rellena al insertar**. Por convención marca toda clave `Guid` como `OnAdd`; aquí la pone la fábrica
del dominio —`Guid.CreateVersion7()`, a propósito, por localidad de índice—, así que venía siempre
puesta, EF concluía «ya existía» y emitía un `UPDATE` contra una fila que no estaba: cero filas,
choque, 412.

Eso es **exactamente el ADR-0010**, aceptado el 26-08-2026, cuatro fases antes. Allí salía `500`
porque no existía el manejador de choques; aquí sale `412`, y es **peor**: un 500 se investiga, un
412 se lee como «alguien guardó antes que tú», que es plausible y falso.

**Lo que el ADR-0010 daba por sabido y no lo era.** Descartaba por escrito una alternativa:
*«`ValueGeneratedNever()` en el mapeo. No sirve: la regla de EF Core es "la clave está puesta", y con
generación desactivada está puesta siempre»*. Es falso, y se midió sobre el modelo real y sin base
de datos:

| Declaración de la clave | Estado del hijo tras `DetectChanges()` |
|---|---|
| `ValueGeneratedOnAdd` (la convención de EF para `Guid`) | `Modified` → `UPDATE` → 412 |
| `ValueGeneratedNever` | `Added` → `INSERT` |

La pregunta que EF se hace no es si el valor está puesto, sino si la clave es **de las que se
rellenan al insertar**. Declarada `Never` no lo es. No cambió EF Core: cambió que el modelo dejó de
decirle algo que no era verdad. Queda en el **ADR-0032**, que sustituye esa frase del ADR-0010 y
deja en pie todo lo demás —incluido `Registrar`, que ahora es redundante y no se retira aquí: eso es
tocar un camino de Identidad que este ítem no tiene por qué tocar—.

**El arreglo es una declaración, no un rodeo.** La convención de cierre `LaClaveLaPoneElDominio`
marca `ValueGeneratedNever` toda propiedad `Guid` de una clave primaria, en los cinco contextos que
heredan de `ContextoDeModulo`. Una convención y no una línea por configuración porque no es mapeo de
una entidad: es una propiedad del sistema entero, y una lista por configuración es una lista de la
que la próxima entidad se cae sin ruido. **Sin migración**: `ValueGenerated` sobre una clave `Guid`
no produce diferencia relacional, y `comprobar-migraciones.sh` sigue diciendo que modelo y
migraciones coinciden en los cuatro módulos.

**El fallo pasó de necesitar PostgreSQL a durar un segundo.** `LoQueCuelgaNaceComoAltaTests` cuelga
los tres hijos de una ficha seguida y afirma el estado de la entrada. Sin contenedor, en el carril
rápido, y con su canario al lado —que el modelo tenga las tres entidades, que la ficha quede
`Unchanged`, y que una modificación de verdad salga `Modified`—, porque si nada se detectara,
`Added` podría salir por no haberse decidido nada.

**Y el hallazgo de segundo orden, que es el que más vale.** La regla que debía cazar esto existía,
miraba **justo esta propiedad** y la daba por buena: `Toda_entidad_tiene_su_clave_completa_antes_de_guardar`
filtraba por `ValueGenerated.OnAdd` y después exoneraba en bloque a toda clave `Guid`, con el motivo
escrito de que en un `Guid` eso no significa que la ponga la base. Cierto **del `INSERT`**, y ciego
al otro uso de `OnAdd`. Hoy no exime: exige la declaración positiva.

**Pero es que además no miraba Terceros.** Cinco reglas recorren el modelo entero —inquilinato,
auditoría, fechas, claves, tipos complejos— y cada una traía su lista de contextos **escrita a
mano**, con dos o tres dentro. Terceros entró en la fase 1 y no se añadió a ninguna: durante todo el
módulo, cuatro reglas que dicen «cada entidad declara…» lo estuvieron diciendo de **dos módulos de
cinco**, en verde. Es el defecto del artículo 32 con otra cara y en el mismo ítem —una obligación
transversal enumerada a mano, ciega a lo que llegue después—. Ahora el universo **se descubre**
(toda clase concreta que hereda de `ContextoDeModulo`) con su lista declarada al lado, para que un
descubrimiento corto salga rojo en vez de silencioso.

Lo que encontró al mirar Terceros por primera vez, todo declarado ya:

- **`Tercero.Version` faltaba** del inventario del ADR-0015, que se compara entero.
- **Los cinco tipos complejos de `Tercero`** no estaban en el barrido de auditoría —`LimiteCredito`
  y `RegimenFiscal`, del propio 1.6, incluidos—.
- **Los tres hijos heredan del tipo base y no llevan testigo.** Es exactamente la divergencia que
  ese caso anunciaba que algún día habría que declarar; ahora está declarada con su motivo —el
  testigo que gobierna su edición es el del tercero— en vez de permitida en blanco.
- **Y los tres no filtraban por empresa ni declaraban por qué**, que es un hueco de R8 del propio
  1.6. Se cierra como `RolDeMembresia`: motivo escrito **más** su `Set<>` prohibido por nombre. El
  de `Contacto` pesa más que los otros dos, porque esa tabla lleva nombre, correo y teléfono de
  personas identificadas y una consulta suelta los serviría de todas las empresas a la vez.

**Y una tercera vez lo mismo, esta ya sin excusa: dos ADR con el mismo número.** El ítem escribió
dos —el del artículo 32 y el de la clave— y los dos salieron `ADR-0031`. No lo cazó nadie: son dos
ficheros de texto, y ningún barrido miraba su numeración. No es cosmético, porque **el número es la
manera de citar un ADR** —lo citan los `remarks` del código, los mensajes de las aserciones, el PLAN
y los demás ADR—, así que un número compartido vuelve ambigua toda cita ya escrita: quien busque
«ADR-0031» encuentra dos decisiones distintas y no sabe cuál avalaba lo que estaba leyendo. Se
renumera el más nuevo a **ADR-0032** —fichero, título, las dos citas del ADR-0010 y las dos de
`LasPertenenciasNuevasSeInsertanTests`— y, antes de arreglarlo, se escribe la regla y **se ve roja
con la colisión puesta**: `ElNumeroDeUnAdrEsSuyoYDeNadieMasTests`. Tres afirmaciones, porque
renumerar es renombrar un fichero y renombrar un fichero deja la cabecera diciendo el número viejo,
que es la misma colisión escondida dentro.

**Seis mutaciones más, sobre árbol limpio**, revertidas restaurando copia —nunca con
`git checkout --`— y con `git status --porcelain` vacío y `grep -rn "MUTACION" src tests` sin
resultados después. Las cuatro primeras sobre la línea base `ecdd796`; las dos últimas sobre
`f2ab8d1`:

| # | Mutación | Línea base | Rojo |
|---|---|---|---|
| 9 | La convención **no se registra** | `ecdd796` | **Triple, y en dos módulos**: el efecto en Terceros (`Lo_que_se_cuelga…`), el canario del ADR-0010 en Identidad (`Colgarla_del_usuario…`) y la declaración (`Toda_entidad_tiene_su_clave_completa…`) |
| 10 | `TercerosDbContext` fuera de la lista declarada | `ecdd796` | `El_universo_de_modelos_es_el_declarado` |
| 11 | La ficha del arnés **no queda seguida** | `ecdd796` | Los **dos** casos de `LoQueCuelgaNaceComoAltaTests` — el canario es lo que impide que el principal salga verde por no haber decidido nada |
| 12 | `Contacto` sin su motivo declarado | `ecdd796` | `Ninguna_entidad_del_modelo_se_queda_sin_filtro_y_sin_motivo` |
| 13 | La cabecera de un ADR deja de decir el número de su fichero | `f2ab8d1` | `Cada_ADR_lleva_en_su_titulo_el_numero_de_su_nombre_de_fichero` |
| 14 | El barrido de ADR mira la carpeta equivocada (`docs/adr` → `docs`) | `f2ab8d1` | `El_barrido_encuentra_los_ADR_del_repositorio` |

La afirmación que falta en esa tabla es la de la colisión, y falta porque **no hizo falta mutarla**:
se vio roja contra el árbol real, con los dos `ADR-0031` dentro, antes de renumerar nada.

**Ítem 1.5 cerrado — el agregado, su identidad fiscal, y un conflicto que no revela:**
run **34046817118** sobre `279b8c7`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Frontal `101523261565` ✓ (17 pasos, 0 omitidos),
Backend `101523261629` ✓ (22 pasos, 0 omitidos) y Humo `101523797385` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 576 casos (576 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 182, Organizacion.IntegrationTests 5,
    Api.FunctionalTests 136, Api.IntegrationTests 1, Identidad.UnitTests 58,
    Arquitectura.Tests 23, Terceros.UnitTests 39
    (528 en 7 ensamblados en el 1.4: +48 casos y +1 ensamblado)

Integración (Testcontainers): 271 casos (271 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 199, y 0 en los otros seis
    (241 en el 1.4: +30 casos, que son los que en esta máquina NO se pudieron ejecutar)

Frontal: arranque 402/450 KiB en 3 ficheros · total servido 548/900 KiB
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  82 operaciones, 47 rutas /api/v1/, y el fichero del artefacto idéntico al del repositorio
Catálogo de `type`: 47 tipos, de 52 sitios de llamada — el artefacto que estrena el ítem (ADR-0030)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 2903
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

**Y este ítem se cierra con una advertencia que vale más que el verde.** El primer run del ítem —el
[34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496)— salió **rojo por un
caso de 271**, y el caso acusaba al producto: la traza no anotaba lo que el art. 32 obliga a anotar.
No era el producto: era el **captador**, que llevaba desde que se escribió sin recibir una sola línea.
Está contado entero en *Verificado en local* → *La avería que la CI destapó*, y arreglado en
`dbabdea` con su canario en el carril rápido. Dos consecuencias que sobreviven al ítem: **(a)** las
dos filas de la tabla de mutaciones que ejecutó la CI se compararon contra una **línea base roja**, y
lo que las sostiene es el conjunto de casos rojos **nombrados**, no el color del *job* — corregido en
`279b8c7` en vez de dejado escrito; **(b)** un arnés de pruebas es código sin nadie detrás, así que
la pregunta que faltaba no era «¿el dominio cumple la regla?» sino «¿la regla mira lo que dice
mirar?».

**Y una consecuencia del 1.5 que se cobró fuera del 1.5: ningún identificador fiscal real, tampoco
en los ficheros que el ítem no tocaba.** El 1.5 dejó escrito en `NifTests` que el DNI de ocho cifras
consecutivas de todos los manuales **existe y es de alguien**, y que el CIF que había antes era el de
una empresa de verdad — y a la vez los dejó vivos en cinco sitios fuera de Terceros: la **semilla** de
la CI (`BASTION_SEMILLA_EMPRESA_NIF`), la *fixture* de MSW del frontal, dos ficheros de test de
Organización, la **prosa** del ADR-0025, y hasta un comentario que los citaba para señalarlos. El
cierre del ítem lo pedía con esas tres palabras exactas —«fixtures, semillas ni prosa»—, así que se
cierra: todo pasa a números inventados con su carácter de control **calculado** (`B99999997`,
`00000001R`, y `00000001A` donde el caso pedía un control equivocado a propósito), y las citas se
reescriben para **describirlos sin escribirlos**.

Lo que **no** se toca, y con motivo: las semillas numéricas de `LaBateriaGeneradaTests`. Un generador
que recorre quince números por las veintitrés letras produce identificadores válidos, y **todo
identificador válido es de alguien o lo será** — eso no lo arregla ningún generador, y pretenderlo
sería una promesa falsa. La regla que sí se puede cumplir es la que el ítem escribió: **no pegar el
ejemplo conocido**. Por eso la única semilla que se movió es la que materializaba como literal
esperado justo el número canónico.

**Ítem 1.4 cerrado — lo bloqueado se puede mirar, y mirarlo no devuelve la llave:**
run **33906512809** sobre `73c2836`, en `main`, **success**, con **3 jobs contados en el propio
run** (`total_count: 3` de la API, no de la memoria): Backend `101132593744` ✓, Frontal
`101132594066` ✓ y Humo `101133688191` ✓ — el de Humo esta vez **corrió**, no se omitió. Los tres
carriles tal como el run los publica:

```
Dominio y arquitectura: 528 casos (528 correctos, 0 con error, 0 omitidos) en 7 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 180, Organizacion.IntegrationTests 5,
    Identidad.UnitTests 58, Api.FunctionalTests 129, Api.IntegrationTests 1, Arquitectura.Tests 23

Integración (Testcontainers): 241 casos (241 correctos, 0 con error, 0 omitidos) en 7 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 169, y 0 en los otros cinco

Frontal: arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 18 ficheros, 8 .js y 1 .css

OpenAPI:  75 operaciones, 43 rutas /api/v1/, y el fichero del artefacto idéntico al del repositorio
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 2721
      caracteres) y el entorno desplegado sirve 1 empresa
```

El run **anterior**, `33904700389` en `main` (y su gemelo `33904703923` en la rama), salió **rojo**
por un fallo de composición que en local no miraba nadie. Está contado entero abajo, en
*Verificado en local → La sexta, que no puse yo*.
`GET /api/v1/organizacion/bloqueados` entrega las empresas, almacenes y ubicaciones bloqueados de la
empresa activa, con su fecha de bloqueo y su **fecha de vencimiento**, a quien tenga
`organizacion.bloqueado.ver` y dejando en el registro **quién** ha preguntado. Es el **quinto** sitio
de `s_aperturasDeBloqueoPermitidas`, declarado con lo que lo distingue de los otros cuatro, y el
primero que no desbloquea.

**Lo que hay que leer primero de este ítem.** La afirmación del enunciado —«algo que se ponga rojo si
un camino de lectura de la API entrega un recurso bloqueado con testigo de versión»— se escribió
**antes** que el endpoint y se miró en rojo **con un DTO que sí lo llevaba**: la mutación 1 de la
tabla, entera más abajo. Salen dos rojos y no uno, y son preguntas distintas:
`Ningun_camino_que_ve_lo_bloqueado_emite_un_testigo_de_version` mira el **código fuente** —los
ficheros que abren el ámbito y los que producen `ConVersion<T>` tienen que ser disjuntos— y
`Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo` mira el **contrato**, recorriendo
los tipos que cada acción declara devolver. La segunda es la que nombra el endpoint y el campo.

**El motivo nuevo, y por qué no se reutilizó el que había.** El ADR-0027 decía en su decisión 1 que
el listado abriría el ámbito «con el motivo que ya existe». Al montarlo se vio que son dos cosas
distintas y que **la traza tiene que distinguirlas**: `AdministracionDelBloqueo` es el sistema
operando sobre el bloqueo, y esto es una persona **mirando datos personales reservados**. Bajo la
misma etiqueta, el registro que el art. 32 obliga a llevar diría lo mismo para las dos. El ADR lleva
su **corrección fechada** en vez de reescribir el texto de aquel día. Y con el segundo valor, el
enumerado pasa a ser una lista cerrada de dos con su propia regla, comparada entera y en los dos
sentidos: un valor declarado que nadie usa es una rama que nadie prueba.

**El vencimiento, que no estaba en el enunciado del ítem y sí en la nota abierta.** Un bloqueo del
art. 32 sin fecha de fin convierte una conservación acotada en indefinida, que es la infracción por
el otro lado; enseñar el listado sin esa columna habría sido enseñar lo segundo como si fuera lo
primero. `PoliticaDeRetencion` la contesta, y el plazo cuelga **del motivo**:
`SupresionSolicitada` vence, `CeseDeUso` **no vence nunca** —un almacén retirado se conserva por
razón contable y sus datos no son de nadie—, seis años por omisión (art. 30 del Código de Comercio,
el suelo más largo de los que le aplican a una pyme) y configurable por instalación. El `switch` es
exhaustivo y **lanza**: un tercer motivo tendría que decidir si vence en vez de heredar el «no
vence» que nadie habría decidido, y eso lo afirma un test con un motivo fuera del enumerado.

**El censo, autorizado explícitamente para este ítem.** `LasReglasDeEsteCarrilTests` censaba
**Arquitectura.Tests y solo ese ensamblado** desde el 0.12; los otros tres carriles con reglas
—`Api.FunctionalTests`, `Api.IntegrationTests` y `Organizacion.IntegrationTests`— tenían la rendija
abierta con **287 casos** dentro. Ahora los cuatro se censan contra su lista escrita, **310 nombres**
en total, con la consulta que los descubre en **un solo sitio** (`tests/Comun/CensoDeReglas.cs`,
enlazado con `Compile Include`): tres copias divergirían el día que una contara los `[Theory]` y otra
no. Las tres UnitTests se quedan fuera a propósito —son tests de dominio, no reglas de frontera— y
eso es una raya que se puede mover, no un olvido.

**Y el censo trajo un falso verde que llevaba ahí desde el 0.12.** `LasReglasDeEsteCarrilTests`
ordenaba con `orderby nombre, StringComparer.Ordinal`, que **no** es «ordena por nombre con este
comparador»: en la sintaxis de consulta son **dos claves de orden** —el nombre con el comparador de
la **cultura** y, a igualdad, una constante—, así que el ordinal no se aplicaba nunca. Con los 23
nombres de Arquitectura los dos órdenes coinciden y el carril salía verde; con 133 dejan de
coincidir, porque la cultura ordena «El_contador» antes que «El_NIF» y el ordinal al revés. Es la
misma familia que la nota de la cultura del ejecutor: verde justo en la máquina que tenía que avisar.
Corregido con `Order(StringComparer.Ordinal)` explícito, en el fichero compartido.

**Ítem 1.3 cerrado — el universo se descubre, y la copia divergente ya no compila:**
[run 33831276412](https://github.com/AOjeda006/Bastion/actions/runs/33831276412) sobre `edd0048`,
en verde con sus **tres jobs contados en el propio run** (`total_count: 3`) y ni un paso no verde:
Frontal 17 pasos, Backend 21, Humo 24. Los tres carriles con la cifra que publicó el run, no la que
se recuerda: **dominio y arquitectura 503 casos** (503 correctos, 0 con error) en 7 ensamblados,
**integración 237** (237 correctos, 0 con error) en 7, y el frontal con el contrato al día y el
presupuesto en 391/450 KiB de arranque y 532/900 KiB de total. El humo levantó el compose entero:
una empresa servida, sesión iniciada, y los maestros del migrador —12 tramos de impuesto, 15
unidades, IVA general vigente al 21,00 %—.
Los cuatro tipos de paginación viven en `Bastion.BuildingBlocks.Contracts` y las **ocho copias** de
Identidad y Organización están borradas; los doce listados aceptan `?sort=` y `?q=` con su lista
blanca y su tope; `POST /api/v1/organizacion/empresas/buscar` busca por **cuerpo** con cursor opaco,
con su exención escrita **en el mismo commit que el endpoint**; y `Todas()` lee su universo de la
tabla de enrutado del host, contrastada con los ensamblados `Endpoints` del disco.

**Lo que hay que leer primero de este ítem.** La **cuarta mutación** —divergir una copia
consolidada, `TamanioMaximo = 5000` en un módulo— **no sale roja: no llega a existir**. Se
intentaron las tres formas que hay y las tres las para el **compilador** (`CS0104` la copia del
contrato, `CS1503` la del borde, `CS1503` la que se salta el ayudante común y llama al caso de uso
directamente). Es la diferencia entre consolidar y copiar a un sitio nuevo, y está contada entera en
*Decisiones tomadas → ítem 1.3*. La **primera** también está entera, y por el motivo contrario: con
el universo escrito a mano restaurado y un `POST` sin proteger atendiendo peticiones, el fichero da
**siete verdes**; con el universo descubierto, **cuatro rojos**.

**Y una rendija que este ítem deja abierta a propósito:** `LasReglasDeEsteCarrilTests` censa
**Arquitectura.Tests y solo ese ensamblado** —sigue en **23** reglas, y allí lo único que cambió fue
la línea del test renombrado—. Las seis afirmaciones nuevas del 1.3 viven en `Api.FunctionalTests` y
en `Organizacion.IntegrationTests`, que **no tienen censo**: borrar una de ellas dejaría la suite
verde. No entra en el criterio del 1.3 y el checklist no se amplía por iniciativa propia, así que
queda anotado con su motivo, no tapado.

**Y una segunda rendija, esta descubierta en rojo y no de cabeza.** El primer run de este ítem
—[33830689761](https://github.com/AOjeda006/Bastion/actions/runs/33830689761)— salió **rojo**, y salió
rojo *bien*: `LaTraduccionASqlTests` hace que `Bastion.Organizacion.IntegrationTests.dll` empiece a
ejecutar casos en el **carril rápido**, y la lista de ensamblados declarados de ese paso —la defensa
que el 0.13 puso justamente para esto— exige declararlo. Se declaró, con su motivo escrito donde está
la lista.

Lo que importa no es el arreglo, que es una línea: es que **la batería local de `AGENTS.md` no podía
cazarlo**. La batería ejecuta los tests, pero no ejecuta `scripts/ci/recuento-de-tests.sh`, que es el
guion que decide el desenlace del paso en la CI. Así que hoy hay una clase entera de rojo —«un ítem
cambia qué ensamblados corren en un carril»— que pasa la batería entera en verde y solo aparece en el
run. El propio `AGENTS.md` dice que su lista «tiene que seguir siendo la de `.github/workflows/ci.yml`»
y se llama a sí mismo «el peor sitio posible para una mentira»; esto es una omisión de esa lista, no
una diferencia de comandos. Se reprodujo en local con el guion real sobre los `.trx` de esta máquina
—rojo nombrando el ensamblado, verde tras declararlo— así que no hace falta la CI para arreglarlo,
solo para haberlo visto. **Candidato para el ítem que toque el carril**, no para este: el 1.3 no lo
lleva en su criterio.

**Ítem 1.2 cerrado — la cuarta vía tiene quien la pare, y la quinta afirmación es la que la para:**
[run 33806271861](https://github.com/AOjeda006/Bastion/actions/runs/33806271861) sobre `c54f783`,
los **tres** *jobs* en `success` —Backend, Frontal y Humo— y **237 de 237** en el carril de
integración, que es donde viven los siete casos nuevos que aquí no se podían ejecutar. El mismo
árbol, ya en `main`, en el
[run 33806915660](https://github.com/AOjeda006/Bastion/actions/runs/33806915660).
`Organizacion.Contracts` publica `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e
`IConsultaDeDivisas`, los tres implementados, registrados y probados contra PostgreSQL; el cruce de
Identidad a `Organizacion.Contracts` está declarado; y `LosIdentificadoresAjenosTests` afirma las
cinco cosas de la tabla de *Decisiones tomadas → ítem 1.2*.

**Lo que hay que leer primero de este ítem.** La segunda mutación —un identificador de otro módulo
con **nombre que no casa** y **sin declarar**— **se queda verde con las cuatro afirmaciones del
enunciado**. La caza una sola cosa, y es la quinta afirmación, que no estaba: «ningún `Guid …Id` del
dominio se queda sin clasificar». Sin ella el ADR-0024 **describiría** la cuarta vía sin cerrarla,
porque el descubrimiento por nombre es exactamente lo que un identificador mal nombrado esquiva. Con
ella, el hueco está cerrado y comprobado: la mutación cae en un test y solo en uno.

El carril de arquitectura pasa de **18 a 23** casos.

### Verificado en local, con la salida real — ítem 1.7

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `3a937a3` (main al abrir
la rama); el «después», `b9ca5d6` (el árbol que se lleva a main al cerrar).

**Y esta vez el carril de integración SÍ se pudo ejercer en local.** Docker Desktop estaba
instalado y **parado**, no ausente: la firma con la que fallaba era
`DotNet.Testcontainers.Builders.DockerUnavailableException : Docker is either not running or
misconfigured … Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine' … ----
System.TimeoutException : The operation has timed out`, con **334 casos rojos de 334**. Arrancado el
motor, los 334 salen verdes. Es la primera vez desde el 1.4 que las dos mitades de este informe se
miden en la misma máquina, y es lo que hace que las **ocho** mutaciones sean atribuibles aquí en vez
de solo en el *runner*.

```
batería:      dotnet build  ->  0 Advertencia(s), 0 Errores
              dotnet format --verify-no-changes  ->  exit 0
              generar-errores.sh --comprobar  ->  al día, 51 tipos de 56 sitios  (49 de 54 en el 1.6)
              generar-openapi.sh --comprobar  ->  al día, 102 operaciones  (93 en el 1.6)
              comprobar-migraciones.sh  ->  Organizacion 5, Identidad 3, Terceros 2, y el modelo
                                            coincide con ellas  (Organizacion eran 4)

dominio y arquitectura:
  bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio "Dominio y arquitectura" 300 \
    "Bastion.Api.FunctionalTests.dll,Bastion.Api.IntegrationTests.dll,Bastion.Arquitectura.Tests.dll,\
Bastion.BuildingBlocks.UnitTests.dll,Bastion.Identidad.UnitTests.dll,\
Bastion.Organizacion.IntegrationTests.dll,Bastion.Organizacion.UnitTests.dll,Bastion.Terceros.UnitTests.dll"

  651 casos (651 correctos, 0 con error, 0 omitidos) en 8 ensamblados
    — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Organizacion.UnitTests 183,
      Terceros.UnitTests 77, Organizacion.IntegrationTests 21, Arquitectura.Tests 34,
      Api.FunctionalTests 140, Api.IntegrationTests 6
    (630 en el 1.6 sobre `3a937a3`, los mismos 8 ensamblados: +21 casos)

integración (Testcontainers):
  bash scripts/ci/recuento-de-tests.sh artifacts/test-results/integracion "Integración (Testcontainers)" 100 \
    "Bastion.Api.IntegrationTests.dll,Bastion.Organizacion.IntegrationTests.dll"

  334 casos (334 correctos, 0 con error, 0 omitidos) en 8 ensamblados
    — Organizacion.IntegrationTests 74, Api.IntegrationTests 260, y 0 en los otros seis
    (318 en el 1.6 sobre `3a937a3`: +16 casos)

  El +16, atribuido entero: `LaInversaYElResolutorTests` 10 (seis `[Fact]` y dos `[Theory]` de dos
  filas), `LaRetiradaNoEsUnBloqueoTests` 4, y `LosPuertosDeLecturaTests` +2 (los dos casos de
  `SoloResuelveLoViejo`, de `93b8e5f`).

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (los mismos que en el 1.6)
         presupuesto  ->  arranque 403/450 KiB en 3 ficheros · total servido 549/900 KiB  (548 en el 1.6)

  cd frontend && npm test -- --run 2>&1 | grep -c "not wrapped in act"
    Antes  — `3a937a3`: 109 avisos en 7 ficheros
    Después — `b9ca5d6`: 109 avisos, la MISMA cifra.
  No se mueve, y el motivo está en el `git diff --stat 3a937a3..b9ca5d6 -- frontend/`: solo
  `esquema.ts` (generado), `es.ts` y `en.ts`. Ni un componente, ni una pantalla.
```

**Licencias — ningún paquete nuevo, con el comando pegado y su salida.**

```
git diff 3a937a3..b9ca5d6 -- '*.csproj' | awk '/^diff --git/{f=$4} /^\+.*PackageReference/{print f" -> "$0}'
    (sin salida)
```

Cero líneas: el ítem no añade ni una `PackageReference`. El único cambio en un `.csproj` es un
`InternalsVisibleTo` hacia un ensamblado del propio repositorio. Se mide con `awk` sobre el
`diff --git` y **no** con `grep -B`, que atribuye por proximidad y le cuelga el paquete al fichero
equivocado en cuanto hay dos `.csproj` seguidos.

### Las ocho mutaciones del 1.7, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio**, línea base `b9ca5d6`, aplicadas con copia de respaldo y revertidas
**restaurando esa copia** —nunca con `git checkout --`, que se llevaría por delante todo lo no
commiteado del fichero—. Después de la tanda: `git status --porcelain` **vacío** y
`grep -rn MUTACION` **sin resultados**, los dos comprobados.

| # | Mutación | Dónde | Qué se pone rojo |
|---|---|---|---|
| 1 | Quitar las dos marcas `[CubreEstadoDeMaestro(…, SoloResuelveLoViejo)]` de divisa y unidad — el estado *antes* del arreglo | `LosPuertosDeLecturaTests.cs` | `LaMatrizDePuertoYEstadoTests.Cada_casilla_de_puerto_por_estado_esta_cubierta`, nombrando **las dos casillas** |
| 2 | Publicar `DELETE /divisas/{id}` | `DivisasController.cs` | `NingunMaestroRetirableSeBorraTests.Ningun_recurso_retirable_publica_el_borrado_de_una_fila` |
| 3 | El `GET` por identificador de una fila retirada devolviendo **404** | `ConsultasDeDivisas.cs` | `LaRetiradaNoEsUnBloqueoTests.El_GET_por_identificador_sigue_devolviendo_la_fila_retirada` (y de rebote `Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva`) |
| 4 | La colección incluyendo lo retirado por omisión | `Paginador.cs` | `LaRetiradaNoEsUnBloqueoTests.La_coleccion_excluye_lo_retirado_por_omision_y_lo_trae_al_pedirlo` |
| 5 | Quitar la comprobación de la inversa de la **modificación**, dejándola solo en el alta | `ModificarConversionUm.cs` | `LaAritmeticaDeLaInversaTests.La_modificacion_vuelve_a_comprobar_la_inversa` **y** `LaInversaYElResolutorTests.La_modificacion_vuelve_a_comprobar_la_inversa` (409 → 200) |
| 6a | `<` donde va `≤` | `LaInversaEsPlausible.cs` | `La_desigualdad_…(1.001, 0.999, casan: True)` **y** `El_par_de_la_frontera_se_separa_EXACTAMENTE_el_margen` |
| 6b | La tolerancia relajada un orden de magnitud (`5·10⁻⁶`) | `LaInversaEsPlausible.cs` | `El_margen_sale_de_la_escala_…`, `El_par_de_la_frontera_…` y **dos** filas del `[Theory]`: `(1.001, 0.998999)` y `(12, 0.083334)`, que pasan a aceptarse |
| 7 | El resolutor componiendo la cadena por el listado en vez de fallar | `ResolverConversionUm.cs` | `LaInversaYElResolutorTests.El_par_que_habria_que_encadenar_no_se_resuelve` (404 → 200) y, en el carril rápido, el doble negándose a listar |
| 8 | El resolutor devolviendo una resolución vacía en vez del error con nombre | `ResolverConversionUm.cs` | `El_par_que_habria_que_encadenar_no_se_resuelve` en **los dos** carriles, más `El_sentido_contrario_no_se_deduce_invirtiendo` |

**La 1 entera, porque es el hallazgo del ítem.** Es la única que no es una mutación inventada: es el
estado **real** del repositorio antes de este trabajo, y por eso se commiteó en rojo en `cf46416`
antes de arreglar nada.

```
Con error Bastion.Organizacion.IntegrationTests.Persistencia.LaMatrizDePuertoYEstadoTests
          .Cada_casilla_de_puerto_por_estado_esta_cubierta
  Shouldly.ShouldAssertException : sinDuenio
    should be empty but had 2 items and was
["IConsultaDeDivisas -> SoloResuelveLoViejo", "IConsultaDeUnidadesDeMedida -> SoloResuelveLoViejo"]

  Additional Info:
    hay casillas de la matriz puerto × estado que ningún caso afirma. Una casilla sin dueño
    significa una de dos cosas, y las dos son malas: o el puerto no puede contestar ese valor —y
    entonces la lista cerrada tiene un valor inalcanzable, que es el defecto que abrió el ítem 1.7—
    o puede y nadie lo comprueba. Se cierra marcando con `CubreEstadoDeMaestro` el caso que lo
    afirma, y si ese caso no existe es que hay que escribirlo
```

**La 3 entera, porque es la única línea que separa retirada de bloqueo y no la vigila nada más.**
Las dos cosas quitan una fila de en medio sin borrarla, las dos son reversibles, y las dos se leen
igual en un diagrama; al que venga después le va a parecer natural «unificar» los dos
comportamientos, y el 404 es lo más fácil de copiar porque **ya está escrito** para el bloqueo.

```
Con error Bastion.Api.IntegrationTests.Retiradas.LaRetiradaNoEsUnBloqueoTests
          .El_GET_por_identificador_sigue_devolviendo_la_fila_retirada
  Shouldly.ShouldAssertException : lectura.StatusCode
    should be HttpStatusCode.OK but was HttpStatusCode.NotFound

  Additional Info:
    una divisa retirada NO desaparece: se sigue devolviendo por su identificador. Eso es lo que la
    distingue de una fila bloqueada, que responde 404 a propósito.
    {"type":"/errors/divisa-no-encontrada","title":"Recurso no encontrado","status":404,
     "detail":"No hay ninguna divisa con el identificador 01a07ea3-9e87-72e7-9178-91ca5a3290fc.",
     "instance":"/api/v1/organizacion/divisas/01a07ea3-9e87-72e7-9178-91ca5a3290fc", …}

Con error … .Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva
  Shouldly.ShouldAssertException : lectura.StatusCode
    should be HttpStatusCode.OK but was HttpStatusCode.NotFound
```

**Y el segundo rojo de la 3 dice algo que no estaba en el enunciado:** con el 404 puesto, la retirada
deja de poder **deshacerse**. Reincorporar necesita el `ETag` de la fila, y el `ETag` sale del `GET`
que acaba de contestar 404. En cuatro maestros de instalación (R8) —donde retirar por error deja sin
esa fila a **todas** las empresas— eso convierte un error reversible en uno permanente. La línea que
separa retirada de bloqueo no es solo una cortesía con las facturas viejas: es lo que sostiene la
puerta de vuelta.

### Verificado en local, con la salida real — ítem 1.6

Medido sobre `911ba5c`, que es el árbol que se lleva a `main`, con la batería de `AGENTS.md`
entera y en su orden.

```
contrato:     npm run api  ->  `esquema.ts` regenerado y SIN cambios
                              (`git status --porcelain` sobre el fichero, vacío)
migraciones:  Auditoria 3, Organizacion 4, Identidad 3, Terceros 2 — y el modelo
              coincide con ellas en los cuatro. La convención `LaClaveLaPoneElDominio`
              NO añade migración: `ValueGenerated` sobre una clave `Guid` no produce
              diferencia relacional
openapi:      documento versionado al día — 93 operaciones (82 en el 1.5)
catálogo de `type`: al día — 49 tipos, de 54 sitios de llamada (47 de 52 en el 1.5)

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (los mismos que en el 1.5)
         presupuesto  ->  arranque 403/450 KiB en 3 ficheros · total servido 548/900 KiB
         act()  ->  109 avisos en 7 ficheros

backend: dotnet build  ->  0 Advertencia(s), 0 Errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido  ->  630 casos (630 correctos, 0 con error, 0 omitidos) en 8
                            ensamblados, medido con el guion de la CI sobre los .trx
                            de esta máquina y con `artifacts/test-results` borrado antes:
                            BuildingBlocks.UnitTests 132, Terceros.UnitTests 77,
                            Identidad.UnitTests 58, Organizacion.UnitTests 182,
                            Organizacion.IntegrationTests 4, Arquitectura.Tests 34,
                            Api.FunctionalTests 137, Api.IntegrationTests 6
                            (576 en el 1.5, los mismos 8 ensamblados: +54 casos)
         carril integración -> NO EJECUTADO AQUÍ. Abajo, con firma.

licencias:   una sola línea de `PackageReference` añadida en todo el `git diff` del ítem
             —`Npgsql.EntityFrameworkCore.PostgreSQL`, en `BuildingBlocks.Infrastructure`,
             que es el bloque común y NO un módulo—, y **no es un paquete nuevo**: ya
             estaba en `Directory.Packages.props` (10.0.3), que el ítem no toca, y su
             licencia —PostgreSQL, permisiva estilo BSD— está ya declarada allí.
             `frontend/package.json` y `package-lock.json`, sin mover. Ninguna licencia
             nueva entra con este ítem. Y el comando importa, porque el primero que usé
             —`grep -B25`— atribuye por CERCANÍA y me hizo nombrar dos módulos que no
             eran; la atribución sale de la cabecera del propio diff:
             `git diff e3a9e9e..3a937a3 -- '*.csproj' | awk '/^diff --git/{f=$4} /^\+.*PackageReference/{print f" -> "$0}'`
```

**Los `act()`, con los dos extremos nombrados y esta vez medidos los dos.** El «después» del ítem se
había medido en `c7b9f80` y se sostenía sobre un `git diff` vacío de `frontend/`. Al cerrar se
vuelve a medir, ya sobre el árbol final:

```
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
```

- **Antes — `e3a9e9e`** (main al abrir): **109 avisos en 7 ficheros**.
- **Después — `911ba5c`** (el árbol que se lleva a main): **109 avisos en 7 ficheros**, y el mismo
  reparto exacto: `ElListadoDeAlmacenes` 24, `ElCambioDeIdioma` 17, `LasRutasProtegidas` 17,
  `ElSelectorDeEmpresa` 14, `ElCambioDeRuta` 14, `ElTestigoDeAcceso` 13, `LaPantallaDeAcceso` 10.

`git diff --stat c7b9f80 911ba5c -- frontend/` sigue saliendo **vacío**, así que la medida de
`c7b9f80` valía; lo que la sostiene ahora no es ese argumento sino la medición del extremo real.

**El carril de integración no se ha ejecutado aquí, y esto es lo que hay que creerse y lo que no.**
El demonio de Docker sigue parado en esta máquina. Ejecutado de todos modos, el guion de la CI
contesta:

```
Integración (Testcontainers): 318 casos (0 correctos, 318 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 246, y 0 en los otros seis
```

Los 318 son **el mismo error repetido**, no 318 fallos: `DotNet.Testcontainers.Builders.
DockerUnavailableException : Docker is either not running or misconfigured … Details: Failed to
connect to Docker endpoint at 'npipe://./pipe/docker_engine' … System.TimeoutException : The
operation has timed out.` **Ni uno solo de esos casos se ha ejecutado.** Lo que sí dice esa salida
—y es lo único que dice— es que el barrido local **alcanza exactamente los mismos 318 casos** que
la CI ejecutó en verde: no falta ninguno por descubrir, falta el demonio.

**Y lo que se ejerció en su lugar, que es la parte que este ítem sí puede reclamar.** El defecto
central del ítem se veía **solo** en ese carril —siete rutas contestando 412 contra PostgreSQL— y
resultó no necesitarlo: decidir si una entrada es alta o modificación necesita el proveedor y el
modelo, no un servidor. `LoQueCuelgaNaceComoAltaTests` lo comprueba con la cadena de conexión
apuntando al puerto 1, en el carril rápido, en un segundo, y con su canario al lado. Lo mismo hacen
las cinco reglas de modelo, que leen el modelo ya construido sin abrir conexión. La regla nueva de
los ADR (`ElNumeroDeUnAdrEsSuyoYDeNadieMasTests`) tampoco necesita nada: lee el árbol.

### Verificado en local, con la salida real — ítem 1.5

```
contrato:     npm run api  ->  `esquema.ts` regenerado y SIN cambios
                              (`git status --porcelain` sobre el fichero, vacío)
migraciones:  Auditoria 3, Organizacion 4, Identidad 3, Terceros 1 — y el modelo
              coincide con ellas en los cuatro
openapi:      documento versionado al día — 82 operaciones (75 en el 1.4: las SIETE
              del módulo son las de Terceros)
catálogo de `type`: al día — 47 tipos, de 52 sitios de llamada. Es el artefacto que
              este ítem estrena (ADR-0030), y su paso EXISTÍA en la CI y NO en la
              batería de AGENTS.md: arreglado en el primer commit de esta tanda

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (eran 9 y 46 en el 1.4)
         presupuesto  ->  arranque 402/450 KiB en 3 ficheros · total servido 548/900 KiB
                          el trozo nuevo es `PaginaDeTerceros-CiAyuBs4.js`, 4,64 kB
         act()  ->  109 avisos en 7 ficheros, la MISMA cifra antes y después.
                    Los dos ficheros de test nuevos —`ElListadoDeTerceros.test.tsx` y
                    `ElTextoDeUnFallo.test.tsx`— no aparecen en el desglose: entran
                    con CERO

backend: dotnet build  ->  0 Advertencia(s), 0 Errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido  ->  576 casos (576 correctos, 0 con error, 0 omitidos) en 8
                            ensamblados, medido con el guion de la CI sobre los .trx
                            de esta máquina y con `artifacts/test-results` borrado antes:
                            BuildingBlocks.UnitTests 132, Organizacion.UnitTests 182,
                            Identidad.UnitTests 58, Terceros.UnitTests 39,
                            Organizacion.IntegrationTests 5, Arquitectura.Tests 23,
                            Api.FunctionalTests 136, Api.IntegrationTests 1
                            (528 en 7 ensamblados en el 1.4: +48 casos y +1 ensamblado.
                            Los DOS últimos son el canario del captador de registro, que
                            entró después de que la CI destapara el falso verde)
         carril integración -> NO EJECUTADO AQUÍ. Abajo, con firma.

licencias:   los conjuntos de paquetes resueltos de TODOS los `packages.lock.json` a
             los dos lados: 145 en `main`, 150 en la rama, y las cinco de diferencia
             son los cinco proyectos nuevos del propio módulo. CERO paquetes de
             terceros añadidos y cero retirados; `Directory.Packages.props` sin tocar
             y `frontend/package*.json` sin mover. Sale del diff, no de la memoria.
             Un `packages.lock.json` SÍ cambia —el de `Api.IntegrationTests`, donde
             Serilog pasa de `CentralTransitive` a `Direct` al arreglar los captadores—
             y no mueve la cifra: mismo nombre, misma versión, mismo `contentHash`.
             Cambia quién lo pide, no qué se resuelve, y por tanto ninguna licencia
```

**El carril de integración no se ha ejecutado aquí, y esto es lo que hay que creerse y lo que no.**
El demonio de Docker sigue parado en esta máquina. Ejecutado de todos modos, el guion de la CI
contesta:

```
Integración (Testcontainers): 271 casos (0 correctos, 271 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 199, y 0 en los otros seis
```

Los 271 son **el mismo error repetido**, no 271 fallos: `DotNet.Testcontainers.Builders.
DockerUnavailableException : Docker is either not running or misconfigured … Failed to connect to
Docker endpoint at 'npipe://./pipe/docker_engine'`. **Ni uno solo de esos casos se ha ejecutado**, así
que de este carril no se afirma nada desde aquí: eran 241 en el 1.4 y son 271, o sea que el ítem
añade **30 casos de integración** que en esta máquina no han corrido. Los verifica la CI.

**Con qué se ha ejercido en su lugar.** Dos cosas, y conviene separarlas porque valen distinto:

1. **Lo que no necesita servidor se ha ejercido de verdad, y es la mitad más algorítmica del ítem.**
   La validación de NIF, NIE y CIF vive entera en `Terceros.UnitTests` —39 casos, sin contenedor—, y
   con ella la batería generada en los dos sentidos. Las reglas de contrato del ítem —que ninguna
   respuesta lleve testigo de versión en el cuerpo, que cada acción declare su permiso, que el
   catálogo de `type` sea el que la API emite— corren en `Api.FunctionalTests`, con el host en pie y
   sin una sola dependencia externa: 134 casos, todos aquí.
2. **Lo que sí necesita PostgreSQL se ha ejercido EN LA CI, y a propósito.** Las dos mutaciones de la
   tabla que solo el carril de integración puede cazar —la 1 y la 3— se empujaron cada una en su
   rama para que la CI las ejecutara de verdad, en vez de darlas por rojas. Los dos runs, sus números
   y su desenlace están en la tabla. Es la diferencia entre «esto lo cazaría el carril de
   integración» y «esto lo cazó el carril de integración, run tal».

Lo que queda sin ejercer en ningún sitio hasta el run del ítem son los **30 casos nuevos en verde**:
que el alta devuelva 201 con `Location`, que el domicilio vaya y vuelva en los seis campos de R17, que
el cursor no repita, que el listado pagine con su total. Esos los dice el run, y solo el run.

#### Las seis mutaciones del 1.5, cada una aplicada, ejecutada y revertida

Las cuatro primeras filas se ejecutaron **en esta máquina**; la 1 y la 3 necesitan PostgreSQL, así que
se empujaron en su propia rama y las ejecutó la CI. Todas se aplicaron sobre **árbol limpio y con el
trabajo del ítem ya commiteado**, y se revirtieron con `git restore --source=HEAD` o borrando el
fichero — nunca con `git checkout --`, que es la orden que en el 1.4 se llevó por delante 128 líneas
de trabajo sin guardar.

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | **El conflicto del alta distingue lo bloqueado de lo que ya existe**: `IdentificacionDuplicada(bool bloqueada)` con dos redacciones, y una segunda consulta fuera del ámbito para saber cuál es | **Rojo en la CI, con la línea base ya en rojo por otra cosa** (aviso debajo de la tabla), [run 33974541031](https://github.com/AOjeda006/Bastion/actions/runs/33974541031) — Backend en `failure` en el paso 13, *Tests de integración (Testcontainers)*. Compila con 0 avisos, y el **carril rápido entero, el formato, las migraciones, el OpenAPI y el catálogo de `type` pasan en verde**: no la ve nadie más | `ElConflictoQueNoRevelaTests.El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`. Entera abajo, con las dos respuestas puestas una al lado de la otra |
| 2 | **El CIF acepta el carácter de control de la clase equivocada**: la rama de iniciales alfabéticas admite además el dígito | **Rojo ×2** en el carril rápido | `LaBateriaGeneradaTests.El_control_de_la_clase_equivocada_se_rechaza` (Terceros) y `NifTests.Un_identificador_con_el_caracter_de_control_equivocado_se_rechaza(valor: "P12345674")` (Organización). Dos carriles distintos y ninguno de los dos escrito para el otro |
| 3 | **La unicidad del alta deja de ver las fichas bloqueadas**: `&& !tercero.Bloqueo.EstaBloqueado` en el `Where` del repositorio | **Rojo en la CI, con la línea base ya en rojo por otra cosa** (aviso debajo de la tabla), [run 33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347) — Backend en `failure` en el mismo paso 13. Igual que la 1: compila, y el carril rápido entero sale verde | `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca` y `El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`. El alta pasa el filtro y choca contra el índice único, que sigue siendo incondicional |
| 4 | **Un código de error nuevo sin regenerar el artefacto** (`regimen-fiscal-no-admitido`) | **Rojo ×1**, y antes de compilar nada | `scripts/generar-errores.sh --comprobar`: «El catálogo de errores ha cambiado y `docs/api/errores.json` se ha quedado atrás», con el diff de la entrada que falta. Es el paso que la batería de `AGENTS.md` no nombraba |
| 5 | **`TerceroDto` con un campo de versión**, copiado a mano como `uint` para que no dependa de que exista un tipo | **Rojo ×1** en el carril rápido, nombrando **las cinco** respuestas | `NingunaLecturaEntregaTestigoDeVersionTests.Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo`, la regla del 1.4: «estas respuestas entregan un testigo de versión en el cuerpo, así que la llave que las cuatro exenciones de `If-Match` de los desbloqueos dan por inalcanzable ya se puede conseguir leyendo» |
| 6 | **Una regla nueva en un carril, sin su línea en el censo** (`MutacionSeisTests` en `Api.FunctionalTests`) | **Rojo ×1** | `ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados`: «estas reglas corren en `Bastion.Api.FunctionalTests` y no están declaradas: `MutacionSeisTests.Una_regla_nueva_que_nadie_ha_declarado`» |

**Un intento fallido de la 5, que dice algo del proyecto.** La primera forma —poner en el DTO el tipo
`VersionDeRecurso` de BuildingBlocks— **no llega a compilar**, y no por la regla sino por la
arquitectura: ese tipo vive en `BuildingBlocks.Application`, y `Terceros.Contracts` no puede
referenciar Application. La segunda —copiar el `uint` a mano— sí compila, y es además «la forma en que
esto se cuela de verdad», que es lo que dice el comentario de la propia regla. La tercera cosa que
apareció es de rutina: sin `<param name="Version">` el compilador para con `CS1573` antes de que la
regla llegue a mirar nada, o sea que la mutación hay que **terminarla** para que la comprobación
signifique algo.

**Y lo que dicen las mutaciones 1 y 3 juntas.** Las dos compilan, las dos pasan `dotnet format`, las
dos pasan el carril rápido entero —574 casos entonces, 576 ahora—, las dos pasan las migraciones, el OpenAPI y el catálogo
de `type`, y las dos rompen la propiedad del ítem. O sea que **de estas dos averías no avisa nada que
se pueda ejecutar sin Docker**, y esa es exactamente la razón por la que valía la pena gastar dos runs
en verlas rojas en vez de razonarlas.

> **Aviso sobre las dos filas que ejecutó la CI: la línea base contra la que se compararon NO
> estaba en verde.** Cuando se empujaron las mutaciones 1 y 3, el run del propio ítem —el
> [34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496)— ya salía rojo por
> **un caso de 271**: `ElConflictoQueNoRevelaTests.La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_
> el_identificador_dentro`, con `anotados.Count should be 2 but was 0`. Un rojo comparado contra un
> rojo no demuestra por sí solo lo que dice la tabla, y decirlo de otro modo sería exactamente el
> falso verde con los colores cambiados. Lo que sostiene las dos filas es el **recuento de casos
> rojos**, leído en las anotaciones de cada run: la base tiene **1**, la mutación 1 tiene **2** —el
> caso añadido es `El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`, y su
> mensaje enseña los dos `detail` distintos— y la mutación 3 tiene **3** —los mismos, más
> `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca`, los dos con
> `500 Internal Server Error`, que es el choque contra el índice único que la mutación predice—.
> Cada mutación añade **sus** rojos, nombrados, y ninguno de ellos es el de la base. La atribución
> se sostiene; la afirmación «sobre verde», no, y por eso se corrige aquí en vez de dejarla escrita.
>
> El rojo de la base tenía causa propia y está arreglado en `dbabdea`, en su fila de abajo.

#### La avería que la CI destapó y que ningún carril miraba: **el arnés no tenía arnés**

El caso rojo de la base no era un fallo del producto. Era el **captador** el que no capturaba.

`src/Api/Program.cs` hace `builder.Logging.ClearProviders()` y a continuación
`builder.Services.AddSerilog(...)`, que instala `SerilogLoggerFactory` — una fábrica de registro que
**ignora todo `ILoggerProvider` que no sea el suyo**. `RegistroDeSucesos` y `RegistroDeFallos` eran
proveedores, enganchados con `builder.ConfigureLogging(r => r.AddProvider(...))`. Entre la API que
escribía y el captador que existía **no había ningún cable**.

Lo que hace esto peor que un test mal escrito es cómo se lee el resultado. El test preguntaba
«¿cuántos sucesos se anotaron?», recibía **cero**, y cero tiene dos lecturas indistinguibles desde
fuera: «la API no anota» —un fallo del producto, que es lo que el rojo parecía decir— y «no hay
cable». La segunda es la verdadera, y no había nada que las separara.

**Y el gemelo llevaba roto desde el día que se escribió.** `RegistroDeFallos` existe por un motivo
muy concreto: los registros de un *job* de la CI devuelven 403 sin autenticar, así que un `500` allí
es una pared, y este captador adjunta el registro del servidor al mensaje de la aserción —que sí sale
publicado como anotación—. En el run de la mutación 3, el
[33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347), hay un `500` de verdad
y detrás **ni una línea** de `· registro del servidor:`. O sea que el diagnóstico falló en el
escenario exacto para el que fue construido, y no se supo hasta ahora. Eso no es una deducción: es lo
que enseña la anotación de aquel run.

**El arreglo, y por qué está donde está.** Los dos captadores pasan a ser `ILogEventSink` de Serilog,
que es lo que esa fábrica sí reparte. El enganche vive en `tests/Comun/CapturaDeRegistro.cs`,
**compartido y no copiado**, y esa decisión es la que da valor al canario: lo que se rompió no fue el
captador sino **cómo se enganchaba**, así que un canario con su propio enganche habría comprobado su
propia copia y habría dejado sin vigilar la línea que falla.
`ElCaptadorDeRegistroCapturaTests` ejecuta **esa misma línea** y vive en `Api.FunctionalTests`, el
carril rápido, porque un cable roto no necesita PostgreSQL para notarse — puesto donde solo corre con
Docker delante, habría avisado en la CI y no en la máquina de quien lo rompe, que es literalmente lo
que acaba de pasar.

Sus dos casos se vieron rojos, cada uno con su mutación sobre árbol limpio y revertida con
`git restore --source=HEAD`:

| Mutación sobre `CapturaDeRegistro` | Desenlace |
|---|---|
| El sumidero se registra pero no se engancha (`WriteTo.Sink` fuera del bucle) | **Rojo ×1** — `Lo_que_la_API_escribe_llega_al_sumidero_con_su_identificador_de_suceso`. Es la avería original, reproducida |
| `EventIdDe` se queda con el primer entero que encuentre, se llame como se llame | **Rojo ×1** — `Un_evento_sin_identificador_de_suceso_no_se_lee_como_si_tuviera_uno`. Un captador que recoge de más miente igual que uno que no recoge |

**Lo que este ítem aprende y no estaba escrito en ninguna regla.** Un arnés de pruebas —un captador,
un espía, un doble— es código sin nadie detrás: si deja de funcionar, lo que se ve no es un rojo sino
un verde, o un rojo que acusa al sitio equivocado. La pregunta que faltaba hacerse no era «¿el
dominio cumple la regla?» sino «¿la regla mira lo que dice mirar?». Aquí la respuesta llevaba siendo
que no desde el ítem que lo escribió.

#### La primera, entera

Es la del ítem, así que va con las dos respuestas puestas una al lado de la otra, que es lo que la
propiedad exige comparar. Sin mutar, el cuerpo que contesta el alta contra un tercero **activo** y el
que contesta el alta contra uno **bloqueado** son el mismo carácter a carácter salvo `traceId`:

```json
{
  "type":     "/errors/tercero-duplicado",
  "title":    "Conflicto",
  "status":   409,
  "detail":   "Esta empresa ya tiene un tercero con ese identificador fiscal.",
  "instance": "/api/v1/terceros/terceros",
  "traceId":  "«el de cada petición»"
}
```

La mutación le da al error el parámetro que no tenía y una redacción por caso:

```csharp
internal static ErrorDeOperacion IdentificacionDuplicada(bool bloqueada) =>
    ErrorDeOperacion.Conflicto(
        "tercero-duplicado",
        bloqueada
            ? "Ese identificador fiscal pertenece a una ficha dada de baja."
            : "Esta empresa ya tiene un tercero con ese identificador fiscal.");
```

y en el caso de uso, la segunda consulta —fuera del ámbito— que le dice cuál es, con la excusa más
razonable que hay escrita en su propio comentario, «un mensaje más útil»:

```csharp
bool visibleSinAbrirElAmbito = await terceros
    .ExisteLaIdentificacionAsync(empresaId, identificacion.Pais, identificacion.Numero, cancelacion)
    .ConfigureAwait(false);

return Resultado.Fallo<TerceroDto>(
    ErroresDeTercero.IdentificacionDuplicada(!visibleSinAbrirElAmbito));
```

Con eso, las dos respuestas dejan de ser la misma en el campo `detail`, y el test falla comparándolas
enteras — no leyendo una y buscándole la palabra «bloqueado» dentro:

```
las dos respuestas de conflicto se distinguen, así que el formulario de alta dice quién está bloqueado.
   contra el activo:    …"detail":"Esta empresa ya tiene un tercero con ese identificador fiscal."…
   contra el bloqueado: …"detail":"Ese identificador fiscal pertenece a una ficha dada de baja."…
```

**Por qué el test compara y no lee.** Un caso que afirmara «el mensaje no dice *bloqueado*» se quedaría
verde con esta mutación cambiándole tres palabras a la redacción —«ese identificador está reservado»—
y la fuga seguiría abierta: quien tenga el formulario de alta puede recorrer identificadores fiscales
y sacar la lista de quién está dado de baja en esa empresa, que es justo lo que el art. 32 de la
LOPDGDD manda reservar. Por eso lo que se compara es el **estado, el `Content-Type`, y el cuerpo
entero con las claves ordenadas y solo `traceId` normalizado** — y por eso el `detail` no lleva el
número dentro, que si no las dos respuestas no serían comparables enteras y habría que decidir qué
trozos no cuentan.

**Y el testigo que impide el falso verde.** El mismo caso da de alta antes contra un identificador
**libre** y exige `201`. Sin esa línea, un servidor que contestara 409 a todo —o que estuviera roto de
la misma manera en los dos caminos— pasaría por indistinguible sin haber comprobado nada. La otra
mitad va en dirección contraria y es un caso aparte: `La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_
el_identificador_dentro` exige **dos** anotaciones del suceso 8400, una con `bloqueada: True` y otra
con `bloqueada: False`, y que ninguna de las dos lleve el número fiscal. Sin él, «la traza lo
registra» sería una promesa escrita en un comentario.


### Verificado en local, con la salida real — ítem 1.4

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts regenerado desde openapi.json y SIN cambios (el DTO nuevo ya
         estaba dentro): `git status --porcelain` sobre el fichero, vacío
         test  ->  9 ficheros, 46 casos, 0 en rojo
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB
                          (el ítem no toca el frontal: los mismos números que el 1.3)

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
              (este ítem NO toca el esquema: añade una lectura sobre lo que ya hay)
openapi:      el documento versionado está al día — 75 operaciones, una más que el 1.3,
              y es GET /api/v1/organizacion/bloqueados. El 75 sale de dos fuentes que no
              se derivan una de otra: el guion del contrato y el recuento de
              `El_barrido_encuentra_el_inventario_entero`

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0 (tres avisos corregidos antes:
                               IDE0037, IDE0007 e IDE0270 en el código nuevo)
         carril rápido     ->  132 + 180 + 58 + 1 + 23 + 5 + 129 = 528 casos, 0 con error
                               (BuildingBlocks / Organizacion.Unit / Identidad /
                                Api.Integration SIN Docker / Arquitectura /
                                Organizacion.Integration SIN Docker / Functional)
                               Eran 525 con 126 en Functional cuando se empujó; los tres
                               de más son `ElInquilinatoSeConstruyeSoloTests`, la regla
                               que salió del rojo de la CI — abajo, «La sexta»
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine; ... open
                               //./pipe/dockerDesktopLinuxEngine: The system cannot find
                               the file specified». Cliente 29.7.2 presente, servidor no.
                               Lo verifica la CI, en el run de arriba.

recuento:     el guion de la CI sobre los .trx de esta máquina, y esta vez ANTES de
              empujar y no después: ROJO nombrando «Bastion.Api.IntegrationTests.dll»
              como ensamblado no declarado, y VERDE —525 casos en 7 ensamblados— tras
              declararlo. Es la pareja que dice que la línea del workflow es el arreglo.
              Repetido al final, ya con la regla de composición dentro: 528 casos en 7
              ensamblados, y la lista declarada del workflow casa en los dos sentidos

licencias:    `git diff --name-only b75b312..HEAD -- '*packages.lock.json'` -> CERO
              ficheros. Los cuatro `.csproj` tocados añaden un `<Compile Include>` y
              ninguna `PackageReference`; `frontend/package*.json` tampoco se mueven.
              La frase sale del diff, no de la memoria
```

**Lo del carril parado, con nombre y apellidos.** De lo que este ítem añade y necesitaría PostgreSQL,
**la parte que no necesita el servidor se ejerció aquí**, y es justo la que más podía romperse:
`LaTraduccionASqlTests.El_listado_de_lo_bloqueado_se_traduce_entero` ejecuta el `ListarAsync` real
contra `Host=127.0.0.1;Port=1` y comprueba que **llegó a la base** — o sea que la consulta se tradujo
entera y lo único que falló fue la conexión. Eso cubre las dos trampas que este listado tenía:
`Concat` de tres ramas después de una proyección (EF no lo traduce si la proyección es a un tipo
propio) y el `ORDER BY` a través de esa proyección (solo lo traduce si es un inicializador de
objeto, no una llamada a constructor). Las dos aparecieron **en rojo** al montarlo y están contadas
en *Decisiones tomadas → ítem 1.4*. Lo que queda para el carril de integración es lo que de verdad
necesita filas: los **cuatro** casos de `ElAccesoReservadoDelArticulo32Tests` —que la fila bloqueada
desaparezca del camino ordinario y aparezca en este, que la supresión traiga su vencimiento a seis
años, que la respuesta **no lleve `ETag`** ni un testigo dentro del JSON, y que lo bloqueado de otra
empresa no asome— y el censo de los 134 casos de ese carril.

#### Las cinco mutaciones del 1.4, cada una aplicada, ejecutada y revertida

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | **`BloqueadoDto` con `string Version` por fila**, rellenado desde el instante del bloqueo | **Rojo ×1** en el carril rápido | `Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo`, nombrando la ruta y el campo. Entera abajo |
| 2 | El quinto `ViendoLoBloqueado(...)` **sin declarar** en `s_aperturasDeBloqueoPermitidas` | **Rojo ×1** | `El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado`, con las dos listas enteras y el fichero de más |
| 3 | El motivo nuevo **declarado y sin usar** (el listado vuelve a abrir con `AdministracionDelBloqueo`) | **Rojo ×1** | `Cada_motivo_para_ver_lo_bloqueado_tiene_su_sitio_y_cada_sitio_su_motivo`: «Usados: AdministracionDelBloqueo. Declarados: AccesoReservadoDelArticulo32, AdministracionDelBloqueo» |
| 4 | El listado **sirviendo lo bloqueado sin registrar quién pregunta**: fuera el ámbito, y `.IgnoreQueryFilters()` en las tres ramas del repositorio | **Rojo ×3** | `Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo` (nombrando el fichero y la llamada), más las dos de arriba, que dejan de tener quinta apertura y motivo usado |
| 5 | Una regla **borrada** de su ensamblado (`El_barrido_ve_los_cuerpos_y_reconoce_un_testigo`, en `Api.FunctionalTests`) | **Rojo ×1**, y es el ÚNICO | `ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados`. Sin el censo del §4, el carril habría salido verde con 125 casos en vez de 126 |

**Una variante de la 5 que no llega a ser mutación:** quitar el `[Fact]` y dejar el método público
—la forma más silenciosa de apagar una regla— **no compila**: `xUnit1013` la para en el analizador.
La que sí compila es borrar el método entero, que es la que está en la tabla.

**Y un arreglo que salió de mirar el rojo de la 5.** El censo se ponía rojo, sí, pero volcaba los
**115 nombres** de las dos listas y marcaba el punto en el que se desplazan — cuando su propio
encabezado dice que existe para decir **cuál** falta y no cuántos. Ahora compara primero las
diferencias en los dos sentidos y el rojo empieza por el nombre: «estas reglas están declaradas en
`Bastion.Api.FunctionalTests` y ya no las ejecuta nadie: `NingunaLecturaEntregaTestigoDeVersionTests.El_barrido_ve_los_cuerpos_y_reconoce_un_testigo`».
La comparación entera se queda detrás, que es la que no depende de que las diferencias estén bien
calculadas.

#### La primera, entera

Es la del ítem, así que va con lo que la provocó. El DTO del listado nació **sin** ningún campo de
versión, y su documentación dice por qué; la mutación le añade uno con la excusa más razonable que
hay —«para poder desbloquear con `If-Match` desde aquí mismo»—:

```csharp
public sealed record BloqueadoDto(
    Guid Id,
    string Tipo,
    string? Codigo,
    string Nombre,
    DateTimeOffset BloqueadoEn,
    string Motivo,
    DateTimeOffset? VenceEn,
    string Version);          // <- la mutación
```

y en el caso de uso, rellenándolo:

```csharp
retencion.VenceEn(...),
recurso.BloqueadoEn.Ticks.ToString(CultureInfo.InvariantCulture));   // <- la mutación
```

Compila, pasa el formateador, y el listado sigue funcionando igual de bien. El carril rápido:

```
Bastion.Api.FunctionalTests.Bloqueos.NingunaLecturaEntregaTestigoDeVersionTests
  .Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo [FAIL]

  Shouldly.ShouldAssertException : fugas should be empty but had 1 item and was
  ["GET /api/v1/organizacion/bloqueados -> BloqueadoDto.Version (es el testigo de
    concurrencia optimista tal cual. En el cuerpo obliga a que esté también en las
    listas, que se leen sin rastreo y no lo traen: el mismo campo valdría una cosa en
    una respuesta y cero en otra)"]

  Additional Info:
    estas respuestas entregan un testigo de versión en el cuerpo, así que la llave que
    las cuatro exenciones de If-Match de los desbloqueos dan por inalcanzable ya se
    puede conseguir leyendo. O se quita el campo, o esas cuatro exenciones caducan y
    hay que volver a exigir If-Match

Con error! - Con error: 1, Superado: 125, Total: 126 - Bastion.Api.FunctionalTests.dll
```

Eso es exactamente lo que el ítem pedía: la mitad que sobrevive de las cuatro cláusulas «DEPENDE DE»
convertida en algo que **se pone rojo**, y roja **antes** de que el endpoint existiera. En el carril
de integración hay además la misma pregunta hecha por el cable —`ETag` ausente y ninguno de
`version/etag/xmin/rowversion/concurrencia` dentro del JSON—, con su comprobación de no vaciedad para
que no la conteste una página vacía.

#### La sexta, que no puse yo: la que encontró la CI

Las cinco de arriba son mutaciones **provocadas**. Hubo una sexta que no provoqué, y es la que más
dice del ítem: el run **33904703923** salió **rojo**. No en el carril rápido, donde todo estaba verde,
sino en Integración —job `101126778489`, paso 12— con **diez** casos caídos y siempre el mismo
mensaje:

```
System.InvalidOperationException : Unable to resolve service for type
'Bastion.BuildingBlocks.Application.Autorizacion.IUsuarioActual' while attempting to
activate 'Bastion.BuildingBlocks.Infrastructure.Bloqueos.AccesoALoBloqueado'.
```

Los de `ReprocesarNoDuplicaTests`, `ElTrabajoDeFondoVaciaLaColaTests` y
`LaEdadDelMasViejoSeMideTests`, más los dos de `SinLaTablaElPublicadorSeParaTests` que caen detrás. Y
los pasos 15 y 16 —el artefacto de OpenAPI— rojos por arrastre, no por sí mismos.

**Qué pasó.** El requisito 2 del enunciado —que quede registrado **quién** pregunta, no solo que se
preguntó— le dio a `AccesoALoBloqueado` una dependencia nueva: `IUsuarioActual`. Ese servicio lo
registra `AgregarAutorizacionPorPermisos()`; `AccesoALoBloqueado` lo registra `AgregarInquilinato()`.
Dos métodos de extensión distintos. La API llama a los dos y no notó nada. El **publicador de la
bandeja de salida** corre fuera de una petición, no necesita permisos y llama solo al primero: se cayó
al construir el primer `DbContext`.

**Lo que el comentario ya prometía.** `AgregarInquilinato` dice desde el 0.9 que el inquilinato y el
acceso a lo bloqueado van juntos «porque los dos son cosas que un `DbContext` de módulo necesita para
construirse, y separarlos dejaría un host que registra una y se olvida de la otra reventando al
resolver el primer contexto». El paquete dejó de ser un paquete y **el comentario siguió diciendo que
lo era**. Una promesa escrita en prosa no se pone roja.

**El arreglo, y la regla que lo sostiene.** El arreglo es una línea: `AgregarInquilinato` registra
también `IUsuarioActual` con `TryAddScoped`. `TryAdd` y no `Add`, porque la API llama además a
`AgregarAutorizacionPorPermisos` y con `Add` a secas habría dos descriptores del mismo servicio —quien
resuelve se queda con el último, o sea que el **orden de las llamadas del `Program.cs`** elegiría
implementación, en silencio—. La regla es `ElInquilinatoSeConstruyeSoloTests`, tres casos en el carril
rápido: se mira qué descriptores añade la llamada, se **descubren** en vez de teclearse —un servicio
nuevo entra en la regla el día que se registra, no el día que alguien se acuerde—, se construyen todos
en un ámbito con `validateScopes: true`, y se afirma que la lista no está vacía (ADR-0020: si la
extensión dejara de registrar algo, el bucle recorrería cero servicios y su verde no significaría
nada). El tercer caso llama dos veces y exige que el recuento de descriptores no se mueva, que es la
afirmación del `TryAdd`.

**Por qué en local no se veía, dicho sin excusa.** No es que Docker estuviera parado: un fallo de
composición no necesita base de datos, necesita que **alguien intente construir lo registrado**, y eso
no lo hacía nadie. La prueba es que la regla nueva reproduce el mensaje exacto de la CI en el carril
rápido y sin contenedor. Comentando la línea del arreglo:

```
ElInquilinatoSeConstruyeSoloTests.Y_el_acceso_a_lo_bloqueado_se_construye_nombrandolo [FAIL]
ElInquilinatoSeConstruyeSoloTests.Todo_lo_que_registra_el_inquilinato_se_puede_construir_sin_nada_mas [FAIL]
  System.InvalidOperationException : Unable to resolve service for type
  'Bastion.BuildingBlocks.Application.Autorizacion.IUsuarioActual' while attempting to
  activate 'Bastion.BuildingBlocks.Infrastructure.Bloqueos.AccesoALoBloqueado'.

Con error! - Con error: 2, Superado: 1, Total: 3
```

Lo que la CI tardó minutos y un contenedor en decir, el carril rápido lo dice antes de empujar. Ojo:
esto **no cierra** la nota del carril de integración parado. Dice que el carril rápido caza una clase
más de fallo, no que sustituya al otro.

### Verificado en local, con la salida real — ítem 1.3

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts regenerado desde openapi.json (el POST /buscar y TramoDeEmpresaDto)
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
              (este ítem no toca el esquema: consolida tipos y añade una LECTURA)
openapi:      el documento versionado está al día — 74 operaciones, una más que el 1.2,
              y es POST /api/v1/organizacion/empresas/buscar. El 74 sale de dos fuentes
              que no se derivan una de otra: el guion del contrato y el recuento de
              `El_barrido_encuentra_el_inventario_entero`

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 180 + 58 + 3 + 121 + 23 = 503 casos, 0 con error
                               (BuildingBlocks / Organizacion.Unit / Identidad /
                                Organizacion.Integration SIN Docker / Functional /
                                Arquitectura)
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine; ... open
                               //./pipe/dockerDesktopLinuxEngine: The system cannot find
                               the file specified». Cliente 29.7.2 presente, servidor no.
                               Lo verifica la CI, en el run de arriba.

recuento:     el guion de la CI, `scripts/ci/recuento-de-tests.sh`, sobre los .trx de
              esta máquina: ROJO nombrando «Bastion.Organizacion.IntegrationTests.dll»
              como ensamblado no declarado, y VERDE —503 casos en 7 ensamblados—
              tras declararlo. Es la pareja que dice que la línea añadida al
              workflow es el arreglo y no una coincidencia
```

**Lo del carril parado, con nombre y apellidos.** De las tres consultas nuevas que sí necesitarían
PostgreSQL para ejercerse enteras, **la parte que no necesita el servidor se ejerció aquí**:
`LaTraduccionASqlTests` genera el SQL de los diez listados de Organización —orden de omisión, cada
campo ordenable en los dos sentidos, y el filtro— y de la búsqueda con su cursor, sin contenedor y en
dos segundos. Lo que queda para el carril de integración es lo que de verdad necesita filas: que el
`ILIKE` encuentre lo que tiene que encontrar y que el «después de esto» del cursor no repita ni se
salte ninguna. Eso lo verifica la CI. **Lo de Identidad no tiene ese ejercicio local**: ningún
proyecto de pruebas ve sus repositorios, así que sus tres listados solo se traducen de verdad en el
carril de integración.

### Verificado en local, con la salida real — ítem 1.2

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts sin cambios tras regenerar el cliente
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
openapi:      el documento versionado está al día — 73 operaciones (este ítem no añade
              endpoints: un puerto de lectura entre módulos se resuelve EN PROCESO, no
              por HTTP, y que el contrato no se mueva es la comprobación de eso)

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 58 + 180 + 23 + 118 = 497 casos, 0 con error
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine». Los 237 casos
                               (72 + 165) «fallan» en 101 ms y 209 ms con
                               DockerUnavailableException: «Docker is either not running
                               or misconfigured — Failed to connect to Docker endpoint at
                               'npipe://./pipe/docker_engine'». Es la forma que tiene
                               Testcontainers de decir que no hay dónde levantar PostgreSQL,
                               no un fallo del código. Los SIETE casos nuevos de
                               `LosPuertosDeLecturaTests` están entre esos 237 y quien los
                               verifica es la CI.
```

### Lo que la CI encontró y en local no se veía

El primer *run* del 1.2 —[33805185736](https://github.com/AOjeda006/Bastion/actions/runs/33805185736)
sobre `e087c56`— salió **rojo en el carril de integración**, y con razón: `Divisa.Crear("PTX", …)`
**lanza**. El catálogo de divisas de los bloques comunes rechaza lo que no sabe redondear, porque una
divisa guardada sin saber con cuántos decimales redondea es una factura mal calculada esperando. O
sea que el código inventado que valía para un impuesto —`PTO-VIG`, que solo mide 20 caracteres— **no
existe** para una divisa.

**Lo que enseña no es el fallo, es cómo se encontró.** Un test que no se puede ejecutar aquí —el
demonio de Docker está parado— se empujó **compilando**, y compilar no ejerce nada: `Crear` lanza en
ejecución, no en el compilador. La regla que queda escrita: **cuando un carril no se puede ejecutar
en local, hay que ejercer en local todo lo del carril que no necesita el carril.** Aquí eso eran las
cuatro llamadas a las factorías del dominio, que no tocan la base de datos y tardan 19 ms. Se hizo
con un test canario temporal, que confirmó que de las cuatro **solo fallaba una** — y eso es lo que
convierte «hay algo roto en 237 casos» en «está roto esto». Corregido con `JPY`, que además es el
contraejemplo de cero decimales del propio catálogo.

**Los tres *runs* de esta rama, y por qué son tres.** El primero
([33805047002](https://github.com/AOjeda006/Bastion/actions/runs/33805047002), sobre `b6aaf60`) salió
**cancelado**, no verde: lo reemplazó el empujón siguiente, y un *run* cancelado no es un *run* verde
—la lección del 0.16—. El segundo (33805185736, sobre `e087c56`) es el rojo de arriba. El tercero
([33806271861](https://github.com/AOjeda006/Bastion/actions/runs/33806271861), sobre `c54f783`) es el
que cierra.

Y no se arregló mirando la salida de la CI: el registro del *job* pide permisos de administrador
(`403 Must have admin rights to Repository`) y el artefacto tampoco se puede bajar sin credenciales.
Lo que sí se puede leer sin ellas son los **pasos** del *job*, y ahí estaba dicho: falló «Tests de
integración (Testcontainers)», y los tres rojos de detrás —«Publicar el OpenAPI» omitido, «Bajar el
artefacto» y «El artefacto contiene el contrato»— eran **consecuencia**, no causa. La anotación «No
hay openapi.json dentro del artefacto» era la más ruidosa y la que menos decía.

### Las ocho mutaciones del 1.2, cada una aplicada, ejecutada y revertida

| # | Mutación | Rojo por | Qué enseña |
|---|---|---|---|
| 1 | `DivisaId` en un agregado de Identidad, sin puerto | **Afirmación 1** | La del enunciado: el descubrimiento por nombre encuentra el identificador ajeno y exige su declaración |
| 2 | `DivisaPreferidaId` en un agregado de Identidad, **nombre que no casa y sin declarar** | **Afirmación 5, y solo ella** | La que decide el ítem: con las cuatro del enunciado **habría salido verde**. Los otros 22 casos pasan |
| 3 | El arnés: romper el patrón de descubrimiento (`EndsWith("Id")` → `EndsWith("Idx")`) sin tocar el dominio | **Afirmación 3** (y la 2 de rebote) | El arnés no tiene arnés: una regla que deja de mirar se pone **verde**, no roja, salvo que alguien afirme que encuentra algo |
| 4 | Borrar la declaración de `TokenDeRefresco.EmpresaActivaId` | **Afirmación 5** | El identificador que el descubrimiento **no** ve queda igualmente cubierto |
| 5 | Dejar una declaración con la clave de una propiedad que no existe | **Afirmaciones 2 y 5** | La lista no puede engordar con inventario muerto |
| 6 | Declarar un cruce con el campo `Puerto` vacío | **Afirmación 1** | Declarar no basta: hay que decir **qué** lo valida |
| 7 | Apuntar el `Puerto` a `…Sesiones.IPuertoInventado` | **Afirmación 4** | Un puerto tecleado que no existe no es un puerto: «no está entre las puertas públicas declaradas» |
| 8 | Cambiar el cruce declarado por otro que no cubre el identificador | **Afirmación 4** | El permiso de cruzar y el identificador que lo necesita son **el mismo** hecho, y se comparan |

Y el control después de la batería, con el árbol restaurado: **23 de 23 en verde**.

**Ítem 1.1 cerrado — el presupuesto ya mide lo que dice medir:**
[run 33779064545](https://github.com/AOjeda006/Bastion/actions/runs/33779064545) sobre `c3e0119`,
los **tres** *jobs* en `success` (y el mismo árbol, ya en `main`, en el
[run 33779690080](https://github.com/AOjeda006/Bastion/actions/runs/33779690080)).

El paso de la CI pasa de una cifra a dos, calculadas por `scripts/ci/presupuesto-del-frontal.sh`:
**arranque 391/450 KiB** (lo que `index.html` referencia, que es lo que se paga antes de pintar
nada) y **total servido 532/900 KiB**. Se suman **bytes** y no bloques de disco, así que la cifra
local y la del *runner* son la misma y un ajuste de presupuesto ya no cuesta un *run* (el 0.1 dejó
escrito lo contrario: 1097 kB en local contra 1104 en el *runner*).

**Y esa promesa se comprobó, no se supuso.** El `::notice::` del *runner* dice, palabra por palabra,
lo mismo que la ejecución local: `Frontal · arranque 391/450 KiB en 3 ficheros · total servido
532/900 KiB`. Es la primera medida del proyecto en la que las dos máquinas dan el mismo número.

### Verificado en local, con la salida real

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts sin cambios tras regenerar el cliente
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Organizacion 4, Identidad 3, y el modelo coincide con ellas
openapi:      el documento versionado está al día — 73 operaciones

backend: dotnet build      ->  0 errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 58 + 180 + 18 + 118 = 492 casos
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker está parado
                               (`docker info` -> exit 1, cliente 29.7.2 sin servidor).
                               Los 230 casos «fallan» en 112 ms y 224 ms, que es la forma
                               que tiene Testcontainers de decir que no hay dónde levantar
                               PostgreSQL. Este ítem no toca ni una línea de backend
                               —documentación, un guion de bash y un paso del workflow—,
                               así que la verificación de ese carril es la de la CI, y la
                               dio: 230 casos, 230 correctos, 0 con error.
```

### Las siete mutaciones del 1.1, cada una aplicada, ejecutada y revertida

| # | Mutación | Qué hizo |
|---|---|---|
| 1 | `src=` → `data-src=` en `index.html` | **rojo**: «el arranque no incluye ningún .js» |
| 2 | quitar el `<script type="module">` entero | **rojo**: el mismo, por el otro camino |
| 3 | borrar `index.html` | **rojo**: «sin él no se sabe qué descarga el navegador» |
| 4 | apuntar a un `dist` que no existe | **rojo**: «¿se ha ejecutado el build?» |
| 5 | bajar el tope de arranque a 300 KiB | **rojo**, y solo el de arranque |
| 6 | bajar el tope total a 400 KiB | **rojo**, y solo el total |
| 7 | **hacer estática una ruta diferida** | **rojo**: arranque **391 → 507 KiB** |

**La séptima es el ítem entero.** Es un empeoramiento real —116 KiB más antes de poder pintar nada—
y la métrica anterior no solo no lo cazaba: **bajaba**, de 554 a 546 kB, porque con un fragmento
menos `du` redondea a menos bloques. O sea que el presupuesto viejo se ponía **más verde** ante un
empeoramiento. Esa es la clase de falso verde que el ADR-0022 y el ADR-0020 persiguen, aquí en el
paso que decide si el frontal ha engordado.

**Y la primera mutación destapó un fallo del propio guion**, que es para lo que existen: `data-src=`
casaba con `src=` porque el patrón no exigía nada delante, así que la mutación que pretendía romper
el parseo salió **verde**. El patrón pasa a exigir un espacio, y el motivo queda escrito en el
guion.

**Dónde retomar exactamente:** el ítem **1.2**, los puertos de lectura de Organización y la cuarta
vía en rojo (ADR-0024).

**Ítem 0.16 cerrado — `features/` espeja los módulos, y la frontera dejó de ser un acuerdo escrito:**
[run 33738721080](https://github.com/AOjeda006/Bastion/actions/runs/33738721080) sobre `6f21d3b`,
los **tres** *jobs* en `success`. Cierra **F4** y **F6** de la auditoría previa a la fase 1, y con él **la fase 0
entera**: es el último ítem con criterio escrito de antemano.

`src/features/` tiene ahora dos carpetas —`identidad` y `organizacion`—, que son módulos del
backend y no recursos; `acceso`, `almacenes` y `empresas` bajan a subcarpetas dentro de ellas, y las
dos pantallas que no son de ningún módulo se van a `src/app/paginas/`, que es donde vive lo que se
monta una vez para todas. Los diccionarios se movieron con las carpetas, y ya no pueden dejar de
moverse: sus espacios de nombres **son** las funcionalidades que hay en disco, comparados enteros y
en los dos sentidos, en `es.ts` y en `en.ts`.

Lo que llega con él: la regla que impide que una funcionalidad importe de otra, ejecutada por
ESLint y **generada de las carpetas que hay en disco**; `funcionalidades.ts`, que afirma a mano
cuántas hay; `ElBarridoDeLasFronteras`, cinco casos que comprueban la regla **por el efecto** —le
piden a ESLint que linte un import prohibido entre cada par y exigen que lo marque— más el barrido
de especificadores que tapa el punto ciego de los `import()` dinámicos; el dueño declarado de cada
ruta con el barrido que comprueba que el módulo que carga vive donde su dueño dice; el glosario del
lenguaje ubicuo fusionado con lo ya construido y con su tabla de agregados **comparada contra el
dominio compilado**; y los ADR **0022** y **0023**.

### Verificado en local, con la salida real

```
frontal: format:check / typecheck / lint / build   ->  exit 0 los cuatro
frontal: npm run test                              ->  9 ficheros, 46 casos   (eran 8 y 39)
frontal: CI=true npm run test                      ->  9 ficheros, 46 casos   (el modo de la CI)
dotnet build Bastion.sln                           ->  0 Advertencia(s), 0 Errores
dotnet format Bastion.sln --verify-no-changes      ->  exit 0, sin salida
carril rápido (Category!=Integracion)              ->  492 casos: 118 BuildingBlocks + 58 Identidad +
                                                       180 Organización + 18 Arquitectura + 118 funcionales
tests/Organizacion.IntegrationTests                ->  65 casos
tests/Api.IntegrationTests                         ->  165 casos
scripts/comprobar-migraciones.sh                   ->  Auditoría 3, Organización 4, Identidad 3; coinciden
scripts/generar-openapi.sh --comprobar             ->  al día: 73 operaciones
```

El carril de arquitectura pasa de 15 a **18**: las tres reglas del glosario. El del frontal, de 39 a
**46**: los cinco del barrido de fronteras y los dos nuevos del de rutas.

### Lo que la CI encontró y en local no se veía

**Que el barrido por el efecto estaba vacío justo donde importaba.** Tres runs rojos del *job*
Frontal con la máquina de desarrollo verde entera, en el único caso que lintea de verdad. No era el
sistema operativo, ni Node, ni ESLint, ni el paquete `ignore`, ni los globs, ni el orden de los
pasos —se descartaron los seis, uno de ellos reproduciendo el *job* completo dentro de un contenedor
`node:22`—: **`typescript-eslint` mira `process.env.CI`**, deduce «ejecución única» y no monta el
programa de TypeScript en modo vigilancia; sin él, `lintText(codigo, { filePath })` analiza el
fichero **del disco** en vez del texto que se le pasa, y el del disco está limpio.

Se reprodujo en local con `CI=true`. El arreglo va en la instancia
(`parserOptions.disallowAutomaticSingleRunInference: true`) y no en el entorno, porque
`npm run lint` sí debe aprovechar la ejecución única. Con él llegan **dos preguntas de control por
testigo** —el canario y el motor inline—, que son la comprobación por el efecto aplicada a sí misma.

Y la lectura que se queda: **salió rojo por la polaridad de la aserción**. El barrido exige que la
lista de avisos no esté vacía, así que un ESLint que no mira nada falla; escrito al revés habría
dado verde en la CI para siempre.

**Ítem 0.15 cerrado — Organización, entera, y F2 y F3 con ella:**
[run 33690830912](https://github.com/AOjeda006/Bastion/actions/runs/33690830912) sobre `1366751`, los
**tres** *jobs* en `success` —Frontal (bastion-web), Backend (Bastion.sln) y Humo (docker compose)—,
con los dos pasos nuevos de Humo mirando **dentro de la imagen** que las semillas están y **dentro
de la base** que el migrador las metió.

Los seis agregados que el §7 pedía y no estaban —`Impuesto`, `Divisa`, `TipoCambio`,
`UnidadMedida`, `ConversionUM` y `Ubicacion`— llegan enteros: dominio, persistencia con su
migración, contrato bajo `/api/v1/organizacion/` con **21 acciones nuevas**, y las semillas de tipos
de IVA y unidades cargándose desde dentro de la imagen. Terceros y Catálogo, que abren la fase 1,
los necesitaban puestos.

Lo que llega con él: seis tablas nuevas en el esquema `organizacion` con la restricción de exclusión
que prohíbe el solape de tramos; seis controladores y sus casos de uso; `db/semillas/` con doce
tramos de impuesto y quince unidades; `CargadorDeSemillasDeOrganizacion`, que el migrador llama
después de aplicar el esquema; el motivo `CargaDeMaestros`; y dos pasos nuevos en el *job* `Humo`
que miran **dentro de la imagen** y **dentro de la base**.

Y un hallazgo que no estaba en el criterio: cuatro `[Range]` con los límites escritos como texto
devolvían 500 y publicaban un mínimo falso en el contrato. Corregido, y con su regla escrita.

### Lo que el criterio del 0.15 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| Los cinco maestros **en el dominio** | `Organizacion.UnitTests`, **180** casos; entre ellos los dos que dejan escrito un porqué (ir y volver por el inverso redondeado de 1/12 no devuelve la cantidad de partida; los dos bordes del tramo se aplican). |
| **Su persistencia** y sus migraciones en `db/migraciones/Organizacion/` | `scripts/comprobar-migraciones.sh` → Organización **4**, «el modelo y las migraciones coinciden»; `Organizacion.IntegrationTests`, **65** casos contra PostgreSQL 17.6, con el `EXCLUDE USING gist` visto **rechazar** un solape y **aceptar** la sucesión normal. |
| **Su contrato** bajo `/api/v1/organizacion/` | `docs/api/openapi.json` versionado, **73** operaciones y 41 rutas; `CadaAccionDeclaraSuPermisoTests` exige permiso en las 21 nuevas. |
| **Semillas** de tipos de IVA y unidades en `db/semillas/` **cargadas por el migrador** | `MigradorDeArranque` las carga tras migrar; probado contra PostgreSQL de verdad en `LaCargaDeSemillasTests` (5 casos) y **por el efecto** en el *compose* real. |
| El cargador **afirmando conjunto no vacío** | Dos afirmaciones: sobre el fichero y sobre la **base** después de guardar; la segunda vista roja en la mutación 2. |
| **La CI mirando dentro de la imagen** que los ficheros están | *Job* `Humo`, paso «Las semillas viajan DENTRO de la imagen» (`ls` dentro del contenedor, comparado con la lista esperada) y «El migrador ha dejado los maestros DENTRO de la base». |
| **F3 no se da por arreglado con la línea de `.dockerignore`** | `<Content Include>` en el `.csproj` de Infrastructure, visto rojo al quitarlo (mutación 1) — y el `.dockerignore` lleva ahora escrito por qué las dos mitades son dos. |

### Verificado en local, con la salida real

```
dotnet build Bastion.sln                        ->  0 Advertencia(s), 0 Errores
dotnet format Bastion.sln --verify-no-changes   ->  exit 0, sin salida
carril rápido (Category!=Integracion)           ->  489 casos: 118 BuildingBlocks + 58 Identidad +
                                                    180 Organización + 15 Arquitectura + 118 funcionales
tests/Organizacion.IntegrationTests             ->  65 casos    (eran 60)
tests/Api.IntegrationTests                      ->  165 casos   (eran 164 + 1 en rojo: el 500 del rango)
scripts/comprobar-migraciones.sh                ->  Auditoría 3, Organización 4, Identidad 3; coinciden
scripts/generar-openapi.sh --comprobar          ->  al día: 73 operaciones
npm run api + git status                        ->  esquema.ts sin cambios
```

Y por el efecto, contra el *compose* de verdad y no contra un doble:

```
migrador, primera vuelta   ->  ImpuestosNuevos 12, DelFichero 12, EnLaBase 12
                               UnidadesNuevas  15, DelFichero 15, EnLaBase 15
migrador, segunda vuelta   ->  Nuevos 0 y 0, EnLaBase 12 y 15          (es idempotente)
auditoría                  ->  CargaDeMaestros|Impuesto|12 · CargaDeMaestros|UnidadMedida|15
los dos pasos nuevos de Humo, ejecutados a mano verbatim:
                               impuestos=12  unidades=15  iva_general_vigente=21.00
dotnet publish                                  ->  /publicado/semillas/ con los dos ficheros
```

**Dónde retomar exactamente:** ítem **0.16**, `features/` espeja los módulos y arranca el glosario.
Criterio: `features/identidad/` y `features/organizacion/` sustituyen a `acceso`, `almacenes`,
`empresas` e `inicio`; `inicio` queda donde le corresponda por no ser de ningún módulo; una regla
**comprobable** —no una convención escrita— impide que una funcionalidad importe de otra; y
`docs/dominio/` estrena el glosario del lenguaje ubicuo con lo ya construido. Es el último de la
addenda: cerrado, se entra en la fase 1, que **no tiene Anexo A.3** y por tanto pasa antes por la
puerta de clarificación.

**Ítem 0.14 cerrado — la internacionalización que el §3 pedía «desde el primer día»:**
el frontal habla castellano e inglés, **ningún texto visible está escrito en un componente**, y que
siga siendo así lo vigila una regla del linter y no un acuerdo.

Es el primero de los tres ítems de la **addenda**, que no salen del Anexo A.3 sino de la auditoría
previa a la fase 1. La i18n no se había decidido no hacer: se había perdido. El 0.11 la tenía
asignada por escrito en este mismo PLAN —dos veces— y cerró en verde sin ella porque su criterio de
aceptación tampoco la nombraba.

Lo que llega con él: `app/i18n/` con los dos diccionarios tipados, la fábrica del motor y la
detección de idioma; `SelectorDeIdioma`; `MensajeDePantallaRota`; la tabla de rutas guardando
**claves** de título en vez de frases; y `shared/api` devolviendo **motivos** en vez de castellano.

### Lo que el criterio del 0.14 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| `react-i18next` montado en `app/` con `es` y `en` | `app/i18n/index.ts`, colgado del `I18nextProvider` más externo de `Proveedores`. |
| **Ningún texto visible escrito en un componente**, comprobado y no inspeccionado | `i18next/no-literal-string` en `eslint.config.js`, vista en rojo (mutación 1). |
| Los títulos de `rutas.tsx` pasan por el mismo diccionario | `ElCambioDeIdioma` → «al elegir English cambia la pantalla ENTERA»; el `<h1>`, el `<title>` y el anuncio, los tres. |
| Cambiar de idioma repinta **sin recargar** | El mismo caso: se cambia con el `<select>` y se espera al encabezado nuevo, sin remontar nada. |
| Las dos traducciones tienen **las mismas claves**, comparadas como listas | El tipo `Diccionario` (mutación 4, error de compilación) **y** `ElCambioDeIdioma` → «los dos diccionarios traen EXACTAMENTE las mismas claves, y no están vacíos». |

Y dos cosas que el criterio no pedía y estaban debajo: el **`lang` del documento** sigue al idioma
(WCAG 3.1.1, mutación 2) y **ninguna traducción se ha quedado sin traducir** (mutación 5) — copiar
el castellano en `en.ts` cumple el tipo y cumple la comparación de claves.

### Verificado en local, con la salida real

```
npm run typecheck        ->  sin errores
npm run lint             ->  sin errores ni avisos
npm run format:check     ->  All matched files use Prettier code style!
npm run test             ->  8 ficheros, 39 casos, 39 pasados   (eran 32)
npm run build            ->  built in 2.18s;  dist = 554 kB  (tope 600)
npm run api + git status ->  esquema.ts sin cambios
scripts/comprobar-migraciones.sh -> Auditoría 3, Organización 3, Identidad 3; el modelo coincide
```

El backend no se toca en este ítem; se pasa igual porque la batería de `AGENTS.md` es la de la CI y
un ítem no se declara hecho con media batería.

**Dónde retomar exactamente:** ítem **0.15**, Organización entera. Criterio: `Impuesto`, `Divisa` +
`TipoCambio`, `UnidadMedida` + `ConversionUM` y `Ubicacion` en el dominio, con su persistencia, sus
migraciones en `db/migraciones/Organizacion/` y su contrato bajo `/api/v1/organizacion/`; semillas
de tipos de IVA y unidades en `db/semillas/` cargadas por el migrador; y el cargador **afirmando
conjunto no vacío**. Ojo con lo que el 0.14 dejó anotado: quitar `db/semillas` del `.dockerignore`
**no basta**, porque `Dockerfile.api` publica solo `/publicado`.

**Ítem 0.13 cerrado, y con él la FASE 0 entera:**
[run 33634114140](https://github.com/AOjeda006/Bastion/actions/runs/33634114140) sobre `518775c`, los **tres**
*jobs* en `success` —Frontal, Backend y Humo (docker compose)—, con **403** casos rápidos, **207**
de integración contra PostgreSQL 17.6 y **46** operaciones de OpenAPI. Son tres y no cuatro porque
este ítem retira el *job* `Imágenes de contenedor`.

El ítem entró con su criterio de aceptación **ya satisfecho** —había *workflow*, compilaba, pasaba
linter, corría los tres carriles y salía verde de punta a punta— y salió con seis verdes que no
probaban lo que decían convertidos en rojos comprobados. Lo que llega con él: `MigradorDeArranque`
y el servicio `migraciones` del *compose*, la sonda de Humo que **lee una tabla**, la lista de
ensamblados en los dos recuentos, el descubrimiento de las capas de aplicación, el barrido de los
permisos del frontal contra el catálogo que sirve la API, y **ADR-0021**.

### Lo que el criterio del 0.13 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| ***Workflow* que compila** | *Job* `Backend`, paso «Compilar» (`--configuration Release --no-restore`); `Frontal`, paso «Construir». |
| **Pasa linter** | `Frontal`: «Tipos», «Linter» y «Formato». `Backend`: «Formato» (`dotnet format --verify-no-changes`). |
| **Tests de dominio** | Paso «Tests de dominio y de arquitectura», **403** casos, con el suelo **y** la lista de cinco ensamblados comparada entera. |
| **Tests con Testcontainers** | Paso «Tests de integración (Testcontainers)», **207** casos contra PostgreSQL 17.6, con la lista de dos ensamblados comparada entera. |
| **Tests de arquitectura** | Los 15 del 0.12, dentro del carril rápido y nombrados en la lista de ensamblados. |
| **Verde de punta a punta** | Los tres *jobs* en `success` en el mismo *run*, con el entorno completo levantado y **sirviendo datos**. |
| *(añadido por el ítem)* **Que el verde afirme algo** | Seis mutaciones, cada una aplicada, ejecutada y revertida. La tabla está abajo. |

### Verificado en local, con la salida real

```
dotnet build Bastion.sln -c Release           -> 0 Advertencia(s), 0 Errores
carril rápido      403 casos (403 correctos)  -> 5 ensamblados, la lista casa
carril integración 207 casos (207 correctos)  -> 2 ensamblados, la lista casa
frontend                                      -> 32 tests en 7 ficheros; build 490 kB
docker compose up --wait                      -> postgres, migraciones(0), api, web, otel, jaeger
POST /api/v1/identidad/sesiones               -> 200, testigo de 1763 caracteres
GET  /api/v1/organizacion/empresas + testigo  -> 200, total=1, «Bastion Humo SL»
GET  /api/v1/organizacion/empresas sin nada   -> 401
```

### Las seis mutaciones, cada una aplicada, ejecutada y revertida

Aquí se rompe **el workflow**, no el código: lo que hay que ver en rojo son los cerrojos de la CI.

| # | La mutación | Cerrojo que la caza | Rojo |
|---|---|---|---|
| 1 | `Bastion.Identidad.UnitTests` sale de `Bastion.sln` | recuento por lista de ensamblados | `exit 1` · «No ha corrido ningún caso de: Bastion.Identidad.UnitTests.dll» |
| 2 | el contrato versionado cambia un campo y nadie regenera el cliente | «Contrato» (frontal) | `exit 1` · el diff entero dentro de la anotación |
| 3 | `docs/api/openapi.json` se queda atrás del que produce la API | «OpenAPI» (backend) | `exit 1` · «versionado 148583 bytes, recién generado 148495» |
| 4 | `DivisaBase` pasa de `HasMaxLength(3)` a `(4)` sin migración | «Migraciones» | `exit 1` · «El modelo de Organizacion tiene cambios sin migrar», con la orden que lo arregla |
| 5 | se borra el método entero de una regla de arquitectura | `LasReglasDeEsteCarrilTests` | 1 de 14 · nombra `LasFronterasEntreModulosTests.Ningun_modulo_ve_el_interior_de_otro` |
| 6 | `DROP SCHEMA` de los tres esquemas del entorno desplegado | la sonda nueva de Humo | `exit 1` · HTTP 500 al iniciar sesión, con las cinco sondas viejas en verde |

#### La primera, entera

Es la mutación que el 0.12 dejó escrita como la que ninguna defensa cazaba: **desaparece un
ensamblado de tests entero.** `dotnet sln remove tests/Identidad.UnitTests/…`, dieciocho líneas
menos en `Bastion.sln`, que es exactamente lo que deja borrar una carpeta de tests.

Lo que dice el árbol después:

```
dotnet build Bastion.sln -c Release    -> 0 Errores
dotnet test  … Category!=Integracion   -> salió con 0
                                          111 BuildingBlocks · 116 Organizacion
                                           15 Arquitectura   · 103 Api.Functional
                                          345 casos, 0 con error
```

**El carril está verde por todos los medios ordinarios.** Y el suelo, que era la única defensa hasta
este ítem, también lo deja pasar: 345 es mayor que 300. Esta mutación se habría entregado en verde.

Lo que dice el paso de la CI sobre esos mismos resultados:

```
::error title=Dominio y arquitectura::No ha corrido ningún caso de: Bastion.Identidad.UnitTests.dll .
  Un ensamblado que ya no se encuentra, o cuyos casos han dejado de casar con el filtro, sale con
  código 0 y solo baja el total.
Dominio y arquitectura: 345 casos (345 correctos, 0 con error, 0 omitidos) en 6 ensamblados — …
::error title=Dominio y arquitectura::Los ensamblados que han corrido no son los declarados.
  Han corrido: Bastion.Api.FunctionalTests.dll Bastion.Arquitectura.Tests.dll
  Bastion.BuildingBlocks.UnitTests.dll Bastion.Organizacion.UnitTests.dll .
EXIT=1
```

**Rojo por el nombre del que falta, no por un número que baja.** Que es la diferencia entre un
informe que dice «mira a ver por qué hay menos tests» y uno que dice cuál falta.

#### La sexta, entera

Con el entorno completo en pie y verde, se tiran los tres esquemas:

```
DROP SCHEMA identidad CASCADE; DROP SCHEMA organizacion CASCADE; DROP SCHEMA auditoria CASCADE;
```

El entorno desplegado se queda **sin una sola tabla**. Lo que contestan los pasos del *job*:

```
La API responde a su sonda de VIDA          -> /health/live  = Healthy               EXIT 0
La sonda de DISPONIBILIDAD ve PostgreSQL    -> /health/ready = Healthy,
                                               «PostgreSQL acepta conexiones
                                                y responde»                          EXIT 0
La API no atiende a quien no se identifica  -> 401                                   EXIT 0
Iniciar sesión con la cuenta sembrada       -> 500                                   EXIT 1  <--
Una petición que LEE UNA TABLA              -> total=0                               EXIT 1  <--
El frontal carga                                                                     EXIT 0
El frontal reenvía /api a la API            -> 401                                   EXIT 0
La imagen del frontal no publica los mapas                                           EXIT 0
Las trazas llegan al visor                  -> Jaeger conoce bastion-api             EXIT 0
```

**Siete sondas verdes sobre una base sin tablas.** No es una hipótesis: es literalmente el estado en
el que este proyecto llevaba desde el 26 de agosto, y por eso no se veía. Las dos que fallan son las
dos que añade este ítem. *Si la sonda nueva no hubiera fallado antes del arreglo, no sería la
sonda* — falla, con el mismo 500 que se midió al empezar.

Revertida relanzando el migrador y reiniciando la API: `200` y «Bastion Humo SL» otra vez.

### Dos falsos rojos —y un falso verde— del instrumento, no del proyecto

Los tres salieron midiendo, y los tres habrían pasado por hallazgos si no se comprueba el aparato:

- **La mutación 2, en su primera forma, se borró sola.** Se editó `esquema.ts` a mano esperando ver
  «Contrato» en rojo, y salió verde: el paso **regenera** el fichero antes de comparar, así que la
  edición desapareció antes de que nadie la mirara. El cerrojo estaba bien; la mutación estaba mal.
  La buena es mover el contrato y no regenerar el cliente.
- **La mutación 4, en su primera forma, dio verde por culpa del extractor.** El paso «Migraciones»
  lleva `CONFIGURACION: Release` en su bloque `env:`, y el arnés que saca los `run:` del YAML no
  copia el `env:`. Sin él, `dotnet ef --no-build` leyó el ensamblado de **Debug**, que no tenía la
  mutación dentro. Con su entorno, rojo a la primera.
- **Los 38 pasos «con sintaxis mala» de la primera comprobación.** El arnés escribía los ficheros
  temporales con una ruta de Windows que `bash` no sabe abrir, así que los 38 fallaron con «No such
  file or directory» y ninguno llegó a analizarse. Un rojo total y uniforme no suele ser un
  hallazgo: suele ser el aparato.

### Un *run* cancelado no es un *run* verde

`concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: true }` está puesto desde el 0.2 y
es lo correcto —un empujón nuevo mata el trabajo del anterior, que ya no interesa—, pero introduce
un **tercer estado** que no es ni verde ni rojo, y que en la interfaz no se parece a un fallo.

Medido por el efecto, empujando estos dos *commits* de documentación con segundos de diferencia:

```
ce9dc37  ->  run 33635719223   status=completed   conclusion=cancelled
         y sus tres jobs igual: Frontal=cancelled, Backend=cancelled, Humo=cancelled
73ac90f  ->  run 33635733634   status=completed   conclusion=success
```

El primero **no llegó a afirmar nada** de lo que este PLAN da por comprobado, y sin embargo no
aparece en rojo. La consecuencia práctica: cualquier comprobación escrita como «no ha fallado»
—`conclusion != failure`, o mirar solo si hay una cruz roja— da por bueno un *run* que se murió a
mitad. Y la ocasión de equivocarse no es rara: es exactamente la de empujar dos veces seguidas al
cerrar un ítem, que es lo que se acaba de hacer.

**La regla, entonces, es de registro y se escribe aquí porque no la vigila ninguna máquina:** un
ítem se anota como cerrado **solo con `conclusion: success` sobre un `head_sha` concreto**, y ese
identificador se copia al PLAN. La ausencia de `failure` no basta, porque `cancelled` también carece
de ella. Es la misma forma de error que el ítem entero persigue —un verde que tiene el aspecto
correcto y no prueba lo que dice—, solo que aquí el que se equivoca es el que lee, no el que corre.

Y una consecuencia del propio experimento que conviene dejar dicha: el *run* que cierra el ítem
es **33634114140 sobre `518775c`**, el último *commit* con código. Los dos de documentación han
vuelto a pasar los tres carriles enteros (33635733634), así que el árbol que se lleva a `main`
está probado; pero el identificador que se anota es siempre el del `head_sha` que se comprobó,
no «el último verde que hubiera».

### La fase 0, criterio por criterio, comprobado por algo observable

El cierre **no se apoya en que los ítems estén marcados**. Cada criterio literal del Anexo A.3 se ha
vuelto a comprobar contra algo que se puede mirar hoy: un caso nombrado de los **610** que corren en
los siete ensamblados cuyas listas compara la CI **enteras** —así que «existe» y «ha corrido» son la
misma cosa en un *run* verde—, o la salida de un paso del *run* que cierra la fase.

| Ítem | Lo observable |
|---|---|
| **0.1** andamiaje y solución | `ElInventarioDeModulosTests.Cada_carpeta_de_modulo_tiene_sus_cinco_capas` y `.Cada_tipo_vive_en_el_espacio_de_nombres_de_su_ensamblado` comparan la estructura del §12 y del §5 contra el disco. `dotnet build` y `npm run build`, pasos «Compilar» y «Construir». |
| **0.2** `docker compose up` | *Job* `Humo`: seis servicios con `--wait`, `/health/live` = Healthy, el frontal sirve su `index.html`, y `Jaeger conoce bastion-api`. |
| **0.3** bloque común | `ReglaDeRedondeoR6Tests.SegunR6_SeRedondeaUnaVezPorParDeBaseYTipo`, `ImporteTests.Cuota_EnElPuntoMedioDeLaUnidadMinima_RedondeaAlejandoseDelCero`, `PoliticaDeErroresTests.UnErrorDeNegocio_LlevaLosCamposDelRfc9457`. |
| **0.4** módulo Organización | `ContratoDeOrganizacionTests` recorre los cuatro recursos sobre HTTP; `EsquemaDelModuloTests.El_historial_de_migraciones_vive_en_el_esquema_del_modulo_y_no_en_public`. El paso «Migraciones» cuenta las de cada módulo y exige cero cambios sin migrar. |
| **0.5** módulo Identidad | `SesionesYTokensTests.El_correo_que_no_existe_y_la_contrasena_mala_dan_la_MISMA_respuesta`, `CadaAccionDeclaraSuPermisoTests.Toda_accion_o_exige_un_permiso_o_esta_en_la_lista_de_excepciones`, `SinEmpresaNoSeConsultaTests.Con_claim_devuelve_la_empresa_del_claim_y_no_otra`. **Y desde este ítem también fuera del banco de pruebas:** el paso de Humo que inicia sesión contra el entorno desplegado. |
| **0.6** filtro multiempresa | Las dos mitades, literales: `ElFiltroDeEmpresaTests.El_padron_de_empresas_no_se_lee_desde_otra_empresa` (y `.El_total_de_la_pagina_tampoco_cuenta_las_filas_de_otra_empresa`) y `NingunaPeticionNombraLaEmpresaTests.Ninguna_accion_recibe_la_empresa_por_la_peticion`. |
| **0.7** módulo Auditoría | `LaTrazaEsDeSoloAnadidoTests.Un_UPDATE_sobre_una_fila_de_traza_lo_rechaza_el_motor` —lo rechaza el motor, no el código— y `UnCambioEnUnMaestroDejaSuRastroTests.El_alta_de_un_almacen_deja_una_fila_con_quien_donde_y_que`. |
| **0.8** *outbox* transaccional | Las tres cláusulas por separado: `ElEventoVaEnLaMismaTransaccionTests.La_empresa_y_su_evento_los_escribe_LA_MISMA_transaccion`, `ElTrabajoDeFondoVaciaLaColaTests.Lo_que_esta_pendiente_acaba_publicado`, `ReprocesarNoDuplicaTests.El_mismo_evento_dos_veces_deja_su_efecto_una_sola`. |
| **0.9** idempotencia y concurrencia | Los cuatro códigos del criterio, cada uno con su caso: `LaMismaClaveDevuelveElMismoRecursoTests.El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro`, `LaVersionViajaDeLaLecturaALaEscrituraTests.Una_version_obsoleta_es_412_y_trae_la_actual`, `.Sin_la_cabecera_es_428_y_no_toca_nada`, `ContratoDeOrganizacionTests.Suprimir_una_serie_que_ya_ha_numerado_es_409`. |
| **0.10** `Bloqueado` y fechas | `LaFilaBloqueadaSigueEnLaBaseTests.Suprimir_por_la_API_deja_la_fila_entera_con_su_motivo_y_su_fecha` (R16), `ContratoDeOrganizacionTests.La_direccion_va_y_vuelve_en_los_seis_campos_de_R17`, `LasFechasDicenDeQueTipoSonTests.No_hay_ni_una_fecha_que_no_diga_si_lleva_zona` (R14). |
| **0.11** *shell* de React | 32 tests en 7 ficheros (`LaPantallaDeAcceso`, `ElSelectorDeEmpresa`, `LasRutasProtegidas`, `ElCambioDeRuta`, `ElBarridoDeRutas`, `ElTestigoDeAcceso`, `ElListadoDeAlmacenes`), y el paso «Contrato», que **regenera** el cliente desde `docs/api/openapi.json` y falla si difiere del versionado. |
| **0.12** tests de arquitectura | Las 15 reglas, más `LasReglasDeEsteCarrilTests.Las_reglas_de_este_carril_son_las_declaradas`, que es la que impide que borrar una regla baje el color. |
| **0.13** integración continua | Este mismo *run*, con los recuentos por ensamblado comparados enteros y las seis mutaciones de arriba. |

**Lo que este repaso encontró y no estaba marcado:** nada del A.3, y una cosa fuera de él. Los tres
`.gitkeep` que no pertenecen a los sesenta del andamiaje modular se miraron uno a uno: `db/semillas/`
y `docs/dominio/` los nombra el plan maestro y se quedan; `frontend/public/` no lo nombra nadie —ni
el §12, ni `vite.config.ts`, ni el `index.html`— y **no era inerte**: Vite copia `public/` al raíz de
`dist`, así que el fichero viajaba a la imagen y nginx lo servía. Comprobado por el efecto antes de
borrarlo: `GET /.gitkeep` devolvía **200**.

### El veredicto de las nueve notas abiertas

Ninguna se queda sin veredicto. Cuatro se cierran aquí; cuatro siguen abiertas **y eso es la
decisión, no un descuido**; una es de vigilancia.

| Nota | Veredicto |
|---|---|
| `UnidadDeTrabajoPorModuloTests` teclea sus ensamblados | **Cerrada** (`71357f2`). La lista se descubre de lo que arrastra `Bastion.Api` filtrando `Bastion.*.Application`. Un ensamblado de aplicación nuevo entra solo. |
| el suelo de recuento protege dos ensamblados de cinco | **Cerrada** (`caacfd7`). Se comparan **listas de ensamblados**, en los dos carriles, en las dos direcciones. Mutación 1. |
| los identificadores de permiso están escritos a mano | **Cerrada** (`d4e2352`). Barrido que compara la lista de `permisos.ts` contra el catálogo que sirve la API. Vive en el carril de **integración**, porque necesita la API en pie: el catálogo es un *endpoint*, no un `enum` del documento. |
| el *compose* no aplica las migraciones | **Cerrada** (`e6425d3`, **ADR-0021**). La deuda más antigua de la fase, del 26 de agosto. |
| la renovación de sesión asierta el cuerpo | **Abierta, con la decisión tomada.** Sigue el **ADR-0019**: escribir el esquema de Zod a mano es la duplicación que la regla del contrato prohíbe. Lo desbloquea un generador de esquemas desde el OpenAPI, o que las aserciones dejen de ser una sola. Nada de eso ha cambiado. |
| lo bloqueado sigue sin camino de lectura | **Abierta, con la decisión tomada.** Necesita un motivo nuevo —administración—, su permiso y un ADR sobre el art. 32. Es trabajo de dominio, no de armazón: lo mira la fase que traiga ese motivo. |
| el contrato describe los enteros como `integer \| string` | **Abierta, con la decisión tomada.** El documento describe con honestidad lo que la API acepta; estrechar el servidor para que el cliente quede cómodo sería mover el contrato para que encaje el consumidor. Se estrecha en la traducción, en un solo sitio. |
| `auditoria.claves_de_idempotencia` crece sin límite | **Abierta, y la mirada del 0.13 ya ocurrió.** Esta nota citaba el 0.13 «junto con quién aplica las migraciones». Lo segundo se ha resuelto —ADR-0021—, y con ello **el mecanismo ya existe**: una limpieza sería una migración más aplicada por el mismo paso del despliegue. Lo que sigue faltando no es mecanismo, es el dato: **cuánto tarda un cliente real en reintentar**, y eso no está en el código. Sin él, borrar es adivinar, y adivinar mal no encoge la tabla: quita la garantía. |
| en Node 25 el `localStorage` de los tests no es el de jsdom | **De vigilancia, no de arreglo.** El remiendo de `setupTests.ts` está puesto y comentado. Se revisa **al subir de versión de Node**: el día que el de jsdom vuelva a ganar, el remiendo sobra y hay que quitarlo, no dejarlo tapando al bueno. |

### Traspaso a la fase 1

**Qué hereda.** Un monolito modular con tres módulos vivos —Organización, Identidad, Auditoría—,
**46** acciones bajo `/api/v1/{modulo}/{recurso}`, todas denegadas por omisión salvo cinco
declaradas; un armazón de React con el cliente **generado** del contrato; y una CI de tres carriles
en la que el verde afirma algo. Un módulo nuevo de la fase 1 entra por sitios ya construidos: sus
cinco capas (el inventario las cuenta), su esquema y su carpeta en `db/migraciones/<Modulo>/`, su
`DbContext` **añadido a `MigradorDeArranque` en el orden que le toque respecto de Auditoría**, y sus
ensamblados **añadidos a las dos listas declaradas** del recuento de la CI.

**Qué imports se añaden a `CLAUDE.md` al empezar** (Anexo A.2.3, y solo estos dos):
`@../BibliotecaDocumentacion/herramientas/proteccion-datos.md` y
`@../BibliotecaDocumentacion/patrones/soft-delete.md`.

**Qué invariantes de la fase 0 son portantes** —romper uno no da un fallo local, da una avería muda:

1. **Ninguna acción pide `If-Match` e `Idempotency-Key` a la vez.** Son mecanismos distintos con
   preguntas distintas: la clave dice «esta petición ya la hice» y la versión dice «el recurso ha
   cambiado desde que lo leí». Juntas en una misma acción, el reintento devuelve el recibo guardado
   sin volver a comprobar la versión, y la protección optimista deja de existir sin que nada se
   ponga rojo. Lo vigila `TodaEscrituraDiceComoSeProtegeTests.Ninguna_accion_pide_los_dos_mecanismos_a_la_vez`,
   sobre el inventario entero de acciones que cambian estado.
2. **La lista de seis testigos del ADR-0015 se compara entera.** No basta con que los seis existan:
   `LasClavesSeConocenAntesDeGuardarTests.Las_entidades_del_tipo_base_y_las_que_llevan_testigo_son_las_MISMAS`
   compara **tres** listas enteras entre sí —las que heredan de `EntidadBase`, las que llevan `xmin`
   y la del ADR-0015—, y `.Todo_lo_que_genera_el_servidor_es_de_verdad_un_testigo_de_concurrencia`
   cierra el otro lado. Una entidad nueva con `xmin` obliga a tocar el ADR; no basta con compilar.
3. **Toda regla afirma también que su conjunto no está vacío, y ese conteo se compara** (ADR-0020).
   Trece de los dieciséis módulos del §5 no existen todavía: una regla que se aplique a lo que hay
   pasa por vacuidad y no dice nada. La fase 1 estrena módulos, o sea que estrena conjuntos: es
   justo cuando esta regla paga.
4. **El filtro de empresa se salta en tres sitios y solo en tres**, declarados y comparados
   (`ElFiltroNoSeSaltaPorAhiTests`). R8 es del compilador y del barrido, no de la disciplina.
5. **El esquema lo aplica el migrador, y la API no migra nunca al arrancar** (ADR-0021). Cualquier
   entorno nuevo hereda la forma: primero el migrador, y solo si termina bien, la aplicación.
6. **Toda entidad dice si se audita y toda fecha dice si lleva zona**, por barrido y no por
   convención (`CadaEntidadDeclaraSuAuditoriaTests`, `LasFechasDicenDeQueTipoSonTests`). Una entidad
   nueva que no lo diga pone el carril en rojo el mismo día que se escribe, que es cuando es barato.

**Lo que la fase 1 debe mirar el primer día:** con más de una réplica, la guarda de la semilla de
arranque —«no hay ningún usuario»— es una lectura y una escritura sin transacción común. Hoy hay una
sola réplica y por eso no importa; el día que haya dos, importa antes que nada.

**Dónde retomar exactamente:** los tres ítems de la **addenda** —0.14, 0.15 y 0.16, en ese
orden—, y **no** la fase 1. Salen de la auditoría previa a la fase 1 (ver *Decisiones tomadas*) y son
cimientos que la fase 1 da por puestos: la i18n que el §3 manda «desde el primer día», los cuatro
agregados de Organización que el §7.1 le da y sin los cuales el criterio del §15 no se puede cumplir,
y el reparto de `features/` que fija el §10. **El orden importa:** el 0.16 mueve los mismos ficheros
que el 0.14 traduce, así que al revés se tocan dos veces.

Después de esos tres, la **fase 1 · Maestros**. Criterio del §15: alta de
cliente/proveedor con NIF validado y de artículo con unidad, impuesto y tarifa; listados paginados
y filtrados en servidor; dominio cubierto por tests. Contenido: Terceros y Catálogo completos,
tarifas, importación CSV y búsquedas.

Y una advertencia para ese primer turno, porque es la diferencia con todos los anteriores: **la
fase 1 no tiene un Anexo A.3.** El checklist con criterios por ítem que ha guiado la fase 0 existe
solo para ella; del §15 sale el criterio de la fase entera, no el de sus pasos. Así que lo primero
no es escribir código, es la **puerta de clarificación** del `CLAUDE.md` §2: acordar con el usuario
el desglose en ítems pequeños con criterio verificable y anotarlo aquí. Empezar sin eso es
exactamente lo que este PLAN existe para impedir.

**Ítem 0.12 cerrado, con la CI en verde:**
[run 33603428022](https://github.com/AOjeda006/Bastion/actions/runs/33603428022) sobre `0e8b93e`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor—,
con **403** casos rápidos y **206** de integración contra PostgreSQL 17.6, el documento OpenAPI al
día —**46** operaciones— y el cliente generado al día con él.

Cierra la fase 0 por el lado de las **fronteras**: a partir de aquí el §4 no es un acuerdo escrito en
un documento, son **quince reglas que se ejecutan**, y cada una se ha visto en rojo por una
violación deliberada antes de aceptarla en verde.

Lo que llega con él: `tests/Arquitectura.Tests/` con sus siete ficheros y 1.183 líneas —`Inventario`
(la mitad declarada), `Ensamblados` (la descubierta), `Barrido` (el único punto de entrada, con las
tres afirmaciones obligatorias) y los cuatro de reglas—, el paquete `NetArchTest.eNhancedEdition` fijado
con su licencia y su mantenimiento comprobados, y **ADR-0020** con la doctrina del carril.

### Lo que el criterio del 0.12 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| **NetArchTest** | `tests/Arquitectura.Tests/`, 15 casos en el carril rápido, en el paso «Tests de dominio y de arquitectura» que ya existía. |
| **Las reglas de frontera del §4** | Reglas **1** y la mitad expresable de la **5**: `LasFronterasEntreModulosTests` (4 casos). Regla **2** y el reparto por capas: `LasCapasVanHaciaDentroTests` (3 casos). Las reglas **3** y **4** son hechos de SQL y de DDL y no caben aquí: están en los tests de esquema contra PostgreSQL de verdad; el mapa completo, con dónde está cubierta cada una, está en el ADR-0020. |
| **Fallan si un módulo cruza una frontera** | Siete mutaciones, cada una aplicada, ejecutada y revertida. La tabla está abajo. |
| *(añadido por el ítem)* **Que ninguna regla esté mirando al vacío** | `ElInventarioDeModulosTests` (7 casos) sostiene a las demás: qué módulos hay, qué capas tienen y **cuáles llevan tipos**. Y `LasReglasDeEsteCarrilTests` compara la lista de las quince por su nombre, que es lo único que caza una regla borrada. |

### Verificado en local, con la salida real

- `dotnet build Bastion.sln` → **0 errores, 0 advertencias** (con `TreatWarningsAsErrors`).
- `dotnet format --verify-no-changes` → sin cambios.
- `dotnet test Bastion.sln --filter "Category!=Integracion"` → **403** casos en verde, repartidos:
  111 `BuildingBlocks.UnitTests` · 116 `Organizacion.UnitTests` · 58 `Identidad.UnitTests` ·
  **15 `Arquitectura.Tests`** · 103 `Api.FunctionalTests`. Eran 388 al cerrar el 0.11.
- Frontal: **32** casos de Vitest en 7 ficheros, sin cambios respecto al 0.11 salvo la aserción
  rápida que se le añadió al selector de empresa (tarea previa a este ítem).

### Las siete mutaciones —con dos variantes—, cada una aplicada, ejecutada y revertida

Las seis que pedía el encargo, más una séptima que salió de ejecutar las otras. Dos llevan variante
porque la forma literal de la mutación no era la interesante: la **1b** ni siquiera compila, y la
**5b** es la 5 llevada hasta el final.

| # | Qué se rompe | Qué se pone rojo |
|---|---|---|
| **1** | `Bastion.Organizacion.Domain` depende de `Bastion.BuildingBlocks.Infrastructure` (referencia + una clase que la usa) | `El_dominio_no_conoce_la_infraestructura_ni_el_framework`: *«el dominio no sabe que existe una base de datos ni un servidor web (§4, regla 2) — y este tipo la cruza: 1. `Bastion.Organizacion.Domain.Almacenes.MutacionUno` — Has dependency on: `Bastion.BuildingBlocks.Infrastructure.Auditoria.EscrituraEnOtraEmpresaException`»* |
| **1b** | La forma literal: `Domain` depende del `Infrastructure` de **su propio** módulo | **No compila.** `error MSB4006: Existe una dependencia circular en el gráfico de dependencias`. Ahí el guardián es el compilador, y por eso la mutación 1 se reformuló contra la infraestructura común, que sí compila y sí es la puerta que un dominio tiene más a mano |
| **2** | `Bastion.Identidad.Application` depende de `Bastion.Organizacion.Domain` (el interior de otro módulo) | **Dos reglas.** `Ningun_modulo_ve_el_interior_de_otro`: *«Identidad solo puede ver el Contracts de otro módulo, nunca su interior — y este tipo la cruza: 1. `Bastion.Identidad.Application.Arranque.MutacionDos` — Has dependency on: `Bastion.Organizacion.Domain.Empresas.RegimenDeIva`»*. Y `El_unico_cruce_entre_modulos_va_por_contratos`, que además dice cuál sobra: `*"Identidad.Application -> Bastion.Organizacion.Domain"*` |
| **3** | `Bastion.Organizacion.Domain` toca el framework: `PackageReference` a EF Core y un método que recibe un `DbContext` | `El_dominio_no_conoce_la_infraestructura_ni_el_framework`: *«… y este tipo la cruza: 1. `Bastion.Organizacion.Domain.Almacenes.MutacionTres` — Has dependency on: `Microsoft.EntityFrameworkCore.DbContext`»* |
| **4** | **Ni una línea de código.** Una letra en el inventario: `Microsoft.EntityFramworkCore` | `La_prohibicion_al_dominio_puede_dispararse`. **Y la regla que da nombre a la frontera se queda verde.** Entera, abajo |
| **5a** | Un módulo nuevo con su andamio de cinco capas, sin declararlo | `Las_carpetas_de_modulo_son_las_declaradas`, con la lista entera y `"Almacenaje"` de más |
| **5b** | Y además compilando y llegando a la salida | **Cuatro reglas.** `Los_ensamblados_modulares_de_la_salida_son_los_declarados`, `Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara`, `Ningun_modulo_ve_el_interior_de_otro` (*«los módulos a los que se les aplica la frontera no son los que el inventario declara montados»*) y la de las carpetas |
| **6** | Se borra una regla entera del fichero (`El_dominio_no_conoce_…`, quince líneas) | `Las_reglas_de_este_carril_son_las_declaradas`, con las dos listas enteras y el total bajando de 15 a 14. Ninguna otra se entera: la suite sale **más rápida y con un caso menos que nadie echa de menos** |
| **7** | La referencia de proyecto que cruza la frontera, **sin una sola línea que la use** | Al principio, **nada**: las catorce reglas verdes. Es el hallazgo que trajo la regla quince. Con ella, `Las_referencias_de_proyecto_son_las_declaradas` |

### La cuarta, entera

Es la que había que leer saliera como saliera, y salió como no convenía. La mutación es de **una
letra** y no toca ni una línea de código de producción: en `Inventario.ProhibidasAlDominio`,
`Microsoft.EntityFrameworkCore` pasa a `Microsoft.EntityFramworkCore`. Compila, pasa `dotnet format`,
pasa los analizadores. Y para que la pregunta fuera la de verdad se aplicó **junto con la mutación
3**: el selector roto **y** el dominio de Organización importando de verdad un `DbContext`.

```
Con error: 1, Superado: 13, Omitido: 0, Total: 14, Duración: 1 s

  LasCapasVanHaciaDentroTests.La_prohibicion_al_dominio_puede_dispararse [FAIL]
  Mensaje de error:
   Shouldly.ShouldAssertException : mudas
    should be empty but had
1
    item and was
["«Microsoft.EntityFramworkCore»: ni un tipo de Infrastructure depende de eso, así que
prohibírselo al dominio no puede fallar nunca. O la cadena está mal escrita, o eso ya no se
usa en el proyecto y la prohibición sobra"]

  Additional Info:
    estas prohibiciones no pueden dispararse:
1. «Microsoft.EntityFramworkCore»: ni un tipo de Infrastructure depende de eso, así que
   prohibírselo al dominio no puede fallar nunca. O la cadena está mal escrita, o eso ya no
   se usa en el proyecto y la prohibición sobra
```

Lo que hay que leer en esa salida no es el rojo, es **cuál de los catorce es**. `El_dominio_no_conoce_la_infraestructura_ni_el_framework`
—la regla que lleva el nombre de la frontera, la que el criterio del ítem pide— **está entre los
trece verdes, con un `DbContext` dentro del dominio**. Su conjunto seleccionado no cambia: sigue
siendo todos los tipos del dominio, los mismos de siempre. La condición tampoco: nadie depende de
`Microsoft.EntityFramworkCore`, porque eso no existe. Verde por vacuidad, y sin ninguna señal.

Lo único que se movió fue el contraejemplo, que es la afirmación que una regla de arquitectura
corriente **no lleva**: la cadena dejó de encontrarse en la capa donde tiene que estar.

Y aquí está la parte que hay que guardar, porque es la que avisó la vez anterior y la que no habría
avisado esta: **el falso verde del 0.11 se cazó porque el resultado era inverosímil**. Este no lo
sería. Trece verdes y un rojo en un test con nombre de comprobación auxiliar es exactamente el
aspecto que tiene un carril sano; si el contraejemplo no existiera, catorce verdes tendrían el mismo
aspecto. **La única alarma disponible era la regla escrita de antemano**, y por eso está escrita de
antemano.

### Lo que este carril NO cubre, y dónde está cubierto

La tabla completa está en el ADR-0020. En corto:

- **Reglas 3 y 4 del §4** (ninguna consulta cruza esquemas, ninguna clave ajena entre esquemas): son
  SQL y DDL, no tipos. Viven en `EsquemaDeIdentidadTests` y `EsquemaDelModuloTests`, contra
  PostgreSQL de verdad. El SQL crudo lo vigila `ElFiltroNoSeSaltaPorAhiTests`, leyendo el código.
- **La mitad de ejecución de la regla 5**: los tests de la bandeja de salida (ADR-0013).
- **La composición en ejecución**: qué resuelve el contenedor no está en la IL de nadie —
  `UnidadDeTrabajoPorModuloTests`.
- **El frontal**: es TypeScript. Sus fronteras las vigilan el contrato generado (ADR-0018) y los
  barridos de rutas y permisos.

### Lo que se encontró por el camino y no estaba previsto

1. **`Bastion.Auditoria.{Domain,Contracts,Application,Endpoints}` compilan a ensamblados con CERO
   tipos.** El módulo del 0.7 registra los cambios desde un interceptor de EF Core, así que todo lo
   suyo vive en `Infrastructure`. No es un fallo —es la forma que tiene ese módulo hoy—, pero sí lo
   sería no decirlo: aplicarles las fronteras del §4 habría dado **cuatro verdes por vacuidad**, y
   el informe habría dicho que Auditoría cumple las cinco. Está declarado en
   `Inventario.EnsambladosConTipos` y comparado por
   `Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara`. Consecuencia escrita en voz
   alta: **a Auditoría no se le comprueba hoy ni una sola regla de capas.** El día que estrene su
   primera entidad de dominio, el inventario se pone rojo y obliga a añadir la línea, que es el día
   en que esas reglas empiezan a proteger algo.
2. **`System.Data` no llegó a ser una regla**, porque su propio contraejemplo la tumbó: cero tipos
   dependen de ella en las tres capas donde tendría que aparecer.
3. **Una referencia de proyecto sin usar es invisible para NetArchTest**, y de ahí la regla quince.
   Ver la mutación 7.
4. **`UnidadDeTrabajoPorModuloTests` tiene la misma avería que este ítem viene a evitar, en el otro
   carril.** Su `s_capasDeAplicacion` fija dos ensamblados a mano con `typeof(...)` y **no incluye
   `Bastion.Auditoria.Application`**. Hoy da igual, porque está vacío; el día que Auditoría estrene
   un caso de uso, esa regla dejará de cubrirlo **en silencio y sin cambiar de color**. No se toca
   aquí —es de otro ítem y arreglarlo duplicando el inventario sería peor—, pero queda anotado en
   *Notas / riesgos* con la forma que tendría el arreglo.
5. **El suelo de recuento de la CI protege dos ensamblados de cinco, y el comentario que lo
   explicaba era falso.** El paso rápido falla si el total baja de 300. Perder `Arquitectura.Tests`
   entero deja 388 y pasa — eso ya estaba anotado al escribirlo. Lo que apareció al hacer la cuenta
   completa es que tampoco caza la pérdida de `Api.FunctionalTests` (403 − 103 = **300 clavados**, y
   la comparación es «menor que») ni la de `Identidad.UnitTests` (345); y que el comentario del
   *workflow* afirmaba lo contrario, cosa que ya era falsa con 388 casos. El comentario queda
   corregido con los números reales; **contar ensamblados** es del 0.13.

### Lo que la CI encontró y en local no se veía

**Nada sobre el carril nuevo, y merece decirse así.** Los 15 casos salieron igual en local y en la
CI, en `Debug` y en `Release`: el descubrimiento sube desde `AppContext.BaseDirectory` buscando
`Bastion.sln`, y eso funciona igual desde `bin/Debug` que desde `bin/Release`. Era el riesgo real de
un carril que lee del disco, y se comprobó a propósito antes de empujar.

Lo que sí aporta la CI y aquí no se ve son los **206 casos de integración** contra PostgreSQL 17.6,
que necesitan Docker: siguen verdes, o sea que mover el §4 a reglas ejecutables no ha tocado nada de
la persistencia. Y el desglose por ensamblado del `::notice::`, que es lo que permitió hacer la
cuenta del suelo y descubrir que el comentario del *workflow* era falso.

El único aviso sigue siendo el ajeno de siempre, arrastrado desde el 0.7 y todavía sin tocar:
`actions/upload-artifact@v4` (en `Backend` y en `Frontal`) y `docker/build-push-action@v6` con
`docker/setup-buildx-action@v3` (en `Imágenes de contenedor`) apuntan a Node.js 20, en desuso, y el
ejecutor los fuerza a Node.js 24. No es un fallo; es del 0.13.

**Dónde retomar exactamente:** ítem **0.13**, integración continua. Criterio: *workflow* que compila,
pasa linter, tests de dominio, tests con Testcontainers y tests de arquitectura; verde de punta a
punta. Es el último de la fase 0, y llega con cinco cosas ya nombradas esperándole:

1. **El suelo de recuento cuenta casos, no ensamblados, y protege dos de cinco.** Con 403 casos y
   un suelo de 300, perder un ensamblado entero deja 292, 287, **300**, **345** o **388** según cuál
   sea: solo los dos primeros bajan del suelo. `Api.FunctionalTests`, `Identidad.UnitTests` y
   `Arquitectura.Tests` podrían no ejecutarse **enteros** con la CI en verde. **Contar ensamblados**
   —lista entera y comparada— es el arreglo, y es de este ítem.
2. **El aviso de Node.js 20**, arrastrado desde el 0.7 y todavía sin tocar:
   `actions/upload-artifact@v4` (en `Backend` y en `Frontal`) y `docker/build-push-action@v6` con
   `docker/setup-buildx-action@v3` (en `Imágenes de contenedor`) apuntan a Node.js 20, en desuso, y
   el ejecutor los fuerza a Node.js 24.
3. **Los identificadores de permiso escritos a mano** (`frontend/src/shared/sesion/permisos.ts`).
   Decidido en este ítem que **no** es de arquitectura y que su sitio es el 0.13: un barrido que
   compare esa lista contra el catálogo que la API sirve de verdad, en la forma de
   `ElFiltroNoSeSaltaPorAhiTests`, que ya lee código fuente.
4. **`UnidadDeTrabajoPorModuloTests.s_capasDeAplicacion` está fijado a mano** y le falta
   `Bastion.Auditoria.Application`. Ver *Notas / riesgos*.
5. **`Humo` e `Imágenes de contenedor` construyen la misma imagen dos veces**, anotado desde el 0.6
   para consolidar aquí.

Y una cosa que **no** hereda: las fronteras del §4 ya no son un documento. Si el 0.13 mueve un
proyecto de sitio, cambia una referencia o toca la raíz de composición, hay quince reglas que lo
dicen por su nombre antes de que llegue a `main`.

---

**Ítem 0.11 cerrado, con la CI en verde:**
[run 33581760198](https://github.com/AOjeda006/Bastion/actions/runs/33581760198) sobre `72b0ca5`,
los cuatro *jobs* en `success` —Backend, Frontal, Humo (docker compose) e Imágenes de contenedor—,
los dos recuentos del backend publicados: **388** casos rápidos y **206** de integración contra
PostgreSQL 17.6, y el carril de frontal con **32** casos de Vitest, el documento OpenAPI al día
—**46** operaciones— y el cliente generado al día con él.

Cierra la fase 0 por el lado del **frontal**: a partir de aquí hay una aplicación en la que se entra,
se cambia de empresa y se navega, y los tipos que cruzan la red salen del contrato y no de la
memoria de nadie.

Lo que llega con él: el cliente tipado (`shared/api/`), el depósito de sesión en memoria con su
renovación compartida (`shared/sesion/`), la tabla de rutas con su exigencia declarada y el quinto
barrido (`app/rutas.tsx`, `app/ElBarridoDeRutas.test.ts`), la disposición con los tres pasos del
cambio de ruta accesible, el selector de empresa que reinicia la caché entera, la pantalla de acceso
y dos listados de solo lectura; y en despliegue, el reenvío de `/api/` por nginx, la CSP y los mapas
de fuentes fuera de la imagen.

### Lo que el criterio del 0.11 pedía, y dónde está probado

| Lo que pedía el criterio | Dónde se prueba |
|---|---|
| **Login** | `LaPantallaDeAcceso.test.tsx`, 5 casos: valida antes de salir a la red (0 peticiones), el correo mal escrito se dice aquí, unas credenciales malas dan **un** mensaje que no distingue cuál de las dos falla, el botón no deja mandarlo dos veces, y estando dentro no se ofrece entrar otra vez. |
| **Selector de empresa** | `ElSelectorDeEmpresa.test.tsx`, 3 casos. El que importa pinta la lista de Alfa, cambia a Beta y exige que se vea «Nave central de Beta» **y** que «Nave central de Alfa» ya no esté. Con dos empresas de verdad y mirando lo que se pinta, no si se llamó a `clear()`. |
| **Layout** | `ElCambioDeRuta.test.tsx`, caso 3: el primer enlace de la página es el salto al contenido, apunta a `#contenido`, y el `<main>` lleva ese `id`. |
| **Rutas protegidas** | `LasRutasProtegidas.test.tsx`, 5 casos: sin sesión se va a la pantalla de acceso, tras entrar se vuelve **a donde se iba**, sin el permiso se explica en vez de enseñar una pantalla rota, la navegación no ofrece lo que no se puede ver, y una dirección que no existe se dice y da salida. |
| **Cliente de API generado desde el OpenAPI** | `npm run api` genera `shared/api/esquema.ts` de `docs/api/openapi.json`; el paso `Contrato` de la CI lo vuelve a generar y falla si difiere — `::notice title=Contrato::… está al día`. Ni un tipo del contrato escrito a mano. |
| **Cambio de ruta accesible: `<title>`** | `ElCambioDeRuta.test.tsx`, caso 1: `document.title` pasa de `Inicio · Bastion` a `Almacenes · Bastion`, y el barrido exige que **los cinco títulos sean distintos** —un título repetido no dice a dónde se ha llegado—. |
| **…`role="status"`** | Mismo caso, buscándolo **por rol y nombre accesible**: si `getByRole` lo encuentra es que está en el árbol de accesibilidad, o sea que no se escondió con `display:none`. Se comprueba su texto y su `aria-live="polite"`. |
| **…foco** | Mismo caso: `document.activeElement` es el `<h1>` del `<main>`. Y un caso aparte exige `tabindex="-1"` **explícito**: con `0` el foco también llegaría y a cambio todo el mundo se comería una parada de más con el tabulador. |
| Que toda ruta diga qué exige | `ElBarridoDeRutas.test.ts`, 5 casos, con las listas comparadas **enteras en los dos sentidos** y la partición contada: **5 = 2 públicas + 1 de sesión + 2 de permiso**, con los caminos exactos de cada grupo. Lo que no exige permiso explica por qué con una frase de verdad (más de veinte caracteres). |
| Que el testigo no acabe en el navegador | `ElTestigoDeAcceso.test.tsx`, 5 casos: `localStorage` y `sessionStorage` vacíos tras entrar, un 401 renueva y repite **una** vez, si la renovación tampoco vale no se entra en bucle, dos peticiones caducadas comparten **una** renovación, y la cabecera `Authorization` lleva el campo del contrato. |
| Que una pantalla esté terminada | `ElListadoDeAlmacenes.test.tsx`, 6 casos: cargando, error **con salida que de verdad reintenta**, vacío diciendo qué está vacío, la página escrita en la URL y leída de la URL, y una página disparatada que no rompe nada. |

### Verificado en local, con la salida real

- `npx tsc -b --force` → limpio. `npx eslint .` → limpio. `npx prettier --check .` → limpio.
- `npx vitest run` → **32** correctos en 7 ficheros, 0 con error.
- `npm run build` → 490 kB de descarga (fragmento mayor: `index`, 331 kB; segundo: `zod`, 82 kB).
- `dotnet build Bastion.sln` → **0 advertencias / 0 errores**; `dotnet format --verify-no-changes` → limpio.
- `dotnet test --filter "Category!=Integracion"` → **388** correctos, 0 con error
  (111 comunes + 116 Organización + 58 Identidad + 103 funcionales).
- El carril de **integración no se ejecutó en local**: el demonio de Docker no estaba levantado en
  esta máquina, así que Testcontainers no podía arrancar nada. Los **206** casos salen de la CI, que
  es donde se ejecutaron de verdad. Se dice porque un carril que no se ha ejecutado no se cuenta
  como ejecutado.

### Las seis mutaciones, cada una aplicada, ejecutada y revertida

| # | Mutación | Qué se puso rojo | Diagnóstico |
|---|---|---|---|
| 1 | El cambio de empresa deja de reiniciar la caché | `ElSelectorDeEmpresa` → «al cambiar de empresa, la lista pasa a ser la de la otra» | Rojo, pero **por plazo agotado a los 5 s**: la fila de Beta no llega a aparecer. Caza la mutación; el mensaje no la nombra. |
| 2 | El testigo se guarda en `localStorage` | `ElTestigoDeAcceso` → «no llega nunca a localStorage ni a sessionStorage» | Impecable: el fallo **imprime el testigo guardado**, con la empresa dentro. |
| 3a | `tabIndex={-1}` → `{0}` | Solo «el destino del foco no entra en el orden de tabulación» | El caso del foco sigue **verde**, como su propio comentario predice. Por eso hay dos casos y no uno. |
| 3b | `tabIndex={-1}` quitado | Los dos casos del foco | El foco ya no llega: un `<h1>` sin `tabindex` no es enfocable por programación. |
| 4 | El anunciador escondido con `display:none` | `ElCambioDeRuta` → «anuncia, retitula y mueve el foco» | «Unable to find an accessible element with the role "status"». Es exactamente el mecanismo: fuera del árbol de accesibilidad, fuera del lector de pantalla. |
| 5a | Ruta `/informes` metida a mano en el enrutador | Los **dos** barridos de lista entera | Nombra la ruta: `expected [ '/informes' ] to deeply equal []`. |
| 5b | `/almacenes` degradada de permiso a sesión | La partición contada | Nombra el camino: `expected [ '/', '/almacenes' ] to deeply equal [ '/' ]`. |
| 6 | Un tipo del contrato escrito a mano | **Ver abajo. Es el hallazgo del ítem.** | |

Y una nota sobre el propio método, porque estuvo a punto de colarse un falso resultado: el primer
intento de la 3a salió **verde**, y no porque el test fuera ciego. El guion de mutación había
sustituido la primera aparición de `tabIndex={-1}` en el fichero, que está **dentro de un comentario
de documentación**, no en el JSX. Mutar un comentario no mata a nadie. Se detectó porque el
resultado era inverosímil —ese test asserta el atributo literalmente— y se repitió apuntando al
marcado. Un verde en una mutación hay que mirarlo dos veces: la primera explicación es casi siempre
que la mutación no se aplicó donde se creía.

### La sexta, entera

Se probó en los tres sitios donde cabía, y dieron tres resultados distintos.

**6a — en un traductor de funcionalidad** (`features/almacenes/api/consultas.ts`, `AlmacenDto`
tecleado con `direccion.ciudad` en vez de `poblacion`). La caza el **compilador**, en el sitio y con
nombre y apellidos: `Property 'ciudad' is missing in type '{ calle… poblacion… }'`. El linter
también, por el import que se queda sin usar. Los seis tests del listado siguen **verdes** — y está
bien que sigan: el valor llega tipado por el cliente generado, así que esto no es un agujero.

**6b — en la traducción de sesión** (`shared/api/traduccion.ts`, `SesionDto` tecleado con `token` en
vez de `tokenDeAcceso`). La cazan **el compilador y tres tests**. Tampoco hay agujero.

**6c — detrás de la aserción de tipo, que es el punto ciego.** La renovación va con `fetch` pelado
—el cliente tipado no puede renovarse a sí mismo sin morderse la cola— y su cuerpo llega como
`unknown`, así que `traducirCuerpoDeSesion` hace un `as`. **Una aserción es el único sitio de toda
la capa donde el compilador no ve el contrato.** Con el tipo escrito a mano ahí: `tsc` limpio,
`eslint` limpio, y cada sesión recuperada de la cookie sale con el testigo en `undefined`.

Los tests **sí** lo cazaban. Y ese era el problema: lo cazaban **de rebote**. Se ponían rojos dos
listados —el del selector de empresa y el del botón de reintento— por agotar su plazo de espera
cinco segundos después, en tests que no hablan de sesiones ni de testigos. Ese es exactamente el
rojo que se archiva como «test intermitente» y se vuelve a lanzar. Y los cuatro tests que **sí**
hablaban del testigo seguían verdes, porque sus manejadores simulados no miraban la cabecera.

Así que el hallazgo no es «no lo caza nada», sino algo peor de detectar: **lo cazaba mal**. Se ha
cerrado con una línea que mira lo único que importa —qué `Authorization` sale hacia el servidor— y
la mutación pasa a fallar en **194 ms** diciendo `expected null to be 'Bearer testigo-de-1111…'`.
Está en `ElTestigoDeAcceso.test.tsx` con el porqué escrito encima, y el commit es `72b0ca5`.

La doctrina que sale de ahí —**toda aserción de tipo sobre un cuerpo de respuesta lleva un test que
fije el campo del contrato, y ese test mira el efecto observable**— está en
**`docs/adr/adr-0019-la-asercion-de-tipo-es-el-punto-ciego-del-contrato.md`**, junto con la
alternativa que se descarta *por ahora* (validar el cuerpo con Zod) y la condición exacta que la
desbloquearía.

Lo único que la CI dice y aquí no se ve sigue siendo el mismo aviso ajeno a este ítem, arrastrado
desde el 0.7 y todavía sin tocar: `actions/upload-artifact@v4` (en `Backend` y en `Frontal`) y
`docker/build-push-action@v6` con `docker/setup-buildx-action@v3` (en `Imágenes de contenedor`)
apuntan a Node.js 20, en desuso, y el ejecutor los fuerza a Node.js 24. No es un fallo; es del 0.13.

**Dónde retomar exactamente:** ítem **0.12**, tests de arquitectura. Criterio: NetArchTest con las
reglas de frontera del §4; **fallan** si un módulo cruza una frontera. Tres cosas que hereda de aquí:

1. **Hay cinco barridos de lista entera, y el quinto vive en el frontal.** Los cuatro del backend
   comparan listas de tipos o de ficheros; `ElBarridoDeRutas.test.ts` compara la tabla de rutas
   contra el enrutador ya construido, y cuenta la partición. El 0.12 añade los suyos con NetArchTest:
   **la forma es la misma** —lista entera, en los dos sentidos, con el inventario escrito— y conviene
   que lo siga siendo, porque es lo que impide que un barrido nuevo nazca verde sin mirar.
2. **El frontal ya consume el contrato, así que una frontera rota se nota más lejos.** Si el 0.12
   mueve tipos entre capas para satisfacer una regla de arquitectura, el documento OpenAPI cambia,
   `esquema.ts` cambia con él, y el paso `Contrato` de la CI lo dice. Es un efecto deseado: la
   comprobación de fronteras y la del contrato se sostienen la una a la otra.
3. **Ningún test de arquitectura se ha adelantado.** El 0.11 no ha añadido NetArchTest ni ha tocado
   las referencias entre proyectos: lo que hay es lo que dejó el 0.10.

---

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
**dos migraciones nuevas** —Organización e Identidad— escritas a mano; y **cuarenta casos más** que
al cerrar el 0.9 —22 de integración y 18 rápidos—, ya descontados los que se fueron con los tres
enumerados.

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
- [x] **0.11 · Shell de React** — criterio de aceptación: login, selector de empresa, layout, rutas
  protegidas y cliente de API **generado desde el OpenAPI**; cambio de ruta accesible (`<title>`,
  `role="status"`, foco).
  Cliente generado con `openapi-typescript` desde `docs/api/openapi.json`, y la CI lo vuelve a
  generar: ni un tipo del contrato escrito a mano. Testigo de acceso **solo en memoria**; lo que
  sobrevive a la recarga es la cookie `HttpOnly`. El cambio de empresa reinicia la caché **entera**
  con `resetQueries()` —`clear()` deja a los observadores montados enseñando la empresa anterior— y
  se prueba con dos empresas de verdad, mirando lo que se pinta. Quinto barrido: toda ruta declara
  su exigencia, con la partición contada **5 = 2 + 1 + 2** y las listas comparadas enteras.
  **32** casos de Vitest con Testing Library y MSW en la frontera de red.
  Los cuatro *jobs* de la CI en verde — [run 33581760198](https://github.com/AOjeda006/Bastion/actions/runs/33581760198),
  con **388** casos rápidos, **206** de integración y **46** operaciones de OpenAPI publicados como
  `::notice::`.
- [x] **0.12 · Tests de arquitectura** — criterio de aceptación: NetArchTest con las reglas de
  frontera del §4; **fallan** si un módulo cruza una frontera.
  **Quince** reglas, cada una vista en rojo por una violación deliberada antes de aceptarla en
  verde: siete mutaciones aplicadas, ejecutadas y revertidas. La doctrina del carril —**toda regla
  afirma también que su conjunto no está vacío, y ese conteo se compara**— está en el **ADR-0020**,
  y la sostiene `ElInventarioDeModulosTests`: trece de los dieciséis módulos del §5 no existen y
  cuatro de las cinco capas de Auditoría están vacías, así que las reglas se aplican a lo que se
  descubre y lo descubierto se compara entero contra lo declarado. Reglas **3** y **4** del §4 no
  caben aquí —son SQL y DDL— y el ADR dice dónde están. **403** casos rápidos.
  Los cuatro *jobs* de la CI en verde — [run 33603428022](https://github.com/AOjeda006/Bastion/actions/runs/33603428022),
  con **403** casos rápidos y **206** de integración.
- [x] **0.13 · Integración continua** — criterio de aceptación: *workflow* que compila, pasa linter,
  tests de dominio, tests con Testcontainers y tests de arquitectura; verde de punta a punta.
  El criterio literal ya se cumplía al empezar el ítem, así que lo que se ha hecho es **comprobar
  que el verde afirmaba lo que decía**: seis mutaciones aplicadas, ejecutadas y revertidas, cada una
  con su rojo. Trae el arreglo de la deuda más antigua de la fase —**nadie aplicaba las migraciones
  en el entorno desplegado** (ADR-0021)— y la sonda que lo habría visto: una petición que **lee una
  tabla** y devuelve los datos sembrados, medida contra el estado anterior, donde daba **500**.
  Los recuentos dejan de ser un suelo y pasan a comparar **listas de ensamblados** en los dos
  carriles. El *job* `Imágenes de contenedor` se retira: su afirmación era un subconjunto
  estricto de la de `Humo`.
  Los **tres** *jobs* de la CI en verde — [run 33634114140](https://github.com/AOjeda006/Bastion/actions/runs/33634114140),
  con **403** casos rápidos en 5 ensamblados y **207** de integración en 2.

### Addenda de la fase 0 — los tres ítems que abre la auditoría (2026-09-02)

> **Estos tres NO son del Anexo A.3.** A.3 está cerrado y cumplido entero. Salen de la auditoría
> previa a la fase 1 y los decidió el usuario tras verlos expuestos; el porqué de cada uno está en
> *Decisiones tomadas → Auditoría previa a la fase 1*. Se numeran 0.14–0.16 porque son cimientos, y
> el §15 dice que una fase termina desplegable y que nunca hay dos fases abiertas a la vez: entrar en
> la 1 arrastrando esto sería abrir las dos.

- [x] **0.14 · Internacionalización del frontal** — criterio de aceptación: `react-i18next` montado
  en `app/` con `es` y `en`; **ningún texto visible escrito en un componente**, comprobado por un
  test que barre los `.tsx` y no por inspección; los títulos de `rutas.tsx` —que alimentan
  `<title>`, el `<h1>` y el anuncio de `aria-live`— pasan por el mismo diccionario; cambiar de
  idioma repinta sin recargar; las dos traducciones tienen **las mismas claves**, comparadas como
  listas y no como recuentos.
- [x] **0.15 · Organización, entera** — criterio de aceptación: `Impuesto`, `Divisa` + `TipoCambio`,
  `UnidadMedida` + `ConversionUM` y `Ubicacion` en el dominio, con su persistencia, sus migraciones
  en `db/migraciones/Organizacion/` y su contrato bajo `/api/v1/organizacion/`; semillas de tipos de
  IVA y unidades en `db/semillas/` cargadas por el migrador; y el cargador **afirmando conjunto no
  vacío**, con la CI mirando dentro de la imagen que los ficheros están.
  **F3 no se da por arreglado con la línea de `.dockerignore`**: quitarla deja que el contexto las
  copie, pero `Dockerfile.api` publica solo `/publicado`, así que hay que publicarlas también
  —`<Content Include>`— o no llegarán al contenedor que las carga.
- [x] **0.16 · `features/` espeja los módulos, y el glosario arranca** — criterio de aceptación:
  `features/identidad/` y `features/organizacion/` sustituyen a `acceso`, `almacenes`, `empresas` e
  `inicio`; `inicio` queda donde le corresponda por no ser de ningún módulo; una regla comprobable
  —no una convención escrita— impide que una funcionalidad importe de otra; y `docs/dominio/`
  estrena el glosario del lenguaje ubicuo **con lo ya construido**.

### Fase 1 · Maestros (2026-09-03)

> **La fase 1 no tiene Anexo A.3.** El §15 da el criterio de la fase entera —«alta de
> cliente/proveedor con NIF validado y de artículo con unidad, impuesto y tarifa; listados paginados
> y filtrados en servidor; dominio cubierto por tests»— y el desglose en ítems lo acordaron usuario
> y agente en la **puerta de clarificación de la fase 1**, cuyas doce respuestas con su motivo están
> en *Decisiones tomadas*. Este checklist es el resultado; **no se reordena ni se amplía** por
> iniciativa propia, igual que el A.3.

- [x] **1.1 · El presupuesto del frontal, remedido** — criterio de aceptación: dos métricas
  —**arranque** frente a **total**— en vez de una; topes **450 KiB** y **900 KiB** con el cálculo
  escrito (qué tarda en cargar y con qué red); y la CI midiendo **lo que dice medir**, comprobado
  enseñando la cifra de antes y la de después. Motivo del orden: la métrica de hoy suma fragmentos
  que el navegador no descarga al arrancar, y la fase 1 añade pantallas diferidas — el tope saltaría
  por lo que no es el problema, y en mitad de Catálogo.
  Lo mide `scripts/ci/presupuesto-del-frontal.sh`, que suma **bytes** y no bloques de disco, publica
  el desglose fichero a fichero y **falla si no ha mirado nada**. Decisión en el **ADR-0028**.
  Probado con **siete mutaciones**, y la séptima es la que enseña por qué existía el ítem: hacer
  estática una ruta diferida sube el arranque de **391 a 507 KiB** (rojo) mientras el `du` de antes
  **baja de 554 a 546 kB** — o sea que la métrica vieja se ponía **más verde** ante un empeoramiento
  real.
- [x] **1.2 · Los puertos de lectura de Organización, y la cuarta vía en rojo** — criterio de
  aceptación: `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e `IConsultaDeDivisas` en
  `Organizacion.Contracts`, con lo mínimo que cada consumidor necesita; el cruce declarado en
  `s_crucesDeclarados` **en el mismo commit** que su primer consumidor; y la **regla de dos
  fuentes** —lista declarada más descubrimiento por reflexión, comparadas enteras y en los dos
  sentidos— vista en **rojo** con su mutación: un `DivisaId` en un agregado sin puerto.
  Los tres puertos contestan un `EstadoDeMaestro` de **tres valores** —no un `bool`—, porque el
  ADR-0023 obliga a distinguir «existe» de «vale para un alta nueva». La regla vive en
  `LosIdentificadoresAjenosTests` y **no es una igualdad**: son **cinco** afirmaciones, y la quinta
  no estaba en el enunciado — se añadió porque es la única que caza un identificador ajeno con
  **nombre que no casa y sin declarar**. Ocho mutaciones, ocho rojos, cada una con la afirmación que
  la cazó, en *Estado actual*.
- [x] **1.3 · El contrato de listado, y los tipos de paginación consolidados** — criterio de
  aceptación: filtro y orden en servidor con tope; la búsqueda por **cuerpo** con cursor opaco, de
  modo que **ni la URL ni el enlace a la página siguiente** lleven el criterio, con su exención de
  `s_exentas` escrita **con el endpoint**; `Bastion.BuildingBlocks.Contracts` con `Paginacion` y
  `PaginaDe`, `ConsultaPaginada` y `Paginador` en los comunes que sus capas ya referencian, y las
  copias de Identidad y Organización **borradas**; y `TodaEscrituraDiceComoSeProtegeTests`
  **descubriendo** sus ensamblados de `Bastion.Api` en vez de tenerlos tecleados.
  Los tipos de recorrido son **cuatro y dos parejas**: `Paginacion`/`PaginaDe` para el listado y
  `Recorrido`/`TramoDe` para la búsqueda, en vez de un tipo con la mitad de los campos vacíos en
  cada respuesta. Las **ocho copias** están borradas y la divergencia ya **no compila** —tres formas
  intentadas, tres errores del compilador, ninguna prueba en rojo—. El universo de `Todas()` sale de
  la **tabla de enrutado** y se compara con una segunda fuente en disco; el criterio sensible lo
  vigila un barrido nuevo sobre el **explorador de API**, que reconoce los listados por lo que
  **devuelven** y no por cómo se llaman. Cinco mutaciones en *Estado actual*.
- [x] **1.4 · El camino de lectura de lo bloqueado** — criterio de aceptación: **listado** —y no
  `GET` individual, a propósito— de lo bloqueado para un rol nominativo y trazado, con su permiso y
  su ADR; `s_aperturasDeBloqueoPermitidas` comparada entera con **cinco** sitios; y reescrita la
  mitad que caduca de las cuatro cláusulas «DEPENDE DE» del ADR-0017, porque una condición que ya no
  es cierta y sigue escrita es una exención que **parece** razonada.
  `GET /api/v1/organizacion/bloqueados` con permiso propio (`organizacion.bloqueado.ver`), motivo
  propio (`AccesoReservadoDelArticulo32`, el **segundo** valor del enumerado, que pasa a ser lista
  cerrada comparada en los dos sentidos) y la traza anotando **quién** pregunta y no solo con qué
  motivo. El DTO **no lleva versión**, y eso ya no es una nota de confianza: son **dos** reglas que
  se ponen rojas —una sobre el contrato entero y otra sobre la respuesta de verdad—. La mitad
  caducada de las cuatro cláusulas está reescrita diciendo qué la sustituye. Y el bloqueo estrena
  **fecha de vencimiento** (`PoliticaDeRetencion`: seis años del art. 30 del Código de Comercio,
  colgando del motivo, configurable por instalación), que cierra media nota abierta desde el 0.11.
  Cinco mutaciones en *Estado actual*, y la primera entera con el DTO que la provocó.
- [x] **1.5 · Terceros: el agregado y su identidad** — criterio de aceptación: alta con **NIF, NIE o
  CIF validados de verdad** (letra de control), extranjero como identificador **opaco con país** y
  con estado de verificación; dirección **estructurada** (R17); roles sobre un solo agregado (§7.2);
  unicidad por **(empresa, NIF)**; el alta contra un identificador bloqueado devuelve un conflicto
  **que no revela**; listado paginado y filtrado por el contrato del 1.3; y `features/terceros/`.
  Y, por el **ADR-0030**, lo que hace falta para que el frontal escriba el texto de una validación
  desde el `type` del `ProblemDetails`: (a) el conjunto de `type` emitibles llega al frontal en un
  **artefacto generado y versionado** con el mismo trato que `openapi.json` —se genera, se versiona,
  la CI lo vuelve a generar y compara—; (b) el barrido de diccionarios enteros
  (`ElCambioDeIdioma.test.tsx`) pasa a comparar **también** los `type` contra sus entradas, de modo
  que un `type` sin texto sea rojo el día que se escribe y no el día que alguien ve una clave sin
  traducir; y (c) en ejecución, un `type` desconocido cae al mensaje genérico **con el identificador
  de traza** que el `ProblemDetails` ya lleva. Los errores **por campo** no entran: ahí el servidor
  nombra el campo y el frontal pone su etiqueta, y eso ya estaba resuelto.
  **Dentro del 1.5, el mecanismo va antes que el primer texto que lo use**: primero (a) y (b) —el
  artefacto y el barrido comparando—, y solo después las pantallas de Terceros. Al revés, el ítem
  escribiría los textos a mano y el barrido nacería ya con excepciones que justificar.
  **Hecho, y el orden se respetó**: los dos primeros commits son el generador de
  `docs/api/errores.json`, su paso en la CI y el barrido de diccionarios comparándolo; las pantallas
  vinieron después, así que ningún `type` de Terceros nació sin texto. El módulo estrena **siete
  rutas** —listado, alta, búsqueda por cuerpo, ficha, modificación, bloqueo y desbloqueo—, la
  identificación es un **tipo complejo de tres columnas** (país, número, verificación) validado de
  verdad para `ES` y opaco para el resto, y el domicilio fiscal es **obligatorio**, al revés que el de
  un almacén. La unicidad por (empresa, identificador) **abarca lo bloqueado** y es una decisión
  escrita, no la del índice por omisión: se comprueba en la base con `indpred IS NULL` y por el efecto
  con el desbloqueo que no choca. El conflicto que no revela se afirma **comparando las dos respuestas
  enteras**, no leyendo una, y la traza sí distingue por dentro sin llevar el número fiscal. La
  batería del carácter de control está **generada** —ni un identificador fiscal real en fixtures,
  semillas ni prosa— y recorre las dos direcciones. Seis mutaciones en *Estado actual*, dos de ellas
  ejecutadas en la CI porque solo el carril de integración las caza, y la primera entera con las dos
  respuestas puestas una al lado de la otra. Queda anotado lo que NO entra: la pantalla de búsqueda
  por cuerpo, el régimen fiscal del §7.2 —que no está en el criterio de ningún ítem— y que un tercero
  bloqueado no aparece en ningún listado, porque el del art. 32 solo recorre empresas, almacenes y
  ubicaciones.
- [x] **1.6 · Terceros: lo que cuelga** — criterio de aceptación: `Contacto`, `CuentaBancaria` con
  IBAN validado, `CondicionPago` con el tope de **60 días de la Ley 3/2004 contado desde la
  entrega**, y `LimiteCredito` **solo como importe**. Fuera, por la raya de la P6: `MandatoSEPA`
  (fase 6) y el riesgo vivo (fase 4).
  **El ítem empezó por otro sitio, y esa es la primera anotación**: antes de lo que cuelga se cerró
  el **agujero del art. 32** —cinco agregados implementan el bloqueo y el listado solo veía tres—,
  y se cerró en el orden que corresponde: primero la regla que pregunta *por qué nada se puso rojo*,
  vista roja con los dos supervivientes nombrados, y el arreglo después. No se añadieron dos valores
  a un enumerado: el enumerado vive en un módulo y la obligación es transversal, así que cada módulo
  que bloquea expone su **puerto de consulta** y la composición los junta —sin `JOIN` entre esquemas
  y sin llamada HTTP—, con una segunda regla que lo exige.
  **Lo que cuelga, hecho**: `Contacto` (cuatro campos, **sin campo de notas**, y su ausencia es la
  decisión), `CuentaBancaria` con IBAN validado por **mod-97 y longitud por país** sobre los 37 de
  SEPA —batería generada, en las dos direcciones y con **un negativo por cada motivo distinto**:
  longitud, país inexistente y control malo—, `CondicionPago` con el tope de 60 días puesto en los
  **tres sitios** (dominio, borde y `CHECK` de la base) y sin calcular ningún vencimiento, y
  `LimiteCredito` con **su divisa explícita**, `numeric(18,4)` por R6, que **no se hereda en
  silencio** de `Empresa.DivisaBase`. Entra también el **régimen fiscal del §7.2** —alcance
  comprometido sin casa—, con el corte de `LimiteCredito`: tres de sus cuatro condiciones como
  columnas con `CHECK` y **ningún comportamiento**; el tipo de retención se queda fuera porque ya
  tiene dueño desde el 1.2.
  **Las tres notas abiertas, resueltas**: la correlación buscar/alta pasa a *Decisiones* con sus
  tres salidas costeadas —no la puede tomar el agente—; el art. 32, cerrado en código; y el régimen
  fiscal, con el corte que se sostiene y el trozo que no, dicho.
  **Catorce mutaciones con su línea base nombrada**, doce rojas y **dos verdes que valen más**: la 7
  destapó que la decisión de la divisa no la guardaba ningún test, y su regla —escrita ya— vive en el
  carril de integración, que esta máquina no ejecuta.
  **Y el ítem no terminó donde parecía**: contra PostgreSQL, las **siete** rutas que escriben un
  hijo contestaban `412`, y ninguna de las que rechazan antes de escribir fallaba. La causa era el
  modelo diciendo que las claves `Guid` se rellenan al insertar cuando las pone la fábrica del
  dominio, y con eso EF marcaba los tres hijos como modificación. **ADR-0032**, con su convención
  de cierre, su caso de un segundo y sin Docker, y los cuatro huecos de declaración que aparecieron
  al descubrir el universo de modelos en vez de enumerarlo.
- [x] **1.7 · La retirada y las dos conversiones** — criterio de aceptación: el **ADR-0023
  implementado entero** —retirada en los cuatro maestros de instalación, tolerancia de la conversión
  inversa, resolutor de conversiones encadenadas con **error con nombre**—, y **antes** de que
  Catálogo referencie `UnidadMedida`, que es el disparador que el propio ADR dejó escrito.
  Cerrado con el run **34177571670** sobre `dd89e9f`. Las tres decisiones del ADR, más las **tres**
  que dejaba abiertas y aquí se cierran con su motivo: cómo se ven las retiradas (parámetro del
  listado, por contraste con el ADR-0027), que `TipoCambio` y `ConversionUM` no tienen puerto de
  consumo sin que eso las haga vacuas, y que una fila retirada **sigue restringiendo** a su inversa.
  Ocho mutaciones en *Estado actual*, con la 1 y la 3 enteras; la 3 destapa que un 404 ahí deja la
  retirada sin marcha atrás.
- [x] **1.8 · Catálogo: artículo y categoría** — criterio de aceptación: alta de artículo con unidad
  e impuesto **validados por los puertos del 1.2** (no guardados a ciegas); `Categoria` jerárquica
  con **comprobación de ciclos**; listado paginado y filtrado; y `features/catalogo/`. Fuera:
  `CodigoBarras`, que es de la **fase 2** con su import.
  Cerrado con el run **34424407829** sobre `f90dbed`. Catálogo es el consumidor para el que se
  construyó la retirada del ADR-0023: las tres casillas de `EstadoDeMaestro` significan tres cosas
  distintas en un camino de negocio, y `SoloResuelveLoViejo` con sus **dos mitades**. Las nueve
  decisiones en *Decisiones tomadas → ítem 1.8*, con las dos que el enunciado pedía nombradas —el
  árbol como **lista de adyacencia** y la **cota** del ascenso— y su motivo. Ocho mutaciones en
  *Estado actual*, con la 1, la 2 y la 3 enteras; la 2 destapa una aserción por HTTP cuyo escenario
  no la ejercía, arreglada en `1eab397`.
- [ ] **1.9 · Tarifas** — criterio de aceptación: `Tarifa` con vigencia y divisa y `LineaTarifa` por
  artículo o categoría con escalado por cantidad (§7.3); **precio o descuento excluyentes** en el
  objeto de valor; **solape de vigencias prohibido por restricción de exclusión**; sin tarifa
  aplicable, **error de negocio con nombre y nunca precio cero**; y la **precedencia completa**
  —artículo > categoría más cercana > … > raíz— probada con el caso que la distingue: **dos líneas
  de categoría a distinta profundidad**.
- [ ] **1.10 · Los dos cruces mutuos** — criterio de aceptación: `ArticuloProveedor` (Catálogo →
  `Terceros.Contracts`) y `Tercero.TarifaAsignada` (Terceros → `Catalogo.Contracts`), cada uno por
  el `Contracts` de su dueño, resuelto en proceso y **declarado**. Es ítem propio porque la
  dependencia es **mutua** y eso se ve, no se reparte.
- [ ] **1.11 · Importación CSV** — criterio de aceptación: **unidad de aislamiento = la fila**, de
  modo que un fichero con filas malas importa las buenas; **idempotencia por fichero** —una
  importación es UNA operación, y el reenvío con la misma `Idempotency-Key` devuelve el resultado
  guardado sin reimportar—, con el **hash del contenido** guardado al lado, de manera que la misma
  clave con **otro fichero** sea un error explícito; e informe con **línea y motivo** de cada fila
  rechazada, que es lo único que permite corregir y reimportar solo esas.


## Imports pendientes de `CLAUDE.md`

`CLAUDE.md` ya trae el **núcleo permanente** (A.2.1) y los de **fase 0** (A.2.2). Los de abajo
**no están puestos a propósito**: cada import cuesta contexto en **cada** turno, así que se añaden
al empezar su fase y se quedan (Anexo A.2.3).

> **Los dos de la fase 1 ya están puestos** (2026-09-03, al abrir la puerta de clarificación de
> la fase): `herramientas/proteccion-datos.md` y `patrones/soft-delete.md`. El segundo no era
> opcional: la **retirada** del ADR-0023 es un final de vida que **no** es el bloqueo de la R16,
> y las dos van a convivir en las mismas tablas.

| Al empezar la fase | Añadir a `CLAUDE.md` |
|---|---|
| **2 · Inventario** | `@../BibliotecaDocumentacion/negocio/identificacion-articulos/convenciones.md` |
| **5 · Facturación** | `@../BibliotecaDocumentacion/negocio/facturacion-espana/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/verifactu/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/iva-espana/convenciones.md` |
| **6 · Tesorería** | `@../BibliotecaDocumentacion/negocio/pagos-y-cobros/convenciones.md` |
| **7 · Contabilidad** | `@../BibliotecaDocumentacion/negocio/contabilidad/convenciones.md` |

Los `referencia.md` **no se importan nunca**: son para que los lea una persona. Se consultan a mano
cuando hace falta el porqué.

> **Aviso para la fase 5:** `negocio/verifactu/convenciones.md` no es material de consulta, es
> **lectura obligatoria entera antes de la primera línea** de esa fase.

## Notas / riesgos

- **TRASLADADA A *DECISIONES* (2026-09-07, ítem 1.6) · el conflicto no revela, pero DOS respuestas
  juntas sí.** El hecho sigue siendo el que se anotó en el 1.5 y no ha cambiado: la búsqueda no
  encuentra lo bloqueado, el alta contesta `409` igual esté la ficha activa o bloqueada, y quien
  tenga los dos permisos deduce lo que ninguna de las dos respuestas dice por separado. Deja de estar
  aquí porque **no es un riesgo que se descubra**: es un cruce conocido, medido y con las tres
  salidas posibles escritas —separar permisos, que la búsqueda mienta, o que el alta deje de
  distinguir— cada una con lo que cuesta. Ninguna la puede tomar el agente, así que la pregunta queda
  planteada entera en *Decisiones tomadas → ítem 1.6*, decisión **1**.

- **CERRADA (2026-09-07, ítem 1.6) · un tercero bloqueado no aparecía en NINGÚN listado — y un
  usuario bloqueado tampoco, desde el 1.4.** La nota se abrió en el 1.5 mirando a `Tercero` y **se
  quedaba corta**: el listado nació corto en el **1.4**, sin los usuarios bloqueados, que son personas
  físicas y el caso más nítido del artículo. Y el motivo no era un olvido, era **estructural** —el
  enumerado vivía en `Organizacion.Application` y el repositorio unía tres tablas de un solo esquema,
  así que por construcción no alcanzaba a Identidad ni a Terceros—. Cerrada con la regla que lo pone
  rojo escrita **antes** del arreglo (roja con los dos supervivientes) y con el patrón de puertos del
  1.2: cada módulo que bloquea contesta por lo suyo y el listado compone. **ADR-0031**, y el detalle
  en *Estado actual* → *Ítem 1.6*.

- **CERRADA (2026-09-07, ítem 1.6) · el régimen fiscal del §7.2 ya tiene ítem, y es el 1.6.** Era una
  pregunta —«si es del 1.6 hay que escribirlo allí; si es de facturación, de qué fase»— y se contesta
  por lo que era: **alcance comprometido sin casa**, no alcance nuevo. Su sitio es el tercero, y
  dejarlo para la fase 3 obligaría a migrar un maestro con datos dentro. Entra con el corte de
  `LimiteCredito` —lista cerrada de valores con su `CHECK` y ningún comportamiento— y el corte se
  sostiene en **tres condiciones de cuatro**: territorio, recargo de equivalencia, criterio de caja y
  el booleano de si se le retiene. **El tipo de retención se queda fuera**, y ese es el trozo donde el
  corte no aguantaba: no significa nada sin la regla que lo aplica, y ya tiene dueño desde el 1.2.
  Razonamiento entero en *Decisiones tomadas → ítem 1.6*, decisión **2**.

- **ABIERTO (2026-09-04, recontado el 2026-09-05) · los tests del frontal sueltan 109 avisos de
  `act(...)`, en SIETE ficheros.** `An update to <X> inside a test was not wrapped in act(...)` sale
  **109** veces en una ejecución limpia. Medido nueve veces: 109 en ocho de ellas y **105** en una,
  o sea que la cifra es estable de sobra para servir de línea base pero **no es determinista** —y esa
  variación es, ella misma, la prueba de lo que la nota dice: hay actualizaciones de estado que caen
  fuera de la ventana que el test controla, y a veces llegan y a veces no.
  El comando, escrito para que no haya que fiarse de la cifra:
  `npm --prefix frontend run test 2>&1 | grep -c "was not wrapped in act"`, y el reparto por fichero
  atribuyendo cada aviso al último encabezado `stderr | <fichero>` **después de quitar los códigos
  ANSI** (sin quitarlos, el encabezado no casa y todo cae en un cajón). Reparto:
  `ElListadoDeAlmacenes.test.tsx` 24, `ElCambioDeIdioma.test.tsx` 17, `LasRutasProtegidas.test.tsx` 17,
  `ElCambioDeRuta.test.tsx` 14, `ElSelectorDeEmpresa.test.tsx` 14, `ElTestigoDeAcceso.test.tsx` 13 y
  `LaPantallaDeAcceso.test.tsx` 10. Componentes: `SelectorDeEmpresa` 25, `Guarda` 25, `Disposicion` 25,
  `RouterProvider` 24, `PaginaDeInicio` 9 y `PaginaNoEncontrada` 1.
  **Las dos cifras anteriores estaban mal y por la misma causa.** El 91 «en seis ficheros» del 1.4 se
  midió sobre un registro **truncado por la cola**: perdía `ElTestigoDeAcceso.test.tsx` entero (13) y
  cinco avisos de `ElCambioDeRuta` (91 + 13 + 5 = 109). El 92 del 0.15 viene del mismo vicio. La
  lección no es la cifra, es que **una cifra medida se publica con el comando que la mide**; está
  escrita como regla en `AGENTS.md`.
  Los componentes que los provocan son pocos y se repiten — o sea que **no** son siete problemas, es un puñado de
  efectos que se resuelven después de que la aserción ya haya mirado. **Por qué importa y no es
  ruido:** es la misma familia que el flake del 0.14 —`document.title` mirado antes de que el efecto
  lo pusiera, rojo en la CI sobre un commit que solo tocaba un `.md`—, y un aviso de `act()` dice
  literalmente que hay una actualización de estado fuera de la ventana que el test controla. Hoy los
  46 casos pasan; el día que el *runner* vaya cargado, alguno de estos es el candidato. **No entra en
  el criterio del 1.4** —el ítem no toca el frontal— y queda anotado con los ficheros medidos, no
  recordados. **Lo que lo cerraría:** envolver la interacción que dispara el efecto, no envolverlo
  todo: `waitFor` sobre lo que pone un efecto y nada más, que es la regla que dejó el 0.14.

- **CERRADO (2026-09-04, en el ítem 1.4) · el censo de reglas cubría un carril de cuatro.** Anotado
  al cerrar el 1.3 como candidato, y autorizado por el usuario para este ítem. `LasReglasDeEsteCarrilTests`
  censaba `Arquitectura.Tests` y solo ese ensamblado desde el 0.12; los otros tres carriles con reglas
  tenían **287 casos** que se podían borrar dejando la suite verde, más rápida y con una frontera sin
  guardián. **Cerrado** con `tests/Comun/CensoDeReglas.cs` compartido por `Compile Include` y una lista
  escrita por ensamblado: **316 nombres** censados en los cuatro carriles (134 + 118 + 41 + 23,
  contados con grep sobre las cuatro listas), comparados enteros y en los
  dos sentidos. Comprobado por el efecto: borrar `El_barrido_ve_los_cuerpos_y_reconoce_un_testigo` deja
  el carril en 125 casos verdes y **solo** el censo se pone rojo, nombrando la regla. Y de paso salió
  un falso verde de la versión original —`orderby nombre, StringComparer.Ordinal` son dos claves de
  orden y el comparador ordinal no se aplicaba nunca— que era invisible con 23 nombres y deja de serlo
  con 133.

- **CERRADO (2026-09-04, en el ítem 1.4) · la batería local de `AGENTS.md` no ejecutaba el guion que
  decide el desenlace de la CI.** Anotado al cerrar el 1.3, cuando el run 33830689761 salió rojo con
  la batería entera en verde: `dotnet test` dice «ningún caso falla» y `scripts/ci/recuento-de-tests.sh`
  dice «han corrido exactamente los ensamblados que tenían que correr», y un cambio que altera **qué**
  corre es invisible para el primero por construcción. Se dejó como candidato «para el ítem que toque
  el carril», y este lo toca: el censo hace que `Bastion.Api.IntegrationTests.dll` empiece a correr en
  el carril rápido. **Cerrado** añadiendo los dos recuentos a la batería de `AGENTS.md`, con las
  listas del *workflow* literalmente y con los `--logger trx --results-directory` que hacen falta para
  poder ejecutarlos. Verificado en este mismo ítem antes de empujar: rojo con la lista vieja
  nombrando el ensamblado, verde con la nueva.


- **CERRADO (2026-09-03) · un test del frontal miraba `document.title` sin esperar al efecto que lo
  pone.** Saltó donde peor se interpreta: en la CI, sobre un commit que **solo tocaba un `.md`**
  —`ElCambioDeIdioma.test.tsx:41`, «expected `''` to be `'Almacenes · Bastion'`»—, después de pasar
  en todas las ejecuciones anteriores. No era una regresión: el `<h1>` y el `<title>` salen del
  mismo componente pero **por caminos distintos**, el primero en el pintado y el segundo en un
  `useEffect`, así que `findByRole` puede resolver **antes** de que el efecto haya corrido. Se
  reprodujo en local lanzando seis copias del fichero a la vez —falló una, con el mismo mensaje—,
  que es la contención que tiene el *runner* cuando va cargado. Arreglado esperando, como ya hacían
  los otros dos sitios que miran el título; el `lang` de la línea de al lado se queda **sin**
  esperar a propósito, porque lo pone `crearI18n` antes del `render`. **La regla que deja:** una
  aserción sobre algo que pone un efecto se espera; una sobre algo que pasó antes de montar, no.

- **ABIERTO (2026-09-03) · el bloqueo del art. 32 no tiene fecha de vencimiento ni proceso de
  destrucción.** `proteccion-datos.md` entra con la fase 1 y es tajante: bloquear es «identificar y
  reservar» **solo durante el plazo de prescripción**, y pasado ese plazo **hay que destruir**. Un
  estado de bloqueo sin vencimiento «convierte una obligación de conservación acotada en conservación
  indefinida, que es otra infracción». Hoy `Empresa`, `Almacen`, `Ubicacion` y `Usuario` llevan
  bloqueo **con su fecha de bloqueo**, que es la mitad buena; lo que no existe es ni el plazo ni el
  proceso que lo aplica. **No entra en el ítem 1.4 a propósito:** ese ítem construye el camino de
  lectura, y el plazo es materia de retención —cuánto dura la prescripción de cada responsabilidad—,
  que no se decide leyendo código. **Lo que lo desbloquea:** el plazo, dicho por quien pueda decirlo.
  **MEDIA NOTA CERRADA (2026-09-04, en el ítem 1.4), y la otra media sigue abierta.** El plazo lo dijo
  el usuario al abrir el ítem —cuelga del motivo, `SupresionSolicitada` vence y `CeseDeUso` no,
  **seis años** por omisión (art. 30 del Código de Comercio) y configurable por instalación— y está
  implementado en `PoliticaDeRetencion`, con el vencimiento **saliendo en el listado del art. 32**:
  enseñar la reserva sin su fecha de fin habría sido enseñar una conservación indefinida como si fuera
  acotada. **Lo que sigue sin existir es el proceso de destrucción al vencer:** hoy el vencimiento se
  **ve**, no se **ejecuta**, y una fila vencida se queda ahí hasta que alguien la mire. Eso necesita
  un trabajo de fondo con su ventana y su registro, y sitio propio en la hoja de ruta.
  **Emparentado** con la nota de `auditoria.claves_de_idempotencia`: las dos son políticas de
  retención sin dato con el que calibrarlas, y las dos tienen ya el mecanismo —una migración aplicada
  por el paso de despliegue del ADR-0021—.

- **ABIERTO (2026-09-03) · el frontal no tiene forma de aprovechar el listado de lo bloqueado
  mientras la lectura individual no exista.** Decidido en la puerta de la fase 1: el ítem 1.4
  construye **listado y no `GET` individual**, porque el individual emitiría `ETag` y **caducaría
  las cuatro exenciones del ADR-0017**, devolviendo `If-Match` a los cuatro desbloqueos. Es la
  decisión correcta y tiene un precio que conviene tener escrito: desde el listado se puede
  desbloquear —el identificador basta y el desbloqueo no pide etiqueta— pero **no se puede abrir la
  ficha** de lo bloqueado para mirarla antes. Si algún día hiciera falta esa ficha, no es un cambio
  de pantalla: es volver a exigir `If-Match` en cuatro acciones y reescribir el ADR-0017.

- **CERRADO (2026-09-03, en el ítem 1.1) · El presupuesto de tamaño del frontal
  se queda corto — y además mide otra cosa.** Al razonarlo en la puerta de la fase 1 apareció lo que
  esta nota no veía: el `dist` está **partido en fragmentos y las rutas se cargan tarde**, así que
  `du -sk --exclude='*.map' dist` **suma también lo que el navegador NO descarga al arrancar**
  (554 kB de total contra ≈400 kB de arranque). El paso contradice su propio comentario, igual que
  lo contradecía cuando medía los `.map`. **Decidido: dos métricas y dos topes —arranque ≤ 450 kB,
  total ≤ 900 kB— con el cálculo escrito**, y el criterio no es nuevo: es el mismo con el que ya se
  excluyeron los mapas, aplicado donde dejó de aplicarse solo (ADR-0028).
  **Hecho:** `scripts/ci/presupuesto-del-frontal.sh`, con siete mutaciones, y la séptima enseñando
  que la métrica vieja **bajaba** (554 → 546 kB) mientras el arranque empeoraba (391 → 507 KiB).
  Texto original: La CI corta en
  **600 kB** (`du -sk --exclude='*.map' dist`). Tras el 0.14 son **554 kB**: la i18n costó 64, de
  490 a 554, medido a los dos lados. Quedan **46 kB** y la fase 1 trae Terceros y Catálogo enteros,
  con sus formularios y sus tablas. Hay que revisar el tope **con un número razonado** —qué tarda en
  cargar y con qué red— y no subirlo el día que salte, que es como un presupuesto deja de serlo.

- **ABIERTO (2026-09-02), CON FECHA DESDE EL 2026-09-03 · El día que un mensaje del servidor se le
  enseñe al usuario, la i18n vuelve a la mesa.** Este era el octavo tema que el usuario puso sobre
  la mesa al abrir la fase 1, y **se cayó de la tanda de preguntas al redactarla**: no se contestó
  porque no se preguntó. **No se hereda el default**; se pregunta antes de empezar el **1.5**, que
  es el primer ítem con una validación —un NIF con la letra mal— que apetece enseñar con el texto
  del servidor. Texto original: Hoy el frontal **no pinta nunca** `ProblemDetails.detail`: todo lo que lee una
  persona está escrito en el frontal, y por eso el 0.14 no tocó la API. Pero las validaciones de la
  fase 1 —un NIF con la letra mal, un código de artículo repetido— son justo las que apetece
  enseñar con el texto que manda el servidor. Ese día hay que decidir entre que la API traduzca por
  `Accept-Language` o que devuelva códigos y traduzca el frontal. **La respuesta por defecto es la
  segunda**, porque mantiene la API sin saber de presentación; pero se decide, no se hereda.

- **CERRADO (2026-09-02) · `UnidadDeTrabajoPorModuloTests` fija sus ensamblados a mano, y le falta
  uno.** `tests/Api.FunctionalTests/Composicion/UnidadDeTrabajoPorModuloTests.cs` declara
  `s_capasDeAplicacion` con dos `typeof(...)` —Organización e Identidad— y **no incluye
  `Bastion.Auditoria.Application`**. Hoy no cambia nada, porque ese ensamblado está vacío; el día que
  Auditoría estrene su primer caso de uso, la regla dejará de cubrirlo **sin ponerse roja y sin
  cambiar de color**, que es exactamente la avería que el ítem 0.12 existe para impedir en el otro
  carril. **Lo que haría falta:** que la lista se descubra de los ensamblados que arrastra
  `Bastion.Api` —filtrando `Bastion.*.Application`— en vez de tecleada. **No se arregla en el 0.12
  a propósito:** es de otro ítem, y la única forma de arreglarlo desde el carril de arquitectura
  sería duplicar allí el inventario, que es peor que el problema. Sitio natural: el **0.13**.
  **Cerrado ahí** (`71357f2`): la lista se descubre de los ensamblados que arrastra `Bastion.Api`
  filtrando `Bastion.*.Application`, y se compara entera en las dos direcciones. Un ensamblado de
  aplicación nuevo entra solo, y uno que desaparezca pone la regla en rojo.
- **CERRADO (2026-09-02) · el suelo de recuento de la CI protege dos ensamblados de cinco.** El paso
  rápido falla si el total baja de 300, y hoy hay 403 en cinco ensamblados. Perder uno entero deja:
  111 → 292 ✅, 116 → 287 ✅, **103 → 300 ❌** (el suelo compara con «menor que», así que 300 clavados
  pasa), **58 → 345 ❌**, **15 → 388 ❌**. O sea que `Api.FunctionalTests`, `Identidad.UnitTests` y
  `Arquitectura.Tests` podrían dejar de ejecutarse **enteros** con la CI en verde. Esto se descubrió
  al cerrar el 0.12 haciendo la cuenta: el comentario del *workflow* afirmaba que el suelo cazaba la
  pérdida de cualquiera de los cuatro grandes, y era **falso** —lo era ya con 388—. El comentario
  está corregido con los números de verdad; el arreglo no. **Lo que haría falta:** contar
  **ensamblados** además de casos, con la lista entera comparada contra la declarada, que es la
  forma de los demás barridos del proyecto. Y empeora sola: cuanto mayor es el total, menos nota la
  pérdida de una parte. **Es más barato de lo que parece:** `scripts/ci/recuento-de-tests.sh` ya
  cuenta y publica el desglose por ensamblado —el `::notice::` dice literalmente «403 casos … en 7
  ensamblados · `Bastion.BuildingBlocks.UnitTests.dll` 111, `Bastion.Organizacion.UnitTests.dll`
  116, …»—, así que el dato ya está calculado y a la vista; lo único que falta es **compararlo**
  contra una lista declarada. Es del **0.13**, el ítem de la revisión final del flujo.
  **Cerrado ahí** (`caacfd7`): los dos carriles reciben además la **lista declarada** de sus
  ensamblados y `scripts/ci/recuento-de-tests.sh` la compara entera, en las dos direcciones
  —falta uno, o corre uno que nadie declaró—. Comprobado por el efecto sacando
  `Bastion.Identidad.UnitTests` de la solución: el árbol compila, el carril sale con 0 y 345
  casos, el suelo lo deja pasar, y el paso de la CI falla **nombrando el que falta**.

- **ABIERTO (2026-09-02) · la renovación de sesión asierta el cuerpo en vez de validarlo.**
  `traducirCuerpoDeSesion` hace un `as` sobre el cuerpo de la renovación, porque esa petición va con
  `fetch` pelado y no llega tipada por el contrato. Es la **única** aserción de la capa y ya lleva su
  test —la cabecera `Authorization` que sale hacia el servidor—, pero un test fija un campo y un
  esquema valida el cuerpo entero. **Lo ortodoxo sería Zod**, y se ha descartado por ahora con
  motivo: el esquema habría que escribirlo a mano, que es exactamente lo que la regla del contrato
  prohíbe; cambiaría una duplicación silenciosa por una ruidosa, que es mejor pero sigue siendo
  duplicación. **Lo que lo desbloquea:** un generador de esquemas de Zod desde el OpenAPI en el que
  se confíe, o que las aserciones dejen de ser una sola. Argumento entero en el **ADR-0019**.
  **Repasada al cerrar la fase 0: sigue abierta, y eso es la decisión.** No ha aparecido el
  generador en el que confiar, y las aserciones siguen siendo una sola.
- **CERRADO (2026-09-02) · los identificadores de permiso son la única parte del contrato escrita a
  mano.** `frontend/src/shared/sesion/permisos.ts` lleva las cadenas `organizacion.almacen.ver` y
  `organizacion.empresa.ver` tecleadas. No hay de dónde generarlas: el catálogo de permisos es un
  endpoint en tiempo de ejecución, no un `enum` del documento OpenAPI, así que `openapi-typescript`
  no las ve. El modo de fallo está elegido y es el bueno —una constante mal escrita **esconde** la
  pantalla, porque la comprobación es «¿está este permiso en la lista que trae la sesión?»— pero
  sigue siendo una cadena que nadie compara con la del servidor. **Lo que haría falta:** que el
  catálogo de permisos salga en el contrato (un `enum` en algún DTO, o un endpoint que lo enumere y
  un barrido que compare las dos listas enteras, como los cinco que ya hay). **Sitio: el 0.13**, y
  eso corrige lo que decía antes esta nota. En el 0.12 se comprobó que **no** es una regla de
  arquitectura —ese carril lee ensamblados de .NET y esto es TypeScript—, pero también que no hace
  falta esperar a la fase 1: el barrido no necesita que haya más permisos para existir, y esperar
  significa entregar la fase 0 entera con la única cadena del contrato escrita a mano sin nadie que
  la compare. La forma es la de `ElFiltroNoSeSaltaPorAhiTests`, que ya lee código fuente.
  **Cerrado ahí** (`d4e2352`), con una corrección sobre lo previsto: el barrido lee el fichero
  fuente, sí, pero **no puede vivir en el carril rápido**, porque la otra mitad de la
  comparación —el catálogo— solo existe con la API en pie. Vive en `Api.IntegrationTests`, que
  ya levanta el host, y compara las dos listas enteras.
- **ABIERTO (2026-09-02) · lo bloqueado sigue sin camino de lectura, y por tanto sin camino de
  desbloqueo desde el frontal.** Decidido en la puerta del 0.11 y sin cambios: una fila bloqueada
  contesta 404 a su propio `GET` (R16, ADR-0016), así que no aparece en ningún listado y la interfaz
  no tiene desde dónde ofrecer el desbloqueo. **La respuesta NO es un `IgnoreQueryFilters` en el
  frontal ni ensanchar el ámbito existente**: los tres sitios que abren `ViendoLoBloqueado` siguen
  siendo los tres desbloqueos y se siguen comparando enteros. Lo que haría falta es un motivo nuevo
  —administración—, su permiso, y un ADR que argumente por qué el art. 32 admite esa visualización.
  Trabajo de dominio y de derecho, no de armazón.
  **Repasada al cerrar la fase 0: sigue abierta, y eso es la decisión.** La fase 0 no trae ese
  motivo; lo mirará la fase que lo traiga, con su ADR.
  **La trae la fase 1: es el ítem 1.4** (2026-09-03), con **dos correcciones de hecho** sobre lo que
  decía esta nota. Primera: **el motivo ya existe** —`MotivoParaVerLoBloqueado.AdministracionDelBloqueo`
  está construido y en uso—, así que el ítem no lo crea, crea el **camino de listado**. Segunda:
  **son cuatro sitios, no tres** —`DesbloquearAlmacen`, `DesbloquearEmpresa`, `AdministracionDeUsuarios`
  y `BloquearUbicacion`—, y Terceros será el quinto; como la lista se compara entera y en los dos
  sentidos, escribir «tres» ahí es escribir un rojo. Y una consecuencia que no se veía: las cuatro
  exenciones del ADR-0017 llevan escrita la condición de la que dependen —«que ninguna lectura de la
  API entregue una fila bloqueada… con su ETag»—, así que el 1.4 **caduca su primera mitad**. Por eso
  construye **un listado y no un `GET` individual**: el listado no emite ETag, o sea que no resucita
  la llave y las cuatro exenciones siguen en pie; reescribir la mitad caducada entra en el criterio
  del ítem.
- **ABIERTO (2026-09-02) · el contrato describe los enteros como `integer | string`.**
  `PaginaDeAlmacenDto.total` y sus hermanos salen del OpenAPI como `type: ["integer","string"]`, en
  la petición **y** en la respuesta. No es un fallo del generador: `JsonSerializerDefaults.Web`
  implica `NumberHandling = AllowReadingFromString`, y el documento describe con honestidad lo que
  la API acepta. Se estrecha en la traducción (`shared/api/enteros.ts`), en un solo sitio y con el
  motivo escrito. **No se ha tocado el servidor a propósito:** cambiar la serialización para que el
  documento quede más cómodo sería mover el contrato para que encaje el cliente. Si algún día se
  decide lo contrario, el sitio es `JsonSerializerOptions` en el arranque de la API, y afecta a todo
  el que ya consuma el contrato.
  **Repasada al cerrar la fase 0: sigue abierta, y eso es la decisión.** Estrechar el servidor
  para que el cliente quede cómodo sería mover el contrato para que encaje el consumidor.
- **ANOTADO (2026-09-02) · en Node 25 el `localStorage` del entorno de tests no es el de jsdom.**
  Node 25 define `globalThis.localStorage` como accesor propio (experimental, tras
  `--localstorage-file`) y gana al de jsdom: lo que queda es un objeto pelado, sin `setItem` ni
  `clear`. Importa porque envenena justo el test que más importa: «el testigo no llega a
  `localStorage`» pasaría siempre —incluso guardándolo— porque la línea que lo guardara reventaría
  antes con un `TypeError`. Verde por avería del entorno, no por la propiedad. `setupTests.ts` pone
  un almacén de verdad con la API estándar. Se vigila al subir de Node: el día que el de jsdom vuelva
  a ganar, el remiendo sobra y hay que quitarlo, no dejarlo tapando al bueno.
  **Repasada al cerrar la fase 0: de vigilancia, no de arreglo.** El disparador es subir de
  versión de Node, no una fecha.
- **ARREGLADO (2026-09-02) · el presupuesto de tamaño del frontal ya aprieta.** Estaba en 1024 kB
  con 205 kB de consumo: un margen de cinco veces no señala nada. Con el armazón terminado
  —enrutador, caché de servidor, formularios, validación y cliente generado— el navegador descarga
  **490 kB**, y el tope baja a **600**: lo medido más un margen corto, que es la recomendación que el
  propio 0.10 dejó escrita. El fragmento mayor son 331 kB (`index`), y el segundo 82 kB (`zod`).
- **ARREGLADO (2026-09-02) · los mapas de fuentes ya no se publican.** `sourcemap: 'hidden'`: se
  generan —hacen falta para leer una traza y viajan en el artefacto de la CI— pero no llevan el
  comentario `sourceMappingURL` que los enlaza, `deploy/Dockerfile.web` los borra del raíz servido y
  nginx contesta 404 a cualquiera que quede. Tres cerrojos porque publicar el mapa es publicar el
  código, y con uno solo basta con que alguien copie el `dist` a mano.
- **ARREGLADO (2026-09-02) · las cabeceras de seguridad no llegaban a `/assets/`.** `add_header` no
  se hereda al bloque de dentro si el de dentro tiene el suyo, y `location /assets/` declaraba su
  `Cache-Control`: se quedaba sin `nosniff`, sin CSP y sin `X-Frame-Options` **justo por donde se
  sirve todo el JavaScript**. Es un fallo mudo —la portada las lleva, que es donde uno mira—.
  Ahora viven en `deploy/nginx-cabeceras-de-seguridad.conf` y se incluyen en cada `location` que
  las necesita.
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
  **Repasada al cerrar la fase 0: sigue abierta, y la mirada del 0.13 ya ha ocurrido.** La
  segunda mitad de la cita —quién aplica las migraciones— está resuelta (**ADR-0021**), así
  que el **mecanismo ya existe**: una limpieza sería una migración más, aplicada por el mismo
  paso del despliegue. Lo que sigue faltando no es mecanismo sino el dato —cuánto tarda un
  cliente real en reintentar—, y ese no está en el código. Sin él, borrar es adivinar; y
  adivinar mal no encoge la tabla, quita la garantía.
- **CERRADO (2026-08-26, en el 0.13) · el *compose* no aplica las migraciones, así que la semilla no llega a
  aplicarse ahí.** Nadie ejecuta `dotnet ef database update` ni al arrancar la API ni en el
  `docker-compose.yml`: la base del entorno local no tiene tablas. Consecuencia práctica de hoy:
  las siete variables `BASTION_SEMILLA_*` están **declaradas en el compose y vacías por omisión**,
  así que la semilla se salta con un aviso en el registro y el entorno levanta igual que antes. Si
  se rellenan sin haber migrado, el arranque **revienta a propósito** (la semilla no se calla). Lo
  que falta es decidir **quién** aplica las migraciones en un despliegue —un paso del compose, un
  `initContainer`, o el propio arranque de la API— y eso es materia del **0.13**, no del 0.5. Los
  tests de integración sí migran: lo hace su fixture antes de levantar el host.
  **Cerrado en el 0.13** (`e6425d3`, **ADR-0021**): lo aplica un contenedor de un solo uso, el
  mismo artefacto de la API invocado con `--migrar`, y el resto espera a que salga bien. La
  deuda llevaba abierta desde el 26 de agosto y no se veía porque **ninguna sonda de Humo
  tocaba una tabla**; hoy hay una que sí, y contra el estado anterior daba 500. De paso
  apareció la avería que la escondía: el `.dockerignore` excluía `db/migraciones`, el
  `<Compile Include>` no casaba con nada y la imagen se publicaba **sin una sola migración**
  —un glob vacío no es un error—.
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
