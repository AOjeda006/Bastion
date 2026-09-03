---
tipo: referencia
stack: [typescript, react, vite]
aplica_a: [entrega-continua, ux-ipo, frontend]
tags: [adr, presupuesto, rendimiento, carga-diferida, code-splitting, ci, vacuidad]
revisado: 2026-09-03
---

# ADR-0028: El presupuesto del frontal mide el arranque, no la suma de los fragmentos

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** continúa la corrección del 0.1 (medir el `sourcemap`) y la del 0.10 (bajar el tope
  a lo medido más un margen corto). Aplica la doctrina del **ADR-0020**: una comprobación tiene que
  afirmar que ha mirado algo. Se implementa en el **ítem 1.1**.

## Contexto

El paso «Presupuesto de tamaño» de la CI mide `du -sk --exclude='*.map' dist` y corta en **600 kB**.
Su comentario dice, literalmente, que se mide **lo que el navegador DESCARGA**, y que por eso se
excluyen los mapas: «un sourcemap pesa más que el bundle y no se descarga al arrancar, así que
contarlo medía otra cosa distinta de la que dice esta frase».

Ese razonamiento sigue siendo correcto. Lo que ha cambiado es que **ya no se aplica entero**, porque
desde el 0.11 las rutas del frontal son **diferidas**: el enrutador las carga con `import()` y Vite
las emite en fragmentos aparte. Medido hoy, `dist` son diez ficheros y el navegador descarga **tres**
al arrancar:

| Fichero | Bytes | ¿Al arrancar? |
|---|---:|---|
| `index.html` | 450 | **sí** |
| `assets/index-*.js` | 388 139 | **sí** (lo referencia el `<script type="module">`) |
| `assets/index-*.css` | 11 648 | **sí** (lo referencia el `<link rel="stylesheet">`) |
| `assets/schemas-*.js` | 82 030 | no — llega con la primera ruta que valide |
| `assets/PaginaDeAcceso-*.js` | 35 496 | no |
| `assets/Paginacion-*.js` | 9 624 | no |
| `assets/PaginaDeInicio-*.js` | 11 433 | no |
| `assets/PaginaDeEmpresas-*.js` | 2 339 | no |
| `assets/PaginaDeAlmacenes-*.js` | 2 375 | no |
| `assets/PaginaNoEncontrada-*.js` | 402 | no |

**Arranque: 400 237 B (391 KiB). Suma de todo lo servido: 543 936 B (532 KiB).** El paso mide lo
segundo y afirma lo primero.

La consecuencia es al revés de como parece. **No es que el presupuesto sea laxo: es que castiga lo
que debería premiar.** Partir una pantalla en su propio fragmento mejora el arranque y **sube** el
número que la CI vigila. La fase 1 trae dos módulos enteros de pantallas diferidas: el tope saltaría
por crecimiento que no degrada el arranque, y el día que saltara la salida cómoda sería subirlo —que
es como un presupuesto deja de serlo.

## Decisión

### 1. Dos métricas, no una

- **Arranque** — lo que el navegador pide **antes de poder pintar nada**: los ficheros referenciados
  por `index.html` (el módulo de entrada, la hoja de estilo y cualquier `modulepreload`), más el
  propio `index.html`. **Tope: 450 KiB.**
- **Total servido** — todo lo que la imagen sirve, sin los `.map`. Vigila el crecimiento global sin
  castigar el troceo. **Tope: 900 KiB.**

### 2. De dónde salen los números

**El de arranque, de un cálculo.** Hoy son 391 KiB. En una conexión 4G de ~1,6 Mbps efectivos son
unos **2 s** de descarga, dentro de la franja que se considera aceptable para el primer render útil.
450 KiB es **lo medido más un margen corto (~15 %)**, que es exactamente la regla que el 0.10 dejó
escrita: un presupuesto que no puede saltar ocupa el sitio del que sí avisaría.

**El de total, holgado a propósito.** 900 KiB sobre 532 medidos: la fase 1 añade dos módulos y sus
pantallas, y este tope no está para discutir cada una, sino para que un paquete de 300 KiB que entre
por descuido se note.

### 3. Se cuentan **bytes**, no bloques de disco

`du -sk` redondea cada fichero al bloque del sistema de ficheros, así que da números distintos en la
máquina de desarrollo y en el *runner* — ya mordió en el 0.1 (1097 kB en local, 1104 en el *runner*)
y dejó escrito que «la cifra local no es la que decide». Sumando bytes, la cifra local **sí** es la
que decide, y el presupuesto se puede razonar sin ejecutar la CI.

### 4. La comprobación afirma que ha mirado algo

Una medida que no encuentra ningún fichero da **cero**, y cero pasa cualquier tope. Es la vacuidad
del ADR-0020 con otra cara: bastaría con que Vite cambiara el nombre del atributo, o con que alguien
moviera `index.html`, para que el paso midiera la nada y siguiera verde para siempre. Así que el
paso **falla** si el conjunto de arranque está vacío, si no incluye ningún `.js`, o si el arranque
sale mayor que el total.

## Lo que este ADR NO es

**No es un cambio de criterio.** Es el criterio que ya estaba escrito —«se mide lo que el navegador
descarga, y contar lo que no se descarga mide otra cosa distinta de la que dice esta frase»—
**aplicado donde dejó de aplicarse solo**. La primera vez que se aplicó fue a los `.map`; esta es la
segunda, a los fragmentos diferidos, y por la misma razón exacta.

## Alternativas descartadas

**Mantener la métrica y subir el tope a un número razonado.** Es lo que pedía la nota abierta, y
resuelve el síntoma de este trimestre sin tocar la causa: el número seguiría subiendo cada vez que
se hace lo correcto (trocear), y volvería a saltar por lo mismo dentro de dos fases.

**Un tope por fragmento** (`chunkSizeWarningLimit` ya lo avisa en el build). Vigila lo contrario de
lo que importa: veinte fragmentos de 40 KiB pasan y arruinan el arranque si son estáticos.

**Medir con una herramienta de análisis de *bundle*.** Más dependencia, más superficie, y el dato que
hace falta se saca leyendo el `index.html` que el navegador va a leer igual.

## Consecuencias

- El paso de la CI publica **las dos cifras** y su desglose, así que un `::notice::` dice de dónde
  sale cada una — igual que hace el recuento de tests.
- Añadir una pantalla **diferida** ya no consume presupuesto de arranque. Añadir un import
  **estático** en `main.tsx` sí, y ahí es donde tiene que doler.
- Convertir una ruta diferida en estática hace saltar el arranque **sin** tocar el total: exactamente
  la señal que hasta hoy no existía.
- La cifra local y la del *runner* pasan a coincidir, así que un ajuste de presupuesto se puede
  razonar sin gastar un *run*.
