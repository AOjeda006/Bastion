---
tipo: referencia
stack: [csharp, dotnet, testing]
aplica_a: [arquitectura, testing, clean-architecture]
tags: [adr, netarchtest, fronteras, vacuidad, mutacion, monolito-modular]
revisado: 2026-09-02
---

# ADR-0020: Una regla de arquitectura sin afirmación de conjunto no vacío no es una regla

- **Estado:** aceptado
- **Fecha:** 2026-09-02
- **Relacionado:** implementa el criterio del ítem 0.12. Hereda el método del ADR-0019 (la mutación
  como forma de comprobar que un test mira) y le añade el modo de fallo propio de este carril.

## Contexto

El §4 del plan maestro tiene cinco reglas de frontera y el criterio del ítem dice **«fallan si un
módulo cruza una frontera»**. El verbo importa: el entregable no son las reglas escritas, es cada
regla **vista en rojo por una violación deliberada** antes de aceptarla en verde.

Eso choca de frente con cómo funciona la herramienta. NetArchTest selecciona un conjunto de tipos y
comprueba una condición sobre cada uno. Si el selector no casa con nada, **no hay ningún tipo que
incumpla, así que la regla se cumple**: verdad por vacuidad. Una regla vacía sale verde hoy, sale
verde el día que alguien cruce la frontera, y sale verde para siempre. Y no hay ninguna señal que la
distinga de una regla que sí protege algo: las dos son una línea de puntos en el informe.

En este repositorio eso no es un riesgo teórico, es el estado del proyecto:

- **Trece de los dieciséis módulos del §5 no existen todavía.** Una batería escrita por nombre
  —«`Bastion.Ventas.Domain` no referencia EF Core»— serían trece reglas verdes que no miran nada, y
  un informe que dice «dieciséis fronteras comprobadas».
- **Auditoría tiene cuatro de sus cinco capas vacías.** El módulo del 0.7 registra los cambios desde
  un interceptor de EF Core, así que todo lo suyo vive en `Infrastructure`; sus proyectos de
  `Domain`, `Contracts`, `Application` y `Endpoints` compilan a un ensamblado **sin un solo tipo
  dentro**. Aplicarles las fronteras del §4 habría dado cuatro verdes por vacuidad. Hoy, con esto
  escrito, **a Auditoría no se le comprueba ni una sola regla de capas** — y eso es un dato del
  informe, no un descuido.

## Decisión

**Toda regla de este carril afirma también que su conjunto no está vacío, y ese conteo se compara
contra un inventario declarado. Una regla sin esa afirmación no cuenta como escrita.**

Se materializa en un único punto de entrada, `Barrido.Exige`, por el que pasan todas. Antes de
evaluar la condición, tres afirmaciones:

1. **El alcance está declarado y no está vacío.** La regla dice sobre qué ensamblados actúa; si son
   cero, falla ahí y con ese motivo.
2. **Los ensamblados leídos son los declarados**, comparados enteros y en los dos sentidos. Que la
   herramienta cargara menos de los que la regla dice cubrir es un fallo de la regla, no un detalle.
3. **El número de tipos seleccionados es el esperado y es mayor que cero.** Lo que no se selecciona
   no se comprueba, y un selector que se queda corto no da ninguna señal por su cuenta.

Y tres corolarios que se siguen de la misma idea:

- **Se descubre el conjunto real y se compara entero contra el declarado.** Los módulos, sus
  carpetas, sus capas, sus ensamblados y cuáles llevan tipos: todo sale del disco y de la salida de
  compilación, y se compara contra `Inventario`. De más es alguien que ha empezado algo sin decirlo;
  de menos es algo que desapareció y que las reglas dejaron de mirar.
- **Toda cadena escrita a mano lleva su contraejemplo.** Las prohibiciones al dominio son de fuera
  del proyecto —`Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Npgsql`— y no se pueden
  derivar de nada, así que cada una viene con **la capa donde ese mismo espacio de nombres tiene que
  aparecer de verdad**. Si la cadena está bien escrita, allí se encuentra. Si está mal, no se
  encuentra en ninguna parte, y eso es lo que se pone rojo.
- **La lista de reglas del carril es ella misma una regla.** Un `[Fact]` borrado no deja hueco: la
  suite sale verde, más rápida, con un caso menos que nadie echa de menos. Contra eso, la lista de
  nombres declarada y comparada entera — de nombres y no un recuento, porque un número diría que
  falta una y esto dice cuál.

## Lo que demostró la mutación 4, y por qué está aquí y no en una nota

La mutación es de una letra y **no toca ni una línea de código de producción**: en el inventario,
`Microsoft.EntityFrameworkCore` pasa a `Microsoft.EntityFramworkCore`. Compila, pasa el formateador,
pasa el analizador. Con ella aplicada **y además el dominio de Organización importando de verdad un
`DbContext`**, el resultado es:

```
Con error: 1, Superado: 13, Total: 14
  LasCapasVanHaciaDentroTests.La_prohibicion_al_dominio_puede_dispararse [FAIL]
    «Microsoft.EntityFramworkCore»: ni un tipo de Infrastructure depende de eso, así que
    prohibírselo al dominio no puede fallar nunca. O la cadena está mal escrita, o eso ya no
    se usa en el proyecto y la prohibición sobra
```

Es decir: **`El_dominio_no_conoce_la_infraestructura_ni_el_framework` estaba entre los trece verdes,
con un `DbContext` dentro del dominio.** La regla que da nombre a la frontera no la protegía; lo
único que se movió fue el contraejemplo. Sin él, el informe habría dicho «regla 2, verde».

Esto no es un defecto de NetArchTest, es la forma de la herramienta: un selector que no casa con
nada es indistinguible de un proyecto limpio. Lo que se puede elegir es si se afirma o no que el
selector casa con algo.

## Lo que este carril NO puede ver, y dónde está cubierto

Escribirlo es parte de la decisión: un carril que no dice dónde acaba invita a creer que llega más
lejos.

| Regla del §4 | ¿Aquí? | Dónde |
|---|---|---|
| **1 · Un módulo solo referencia el `Contracts` de otro** | **Sí** | `Ningun_modulo_ve_el_interior_de_otro` (la cara negativa) y `El_unico_cruce_entre_modulos_va_por_contratos` (la positiva: hay un cruce, es este, y va por un contrato) |
| **2 · Ningún `Domain` conoce EF Core, ASP.NET Core ni infraestructura** | **Sí** | `El_dominio_no_conoce_la_infraestructura_ni_el_framework` + su contraejemplo, y `Ninguna_capa_mira_hacia_fuera_de_su_modulo` para el reparto por capas |
| **3 · Ninguna consulta cruza esquemas** | **No** | Es un hecho de SQL, no de tipos. `EsquemaDeIdentidadTests` y `EsquemaDelModuloTests` (Testcontainers, PostgreSQL de verdad) comprueban que cada módulo tiene sus tablas en su esquema y que en `public` no queda ninguna; el SQL crudo lo vigila `ElFiltroNoSeSaltaPorAhiTests`, que lee el código fuente. La regla 1 tapa el resto por construcción: quien no alcanza el `DbContext` ajeno no puede escribir una consulta contra él |
| **4 · Ninguna clave ajena entre esquemas** | **No** | Es DDL. `EsquemaDelModuloTests.No_hay_ninguna_clave_foranea_que_salga_del_esquema_del_modulo` y `EsquemaDeIdentidadTests.La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena` |
| **5 · Escrituras entre módulos solo por eventos** | **La mitad** | Aquí, `Las_puertas_publicas_de_los_contratos_son_las_declaradas`: la regla 1 impide *llamar* a un caso de uso ajeno, pero no impide publicar en el `Contracts` propio un puerto que escriba, y eso sí es un hecho de tipos. La mitad de ejecución —que el evento salga en la misma transacción y el efecto ocurra una vez— son los tests de la bandeja de salida (ADR-0013) |

Además, y fuera de la tabla, hay tres cosas que la herramienta no ve **por cómo funciona**:

- **Los `.csproj`.** NetArchTest lee IL, y una referencia de proyecto que todavía no usa nadie no
  emite IL. Se comprobó: con `Bastion.Identidad.Application` referenciando
  `Bastion.Organizacion.Domain` —el cruce que la regla 1 prohíbe— las catorce reglas seguían verdes,
  y el compilador tampoco avisa, porque una referencia sin usar no es un aviso. Es el orden natural
  de las cosas: primero se añade la referencia, y la línea que la usa llega en otro commit. Por eso
  hay una decimoquinta regla que **lee los `.csproj`** y compara el grafo de aristas entero. Uso y
  permiso son dos hechos distintos y se vigilan por separado.
- **La composición en ejecución.** Qué resuelve el contenedor de dependencias no está en la IL de
  nadie. Lo cubre `UnidadDeTrabajoPorModuloTests`, en los tests funcionales.
- **El frontal.** Es TypeScript; este carril lee ensamblados de .NET. Sus fronteras las vigila el
  contrato generado (ADR-0018) y sus barridos de rutas y permisos.

## Qué regla vive en qué carril

Solo hay una regla que cabría en los dos sitios, y se nombra para que no se escriba dos veces:
**`UnidadDeTrabajoPorModuloTests.NingunCasoDeUso_PideLaUnidadDeTrabajoComun`**. Es un hecho de tipos
—ningún constructor pide `IUnidadTrabajo` a secas— y NetArchTest lo expresaría sin esfuerzo.

**Se queda donde está**, en `tests/Api.FunctionalTests/Composicion/`, por una razón concreta: va en
pareja con `CadaModulo_DeclaraSuPropiaUnidadDeTrabajo`, que **sí** necesita el contenedor construido
y por tanto no puede vivir aquí. Partir la pareja entre dos carriles para ganar pureza dejaría dos
mitades que se leen sin la otra, y el día que alguien cambiara el registro solo se acordaría de una.
La regla general: **cuando una regla cabe en los dos carriles, va con la regla hermana que solo cabe
en uno.**

## Alternativas descartadas

**Escribir las reglas módulo a módulo, por nombre.** Es lo que hace casi todo el mundo y es lo que
este ADR existe para no hacer. Trece de los dieciséis módulos darían verdes vacíos, y el problema no
es que no protejan: es que **se cuentan** como si protegieran.

**Prohibir `System.Data` junto con EF Core y Npgsql.** Parecía gratis —ADO.NET es la otra puerta al
mismo sitio— y el contraejemplo la tumbó antes de que llegara a existir: **cero** tipos dependen de
`System.Data` en las tres capas donde tendría que aparecer, porque el acceso a datos entra por EF
Core y por Npgsql. Habría sumado una regla verde que no protege nada. Es el mecanismo de este ADR
funcionando sobre su propio autor, y por eso queda anotado en el fichero.

**Referenciar cada proyecto de módulo desde el proyecto de test.** Se descartó: la lista de
referencias sería una segunda declaración de qué módulos hay, escrita a mano y en un `.csproj`,
donde nadie la compara. El proyecto referencia **solo la raíz de composición** (`Bastion.Api`), que
arrastra todos los ensamblados a la salida, y el conjunto se **descubre** de ahí.

**Conformarse con `IsSuccessful`.** Es la API que la herramienta invita a usar y es exactamente la
que no distingue «nadie ha cruzado» de «no he mirado a nadie».

## Consecuencias

- El carril son **quince** reglas, cada una vista en rojo por una violación deliberada antes de
  aceptarla en verde. La tabla de mutaciones está en `docs/PLAN.md`.
- El coste está en el sitio correcto: **añadir un módulo, una capa con tipos, un cruce entre
  módulos, una puerta pública o una referencia de proyecto obliga a escribir su línea declarada**.
  Es fricción, y es la fricción que hace que alguien decida en vez de que ocurra.
- Queda escrito que **Auditoría no tiene hoy ninguna regla de capas comprobada**. El día que estrene
  su primera entidad de dominio, el inventario se pone rojo y obliga a añadir la línea — que es el
  día en que esas reglas empiezan a proteger algo.
- El paquete es **`NetArchTest.eNhancedEdition` 1.4.5, MIT declarada en el propio `.nuspec`**, una
  bifurcación del `NetArchTest.Rules` original. Los tres motivos —licencia declarada en el
  artefacto, mantenimiento vivo (2025 frente a 2021) y `Mono.Cecil` 0.11.6 frente a 0.11.3 para leer
  ensamblados de .NET 10— están escritos con su fecha de comprobación en `Directory.Packages.props`,
  junto con lo que se pierde (53 estrellas frente a 1772, un solo mantenedor) y por qué el riesgo
  está acotado: solo entra en proyectos de test, y la superficie que se usa es la API fluida de la
  1.3.2, común a las dos ediciones.
