---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, ef-core, sql, concurrencia, multiempresa, idempotencia]
tags: [adr, r5, r8, r10, r12, numeracion, series, sql-crudo, cerrojo, transacciones, adr-0007, adr-0013, adr-0014, adr-0039]
revisado: 2026-09-19
---

# ADR-0040: El número lo toma una sentencia en el esquema de otro módulo, y esa es la segunda excepción al «único camino»

- **Estado:** aceptado, con la **cláusula 2 del criterio enmendada** por el [ADR-0041](adr-0041-el-cerrojo-del-ejercicio-lo-pide-una-lectura-cruda.md)
- **Fecha:** 2026-09-19
- **Enmienda el [ADR-0013](adr-0013-el-evento-va-en-la-misma-transaccion-y-el-efecto-ocurre-una-vez.md)**
  en sus dos frases cerradas: «la bandeja de salida es el único camino por el que un módulo escribe
  en otro» (contexto, §4 regla 5) y «la excepción a la prohibición de SQL crudo: **una**, nombrada,
  por su ruta y con su motivo» (§5). Pasan a ser dos, y aquí está **el criterio** que decide si
  alguna vez hay una tercera.
- **Corrige lo que `Serie.cs` prometía** desde el ítem 0.4.
- **Sale del ítem 2.4**, y da por supuesto el [ADR-0039](adr-0039-el-contador-de-una-serie-no-cabe-en-la-fila-que-lleva-el-etag.md),
  que es el que decide **dónde** vive el contador. Este decide **quién lo sube y cómo**.

## Contexto

La R5 dice que la numeración de documentos es «por serie y ejercicio, correlativa y sin huecos», y
el artículo 6.1.a del RD 1619/2012 dice lo mismo con más consecuencias. De esas tres cláusulas, la
que decide la forma del mecanismo es **«sin huecos»**, porque es la única que no se puede reparar
después: un número duplicado se descubre y se discute, un hueco es un documento que no existe y que
nadie puede enseñar.

Dos hechos, que juntos no dejan casi ninguna libertad:

- **El contador vive en `organizacion.contadores_de_serie`** (ADR-0039), o sea en el esquema del
  módulo de Organización.
- **Quien numera es el módulo del documento** —hoy Inventario, mañana Compras, Ventas, Facturación
  y Tesorería—, y el número tiene que quedar escrito **en la misma transacción** en la que el
  documento pasa a confirmado. Si no, hay un instante —o un para siempre, si algo falla en medio—
  con el documento confirmado y sin número, o con el número gastado y sin documento. Los dos son
  huecos.

Y el ADR-0013 cerró, con razón, el camino que a primera vista serviría: *la bandeja de salida es el
único camino por el que un módulo escribe en otro*. Un módulo que necesite algo del esquema de otro
publica un evento y espera. Aquí **no se puede esperar**: la bandeja es asíncrona por definición, y
un número que llega después no es un número que llega tarde, es una R5 rota mientras tanto.

## Decisión

### 1. El mecanismo vive en el bloque común, y cada módulo deriva el suyo sobre su contexto

`INumeradorDeSerie` en `BuildingBlocks.Application`, `NumeradorDeSerie` —abstracta— en
`BuildingBlocks.Infrastructure`, y cada módulo que numere declara una clase sellada sobre **su**
`DbContext`. Inventario declara la suya en el ítem 2.4 y es la primera.

**Por qué abstracta y por módulo, y no un servicio común registrado una vez.** Un servicio común
tendría que quedarse con algún contexto, y sería el de otro módulo: el número saldría entonces de
una transacción que **no es** la que confirma el documento. Es exactamente el fallo que el
mecanismo existe para impedir, y llegaría por la puerta del cableado, en silencio, y solo en
producción. Atando el numerador al contexto del módulo que confirma, «la misma transacción» deja
de ser una instrucción que alguien tiene que recordar y pasa a ser la única forma de construirlo.

### 2. La segunda excepción al «único camino», y el criterio que la acota

**La excepción, dicha entera:** la sentencia de numeración escribe en `organizacion.contadores_de_serie`
y lee `organizacion.series` desde el contexto de otro módulo, en SQL crudo, sin pasar por la bandeja
de salida y sin pasar por el modelo de Organización.

**El criterio, que es lo que este ADR aporta de verdad.** Un módulo escribe en el esquema de otro
fuera de la bandeja **solo si se cumplen las cuatro**:

1. **La escritura tiene que ser atómica con la del documento.** No «conviene»: si se parte en dos
   transacciones, hay un estado intermedio observable que rompe una **regla dura**, no una
   convención. Aquí ese estado es un documento confirmado sin número, o un número sin documento.
2. **Es una sola fila y una sola columna, con una sentencia que no lee nada para decidir.** El
   incremento opera **sobre lo que hay** (`ultimo_numero = ultimo_numero + 1`); no hay un valor que
   el llamante pueda traer ni una lectura previa que se pueda quedar vieja.

   > **Enmendado por el ADR-0041 (2026-09-25).** El motivo sigue en pie; la frase solo admitía
   > una forma de cumplirlo. Hoy la cláusula admite también una lectura que decide **si trae en
   > la misma sentencia el cerrojo** que impide que se quede vieja hasta el `COMMIT`: es lo que
   > hacen las dos sentencias del ejercicio del ítem 2.6, que no se pueden escribir sin leer.
3. **El módulo dueño no expone —ni puede exponer— un método que haga eso.** No es que no se haya
   escrito: `Serie` vive en `Organizacion.Domain` y ningún módulo ve el interior de otro, así que
   ningún método de allí es llamable desde aquí. El punto 6 lo desarrolla.
4. **Lo que el SQL crudo se salta, la sentencia lo repone dentro de sí misma y se comprueba.** El
   filtro global de inquilinato no se aplica al SQL a mano; el `WHERE` compara `empresa_id` contra
   el mismo `IInquilinoActual` del que lo toma el filtro, y hay un caso que lo pone en rojo si esa
   comparación desaparece.

**Por qué el criterio va aquí y no queda implícito.** La primera excepción —`CerrojoDeLaBandeja.cs`,
ADR-0013 §5— se justificaba por algo que **no vale para esta**: aquellas dos órdenes no leen ninguna
tabla, así que no había filas que el filtro de empresa pudiera haber protegido. Esta sí lee una
tabla con `empresa_id` dentro, así que hereda cero del argumento de la bandeja y necesita el suyo,
que es el punto 4. Con dos excepciones y sin criterio escrito, la tercera se decide por parecido
con las anteriores, que es como se pierde una regla: por acumulación, sin que nadie llegue a
discutirla. Con el criterio escrito, quien traiga la tercera tiene que **defender las cuatro
cláusulas** o traer un ADR que las cambie.

**Y la excepción está declarada por su ruta**, no descrita en prosa:
`ElFiltroNoSeSaltaPorAhiTests` lleva la lista cerrada de sitios que usan SQL crudo, y
`src/BuildingBlocks/Infrastructure/Numeracion/NumeradorDeSerie.cs` entra en ella con su motivo al
lado. Un fichero nuevo que use SQL crudo y no esté en la lista sale rojo en el carril rápido.

### 3. Dos sentencias y no una, y la de arriba es la que decide

El incremento se lleva **todo** el `WHERE`; la segunda sentencia solo lee lo que el incremento
acaba de escribir, sin condición ninguna, y es segura porque corre en la misma transacción sobre
una fila que el incremento mantiene bloqueada hasta el `COMMIT`.

**No se parten por gusto: una sola con `RETURNING` no cabe.** EF Core **compone** las consultas
crudas dentro de un `SELECT … FROM (…)`, y PostgreSQL no admite ahí una sentencia que escribe. Por
lo mismo, ninguna de las dos lleva punto y coma final: un `;` compuesto dentro de otra sentencia es
un error de sintaxis **en ejecución**, o sea en el humo o en producción, no al compilar.

### 4. El `WHERE` es la regla, y por eso no hay guardas de entrada que lo imiten

Tres cláusulas sobre `organizacion.series`, y ninguna es el tipo de documento: la serie existe, es
de esta empresa, y su estado es `Activa`. Las dos últimas sustituyen a las dos guardas que se
fueron con `Serie.RegistrarNumeroAsignado`.

**Por qué en el `WHERE` y no en C# antes de llamar.** Una guarda de entrada comprueba el estado de
la serie en el instante en que se la pregunta; entre esa respuesta y el incremento cabe otra
transacción que cierre la serie. El `WHERE` no tiene ese hueco: la condición y el efecto son la
misma sentencia, y el cerrojo de fila la sostiene hasta el `COMMIT`. Una guarda de entrada, además,
haría **parecer** que el mecanismo está protegido y desplazaría la atención del sitio que de verdad
decide — que es justo lo que un mutante bien puesto destapa (ADR-0038).

**El tipo de documento no está en el `WHERE`, y es deliberado.** Que una serie de albaranes no
numere una factura es una comprobación del **alta** del documento, no de su confirmación, y va
donde va cada documento. Meterla aquí la escondería en un sitio común y la haría depender de que
cada módulo pase bien un parámetro. El mecanismo numera; **no sabe qué numera**, y el caso que lo
demuestra —`ElCerrojoDeLaNumeracionTests.Una_serie_numera_sin_huecos_sea_cual_sea_el_documento_que_numera`—
recorre todos los valores de `TipoDeDocumento` sin nombrar ninguno.

### 5. Ninguna fila devuelta es un fallo, y los tres motivos dan el mismo error

Una escritura que no casa con nada **no lanza**: contesta «cero filas» y sigue. Dejarlo pasar
confirmaría el documento con el número que hubiera en la variable —cero, o el de otro— sin que la
serie se enterara. Así que cero filas devuelve `Resultado.Fallo` con `serie-no-numera`, un `409`.

**Los tres motivos posibles —serie cerrada, serie de otra empresa, serie inexistente— dan
exactamente el mismo error, y esa coincidencia es la decisión.** Distinguirlos convertiría la
confirmación en un oráculo de existencia entre sociedades de la misma instalación: quien probara
identificadores al azar sabría cuáles existen en otra empresa por el código de error. Es el mismo
criterio con el que el puerto de series contesta `NoExiste` a una serie ajena (R8).

Que la fila del contador **exista siempre** —nace con la serie, ADR-0039— es lo que permite que
«cero filas» no sea ambiguo. Si se creara al numerar por primera vez, la sentencia no podría
distinguir «cerrada o ajena» de «todavía sin contador», y el primer documento de cada serie fallaría.

### 6. Revienta si no hay transacción abierta, y por eso la confirmación exige la clave

El numerador comprueba `Database.CurrentTransaction` y **lanza** si no hay ninguna. No es una
precaución redundante: EF Core abre una transacción **implícita** por cada orden que ejecuta, así
que sin esa comprobación el incremento se confirmaría solo y no se notaría nada. El síntoma
llegaría mucho después y en otro sitio —un documento que falla al guardarse deja el número gastado,
o sea un hueco— y sería indistinguible de un fallo del motor.

Lanza en vez de devolver un fallo de negocio (ADR-0004): quien llama sin transacción no se ha
equivocado de datos, está **mal cableado**, y eso no es un desenlace que contarle a un cliente.

**Y de ahí sale la única acción de toda la API que exige `Idempotency-Key`.** El dueño de la
transacción es el filtro de idempotencia y nadie más (ADR-0014), y ese filtro **se aparta en su
primera línea** cuando la cabecera no viene. Una confirmación sin clave, por tanto, no es una
confirmación sin red: es una confirmación **sin transacción**, o sea el camino que el párrafo
anterior describe. Así que `POST /api/v1/inventario/ajustes/{id}/confirmacion` se declara
`[AdmiteIdempotencia(Obligatoria = true)]` y sin cabecera contesta `428`.

Eso invierte, **para esta acción y solo para esta**, la doctrina escrita en el ítem 0.9: «la clave
es una garantía que el cliente *pide*, no un peaje que se le cobra». La inversión no se deja a la
buena fe de quien escriba la siguiente acción: `TodaEscrituraDiceComoSeProtegeTests` lleva la lista
de acciones obligadas y la compara **en los dos sentidos** con las que llevan el atributo, y afirma
aparte que son exactamente una. Una acción nueva que lo declare sin discutirlo sale roja.

El `428` es el mismo código que el `If-Match` que falta, y comparten clase de error
(`TipoDeError.FaltaLaPrecondicion`) porque la frase que los dos dicen es la misma: la petición está
impecable y lo que falta es una precondición. Cuál de las dos lo dice el código del error.

### 7. Lo que `Serie.cs` prometía, y por qué no era ambiguo sino imposible

Desde el ítem 0.4, `Serie` llevaba escrito que la asignación del número —«bloquear esta fila dentro
de la transacción de confirmación, incrementar y componer»— era **del módulo de Facturación**, de
la fase 5, y que aquí vivía el dato y no el procedimiento.

Eso no era una imprecisión: era **imposible**. `Serie` vive en `Organizacion.Domain`, ningún módulo
ve el interior de otro y la frontera la vigila el compilador, así que ni Facturación ni ningún otro
módulo podría haber llamado nunca a un método de allí. El plazo —«en la fase 5»— tapaba la
contradicción porque nadie iba a intentarlo hasta entonces.

La corrección tiene dos mitades, y las dos están ya en el código:

- **El procedimiento vive en el bloque común**, como la bandeja de salida y como el almacén de
  idempotencia, que es donde va lo que todos los módulos necesitan y ninguno puede prestar.
- **`Serie.RegistrarNumeroAsignado` se borra.** No porque estorbe: porque nadie puede llamarlo, así
  que solo lo llamaban sus propios tests. Y lo que comprobaba —que el número recibido fuera el
  siguiente— **ya no puede fallar**, porque no hay número que un llamante pueda pasar: la sentencia
  incrementa sobre lo que hay. La invariante dejó de viajar en un método que alguien puede no
  llamar y viaja en la única sentencia que escribe esa columna.

`Serie.cs` lleva hoy escrito lo que sí es verdad, con el enlace al ADR-0039 para el dónde y a este
para el quién.

### 8. `TipoDeDocumento` se completa, y eso no es adelantar fases

El enumerado estrena los **tres documentos de Inventario** que van a numerar —ajuste,
transferencia y recuento— y pasa de seis valores a nueve. **Solo uno de los tres existe hoy como
documento**: el ajuste. Lo único que los otros dos cambian es que una serie se puede declarar de
ese tipo y que el recorrido del caso de «sin huecos» los incluye; su agregado y su máquina de
estados llegan en su ítem.

El motivo de traer los tres ahora y no uno por ítem es el caso que no sabe qué numera: recorre el
enumerado **entero**, así que un valor que entre más tarde queda cubierto sin que nadie tenga que
acordarse de ampliar nada, y uno que hoy no se pudiera numerar saldría rojo hoy.

## Consecuencias

- **Dos excepciones a la bandeja, y un criterio de cuatro cláusulas** que la tercera tendrá que
  defender. El ADR-0013 sigue siendo el sitio donde se lee el «único camino»; su contexto y su §5
  hay que leerlos con este delante.
- **El módulo que numera queda atado a la forma de la tabla de otro**: cuatro cadenas —esquema, dos
  tablas y una columna— escritas a mano en el bloque común. Eso se paga con
  `LaSentenciaDeNumeracionNombraLaTablaDeVerdadTests`, que las compara contra el modelo de EF Core,
  que es quien manda: un `ToTable` que cambie en Organización deja el mecanismo en rojo en el carril
  rápido, no en producción.
- **La confirmación de un documento serializa por serie.** Dos confirmaciones simultáneas de la
  misma serie se esperan la una a la otra hasta el `COMMIT`. Es el precio de «correlativa y sin
  huecos» y no tiene alternativa barata: cualquier cosa que no espere reparte el mismo número dos
  veces. Contra series distintas no hay contención ninguna, y una serie es por empresa y ejercicio.
- **Una acción de la API exige la clave de idempotencia**, y es la primera. El cliente que confirme
  sin ella recibe un `428` con el texto que explica cómo generar una.
- **El mecanismo no publica el formato.** `Serie.Formato` sigue donde estaba y componer el número
  visible —`AJU-2026-0001`— no es de este ítem: lo que la R5 exige es el correlativo, y es lo que
  el documento guarda.
- **Lo que este ADR no trae, dicho:** que un documento vaya a la serie **de su ejercicio** no lo
  comprueba nadie todavía; es del ítem 2.6, con el periodo (R9).

  > **Enmendado por el ADR-0041 (2026-09-25).** El ítem 2.6 hizo viva la R9 y **no** cerró esto:
  > el inverso de una anulación hereda la serie del original y estrena la fecha de hoy, así que
  > «la serie de su ejercicio» no tiene todavía una respuesta única. Queda en las preguntas
  > abiertas de `docs/PLAN.md`.

## Procedencia

Ítem 2.4 del checklist de `docs/PLAN.md`. La decisión de **dónde** vive el contador está en el
ADR-0039, tomada antes de escribir el código de este ítem; aquí está el mecanismo que la usa. La
enmienda al ADR-0007 §5 va en aquel y no en este, por el mismo motivo por el que la segunda
excepción al ADR-0013 va en este y no en aquel: quien lea un ADR enmendado tiene que encontrar en
**un** sitio qué parte de él sigue en pie.
