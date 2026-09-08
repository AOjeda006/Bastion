---
tipo: referencia
stack: [csharp, dotnet]
aplica_a: [ddd, dominio, testing, documentacion]
tags: [adr, conversiones, redondeo, tolerancia, ejemplo-trabajado, adr-0023]
revisado: 2026-09-08
---

# ADR-0033: El ejemplo de un ADR también se comprueba, y este contradecía su propia desigualdad

- **Estado:** aceptado
- **Fecha:** 2026-09-08
- **Sustituye a:** el **ejemplo trabajado** de la decisión 2 del
  [ADR-0023](adr-0023-los-maestros-de-instalacion-se-retiran-y-una-conversion-ni-se-invierte-ni-se-encadena.md).
  El resto de esa decisión sigue en pie **entero**: la desigualdad, la escala de la que sale la
  tolerancia, el sitio donde vive la comprobación y el rechazo del número elegido por comodidad son
  correctos y no se tocan. Lo que se sustituye son **catorce palabras**.

## Contexto

El ítem 1.7 implementó la decisión 2 del ADR-0023: si los dos sentidos de una conversión están
declarados, sus factores tienen que ser inversas plausibles el uno del otro, con `f` y `g` ya
redondeados a seis decimales, exigiendo

```
|f · g − 1|  ≤  5·10⁻⁷ · (f + g)
```

Al escribir el caso que ejerce esa desigualdad, el ejemplo que el propio ADR daba salió **rojo**.

## La redacción que se sustituye

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

## Consecuencias

- **El ADR-0023 queda más fuerte, no más débil.** La afirmación que sustituye al ejemplo es
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
