---
tipo: referencia
stack: [csharp, dotnet]
aplica_a: [ddd, dominio, testing, documentacion]
tags: [adr, conversiones, redondeo, tolerancia, ejemplo-trabajado, adr-0023]
revisado: 2026-09-11
---

# ADR-0033: El ejemplo de un ADR también se comprueba, y este contradecía su propia desigualdad

- **Estado:** aceptado
- **Fecha:** 2026-09-08
- **Corrige, y no sustituye** (la diferencia está escrita más abajo, y este encabezado decía
  «sustituye a» antes de que lo estuviera): el **ejemplo trabajado** de la decisión 2 del
  [ADR-0023](adr-0023-los-maestros-de-instalacion-se-retiran-y-una-conversion-ni-se-invierte-ni-se-encadena.md).
  El resto de esa decisión sigue en pie **entero**: la desigualdad, la escala de la que sale la
  tolerancia, el sitio donde vive la comprobación y el rechazo del número elegido por comodidad son
  correctos y no se tocan. Lo que se corrige son **catorce palabras**.

## Contexto

El ítem 1.7 implementó la decisión 2 del ADR-0023: si los dos sentidos de una conversión están
declarados, sus factores tienen que ser inversas plausibles el uno del otro, con `f` y `g` ya
redondeados a seis decimales, exigiendo

```
|f · g − 1|  ≤  5·10⁻⁷ · (f + g)
```

Al escribir el caso que ejerce esa desigualdad, el ejemplo que el propio ADR daba salió **rojo**.

## La redacción que se corrige

Del ADR-0023, decisión 2, tercer párrafo de *Qué se decide*:

> Con `f = 12` admite `g ∈ {0,083333, 0,083334}` —**las dos lecturas razonables de 1/12**— y
> rechaza `0,5` por seis órdenes de magnitud.

Queda así:

> Con `f = 12` admite `g = 0,083333` —el redondeo de 1/12 a seis decimales, que es el único que
> hay— y rechaza `0,083334` y `0,5`: la primera por poco y la segunda por seis órdenes de
> magnitud.

## Por qué `0,083334` no es un redondeo de 1/12

`1/12 = 0,083333333…`. Redondear a seis decimales admite **un** resultado, no dos: el más cercano.

| Candidato | Distancia a 1/12 | ¿Es su redondeo a seis decimales? |
|---|---|---|
| `0,083333` | `3,33·10⁻⁷` | sí — es el más cercano |
| `0,083334` | `6,67·10⁻⁷` | no — pasa de media unidad del último decimal (`5·10⁻⁷`) |

`0,083334` no sale de redondear: sale de **subir siempre**, que no es una regla de redondeo que
nadie aplique a un factor de conversión. La frase «las dos lecturas razonables» daba por hecho que
truncar y redondear dan resultados distintos aquí, y en este número **dan el mismo**.

Y la aritmética de la desigualdad dice lo mismo, que era lo esperable porque las dos preguntas son
la misma:

```
f = 12, g = 0,083334   →   |f·g − 1| = 8,0·10⁻⁶
margen                 →   5·10⁻⁷ × (12 + 0,083334) = 6,0416670·10⁻⁶
8,0·10⁻⁶  >  6,0416670·10⁻⁶   →   rechazado
```

**La desigualdad y el redondeo no podían discrepar, y por eso el ejemplo estaba mal y no la
fórmula.** Si `g` es el redondeo a seis decimales de `1/f`, entonces `|g − 1/f| ≤ 5·10⁻⁷` y por
tanto

```
|f·g − 1|  =  f · |g − 1/f|  ≤  5·10⁻⁷ · f  <  5·10⁻⁷ · (f + g)
```

con desigualdad **estricta**, porque `g` es positivo. Es decir: la regla no puede rechazar un
redondeo legítimo ni apurando, y cualquier par que rechace es un par que ninguna pareja de
redondeos del mismo cociente produce. Que `0,083334` cayera fuera no era un margen corto: era la
única respuesta posible.

## Decisión

**Se corrige el ejemplo, no la regla.** La desigualdad, la escala de la que sale `5·10⁻⁷`, el `≤`
—que es lo que hace entrar al caso frontera—, la capa donde vive la comprobación y el error de
negocio con nombre se quedan exactamente como están.

**Y el ejemplo deja de poder divergir de la regla.** El par que el ADR nombra entra como caso con
nombre propio en `LaAritmeticaDeLaInversaTests`, afirmando el **rechazo** y su aritmética, al lado
del caso frontera que ya afirmaba la igualdad exacta del margen. Quien lea el ADR y quien lea el
test llegan a la misma respuesta, y el día que alguien vuelva a ensanchar la tolerancia «para que
entre el ejemplo del ADR», hay un caso rojo esperándole con el nombre del par escrito.

## La forma, que hasta hoy se decidía de oído: corregir en el sitio no es sustituir

Esta es la tercera vez que hay que elegir entre **editar** un ADR ya aceptado y **sustituirlo** con
otro, y las tres veces se ha elegido bien sin que la distinción estuviera escrita en ninguna parte.
Queda escrita aquí, una sola vez, porque lo que había escrito decía lo contrario: *«una decisión
aceptada no se edita»*, a secas, en `docs/PLAN.md`.

**La frase es correcta para lo que nombra —una decisión— y falsa para lo que no nombra: un hecho.**
Son dos cosas distintas dentro del mismo documento:

| Qué cambia | Qué se hace | Por qué |
|---|---|---|
| **La decisión** — lo que se resolvió, o el criterio con el que se resolvió | **ADR nuevo que la sustituye**, citando la redacción anterior | La decisión anterior **rigió**: hay código, datos y commits tomados bajo ella. Borrarla deja sin explicación todo lo que se hizo mientras estuvo en pie. Es lo que hicieron el ADR-0015 sobre el punto 2 del ADR-0012 y el ADR-0032 sobre la alternativa descartada del ADR-0010 |
| **Un hecho dentro de una decisión correcta** — un número, un ejemplo trabajado, una cita | **Se corrige en el sitio**, con una línea que dice que antes decía otra cosa y adónde ir a leer por qué | Un hecho erróneo **nunca rigió**: no hay nada que explicar, solo algo que dejar de afirmar. Y un número equivocado que se deja en pie **no se lee como historia, se lee como permiso** — el siguiente que pase por ahí lo tomará por bueno, que es exactamente lo que este ADR vino a impedir |

**Las dos mitades, y por eso este ítem hizo las dos cosas.** El ejemplo del ADR-0023 se corrigió
**dentro** del ADR-0023 —en su párrafo, donde lo lee quien va a implementar la regla— y además se
escribió este ADR **al lado**, porque el *porqué* no cabe en el párrafo corregido y porque la
lección —«un ejemplo con números va a un caso de prueba»— sí es una decisión, y las decisiones se
escriben enteras. Lo que no se puede hacer es solo una de las dos: un ADR nuevo sin corregir el
sitio deja el número falso en pie para quien no siga el enlace, y una corrección sin ADR deja el
cambio sin motivo a la vista.

**Y la prueba de cuál es cuál, cuando haya duda:** pregúntese si alguien pudo **actuar** sobre lo
que se va a cambiar. Si alguien pudo escribir código distinto por creerlo, es una decisión y se
sustituye. Si lo único que pudo hacer es **creerlo**, es un hecho y se corrige.

## Consecuencias

- **El ADR-0023 queda más fuerte, no más débil.** La afirmación que ocupa el sitio del ejemplo es
  general y demostrada —ningún redondeo legítimo se rechaza— donde la anterior era un par de
  números, uno de ellos falso.
- **La lección, que es la única parte reutilizable.** Un ejemplo trabajado dentro de un documento
  se lee como si fuera parte de la decisión, y **no lo es**: es una comprobación que alguien hizo
  de cabeza y que nada volvió a hacer. Aquí el ejemplo se escribió razonando sobre el número
  —«redondeando hacia arriba y hacia abajo»— en vez de sobre la fórmula que estaba tres líneas más
  arriba, y sobrevivió cuatro días en un documento aceptado. Lo que lo destapó no fue releerlo: fue
  **ejercerlo**.
- **Regla práctica que sale de aquí:** si un ADR trae un ejemplo con números, ese ejemplo va a un
  caso de prueba con nombre. Un ejemplo que nadie ejecuta es una afirmación sin arnés dentro de un
  documento que se cita como fuente.
- **Ningún cambio de código de producción.** `LaInversaEsPlausible` no se toca: ya calculaba lo que
  el ADR decía calcular. Lo que cambia es un fichero de texto y un fichero de pruebas.
