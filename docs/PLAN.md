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

## Estado actual

**Terminado el andamiaje de configuración; la fase 0 no ha empezado.** Lo que hay en el repositorio
es el arnés del agente (`CLAUDE.md`, `AGENTS.md`, este `PLAN.md`, `.claude/settings.json`), la
configuración del repositorio (`.gitignore`, `.gitattributes`, `.editorconfig`,
`Directory.Build.props`, `Directory.Packages.props`), el `docker-compose.yml` con sus Dockerfiles, el
*workflow* de CI, el `README.md`, la estructura de carpetas del §12 con `.gitkeep`, y el **andamiaje
del frontal ya verificado** (`npm run build`, `npm run lint` y `npm run typecheck` en verde).

**Siguiente: ítem 0.1**, aún sin empezar. Es el primero y el que desbloquea al resto.

### Lo que el agente de configuración NO pudo dejar hecho, y por qué

Leer esto antes de empezar el 0.1: explica por qué faltan cosas que parecerían obvias.

1. **No existe `Bastion.sln` ni ningún `.csproj`.** El entorno donde se montó esto **no tenía el SDK
   de .NET**, así que no había forma de comprobar que la solución compilaba. La regla era explícita:
   un esqueleto que no compila es peor que ninguno. Están las **carpetas** del §12 (una por módulo y
   capa, con el nombre del proyecto ya puesto: `src/Modules/Ventas/Bastion.Ventas.Domain/`), los
   `.gitkeep` y toda la configuración de MSBuild que los proyectos heredarán. **Crear la solución y
   los proyectos es la primera mitad del ítem 0.1.**
2. **La CI está escrita pero su mitad de backend no se ejecuta todavía.** El *job* `backend` de
   `.github/workflows/ci.yml` comprueba primero si existe `Bastion.sln`: mientras no exista, se salta
   los pasos y lo deja dicho en el resumen del *run*. **No es una CI en rojo ni un verde falso: es
   una CI que declara lo que no ha podido comprobar.** En cuanto el 0.1 cree la solución, el mismo
   *workflow* pasa a compilar, formatear y ejecutar tests **sin tocar una línea** — y a partir de ahí
   un fallo suyo es un fallo de verdad. El *job* `frontend` sí corre de verdad desde el primer commit.
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

- [ ] **0.1 · Andamiaje del repositorio y solución modular** — criterio de aceptación: `Bastion.sln`
  compila; la estructura coincide con el §12; `dotnet build` y `npm run build` en verde.
- [ ] **0.2 · `docker compose up`** — criterio de aceptación: levanta PostgreSQL, API, frontal y
  observabilidad; la API responde a su sonda de vida y el frontal carga.
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
