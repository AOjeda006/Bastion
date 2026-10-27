---
tipo: referencia
stack: [dotnet]
aplica_a: [exportacion, importacion, terceros, seguridad]
tags: [adr, exportacion, importacion, csv, apostrofo, formulas, ida-y-vuelta, adr-0034, adr-0073]
revisado: 2026-10-27
---

# ADR-0075: El apóstrofo hace el viaje de vuelta: sale también delante de otro, y la importación quita uno

- **Estado:** aceptado
- **Fecha:** 2026-10-27
- **Sale del ítem 3.2**, al escribir la exportación de terceros, que es la única de las trece que
  tiene importación. Se preguntó porque el dato no volvía igual, y la respuesta, la recomendada,
  está en `docs/PLAN.md`, *La pregunta del apóstrofo, contestada*.
- **Enmienda el §6 del [ADR-0073](adr-0073-la-exportacion-recorre-el-listado-en-una-foto-y-escribe-el-dialecto-de-la-importacion.md)
  y el §5 del [ADR-0034](adr-0034-la-importacion-escribe-el-fichero-entero-y-el-recibo-caduca.md)**:
  el primero pasa de seis disparadores a siete, con el propio apóstrofo, y el segundo lee un campo
  de texto sin el apóstrofo que le puso la exportación.
- **Se apoya en** la decisión 7 del 1.11, en `docs/historia/fase-1.md`, que fijó la regla del
  apóstrofo antes de que hubiera una exportación.

## Contexto

La decisión 7 del 1.11 dice que un campo que escribe una persona y empieza por `=`, `+`, `-`, `@`, un
tabulador o un retorno sale con un apóstrofo delante. Es lo que una hoja de cálculo lee como «lo que
sigue es texto», y sin él, un `=HIPERVINCULO(…)` en una razón social se ejecuta al abrir el fichero.
El ADR-0073 la puso en el escritor común, solo en los campos de texto.

La importación de terceros no sabía nada de ella. Leía cada campo tal como llegaba, así que un
tercero con la razón social «=Foo SL», exportado y vuelto a importar, entraba como «'=Foo SL». El
criterio del 3.2 pide el dialecto de la importación para que el fichero vuelva a entrar, y entraba,
pero no igual.

Quitar el apóstrofo al leer no bastaba con seis disparadores. **«'=Foo» y «=Foo» salían iguales**,
los dos como «'=Foo», porque el apóstrofo no era uno de ellos, y al volver el primero habría perdido
el suyo. Un texto que ya empieza por un apóstrofo es raro, pero lo escribe una persona, y Bastion no
tiene por qué decidir que no lo quería.

## Decisión

1. **El apóstrofo es el séptimo disparador.** Un texto que empieza por `=`, `+`, `-`, `@`, un
   tabulador, un retorno o un apóstrofo sale con un apóstrofo delante: «=Foo» sale «'=Foo»,
   «'=Foo» sale «''=Foo» y «'hola» sale «''hola». La regla vive en un solo sitio,
   `ApostrofoDelDialecto`, en `BuildingBlocks.Application/Importacion`, y la usan los dos lados:
   `EscritorCsv` para ponerlo y la importación para quitarlo.
2. **La importación quita uno solo, y solo cuando lo sigue uno de los siete.** Es exactamente lo
   que se puso, así que `Quitar(Poner(x))` es `x` para todo texto. Un apóstrofo delante de otra
   cosa —«'0012», «'hola», o un apóstrofo solo— no lo puso Bastion, y se queda.
3. **Solo en los campos de texto, en los dos sentidos.** El escritor no se lo pone a un número, una
   fecha, un identificador ni un sí o un no (ADR-0073 §6), y la importación tampoco se lo quita: un
   importe «'-5» no es del dialecto y se rechaza con `formato-no-valido`, como antes. Si se le
   quitara, entraría como −5 un campo que nadie escribió así.
4. **Va en la lectura de los campos, no en `LectorCsv`.** El lector sigue leyendo la RFC 4180 sin
   saber de tipos. Qué columna es texto lo sabe la importación, en `FilasDeTerceros`, igual que en
   la exportación lo sabe `CampoCsv`. La próxima importación que lea texto quita con el mismo
   `ApostrofoDelDialecto.Quitar`.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| Los siete en los dos sentidos, lo demás tal cual, y el apóstrofo que no puso la exportación | `ApostrofoDelDialectoTests` | rápido |
| La vuelta en un universo de 7.381 textos, de hasta cuatro caracteres sobre los siete y dos que no lo son | `ApostrofoDelDialectoTests.Todo_texto_sale_y_vuelve_igual` | rápido |
| Que el escritor lo pone también delante de otro apóstrofo, y que lo escrito se lee y vuelve | `EscritorCsvTests` | rápido |
| Que la fila de terceros vuelve sin él en sus textos, lo conserva cuando no lo puso la exportación, y rechaza el importe que lo lleva | `LaFilaQueVuelveDeLaExportacionTests` | rápido |

El universo es el que rompería un disparador de menos en un lado y no en el otro, y su tamaño está
afirmado: un alfabeto que se quedara corto lo dejaría en verde sin mirar. La vuelta entera, exportar
terceros e importar lo exportado, es un caso de la exportación de terceros, y llega con ella.

## Alternativas descartadas

- **Quitarlo en la importación sin tocar el escritor.** Con seis disparadores, «'=Foo» y «=Foo»
  salen iguales, y el primero perdería su apóstrofo al volver.
- **Dejarlo, y que entre como parte del dato.** El fichero vuelve a entrar, pero el dato cambia en
  la primera vuelta: un tercero exportado e importado ya no es el mismo tercero.
- **Terceros sin apóstrofo.** Sus textos volverían iguales, pero va contra la decisión 7 del 1.11: es
  justo la exportación que saca lo que escribe una persona, nombres y direcciones, y la que se abre
  en una hoja de cálculo.
- **Quitarlo en `LectorCsv`, en todas las columnas.** El lector no sabe cuál es texto, y se lo
  quitaría a un importe.

## Consecuencias

- **Un texto tecleado a mano que empieza por un apóstrofo y uno de los siete pierde ese apóstrofo
  al importarse**: «'=Foo», escrito en un editor, entra como «=Foo». La importación no puede saber
  quién lo puso, y lo trata como el de la exportación. Es el precio de que lo que sale vuelva igual.
- **Un texto que empieza por un apóstrofo sale con dos**, aunque no se parezca a una fórmula.
- **No hay ficheros viejos que se lean mal.** La de terceros es la única exportación que tiene
  importación, y llega después de esta enmienda: ningún fichero que se pueda importar se escribió
  con la regla de seis.
