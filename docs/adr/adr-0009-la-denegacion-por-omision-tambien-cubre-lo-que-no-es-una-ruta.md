---
tipo: referencia
stack: [dotnet, aspnetcore]
aplica_a: [autenticacion, seguridad, api-rest, testing]
revisado: 2026-08-26
tags: [adr, autorizacion, fallback-policy, 401, 404, enrutado]
---

# ADR-0009: La denegación por omisión también cubre lo que no es una ruta

- **Estado:** aceptado
- **Fecha:** 2026-08-26

## Contexto

El ítem 0.5 cierra la API: se deniega por omisión y se abre con un permiso explícito. En ASP.NET
Core eso es una línea:

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
```

La palabra *fallback* sugiere «lo que se aplica a los endpoints que no dicen nada». Y es verdad,
pero **no es toda la verdad**: el middleware de autorización aplica la política de respaldo también
cuando `HttpContext.GetEndpoint()` devuelve **nulo**, es decir, cuando la petición **no casa con
ninguna ruta**.

Se descubrió por el efecto y no leyendo la documentación: al poner esa línea, **once de los
diecinueve** tests funcionales que ya estaban en verde se pusieron en rojo con 401. Ninguno tenía
que ver con la autorización — eran los de la política de errores, que piden rutas inventadas a
propósito para comprobar qué `ProblemDetails` sale.

## Decisión

**Se acepta el comportamiento y no se rodea.** Una petición sin credenciales a una ruta que no
existe recibe **401**, no 404.

Es, además, el comportamiento deseable: un 404 le confirma a quien sondea que esa ruta *no* existe,
y por descarte, que las que contestan otra cosa sí. Con la puerta delante, el que no se ha
identificado no distingue una ruta real de una inventada, que es exactamente lo que hay que
contarle. **Quien sí se ha identificado recibe 404**, porque para él la diferencia entre «no
existe» y «no te dejo» es información legítima y necesaria para depurar.

Consecuencias en el árbol:

1. **Las sondas de salud llevan `AllowAnonymous()` explícito.** `/health/live` y `/health/ready` se
   publican con endpoints propios, así que la política de respaldo se les aplicaría igual que a un
   controlador. Un orquestador que consulta la sonda de vida sin token recibiría 401 y reiniciaría
   el contenedor **en bucle**: el sistema entero caído por una línea de autorización, y el registro
   diciendo la verdad todo el rato.
2. **Los tests que fabrican rutas para que fallen tienen que publicarlas como endpoints.** No basta
   con un middleware que intercepte la ruta: si no hay `Endpoint` en el contexto, la petición muere
   en el 401 antes de llegar a él. Ahora se registra un `Endpoint` de verdad, con
   `AllowAnonymousAttribute` en sus metadatos, **antes** de encadenar el resto de la tubería.
3. **`UseAuthentication()` va siempre antes de `UseAuthorization()`.** La segunda decide sobre el
   principal que reconstruye la primera; al revés, la autorización mira un anónimo y contesta 401 a
   todo el mundo, traiga token o no. Es otro fallo que deja el registro impecable.

## Consecuencias

- Los dos lados del comportamiento están **probados**, y no descritos:
  - anónimo a ruta inexistente → 401, en `Api.FunctionalTests`
    (`UnaRutaQueNoExiste_LeResponde401AlAnonimoYTambienEnProblemDetails`);
  - identificado a ruta inexistente → 404 con `application/problem+json`, en
    `Api.IntegrationTests` (`Una_ruta_que_no_existe_es_404_para_quien_si_se_ha_identificado`).
- Cuando llegue el ítem 0.11, el frontal tiene que tratar el **401 sobre una URL desconocida** como
  «esta ruta no existe» y no como «la sesión ha caducado», o un enlace roto sacará al usuario de la
  aplicación.

## El aprendizaje, que es lo que hace que esto sea un ADR

Nada de esto salió de leer la configuración: salió de que **once tests que no hablaban de
autorización se pusieron rojos**. Una cadena de autorización mal emparejada se construye sin error,
deja el registro correcto y no avisa de nada; lo único que la delata es una petición de verdad y su
código de respuesta.

De ahí la forma que tienen los tests de este ítem: barren **la tabla de rutas que el host ha
construido** —`IActionDescriptorCollectionProvider`, la misma que usa el enrutado para servir— y
mandan una petición por cada acción, sin credenciales y con las equivocadas. Una lista escrita a
mano solo prueba lo que alguien se acordó de escribir, y la regla que falta es siempre la que nadie
recordó.
