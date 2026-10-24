---
tipo: referencia
stack: [dotnet, efcore, postgresql, react, typescript]
aplica_a: [inventario, observabilidad, rendimiento, ux-ipo, accesibilidad, pruebas]
tags: [adr, cuadre, jit, corte, metrica, propiedad, reflow, wcag, bloque-contenedor, adr-0067]
revisado: 2026-10-24
---

# ADR-0070: El cuadre lee cada mes sin JIT, lo que le falta tiene su indicador, y las páginas caben en 320 px

- **Estado:** aceptado
- **Fecha:** 2026-10-24
- **Sale del ítem 2.20**, que es la respuesta 4 de la puerta del 2.19: el 2026-10-20 el usuario pasó
  a un ítem propio las cuatro propuestas que el 2.18 y su recorrido habían dejado abiertas, cada
  una en su commit, con su caso y su mutación. Está en `docs/PLAN.md`, en la casilla del 2.20.
- **Enmienda el [ADR-0067](adr-0067-el-avance-del-corte-va-un-mes-por-transaccion-y-el-cuadre-guarda-hasta-donde-llega.md)**
  en cuatro sitios:
  - **§4**, el último punto: el indicador del retraso no cambia, pero ya no es el único (punto 2);
  - **§5**, el primer punto: la transacción de cada mes del cuadre empieza apagando el JIT del
    motor (punto 1);
  - ***Medido***: las tres cifras que quedaron sin explicar, ahora explicadas o explicadas en parte,
    con las cifras (*Medido* de este);
  - ***Lo que no cubre***, el tercer punto y el quinto: ya hay una métrica que dice que un cuadre
    no avanza, y el cuadre ya no paga el JIT (puntos 1 y 2).

## Contexto

**El 2.18 dejó cuatro propuestas y tres cifras sin explicar.** Las propuestas:

1. el cuadre del mes del corte pagaba unos 6 s de compilación del JIT del motor en B y en C, para
   una lectura que sin él tardaba 0,4 y 3,0 s (ADR-0067, *Lo que no cubre*, quinto punto);
2. ninguna métrica decía que un cuadre falla una y otra vez (el tercer punto);
3. la propiedad del saldo no tenía ningún movimiento en día 1, así que no veía la mutación 656, el
   libro de un mes leído desde el día 2 (la tanda del 2.18);
4. las páginas de los listados corrían en horizontal a 320 px, con la cabecera y los filtros (el
   recorrido del 2.17).

Las cifras, del *Medido* del ADR-0067: la tercera mensual de A, 6 222 ms; las tres mensuales de B
antes del `CHECKPOINT`, 11 s; y el recálculo de C, 46 202 ms.

## Decisión

### 1. Cada mes del cuadre, sin JIT

Lo primero de la transacción de cada mes de `CuadrarLoPendienteAsync`, antes de leer la fila del
corte, es **`SET LOCAL jit = off`** (`ElCuadreDeLasExistencias.SqlSinJit`).

- **`LOCAL`, para que no salga de la foto.** Con su `COMMIT`, la conexión vuelve al JIT que tenía.
  La marca, que va fuera de esa transacción (ADR-0067 §5), lo encuentra como estaba, y también lo
  que venga después por la misma conexión del *pool*.
- **Solo el cuadre de lo pendiente.** Lo que compila el JIT depende de lo grande que es el plan, y
  no de las filas que lee, así que no se amortiza con empresas más grandes. El mes del avance no lo
  paga: su coste estimado va de 13 054 a 37 415, por debajo de `jit_above_cost`, 100 000 (*Medido*).
  El cuadre entero, `CuadrarAsync`, sigue como estaba, porque solo lo llaman los casos y quien
  recalcula a mano.
- **Lo comprueba un caso de integración, y no uno funcional**, porque el JIT de una conexión solo
  existe en un PostgreSQL de verdad. Es
  `El_cuadre_lee_cada_mes_sin_jit_y_la_marca_lo_encuentra_como_lo_tiene_la_conexion`, con la semilla
  923: un interceptor de órdenes pregunta el JIT de la conexión, en su transacción, antes de cada
  lectura del cuadre y antes de cada marca. Todas las lecturas van sin JIT, hay una marca por mes, y
  cada marca lo encuentra encendido. La marca es la pareja: si el apagado se escapara de la foto,
  ella lo vería. El caso abre la conexión antes del cuadre, porque al cerrarla el *pool* la limpia
  con `DISCARD ALL`, y un `SET` sin `LOCAL` se borraría antes de que nadie lo viera.

### 2. Lo que le falta al cuadre, en un indicador

**`bastion.inventario.corte.meses_sin_cuadrar`**, un indicador en `{mes}` y sin la empresa como
etiqueta, por lo mismo que los del ADR-0064 §3. Son los meses que le faltan por cuadrar a la empresa
a la que más le faltan, de su primer mes sin cuadrar a su corte, los dos dentro: un corte sin
cuadrar es un mes, y no cero. Cero si todas lo tienen cuadrado; una empresa sin corte no tiene
instantáneas que cuadrar, y no cuenta.

- **El retraso no lo ve.** Una empresa cuyo cuadre falla cada hora tiene el corte en el mes que
  toca, así que su retraso es cero, y su cuadre no avanza.
- **Hasta el corte, y no hasta el mes que toca**: lo que el corte no ha alcanzado ya lo cuenta el
  retraso, y contarlo en los dos haría saltar dos alertas por lo mismo.
- **Se anota al acabar la vuelta, con el retraso, y de la misma lectura de los cortes**: lo que
  importa es dónde se han quedado, no dónde estaban. El corte de cada empresa lleva ahora desde
  dónde le falta, `SinCuadrarDesde`.
- **Empieza donde empezaría el cuadre**: el mes siguiente al cuadrado y, con lo cuadrado a nulo, el
  de `ElCuadreDeLasExistencias.SqlDelPrimerMesPorCuadrar`, la misma sentencia y no una copia. Esa
  recorre el libro de la empresa, y solo se lanza para las que tienen corte y nada cuadrado, que es
  tras rehacer el corte o con un cuadre que falla en su primer mes.
- **Es SQL crudo en un fichero que no lo tenía**, y la regla de `ElFiltroNoSeSaltaPorAhiTests` lo
  encontró: el carril rápido salió con un rojo, el suyo. Entra en la lista de saltos permitidos con
  su motivo: solo lee, devuelve un mes y no una fila, y compara la empresa en las dos tablas con la
  del corte que está mirando.
- **Los casos.** Sin base, el cálculo con sus bordes (los años, la más atrasada, sin corte); una
  vuelta en la que un cuadre parado deja el indicador en 3 con el retraso en 0, montada de forma
  que, medida antes de los cuadres, daría 6; y la unidad. Con base, lo que contesta el corte de cada
  empresa tras avanzar, tras un cuadre que para en junio, y tras cuadrar hasta el corte, que ya no
  deja nada.

### 3. La propiedad del saldo, con los dos bordes de cada mes

Dos de cada tres claves nuevas antes del corte van al día 1 de su mes o al último, y al final cada
semilla tiene en el libro un movimiento en cada uno de los dos. La fecha se fuerza sin sacar otro
número del azar, como el coste, y las seis semillas siguen pasando por todas las clases de paso. La
656, que lee el mes desde el día 2, sale roja en la propiedad.

### 4. Cada página de listado cabe en 320 px

Es el 1.4.10 de WCAG (*Reflow*, AA): a 320 px de ancho, la página no corre en horizontal. Exime las
tablas de datos, no la página que las lleva.

- **Cada tabla corre en su contenedor, `relative overflow-x-auto`**, con el margen en él: las
  dieciséis (`git grep -n "<table" -- 'frontend/src/**/*.tsx' | wc -l` y
  `git grep -n "relative[^\"']*overflow-x-auto" -- 'frontend/src/**/*.tsx' | wc -l`, 16 y 16).
- **Y cada lista desplegable de un filtro o de una cabecera lleva `min-w-0` en su caja.** Un
  `<select>` mide lo que su opción más larga, y con nombres de almacén largos ponía la página en
  449 px. El recorrido del 2.19 dio 320 porque los nombres eran cortos.
- **El caso mide cada ruta de `RUTAS`, con sus datos, en un Chromium sin cabeza a 320 px**,
  `LasPaginasCabenEn320.navegador.test.tsx`: el ancho del documento es el de la ventana, y cada
  tabla corre en su contenedor. Su pareja quita el contenedor, y la página tiene que salirse; por
  eso los datos son largos a propósito. El rojo dice qué elemento se sale y hasta dónde.
- **Es un carril aparte, el del navegador**: vitest en modo navegador con Playwright,
  `npm run test:navegador`, con su configuración, `vitest.navegador.config.ts`, que el carril de
  jsdom excluye. Es el Playwright que el §3 del plan maestro pone para los tests del frontal, por
  debajo de vitest. Las licencias, en la cabecera de esa configuración. La CI instala el Chromium y
  lo corre después del canal de la consola, y la batería de `AGENTS.md` lo lleva en el mismo sitio.

**Lo absoluto solo lo recorta su cadena de bloques contenedores.** Es lo que puso rojo el primer
commit de este punto, `b9892e6`, en la CI (run 40064764889): `/tarifas` medía 322 allí y 320 aquí.
Un elemento `position: absolute`, como el `sr-only` de una cabecera, no lo recorta una caja con
`overflow` que no tenga posición: su bloque contenedor está más arriba, y cuenta en el
`scrollWidth` del documento desde su posición estática. Dónde cae esa posición depende de la
fuente, y las de Linux son más anchas que las de Windows. El fallo se reprodujo, y el arreglo se
verificó, en la imagen de Playwright 1.64.0 sobre Ubuntu 24.04, la del *runner*:
`mcr.microsoft.com/playwright:v1.64.0-noble`. Así que:

- **el contenedor lleva `relative`**, que lo hace el bloque contenedor de lo que va dentro;
- **el caso mete una sonda**, un `sr-only` en la última celda de cada tabla, con la tabla a 2000 px,
  para que el resultado no dependa de un par de píxeles de fuente. Su pareja pone el contenedor en
  `position: static`, y la sonda tiene que empujar la página;
- **y el diagnóstico sigue la cadena de bloques contenedores**: no cuenta como recorte una caja con
  `overflow` que no es de esa cadena. Con la cuenta de antes, el rojo de la CI salió sin decir qué
  elemento se salía.

## Medido

**El entorno.** El humo del 2.18, en su propio proyecto de *compose*, `bastion-humo-220`, sobre
`f3b0bec`, con PostgreSQL 17.6 en Docker Desktop, en esta máquina, y los ajustes del motor por
omisión: `jit = on`, `jit_above_cost` 100 000, `jit_inline_above_cost` y `jit_optimize_above_cost`
500 000, `max_wal_size` de 1 GB y `checkpoint_timeout` de 300 s. Solo se añadió registro:
`auto_explain` a partir de 1 s y `log_autovacuum_min_duration = 0`. Las mismas tres empresas y la
misma semilla.

**Las órdenes.** `bash humo220.sh montar`, `medir`, `rehacer` y `rematar`, que llaman a los guiones
del 2.18: `medir.py` y `explicar.py`, y `recalcular.py`, ahora con `solo`, `vacuum` o `analyze`
entre la vuelta atrás y el avance. `coste.py` lee el coste estimado del cuadre del mes del corte con
el JIT encendido, y `python -X utf8 leer-pg.py <registro>`, el registro del motor. Viven fuera del
repositorio, en el *scratchpad* de la sesión.

**Lo medido**, en milisegundos y en el orden en que se midió. La espera de las confirmaciones no se
midió otra vez: el 2.20 no cambia el avance.

| Ronda | Meses | El mes más largo | Suma de los meses | Cuadre |
|---|---|---|---|---|
| A, la primera | 12 | 331 | 3 589 | 1 790 |
| A, mensual ×3 | 1 | 320 · 399 · 409 | — | 329 · 367 · 362 |
| B, la primera | 12 | 461 | 4 021 | 2 728 |
| B, mensual ×3 | 1 | 486 · 400 · 773 | — | 537 · 437 · 820 |
| B, la primera, tras un `CHECKPOINT` | 12 | 1 769 | 6 176 | 3 171 |
| B, mensual, tras el mismo | 1 | 372 | — | 474 |
| C, la primera | 12 | 4 364 | 37 663 | 11 255 |
| C, mensual ×3 | 1 | 3 146 · 3 405 · 3 116 | — | 3 190 · 3 356 · 3 652 |

Ninguna ronda dejó un descuadre ni una vuelta fallida, y las cuatro fases salieron con 0 fallos.

- **El cuadre mensual baja**: en A, de 923 · 864 · 6 222 a 329 · 367 · 362; en B, de 11 s a
  537 · 437 · 820 antes del `CHECKPOINT`, y de 6 563 a 474 después; en C, de 9 719 · 9 845 · 9 802 a
  3 190 · 3 356 · 3 652, de los 30 s del plazo. Los planes, sin JIT, como los lee ahora el cuadre:

  | Sentencia | A | B | C |
  |---|---|---|---|
  | El mes del avance, septiembre | 318 | 334 | 2 953 |
  | El cuadre de un mes, mayo | 78 | 275 | 797 |
  | El cuadre del mes del corte | 226 | 427 | 3 101 |

- **La tercera mensual de A: explicada.** El coste estimado del cuadre del mes del corte, en cada
  vuelta y con el JIT encendido, fue de 240 719,55 · 489 384,08 · 509 994,47. Las dos primeras
  quedan por debajo de 500 000, y el motor solo compila expresiones: *Inlining* y *Optimization* a
  `false`. La tercera queda por encima, y las dos pasan a `true`. Cruzaba el umbral de optimización.
- **Las mensuales de B antes del `CHECKPOINT`: explicadas en parte.** El JIT son 6,8 a 7,2 s de los
  11:
  - con la base quieta, 8 095,7 ms, de los que 6 801,4 son JIT (*Optimization* 3 317,1, *Emission*
    3 116,4, *Inlining* 268,3 y *Generation* 99,6), con 1 251 funciones;
  - durante un punto de control por `wal`, el que deja sembrar una cuarta empresa con 10⁶
    movimientos, 7 705,7 y 7 632,3, con 7 084,8 y 7 174,0 de JIT. El punto de control encarece el
    JIT en 373 ms como mucho.

  Los 4 s que quedan son del orden de las esperas de E/S medidas durante un punto de control, sin
  JIT: en esa misma tanda, el cuadre de un mes tardó 6 039,9 ms con `shared write` de 5 197,1, y el
  mes del avance, 4 370,7 con `shared read` de 3 597,1. Pero no se midieron sobre aquellas
  mensuales. Sin JIT, durante el punto de control de 03:36:19 a 03:38:02, las de B dieron
  537 · 437 · 820.
- **El recálculo de C: explicado.** Lo que lo mueve es la estadística de `instantaneas_mensuales`
  en el momento del avance, y no las tuplas muertas:

  | Entre la vuelta atrás y el avance | Total | El mes más largo hacia delante |
  |---|---|---|
  | Nada | 33 889 | 3 338 |
  | `VACUUM`, que deja 0 tuplas muertas | 32 077 | 2 839 |
  | `ANALYZE` | 53 403 | 7 537 |

  Los planes del `INSERT` del avance, de `auto_explain`: sin el `ANALYZE` en medio, un *Bitmap
  Heap Scan* y un *HashAggregate*, de coste 33 131, que estima 10 092 filas del mes anterior. Con el
  `ANALYZE` tomado cuando la empresa no tiene ninguna instantánea, un *Index Scan*, un *Sort* y un
  *GroupAggregate*, de coste 19 096, que estima una fila donde hay hasta 100 000; y los meses
  grandes tardan de 4 505 a 7 488. El `VACUUM` ahorra 1,8 s de 33,9. Los 46 202 del 2.18 quedan
  entre los dos, y con qué estadística avanzó aquel ya no se puede saber: su registro no anotaba el
  autovacío.
- **El avance no cambia en el 2.20, y sus cifras se mueven de una ronda a otra**: el mes más largo
  de la primera de C, 4 364, frente a los 2 729 del 2.18, y el de la primera de B tras el
  `CHECKPOINT`, 1 769, frente a 482. Los dos, por debajo del disparador de 10 s del ADR-0064 §2. No
  se han explicado.
- **El segundo arranque** sale con 0, en 18 s, y el rol del sistema concede los 115 permisos en los
  dos arranques. El cuadre de la API tenía C cuadrada hasta agosto, con el corte en septiembre,
  cuando se leyó: un mes sin cuadrar, de verdad. En `f3b0bec` todavía no estaba el indicador del
  punto 2.

## Lo que no cubre, dicho

- **El indicador dice cuánto falta, y no a quién.** No lleva la empresa: quién es lo dicen el `8702`
  de cada hora y el `8705` de cada cuadre.
- **Un mes ya cuadrado que se estropea después** sigue sin verlo el cuadre de lo pendiente
  (ADR-0067), y el indicador tampoco: cuenta desde lo cuadrado.
- **Con lo cuadrado a nulo, cada vuelta recorre el libro de esa empresa** para saber desde dónde le
  falta. Es una sentencia por empresa en ese estado, que no se ha medido aparte; recorre lo mismo
  que la búsqueda del primer movimiento, que el ADR-0064 midió en 0,66 s con 10⁶ movimientos.
- **La estadística del recálculo se queda como está.** El recálculo sigue sin entrada en producción
  (ADR-0044 §5), y lo que mide cuesta entre 32 y 53 s según cuándo pase el `ANALYZE`. Se propone, en
  el PLAN, que si algún día la tiene, el avance hacia delante analice la tabla después de su primer
  mes, y medir otra vez las tres variantes. No se hace sin encargo.
- **El carril del navegador mide el ancho, y no la altura.** El 1.4.10 pide 320 px de ancho para lo
  que corre en vertical, y 256 de alto para lo que corre en horizontal, y no hay nada de lo segundo.
- **El carril corre con `vitest` 4.1.11**, cuyo `@vitest/mocker` tiene dos avisos moderados que
  tocan justo a este modo, el registro de *mocks* del servidor del navegador (GHSA-qxg6-89mg-rqv8 y
  GHSA-xp26-3454-mjgp). Se corrigen en 4.1.12, y la subida va en la propuesta de la auditoría de la
  herramienta, no aquí. El servidor escucha solo en `localhost`.

## Consecuencias

- **Un cuadre atascado se ve en una métrica**, sin la empresa, y aunque el corte llegue.
- **El cuadre del mes del corte de C baja de 9,8 s a entre 3,2 y 3,7** de los 30 del plazo.
- **Cada página de listado cabe en 320 px**, y una tabla nueva sin su contenedor pone el caso rojo,
  sea cual sea la fuente de la máquina.
- **Para otro proyecto**:
  - en PostgreSQL, el JIT de un plan grande que lee pocas filas puede costar más que la lectura. Se
    ve en la sección *JIT* de `EXPLAIN (ANALYZE)`, y se apaga con `SET LOCAL` en la transacción, no
    en la conexión, que el *pool* comparte;
  - una caja con `overflow` solo recorta a un descendiente absoluto si está en su cadena de bloques
    contenedores: todo contenedor que corre lleva `position`. Y un test de disposición no depende de
    las fuentes de la máquina: lo que mide se ensancha hasta que un par de píxeles no cambien el
    resultado;
  - el indicador del progreso de un trabajo que se reintenta sale de lo que lleva hecho, en la misma
    lectura que el resto de indicadores de la vuelta, y no de lo que quería hacer.

## Procedencia

Los cuatro temas y su criterio los fijó el usuario en la respuesta 4 de la puerta del 2.19, el
2026-10-20. Lo demás lo decide el agente al montarlo: el `LOCAL`; que el caso del JIT sea de
integración; que el indicador cuente hasta el corte y no hasta el mes que toca, que se anote al
acabar la vuelta y que empiece en la sentencia del cuadre; la entrada en la lista de saltos
permitidos; los dos bordes forzados sin otro número del azar; el `relative` de los contenedores, la
sonda y su pareja; el `min-w-0` de las listas; y el carril del navegador con vitest y Playwright.
El usuario no los ha visto todavía.
