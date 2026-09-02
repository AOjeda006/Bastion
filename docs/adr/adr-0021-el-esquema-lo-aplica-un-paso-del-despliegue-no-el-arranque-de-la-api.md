---
tipo: referencia
stack: [dotnet, docker, postgresql]
aplica_a: [entrega-continua, docker, api-rest, bases-de-datos]
tags: [adr, migraciones, despliegue, compose, arranque, ddl]
revisado: 2026-09-02
---

# ADR-0021: El esquema lo aplica un paso del despliegue, no el arranque de la API

- **Estado:** aceptado
- **Fecha:** 2026-09-02
- **Relacionado:** cierra la deuda más antigua de la fase 0, abierta el 26 de agosto con el ítem
  0.4. Condiciona todos los entornos de aquí en adelante.

## Contexto

Desde el ítem 0.4 hay migraciones de EF Core en `db/migraciones/<Modulo>/` y tres contextos con
persistencia. Los tests de integración las aplican ellos mismos con Testcontainers, y ahí el
esquema siempre está al día. **En el entorno desplegado no las aplicaba nadie.** Ni un paso del
`docker-compose.yml`, ni un contenedor de inicialización, ni el arranque de la API: nadie.

Eso no se veía, y el porqué es el interesante. El *job* de Humo comprobaba la sonda de vida, la de
disponibilidad, que el frontal carga, que el frontal reenvía `/api` y que las trazas llegan al
visor. **Ninguna de esas peticiones toca una tabla.** La sonda de disponibilidad pregunta si
PostgreSQL acepta conexiones y responde, y eso es cierto en una base vacía. El entorno estaba verde
y no podía atender una petición que leyera datos.

Medido antes de arreglarlo, sobre el `docker-compose.yml` tal y como se publicaba:

```
/health/live   -> 200 Healthy
/health/ready  -> 200 {"estado":"Healthy", "base-de-datos":"Healthy",
                       "descripcion":"PostgreSQL acepta conexiones y responde."}
POST /api/v1/identidad/sesiones -> 500
```

Y con las siete `BASTION_SEMILLA_*` rellenas —el camino que nadie había ejercido todavía— la API
**ni siquiera arrancaba**: la semilla consulta `organizacion.empresas` antes de crear nada, la
tabla no existe, la excepción sube hasta `Program.cs` y el contenedor entra en bucle de reinicio.

```
Unhandled exception. Npgsql.PostgresException (0x80004005):
42P01: relation "organizacion.empresas" does not exist
   at Bastion.Organizacion...ConsultaDeEmpresas.PrimeraActivaAsync(...)
   at Bastion.Api.Arranque.SemillaDeArranque.SembrarAsync(WebApplication app)
   at Program.<Main>$(String[] args)
```

Las dos averías son la misma: **falta el DDL**. Lo que hay que decidir es quién lo aplica.

## Decisión

**Un contenedor de un solo uso, construido del mismo artefacto que la API, invocado con
`--migrar`.** Aplica las migraciones pendientes de los tres módulos en orden y sale. El resto de
servicios espera a que salga **bien**:

```yaml
migraciones:
  <<: *imagen-de-la-api
  command: ['--migrar']
  restart: 'no'
  depends_on:
    postgres: { condition: service_healthy }

api:
  depends_on:
    migraciones: { condition: service_completed_successfully }
```

**La API no migra nunca al arrancar.** Con `--migrar` migra y sale; sin él, ni mira si hay
migraciones pendientes.

Cuatro detalles que forman parte de la decisión:

1. **Se pide por argumento, no por variable de entorno.** Una variable se hereda: basta con que
   alguien la ponga en el `.env` compartido para que *todas* las réplicas se conviertan en
   migradoras. Un argumento se escribe servicio a servicio y se ve en `docker compose config`.
2. **Es el mismo artefacto**, no un segundo host más pequeño. Un migrador con su propio cableado
   sería un segundo sitio donde se dice el proveedor, dónde vive el historial y qué convención de
   nombres se aplica; dos cableados divergen, y el día que divergieran prepararía un esquema que no
   es el que la API espera.
3. **Orden fijo: Auditoría → Organización → Identidad.** Auditoría es la dueña de
   `auditoria.registros`, y los otros dos escriben ahí en cuanto guardan algo. Es el mismo orden
   que usa el arranque de los tests de integración.
4. **El código de salida es el contrato.** La excepción se captura y se sale con `1` en vez de
   dejarla subir: una excepción sin capturar sale con 134 y un volcado de pila, y lo que
   `service_completed_successfully` lee es el código. El diagnóstico va al registro estructurado,
   con una línea por migración pendiente y por su nombre.

## Alternativas y su precio

**El arranque de la API migra.** Es la más cómoda —cero infraestructura, funciona en un `dotnet
run`—, y es la que se descarta. Con una sola réplica es correcta; con dos es una avería: dos
procesos que arrancan a la vez ejecutan DDL a la vez, y el que pierde la carrera se encuentra el
esquema a medio cambiar. Peor que la carrera es la atribución: el cambio de esquema lo aplica *la
réplica que arranque primero*, o sea nadie en concreto, y cuando falla no hay un paso al que mirar,
hay un servidor web que no levanta. Y hace falta darle permisos de DDL al usuario con el que corre
la aplicación en régimen normal, que es justo el que no debería tenerlos.

**Un paso del `docker-compose.yml` con el cliente de línea de órdenes (`dotnet ef database
update`).** Se descarta por dos motivos. Necesita el SDK y el proyecto en el contenedor —la imagen
de ejecución no los lleva—, así que obligaría a construir y publicar una segunda imagen mucho más
gorda solo para esto. Y aplicaría las migraciones desde el **código fuente**, no desde el
artefacto: el binario que migra dejaría de ser el binario que se despliega, que es exactamente la
distancia por la que se cuelan las diferencias que nadie ve.

**Precio de la elegida.** El contenedor recibe configuración que no usa, incluida la clave de firma
de los JWT: es el mismo artefacto y el mismo bloque de entorno, así que hereda todo. Se acepta a
cambio de no tener dos cableados. Lo único que se le cambia es el extremo OTLP, que se deja vacío
—un proceso que vive tres segundos no alcanza a exportar nada y sí alcanza a llenar el registro de
errores de red—, y la sonda de la imagen, que se desactiva porque pregunta por HTTP a un proceso
que aquí no escucha.

## Lo que queda fuera, y por qué

**La semilla de arranque sigue en la API, no en el migrador.** Es dato, no DDL, y su guarda es
distinta: se aplica solo si no hay *ningún* usuario, así que repetirla no crea una segunda cuenta.
Moverla al migrador obligaría a darle a un segundo servicio las siete `BASTION_SEMILLA_*`, entre
ellas la contraseña de la primera cuenta, y las credenciales cuantos menos sitios visiten, mejor.
Queda anotado en `docs/PLAN.md` que con varias réplicas la guarda «no hay ningún usuario» es una
comprobación y una escritura sin transacción común, y que eso hay que resolverlo antes de que haya
más de una réplica; hoy no la hay.

## Consecuencias

- El esquema pasa a ser **un paso del despliegue con su propio resultado**: verde o rojo,
  atribuible, con su registro, en vez de un efecto secundario del arranque de un servidor web.
- El *job* de Humo gana una sonda que **lee tablas y devuelve datos** —iniciar sesión con la cuenta
  sembrada y listar empresas con el testigo— y con ella el entorno desplegado deja de poder estar
  verde estando vacío. La sonda se comprobó contra el estado anterior: daba `500`.
- Cualquier entorno futuro (`cd.yml`, un Kubernetes, un despliegue manual) hereda la forma: primero
  el migrador, y solo si termina bien, la aplicación. Un `Job` de Kubernetes con un `initContainer`
  es la misma idea con otro nombre.
- Añadir un módulo con persistencia en la fase 1 obliga a añadir su contexto a `MigradorDeArranque`,
  y en el orden que le toque respecto de Auditoría.
