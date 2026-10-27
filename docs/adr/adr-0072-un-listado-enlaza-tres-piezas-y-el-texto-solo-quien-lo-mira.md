---
tipo: referencia
stack: [dotnet, aspnetcore, efcore]
aplica_a: [api-rest, catalogo, identidad, inventario, organizacion, terceros]
tags: [adr, listado, paginacion, filtro, q, contrato, exportacion, 400, adr-0068, adr-0069, adr-0004, adr-0025]
revisado: 2026-10-27
---

# ADR-0072: Un listado enlaza tres piezas, y el texto solo lo enlaza quien lo mira

- **Estado:** aceptado
- **Fecha:** 2026-10-27
- **Sale del ítem 3.2**, la exportación de los listados. Su criterio, en `docs/PLAN.md`, termina
  así: «Y un `?q=` sin filtro declarado es un `400` (ADR-0069)». Es lo que el punto 3 del ADR-0069
  dejó propuesto, con su nota abierta en el PLAN desde el 2026-10-24, y la puerta de la fase 3 lo
  trajo a este ítem.
- **Enmienda** el [ADR-0069](adr-0069-un-q-sin-filtro-declarado-llega-y-no-filtra-nada.md) y el
  [ADR-0068](adr-0068-la-transferencia-se-abre-como-el-ajuste-con-dos-puntas-en-cada-linea.md):
  - del **0069**, los puntos 1 y 3 de su decisión: el `?q=` de un listado sin filtro declarado ya
    no llega y no filtra nada, es un `400` (puntos 2 y 3 de este), y el rechazo que dejaba
    propuesto se hace aquí;
  - del **0068**, el punto «Sin `?q=`» del §1, que el 0069 había corregido: el borde vuelve a
    contestar que el recurso no admite filtro de texto, que es lo que el punto decía.

## Contexto

Hasta este ítem, una acción de listado enlazaba **un solo tipo** de la URL, `ConsultaPaginada`, con
cuatro parámetros: `page`, `size`, `sort` y `q`. Los listados con un criterio propio lo heredaban
(`ConsultaDeMaestro` con `?retiradas=`, `ConsultaDeArticulos` con `?categoria=`…). Eso dejaba dos
mentiras del contrato, de las que no fallan:

- **Seis listados publicaban un `q` que no miraban** (ADR-0069). El paginador lo dejaba pasar
  cuando el repositorio no declaraba `Filtro`, y la respuesta era la página entera, con un `200`.
- **La exportación iba a heredar `page` y `size`**. Un fichero exportado lleva todas las filas que
  casan con el orden, los criterios y el texto, y ninguna página. Con un solo tipo, la acción que
  exporta habría publicado dos parámetros que no lee.

Con la exportación, la primera mentira empeora. Un cliente que manda `?q=` a uno de esos seis
listados y recibe la página entera puede notar que no ha filtrado, porque ve las filas. Si la
exporta, recibe un fichero con todo, con el nombre de lo que creía haber buscado.

## Decisión

1. **Una acción de listado enlaza tres piezas por separado**, todas con `[FromQuery]`:
   - `PaginaPedida`, con `page` y `size` y sus topes;
   - `ConsultaDeListado` o su derivada, con `sort` y los criterios propios del recurso;
   - `FiltroDeTexto`, con `q`, **solo si el listado lo mira**.

   `RespuestasDeListado` las compone en la `Paginacion` que entiende la aplicación. La exportación
   enlaza las dos últimas, y su contrato publica justo lo que lee.
2. **El texto lo enlazan diecisiete listados, y seis no**: las líneas de una tarifa, el saldo, las
   transferencias, las conversiones de unidades, los ejercicios y los tipos de cambio. Esos seis
   dejan de publicar `q` en `docs/api/openapi.json`. Ninguno tiene un campo que una persona escriba
   y que tenga sentido buscar.
3. **Un `?q=` con algo escrito a un listado que no enlaza el texto es un `400`**, con el `type`
   `/errors/filtro-no-admitido` y `errors.q`. En blanco o sin él, pasa, porque es lo que manda un
   formulario con la caja vacía. Lo decide `RespuestasDeListado`, que mira la cadena de consulta
   cuando la pieza del texto le llega nula: la acción no ha enlazado nada que lo diga, y nulo
   significa «este listado no tiene texto», no «no han mandado texto». Se mira **antes que el
   orden**: con los dos mal, el `400` es el del texto, que es el que dice qué sobra.
4. **El paginador lanza si le llega un texto escrito y los criterios no declaran `Filtro`**, con
   una `ArgumentException` de `filtro`. Es la guarda de detrás del borde, para quien componga la
   paginación sin pasar por él, y es una guarda de argumento, así que lanza (ADR-0004). No es un
   desenlace de negocio: llegar ahí es un fallo del código, como un orden que no se admite.
5. **Las búsquedas por `POST .../buscar` no cambian.** Llevan el criterio en el cuerpo porque es
   sensible (ADR-0025), y van por cursor, sin página ni texto en la URL.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| Los listados `GET` sin `q` son los seis, en una lista cerrada | `SoloPublicaElTextoQuienLoMiraTests` | rápido |
| La composición y el `400`, con sus parejas en blanco y con texto | `ElBordeComponeLasTresPiezasTests` | rápido |
| La guarda del paginador, con sus dos parejas que llegan a la base | `LaTraduccionASqlTests.Un_texto_sin_filtro_declarado_lanza_antes_de_llegar_a_la_base` | rápido |
| Un `?q=zz` a cada listado `GET`: `200` o `404` si lo enlaza, `400` si no | `ElTextoSoloLoRecibeQuienLoMiraTests` | integración |

La lista cerrada pone rojo un listado nuevo sin texto, y obliga a decidir si de verdad no tiene nada
que buscar. Uno nuevo con texto no la toca: lo ve el barrido de integración, que le manda un `?q=`
y exige que conteste, y no contesta si el repositorio no declara su `Filtro`, porque el paginador
lanza.

## Medido

Los listados del contrato, con `q` y sin él, sobre el `docs/api/openapi.json` regenerado. Es la
orden del ADR-0069, contada:

```bash
python -X utf8 - <<'EOF' | sort | uniq -c
import json
d = json.load(open("docs/api/openapi.json", encoding="utf-8"))
for ruta, ops in d["paths"].items():
    ps = [p.get("name") for p in ops.get("get", {}).get("parameters", [])]
    if "page" in ps:
        print("q" if "q" in ps else "-")
EOF
```

Antes de este ítem daba `23 q`. Ahora da `17 q` y `6 -`, y los seis son los de la lista cerrada.

El orden de los parámetros cambia en los 23: `page` y `size` van primero, porque son la primera
pieza. Un cliente los manda por su nombre, así que no le afecta, y el cliente del frontal se
regenera con el contrato.

## Alternativas descartadas

- **Seguir ignorándolo**, que era el ADR-0069. Con la exportación, un fichero con todas las filas
  sale con el nombre de lo que se creía haber buscado.
- **Un filtro de acción para toda la API** que rechazara el `?q=` en cualquier acción que no enlace
  el texto. Alcanzaría también a las fichas y a las escrituras, que no son listados y a las que un
  `q` no les dice nada. Y la regla estaría donde no se ve: en la tubería, no en el sitio que
  compone el listado.
- **Dejar el texto en `ConsultaDeListado`, con una marca de si se admite.** El explorador de la API
  publica la propiedad igual, así que los seis seguirían publicando `q`. El contrato seguiría
  diciendo lo contrario de lo que hace el borde.
- **Dejar la página en `ConsultaDeListado`, y que la exportación la ignorase.** La exportación
  publicaría `page` y `size` sin leerlos, que es la misma mentira del `q`.

## Consecuencias

- **El contrato cambia en los seis**: dejan de publicar `q`. El frontal no se lo mandaba, porque
  sus pantallas de esos recursos no tienen buscador, y el `typecheck` sobre el cliente regenerado
  lo confirma.
- **El ADR-0069 queda enmendado en sus puntos 1 y 3, y el ADR-0068 §1 vuelve a decir lo que
  hace el borde.** Los dos lo anotan en su *Estado* y en el punto que cambia.
- **El caso de la lista de la transferencia cambia**, como el ADR-0069 avisó: el `?q=` escrito es
  un `400`, y el `?q=` en blanco trae las tres transferencias de la escena.
- **Un listado nuevo elige**: enlaza `FiltroDeTexto` y declara su `Filtro`, o no lo enlaza y entra
  en la lista cerrada de `SoloPublicaElTextoQuienLoMiraTests`.
