# Bastion

**ERP completo para pyme española**, construido por fases: inventario, compras, ventas, facturación
con validez fiscal, tesorería y contabilidad.

Es un **monolito modular** — un solo desplegable, con módulos cuyas fronteras vigila el compilador y
comprueba la CI. No es una elección tímida: en un ERP el dominio está intrínsecamente acoplado (una
factura toca stock, tesorería y contabilidad en la misma operación), así que repartirlo en servicios
produciría un monolito distribuido, que es la peor de las dos opciones. Lo que sí se hace desde el
principio es construirlo **como si algún día fuese a partirse**: cada módulo con su esquema de base
de datos, su contrato público y su comunicación por eventos.

> **Estado: fase 0 (Cimientos), sin empezar.** Lo que hay ahora es el andamiaje del repositorio y la
> configuración. El detalle exacto de qué falta está en **[`docs/PLAN.md`](docs/PLAN.md)**, que es la
> fuente de verdad del estado del trabajo.

---

## Stack

| Capa | Elección |
|---|---|
| Lenguaje y runtime | **C# 14** sobre **.NET 10 (LTS)** |
| API | **ASP.NET Core**, controladores `[ApiController]`; minimal APIs solo para `/health` |
| Persistencia | **EF Core 10** + Npgsql, un `DbContext` y un historial de migraciones **por módulo** |
| Base de datos | **PostgreSQL 17+**, un **esquema por módulo**, `snake_case`, sin claves foráneas entre esquemas |
| Identidad | **ASP.NET Core Identity** + JWT · access corto en memoria, refresh rotativo en cookie `httpOnly` |
| Frontal | **React 19** + **TypeScript** estricto + **Vite** + Tailwind |
| Contrato | **OpenAPI** generado por el servidor; el cliente de TypeScript **se genera**, no se escribe |
| Observabilidad | **Serilog** + **OpenTelemetry** + `HealthChecks` |
| Tests | **xUnit** + **Testcontainers** (PostgreSQL real) + **NetArchTest** · **Vitest** + Testing Library + MSW |
| Entorno local | **Docker Compose** · CI en **GitHub Actions** |

---

## Puesta en marcha

### Requisitos

- **.NET SDK 10** (la versión exacta la fija [`global.json`](global.json))
- **Node.js 22+**
- **Docker** con Compose v2

### 1 · Clonar, con la biblioteca como carpeta hermana

Esto **no es opcional si vas a trabajar con un agente**. El `CLAUDE.md` de este repositorio importa
las convenciones con rutas `@../BibliotecaDocumentacion/…`, así que ambos repositorios tienen que
quedar al mismo nivel:

```
/donde-sea/
├── Bastion/                     ← este repositorio
└── BibliotecaDocumentacion/     ← las convenciones (solo lectura)
```

```bash
mkdir bastion-workspace && cd bastion-workspace
git clone https://github.com/AOjeda006/Bastion.git
git clone https://github.com/AOjeda006/BibliotecaDocumentacion.git
```

Si clonas solo este repositorio, el proyecto compila igual pero el agente arranca **sin memoria**: los
imports no resuelven y trabaja sin las convenciones, sin avisar de nada.

> **`BibliotecaDocumentacion` es de solo lectura durante todo el proyecto.** Los aprendizajes se
> dejan como ADR en [`docs/adr/`](docs/adr/) y se destilan a la biblioteca **al terminar**, nunca a
> mitad.

### 2 · Configurar el entorno

```bash
cp deploy/.env.example deploy/.env
# Edita deploy/.env: al menos POSTGRES_PASSWORD y JWT_SIGNING_KEY.
# Para la clave de firma:  openssl rand -base64 48
```

`deploy/.env` está en `.gitignore` y **no se commitea nunca**. Ningún secreto vive en el
repositorio: todo entra por variables de entorno.

### 3 · Levantar el entorno completo

El compose vive en `deploy/` (estructura del §12 del plan maestro), así que **todos** los comandos
llevan `-f`:

```bash
docker compose -f deploy/docker-compose.yml up --build
```

| Servicio | URL | Qué es |
|---|---|---|
| Frontal | http://localhost:5173 | `bastion-web` servido por nginx |
| API | http://localhost:8080 | host único de ASP.NET Core |
| Sonda de vida | http://localhost:8080/health/live | «el proceso responde» — sin dependencias |
| Sonda de disponibilidad | http://localhost:8080/health/ready | «puedo atender tráfico» — sí mira la base |
| Trazas (Jaeger) | http://localhost:16686 | visor de trazas del entorno local |
| PostgreSQL | `localhost:5432` | base `bastion_dev` |

Para parar y borrar los datos: `docker compose -f deploy/docker-compose.yml down -v`.

---

## Desarrollo

```bash
# Backend
dotnet build Bastion.sln
dotnet test  Bastion.sln --filter "Category!=Integracion"   # rápido, sin Docker
dotnet test  Bastion.sln                                    # todo, con Testcontainers
dotnet format Bastion.sln --verify-no-changes

# Frontal
npm --prefix frontend ci
npm --prefix frontend run dev          # servidor de desarrollo en :5173
npm --prefix frontend run build
npm --prefix frontend run test
npm --prefix frontend run lint
```

La lista completa está en **[`AGENTS.md`](AGENTS.md) → *Comandos del proyecto***, que es lo que lee
el agente. Esos cinco comandos —build, test, format, build del frontal y lint— son exactamente lo
que ejecuta la CI: pasarlos en local antes de commitear evita el ciclo caro de descubrirlo en el
*runner*.

---

## Estructura

```
Bastion/
├── src/
│   ├── Api/                      host único: arranque, middleware, DI, OpenAPI
│   ├── BuildingBlocks/           Domain · Application · Infrastructure (lo común)
│   └── Modules/<Modulo>/         Domain · Application · Infrastructure · Contracts · Endpoints
├── tests/
│   ├── Arquitectura.Tests/       fitness functions: fronteras de módulo y de capa
│   ├── Api.FunctionalTests/      contrato de extremo a extremo
│   └── <Modulo>.{Unit,Integration}Tests/
├── frontend/                     React + Vite (app · features · shared)
├── db/
│   ├── migraciones/              generadas por EF Core, versionadas
│   └── semillas/                 PGC, tipos de IVA, unidades, países — configuración, no esquema
├── docs/
│   ├── PLAN.md                   estado del trabajo (fuente de verdad)
│   ├── adr/                      decisiones con su contexto y consecuencias
│   ├── dominio/                  glosario del lenguaje ubicuo, diagramas E-R y de estados
│   └── api/                      OpenAPI publicado por CI
├── deploy/                       docker-compose · Dockerfile.api · Dockerfile.web · nginx
└── .github/workflows/            CI
```

**Las carpetas de `frontend/src/features/` espejan los módulos del backend.** Una funcionalidad
nunca importa de otra: lo compartido sube a `shared/`.

### Las cinco fronteras que comprueba la CI

Los tests de arquitectura fallan —y con ellos la CI— si alguna se rompe. Sin ellos, las fronteras de
un monolito modular se erosionan en tres semanas.

1. Un módulo solo referencia el proyecto **`Contracts`** de otro, nunca su `Domain`, `Application`
   ni `Infrastructure`.
2. Ningún `Domain` referencia EF Core, ASP.NET Core ni nada de infraestructura. El dominio no sabe
   que existe una base de datos.
3. Ninguna consulta cruza esquemas.
4. Sin claves foráneas entre esquemas: se guarda el identificador y se valida contra el contrato.
5. Las escrituras entre módulos van **solo por eventos**.

---

## Por qué el ERP es correcto y no solo funcional

Diecisiete reglas duras gobiernan el dominio (§6 del plan maestro). Estas cuatro son las que **no
admiten segunda oportunidad**, porque son decisiones de esquema que salen gratis el primer día y
cuestan una migración manual sobre datos sucios el segundo:

- **Los libros son *append-only*.** El stock es un libro mayor, no un contador; las existencias son
  una proyección. La contabilidad funciona igual, y además cuadra. Corregir es asentar el inverso,
  nunca editar.
- **Tres fechas, tres columnas:** devengo, expedición y cobro. Ni son la misma ni se derivan una de
  otra — una entrega del 28 de diciembre facturada el 12 de enero se declara en el cuarto trimestre
  y se numera en la serie del año nuevo. Un modelo con una sola fecha se equivoca en dos de las tres
  cosas, y el error solo se ve en el cierre.
- **Suprimir no es borrar: es bloquear.** El art. 32 de la LOPDGDD obliga a reservar los datos
  impidiendo su tratamiento *incluida la visualización*. Un `activo = false` que sigue apareciendo en
  informes está oculto en la interfaz, que es justo lo que la norma prohíbe.
- **Direcciones en campos estructurados.** El SEPA Credit Transfer Rulebook retira el formato no
  estructurado el **15 de noviembre de 2026**: a partir de ahí, una transferencia con la dirección en
  texto libre no se cursa.

Y la que gobierna el dinero: **`decimal` con divisa** (`numeric(18,4)` importes, `numeric(18,6)`
unitarios), nunca coma flotante, con el redondeo aplicado **por base imponible y tipo impositivo** —
ni línea a línea ni al total.

> *En un ERP, los datos son el producto.* El código se reescribe en un fin de semana; un histórico de
> existencias mal registrado no se arregla nunca.

---

## Documentación

| Dónde | Qué |
|---|---|
| `ERP-PLAN-MAESTRO.md` | **La especificación del producto.** Stack, arquitectura, dominio, las diecisiete reglas y la hoja de ruta. Lo aporta el usuario **fuera del repositorio**; es la fuente de verdad y no se contradice |
| [`docs/PLAN.md`](docs/PLAN.md) | El **estado del trabajo**: checklist con criterios de aceptación, decisiones tomadas y dónde retomar |
| [`CLAUDE.md`](CLAUDE.md) | Memoria del agente: qué convenciones importa, política de commits, puerta de clarificación |
| [`AGENTS.md`](AGENTS.md) | Contrato operativo del agente y comandos del proyecto |
| [`docs/adr/`](docs/adr/) | Decisiones de arquitectura, con su contexto y sus consecuencias |

---

## Licencia

Sin licencia pública por ahora. Todos los derechos reservados.
