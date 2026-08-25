---
tipo: referencia
stack: [dotnet]
aplica_a: [csharp, aspnetcore]
revisado: 2026-08-25
tags: [adr, errores, resultado, excepciones, problemdetails, rfc9457, observabilidad]
---

# ADR-0004: Dónde acaba el `Resultado` y dónde empieza la excepción

- **Estado:** aceptado
- **Fecha:** 2026-08-25

## Contexto

Hay dos fuentes normativas y, leídas de corrido, parecen decir cosas distintas.

El **plan maestro** (§12, Anexo A.4, no reabrible) sitúa un tipo `Resultado` en el bloque común de
dominio, junto a `Importe`. El ítem 0.3 lo pide explícitamente: «resultado de operación».

`principios/manejo-errores.md` dice, con la misma claridad: «**Usa excepciones** en lugar de
devolver códigos de error (CC 35/84) **en la lógica de negocio de aplicación**», y pone en su lista
de *Nunca* «funciones que devuelven códigos de error que nadie comprueba».

Resolver esto caso por caso, a ojo, es la peor de las opciones: acabaríamos con la mitad del sistema
devolviendo `Resultado` y la otra mitad lanzando, sin que nadie sepa cuál toca. Hace falta una
frontera escrita.

## Decisión

### La regla

> Una operación devuelve **`Resultado`** cuando su fallo es un **desenlace previsto del negocio** que
> quien la pidió tiene que poder **distinguir para hacer algo distinto**.
>
> **Lanza** cuando el fallo significa que el **código está mal escrito**, que una **invariante del
> dominio** se ha roto, o que la **infraestructura** no está.

Para aplicarla, tres preguntas en este orden:

1. ¿Va a pasar en producción de forma **rutinaria**, con datos correctos y gente haciendo su
   trabajo? → `Resultado`.
2. ¿El cliente necesita **distinguirlo** de los demás fallos para reaccionar distinto? → `Resultado`.
3. ¿Significa que alguien se ha saltado una validación, que una invariante ha reventado o que una
   dependencia no responde? → **excepción**.

### Dónde vive cada cosa

| Sitio | Qué hace |
|---|---|
| **Dominio** (entidades, objetos de valor, servicios de dominio) | **Siempre lanza.** Nunca devuelve `Resultado`. |
| **Guardas de argumento**, en cualquier capa | **Siempre lanzan** (`ArgumentException` y familia). |
| **Infraestructura** (base de datos, red, ficheros) | **Lanza.** Sube hasta el borde sin traducirse. |
| **Aplicación** (casos de uso) | **La única** que devuelve `Resultado`, y solo hacia fuera. |
| **Borde** (endpoints) | **Consume** el `Resultado` y lo traduce a HTTP. Ahí se acaba. |

`Resultado` cruza **una sola costura**: de Aplicación al borde. No vuelve a entrar hacia dentro, no
se propaga entre módulos y **no aparece en el dominio**. Esa restricción es lo que impide que se
convierta en el «código de error que nadie comprueba» del que avisa el documento de principios.

### Cuatro ejemplos que no se prestan a duda

1. **Confirmar un pedido que ya estaba confirmado** → `Resultado` fallido, con
   `ErrorDeOperacion.Conflicto("pedido-ya-confirmado", …)`, que el borde traduce a **409**.
   Pasa todos los días —dos pestañas abiertas, un doble clic— y el cliente tiene que poder
   distinguirlo de «ese pedido no existe» para decir algo útil en pantalla.

2. **Reservar stock cuando no hay existencias** → `Resultado` fallido, con
   `ErrorDeOperacion.ReglaDeNegocio("stock-insuficiente", …)` → **422**. Los datos son válidos y el
   estado es coherente; lo que lo impide es una **regla**. Es el caso canónico del tipo.

3. **`Importe.De(1m, "€")`** → **lanza** `ArgumentException`. `€` no es un código ISO 4217; que ese
   valor llegue hasta ahí significa que nadie validó en el borde. No hay respuesta de negocio que
   dar, porque no hay negocio: hay un fallo de programación.

4. **`Importe.De(10m, "EUR") + Importe.De(10m, "USD")`** → **lanza** `InvalidOperationException`. Es
   una invariante del dominio. Si esto devolviera `Resultado`, cada suma del sistema tendría que
   comprobarlo, y la primera que se olvidara sumaría euros con dólares como si fueran lo mismo.

### El error lleva un código estable, no solo un mensaje

`ErrorDeOperacion` tiene **`Codigo`** y **`Mensaje`**, y son cosas distintas:

- **`Codigo`** es **contrato**. Se publica como `type` del ProblemDetails (`/errors/{codigo}`) y un
  cliente puede ramificar sobre él. No se cambia sin romper a alguien. Se valida al construirlo:
  minúsculas y guiones, porque viaja dentro de un URI.
- **`Mensaje`** **no** es contrato. Se puede reescribir, traducir o afinar sin avisar.

Un error que solo lleva mensaje obliga al cliente a comparar cadenas de texto, que es la forma más
frágil que existe de acoplar dos sistemas.

### Por qué esto NO contradice `principios/manejo-errores.md`

Las tres razones salen del propio documento:

1. **Lo que la regla prohíbe es otra cosa.** Prohíbe el código de error mágico que se propaga por
   todo el núcleo y que alguien acaba sin comprobar. `Resultado` no se propaga por el núcleo —el
   dominio no lo usa—, cruza una sola costura, y el tipo **obliga** a mirar `EsCorrecto` antes de
   leer `Valor`: leerlo en un resultado fallido lanza.

2. **El documento ya admite el retorno con error explícito cuando el error es contrato.** Dice que
   los códigos de error de un protocolo o contrato de red «son **contrato** y son legítimos», y que
   «se traducen a tu modelo interno **en el borde**». Aquí es exactamente el mismo movimiento en la
   dirección contraria: `Codigo` es el contrato, y el borde lo traduce.

3. **«Define el flujo normal, no el excepcional» (CC 89).** Un stock insuficiente no es excepcional:
   ocurre a diario. Gobernarlo con excepciones es justo lo que la lista de *Nunca* del documento
   prohíbe cuando dice «lógica de negocio gobernada por excepciones».

## Cómo cruza el borde: `ProblemDetails` (RFC 9457)

### Una sola política, central, en middleware

`AgregarPoliticaDeErrores()` y `UsarPoliticaDeErrores()` cablean **lo único** que traduce errores
hacia fuera. No hay `try/catch` por controlador devolviendo un 500 a mano, y no lo habrá: un
manejador por endpoint deja sin cubrir precisamente los sitios **donde no hay endpoint** —una ruta
que no existe, un fallo de enrutado, una excepción en otro middleware—, que son los que aparecen a
las tres de la mañana. Va **lo primero** de la tubería, porque un manejador de excepciones solo cubre
lo que tiene por dentro, y **por fuera** del registro de peticiones, para que cada 500 no se registre
dos veces con su traza entera.

La correspondencia con los códigos de estado (§9) vive en un único sitio:

| `TipoDeError` | Estado | Cuándo |
|---|---|---|
| `Validacion` | 400 | Los datos no cumplen el contrato de entrada. |
| `PermisoDenegado` | 403 | Identificado, pero sin permiso. |
| `NoEncontrado` | 404 | No existe, o no es visible para quien pregunta. |
| `Conflicto` | 409 | El estado no admite la operación, o hay concurrencia. |
| `ReglaDeNegocio` | 422 | Datos válidos, estado coherente, y una regla lo impide. |

El **dominio no sabe que existe HTTP**: devuelve un `TipoDeError`, y esta tabla —que está en
`Infrastructure`, no en `Domain`— es la única que sabe traducirlo.

### Dos destinatarios que no comparten texto

El de fuera necesita saber **qué hacer**; el de dentro, **qué ha pasado**. Fundirlos es cómo el texto
de una excepción acaba publicando rutas de disco, consultas SQL y nombres de tabla.

Por eso el manejador de excepciones **nunca** compone la respuesta con el texto de la excepción: la
excepción entera va al **registro**, y la respuesta lleva un texto **fijo** por clase de fallo. Y por
eso la excepción **no se pasa** al `ProblemDetailsContext`: si no está a mano, no puede colarse ni
hoy ni cuando alguien añada una personalización dentro de un año.

Esto no se comprueba leyendo el código; se comprueba **mandando basura y leyendo lo que vuelve**. El
test funcional manda un marcador por la cadena de consulta, ese marcador acaba dentro del mensaje de
la excepción, y luego afirma las **dos** direcciones a la vez: que el marcador **está** en el
registro y que **no está** en la respuesta.

### El identificador de traza de la respuesta es el mismo que el del registro

Todo `ProblemDetails` lleva una extensión `traceId` con `Activity.Current.TraceId`, que es **el mismo
valor** que Serilog escribe como `@tr`. No es el `TraceIdentifier` de Kestrel ni el `traceparent`
entero: si no coincidieran exactamente, pedirle a alguien «dame el identificador que te salió» no
localizaría nada, que es justo para lo que sirve.

Se afirma en un test, no se supone: la petición entra con un `traceparent` conocido, y el test
comprueba que ese mismo valor sale en el `traceId` de la respuesta **y** aparece como `@tr` en la
línea del registro.

## Consecuencias

- **`BuildingBlocks.Infrastructure` pasa a conocer ASP.NET Core** (`FrameworkReference` a
  `Microsoft.AspNetCore.App`). Es inevitable: la política de `ProblemDetails` que el §12 sitúa en ese
  proyecto *es* HTTP. Los otros dos bloques comunes siguen limpios —`Domain` no referencia nada y
  `Application` solo referencia a `Domain`—, así que la frontera que importa sigue en pie y el
  compilador la sigue vigilando. Efecto colateral: el paquete
  `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` se vuelve redundante (viene en el
  marco compartido) y NuGet lo señala con `NU1510`; se ha quitado.
- **Añadir una clase de error nueva** es añadir un valor a `TipoDeError` y su fila a las dos tablas
  de `PoliticaDeErrores`. Los `switch` no tienen rama por omisión silenciosa: lanzan
  `NotSupportedException` si alguien añade un valor y olvida la traducción.
- **Un endpoint no puede devolver un error de negocio sin pasar por aquí**, porque la única forma de
  convertir un `ErrorDeOperacion` en respuesta es `ARespuesta()`, y esa escribe a través de
  `IProblemDetailsService` —no de `Results.Problem`— para pasar por la misma personalización central
  que todo lo demás. Si no, estas respuestas serían las únicas sin `traceId`.
- **Errores por campo en validación** (§9) todavía no existen: ningún caso de uso los pide en la fase
  0. Cuando lleguen, entran como extensión `errors` del mismo `ProblemDetails`.

## Lo que salió de aquí y vive en otro sitio

Escribir los tests de esta política destapó una trampa que **no es de errores**: dos de ellos pasaban
en aislado y fallaban con la suite entera, porque `AddSerilog(configurar)` deja el registro en el
`Log.Logger` estático. Es un problema de *toolchain*, transversal a todo el proyecto, y por eso no se
archiva dentro de un ADR sobre la frontera `Resultado`/excepción: está en
[`adr-0006-un-test-que-solo-se-ejecuta-aislado-no-esta-probado.md`](adr-0006-un-test-que-solo-se-ejecuta-aislado-no-esta-probado.md).
