---
tipo: referencia
stack: [dotnet, typescript, react]
aplica_a: [api-rest, manejo-errores, ux-ipo, i18n]
tags: [adr, problem-details, rfc-9457, i18n, contrato, openapi, validacion]
revisado: 2026-09-03
---

# ADR-0030: el texto que lee una persona lo escribe el frontal, desde el `type` del `ProblemDetails`

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** ADR-0004 (frontera entre resultado y excepción), ADR-0018 (el contrato se genera,
  se versiona y la CI lo vuelve a generar), ADR-0019 (la aserción de tipo es el punto ciego del
  contrato). Se decide en el **ítem 1.2** y se **implementa en el 1.6**, que es el primer sitio
  donde una persona ve un mensaje de validación.

## Contexto

Una validación de negocio falla —un NIF con letra que no cuadra, un código de tercero repetido— y
hay que enseñarle una frase a quien está delante. La pregunta es **quién la escribe**: si la API
devuelve el texto ya redactado en el idioma de quien pregunta, o si devuelve un identificador
estable y el frontal pone la frase.

No es una preferencia de estilo. La convención importada ya dice de qué está hecho el error:

> **`type` estable por clase de error de negocio** (`/errors/stock-insuficiente`): es lo que el
> cliente puede tratar. El `detail` es para personas y puede cambiar de redacción.
> — `herramientas/api-rest.md`

O sea que el reparto **ya estaba escrito**: el `type` es contrato, el `detail` no lo es.

## Decisión

**El frontal escribe el texto, mapeando el `type` del `ProblemDetails` a una clave de su
diccionario.** La API no negocia idioma por `Accept-Language` ni redacta para personas.

### Tres cosas que esto obliga a montar, y sin las cuales la decisión no se sostiene

**Uno. El conjunto de `type` emitibles llega al frontal por un artefacto versionado.** Un mapa
`type → clave` que el frontal escribe de memoria es una lista que se desincroniza en silencio: el
día que el servidor emite un `type` nuevo, el frontal cae al genérico y nadie se entera hasta que
un usuario lo cuenta. Así que el catálogo se **genera** desde el servidor —dentro del propio
documento OpenAPI si cabe con la forma que ya tiene, o en un fichero al lado si no— y recibe **el
mismo trato que `openapi.json`** (ADR-0018): se genera, se versiona, y la CI lo vuelve a generar y
compara. Si difiere, rojo.

**Dos. El barrido de diccionarios enteros pasa a comparar también los `type`.** Ya existe: los dos
diccionarios tienen que traer exactamente las mismas claves, y no estar vacíos
(`ElCambioDeIdioma.test.tsx`). Ampliarlo para exigir que **cada `type` del catálogo tenga su
entrada en los dos idiomas** es lo que convierte el artefacto en una defensa y no en un inventario:
**un `type` nuevo sin texto es rojo el día que se escribe, no el día que un usuario ve una clave sin
traducir.**

**Tres. En ejecución se cae con red.** Un `type` desconocido —una versión del frontal más vieja que
la de la API— no puede acabar en una pantalla en blanco ni en una cadena cruda: se enseña el mensaje
genérico **y el identificador de traza que el `ProblemDetails` ya lleva**. Eso convierte el aviso en
una consulta directa a la telemetría, que es exactamente para lo que la convención pide que el
identificador viaje en el error.

### Lo que esta decisión NO cubre

**Los errores por campo son otra cosa y ya están resueltos.** Ahí el servidor nombra el **campo**
—no redacta una frase— y el frontal lo mapea a la etiqueta que ese campo tiene en su formulario. No
necesitan `type` propio ni entran en el catálogo.

## Alternativas descartadas

**Traducir en el servidor por `Accept-Language`.** Es la que parece más cómoda y es la que sale
cara:

- Convierte el `detail` en un contrato **no declarado**. La convención dice que puede cambiar de
  redacción; en cuanto una pantalla lo pinta tal cual, deja de poder cambiar sin romper a alguien, y
  nada lo vigila porque nadie lo declaró.
- Obliga a **dos catálogos de idioma en el servidor**, con sus dos revisiones, y a negociar
  `Accept-Language` en cada endpoint.
- Y rompe una propiedad que hoy sale gratis: **todo lo que lee una persona está escrito en el
  frontal.** Es lo que hace que `i18next/no-literal-string` y la paridad de diccionarios signifiquen
  algo. Con la mitad de los textos en C#, las dos reglas siguen verdes vigilando la mitad que queda,
  que es la peor forma de fallar: la que no se nota.

**Que el frontal use el `detail` cuando conozca el `type` y el genérico cuando no.** Es la anterior
disfrazada: el `detail` acabaría en pantalla igual, solo que a veces.

**No mandar `type` y distinguir por el código HTTP.** Un 422 no dice si el NIF es inválido o si el
código está repetido, y son dos frases distintas para quien las lee.

## Consecuencias

- El **ítem 1.6** hereda tres criterios: el catálogo generado y comparado por la CI, el barrido de
  diccionarios ampliado a los `type`, y el genérico con traza para el `type` desconocido.
- Cada error de negocio nuevo obliga a **tres cambios en el mismo commit**: el `type` en el
  servidor, su entrada en `es` y su entrada en `en`. Los dos últimos los exige el barrido, no la
  disciplina de quien lo escribe.
- La API queda **sin texto para personas** en sus errores de negocio, que es lo que permite que
  mañana la consuma algo que no es esta pantalla.
- El `detail` sigue existiendo y sigue sin ser contrato: vale para la traza y para el registro, no
  para pintar.
