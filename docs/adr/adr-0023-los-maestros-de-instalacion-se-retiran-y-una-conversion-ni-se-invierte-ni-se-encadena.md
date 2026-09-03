---
tipo: referencia
stack: [csharp, dotnet, api-rest]
aplica_a: [ddd, dominio, api-rest, seguridad]
tags: [adr, glosario, lenguaje-ubicuo, maestros, multiempresa, conversiones, r16]
revisado: 2026-09-03
---

# ADR-0023: Los maestros de instalación se retiran, y una conversión ni se invierte sola ni se encadena

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** sale de escribir el glosario del ítem 0.16 (`docs/dominio/glosario.md`). Toca la
  R8 (multiempresa), la R16 (suprimir no es borrar, ADR-0016) y el ADR-0007 §5. **Nada de esto se
  implementa en la fase 0**: se implementa en la fase 1.

## Contexto

Escribir un glosario obliga a decir qué es cada cosa, y decir qué es una cosa obliga a decir qué le
pasa cuando deja de valer. Al definir cuatro términos del §7.1 —`Divisa`, `TipoCambio`,
`UnidadMedida` y `ConversionUM`— apareció que **no se podía terminar la definición** sin decidir tres
cosas que nadie había decidido. Están escritas aquí para que no las decida, por omisión, la primera
línea de la fase 1 que las necesite.

Las tres son hallazgos del glosario, no de una revisión de código: aparecieron porque hubo que
escribir la frase entera.

---

## Decisión 1 — Los cuatro maestros de instalación tendrán una **retirada**, que no es un bloqueo ni un cierre

### El hueco, medido

La asimetría es exacta, y está en el contrato generado (`docs/api/openapi.json`):

| Recurso | Verbos de la colección | Verbos del elemento | Salida |
|---|---|---|---|
| `divisas` | GET, POST | GET, PUT | **ninguna** |
| `tipos-de-cambio` | GET, POST | GET, PUT | **ninguna** |
| `unidades-de-medida` | GET, POST | GET, PUT | **ninguna** |
| `conversiones-de-unidades` | GET, POST | GET, PUT | **ninguna** |
| `impuestos` | GET, POST | GET, PUT | `POST /cierre` |
| `almacenes`, `empresas`, `ubicaciones` | GET, POST | GET, PUT, DELETE | `POST /desbloqueo` |
| `ejercicios` | GET, POST | GET, PUT, DELETE | `POST`/`DELETE` `/cierre` |
| `series` | GET, POST | GET, PUT, DELETE | — |

Los cuatro primeros **se crean y se editan, y no se puede hacer nada más con ellos**: ni cierre, ni
bloqueo, ni borrado. Y `Modificar` es estrecho a propósito:

```csharp
Divisa.Modificar(string nombre)        UnidadMedida.Modificar(string nombre)
TipoCambio.Modificar(decimal tasa)     ConversionUM.Modificar(decimal factor)
```

De donde: un **código** mal escrito (`EURO` en vez de `EUR`), unos **decimales** equivocados en una
unidad, o un `TipoCambio` dado de alta con el **par o la fecha** que no eran, no se pueden arreglar
ni retirar. Y como los cuatro son **maestros de instalación** (R8), el alta equivocada es permanente
**y visible desde todas las empresas de la instalación**. Ese es el motivo por el que esto no espera:
el radio del error es la instalación entera.

### Qué se decide

Los cuatro tendrán una **retirada**. La palabra es nueva y queda reservada en el glosario:

- **No es un bloqueo.** El bloqueo (R16) es la respuesta al artículo 32 de la LOPDGDD: reservar los
  datos e impedir su tratamiento. Habla de **datos personales**, y una divisa no tiene ninguno.
  Reutilizarlo aquí metería dos máquinas de estados con motivos distintos en la misma columna, que
  es exactamente lo que el glosario dice de la pareja bloqueo/cierre.
- **No es un cierre.** El cierre es el final de una **línea temporal**: un ejercicio, una serie, un
  tramo de impuesto. `Impuesto` tiene `/cierre` porque un tramo se sucede por otro. Una unidad de
  medida no se sucede: deja de usarse.
- **Comportamiento:** una fila retirada no se ofrece para operaciones nuevas, pero **sigue
  resolviendo** para lo que ya apunta a ella. El `GET` de la colección la excluye por omisión; el
  `GET` por identificador **la sigue devolviendo** —al revés que el bloqueo, que responde 404—,
  porque un documento antiguo tiene que poder enseñar en qué divisa se emitió.
- **Ningún `DELETE`** para ninguno de los cuatro. Nunca.

### Por qué en la fase 1 y no aquí

Porque hoy no protegería nada. Mientras no haya una sola operación transaccional que apunte a una
divisa o a una unidad, la diferencia entre «retirada» y «borrada» no la nota nadie: la columna
existiría, no la miraría ningún camino, y sería una regla vacía de las del ADR-0020 escrita a mano.
Se implementa **con el primer módulo que las referencie**, que es cuando la distinción empieza a
significar algo.

---

## Decisión 2 — Si la inversa está declarada, tiene que ser la inversa

### El hueco

Los dos sentidos de una conversión son **dos filas independientes**, y eso es deliberado: el propio
`ConversionUM` lo explica —«el inverso de 12 no cabe en seis decimales, así que ir y volver no
devuelve la cantidad de partida»— y concluye que la vuelta, si hace falta, «se da de alta con su
propio factor y su propio redondeo pensado».

Lo que falta es el límite. **Nada acota cuánto pueden discrepar los dos sentidos.** Hoy se puede
declarar `caja→unidad = 12` y, en la fila de al lado, `unidad→caja = 0,5`, y las dos pasan todas las
comprobaciones que existen: cada factor por separado es positivo y cae en el rango `[0,000001,
1000000]`. Un inventario valorado con esas dos filas cuadra por un lado y descuadra por el otro,
sin un solo error.

### Qué se decide

Si la fila inversa está declarada, su factor tiene que ser **una inversa plausible** del directo: la
libertad que concede el diseño es la de **elegir el redondeo**, no la de declarar otro número.

Con `f` el factor directo y `g` el inverso, ambos ya redondeados a seis decimales, se exige:

```
|f · g − 1|  ≤  5·10⁻⁷ · (f + g)
```

**La tolerancia no está inventada: es exactamente la que impone la escala.** Cada factor se guarda
redondeado a seis decimales, así que arrastra hasta media unidad del último decimal (5·10⁻⁷) de
error; propagado al producto, eso es el margen de arriba y ni uno más. Con `f = 12` admite
`g ∈ {0,083333, 0,083334}` —las dos lecturas razonables de 1/12— y rechaza `0,5` por seis órdenes de
magnitud. Y como el rango declarado del factor es `[0,000001, 1000000]`, la inversa de cualquier
factor válido cae también dentro del rango: la regla es **total**, no tiene casos en los que se
calle.

### Dónde vive la comprobación

En la **capa de aplicación**, al escribir (alta y modificación), leyendo la fila inversa si existe.
No en el dominio: la regla relaciona **dos instancias distintas** del agregado, y la R12 dice una
transacción, un agregado — un invariante de dominio que necesita cargar otro agregado sería
precisamente la grieta que la R12 cierra. Tampoco como restricción de la base: PostgreSQL tendría
que mirar otra fila, y eso es un disparador, con el coste y la invisibilidad que tienen.

El fallo es un error de negocio con nombre, no una excepción (ADR-0004).

---

## Decisión 3 — Una conversión encadenada no compone, y preguntarla es un error con nombre

### El hueco

`ConversionUM` ya dice **«No hay transitividad»**: tener `kg→g` y `g→mg` no da `kg→mg`. Lo que no
dice —y es la mitad que se usa— es **qué pasa cuando alguien la pide**. Sin decidirlo, lo decidirá
la primera línea de la fase 1 que la necesite, y las tres salidas posibles (multiplicar, devolver
cero, devolver nulo) son las tres peores maneras de equivocarse en un inventario.

### Qué se decide

**No compone.** Pedir un par no declarado es un **error de negocio con nombre** —un 404 en el punto
que resuelva conversiones—, nunca un cero, nunca un nulo, y nunca el producto de la cadena. Motivos:

- **Encadenar multiplica el error de redondeo.** Cada factor arrastra hasta 5·10⁻⁷; tres saltos
  arrastran el triple, y en existencias eso se convierte en un descuadre que no tiene autor.
- **El orden de composición sería una entrada invisible.** Con varias cadenas posibles entre dos
  unidades, el número que sale depende de cuál elija el buscador de caminos. Un dato de negocio no
  puede depender de un detalle de implementación que nadie ve.
- **Un cero o un nulo se propagan sin ruido.** Un error con nombre para el proceso donde está el
  fallo: que falta declarar `kg→mg`.

La conversión que haga falta **se da de alta**, con su factor pensado y su redondeo pensado. Es una
línea en un maestro; el descuadre que evita no tiene precio conocido.

---

## Alternativas descartadas

- **Reutilizar el bloqueo (R16) como salida de los cuatro maestros.** Sale «gratis» —la columna y el
  `DELETE` ya existen para otros recursos— y estropea las dos cosas: mete datos no personales en la
  máquina de estados del artículo 32, y hace que «bloqueado» signifique dos cosas según la tabla.
- **Reutilizar el cierre.** Un cierre presupone una sucesión temporal. Una unidad de medida retirada
  no da paso a otra unidad.
- **Un `DELETE` de verdad para los maestros de instalación.** Es lo que más se parece a lo que quiere
  quien se equivoca al teclear, y es lo que rompe el histórico: un documento antiguo dejaría de saber
  en qué divisa se emitió.
- **Derivar la inversa dividiendo, en vez de exigir que se declare.** Es lo que el dominio ya rechaza
  y con razón: `1/12` no cabe en seis decimales, así que ir y volver no devolvería la cantidad de
  partida.
- **Una tolerancia redonda para la inversa** (un 1 %, un 0,1 %). Sería un número elegido por
  comodidad, y cualquier número elegido por comodidad admite discrepancias reales o rechaza
  redondeos legítimos. El margen que impone la escala no hay que elegirlo: se calcula.
- **Componer cadenas y documentarlo.** Un número correcto sobre el papel y no reproducible en la
  práctica, porque depende del camino.

## Consecuencias

- **Tres términos del glosario quedan definidos hasta el final**, incluido uno —«retirada»— que
  todavía no existe en el código y que ya no se puede usar para otra cosa.
- **La fase 1 hereda tres trabajos concretos**, anotados en `docs/PLAN.md`: la retirada de los cuatro
  maestros (con el primer módulo que los referencie), la comprobación de la inversa en la capa de
  aplicación, y el resolutor de conversiones con su error con nombre.
- **`Impuesto` se queda como está.** Su `/cierre` es correcto: un tramo se sucede. No se le añade
  retirada.
- **Nada de esto cambia el contrato de la fase 0.** `docs/api/openapi.json` no se toca en el 0.16;
  los verbos nuevos llegan con la implementación.
