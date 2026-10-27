# PLAN — Bastion · Fase 3 (Compras)

> Plan de registro (**fuente de verdad del estado del trabajo**). Es lo que hace segura la
> resumabilidad: el agente lo lee al arrancar y tras cada `/compact`, y lo actualiza al avanzar.
> Mantenlo siempre coherente con la realidad del repo.
>
> La especificación del producto es **`ERP-PLAN-MAESTRO.md`** (fuera del repositorio, lo aporta el
> usuario). Este fichero **no la duplica**: solo lleva el estado del trabajo. Cuando cambie una
> decisión de producto, se cambia allí y se anota el porqué en un ADR de `docs/adr/`.

## Objetivo

Dejar la **fase 3 (Compras)** terminada: el pedido a proveedor, su recepción, su devolución y la
solicitud, con lo que necesitan antes: la casa en orden, la exportación, los puertos con el tipo de
la tarifa, la bandeja lista para su primer manejador, la zona y la retirada del artículo.

**Criterio de salida de la fase 3 (§15 del plan maestro):** un pedido confirmado no mueve stock; su
recepción parcial sí, y deja las cantidades pendientes correctas; la devolución revierte.

> **La historia de las fases cerradas está en `docs/historia/`**, un fichero por fase, desde el
> ítem 3.1: lo que este PLAN decía de cada una, movido y no reescrito. El índice de *Decisiones
> tomadas* lleva a cada bloque.

## Alcance / No-objetivos

- **Dentro:** los once ítems del checklist de la fase 3, del 3.1 al 3.11, y nada más.
- **Fuera:**
  - **Las fases 4 a 11.** Ventas, Facturación, Tesorería, Contabilidad, Producción, CRM, RRHH e
    Informes tienen ya su carpeta de módulo creada y vacía; **no se empiezan ahora**, ni "de paso"
    ni "para dejarlo encaminado".
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

### Lo decidido en las fases 0, 1 y 2, y dónde está

> **Desde el ítem 3.1 (2026-10-27), la historia de cada fase cerrada vive en su fichero de
> `docs/historia/`**, movida tal cual del PLAN: sus decisiones, su estado, su checklist y sus notas
> cerradas. Lo que se decidió de forma duradera está en los ADR de [`docs/adr/`](adr/), y se cita
> por su número. Este índice lleva a cada bloque de decisiones y a las piezas del cierre de cada
> fase. Una cita de «`docs/PLAN.md` → *Decisiones tomadas* → …» escrita antes del 3.1, en el
> código o en un ADR, se resuelve aquí.

#### Fase 0 (Cimientos) — [`docs/historia/fase-0.md`](historia/fase-0.md)

| Bloque | Sección |
|---|---|
| [Tomadas por el agente de desarrollo — ítem 0.1 (2026-08-25)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-01-2026-08-25) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.2 (2026-08-25)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-02-2026-08-25) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.3 (2026-08-25)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-03-2026-08-25) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.4 (2026-08-26)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-04-2026-08-26) | *Decisiones tomadas* |
| [Convención de esquemas de PostgreSQL — los dieciséis módulos](historia/fase-0.md#convención-de-esquemas-de-postgresql--los-dieciséis-módulos) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.5 (2026-08-26)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-05-2026-08-26) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.6 (2026-08-27)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-06-2026-08-27) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.7 (2026-08-27)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-07-2026-08-27) | *Decisiones tomadas* |
| [La prueba fuerte del 0.7: seis mutaciones, y las dos que hay que contar enteras](historia/fase-0.md#la-prueba-fuerte-del-07-seis-mutaciones-y-las-dos-que-hay-que-contar-enteras) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.8 (2026-08-29)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-08-2026-08-29) | *Decisiones tomadas* |
| [La prueba fuerte del 0.8: seis mutaciones, y la que salió verde contada entera](historia/fase-0.md#la-prueba-fuerte-del-08-seis-mutaciones-y-la-que-salió-verde-contada-entera) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.9 (2026-08-31)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-09-2026-08-31) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.10 (2026-08-31)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-010-2026-08-31) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.11 (2026-08-31)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-011-2026-08-31) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.12 (2026-09-02)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-012-2026-09-02) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.13 (2026-09-02)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-013-2026-09-02) | *Decisiones tomadas* |
| [Auditoría previa a la fase 1 (2026-09-02)](historia/fase-0.md#auditoría-previa-a-la-fase-1-2026-09-02) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.14 (2026-09-02)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-014-2026-09-02) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.15 (2026-09-03)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-015-2026-09-03) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 0.16 (2026-09-03)](historia/fase-0.md#tomadas-por-el-agente-de-desarrollo--ítem-016-2026-09-03) | *Decisiones tomadas* |
| [La fase 0, criterio por criterio, comprobado por algo observable](historia/fase-0.md#la-fase-0-criterio-por-criterio-comprobado-por-algo-observable) | *Estado actual* |
| [El veredicto de las nueve notas abiertas](historia/fase-0.md#el-veredicto-de-las-nueve-notas-abiertas) | *Estado actual* |
| [Traspaso a la fase 1](historia/fase-0.md#traspaso-a-la-fase-1) | *Estado actual* |
| Las secciones enteras: [Objetivo](historia/fase-0.md#objetivo) · [Estado actual](historia/fase-0.md#estado-actual) · [Checklist](historia/fase-0.md#checklist) · [Notas / riesgos](historia/fase-0.md#notas--riesgos) | |

#### Fase 1 (Maestros) — [`docs/historia/fase-1.md`](historia/fase-1.md)

| Bloque | Sección |
|---|---|
| [Tomadas por el agente de desarrollo — puerta de clarificación de la fase 1 (2026-09-03)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--puerta-de-clarificación-de-la-fase-1-2026-09-03) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.2 (2026-09-03)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-12-2026-09-03) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.3 (2026-09-04)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-13-2026-09-04) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.4 (2026-09-04)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-14-2026-09-04) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.5 (2026-09-05)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-15-2026-09-05) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.6 (2026-09-07)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-16-2026-09-07) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.7 (2026-09-07)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-17-2026-09-07) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.8 (2026-09-10)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-18-2026-09-10) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.9 (2026-09-11)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-19-2026-09-11) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.10 (2026-09-13)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-110-2026-09-13) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 1.11 (2026-09-14)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--ítem-111-2026-09-14) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — addenda de la fase 1, ítems 1.12 a 1.14 (2026-09-16)](historia/fase-1.md#tomadas-por-el-agente-de-desarrollo--addenda-de-la-fase-1-ítems-112-a-114-2026-09-16) | *Decisiones tomadas* |
| [La fase 1, criterio por criterio, comprobado por algo observable](historia/fase-1.md#la-fase-1-criterio-por-criterio-comprobado-por-algo-observable) | *Estado actual* |
| [El veredicto de las notas abiertas](historia/fase-1.md#el-veredicto-de-las-notas-abiertas) | *Estado actual* |
| [Traspaso a la fase 2](historia/fase-1.md#traspaso-a-la-fase-2) | *Estado actual* |
| Las secciones enteras: [Estado actual](historia/fase-1.md#estado-actual) · [Checklist](historia/fase-1.md#checklist) · [Notas / riesgos](historia/fase-1.md#notas--riesgos) | |

#### Fase 2 (Inventario) — [`docs/historia/fase-2.md`](historia/fase-2.md)

| Bloque | Sección |
|---|---|
| [Tomadas en la puerta de clarificación de la fase 2 (2026-09-18)](historia/fase-2.md#tomadas-en-la-puerta-de-clarificación-de-la-fase-2-2026-09-18) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.3, antes de escribir su código (2026-09-18)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-23-antes-de-escribir-su-código-2026-09-18) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.4, antes de escribir su código (2026-09-19)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-24-antes-de-escribir-su-código-2026-09-19) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.5, antes de escribir su código (2026-09-23)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-25-antes-de-escribir-su-código-2026-09-23) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.6, antes de escribir su código (2026-09-23)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-26-antes-de-escribir-su-código-2026-09-23) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-09-26 — entre el cierre del 2.6 y la apertura del 2.7](historia/fase-2.md#traídas-por-el-encargo-del-2026-09-26--entre-el-cierre-del-26-y-la-apertura-del-27) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.7, al escribir su código (2026-09-27)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-27-al-escribir-su-código-2026-09-27) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-09-28 — entre el cierre del 2.7 y la apertura del 2.8](historia/fase-2.md#traídas-por-el-encargo-del-2026-09-28--entre-el-cierre-del-27-y-la-apertura-del-28) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.8, antes de escribir su código (2026-09-28)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-28-antes-de-escribir-su-código-2026-09-28) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.8, al escribir su código (2026-09-28)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-28-al-escribir-su-código-2026-09-28) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-09-29 — entre el cierre del 2.8 y la apertura del 2.9](historia/fase-2.md#traídas-por-el-encargo-del-2026-09-29--entre-el-cierre-del-28-y-la-apertura-del-29) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-02 — el epílogo del 2.9 y el 2.10](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-02--el-epílogo-del-29-y-el-210) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-03 — el epílogo del 2.10 y el 2.11](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-03--el-epílogo-del-210-y-el-211) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-05 — el epílogo del 2.11 y el 2.12](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-05--el-epílogo-del-211-y-el-212) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-06 — la puerta del 2.12, contestada](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-06--la-puerta-del-212-contestada) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.12, al escribir su código (2026-10-07)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-212-al-escribir-su-código-2026-10-07) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-08 — el epílogo del 2.12, la subida de vitest y la puerta del 2.13](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-08--el-epílogo-del-212-la-subida-de-vitest-y-la-puerta-del-213) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — la subida de vitest (2026-10-09)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--la-subida-de-vitest-2026-10-09) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-09 — la puerta del 2.13, contestada](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-09--la-puerta-del-213-contestada) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.13, al escribir su código (2026-10-09)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-213-al-escribir-su-código-2026-10-09) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-13 — la propiedad por escritura, y la puerta del 2.14](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-13--la-propiedad-por-escritura-y-la-puerta-del-214) | *Decisiones tomadas* |
| [La puerta del 2.14 — lo que se pregunta, y lo que el ADR-0063 decide sin preguntar](historia/fase-2.md#la-puerta-del-214--lo-que-se-pregunta-y-lo-que-el-adr-0063-decide-sin-preguntar) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-17 — las siete respuestas del repaso, y los addenda](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-17--las-siete-respuestas-del-repaso-y-los-addenda) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.16, antes de escribir su código (2026-10-18)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-216-antes-de-escribir-su-código-2026-10-18) | *Decisiones tomadas* |
| [La puerta del 2.17 — lo que se pregunta, y lo que el ADR-0065 decidirá sin preguntar](historia/fase-2.md#la-puerta-del-217--lo-que-se-pregunta-y-lo-que-el-adr-0065-decidirá-sin-preguntar) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-18 — la puerta del 2.17, contestada](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-18--la-puerta-del-217-contestada) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — la regla del reloj, el paso C (2026-10-18)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--la-regla-del-reloj-el-paso-c-2026-10-18) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.17, al escribirlo y al medirlo (2026-10-19)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-217-al-escribirlo-y-al-medirlo-2026-10-19) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.18, al escribirlo y al medirlo (2026-10-20)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-218-al-escribirlo-y-al-medirlo-2026-10-20) | *Decisiones tomadas* |
| [La puerta del 2.19 — lo que se pregunta, y lo que el ADR-0068 decidirá sin preguntar](historia/fase-2.md#la-puerta-del-219--lo-que-se-pregunta-y-lo-que-el-adr-0068-decidirá-sin-preguntar) | *Decisiones tomadas* |
| [Traídas por el encargo del 2026-10-20 — la puerta del 2.19, contestada](historia/fase-2.md#traídas-por-el-encargo-del-2026-10-20--la-puerta-del-219-contestada) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.19, al escribirlo y al medirlo (2026-10-24)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-219-al-escribirlo-y-al-medirlo-2026-10-24) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 2.20, al escribirlo y al medirlo (2026-10-24)](historia/fase-2.md#tomadas-por-el-agente-de-desarrollo--ítem-220-al-escribirlo-y-al-medirlo-2026-10-24) | *Decisiones tomadas* |
| [La fase 2, criterio por criterio: el repaso, preparado y sin cerrar (2026-10-14)](historia/fase-2.md#la-fase-2-criterio-por-criterio-el-repaso-preparado-y-sin-cerrar-2026-10-14) | *Estado actual* |
| [La fase 2, cerrada: del 2.15 al 2.20, criterio por criterio (2026-10-24)](historia/fase-2.md#la-fase-2-cerrada-del-215-al-220-criterio-por-criterio-2026-10-24) | *Estado actual* |
| [El veredicto de las notas abiertas, al cerrar la fase 2](historia/fase-2.md#el-veredicto-de-las-notas-abiertas-al-cerrar-la-fase-2) | *Estado actual* |
| [Traspaso a la fase 3](historia/fase-2.md#traspaso-a-la-fase-3) | *Estado actual* |
| Las secciones enteras: [Estado actual](historia/fase-2.md#estado-actual) · [Checklist](historia/fase-2.md#checklist) · [Notas / riesgos](historia/fase-2.md#notas--riesgos) | |

### La puerta de la fase 3 — lo que dice el plan maestro, y lo que se pregunta (2026-10-24)

Escrita el 2026-10-24, en la rama `cierre-de-la-fase-2`, con la fase 2 ya cerrada. **Se para
aquí.** La fase 3 no tiene Anexo A.3, como la 1 y la 2, así que su desglose sale de esta puerta, y
el código empieza con las respuestas. No se reabre nada del Anexo A.4.

#### Lo que dice el plan maestro

`sha256sum ERP-PLAN-MAESTRO.md` sigue dando `86aa53a0…`, la huella de
`docs/dominio/reglas-duras.md`.

- **El §15, la fila de la fase**:

  > **3 · Compras** · Solicitud, pedido, recepción (total y parcial), devolución · **L** · Un
  > pedido confirmado no mueve stock; su recepción parcial sí, y deja las cantidades pendientes
  > correctas; la devolución revierte.

- **El §7.5, el modelo**, en el esquema `compras`:
  - **la solicitud**: quien la pide, el motivo, las líneas y su estado, `Borrador`, `Aprobada` o
    `Rechazada`, con «aprobación por importe con umbrales»;
  - **el pedido**: proveedor, serie y número, las fechas de pedido y de entrega prevista, divisa,
    condiciones, y su estado, `Borrador`, `Confirmado`, `RecibidoParcial`, `Recibido`,
    `Facturado`, `Cerrado` o `Anulado`. Su línea lleva artículo, descripción congelada, cantidad,
    cantidad recibida, cantidad facturada, precio, descuento, impuesto, y el importe como columna
    computada;
  - **la recepción**, el albarán de entrada: pedido origen, almacén, fecha, y líneas con la
    cantidad recibida y el lote o la serie. «Al confirmar genera movimientos de entrada y
    actualiza la valoración»;
  - **la devolución**: recepción origen, líneas, y «genera movimientos de salida»;
  - y su regla propia: «confirmar un pedido no mueve stock, solo compromete (stock "pedido a
    proveedor" para el MRP). El stock entra con la recepción».
- **El §8.1, el ciclo**: la solicitud se aprueba y da un pedido; el pedido se confirma, que es un
  compromiso sin stock; se recepciona, y la recepción, al confirmarse, es un movimiento de entrada
  con la valoración por PMP. El otro brazo, facturar y el cotejo a tres bandas, es de la fase 5.
- **Lo que la fase cruza por primera vez**:
  - la **R7**: el precio, el descuento, el tipo impositivo y la descripción se congelan en la
    línea al confirmar, y el importe es una columna computada;
  - la **R12** y la **regla 5 del §4**: una transacción modifica un solo agregado, y entre módulos
    se escribe «solo por eventos», con consumidores que registran el evento procesado;
  - la **R14**: la fecha de la recepción es la primera fecha de entrega del proyecto, y será el
    devengo de la factura recibida de la fase 5;
  - el **§11**: «quien crea un pedido de compra no debería poder aprobarlo por encima de su
    umbral», con permisos distintos y no con un rol que todo lo puede.
- **Lo que cita**: el §4 remite a `fundamentos/datos-distribuidos/convenciones.md`, que pide
  «idempotencia + patrón Outbox + sagas» antes que una transacción distribuida. No es import de
  ninguna fase, y se lee a mano antes del primer manejador. Y el §18 dice que las fases 0 a 4 no
  necesitan ninguna norma: son reglas de negocio.
- **El Anexo A.2.3 no tiene fila para la fase 3**: no hay import que añadir.

#### Lo que ya está escrito

- **Compras es un esqueleto**: `git ls-files src/Modules/Compras` da cinco `.gitkeep`, uno por
  proyecto, sin contexto, sin esquema y sin permisos.
- **Ningún módulo maneja todavía un evento de otro** (invariante 12 del traspaso). El ADR-0013 §6
  dejó escrito para ese día que «cerrarlo del todo exige que el manejador escriba su efecto y su
  huella en el **mismo** `SaveChanges`», y `EventoProcesado` ya está mapeado en los contextos de
  módulo para eso.
- **Inventario escribe en el libro por un solo camino**, `AnotarEnElLibroAsync`, y el libro conoce
  dos documentos de origen: `TipoDeDocumentoOrigen` tiene `Ajuste` y `Transferencia`. Sus importes
  van en la divisa base de la empresa, como dice `Ajuste.Divisa`.
- **Las lecturas que Compras necesitará**, y la que no existe:
  - `IConsultaDeTerceros.EstadoDeAsync` con `RolDeTercero.Proveedor` dice si un proveedor existe,
    hace ese rol y se puede tratar (R16);
  - `IConsultaDeExistencias.DisponibleDeAsync` da el físico, lo reservado y lo disponible por
    almacén y artículo;
  - `IConsultaDeConversiones` da el factor de una línea en cajas, como en el ajuste;
  - **ningún puerto resuelve un precio.** `IConsultaDeTarifas` solo publica `EstadoDeAsync`, y
    `PrecioResueltoDto`, que está en `Catalogo.Contracts`, solo lo devuelve el caso de uso HTTP
    de Catálogo. Lleva la divisa como `Guid`, `DivisaId`, y la empresa la tiene en ISO 4217, en
    `EmpresaDto.DivisaBase`: es el invariante 8;
  - **una tarifa no es de compra ni de venta**: `Tarifa` no tiene tipo, y un tercero, sea cliente
    o proveedor, lleva una sola, `TarifaAsignadaId`. `ArticuloProveedor` solo guarda
    `ReferenciaDelProveedor`, sin precio.
- **La rejilla de líneas y el buscador del artículo se comparten** desde el 2.19, en
  `features/inventario/lineas`. El ADR-0068 ya dice que el pedido de compra «la encuentra hecha».

#### Las cifras de partida, con la orden que las mide

- **426/450 KiB de arranque y 828/900 de total**, con
  `bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900` sobre una construcción del
  frontal de `08ea605`. El run **40104536177** dice lo mismo. Quedan **24 y 72 KiB**.
- **115 permisos, 158 operaciones en 95 rutas, y 240 `type` de 248 sitios**, del mismo run.
- **2 284 casos de .NET**, 1 544 en el carril rápido y 740 contra PostgreSQL; **392** de jsdom en
  47 ficheros, y **22** en un Chromium.

#### Lo que se pregunta, todo junto

Cada pregunta dice lo que hay hoy, las salidas y la que el agente recomienda, con su motivo. Las
respuestas van a *Decisiones tomadas*, y el desglose del *Checklist* sale de ellas.

1. **El desglose de la fase, y su orden.** La propuesta, con el criterio de cada ítem:
   - **3.1 · La exportación de los listados**: los trece de su nota, con los mismos filtros que
     cada uno ya manda al servidor (pregunta 2);
   - **3.2 · Los puertos de la fase**: el precio que Catálogo resuelve para otro módulo
     (pregunta 4), la divisa de la tarifa en ISO, que desarma el invariante 8, y Compras con
     persistencia: su esquema, su contexto, sus migraciones y su inscripción en
     `IDocumentosDeUnPeriodo`. Los tests de arquitectura lo cuentan como módulo con persistencia;
   - **3.3 · La retirada del artículo** (ADR-0023), decidida (a) el 2026-10-17:
     `IConsultaDeArticulos` produce `SoloResuelveLoViejo`, una línea nueva no elige un artículo
     retirado, y el retirado sigue contestando su unidad base, para que un recuento pueda sacar
     sus existencias;
   - **3.4 · El pedido de compra**: el alta en borrador, sus líneas (pregunta 6), confirmar, que
     numera en su serie, congela la R7 y no escribe en el libro, cerrar con lo pendiente, anular y
     descartar, con su listado, su ficha y sus pantallas. El criterio, el primero del §15: un
     pedido confirmado no tiene ni una fila en el libro;
   - **3.5 · La recepción**: total o parcial contra un pedido confirmado, con su lote o su serie y
     su ubicación, y la entrada valorada con el coste de su línea (preguntas 3 y 9). El criterio,
     el segundo: dos recepciones parciales dejan lo pendiente de cada línea bien, y el saldo sigue
     siendo la suma del libro;
   - **3.6 · La devolución de compra**, desde una recepción (pregunta 7). El criterio, el tercero:
     recibir y devolver lo mismo deja el libro en cero por artículo, almacén, ubicación y lote, en
     cantidad y en valor;
   - **3.7 · La solicitud de compra**, con sus umbrales (pregunta 8), y de una aprobada sale un
     pedido en borrador;
   - **3.8 · La zona de cada almacén**, si va en esta fase (pregunta 11);
   - **3.9 · El repaso de la fase**, criterio por criterio, y su cierre.

   **Recomendada: este desglose, en este orden.** La exportación va primero porque así lo
   contestó la respuesta 4 de la puerta del 2.17. La retirada va antes del pedido porque su
   disparador es la primera línea de compra. Y el pedido va antes que la solicitud, aunque el
   §8.1 empiece por ella: el criterio del §15 es del pedido, la recepción y la devolución, y una
   solicitud aprobada no tiene adónde ir hasta que haya pedidos. Las salidas: (a) esta; (b) la
   solicitud primero, en el orden del flujo; (c) la del usuario.

2. **La exportación.** El §10 pide que «cada listado exporta a CSV/Excel lo que está viendo, con
   los mismos filtros aplicados», y la decisión 7 del 1.11 ya escribió la regla del apóstrofo para
   los campos que empiecen por `=`, `+`, `-`, `@`, tabulador o retorno.
   - **(a) Recomendada: CSV, en el dialecto de la importación** (ADR-0034 §5): `;` y UTF-8, aquí
     con BOM, para que lo abra bien un Excel en español. Lo genera el servidor, con los filtros y
     el orden del listado y **todas sus páginas**, no solo la que se ve, y con el mismo permiso que
     el listado: quien lo ve ya puede recorrerlo entero página a página, así que otro permiso no
     protegería nada. Lo bloqueado queda fuera, como en el listado (R16).
   - (b) CSV y también `.xlsx`. Necesita una biblioteca con su licencia comprobada, y el CSV ya
     abre en Excel.
   - (c) CSV hecho en el navegador con lo que ya ha bajado. Exportaría la página, no el listado.

3. **La escritura de Compras en Inventario.** La regla 5 del §4 la manda por eventos, así que la
   recepción no escribe en el libro: publica, e Inventario anota. Lo que hay que decidir es qué
   pasa cuando Inventario no puede. La entrada puede caer en un ejercicio que se cerró entre medias
   o en una ubicación bloqueada, y la salida de la devolución, además, puede no caber
   (invariante 3).
   - **(a) Recomendada: el evento y su respuesta, una saga de dos pasos.** Compras confirma y
     publica, y su documento queda en un estado que dice que el stock está en camino. Inventario
     anota en su manejador, con **el efecto y la huella en el mismo `SaveChanges`**, que cierra el
     hueco del ADR-0013 §6, y contesta con otro evento: anotado, o rechazado con su motivo. Compras
     lo recibe y pasa su documento al estado final, o lo devuelve con el motivo. Lo pendiente del
     pedido se mueve con la respuesta, no antes. Y antes de confirmar, Compras pregunta por los
     puertos de lectura lo que pueda, el ejercicio, la ubicación y lo disponible, para que el
     rechazo sea la carrera y no el camino de siempre.
   - (b) El evento sin respuesta. Si Inventario no puede, el evento se aparca a la quinta
     (ADR-0013 §7), y Compras sigue diciendo que el stock entró. Es más corto, y deja un documento
     que afirma algo falso sin saberlo.
   - (c) La huella en su propia transacción, como hoy, con (a) o con (b). Deja el hueco: un
     manejador que se cae entre su efecto y su huella anota dos veces, y en un libro que no se
     puede limpiar (invariante 1) eso no tiene arreglo.

4. **El precio de la línea (R7), y su divisa.** Hoy no hay de dónde sacar un precio de compra.
   - **(a) Recomendada: el precio se escribe en la línea, y la tarifa del proveedor lo propone si
     tiene una.** Un puerto nuevo en `Catalogo.Contracts` resuelve el precio y el descuento de una
     tarifa para un artículo, una cantidad y una fecha, que es lo que ya hace el caso de uso HTTP.
     Confirmar congela lo que la línea tenga, venga de la tarifa o de la mano, con la descripción
     y el tipo impositivo del artículo a la fecha del pedido.
   - (b) El precio, solo a mano. Sin puerto nuevo, y sin ayuda.
   - (c) Un precio de compra por artículo y proveedor, en `ArticuloProveedor`. Cambia un maestro de
     la fase 1 con datos dentro.

   **Y la divisa:**
   - **(i) Recomendada: el pedido, en la divisa base de la empresa, y solo en ella en esta
     fase.** La tarifa pasa a ISO en el 3.2, y una tarifa en otra divisa no propone precio. El
     libro valora en divisa base, así que un pedido en dólares necesitaría el cambio del día de la
     recepción, y esa regla es de la fase 5, con la factura.
   - (ii) Pedidos en cualquier divisa, valorados en la recepción al tipo de cambio de su fecha, con
     el `TipoCambio` de Organización.

5. **Los impuestos del pedido.** La R7 congela el tipo, y la R6 redondea por base imponible y
   tipo, con unos casos dorados que son de la fase 5.
   - **(a) Recomendada: el pedido congela el tipo en cada línea y suma la base, sin cuota.** La
     cuota, con su regla de redondeo, se escribe una sola vez, en la fase 5, y el pedido la
     enseñará entonces.
   - (b) El pedido calcula ya la cuota por tipo, con la regla de la R6 y sus casos dorados
     adelantados a esta fase.

6. **Las líneas de un borrador de pedido.** El ADR-0065 §9 puso aquí su disparador: «el primer
   documento de la fase 3 con líneas editables, el pedido de compra».
   - **(a) Recomendada: el borrador del pedido se edita**: añadir, cambiar y quitar líneas, cada
     una con su `If-Match`, como el recuento, y confirmar pasa a pedir la versión (ADR-0057). El
     ajuste y la transferencia siguen descartándose, hasta que alguien pida corregirlos. Un pedido
     se escribe en días, con el proveedor al teléfono, y descartarlo tira todas sus líneas; un
     ajuste se escribe de una vez.
   - (b) Como el ajuste: un borrador equivocado se descarta y se escribe otro.
   - (c) Editables en los tres documentos, en el mismo ítem.

7. **La devolución: qué se lleva, y qué le hace al pedido.**
   - **Qué valor.** **(a) Recomendada: el coste de su recepción, acotado como la transferencia**
     (ADR-0054): se lleva el valor que entró con esa línea, como mucho el que queda, y todo el que
     queda si vacía la clave. El lote y la serie salen de la línea de la recepción, y la ubicación
     la elige quien devuelve, con la de la recepción por omisión, porque la mercancía ha podido
     moverse. (b) El PMP vigente, como cualquier salida: «la devolución revierte» se leería solo en
     cantidades, porque una devolución al día siguiente de una entrada cara no devolvería su valor.
   - **Qué le hace al pedido.** **(i) Recomendada: revierte también lo recibido**: lo devuelto
     vuelve a estar pendiente, y un pedido `Recibido` vuelve a `RecibidoParcial`. Si no se quiere
     reposición, el pedido se cierra con lo pendiente. (ii) Solo el stock: el pedido no se entera.
   - Una devolución puede ser parcial, y una línea no devuelve más de lo que su recepción recibió
     menos lo ya devuelto.

8. **La solicitud y los umbrales** (el §7.5 y el §11).
   - **(a) Recomendada: un umbral por empresa, que rige la aprobación de la solicitud y la
     confirmación del pedido.** Por debajo basta el permiso de la acción, y por encima hace falta
     otro, `…-sin-limite`. Quien creó el documento no lo aprueba ni lo confirma por encima del
     umbral, ni con los dos permisos. La solicitud es opcional: un pedido se abre con ella o sin
     ella. Su importe es el de sus líneas a precio estimado, en divisa base.
   - (b) Un umbral por usuario, en su membresía. Cada aprobador con su cifra, pero Identidad
     guardaría un dato de Compras.
   - (c) La solicitud, obligatoria para todo pedido.

9. **La recepción: sin pedido, de más, y sus fechas.**
   - **(a) Recomendada: toda recepción sale de un pedido confirmado, y no recibe más de lo
     pendiente**: una línea de más es un `422` con su línea. La recepción lleva su fecha, la de la
     entrada en el almacén, que es la del movimiento y será el devengo de la factura (R14). Y, sin
     ser obligatorios, el número y la fecha del albarán del proveedor, que es lo que mirará el
     cotejo a tres bandas de la fase 5. Ponerlos ahora son dos columnas; ponerlos en la fase 5, una
     migración sobre recepciones ya escritas.
   - (b) Con una tolerancia de exceso por empresa, en porcentaje.
   - (c) También sin pedido: una entrada sin compromiso, que el criterio del §15 no pide.

10. **La cifra «pedido a proveedor»**, la que el §7.5 deja para el MRP.
    - **(a) Recomendada: lo pendiente, en el pedido y en cada línea**, que es lo que pide el
      criterio, y ninguna cifra agregada por artículo hasta que alguien la lea: el MRP de la fase 8,
      o la ficha del artículo. Su disparador se escribe.
    - (b) Un listado en Compras de lo pendiente de recibir, por artículo y almacén.
    - (c) Una columna «pedido» en el saldo de Inventario, alimentada por eventos.

11. **La zona de cada almacén**, decidida (a) con (ii) y `Europe/Madrid` para lo que no tiene
    almacén, antes de la fase 5. ¿En qué fase va su ítem?
    - **(a) Recomendada: el último de la fase 3.** La recepción es el primer documento cuya fecha
      será un devengo, y con el día UTC, una entrada a las 00:30 del 1 de enero en Madrid propone
      el 31 de diciembre, que es otro trimestre. La fase 4 empieza con el albarán, que es lo mismo
      del lado de la venta.
    - (b) El primero de la fase 4.
    - (c) El último de la fase 4, justo antes de la 5.

    Si la zona es una por instalación o una por empresa lo pregunta la puerta de ese ítem.

12. **Lo que traen las notas y la fase 1, y que ningún ítem pide.** Cada una, (a) un commit o un
    ítem en la fase 3, o (b) se queda con su disparador:
    - **el `?q=` sin filtro** (ADR-0069): un `400` cuando el repositorio no declara filtro, y
      los cinco listados dejan de publicar `q`. **Recomendada: (a), dentro del 3.1**, que toca los
      mismos listados y el mismo paginador;
    - **la auditoría de las herramientas** (ADR-0045): `npm audit fix` a secas, que sube seis
      paquetes de desarrollo sin versión mayor, entre ellos `vitest` y
      `@vitest/browser-playwright` a 4.1.12. **Recomendada: (a), el primer commit de la fase**,
      porque dos de los avisos tocan el carril del navegador;
    - **la correlación buscar/alta** (decisión 1 del 1.6): separar los permisos, que la búsqueda
      enseñe lo bloqueado, o que el alta deje de distinguir. **Recomendada: (b)**, aceptada como
      riesgo conocido: las tres salidas cuestan más que un canal que exige tener los dos permisos;
    - **la destrucción del art. 32 al vencer**, con la anonimización que pide la decisión 12 de la
      puerta de la fase 2. **Recomendada: su ítem en la fase 5**, porque lo que no se puede
      destruir lo fija la conservación de las facturas;
    - **y los restos de la fase 1**: la importación de artículos, la carrera de unicidad del alta,
      que da un `500` en vez de un `409`, y el `413` de nginx que la importación no sabe leer.
      **Recomendada: (b).**

13. **El presupuesto del frontal.** Las pantallas del ajuste, en el 2.17, sumaron **56 KiB** al
    total, cuando se estimaron entre 25 y 40, y sus tres páginas pesan 30 KB. Repartidos por
    fichero, de `b1db83e` a `2fdd3cb`, con un build de `vite` de cada uno en el *scratchpad* y
    `python -X utf8 kib-por-fichero.py <dist-de-antes> <dist-de-ahora>`, el guion del 2.19, que
    compara los ficheros servidos por su nombre sin el hash y no cuenta los mapas:

    | Qué | Bytes | Ficheros, sin el hash |
    |---|---|---|
    | Las tres páginas | +30 205 | `PaginaDelAlta` 12 969, `PaginaDelAjuste` 11 957, `PaginaDeAjustes` 5 279, las tres nuevas |
    | Los módulos nuevos del ajuste | +7 021 | `ajustes` 3 219, `formularios` 3 130, `saldo` 579, `iso` 93 |
    | Los tres `nombres`, juntos | +4 076 | de 5 768 a 9 844: el del ajuste, nuevo, y los maestros de Inventario, que pasaron de los recuentos a compartirse (ADR-0065 §8) |
    | `zod`, que es `react-hook-form` con su resolutor y no zod | +7 753 | de 34 264 a 42 017 |
    | Los diccionarios | +7 377 | `es` 3 732, `en` 3 645 |
    | El índice y la hoja de estilos | +3 427 | `index.js` 1 921, `index.css` 1 506 |
    | El resto | −2 497 | `cantidad` −2 183, `PaginaDeSaldos` −392, y +78 en seis más |
    | **Total** | **+57 362** | de 738 894 a 796 256 B, de 721,6 a 777,6 KiB |

    **El arranque** creció 7 159 B en el 2.17, y 5 952 del 2.18 al 2.20, contados con
    `python -X utf8 kib-por-ruta.py <dist>`, otro guion del *scratchpad*, que suma `index.js`,
    `index.css` y `es.js`: de 421 992 a 429 151 y a 435 103 B. Y **la ruta diferida más cara**,
    lo que cuesta su primera visita con el arranque pagado, es `PaginaDelRecuento`, con
    **160,8 KiB**, por delante de `PaginaDeLaTransferencia`, con 160,7.

    **Lo que eso dice de la fase 3.** El 2.17 costó 56 KiB y el 2.19, 46, con la rejilla ya
    compartida. La fase 3 trae cuatro documentos con pantalla, el pedido, la recepción, la
    devolución y la solicitud: a 46 KiB cada uno, son unos 184, y quedan 72. Y el arranque, a
    unos 6 o 7 KiB por documento, llega a la raya: de 24 a 28, con 24 de margen.

    - (a) **Mantener 450 y 900, y bajar lo que cuesta cada pantalla.** Para que quepan los cuatro,
      cada documento tendría que costar unos 18 KiB, un tercio de lo que costó el ajuste.
    - **(b) Recomendada: el arranque en 450, como está, un tope por ruta diferida, y el total solo
      a título informativo.** El tope que se propone es **192 KiB**, que deja un 19 % sobre la ruta
      más cara de hoy. El arranque es lo que paga todo el que entra, y una ruta, lo que paga quien
      la abre; el total crece con cada pantalla por construcción, y mide el tamaño de la
      aplicación, no la espera de nadie. Para que el arranque deje de crecer con cada documento,
      los textos de cada módulo viajarían con sus rutas, y no en el diccionario del arranque. Es un
      cambio del guion del presupuesto, de la CI y del ADR-0028, que se enmienda, en su propio
      commit al principio de la fase.
    - (c) Subir el total, por ejemplo a 1 200 KiB, y seguir midiendo igual.

    **El límite no se sube sin que decida el usuario**, y por eso es una pregunta.

14. **Los cambios de `CLAUDE.md`**, que se aplican al contestar, al principio de una sesión. El §5
    pasaría a decir:
    - **Objetivo de este encargo:** completar la **fase 3 (Compras)**. La **fase 2 está cerrada con
      veinte ítems** (run 40104536177 sobre `08ea605`; el cierre, `8b296ad`). El desglose en ítems
      y sus criterios de aceptación los acordó la *puerta de clarificación de la fase 3*, y su
      motivo está en `docs/PLAN.md` → *Decisiones tomadas*.
    - **No adelantes fases.** Lo que sea de la fase 4 en adelante no se hace ahora, ni «de paso»:
      en particular, el estado `Facturado` del pedido y la cantidad facturada de su línea, que son
      de la fase 5, y el MRP, que es de la 8.
    - **Imports: ninguno**, porque el A.2.3 no tiene fila para la fase 3. *Imports pendientes*
      apunta que antes del primer manejador se lee a mano
      `fundamentos/datos-distribuidos/convenciones.md`.

    **Recomendada: así.** Hoy el §5 dice que el objetivo es la fase 2, y que no se adelanta nada
    «de la fase 2 en adelante», con `CodigoBarras` de ejemplo, que entró en el 2.10.

#### Lo que la puerta decidirá sin preguntar

Con las respuestas recomendadas. Si alguna cambia, cambia lo que cuelga de ella.

1. **Las rutas**, bajo `/api/v1/compras/`, con la forma del ajuste (ADR-0065 §6): el alta con la
   `Idempotency-Key` obligatoria, el listado, la ficha con su `ETag`, y el descarte con `If-Match`.
2. **Los permisos**, `compras.<documento>.<acción>`, uno por acción, como en Inventario.
3. **La numeración**: un tipo de serie por documento, en el ejercicio de su fecha y bajo el cerrojo
   del 2.4 (invariante 4).
4. **El cierre del ejercicio**: Compras se inscribe en `IDocumentosDeUnPeriodo` con su primer
   documento (invariante 6).
5. **La doble flecha** (R13): `TipoDeDocumentoOrigen` gana la recepción y la devolución, con el
   identificador del documento de Compras, y la vuelta es un puerto de lectura en
   `Inventario.Contracts`.
6. **Un proveedor que no se puede tratar** (R16) no recibe pedidos, recepciones ni devoluciones
   nuevas, con el rechazo que ya dan los demás documentos.
7. **La rejilla y el buscador** se comparten con Compras, no se copian, y se mueven en su propio
   commit, como en el 2.19.
8. **Cada ítem**, con su tanda escrita antes de medir y una mutación sobre el arnés, y con el humo
   si trae migración o pantalla.

#### Lo que sigue pendiente, aparte de la puerta

- **`/code-review`** no se hizo sobre `0638419..0d26e76`: el traspaso la anota como no hecha.

La siguiente mutación es la **745**, y el siguiente ADR, el **0071**.

### Traídas por el encargo del 2026-10-26 — la puerta de la fase 3, contestada

El usuario lo trae tras verificar el tramo desde fuera:

- **27** commits desde `ffd8015`, con 27 firmas válidas y sin *trailers*;
- un run por commit, con los dos rojos, el **296** y el **304**, arreglados en el commit
  siguiente;
- `main` avanzó a `ec023f5` solo después del verde de su rama;
- y las cifras, iguales a las del *runner*: **1544** y **740** casos, **426/450** y **828/900**
  KiB, **392** casos de jsdom y **22** en Chromium, y **115** permisos.

**Faltaban por anotar los runs del cierre.** El encargo cuenta tres, el de `8b296ad` y los dos de
`ec023f5`, en su rama y en `main`. Tampoco estaban en el PLAN los de `35853be` y `95ed9f2`, así
que son cinco: `git grep -n -E "40105877001|40107238444" ec023f5 -- docs/PLAN.md` no da nada. Van
en *Estado actual → El cierre de la fase 2, por el flujo de la rama*.

**El próximo informe cuenta desde `ec023f5`.** La siguiente mutación es la **745**, y el siguiente
ADR, el **0071**. **El arnés** está al día desde `dbf2664`, y `arnes-de-agente.md` se lee al
empezar cada sesión. La biblioteca no ha cambiado desde `3b45c6b`.

**Antes de la fase 3, una revisión completa del proyecto**, que encargó el usuario. Lo que salió de
ella va en estas respuestas y en el 3.1.

#### Las respuestas de la puerta de la fase 3, decisión del usuario

1. **El desglose: (a), con cuatro cambios.** Entra un ítem primero, la casa en orden; el de los
   puertos lleva también el tipo de la tarifa; entra la bandeja, lista para su primer manejador,
   antes de los documentos; y la zona pasa del final a antes del pedido. El orden queda así:
   - **3.1** · La casa en orden: el punto A de *El orden de trabajo*, debajo;
   - **3.2** · La exportación;
   - **3.3** · Los puertos, con el tipo de la tarifa;
   - **3.4** · La bandeja, lista para su primer manejador;
   - **3.5** · La zona;
   - **3.6** · La retirada del artículo;
   - **3.7** · El pedido;
   - **3.8** · La recepción;
   - **3.9** · La devolución;
   - **3.10** · La solicitud;
   - **3.11** · El repaso y el cierre.
2. **La exportación: (a)**, CSV en el dialecto de la importación, generado en el servidor.
   - Se genera por páginas internas y en flujo, **nunca en una sola orden ni entero en memoria**.
   - Se mide el listado más grande sobre una empresa del tamaño de la C del ADR-0064. **Si no cabe
     en el plazo de 30 s por orden, se pregunta.**
   - El `?q=` sin filtro (ADR-0069) va en este ítem.
3. **La escritura de Compras en Inventario: (a), la saga de dos pasos.** El ADR de la fase tiene
   que resolver dos cosas:
   - **lo pendiente ve lo que está en camino.** Una recepción confirmada y todavía sin respuesta
     resta de lo pendiente, bajo un cerrojo que pone en fila a dos recepciones del mismo pedido.
     Lo prueba una carrera de dos transacciones de verdad, en la que **dos recepciones de 60 sobre
     un pedido de 100 no pasan las dos**. Lo mismo para la devolución, con lo devuelto en camino;
   - **la respuesta no cambia dos agregados en una transacción** (R12). Si no sale sin excepción,
     se propone en la puerta de la recepción y se pregunta.
4. **El precio y la divisa: (a)(i).**
   - La columna de la divisa existe **desde el primer día** en el pedido, como dice el §7.5, y se
     valida contra la divisa base.
   - **La tarifa gana su tipo**, de compra o de venta, y **el tercero tiene una tarifa por rol**.
     Va en el 3.3, porque hoy un tercero que es cliente y proveedor propondría su precio de venta
     como precio de compra.
   - Solo propone precio **una tarifa de compra en la divisa base**.
5. **Los impuestos: (a).** El pedido congela el tipo en cada línea y suma la base, sin cuota.
6. **Las líneas: (a).** El borrador del pedido se edita, cada línea con su `If-Match`, y confirmar
   pide la versión. El ajuste y la transferencia siguen descartándose.
7. **La devolución: (a)(i)**, y **no devuelve más de lo recibido menos lo ya devuelto, incluido lo
   que está en camino**.
8. **Los umbrales: (a).**
9. **La recepción: (a), con dos fechas:**
   - **la del movimiento**, sujeta al ADR-0047, y que por omisión es hoy;
   - **la de entrega del proveedor**, con su número de albarán, que el ADR-0047 no limita.

   **El devengo de la fase 5 sale de la segunda.** Sin ella, una recepción registrada tarde,
   después de otro movimiento de su clave, no puede llevar su fecha real, y a fin de mes cae en
   otro periodo de IVA.
10. **«Pedido a proveedor»: (a).**
11. **La zona va en el 3.5, antes del pedido.** Y la pregunta que se dejaba para la puerta del
    ítem queda contestada:
    - **una zona por empresa**, `Europe/Madrid` por defecto, que rige lo que no tiene almacén;
    - **el almacén la hereda y la puede cambiar**;
    - en la transferencia, **cada fecha va en la zona de su almacén, y la guarda compara
      instantes**;
    - y entran también **las diez llamadas del frontal a `hoyEnElServidor()`**.
12. **Las notas, como se recomendaron**, salvo un sitio: `npm audit fix` va en el 3.1, y no como
    primer commit de la fase. El `?q=` sin filtro, en el 3.2, que es ahora la exportación. La
    correlación buscar/alta y los restos de la fase 1 se quedan con su disparador, y la
    destrucción del art. 32 tiene su ítem en la fase 5.
13. **El presupuesto: (b).**
    - El arranque sigue en **450**, cada ruta diferida tiene un tope de **192 KiB**, y **el total
      pasa a ser informativo**.
    - **Los textos de cada módulo viajan con sus rutas.**
    - Y **una línea informativa con lo comprimido**, que es lo que baja el usuario.
    - Un ADR enmienda el **ADR-0028** y el **ADR-0036**, y cada uno lo anota.
14. **`CLAUDE.md`: (a)**, y también **las líneas que mandan leer el PLAN, adaptadas al PLAN
    partido** del punto A. **Se aplica todo junto al principio de una sesión.**

#### El orden de trabajo, decisión del usuario

**A. El 3.1, la casa en orden.** En su rama, un tema por subida:

1. **El primer commit** lleva los runs anotados, las respuestas en *Decisiones tomadas*, el
   checklist de la fase 3 con sus criterios y la línea del README.
2. **`npm audit fix`**, y que la auditoría informativa vuelva a 0 o diga qué queda.
3. **Las enmiendas que no se declararon.** El ADR-0013 no dice que lo enmienda el ADR-0040, y el
   ADR-0007 no dice que lo enmienda el ADR-0039. Lo encontró un guion de reciprocidad sobre los 70
   ADR.
   - Cada uno lo anota en su *Estado* y en el punto afectado.
   - Ese guion pasa al carril rápido como test, con su pareja: una enmienda que sí se declara.
4. **El PLAN partido.** Hoy pesa 2 MB y unos 500 000 *tokens*, así que no se puede leer «antes de
   nada».
   - La historia de las fases 0, 1 y 2 pasa tal cual a `docs/historia/fase-N.md`: **se mueve, no
     se reescribe**.
   - El PLAN se queda con lo vigente: el objetivo, el alcance, un índice de decisiones que enlaza a
     los ADR o a la historia, dónde se retoma, el checklist de la fase 3 y las notas abiertas.
   - Se adapta lo que hoy lee el PLAN: `ElEstadoDelReadmeEsElDelPlanTests` y la regla de
     numeración de las mutaciones de `AGENTS.md`, que tiene que buscar también en la historia.
   - Se mide el tamaño antes y después, con la orden que lo mide.
   - **Si algo de esto no sale sin perder información, se para y se pregunta.**
5. **El presupuesto, con la forma nueva**: el guion, la CI, `AGENTS.md` y las dos enmiendas.
6. **Cuatro notas abiertas nuevas**, con el disparador «antes del primer despliegue fuera de local,
   y en todo caso en la puerta de la fase 5»:
   - **los dos roles de la base**: uno dueño para el migrador, y otro solo de datos para la API.
     Hoy la API es dueña de las tablas y, en el *compose*, superusuaria, así que puede desactivar
     los disparadores de solo añadido;
   - **`Include Error Detail=true`**, solo en desarrollo;
   - **la limitación de ritmo en el acceso**: el bloqueo de 5 intentos durante 15 minutos deja
     bloquear la cuenta de cualquiera;
   - **las 100 000 iteraciones de PBKDF2**, frente a las 210 000 de OWASP.

**B. El 3.2 y el 3.3**: la exportación y los puertos, cada uno con su criterio, su tanda y su
cierre por el flujo de la rama.

**C. El 3.4, la bandeja, lista para su primer manejador.** Antes de este ítem se lee
`fundamentos/datos-distribuidos/convenciones.md`. Son cinco huecos, cada uno con su caso y su
mutación:

1. **Un ámbito y un contexto por evento.** Hoy todo el lote comparte uno, y lo que un manejador
   deja a medias se lo guarda el siguiente.
2. **El manejador corre con el inquilino del evento**, sacado de `EmpresaId`, y no dentro de
   `SinInquilino`. Hoy lo que lee por EF ve todas las empresas.
3. **El evento lleva quién lo causó y su correlación**, para que la traza de auditoría y la
   telemetría no se corten entre módulos.
4. **Un indicador de aparcados sin resolver.** Hoy un aparcado desaparece de la métrica de
   antigüedad.
5. **El despachador abre la transacción del manejador**, y en ella van el efecto, la huella
   (ADR-0013 §6) y el evento de respuesta. Un manejador que choca con la versión de un documento
   reintenta, y no se aparca por eso.

Lo ejerce un manejador de prueba, con una carrera de verdad. Es una decisión de fondo: **si alguno
de los cinco tiene varias salidas viables, se para en la puerta del ítem y se pregunta.**

> **Se para cuando el 3.4 esté cerrado, y se pregunta antes del 3.5.** Es la base de toda la fase,
> y el usuario la quiere verificada antes de los documentos.

#### Las herramientas de la máquina, y cómo se trabaja

- **Los servidores de lenguaje**: se busca por símbolo antes de leer, y se abren los diagnósticos
  de cada edición. Un diagnóstico limpio no sustituye al build ni a los tests.
- **`playwright-cli`**, para las pantallas, contra la pila local en su propio proyecto de
  *compose*.
- **Context7 (`ctx7`)**, solo para APIs de Npgsql, EF Core o del SDK de OpenTelemetry que no
  cuadren, y contrastado con la documentación oficial.
- **El método de siempre**: la tanda se escribe antes de medir, con una mutación sobre el arnés;
  las dos listas, por nombre; commits firmados, un tema por subida, esperando a la conclusión de la
  anterior; el humo, si hay migración o pantalla; `main` solo avanza a un commit que ya está en
  verde en su rama, y la rama se borra después de su run de `main`; las ediciones de `CLAUDE.md`,
  al principio de la sesión; y la unidad se cierra antes de una pausa larga.
- **El informe** cuenta todos los commits desde `ec023f5`, cada uno con su asunto y con sus runs.
  Lo encontrado por el camino va arriba, y cada cifra va con la orden que la mide.

#### Lo que el agente decide sin preguntar, al anotarlo

1. **La rama del 3.1 es `3.1-la-casa-en-orden`**, abierta desde `ec023f5`.
2. **`CLAUDE.md` se edita al principio de la primera sesión después de que el PLAN partido llegue a
   `main`.** Las líneas que mandan leer el PLAN tienen que describir el PLAN partido, y «todo
   junto» quiere decir con ellas. Hasta entonces el §5 sigue diciendo que el objetivo es la fase 2,
   y manda este PLAN, como mandó durante la fase 2 sobre un §5 que decía la fase 1.
3. **El ADR de la saga** (respuesta 3) se escribe en la puerta de la recepción, el 3.8, que es
   donde se cruza la frontera por primera vez con un documento. El 3.4 deja el despachador listo y
   lo ejerce con un manejador de prueba.
4. **La inscripción de Compras en `IDocumentosDeUnPeriodo` va con su primer documento**, el pedido
   del 3.7: antes no hay documento que contar. El 3.3 trae el esquema, el contexto y las
   migraciones.
5. **El umbral rige la confirmación del pedido desde el 3.10**, con la solicitud, porque la
   respuesta 8 los pone juntos. Hasta entonces, confirmar un pedido pide solo su permiso.

### Tomadas por el agente de desarrollo — ítem 3.1, al escribirlo y al medirlo (2026-10-27)

Cada tema del criterio fue en su subida, como pedía el orden de trabajo, y algunos en más de un
commit: los runs y las respuestas, en `4a51ad0`; la auditoría, en `5661033`; las enmiendas, en
`a5e086b` y `de17700`; el PLAN partido, en `73adc0e` y `fa07a91`; el presupuesto, en `6af95ca`,
`571ece4`, `301a631`, `241750d` y `3ee6082`; y las cuatro notas, en `c99cd54`. Lo que se decidió
sin preguntar, porque el criterio no lo decía o porque salió al escribirlo:

1. **`npm audit fix` a secas no bastaba** (`5661033`). Subió `vite` a 7.3.7 y `undici` a 7.30.1,
   y los cuatro paquetes de `vitest` se quedaron, porque `vitest` 4.1.12 pide
   `@vitest/browser-playwright` 4.1.12 exacto, y al revés. Subieron juntos con un `npm install -D`
   de los dos, que deja los rangos en `^4.1.12`. Ninguno es versión mayor: doce paquetes cambian
   de versión, todos MIT, y no entra ni sale ninguno por nombre.
2. **La regla de las enmiendas lee también las ampliaciones** (`a5e086b`). Una declaración es una
   viñeta de la cabecera que empieza por «Enmienda» o «Amplía», con o sin etiqueta delante. Al
   leerlas salieron seis huecos más que los dos del encargo, y se cerraron en el mismo commit:
   - sin anotar en el *Estado*: el 0035 amplía el 0021, el 0043 el 0040, y el 0016 el 0011;
   - anotadas y sin nota en el punto: el 0042 al 0041, y el 0063 al 0030 y al 0036.

   Cada anotación nueva dice que es del 2026-10-26, porque el enmendado no lo decía, y ninguna
   cambia lo que el ADR decidió.
3. **Las cinco viñetas que nombran una enmienda sin declararla van en una lista cerrada**, cada
   una con su motivo, y la lista se compara entera: una que sobra o que falta es un rojo.
4. **La tanda de las enmiendas cambió el arnés dos veces** (`de17700`). La 752 salió verde, como
   se escribió, porque ningún caso tenía un *Estado* anotado sin nota en el punto: el caso
   sintético gana ese tercer estado. La 753 quitaba un filtro que nada ejercía, y el filtro se
   borra: un ADR que se nombrara a sí mismo saldría como una anotación que nadie declara.
5. **Cada bloque de la historia va a su fase** (`73adc0e`). Las decisiones, el estado y el
   checklist, por sus encabezados. Las notas cerradas, a la fase de la fecha de su etiqueta, y las
   quince sin etiqueta, del 2026-08-25, a la fase 0, con el título, el objetivo y el alcance de
   antes, que eran de la fase 0.
6. **`scripts/comprobar-el-plan-partido.py` cuenta las líneas como multiconjunto**, y lo único que
   deshace antes de comparar son los enlaces a los ADR, que en la historia ganan un `../`. Sin
   argumentos compara el commit que creó `docs/historia/` con su padre, así que se repite.
7. **`ElEstadoDelReadmeEsElDelPlanTests` gana dos casos**: el PLAN lleva casillas de una sola fase,
   y las fases con historia son exactamente las de antes de la vigente, cada una con sus casillas
   marcadas. La pareja de las casillas de la fase 0 se busca ahora en su historia.
8. **Los textos siguen en un solo espacio de nombres**, `traduccion` (`6af95ca`), así que
   `t('inventario.…')` no cambia: cambia cuándo llegan. Los trae el `lazy` de la ruta, a la vez
   que la pantalla, con el dueño que declara la tabla de rutas. El enrutador se crea en `main.tsx`
   con la promesa de la instancia, para que la pantalla y el diccionario sigan bajando a la vez.
9. **La ruta se mide con el manifiesto de Vite** (`301a631`, ADR-0071), porque en el `dist` un
   fragmento compartido no dice quién lo importa: su cierre estático, menos el arranque, más los
   textos de su dueño en el mayor de sus idiomas. El manifiesto no se sirve: `Dockerfile.web` lo
   borra con los mapas, y el humo de la imagen lo comprueba, con el `index.html` como pareja.
10. **Lo comprimido se mide con `gzip` de nivel 1**, el que usa nginx cuando su configuración no
    dice `gzip_comp_level`, y `deploy/nginx.conf` no lo dice.
11. **Las etiquetas de las descargas retenidas llevan barra** (`3ee6082`): `organizacion/es`, y no
    `organizacion.es`. `LosPermisosQueNombraElFrontalTests`, en el carril de integración, vio
    diecisiete literales con forma de permiso en los casos nuevos, y el barrido no se tocó: son
    nombres del arnés, y la barra no tiene forma de permiso. Dos aserciones que tecleaban una clave
    suelta se quitaron, porque otra del mismo caso ya las implica.
12. **Se arregló en un commit nuevo, y no reescribiendo los cuatro**, que son la base de las
    tandas de la 765 a la 791. Así que `6af95ca`, `571ece4`, `301a631` y `241750d` tienen ese caso
    en rojo, cada uno por separado. Ningún run los vio rojos, porque fueron en la misma subida que
    `3ee6082`: lo vio la batería sobre `241750d`, antes de subir.
13. **Las cuatro notas nuevas llevan la orden que comprueba lo que dicen** (`c99cd54`), y el
    disparador del encargo. Ninguna se hace ahora.
14. **La nota de la auditoría se cierra aquí y se queda en el PLAN**, con su CERRADA, hasta que la
    fase se cierre y su historia se mueva. Así se hizo en las fases de antes.
15. **La debilidad que encontraron la 790 y la 791 va a una nota abierta**, y no se arregla, porque
    ningún criterio del 3.1 la pide: *Notas / riesgos*, la quinta.

**Las cinco tandas, de la 745 a la 791, se escribieron antes de medir**, cada una sobre el commit
de su tema, en el *scratchpad*: `tanda-enmiendas.md`, `tanda-partido.md` y `tanda-presupuesto.md`,
que lleva también la de los textos y la del ADR-0071. Cada mutación se aplicó sola en el árbol
limpio, se revirtió con `git restore --source=HEAD` y se tocó con la fecha de ahora:

```bash
python -X utf8 mutar-enmiendas.py 745 746 747 748 749 750 751 752 753  # a5e086b, y de17700
python -X utf8 mutar-partido.py 754 755 756 757 758 759 760 761 762 763 764   # sobre fa07a91
python -X utf8 mutar-textos.py <salida>    # 765-775, sobre 6af95ca, 571ece4 y 3ee6082
python -X utf8 solo-768.py                 # la 768, con cada caso solo y tres rondas
python -X utf8 mutar-presupuesto.py <salida> 776 777 778 779 780 781 782 783   # sobre 301a631
python -X utf8 mutar-presupuesto.py <salida> 784 785 786 787 788
python -X utf8 canario-adr.py <salida>     # 790 y 791, sobre 241750d
```

Los carriles: las de las enmiendas y las del ADR-0071, con `dotnet test tests/Arquitectura.Tests`
filtrado a la clase `UnaEnmiendaSeAnotaEnLosDosAdrTests`; las del PLAN partido, con la clase
`ElEstadoDelReadmeEsElDelPlanTests` o con el guion que compara;
las de los textos, con `npx vitest run` entero y su informe JSON, contando los casos por nombre; y
las del presupuesto, con el guion entero dos veces, con el tope por ruta en la ruta más pesada,
173, y en 172. Las bases, en verde: las enmiendas, 0 de 8; el PLAN partido, 0 de 4, y el guion,
verde; los textos, 0 de 400 sobre `571ece4` y sobre `3ee6082`; el presupuesto, 0 con 173 y 1 con
172, por la transferencia y el recuento; y el ADR-0071, 0 de 8. Al acabar cada tanda, la porcelana,
vacía. Por los casos de los textos: (a) `una ruta trae los textos de su funcionalidad, y solo los
suyos`; (b) `mientras sus textos viajan, la pantalla no se pinta…`; (c) `cambiar de idioma trae los
textos de lo visitado…`; (d) `si el idioma cambia mientras los textos de la ruta viajan…`; y (e)
`si los textos de la ruta llegan mientras el idioma cambia…`.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 745 | El *Estado* del ADR-0007 vuelve a «aceptado» a secas | **1 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda` |
| 746 | Sin la nota del §5 del ADR-0007 | **1 de 8**: `El_enmendado_lleva_la_nota_en_el_punto_que_cambia` |
| 747 | El ADR-0016 dice «aplica el» en vez de «amplía el» | **1 de 8**: `Lo_que_anota_un_estado_lo_declara_quien_enmienda` |
| 748 | **Arnés**: la declaración ya no admite etiqueta delante | **3 de 8**: `Lo_que_anota_un_estado_lo_declara_quien_enmienda`, `Ninguna_mencion_de_una_enmienda_queda_sin_clasificar` y `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 749 | **Arnés**: «por el ADR-» pasa a «por la ADR-» | **4 de 8**: `Cada_enmienda_nombra_lo_que_enmienda`, `El_barrido_ve_las_enmiendas_y_la_pareja_anotada_en_los_dos_lados`, `El_enmendado_anota_en_su_estado_quien_lo_enmienda` y `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 750 | **Arnés**: la cláusula no se corta nunca | **2 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda` y `El_enmendado_lleva_la_nota_en_el_punto_que_cambia` |
| 751 | Sale de la lista cerrada la mención del ADR-0055 | **1 de 8**: `Ninguna_mencion_de_una_enmienda_queda_sin_clasificar` |
| 752 | **Arnés**: el cuerpo empieza en el título, con la cabecera dentro | sobre `a5e086b`, **0 de 8**, como se escribió; sobre `de17700`, **1 de 8**: `Una_enmienda_que_el_enmendado_no_anota_se_ve`, por el tercer estado del caso sintético (punto 4) |
| 753 | **Arnés**: el número propio no se aparta | sobre `a5e086b`, **0 de 8**, como se escribió; sobre `de17700` no cambia nada, porque el filtro ya no está (punto 4) |
| 754 | Una casilla de `fase-1.md` se desmarca, la del 1.5 | **1 de 4**: `Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero` |
| 755 | Sin `fase-0.md` | **2 de 4**: el mismo y `El_checklist_del_plan_se_lee` |
| 756 | El checklist del PLAN gana una casilla marcada de la fase 2 | **1 de 4**: `El_plan_lleva_las_casillas_de_una_sola_fase` |
| 757 | `docs/historia/` gana un `notas.md` | **1 de 4**: `Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero` |
| 758 | La historia de la fase 1 gana una casilla de la fase 2 | **1 de 4**: el mismo |
| 759 | **Arnés**: la historia se busca como `fase-N.markdown` | **1 de 4**: el mismo, porque ninguna fase tiene historia |
| 760 | **Arnés**: las fases debidas son `Range(0, vigente - 1)` | **1 de 4**: el mismo |
| 761 | Una línea única, quitada de `fase-2.md` | **el guion, con 1**: falta `### Tomadas en la puerta de clarificación de la fase 2 (2026-09-18)` |
| 762 | **Arnés**: el guion no deshace el `../` de los enlaces a los ADR | **el guion, con 1**: faltan 20 líneas, las que enlazan un ADR |
| 763 | **Arnés**: el guion busca la historia en `docs/historias` | **el guion, con 1**: «el después no tiene ningún fichero en docs/historias/» |
| 764 | Una de las dos copias de una línea que el PLAN de antes tenía dos veces | **el guion, con 1**: falta una vez `- rama propia por unidad, y commits pequeños y firmados.`; cuenta, y no solo mira si está |
| 765 | `cargarTextosDe` sin el bucle: no mira si el idioma cambió | **1**: (d) |
| 766 | `cambiarIdioma` sin la última mirada antes de cambiar | **1**: (e) |
| 767 | `cambiarIdioma` no pide los textos de lo visitado | **1**: (c). Sobre `6af95ca`, 3: el hallazgo de abajo |
| 768 | El `lazy` no espera a los textos | **2**: (b), y (d) solo en la pasada entera. Con el caso solo, (b) rojo en 3 de 3 y (d) verde en 3 de 3: el hallazgo de abajo |
| 769 | El `lazy` no trae los textos | **5**: de la (a) a la (e) |
| 770 | `es.ts` vuelve a llevar un espacio de una funcionalidad | **7**: el del arranque, el barrido de los espacios, y de la (a) a la (e), porque con el espacio en el armazón nadie lo trae |
| 771 | La expresión del *linter*, con `tiposDeError\.` como único prefijo | **1**: `marca el valor desde el armazón…`, de `ElDiccionarioSeImportaPorSuTipo` |
| 772 | El armazón nombra una clave de una funcionalidad | **1**: `ningún fichero del armazón ni de otra funcionalidad nombra una clave suya` |
| 773 | **Arnés**: el barrido deja de casar las plantillas | **1**: `y la ve si alguien la nombra: desde el armazón, desde otra, y en una plantilla` |
| 774 | **Arnés**: la lista de fuentes sale vacía, con `.jsx` | **1**: `ningún fichero del armazón…`, por su pareja, `FUENTES.length > 50` |
| 775 | **Arnés**: la retención del cargador no retiene | **4**: de la (b) a la (e), los que retienen |
| 776 | El tope por ruta compara con `>=` | **con 173, 1**: la del recuento, en su tope |
| 777 | **Arnés**: el guion de las rutas sale siempre con 0 | **con 172, 0**, aunque escribe su `::error::` |
| 778 | La ruta deja de sumar los textos de su dueño | **con 172, 0**: la más pesada baja a 161 |
| 779 | El cierre no sigue las importaciones estáticas | **con 172, 0**: la más pesada baja a 29 |
| 780 | La ruta no resta el arranque | **con 173, 1**: la más pesada sube a 577 |
| 781 | El guion del arranque no pasa lo que lee del índice | **con 173, 1**: «el manifiesto pone en el arranque … y el guion no lo ha contado» |
| 782 | **Arnés**: el guion del arranque se traga la salida 1 de las rutas | **con 172, 0** |
| 783 | `build.manifest` apagado | **con 173, 1**: «no existe … manifest.json» |
| 784 | El armazón importa por su valor los textos de una funcionalidad | **con 173, 1**: «no hay fragmento de los textos de 'organizacion' en 'es'» |
| 785 | La entrada importa en diferido algo sin clasificar | **con 173, 1**: «la entrada importa en diferido src/app/sobrante.ts…» |
| 786 | Una pantalla importa algo en diferido | **con 173, 1**: «…PaginaDeAlmacenes.tsx importa algo en diferido» |
| 787 | Una pantalla que no está en `rutas.tsx` | **con 173, 1**: «Sobran: [src/features/organizacion/PaginaSobrante.tsx]» |
| 788 | Una pantalla engorda 30 000 B | **con 173, 1**: el recuento, 202 KiB |
| 789 | `Dockerfile.web` sin el `rm` del manifiesto | **a mano, con Docker**: la imagen construida con `Dockerfile.web.sin-rm`, del *scratchpad*, tiene `/usr/share/nginx/html/.vite`, y la de `Dockerfile.web`, no, con el `index.html` en su sitio |
| 790 | El 0036 pierde de su *Estado* la frase del 0071 | **1 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda`, en `ADR-0071 → ADR-0036` |
| 791 | El 0028 pierde sus dos notas del 0071 | **1 de 8**: `El_enmendado_lleva_la_nota_en_el_punto_que_cambia`, en `ADR-0071 → ADR-0028` |

- **Lo que salió distinto de lo escrito, y por qué**:
  - **la 752 y la 753, verdes, como se escribió**, y cada una dejó su cambio en `de17700`: el
    punto 4;
  - **la 767 dio tres rojos sobre `6af95ca`, y no uno**. El `afterEach` soltaba lo que un caso
    rojo había dejado retenido, y eso corría dentro del caso siguiente. Arreglado en `571ece4`: lo
    retenido se olvida sin soltarse;
  - **la 768, con el caso solo, salía verde sobre `6af95ca`**: la pantalla tardaba más en
    compilarse que los textos en llegar. Desde `571ece4`, el caso carga antes la pantalla, y afirma
    que el enrutador no ha terminado su primera carga. El rojo de (d) en la pasada entera es la
    pantalla caliente que deja (b), y no su sujeto;
  - **la 784 y la 788 no compilaban** en su primera forma: `tsc -b` pide `['…']`, por
    `noPropertyAccessFromIndexSignature`. Se reescribieron y se midieron en la segunda pasada;
  - **la 790 y la 791 salieron verdes en su primera forma, por diseño**, y no cuentan. Una cambiaba
    «enmendado» por «corregido» en el *Estado* del 0036, y la viñeta seguía diciendo «Ampliado» por
    el 0063 y nombrando «por el ADR-0071». La otra quitaba una sola de las dos notas del 0028, y la
    regla pide la nota en el cuerpo, no en qué punto. Se reescribieron sobre el sujeto. La primera
    es la nota abierta del punto 15;
  - **el barrido de permisos del carril de integración vio lo que el carril rápido no ve**: un
    literal de un caso del frontal con forma de permiso. Es el punto 11, y por eso la tanda de los
    textos se midió otra vez sobre `3ee6082`, con los mismos rojos, caso por caso
    (`tanda-textos-3.log`).

La siguiente mutación es la **792**, y el siguiente ADR, el **0072**.

## Estado actual

**FASE 3 EN CURSO — 1 de 11 ítems.** La puerta de clarificación se contestó el 2026-10-26: las
catorce respuestas, el orden de trabajo y lo que el agente decidió al anotarlas están en
*Decisiones tomadas → Traídas por el encargo del 2026-10-26*, y el desglose son **once ítems**, del
3.1 al 3.11, en el *Checklist*. El primero, la casa en orden, está hecho.

> **Dónde se retoma (2026-10-27): en el 3.2, la exportación**, en su rama, `3.2-la-exportacion`,
> abierta desde `main` cuando el cierre del 3.1 haya llegado a él. Su primer commit anota los dos
> runs del cierre del 3.1, el de la rama y el de `main`. Después, el 3.3, y el 3.4, **que se para
> al cerrarse, antes del 3.5**.
>
> **`CLAUDE.md` se edita al principio de la primera sesión después de que el PLAN partido llegue a
> `main`**: el §5 de la respuesta 14 y las líneas que mandan leer el PLAN, todo junto. Llega a
> `main` con el cierre del 3.1.
>
> `/code-review` no se hizo: el traspaso la anota como no hecha, con su rango,
> `0638419..0d26e76`.

**El 3.1, hecho el 2026-10-27**, en la rama `3.1-la-casa-en-orden`, abierta el 2026-10-26 desde
`main` en `ec023f5`. Lo que pide la respuesta 13 está en el ADR-0071, que enmienda el 0028 y el
0036; lo que se decidió al escribirlo y al medirlo, en *Decisiones tomadas → Tomadas por el agente
de desarrollo — ítem 3.1*. Lleva un ADR, así que el siguiente es el **0072**. Sus mutaciones van de
la **745** a la **791**, y las siguientes empiezan en la **792**.

**Los commits, por este orden, y las subidas, una por tema, cada una con su run de rama** sobre su
último commit (los trabajos, Backend, Frontal y Humo). Todos en verde:

1. `4a51ad0`, «docs(plan): la puerta de la fase 3, contestada, y su checklist»: **40415382989**,
   Frontal `121896286522`, Backend `121896286797` y Humo `121901754984`.
2. `5661033`, «fix(frontal): vitest 4.1.12 y vite 7.3.7, y la auditoría a cero»:
   **40417648295**, Backend `121903848260`, Frontal `121903848630` y Humo `121909207661`.
3. `a5e086b`, «docs(adr): las enmiendas, anotadas en los dos lados, y su regla», y `de17700`,
   «test(arquitectura): la nota del punto no es la del Estado», en una subida: **40419763345**,
   Frontal `121910970468`, Backend `121910970829` y Humo `121915165263`.
4. `73adc0e`, «docs(plan): el PLAN partido, con cada fase cerrada en su historia», y `fa07a91`,
   «docs(agents): la numeración de las mutaciones busca en la historia»: **40421394770**, Backend
   `121916433084`, Frontal `121916433460` y Humo `121919992685`.
5. `6af95ca`, «feat(frontal): los textos de cada módulo viajan con sus rutas»; `571ece4`,
   «test(frontal): un caso rojo no suelta sus cargas en el siguiente»; `301a631`, «ci(frontal):
   un tope por ruta, el total informativo y lo comprimido»; `241750d`, «docs(adr): el presupuesto
   vigila el arranque y cada ruta (ADR-0071)»; y `3ee6082`, «test(frontal): los casos de los
   textos no teclean forma de permiso», en una subida: **40435035345**, Backend `121961570006`,
   Frontal `121961570175` y Humo `121965776910`. Los cuatro primeros tienen en rojo
   `LosPermisosQueNombraElFrontalTests`, y el quinto lo arregla: el punto 12 de las decisiones.
6. `c99cd54`, «docs(plan): cuatro notas abiertas de seguridad, para antes de desplegar»:
   **40436956193**, Frontal `121967812130`, Backend `121967812439` y Humo `121972126362`.
7. El que lo cierra, «docs(plan): cierra el 3.1, con sus tandas y la batería», por el flujo de la
   rama. Su run de rama y el de `main` los anota el primer commit del 3.2.

Los runs se leyeron con `python -X utf8 vigilar212.py <sha> <rama>`, un guion del *scratchpad*, y
el de `c99cd54`, con su copia `vigilar31.py`, que pregunta con `curl`: el 2026-10-27 el `urllib`
de Python dejó de verificar el certificado de `api.github.com`, y el guion salió con su «sondeo
roto», como debe.

**El tamaño del PLAN**, con `python -X utf8 scripts/comprobar-el-plan-partido.py`, sin argumentos:
de 2 036 368 bytes en 25 280 líneas a 103 376 en 1298, y la historia, 346 621, 523 775 y
1 083 260 bytes en las tres fases. El guion dice también que no se perdió ninguna línea.

**La batería de `AGENTS.md`, entera y en su orden, sobre `3ee6082`**, con `bash bateria31.sh`, un
guion del *scratchpad* que deja cada paso en `artifacts/bateria-<paso>.log` y su código de salida
en una línea. Los diecinueve pasos salen con 0, y la porcelana, vacía. `c99cd54` y el cierre solo
tocan el PLAN y el README:

- `npm run api`, sin cambios en `esquema.ts`; las migraciones, al día; **158** operaciones en
  **95** rutas y **240** tipos de **248** sitios, como en la base, con los dos generadores en
  `--comprobar`. Las rutas, contadas sobre `docs/api/openapi.json`;
- `typecheck`, `lint` y `format:check`, limpios; **400** casos de jsdom en **48** ficheros, ocho
  casos y un fichero más que la base; `canal-de-la-consola.sh`, con la marca, ningún aviso de
  `act()` y siete trozos para las ocho cadenas; y el carril del navegador, **22** casos en **1**
  fichero, todos en verde;
- el arranque, **407/450 KiB** en 4 ficheros, 19 menos que la base; la ruta diferida más pesada,
  `PaginaDelRecuento`, **173/192 KiB**, de 21 rutas; el total servido, **831 KiB**, informativo; y
  lo comprimido, **145**, **61** y **298 KiB**, también informativo;
- el build, con 0 errores y 0 avisos, y `dotnet format`, limpio;
- **1554** casos en el carril rápido y **740** en el de integración, 652 de `Api.IntegrationTests`
  y 88 de `Organizacion.IntegrationTests`, todos en verde y contados con `recuento-de-tests.sh` y
  las dos listas del *workflow*. Son diez más en el rápido, los ocho de
  `UnaEnmiendaSeAnotaEnLosDosAdrTests` y los dos nuevos de `ElEstadoDelReadmeEsElDelPlanTests`;
- y las dependencias, con `python -X utf8 scripts/dependencias-por-conjuntos.py ec023f5 HEAD`: 125
  pares y 35 `Project` en los dos lados, sin nada añadido ni retirado; y en el frontal, 443
  entradas en los dos, con doce que cambian de versión y ninguna de nombre: `vitest` y nueve de
  `@vitest/` de 4.1.11 a 4.1.12, `vite` de 7.3.6 a 7.3.7 y `undici` de 7.30.0 a 7.30.1.

**La auditoría de la herramienta**, con `npm --prefix frontend audit --json` sobre `c99cd54`: 0 en
todas las gravedades. La puerta, `npm --prefix frontend audit --omit=dev --audit-level=high`, sale
con 0.

## Checklist

> Un ítem = una unidad de trabajo pequeña, con criterio de aceptación verificable.
> `[ ]` pendiente · `[x]` hecho (cumple criterio + verificado + commiteado si procede).

### Fase 3 · Compras (2026-10-26)

> **La fase 3 tampoco tiene Anexo A.3.** El §15 da el criterio de la fase entera —«un pedido
> confirmado no mueve stock; su recepción parcial sí, y deja las cantidades pendientes correctas;
> la devolución revierte»— y el desglose en **once ítems** lo acordaron usuario y agente en la
> **puerta de clarificación de la fase 3**, cuyas catorce respuestas con su motivo están en
> *Decisiones tomadas → Traídas por el encargo del 2026-10-26*. Este checklist es el resultado;
> **no se reordena ni se amplía** por iniciativa propia, igual que el A.3. Cada criterio se
> escribió antes de empezar su ítem.
>
> **Se para cuando el 3.4 esté cerrado**, y se pregunta antes del 3.5.
>
> **Las líneas base, sobre `ec023f5`**, para que el primer ítem mida contra ellas: carril rápido
> **1544** en 10 ensamblados · integración **740** en 2 · OpenAPI **158** operaciones en **95**
> rutas · catálogo de `type` **240** de 248 sitios · frontal 47 ficheros / **392** casos / **0**
> avisos de `act()`, y **22** casos en un Chromium · arranque **426/450 KiB**, total servido
> **828/900** · **115** permisos · dependencias **125** pares y **35** `Project`, y **443** entradas
> del frontal sin la raíz. Son las del run **40104536177** y de la batería del cierre del 2.20, y
> `ec023f5` solo toca el PLAN.

- [x] **3.1 · La casa en orden** — criterio de aceptación: seis temas, cada uno en su commit y en
  su subida, en la rama del ítem.
  - **El primer commit**: los cinco runs del cierre de la fase 2 anotados, las respuestas de la
    puerta en *Decisiones tomadas*, este checklist y la línea del README.
  - **`npm audit fix`**, y la auditoría informativa en 0, o diciendo qué queda y por qué.
  - **Las enmiendas que no se declararon**: el ADR-0013 anota que lo enmienda el ADR-0040, y el
    ADR-0007, que lo enmienda el ADR-0039, cada uno en su *Estado* y en el punto afectado. Y la
    reciprocidad pasa al carril rápido como test: una enmienda que un ADR declara, y que el
    enmendado no anota, es un rojo. Con su pareja, una enmienda que sí se declara, y con la
    afirmación de que encontró enmiendas.
  - **El PLAN partido**: la historia de las fases 0, 1 y 2 pasa tal cual a
    `docs/historia/fase-N.md`, movida y no reescrita; el PLAN se queda con el objetivo, el
    alcance, un índice de decisiones que enlaza a los ADR o a la historia, dónde se retoma, el
    checklist de la fase 3 y las notas abiertas. `ElEstadoDelReadmeEsElDelPlanTests` y la regla de
    numeración de las mutaciones de `AGENTS.md` leen también la historia. El tamaño, antes y
    después, con la orden que lo mide. Y que no se pierde nada, comprobado por un guion: cada
    línea del PLAN de antes está en uno de los ficheros de después.
  - **El presupuesto con la forma nueva**: el arranque en 450, un tope de 192 KiB por ruta
    diferida, el total solo informativo, y una línea informativa con lo comprimido. Los textos de
    cada módulo viajan con sus rutas. El guion, la CI y `AGENTS.md`, y un ADR que enmienda el 0028
    y el 0036, que lo anotan.
  - **Cuatro notas abiertas nuevas**, con el disparador «antes del primer despliegue fuera de
    local, y en todo caso en la puerta de la fase 5»: los dos roles de la base, `Include Error
    Detail=true` solo en desarrollo, la limitación de ritmo en el acceso y las iteraciones de
    PBKDF2.

  **Hecho el 2026-10-27**, en la rama `3.1-la-casa-en-orden`, en doce commits, del `4a51ad0` al
  `c99cd54`, y el que lo cierra. Cada tema, en su subida; tres de ellos, en más de un commit. Lo
  decidido sin preguntar y las tandas de la 745 a la 791, en *Decisiones tomadas → Tomadas por el
  agente de desarrollo — ítem 3.1*; los commits, sus runs y la batería, en *Estado actual → El 3.1,
  hecho*. Cada tema, con lo que lo comprueba:

  - **el primer commit** (`4a51ad0`): los cinco runs, en *Estado actual* de la historia de la fase
    2, y las respuestas, este checklist y la línea del README, que comprueba
    `ElEstadoDelReadmeEsElDelPlanTests`;
  - **la auditoría** (`5661033`): 0 en todas las gravedades, con `npm --prefix frontend audit
    --json`, y la nota de la auditoría, cerrada;
  - **las enmiendas** (`a5e086b`, `de17700`): las dos del encargo y seis más, anotadas, y
    `UnaEnmiendaSeAnotaEnLosDosAdrTests`, con su pareja, el 0070, y sus dos fuentes por encima de
    veinte, que ven de la 745 a la 752;
  - **el PLAN partido** (`73adc0e`, `fa07a91`): de 2 036 368 a 103 376 bytes, sin perder una
    línea, con `scripts/comprobar-el-plan-partido.py`, que ven de la 761 a la 764; los dos casos
    nuevos de `ElEstadoDelReadmeEsElDelPlanTests`, que ven de la 754 a la 760; y la orden de la
    numeración de `AGENTS.md`, que lista 64 tablas en los dos sitios;
  - **el presupuesto** (`6af95ca`, `571ece4`, `301a631`, `241750d`, `3ee6082`): el arranque,
    407/450; la ruta más pesada, 173/192; el total y lo comprimido, informativos; los textos de
    cada módulo, con sus rutas, que ven de la 765 a la 775; el guion, que ven de la 776 a la 789;
    la CI y `AGENTS.md`, en `450 192`; y el ADR-0071, con el 0028 y el 0036 anotados, que ven la
    790 y la 791;
  - **las cuatro notas** (`c99cd54`), en *Notas / riesgos*, cada una con su orden.
- [ ] **3.2 · La exportación** — criterio de aceptación: cada listado de la nota de la exportación
  descarga un CSV en el dialecto de la importación (ADR-0034 §5), con BOM y con la regla del
  apóstrofo de la decisión 7 del 1.11. Lo genera el servidor con los filtros y el orden del
  listado, **todas sus páginas**, el permiso del listado y sin lo bloqueado. Se genera por páginas
  internas y en flujo, nunca en una sola orden ni entero en memoria. El listado más grande, medido
  sobre una empresa del tamaño de la C del ADR-0064, cabe en el plazo de 30 s por orden; si no
  cabe, se pregunta. Y un `?q=` sin filtro declarado es un `400` (ADR-0069).
- [ ] **3.3 · Los puertos, con el tipo de la tarifa** — criterio de aceptación: la tarifa es de
  compra o de venta, y un tercero lleva una tarifa por rol; un puerto de `Catalogo.Contracts`
  resuelve el precio y el descuento de una tarifa para un artículo, una cantidad y una fecha, y
  solo propone precio una tarifa de compra en la divisa base. Un tercero que es cliente y
  proveedor recibe su precio de compra, no el de venta. La divisa de la tarifa pasa a ISO 4217, y
  el invariante 8 del traspaso de la fase 1 queda desarmado. Y Compras gana persistencia: su
  esquema, su contexto y sus migraciones, y los tests de arquitectura lo cuentan como módulo con
  persistencia.
- [ ] **3.4 · La bandeja, lista para su primer manejador** — criterio de aceptación: los cinco
  huecos de *Decisiones tomadas → El orden de trabajo, C*, cada uno con su caso y su mutación: un
  ámbito y un contexto por evento; el manejador con el inquilino del evento, y no dentro de
  `SinInquilino`; quién lo causó y su correlación en el evento; un indicador de aparcados sin
  resolver; y la transacción del manejador, abierta por el despachador, con el efecto, la huella y
  el evento de respuesta dentro, y un choque de versión que reintenta sin aparcar. Lo ejerce un
  manejador de prueba, con una carrera de verdad. Antes se lee
  `fundamentos/datos-distribuidos/convenciones.md`. **Al cerrarlo, se para y se pregunta antes
  del 3.5.**
- [ ] **3.5 · La zona** — criterio de aceptación: una zona por empresa, `Europe/Madrid` por
  defecto, que rige lo que no tiene almacén; el almacén la hereda y la puede cambiar. El día de
  negocio de un documento sale de la zona de su almacén: a las 00:30 del 1 de enero en Madrid, la
  fecha por omisión es el 1 de enero, y no el 31 de diciembre. En la transferencia, cada fecha va
  en la zona de su almacén, y la guarda compara instantes. Y las diez llamadas del frontal a
  `hoyEnElServidor()` dan el día de su zona.
- [ ] **3.6 · La retirada del artículo** (ADR-0023) — criterio de aceptación: `IConsultaDeArticulos`
  produce `SoloResuelveLoViejo`; una línea nueva no elige un artículo retirado; y el retirado sigue
  contestando su unidad base, para que un recuento pueda sacar sus existencias.
- [ ] **3.7 · El pedido de compra** — criterio de aceptación: el alta en borrador, con la divisa
  desde el primer día y validada contra la base; las líneas del borrador se añaden, se cambian y se
  quitan, cada una con su `If-Match`, y confirmar pide la versión; confirmar numera en su serie,
  congela la R7 —el precio, el descuento, el tipo impositivo y la descripción, a la fecha del
  pedido— y suma la base sin cuota; cerrar con lo pendiente, anular y descartar; lo pendiente, en
  el pedido y en cada línea; un proveedor que no se puede tratar no recibe pedidos; Compras se
  inscribe en `IDocumentosDeUnPeriodo`; y el listado, la ficha y sus pantallas. El primer criterio
  del §15: **un pedido confirmado no tiene ni una fila en el libro**.
- [ ] **3.8 · La recepción** — criterio de aceptación: total o parcial contra un pedido
  confirmado, y nunca más de lo pendiente, que es un `422` con su línea; con su lote o su serie y
  su ubicación; con dos fechas, la del movimiento, sujeta al ADR-0047 y hoy por omisión, y la de
  entrega del proveedor con su número de albarán, que el ADR-0047 no limita. La entrada, valorada
  con el coste de su línea, llega a Inventario por la saga de dos pasos: lo pendiente ve lo que
  está en camino, bajo un cerrojo que pone en fila a dos recepciones del mismo pedido, y **dos
  recepciones de 60 sobre un pedido de 100 no pasan las dos**, en una carrera de dos transacciones
  de verdad; y la respuesta no cambia dos agregados en una transacción (R12). Si eso no sale sin
  excepción, se propone en la puerta del ítem y se pregunta. El segundo criterio del §15: **dos
  recepciones parciales dejan lo pendiente de cada línea bien, y el saldo sigue siendo la suma del
  libro**.
- [ ] **3.9 · La devolución de compra** — criterio de aceptación: desde una recepción, total o
  parcial, y sin devolver más de lo recibido menos lo ya devuelto, incluido lo que está en camino,
  con su carrera; se lleva el coste de su recepción, acotado como la transferencia (ADR-0054); el
  lote y la serie salen de la línea de la recepción, y la ubicación la elige quien devuelve, con la
  de la recepción por omisión; y revierte lo recibido, así que lo devuelto vuelve a estar
  pendiente, y un pedido `Recibido` vuelve a `RecibidoParcial`. El tercer criterio del §15:
  **recibir y devolver lo mismo deja el libro en cero** por artículo, almacén, ubicación y lote, en
  cantidad y en valor.
- [ ] **3.10 · La solicitud de compra** — criterio de aceptación: un umbral por empresa rige la
  aprobación de la solicitud y la confirmación del pedido; por encima hace falta el permiso
  `…-sin-limite`, y quien creó el documento no lo aprueba ni lo confirma por encima del umbral, ni
  con los dos permisos (§11). La solicitud es opcional, su importe es el de sus líneas a precio
  estimado en divisa base, y de una aprobada sale un pedido en borrador.
- [ ] **3.11 · El repaso de la fase y su cierre** — criterio de aceptación: cada criterio de la
  fase, contra un caso con nombre que un guion busca en el código, con su canario; el veredicto de
  cada nota abierta; y el traspaso a la fase 4.

## Imports pendientes de `CLAUDE.md`

`CLAUDE.md` ya trae el **núcleo permanente** (A.2.1) y los de **fase 0** (A.2.2). Los de abajo
**no están puestos a propósito**: cada import cuesta contexto en **cada** turno, así que se añaden
al empezar su fase y se quedan (Anexo A.2.3).

> **Medido el 2026-09-29: ningún import de la biblioteca se carga.** El `/context` de la sesión,
> lanzado por el usuario al importar `herramientas/arnes-de-agente.md`, da:
>
> - un arranque de **35,6k** tokens sobre una ventana de **300k**, la de `autoCompactWindow`;
> - repartidos en **3,3k** del *prompt* del sistema, **21,1k** de herramientas, **6,7k** de memoria
>   y **4,5k** de *skills*;
> - y en *Memory files*, solo `CLAUDE.md`, con **3,9k** para sus 8.807 bytes, y el índice de la
>   memoria local.
>
> Los **26** imports de la biblioteca, contando el nuevo, suman **200.392** bytes (`wc -c` sobre
> cada uno), unos **89k** tokens con la misma proporción, y no están. El motivo lo da la documentación oficial de
> Claude Code (code.claude.com/docs/en/memory). Un import que sale del directorio del proyecto es
> *externo*, y necesita una aprobación que se pide una sola vez. Sin ella, queda apagado. En este
> proyecto no se aprobó nunca, y el diálogo no llegó a mostrarse:
> `hasClaudeMdExternalIncludesApproved` y `hasClaudeMdExternalIncludesWarningShown` están los dos
> en `false` en `~/.claude.json`.
>
> **Decidido por el usuario, el mismo día: siguen apagados.** Cargarlos costaría unos 89k tokens en
> cada sesión, más de dos veces el arranque entero. Las convenciones se leen bajo demanda, como
> hasta hoy, y los imports se quedan escritos para el día que se aprueben. La tabla de abajo sigue
> diciendo qué se lee al empezar cada fase.
>
> **La fase 3 no tiene import** (2026-10-26): el A.2.3 no tiene fila para ella. Antes del
> primer manejador, en el 3.4, se lee a mano `fundamentos/datos-distribuidos/convenciones.md`,
> que cita el §4 del plan maestro.
>
> **El de la fase 2 ya está puesto** (2026-09-18, al pasar la puerta de clarificación de la
> fase): `negocio/identificacion-articulos/convenciones.md`, ese y solo ese. Con él entra
> `CodigoBarras`, que los ítems de la fase 1 que rozaban el artículo dejaron fuera con ese
> motivo escrito.
>
> **Los dos de la fase 1 ya están puestos** (2026-09-03, al abrir la puerta de clarificación de
> la fase): `herramientas/proteccion-datos.md` y `patrones/soft-delete.md`. El segundo no era
> opcional: la **retirada** del ADR-0023 es un final de vida que **no** es el bloqueo de la R16,
> y las dos van a convivir en las mismas tablas.

| Al empezar la fase | Añadir a `CLAUDE.md` |
|---|---|
| **5 · Facturación** | `@../BibliotecaDocumentacion/negocio/facturacion-espana/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/verifactu/convenciones.md`<br>`@../BibliotecaDocumentacion/negocio/iva-espana/convenciones.md` |
| **6 · Tesorería** | `@../BibliotecaDocumentacion/negocio/pagos-y-cobros/convenciones.md` |
| **7 · Contabilidad** | `@../BibliotecaDocumentacion/negocio/contabilidad/convenciones.md` |

Los `referencia.md` **no se importan nunca**: son para que los lea una persona. Se consultan a mano
cuando hace falta el porqué.

> **Aviso para la fase 5:** `negocio/verifactu/convenciones.md` no es material de consulta, es
> **lectura obligatoria entera antes de la primera línea** de esa fase.

## Notas / riesgos

> **Aquí están las notas abiertas**: las que llevan ABIERTA, ABIERTO o ANOTADO, catorce al
> empezar la fase 3, cada una con su veredicto del cierre de la fase 2. Las cerradas hasta el 3.1
> están en las *Notas / riesgos* de la historia, cada una en la fase de la fecha de su etiqueta, y
> en la de la fase 0 están también las quince de antes de las etiquetas, del 2026-08-25. Tres de
> esas quince miran hacia delante: la cadena de R15 serializa la emisión y se diseña antes de la
> fase 5, el reloj del servidor es corrección fiscal, y el servicio de impuestos de la fase 5 tiene
> su caso dorado en el ADR-0005.
>
> Las cuatro primeras son del 3.1, del orden de trabajo de la puerta de la fase 3, y comparten
> disparador: antes del primer despliegue fuera de local, y en todo caso en la puerta de la
> fase 5. La quinta salió de una tanda del 3.1.
>
> **Las que se cierran durante la fase 3 se quedan aquí**, con CERRADA, la fecha y el ítem que las
> cierra, hasta que la fase se cierre y su historia se mueva, como en las fases de antes. La
> primera es la de la auditoría, que cerró el 3.1.

- **ABIERTA (2026-10-27, ítem 3.1, el orden de trabajo de la puerta de la fase 3) · la API es
  dueña de las tablas, y en el *compose*, superusuaria.** La API y el migrador comparten la cadena
  de conexión, `&entorno-de-la-api` en `deploy/docker-compose.yml`, con
  `Username=${POSTGRES_USER:-bastion}`, que es el usuario que la imagen de PostgreSQL crea como
  superusuario. El migrador crea las tablas, así que las tablas son de ese usuario, y la API, que
  entra con él, puede hacer lo que haga su dueño: `ALTER TABLE … DISABLE TRIGGER` sobre un libro,
  o, como superusuaria, `SET session_replication_role = replica`, que apaga los disparadores de la
  sesión. Los dos caminos se saltan el solo añadido, que hoy guardan dos funciones de disparador,
  `auditoria.registros_son_de_solo_anadido` e `inventario.movimientos_son_de_solo_anadido`. Lo
  dicen `git grep -n "Username=" -- deploy/docker-compose.yml` y
  `git grep -n "_son_de_solo_anadido() RETURNS trigger" -- db/migraciones`. **La propuesta**: dos
  roles, uno dueño para el migrador y otro solo de datos para la API, que no es dueño de nada ni
  superusuario, con los permisos de cada esquema concedidos por el migrador, y un caso de
  integración que afirme que el rol de la API no puede desactivar un disparador de un libro.
  **El disparador**: antes del primer despliegue fuera de local, y en todo caso en la puerta de
  la fase 5.

- **ABIERTA (2026-10-27, ítem 3.1, el orden de trabajo de la puerta de la fase 3) ·
  `Include Error Detail=true` va en la cadena de todos los entornos.** Está en la misma cadena de
  la nota de arriba, y no depende de `ASPNETCORE_ENVIRONMENT`: con él, Npgsql añade al mensaje de
  cada `PostgresException` el detalle del servidor, que puede llevar los valores de la fila que
  la causó, y el mensaje acaba en el registro. Ningún código lo lee:
  `git grep -n -E "\.Detail\b|ErrorDetail" -- 'src/*.cs'` no encuentra nada, y
  `git grep -n "Include Error Detail" -- ':!*.md'` solo lo encuentra en el *compose*. **La
  propuesta**: que salga de la cadena común y lo ponga solo el entorno de desarrollo. **El
  disparador**: antes del primer despliegue fuera de local, y en todo caso en la puerta de la fase
  5.

- **ABIERTA (2026-10-27, ítem 3.1, el orden de trabajo de la puerta de la fase 3) · el bloqueo
  por intentos deja bloquear la cuenta de cualquiera.** `Usuario.IntentosTolerados` son 5, y
  `Usuario.EsperaTrasIntentosFallidos`, 15 minutos (ADR-0008). Quien sepa el correo de alguien
  manda cinco contraseñas falsas a `POST /api/v1/identidad/sesiones` y lo deja fuera un cuarto de
  hora, y otra vez al acabar, sin saber su contraseña. El acceso no tiene limitación de ritmo:
  `git grep -n -E "AddRateLimiter|RequireRateLimiting" -- 'src/*.cs'` no encuentra nada. **La
  propuesta**: un límite de ritmo por origen en el acceso, con el limitador de ASP.NET Core, que
  viene con la plataforma, y decidir con él si el rechazo sigue siendo por cuenta o pasa a ser por
  cuenta y origen. Cambia lo que decide el ADR-0008, así que va con un ADR que lo enmiende. **El
  disparador**: antes del primer despliegue fuera de local, y en todo caso en la puerta de la fase
  5.

- **ABIERTA (2026-10-27, ítem 3.1, el orden de trabajo de la puerta de la fase 3) · las
  contraseñas se resumen con 100 000 iteraciones de PBKDF2, y OWASP pide 210 000.** El ADR-0008 §1
  adoptó el `PasswordHasher` de ASP.NET Core con sus parámetros por omisión, PBKDF2-HMAC-SHA512 y
  100 000 iteraciones, y la hoja de OWASP sobre el almacenamiento de contraseñas pide 210 000 para
  ese PRF. Subirlo no necesita migración: el resumen lleva sus parámetros, y
  `HasherDeContrasenas` ya convierte `SuccessRehashNeeded` en
  `CorrectaPeroConvieneRehashear`, así que cada cuenta se rehace en su siguiente acceso. **La
  propuesta**: `PasswordHasherOptions.IterationCount` a 210 000, con un ADR que enmiende el §1
  del 0008, y medir otra vez lo que cuesta un acceso, que el ADR da en unos 100 ms y paga también
  el que falla, contra el `HashDeRelleno`. **El disparador**: antes del primer despliegue fuera de
  local, y en todo caso en la puerta de la fase 5.

- **ABIERTA (2026-10-27, ítem 3.1, la tanda del ADR-0071) · la anotación del *Estado* no ata cada
  «por el ADR-NNNN» a su verbo.** `UnaEnmiendaSeAnotaEnLosDosAdrTests` da por anotada una
  enmienda cuando la viñeta del *Estado* dice «enmendad» o «ampliad» en algún sitio y «por el
  ADR-NNNN» en cualquier otro: toma todos los números de la viñeta, con `Numeros(PorEl(), vineta)`.
  Lo encontró la primera forma de la 790, verde por diseño: con «corregido por el ADR-0071» en el
  *Estado* del 0036, la regla lo daba por anotado, porque la misma viñeta dice «Ampliado» por el
  0063. Ningún ADR de hoy lo aprovecha. De los 23 *Estado* anotados, ocho nombran a más de un
  autor en cadena, como el del 0044, «Enmendado por el ADR-0046 (…), por el ADR-0059 (…), por el
  ADR-0063 (…) y por el ADR-0067», y en los 23 el último participio delante de cada «por el» es
  «enmendado» o «ampliado». Lo mide `python -X utf8 verbos-del-estado.py <repo>`, del
  *scratchpad*, que limpia la viñeta como `Limpio()`. **La propuesta**: que cada «por el
  ADR-NNNN» cuente solo si el último participio que lo precede es «enmendado» o «ampliado», con la
  primera forma de la 790 como su mutación y las cadenas de hoy como su pareja. No se hace sin
  encargo. **El disparador**: el próximo ADR que enmiende otro, o el repaso del 3.11.

- **ABIERTA (2026-10-24, ítem 2.20, ADR-0070) · el recálculo de C cuesta entre 32 y 53 s según
  cuándo pase el `ANALYZE`.** Medido en el humo del 2.20, con tres recálculos de C sobre la misma
  base: 33,9 s a secas, 32,1 con `VACUUM` entre la vuelta atrás y el avance, y 53,4 con `ANALYZE`.
  Lo mueve la estadística de `instantaneas_mensuales` en el momento del avance, y no las tuplas
  muertas: con el `ANALYZE` tomado cuando la empresa no tiene ninguna instantánea, el plan estima
  una fila donde hay hasta 100 000 (ADR-0070, *Medido*). El recálculo no tiene entrada en
  producción (ADR-0044 §5), así que hoy no lo paga nadie. **La propuesta**: si algún día la tiene,
  que el avance hacia delante analice la tabla después de su primer mes, y medir otra vez las tres
  variantes. No se hace sin encargo. **El disparador**: una entrada de producción para el
  recálculo. En el mismo *Medido* quedan dos cifras sin explicar del todo: los 4 s de las
  mensuales de B que no son del JIT, y lo que se mueve el avance de una ronda a otra.
  **Repasada al cerrar la fase 2: sigue abierta, con su propuesta.** Ningún criterio de la fase 3
  le da al recálculo una entrada en producción, así que el disparador no se acerca.

- **CERRADA (2026-10-27, ítem 3.1, ADR-0045) · la auditoría de la herramienta da dos altas, en
  `vite` y en `undici`.** Medido con `npm --prefix frontend audit --json` sobre `4f6ac53`: 0
  críticas, 2 altas y 2 moderadas, en `@vitest/mocker`, `undici`, `vite` y `vitest`. La alta de
  `vite` es de `launch-editor`, que abre el editor desde el servidor de desarrollo
  (GHSA-587h-7wmj-h5pm), y la de `undici`, que llega por `jsdom`, el de los tests, una denegación
  de servicio (GHSA-xjj6-f33q-929c). Ninguna llega al navegador: la puerta,
  `npm audit --omit=dev --audit-level=high`, sale con 0, y los cinco avisos de `vite` son del
  servidor de desarrollo, así que el disparador de *Lo que no cubre* del ADR-0045 no se cumple.
  **La propuesta**: `npm audit fix` a secas, en su propio commit, porque los cuatro dan
  `fixAvailable` sin versión mayor, y `npm view` publica `vite` 7.3.7, `undici` 7.30.1 y `vitest`
  4.1.12. No lo pide ningún ítem, así que el 2.19 no lo hace. **El disparador**: el cierre de la
  fase 2, que decide si pasa al traspaso; o un aviso de `vite` que afecte a lo que se construye.

  **Medido otra vez sobre `10ed2e8`, en el cierre del 2.20**, con la misma orden: 0 críticas, 2
  altas y 4 moderadas. Las dos nuevas son las del carril del navegador, `@vitest/browser` y
  `@vitest/browser-playwright`, y llegan por los dos avisos de `@vitest/mocker`
  (GHSA-qxg6-89mg-rqv8 y GHSA-xp26-3454-mjgp), que tocan justo a ese modo (ADR-0070, *Lo que no
  cubre*). Los seis paquetes dan `fixAvailable` sin versión mayor, `npm view` publica `vitest` y
  `@vitest/browser-playwright` 4.1.12, y la puerta, `--omit=dev --audit-level=high`, sigue con 0.
  La propuesta es la misma, con el carril nuevo dentro.
  **Repasada al cerrar la fase 2: sigue abierta, y va a la puerta de la fase 3.** El run de `main`
  de `08ea605`, el **40104536177**, dice lo mismo que la medida de arriba: 0 críticas, 2 altas y 4
  moderadas, en seis paquetes de desarrollo, con la puerta en 0. La propuesta es una pregunta de
  la tanda.
  **La cerró el 3.1** (respuesta 12 de la puerta de la fase 3), en `fix(frontal): vitest 4.1.12 y
  vite 7.3.7, y la auditoría a cero` (`5661033`). `npm audit fix` a secas solo subió `vite` y
  `undici`, y los cuatro de `vitest` subieron juntos con un `npm install -D`, porque se piden entre
  sí la versión exacta: el punto 1 de las decisiones del 3.1. Ninguno es versión mayor. Medido con
  la misma orden sobre `c99cd54`: 0 en todas las gravedades, y la puerta, también con 0.

- **ABIERTA (2026-10-24, ítem 2.19, ADR-0069) · un `?q=` a un listado sin filtro declarado llega y
  no filtra nada.** `Paginador.Filtrar` devuelve la consulta tal cual cuando el repositorio no
  declara `Filtro`, y cinco no lo declaran: las líneas de tarifa, las transferencias, los
  ejercicios, los tipos de cambio y las conversiones de unidades. Los 23 listados paginados
  publican `q` en el contrato, porque viene de `ConsultaPaginada`, así que un cliente que lo mande
  a uno de esos cinco recibe la página entera. El frontal no lo hace: esas pantallas no tienen
  buscador. **La propuesta**: que el borde conteste con un `400` cuando el repositorio no declara
  filtro, y que esos cinco dejen de publicar `q`. Cambia el contrato de cinco listados y el
  paginador común, y no lo pide ningún ítem, así que el 2.19 no lo hace. **El disparador**: el
  cierre de la fase 2, que decide si pasa al traspaso; o un cliente de la API que cuente con el
  filtro. Si se hace, el caso que se pone rojo es
  `La_lista_va_de_la_ultima_fecha_de_envio_…_no_trae_la_descartada`, que afirma la página entera.
  **Repasada al cerrar la fase 2: sigue abierta, y va a la puerta de la fase 3**, con su propuesta
  tal cual. Ningún cliente cuenta con el filtro: el frontal no lo manda a ninguno de los cinco.

- **ABIERTA (2026-10-20, respuesta 2 de la puerta del 2.19) · el lote y la serie de una
  transferencia se escriben a mano.** El alta los toma como el ajuste, y que estén en la ubicación
  de origen lo dice el envío, con `transferencia-por-encima-del-disponible`, en la ficha. Elegirlos
  de lo que hay en el origen necesita una lectura que hoy no existe: las existencias por ubicación,
  lote y serie, porque el saldo es por artículo y almacén (`SaldoDto`). Con su permiso y su
  pantalla, es un ítem por sí misma. **El disparador**: la expedición de la fase 4, que elegirá lo
  mismo de lo que hay en una ubicación; o alguien que pida elegir el lote del origen en una
  transferencia antes.
  **Repasada al cerrar la fase 2: sigue abierta, con su disparador.** La devolución de compra de la
  fase 3 la roza, porque saca lo que entró con una recepción, y la puerta pregunta de dónde toma
  su lote y su serie.

- **ABIERTA (2026-10-18, respuesta 4 de la puerta del 2.17) · ningún listado exporta lo que está
  viendo.** El §10 del plan maestro pide que «cada listado exporta a CSV/Excel lo que está viendo,
  con los mismos filtros aplicados», y ninguno lo hace: es una deuda desde la fase 1. La
  importación de terceros lee un CSV, y nada escribe uno. Los listados que no exportan, por su
  ruta, que se listan todas con `git grep -n "ruta: '" -- frontend/src/app/rutas.tsx`:
  - de Organización, `/almacenes` y `/empresas`;
  - de Catálogo, `/articulos`, `/categorias` y `/tarifas`, y los códigos de barras de un artículo,
    en `/articulos/:id/gtin`;
  - de Terceros, `/terceros`;
  - de Inventario, `/recuentos`, las líneas de un recuento en su ficha, `/recuentos/:id`, y
    `/saldos`;
  - y `/ajustes`, el del 2.17, que nace sin exportar por esta misma respuesta.

  **El disparador**: un ítem propio al empezar la fase 3, para todos a la vez, con los mismos
  filtros que cada listado ya lleva en la URL al servidor. No va en el 2.17 ni en ningún otro ítem
  de la fase 2.
  **Repasada al cerrar la fase 2: sigue abierta, y su disparador es ya.** Va en el desglose de la
  puerta de la fase 3, como el primer ítem. A la lista de arriba se suman los dos listados del
  2.19: `/transferencias`, y las líneas de una transferencia en su ficha, `/transferencias/:id`.

- **ABIERTA (2026-10-17, respuesta 7 del repaso) · el editor avisa de que `baseUrl` dejará de
  funcionar.** Lo dan `frontend/tsconfig.app.json` y `frontend/tsconfig.test.json` en el editor,
  cuyo TypeScript es el 6: dice que `baseUrl` deja de funcionar en el 7. El proyecto usa la
  `^5.9.3`, y `typecheck` sale limpio. Lo vio el ítem 14 del 2.14, y lo llevó al repaso. **El
  disparador, decisión del usuario: al subir TypeScript.** El ítem que suba la versión del
  `package.json` quita `baseUrl` o lo sustituye en los dos ficheros, en el mismo commit, y
  `typecheck` lo dice.
  **Repasada al cerrar la fase 2: sigue abierta, con su disparador.** La fase 2 no subió
  TypeScript: `frontend/package.json` sigue en `^5.9.3`.

- **ABIERTA (2026-10-08, encargo del usuario) · el recuento cíclico tiene un segundo disparador, la
  huella.** El ADR-0055 deja el recuento por ubicación para el cierre de la fase con uno: el primer
  almacén que no se pueda contar de una vez. El segundo es la huella, que es la del teórico de todas
  las líneas y, con la precarga, la del almacén entero: en uno con movimiento continuo, cualquier
  entrada o salida de una clave contada entre leer la ficha y confirmar da el `409`
  `recuento-teorico-cambiado`, y el recuento puede no llegar a confirmarse nunca. El primero de los
  dos que se dé abre la pregunta, que no se decide aquí.

  **Contestada el 2026-10-17 (respuesta 3 del repaso): (a), se queda como está.** Abierta con sus
  dos disparadores: el primer almacén que no se pueda contar de una vez, y el primer recuento que
  no llegue a confirmarse por la huella. Los dos están aquí y en el ADR-0055, que recibe el segundo
  por dentro, en su propio commit.
  **Repasada al cerrar la fase 2: sigue abierta, decidida (a).** Ninguno de sus dos disparadores
  se ha dado.

- **ABIERTA (2026-09-27, ítem 2.7) · «hoy» es el día UTC, y no el de la empresa.** Confirmar
  rechaza una fecha de operación posterior a hoy —la decisión (b), ADR-0044 §4—, y el cuadre suma el
  libro hasta hoy. Los dos sacan «hoy» del `TimeProvider` en UTC. En España eso deja dos horas, de
  00:00 a 02:00 en verano (una en invierno), en las que aquí ya ha empezado el día y en UTC todavía
  no: un ajuste fechado hoy a esa hora se rechaza como futuro, con `ajuste-con-fecha-futura`, y se
  confirma sin tocar nada pasadas las dos. **Hoy es una molestia y no una discrepancia**: lo que se
  rechaza no entra en el libro, así que la R3 no se rompe. Toca a la R14: una fecha de negocio no
  lleva zona, y aquí la zona decide qué fecha es hoy. **El disparador:** antes de la **fase 5**,
  porque la fecha de expedición de una factura sí tiene consecuencias fiscales; o antes, en cuanto
  alguien confirme de madrugada. No se decide aquí ni se amplía el checklist por cuenta propia.

  **Corregido el 2026-09-28 (encargo del usuario):** esta nota decía que la salida probable era una
  zona horaria **por empresa**. El usuario lo corrige: la zona es **del sitio, no de la empresa**,
  porque Canarias va una hora por detrás de la península, y una misma empresa puede tener almacenes
  en los dos sitios. Un ajuste de un almacén de Las Palmas a las 23:30 es de hoy allí y ya de mañana
  en Madrid. Es una de las tres preguntas que el usuario deja para el **cierre de la fase**, y sigue
  sin decidir: qué «sitio» es (el almacén, el centro o el terminal) y dónde vive su zona.

  **Añadido el 2026-10-05 (epílogo del 2.11, encargo del usuario): la transferencia es el primer
  documento con dos sitios**, y la respuesta tendrá que decidir también esto. Si cada movimiento
  llevara la fecha de su sitio, un envío desde Madrid a las 00:30 del día 11 (22:30 UTC del 10, en
  verano) y una recepción en Las Palmas a las 23:45 del día 10 (22:45 UTC), un cuarto de hora
  después, chocarían con «la recepción no es anterior al envío»: el `422`
  `transferencia-recepcion-antes-del-envio` rechazaría una llegada que ocurrió después de la
  salida. Con «hoy» en UTC no pasa, porque las dos fechas salen del mismo reloj. Lo que haya que
  decidir —comparar las dos puntas en una sola zona, o comparar instantes y no días— va con la
  pregunta del cierre de la fase, no aquí.

  **Contestada el 2026-10-17 (respuesta 2 del repaso): (a) con (ii).** La zona IANA va en el
  almacén, con `Europe/Madrid` por defecto, y se hace **antes de la fase 5**: ese es el disparador.
  En la transferencia, cada fecha va en la zona de su almacén, y la guarda «la recepción no es
  anterior al envío» compara instantes. Hasta entonces, «hoy» sigue siendo el día UTC, pero de un
  solo sitio en el backend y del servidor en el frontal: es el **2.16**, que escribe aquí también
  qué zona rige para lo que no tiene almacén, las tarifas, los artículos y los terceros.

  **Añadido el 2026-10-18 (ítem 2.16): qué zona rige para lo que no tiene almacén. Propuesta, sin
  decidir.** El día de negocio sale ya de un solo sitio, `DiaDeNegocio`, y es el UTC. De los
  dieciséis que lo piden, doce son de Inventario y tendrán la zona del almacén; los que miran más
  de uno, como el cuadre o el mes del trabajo mensual, lo decidirán con ella. Los otros cuatro no
  tienen almacén, y los cuatro preguntan si algo rige hoy:
  - **las tarifas**: `ResolverPrecio`, sin fecha, elige el tramo con el día de hoy, y la pantalla
    de tarifas dice «Rige hoy» con el mismo;
  - **los artículos**: `CrearArticulo` y `ModificarArticulo` comprueban que el impuesto por
    defecto se sigue ofreciendo hoy para lo nuevo;
  - **los terceros**: `AsignarTarifa` comprueba que la tarifa que se asigna se ofrece hoy.

  Ninguno fija un precio que se cobre: el documento que lo cobre, de la fase 3 en adelante, pasará
  su propia fecha y volverá a preguntar. Las salidas viables son tres:
  - **(i) `Europe/Madrid`, la misma que el almacén por defecto**, escrita una vez en
    `DiaDeNegocio`. Es el día de quien trabaja en la península, y el del almacén que no diga otra
    cosa, así que una tarifa y un ajuste de la misma madrugada dirían el mismo día. En Canarias,
    lo que no tiene almacén iría con el día de Madrid una hora, de las 23:00 a las 24:00 de allí;
  - **(ii) el día UTC, como hoy.** No cambia nada, pero de las 00:00 a las 02:00 de la península
    una tarifa que acabó ayer sigue rigiendo, y, en cuanto el almacén lleve su zona, lo que no
    tiene almacén y lo que sí dirían días distintos a la misma hora;
  - **(iii) la del navegador de quien pregunta.** La descarta el propio 2.16: la etiqueta y el
    precio volverían a decir cosas distintas a la misma hora.

  **Recomendada: (i)**, y con la zona del almacén, antes de la fase 5, no ahora. Cambian dos
  sitios a la vez: `DiaDeNegocio` y `hoyEnElServidor`, que daría el día de Madrid y no el UTC. La
  pareja del 2.16, la pantalla de tarifas y `ResolverPrecio` con el mismo instante, es la que los
  obliga a moverse juntos.

  **Y un `current_date` que no es día de negocio.** La función
  `inventario.asegurar_particiones_de_movimientos` lo usa para decidir desde qué mes mantiene la
  pista de particiones, con la zona de la sesión del migrador. Nada del proyecto la fija, así que
  es la del servidor: `git grep -n -i -E "timezone|PGTZ|\bTZ\b" -- deploy src scripts .github` no
  devuelve nada. Si diera otro día en la frontera de un mes, la pista empezaría un mes antes o
  después, con doce por delante, y una fila de un mes sin partición iría a la partición por
  defecto: ninguna fecha se acepta ni se rechaza por eso. La regla del 2.16 no lo ve, porque es
  SQL, y no le hace falta.

  **Contestada el 2026-10-18 (respuesta 6 de la puerta del 2.17): (i), `Europe/Madrid`**, que se
  aplica junto con la zona de cada almacén, antes de la fase 5: ese sigue siendo el disparador.
  **Va en una sola fuente del servidor, que el frontal recibe**, y no en dos literales en dos
  lenguajes: `DiaDeNegocio` la lee de un solo sitio, y el frontal deja de calcular el día y recibe
  del servidor el que rige. Así la pareja del 2.16 no puede separarse por un literal cambiado en un
  lado y no en el otro.

  **Lo que queda por decidir, y se pregunta cuando toque: ¿una zona por instalación o una por
  empresa?** Pensando en una empresa canaria:
  - **una por instalación** es la más sencilla: una variable de configuración, con
    `Europe/Madrid` por omisión, y una instalación entera en Las Palmas la pone en
    `Atlantic/Canary`. Pero una instalación que lleve a la vez una empresa de la península y una
    canaria daría a la segunda el día de Madrid de las 23:00 a las 24:00 de allí;
  - **una por empresa** lo arregla: la empresa lleva su zona, con `Europe/Madrid` por omisión, y
    lo que no tiene almacén va con la de la empresa del *claim*, que es la que ya llega en cada
    petición (R8). Cuesta una columna y su migración en Organización, y una empresa canaria con un
    almacén en la península tendría dos días a la vez: el de su zona y el del almacén, que es el
    que manda en lo que tiene almacén.

  No se decide aquí. El ítem que haga la zona del almacén lo pregunta en su puerta.
  **Repasada al cerrar la fase 2: sigue abierta, decidida.** Su disparador es antes de la fase 5, y
  la puerta de la fase 3 pregunta si el ítem de la zona del almacén va en la fase 3 o en la 4. Una
  zona por instalación o una por empresa se pregunta en la puerta de ese ítem.

- **ABIERTA (2026-09-18, ítem 2.2) · ¿recibe el artículo su final de vida en esta fase, una vez el 2.7
  le dé existencias?** Es **pregunta del cierre de la fase 2**, y se deja escrita aquí para que lo
  sea. `IConsultaDeArticulos` sale con **tres** valores porque el cuarto —`SoloResuelveLoViejo`— no
  tiene productor: el artículo no se bloquea y no se retira. Y su cabecera no dice que no vaya a
  tenerlo nunca; dice **cuándo tendrá motivo**: «qué le pasa cuando deja de venderse es una pregunta
  de la fase 2, **cuando tenga existencias que sostener**. Hoy no tiene ninguna». Las existencias
  llegan en el **2.7**, o sea que la condición se cumple **dentro de esta fase** —y, repasados los
  catorce ítems, **ninguno le da una baja**: al artículo solo lo tocan el 2.9 (trazabilidad) y el 2.10
  (GTIN). Así que la fase se cierra con el motivo ya existiendo y el mecanismo sin dueño, que es
  justo el hueco que hay que mirar de frente y no heredar en silencio. **Quien la conteste no es el
  agente y no se amplía el checklist por cuenta propia**: si la respuesta es que sí, es un
  **addendum**, la forma que el proyecto ya usó dos veces —las tres addenda de la fase 0 y las tres
  de la fase 1—. Si es que no, el disparador queda escrito y con nombre en `IConsultaDeArticulos`.

  **Añadido el 2026-09-27 (ítem 2.7):** la condición ya se cumple. El artículo tiene existencias,
  en `inventario.existencias`, y la pregunta sigue en pie y sin contestar, para el cierre de la
  fase.

  **Añadido el 2026-09-28 (encargo del usuario):** es una de las tres preguntas que el usuario deja
  escritas para el cierre de la fase, sin decidir. Las otras dos son las pantallas del ajuste y la
  zona de «hoy», en las notas de arriba.

  **Añadido el 2026-10-08 (epílogo del 2.12, revisión de código):** el recuento es el primero que
  la necesita contestada. `AbrirRecuento` pide a Catálogo la unidad base de cada artículo con
  físico, y si falta una, lanza. Hoy no puede faltar, porque el artículo no se retira y el contexto
  de Catálogo solo filtra por empresa. Pero si la respuesta trae una baja que lo saque de
  `UnidadesBaseDeAsync`, un artículo retirado con existencias dejaría sin poder abrir ningún
  recuento de su almacén, que es justo lo que hace falta para sacarlas. La baja tendría que seguir
  contestando su unidad base.

  **Contestada el 2026-10-17 (respuesta 4 del repaso): (a), no en esta fase.** El disparador se
  escribe en `IConsultaDeArticulos`, en su propio commit: la primera línea de un documento de
  compra que elija un artículo, en la fase 3. Y va con la condición de arriba: la baja sigue
  contestando la unidad base. Queda abierta, con la decisión tomada.
  **Repasada al cerrar la fase 2: sigue abierta, decidida (a).** Su disparador llega con la fase 3,
  así que va al desglose de su puerta, con la condición de arriba.

- **ABIERTO A MEDIAS (2026-09-03; el plazo, cerrado en el ítem 1.4) · el bloqueo del art. 32 no
  tenía fecha de vencimiento, y sigue sin proceso de destrucción.** `proteccion-datos.md` entra con
  la fase 1 y es tajante: bloquear es «identificar y reservar» **solo durante el plazo de
  prescripción**, y pasado ese plazo **hay que destruir**. Un
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
  **Repasada al cerrar la fase 1: la destrucción sigue abierta, y eso es la decisión.** El 1.11 deja
  construida la forma —un trabajo de fondo en su módulo, con su ventana y su suceso sin datos:
  `PurgadorDeRecibos`—, pero destruir una ficha no es borrar un recibo: hay que decidir qué pasa con
  lo que la referencia, y a un tercero ya lo referencia `ArticuloProveedor` desde el 1.10. La nota
  emparentada ya no lo es: la de `auditoria.claves_de_idempotencia` se cerró en el 1.11. Necesita su
  sitio en la hoja de ruta, y ese sitio se pregunta, no se le da de paso.
  **Repasada al cerrar la fase 2: la destrucción sigue abierta, y eso es la decisión.** La fase 2
  le añadió un hecho, la decisión 12 de su puerta: el libro no se borra y apunta a lo que mueve,
  así que la destrucción será anonimización. La fase 3 le suma las referencias de un pedido y de
  una recepción a su proveedor. Su sitio en la hoja de ruta va a la puerta de la fase 3.

- **ABIERTO (2026-09-03) · el frontal no tiene forma de aprovechar el listado de lo bloqueado
  mientras la lectura individual no exista.** Decidido en la puerta de la fase 1: el ítem 1.4
  construye **listado y no `GET` individual**, porque el individual emitiría `ETag` y **caducaría
  las cuatro exenciones del ADR-0017**, devolviendo `If-Match` a los cuatro desbloqueos. Es la
  decisión correcta y tiene un precio que conviene tener escrito: desde el listado se puede
  desbloquear —el identificador basta y el desbloqueo no pide etiqueta— pero **no se puede abrir la
  ficha** de lo bloqueado para mirarla antes. Si algún día hiciera falta esa ficha, no es un cambio
  de pantalla: es volver a exigir `If-Match` en cuatro acciones y reescribir el ADR-0017.
  **Repasada al cerrar la fase 1: sigue abierta, y eso es la decisión**, con una cifra corregida:
  las acciones que volverían a exigir `If-Match` ya son **cinco**, porque `TercerosController.Desbloquear`
  entró en la lista de exentas en el 1.5 (`c69e03c`) con el mismo argumento. Nada en la fase 1 ha
  pedido abrir la ficha de lo bloqueado.
  **Repasada al cerrar la fase 2: sigue abierta, y eso es la decisión.** Siguen siendo cinco:
  `git grep -l "public async Task<IActionResult> Desbloquear(" HEAD -- src` encuentra los mismos
  cinco controladores. Ningún criterio de la fase 2 ha pedido abrir la ficha de lo bloqueado.

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
  **Repasada al cerrar la fase 1: sigue abierta, y eso es la decisión.** El generador sigue sin
  aparecer. Y hay un segundo `as` sobre un cuerpo de respuesta desde el 1.5 —`problema as
  CuerpoDeProblema` en `shared/api/errores.ts`, `b7607e0`—, que no dispara la revisión del ADR-0019:
  sus dos campos son `unknown` y cada uno pasa por un `typeof` antes de usarse, así que no afirma
  nada que no se mire, y lleva su test (`ElTextoDeUnFallo.test.tsx`). La que afirma una forma sin
  comprobarla sigue siendo una.
  **Repasada al cerrar la fase 2: sigue abierta, y eso es la decisión.** Los dos `as` son los de
  siempre, `errores.ts:135` y `traduccion.ts:43`, contados con
  `grep -rn " as [A-Z]" frontend/src/shared/api frontend/src/shared/sesion`, sin los tests.
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
  **Repasada al cerrar la fase 1: sigue abierta, y eso es la decisión.** El documento lleva hoy
  **106** `"type": ["integer", "string"]` y **14** `["number", "string"]`, contados con
  `re.findall(r'"type":\s*\[\s*"integer",\s*"string"\s*\]', texto)` sobre `docs/api/openapi.json`
  (y lo mismo con `number`); la traducción sigue en `shared/api/enteros.ts`, en un solo sitio.
  **Corregida el 2026-10-17, en el 2.15: la expresión de arriba no veía los nulables.** Busca la
  lista `["integer", "string"]` tal cual, y un entero que admite `null` sale como
  `["null", "integer", "string"]`; con `number`, igual. Al cerrar la fase 1 se quedaron fuera
  **4** enteros y **12** números nulables, y hoy serían **11** y **20**. La cuenta corregida
  recorre el JSON en vez de buscar texto: `object_hook` ve cada objeto del documento, y cada `type`
  que es una lista se cuenta como un conjunto, sin depender del orden ni del formato.

  ```bash
  contar='import json, sys, collections; cuenta = collections.Counter(); (
      json.load(sys.stdin, object_hook=lambda d: d if not isinstance(d.get("type"), list)
                else cuenta.update([" | ".join(sorted(d["type"]))]) or d),
      print(*cuenta.most_common(), sep="\n"))'
  python -X utf8 -c "$contar" < docs/api/openapi.json                    # hoy
  git show 36fc437:docs/api/openapi.json | python -X utf8 -c "$contar"   # al cerrar la fase 1
  grep -cE '"type": *"(integer|number)"' docs/api/openapi.json           # 0
  grep -cE '"type": *"string"' docs/api/openapi.json                     # 489, su pareja
  ```

  | Forma del `type` | Al cerrar la fase 1 (`36fc437`) | Hoy (`dba3653`) |
  |---|---|---|
  | `integer \| string` | 106 | 127 |
  | `integer \| null \| string` | 4 | 11 |
  | `number \| string` | 14 | 18 |
  | `null \| number \| string` | 12 | 20 |
  | `null \| string` | 72 | 89 |
  | **Todas las listas** | **208** | **265** |

  Enteros: **110** al cerrar la fase 1 y **138** hoy. Números: **26** y **38**. Las dos filas sin
  `null` repiten las cifras de la expresión vieja, 106 y 14 entonces y 127 y 18 hoy, así que la
  cuenta nueva contiene a la vieja y no la contradice. Y ningún entero se queda fuera de las
  formas de la tabla: el primer `grep` dice que el documento no tiene ni un `integer` ni un
  `number` escalar, y el segundo, que ese mismo patrón sí encuentra los escalares que existen. La
  nota sigue abierta, y la decisión no cambia.
  **Repasada al cerrar la fase 2: sigue abierta, y la decisión no cambia.** Con la cuenta de arriba
  sobre `08ea605`, las cinco filas de la tabla dan 139, 15, 26, 24 y 104, **308** listas: **154**
  enteros y **50** números. El primer `grep` sigue en 0, y el segundo da 550.
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
  **Repasada al cerrar la fase 1: igual.** La máquina de desarrollo sigue en Node **25.6.1**
  (`node --version`) y la CI en **22** (`node-version: '22'` en `ci.yml`), así que el remiendo de
  `setupTests.ts` sigue haciendo falta aquí.
  **Repasada al cerrar la fase 2: igual.** Node **25.6.1** aquí (`node --version`) y **22** en la
  CI (`node-version: '22'` en `ci.yml`).
