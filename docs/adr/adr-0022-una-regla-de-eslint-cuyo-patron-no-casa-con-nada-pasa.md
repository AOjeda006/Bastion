---
tipo: referencia
stack: [typescript, react, eslint, testing]
aplica_a: [arquitectura, testing, frontend, clean-architecture]
tags: [adr, eslint, fronteras, vacuidad, mutacion, feature-sliced]
revisado: 2026-09-03
---

# ADR-0022: Una regla de ESLint cuyo patrón no casa con nada pasa

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** implementa el criterio del ítem 0.16. Es el **ADR-0020 traducido a JavaScript**:
  el mismo modo de fallo —la regla verde por vacuidad— en una herramienta distinta, con un sitio
  nuevo donde esconderse. Hereda de él la afirmación de conjunto no vacío y del ADR-0019 la
  mutación como forma de comprobar que una regla mira.

## Contexto

El §10 del plan maestro y `stacks/react/convenciones.md`:39 dicen lo mismo: **una funcionalidad
nunca importa de otra**. Hasta el 0.16 eso estaba escrito en dos documentos y en ningún ejecutable,
que es otra manera de decir que no lo comprobaba nadie. El 0.16, además, reorganizó `src/features/`
para que **espeje los módulos del backend** (`identidad`, `organizacion`) en vez de los recursos, y
un renombrado es exactamente el momento en que las comprobaciones dejan de casar en silencio.

El sitio natural de la regla es ESLint: ya se ejecuta en la CI, ya rompe el build, y falla mientras
se escribe en lugar de veinte minutos después. `no-restricted-imports` con `patterns` hace justo lo
que hace falta y **viene en el núcleo**: sin paquete nuevo, sin licencia que comprobar.

Y ahí está el problema. **Una regla de ESLint cuyo patrón no case con nada pasa.** No avisa, no
cuenta cuántos ficheros ha mirado, no distingue «ninguno lo incumple» de «no he mirado ninguno». Un
glob mal escrito, una carpeta renombrada, un `files:` que apunta a un sitio que ya no existe — y la
regla sigue en el fichero, se lee perfectamente, y no prohíbe nada. Es la vacuidad del ADR-0020 en
otro idioma.

## Decisión

La frontera la ejecuta ESLint, y **cuatro comprobaciones distintas afirman que ESLint está
ejecutando algo**. Ninguna sobra: cada una tapa un agujero que las otras no ven.

### 1. La regla se genera del disco, no se escribe a mano

`eslint.config.js` lee las carpetas de `src/features/` y genera **una configuración por
funcionalidad**, cada una prohibiendo a las demás. Así una funcionalidad nueva queda vallada **por
existir**, y no porque alguien se acuerde de venir a este fichero a añadir dos líneas.

### 2. El descubrimiento revienta si encuentra menos de dos

Con cero o una funcionalidad no hay ninguna frontera que vigilar, así que el bucle generaría cero
reglas y `npm run lint` saldría verde sin prohibir nada. Si eso pasa, es que el descubrimiento está
roto —otro directorio de trabajo, una carpeta renombrada—, y entonces **es mejor reventar la
configuración que lintar sin regla**. Es la afirmación de conjunto no vacío, puesta en el único
sitio donde este fallo puede ocurrir sin que nadie lo note.

### 3. La lista declarada se compara entera contra el disco, en los dos sentidos

`src/features/funcionalidades.ts` afirma **a mano** qué funcionalidades hay. Una lista que se
descubre sola no puede desmentir al disco: si el barrido se rompe o alguien renombra una carpeta, la
lista descubierta cambia con ella y todo sigue verde sin haber comprobado nada. La declarada es lo
que alguien **afirma**, y `ElBarridoDeLasFronteras` la compara con `src/features/` en los dos
sentidos: una carpeta sin declarar es roja, y una declaración sin carpeta también.

De paso, `Funcionalidad` deja de ser `string` en el resto del frontal: una errata no compila.

### 4. La regla se comprueba POR EL EFECTO, no leyendo la configuración

Esta es la que importa, y es la que la mutación 2 dejó demostrada. El barrido instancia ESLint
programáticamente y, **para cada par ordenado** de funcionalidades, lintea un import prohibido y
exige que lo marque — en las dos formas que existen: la del alias (`@/features/otra/...`) y la del
camino relativo que sube y vuelve a bajar. Y lintea también tres imports que **no** debe marcar:
`@/shared/...`, `@/app/...` y uno de la propia funcionalidad. Los pares comprobados se cuentan y se
comparan contra `n·(n−1)`: si el bucle se quedara corto, el número no cuadraría.

Leer la configuración habría sido comprobar que el fichero pone lo que pone. Lintar es comprobar que
ESLint **hace** lo que el fichero pone.

Con una condición que costó tres rojos de la CI descubrir, y que está más abajo: **el que lintea
también tiene que demostrar que ha mirado algo.**

### 5. Y un barrido de fuentes, porque `no-restricted-imports` tiene un punto ciego

La regla ve los `import` estáticos y los `import type`. **No ve `import()` dinámico.** Así que un
barrido aparte resuelve todos los especificadores escritos bajo `src/features/` —estáticos y
dinámicos— y comprueba a mano que ninguno cruza, con su propio recuento de especificadores mirados
para que no pueda salir verde habiendo leído cero.

### Dos patrones por funcionalidad prohibida, y no tres

```js
group: [`@/features/${otra}`, `**/${otra}/**`]
```

- **El del alias.** Los patrones de `no-restricted-imports` se leen con la semántica de
  `.gitignore`, así que `@/features/organizacion` **a secas ya cubre todo lo que cuelga**. La cola
  explícita (`@/features/organizacion/**`) es redundante, y esto no es una lectura de la
  documentación: se quitó y el alias se siguió cazando (mutación 2b).
- **El ancho**, que solo pide que el nombre aparezca como carpeta a cualquier profundidad. Es el
  que atrapa `../../../organizacion/loQueSea.ts`, donde la palabra `features` ni siquiera aparece.
  Sin él, esa forma pasa: el lint sale verde y la frontera no existe. También comprobado quitándolo.

El precio del ancho es que una carpeta de `shared/` o de `app/` que se llamara igual que una
funcionalidad quedaría prohibida sin querer. **Que no exista ninguna es la quinta comprobación del
barrido**, así que el precio está vigilado en vez de anotado.

### Lo que la regla NO prohíbe, y es a propósito

`@/shared/**` y `@/app/**`. **La frontera va de funcionalidad a funcionalidad.** `organizacion`
necesita saber con qué empresa se está operando, y eso vive en `shared/sesion/`; si la regla
obligara a bajar la sesión dentro de `identidad` para no incumplirla, la regla estaría mal, no la
estructura.

El único cruce hacia el armazón que hay hoy es `PaginaDeAcceso.tsx` importando
`type { Diccionario } from '@/app/i18n/es.ts'`: un tipo, del diccionario, que es del armazón por
definición. Se deja permitido.

### Lo que además quedó atado, porque un renombrado rompe más cosas que los imports

- **Los espacios de nombres de los diccionarios son las funcionalidades que hay en disco**, lista
  entera y en los dos sentidos, en `es.ts` y en `en.ts`. Mover carpetas sin mover los espacios de
  nombres dejaría los diccionarios describiendo una estructura que ya no existe, y **TypeScript no
  diría nada, porque una clave es una cadena**.
- **Toda ruta declara de quién es su pantalla** (`duenio: 'armazon' | Funcionalidad`), y el barrido
  de rutas saca del `cargar` qué módulo importa de verdad y comprueba que vive donde su dueño dice.
  Sin eso, mover una pantalla del armazón a dentro de una funcionalidad —o al revés— es un `git mv`
  y dos imports que compilan, lintan y pasan los tests.

## Lo que demostró la mutación 2, y por qué está aquí y no en una nota

La mutación consistió en romper el **patrón** de la regla sin tocar ni un import: cambiar el
`files:` de `src/features/${f}/**/*.{ts,tsx}` a `src/funcionalidades/${f}/**/*.{ts,tsx}`, una
carpeta que no existe. La regla queda intacta, legible y perfectamente razonable en el fichero.

**`npm run lint` salió con código 0. Verde total.** Ni un aviso, ni una nota, ni un «0 ficheros
comprobados». La única cosa en todo el repositorio que se puso roja fue el test de comportamiento:

```
identidad puede importar de organizacion con el alias, y no debería:
  expected [] to not deeply equal []
```

Y conviene mirar lo que **no** se puso rojo: el barrido de imports siguió verde, correctamente,
porque ningún import cruzó de verdad. Las dos cosas juntas son el hallazgo: la comprobación que
mira el código no puede detectar una regla desarmada, porque el código está bien; y la que mira la
regla no puede detectar un import prohibido, porque la regla ya no existe. Hacen falta las dos.

Si esa mutación hubiera salido verde entera, la conclusión no habría sido «la mutación falló»: habría
sido que la regla no prohibía nada, y ese habría sido el resultado del ítem.

## El corolario, que costó tres rojos de la CI: comprobar por el efecto también se puede quedar vacío

La decisión 4 dice que la regla se comprueba lintando de verdad. Lo que este ítem aprendió, y no por
razonarlo, es que **el que lintea de verdad también necesita que alguien afirme que ha mirado algo**.

Los tres *jobs* del frontal salieron rojos en la CI mientras la máquina de desarrollo estaba verde
entera. La anotación decía que `identidad` podía importar de `organizacion` con el alias; el
diagnóstico que se añadió al segundo intento decía además que la configuración aplicada **sí**
llevaba `no-restricted-imports` con los patrones correctos, que el fichero no estaba ignorado, y que
ESLint no había dicho absolutamente nada.

La causa: **`typescript-eslint` mira `process.env.CI`**. Si vale `'true'` deduce «ejecución única» y
se ahorra el programa de TypeScript en modo vigilancia (`inferSingleRun`). El ahorro es correcto
para `eslint .`, que lee ficheros del disco. Pero sin ese programa no hay dónde poner un contenido
en memoria, y `lintText(codigo, { filePath })` acaba analizando **el fichero que hay en disco en vez
del texto que se le pasa**. El fichero de disco está limpio, así que ESLint no marcaba nada: ni el
import prohibido, ni —se comprobó después— un `any` descarado.

Se reprodujo en local con `CI=true` en una sola orden, después de haber descartado el sistema
operativo, la versión de Node, la de ESLint, la del paquete `ignore` que hace de motor de patrones,
la semántica de los globs y el orden de los pasos del *workflow*. **Ninguna de esas seis cosas era.**
La variable de entorno no estaba en la lista de sospechosos porque no se parece a una entrada del
problema.

Dos consecuencias, y las dos están en el código:

- **El arreglo:** el motor de este barrido se construye con
  `parserOptions.disallowAutomaticSingleRunInference: true`. Va en la instancia y no en el entorno
  del proceso: el único que lintea texto inventado es este barrido, y `npm run lint` debe seguir
  aprovechando la ejecución única.
- **Las dos preguntas de control**, que son la decisión 4 aplicada a sí misma. Antes de creerle a
  ESLint un silencio, por cada testigo se comprueba (1) que marca **un `any` descarado** —el
  canario: si calla, no está mirando ese fichero y nada de lo de abajo prueba nada— y (2) que el
  motor prohíbe el mismo import con una configuración inline que no lleva otra cosa. La primera que
  falle nombra la capa rota: ESLint no mira · el patrón no casa · la configuración del proyecto no
  llega.

Y una lectura que no conviene perder: **esto salió rojo, no verde**. La aserción es «que la lista de
avisos NO esté vacía», así que un ESLint que no mira nada falla en vez de pasar. Si el barrido se
hubiera escrito al revés —comprobando que los imports permitidos no se marcan— la ejecución única
habría dado verde en la CI para siempre, y la frontera habría sido un adorno desde el primer día.
La polaridad de una comprobación no es un detalle de estilo: decide qué pasa el día que el
comprobador se rompe.

## La tabla entera

| # | Mutación | Resultado |
|---|---|---|
| 1 | Un import de `@/features/organizacion/...` dentro de `identidad/acceso/model/` | **Rojo dos veces**: `npm run lint` código 1 con el mensaje de la regla, y el barrido de imports nombrando el fichero y el destino |
| 2a | `files:` apuntando a una carpeta que no existe, sin tocar ningún import | **`lint` código 0, verde total.** Solo rojo el test de comportamiento |
| 2b | Quitar el patrón ancho y dejar solo el del alias | `lint` código 0; rojo **solo** en el caso del camino relativo. El alias se seguía cazando → los globs de `.gitignore` ya cubren los descendientes, y la cola explícita sobraba |
| 3 | Renombrar `features/organizacion/` sin tocar el diccionario | **Rojo dos veces**: lista declarada contra disco, y partición de espacios de nombres del diccionario |
| 5 | Mover `PaginaDeInicio` del armazón a dentro de una funcionalidad | `typecheck` 0, `lint` 0, fronteras verde; rojo **solo** en el barrido de rutas — que es el que se escribió para esto |
| 6 | Un canario que ESLint no tiene por qué marcar (`export const x = 1;` en vez del `any`) | Rojo: «ESLint no ha marcado un 'any' descarado en el testigo de identidad: no está mirando ese fichero» |
| 7 | El patrón del motor inline apuntando a `@/nada/...` | Rojo: «el motor de ESLint no prohíbe '@/features/organizacion/...' ni con una configuración que no lleva otra cosa» |
| 2a *(otra vez, con `CI=true`)* | La misma de la 2a, en el modo en el que la CI ejecuta | Rojo, y nombrando la capa: «la configuración que le aplica **NO LLEVA** no-restricted-imports» |

La 5 merece una lectura: la frontera de ESLint **no la caza**, y no es un fallo. Meter una pantalla
del armazón dentro de una funcionalidad no cruza ninguna frontera entre funcionalidades; le presta
un dueño que no tiene. Es una regla distinta, y por eso hay una regla distinta.

## Alternativas descartadas

- **Un plugin de fronteras (`eslint-plugin-boundaries` y parecidos).** Habría traído la misma
  vacuidad —sus capas también se declaran con globs— más una dependencia que mantener y una licencia
  que comprobar, para hacer lo que `no-restricted-imports` hace de serie con dos patrones. Se
  descartó por precio, no por gusto.
- **Dejarlo como convención escrita.** Es el estado del que se venía. Escrito no es comprobado.
- **Referencias de proyecto de TypeScript, una por funcionalidad.** Vallarían el import cruzado con
  el compilador, que es más fuerte; a cambio, multiplican los `tsconfig`, rompen el `paths` único de
  Vite y hacen que un renombrado toque cinco ficheros de configuración. No compensa para dos
  funcionalidades, y la puerta queda abierta si algún día son quince.
- **Comprobar la configuración leyendo `eslint.config.js` desde el test.** Comprobaría que el
  fichero pone lo que pone. La mutación 2 es exactamente el caso en que eso sale verde y la frontera
  no existe.

## Consecuencias

- **Una funcionalidad nueva queda vallada por existir.** No hay ningún paso que recordar; sí hay uno
  que hacer: declararla en `funcionalidades.ts`, y si no se hace, el barrido lo dice por su nombre.
- **La comprobación por el efecto cuesta tiempo.** Instanciar ESLint con comprobación de tipos y
  lintar `n·(n−1)` textos tarda unos segundos, y el caso lleva un plazo de 60 s. Con dos
  funcionalidades son dos linteos; con quince serían doscientos diez, y ahí habrá que decidir si se
  comprueban todos los pares o una muestra que cubra cada funcionalidad a los dos lados.
- **El lint programático necesita un fichero real.** `lintText` con comprobación de tipos exige que
  el `filePath` exista dentro del proyecto de TypeScript; el barrido usa uno de verdad y le pasa el
  código de mentira. Si alguna vez ese fichero desaparece, el caso falla — que es lo correcto.
- **La frontera entre `shared/` y las funcionalidades sigue sin vigilar**, porque no es una frontera:
  es una dirección permitida. Lo que sí quedó vigilado es que ninguna carpeta de `shared/` o `app/`
  se llame como una funcionalidad, que es el único modo en que esa dirección podía envenenarse.
