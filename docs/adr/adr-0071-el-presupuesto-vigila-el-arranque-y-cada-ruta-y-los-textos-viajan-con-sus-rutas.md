---
tipo: referencia
stack: [typescript, react, vite, i18next]
aplica_a: [entrega-continua, ux-ipo, frontend, i18n]
tags: [adr, presupuesto, rendimiento, carga-diferida, code-splitting, ci, vacuidad, i18n, manifiesto, gzip, adr-0028, adr-0036, adr-0024]
revisado: 2026-10-27
---

# ADR-0071: El presupuesto vigila el arranque y cada ruta, y los textos de cada módulo viajan con sus rutas

- **Estado:** aceptado
- **Fecha:** 2026-10-27
- **Sale del ítem 3.1**, que es la respuesta 13(b) de la puerta de la fase 3: el arranque sigue en
  450 KiB, cada ruta diferida tiene un tope de 192 KiB, el total pasa a ser informativo, los textos
  de cada módulo viajan con sus rutas, y una línea informativa da lo comprimido. Está en
  `docs/PLAN.md`, en la casilla del 3.1.
- **Enmienda** el [ADR-0028](adr-0028-el-presupuesto-mide-el-arranque-no-la-suma-de-los-fragmentos.md)
  y el [ADR-0036](adr-0036-el-presupuesto-cuenta-lo-que-se-paga-aunque-no-este-en-el-indice.md):
  - del **0028**, el tope del total servido (§1 y §2), que pasa a ser una cifra que se publica y no
    corta (punto 3);
  - del **0036**, la lista declarada del arranque (§1), que gana un tercer grupo, el de los textos
    de cada funcionalidad (punto 6); y la alternativa del manifiesto de Vite, que sigue descartada
    para el arranque y se adopta para las rutas (punto 4).

## Contexto

El ADR-0028 dejó dos métricas con tope: el **arranque**, en 450 KiB, y el **total servido**, en 900,
«holgado a propósito», para que un paquete de 300 KiB que entrara por descuido se notara sin
discutir cada pantalla. El ADR-0036 corrigió qué es el arranque y dejó los dos topes como estaban.

**El total ha hecho lo que el ADR-0028 sacó del arranque: crecer por hacer lo correcto.** Eran
532 KiB en el 1.1, 593 en el 2.1 y **828** en `fa07a91`, al empezar la fase 3. Ninguna pantalla
pesaba más que la anterior: había más pantallas. La fase 3 trae las de Compras, y el tope iba a
saltar a mitad de fase. Y lo que el total vigilaba, el paquete gordo que entra por
descuido, siempre entra **por alguna pantalla**: es ahí donde degrada a alguien, a quien entra en
ella por primera vez, y es ahí donde la cifra global no dice nada.

**El arranque tenía la misma enfermedad, más escondida.** El diccionario del idioma, que el arranque
espera (ADR-0036), llevaba los textos de **todos** los módulos: 23 686 B en castellano en `fa07a91`.
Cada módulo nuevo los subía, y todo usuario los pagaba antes de pintar aunque no abriera nunca ese
módulo. Es el error que el ADR-0036 encontró, con otra cara: pagar antes de pintar lo que se lee
después.

**Y ninguna de las cifras decía lo que baja el usuario.** nginx sirve con `gzip`, y lo descomprimido
es lo que el navegador tiene que leer y ejecutar, no lo que viaja por la red.

## Decisión

### 1. El arranque, como estaba: 450 KiB

La definición del ADR-0036 —lo que el `index.html` referencia, más lo declarado, el mayor de cada
grupo de alternativas— y su tope. Lo que cambia es lo que pesa: sin los textos de los módulos, el
diccionario baja de 23 686 a 2 684 B (*Medido*).

### 2. Cada ruta diferida, su tope: 192 KiB

```text
ruta = el fragmento de su pantalla
     + lo que ese fragmento importa estáticamente, hasta el fondo, con sus hojas de estilo
     - lo que ya se pagó en el arranque
     + los textos de la funcionalidad dueña, en el MAYOR de sus idiomas
```

Es lo que se descarga **la primera vez** que alguien entra en esa ruta, con el arranque ya en casa.
Lo último es el §3 del ADR-0036 aplicado a cada ruta: los idiomas son alternativas, se paga uno, y
el tope promete sobre el peor. Un fragmento compartido entre dos rutas se cuenta **entero en las
dos**: quien solo abre una lo paga entero.

**192 es lo medido más un margen corto**, la regla del 0.10: la ruta más pesada son 173 KiB, y 192
le deja un 11 %. Un tope que no puede saltar ocupa el sitio del que avisaría.

**No es el «tope por fragmento» que el ADR-0028 descartó**, y la diferencia es la razón por la que
aquel se descartó: veinte fragmentos de 40 KiB pasan un tope por fragmento y arruinan lo que se
espera. Un tope por ruta los suma, porque suma el cierre de lo que la ruta arrastra.

### 3. El total, informativo

Se sigue midiendo y publicando —todo lo servido, sin los `.map` y sin el manifiesto—, y no corta.
Lo que vigilaba lo vigila ahora el punto 2, y con un nombre: el de la ruta por la que entró.

### 4. Las rutas se leen del manifiesto de Vite; el arranque, no

El ADR-0036 descartó el `manifest.json` **para el arranque**, y la razón sigue en pie: el manifiesto
no dice cuáles de las importaciones dinámicas de la entrada se esperan antes de pintar. Para una
ruta la pregunta es otra —qué arrastra su fragmento—, y esa el `dist` no la contesta y el manifiesto
sí: en el `dist`, un fragmento compartido no lleva escrito quién lo importa.

Así que el arranque se sigue leyendo del `index.html` más lo declarado, en
`scripts/ci/presupuesto-del-frontal.sh`, y las rutas, del manifiesto, en
`scripts/ci/presupuesto-por-ruta.mjs`, al que el primero llama con la lista de lo que ha contado. Y
como las dos fuentes **pueden** dar la misma lista para lo que comparten —lo que el `index.html`
referencia—, se comparan con las afirmaciones del ADR-0024: lo que el manifiesto pone en el
arranque está todo en lo contado, y lo contado de más solo puede ser un diccionario del arranque.

El manifiesto lo escribe `build.manifest` en `vite.config.ts`, y **no se sirve**: `Dockerfile.web`
borra `.vite/` con los mapas, y el humo de la CI lo comprueba en la imagen.

### 5. Lo que se afirma antes de medir

La disciplina del ADR-0020: una medida que no encuentra nada da cero, y cero pasa el tope. El guion
de las rutas falla, con su mensaje y sin medir, si:

- no hay manifiesto, o tiene más de un punto de entrada, o el que tiene no es `index.html`;
- el arranque del manifiesto y el del guion dejan de decir lo mismo (punto 4);
- una importación dinámica de la entrada no es ni un diccionario, ni los textos de los `type`, ni
  los textos de una funcionalidad, ni una pantalla: sería una descarga que nadie mide;
- algún fragmento que no es la entrada importa algo en diferido, por lo mismo;
- las pantallas del manifiesto no son las que importa `src/app/rutas.tsx`, en los dos sentidos;
- falta el fragmento de textos de alguna funcionalidad en algún idioma —las listas salen de
  `FUNCIONALIDADES` e `IDIOMAS`, en `src/`—, o alguno está en el arranque;
- alguna ruta mide cero bytes.

Sale con 2 si no ha podido medir y con 1 si una ruta se pasa, y el guion del arranque distingue las
dos salidas. El tope del arranque se compara **después** de medir las rutas, para que un rojo del
arranque no se coma el desglose de las rutas.

### 6. Los textos de cada módulo viajan con sus rutas

El diccionario del arranque se queda con lo del armazón: `comun`, `paginacion`, `rutas`, `sesion`,
`errores` e `inicio`. Cada funcionalidad tiene los suyos en
`src/app/i18n/<funcionalidad>.<idioma>.ts`, que Vite emite como fragmento propio, y llegan así:

- **con su primera ruta**: el `lazy` de la ruta descarga la pantalla y los textos de su dueña **a la
  vez**, y la ruta no se pinta hasta tener los dos. No hay ida y vuelta de más en serie: hay una
  petición más, en paralelo con la de la pantalla, la primera vez que se entra en un módulo;
- **al cambiar de idioma**: se descargan los textos de lo ya visitado en el idioma nuevo, a la vez
  que su diccionario, y el idioma cambia cuando han llegado todos. Si mientras tanto llega una
  ruta nueva, o el idioma vuelve a cambiar, se vuelve a mirar lo que falta antes de cambiar.

Siguen en un solo espacio de nombres, `traduccion`: los textos de una funcionalidad se añaden con
`addResourceBundle` bajo su clave, y ninguna llamada a `t()` cambia.

**Y la regla que hace que esto baste: los textos de una funcionalidad solo los lee el código de esa
funcionalidad.** El armazón, o la pantalla de otro módulo, los encontraría sin cargar y pintaría la
clave en crudo. Lo comprueba `LosTextosViajanConSusRutas.test.tsx` sobre el fuente, con su pareja:
la cadena que el barrido tiene que ver y la que no.

### 7. Lo comprimido, informativo

Una línea más con el arranque, la ruta más pesada y el total **como los sirve nginx**: `gzip` de
nivel 1, que es el de omisión, para los tipos de `gzip_types` y a partir de `gzip_min_length`
(`deploy/nginx.conf`). Sin tope: los topes miden lo que el navegador tiene que leer y ejecutar, y
eso es lo descomprimido. La línea está para que nadie razone el tiempo de descarga con la cifra
equivocada.

## Medido

Con `npm --prefix frontend run build` y después
`bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900` sobre `fa07a91`, y
`bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 192` sobre `301a631`, que trae los
guiones de este ADR:

| | `fa07a91` | `301a631` |
|---|---:|---:|
| Arranque | 435 553 B · **426/450** KiB | 416 730 B · **407/450** KiB |
| … el diccionario `es` | 23 686 B | 2 684 B |
| … el fragmento de entrada | 393 133 B | 395 312 B |
| La ruta más pesada | — | `PaginaDelRecuento`, 176 526 B · **173/192** KiB |
| Total servido | **828/900** KiB | 831 KiB, informativo |
| Comprimido: arranque · ruta más pesada · total | — | 145 · 61 · 298 KiB |

El arranque baja 19 KiB: el diccionario pierde 21 002 B y la entrada gana 2 179, que son el
cargador de los textos. El total sube 3, porque diez fragmentos de textos —cinco funcionalidades
por dos idiomas— pagan cada uno su envoltorio. Son **21 rutas**; la más ligera con textos propios,
`PaginaDeImportacion`, 11 KiB.

## Lo que enseñó la tanda

La de los textos encontró dos agujeros del arnés de `LosTextosViajanConSusRutas`, arreglados
antes de contar sus mutaciones, y los dos reutilizables:

- **Un caso que retiene una carga y se pone rojo no la suelta al acabar.** Soltarla en el
  `afterEach` la hacía correr dentro del caso siguiente, que la veía en su lista y se ponía rojo sin
  culpa: una mutación, tres rojos. Lo retenido se olvida y se queda esperando.
- **Un caso de espera precarga todo lo que no es su sujeto.** Quitar la espera de los textos salía
  verde con el caso solo, porque la pantalla tardaba más en compilarse que los textos en llegar, y
  la ventana miraba antes de que hubiera nada que ver. Con la pantalla cargada antes, lo único que
  puede retener la ruta son sus textos.

## Lo que este ADR NO es

**No es un presupuesto más laxo.** El total no cortaba nada que el tope por ruta deje pasar: un
paquete gordo entra por una pantalla y salta en esa ruta, con su nombre, en vez de en una suma que
no dice dónde. Y el arranque conserva su tope con 43 KiB de margen, 19 más que ayer, sin haberlo
movido.

**Ni un cambio de criterio.** Es el del ADR-0028 —se mide lo que se descarga, y cuándo— aplicado
una cuarta vez: a los `.map` (0.1), a las rutas diferidas (1.1), a lo que el arranque espera (2.1),
y ahora a lo que cada ruta espera.

## Alternativas descartadas

**Subir el tope del total.** Es la primera alternativa que descartó el ADR-0028, con la misma
razón: resuelve esta fase y vuelve a saltar dentro de dos, cada vez por hacer lo correcto.

**Quitar el total sin poner nada.** Entonces el paquete gordo no lo ve nadie. El tope por ruta es lo
que lo sustituye, no un adorno.

**Un tope sobre lo comprimido.** La proporción de `gzip` cambia con el contenido, y lo que el
dispositivo analiza y ejecuta es lo descomprimido. Además, la cifra dependería de la configuración
de nginx, que no es el artefacto.

**Leer el cierre de cada ruta de los `import` de sus fragmentos en el `dist`.** Se puede, porque los
fragmentos se importan por nombre relativo. Es analizar JavaScript minificado con un patrón, y el
manifiesto es la declaración del propio empaquetador de lo mismo.

**Un espacio de nombres de i18next por funcionalidad**, que es la forma de la casa para cargar
textos por partes. Obliga a nombrar el espacio en cada componente y cambia el tipo de cada llamada a
`t()`. Con un solo espacio y los textos colgados de su clave, el resultado es el mismo y no cambia
ninguna llamada.

**Diferir los textos de todos los módulos en un solo fragmento, después de pintar.** Los saca del
arranque y deja a todo usuario descargando los de todos los módulos, y a la primera pantalla de
cada uno esperando a que lleguen todos.

## Consecuencias

- El paso de la CI anota tres avisos —el arranque con su tope y el total, marcado como
  informativo; la ruta más pesada con el suyo; y lo comprimido— y deja en su registro el desglose
  de cada ruta.
- Una pantalla nueva no gasta del arranque ni de una bolsa común: tiene su tope, y si lo pasa, el
  rojo la nombra.
- Una funcionalidad nueva necesita sus textos, un fichero por idioma. Si falta uno, el guion de las
  rutas falla; si sus textos acaban en el diccionario del arranque, también.
- Cambiar de idioma con cinco módulos visitados descarga siete ficheros en vez de dos —el
  diccionario, los textos de los `type` y los de cada módulo—, todos a la vez.
- El presupuesto queda acoplado al formato del manifiesto de Vite. Es un acoplamiento con rojo: si
  el formato cambia, una de las afirmaciones del punto 5 deja de cumplirse y el paso falla diciendo
  cuál, en vez de medir cero.
