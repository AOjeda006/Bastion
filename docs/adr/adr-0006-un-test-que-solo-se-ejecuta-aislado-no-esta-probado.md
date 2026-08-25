---
tipo: referencia
stack: [dotnet]
aplica_a: [csharp, testing, xunit, serilog]
revisado: 2026-08-26
tags: [adr, testing, estado-global, paralelismo, serilog, toolchain, falsos-verdes]
---

# ADR-0006: Un test que solo se ejecuta aislado no está probado

- **Estado:** aceptado
- **Fecha:** 2026-08-26

## Contexto

En el ítem 0.3, dos tests de la política de errores **pasaban al ejecutarlos solos y fallaban al
ejecutar la suite entera**. No era intermitencia ni un problema de tiempos: el fallo era
determinista en cada modo, y opuesto en cada uno.

La causa está en una opción por omisión de una biblioteca, no en el código de los tests:

```csharp
servicios.AddSerilog(configuracion => configuracion.WriteTo.Sink(sumidero));
```

`AddSerilog(Action<LoggerConfiguration>)` construye el registro y, **por omisión**, lo deja también
en el `Log.Logger` **estático**; el registro que resuelve el contenedor queda atado a ese estático.
Con dos `WebApplicationFactory` levantándose en paralelo —xUnit ejecuta colecciones distintas a la
vez—, el último host en construirse le pisaba el registro al otro. El sumidero de captura del host
pisado no recibía ninguna línea, y las dos aserciones que miraban el registro fallaban.

Ejecutado solo, ese test es el único que escribe el estático. Verde. Y el verde no significaba nada:
lo que estaba probando era una configuración que en la suite real no existe.

Esto no es un problema de Serilog ni de la política de errores. Es la forma general de una trampa de
*toolchain*: **una biblioteca guarda estado en un `static`, y el aislamiento del test lo esconde.**
`Log.Logger`, `Activity.Current`, `CultureInfo.DefaultThreadCurrentCulture`, `AppContext`, las
variables de entorno del proceso, `HttpClient.DefaultRequestHeaders` de un cliente compartido, un
caché estático de EF Core, `TimeZoneInfo.ClearCachedData()`: todos tienen la misma firma de fallo.

## Decisión

### La regla

> **Una verificación en aislado no cuenta como verificación.** Un test se da por probado cuando pasa
> **dentro de la suite completa**, en el mismo proceso y con el mismo paralelismo con que se va a
> ejecutar siempre.

Corolarios operativos:

1. **El comando que decide es el de la suite entera.** `dotnet test` sobre la solución, sin
   `--filter` que reduzca a un caso. Un `--filter` de nombre sirve para *depurar* un rojo, nunca para
   *declarar* un verde.
2. **Al arreglar un test aislado, se vuelve a ejecutar la suite entera antes de darlo por cerrado.**
   El arreglo puede haber movido el problema a otro caso en lugar de quitarlo.
3. **Cualquier host de prueba que capture registro usa `preserveStaticLogger: true`.** Es el caso
   concreto que motivó este ADR y no se vuelve a discutir:

   ```csharp
   servicios.AddSerilog(
       configuracion => configuracion.WriteTo.Sink(sumidero),
       preserveStaticLogger: true);
   ```

4. **Un test que necesita aislamiento lo declara**, no lo hereda del azar del orden de ejecución.
   En xUnit eso es `[Collection]` compartida o `DisableTestParallelization`, y escrito con el motivo
   al lado. Un test que solo funciona solo y no dice por qué es un test que va a fallar el día que
   alguien añada otro.

### Cómo se detecta antes de que muerda

El síntoma —«pasa solo, falla acompañado»— aparece **tarde y en la máquina de otro**. Tres cosas lo
adelantan, y las tres están puestas:

- **Ejecutar siempre la suite entera** (regla 1), que es lo que la CI hace de todas formas.
- **Que el verde diga cuántos casos ha ejecutado.** Un `dotnet test` que dejara de encontrar
  ensamblados, o un filtro que no casara con nada, sale con código 0 exactamente igual que un verde
  de verdad. Por eso los dos pasos de test de la CI emiten el recuento como anotación `::notice::` y
  fallan si baja del mínimo declarado.
- **Sospechar de toda opción por omisión que escriba en un `static`.** Cuando una biblioteca ofrece
  un parámetro llamado `preserve…`, `shared…`, `useStatic…` o `global…`, ese parámetro *es* la
  advertencia.

## Consecuencias

- **El coste es de segundos por iteración**, porque obliga a ejecutar la suite completa en vez del
  caso que se está tocando. Es el precio correcto: la alternativa la paga la CI de otro día, con el
  contexto ya perdido.
- **La regla se aplica también a los tests con Testcontainers.** Comparten proceso con los demás,
  luego comparten estáticos con los demás. Cada contenedor es aislado; el proceso de prueba que los
  gobierna, no.
- **`preserveStaticLogger: true` es obligatorio, no una opción de estilo**, en todo host de prueba
  que capture registro. Sin él, el sumidero de captura de un host puede quedarse mudo por culpa de
  otro host que ni siquiera participa en el test.
- **Esto no invalida los tests unitarios rápidos.** Un test de dominio puro sin estado global sigue
  siendo válido y sigue ejecutándose en milisegundos. Lo que cambia es qué comando se considera
  evidencia: el de la suite.

## Procedencia

Descubierto arreglando el ítem 0.3 (política de errores y `ProblemDetails`). Los tests concretos
están en `tests/Api.FunctionalTests/Errores/PoliticaDeErroresTests.cs` y el host que aplica la regla,
en `tests/Api.FunctionalTests/Errores/ApiConRutasQueFallan.cs`. La decisión sobre la frontera
`Resultado`/excepción, que es de lo que iba aquel ítem, vive aparte en el
`adr-0004-frontera-entre-resultado-y-excepcion.md`: esto de aquí es una trampa de *toolchain* y no
tiene nada que ver con el diseño de errores.
