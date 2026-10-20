---
tipo: referencia
stack: [dotnet, efcore, postgresql, react, typescript]
aplica_a: [inventario, concurrencia, multiempresa, i18n, presupuesto, observabilidad]
tags: [adr, r3, instantanea, saldo, fecha-pasada, transito, cerrojo-consultivo, trabajo-periodico, presupuesto, adr-0044, adr-0036, adr-0030]
revisado: 2026-10-13
---

# ADR-0063: El saldo a una fecha es la instantánea más su tramo, y el cerrojo de la instantánea es de la empresa

- **Estado:** aceptado. **Enmendado por el ADR-0064 (§7) y por el ADR-0067 (§1 y §7).**
  - El [ADR-0064](adr-0064-el-avance-del-corte-se-mide-contra-el-plazo-de-cada-orden.md) enmienda,
    en el §7, el disparador y las métricas: el disparador es de 10 s o un fallo por plazo, porque la
    orden se corta a los 30 s; y a los tres contadores se suman un histograma de lo que tarda cada
    avance y un indicador del retraso del corte más viejo.
  - El [ADR-0067](adr-0067-el-avance-del-corte-va-un-mes-por-transaccion-y-el-cuadre-guarda-hasta-donde-llega.md)
    enmienda el §1 y el §7: el valor de la instantánea se suma del mes anterior y del libro de su
    mes, y no con `LasDebidas`, que desaparece; el avance va un mes por transacción hasta llegar;
    y el cuadre de cada vuelta es lo que tenga pendiente toda empresa, haya avanzado o no.
- **Fecha:** 2026-10-13
- **Sale del ítem 2.14** y de su puerta, que el usuario contestó el 2026-10-13 con la opción
  recomendada en las cuatro preguntas. Las preguntas y las respuestas están en `docs/PLAN.md`, en
  *Decisiones tomadas → La puerta del 2.14*.
- **Enmienda el [ADR-0044](adr-0044-la-existencia-se-mueve-en-la-sentencia-que-anota-el-libro.md)**
  en tres puntos:
  - **§5**: la empresa se pliega en C# y no con `hashtext`, y el compartido se toma en un sitio
    concreto del orden de cerrojos (puntos 5 y 6 de este);
  - **§6**: el cuadre compara también el valor de la instantánea, y recibe la empresa de quien lo
    llama (punto 2);
  - **§10**: el trabajo mensual se pone al día solo y cuadra solo la empresa que avanza (punto 7).
- **Amplía el [ADR-0036](adr-0036-el-presupuesto-cuenta-lo-que-se-paga-aunque-no-este-en-el-indice.md)**:
  un segundo grupo declarado, que tiene que existir y no se cuenta en el arranque (punto 9).
- **Amplía el [ADR-0030](adr-0030-el-texto-que-lee-una-persona-lo-escribe-el-frontal-desde-el-type.md)**:
  un cuarto caso al traducir un `type`, el de los textos que todavía no han llegado (punto 9).
- **Aplica** el ADR-0046 §5 (el valor de una fecha pasada es la suma de la columna `valor` del
  libro) y el ADR-0053 §1 (el tránsito de una fecha pasada sale de los documentos y de sus fechas).

## Contexto

**El §15 del plan maestro pone «consulta de stock a fecha pasada» en el criterio de la fase 2**, y
la R3 dice cómo: «instantánea periódica + saldo incremental». Hasta aquí la mitad estaba hecha. La
instantánea mensual existe desde el 2.7, se mueve en la sentencia que anota el libro y se puede
tirar y rehacer (ADR-0044). Pero nadie la leía, nadie avanzaba el corte, y su recálculo tomaba
`LOCK TABLE` sobre una tabla de todas las empresas.

**El ADR-0044 §5 dejó el cerrojo por empresa para este ítem**, porque es el que llama al recálculo
cada mes para todas. Con el `LOCK TABLE`, recalcular la empresa A detenía las confirmaciones de
cualquier almacén de la B.

**Y el arranque del frontal está a 8 394 B de su tope**: 452 406 B, 442/450 KiB. El 68 % del
diccionario del idioma son los textos de los `type` de error (33 454 B de 48 966 en el castellano),
que solo se leen cuando algo falla. La pantalla de este ítem cabía; la siguiente, no está claro.

## Decisión

### 1. El saldo es la cantidad y el valor, por artículo y almacén

> **Enmendado por el ADR-0067 (2026-10-19), en el tercer punto.** La reposición ya no suma el
> valor del libro con `LasDebidas`, que desaparece: cada mes lo suma de la instantánea del mes
> anterior y del libro de su mes, en la misma sentencia que el físico.

Lo contesta el usuario. Cada par de artículo y almacén que tiene valoración contesta, a la fecha D,
su cantidad y su valor, con la divisa de su valoración.

- **El valor solo significa algo sumado por artículo y almacén.** Por ubicación no: si entran 10
  unidades a 1 € en la U1 y 10 a 3 € en la U2, y salen 10 de la U1 al precio medio de 2 €, la U1
  queda con cero unidades y −10 €. La consulta nunca lo enseña por debajo de esa clave.
- **La instantánea gana una columna `valor`**, `numeric(18,4)` como el del libro, en la misma fila
  y por la misma sentencia que `fisico`. La sentencia que mueve la proyección le suma el `valor`
  del movimiento, que ya va redondeado (ADR-0046 §5), y la reposición del recálculo lo suma del
  libro con el mismo fragmento `LasDebidas`. No se redondea nada más: sumar importes de cuatro
  decimales da importes de cuatro decimales.
- **La migración rellena `valor` desde el libro** en las filas que haya. En producción no hay
  ninguna, porque ninguna empresa tiene corte; el relleno está para que la migración diga la verdad
  en cualquier base, no para un caso que exista hoy.

### 2. La lectura, en una sola sentencia

**El saldo a la fecha D es la instantánea del mes M más el libro desde el día 1 del mes siguiente
hasta D.** M es el mes anterior al de D, o el corte de la empresa si el corte es anterior. Sin
corte, es el libro hasta D.

- **Una clave sin fila en el mes M no se había movido todavía**, y suma cero. Las instantáneas son
  densas desde el primer mes de cada clave hasta el corte (ADR-0044 §3), así que la falta de fila
  es un dato y no un hueco.
- **Una sola sentencia**, sobre tablas del propio esquema, que filtra el libro por su fecha y poda
  las particiones. Pagina en la base, con su total, y entra en la lista cerrada de
  `ElFiltroNoSeSaltaPorAhiTests` con su motivo, como el resto del SQL crudo de Inventario
  (ADR-0044 §9).
- **El cuadre compara también el valor de la instantánea** con la suma del libro hasta el final de
  su mes, como ya comparaba `fisico`. Sin esa comparación, una sentencia que moviera la cantidad y
  no el valor dejaría la lectura mal y el cuadre limpio.

### 3. Las fechas

- **D posterior a hoy se rechaza** con `saldo-con-fecha-futura`, de clase `Validacion`: es un
  parámetro de una lectura, no choca con ningún estado, y el saldo del futuro no está en el libro
  (ADR-0044 §4). «Hoy» es el día UTC, como en el resto del módulo.
- **D anterior al primer movimiento contesta cero, y no un error.** La lista son los pares que
  tienen valoración, y cada uno contesta su saldo a D, que puede ser cero.
- **Sin D, hoy.** La respuesta lleva siempre la fecha que contesta.

### 4. El tránsito a esa fecha, dicho aparte

Lo contesta el usuario. Cada par contesta también la cantidad y el valor **en vuelo hacia ese
almacén** a la fecha D, sin sumarlos al saldo. El tránsito no está en el libro (ADR-0053 §1), así
que sale de las transferencias: las líneas de las que son originales, no inversas, y no están en
borrador, con

- la fecha de envío igual o anterior a D,
- la de recepción vacía o posterior a D,
- y sin ningún inverso con su fecha de recepción igual o anterior a D. El inverso nace `Recibida`
  con las dos fechas de hoy (ADR-0053 §5), así que su recepción es el día en que la anulación
  devolvió la mercancía.

La cantidad de cada línea es la introducida por su factor a la unidad base, redondeada a seis
decimales, como la que se anotó al enviar. El valor es el de la línea, que se fijó al enviar.

### 5. El cerrojo es de la empresa

**Dos `int4`.** El primero es una constante del módulo, `0x496E7631` («Inv1» en ASCII), que nombra
las instantáneas de Inventario. El segundo es la empresa.

- **No choca con el de la bandeja.** La bandeja usa una sola clave `int8`, y PostgreSQL separa los
  dos espacios. Lo dice su documentación, en *Advisory Lock Functions*: los recursos se identifican
  «either by a single 64-bit key value or two 32-bit key values (note that these two key spaces do
  not overlap)». La línea de la bandeja que dice que no hay ningún otro cerrojo consultivo se
  corrige.
- **La empresa se pliega en C#**, con un `XOR` de las cuatro palabras de 32 bits del `Guid`, y no
  con `hashtext(empresa_id::text)`, como proponía el ADR-0044 §5. `hashtext` es una función interna
  de PostgreSQL, sin promesa de que no cambie entre versiones. Y el plegado en C# se prueba en el
  carril rápido, con un valor fijo que lo pone rojo si alguien lo cambia, porque dos versiones del
  plegado en dos instancias a la vez no se esperarían.
- **Dos empresas con el mismo plegado se esperan entre sí**: pierden paralelismo, no corrección. Un
  caso del carril rápido construye esa colisión a mano, permutando las palabras de un `Guid`.

**Quien anota toma `pg_advisory_xact_lock_shared`; el recálculo, `pg_advisory_xact_lock`.** Los dos
son de transacción y se sueltan solos al acabarla. Los compartidos no se esperan entre sí. El
exclusivo espera a los compartidos de su empresa, detiene a los que lleguen después y no toca los
de otra empresa.

### 6. El compartido va en un solo sitio, y no forma ciclo

**Se toma justo antes de la sentencia que crea y bloquea las filas vivas**, que es por donde pasan
el libro y el tránsito. En el orden de cerrojos del ADR-0048 entra un eslabón, **entre la fila del
documento y las existencias**:

1. el ejercicio, compartido;
2. la marca de los artículos del documento, compartida;
3. el contador de la serie;
4. la valoración;
5. los lotes y las series;
6. la fila del documento;
7. **el cerrojo de la instantánea de la empresa, compartido**;
8. las filas de la existencia, y con ellas las instantáneas.

**Por qué no hay ciclo.** El recálculo toma el exclusivo lo primero, y mientras lo espera no tiene
nada cogido. Cuando lo tiene, ya no queda ninguna transacción de su empresa más allá del eslabón 7,
y las que lleguen esperan en él sin haber tocado una existencia ni una instantánea. Lo que el
recálculo toma después es la fila de su corte, que no toma nadie más, las instantáneas de su
empresa, y el `FOR KEY SHARE` que la clave ajena de cada instantánea nueva pide sobre su
existencia. Nadie que espere tiene ninguna de las tres.

**Una transferencia pasa dos veces por el eslabón**: al enviar, por la salida del origen y por el
tránsito del destino; al recibir, al revés. La segunda petición no espera aunque haya un recálculo
esperando el exclusivo. Lo dice la documentación de PostgreSQL, en *Advisory Locks*: «If a session
already holds a given advisory lock, additional requests by it will always succeed, even if other
sessions are awaiting the lock».

**Tres casos, con dos transacciones de verdad cada uno**, y cada uno con su mutación:

| Caso | Lo pone rojo |
|---|---|
| Dos empresas: la confirmación de la B no espera al recálculo abierto de la A | una clave sin la empresa, que es lo que hacía el `LOCK TABLE` |
| La misma empresa: la confirmación de una clave nueva espera al recálculo | el compartido al recalcular |
| La misma empresa: dos confirmaciones de series y claves distintas no se esperan | el exclusivo al anotar |

El encargo esperaba que el exclusivo al anotar pusiera rojo el primero. **Por construcción no
pasa**: con la clave de la empresa, el exclusivo de la B no choca con el de la A. Lo confirmó el
usuario en la puerta, y el exclusivo al anotar lo ve el tercer caso, que es lo que el ADR-0044 §5
prometía: dos confirmaciones de la misma empresa siguen sin esperarse.

### 7. El trabajo mensual

> **Enmendado por el ADR-0067 (2026-10-19), en el cuarto y el quinto puntos.** El avance no rehace
> las instantáneas desde el libro: avanza un mes por transacción, el del corte más uno, hasta
> llegar, y suelta el exclusivo entre uno y otro. Y el cuadre no va solo en la empresa que avanza:
> cada vuelta cuadra lo que tenga pendiente toda empresa, desde el último mes anotado en la fila
> del corte, y cada mes tiene su línea en el registro.

> **Enmendado por el ADR-0064 (2026-10-17), en el último punto y en el último párrafo.** A los tres
> contadores se suman un histograma de lo que tarda cada avance, en segundos, y un indicador de los
> meses de retraso del corte más viejo. Y el disparador no es un minuto: la reposición es una sola
> orden, que se corta a los 30 s. Es de 10 s, o un fallo por plazo.

El primer proceso periódico de Inventario, con la forma de la purga de recibos:

- **un `BackgroundService` que se despierta cada hora**, con `Task.Delay` sobre el `TimeProvider`.
  El mes que toca es el anterior al de hoy en UTC;
- **las empresas salen de las tablas del módulo**, las que tienen existencias, y no de
  Organización: una empresa sin movimientos no necesita corte. Se leen en un ámbito sin inquilino,
  con un motivo nuevo de la lista cerrada, `AvanceDelCorte`, que `ElFiltroNoSeSaltaPorAhiTests`
  admite en su sitio;
- **cada empresa en su propio ámbito y su propio `try`**. El fallo de una se registra, cuenta en su
  métrica y no para la vuelta, y la hora siguiente lo reintenta;
- **idempotente, y se pone al día solo.** Con el exclusivo de su empresa ya tomado, el avance lee el
  corte y no hace nada si ya llega al mes que toca. Si no llega, rehace las instantáneas desde el
  libro hasta ese mes, así que los meses saltados salen de una vez. Con dos instancias, las dos lo
  intentan y no importa: la primera que toma el exclusivo avanza, y la segunda encuentra el corte
  puesto;
- **el cuadre va después, solo en la empresa que avanza.** Sus cifras van al registro: cuántas
  existencias e instantáneas comparó, cuántos descuadres encontró y cuánto tardó el avance. Un
  descuadre es un aviso, con las cifras;
- **el recálculo y el cuadre reciben la empresa de quien los llama**, porque un trabajo de fondo no
  tiene inquilino. La API les pasa la del inquilino;
- **tres contadores en su propio medidor**, sin la empresa como etiqueta: empresas avanzadas,
  avances fallidos y descuadres encontrados.

**Se registra si hay base y si la configuración no lo apaga.** Los anfitriones de los tests de
integración comparten una base, y un trabajo que arrancara con cada anfitrión avanzaría el corte de
las empresas de otros casos a mitad de su caso. `ApiDeVerdad` lo apaga, y lo dice. El avance se
prueba llamándolo con su empresa, y el registro, mirando los servicios.

**Lo que cuesta, con su disparador.** Avanzar rehace todas las instantáneas de la empresa desde el
libro entero: es lineal en el libro, una vez al mes. Si el registro dice que el avance de una
empresa pasa de un minuto, se cambia por el de un mes desde el anterior, que solo lee el libro de
ese mes.

### 8. La superficie

- **`GET /api/v1/inventario/saldos`**, con `?fecha=`, `?almacen=`, `?articulo=`, `?pagina=` y
  `?tamanio=`, paginada como los recuentos.
- **Un permiso nuevo, `inventario.saldo.ver`**, para los roles del sistema que ven los recuentos.
- **La pantalla es `/saldos`**, con la ruta diferida: la fecha arriba, que por omisión es hoy, los
  dos filtros, y los nombres del artículo y del almacén por el mismo camino que el recuento. El
  tránsito sale en sus propias columnas, y la pantalla dice que no está sumado.

### 9. El presupuesto: los textos de los `type` salen del arranque

Lo contesta el usuario, con la (A). El tope no se sube.

- **Los textos de `errores.tipos` pasan a un módulo por idioma**, fuera de `es.ts` y `en.ts`, con
  la forma escrita en el castellano y comprobada por el compilador en el inglés, como el
  diccionario. Siguen en el mismo espacio de nombres y en la misma clave, así que
  `t('errores.tipos.…')` no cambia en ningún sitio: el módulo se mezcla en el diccionario cuando
  llega.
- **Se piden después de montar el árbol, y no antes.** El arranque espera al diccionario y no a los
  tipos. Cambiar de idioma espera a los dos, porque ahí la pantalla ya está pintada y se queda en el
  idioma viejo hasta que el nuevo está entero.
- **Quien traduce un `type` antes de que lleguen dice el genérico por código de estado**, y se
  vuelve a pintar cuando llegan, porque los componentes escuchan cuándo se añade un texto. Es el
  cuarto caso del ADR-0030: no es un `type` desconocido, así que no lleva la traza.
- **Ningún módulo importa un diccionario por su valor**, salvo el que los carga y los tests. Lo
  vigila el *linter*. Hoy `useTextoDeFallo` importa `es` para su guarda, y eso ata el fragmento
  castellano a once páginas: quien entra en inglés a `/almacenes` se descarga los dos diccionarios.
- **El presupuesto declara el fragmento de los tipos**, uno por idioma, que tiene que existir y no
  se cuenta en el arranque. Si los textos vuelven al diccionario, o algo del arranque importa el
  módulo, el fragmento desaparece y el paso se pone rojo.
- **El barrido de `ElCambioDeIdioma`** compara el catálogo de la API contra los módulos nuevos, en
  los dos sentidos y en los dos idiomas.

### 10. La propiedad

Tres clases de paso nuevas en `ElSaldoEsLaSumaDelLibroPorPropiedadTests`, que la aserción final de
cada semilla pide:

- **el saldo a una fecha al azar**, que puede ser anterior al primer movimiento, de un mes ya
  cortado, posterior al corte o hoy, contra la suma del modelo hasta esa fecha, en cantidad y
  valor; y mañana, con su `saldo-con-fecha-futura`;
- **el trabajo mensual**, que avanza el corte de su empresa, seguido del cuadre limpio;
- **la clave nueva antes del corte**: un movimiento fechado en un mes ya cortado que es la primera
  fila de su clave. Hoy sale al azar y nada lo exige. Su mutación: la serie de meses de la sentencia
  que mueve la proyección empieza en el corte y no en el mes del movimiento.

### 11. Las notas

- **«Nadie avanza el corte» se cierra** con el trabajo mensual.
- **«Hoy es el día UTC» sigue abierta**, para el cierre de la fase.
- **La pantalla del saldo contesta también el de hoy**, que es parte de la pregunta abierta de la
  lectura de las existencias. No la cierra: la lectura de hoy, con lo reservado y lo disponible, es
  otra pantalla.

## Medido

Las cifras de partida, con la orden que las mide:

- **El arranque, 452 406 B, 442/450 KiB**: `index.html` 450 B, `index-*.js` 387 319 B, la hoja de
  estilo 15 671 B y el diccionario `es` 48 966 B. Con `bash scripts/ci/presupuesto-del-frontal.sh
  frontend/dist 450 900`, sobre la construcción de `fdaf42f`, el último commit que toca el frontal.
- **Los textos de `errores.tipos`, 33 454 B de los 48 966 del fragmento `es`** y 33 327 de los
  48 151 del `en`: 234 tipos. Con `python medir-tipos.py frontend/src/app/i18n/es.ts
  frontend/dist/assets/es-B8hyXF4G.js`, que recorta el bloque por sus llaves.
- **El castellano, importado por doce fragmentos**: `grep -lE '(from|import)"\./es-B8hyXF4G\.js"'
  frontend/dist/assets/*.js` devuelve el de `useTextoDeFallo` y once páginas. El primero empieza por
  `import{es as a}from"./es-B8hyXF4G.js"`, y las páginas lo importan por él. El de entrada solo lo
  nombra en su `import()`, que es la carga por idioma.

Lo que cuesta cada parte, y qué pone rojo cada mutación, va en el PLAN al cerrar cada commit.

## Consecuencias

- **La R3 está entera**: la instantánea tiene un lector, un avance y un cuadre que la vigila en
  cantidad y valor.
- **Recalcular una empresa ya no para a las demás.** Dentro de la empresa, sí, y es lo que tiene que
  pasar: una confirmación en vuelo de una clave nueva no se ve hasta que confirma.
- **Inventario tiene su primer trabajo de fondo**, y la lista cerrada de ámbitos sin inquilino, su
  noveno motivo.
- **Un `type` nuevo deja de contar en el arranque**, y el diccionario del idioma baja unos 33 KB.
- **Para otro proyecto con PostgreSQL**: un cerrojo consultivo de dos claves deja que cada módulo
  tenga su espacio sin chocar con una clave `int8`; la segunda se pliega en el código de la
  aplicación, donde se prueba, y no con una función interna de la base.

## Procedencia

Las cuatro preguntas las hizo el agente en la puerta del 2.14 y las contestó el usuario el
2026-10-13, con la recomendada en las cuatro. Lo demás lo decide el agente, y está en la puerta, en
el PLAN, como lo que el ADR-0063 decide sin preguntar. El plegado en C# en vez de `hashtext`, el
cuarto caso del ADR-0030, la regla del *linter* y el grupo del presupuesto que no se cuenta salieron
al preparar este ADR, y el usuario no los ha visto todavía.
