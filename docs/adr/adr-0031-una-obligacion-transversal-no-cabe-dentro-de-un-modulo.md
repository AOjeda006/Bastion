---
tipo: referencia
stack: [csharp, dotnet, postgresql, efcore]
aplica_a: [arquitectura, rgpd, ddd, testing]
tags: [adr, r16, lopdgdd, art-32, bloqueo, puertos, monolito-modular, paginacion, k-vias]
revisado: 2026-09-07
---

# ADR-0031: Una obligación transversal no cabe dentro de un módulo

- **Estado:** aceptado
- **Fecha:** 2026-09-07
- **Relacionado:** corrige un defecto de cumplimiento de **ADR-0027** (el listado del art. 32) que
  venía de **ADR-0016** y **ADR-0017**. Usa el patrón de puertos de **ADR-0024**. Aplica
  `herramientas/proteccion-datos.md` y `patrones/repository-y-dto.md`. Se implementa en el
  **ítem 1.6**.

## Contexto

El ítem 1.4 montó el listado del art. 32 —el único camino por el que se leen filas bloqueadas— y lo
montó **dentro de Organización**: el enumerado `TipoDeRecursoBloqueado` vivía en
`Organizacion.Application` y `RepositorioDeLoBloqueado` unía en SQL las tres tablas bloqueables del
esquema `organizacion` (empresas, almacenes, ubicaciones).

Cinco agregados del proyecto son bloqueables. Ese listado veía **tres**.

Los dos que faltaban no eran casos de borde:

- **`Usuario`**, desde el mismo ítem 1.4. Tiene su `Bloquear(MotivoDeBloqueo…)` —distinto del
  rechazo temporal por intentos fallidos, que el ADR-0017 se molesta en separar— y es el caso **más
  nítido** del artículo: una persona física que ejerce su derecho de supresión y cuyos datos se
  reservan en vez de borrarse. No asomaba por ningún listado.
- **`Tercero`**, desde el ítem 1.5, que lo dejó anotado como nota abierta.

**Y no fue un olvido.** Alcanzar `identidad.usuarios` o `terceros.terceros` desde aquel repositorio
exigía un `JOIN` entre esquemas, que es exactamente la frontera que un monolito modular no cruza
(§4). Una obligación **transversal** montada dentro de un módulo no llega a los demás **por
construcción**: no había ninguna línea que escribir para arreglarlo sin cambiar de sitio la
obligación.

Lo más grave no es el agujero, es que **nada se puso rojo**. La doctrina del proyecto desde el ítem
1.2 es que una lista cerrada se compara **entera** contra aquello que enumera, y el comentario de
`IBloqueable` lo decía con todas las letras desde el día que se escribió: «es la marca que hace de
lista […] sin ella, "cuáles se bloquean" sería una lista escrita en un test, que es una lista que se
queda vieja». Nadie comparaba la marca contra el enumerado. La lista se quedó vieja **dos veces
seguidas** —un ítem cada vez— con la suite entera en verde.

## Decisión

### 1. La regla se escribe primero y se mira roja

`LoBloqueadoSeVeEnteroTests` compara `TipoDeRecursoBloqueado` contra los tipos que implementan
`IBloqueable`, **en las dos direcciones**, con su ancla. Escrita antes del arreglo, sale roja con
**dos supervivientes a la vez** —`Tercero` y `Usuario`—, que es la demostración de que el mecanismo
funciona y no una comprobación posterior de que el arreglo se hizo.

Y una segunda afirmación, `Todo_modulo_que_bloquea_contesta_por_lo_suyo`, porque la primera **se
puede cumplir sin arreglar nada**: el enumerado es una lista de nombres y se pone verde en cuanto
alguien añade el valor. Un valor declarado que ningún módulo contesta es **peor** que el agujero de
partida — el contrato promete un tipo de recurso que no aparece nunca, así que quien audite leerá
«no hay usuarios bloqueados» en vez de «nadie los está buscando».

### 2. Cada módulo que bloquea expone un puerto; el listado los compone

`IConsultaDeLoBloqueado` vive en el **bloque común**, no en el `Contracts` de ningún módulo. Es el
patrón de ADR-0024 con la flecha al revés: allí un dueño publica una lectura y varios preguntan;
aquí hay **varios dueños y un consumidor**, así que ponerlo en un `Contracts` obligaría al consumidor
a conocerlos a todos y a que añadir un módulo tocara el listado. El consumidor resuelve
`IEnumerable<IConsultaDeLoBloqueado>`; un módulo nuevo entra registrando su implementación.

Sin `JOIN` entre esquemas y sin llamada HTTP, como el cruce declarado del ítem 1.2. **Y sin cruce
nuevo que declarar**: el puerto está en el bloque común, así que no aparece ninguna referencia de
proyecto entre módulos.

El filtro, el orden, el recuento y el corte se escriben **una vez**
(`BuildingBlocks.Infrastructure.Bloqueos.ConsultasDeLoBloqueado`); cada módulo aporta solo la
proyección de sus tablas. Repartidos, tres implementaciones del mismo puerto divergirían el día que
una se dejara el `ESCAPE` del `ILIKE` o la intercalación, y el listado saldría verde con una página
mal ordenada.

### 3. La cota de la fusión k-vías, que es lo que hacía viable el puerto

El repositorio anterior llevaba escrito el motivo por el que esto no se podía hacer: «tres consultas
paginadas por separado no se pueden juntar en una página: no hay forma de saber cuántas filas traer
de cada una sin traerlas todas». **La primera mitad es cierta y la segunda no.**

Para la página *k* de un orden global bastan las `salto + tamaño` primeras filas de **cada** fuente.
Se demuestra sola: una fila que no está entre las `salto + tamaño` primeras de su propia fuente tiene
por delante, solo en su fuente, más filas que las que caben hasta el final de la página, así que no
puede aparecer en ella. Y no es asintóticamente peor que el `UNION` que sustituye, que también tenía
que recorrer `OFFSET + LIMIT` filas para llegar a la página *k*; la diferencia es un factor N de
módulos en el número de **viajes**, con `Tamanio` topado en `Paginacion.TamanioMaximo`.

El total es exacto y no estimado: cada módulo cuenta las suyas con el filtro puesto y los totales se
suman, que es el mismo número que daba el `COUNT` sobre la unión.

### 4. El orden es **ordinal a los dos lados**, y hay que decirlo

El orden final lo decide un comparador en memoria; cada módulo ordena en su base para saber cuáles
son sus primeras. Si las dos ordenaciones no coincidieran, una fila del borde podría quedarse fuera
del candidato aunque el orden global la incluyera: la página saldría **completa, plausible y con una
fila cambiada**. Por eso las consultas ordenan con la intercalación `C` de PostgreSQL —orden de
bytes— y la composición usa `string.CompareOrdinal`: son la misma relación de orden. Sin fijarlo, el
resultado dependería de la intercalación de la base y de la cultura del proceso.

Y el desempate por identificador va **siempre** y en el mismo sentido a los dos lados: aquí conviven
cinco agregados de tres módulos, y dos filas de tablas distintas pueden compartir fecha al
milisegundo si se bloquearon en la misma petición. Sin desempate único, la página 2 repite filas de
la 1.

### 5. Los módulos se consultan **uno detrás de otro**

No con `Task.WhenAll`. Cada puerto arrastra el contexto de su módulo, y lanzar tres consultas de EF
Core a la vez solo es seguro mientras cada contexto tenga su **propia** conexión. El día que dos
compartieran una —para poder abrir una transacción común, que es una petición razonable— esto
reventaría con «a second operation was started on this context», y reventaría en la pantalla del
art. 32. Son tres viajes en una pantalla de administración: el paralelismo ahorraría milisegundos a
cambio de acoplarse a algo que no se ve desde el punto donde se decide.

### 6. En el listado del art. 32 **no viaja ningún identificador de persona**

Ni el NIF del tercero, ni el correo del usuario. Para levantar un bloqueo hecho por error basta con
el nombre y el identificador de la fila, que es lo que el desbloqueo pide; dos cuentas con el mismo
nombre se distinguen por ese identificador, no publicando la dirección. Este listado enseña
precisamente a personas cuyos datos se han **reservado** —alguna, por haber pedido su supresión—, así
que enseñar de más aquí es tratar un dato personal por comodidad.

## Alternativas descartadas

- **Añadir los dos valores al enumerado y ya.** Es la que parece el arreglo y es la que deja el
  agujero intacto: el listado seguiría trayendo filas de tres tablas de un esquema. Por eso existe la
  segunda afirmación del punto 1.
- **Un `JOIN` entre esquemas.** Es la frontera del §4. Además ata el despliegue: los cinco agregados
  tendrían que vivir en la misma base para siempre.
- **Una llamada HTTP entre módulos.** El monolito modular resuelve en proceso (ADR-0024); una
  llamada de red dentro del mismo proceso añade un modo de fallo y un tiempo de espera a una
  consulta que no los necesita.
- **Mover el listado entero al bloque común.** La obligación es transversal, pero la ruta, el
  permiso (`organizacion.bloqueado.ver`) y la pantalla son de la administración de la instalación,
  que es lo que Organización ya es. Mover la ruta habría cambiado el contrato público de la API por
  una razón interna.
- **Paginar cada módulo por separado y concatenar.** Da una página equivocada, y es justo lo que el
  comentario del repositorio anterior descartaba con razón.

## Consecuencias

- `BuildingBlocks.Infrastructure` pasa a referenciar `Npgsql.EntityFrameworkCore.PostgreSQL`. Su
  `.csproj` decía «EF Core, no el proveedor: este proyecto no sabe contra qué base corre», y la frase
  **ya era falsa cuando se escribió** —dos párrafos más abajo admitía mapear una columna `jsonb`—. Se
  corrige la frase en vez de esconder el hecho detrás de una copia por módulo. La frontera que este
  proyecto declaraba como la buena sigue intacta: Domain y Application no saben de persistencia.
- **Y eso ensancha el grafo transitivo:** los tres `Endpoints` referencian
  `BuildingBlocks.Infrastructure` —por la política de `ProblemDetails`, que es HTTP y está declarada
  ahí— así que ahora ven también el proveedor. Se dice, no se esconde. Es un ensanchamiento de una
  puerta que ya estaba abierta: ya veían `Microsoft.EntityFrameworkCore` y `Npgsql` por el mismo
  camino. Lo que el compilador vigila —que `Domain` no vea ni EF Core ni Npgsql ni la infraestructura
  común— no lo toca esto, y `LasCapasVanHaciaDentroTests` lo sigue afirmando con su contraejemplo.
- El barrido que comprueba que el listado **se traduce a SQL** se muda de
  `Organizacion.IntegrationTests` a `Api.IntegrationTests`, que es el único proyecto de pruebas que
  ve las tres infraestructuras. Donde estaba, «se traduce entero» habría pasado a significar un
  tercio — la misma clase de defecto que este ADR corrige.
- **Ese barrido encontró dos defectos antes de salir de la rama**, y los dos eran invisibles al
  compilador y a la suite anterior:
  1. una condición de membresía escrita a mano en la proyección de Identidad duplicaba un invariante
     que el filtro global de `Usuario` ya impone —y, sumada a la navegación que ese filtro expande,
     **no se traducía**;
  2. `Codigo = usuario.Correo.Valor` tampoco se traducía, porque `Correo` se mapea con conversor de
     valor y `.Valor` no es una columna. Habrían sido nueve combinaciones de orden convertidas en un
     500 justo en la pantalla que se abre para rectificar un bloqueo hecho por error.
- El plazo de retención se sigue calculando al proyectar y no se guarda en columna (ADR-0027), y el
  listado sigue sin emitir versión, así que las cuatro exenciones de `If-Match` de los desbloqueos
  siguen en pie.
