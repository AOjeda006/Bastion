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
| **Tests frontal** | `npm --prefix frontend run test` |
| **Lint / formato frontal** | `npm --prefix frontend run lint` · `npm --prefix frontend run format:check` |
| **Tipos frontal** | `npm --prefix frontend run typecheck` |
| **Arranque local completo** | `docker compose -f deploy/docker-compose.yml up --build` |
| **Parar y limpiar volúmenes** | `docker compose -f deploy/docker-compose.yml down -v` |

> **`docker-compose.yml` vive en `deploy/`** (estructura del §12 del plan maestro), así que **todos**
> los comandos de compose llevan `-f deploy/docker-compose.yml`. Un `docker compose up` a secas desde
> la raíz no encuentra nada.

**Antes de dar por hecho un ítem**, la batería mínima es: `dotnet build` + `dotnet test` +
`dotnet format --verify-no-changes` + `npm --prefix frontend run build` + `npm --prefix frontend run lint`.
Es exactamente lo que ejecuta la CI (`.github/workflows/ci.yml`); pasarlo en local antes de
commitear evita el ciclo caro de descubrirlo en el *runner*.
