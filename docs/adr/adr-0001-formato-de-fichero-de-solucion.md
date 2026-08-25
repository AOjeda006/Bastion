---
tipo: referencia
stack: [dotnet]
aplica_a: [csharp]
revisado: 2026-08-25
tags: [adr, dotnet, toolchain, sln, slnx, ci]
---

# ADR-0001: La solución es `Bastion.sln`, no `Bastion.slnx`

- **Estado:** aceptado
- **Fecha:** 2026-08-25

## Contexto

Al crear la solución en el ítem 0.1, `dotnet new sln --name Bastion` con el SDK **10.0.301**
no produjo `Bastion.sln`, sino **`Bastion.slnx`**: el formato XML de solución pasó a ser el
**predeterminado** en el SDK de .NET 10 (`dotnet new sln --help` lo confirma:
`-f, --format <sln|slnx>` … `Predeterminado: slnx`).

El cambio es silencioso y el comando **no falla**, así que el error no se manifiesta donde se
comete. Se manifiesta en dos sitios, ambos tardíos:

1. **La CI se salta el backend entero sin ponerse en rojo.** El *job* `backend` de
   `.github/workflows/ci.yml` se activa con `if [ -f Bastion.sln ]`. Con un `.slnx`, la
   condición es falsa para siempre: no compila, no formatea, no ejecuta tests, y publica en el
   resumen del *run* el mensaje de «pendiente del ítem 0.1» — que es exactamente lo que se
   esperaría leer si el ítem no estuviese hecho. Es un **verde falso**, la peor clase de fallo
   de CI: el que se parece a un estado legítimo.
2. **La imagen de la API no se construye.** `deploy/Dockerfile.api` hace `COPY Bastion.sln ./`
   y el *job* `imagenes` se condiciona con `hashFiles('Bastion.sln')`.

Además, `Bastion.sln` no es un detalle de herramienta: es **identidad del proyecto**, fijada en
el Anexo A.1 del plan maestro y repetida literalmente en `AGENTS.md`, `README.md`, `.gitignore`,
`.gitattributes`, `.editorconfig` y `docs/PLAN.md`.

## Decisión

La solución se crea **explícitamente** en formato clásico:

```
dotnet new sln --name Bastion --format sln
```

El `--format sln` es obligatorio y no se omite «porque antes no hacía falta». Si algún día se
migra a `.slnx`, es un cambio con su propio commit que toca a la vez el fichero, la condición de
la CI, el Dockerfile y la documentación — nunca un efecto colateral de crear la solución.

## Consecuencias

- **A favor:** la CI arranca su mitad de backend en cuanto existe la solución, sin tocar el
  *workflow*, como estaba diseñado. La identidad del Anexo A.1 se cumple al pie de la letra. El
  `.sln` lo entiende cualquier versión del SDK, IDE o herramienta de terceros.
- **En contra:** se renuncia (por ahora) a las ventajas reales del `.slnx` — es XML legible, no
  lleva GUID y produce diffs limpios, que en una solución que va a tener del orden de setenta
  proyectos no es poca cosa. Se acepta a cambio de no reabrir la identidad a mitad de fase 0.
- **A vigilar:** la condición `[ -f Bastion.sln ]` de la CI es un *centinela por existencia de
  fichero*. Funciona, pero no distingue «todavía no» de «se llama de otra forma». Cuando el 0.13
  revise la CI de punta a punta, conviene que el *job* falle explícitamente si no encuentra
  **ninguna** solución, en vez de callar.

## Alternativas consideradas

- **Aceptar `Bastion.slnx` y adaptar CI, Dockerfile y documentación.** Descartada: cambia una
  decisión de identidad cerrada (Anexo A.1) por conveniencia de la herramienta, y obliga a tocar
  seis ficheros en el ítem cuyo trabajo es justamente crear la solución.
- **Crear ambos formatos.** Descartada: dos ficheros que describen la misma solución se
  desincronizan en cuanto alguien añada un proyecto con el IDE en vez de con la CLI.

## Aprendizaje transversal (materia prima para la biblioteca)

Dos cosas que valen más allá de este proyecto:

- **Un valor por omisión que cambia entre versiones mayores del SDK es un cambio de ruptura
  silencioso.** `stacks/dotnet/convenciones.md` ya obliga a comprobar la LTS vigente en vez de
  recordarla; el corolario es que **los comandos de andamiaje se escriben con sus opciones
  explícitas**, precisamente porque su valor por omisión es lo que más cambia.
- **Una condición de CI que se salta pasos «hasta que exista X» debe decir en voz alta que no ha
  comprobado nada, y no debe poder confundirse con el estado normal.** Aquí lo decía —el resumen
  del *run* lo explicaba— y aun así el fallo habría sido indistinguible del estado esperado.
