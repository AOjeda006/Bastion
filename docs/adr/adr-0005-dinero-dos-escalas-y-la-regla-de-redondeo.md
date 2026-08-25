---
tipo: referencia
stack: [dotnet, postgresql]
aplica_a: [csharp, dominio]
revisado: 2026-08-25
tags: [adr, dinero, importe, redondeo, r6, impuestos, decimal]
---

# ADR-0005: El dinero tiene dos escalas, y el redondeo va escrito

- **Estado:** aceptado
- **Fecha:** 2026-08-25

## Contexto

R6 dice, literalmente: «El dinero es `decimal`, con divisa, y con una regla de redondeo escrita.
`numeric(18,4)` para importes, `numeric(18,6)` para precios unitarios y tipos de cambio; objeto de
valor `Importe(cantidad, divisa)`; **nunca** coma flotante. El redondeo se aplica **por base
imponible y tipo impositivo**, no línea a línea ni al total, y esa regla se documenta y se prueba
con casos dorados.»

Tres decisiones quedan sin fijar y las tres tienen consecuencia contable.

## Decisión

### 1. El modo de redondeo va escrito en cada llamada, y no es el de .NET

Todas las operaciones que redondean usan **`MidpointRounding.AwayFromZero`**, escrito. El valor por
omisión de .NET es `ToEven` —el redondeo «del banquero»—, así que **omitirlo cambia el resultado**:
0,125 € daría 0,12 en vez de 0,13.

Hay un test dedicado a ese punto exacto, que además comprueba en la misma prueba lo que habría dado
el modo por omisión, para que quede claro que la diferencia es real y no teórica. Y otro en negativo:
`-0,00005` vale `-0,0001`, porque *away from zero* se aleja del cero, no «va hacia abajo». Los abonos
existen.

### 2. Dos escalas son dos TIPOS, no un tipo con más decimales

- **`Importe`** vive en `numeric(18,4)`.
- **`PrecioUnitario`** vive en `numeric(18,6)`.

Son tipos distintos porque las dos escalas **no son intercambiables**: un precio unitario no es
dinero que se pueda sumar a una factura, y multiplicarlo por una cantidad **no devuelve otro precio
unitario**. Que sean tipos distintos hace que el compilador impida confundirlos, que es más barato
que acordarse.

Cada uno **reduce a su escala al construirse**, y no al guardarse. Si `Importe` admitiera seis
decimales y la reducción ocurriera en la base de datos, el modo de redondeo dejaría de ser el nuestro
y pasaría a ser el del motor.

### 3. La escala baja en `PrecioUnitario.Por(cantidad)`, y en ningún otro sitio

Es el único punto donde se pasa de escala 6 a escala 4, y es **un solo redondeo sobre el producto
exacto**. Ni se redondea el unitario a 4 y luego se multiplica —eso multiplicaría también el error—
ni se arrastran seis decimales dentro de un importe.

Devolver `Importe` y no `decimal` es parte de la decisión: un `decimal` suelto se seguiría tratando
como si tuviera seis decimales.

Y por el mismo motivo **la suma de importes no redondea**: dos sumandos en escala 4 dan una suma en
escala 4. Redondear en cada acumulación repartiría por todas partes el error que R6 quiere concentrar
en un único punto.

### 4. La cuota se redondea UNA vez, a la unidad mínima de la divisa

`Importe.Cuota(tipo)` redondea el producto **exacto** de base por tipo directamente a la unidad
mínima de la divisa. No pasa antes por la escala de importe: **redondear dos veces no da lo mismo que
redondear una**.

La unidad mínima sale de una tabla por divisa que hoy tiene **una sola entrada** (EUR = 2) y
**ninguna entrada por omisión**: una divisa desconocida **lanza**. Suponer dos decimales acertaría con
el dólar y fallaría en silencio con el yen (cero) y con el dinar (tres). Cuando entre una divisa más,
entra con su caso dorado.

### 5. El caso dorado de R6 está construido para que las tres estrategias difieran

Un test cuyas tres estrategias coinciden no ha probado nada. El caso, en euros:

| grupo | tipo | líneas | base | cuota exacta | cuota R6 |
|---|---|---|---|---|---|
| A | 21 % | 3 × (4,008 × 3 uds) | 36,0720 | 7,5751200 | **7,58** |
| B | 10 % | 3 × (4,030 × 2 uds) | 24,1800 | 2,4180000 | **2,42** |

- **R6** (una vez por par base/tipo): 7,58 + 2,42 = **10,00** ← la regla
- **Línea a línea** (redondear cada línea): 7,59 + 2,43 = **10,02**
- **Al final** (acumular exacto y redondear una vez): 9,99312 = **9,99**

Tres euros con dos decimales y tres respuestas distintas. Elegir mal no es un matiz de presentación:
es la diferencia entre cuadrar con la AEAT y no cuadrar.

## Qué NO se ha hecho, y por qué

- **No hay servicio de cálculo de impuestos.** El §12 lo sitúa en su propio módulo de dominio, no en
  el bloque común, y el plan maestro lo coloca en la fase de facturación. Lo que el bloque común
  aporta es la **primitiva** `Importe.Cuota`, que redondea una vez, más el caso dorado de arriba como
  referencia que ese servicio tendrá que reproducir. La agrupación por par (base, tipo) vive **en el
  test**, escrita como una de tres estrategias comparadas.
- **No hay librería de dinero de propósito general.** No hay resta, ni multiplicación libre, ni
  conversión de divisa, ni formateo, ni `IComparable`. Lo que no tiene un test que lo pida, no está
  escrito. `Sumar` existe además del operador `+` solo porque el analizador pide una alternativa con
  nombre.
- **Operar entre divisas distintas lanza.** No convierte ni deja pasar. Convertir exige un tipo de
  cambio **con su fecha**, que un objeto de valor no tiene ni debe adivinar.

## Consecuencias

- El mapeo a `numeric(18,4)` y `numeric(18,6)` de la fase 0.4 en adelante es una consecuencia de los
  tipos, no una decisión aparte: si una columna no coincide con la escala de su tipo, la que está mal
  es la columna.
- Cuando llegue el servicio de impuestos, su criterio de aceptación es reproducir la tabla de arriba.
  Si da 10,02 o 9,99, está aplicando una estrategia descartada.
- Añadir una divisa es añadir una fila a la tabla de unidades mínimas **y** su caso dorado. Sin lo
  segundo, la primera no entra.
