---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, ddd, testing]
revisado: 2026-09-07
tags: [adr, ef-core, change-tracker, guid-v7, agregados, 412, concurrencia]
---

# ADR-0031: La clave declarada `Never` es lo que hace que un hijo nazca como alta

- **Estado:** aceptado
- **Fecha:** 2026-09-07
- **Sustituye a:** la alternativa descartada del [ADR-0010](adr-0010-una-entidad-hija-con-clave-propia-no-se-da-de-alta-sola.md). Su
  diagnóstico sigue en pie entero; lo que se sustituye es **una frase**: la que decía que
  `ValueGeneratedNever()` no sirve.

## Contexto

El ítem 1.6 colgó tres hijos del tercero —`Contacto`, `CuentaBancaria`, `CondicionPago`—. Contra
PostgreSQL, **las siete rutas que llegan a escribir un hijo contestaban `412`**, y ninguna de las
que rechazan antes de escribir fallaba. Ni una sola ruta de colección hija funcionaba, y no había
ningún verde que dijera lo contrario.

Es la misma avería del ADR-0010, cuatro fases después y con otra cara: allí salía `500` porque
todavía no existía el manejador de choques; aquí sale `412` porque ahora sí existe y traduce
`DbUpdateConcurrencyException` a «la versión ya no es la actual». La respuesta era **peor** que el
`500`: un `500` se investiga, y un `412` se lee como «alguien guardó antes que tú», que es una
explicación plausible y falsa.

## El mecanismo, otra vez

EF Core decide si una entidad recién aparecida en la colección de un padre ya seguido es un alta o
una fila que ya existía **mirando si su clave viene puesta**. Con un `identity` la pregunta
distingue: vale cero hasta que la base la genera. Con los `Guid` v7 que este sistema genera en las
fábricas del dominio, viene siempre puesta, así que EF concluye «ya existía», emite un `UPDATE`
contra una fila que no está, afecta a cero filas y eso es un choque de concurrencia.

## Lo que el ADR-0010 dio por sabido y no lo era

El ADR-0010 descartó por escrito una de las alternativas:

> **`ValueGeneratedNever()` en el mapeo.** No sirve: la regla de EF Core es «la clave está puesta»,
> y con generación desactivada está puesta siempre.

**Es falso, y se midió.** La pregunta que EF se hace no es «¿el valor es distinto del de por
defecto?», sino «¿es esta una clave de las que se rellenan **al insertar**, y está todavía sin
rellenar?». Una clave declarada `Never` no es de esas, así que la pregunta no aplica y el hijo cae
del lado de `Added`. El experimento, con el modelo real y **sin base de datos**:

| Declaración de la clave | Estado del hijo tras `DetectChanges()` |
|---|---|
| `ValueGeneratedOnAdd` (la convención de EF para `Guid`) | `Modified` → `UPDATE` → 412 |
| `ValueGeneratedNever` | `Added` → `INSERT` |

No es que EF Core haya cambiado de criterio: es que el modelo le estaba diciendo algo que no era
verdad. Aquí **ninguna** clave se genera al insertar —la pone la fábrica del dominio, a propósito,
por localidad de índice—, y el mapeo declaraba lo contrario porque nadie lo había contradicho.

## Decisión

**El modelo declara que la clave la pone el dominio.** Una convención de cierre,
`LaClaveLaPoneElDominio`, marca `ValueGeneratedNever` toda propiedad `Guid` de una clave primaria,
en todos los contextos que heredan de `ContextoDeModulo`.

Una convención y no una línea por configuración, porque no es una decisión de mapeo de una entidad:
es una propiedad del sistema entero. Una lista por configuración es una lista de la que la próxima
entidad se cae sin ruido —que es exactamente lo que pasó aquí—.

**Lo que el ADR-0010 decidió sigue valiendo y no se retira:** apuntar el hijo explícitamente en su
repositorio (`usuarios.Registrar(...)`) es correcto y ahora es redundante. No se quita en este ítem:
quitarlo es tocar un camino de Identidad que este ítem no tiene por qué tocar, y con la convención
puesta no hace daño. Cuando se retire, que sea con su propio ítem y con el rojo de la ruta delante.

## Consecuencias

- **Añadir un hijo a la colección de un agregado ya basta.** El rodeo del ADR-0010 deja de ser
  necesario; lo que impide que vuelva a hacer falta es la convención, no la memoria de nadie.
- **El canario del ADR-0010 se puso rojo, que es para lo que estaba.** Decía de sí mismo: «el día
  que EF Core cambie de criterio, este test se pondrá rojo y el rodeo podrá desaparecer». Se
  reescribió al comportamiento medido y sigue siendo canario, ahora del lado contrario: si vuelve a
  salir `Modified`, la convención ha dejado de aplicarse y todas las altas de hijos son un 412.
- **La regla que debía cazarlo miraba justo esta propiedad y la eximía.**
  `Toda_entidad_tiene_su_clave_completa_antes_de_guardar` filtraba por `ValueGenerated.OnAdd` y
  luego exoneraba en bloque a toda clave `Guid`, con el motivo escrito de que en un `Guid` eso no
  significa que la ponga la base. Cierto del `INSERT`, y ciego al otro uso de `OnAdd`. Hoy no exime:
  exige la declaración positiva.
- **Y no la habría cazado igualmente, porque no miraba Terceros.** Las cinco reglas que recorren el
  modelo enumeraban sus contextos a mano y ninguna incluía el de Terceros, que entró en la fase 1.
  Ahora el universo **se descubre** —toda clase concreta que hereda de `ContextoDeModulo`— con su
  lista declarada al lado para que un descubrimiento que se quede corto salga rojo en vez de
  silencioso.
- **Sin migración.** `ValueGenerated` sobre una clave `Guid` no produce ninguna diferencia
  relacional: el comprobador de migraciones de la CI sigue diciendo que modelo y migraciones
  coinciden en los cuatro módulos.
- **La lección, otra vez y más barata que la primera:** preguntar por el estado del `ChangeTracker`
  es un test de dos segundos y sin contenedor. La avería parecía necesitar PostgreSQL —se vio en el
  carril de integración, en la CI, siete veces— y no lo necesitaba. Lo fija
  `LoQueCuelgaNaceComoAltaTests`.
