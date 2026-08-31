---
tipo: referencia
stack: [dotnet, aspnetcore, typescript, ci]
aplica_a: [api-rest, entrega-continua, frontend]
tags: [adr, openapi, contrato, generacion, deriva, nuget-audit]
revisado: 2026-08-31
---

# ADR-0018: El contrato se genera, se versiona, y la CI lo vuelve a generar

- **Estado:** aceptado
- **Fecha:** 2026-08-31
- **Relacionado:** habilita el criterio del ítem 0.11 («cliente de API generado desde el OpenAPI»).
  Comparte forma con el paso *Migraciones — el modelo no tiene cambios pendientes* del ítem 0.4.

## Contexto

El ítem 0.11 exige que el cliente de la API del frontal **se genere desde el OpenAPI**. Hasta aquí
no había documento: `docs/api/` solo tenía un `.gitkeep`, y el paso `Publicar el OpenAPI` de la CI
llevaba nueve ítems saliendo como `skipped` porque su guarda —`hashFiles(...) != ''`— nunca casaba.
Nada de eso era una regresión: era el hueco que este ítem viene a llenar.

Escribir a mano los tipos del contrato en TypeScript da **dos fuentes de verdad**: el día que un DTO
gana un campo, el frontal compila igual y falla en ejecución. Generarlos resuelve eso solo si el
documento del que se generan está al día — si no, se tienen las mismas dos fuentes de verdad con un
paso extra en medio, y encima con la apariencia de que una deriva de la otra.

Había dos formas viables de conseguirlo, y la decisión era cuál.

## Alternativas descartadas

**Encadenar los trabajos y pasarse el documento como artefacto.** El trabajo `Backend` genera el
documento, lo sube, y `Frontal` lo descarga y genera el cliente. Es correcto y no versiona nada
derivado, pero cuesta caro: obliga a que `Frontal` dependa de `Backend` —hoy corren en paralelo y
el frontal falla en segundos—, y deja al desarrollador sin el fichero delante. El contrato dejaría
de verse en el `diff` de una revisión, que es justo donde un campo que cambia de nombre tiene que
notarse.

**Servir el documento por HTTP y generar el cliente contra el servidor.** `MapOpenApi()` y un
`openapi-typescript http://localhost:8080/openapi/v1.json`. Se descarta por dos motivos
independientes: exigiría levantar la API para poder compilar el frontal, y sobre todo abriría el
**único endpoint anónimo** de una API que deniega por defecto (ADR-0009) — el catálogo completo de
rutas y esquemas servido sin credenciales. Uno autenticado no serviría para generar nada.

**Generarlo y no comprobarlo.** Es la que parece más barata y es la peor: el fichero versionado
envejece en silencio, el cliente se genera de un contrato de hace tres semanas, y el error aparece
como un `undefined` en una pantalla.

## Decisión

**El documento se genera al compilar, se versiona en `docs/api/openapi.json`, y la CI lo vuelve a
generar y falla si difiere.** Exactamente la forma del paso de migraciones, en la que el proyecto ya
confía: el artefacto está en el repositorio, se ve en la revisión, y un paso barato —segundos, sin
Docker, sin encadenar trabajos— comprueba que no ha divergido.

1. **`Microsoft.AspNetCore.OpenApi` construye el documento** desde las descripciones que ASP.NET
   Core ya publica, y `Microsoft.Extensions.ApiDescription.Server` engancha la generación al build.
   El contrato se escribe **una vez**, en los controladores y los DTO, con sus comentarios de
   documentación: título, descripciones y esquemas salen de ahí.

2. **No se sirve por HTTP.** `AddOpenApi()` sí; `MapOpenApi()` no. La generación ocurre al compilar
   y `OpenApiGenerateDocumentsOnBuild` va en `false`: construir el host entero en cada
   `dotnet build` cargaría el bucle interno por un fichero que solo cambia cuando cambia el
   contrato. Lo dispara `scripts/generar-openapi.sh`.

3. **Un solo script para las dos cosas.** `scripts/generar-openapi.sh` escribe el fichero;
   `scripts/generar-openapi.sh --comprobar` genera en un temporal y compara. Que la generación viva
   en un único sitio es lo que impide que «lo que se compara» y «lo que se escribe» se separen.

4. **Cuatro arreglos en `ContratoDeLaApi`,** porque lo que sale de fábrica no es publicable tal cual:
   el título era `Bastion.Api | v1` —el nombre de un proyecto de C# asomando en el contrato
   público—; ninguna operación traía `operationId`, así que cada generador se inventaría los nombres
   de los métodos; no había esquema de autenticación declarado; y **el cuerpo de toda petición salía
   descrito como «Cancelación de la petición en curso»**, porque la herramienta reparte los
   `<param>` del comentario entre los parámetros que ve y el `CancellationToken` se lleva el último
   sitio. Una descripción equivocada es peor que ninguna: se quita.

5. **Lo anónimo se declara anónimo.** Las tres acciones con `[AllowAnonymous]` —abrir, renovar y
   cerrar sesión— salen con `security: []` y las otras cuarenta y tres exigen el testigo. Un cliente
   generado que mandara el testigo al iniciar sesión no fallaría: haría algo que nadie ha pensado.

## Consecuencias

- **El `diff` de un cambio de contrato es visible.** Renombrar un campo de un DTO ya no es un cambio
  de C#: es un cambio de C# **y** un `diff` en `docs/api/openapi.json` que hay que commitear. Quien
  revisa ve las dos mitades juntas.

- **Olvidarse de regenerar pone la CI en rojo,** con el `diff` en el propio paso y la orden exacta
  para arreglarlo. Comprobado por el efecto: tocando el título del fichero versionado, el paso pasa
  de `notice` a `error` con código de salida 1, y vuelve a verde al restaurarlo.

- **La cadena de firma del JWT.** Generar el documento construye el host, y el host exige las tres
  variables del JWT. El script las genera al vuelo con `/dev/urandom` y no las escribe en ninguna
  parte, igual que `comprobar-migraciones.sh`. Esa clave no firma nada: solo hace que el host llegue
  a construirse.

- **Un aviso de seguridad que la restauración cazó.** `Microsoft.AspNetCore.OpenApi` 10.0.9 arrastra
  `Microsoft.OpenApi` 2.0.0, y esa versión tiene el GHSA-v5pm-xwqc-g5wc (gravedad alta: un esquema
  con referencias circulares aborta el análisis del documento). Con `NuGetAudit` como error, la
  restauración **falló**, que es lo que se quiere que pase. Se fija la 2.7.5 —la primera parcheada—
  con el fijado transitivo central. Es el mecanismo funcionando, no un contratiempo.

- **Lo que se ha dejado como está.** Cada respuesta declara `text/plain`, `application/json` y
  `text/json`, y cada cuerpo tres tipos de contenido, porque MVC negocia con los tres. Es ruido en el
  documento, pero apretarlo con un `[Produces]` global cambiaría la negociación de contenido en
  ejecución —incluidos los `application/problem+json` de la política de errores—, y eso es un cambio
  de comportamiento del servidor disfrazado de arreglo de documentación. Queda anotado, no hecho.
