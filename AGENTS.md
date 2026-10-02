# AGENTS.md — Contrato de trabajo del agente

Este documento es el **contrato operativo**. `CLAUDE.md` dice *qué* convenciones seguir;
este dice *cómo* proceder. Ante conflicto, mandan las convenciones importadas en `CLAUDE.md`.

## Ciclo de trabajo (cada turno)

1. **Orientarse:** lee `docs/PLAN.md` y el último commit. Localiza el primer ítem no completado
   del checklist. No reconstruyas el estado de memoria: el PLAN es la verdad.
2. **Clarificar (puerta de arranque):** si hay decisiones esenciales sin especificar con varias
   opciones viables, pregúntalas **todas juntas** y **no avances** hasta resolverlas. Anota las
   respuestas en `PLAN.md` → *Decisiones*.
3. **Ejecutar un paso:** aborda **un** ítem del checklist a la vez. Respeta las convenciones
   importadas (arquitectura, naming, errores, testing…).
4. **Verificar:** ejecuta build/tests/lint (ver *Comandos del proyecto*). Un paso no está "hecho"
   hasta cumplir su criterio de aceptación en `PLAN.md`.
5. **Registrar:** marca el ítem en el checklist, actualiza *Estado actual*, y **commitea** si el
   modo git lo permite (ver política de commits en `CLAUDE.md`).
6. **Cerrar el turno limpio:** deja `PLAN.md` coherente y el árbol en verde.

## Definición de "hecho" (Definition of Done)

Un ítem está terminado cuando: cumple su **criterio de aceptación**, pasa build/tests/lint, respeta
las convenciones, no deja `TODO` en el código, y su cambio está reflejado en `PLAN.md` (y commiteado
si procede).

## Cómo retomar tras `/compact`

El `/compact` resume el contexto conversacional; el disco no se toca. Lo que **no** vuelve solo:
las reglas con `paths:`, los `CLAUDE.md` anidados y el cuerpo completo de las *skills* (se
reinyectan truncadas). El `CLAUDE.md` de la raíz sí vuelve. Para continuar:

1. Lee `docs/PLAN.md` (checklist + *Estado actual* + *Decisiones*) y `git log`.
2. Repite la **puerta de clarificación**: ¿surgió algo esencial no decidido? Pregunta antes de seguir.
3. Continúa por el primer ítem no marcado. Si algo del último turno quedó a medias, el *Estado
   actual* debe decir exactamente dónde retomar; si no lo dice, es un fallo del turno anterior —
   deduce lo mínimo del `git log`/diff, anótalo y sigue.
4. Si trabajabas en un subdirectorio con memoria propia o con reglas por ruta, **vuelve a leer un
   fichero de esa zona** antes de tocar nada: hasta entonces esas reglas no están cargadas.

**Mejor que retomar es no perder el hilo:** cuando el indicador de contexto se acerque al umbral y
estés **entre dos tareas**, lanza tú `/compact` con foco en lugar de esperar al automático.

## Reglas de oro

- **No trabajes sobre suposiciones esenciales.** Preguntar > adivinar.
- **El estado vive en disco.** Si algo importa para continuar, está en `PLAN.md` o en git, no solo
  en tu respuesta.
- **Pasos pequeños y verificados.** Commits atómicos = puntos de retorno seguros.
- **No amplíes el alcance** por tu cuenta: lo que no esté en `PLAN.md` se propone, no se hace.
- **Registra los aprendizajes transversales como ADR.** Cuando resuelvas una cuestión reutilizable
  más allá de este proyecto (trampa de *toolchain*, patrón de arquitectura, idioma del lenguaje,
  regla de portabilidad), déjala en un **ADR** (`docs/adr/adr-NNNN-titulo-corto.md`). Son la materia
  prima con la que, **al terminar el proyecto**, se enriquece la biblioteca de documentación (ver el
  ciclo en su `README.md`). No edites la biblioteca a mitad de proyecto: primero el ADR.

## Reglas de oro propias de este proyecto

Estas no son estilo, son corrección. Salen del §6 del plan maestro (las diecisiete reglas duras) y
del §4 (fronteras). Romperlas no se arregla con un parche: hay que rehacer datos.

- **Las fronteras las vigila el compilador, no la buena voluntad.** Un módulo solo referencia el
  proyecto `Contracts` de otro. Ningún `Domain` conoce EF Core ni ASP.NET Core. Ninguna consulta
  cruza esquemas y no hay claves foráneas entre ellos. Las escrituras entre módulos van por eventos.
  Los tests de arquitectura son los que lo demuestran: si te estorban, es que estás cruzando una
  frontera.
- **Lo que se decide en el esquema no tiene segunda oportunidad** (R14–R17): las tres fechas
  (devengo, expedición, cobro) como tres columnas; el estado `Bloqueado` con su fecha, distinto de
  activo y de borrado, filtrado **en el repositorio**; las direcciones en campos estructurados.
  Sale gratis hoy y cuesta una migración manual sobre datos sucios mañana.
- **El dinero es `decimal` con divisa** (`numeric(18,4)` importes, `numeric(18,6)` unitarios), nunca
  coma flotante, y el redondeo se aplica **por base imponible y tipo impositivo** (R6).
- **Los libros son *append-only*:** movimientos de stock y apuntes contables no se editan ni se
  borran; se corrigen con otro documento (R2, R3, R4). El borrado lógico es solo para borradores y
  maestros.
- **Multiempresa desde la primera tabla** (R8): `empresa_id` en toda entidad transaccional, filtro
  global de EF Core, y el identificador de empresa sale **del *claim***, jamás del cuerpo de la
  petición.
- **Nada de EF Core InMemory** para probar comportamiento relacional: no traduce SQL real, ignora
  restricciones, columnas computadas y filtros globales, y da falsos verdes. Testcontainers.
- **TDD en el dominio** — en Inventario, Facturación y Contabilidad no es negociable.
- **Antes de adoptar una dependencia, comprueba su licencia.** MediatR, AutoMapper y
  FluentAssertions 8 pasaron a licencia comercial en 2025. Hay equivalentes libres (Shouldly,
  Mapperly, un despachador propio). No se adopta nada "porque siempre fue gratis".
- **La licencia comprobada se escribe donde se lee la decisión, y NUNCA como última línea del
  commit.** NuGet, en el bloque de comentario de `Directory.Packages.props`; npm, en la
  cabecera del módulo o de la regla que adopta el paquete. Una línea final con forma
  `Clave: valor` **es un trailer** para git —`git log --format='%(trailers)'` la devuelve— y esa
  forma tiene que quedar vacía en todos los commits, porque es la que sirve para comprobar de
  un vistazo que no se ha colado ninguno de sesión, de herramienta ni de terceros. La licencia
  va en la prosa del cuerpo, si va. (Los commits `2398889` y `309f594` la llevan como trailer;
  están publicados y no se reescriben — el dato se movió a su sitio en el 0.16.)

- **El ciclo de una mutación empieza con el árbol limpio.** Una mutación se aplica para **verla
  roja**, y se revierte. La orden de revertir no distingue intenciones: `git checkout -- <fichero>`
  devuelve el fichero a HEAD y se lleva por delante **todo** lo no commiteado que hubiera dentro,
  no solo la mutación. Así que:
  1. **Antes de la tanda, el trabajo del ítem está commiteado** (el árbol verde es cuando se puede).
  2. Se revierte con `git revert`, `git stash pop` o `git restore --source=HEAD <fichero>` **sobre
     árbol limpio**.
  3. Si por lo que sea la mutación cae sobre trabajo sin guardar, **la primera orden del ciclo es
     `git stash push`**, y la última `git stash pop`.
  4. `git checkout -- <fichero>` no se usa nunca con trabajo sin guardar dentro del fichero.
  5. **Lo que se restaura copiando se toca con la fecha de ahora** (`os.utime(ruta, None)`). `copy2`
     y `cp -p` conservan el `mtime`, MSBuild no recompila ese proyecto, y la mutación anterior se
     queda dentro del `.dll`. Al revés es peor: una mutación copiada con fecha vieja no se compila,
     sale verde y parece que ningún caso la ve.
  En el ítem 1.4 esto costó ~128 líneas de `ElFiltroNoSeSaltaPorAhiTests.cs` —una apertura declarada
  y dos reglas enteras—, que hubo que rehacer; el reflex de «revertir con git» supone que lo bueno
  ya está a salvo, y durante un ítem largo casi nunca lo está.

- **La rama de un ítem no sobrevive a su avance rápido.** Una rama por unidad de trabajo, verde ahí,
  `--ff-only` a `main`, y **borrada en cuanto main la contiene** —local y remota—. Una rama vieja que
  ya está dentro de `main` no guarda nada (sus commits viven en `main`) y sí engaña: al retomar
  parece trabajo pendiente. `git for-each-ref refs/heads/` no debería devolver más que `main` y, si
  hay ítem en curso, su rama.

- **Una cifra medida se acompaña del comando que la mide.** Un recuento sacado de un registro largo
  puede perder la cola —o la cabeza— sin decirlo: en el 1.4 los avisos de `act(...)` se contaron dos
  veces mal (92 y 91) por leer un log truncado, y la cifra reproducible es **109 en siete ficheros**.
  Si la cifra va a un documento, va con la orden exacta que la produce, para que el siguiente la
  repita en vez de creerla.

- **El número de una mutación se toma del final de la última tabla, sea del tramo que sea.** Las
  mutaciones llevan una sola numeración en todo el PLAN: un ítem, un epílogo o un addendum siguen
  donde acabó la tabla más reciente, no donde acabó la de su propio ítem. El 2.8 empezó en la 52
  porque la tabla del 2.7 acababa en la 51, sin ver la del epílogo, que ya llegaba a la 56. Cinco
  quedaron numeradas dos veces, y el PLAN las cualifica («52 del epílogo», «52 del 2.8»); los
  mensajes de commit no se tocan. El PLAN no está en orden cronológico, así que «la última» no es la
  de más abajo: `grep -n "| # | Mutación" docs/PLAN.md` lista todas las tablas, y el número
  siguiente es uno más que la mayor de sus filas.

- **Un ADR aceptado no se reescribe: se enmienda desde uno nuevo. Y si se reescribe, lo dice su
  cabecera.** Lo pide `principios/git-workflow.md`: la historia de decisiones es inmutable, como la
  de git, y quien lee un ADR tiene que poder fiarse de que dice lo que se decidió aquel día. El ADR
  nuevo cita el punto que enmienda. El viejo lo anota en su *Estado* y, al principio del punto, con
  una nota («> **Enmendado por el ADR-NNNN (fecha).**»). Si el usuario encarga corregir un ADR por
  dentro, se hace, pero su *Estado* lo declara con el commit, y el punto lleva la misma nota. El
  mensaje de ese commit dice qué cambió y por qué. Así pasó con el ADR-0044 §5, corregido en
  `d9dd1e1` porque el cerrojo del recálculo era de todas las empresas. Sin esa línea, el ADR parece
  haber dicho siempre lo que dice ahora.

### El método, aprendido ítem a ítem

Vivía en la memoria local del agente, que no llega a otra máquina ni a una sesión en la nube, y que
nadie revisa. Pasó aquí el 2026-09-29. Cada regla dice dónde se aprendió, y la historia entera está
en el PLAN. Lo que solo vale en esta máquina o en su *shell* se quedó fuera.

**Verificar**

- **Se verifica por el efecto, no por el código de salida.** Un `dotnet test --filter` que no
  encuentra ningún caso sale con 0 (0.1). Antes de escribir «verificado», se dice qué ejerció la
  orden, y si no ejerció nada, se dice eso. Tras empujar se espera al run y se lee su conclusión,
  trabajo a trabajo. Un rojo se arregla antes de seguir, no se anota como riesgo. En un run rojo se
  mira el último paso ejecutado, no el último del YAML.
- **Toda regla nace con la afirmación que la pondría roja.** Una regla que deja de mirar no se pone
  roja: se pone verde. Un selector con una letra cambiada (`Microsoft.EntityFramworkCore`, 0.12),
  una lista vacía o un captador sin cable (1.5) tienen el aspecto de un carril sano. Por eso:
  - la regla afirma que encontró algo;
  - toda prohibición va con su pareja, el sitio donde lo prohibido existe de verdad. Si ese
    contraejemplo sale a cero, la regla no protege nada y se borra (`System.Data`, 0.12);
  - toda cadena tecleada que no se deriva del proyecto es un selector, y necesita su contraejemplo;
  - la tanda de mutaciones lleva una que rompe el arnés sin tocar el sujeto;
  - el enganche del arnés se comparte, no se copia, y su canario va al carril más barato que lo
    ejerza.
- **Comparar entero solo vale si las dos fuentes pueden dar la misma lista** (ADR-0024). Si una
  infradetecta por diseño, la igualdad es falsa antes de escribirla. Hacen falta cinco afirmaciones:
  - la contención en un sentido;
  - la simetría que sí se cumple;
  - las dos listas, no vacías;
  - que cada entrada obligue a que exista lo que la valida;
  - la que no es obvia: nada del universo queda sin clasificar.
- **Un test fija su ambiente, nunca lo hereda.** La cultura, la zona horaria y el fin de línea van al
  valor de producción; si hay que cambiarlos, en un hilo propio. En el 0.15, `[Range(typeof(decimal),
  "0.000001", …)]` lanzaba en es-ES y pasaba en en-US, que es el *runner*, y OpenAPI publicó
  `minimum: 1`. Ante un valor escrito como cadena que convierte un *framework*, se pregunta quién lo
  parsea y con qué cultura.
- **Si la CI da rojo y aquí sale verde, se prueba primero el entorno de la CI, empezando por
  `CI=true`.** En el 0.16, `typescript-eslint` leía `CI` y analizaba el fichero de disco en vez del
  texto inyectado. Una comprobación que inyecta entrada a una herramienta lleva un canario que la
  herramienta está obligada a marcar, con la polaridad que pone rojo el fallo.
- **El guion que decide un paso de la CI se verifica en la plataforma de la CI.** En el 1.12,
  `logs | grep -q` bajo `pipefail` dio 25 rojos de 30 en Linux, por el SIGPIPE, y verde en Git
  Bash. Los registros van a un fichero, y el `grep`, sobre el fichero. Lo que puede fallar por azar
  se repite N veces antes de fiarse.
- **El paso de la CI no es solo su comando.** Antes de cerrar, también se ejecutan aquí el guion que
  decide el desenlace y los generadores en modo `--comprobar`. En el 2.3 el run murió en el catálogo
  de errores con los dos carriles en verde. Si un generador cambia algo, se busca quién consume su
  salida: un `type` nuevo necesita su texto en todos los diccionarios.
- **Un commit que solo toca documentos también pasa el carril rápido.** `docs/PLAN.md` y el
  README son entrada de `ElEstadoDelReadmeEsElDelPlanTests`, y marcar una casilla cambia la línea
  que el README tiene que decir. En el cierre del 2.9, `e6975ed` marcó la suya sin mover el README
  y puso `main` rojo (run 36972547394). La casilla y la línea del README van en el mismo commit.
- **Parado no es ausente, y lo que no necesita la dependencia se ejerce sin ella.** «No se puede en
  local» se comprueba: en el 1.7 Docker solo estaba apagado, y 334 rojos pasaron a 334 verdes.
  Antes de empujar un test que no se puede correr, un canario temporal ejerce lo que no la necesita.
  Y un caso frontera no vive solo detrás de un contenedor: la aritmética baja al carril rápido.
- **Un `AgregarX()` que promete bastarse se prueba construyendo lo que registra** (1.4). En el
  carril rápido:
  - los descriptores que añade (`Skip(antes)`), no vacíos;
  - `BuildServiceProvider(validateScopes: true)`;
  - `GetRequiredService` sobre todos.

  Si dos extensiones pueden registrar lo mismo, `TryAdd`.
- **El SQL que compone el ORM no es el que se escribe** (2.3, ADR-0043). `SqlQueryRaw` y `FromSql`
  lo meten dentro de un `SELECT … FROM (<sql>)`, así que un `;` final da un `42601`. Una fila de
  `SqlQueryRaw<Clase>` se lee con la convención de nombres del contexto; un escalar `AS "Value"`,
  no. Lo que solo corre al arrancar lo ve solo el humo.
- **El humo local va en su propio proyecto de *compose*.** `segundo-arranque.sh` afirma sobre un
  primer arranque (`SinRolesDelSistema`) y muta la base para fabricar el estado viejo. Así que:
  - va sobre una copia del `.env` en el *scratchpad*, con sus puertos añadidos al final;
  - se levanta con `-p bastion-humo-<item>`;
  - el `down -v` lleva ese `-p` en la misma orden;
  - la copia se borra al terminar, porque tiene secretos.

**Mutar y atribuir**

- **La mutación va sobre la línea que decide, y manda lo medido.** En el 2.2, `Math.Min` partió las
  cuatro combinaciones en dos mitades, porque las simétricas acertaban por casualidad. Las dos
  listas van por nombre. Un comentario que contradice la medida se reescribe.
- **Rojo contra rojo no es atribución** (1.5). Con la base roja, se comparan los conjuntos de casos
  rojos nombrados. Cada mutación añade los suyos, ninguno es de la base, y la base va escrita en el
  informe.
- **Un rojo en masa se mide con el caso solo** (2.7). Los 38 rojos de un `WHERE` venían de cortes
  que habían dejado otros casos, y los 418 de un `stored: false`, del `MigrateAsync` del *fixture*.
  El número de la mutación va solo junto al caso que la ve por diseño. Una mutación verde en los dos
  carriles es un hallazgo: se cubre en su commit y se vuelve a medir.
- **Un caso de carrera para a la primera transacción con el cerrojo tomado y nada escrito** (2.8).
  Si la primera ya escribió, la segunda espera por otra razón: un `INSERT … ON CONFLICT` espera en el
  índice único. Así, la mutación que quitaba el cerrojo dio 0 rojos de 432. Se mide con esa
  mutación, sola.
- **La tanda lleva plazo de cuelgue, y un caso no cierra un contexto con algo en vuelo** (addendum
  del 2.8). Sin plazo, la 73 tuvo la tanda parada tres horas y media: un caso de carrera falló a
  medias y el `await using` cerró el contexto de la operación que seguía esperando. Con
  `--blame-hang --blame-hang-timeout 4m`, un cuelgue es un rojo con el nombre del caso. Lo que un
  caso lanza sin esperar, lo espera quien cierra su contexto (`ElModuloDeInventario.DisposeAsync`).

**Escribir tests**

- **Un flake se reproduce antes de arreglarlo.** Un rojo sobre un commit que no pudo causarlo es un
  test no determinista, y hay que demostrarlo. N copias del mismo fichero a la vez dan la contención
  del *runner*. El orden es rojo de control, arreglo, varias rondas en verde y mutar el arreglo.
- **Lo que pone un efecto se espera; lo que ya estaba antes de montar, no.** `document.title` lo
  pone un `useEffect` y necesita `waitFor`. `lang` lo pone el arranque del i18n antes del `render`, y
  envolverlo taparía una regresión. El test dice en cuál de los dos casos está.
- **Las semillas de integración se reparten entre ficheros.** `Api.IntegrationTests` corre en un
  solo contenedor, y una semilla repetida pone rojo al que llega segundo, que puede ser un caso
  viejo. Los números libres se buscan por literal y por cálculo (`320 + (int)tipo`), y el reparto va
  en la cabecera del fichero.
- **Un caso nuevo o renombrado entra en `ElCensoDeEsteCarrilTests` en su commit.** El censo corre en
  el carril rápido aunque cense casos de integración, así que filtrar por la clase del caso no lo
  ejecuta. Antes de commitear se corre `--filter "FullyQualifiedName~ElCensoDeEsteCarrilTests"`.
  `828f7b2` quedó rojo por eso, y lo arregló `fcfa928`.

**Informar y publicar**

- **La comprobación no va en la misma orden que la acción irreversible** (1.10). El barrido va en
  una llamada y se lee; el commit y el push, en la siguiente. Si el PLAN pega el patrón del barrido,
  el barrido se encuentra a sí mismo: esa línea se excluye y se dice.
- **El informe cuenta los commits desde el último verificado, sean del ítem o no**, con sus sujetos
  y sus runs de rama y de `main`. «Se subió el trabajo» no vale.

## Comandos del proyecto (parte variable)

Desde la raíz del repositorio.

| Qué | Comando |
|---|---|
| **Build backend** | `dotnet build Bastion.sln` |
| **Tests backend (todos)** | `dotnet test Bastion.sln` |
| **Solo dominio + arquitectura** (rápido, sin Docker) | `dotnet test Bastion.sln --filter "Category!=Integracion"` |
| **Formato backend** | `dotnet format Bastion.sln --verify-no-changes` (sin `--verify-no-changes` para arreglar) |
| **Migraciones** (por módulo, cada uno con su `DbContext`) | `dotnet ef migrations add <Nombre> --project src/Modules/<Modulo>/Bastion.<Modulo>.Infrastructure --startup-project src/Api --output-dir ../../../../db/migraciones/<Modulo>` |
| **Build frontal** | `npm --prefix frontend run build` |
| **Presupuesto del frontal** (arranque y total, en KiB) | `bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900` |
| **Tests frontal** | `npm --prefix frontend run test` |
| **Lint / formato frontal** | `npm --prefix frontend run lint` · `npm --prefix frontend run format:check` |
| **Tipos frontal** | `npm --prefix frontend run typecheck` |
| **Contrato al día** (el cliente generado) | `npm --prefix frontend run api` y que `git status --porcelain -- frontend/src/shared/api/esquema.ts` salga **vacío** |
| **Migraciones al día** (el modelo no tiene cambios pendientes) | `bash scripts/comprobar-migraciones.sh` |
| **OpenAPI al día** (el contrato versionado) | `bash scripts/generar-openapi.sh --comprobar` |
| **Catálogo de `type` al día** (el que lee el frontal, ADR-0030) | `bash scripts/generar-errores.sh --comprobar` |
| **Arranque local completo** | `docker compose -f deploy/docker-compose.yml up --build` |
| **Parar y limpiar volúmenes** | `docker compose -f deploy/docker-compose.yml down -v` |

> **`docker-compose.yml` vive en `deploy/`** (estructura del §12 del plan maestro), así que **todos**
> los comandos de compose llevan `-f deploy/docker-compose.yml`. Un `docker compose up` a secas desde
> la raíz no encuentra nada.

**Antes de dar por hecho un ítem**, esta es la batería, **en este orden**, que es el de lo barato
primero: lo que falla en segundos tiene que fallar antes de que arranques Docker.

```bash
# 1. Segundos, sin compilar nada. Los tres que más rojos han causado en la CI.
npm --prefix frontend run api && git status --porcelain -- frontend/src/shared/api/esquema.ts
bash scripts/comprobar-migraciones.sh
bash scripts/generar-openapi.sh --comprobar
bash scripts/generar-errores.sh --comprobar

# 2. Frontal. `lint` NO cubre ni los tipos ni el formato: son tres pasos distintos.
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run format:check
npm --prefix frontend run test
npm --prefix frontend run build
bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900

# 3. Backend. El carril rápido no necesita Docker; el de integración sí.
dotnet build Bastion.sln
dotnet format Bastion.sln --verify-no-changes
dotnet test Bastion.sln --filter "Category!=Integracion" \
  --logger trx --results-directory artifacts/test-results/dominio
dotnet test Bastion.sln --filter "Category=Integracion" \
  --logger trx --results-directory artifacts/test-results/integracion

# 4. El recuento, que es QUIEN DECIDE el desenlace de los dos pasos de la CI. `dotnet test`
#    dice «ningún caso falla»; esto dice «han corrido exactamente los ensamblados que tenían
#    que correr». Un cambio que altera QUÉ corre —un rasgo, un filtro, un proyecto de test
#    nuevo— es invisible para el primero por construcción, y puso rojo el run 33830689761 con
#    la batería entera en verde. Las dos listas son las del workflow, literalmente.
bash scripts/ci/recuento-de-tests.sh \
  artifacts/test-results/dominio "Dominio y arquitectura" 300 \
  "Bastion.Api.FunctionalTests.dll,Bastion.Api.IntegrationTests.dll,Bastion.Arquitectura.Tests.dll,Bastion.BuildingBlocks.UnitTests.dll,Bastion.Catalogo.UnitTests.dll,Bastion.Identidad.UnitTests.dll,Bastion.Inventario.UnitTests.dll,Bastion.Organizacion.IntegrationTests.dll,Bastion.Organizacion.UnitTests.dll,Bastion.Terceros.UnitTests.dll"
bash scripts/ci/recuento-de-tests.sh \
  artifacts/test-results/integracion "Integración (Testcontainers)" 100 \
  "Bastion.Api.IntegrationTests.dll,Bastion.Organizacion.IntegrationTests.dll"

# 5. Las dependencias, por CONJUNTOS y contra la base del ítem: qué paquete entra o sale, no
#    cuántos hay. El frontal se cuenta SIN la raíz; la convención está en la cabecera del guion.
python scripts/dependencias-por-conjuntos.py <commit-base> HEAD
```

> **Los avisos de `act()` del frontal son un CANAL desde el 1.8, no una cifra.** Hasta ese ítem
> había un fondo de 109 avisos que venía del desmontaje del armazón, y con ese fondo puesto un aviso
> nuevo no se veía: entraba en el ruido y lo único que se podía hacer con ellos era contarlos. El
> fondo está a **cero** desde `9f8ff56`, así que a partir de ahí **un solo aviso es un hallazgo**.
> Se diagnostica —qué efecto se quedó fuera de un `act()`, o qué aserción se hizo sobre algo que
> todavía no había ocurrido— y se arregla. No se cuenta, no se compara contra un fondo y no se
> tolera «porque son pocos»: el día que se toleren dos, el canal vuelve a ser una cifra.

Y **el humo, con Docker**, cuando el ítem toque despliegue, esquema, imágenes o el *compose*:
`docker compose -f deploy/docker-compose.yml up --build`, y **sobre ese mismo entorno ya en pie**
el segundo arranque, `bash scripts/ci/segundo-arranque.sh`, que recrea el migrador y la API sobre el
mismo volumen con la semilla retirada (ADR-0035). Es el último paso del Humo: el código que solo
corre al arrancar se prueba arrancando. Contra un proyecto de *compose* aparte, las variables
`COMPOSE`, `ENTORNO` y `PYTHON` de su cabecera.

> **Esta lista tiene que seguir siendo la de `.github/workflows/ci.yml`.** Dejó de serlo entre el
> 0.11 y el 0.13 —le faltaban «Contrato», «Migraciones», «OpenAPI», `typecheck`, `format:check` y
> `test`— y eso convierte esta instrucción en el peor sitio posible para una mentira: es la que
> decide cuándo se declara terminado un ítem. **Al tocar el *workflow*, se toca esto.** El
> presupuesto entró aquí en el 1.1, cuando pasó a ser un guion que se puede ejecutar en local: los
> bytes no dependen del sistema de ficheros, así que la cifra de esta máquina y la del *runner* son
> la misma (ADR-0028).
