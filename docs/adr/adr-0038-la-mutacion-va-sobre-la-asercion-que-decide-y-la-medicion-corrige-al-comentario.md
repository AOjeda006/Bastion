---
tipo: referencia
stack: [csharp, dotnet]
aplica_a: [testing, desarrollo-con-ia]
tags: [adr, mutacion, testing, metodo, cobertura, adr-0006, adr-0020, adr-0033]
revisado: 2026-09-18
---

# ADR-0038: La mutación va sobre la aserción que decide, y la medición corrige al comentario

- **Estado:** aceptado
- **Fecha:** 2026-09-18
- **Sale del ítem 2.2**, que es donde se midió. Generaliza para los ítems siguientes lo que allí fue
  un hallazgo concreto.
- **Familia:** el [ADR-0006](adr-0006-un-test-que-solo-se-ejecuta-aislado-no-esta-probado.md) («un
  test que solo se ejecuta aislado no está probado») y el
  [ADR-0020](adr-0020-una-regla-sin-afirmacion-de-conjunto-no-vacio-no-es-una-regla.md) («una regla
  sin afirmación de conjunto no vacío no es una regla»). Los tres dicen lo mismo de tres maneras:
  **una comprobación vale lo que valga la vez que se la ha visto fallar**.

## Contexto

El ítem 2.2 escribió la composición del ADR-0037 §4 —una ubicación hereda el estado de su almacén y
el suyo propio solo puede empeorarlo— y las **cuatro** combinaciones de almacén × ubicación como
cuatro casos de integración. Cuatro casos para una función de dos booleanos parece de más, y el
comentario de uno de ellos lo decía con todas las letras:

> Las otras tres combinaciones salen verdes con el mínimo, así que esta es la única que lo caza.

Esa frase era **una suposición escrita con la seguridad de una medida**, y era falsa.

La aserción que decide no era ninguna guarda de entrada ni ningún recuento: era la línea que compone
los dos estados. `EstadoDeMaestro` no está ordenado por severidad —`NoExiste = 0`,
`SeOfreceParaLoNuevo = 1`, `SoloResuelveLoViejo = 2`— y el error plausible, el que un programador
escribe sin pensarlo, es exactamente `Math.Min`. Se aplicó:

```csharp
// La mutación, en ConsultaDeUbicaciones.EstadoDeAsync
return (EstadoDeMaestro)Math.Min((int)almacen, (int)ubicacion);
```

**El resultado, medido:** las cuatro combinaciones se parten en **dos mitades exactas**.

| Almacén | Ubicación | Con `Math.Min` |
|---|---|---|
| activo | activa | **verde** — el mínimo de dos iguales acierta por casualidad |
| bloqueado | bloqueada | **verde** — íd. |
| bloqueado | activa | **ROJO** — `should be SoloResuelveLoViejo but was SeOfreceParaLoNuevo` |
| activo | bloqueada | **ROJO** — íd. |

Dos rojos, no uno. Y las dos que quedan verdes son **las simétricas**: las que a primera vista
parecen las obvias, las que alguien recortaría primero si le pidieran acortar el fichero.

## Decisión

**1. La mutación se aplica a la línea de la que depende la respuesta**, no a la que es cómoda de
romper. Invertir una condición de guarda, romper un recuento o quitar un `await` mide el arnés —dice
que los casos se ejecutan— y no mide la regla. La línea que decide es la que convierte las entradas
en la salida que el caso afirma; en el 2.2 era la composición de dos estados, y la mutación tenía
que ser **la equivocación plausible**, no una avería evidente.

**2. El reparto de rojos se anota por nombre, y es el argumento de por qué están escritos todos los
casos.** «Cuatro casos para dos booleanos» no se defiende con simetría ni con gusto por la
exhaustividad: se defiende diciendo que dos de ellos, y precisamente los dos que nadie recortaría,
son los únicos que ven el defecto. Ese reparto **solo se sabe midiéndolo**.

**3. Si la medición contradice al comentario, se reescribe el comentario.** No se redondea, no se
deja «aproximadamente» y no se calla. En el 2.2 fueron tres comentarios corregidos sobre la marcha,
y el texto que quedó dice lo que pasó: dos cazan el mínimo, dos siguen verdes porque el mínimo de
dos valores iguales acierta por casualidad.

**4. El informe separa lo visto en rojo de lo visto solo en verde**, con nombres en las dos listas.
Un caso que solo se ha visto verde sostiene lo que dice y nada más; mezclarlo con los que se han
visto fallar convierte el informe en una cifra de cobertura, que es la clase de número que tranquiliza
sin informar. La lista de «solo verde» no es una confesión: es el alcance real de lo comprobado.

## El ejemplo de arriba, y por qué este no puede tener su test

El [ADR-0033](adr-0033-el-ejemplo-de-un-adr-tambien-se-comprueba.md) obliga a que el ejemplo de un
ADR entre como caso con nombre propio, para que no pueda divergir de la regla. **Aquí no se puede, y
el motivo es el propio contenido:** la tabla describe lo que pasa con una línea que el repositorio
**no debe contener nunca**. Un test que la mantuviera cierta tendría que meter el `Math.Min` en el
adaptador, que es justamente el defecto.

Lo que sí está comprobado, y es donde se mira si esta tabla envejece: las cuatro combinaciones son
los cuatro `[Fact]` de `UnaEstanteriaBloqueadaSigueExistiendoTests`, declarados uno a uno en
`ElCensoDeEsteCarrilTests` de Organización, y la línea que deciden es
`ConsultaDeUbicaciones.LaPeorDeLasDos`. Si alguien borra dos casos, el censo se pone rojo y esta
tabla queda hablando de casos que ya no existen. **El límite dicho:** que los rojos sigan siendo
esos dos y no otros es una medida del 2026-09-18, no una comprobación que se repita sola.

## Lo que este ADR **no** es

**No es «mutad todo».** Mutar cada aserción de cada caso cuesta un ciclo completo por mutación y
mide, en la mayoría, que el caso se ejecuta —lo cual ya lo dice el censo del carril—. Lo que este
ADR obliga es a elegir **una** por regla nueva: la que decide.

**No es una herramienta.** No se introduce un mutador automático. Una herramienta genera mutantes
sintácticos y puntúa; lo que hace falta aquí es la equivocación **verosímil**, que es una decisión
de quien conoce el dominio —`Math.Min` sobre un enumerado cuyo orden numérico no es el de severidad
no lo propone una herramienta por su interés, lo propone alguien que ha visto ese error—.

**No dispensa del contraejemplo.** Ver un caso en rojo dice que el caso mira; no dice que lo
prohibido exista donde se permite. Eso sigue siendo cosa del ADR-0020.

## Alternativas descartadas

**Dejar el reparto sin medir y escribir las cuatro «por simetría».** Es lo que había, y produjo un
comentario falso. Además es frágil de la peor manera: el siguiente que lea «por simetría» y quiera
acortar el fichero borrará dos casos, y lo más probable —por parecer los redundantes— es que borre
justo los dos que cazan.

**Anotar el hallazgo en el cierre del ítem 2.2 y no más.** Era la opción barata, y es la que el
usuario descartó por su nombre: «anótalo como doctrina, no como anécdota». Un hallazgo en el cierre
de un ítem lo lee quien revisa ese ítem; un ADR lo encuentra quien escribe el siguiente.

**Medir cobertura de líneas o de ramas en la CI y poner un umbral.** Las cuatro combinaciones
cubren las mismas líneas y las mismas ramas **antes y después** de la mutación: la cobertura no
distingue las dos mitades, que es exactamente lo que había que distinguir. Un umbral habría dado el
mismo número con dos casos que con cuatro.

## Consecuencias

- Cada ítem con una regla nueva que **decide** una respuesta elige su mutación, la ejecuta y anota el
  reparto de rojos por nombre en el cierre de `docs/PLAN.md`.
- El informe de cierre de un ítem lleva **dos listas**: lo visto en rojo y lo visto solo en verde.
- Un comentario que afirme cuántos casos cazan algo es una afirmación medible: o está medida, o no
  se escribe.
- Cuesta un ciclo de `dotnet test` por mutación, y la reversión tiene su propia trampa ya conocida
  —una copia que conserva la fecha no recompila—, así que se revierte escribiendo el fichero, no
  copiándolo con su `mtime`.
