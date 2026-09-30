---
tipo: referencia
stack: [nginx, dotnet, http]
aplica_a: [concurrencia, despliegue, proxy, api-rest]
tags: [adr, etag, if-match, gzip, nginx, rfc-9110, humo]
revisado: 2026-09-30
---

# ADR-0049: El proxy no comprime lo que lleva versión

- **Estado:** aceptado
- **Fecha:** 2026-09-30
- **Sale del ítem 2.9**, al probar contra la pila de verdad la primera pantalla que escribe: la de
  la trazabilidad del artículo. Lo decidió el agente; es reversible y no toca lo cerrado en A.4.

## Contexto

La concurrencia optimista del proyecto publica la versión de un recurso en `ETag` y la pide de
vuelta en `If-Match` (`ConVersion`, ADR-0014). La API exige una etiqueta **fuerte y
entrecomillada**, y contesta `400 if-match-no-valido` a cualquier otra cosa. Es lo que dice la RFC
9110: `If-Match` compara en fuerte, y una etiqueta débil no casa nunca.

nginx sirve el frontal y reenvía `/api/` a la API por el mismo origen. Tenía `gzip on` para todo el
`server`, con `application/json` en `gzip_types`. Y **nginx debilita la etiqueta de toda respuesta
que comprime**: desde la 1.7.3, una modificación del cuerpo convierte `"797"` en `W/"797"`. Medido
en un proyecto de *compose* aparte, pidiendo compresión como la pide un navegador:

```
GET api directa: 200 etag='"797"'   content-encoding=None
GET por nginx:   200 etag='W/"797"' content-encoding='gzip'
PUT por nginx con If-Match='W/"797"': 400 /errors/if-match-no-valido
```

**`gzip_min_length 1024` no la salvaba**, aunque la ficha de un artículo ocupa menos: nginx solo
mide el tamaño por `Content-Length`, y la API escribe el JSON a trozos (`transfer-encoding:
chunked`), sin esa cabecera. Sin ella, comprime cualquier tamaño.

**Nada lo había visto**, porque ninguna pantalla escribía hasta el 2.9. Los tests de integración
hablan con la API sin nginx en medio. Los del frontal usan msw, que no comprime, y el proxy de Vite
tampoco. El humo de la CI pasaba por nginx, pero solo con lecturas y sin comparar la etiqueta.

## Decisión

**`gzip off` en la `location /api/` de `deploy/nginx.conf`.** El proxy no transforma una respuesta
que lleva versión. Lo estático sigue comprimido: `/assets/` hereda el `gzip on` del `server`, y el
JavaScript sale con `Content-Encoding: gzip` (medido en la misma pasada).

Las alternativas, y por qué no:

- **Que la API acepte etiquetas débiles en `If-Match`.** Va contra la RFC 9110. Además, «la misma
  versión con otra representación» es justo lo que la comparación fuerte existe para no dar por
  bueno.
- **Que el frontal quite el `W/` antes de mandarla.** El cliente devolvería una etiqueta que no
  recibió, y funcionaría solo mientras sepa qué hace el proxy.
- **Que comprima la API** (`ResponseCompression`), y nginx no toque lo que ya llega comprimido. Es
  otro *middleware* que configurar. Y ASP.NET no comprime por HTTPS por omisión, por BREACH: la
  respuesta de la sesión lleva el testigo.
- **`gzip_proxied`.** No sirve: decide por la cabecera `Via` de la **petición**, no por que la
  respuesta venga de un `proxy_pass`.

## Consecuencias

- **El JSON de la API viaja sin comprimir.** Los listados van paginados, a 20 por página si no se
  pide otra cosa y a 200 como mucho (`Paginacion`), así que es poco. Si un día pesa, la compresión
  va en la API, con BREACH resuelto, y no en el proxy.
- **El humo de la CI lo comprueba por el efecto.** El paso «El frontal no toca la versión de lo que
  reenvía» lee una empresa por la API y por el frontal, pidiendo compresión, y exige la misma
  etiqueta fuerte. Se midió en rojo contra el nginx de antes y en verde con este. Borra las
  cabeceras antes de cada petición y exige el 200: sin eso, un frontal caído dejaría leer las de la
  API y saldría verde.
- **Lo transversal:** cualquier intermediario que reescriba el cuerpo —comprimir, minificar,
  inyectar— debilita o quita la `ETag` fuerte. Una API con `If-Match` necesita que su camino hasta
  el navegador lo respete, y eso solo se ve con el intermediario de verdad delante.

## Procedencia

Lo encontró el agente al verificar la pantalla del 2.9 contra la pila de verdad, antes de darla por
hecha. La medida, la decisión y el paso del humo son del agente.
