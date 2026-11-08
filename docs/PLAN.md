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

### Los ítems cerrados de la fase 3, y dónde está cada uno

> **Desde el epílogo del 3.4 (2026-11-04, punto B8), lo largo de cada ítem cerrado está en
> [`docs/historia/fase-3.md`](historia/fase-3.md)**: su puerta contestada, lo que el agente decidió
> al escribirlo y al medirlo, con sus tandas, y su entrada de *Estado actual*, con sus commits, sus
> runs, su batería y su humo. Se movió tal cual, y cada ítem que cierre se mueve en su commit de
> cierre. El checklist y las notas abiertas se quedan aquí hasta que la fase se cierre. Una cita de
> «*Decisiones tomadas* → *La puerta del 3.N, contestada*», de «→ *La pregunta del apóstrofo*» o
> de «→ *Tomadas por el agente de desarrollo — ítem 3.N*», en el PLAN, en el código o en un ADR,
> se resuelve en ese fichero, igual que una de «*Estado actual* → el 3.N, hecho».

| Ítem | Rama, abierta desde | ADR | Mutaciones | Cierre, run de la rama y run de `main` |
|---|---|---|---|---|
| 3.1 · La casa en orden | `3.1-la-casa-en-orden`, `ec023f5` | 0071 | 745–791 | `eef05f7`, 40449192600 y 40452123280 |
| 3.2 · La exportación | `3.2-la-exportacion`, `eef05f7` | del 0072 al 0075 | 792–971 | `714cc6e`, 41399969691 y 41401353186 |
| 3.3 · Los puertos, con el tipo de la tarifa | `3.3-los-puertos`, `714cc6e` | 0076 y 0077 | 972–1077 | `3af3039`, 41482866156 y 41486166333 |
| 3.4 · La bandeja, lista para su primer manejador | `3.4-la-bandeja`, `3af3039` | 0078 | 1078–1125 | `c2ce759`, 41566323913 y 41568164543 |

Los bloques, en el orden en que están en la historia:

| Bloque | Sección |
|---|---|
| [Tomadas por el agente de desarrollo — ítem 3.1, al escribirlo y al medirlo (2026-10-27)](historia/fase-3.md#tomadas-por-el-agente-de-desarrollo--ítem-31-al-escribirlo-y-al-medirlo-2026-10-27) | *Decisiones tomadas* |
| [La puerta del 3.2, contestada (2026-10-27)](historia/fase-3.md#la-puerta-del-32-contestada-2026-10-27) | *Decisiones tomadas* |
| [La pregunta del apóstrofo, contestada (2026-10-27)](historia/fase-3.md#la-pregunta-del-apóstrofo-contestada-2026-10-27) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 3.2, al escribirlo y al medirlo (2026-10-28)](historia/fase-3.md#tomadas-por-el-agente-de-desarrollo--ítem-32-al-escribirlo-y-al-medirlo-2026-10-28) | *Decisiones tomadas* |
| [La puerta del 3.3, contestada (2026-11-02)](historia/fase-3.md#la-puerta-del-33-contestada-2026-11-02) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 3.3, al escribirlo y al medirlo (2026-11-02)](historia/fase-3.md#tomadas-por-el-agente-de-desarrollo--ítem-33-al-escribirlo-y-al-medirlo-2026-11-02) | *Decisiones tomadas* |
| [La puerta del 3.4, contestada (2026-11-02)](historia/fase-3.md#la-puerta-del-34-contestada-2026-11-02) | *Decisiones tomadas* |
| [Tomadas por el agente de desarrollo — ítem 3.4, al escribirlo y al medirlo (2026-11-03)](historia/fase-3.md#tomadas-por-el-agente-de-desarrollo--ítem-34-al-escribirlo-y-al-medirlo-2026-11-03) | *Decisiones tomadas* |
| [Estado actual](historia/fase-3.md#estado-actual): el 3.4, el 3.3, el 3.2 y el 3.1, por este orden | *Estado actual* |

**Lo que sigue abierto de esos cuatro**, dicho en sus bloques, y dónde se recoge:

- **la pantalla de alta de tarifas y la ficha del tercero**, los puntos 4 y 5 del 3.3, van a la
  puerta del 3.7, por la parte D del encargo del 2026-11-04;
- **ninguna regla mira qué interceptores lleva un contexto**, el punto 13 del 3.3, y **la lista de
  contextos tecleada en el paso del humo**, lo que queda del punto 14 tras el B5, van al repaso
  del 3.11, en su casilla;
- **`IUsuarioActual` sin token dentro de un manejador, y el choque que un caso de uso convierte en
  un `Resultado`**, del punto 13 del 3.4 y de *Lo que no cubre* del ADR-0078, los decide la puerta
  del 3.8, en su casilla. Del mismo punto, el `EventId` 8200 lo cerró el B2, y el arnés que pedía
  el punto 4, el B4;
- **la retención de la bandeja**, también de *Lo que no cubre* del ADR-0078, no tiene casilla: se
  propone en el informe del epílogo;
- y las notas abiertas que salieron de ellos siguen en *Notas / riesgos*.

### Traídas por el encargo del 2026-11-04 — el epílogo del 3.4, el 3.5 y el 3.6

#### Lo que el informe del 3.4 no decía, y va arriba

Lo trae el usuario. Cada cifra, con la orden que la mide:

1. **`CLAUDE.md` seguía en la fase 2.** Se aplazó el 2026-10-27, con el PLAN partido ya en `main`
   «a mitad de una sesión», y desde entonces se cerraron tres ítems y pasaron varias sesiones con
   un §5 que decía otra fase. `git show c2ce759:CLAUDE.md | grep -n "Objetivo de este encargo"`
   da la fase 2. Se editó lo primero de esta sesión, en `dfd8784`.
2. **El PLAN ha vuelto a crecer**: de **103 376** bytes en `73adc0e`, el commit que lo partió, a
   **283 025** en `c2ce759`; son 179 649 más en cuatro ítems, unos **45 KB por ítem**:
   `for c in 73adc0e eef05f7 c2ce759; do git show $c:docs/PLAN.md | wc -c; done` da 103376,
   132624 y 283025. Lo arregla el punto B8.
3. **La auditoría informativa vuelve a dar 16 moderadas**, con el *lockfile* sin tocar desde
   `5661033`, el commit del 3.1 que la dejó a cero: `git log -1 --format=%h --
   frontend/package-lock.json` da `5661033`, y `npm audit --json` en `frontend/` da 0 críticas, 0
   altas, 16 moderadas y 0 bajas, todas por `minimatch` y todas de herramientas de desarrollo:
   `eslint`, `typescript-eslint`, `openapi-typescript` y lo que cuelga de ellas. La de producción,
   `npm audit --omit=dev --audit-level=high`, sale con 0. Lo que cambió es el aviso publicado, no
   el árbol. Lo mira el punto B7.
4. **La comprobación nueva del humo solo vive en el *scratchpad***, en `humo34.sh`: la CI no mira
   la bandeja. `git grep -n "LaCausaDeCadaEvento\|bandeja" -- .github scripts` no da nada. Lo
   arregla el punto B5.

#### El tramo, verificado desde fuera

- **55** commits desde `ec023f5`, todos firmados y sin *trailers*: `git log --oneline
  ec023f5..c2ce759 | wc -l` da 55;
- **48** runs, del 315 al 362, todos en verde;
- `main` avanzó cuatro veces, y siempre a un commit que ya estaba en verde en su rama;
- las cifras, iguales a las del *runner*: **1733** y **806** casos, **173** operaciones en
  **109** rutas, **245** tipos de **253** sitios, **428** casos de jsdom y **22** en Chromium,
  **408/450** y **175/192** KiB, y **116** permisos;
- y el diagnóstico del ADR-0078, correcto.

**Faltaban por anotar los dos runs del cierre del 3.4**, el **41566323913**, de la rama, y el
**41568164543**, de `main`. Van en *Estado actual → El 3.4, hecho*, punto 11. **El próximo informe
cuenta desde `c2ce759`.** La siguiente mutación es la **1126**, y el siguiente ADR, el **0079**. El
arnés está al día desde `dbf2664`, y `arnes-de-agente.md` se lee al empezar cada sesión. La
biblioteca no ha cambiado desde `3b45c6b`.

#### Las tres respuestas

1. **El 3.5 empieza después del epílogo del 3.4**, el punto B. Sigue el 3.6, y **se para en la
   puerta del 3.7**.
2. **El ADR-0078 se corrige en su sitio, por encargo del usuario**, como el ADR-0044 §5:
   - su *Estado* lo declara, con el commit;
   - el punto lleva la nota «> **Corregido el …, por encargo del usuario.**»;
   - el mensaje del commit dice qué cambió y por qué;
   - y `5ce629d` no se toca.
3. **La nota del *Estado* se implementa como se propuso** en la nota abierta del 3.1: cada «por el
   ADR-NNNN» cuenta solo si el último participio que lo precede es «enmendado» o «ampliado». La
   790, en su primera forma, es la mutación, y las cadenas de hoy son la pareja.

#### El orden de trabajo, decisión del usuario

**A. Lo primero de la sesión, `CLAUDE.md`**, en un commit propio y en su propia subida: el §5 con la
respuesta 14 de la puerta de la fase 3, y las líneas del §2 y del §4 que mandan leer el PLAN,
descritas como el PLAN vigente más `docs/historia/`, que se consulta cuando haga falta. No se aplaza
otra vez: se acepta romper la caché una vez.

**B. El epílogo del 3.4**, en su rama y un tema por subida:

1. **El primer commit** lleva los dos runs anotados, estas respuestas, las casillas del epílogo y
   la línea del README.
2. **El `EventId` 8200, renumerado.** El del despachador pasa a un número libre de su tramo. Y una
   regla del carril rápido recorre por reflexión cada `[LoggerMessage]` de los ensamblados de
   Bastion, afirma que encontró más de N y que ningún número se repite. Su pareja es un duplicado
   de verdad que la regla tiene que marcar, y una mutación que vuelve a duplicar.
3. **El ADR-0078, corregido en su sitio**, con la respuesta 2. En el mismo commit, la viñeta del
   8200 de *Lo que no cubre* lleva una nota de que está resuelta, citando el commit del punto 2.
4. **Un arnés que hace fallar el guardado de la marca**: un interceptor de EF Core, solo de test,
   que falla una vez al guardar la marca. El caso afirma que la fila no queda publicada, que queda
   en error en el acto y que la vuelta siguiente la resuelve. La 1125 tiene que salir roja, y una
   mutación sobre el arnés que no falla nunca tiene que hacer que el caso diga que no inyectó nada.
5. **El Humo de la CI**:
   - los tres `echo "$REGISTRO" | grep -q` de `ci.yml` pasan a un fichero de registro, y el `grep`,
     sobre el fichero: la regla del 1.12, por el `pipefail`, y el registro del migrador es grande;
   - la comprobación de la bandeja de `humo34.sh` pasa a `scripts/ci`: `LaCausaDeCadaEvento` una
     vez en el historial, la fila de la semilla publicada sin empresa y con su correlación, y 0
     aparcados;
   - con su mutación, y `AGENTS.md` al día, porque se toca el *workflow*.
6. **La regla de la nota del *Estado***, con la respuesta 3.
7. **`npm audit fix`, sin `--force`.** Si resuelve sin subir de mayor, va en su commit con la
   batería. Si no, el PLAN dice qué aviso, en qué herramienta y por qué se queda.
8. **El PLAN deja de crecer**:
   - al cerrar cada ítem, en el commit de cierre, su «La puerta del 3.N, contestada», su «Tomadas
     por el agente — ítem 3.N» y su entrada larga de *Estado actual* se mueven tal cual a
     `docs/historia/fase-3.md`;
   - el PLAN se queda con unas líneas por ítem, el enlace y lo que sigue abierto; el checklist y
     las notas abiertas no se mueven;
   - se aplica ya al 3.1–3.4; los nombres viejos de las filas 835–848 y 904/912 se anotan con los
     de hoy al moverlos, sin reescribirlos;
   - se ajusta la regla de `AGENTS.md`, y `ElEstadoDelReadmeEsElDelPlanTests` se adapta sin
     debilitarlo;
   - se mide el tamaño antes y después, con la orden;
   - **si algo no se puede mover sin perder información, se para y se pregunta.**

El epílogo se cierra por el flujo de siempre: verde en su rama, `main`, y la rama borrada.

**C. El 3.5, la zona, y el 3.6, la retirada del artículo**, cada uno con el criterio de su casilla.
La zona sigue la respuesta 11 de la puerta de la fase 3:

- una zona por empresa, `Europe/Madrid` por omisión; el almacén la hereda y la puede cambiar;
- en la transferencia, cada fecha va en la zona de su almacén, y la guarda compara instantes;
- y las diez llamadas del frontal, incluidas.

El día de negocio deja de ser el del UTC, así que **el ADR que lo fijó se enmienda desde el ADR de
la zona, en los dos lados**. El test del IL sigue exigiendo una sola fuente de «hoy». El ADR dice
qué pasa con lo ya escrito: no hay datos reales, así que no hay *backfill*, pero se dice. **Si
cambiar la zona de un almacén que ya tiene movimientos admite varias salidas, se pregunta en la
puerta del 3.5.**

**D. Se para en la puerta del 3.7.** La puerta lleva, además de lo que se encuentre:

- **las pantallas de la tarifa** —el alta y la edición, con el tipo y la divisa— y **la ficha del
  tercero con sus dos tarifas**, dentro del 3.7 y antes de la pantalla del pedido. Es la
  recomendación del usuario; si hay otra forma viable, se propone;
- y la inscripción de Compras en `IDocumentosDeUnPeriodo`.

Y dos encargos para casillas que vienen después:

- **la casilla del 3.8 dice que la puerta de la recepción decide, por su nombre, los dos puntos de
  *Lo que no cubre* del ADR-0078**: `IUsuarioActual` sin token dentro de un manejador, y el choque
  de versión que un caso de uso convierte en un `Resultado`;
- **el repaso del 3.11 lleva tres cosas más**: la regla de qué interceptores lleva cada contexto,
  con su pareja; las parejas de cada foto de la exportación, contadas una sola vez (la nota de los
  78–86 s); y la propuesta 14 del 3.3, si el B5 no la cubre entera.

#### Las herramientas, y cómo se trabaja

- **Los servidores de lenguaje**: se busca por símbolo antes de leer, y se abren los diagnósticos
  de cada edición. Un diagnóstico limpio no sustituye al build ni a los tests.
- **`playwright-cli`**, para las pantallas, contra la pila local en su propio proyecto de
  *compose*.
- **Context7 (`ctx7`)**, solo para APIs de `TimeZoneInfo`, de Npgsql o de EF Core que no cuadren,
  y contrastado con la documentación oficial.
- **El método de siempre**: la tanda se escribe antes de medir, con una mutación sobre el arnés;
  las dos listas, por nombre; commits firmados, un tema por subida, esperando a la conclusión de la
  anterior; el humo, si hay migración, pantalla o *workflow*; `main` solo avanza a un commit que ya
  está en verde en su rama, y la rama se borra después de su run de `main`; y la unidad se cierra
  antes de una pausa larga.
- **El informe** cuenta todos los commits desde `c2ce759`, cada uno con su asunto y con sus runs.
  Lo encontrado por el camino va arriba, y cada cifra va con la orden que la mide. Si una cifra del
  run cambia respecto al cierre anterior, se dice, y también la de la auditoría informativa, aunque
  no se haya tocado nada.

#### Lo que el agente decide sin preguntar, al anotarlo

1. **La rama del epílogo es `3.4-el-epilogo`**, abierta desde `main` en `c2ce759`. `CLAUDE.md` va
   en su primer commit, `dfd8784`, y su primera subida.
2. **El epílogo no es un ítem del desglose**: sus casillas van debajo de la del 3.4, sangradas, y
   no cuentan en la línea del README, que sigue en «4 de 11» y dice en su prosa que el epílogo está
   en curso. Así `ElEstadoDelReadmeEsElDelPlanTests` sigue contando los once ítems de la puerta.
3. **Los dos encargos de casilla** de la parte D se escriben ya en las casillas del 3.8 y del 3.11,
   en este commit, para que no dependan de esta sección. Lo del 3.7 se queda aquí, porque lo
   decide su puerta.

#### Lo que encontró el epílogo, tema a tema

**B2 · el `EventId` 8200, renumerado** (`6396114`). El despachador lo estrenó el 2026-08-29 para
un evento que un consumidor ya había procesado, y dos días después `AccesoALoBloqueado` tomó el
mismo número: un panel que filtrara uno traía los dos. El del despachador pasa al **8202**, el
siguiente libre de su tramo, y `AccesoALoBloqueado` se queda con el 8200. El generador de los
registros solo avisa de un número repetido dentro de una misma clase (SYSLIB1006), así que entre
dos clases no lo miraba nadie. Ahora lo mira `ElNumeroDeUnRegistroEsSuyoYDeNadieMasTests`, en el
carril rápido: recorre por reflexión cada `[LoggerMessage]` de los ensamblados de Bastion, afirma
que encuentra más de veinte con número —hoy, veintiséis— y que ningún número lo llevan dos. Su
pareja son dos registros de verdad, compilados por el generador en dos clases de la propia regla
con el mismo número. Antes del arreglo, la regla dio rojo con «8200: AccesoALoBloqueado.Anotar,
DespachadorDeEventos.Repetido».

**Su tanda, de la 1126 a la 1129**, sobre `6396114`, en el carril de arquitectura
(`Arquitectura.Tests`, `--no-build`), con `python -X utf8 mutar40.py base 1126-1129`, del
*scratchpad*. La base, 0 rojos de 70:

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1126 | El despachador, otra vez en el 8200 | `Ningun_EventId_lo_llevan_dos_registros` |
| 1127 | **Arnés**: el barrido, sin los métodos no públicos | `El_barrido_encuentra_los_registros_numerados_de_Bastion` y `Un_numero_repetido_entre_dos_clases_se_marca` |
| 1128 | **Arnés**: el barrido, sin el bloque común, solo los módulos y la API | `El_barrido_encuentra_los_registros_numerados_de_Bastion` |
| 1129 | Un repetido pide tres: `Count() > 2` | `Un_numero_repetido_entre_dos_clases_se_marca` |

**B3 · el ADR-0078, corregido en su sitio** (`04edd93` y `43e02f8`), con la respuesta 2. El último
párrafo de «Cómo se prueba» decía que en el proceso de los tests de integración estaba también el
host de la API, con su propio publicador, y no estaba: lo encontró la 1124, verde en la tanda del
cierre del 3.4, y `3f7d6e6` ya había hecho que el caso pusiera él mismo el medidor de antes. El
párrafo lleva delante la nota «Corregido el 2026-11-04, por encargo del usuario», con lo que decía
antes y quién lo encontró, y el *Estado* lo declara con el commit; el hash va en `43e02f8`, porque
un commit no puede llevar el suyo. En *Lo que no cubre*, la viñeta del 8200 lleva la nota de que lo
resolvió `6396114`. `5ce629d` no se toca. Sin tanda: solo cambia texto.

**B4 · el fallo al guardar la marca, inyectado** (`53fe640`). La 1125 marca la fila en el contexto
de la vuelta, y salió verde en la tanda del cierre del 3.4 porque ningún caso sabía hacer fallar
ese guardado. Ahora lo sabe `UnaMarcaQueNoSeGuardaTests`. Su arnés es un interceptor del contexto
de la cola, solo de test, que lanza una `DbUpdateException` una vez, y solo en el guardado que
lleva la fila marcada como publicada; `BandejaDeVerdad` lo acepta como parámetro opcional, y sin
él se monta como antes. El caso afirma antes que nada que el arnés inyectó el fallo; después, que
la fila sigue pendiente, sin fecha de publicación, con un intento y con el error del arnés; y que
un segundo publicador la publica en su primera vuelta, con el error limpio y el efecto del
manejador una sola vez.

**Su tanda, la 1125 y la 1130**, sobre `53fe640`, en el carril de integración
(`Api.IntegrationTests`, filtrado a `BandejaDeSalida`, `--no-build`), con
`python -X utf8 mutar41.py base 1125 1130`. La base, 0 rojos de 44:

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1125 | La marca, en el contexto de la vuelta | `UnaMarcaQueNoSeGuardaTests.La_fila_sigue_pendiente_con_su_error_y_la_vuelta_siguiente_la_publica`: «fila.Estado should be Pendiente but was Publicado» |
| 1130 | **Arnés**: el interceptor busca una fila aparcada, y no lanza nunca | el mismo caso, por su primer aserto: «arnes.Inyectados should be 1 but was 0» |

La 1125, verde en la tanda del 3.4, sale roja: el hallazgo de aquella tanda queda cubierto.

**B5 · el Humo de la CI.** El registro del migrador va a `migraciones.log`, los tres `grep -q` lo
leen, y el fichero entra en el *Diagnóstico*. La bandeja del primer arranque pasa a
`scripts/ci/la-bandeja-del-arranque.sh`, un paso del job Humo entre el migrador y el segundo
arranque, con lo que afirma y por qué en su cabecera. `AGENTS.md` lo pone en el humo local, antes
de `segundo-arranque.sh`. Dos hallazgos, y el primero cambia cómo se leía el 3.3:

1. **El paso del migrador no tenía `pipefail`**, y el punto 14 del 3.3 decía que sí. Un `run` sin
   `shell:` corre con `bash -e {0}`, que no lo pone (la sintaxis de los *workflows* de GitHub,
   `jobs.<job_id>.steps[*].shell`), y `ci.yml` no cambia el intérprete en ningún sitio:
   `grep -n "shell:" .github/workflows/ci.yml` no da nada, y su único `defaults:`, el del job
   Frontal, solo pone el `working-directory`. El propio `ci.yml` ya lo decía, en el paso de los
   tests del frontal. Así que el SIGPIPE del 1.12 no podía poner ese paso en rojo; bastaba con que
   alguien añadiera el `pipefail` arriba. El texto del 3.3 no se reescribe.
2. **La columna del historial es `migration_id`, y no `"MigrationId"`**: la convención de nombres
   del contexto renombra también las columnas del historial, no solo la tabla. Lo encontró el
   primer humo del guion, con `column "MigrationId" does not exist`, y ningún test consulta esa
   columna. Lo dice la cabecera del guion, junto a la consulta.

**La propuesta 14 del 3.3 queda a medias**: el `grep` lee un fichero, pero la lista de los siete
contextos sigue tecleada en `ci.yml`, y nada la compara con el disco. Esa mitad va al repaso del
3.11, como dice su casilla.

**Su tanda, de la 1131 a la 1134, sobre el humo local**: `humo35.sh`, del *scratchpad*, en el
proyecto `bastion-humo-35`, con su propia copia del entorno, borrada al acabar. Cada mutación se
hace sobre la base del primer arranque, se lanza el guion de la CI, y se deshace antes de la
siguiente. Después, un control: la fila de la semilla, otra vez pendiente, que el publicador vuelve
a publicar, y el guion sale con 0.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1131 | **Arnés**: el estado de la semilla, `'Publicada'`, con una letra cambiada en el guion | **el guion**: «La fila de la semilla (…) está 0 veces, y tiene que estar una» |
| 1132 | La fila de la semilla, sin su correlación | **el guion**: «La fila de la semilla (…) está 0 veces, y tiene que estar una» |
| 1133 | Una fila aparcada en la cola | **el guion**: «Hay 1 sin publicar en la cola, de ellas 1 aparcadas, tras esperar hasta 60 s» |
| 1134 | La migración, fuera del historial de Auditoría | **el guion**: «La migración 20261102183738_LaCausaDeCadaEvento está 0 veces en el historial de Auditoría» |

El control salió verde en la segunda mirada, y el segundo arranque, detrás, también: el humo
acabó con `FALLOS: 0`. La plataforma de la CI la verifica el run de este commit, con el paso nuevo
en Linux.

**B6 · cada «por el ADR-NNNN», atado a su verbo**, con la respuesta 3. En
`UnaEnmiendaSeAnotaEnLosDosAdrTests`, los autores de un *Estado* salen de `Autores()`: un «por el
ADR-NNNN» cuenta si el último participio que lo precede es «enmendado» o «ampliado». El
participio es una palabra de tres letras al menos y `-ado` o `-ido`, para que «cada» y «nada» no
cuenten; uno de más, como «Estado», solo puede dejar un autor sin contar, que es un rojo y no un
verde. El caso sintético suma una viñeta con «corregido por el ADR-0003» detrás de un
«Enmendado», y `El_barrido…` afirma la cadena del 0044 entera entre las anotadas. Cierra la nota
abierta del 3.1. Su tanda se mide sobre su commit, y va aquí en el cierre del epílogo.

**La tanda del B6, de la 1135 a la 1139**, sobre `3572b3a`, en el carril de arquitectura, con
`python -X utf8 mutar42.py base 1135-1139`. La base, 0 rojos de 70:

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1135 | La 790 en su primera forma: «corregido por el ADR-0071» en el *Estado* del 0036, que dice «Ampliado» por el 0063 | `El_enmendado_anota_en_su_estado_quien_lo_enmienda` |
| 1136 | Cuenta todos los «por el», como antes: `Numeros(PorEl(), vineta)` | `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 1137 | Demasiado estricta: el participio, solo en los doce caracteres de delante | `El_barrido_ve_las_enmiendas_y_la_pareja_anotada_en_los_dos_lados`, por la pareja: «la cadena del ADR-0044 ha dejado de leerse entera»; y `El_enmendado_anota_en_su_estado_quien_lo_enmienda` |
| 1138 | El participio, sin «-ido»: «corregido» deja de ser verbo, y el «Enmendado» de delante cuenta por él | `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 1139 | **Arnés**: el participio, en mayúsculas (`\p{Lu}`), no casa nunca | `El_barrido_…` («solo se han leído 0 anotaciones»), `Cada_enmienda_nombra_lo_que_enmienda`, `El_enmendado_anota_…` y `Una_enmienda_…` |

**B7 · `npm audit fix`, sin `--force`: no resuelve, y no se commitea.** Sobre `3572b3a`, con el
*lockfile* de `5661033`, movió quince versiones dentro de sus rangos —`eslint` de 10.11.0 a
10.14.0, `typescript-eslint` y sus diez `@typescript-eslint/*` de 8.70.1 a 8.72.1,
`@redocly/openapi-core` de 1.34.20 a 1.34.23, y dos de sus dependencias— y la auditoría siguió
en **16 moderadas**. El encargo pedía el commit solo si resolvía, así que el *lockfile* volvió a
`HEAD` y `npm ci` dejó `node_modules` como estaba. El aviso, las herramientas y por qué se
queda, en la nota abierta de *Notas / riesgos* que empieza «la auditoría informativa da 16
moderadas».

**B8 · el PLAN deja de crecer.** Lo largo del 3.1 al 3.4 —sus ocho bloques de *Decisiones
tomadas* y sus cuatro entradas de *Estado actual*— está en `docs/historia/fase-3.md`, movido tal
cual, y en su sitio queda *Los ítems cerrados de la fase 3*: una fila por ítem, el enlace a cada
bloque y lo que sigue abierto. La historia solo añade su cabecera y una nota tras la tabla de las
tandas del 3.2, con los nombres de hoy de las filas de la 835 a la 848, la 904 y la 912, que se
dejan como estaban. Ningún bloque movido lleva un enlace, así que ninguno cambia. Lo comprueba
`python -X utf8 scripts/comprobar-el-plan-partido.py b79d0bf --disco`, sobre el árbol del B8:
VERDE. El tamaño, con `wc -c docs/PLAN.md` y `wc -l`: de 302 343 bytes en 3283 líneas, en
`b79d0bf`, a 150 461 bytes en 1895 líneas; y la historia de la fase 3, 160 013 bytes en 1489 líneas.
`ElEstadoDelReadmeEsElDelPlanTests` admite la historia de la fase vigente, y gana
`Lo_largo_de_cada_item_cerrado_esta_en_la_historia_y_no_en_el_plan`: lo largo de un ítem con la
casilla marcada no se queda en el PLAN; la historia de la vigente lleva algo de cada ítem cerrado,
y de ninguno abierto; y solo lleva *Decisiones tomadas* y *Estado actual*. La regla de `AGENTS.md`
dice cuándo se mueve y cómo se comprueba. La casilla se marca en el cierre del epílogo, para que
este commit sea solo el movimiento y la comprobación salga VERDE también sobre él; su tanda va
allí.

**La tanda del B8, de la 1140 a la 1150**, sobre `f25087f`, en el carril de arquitectura, con
`python -X utf8 mutar43.py base 1140-1150`. La base, 0 rojos de 71, y cada mutación, 1 de 71: el
caso que esperaba. Todas las ve `Lo_largo_de_cada_item_cerrado_esta_en_la_historia_y_no_en_el_plan`,
menos la 1146:

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1140 | El PLAN vuelve a llevar «La puerta del 3.4, contestada» | `Lo_largo_…` |
| 1141 | La historia de la vigente gana un checklist | `Lo_largo_…`, y no `Cada_fase_cerrada_…` |
| 1142 | La historia de la vigente gana sus notas | `Lo_largo_…` |
| 1143 | La historia no lleva ningún bloque del 3.1, que tiene la casilla marcada | `Lo_largo_…` |
| 1144 | La historia lleva un bloque del 3.5, que está abierto | `Lo_largo_…` |
| 1145 | **Arnés**: el reconocedor de los bloques no casa nunca | `Lo_largo_…`: ningún ítem cerrado tiene bloques |
| 1146 | La vigente vuelve a contar como cerrada, como antes del B8 | `Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero` |
| 1147 | El ítem se lee del grupo de la fase: todos son el 3 | `Lo_largo_…` |
| 1148 | Sin el filtro de las marcadas: del 3.5 al 3.11 contarían como cerrados | `Lo_largo_…` |
| 1149 | El PLAN vuelve a llevar la entrada «El 3.4, hecho el» | `Lo_largo_…` |
| 1150 | El PLAN vuelve a llevar «Tomadas por el agente de desarrollo — ítem 3.2» | `Lo_largo_…` |

Cada alternativa del reconocedor tiene su mutación en el PLAN: la puerta, la 1140; lo que tomó el
agente, la 1150; y la entrada de *Estado actual*, la 1149. Y la comprobación, sobre el commit y no
solo sobre el disco, con `python -X utf8 scripts/comprobar-el-plan-partido.py b79d0bf f25087f`:
VERDE.

#### El cierre del epílogo

**La batería entera de `AGENTS.md`, sobre `f25087f`**, con `bateria32.sh`, del *scratchpad*, que
lanza cada paso en su orden y deja su registro en `artifacts/bateria-<paso>.log`: los diecinueve
pasos salen con 0, y la porcelana, vacía. Las cifras, cada una con la orden que la da:

- **1737** casos en el carril rápido, en 10 ensamblados, **71** de ellos en `Arquitectura.Tests`, y
  **807** en el de integración, en 2, con `scripts/ci/recuento-de-tests.sh` y las listas de
  `AGENTS.md`. Son cuatro y uno más que en el cierre del 3.4: los tres casos de
  `ElNumeroDeUnRegistroEsSuyoYDeNadieMasTests` y `Lo_largo_…`, en el rápido, y
  `UnaMarcaQueNoSeGuardaTests`, en el de integración;
- **173** operaciones, con `bash scripts/generar-openapi.sh --comprobar`, en **109** rutas, las
  claves de `paths` de `docs/api/openapi.json`; y **245** tipos de **253** sitios, con
  `bash scripts/generar-errores.sh --comprobar`;
- **428** casos de jsdom y **22** en Chromium, y el canal de la consola abierto, con su marca y
  ningún aviso de `act()`;
- **408/450** KiB de arranque, y **175/192** la ruta diferida más pesada, `PaginaDelRecuento`, con
  `bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 192`;
- y ninguna dependencia entra ni sale: con
  `python -X utf8 scripts/dependencias-por-conjuntos.py c2ce759 HEAD`, los seis conjuntos, vacíos.

Salvo los casos, todas son las del cierre del 3.4.

**El humo, sobre `f25087f`**, con `humo36.sh`, del *scratchpad*: el de `humo35.sh`, en el proyecto
`bastion-humo-36`, con su propia copia del entorno, borrada al acabar. El migrador, con 0 y los
siete contextos en su orden; la bandeja del arranque, con `scripts/ci/la-bandeja-del-arranque.sh`,
verde en la primera mirada; las cuatro mutaciones, de la 1131 a la 1134, otra vez rojas y con su
mensaje; el control, verde en la segunda mirada; y el segundo arranque, con 0 y **116** permisos
antes y después. `FALLOS: 0`, y del proyecto no queda nada.

**Los runs de la rama, uno por subida**, cada uno con su conclusión antes de la siguiente: el de
`dfd8784`, la parte A, el **41799415438**; el de `c084071`, el B1, el **41801727921**; el de
`6396114`, el B2, el **41804134461**; y el de `43e02f8`, el B3 con `04edd93`, el **41806797530**.
Los cuatro, en verde, con sus tres trabajos. Los del B4 al B8, el de este commit y el de `main` se
anotan en el primer commit del 3.5, como los del cierre del 3.4 en el B1.

**Los runs del B4 al B8, anotados en el primer commit del 3.5.** El B4, `53fe640`, **no tuvo run
propio**: GitHub no procesó su subida, el día de la incidencia de Git Operations y de Actions del
2026-11-04, y lo comprobó el del B5, `c11a604`, el **41820699303**, con los dos dentro. No se hizo
un commit vacío ni se volvió a subir la rama: desde ahí, tras cada subida se confirma que existe un
run para ese hash antes de darlo por «en CI». El del B6, `3572b3a`, es el **41843181274**; el del
B7, `b79d0bf`, el **41846156376**; y el del B8, `f25087f`, el **41849176419**. Los cuatro, en verde,
con sus tres trabajos. El del cierre, `dfc88e8`, es el **41851844475**, y estaba en curso al
escribir esto: él y el de `main` se anotan en el commit siguiente del 3.5.

**El del cierre y el de `main`, anotados en la parte B del 3.5.** El del cierre, `dfc88e8`, el
**41851844475**, salió en verde con sus tres trabajos: el Backend, el 126649923858; el Frontal, el
126649923911; y el Humo, el 126658353112. Con él en verde, `main` avanzó a `dfc88e8`, y su run, el
**41854898734**, salió en verde también: el Backend, el 126660802727; el Frontal, el
126660803419; y el Humo, el 126669668181. Después se borró la rama `3.4-el-epilogo`, aquí y en el
remoto.

### La puerta del 3.5, contestada (2026-11-04)

La trae el mismo encargo del 2026-11-04, en su segunda parte. Las cinco primeras contestan lo que
el agente preguntó al empezar el ítem, y la sexta es lo que el usuario añade:

1. **Cambiar la zona de un almacén que ya tiene movimientos: se admite siempre.** El motivo es
   corregir una zona mal puesta. El ADR recoge la ventana de hasta una hora con el `422` del
   ADR-0047, y que lo ya fechado no cambia. El formulario avisa cuando la zona cambia. Lo mismo
   vale para la zona de la empresa.
2. **La herencia, en vivo.** La columna del almacén admite nulo, y el nulo es la zona de la
   empresa. El almacén puede volver a heredar, y la pantalla distingue «de la empresa» de «propia».
3. **La guarda de la transferencia, por solapamiento de días**, con casos el 2026-03-29, un día de
   23 horas, y el 2026-10-25, uno de 25. El ADR dice que la guarda usa las zonas como están en el
   momento en que comprueba.
4. **Lo que abarca varios almacenes:**
   - los saldos sin fecha y los listados, con la zona de la empresa;
   - el corte, con el día **más atrasado** entre las zonas de la empresa, la suya y la efectiva de
     cada almacén. Lleva un caso de fin de mes con dos zonas, y su mutación es el corte con la zona
     de la empresa;
   - el cuadre, con el día **más adelantado**, y un caso con dos zonas;
   - y, si al escribirlo alguno de los dos admite otra lectura viable, se pregunta.
5. **Las zonas admitidas, una lista cerrada**: `Europe/Madrid`, `Atlantic/Canary` y
   `Africa/Ceuta`. Un enumerado en OpenAPI, validado en el dominio. Un caso del carril rápido
   resuelve las tres en el *runner*, y el humo, en la imagen. La comprobación del `tzdata` de
   `aspnet:10.0-noble` va en el ADR. Ampliar la lista es una pregunta al usuario.
6. **Además:**
   - ningún test hereda la zona de la máquina;
   - el test del IL sigue exigiendo una sola fuente de «hoy», que ahora recibe la zona;
   - el ADR-0079 enmienda el §4 del ADR-0044 y el ADR-0063, con la nota en los dos lados;
   - y, en el frontal, la fecha por omisión de un formulario sigue al almacén elegido.
7. **El formulario de la zona: un diálogo en cada fila de los dos listados** (preguntado al
   empezar la E, contestado el 2026-11-05). El frontal de Organización solo tenía los listados, y
   la zona solo se cambiaba por la API. «Cambiar la zona», con el permiso de modificar: el del
   almacén ofrece «De la empresa» y las tres, y el de la empresa, las tres. Avisa de la hora que
   sigue al cambio y de que lo ya fechado no cambia. Lee el maestro y lo reenvía entero con su
   `If-Match`, porque la modificación lo reemplaza. Los listados muestran la zona, con «de la
   empresa» o «propia».

### Tomadas por el agente de desarrollo — ítem 3.5, al escribirlo y al medirlo (2026-11-04)

1. **La rama es `3.5-la-zona`, abierta desde `dfc88e8`**, el cierre del epílogo, que es el commit
   al que avanza `main`.
2. **El ítem va por partes, un tema por subida:**
   - **A, la zona en Organización**: el tipo, el contrato, las dos columnas y su migración;
   - **B, el día de negocio con su zona**: `DiaDeNegocio` pide la zona en todo lo que publica,
     Organización contesta las zonas por un puerto del bloque común, y los diecinueve sitios que
     piden un día se la pasan;
   - **C, la guarda de la transferencia**, por solapamiento;
   - **D, el corte y el cuadre**, con el día más atrasado y el más adelantado;
   - **el ADR-0079**, en su propio tema, con las notas en el ADR-0044 y en el ADR-0063;
   - **E, el frontal**: el día de su zona en las diez llamadas, y la zona en las pantallas;
   - **F, el cierre**: la tanda, la batería, el humo y el flujo de la rama.
3. **El tipo es `ZonaHoraria`, del dominio común**, con las tres admitidas y Madrid como la de por
   omisión. Se lee solo por su identificador IANA exacto, en ordinal: «europe/madrid», «UTC» o
   «Romance Standard Time» no son una zona admitida, y `De` lanza diciendo cuáles lo son. Sus
   reglas se piden al sistema cada vez, y el tipo no guarda un `TimeZoneInfo`.
   `Esta_maquina_conoce_cada_admitida_con_sus_reglas`, en el carril rápido, resuelve las tres con
   su desplazamiento de invierno y de verano: aquí, en Windows, y en el *runner*, en Linux.
4. **El contrato es `ZonaHorariaDto`**, un enumerado del `Contracts` común que se escribe con el
   identificador IANA (`JsonStringEnumMemberName`) y no lee números. `MapeoDeZonas` lo traduce en
   los dos sentidos, y `MapeoDeZonasTests` compara las dos listas por nombre: ninguna vacía, cada
   una con el identificador de la otra, ida y vuelta, y la misma lista sin repetidos.
   `ElContratoPublicaLasZonasAdmitidasTests` afirma que el enumerado publicado son las tres y nada
   más, y que la zona de la empresa no admite nulo y la propia del almacén sí.
5. **En la base**, `organizacion.empresas.zona_horaria`, obligatoria y con `'Europe/Madrid'` por
   omisión, y `organizacion.almacenes.zona_horaria`, que admite nulo. Cada una lleva su `CHECK`,
   `ck_empresas_zona_horaria` y `ck_almacenes_zona_horaria`, con la lista sacada de
   `ZonaHoraria.Admitidas` y no tecleada. La migración es `20261104141911_LaZonaHoraria`. **Lo ya
   escrito**: no hay datos reales, así que no hay *backfill*; con datos, la migración dejaría cada
   empresa en Madrid y cada almacén heredando. `LasMigracionesSobreTablasConFilasTests` nombra los
   dos `CHECK`: el de la empresa, con la zona peninsular en su fila inventada, y el del almacén sin
   relleno, porque su nulo es heredar. Sin ellos, el carril de integración dio un rojo, el de
   `Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila`.
6. **La API**: el alta de una empresa admite la zona y, sin ella, la empresa nace peninsular; la
   modificación la pide, y sin ella es un `400` del campo, porque reemplaza la empresa entera. El
   almacén publica `zonaHoraria`, la propia, nula si hereda, y `zonaEfectiva`, la que rige; el alta
   y la modificación admiten la propia o el nulo, que vuelve a heredar. La semilla de arranque crea
   su empresa en Madrid. Lo prueban los ocho casos de `LaZonaDeLaEmpresaYDelAlmacenTests`, con las
   semillas de los 35 millones.
7. **El frontal, en esta parte, solo sus datos de prueba**: los almacenes y la empresa de
   `pruebas/datos.ts` y de `LasPaginasCabenEn320` ganan los campos nuevos, para que compilen contra
   el cliente generado. Las pantallas son la parte E.
8. **Hallazgo: el conversor de enumerados de las opciones manda sobre el que declara el tipo.**
   `Program.cs` registra un `JsonStringEnumConverter` para toda la API, y System.Text.Json elige
   uno de las opciones antes que el `[JsonConverter]` de un tipo; solo el de una propiedad pasa por
   delante. El de las opciones leía números, así que `"zonaHoraria": 7` llegaba al dominio como una
   zona que no existe, y la petición contestaba un `500`. Lo encontró
   `Una_zona_que_no_se_publica_es_un_400_y_no_un_500`, con el `7`; con el `0` habría sido Madrid,
   sin decirlo. El arreglo va en el de las opciones, `allowIntegerValues: false`, y **cambia todos
   los enumerados de la API: un número es un `400` en cualquiera.** El contrato ya los publicaba
   como texto, el cliente generado no manda números, y ningún caso de los dos carriles mandaba
   uno: los dos salieron en verde con el cambio.
9. **Hallazgo: el generador de OpenAPI añade `null` a un enumerado compartido** en cuanto una
   propiedad que lo usa admite nulo, y como el esquema es uno, la zona de la empresa también lo
   admitía en el contrato. `ContratoDeLaApi` lo quita con un transformador del documento,
   `QuitarElNuloDeLosEnumerados`, y el nulo se queda en la propiedad, como `oneOf`. Ningún otro
   enumerado del contrato cambia: el de la zona es el primero compartido con una propiedad
   anulable. Lo afirma `La_zona_de_la_empresa_no_es_anulable_y_la_propia_del_almacen_si`.
10. **La clave de un `[Required]` es la de C#**, «ZonaHoraria», y no la del cuerpo, como la ponen
    los casos de uso. Es así en todos los `[Required]` de la API, y no es de este ítem: el caso
    afirma el campo sin mirar la grafía, y la propuesta va a *Notas / riesgos*.
11. **`DiaDeNegocio` pide la zona en todo lo que publica**: `De(instante, zona)`, `Hoy(reloj,
    zona)`, `Inicio(dia, zona)`, que es el instante UTC en que el día empieza en esa zona, y
    `ElMasAtrasado` y `ElMasAdelantado`, que reciben varias y lanzan sin ninguna. No queda un día
    sin zona que alguien pueda pedir por comodidad, y que sería el UTC de antes con otro nombre.
    `ElDiaDeNegocioSaleDeUnSoloSitioTests` busca desde ahora dos agujas en la IL,
    `DateOnly.FromDateTime` y las conversiones de `TimeZoneInfo`, y su pareja afirma las tres
    llamadas del sitio y en qué método está cada una. Su caso nuevo,
    `Todo_lo_que_da_un_dia_recibe_su_zona`, afirma que todo método público del sitio recibe una
    zona o una colección de ellas, con su pareja dentro: el barrido ve `De` y reconoce `Hoy` por
    su tipo. `DiaDeNegocioTests` va sobre la madrugada de fin de año en las tres zonas, el verano,
    los dos cambios de hora de 2026 en las tres, con sus días de 23 y de 25 horas, y la medianoche,
    que de 2000 a 2100 existe y es una sola: **36 890** días por zona.
12. **Las zonas se preguntan por un puerto del bloque común**, `IZonasDeLaEmpresa`, en
    `BuildingBlocks.Application.Fechas`: la de la empresa, la efectiva de un almacén y todas las de
    la empresa, la suya primero y sin repetir. Lo implementa Organización, `ZonasDeLaEmpresa`, que
    es de quien son, y resuelve la herencia al leer. Inventario, Catálogo y Terceros preguntan al
    puerto, y la exportación de un listado también: por eso es del bloque común y no del
    `Contracts` de Organización.
    - **Ve lo bloqueado**: un documento de un almacén que se bloqueó después sigue necesitando saber
      qué día es allí. Abre `ViendoLoBloqueado` con el motivo de `ConsultaDeAlmacenes`, una vez, en
      un método, y solo alrededor de las lecturas; el filtro de empresa sigue puesto.
      `ElFiltroNoSeSaltaPorAhiTests` cuenta esa apertura, la tercera de los adaptadores de puerto.
    - **Un almacén que no existe, o de otra empresa, lanza**: quien pregunta ya lo comprobó.
    - Lo prueba contra la base
      `El_puerto_de_las_zonas_ve_el_almacen_bloqueado_y_no_el_de_otra_empresa`, con las semillas 5
      y 6 de `LaZonaDeLaEmpresaYDelAlmacenTests`.
13. **«Hoy» en la empresa, `IHoyEnLaEmpresa`**, junto al puerto: el día en la zona de la empresa de
    quien pregunta, que sale del *claim*, y el más adelantado entre la suya y las de sus almacenes.
    Organización lo registra con el puerto, porque no tiene otra implementación que la que lee sus
    zonas. Tres casos en `HoyEnLaEmpresaTests`, con un reloj cuya zona local va catorce horas por
    delante, para que leerla diera otro día.
14. **Quién usa qué zona**, en los diecinueve sitios que pedían un día:
    - **la del almacén**: el ajuste al confirmarse y al anularse, el recuento al abrirse, al
      confirmarse y al anularse, la reserva al consumirse, el envío con la del origen y la recepción
      con la del destino;
    - **la de la empresa**: el alta y la modificación de un artículo, el precio y las tarifas del
      tercero sin fecha, los saldos sin fecha y su exportación, y el día del nombre del fichero de
      toda exportación;
    - **los saldos con fecha**: es futura si lo es en todas las zonas de la empresa, con el día más
      adelantado, para no rechazar nada que ya sea hoy en alguno de sus almacenes;
    - **y tres provisionales**, hasta su parte: la anulación de una transferencia, con el más
      atrasado de sus dos almacenes, porque el inverso nace con sus dos fechas en un mismo día —la
      C la revisa—; el corte, con el más atrasado de las tres admitidas, y el cuadre, con el más
      adelantado de las tres, hasta la D. Los dos sin zona de empresa todavía, pero ya sin el UTC.
15. **Dos órdenes cambian, porque la zona es la del almacén y hay que saber cuál es antes.** El
    recuento lee el almacén antes de calcular «hoy», y no cambia qué error gana: ya se comprobaba
    antes que las series, en `ComprobarLosMaestrosAsync`, que ahora es `ComprobarLasSeriesAsync`.
    **La reserva sí**: lee la clave del origen antes de la guarda de la fecha futura, porque es la
    que dice el almacén, así que con una reserva que no existe y una fecha futura gana el `404` de
    la reserva, donde antes ganaba el `422` de la fecha. Ningún caso de los dos carriles mandaba las
    dos cosas a la vez.
16. **Los dobles de la prueba, en `tests/Comun/LasZonasDeLaPrueba.cs`**, enlazado en los cuatro
    proyectos de dominio y en el funcional: `ZonasFijas`, que apunta a quién se le preguntó, y
    `HoyDeLaPrueba`, el `HoyEnLaEmpresa` de verdad con una zona fija. **Ningún caso hereda la zona
    de la máquina**: los de dominio dicen la suya, y los de integración que pedían el día UTC piden
    `ElDiaDeLaPrueba.Hoy()`, el de Madrid, que es la de las empresas de la prueba. Sin eso, entre
    las 22:00 y las 24:00 UTC un caso daría por futuro lo que ya es hoy allí.
    `LaParticionPorDefectoSeDenunciaTests` se queda en UTC: va veinte meses por delante.
17. **Los casos que cambian de nombre, porque cambia lo que afirman:**
    - `Sin_fecha_el_dia_es_el_UTC_y_no_el_del_calendario_de_quien_pregunta`, de `ResolverPrecio`,
      es ahora la teoría
      `Sin_fecha_el_dia_es_el_de_la_zona_de_la_empresa_y_no_el_UTC_ni_el_del_servidor`, con
      Madrid y Canarias a las 22:30 UTC y el servidor a −10 y a +14 horas;
    - el de la exportación de los saldos es la teoría
      `El_dia_de_la_exportacion_es_el_pedido_y_sin_pedir_ninguno_hoy_en_la_zona_de_la_empresa`,
      con Madrid y Canarias a las 23:30 UTC;
    - el de verano de `DiaDeNegocioTests` es
      `En_verano_la_peninsula_cambia_de_dia_a_las_diez_de_la_noche_UTC_y_Canarias_a_las_once`;
    - y `LaExportacionRecorreUnaFotoTests` espera el fichero del 28, el día de Madrid.
18. **La pantalla de tarifas sigue en el día UTC hasta la E.** Su caso de `ElListadoDeTarifas`
    cambia de título y de comentario para decirlo —«la pantalla lee el día UTC»—, y la E lo
    reescribe cuando el frontal pida el día de su zona.
19. **El contrato solo cambia una descripción**, la de los saldos: sin `fecha`, hoy en la zona de
    la empresa, y una fecha posterior a hoy en todas sus zonas es un `400`. Ninguna operación, ni
    ningún esquema.
20. **La guarda de la recepción compara instantes, por solapamiento de días** (respuesta 3). La
    recepción es un día de la zona del destino, y el envío, uno de la del origen: se admite si su
    día **no acaba antes de que empiece** el del envío. Es `DiaDeNegocio.NoAcabaAntesDe`, y la
    transferencia lo pregunta por `ZonasDeLaTransferencia`, un registro del dominio con las dos
    zonas que no se guarda: se leen al comprobar, como están entonces. En una sola zona, es la
    comparación de siempre.
    - `LaTransferenciaEntreDosZonasTests`, del carril rápido, la prueba en diez filas con el 29 de
      marzo, de 23 horas, y el 25 y el 26 de octubre de 2026, el de 25: de Madrid a Canarias, la
      recepción puede ir un día por detrás; al revés, no; entre Madrid y Ceuta, nunca.
    - Su barrido de 2000 a 2100 por los nueve pares afirma que una recepción admitida va como mucho
      un día por detrás, y nombra los dos pares en que va: de Madrid y de Ceuta a Canarias,
      **36 890** días cada uno.
21. **La base admite un día de holgura, y ni uno más.** El `CHECK`
    `ck_transferencias_recepcion_no_antes_del_envio` pasa a
    `fecha_de_recepcion >= fecha_de_envio - 1`, con la holgura de
    `Transferencia.DiasQueLaRecepcionPuedeIrPorDetras`; la migración es
    `20261104200558_LaRecepcionPorSolapamiento`. La base no sabe de zonas: el dominio compara
    instantes, y la base se queda en la cota que mide el barrido. Lo prueba contra la base
    `La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos`:
    la misma transferencia es un `422` con el destino en Madrid y entra con el destino en Canarias,
    y un `UPDATE` a mano a dos días es el `23514` de ese `CHECK`.
22. **La anulación lleva cada fecha a su zona, y deja de ser provisional** (punto 14). El inverso
    nace con su envío hoy en la zona del origen y su recepción hoy en la del destino, las dos del
    mismo instante, y cada pata del libro va con la suya. La R9 pregunta por los dos días, en orden:
    en Nochevieja, a las 23:30 UTC, la península ya escribe en el año nuevo y Canarias todavía en el
    viejo. `ConfirmarComoInverso` ya no iguala la recepción al envío: la trae del alta, y sin ella
    lanza. Lo prueba `La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`,
    a las 23:30 UTC del 25 de octubre: la salida del destino, el 25, y la vuelta al origen, el 26.
23. **La hora que sigue a un cambio de zona.** Con lo ya escrito, pasar el destino de Madrid a
    Canarias deja una hora en la que «hoy» allí es el día anterior al de la recepción que el inverso
    compensa. `AdmiteUnInversoEn` compara cada fecha del inverso con cada una del original, con las
    zonas de ahora, y si alguna va antes es el `422`
    `transferencia-fecha-anterior-al-ultimo-movimiento`, el código que daría la valoración
    (ADR-0047). Va antes de construir, con un mensaje propio que dice el cambio de zona y que dura
    como mucho una hora. Lo prueba
    `En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento`: a
    las 23:30 UTC del 25 de octubre no hay inverso, y a las 00:00 UTC se anula.
24. **Hallazgo: con las fechas por zona, el saldo a fecha de toda la empresa puede contar dos
    veces.** El día del calendario en que una recepción va un día por detrás de su envío, la
    mercancía ya está en el destino, en su día, y todavía en el origen, en el suyo: sumada por la
    empresa, sale doble ese día. Cada almacén está bien en su calendario. Y el tránsito del destino
    empieza el día del envío, que es de la otra zona, así que a un día D puede dejarse o llevarse
    hasta una hora. Los dos van al ADR-0079 y no cambian código: es lo que significa que cada fecha
    sea la de su almacén.
25. **Hallazgo: la instantánea de Inventario no tenía las tres columnas de la causa de la
    bandeja.** `4f26e2b`, el hueco 3 del 3.4, las añadió a `bandeja_de_salida`, que Inventario
    excluye de sus migraciones, y la instantánea no se regeneró porque Inventario no tuvo migración;
    `has-pending-model-changes` no ve una tabla excluida, y por eso `comprobar-migraciones.sh` salió
    verde. EF la puso al día al generar la de la C, y la migración no lleva ninguna operación sobre
    la bandeja. La de Organización se puso al día igual en la A.
26. **Los dobles dicen sus zonas.** `ElLibroDeLaPrueba.EnMadrid`, e `InversoEnMadrid` para los
    casos de una sola zona, en los que las dos fechas del inverso son la de la anulación; en el
    carril de integración, `ElLibro.AnularSinLaApiAsync`, con las dos en Madrid, la de las empresas
    de la prueba. Los tres casos de integración usan las semillas del 739 al 741, y la cabecera de
    `LaTransferenciaTests` lo dice.
27. **Las zonas del trabajo mensual se leen sin inquilino, una vez por empresa y vuelta**
    (respuesta 4). `IAvanceDelCorte.ZonasAsync` abre el ámbito `AvanceDelCorte`, la tercera
    apertura de `ElAvanceDelCorte.cs`, y le pide a `IZonasDeLaEmpresa.TodasAsync` la de la empresa
    y la efectiva de cada almacén, sin repetir. Las mismas van al mes del avance, con el día más
    atrasado, y al cuadre, con el más adelantado: leídas dos veces, un cambio de zona entre las dos
    lecturas daría a cada mitad unas zonas distintas. El motivo del ámbito lo dice en
    `IInquilinoActual`, y `ElFiltroNoSeSaltaPorAhiTests` cuenta las tres aperturas.
28. **El retraso se mide por empresa, contra su mes.** La que falla antes de leer sus zonas se
    mide contra el mes de todas las admitidas, el más atrasado posible, que es el que menos
    retraso le cuenta: el fallo ya lo cuenta su registro, y no infla la métrica. Los registros
    8700 y 8702 dejan de decir el mes, porque la vuelta ya no tiene uno.
29. **Los dobles del cuadre.** `LasExistencias.CuadrarEnAsync` y los tres cuadres directos de
    `LasExistenciasSonLaSumaDelLibroTests` pasan la zona por omisión, la de las empresas de la
    prueba. El ayudante que cuadra por el contenedor y la propiedad leen las zonas como el trabajo,
    y la propiedad exige que sean la de su modelo. Los dos casos nuevos,
    `ElCuadreEnLasZonasDeLaEmpresaTests`, usan el 742 y el 743, y la cabecera de
    `LaTransferenciaTests` lo dice.
30. **Hallazgo: la hora que sigue a un cambio de zona también la ve el cuadre, y como un
    descuadre.** Pasar un almacén de Madrid a Canarias deja escritas fechas del día de Madrid, y
    mientras Canarias va un día por detrás, el libro hasta hoy no las suma. El cuadre marca el mes
    como cuadrado igual, así que es una alarma, una vez, que una comprobación a mano media hora
    después no repite. Lo prueba
    `Con_el_almacen_de_vuelta_en_Canarias_la_entrada_de_Madrid_descuadra_hasta_que_alli_es_su_dia`,
    y va al ADR-0079 y al informe: es lo que pidió la respuesta 4, y cambiarlo sería otra lectura.
31. **El ADR-0079 enmienda siete ADR, y no solo los dos que nombró la respuesta 6.** El día UTC
    también estaba escrito en el §4 del ADR-0047, cuyo «anular nunca choca con esta regla» deja en
    falso la hora que sigue a un cambio de zona; en el §3 y el §5 del ADR-0053, la guarda de la
    recepción y las fechas del inverso; en el §13 del ADR-0055, la fecha de apertura del recuento;
    y en las *Consecuencias* del ADR-0073, el día del nombre del fichero. Y el retraso del corte se
    medía contra un mes de toda la vuelta en el §3 del ADR-0064. Cada uno lleva la nota en su
    *Estado* y al principio de su punto, y el ADR-0079 los cita con su enlace. Salieron de
    `git grep -n "UTC" -- docs/adr`, y el del ADR-0064, de leer su indicador.
32. **El paso del humo que resuelve las tres zonas en la imagen es de la F.** El ADR lo dice así, y
    pide que mire lo que .NET resuelve con los ficheros, y no solo que estén. La comprobación a mano
    de la imagen, con su resumen, su `tzdata` y la orden que la repite, está en su punto 11.
33. **Hallazgo: el commit del ADR-0079, que solo tocaba documentos, puso rojo el carril rápido**
    (run 41916108441, `87fa6cb`). `UnaEnmiendaSeAnotaEnLosDosAdrTests` lee lo que enmienda un ADR
    en la primera cláusula de su viñeta, hasta los dos puntos, y la del 0079 decía «Enmienda, con
    la nota en cada uno:», con los siete en las subviñetas: dos rojos, siete anotadas sin declarar
    y una declaración sin objeto. Antes de commitear se corrieron ocho casos con un filtro, los del
    README y del número de los ADR, y no el proyecto entero. La cabecera se reescribe en el commit
    siguiente, sin cambiar ninguna decisión, y su *Estado* lo dice. Con un documento tocado, se
    corre `dotnet test tests/Arquitectura.Tests` entero: **72** casos, en 10 s.
34. **La E va en cuatro temas, cada uno en su subida.** El frontal no calcula el día: lo lee del
    servidor (ADR-0079 §12). Así que primero va lo que el servidor publica, y después las pantallas:
    - **E1, el «hoy» de la API.** `GET /api/v1/organizacion/hoy` da el día de la empresa, el más
      adelantado de sus zonas y el de cada zona admitida, con el instante en que acaba. Todo sale
      de una sola lectura del reloj, en `IHoyEnLaEmpresa.EnTodasAsync`. Van las tres zonas, y no
      solo las de la empresa, porque un almacén puede cambiar de zona mientras la pantalla tiene la
      lista de antes. **Autenticada y sin permiso**, la sexta de la lista cerrada de
      `CadaAccionDeclaraSuPermisoTests`: saber qué día es lo necesita todo formulario con fecha, de
      cualquier módulo, y no es una facultad que se conceda. No lleva ADR propio, porque no cambia
      el contrato de una forma que valga fuera de este proyecto.
    - **E2, el primer día de la recepción.** La ficha de una transferencia enviada lo publica, con
      las zonas de ahora: la recepción puede ir un día por detrás del envío hacia Canarias, y el
      `dia >= envio` del formulario la rechazaría. El dominio lo calcula con la misma regla que la
      guarda, y un caso afirma que es el primero que la guarda admite.
    - **E3, el frontal**: las diez llamadas, con el día de su zona; la fecha por omisión y el tope
      siguen al almacén elegido, y antes de elegirlo, el de la empresa.
    - **E4, el diálogo de la zona**, en los dos listados, como contestó el usuario (punto 7 de la
      puerta).
35. **Hallazgo: la E1 puso rojo el carril rápido** (run 41981864138, `564056d`), por lo mismo que el
    punto 33 con otro proyecto. `TodaEscrituraDiceComoSeProtegeTests` cuenta todas las acciones de
    la API, y el «hoy» es una más: 174, y no 173. Antes de commitear se corrieron los casos que la
    acción tocaba, con un filtro, y no el carril entero. El recuento sube en el commit siguiente,
    con su párrafo: una lectura, que no mueve ningún reparto. Con una acción nueva, se corre
    `dotnet test Bastion.sln --filter "Category!=Integracion"` entero, que tarda un minuto.
36. **La E3 lee el día del servidor, y no lo vuelve a pedir hasta que acaba.** `useHoy()`, en
    `shared/hoy`, pregunta a `GET /organizacion/hoy` con la clave `['hoy']`, y la respuesta vale
    hasta que acaba el primero de los días de sus zonas, y un segundo más. Si ese instante ya pasó,
    un minuto, porque un cero en `refetchInterval` es «nunca». Del reloj queda solo
    `milisegundosHasta`, en `shared/lib/dias.ts`, y la regla de ESLint lo sigue diciendo. Un
    formulario con fecha espera al día, con su «cargando» y su fallo con reintento
    (`ConElDiaDeHoy`); la recepción lo pide ya con el botón, como la zona del destino. Los casos de
    las pantallas no paran el reloj para fijar el día: lo dice el simulado. Con `HOY_PARTIDO`, las
    once y media de la noche en Canarias, que en la península ya es el 12, y la nave de Las Palmas,
    cada sitio afirma que toma el día de su zona y no el de la empresa. El README del catálogo
    decía que el «hoy» de las tarifas salía de `getFullYear`, y no era así desde el 2.16. Las diez
    llamadas a `hoyEnElServidor()`, una a una:
    - las tarifas, el día de la empresa, que es con el que la API elige el tramo sin fecha;
    - el saldo, por omisión el de la empresa y de tope el más adelantado (ADR-0079 §3);
    - el alta del ajuste, tres, con el almacén: el valor por omisión, el tope del campo y el del
      esquema, que ahora recibe el día;
    - el alta de la transferencia, las mismas tres, con el origen;
    - y la recepción, dos, con el destino: el valor por omisión y el tope de su esquema, que va del
      primer día que publica la ficha (la E2) a hoy en el destino.
37. **La E4 cambia la zona en una ventana, desde la fila de cada listado** (punto 7 de la puerta,
    ADR-0080). Los dos listados ganan la columna «Zona horaria»: la del almacén dice la zona que
    rige, con «de la empresa» o «propia», y la de la empresa, solo la zona, que siempre es suya.
    Con el permiso de modificar, cada fila lleva «Cambiar la zona de …», y la ventana:
    - **lee el maestro al abrirse, cada vez**, con su `ETag`, y lo reenvía entero con el
      `If-Match`, porque el `PUT` lo reemplaza: lo que no es la zona vuelve como está ahora. Con
      `gcTime: 0`, al volver a abrirla espera a la lectura de ahora (punto 40).
      Sin `ETag` no enseña el formulario, como la ficha del artículo. Un `412` ofrece cargar la
      versión de ahora, y el formulario se monta otra vez con ella;
    - en el almacén ofrece «La de la empresa» y las tres, y volver a heredar manda un nulo; en la
      empresa, las tres;
    - avisa antes de elegir, en la descripción del grupo: lo ya fechado no cambia, y la hora que
      sigue al cambio puede rechazar lo que se feche hoy (ADR-0079 §7). La de la empresa añade que
      arrastra a sus almacenes sin zona propia;
    - con la misma zona, cierra sin escribir;
    - al guardar, un aviso dice cómo queda, también la zona que se hereda, y la caché entera se
      invalida (ADR-0080 §6), `['hoy']` incluida.

    Lo común de las dos vive en `features/organizacion/zonas`, y la ventana, en
    `shared/ui/Dialogo.tsx`. **Hallazgo:** el simulado del listado de empresas era un JSON sin tipo,
    y no tenía la zona. Ahora es `EMPRESA_ALFA`, tipado, en `pruebas/datos.ts`. Y el comentario de
    `CLAVE_DE_HOY` solo nombraba el cambio de empresa entre lo que mueve las zonas.
38. **La ventana es el `<dialog>` del navegador, sin librería** (ADR-0080). La nota de la fase 0
    esperaba una librería de primitivas para el primer diálogo modal. `showModal()` da la capa de
    arriba, el resto de la página inerte, `Escape` y el foco que vuelve, medidos en Chromium, y el
    bloqueo del *scroll*, que no da, es una línea de `index.css`. jsdom no trae `showModal()` ni
    `close()`, y `setupTests.ts` pone los dos, sin más: lo que no imitan lo prueba el carril del
    navegador, que desde aquí también es el de la ventana modal. El paso de la CI cambia de nombre,
    y `AGENTS.md` lo dice en su tabla y en la batería. **Hallazgo:** un borrador descartaba un
    `close` con la ventana todavía abierta, el que el modo estricto mandaría al desmontar y volver a
    montar el efecto. Medido en Chromium, no llega: el efecto depende del elemento, que va en el
    estado, y en el doble montaje todavía no hay elemento. La guarda y su comentario, que
    contradecía la medida, se quitaron, y el caso afirma los dos montajes y ningún `close`. La
    viñeta «No enmienda ningún ADR» del 0080 entra en la lista cerrada de
    `UnaEnmiendaSeAnotaEnLosDosAdrTests`, como la del 0055 y la del 0065.
39. **La F1 es el paso del humo que resuelve las tres zonas en la imagen** (punto 32, ADR-0079
    §11). `scripts/ci/las-zonas-de-la-imagen.sh` pide `GET /organizacion/hoy` con el testigo del
    paso de la sesión, y afirma cuatro cosas: que vienen las tres admitidas y ninguna más; que el
    día de cada una acaba a la medianoche de su zona, Madrid y Ceuta a +1 o +2, y Canarias a 0 o
    +1; que Ceuta va con Madrid, y Canarias, una hora por detrás; y que el día de la empresa
    sembrada es el de Madrid. Va después de «Una petición que LEE UNA TABLA», y `AGENTS.md` lo
    pone el primero de los tres guiones del humo local. Antes de la CI corrió contra doce
    respuestas fabricadas por un servidor de pruebas del *scratchpad*, que no entra en el
    repositorio. Tres verdes: invierno, verano y la noche de octubre en que Madrid va un día por
    delante. Y nueve rojos, uno por cada forma de fallar, cada uno con el `::error` del propio
    guion: un 500, todas como UTC, Canarias con las reglas de Madrid, sin Ceuta, una zona de más,
    Ceuta con Canarias, Canarias por delante de Madrid, la empresa con otro día y ninguna zona.
    Lo que se repite está en el humo del cierre, con una imagen sin el fichero de Canarias.
    **Hallazgos:** la primera vuelta lanzó el bash de WSL, y los nueve rojos «pasaban» con un
    127, que es rojo contra rojo: por eso cada caso exige el mensaje del guion. Y en Windows el
    python del guion escribía en la página de códigos del sistema al ir a una tubería, y el arnés
    no pudo leer el primer acento. Corre con `-X utf8`, que en el *runner* no cambia nada.
40. **La F2 es la tanda, y dejó cinco mutaciones en verde en los dos carriles.** La tanda, con su
    tabla, va después de esta lista. Lo que se encontró al medirla:
    - **El motor contaba un solo ensamblado.** Cada proyecto de test escribía su `.trx` con el
      mismo `LogFileName`, y el último pisaba a los demás: la base del carril rápido salió con 72
      casos, los de `Arquitectura.Tests`, y no con los 1820 de diez ensamblados. Un rojo de
      Arquitectura se habría visto, y uno de los otros nueve, no. El motor lee ahora todos los
      `.trx` del directorio, y el carril rápido se repitió entero. El de integración es un solo
      proyecto, y su primera vuelta vale. La base se compara con el recuento antes de seguir.
    - **La 1159 no compilaba en su primera forma**: devolver el envío tal cual deja el método sin
      leer la instancia, y CA1822 lo pide estático. Se reescribió con el día del envío en la zona
      del origen, que rompe lo mismo, y en la segunda vuelta salió roja en los dos carriles.
    - **La 1157 no la ve el carril de integración por lo que afirma.** Su rojo en masa, 23 nombres
      en la primera vuelta, era el carril entero: en la segunda pasada caen los 28 casos con el
      mismo `PendingModelChangesWarning` de `OrganizacionDbContext`, y también el caso que la ve
      por diseño, `Una_empresa_nace_peninsular_y_cambia_de_zona_por_su_ficha`, corrido solo. La
      zona por omisión es el valor por omisión de la columna (`ConfiguracionDeEmpresa`), así que
      el modelo deja de coincidir con la migración, y el `MigrateAsync` del *fixture* se niega a
      migrar. Por lo que afirman, la ven los dos casos del carril rápido de su fila.
    - **La 1156 la vio otro caso**, `Lo_que_no_es_una_admitida_exacta_no_se_lee`, y no el que
      esperaba la tanda, `Una_zona_que_no_se_admite_lanza_y_dice_cuales_se_admiten`. Manda lo
      medido.
    - **Cinco salieron verdes en los dos carriles**, y cada una es un hallazgo:
      - la 1160 y la 1172, el futuro de la recepción en la zona del origen y el del envío en la
        península. Ningún caso fechaba una punta en un día al que su zona ha llegado y la otra
        no. Las cubre `El_futuro_de_cada_punta_es_el_de_la_zona_de_su_almacen`, con la semilla
        744: a las 23:30 UTC del 25 de octubre, en Madrid ya es el 26, y en Canarias, todavía no;
      - la 1170, el tope del saldo a una fecha con el día de la empresa. Ningún caso pedía el
        saldo de un día al que solo ha llegado la zona de un almacén. La cubre un caso de
        `ElCuadreEnLasZonasDeLaEmpresaTests`, con la semilla 745, que también ve una mutación
        nueva, la 1212: sin fecha, el más adelantado en vez del día de la empresa. Al escribirlo
        se vio que el saldo sin fecha de una entrada que aún no ha llegado no es una lista vacía,
        sino su fila, con cero;
      - la 1178, el «hoy» que no se vuelve a pedir al acabar el día. Ningún caso dejaba pasar el
        fin del día con la pantalla abierta. La cubre `useHoy.test.tsx`;
      - y la 1205, la ficha de la ventana del almacén, que se queda en la caché. Ningún caso
        volvía a abrir la ventana. La cubre un caso que la abre, la cierra y la vuelve a abrir
        con la lectura retenida, y afirma que no ofrece la de antes. La ventana de la empresa
        lleva el mismo caso, y su mutación, nueva, es la 1213.
    - **El `gcTime: 0` no hace lo que decía su comentario.** El punto 37, el comentario de
      `DialogoDeZonaDelAlmacen.tsx` y el README de Organización le atribuían que la ventana lea al
      abrirse, y eso lo hace el `staleTime` de cero, al montar. Lo que hace es que, al volver a
      abrirla, la ventana espere a la lectura de ahora y no ofrezca la de antes mientras llega.
      Se corrigen los tres en el commit de la 1205.
    - **La 1199 solo la vio jsdom.** Quitaba el `focus()` del título, y el caso del navegador que
      fija el foco siguió en verde: `showModal()` lleva el foco a lo primero que se puede enfocar,
      y eso ya es el título, por su `tabIndex={-1}`. El caso de jsdom probaba la línea, porque su
      `showModal()` no mueve el foco. El ADR-0081 enmienda el 0080: sin `focus()`, el foco que
      entra se prueba en el navegador, y la 1211 quita el `tabIndex`.

    Las coberturas van en tres commits, cada uno en su subida: el backend, `62309e3`, con este
    punto; el frontal, la 1178, la 1205 y la 1213; y el ADR-0081, con la 1199 y la 1211. Después
    de cada uno se vuelven a medir sus mutaciones. La segunda pasada del backend, sobre
    `62309e3`, con `mutar43.py`, tiene las bases a cero, `rápido` 1820 en 10 ensamblados e `int`
    28, y ve cada mutación con su caso y solo con él: la 1160 y la 1172, **int 1**,
    `El_futuro_de_cada_punta_es_el_de_la_zona_de_su_almacen`; y la 1170 y la 1212, **int 1**, el
    del saldo. La 1212 no la ve el carril rápido.

    ```bash
    python -X utf8 mutar43.py base 1157 1160 1170 1172 1212   # sobre 62309e3
    ```

**La tanda de la F2, de la 1151 a la 1210, se escribió antes de medir**, cada mutación con su
«espera», sobre `0152639`, en dos guiones del *scratchpad*: `mutar41.py`, el backend, de la 1151 a
la 1173, y `mutar42-web.py`, el frontal, de la 1174 a la 1210. Cada una se aplicó sola sobre el
árbol limpio, se revirtió con `git restore --source=HEAD` y se tocó con la fecha de ahora, y al
acabar cada vuelta el árbol restaurado compiló y la porcelana salió vacía. Las del arnés son dos:
la 1173, las zonas fijas de la prueba sin las de los almacenes, y la 1210, el servidor de pega de
los almacenes, que no mira el `If-Match`. Los registros, `tanda41-primera.log`, `tanda41b.log` y
`tanda42.log`:

```bash
python -X utf8 mutar41.py base 1151-1173                         # vale su carril de integración
python -X utf8 mutar41.py base 1151-1173 solo=rapido ambos=1159  # el motor arreglado (punto 40)
python -X utf8 mutar42-web.py base 1174-1210
```

Los carriles corren sin compilar y con plazo de cuelgue: `rápido`, `dotnet test Bastion.sln
--filter "Category!=Integracion"`, entero; `int`, `Api.IntegrationTests` filtrado a las cuatro
clases de la zona: `LaZonaDeLaEmpresaYDelAlmacenTests`, `ElCuadreEnLasZonasDeLaEmpresaTests`,
`LaTransferenciaEntreDosZonasTests` y `LasGuardasDeLaTransferenciaTests`; `web`, `vitest run`,
entero, con los avisos de `act()` de los siete trozos del canal; y `nav`, el carril del navegador,
entero. Las bases, todas a cero: `rápido` 1820 casos en 10 ensamblados, `int` 26, `web` 451 en 54
ficheros, sin un aviso, y `nav` 29 en 3. Ninguna mutación dejó un aviso de `act()`, ni agotó un
plazo.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1151 | El día de un instante, en UTC y no en su zona | **rápido 25**: `DiaDeNegocioTests.A_las_doce_y_media_del_uno_de_enero_en_Madrid_la_fecha_es_el_uno_de_enero`, `DiaDeNegocioTests.Ceuta_dice_el_dia_de_la_peninsula`, `DiaDeNegocioTests.El_desplazamiento_con_el_que_se_escribe_el_instante_no_cuenta`, `DiaDeNegocioTests.El_ultimo_tic_del_dia_en_su_zona_sigue_siendo_ese_dia`, `DiaDeNegocioTests.En_verano_la_peninsula_cambia_de_dia_a_las_diez_de_la_noche_UTC_y_Canarias_a_las_once`, `DiaDeNegocioTests.Entre_varias_zonas_el_mas_atrasado_y_el_mas_adelantado`, `DiaDeNegocioTests.Hoy_es_el_dia_de_la_zona_en_el_instante_que_da_el_reloj`, `DiaDeNegocioTests.La_medianoche_existe_y_es_una_sola`, `DiaDeNegocioTests.Un_dia_no_va_antes_que_otro_si_no_acaba_antes_de_que_el_otro_empiece`, `ElDiaDeNegocioSaleDeUnSoloSitioTests.El_dia_de_negocio_se_calcula_en_su_sitio`, `ElTrabajoMensualAvanzaElCorteTests.Cada_empresa_avanza_hasta_el_mes_de_sus_zonas_y_su_cuadre_recibe_las_mismas`, `ElTrabajoMensualAvanzaElCorteTests.El_mes_que_toca_es_el_anterior_al_dia_mas_atrasado_de_las_zonas_de_la_empresa`, `ElTrabajoMensualAvanzaElCorteTests.El_retraso_mide_cada_corte_contra_su_mes_y_unas_zonas_que_no_se_leen_son_un_fallo_que_tambien_cuenta`, `HoyEnLaEmpresaTests.El_mas_adelantado_cuenta_las_zonas_de_los_almacenes`, `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba`, `HoyEnLaEmpresaTests.Hoy_es_el_dia_en_la_zona_de_la_empresa_de_quien_pregunta`, `LaExportacionRecorreUnaFotoTests.El_fichero_se_llama_por_el_recurso_y_el_dia_de_negocio`, `LaMaquinaDeEstadosDeLaTransferenciaTests.El_inverso_no_va_antes_que_la_llegada_que_compensa`, `LaMaquinaDeEstadosDeLaTransferenciaTests.La_recepcion_no_va_antes_que_el_envio`, `LaTransferenciaEntreDosZonasTests.El_inverso_lleva_cada_fecha_en_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_se_admite_si_su_dia_no_acaba_antes_de_que_empiece_el_del_envio`, `LaTransferenciaEntreDosZonasTests.Ninguna_fecha_del_inverso_acaba_antes_de_que_empiece_ninguna_del_original`, `LaTransferenciaEntreDosZonasTests.Una_recepcion_admitida_va_como_mucho_un_dia_por_detras_de_su_envio`, `LasExportacionesDeInventarioTests.El_dia_de_la_exportacion_es_el_pedido_y_sin_pedir_ninguno_hoy_en_la_zona_de_la_empresa`, `ResolverPrecioTests.Sin_fecha_el_dia_es_el_de_la_zona_de_la_empresa_y_no_el_UTC_ni_el_del_servidor`; **int 3**: `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada`, `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos` |
| 1152 | El principio de un día, en UTC y no en su zona | **rápido 10**: `DiaDeNegocioTests.La_medianoche_existe_y_es_una_sola`, `DiaDeNegocioTests.Un_dia_de_cambio_de_hora_no_dura_veinticuatro_horas`, `DiaDeNegocioTests.Un_dia_no_va_antes_que_otro_si_no_acaba_antes_de_que_el_otro_empiece`, `ElDiaDeNegocioSaleDeUnSoloSitioTests.El_dia_de_negocio_se_calcula_en_su_sitio`, `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba`, `LaTransferenciaEntreDosZonasTests.El_inverso_lleva_cada_fecha_en_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_primera_recepcion_es_el_primer_dia_que_admite_la_guarda`, `LaTransferenciaEntreDosZonasTests.La_recepcion_se_admite_si_su_dia_no_acaba_antes_de_que_empiece_el_del_envio`, `LaTransferenciaEntreDosZonasTests.Ninguna_fecha_del_inverso_acaba_antes_de_que_empiece_ninguna_del_original`, `LaTransferenciaEntreDosZonasTests.Una_recepcion_admitida_va_como_mucho_un_dia_por_detras_de_su_envio`; **int 2**: `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos` |
| 1153 | Un día no va antes que otro: se comparan las fechas, sin zonas | **rápido 6**: `DiaDeNegocioTests.Un_dia_no_va_antes_que_otro_si_no_acaba_antes_de_que_el_otro_empiece`, `LaTransferenciaEntreDosZonasTests.El_inverso_lleva_cada_fecha_en_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_primera_recepcion_es_el_primer_dia_que_admite_la_guarda`, `LaTransferenciaEntreDosZonasTests.La_recepcion_se_admite_si_su_dia_no_acaba_antes_de_que_empiece_el_del_envio`, `LaTransferenciaEntreDosZonasTests.Ninguna_fecha_del_inverso_acaba_antes_de_que_empiece_ninguna_del_original`, `LaTransferenciaEntreDosZonasTests.Una_recepcion_admitida_va_como_mucho_un_dia_por_detras_de_su_envio`; **int 2**: `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos` |
| 1154 | El más atrasado de varias zonas es el más adelantado | **rápido 4**: `DiaDeNegocioTests.Entre_varias_zonas_el_mas_atrasado_y_el_mas_adelantado`, `ElTrabajoMensualAvanzaElCorteTests.Cada_empresa_avanza_hasta_el_mes_de_sus_zonas_y_su_cuadre_recibe_las_mismas`, `ElTrabajoMensualAvanzaElCorteTests.El_mes_que_toca_es_el_anterior_al_dia_mas_atrasado_de_las_zonas_de_la_empresa`, `ElTrabajoMensualAvanzaElCorteTests.El_retraso_mide_cada_corte_contra_su_mes_y_unas_zonas_que_no_se_leen_son_un_fallo_que_tambien_cuenta` |
| 1155 | El más adelantado de varias zonas es el más atrasado | **rápido 3**: `DiaDeNegocioTests.Entre_varias_zonas_el_mas_atrasado_y_el_mas_adelantado`, `HoyEnLaEmpresaTests.El_mas_adelantado_cuenta_las_zonas_de_los_almacenes`, `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba`; **int 1**: `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada` |
| 1156 | La zona se lee sin distinguir mayúsculas | **rápido 1**: `ZonaHorariaTests.Lo_que_no_es_una_admitida_exacta_no_se_lee` |
| 1157 | La zona por omisión es Canarias | **rápido 2**: `EmpresaTests.Una_empresa_que_no_dice_su_zona_es_peninsular`, `ZonaHorariaTests.Las_admitidas_son_las_tres_de_Espana_y_la_peninsular_la_de_por_omision`; **int 23**: `ElCuadreEnLasZonasDeLaEmpresaTests.Con_el_almacen_de_vuelta_en_Canarias_la_entrada_de_Madrid_descuadra_hasta_que_alli_es_su_dia`, `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada`, `LaTransferenciaEntreDosZonasTests.En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento`, `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos`, `LaZonaDeLaEmpresaYDelAlmacenTests.El_puerto_de_las_zonas_ve_el_almacen_bloqueado_y_no_el_de_otra_empresa`, `LaZonaDeLaEmpresaYDelAlmacenTests.Hoy_sale_en_las_tres_zonas_con_su_fin_y_el_de_la_empresa_con_la_suya`, `LaZonaDeLaEmpresaYDelAlmacenTests.La_base_no_guarda_una_zona_fuera_de_la_lista_ni_una_empresa_sin_zona`, `LaZonaDeLaEmpresaYDelAlmacenTests.Modificar_una_empresa_sin_decir_su_zona_es_un_400_del_campo`, `LaZonaDeLaEmpresaYDelAlmacenTests.Un_almacen_sin_zona_hereda_la_de_su_empresa_en_vivo_y_puede_volver_a_heredarla`, `LaZonaDeLaEmpresaYDelAlmacenTests.Una_empresa_nace_peninsular_y_cambia_de_zona_por_su_ficha`, `LaZonaDeLaEmpresaYDelAlmacenTests.Una_zona_que_no_se_publica_es_un_400_y_no_un_500`, `LasGuardasDeLaTransferenciaTests.Cada_accion_fuera_de_su_estado_es_409_y_en_el_suyo_pasa`, `LasGuardasDeLaTransferenciaTests.El_alta_pregunta_cada_hueco_a_su_almacen_y_no_toca_lo_bloqueado`, `LasGuardasDeLaTransferenciaTests.El_alta_rechaza_cada_cosa_que_no_se_mueve_con_su_codigo`, `LasGuardasDeLaTransferenciaTests.El_envio_vuelve_a_leer_la_marca_y_no_saca_un_borrador_que_ya_no_casa`, `LasGuardasDeLaTransferenciaTests.Encoger_el_ejercicio_por_encima_de_una_recepcion_es_409_y_por_el_otro_lado_no`, `LasGuardasDeLaTransferenciaTests.La_anulacion_pide_motivo_y_el_ejercicio_de_hoy_abierto`, `LasGuardasDeLaTransferenciaTests.Sin_ejercicio_para_el_dia_ni_se_recibe_ni_se_anula`, `LasGuardasDeLaTransferenciaTests.Una_clave_vaciada_en_la_divisa_de_antes_recibe_el_transito_en_la_nueva`, `LasGuardasDeLaTransferenciaTests.Una_fecha_anterior_al_ultimo_movimiento_de_su_punta_es_422`, `LasGuardasDeLaTransferenciaTests.Una_punta_valorada_en_otra_divisa_para_el_envio_por_los_dos_lados`, `LasGuardasDeLaTransferenciaTests.Una_transferencia_en_borrador_impide_cerrar_el_ejercicio_y_enviada_no`; en `int`, el carril entero, porque el `MigrateAsync` del *fixture* no migra un modelo con cambios (punto 40) |
| 1158 | La guarda de la transferencia, con la recepción en la zona del origen | **rápido 5**: `LaTransferenciaEntreDosZonasTests.El_inverso_lleva_cada_fecha_en_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_primera_recepcion_es_el_primer_dia_que_admite_la_guarda`, `LaTransferenciaEntreDosZonasTests.La_recepcion_se_admite_si_su_dia_no_acaba_antes_de_que_empiece_el_del_envio`, `LaTransferenciaEntreDosZonasTests.Ninguna_fecha_del_inverso_acaba_antes_de_que_empiece_ninguna_del_original`, `LaTransferenciaEntreDosZonasTests.Una_recepcion_admitida_va_como_mucho_un_dia_por_detras_de_su_envio`; **int 2**: `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos` |
| 1159 | La primera recepción, en el día del origen | en su primera forma, no compila (CA1822, punto 40); en la segunda, **rápido 1**: `LaTransferenciaEntreDosZonasTests.La_primera_recepcion_es_el_primer_dia_que_admite_la_guarda`; **int 1**: `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos` |
| 1160 | La recepción mira el futuro en la zona del origen | **ninguno**, en los dos carriles: hallazgo, lo cubre la semilla 744 (punto 40) |
| 1161 | La anulación fecha la salida del destino en la zona del origen | **int 2**: `LaTransferenciaEntreDosZonasTests.En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento`, `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen` |
| 1162 | Un almacén con zona propia toma la de su empresa | **int 4**: `LaTransferenciaEntreDosZonasTests.En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento`, `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos`, `LaZonaDeLaEmpresaYDelAlmacenTests.El_puerto_de_las_zonas_ve_el_almacen_bloqueado_y_no_el_de_otra_empresa` |
| 1163 | Las zonas de la empresa, sin las de sus almacenes | **int 2**: `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada`, `LaZonaDeLaEmpresaYDelAlmacenTests.El_puerto_de_las_zonas_ve_el_almacen_bloqueado_y_no_el_de_otra_empresa` |
| 1164 | El corte, con la zona de la empresa (la pidió el usuario) | **rápido 3**: `ElTrabajoMensualAvanzaElCorteTests.Cada_empresa_avanza_hasta_el_mes_de_sus_zonas_y_su_cuadre_recibe_las_mismas`, `ElTrabajoMensualAvanzaElCorteTests.El_mes_que_toca_es_el_anterior_al_dia_mas_atrasado_de_las_zonas_de_la_empresa`, `ElTrabajoMensualAvanzaElCorteTests.El_retraso_mide_cada_corte_contra_su_mes_y_unas_zonas_que_no_se_leen_son_un_fallo_que_tambien_cuenta` |
| 1165 | El cuadre, con el día más atrasado | **int 1**: `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada` |
| 1166 | El más adelantado, solo con la zona de la empresa | **rápido 1**: `HoyEnLaEmpresaTests.El_mas_adelantado_cuenta_las_zonas_de_los_almacenes` |
| 1167 | El «hoy» de cada zona, el de Madrid | **rápido 2**: `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba`, `HoyEnLaEmpresaTests.En_todas_lee_el_reloj_una_sola_vez` |
| 1168 | El «hoy» de cada zona, solo las de la empresa | **rápido 1**: `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba`; **int 1**: `LaZonaDeLaEmpresaYDelAlmacenTests.Hoy_sale_en_las_tres_zonas_con_su_fin_y_el_de_la_empresa_con_la_suya` |
| 1169 | El «hoy» de la empresa lee el reloj otra vez | **rápido 1**: `HoyEnLaEmpresaTests.En_todas_lee_el_reloj_una_sola_vez` |
| 1170 | El tope del saldo a una fecha, el día de la empresa | **ninguno**, en los dos carriles: hallazgo, lo cubre la semilla 745 (punto 40) |
| 1171 | La zona efectiva de un almacén, siempre la de la empresa | **int 6**: `ElCuadreEnLasZonasDeLaEmpresaTests.Con_el_almacen_de_vuelta_en_Canarias_la_entrada_de_Madrid_descuadra_hasta_que_alli_es_su_dia`, `ElCuadreEnLasZonasDeLaEmpresaTests.Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada`, `LaTransferenciaEntreDosZonasTests.En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento`, `LaTransferenciaEntreDosZonasTests.La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen`, `LaTransferenciaEntreDosZonasTests.La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos`, `LaZonaDeLaEmpresaYDelAlmacenTests.Un_almacen_sin_zona_hereda_la_de_su_empresa_en_vivo_y_puede_volver_a_heredarla` |
| 1172 | El envío mira el futuro en Madrid | **ninguno**, en los dos carriles: hallazgo, lo cubre la semilla 744 (punto 40) |
| 1173 | **Arnés**: las zonas fijas de la prueba, sin las de los almacenes | **rápido 2**: `HoyEnLaEmpresaTests.El_mas_adelantado_cuenta_las_zonas_de_los_almacenes`, `HoyEnLaEmpresaTests.En_todas_da_el_dia_de_cada_zona_admitida_con_el_instante_en_que_acaba` |
| 1174 | El día de una zona es siempre el de la empresa | **web 4**: «El alta de un ajuste el día es el del almacén elegido, por omisión y de tope, y el que se escribe no se mueve», «El alta de una transferencia el día es el del origen, por omisión y de tope; el destino no lo mueve, ni el origen al escrito», «El día de hoy en una zona es el de su zona, y sin zona, el de la empresa», «La ficha de una transferencia hacia Canarias, el día es el del destino, y el primero que admite puede ser anterior al envío» |
| 1175 | Vale hasta el ÚLTIMO fin de día, y no el primero | **web 2**: «Lo que vale lo que dijo el servidor dura hasta que acaba el primero de los días de sus zonas, y un segundo más», «Lo que vale lo que dijo el servidor un minuto, y no cero, si el día ya tenía que haber acabado: cero sería «nunca»» |
| 1176 | Un día ya acabado vale cero, que es «nunca» | **web 1**: «Lo que vale lo que dijo el servidor un minuto, y no cero, si el día ya tenía que haber acabado: cero sería «nunca»» |
| 1177 | Sin el segundo de margen | **web 1**: «Lo que vale lo que dijo el servidor dura hasta que acaba el primero de los días de sus zonas, y un segundo más» |
| 1178 | El «hoy» no se vuelve a pedir al acabar el día | **ninguno**, en los dos carriles: hallazgo, lo cubre `useHoy.test.tsx` (punto 40) |
| 1179 | El ajuste: elegir almacén pone el día de la empresa | **web 1**: «El alta de un ajuste el día es el del almacén elegido, por omisión y de tope, y el que se escribe no se mueve» |
| 1180 | El ajuste: el tope del campo, el de la empresa | **web 1**: «El alta de un ajuste el día es el del almacén elegido, por omisión y de tope, y el que se escribe no se mueve» |
| 1181 | El ajuste: se valida con el día de la empresa | **web 1**: «El alta de un ajuste el día es el del almacén elegido, por omisión y de tope, y el que se escribe no se mueve» |
| 1182 | La transferencia: elegir origen pone el día de la empresa | **web 1**: «El alta de una transferencia el día es el del origen, por omisión y de tope; el destino no lo mueve, ni el origen al escrito» |
| 1183 | La transferencia: el tope del campo, el del destino | **web 1**: «El alta de una transferencia el día es el del origen, por omisión y de tope; el destino no lo mueve, ni el origen al escrito» |
| 1184 | La transferencia: se valida con el día del destino | **web 1**: «El alta de una transferencia el día es el del origen, por omisión y de tope; el destino no lo mueve, ni el origen al escrito» |
| 1185 | La recepción: el tope, el día de la empresa | **web 1**: «La ficha de una transferencia hacia Canarias, el día es el del destino, y el primero que admite puede ser anterior al envío» |
| 1186 | La recepción: el primer día, el del envío | **web 1**: «La ficha de una transferencia hacia Canarias, el día es el del destino, y el primero que admite puede ser anterior al envío» |
| 1187 | Los saldos: el tope, el día de la empresa | **web 1**: «El saldo a una fecha sin día, el campo enseña el de la empresa, y deja llegar al de la zona más adelantada» |
| 1188 | Los saldos: sin día, el más adelantado | **web 1**: «El saldo a una fecha sin día, el campo enseña el de la empresa, y deja llegar al de la zona más adelantada» |
| 1189 | Las tarifas: el día, el más adelantado | **web 1**: «El listado de tarifas el día es el de la empresa, que lo dice el servidor, y no el del reloj de quien mira» |
| 1190 | Cambiar la zona de un almacén no invalida nada | **web 2**: «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera», «La zona del almacén volver a la de la empresa manda la zona nula, y dice cuál rige ahora» |
| 1191 | Cambiar la zona de un almacén invalida solo Organización | **web 2**: «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera», «La zona del almacén volver a la de la empresa manda la zona nula, y dice cuál rige ahora» |
| 1192 | Cambiar la zona de una empresa no invalida nada | **web 1**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes» |
| 1193 | El almacén se escribe sin If-Match | **web 3**: «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera», «La zona del almacén si otra persona ha guardado antes, lo dice y deja cargar la versión actual», «La zona del almacén volver a la de la empresa manda la zona nula, y dice cuál rige ahora» |
| 1194 | El almacén se escribe sin lo que no es la zona | **web 1**: «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera» |
| 1195 | La empresa se escribe sin If-Match | **web 1**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes» |
| 1196 | La empresa se escribe sin lo que no es la zona | **web 1**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes» |
| 1197 | Volver a heredar manda la península, y no el nulo | **web 1**: «La zona del almacén volver a la de la empresa manda la zona nula, y dice cuál rige ahora» |
| 1198 | La ventana en una referencia, abierta en un efecto sin dependencias | **nav 5**: «La ventana modal Escape la cierra una vez, y el foco vuelve a quien la abrió», «La ventana modal la página de debajo no corre mientras está abierta, y al cerrarla vuelve a correr», «La ventana modal la salida de dentro la cierra una vez, y el foco vuelve a quien la abrió», «La ventana modal lo de fuera queda inerte: el foco no sale de la ventana», «La ventana modal se abre modal, con el foco en el título, y el modo estricto no la cierra» |
| 1199 | El foco no va al título | **web 1**: «La zona del almacén abre la ventana con la zona de ahora marcada, y lo que avisa antes de las opciones»; `nav`, ninguno: `showModal()` ya lleva el foco al título (punto 40, ADR-0081) |
| 1200 | Sin el bloqueo del scroll | **nav 1**: «La ventana modal la página de debajo no corre mientras está abierta, y al cerrarla vuelve a correr» |
| 1201 | La ventana no avisa al cerrarse | **web 2**: «La zona del almacén guardar la que ya tenía cierra sin escribir, y cancelar también», «La zona del almacén si otra persona ha guardado antes, lo dice y deja cargar la versión actual»; **nav 3**: «La ventana modal Escape la cierra una vez, y el foco vuelve a quien la abrió», «La ventana modal la página de debajo no corre mientras está abierta, y al cerrarla vuelve a correr», «La ventana modal la salida de dentro la cierra una vez, y el foco vuelve a quien la abrió» |
| 1202 | Cerrar desde dentro no cierra | **web 4**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes», «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera», «La zona del almacén guardar la que ya tenía cierra sin escribir, y cancelar también», «La zona del almacén sin la versión del almacén no enseña el formulario: no habría con qué guardar»; **nav 1**: «La ventana modal la salida de dentro la cierra una vez, y el foco vuelve a quien la abrió» |
| 1203 | Guardar la misma zona escribe | **web 1**: «La zona del almacén guardar la que ya tenía cierra sin escribir, y cancelar también» |
| 1204 | Un almacén que hereda abre con la zona de la empresa marcada como propia | **web 1**: «La zona del almacén abre la ventana con la zona de ahora marcada, y lo que avisa antes de las opciones» |
| 1205 | La ficha de la ventana se queda en la caché | **ninguno**, en los dos carriles: hallazgo, lo cubre volver a abrir la ventana (punto 40) |
| 1206 | Los avisos no describen el grupo | **web 2**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes», «La zona del almacén abre la ventana con la zona de ahora marcada, y lo que avisa antes de las opciones» |
| 1207 | «Propia» y «de la empresa», cruzadas | **web 4**: «La zona del almacén (en) el listado dice la zona y de dónde le viene», «La zona del almacén (es) el listado dice la zona y de dónde le viene», «La zona del almacén guarda el almacén entero sobre la versión leída, lo dice, e invalida la caché entera», «La zona del almacén volver a la de la empresa manda la zona nula, y dice cuál rige ahora» |
| 1208 | La ventana de la empresa no avisa de sus almacenes | **web 1**: «La zona de la empresa guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes» |
| 1209 | La ventana, más ancha que 320 px | **nav 2**: «Las ventanas de la zona a 320 px la de '/almacenes' cabe, y lo de dentro no pasa de su borde», «Las ventanas de la zona a 320 px la de '/empresas' cabe, y lo de dentro no pasa de su borde» |
| 1210 | ARNÉS: el servidor de pega de los almacenes no mira el If-Match | **web 1**: «La zona del almacén si otra persona ha guardado antes, lo dice y deja cargar la versión actual» |

## Estado actual

**FASE 3 EN CURSO — 4 de 11 ítems.** La puerta de clarificación se contestó el 2026-10-26: las
catorce respuestas, el orden de trabajo y lo que el agente decidió al anotarlas están en
*Decisiones tomadas → Traídas por el encargo del 2026-10-26*, y el desglose son **once ítems**, del
3.1 al 3.11, en el *Checklist*. Los cuatro primeros, la casa en orden, la exportación, los puertos y
la bandeja, están hechos.

> **Dónde se retoma (2026-11-08): el 3.5, la zona, en su rama `3.5-la-zona`, por la parte F.**
> La puerta del 3.5 está contestada, y lo que el agente decidió, con el orden de sus partes, en
> *Decisiones tomadas → Tomadas por el agente de desarrollo — ítem 3.5*. La parte A, la zona en
> Organización, es `c9726cd`, con su run, el 41858094972, en verde; la B, el día con su zona,
> `1a3472e`, con el 41861853335. La C, la guarda de la transferencia por solapamiento y la
> fecha de su anulación en cada zona, `653b0a0`, con el 41907028697. La D, el corte, con el día
> más atrasado de las zonas de la empresa, y el cuadre, con el más adelantado, `9740980`, con el
> 41913400780; aquí, el carril rápido da **1817** casos en 10 ensamblados, y el de integración,
> **821** en 2, los dos sin un rojo, con `bash scripts/ci/recuento-de-tests.sh`. El ADR-0079 es
> `87fa6cb`, con la nota en los siete que enmienda, y su run, el 41916108441, salió rojo por la
> cabecera (punto 33); la arregla `ebecddd`. La E va en cuatro temas (punto 34), y la E1, el «hoy»
> de la API, es `564056d`; su run, el 41981864138, salió rojo por el recuento de las acciones
> (punto 35), y lo sube `074f3aa`. La E2, el primer día de la recepción en la ficha, es `cbb5807`,
> con el 41984914641. La E3, el frontal con el día de cada zona (punto 36), es `59dde65`, con el
> 41987871181. La E4, la ventana de la zona en los dos listados (puntos 37 y 38, ADR-0080), es
> `bc90b65`, con el 42480674393. La F1, el paso del humo que resuelve las tres zonas en la imagen
> (punto 39), es `0152639`, con el 42482093033. La F2, la tanda, está medida (punto 40), y dejó
> cinco verdes en los dos carriles: sus coberturas van en tres commits, el del backend,
> `62309e3`, con el punto 40, el del frontal y el del ADR-0081, y después de cada uno se vuelven
> a medir. Las mutaciones nuevas son la 1211, la 1212 y la 1213, así que la siguiente es la
> **1214**; y el ADR, el **0081**. Queda la F3, la batería, el humo y el cierre.
> Después del 3.5, el 3.6, y se para en la puerta del 3.7. `CLAUDE.md` ya dice la fase 3, desde
> `dfd8784`.
>
> `/code-review` no se hizo: el traspaso la anota como no hecha, con su rango,
> `0638419..0d26e76`.

**Lo largo de los cuatro ítems hechos**, el 3.1, el 3.2, el 3.3 y el 3.4, está en
[`docs/historia/fase-3.md`](historia/fase-3.md#estado-actual), movido tal cual en el epílogo del
3.4: una entrada por ítem, con sus commits y sus runs, su batería, su humo y sus cifras. Su fila,
con el cierre y lo que sigue abierto, en *Decisiones tomadas → Los ítems cerrados de la fase 3*.

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
- [x] **3.2 · La exportación** — criterio de aceptación: cada listado de la nota de la exportación
  descarga un CSV en el dialecto de la importación (ADR-0034 §5), con BOM y con la regla del
  apóstrofo de la decisión 7 del 1.11. Lo genera el servidor con los filtros y el orden del
  listado, **todas sus páginas**, el permiso del listado y sin lo bloqueado. Se genera por páginas
  internas y en flujo, nunca en una sola orden ni entero en memoria. El listado más grande, medido
  sobre una empresa del tamaño de la C del ADR-0064, cabe en el plazo de 30 s por orden; si no
  cabe, se pregunta. Y un `?q=` sin filtro declarado es un `400` (ADR-0069).

  **Hecho el 2026-11-02**, en la rama `3.2-la-exportacion`, en veinte commits, del `bb5d843` al
  `0b7661c`, y el que lo cierra. Cada tema del orden de trabajo, en su subida; ocho de ellos, en
  más de un commit. Lo decidido sin preguntar, las tandas de la 792 a la 971 y la medida, en
  *Decisiones tomadas → Tomadas por el agente de desarrollo — ítem 3.2*; los commits, sus runs y
  la batería, en *Estado actual → El 3.2, hecho*. Cada parte del criterio, con lo que la comprueba:

  - **cada listado de la nota, con su CSV** (`77568cd`, `b81c67c`, `9b31c16`, `3a78bda`): los
    once de la nota y los dos del 2.19, trece, en la lista cerrada de
    `LaExportacionEsHermanaDelListadoTests`, que ven la 859 y la 861; y cada una, pulsada desde su
    pantalla (`f912e12`), con el censo `CadaExportacionTieneSuBoton`, que ven la 970 y la 971;
  - **en el dialecto de la importación, con BOM y el apóstrofo** (`823fa7f`, `c4c4650`):
    `EscritorCsvTests`, con el lector de la importación leyendo lo que escribe el escritor, que
    ven de la 806 a la 822; el apóstrofo de ida y vuelta, en `ApostrofoDelDialectoTests` y en la
    importación de terceros, que ven de la 864 a la 872; y el viaje entero de terceros, de una
    empresa a otra y otra vez fuera, byte a byte, que ven de la 873 a la 886;
  - **con los filtros y el orden del listado, y todas sus páginas** (`823fa7f`): la exportación lee
    de la URL lo mismo que su listado, sin la página, en la hermana del listado, que ve la 851, y
    la 860, la 909 y la 910 por su arnés; recorre todas las páginas del tamaño máximo, en
    `LaExportacionRecorreUnaFotoTests`, que ven de la 808 a la 811 y de la 825 a la 827; y cada
    módulo, contra la base, con los filtros de su listado, que ven las que quitan uno, como la 851,
    la 877, la 898, la 913, la 917, la 921 y la 936, y en el frontal, de la 959 a la 963;
  - **con el permiso del listado**, en la hermana del listado, que ven la 850, la 878 y la 944; y
    los maestros de otro módulo, nombrados solo con el permiso de verlos (punto 12), que ven la
    889, la 890, la 904, la 912, la 940 y la 941;
  - **sin lo bloqueado**: lo filtra el mismo repositorio que el listado, y lo afirman los casos de
    Organización y de Terceros contra la base, con un bloqueado en la empresa; y los puertos que
    nombran no devuelven el almacén ni la ubicación bloqueados, que ven la 830 y la 831;
  - **por páginas internas y en flujo, nunca en una sola orden ni entera en memoria** (`823fa7f`,
    ADR-0073): cada página está en la corriente antes de pedir la siguiente, que ven la 803 y la
    824; la foto es de solo lectura y en `REPEATABLE READ`, en `LaFotoDeLecturaTests` y en
    `LaFotoDeCadaModuloEsUnaFotoTests`, que ven la 804, la 805, la 823, la 858, la 862 y la 943;
    y en la medida, 500 páginas para 100 000 filas, con la memoria de la API entre 132 y 172 MiB;
  - **el listado más grande cabe en 30 s por orden**, sobre una empresa del tamaño de la C del
    ADR-0064: el saldo, con 1630 órdenes y la más larga en 268 ms, así que no se pregunta. La
    descarga entera tarda entre 78 y 86 s, que va a la sexta nota abierta;
  - **y un `?q=` sin filtro declarado es un `400`** (`ace1b7c`, ADR-0072): `filtro-no-admitido`,
    con `errors.q`, en `ElBordeComponeLasTresPiezasTests`, `SoloPublicaElTextoQuienLoMiraTests` y
    `ElTextoSoloLoRecibeQuienLoMiraTests`, que ven de la 792 a la 802. Cierra la nota abierta del
    2.19, y la de la exportación, la del 2.17.
- [x] **3.3 · Los puertos, con el tipo de la tarifa** — criterio de aceptación: la tarifa es de
  compra o de venta, y un tercero lleva una tarifa por rol; un puerto de `Catalogo.Contracts`
  resuelve el precio y el descuento de una tarifa para un artículo, una cantidad y una fecha, y
  solo propone precio una tarifa de compra en la divisa base. Un tercero que es cliente y
  proveedor recibe su precio de compra, no el de venta. La divisa de la tarifa pasa a ISO 4217, y
  el invariante 8 del traspaso de la fase 1 queda desarmado. Y Compras gana persistencia: su
  esquema, su contexto y sus migraciones, y los tests de arquitectura lo cuentan como módulo con
  persistencia.

  **Hecho el 2026-11-02**, en la rama `3.3-los-puertos`, en nueve commits, del `1fd5efb` al
  `4955575`, y el que lo cierra. Cada tema del orden de trabajo, en su subida; tres de ellos, en
  dos commits. Lo decidido sin preguntar y las tandas de la 972 a la 1077, en *Decisiones tomadas
  → Tomadas por el agente de desarrollo — ítem 3.3*; los commits, sus runs y la batería, en
  *Estado actual → El 3.3, hecho*. Cada parte del criterio, con lo que la comprueba:

  - **la tarifa es de compra o de venta** (`c12f3e2`, ADR-0076): el tipo va con el código, y lo
    impide la base, en `ContratoDeTarifasTests`, que ven la 977 y la 993; el alta lo contesta
    antes, en `CrearTarifaTests`, que ven de la 982 a la 987 y la 989; la migración pasa a venta
    las de hoy, que ve la 976; y el listado lo enseña (`eead063`), que ven de la 994 a la 1004;
  - **y un tercero lleva una tarifa por rol** (`e26217d`): la de venta pide un cliente, y la de
    compra, un proveedor, en `TerceroTests`, `LasTarifasDelTerceroTests` y
    `ContratoDeLosCrucesTests`, que ven de la 1005 a la 1019, y la 1028, la 1029 y la 1031, por
    su arnés; cada una por su subrecurso y con su permiso, que ven de la 1022 a la 1025; auditada
    y declarada en el inventario, que ven la 1026 y la 1027; y lo asignado se conserva como la de
    venta, en `LaTarifaDelTerceroSeConservaAlMigrarTests`, que ven la 1020, la 1021 y la 1030,
    esta por su arnés;
  - **un puerto de `Catalogo.Contracts` resuelve el precio y el descuento de una tarifa para un
    artículo, una cantidad y una fecha** (`ae253c1`, ADR-0077): `ElPrecioDeCompraTests`, motivo a
    motivo, que ven de la 1032 a la 1042, de la 1044 a la 1048 y la 1056; contra la base, en
    `ElPrecioDeCompraContraLaBaseTests`, que ven la 1034, la 1037, la 1040 y la 1057; la cantidad
    del precio por HTTP, en `ResolverPrecioTests` (`1e47d47`), que ve la 1043; registrado en el
    *host* de verdad y declarado como puerta pública, que ven la 1052 y la 1053; y con sus
    casillas en la matriz de puerto por estado, que ven la 1054 y la 1055, por su arnés;
  - **solo propone precio una tarifa de compra en la divisa base**: la de venta contesta
    `EsDeVenta`, que ven la 1032 y la 1033, y la de otra divisa, `NoEstaEnLaDivisaBase`, que ven
    de la 1035 a la 1037 y la 1056;
  - **un tercero que es cliente y proveedor recibe su precio de compra, no el de venta**:
    `Un_cliente_que_tambien_es_proveedor_recibe_su_precio_de_compra_y_no_el_de_venta`, contra la
    base, que ven la 1032 y la 1050; y la tarifa de compra la contesta solo quien es proveedor, en
    su empresa y sin bloquear, que ven la 1049 y la 1051;
  - **la divisa de la tarifa pasa a ISO 4217** (`c12f3e2`, `7f867bb`): la migración la traduce una
    vez desde `organizacion.divisas`, y se para si no puede, en
    `LaDivisaDeLaTarifaSeTraduceAlMigrarTests`, que ven de la 972 a la 981; el recorrido con filas
    aprende la referencia que la base no declara, que ven la 991 y la 992, por su arnés; y se
    valida y se guarda por su código, que ven la 984, la 985, la 988 y la 990;
  - **el invariante 8 queda desarmado** (punto 11): el puerto compara el código del tramo con el
    de la empresa sin traducir, que ven la 1035, la 1036 y la 1037;
  - **y Compras gana persistencia** (`82cf007`, `4955575`): su esquema y su historial, en
    `EsquemaDeIdentidadTests` y en el humo, que ven la 1058, la 1059, la 1066 y la 1075; su
    contexto, registrado y con sus filtros, que ven de la 1060 a la 1062; y su migración,
    compilada, recorrida y comprobada, que ven la 1064, la 1065, la 1071, la 1076 y la 1077. Los
    tests de arquitectura lo cuentan como módulo con persistencia: montado, con su capa con tipos
    y sus siete aristas, que ven de la 1067 a la 1069; y los censos de modelos y de filtros, que
    ven la 1070 y la 1072. La 1063, la 1073 y la 1074 salen en verde, y lo explican los puntos 12
    y 13.
- [x] **3.4 · La bandeja, lista para su primer manejador** — criterio de aceptación: los cinco
  huecos de *Decisiones tomadas → El orden de trabajo, C*, cada uno con su caso y su mutación: un
  ámbito y un contexto por evento; el manejador con el inquilino del evento, y no dentro de
  `SinInquilino`; quién lo causó y su correlación en el evento; un indicador de aparcados sin
  resolver; y la transacción del manejador, abierta por el despachador, con el efecto, la huella y
  el evento de respuesta dentro, y un choque de versión que reintenta sin aparcar. Lo ejerce un
  manejador de prueba, con una carrera de verdad. Antes se lee
  `fundamentos/datos-distribuidos/convenciones.md`. **Al cerrarlo, se para y se pregunta antes
  del 3.5.**

  **Hecho el 2026-11-03**, en la rama `3.4-la-bandeja`, en diez commits, del `ef05f8a` al `a70195b`,
  y el que lo cierra. Cada tema del orden de trabajo, en su subida; el segundo, en dos commits; y la
  tanda del cierre, con dos commits más, cada uno en su subida, que cubren lo que dejó en verde. Lo
  decidido sin preguntar y las tandas de la 1078 a la 1125, en *Decisiones tomadas → Tomadas por el
  agente de desarrollo — ítem 3.4*; los commits, sus runs, la batería y el humo, en *Estado actual →
  El 3.4, hecho*. Cada parte del criterio, con lo que la comprueba:

  - **un ámbito y un contexto por evento** (`e840456`): uno por manejador y por intento, en
    `ElManejadorVaEnSuTransaccionTests`, que ven la 1078 y la 1081; y uno nuevo en cada reintento,
    en `UnChoqueDeVersionSeReintentaTests`, que ve la 1111;
  - **el manejador con el inquilino del evento, y no dentro de `SinInquilino`** (`ce3d7dd`): la
    empresa de su fila, lo que ve y la guarda de escribir en otra, en
    `ElManejadorVaConLaEmpresaDeSuFilaTests`, que ven la 1092 y la 1093; la fila sin empresa, que no
    se entrega y a la quinta se aparca, que ven la 1095 y la 1096; y el despachador, que recibe la
    fila y es *singleton*, que ve la 1090. La 1094 sale en verde, y lo explica el punto 9;
  - **quién lo causó y su correlación en el evento** (`4f26e2b`): en tres columnas de la fila, con
    su migración en Auditoría, que lee el humo; llenas desde la causa del ámbito, en
    `LaCausaDelAmbitoTests` y `ElAltaDeUnaEmpresaSePublicaTests`, que ven de la 1097 a la 1099;
    restauradas en el manejador, en `ElManejadorVaConQuienLoCausoTests`, que ven la 1100, la 1101
    y la 1106; y la traza, hija de la de la fila y en error si el manejador falla, que ven de la
    1102 a la 1104, y escuchada por la API, en `LaBandejaSeMideYNoSeSondeaTests` (`a70195b`), que
    ve la 1107. La 1105 sale en verde, como la 1094;
  - **un indicador de aparcados sin resolver** (`5ce629d`): `LosAparcadosSinResolverSeCuentanTests`,
    contra la base, que ven de la 1120 a la 1123, y la 1124, por su arnés (`3f7d6e6`); y
    `LaBandejaSeMideYNoSeSondeaTests`, que ve la 1123;
  - **la transacción del manejador, abierta por el despachador, con el efecto, la huella y el evento
    de respuesta dentro** (`e840456`, `bacf441`): por el `xmin` de las cuatro filas, en
    `ElManejadorVaEnSuTransaccionTests`, que ven la 1079, la 1080, la 1082, la 1083 y la 1087, y la
    1091, por su arnés; el evento que atiende cada manejador, leído de su base al registrarlo, que
    ven de la 1084 a la 1086; y la marca del publicador, en su ámbito, que ven la 1088 y la 1089. La
    1125 sale en verde, y lo explica el punto 4;
  - **y un choque de versión que reintenta sin aparcar** (`691e07d`):
    `UnChoqueDeVersionSeReintentaTests` y `LaColaSeDefiendeSolaTests`, que ven de la 1108 a la 1118;
  - **lo ejerce un manejador de prueba, con una carrera de verdad**: los del módulo de prueba, que
    solo existe en los tests de integración, con su tabla en una base nueva por caso; y la carrera
    es de dos transacciones sobre el `xmin` de su documento, que ve la 1119, por su arnés;
  - **y antes se lee `fundamentos/datos-distribuidos/convenciones.md`**: se leyó antes del primer
    manejador, y el ADR-0078 se apoya en ella.

  **El epílogo del 3.4** (encargo del 2026-11-04), en la rama `3.4-el-epilogo`, un tema por
  subida. No es un ítem del desglose, así que sus casillas van sangradas y no cuentan en la línea
  del README. El detalle de cada tema, en *Decisiones tomadas → Traídas por el encargo del
  2026-11-04*, parte B:

  - [x] **A · `CLAUDE.md`**: el §5, con la fase 3, y las líneas que mandan leer el PLAN
    (`dfd8784`);
  - [x] **B1 · los runs del cierre del 3.4, las respuestas del encargo y estas casillas**;
  - [x] **B2 · el `EventId` 8200, renumerado**, con la regla de los `[LoggerMessage]` y su pareja;
  - [x] **B3 · el ADR-0078, corregido en su sitio**, por encargo del usuario;
  - [x] **B4 · el fallo al guardar la marca, inyectado**, con la 1125 en rojo;
  - [x] **B5 · el Humo de la CI**: el registro en un fichero, y la bandeja en `scripts/ci`;
  - [x] **B6 · cada «por el ADR-NNNN», atado a su verbo**;
  - [x] **B7 · `npm audit fix`, sin `--force`**;
  - [x] **B8 · el PLAN deja de crecer**, con el 3.1–3.4 en `docs/historia/fase-3.md`;
  - [x] **y el cierre**: la tanda, la batería y el humo, y el flujo de la rama.
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
  libro**. **La puerta de la recepción decide, por su nombre, los dos puntos de *Lo que no cubre*
  del ADR-0078**: `IUsuarioActual` sin token dentro de un manejador, y el choque de versión que un
  caso de uso convierte en un `Resultado`.
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
  cada nota abierta; y el traspaso a la fase 4. Y tres cosas que trae el encargo del 2026-11-04: la
  regla de qué interceptores lleva cada contexto, con su pareja; las parejas de cada foto de la
  exportación, contadas una sola vez (la nota abierta de los 78–86 s); y la propuesta 14 del 3.3,
  si el epílogo del 3.4 no la cubre entera.

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
> fase 5. La quinta salió de una tanda del 3.1, y la sexta, de la medida del 3.2. La quinta la
> cerró el epílogo del 3.4, en su punto B6, y la séptima es de su punto B7. La octava es de la
> parte A del 3.5.
>
> **Las que se cierran durante la fase 3 se quedan aquí**, con CERRADA, la fecha y el ítem que las
> cierra, hasta que la fase se cierre y su historia se mueva, como en las fases de antes. La
> primera es la de la auditoría, que cerró el 3.1, y el 3.2 cerró dos: la del `?q=` que no
> filtra y la de la exportación.

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

- **CERRADA (2026-10-27, ítem 3.1, la tanda del ADR-0071) · la anotación del *Estado* no ata cada
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
  **Saltó en el 3.2, dos veces**: con el ADR-0074, que enmienda el 0055, y con el 0075, que
  enmienda el 0073 y el 0034. Medido otra vez con el mismo guion: 26 *Estado* anotados, diez con
  más de un autor, y ninguno aprovecha el hueco. Se pregunta en el informe del 3.2.
  **La cerró el epílogo del 3.4**, en su punto B6, con la respuesta 3 del encargo del 2026-11-04:
  cada «por el ADR-NNNN» cuenta solo si el último participio que lo precede es «enmendado» o
  «ampliado», con `Autores()` y `Participio()`. El caso sintético lleva un «corregido por el
  ADR-0003» en la viñeta de un «Enmendado», y la pareja es la cadena del 0044, entera entre las
  anotadas. Medido sobre el árbol del B6 con `python -X utf8 verbos-del-estado-2.py <repo>`, del
  *scratchpad*, con el participio de la regla: 27 *Estado* anotados, 43 autores que cuentan, 12
  con más de uno y 0 «por el» que no cuentan. La tanda, de la 1135 en adelante, en *Decisiones
  tomadas → Traídas por el encargo del 2026-11-04 → Lo que encontró el epílogo*.

- **ABIERTA (2026-10-28, ítem 3.2, la medida del tema 9) · la descarga entera del saldo de la
  empresa C tarda entre 78 y 86 s.** Cada orden cabe de sobra en el plazo —la más larga, 268 ms de
  30 s—, pero son 500 páginas de unos 180 ms, y el navegador junta el fichero entero antes de
  guardarlo (respuesta 3 de la puerta del 3.2): quien exporta espera minuto y medio con el botón
  apagado. El coste es por página y no por profundidad: en la del 2026-03-15, 75 ms la primera y
  105, 183, 182 y 184 de media por cuartos. Cada página rehace el `count(*)` de los pares, y en
  una foto el total no cambia, así que solo hace falta el de la primera; y cada una rehace también
  las sumas laterales de sus pares. Qué parte es de cada uno no se midió. Lo mide
  `bash humo32.sh medir`, del *scratchpad*, sobre la semilla de `sembrar32.sql` (*Decisiones
  tomadas → Tomadas por el agente de desarrollo — ítem 3.2*, la medida). **La propuesta**: medir
  primero una página del medio con `EXPLAIN (ANALYZE, BUFFERS)`, y si el `count(*)` pesa, que la
  exportación lo pida una vez. Recorrer el saldo por clave y no por desplazamiento lo descartó el
  ADR-0073, y volver a eso pide un ADR que lo enmiende. No se hace sin encargo. **El
  disparador**: una empresa mayor que la C, o alguien que encuentre lenta una descarga.

- **ABIERTA (2026-11-04, epílogo del 3.4, punto B7) · la auditoría informativa da 16 moderadas,
  todas por `minimatch`, y `npm audit fix` sin `--force` no las quita.** El aviso es uno,
  GHSA-g3hv-75wg-m249: `minimatch` antes de la 11.0.1 se puede llevar a una denegación de servicio
  multiplicando las partes de un *glob* (CWE-405). Está en la 10.2.6, por `eslint`,
  `@eslint/config-array`, `typescript-eslint` y `eslint-plugin-jsx-a11y-x`, y en la 5.1.9, por
  `@redocly/openapi-core`, que trae `openapi-typescript`; los dieciséis avisos son esos paquetes y
  lo que cuelga de ellos. Son el *linter* y el generador del cliente del contrato: herramientas de
  desarrollo, y los *globs* que leen son los de su propia configuración, no una entrada de fuera.
  La puerta de producción, `npm --prefix frontend audit --omit=dev --audit-level=high`, sale con
  0. **Por qué se queda**: el arreglo es la 11, y ningún paquete que la trae la admite, ni en su
  última versión publicada. `npm view <paquete> dependencies.minimatch` da `^10.2.6` en
  `eslint@10.14.0`, `^10.2.4` en `@eslint/config-array@0.23.5`, `^10.2.2` en
  `@typescript-eslint/typescript-estree@8.72.1`, `^10.2.5` en `eslint-plugin-jsx-a11y-x@0.2.0` y
  `5.1.9` en `@redocly/openapi-core@1.34.20`; y `openapi-typescript@7.13.0`, la última, pide
  `@redocly/openapi-core` `^1.34.6`. `npm audit fix --force` no sube: baja de versión mayor
  `typescript-eslint` a la 8.14.0, `openapi-typescript` a la 6.7.6, `eslint-config-prettier` a la
  6.15.0, `@eslint/js` a la 9.39.5 y `eslint-plugin-jsx-a11y-x` a la 0.0.1, y para
  `eslint-plugin-react-refresh` no tiene arreglo. Forzar la 11 con `overrides` iría contra el
  rango que declaran cinco paquetes, y eso no lo pide nadie. Lo mide `npm --prefix frontend audit
  --json`: 0 críticas, 0 altas, 16 moderadas y 0 bajas. **El disparador**: una versión de esos
  paquetes que admita la 11 —se mira con la misma `npm view`—, un aviso de gravedad alta en
  cualquiera de ellos, o el repaso del 3.11.

- **ABIERTA (2026-11-04, ítem 3.5, parte A) · la clave del error de un `[Required]` es el nombre de
  la propiedad de C#, y no la ruta del cuerpo.** MVC valida el atributo antes del caso de uso y
  pone la clave con el nombre de C#: sin zona, la modificación de una empresa contesta un `400` con
  `ZonaHoraria`, y no con `zonaHoraria`. Los casos de uso, en cambio, ponen la ruta del cuerpo, y
  esa es la que el frontal lee para saber en qué campo pintar el error (ADR-0065 §3). Pasa con
  todos los `[Required]` de la API, que son noventa y cinco, la suma de `git grep -c
  "\[Required(" -- 'src/*.cs'`, así que no es del 3.5:
  `Modificar_una_empresa_sin_decir_su_zona_es_un_400_del_campo` afirma el campo sin mirar la
  grafía. **Propuesta**: que MVC nombre las claves como el cuerpo, con
  `SystemTextJsonValidationMetadataProvider` en sus opciones, y un caso que afirme la clave exacta
  de un `[Required]` anidado, como la calle de una dirección. **El disparador**: la primera
  pantalla que pinte en su campo el error de un `[Required]`, o el repaso del 3.11.

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

- **CERRADA (2026-10-27, ítem 3.2, ADR-0069) · un `?q=` a un listado sin filtro declarado llega y
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
  **La cerró el 3.2**, en `feat(listados): tres piezas por listado, y un ?q= que no mira es
  400` (`ace1b7c`), con el ADR-0072 (punto 1): un `?q=` con algo escrito a un listado sin texto
  es un `400` `filtro-no-admitido`, con `errors.q`, y en blanco pasa; y los que no lo miran
  dejan de publicar `q`. Eran seis, y no cinco: el saldo tampoco lo declaraba. El caso que se
  iba a poner rojo afirma ahora el `400`, y lo ven la 792 y la 794.

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

- **CERRADA (2026-10-28, ítem 3.2, respuesta 4 de la puerta del 2.17) · ningún listado exporta
  lo que está viendo.** El §10 del plan maestro pide que «cada listado exporta a CSV/Excel lo que
  está viendo, con los mismos filtros aplicados», y ninguno lo hace: es una deuda desde la fase 1.
  La importación de terceros lee un CSV, y nada escribe uno. Los listados que no exportan, por su
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
  **La cerró el 3.2**: los trece, los once de la lista y los dos del 2.19, descargan su CSV con
  los filtros y el orden de su listado y todas sus páginas (`77568cd`, `b81c67c`, `9b31c16` y
  `3a78bda`), y cada pantalla tiene su botón (`f912e12`). Los cuenta la lista cerrada de
  `LaExportacionEsHermanaDelListadoTests`, y el censo `CadaExportacionTieneSuBoton` exige que
  las del contrato, las que pide el frontal y las que pulsa algún caso sean las mismas trece.
  El Excel queda fuera: el criterio del 3.2 pide el CSV.

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
