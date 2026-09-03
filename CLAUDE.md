# CLAUDE.md — Bastion

Eres el agente encargado de desarrollar este proyecto (desde cero o ampliándolo). Trabaja según
el contrato de `@AGENTS.md` y mantén siempre actualizado el plan de registro `@docs/PLAN.md`.

La especificación completa del producto —stack, arquitectura, dominio, las diecisiete reglas duras
y la hoja de ruta— es **`ERP-PLAN-MAESTRO.md`**, que el usuario aporta fuera del repositorio. Es la
fuente de verdad: no la contradigas ni la reinterpretes. `docs/PLAN.md` solo lleva el **estado del
trabajo**.

---

## 1. Memoria — principios y convenciones (parte fija)

Sigue estas convenciones como **fuente de verdad** de estilo y buenas prácticas. Son normativas.

@../BibliotecaDocumentacion/principios/naming-y-estilo.md
@../BibliotecaDocumentacion/principios/solid.md
@../BibliotecaDocumentacion/principios/ddd.md
@../BibliotecaDocumentacion/principios/clean-architecture.md
@../BibliotecaDocumentacion/principios/testing.md
@../BibliotecaDocumentacion/principios/manejo-errores.md
@../BibliotecaDocumentacion/principios/git-workflow.md
@../BibliotecaDocumentacion/principios/desarrollo-con-ia.md
@../BibliotecaDocumentacion/stacks/csharp/convenciones.md
@../BibliotecaDocumentacion/stacks/dotnet/convenciones.md
@../BibliotecaDocumentacion/stacks/typescript/convenciones.md
@../BibliotecaDocumentacion/stacks/react/convenciones.md
@../BibliotecaDocumentacion/bases-de-datos/sql/convenciones.md
@../BibliotecaDocumentacion/herramientas/api-rest.md
@../BibliotecaDocumentacion/ux-ipo/convenciones.md
@../BibliotecaDocumentacion/herramientas/autenticacion.md
@../BibliotecaDocumentacion/herramientas/seguridad.md
@../BibliotecaDocumentacion/herramientas/docker.md
@../BibliotecaDocumentacion/herramientas/observabilidad.md
@../BibliotecaDocumentacion/herramientas/entrega-continua.md
@../BibliotecaDocumentacion/patrones/inyeccion-dependencias.md
@../BibliotecaDocumentacion/patrones/repository-y-dto.md

<!-- Fase 1 · Maestros (Anexo A.2.3). Entran al empezar la fase y se quedan. -->
@../BibliotecaDocumentacion/herramientas/proteccion-datos.md
@../BibliotecaDocumentacion/patrones/soft-delete.md

> Si necesitas el **porqué** de una convención, consulta su `referencia.md` hermano. Para código,
> guíate por los `convenciones.md`.

> **Los imports de cada fase se añaden al empezar esa fase, no antes** (cada import cuesta
> contexto). La tabla de qué añadir y cuándo está en `docs/PLAN.md` → *Imports pendientes*.

---

## 2. Puerta de clarificación al arrancar (parte fija)

**Antes de tocar nada**, y también al retomar tras un `/compact`:

1. Lee el objetivo del proyecto, `@docs/PLAN.md` y las convenciones importadas.
2. Identifica toda decisión **esencial** que esté **sin especificar y admita varias opciones
   viables** (incluye cualquier `<<...>>` sin resolver que sea esencial).
3. Si existe alguna, **pregunta todas juntas en una sola tanda** y **no empieces a trabajar**
   hasta tener respuesta. Ante la duda entre preguntar o suponer en algo esencial → **pregunta**.
4. Registra las respuestas en `docs/PLAN.md` (sección *Decisiones*) para que sobrevivan a `/compact`.

**Excepción (para no paralizarte):** lo **trivial o reversible** con un default obvio no se
pregunta — decídelo, anótalo en `PLAN.md` y sigue.

**No reabras** lo ya decidido en el Anexo A.4 del plan maestro: el stack (§2 y §3), el estilo
arquitectónico (§4), el mapa de módulos (§5), las diecisiete reglas duras (§6), el modelo de
dominio (§7) y el orden de las fases (§15). Si algo de eso resulta inviable al montarlo, **se
pregunta**; no se cambia por iniciativa propia.

---

## 3. Política de commits (parte fija)

- **No hagas commit ni push hasta que el usuario lo autorice para este trabajo.** Al arrancar,
  el usuario indica el **modo**: `solo-preparar` (no toca git), `commit` (commits locales) o
  `commit+push`. Si no lo ha indicado, **pregúntalo** (es una decisión esencial, pilar 2).
- **Firma siempre** los commits. Un commit debe contener **solo las credenciales del usuario y
  ninguna otra**: nada de `Co-Authored-By`, ni de Claude ni de terceros, ni trailers de sesión.
  Ningún commit *unverified*.
- **Comprueba la firma antes del primer commit:** si `git config --global commit.gpgsign` no es
  `true` (falta el secreto `SIGNING_KEY_B64` en el entorno), **no commitees** y avisa al usuario
  brevemente: *"La firma no está activa. Añade el secreto `SIGNING_KEY_B64` en la config del entorno
  (Environments → variables) con `base64 -w0 ~/.ssh/claude_signing` y reinicia la sesión (detalle en
  `plantillas/README.md`)."* Espera a que lo resuelva antes de commitear.
- **Commits progresivos permitidos** en desarrollos largos: pequeños, atómicos y con un único
  propósito (Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).
- **Cada commit deja el árbol coherente:** checklist de `PLAN.md` actualizado y build/tests en
  verde. El historial de git es la memoria del proyecto.
- **Nunca** commitees secretos, claves ni credenciales (usa variables de entorno y `.gitignore`).
- Trabaja en una **rama por unidad de trabajo**; `main` se mantiene estable. No abras PR salvo
  petición explícita del usuario.

---

## 4. Protocolo de resumabilidad (parte fija)

El objetivo es que puedas perder el contexto (`/compact`) y **retomar sin riesgo**. El estado
vive **en disco**, no en el chat:

- **Al arrancar / retomar:** lee `docs/PLAN.md` y el último commit **antes de nada**. El PLAN es
  la única fuente de verdad de "qué falta"; no reconstruyas el estado de memoria.
- **Al completar cada paso:** marca el checklist de `PLAN.md`, actualiza *Estado actual* y
  *Decisiones*, y (si estás autorizado) commitea. Commits pequeños = puntos de retorno.
- **Antes de terminar un turno:** deja `PLAN.md` coherente y el build/tests en verde, para que el
  siguiente turno (tú tras un `/compact`, u otro agente) pueda continuar sin ambigüedad.
- **No dejes marcadores `TODO` sueltos en el código:** el trabajo pendiente vive en `PLAN.md`.
- **Compacta tú, en una frontera de tarea, y con foco** (`/compact céntrate en <lo que importa>`).
  El automático salta cuando le toca y descarta lo más antiguo a ciegas.
- **Lo que deba sobrevivir a un `/compact` va en ESTE fichero** (memoria de la raíz), que se
  reinyecta desde disco. Lo que se pierde y no vuelve hasta releer un fichero que case: las reglas
  con `paths:` en el *frontmatter* y los `CLAUDE.md` anidados en subdirectorios. En una *skill*, lo
  importante arriba: al reinyectarse se trunca conservando el principio.

---

## 5. Trabajo concreto de este proyecto (parte variable)

- **Qué es:** **Bastion**, un ERP completo para pyme española (inventario, compras, ventas,
  facturación con validez fiscal, tesorería y contabilidad), construido por fases como **monolito
  modular** con fronteras que el compilador vigila.
- **Stack:** C# 14 sobre **.NET 10 (LTS)** · ASP.NET Core · **EF Core 10** + Npgsql ·
  **PostgreSQL 17+** (un esquema por módulo, `snake_case`) · **React 19 + TypeScript + Vite** ·
  Docker Compose · GitHub Actions · Serilog + OpenTelemetry · xUnit + Testcontainers + NetArchTest.
- **Objetivo de este encargo:** completar la **fase 1 (Maestros)** — Terceros y Catálogo completos,
  tarifas, importación CSV y búsquedas. La **fase 0 está cerrada** (run 33739991499 sobre `fe7059d`).
  El checklist con sus criterios de aceptación está en `docs/PLAN.md`; no lo amplíes ni lo reordenes
  por tu cuenta. Ojo: la fase 1 **no tiene Anexo A.3** — sus once ítems los acordó la *puerta de
  clarificación de la fase 1*, y su motivo está en `docs/PLAN.md` → *Decisiones tomadas*.
- **Restricciones / no-objetivos:**
  - **No toques `../BibliotecaDocumentacion`.** Es de solo lectura. Los aprendizajes se dejan como
    ADR en `docs/adr/`; la biblioteca se enriquece **al terminar el proyecto**, no a mitad.
  - **No adelantes fases.** Lo que sea de la fase 2 en adelante no se hace ahora, ni "de paso" —
    en particular `CodigoBarras`, que arrastra un import de la fase 2.
  - **Nada de secretos** en ficheros ni en prosa: variables de entorno y `.gitignore`.
- **Identidad, fijada y no negociable** (Anexo A.1): solución `Bastion.sln` · raíz de espacios de
  nombres `Bastion` (`Bastion.Facturacion.Domain`, `Bastion.Inventario.Application`…), sin repetir
  «Bastion» ni «ERP» dentro · base de datos `bastion` (desarrollo `bastion_dev`) · un esquema
  PostgreSQL por módulo · paquete del frontal `bastion-web` · API bajo `/api/v1/{modulo}/{recurso}`.
  Una sola grafía en todas partes: **`Bastion`**, sin tilde.
- **Cómo ejecutar y probar:** ver `AGENTS.md` → *Comandos del proyecto*.
