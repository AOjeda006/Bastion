---
tipo: referencia
stack: [dotnet]
aplica_a: [csharp]
revisado: 2026-08-25
tags: [adr, dotnet, nuget, toolchain, ci, github-actions, cache]
---

# ADR-0002: La caché de paquetes no se reubica, y la CI se asegura de que exista

- **Estado:** aceptado
- **Fecha:** 2026-08-25

## Contexto

El primer *run* de CI con `Bastion.sln` ya creada
([run 32845328258](https://github.com/AOjeda006/Bastion/actions/runs/32845328258)) salió en rojo
en dos *jobs*. El del backend es el interesante:

```
JOB Backend (Bastion.sln): failure
       3. ¿Existe ya la solución?: success
       4. .NET: success
       5. Restaurar: success
       6. Compilar: success
       7. Formato: success
       8. Tests de dominio y de arquitectura: success
       9. Tests de integración (Testcontainers): success
      10. Migraciones: success
      12. Publicar resultados de test: success
  ->  23. Post .NET: failure
```

**Los doce pasos reales en verde, y el *job* en rojo.** El mensaje:

```
Cache folder path is retrieved for .NET CLI but doesn't exist on disk:
/home/runner/work/Bastion/Bastion/%NUGET_PACKAGES%
```

Detrás hay **dos hechos encadenados**, y el segundo es el que no es obvio.

**Primero: `%VAR%` sin definir no es un error, es un nombre de carpeta.** `nuget.config` traía

```xml
<config>
  <add key="globalPackagesFolder" value="%NUGET_PACKAGES%" />
</config>
```

NuGet expande variables con la sintaxis `%VAR%` **en cualquier sistema operativo**, no solo en
Windows. Si la variable **no está definida** —el caso de un portátil recién montado y el de un
*runner* limpio— no falla ni avisa: **toma el literal**, lo trata como ruta relativa y la resuelve
**respecto al propio `nuget.config`**, o sea dentro del repositorio. Comprobado en local antes y
después del arreglo:

```
antes:    global-packages: …\PROYECTOS Y REPOS\Bastion\%NUGET_PACKAGES%
después:  global-packages: C:\Users\Predator\.nuget\packages\
```

**Segundo: `actions/setup-dotnet` con `cache: true` puede tumbar un *job* después de que todo
haya pasado.** Su *post-step* guarda la caché, pregunta a la CLI por la carpeta de paquetes y
**falla si no existe en disco**. Y aquí no existía por un motivo que no tiene que ver con la
ruta: **la solución no consume ni un paquete de NuGet**. Los 19 `packages.lock.json` contienen
solo entradas `"type": "Project"` (el de `Bastion.Api`, el más cargado, tiene 18 y ninguna es un
paquete), así que ningún `restore` escribe nada y **la carpeta no llega a crearse ni siquiera en
la ruta correcta**. Borrar el `<config>` era necesario, pero **no suficiente**.

El diagnóstico de la causa se había anotado en `docs/PLAN.md` un commit antes, pero con el
alcance mal estimado: se predijo que mordería «con el primer `PackageVersion`, en el 0.2 o el
0.3». Mordió el mismo día, sin ningún paquete, porque quien toca esa carpeta no es `restore` —
es el *post-step*.

## Decisión

1. **`nuget.config` no fija `globalPackagesFolder`.** Se borra el bloque `<config>` entero y se
   deja el valor por omisión (`~/.nuget/packages`), que es el que se quiere y el que
   `actions/setup-dotnet` espera. La reproducibilidad la dan las fuentes explícitas con
   `<clear/>`, el `packageSourceMapping` y los `packages.lock.json` — **nunca la ubicación de la
   caché**.
2. **La CI crea la carpeta antes de usarla:** un paso `mkdir -p ~/.nuget/packages` justo después
   del paso `.NET`. Es idempotente, cuesta milisegundos y deja de hacer falta solo cuando la
   solución adopte su primer paquete de verdad. Mientras tanto, no estorba.

## Consecuencias

- **A favor:** el *job* del backend deja de depender de un efecto colateral del *restore*. La
  caché vuelve a ser compartida entre proyectos y entre *runs*. Y desaparece la bomba de relojería
  de encontrarse una carpeta `%NUGET_PACKAGES%/` sin rastrear —y **sin cubrir por el
  `.gitignore`**— dentro del repositorio en cuanto se adoptara el primer paquete.
- **En contra:** el `mkdir` es andamiaje que responde a una condición temporal (una solución sin
  paquetes). Cuando el 0.2 o el 0.3 adopte el primero, deja de tener efecto pero sigue ahí. Se
  quita en el 0.13, al revisar la CI de punta a punta, no antes: quitarlo pronto reintroduce el
  fallo si algún día un *job* corre sobre un subconjunto sin paquetes.
- **A vigilar:** cualquier `uses:` con `cache: true` añade *post-steps* que pueden fallar por su
  cuenta. No basta con mirar que los pasos escritos en el *workflow* estén en verde.

## Alternativas consideradas

- **Definir `NUGET_PACKAGES` en el `env:` del *workflow*.** Descartada: arregla la CI y deja el
  portátil roto, que es donde el fallo es más difícil de ver — allí no hay *post-step* que avise,
  solo aparece una carpeta rara en la raíz del repositorio.
- **Quitar `cache: true` de `actions/setup-dotnet`.** Descartada: apagar la señal en vez de
  arreglar la causa, y renunciar a la caché justo cuando empiecen a entrar paquetes.
- **Dejar el `<config>` y crear la carpeta dentro del repositorio.** Descartada: la caché de
  paquetes no es contenido del repositorio, y ningún `.gitignore` debería tener que saberlo.

## Aprendizaje transversal (materia prima para la biblioteca)

Tres cosas que valen más allá de este proyecto. La tercera es la que costó el error.

- **Una variable de entorno sin definir dentro de un fichero de configuración es peor que un
  error: es un valor válido y silencioso.** NuGet, y no es el único, convierte `%VAR%` no resuelta
  en un nombre de carpeta relativo. El síntoma aparece lejos del fichero que lo causa.
- **Un *job* de CI puede tener todos sus pasos en verde y estar en rojo.** Los *post-steps* que
  inyectan las acciones (`setup-*`, `cache`) corren fuera de lo que está escrito en el YAML y
  fallan por su cuenta. Al leer un *run* rojo, el primer sitio donde mirar no es el último paso
  escrito, es el **último paso ejecutado**.
- **La conclusión de la CI se lee; no se predice desde el disco propio.** `principios/testing.md`
  lo dice como regla —«afirma por el efecto y por el camino de verdad»; «leer la configuración, o
  verla en el log, no es la prueba»— y `herramientas/seguridad.md` lo repite. Aquí se incumplió
  dos veces en el mismo turno: se dio por verificado un `dotnet test` que salía **0 sin ejecutar
  ni un test** (no había proyectos de test), y se empujó sin abrir el *run*. Un comando que sale 0
  sin ejercer nada no es evidencia; el verde local no es la CI.
