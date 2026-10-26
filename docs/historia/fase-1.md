# PLAN — Bastion · Fase 1 (Maestros)

> **La historia de la fase 1, movida del PLAN en el ítem 3.1 (2026-10-27).** Es lo que
> `docs/PLAN.md` decía de esta fase en `de17700`, línea a línea y en su orden, bajo el encabezado
> de la sección de la que sale cada bloque. Se movió, no se reescribió: lo único que cambia son
> los enlaces a los ADR, que ganan un `../` porque este fichero está una carpeta más abajo. Lo
> comprueba `python scripts/comprobar-el-plan-partido.py`.
>
> Las *Notas / riesgos* de aquí son las cerradas cuya etiqueta lleva una fecha de esta fase,
> del 2026-09-03 al 2026-09-17.
> Una referencia a otra sección, como «ver *Notas / riesgos*», es a la de este fichero, o a la
> del PLAN si lo citado sigue abierto. Lo vigente está en el PLAN, y su índice de *Decisiones
> tomadas* lleva a cada bloque de las fases cerradas.

## Decisiones tomadas

### Tomadas por el agente de desarrollo — puerta de clarificación de la fase 1 (2026-09-03)

> **Doce preguntas, planteadas todas juntas y contestadas todas juntas**, como manda el `CLAUDE.md`
> §2. No se escribió una línea de código antes de tenerlas resueltas, y ese es el punto: la fase 1
> **no tiene Anexo A.3**. El §15 da el criterio de la fase entera —«alta de cliente/proveedor con NIF
> validado y de artículo con unidad, impuesto y tarifa; listados paginados y filtrados en servidor;
> dominio cubierto por tests»— y nada más. El desglose en ítems es una **decisión**, no un literal
> que copiar, y por eso vive aquí.
>
> **Tres cosas no se preguntaron**, porque el plan maestro ya las decide y reabrirlas sería tocar el
> §7:
>
> - **El modelo de tarifa** lo fija el §7.3: `Tarifa` con vigencia y divisa; `LineaTarifa` por
>   artículo **o** categoría, con precio **o** descuento, escalado por cantidad. Lo que el §7.3 **no**
>   fija —precedencia, exclusividad, solape, ausencia— sí se decidió, y está en P5.
> - **La unicidad del identificador fiscal** la fija el §7.2: «su identidad es única por CIF **y**
>   empresa». Dentro de una empresa no se repite; entre empresas sí. Lo que quedaba abierto está
>   en P4.
> - **`Tercero` es un solo agregado con roles** (§7.2), así que es **una** carpeta `terceros/` y no
>   `clientes/` y `proveedores/`.

#### P1 · El desglose en ítems — once, con dos ajustes sobre lo propuesto

**Decidido:** once ítems, cada uno con criterio verificable, en el orden de la tabla de más abajo.
Dos cambios sobre la propuesta inicial del agente, los dos del usuario y los dos con el mismo
motivo —**decidir barato hoy en vez de urgente mañana**—:

1. **El presupuesto del frontal pasa de último a primero.** El propio P11 demuestra que la métrica
   ya mide mal **hoy**, y que la fase 1 va a añadir pantallas diferidas que engordan el número sin
   empeorar el arranque. Dejarlo al final significa que salta en mitad de Catálogo, con una pantalla
   a medias, y ahí es cuando un tope se sube por prisa — que es exactamente lo que la nota abierta
   del riesgo dice que no se haga.
2. **El contrato de listado absorbe el P12.** Se está tocando ese contrato y están a punto de nacer
   dos módulos más: es el momento más barato que va a existir para consolidar los tipos de
   paginación. Después son cuatro copias.

**Por qué el cruce mutuo es ítem propio.** Del §7 salen dos dependencias en sentidos opuestos:
`Tercero.TarifaAsignada` mira a Catálogo y `ArticuloProveedor` mira a Terceros. Ninguno de los dos
módulos puede hacerse entero primero, así que la mutualidad se hace **visible** en un ítem en vez de
colarse dentro de otro.

**§15 respetado:** la fase termina desplegable y no hay dos fases abiertas a la vez. Nada de la fase
2 en adelante entra aquí — en particular `CodigoBarras`, que arrastra
`negocio/identificacion-articulos/convenciones.md`, y ese import es de la **fase 2**.

#### P2 · La cuarta vía en rojo — regla por reflexión, con dos fuentes

**El problema.** De los cuatro caminos por los que Catálogo puede tocar a Organización, tres se
ponen rojos solos: una clave foránea entre esquemas la rechaza el SQL, tocar `Organizacion.Domain`
lo rechaza el compilador, y referenciar `Organizacion.Contracts` sin declarar el cruce lo rechaza
`LasFronterasEntreModulosTests`. El cuarto —**guardar `unidad_id` sin validarlo contra nadie**— sale
**verde en todas partes**. Es el camino equivocado que no avisa.

**Decidido:** una regla nueva del carril de arquitectura, con **dos fuentes independientes
comparadas enteras y en los dos sentidos**, que es el patrón de `funcionalidades.ts` y el de
`Inventario`:

- **La lista declarada a mano** de las propiedades que son identificadores de otro módulo, con su
  dueño. Entra por aquí lo que el nombre no delata.
- **El descubrimiento por reflexión** sobre el dominio compilado: todo `Guid` de un agregado cuyo
  nombre case con un agregado de otro módulo. Entra por aquí lo que nadie declare.

Y para cada entrada, la exigencia: **tiene que existir un cruce declarado hacia el `Contracts` de
ese módulo**.

**Por qué dos fuentes y no una.** La heurística de nombres sola tiene un agujero que aparece en el
primer caso real: el §7.3 le da al artículo una **unidad base**, y ese campo se llamará
`UnidadBaseId`, no `UnidadMedidaId`. No casa, la regla calla, y el agujero queda abierto justo donde
se abrió para cerrarlo. La lista declarada sola tiene el agujero simétrico: un `DivisaId` que nadie
apunte no existe para la regla. Cada fuente tapa el fallo de la otra, y compararlas enteras es lo
que impide que una envejezca en silencio.

**Mutación que la valida:** añadir `DivisaId` a un agregado que no tenga puerto, y ver el rojo.

**Lo que se descarta, y por qué.** Declarar la línea de `s_crucesDeclarados` **antes** que el código
—para que el rojo llegue el día que Catálogo compile sin usar el puerto— tiene un precio que no se
puede pagar: la comparación es **entera y en los dos sentidos**, así que una línea declarada sin
código que la ejerza deja el carril **en rojo desde que se escribe**. Eso choca de frente con «cada
commit deja el árbol verde» (`AGENTS.md`). El puerto, su primer consumidor y su línea de cruce van
en el **mismo commit**; lo que obliga al siguiente agregado no es el orden de escritura, es la regla
de P2.

#### P3 · El cruce mutuo Terceros ↔ Catálogo — los dos, declarados, en ítem propio

**Decidido:** se aceptan los dos cruces, cada uno por el `Contracts` del módulo dueño y declarado en
`s_crucesDeclarados`, en el ítem 1.10.

No es un ciclo de proyectos —`Contracts` no arrastra el interior de su módulo, así que compila—,
pero sí una dependencia mutua entre módulos, y por eso se hace visible en vez de repartirla.

**Descartado el evento de integración** para uno de los dos sentidos: son **lecturas**, no
reacciones a un cambio, y el §4 ya dice que las lecturas van por la interfaz del `Contracts` del
dueño resuelta en proceso. Un evento pagaría coste de bandeja, de idempotencia y de orden a cambio
de nada.

#### P4 · Qué valida «NIF validado»

**Decidido:** **NIF, NIE y CIF con su letra de control**, validados de verdad en el objeto de valor.
El identificador extranjero se guarda como **identificador opaco con su país**, sin validar y
marcado como tal. **Nada de VIES en el camino del alta**, pero con su hueco previsto: el campo de
estado —*sin verificar · verificado · rechazado*, con su fecha— **existe desde el primer día**, para
que la comprobación diferida sea un caso de uso nuevo y no una migración sobre datos.

**Por qué no VIES ahora.** Es un servicio externo con caída y con latencia: meterlo en el camino del
alta convierte un formulario en una integración, y la primera indisponibilidad ajena impide dar de
alta un cliente. Y el dato que de verdad decide el IVA es el **territorio fiscal**, que el §7.2 ya
hace campo propio y que VIES no aporta.

**Las dos coletillas:**

- **Tercero sin identificador fiscal: sí, con el rol restringido.** Puede ser cliente de contado; no
  puede ser proveedor ni entrar en factura hasta que lo tenga. La regla vive en el dominio, no en el
  formulario.
- **NIF de un tercero bloqueado: son DOS caminos, no uno, y el matiz es del usuario.** Resolverlo con
  el ítem 1.4 no basta: quien da un alta normalmente **no** tendrá el permiso de ver lo bloqueado, y
  un mensaje que diga «está bloqueado» **publica la existencia de un dato del art. 32 a alguien sin
  derecho a saberlo**. Así que:
  - **el alta** devuelve un conflicto que **no revela** —«no se puede dar de alta con ese
    identificador; consulta con administración»—, sin confirmar ni desmentir que exista una fila;
  - **el administrador**, con su permiso y por el camino del 1.4, sí la encuentra y puede levantarla.

  Va escrito así en el **ADR-0027**, porque es la clase de detalle que se decide una vez y se copia
  mal diez veces.

#### P5 · Lo que el §7.3 no fija de las tarifas — cinco decisiones

1. **Precedencia: artículo > categoría más cercana > … > raíz.** No basta con «gana la más
   específica»: como la categoría es **jerárquica con padre** (ver *Lo que decide el agente*), un
   artículo puede casar con una línea de su categoría **y** con otra de la categoría padre, y las
   dos son «por categoría». Sin la precedencia completa, el desempate lo decide **el orden en que
   salgan las filas**, que es un no-determinismo silencioso. El caso que hay que probar es
   exactamente ese: **dos líneas de categoría a distinta profundidad**.
2. **Precio o descuento, excluyentes en el propio objeto de valor.** Los dos juntos son dos maneras
   de decir lo mismo con un orden de aplicación invisible.
3. **Solape de vigencias prohibido con restricción de exclusión**, igual que los tramos de impuesto
   del 0.15. La máquina ya sabe hacerlo; dejarlo a la aplicación es dejarlo a una carrera.
4. **Sin tarifa aplicable: error de negocio con nombre, nunca precio cero.** Mismo criterio que la
   decisión 3 del ADR-0023.
5. **`Tercero.TarifaAsignada` entra en la fase 1**, dentro del ítem de cruces (1.10). Sin él la
   tarifa por cliente no existe y el criterio del §15 se queda a medias.

#### P6 · Hasta dónde llega Terceros en esta fase — la raya entre maestro y movimiento

El §7.2 cuelga seis cosas del tercero. Tres son de fase 1 sin discusión (`Direccion`, `Contacto`,
régimen fiscal). Las otras tres tienen dueño más adelante:

| | Fase 1 | Su fase |
|---|---|---|
| `CuentaBancaria` (IBAN, BIC) | dato maestro del tercero | — |
| `MandatoSEPA` | — | **6 · Tesorería**, con las remesas |
| `CondicionPago` | plazos y tope legal de 60 días | — |
| `LimiteCredito` | el **importe** | el **riesgo vivo** y el bloqueo por impago son **4 · Ventas** |

**El criterio:** en la fase 1 entra lo que es **maestro**; lo que necesita **movimientos** para
significar algo entra con el módulo que los genera. Meter `MandatoSEPA` ahora es escribir una
máquina de estados —incluida la baja automática a los 36 meses— que **nada ejercita**: es la
vacuidad del ADR-0020 en forma de tabla.

#### P7 · Cuándo entra la retirada del ADR-0023 — ítem propio, antes de Catálogo

**Decidido:** ítem 1.7, propio, **antes** del primer ítem de Catálogo, con sus dos hermanos —la
tolerancia de la conversión inversa y el resolutor de conversiones encadenadas con su error con
nombre—.

El disparador escrito en el ADR-0023 es «con el primer módulo que los referencie, no antes», y ese
momento es Catálogo usando `UnidadMedida`. Metido **dentro** del ítem de Catálogo, el criterio de
ese ítem dejaría de ser verificable de un vistazo: son cambios de contrato en cuatro recursos de
Organización más una regla de aplicación. Y dejarlo **para el final** significa que Catálogo nace
pudiendo apuntar a una unidad que ya nadie debería ofrecer.

#### P8 · El NIF no viaja en la cadena de consulta

`herramientas/api-rest.md` es explícito —«un dato sensible no viaja en la cadena de consulta»— y
`proteccion-datos.md` entra con esta fase. Hoy los listados solo llevan `?page=&size=` y `PaginaDe`
no devuelve enlaces, así que la decisión se toma limpia y no hay que deshacer nada.

**Decidido:** la búsqueda va por **`POST .../buscar` con el criterio en el cuerpo**, la paginación
en el cuerpo también, y la respuesta devuelve un **cursor opaco** en lugar de una URL que
reintroduzca el criterio. **La comprobación no termina en la entrada:** el enlace a la página
siguiente que fabrica el servidor tampoco puede llevar el criterio dentro.

**Descartado** mitigar en el proxy (no registrar la cadena de consulta): confía la protección a un
componente que no vive en este repositorio, y el historial del navegador y la analítica del cliente
siguen ahí. **Descartado** partir el listado en dos formas según el filtro: la asimetría que nadie
recuerda seis meses después.

**El efecto secundario, y su exención.** Un `POST` que no crea nada choca con la lectura ingenua de
REST; queda documentado en el **ADR-0025**. Y tiene una consecuencia concreta sobre lo construido:
`TodaEscrituraDiceComoSeProtegeTests` reparte las acciones **por el verbo**, así que
`POST .../buscar` cae del lado de las que cambian estado y el barrido pedirá `If-Match` o
`Idempotency-Key` para una búsqueda. Necesita **su exención con motivo escrito** en `s_exentas`, que
ya se compara entera en los dos sentidos. Es una línea, y se escribe **con el endpoint**, no cuando
el carril se ponga rojo: la tentación entonces sería ensanchar la partición, que es peor.

#### P9 · La importación CSV — aislamiento por fila, idempotencia por fichero, y el hash

**Unidad de aislamiento: la fila.** Es literalmente lo que pide `principios/manejo-errores.md`: en
un bucle que procesa muchos elementos, el fallo de uno no puede ser el fallo de la vuelta. Un CSV de
tres mil filas de un cliente real no puede convertirse en un «no» sin diagnóstico.

**Idempotencia (R10): una importación es UNA operación.** Con `Idempotency-Key`, el reenvío del
mismo fichero devuelve **el resultado guardado** sin reimportar. **Descartado** hacerla N
operaciones —una por fila con clave derivada—: multiplicaría por tres mil las filas de
`auditoria.claves_de_idempotencia`, que **ya crece sin política de retención y es riesgo abierto**.

**El segundo cerrojo, que es lo que hace segura a la decisión anterior:** se guarda el **hash del
contenido** junto al resultado. Misma clave con **fichero distinto** es un **error explícito**, no
el informe equivocado devuelto con cara de acierto.

**En el criterio del ítem:** con aislamiento por fila, el informe dice **qué filas fallaron, por qué
y en qué línea** — es lo único que permite corregir y reimportar solo esas. Y esa reimportación es
**fichero distinto → hash distinto → clave nueva**: coherente, y dicho en voz alta para que nadie lo
lea como una fuga de la idempotencia.

#### P10 · El camino de lectura de lo bloqueado — ítem propio, con dos correcciones de hecho

**Sí lo necesita el desglose, y no por comodidad.** Sin él, el alta de un tercero cuyo NIF está
bloqueado choca contra un índice único sobre una fila **invisible**, y el mensaje sería «duplicado»
señalando a nada (ver P4).

**Dos correcciones al análisis previo, comprobadas en el código:**

1. **El motivo ya existe.** `MotivoParaVerLoBloqueado.AdministracionDelBloqueo` está construido en
   `src/BuildingBlocks/Application/Bloqueos/IAccesoALoBloqueado.cs` y en uso. **No hay que crearlo.**
2. **Son cuatro sitios, no tres.** `s_aperturasDeBloqueoPermitidas` lista `DesbloquearAlmacen`,
   `DesbloquearEmpresa`, `AdministracionDeUsuarios` y `BloquearUbicacion`. Terceros sería el
   **quinto**. Importa porque esa lista **se compara entera y en los dos sentidos**: escribir «tres»
   ahí es escribir un rojo.

Así que el ítem **no construye el motivo**: construye el **camino de listado** —una consulta que
enseñe lo bloqueado a un rol nominativo y trazado—, con su permiso y su ADR.

**Y lo que el `proteccion-datos.md` añade y hoy no existe:** el art. 32 exige que el bloqueo tenga
**fecha de vencimiento y proceso de destrucción**. No entra en el ítem —es materia de retención, y
la fase 1 no tiene con qué decidir el plazo— pero **entra como riesgo anotado con su fecha**, no en
silencio.

#### P11 · El presupuesto del frontal — dos topes, y el criterio que ya estaba escrito

**Lo medido hoy:** `dist` son **554 kB** de un tope de 600. Pero el desglose enseña que el `dist`
está **partido en fragmentos y las rutas se cargan tarde**: `index` 380 kB, `schemas` (zod) 84,
`PaginaDeAcceso` 36, css 12, `Paginacion` 12. El paso de la CI hace
`du -sk --exclude='*.map' dist`, o sea que **suma también los fragmentos que el navegador no
descarga al arrancar**. Su propio comentario dice que mide «lo que el navegador descarga», y con
carga diferida **eso dejó de ser cierto**.

**La consecuencia es al revés de como parece:** la fase 1 va a añadir pantallas **diferidas**, que
engordan el número sin empeorar el arranque, y el presupuesto saltaría por algo que **no es el
problema que existe para vigilar**.

**Decidido: dos topes, con el cálculo escrito.**

- **Arranque ≤ 450 kB** — el fragmento de entrada más sus importaciones estáticas y el CSS. Hoy son
  ≈400 kB; en una 4G de 1,6 Mbps efectivos, ~2 s de descarga, dentro del segundo y medio a dos y
  medio que se considera aceptable para el primer render útil. Margen corto sobre lo medido, que es
  la regla que ya dejó escrita el 0.10.
- **Total ≤ 900 kB** — holgado a propósito: vigila el crecimiento global sin castigar el troceo.

**Y lo importante, que es de dónde sale el criterio y no el número:** el propio paso de la CI ya
excluye los `.map` con este razonamiento escrito —«no se descarga al arrancar, así que contarlo
medía otra cosa distinta de la que dice esta frase»—. Ese argumento vale **palabra por palabra**
para los fragmentos diferidos. Esto **no cambia el criterio: lo aplica donde dejó de aplicarse
solo.** Así queda en el **ADR-0028**.

#### P12 · Los tipos de paginación se consolidan — y la premisa de la pregunta era falsa

La pregunta se planteó como «¿seguimos copiando o tocamos el §4?». **Comprobado en el código, no hay
§4 que tocar.**

**Uno.** `Contracts` **ya** referencia el núcleo común, y solo en un módulo:

```
Bastion.Organizacion.Contracts.csproj  ->  Bastion.BuildingBlocks.Domain.csproj
Bastion.Identidad.Contracts.csproj     ->  (ninguna)
Bastion.Auditoria.Contracts.csproj     ->  (ninguna)
```

O sea que hoy **no hay regla**: hay una **incoherencia entre dos módulos**, con la puerta abierta y
uno solo entrando por ella.

**Dos.** La regla de capas no dice lo que la pregunta suponía. `LasCapasVanHaciaDentroTests`
prohíbe que `Contracts` arrastre **el interior de su propio módulo**, y el motivo está escrito ahí
mismo: «si el contrato arrastrase el `Domain`, cualquier módulo que lo referenciara —que es lo que la
regla 1 le PERMITE hacer— acabaría viendo el dominio ajeno por transitividad». **El bloque común no
es interior de nadie**: todos pueden verlo, así que la transitividad que justifica la regla **no
existe en este caso**.

**Tres.** Dos de los cuatro tipos ni siquiera están en `Contracts`: `ConsultaPaginada` vive en
`Endpoints` y `Paginador` en `Infrastructure`, y las dos capas **ya** referencian
`BuildingBlocks.Infrastructure`. Para esos dos no hay ni discusión.

**Cuatro.** Son duplicados de verdad, no parientes: `diff` entre los dos `Paginacion.cs` devuelve
**una sola línea**, la del `namespace`; los dos `PaginaDe.cs`, lo mismo.

**Con eso, seguir copiando deja de ser la opción barata:** sería institucionalizar **dieciséis
ficheros idénticos** —cuatro tipos por cuatro módulos— más un barrido que los vigile para siempre,
contra un principio que la biblioteca declara sin matices. Un barrido que compara copias no es una
defensa: es la cuota anual de una deuda que no hacía falta contraer.

**Decidido, y cómo:**

- **`Bastion.BuildingBlocks.Contracts`**, nuevo y mínimo, con `Paginacion` y `PaginaDe`. Es su sitio
  semántico: son tipos **de contrato**, no de dominio.
- **`ConsultaPaginada` y `Paginador`** se van a los comunes que sus capas ya referencian.
- **Identidad y Organización pasan a usarlos y sus copias se borran.** Que digan lo mismo **por
  construcción**, no por vigilancia.

**Aviso previsto:** `Inventario.ComunesConTipos` declara tres ensamblados comunes con un comentario
que dice que «son tres y **no van a crecer con las fases**». El cuarto lo pondrá **rojo**, y hay que
actualizar la línea. Eso es el mecanismo funcionando como se diseñó — y la ocasión de corregir el
comentario, que resultó ser **una predicción y no una regla**.

#### Lo que decide el agente, por trivial o reversible

- **Categoría jerárquica con padre y comprobación de ciclos**, no ruta materializada: la ruta
  optimiza consultas que todavía no existen. Es la que obliga a completar la precedencia de P5.
- **Idioma del tercero** restringido a los del diccionario del frontal.
- **Orden por omisión** de cada listado, declarado en el caso de uso y no heredado del motor.
- **Nombres de permisos** siguiendo `modulo.recurso.accion`, como el catálogo ya existente.
- **Dónde vive cada barrido nuevo**, siguiendo el carril que ya le corresponde por lo que lee.

#### Lo que se encontró al comprobar las decisiones contra el código

Cuatro cosas que no estaban en la tanda y que cambian el trabajo. Las dos primeras son **averías
mudas** que la fase 1 activa; las dos últimas, consecuencias del P12.

1. **El ítem 1.4 caduca dos exenciones del ADR-0017, y ellas mismas lo dicen.** Las exenciones de
   `EmpresasController.Desbloquear`, `AlmacenesController.Desbloquear`,
   `UbicacionesController.Desbloquear` y `UsuariosController.Desbloquear` llevan escrita **la
   condición de la que dependen**: «DEPENDE DE que el filtro `Bloqueo` siga tapando la empresa en
   **TODA lectura que llegue por la API**: el día que un endpoint abra `ViendoLoBloqueado(...)` y
   devuelva una empresa bloqueada **con su ETag**, la llave vuelve a existir, esta exención caduca y
   hay que volver a exigir `If-Match` aquí». El ítem 1.4 hace exactamente eso.

   **La cláusula tiene dos mitades y solo una se rompe si el camino es de listado.** Un **listado**
   de lo bloqueado rompe la primera mitad —ya no es «toda lectura»— pero **no entrega ETag**, que es
   lo que resucita la llave; un **`GET` individual** rompe las dos y obliga a devolver el `If-Match`
   a los cuatro desbloqueos. **Decidido: el 1.4 construye SOLO el listado**, que es además lo único
   que hace falta —del listado sale el identificador, y el desbloqueo no pide etiqueta—. Y **entra en
   el criterio del ítem** reescribir la mitad rota de las cuatro cláusulas: una condición que ha
   dejado de ser cierta y sigue ahí es una exención que parece razonada y no lo es.

2. **`TodaEscrituraDiceComoSeProtegeTests` fija sus ensamblados a mano, y la fase 1 lo deja ciego.**
   Su `Todas()` enumera `typeof(ControladorDeOrganizacion).Assembly` y
   `typeof(ControladorDeIdentidad).Assembly`, tecleados. El día que exista `Terceros.Endpoints`, el
   barrido **dejará de mirarlo sin ponerse rojo y sin cambiar de color** — un controlador nuevo
   entero sin `If-Match`, sin `Idempotency-Key` y sin exención, y el informe diciendo que se
   comprobó. Es **la misma avería** que se cerró en el 0.13 para `UnidadDeTrabajoPorModuloTests`, y
   es hoy la última lista de ensamblados tecleada del proyecto.

   **Decidido:** se arregla con la receta ya aceptada aquí —descubrir los ensamblados que arrastra
   `Bastion.Api` filtrando `Bastion.*.Endpoints`, y comparar la lista entera en las dos
   direcciones—, y **va en el 1.3**, o sea **antes** de que exista el primer controlador nuevo. Se
   arregla cuando cuesta una tarde, no cuando ya escondió algo.

3. **`Inventario.ComunesConTipos` se pondrá rojo con el cuarto común**, como estaba previsto (P12).

4. **`Paginador.cs` de Organización arrastra un resto de edición en su comentario de documentación**
   —una etiqueta suelta dentro de una palabra—, que compila porque es XML válido. No se arregla por
   separado: ese fichero **desaparece** con la consolidación del P12.

#### Lo que faltó en la tanda, y por tanto no se supone

**El octavo tema que el usuario puso sobre la mesa —«quién escribe el texto que lee una persona
cuando falla una validación»— se cayó de la tanda al redactarla.** No se contestó porque no se
preguntó, así que **no se hereda un default**: queda como decisión abierta con fecha de caducidad.

- **Dónde muerde:** en el **1.5**, que es el primer ítem con una validación que apetece enseñar con
  el texto del servidor (un NIF con la letra mal). Los ítems 1.1 a 1.4 no la tocan.
- **Las opciones:** que la API traduzca por `Accept-Language`, o que devuelva un `type` estable y lo
  traduzca el frontal.
- **Lo que ya está escrito** en *Notas / riesgos* desde el 0.14: «la respuesta por defecto es la
  segunda, porque mantiene la API sin saber de presentación; **pero se decide, no se hereda**». Y
  encaja con lo construido —el `type` de `ProblemDetails` **ya es contrato**—, pero eso es un
  argumento, no una respuesta.
- **Se pregunta antes de empezar el 1.5**, y sale de ahí un ADR si la respuesta es de arquitectura.

#### El desglose de la fase 1, definitivo

> Once ítems. Entre paréntesis, el número que tenía en la propuesta inicial, para que no se pierda
> nada al leer el historial de esta decisión.

| # | Ítem | Criterio de aceptación |
|---|---|---|
| **1.1** | El presupuesto del frontal, remedido *(era 1.11)* | Dos métricas —arranque frente a total— con topes **450 / 900 kB** razonados con el cálculo escrito; la CI mide **lo que dice medir**, comprobado con la cifra de antes y la de después |
| **1.2** | Los puertos de lectura de Organización, y la cuarta vía en rojo *(1.1)* | `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e `IConsultaDeDivisas` en `Organizacion.Contracts`; el cruce declarado en el mismo commit que su primer consumidor; la **regla de dos fuentes** de P2 vista en rojo con su mutación (`DivisaId` sin puerto) |
| **1.3** | El contrato de listado, y los tipos de paginación consolidados *(1.2 + P12)* | Filtro y orden en servidor con tope; búsqueda por cuerpo, **sin dato sensible en la URL ni en el enlace a la página siguiente**, con su exención escrita en `s_exentas`; `BuildingBlocks.Contracts` con `Paginacion` y `PaginaDe`, `ConsultaPaginada` y `Paginador` en sus comunes, las ocho copias borradas; y `TodaEscrituraDiceComoSeProtegeTests` **descubriendo** sus ensamblados |
| **1.4** | El camino de lectura de lo bloqueado *(1.3)* | **Listado** —no `GET` individual— de lo bloqueado, con su permiso y su ADR; `s_aperturasDeBloqueoPermitidas` comparada entera con **cinco** sitios; y la mitad caducada de las cuatro cláusulas «DEPENDE DE» del ADR-0017 reescrita |
| **1.5** | Terceros: el agregado y su identidad *(1.4)* | Alta con **NIF/NIE/CIF validado de verdad**, extranjero opaco con país y estado de verificación; dirección estructurada (R17); roles; unicidad por (empresa, NIF); el alta contra un bloqueado devuelve un conflicto **que no revela**; listado paginado y filtrado; `features/terceros/`. Y el **ADR-0030**, movido aquí desde el 1.6 en el ítem 1.3: el artefacto de `type` y el barrido, **antes** que el primer texto que los use |
| **1.6** | Terceros: lo que cuelga *(1.5)* | `Contacto`, `CuentaBancaria` con IBAN validado, `CondicionPago` con el tope de 60 días **contado desde la entrega**, `LimiteCredito` (solo el importe) — la raya de P6 |
| **1.7** | La retirada y las dos conversiones *(1.6)* | ADR-0023 implementado entero: retirada en los cuatro maestros de instalación, tolerancia de la conversión inversa, resolutor de encadenadas con **error con nombre**. Antes de que Catálogo los referencie |
| **1.8** | Catálogo: artículo y categoría *(1.7)* | Alta de artículo con unidad e impuesto **validados por los puertos del 1.2**; categoría **jerárquica con ciclos comprobados**; listado paginado y filtrado; `features/catalogo/` |
| **1.9** | Tarifas *(1.8)* | Vigencia y divisa; línea por artículo o categoría; escalado por cantidad; **la precedencia completa de P5 probada con dos líneas de categoría a distinta profundidad**; solape prohibido por restricción de exclusión; sin tarifa aplicable, error con nombre |
| **1.10** | Los dos cruces mutuos *(1.9)* | `ArticuloProveedor` y `Tercero.TarifaAsignada`, cada uno por el `Contracts` del dueño y **declarado** |
| **1.11** | Importación CSV *(1.10)* | Aislamiento **por fila**; idempotencia **por fichero** con `Idempotency-Key` **y hash del contenido** —misma clave con fichero distinto, error explícito—; informe con **línea y motivo** de cada fila rechazada |

**Los ADR que salen de esta puerta**, numerados del 0024 en adelante: la cuarta vía y su regla de dos
fuentes (**0024**), la búsqueda sin dato sensible en la URL (**0025**), la idempotencia de la
importación con el hash (**0026**), la lectura de lo bloqueado y los dos caminos del art. 32
(**0027**), el presupuesto de arranque frente a total (**0028**) y la consolidación de los tipos de
paginación (**0029**) — este último deja escrito **por qué la regla de `Contracts` no era la que
parecía**, porque es la corrección de una creencia y no solo una decisión.


### Tomadas por el agente de desarrollo — ítem 1.2 (2026-09-03)

**1. Los tres puertos contestan un `EstadoDeMaestro`, no un `bool`.** El ADR-0023 dice que una fila
retirada «no se ofrece para operaciones nuevas, pero **sigue resolviendo** para lo que ya apunta a
ella». Eso son **dos preguntas**, y un puerto que solo conteste una deja un agujero en cualquiera de
los dos sentidos: contestando solo «existe» se dan de alta artículos con unidades retiradas;
contestando solo «se puede usar» el histórico se queda sin poder resolver la unidad de un albarán de
hace tres años. `EstadoDeMaestro` tiene `NoExiste = 0`, `SeOfreceParaLoNuevo` y
`SoloResuelveLoViejo`. **Se fija ahora aunque la retirada sea del 1.7**, porque el consumidor es
Catálogo en el **1.8**: descubrir el estado que falta entonces sería cambiar el contrato con una
pantalla a medias. El cero es `NoExiste` a propósito — un `default(EstadoDeMaestro)` que llegue por
un camino que no pasó por el puerto no autoriza nada.

**2. El de impuestos lleva la fecha de devengo como parámetro; los otros dos no.** Un impuesto no
«existe» o «no existe»: rige **entre dos fechas**. El IVA general al 18 % dejó de regir el 31 de
agosto de 2012 y sigue teniendo que resolver una rectificativa de 2011. Preguntar «¿vale este
impuesto?» sin decir para qué fecha no tiene respuesta, así que la fecha va en la firma. Divisa y
unidad no tienen tramos: hoy contestan dos de los tres valores, y el tercero llega con el 1.7.

**3. Los tres maestros no son bloqueables, y está comprobado, no supuesto.** `Impuesto`, `Divisa` y
`UnidadMedida` son `: EntidadBase` y nada más — ni `IBloqueable`, ni filtro de empresa. Importaba
comprobarlo antes de escribir los puertos: si alguno lo fuera, el puerto heredaría el 404 de la R16
sobre lo bloqueado y el consumidor **no podría distinguir «no existe» de «no puedes verlo»**. Lo
prueba `Los_tres_puertos_contestan_sin_empresa_activa_y_sin_ver_lo_bloqueado`, que los ejerce con el
inquilino y el acceso que **lanzan en cuanto alguien les pregunta**: que contesten sin excepción es
la prueba, y el día que alguien les ponga un filtro el test revienta diciendo cuál de los dos ha
sido.

**4. La regla de dos fuentes NO es una igualdad, y aplicarla como tal saldría roja el primer día.**
El descubrimiento por reflexión **infradetecta por diseño**: encuentra un identificador ajeno cuando
el nombre de la propiedad casa con el del módulo dueño, y hay uno que existe desde el 0.5 y no casa
— `TokenDeRefresco.EmpresaActivaId`, que apunta a Organización. (No es un agujero: `ConstructorDeSesion`
y `RenovarSesion` lo comprueban contra el selector que sale de `IConsultaDeEmpresas`. Es la **prueba
viva** de por qué hace falta la lista declarada.) Así que la regla son **cinco afirmaciones** y
ninguna es una igualdad:

| # | Afirmación | Qué agujero tapa |
|---|---|---|
| 1 | Todo lo **descubierto** está en la lista, con su puerto | El identificador ajeno con nombre que casa y nadie declaró |
| 2 | Toda **declaración** sigue correspondiendo a una propiedad del dominio | La entrada que sobrevive al borrado de su propiedad y engorda un inventario muerto |
| 3 | Las **dos fuentes** encuentran algo | La vacuidad: un patrón de descubrimiento roto, o una lista vaciada |
| 4 | Cada módulo de la lista tiene su **cruce declarado y su puerto**, y el puerto existe y es del dueño | La mitad que convierte la regla en protección y no en inventario |
| 5 | **Ningún** `Guid …Id` del dominio se queda sin clasificar | El identificador ajeno con **nombre que no casa** y sin declarar |

**La quinta no estaba en el enunciado y es la que decide si el ADR-0024 protege o solo describe.**
Se comprobó por mutación: un `DivisaPreferidaId` en un agregado de Identidad —cruzado, con nombre que
no casa, sin declarar— lo caza **exactamente un test**, el quinto; los otros veintidós pasan. Con las
cuatro afirmaciones del enunciado esa mutación se habría quedado **verde**.

**5. Con dos entradas en la lista y una en el descubrimiento, la regla se ve trabajar hoy.** No hace
falta esperar a Catálogo. Los sujetos que hay son reales: `Membresia.EmpresaId` (que el
descubrimiento sí encuentra) y `TokenDeRefresco.EmpresaActivaId` (que no), más siete identificadores
del **mismo** módulo que la quinta afirmación obliga a clasificar. Una regla cuyo primer sujeto
llegara en el 1.8 sería una regla que nadie ha visto trabajar.

**6. El texto que lee una persona cuando falla una validación lo escribe el frontal** — la decisión
que quedaba abierta de la puerta de clarificación, contestada: se mapea el **`type` estable** del
`ProblemDetails`, y la API **no** negocia idioma por `Accept-Language`. El motivo, las tres cosas que
obliga a montar y las tres alternativas descartadas están en el **ADR-0030**; los criterios que
genera están en el **ítem 1.6** del checklist.

> **Una nota de orden sobre esto último, resuelta en el ítem 1.3 (2026-09-04).** El criterio se
> escribió en el **1.6** siguiendo la instrucción y **se ha movido al 1.5**, con el mecanismo —(a) el
> artefacto de `type` generado y versionado y (b) el barrido comparándolo contra los diccionarios—
> **antes** que el primer texto que lo use.
>
> El motivo no es que el 1.5 traiga la primera pantalla, que también: es que **el catálogo de `type`
> no está vacío hoy**. `PoliticaDeErrores.BaseDeTipos` y `ErrorDeOperacion.Codigo` ya componen
> `/errors/{codigo}` en los dos módulos montados, así que el artefacto y el barrido tienen sujetos
> desde el primer día y se pueden ver en rojo — que es la condición que el ADR-0020 le pone a
> cualquier regla nueva. Escrito en el 1.6, el 1.5 habría llenado `features/terceros/` de textos a
> mano y el 1.6 habría sido una reescritura, no una implementación; y el barrido habría nacido con
> las excepciones de esos textos ya dentro.
>
> Queda anotado aquí para que dentro de tres ítems nadie lo lea como un descuido: **no es que el 1.6
> perdiera un criterio, es que el 1.5 lo necesita antes.**


### Tomadas por el agente de desarrollo — ítem 1.3 (2026-09-04)

**1. Paginar y buscar son DOS parejas de tipos, no una.** `Paginacion`/`PaginaDe<T>` para el listado
ordinario; `Recorrido`/`TramoDe<T>` para la búsqueda. La alternativa era un solo `PaginaDe` que
llevara número, total y cursor — y entonces `Total = 0` en cada búsqueda y `CursorSiguiente = null`
en cada listado, o sea **la mitad de los campos vacíos en cada respuesta**. Un cliente no puede
distinguir «vacío porque esta forma no lo usa» de «vacío porque no hay», así que ese tipo miente
siempre y obliga a documentar fuera del contrato cuál de sus mitades vale. Dos tipos con nombres
que dicen lo que son cuestan un fichero más y no mienten ninguna vez. `TramoDe<T>` **no lleva
total**, y tampoco por pereza: contar un conjunto filtrado cuesta un recorrido entero en cada tramo,
que es justo lo que un cursor viene a evitar; el listado sí lo lleva porque su total es el de la
tabla y sale barato (§9, «con el total cuando es barato calcularlo»).

**2. La lista de campos por los que se deja ordenar tiene UN dueño, y no hay regla que la compare
con nada.** Se descartó lo que parecía natural —los nombres declarados en `Contracts` y el mapa a
columnas en `Infrastructure`—: son dos listas, divergen, y entonces hace falta una tercera cosa que
las compare. Lo que hay es un solo diccionario, `CriteriosDe<T>.Ordenables`, en el repositorio, y
**sus claves SON la lista blanca**. Sube al borde por el tipo: `IOrdenaPor` → `IListado<TDto>` →
`ConsultaPaginada.APaginacion(camposOrdenables)`, que devuelve `400` con los campos admitidos
nombrados. Un `?sort=` que el borde no rechace y llegue al paginador **lanza**, porque a esas
alturas es un error de programación y no una entrada (ADR-0004).

**3. Las claves de orden son `LambdaExpression` y no `Expression<Func<T, object>>`.** Con `object`,
el compilador inserta un `Convert` en el árbol, y ese `Convert` no revienta al compilar: revienta en
ejecución y **solo** sobre las propiedades con conversor de valor —`Usuario.Correo`, `Empresa.Nif`—,
o sea que la pantalla que lo estrena da un 500 y las demás no. El precio es un `(Expression<Func<T,
K>>)` explícito por entrada del diccionario, y está escrito al lado.

**4. El universo de `Todas()` se descubre; no se escribe.** Sale de
`IActionDescriptorCollectionProvider` —la tabla de enrutado del host, no una reconstrucción suya por
reflexión—, así que un módulo entra en el barrido **en el momento en que se monta**, que es el mismo
momento en que sus acciones empiezan a atender peticiones. Y lleva una segunda fuente que no se
deriva de la primera: los ensamblados `Bastion.<Modulo>.Endpoints` **del disco** con controladores
dentro. Se comparan **enteras**, y la igualdad es legítima porque las dos describen el mismo
conjunto: un módulo con controladores es un módulo que se monta. Añadir un tercer `typeof` habría
arreglado hoy lo mismo que se rompería con Catálogo.

**5. El barrido del criterio sensible mira el explorador de API, y reconoce los listados por lo que
DEVUELVEN.** El explorador es la misma fuente de la que sale `docs/api/openapi.json` y por tanto el
cliente del frontal; una reconstrucción por reflexión que mirase `[FromQuery]` **se saltaría** los
parámetros que en un `GET` se enlazan desde la URL por convenio, que es exactamente la fuga que la
regla existe para ver. Y los listados se reconocen por su tipo de respuesta —`PaginaDe<>` o
`TramoDe<>`— y no por que el método se llame `Listar`: la heurística por nombre es la que el ítem 1.2
tumbó, y se le escapa el primero que se llame `Consultar` **en silencio**. La comparación con los
doce campos sensibles es **por contención y sin distinguir mayúsculas**, para que `nifDelCliente`
cuente igual que `nif`; y la lista de los cuatro parámetros que hoy aceptan los listados
—`page`, `q`, `size`, `sort`— se compara **entera**, de modo que uno nuevo obliga a decidirlo donde
están escritos los motivos.

**6. `POST /api/v1/organizacion/empresas/buscar` nace con su exención escrita**, no cuando el carril
se pone rojo. La partición de `TodaEscrituraDiceComoSeProtegeTests` reparte **por el verbo**, así que
una búsqueda por `POST` cae del lado de las escrituras y el barrido le pide `If-Match` o
`Idempotency-Key`. Lo que **no** se hace es ensanchar la partición: es correcta y no tiene falsos
negativos, y aflojarla para acomodar un caso cambia una lista de excepciones razonadas por una regla
que ya no distingue (ADR-0025). La línea está en `s_exentas` con su cláusula «DEPENDE DE»: el día que
una búsqueda apunte algo —un registro de lo buscado, una lista de recientes—, la exención caduca.

**7. El cursor lleva posición y nada más, y el recorrido va por el identificador.** El `Id` es un
GUID v7: sus primeros bits son el instante de creación, así que ascendente por identificador **es**
por antigüedad de alta —un orden con sentido, no un capricho— y además es único, con lo que el
«después de esto» no necesita desempate ni comparación de tuplas y cae sobre la clave primaria. Se
leen `tamaño + 1` filas para distinguir «no hay más» de «hay más», sin contar el conjunto entero.
«Opaco» aquí significa que el cliente no lo construye ni lo interpreta —lo que permite cambiar la
clave del recorrido sin romper a nadie—, no que esté cifrado: dentro va la posición del último
elemento que el cliente ya tiene delante. **El criterio no entra en el cursor**: lo reenvía el
cliente en el cuerpo del `POST` siguiente. Si entrara, el NIF volvería al cliente en una cadena que
se copia y se comparte, que es la fuga del ADR-0025 entrando por la puerta de al lado.

**8. Que `Contracts` vea el bloque común no es tocar el §4.** La regla de capas prohíbe que un
`Contracts` arrastre el **interior de su propio módulo**; el núcleo común lo ven ya todas las capas
de todos los módulos. Está argumentado en el **ADR-0029** y se comprueba en
`LasFronterasEntreModulosTests.Las_referencias_de_proyecto_son_las_declaradas`, donde las dos aristas
nuevas —`Identidad.Contracts -> BuildingBlocks.Contracts` y `Organizacion.Contracts ->
BuildingBlocks.Contracts`— están declaradas.

**9. El comentario de `Inventario.ComunesConTipos` era una predicción, no una regla.** Decía que los
comunes «no van a crecer con las fases»; la fase 1 los ha hecho crecer en su tercer ítem. Se ha
reescrito para que diga lo que de verdad afirma —qué comunes hay y que todos llevan tipos— y el
nombre del test perdió el número: `El_bloque_comun_tiene_sus_tres_capas…` pasó a
`El_bloque_comun_tiene_las_capas_declaradas…`, porque **un número dentro de un nombre es una
afirmación que nadie comprueba**.

**10. El carril de integración se ejerce en local en la parte que no necesita el carril.**
`LaTraduccionASqlTests` vive en `Organizacion.IntegrationTests` **sin** el rasgo `Integracion`, y es
la única excepción declarada de ese proyecto —su cabecera lo dice—: generar el SQL necesita el
proveedor de Npgsql y el modelo, no un servidor, y ese proyecto es el único que ve los repositorios.
Descubre los diez listados del módulo por el **tipo** de su campo de criterios —no por el nombre del
campo— y traduce el orden de omisión, cada campo ordenable en los dos sentidos y el filtro. Lleva su
pregunta de control —la consulta que entra en el `.Valor` de un valor convertido tiene que
romperse— porque si `ToQueryString` dejara de traducir, el silencio no diría nada. La búsqueda se
sondea aparte y por el efecto: se ejecuta contra un sitio donde no hay nadie y se afirma **de qué**
se queja, mirando si en la cadena de excepciones hay una `DbException` —el tipo, no el mensaje, que
habría dependido del idioma del ejecutor—, con su contraria en el mismo test.

**11. Ninguna dependencia nueva.** El fichero de bloqueo suma **un** proyecto —
`Bastion.BuildingBlocks.Contracts`, `"type": "Project"`— y **cero** entradas `Direct` o
`Transitive`. No hay licencias que comprobar porque no hay paquete que comprobar.

**12. Lo que este ítem NO toca**, dicho para que no se lea como un olvido: ni Terceros, ni Catálogo,
ni tarifas, ni la retirada del ADR-0023. El camino de lectura de lo bloqueado es el **1.4**. El
artefacto de `type` del ADR-0030 es el **1.5** (ver la nota de orden del ítem 1.2, resuelta arriba).

#### Lo que este ítem deja abierto y no arregla

`LasReglasDeEsteCarrilTests` es el **censo de Arquitectura.Tests y solo de ese ensamblado**: se
descubre con `typeof(LasReglasDeEsteCarrilTests).Assembly`. Sus reglas siguen siendo **23** y lo
único que cambió allí fue la línea del test renombrado. Las afirmaciones nuevas de este ítem —tres en
`Api.FunctionalTests` y tres en `Organizacion.IntegrationTests`— viven en carriles **sin censo**, así
que borrar una de ellas dejaría la suite verde, más rápida y con una regla menos que nadie echa de
menos. Es la misma rendija que `LasReglasDeEsteCarrilTests` tapó para su carril en el 0.12, abierta
en otros dos. **No se cierra aquí** porque no está en el criterio del 1.3 y el checklist no se amplía
por iniciativa propia; queda anotado como candidato, con su motivo, para quien decida el alcance del
siguiente ítem.

#### Las cinco mutaciones del 1.3, cada una aplicada, ejecutada y revertida

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | Un controlador de un módulo montado **fuera** del universo que la lista escrita a mano nombraba (`POST /api/v1/terceros/fichas`, sin protección) | **Rojo ×4** | `Toda_accion_que_cambia_estado_dice_como_se_protege`, `El_universo_cubre_a_todos_los_modulos_montados`, `El_barrido_encuentra_el_inventario_entero` y `Toda_accion_o_exige_un_permiso_o_esta_en_la_lista_de_excepciones` |
| 2 | Un `[FromQuery(Name = "nif")]` en el listado de empresas | **Rojo ×2** | `Ningun_listado_recibe_un_criterio_sensible_por_la_url` (con el motivo del campo impreso) y `El_barrido_ve_los_listados_y_sus_parametros` |
| 3 | El descubrimiento del barrido nuevo apuntando a un tipo que ningún listado devuelve | **Rojo ×1** | Solo `El_barrido_ve_los_listados_y_sus_parametros`. La regla de al lado salió **verde recorriendo una lista vacía**, que es exactamente lo que la afirmación de conjunto no vacío existe para cazar (ADR-0020) |
| 4 | Divergir una copia consolidada: `TamanioMaximo = 5000` en un módulo | **Imposible ×3** — no rojo | El compilador, tres veces. Ver abajo |
| 5 | Quitar la exención de `EmpresasController.Buscar` de `s_exentas` | **Rojo ×2** | `Toda_accion_que_cambia_estado_dice_como_se_protege` (nombrando la acción) y `El_barrido_encuentra_el_inventario_entero` (15 esperadas, 14 encontradas) |

#### La primera, entera

Es la que decide si el arreglo es estructural o aplazado, así que se hizo con su contraste. Se añadió
un controlador **montado y enrutado** que vive fuera de `Bastion.Organizacion.Endpoints` y de
`Bastion.Identidad.Endpoints` —los dos ensamblados que el universo escrito a mano nombraba—, con una
escritura sin `If-Match` ni `Idempotency-Key`:

```csharp
[ApiController]
[Route("api/v1/terceros/fichas")]
public sealed class MutacionUnoControladorDeTerceros : ControllerBase
{
    [HttpPost]
    public IActionResult Crear() => Ok();
}
```

Con el universo descubierto, **cuatro rojos**, y cada uno dice algo distinto:

```
sinDecirlo    should be empty but had 1 item and was ["MutacionUnoControladorDeTerceros.Crear"]
sinDeclarar   should be empty but had 1 item and was ["MutacionUnoControladorDeTerceros.Crear"]
enrutados     should be ["identidad", "organizacion"]
              but was    ["identidad", "organizacion", "terceros"]
todas.Count   should be 74 but was 75
```

Después, **con la mutación puesta**, se restauró el universo escrito a mano —los dos `typeof`, sobre
la misma tabla de enrutado— y se volvió a ejecutar solo ese fichero:

```
Correctas! - Con error: 0, Superado: 7, Omitido: 0, Total: 7
```

**Siete verdes con un `POST` sin proteger atendiendo peticiones.** Eso es lo que quedaba en pie: no
que faltara un `typeof`, sino que el barrido no tenía forma de enterarse de que faltaba. Las dos
mitades —el controlador y el universo viejo— se revirtieron y el fichero volvió a 121 de 121.

#### La cuarta, entera — y no sale roja porque no llega a existir

El enunciado pedía comprobar que divergir una copia consolidada **es imposible, no rojo**. Se
intentaron las tres formas que hay, de la más ingenua a la única que de verdad importaría.

**Forma literal: cambiar `TamanioMaximo` en un módulo.** No hay dónde. En todo el repositorio la
constante se **declara una vez**:

```
src/BuildingBlocks/Contracts/Paginacion/Paginacion.cs:31:    public const int TamanioMaximo = 200;
```

y sus dos usos —`ConsultaPaginada` y `BuscarEmpresasDto`— resuelven a esa. La edición no tiene
objetivo.

**Forma 1: recrear la copia en `Bastion.Organizacion.Contracts.Comun` con 5000.** El compilador la
rechaza, y no por una regla que alguien escribió:

```
error CS0104: 'Paginacion' es una referencia ambigua entre
  'Bastion.Organizacion.Contracts.Comun.Paginacion' y
  'Bastion.BuildingBlocks.Contracts.Paginacion.Paginacion'
```

**Forma 2: divergir el borde.** Una `ConsultaPaginadaDeOrganizacion` con tope 5000, enlazada en
`EmpresasController.Listar`:

```
error CS1503: Argumento 1: no se puede convertir de
  'Bastion.Organizacion.Endpoints.Comun.ConsultaPaginadaDeOrganizacion' a
  'Bastion.BuildingBlocks.Infrastructure.Listados.ConsultaPaginada'
```

**Forma 3: saltarse el ayudante común.** Una `Paginacion` propia en un espacio de nombres que no
importa nadie —para que exista sin ambigüedad— pasada **directamente** al caso de uso, sin pasar por
`ResponderListadoAsync`:

```
error CS1503: Argumento 1: no se puede convertir de
  'Bastion.Organizacion.Endpoints.Paginado.Paginacion' a
  'Bastion.BuildingBlocks.Contracts.Paginacion.Paginacion'
```

Las tres son **errores de compilación**, no pruebas en rojo. Y el motivo es el mismo en las tres, y
es lo que distingue consolidar de copiar a un sitio nuevo: el tipo que atraviesa el camino de
listado está fijado por `IListado<TDto>.EjecutarAsync(Paginacion, …)` y por
`ConsultaPaginada.APaginacion(...)`, así que **una copia divergente no tiene por dónde entrar**.
Puede existir como fichero muerto en un rincón que nadie importe; lo que no puede es llegar a un
listado. Que es la diferencia que se pedía comprobar.


### Tomadas por el agente de desarrollo — ítem 1.4 (2026-09-04)

**1. El listado reutiliza el contrato del 1.3 y no estrena un segundo idioma de listado.**
`IListarLoBloqueado : IListado<BloqueadoDto>`, `CriteriosDe<RecursoBloqueado>` con sus ordenables
(`tipo`, `codigo`, `nombre`, `fecha`, por omisión `fecha` descendente y desempate por `Id`) y el
mismo `?page`, `?size`, `?sort`, `?q` que los otros doce. No es pereza: los cuatro parámetros de
consulta que la API acepta están **declarados** en `NingunCriterioSensibleViajaEnLaUrlTests`, y un
listado con su propia forma habría entrado ahí como excepción el primer día.

**2. El motivo nuevo, contra lo que decía el ADR-0027.** Ese ADR dejó escrito que el listado abriría
el ámbito «con el motivo que ya existe». Se decidió lo contrario y el ADR lleva su **corrección
fechada**: `AdministracionDelBloqueo` es el sistema operando sobre un bloqueo y esto es una persona
mirando datos personales reservados, y bajo la misma etiqueta la traza del art. 32 no las distingue
—que es justo para lo que sirve—. Era además la deuda que el propio enumerado tenía escrita en su
comentario: «un segundo valor cuando exista un camino de lectura». Consecuencia:
`MotivoParaVerLoBloqueado` pasa a lista cerrada de dos **con regla propia**, comparada entera y en
los dos sentidos contra las citas del código de producción.

**3. El vencimiento cuelga del MOTIVO, no de un número global.** Los dos motivos son de naturalezas
distintas: `SupresionSolicitada` reserva datos personales y **vence** —pasado el plazo procede
destruirlos—, y `CeseDeUso` **no vence nunca**, porque un almacén retirado se conserva por razón
contable y sus datos no son de nadie. Poner caducidad a los dos por igual pondría a destruir un dato
mercantil que hay que guardar. **Seis años por omisión** con procedencia: el art. 30 del Código de
Comercio, que es el suelo más largo de los que le aplican a una pyme española (cuatro la prescripción
tributaria del art. 66 LGT, cinco las acciones personales del art. 1964 CC); se elige el más largo
porque un bloqueo que venciera antes que la obligación de conservar destruiría lo que todavía hay que
poder enseñar. **Configurable por instalación** (`BASTION_PLAZO_DE_SUPRESION_ANIOS`), a diferencia de
`OpcionesDeLaBandeja`, que dice por escrito que no tiene variable «porque nadie ha necesitado
cambiarlos sin recompilar»: aquí el caso existe y no es hipotético. Ausente vale y significa el de
omisión; **puesta y mal** no vale y **para el arranque**, porque eso es alguien intentando configurar
algo y consiguiendo otra cosa. Y el `switch` sobre el motivo es exhaustivo y lanza: un tercer motivo
tiene que **decidir** si vence, no heredar el «no vence» en silencio.

**4. La traza dice QUIÉN, y eso cambió una línea que ya existía.** Hasta este ítem la anotación del
ámbito llevaba solo el motivo, y bastaba mientras el único camino era desbloquear: la escritura
siguiente dejaba su propia fila de auditoría con el usuario dentro. **Una consulta no escribe nada**,
así que si no lo dice esa línea no lo dice nadie, y el art. 32 no reserva el acceso «a la
administración» sino a personas concretas. El usuario va **anulable y sin lanzar**: el ámbito también
se abre fuera de una petición —arranque, trabajos de fondo— y ahí no hay usuario; un `Guid.Empty` de
relleno pasaría por un usuario de verdad en cuanto alguien leyera el registro.

**5. El rastro de la lectura NO es una fila de auditoría, y eso ya estaba resuelto.** El criterio
pedía que la traza no dependiera de la integridad referencial de lo que audita ni impidiera destruir
lo observado. Se comprobó en vez de reinventarlo: `RegistroDeAuditoria` no tiene **ninguna** clave
foránea —guarda identificadores sueltos— y una lectura no dispara el interceptor, que solo mira
entidades modificadas. O sea que la traza de esta consulta es la línea del `ILogger`, que no ata nada
y no impide destruir la fila el día que su bloqueo venza.

**6. El censo, extendido a los cuatro carriles que tienen reglas —con el coste medido.** El usuario
lo autorizó explícitamente para este ítem. Medido antes de decidir, no estimado: `Api.FunctionalTests`
112 `[Fact]` + 2 `[Theory]`, `Api.IntegrationTests` 124 + 9, `Organizacion.IntegrationTests` 30 + 10
—**287 casos** sin censo— frente a `Arquitectura.Tests` 23 + 0, que sí lo tenía desde el 0.12. El
coste real es **una lista escrita por ensamblado** y un fichero compartido, porque el mecanismo ya
existía: no había que inventar nada. Las tres UnitTests (`BuildingBlocks` 56+16, `Identidad` 43+5,
`Organizacion` 92+28) se quedan **fuera a propósito**: son tests de dominio, no reglas de frontera, y
borrar uno se nota en el comportamiento; borrar una regla de frontera no se nota en nada. Es una raya
movible, no un olvido.

**7. Dos trampas de EF Core que aparecieron en rojo y valen para el próximo listado.**
(a) `Concat`/`Union` **no** se traducen después de una proyección de cliente: las tres ramas
proyectaban a `new RecursoBloqueado(...)` y salieron **diez pruebas en rojo** con «Unable to translate
set operation after client projection has been applied». Se diagnosticó con una sonda temporal que
demostró que las ramas con **tipo anónimo** sí se unen (`UNION ALL`), y la construcción del tipo
propio se movió **detrás** de la unión. (b) El `ORDER BY` a través de esa proyección solo se traduce
si la proyección es un **inicializador de objeto**; con una llamada a constructor, EF la inlinea en la
clave de orden y no sabe traducirla. Por eso `RecursoBloqueado` es un `record` con propiedades
`init` y no un `record` posicional. Las dos las cazó `LaTraduccionASqlTests`, **sin contenedor**.

**8. Lo que este ítem NO toca**, dicho para que no se lea como un olvido: ni Terceros, ni el
artefacto de `type` del ADR-0030 (es el **1.5**), ni Catálogo, ni la retirada del ADR-0023 (el
**1.7**). Y no hay pantalla: el listado existe en la API y el frontal no lo consume todavía, que es
lo que la nota de riesgo de la ficha individual ya decía.

**9. Ninguna dependencia nueva.** `git diff --name-only b75b312..HEAD -- '*packages.lock.json'`
devuelve **cero** ficheros: los cuatro `.csproj` tocados añaden un `<Compile Include>` y ninguna
`PackageReference`, y `frontend/package*.json` no se mueven. No hay licencias que comprobar porque no
hay paquete que comprobar.

**10. Un error de proceso, escrito porque costó trabajo de verdad.** Al revertir la segunda mutación
se usó `git checkout -- <fichero>` sobre un fichero que tenía **trabajo del ítem sin commitear**, y
eso lo devolvió a HEAD: se perdieron las ~128 líneas que el 1.4 había añadido a
`ElFiltroNoSeSaltaPorAhiTests.cs` —la quinta apertura declarada y las **dos reglas nuevas**—. Se
rehicieron, y el propio censo del §4 sirvió de comprobante de que la reconstrucción era completa: su
lista declarada volvió a casar nombre a nombre. **La regla que deja:** antes de una tanda de
mutaciones, o se commitea lo que hay, o se respalda **todo** lo modificado y no solo lo no rastreado;
`git checkout --` no distingue entre «deshaz la mutación» y «deshaz el ítem».

**11. Un método de extensión es un paquete, y ahora hay quien lo comprueba.** El requisito 2 del
enunciado —registrar **quién** pregunta— le dio a `AccesoALoBloqueado` la dependencia `IUsuarioActual`,
que registraba **otro** método de extensión. La API llamaba a los dos y no notó nada; el publicador de
la bandeja, que corre fuera de una petición, llamaba solo a `AgregarInquilinato()` y reventó en la CI
con diez casos de Integración caídos. Se arregla registrando `IUsuarioActual` ahí también, con
`TryAddScoped` para que llamar a las dos extensiones no ponga dos descriptores del mismo servicio y el
orden del `Program.cs` no elija implementación en silencio. **Lo que deja escrito** es
`ElInquilinatoSeConstruyeSoloTests`, en el carril rápido: construye **todo** lo que la extensión
registra —descubriéndolo, no tecleándolo— en un ámbito con `validateScopes: true`. La lección, que no
es sobre Docker: un fallo de composición no necesita base de datos, necesita que alguien intente
construir lo registrado. El relato completo, con el mensaje de la CI y el mismo mensaje reproducido en
local en milisegundos, en *Verificado en local → La sexta, que no puse yo*.


### Tomadas por el agente de desarrollo — ítem 1.5 (2026-09-05)

**1. La unicidad de (empresa, identificador) abarca TAMBIÉN lo bloqueado, y la decisión no la toma
el índice.** El enunciado del ítem cruza dos cosas —unicidad por (empresa, NIF) y bloqueo— y avisa de
que lo que no vale es que decida el índice por omisión. Son dos opciones de verdad y las dos tienen
coste: un índice **parcial** (`WHERE NOT bloqueado`) libera el identificador al bloquear la ficha, y
entonces desbloquear puede encontrarse **dos filas con la misma llave** y alguien tiene que deshacer
a mano un empate con datos personales por medio; un índice **incondicional** deja el identificador
ocupado durante todo el plazo del art. 32, y quien quiera volver a operar con ese tercero tiene que
desbloquear el que hay en vez de dar de alta otro. Se elige el **incondicional**, porque un
desbloqueo que puede fallar por una colisión creada mientras tanto no es reversible, y el art. 32
exige poder devolver la ficha a su sitio. La contrapartida está escrita donde se paga
(`ErroresDeTercero`, `IRepositorioDeTerceros`) y comprobada **por el efecto**, no por la
configuración: `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca` da de
alta, bloquea, vuelve a intentar el alta —409— y luego desbloquea y comprueba que la ficha vuelve
entera con su identificador. Y `La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado`
se lo pregunta a `pg_index` con `indpred IS NULL`, que es la mitad que dice que **no hay predicado
parcial**: sin esa condición el test pasaría igual con el índice contrario.

**2. El índice está escrito a mano en la migración, y eso obliga a que lo vigile otro.** EF Core 10
**no sabe indexar miembros de un tipo complejo**: se intentaron las dos formas y las dos fallan —con
el selector, «no es una expresión de acceso a miembro válida»; con los nombres, «la propiedad
"Identificacion.Pais" no se puede añadir al tipo "Tercero"»—. La alternativa era degradar la
identificación a **tipo poseído**, que sí admite índices, y se descartó: un poseído tiene identidad
sintetizada y EF lo sigue como una entidad más (ADR-0016), o sea que el mapeo mentiría sobre el
modelo para conseguir un índice. Se escribe con `CreateIndex` en la migración. Lo que eso rompe hay
que decirlo: como el modelo **no conoce** el índice, `has-pending-model-changes` no puede echarlo en
falta, así que borrarlo de la migración no pondría roja ninguna comprobación de migraciones. Quien lo
nota es el test de esquema, que lo busca **en la base** — que además es donde de verdad importa.

**3. La indistinguibilidad del conflicto se sostiene en CUATRO decisiones de estructura, no en una
redacción.** El criterio pide que el alta contra un bloqueado devuelva un conflicto que no revela, y
lo que se ha buscado es que la redacción no pueda romperlo:

- (a) el puerto devuelve un **booleano** —`ExisteLaIdentificacionAsync`—, así que no es que el caso
  de uso decida no mirar si el que estorba estaba bloqueado: **no lo tiene**. Un enumerado de tres
  valores habría dejado la propiedad en manos de que nadie escribiera un `if`, y la primera vez que
  alguien quisiera «un mensaje más útil» lo escribiría.
- (b) `ErroresDeTercero.IdentificacionDuplicada()` **no recibe parámetros y no lleva el número
  dentro** —al revés que el almacén duplicado, que sí lo lleva—, porque con el número las dos
  respuestas dejarían de ser comparables enteras y la comparación tendría que normalizar el cuerpo. Un
  test que decide qué trozos no cuenta es un test que decide qué se puede filtrar.
- (c) el **tiempo** es el tercer canal, y los dos desenlaces hacen literalmente la misma consulta —un
  índice, una fila—: no hay un camino que además cargue el bloqueo ni uno que se ahorre la lectura.
  Iguales **por construcción**, no por haberlos medido.
- (d) cuál de los dos era **sí** queda escrito, pero en el registro y desde la implementación, que es
  quien lo sabe: suceso **8400** con `{Bloqueada}`, la empresa y el instante, y **sin el identificador
  fiscal dentro** — un NIF es un dato personal y el registro se agrega, se exporta y se conserva con
  menos ceremonia que la base de datos.

El ámbito de bloqueo se abre **solo alrededor de la pregunta** y no hasta el `ConfirmarAsync`: si
llegara hasta la grabación, se grabaría con el filtro de R16 apagado, que es mucho más de lo que aquí
hace falta.

**4. La fuga que este ítem NO cierra, dicha porque existe.** Quien tenga a la vez
`terceros.tercero.ver` y `terceros.tercero.crear` puede **correlacionar dos respuestas**: la búsqueda
por identificador (`POST .../buscar`) no encuentra la ficha bloqueada —una consulta ordinaria no ve lo
bloqueado— y el alta con ese mismo identificador contesta 409. «No aparece y sin embargo está
ocupado» solo puede significar una cosa. Cerrarlo pide algo que no es de este ítem —separar los dos
permisos, o hacer que la búsqueda mienta igual que el alta— y el checklist no se amplía por iniciativa
propia. Queda anotado con su nombre: lo que este ítem garantiza es que **una sola respuesta** no lo
diga, y eso es lo que estaba en el criterio.

**5. Un tercero bloqueado no aparece en NINGÚN listado, y esto lo estrena el 1.5.** El listado del
art. 32 que construyó el 1.4 recorre `TipoDeRecursoBloqueado`, que tiene exactamente tres valores
—`Empresa`, `Almacen`, `Ubicacion`—. Un tercero bloqueado desaparece del camino ordinario, como debe,
y **no asoma por el camino reservado**, que es donde debería. O sea que hoy la única forma de saber
que existe es intentar darlo de alta otra vez, que es justo el canal que el punto 3 cierra. No se
arregla aquí porque el enumerado, el permiso y el DTO de ese listado son del módulo Organización y
ampliarlos es cambiar el criterio del 1.4, ya cerrado; se anota como **deuda con nombre** para el ítem
que toque la retirada (1.7) o el que abra ese listado a más módulos.

**6. Los datos de prueba del identificador fiscal se GENERAN, y por eso la batería prueba el
algoritmo en las dos direcciones.** Un NIF real es un dato personal, y en una fixture queda en el
repositorio, en el artefacto de resultados y en el registro de la CI, para siempre y sin plazo. No hay
ni uno: `IdentificadoresInventados` calcula el carácter de control de números inventados
—`"TRWAGMYFPDXBNJZSQVHLCKE"[n % 23]` para el NIF, y el doblado de las posiciones pares para el CIF— y
`LaBateriaGeneradaTests` los recorre **en los dos sentidos**: el generado con su control se acepta, y
el mismo número con **cualquiera de los otros 22** controles se rechaza. Eso segundo es lo que
convierte la batería en una prueba del algoritmo y no en una lista de ejemplos: un validador que
aceptara cualquier letra pasaría la mitad de arriba entera. En el frontal, lo mismo: los
identificadores de `datos.ts` son `00000001R`, `00000002W` y `00000003A`, derivados con el mismo
algoritmo y documentados como tales, más uno extranjero opaco.

**7. El artefacto de `type` fue primero, y el orden se cobró.** Es la condición que el 1.3 dejó
escrita —«el mecanismo antes que el primer texto que lo use»— y se cumplió literalmente: los dos
primeros commits del ítem son el generador (`scripts/generar-errores.sh`), `docs/api/errores.json`, su
paso en la CI y el barrido de diccionarios comparándolo; las pantallas de Terceros vinieron después.
La consecuencia práctica es que `tercero-duplicado`, `tercero-no-encontrado` y los demás **nacieron
con texto en los dos idiomas el día que se escribieron**, sin una sola excepción que justificar en el
barrido. Al revés, el ítem habría escrito los textos a mano y el barrido habría nacido con la lista de
lo que no cumple.

**8. `Terceros.UnitTests` nace SIN censo, siguiendo la raya que puso el 1.4.** El censo cubre los
cuatro carriles con **reglas** —afirmaciones sobre un universo descubierto, cuyo borrado no se nota en
nada—; los tres proyectos `*.UnitTests` se quedaron fuera a propósito porque son tests de dominio y
borrar uno sí se nota en el comportamiento. El cuarto proyecto unitario se crea con el mismo criterio,
y no por omisión. Es una raya movible; moverla no es de este ítem.

**9. El frontal entrega el listado del criterio y NO la búsqueda por cuerpo.** El criterio pide
«listado paginado y filtrado» y `features/terceros/`, y eso está: `/terceros` con `?pagina=`,
`?tamanio=` y `?busqueda=` en la URL, el sello de verificación pintado **siempre** —también cuando
está comprobado— y el papel, que dice «cliente y proveedor» cuando son las dos cosas porque es **una**
ficha. El recuadro de filtro **no busca por identificador fiscal**, y es el ADR-0025 aplicado donde se
puede desobedecer: lo que se teclea ahí acaba en el historial del navegador, en el enlace que se copia,
en la cabecera `Referer` y en el registro de acceso del servidor de delante. Buscar por identificador
existe en la API, va por el cuerpo con `POST .../buscar`, y **el frontal todavía no lo usa**: es
alcance que el criterio no pide y que se deja anotado en vez de colarlo. El servidor simulado de los
tests **no filtra por identificador a propósito**, para que ningún test pueda pasar contra un servidor
que sí lo hiciera.

**10. El régimen fiscal del §7.2 no está en el criterio de ningún ítem, y queda anotado.** El modelo
de dominio del plan maestro se lo pone a `Tercero`; el criterio escrito del **1.5** no lo nombra, y el
del **1.6** tampoco —ese enumera `Contacto`, `CuentaBancaria`, `CondicionPago` y `LimiteCredito`—. No
se añade por iniciativa propia (el checklist no se amplía) y no se da por olvidado: si es del 1.6, hay
que decirlo allí; si es de facturación, hay que decir de qué fase. Es una pregunta, no una tarea.

**11. Los tests nuevos montan la PANTALLA, no la aplicación, y la cifra está medida.** Montar el
armazón (`montarAplicacion`) cuesta exactamente **cuatro** avisos de «not wrapped in act» por montaje
—`SelectorDeEmpresa`, `Guarda`, `Disposicion` y `RouterProvider`—, con carga perezosa de la ruta y sin
ella, y saltan **después** de que el ámbito de `act` se cierre, así que envolver no los quita: se
probaron las dos formas, y una de ellas —sondear dentro de un `act` asíncrono— **cuelga**, porque
React no repinta dentro de un ámbito `act` asíncrono. La salida es `montarPantalla`, que monta la
pantalla bajo prueba con su enrutador de memoria, su caché y su i18n: **cero avisos**. Los nueve casos
nuevos del listado y los del texto de fallo entran con cero, y la cifra global se queda en **109 en
siete ficheros**, la misma antes y después. Cerrar esos 109 no es de este ítem; no aumentarlos, sí.

**12. Una desviación entre la batería de `AGENTS.md` y la CI, encontrada al diseñar la mutación 4.**
La CI comprueba desde este ítem que `docs/api/errores.json` no se ha quedado atrás (paso «Catálogo de
errores»), y la batería de aceptación de `AGENTS.md` **no lo nombraba**: quien la pasara entera en
local podía empujar un `type` nuevo sin regenerar el artefacto y descubrirlo en el run. Es la misma
familia que la rendija del recuento que anotó el 1.3 —«el paso de CI no es solo su comando»— y se
arregla igual: la línea en la tabla y en el bloque de la batería. Salió de preguntarse **quién** iba a
poner roja la mutación 4, no de leer el fichero por encima.

**13. Ninguna dependencia nueva, y la frase sale del diff.** Los conjuntos de paquetes resueltos de
todos los `packages.lock.json` a los dos lados: **145 en `main`, 150 en la rama**, y las cinco entradas
de diferencia son los cinco proyectos nuevos del propio módulo (`bastion.terceros.domain`,
`.contracts`, `.application`, `.infrastructure`, `.endpoints`). **Cero paquetes de terceros añadidos,
cero retirados**, `Directory.Packages.props` sin tocar, y `frontend/package.json` y `package-lock.json`
sin mover. No hay licencias que comprobar porque no hay paquete que comprobar.

**14. Los captadores del registro son sumideros de Serilog, y ahora tienen quien los vigile.**
Lo destapó el run del propio ítem, el
[34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496), con **un caso rojo de
271**: la mitad de §1 que dice que *la traza sí registra cuál de los dos era* esperaba dos sucesos
anotados y leyó **cero**. No era el producto. `src/Api/Program.cs` hace `ClearProviders()` y luego
`AddSerilog(...)`, que instala `SerilogLoggerFactory`, y esa fábrica **ignora todo `ILoggerProvider`
ajeno**: `RegistroDeSucesos` y `RegistroDeFallos` eran proveedores, así que nunca recibieron nada.
Cero significaba «no hay cable» y se leía como «la API no anota» — las dos lecturas son
indistinguibles desde el test, que es lo que lo hace peligroso.

Se pasan los dos a `ILogEventSink`. **La decisión de verdad no es esa, es dónde vive el enganche:**
`tests/Comun/CapturaDeRegistro.cs`, compartido por los dos carriles y **no copiado**. Lo que se rompió
fue el enganche, así que un canario con su propia copia habría comprobado su copia; el de ahora
—`ElCaptadorDeRegistroCapturaTests`, en `Api.FunctionalTests`— ejecuta **la misma línea** que ejecuta
`ApiDeVerdad`. Y va al **carril rápido**, no al de integración, porque un cable roto no necesita
PostgreSQL para notarse: donde estaba, avisó en la CI y no en la máquina de quien lo rompió.

**Lo que además se descubrió por el camino y vale la pena no olvidar:** `RegistroDeFallos` llevaba sin
funcionar desde que se escribió, y su motivo era precisamente que los registros de un *job* devuelven
403 sin autenticar. En el [run 33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347)
hay un `500` de verdad y detrás ni una línea de `· registro del servidor:`. Un arnés de pruebas es
código sin nadie detrás: la primera pregunta ante una regla nueva no es «¿el dominio la cumple?» sino
**«¿la regla mira lo que dice mirar?»**. El desarrollo entero está en *Verificado en local* →
*La avería que la CI destapó*, con las dos mutaciones que ponen rojo el canario.


### Tomadas por el agente de desarrollo — ítem 1.6 (2026-09-07)

**1. La correlación buscar/alta no se cierra en este ítem, y deja de ser una nota abierta: es una
decisión con su razonamiento escrito.** El hecho está comprobado y no ha cambiado: `POST
.../terceros/buscar` no encuentra una ficha bloqueada —una consulta ordinaria no ve lo bloqueado— y
`POST .../terceros` contesta `409` si su identificador está ocupado, esté la ficha activa o bloqueada.
Cada respuesta por separado no revela nada; **las dos juntas, sí**: quien tenga a la vez
`terceros.tercero.ver` y `terceros.tercero.crear` aprende que existe una ficha bloqueada con ese
identificador, y «no aparece y sin embargo está ocupado» solo significa una cosa. Las tres salidas
posibles, con lo que cuesta cada una:

  - **Separar los permisos** para que nadie tenga los dos. Es una decisión de **política de
    permisos**, no de código: en una pyme de cinco personas, quien da de alta un cliente es la misma
    persona que lo busca antes para no duplicarlo. Cierra el canal y rompe el trabajo diario.
  - **Que la búsqueda mienta igual que el alta** —que devuelva la ficha bloqueada— rompe R16 y el
    art. 32 a la vez: el bloqueo existe precisamente para que no aparezca.
  - **Que el alta deje de distinguir**: contestar `201` con una ficha nueva sobre un identificador
    ocupado. Rompe la unicidad de (empresa, identificador), que es la decisión 1 del 1.5.

  Ninguna es gratis, así que **no la toma el agente**: es de las de preguntar. Se anota aquí con las
  tres opciones y su coste para que la pregunta llegue entera al día que se conteste, y **no vuelve a
  *Notas / riesgos*** porque no es un riesgo que se descubra: es un cruce conocido, medido y
  documentado. Lo que sí queda cerrado del 1.5 es lo que su criterio pedía —una respuesta que no
  revela—, y eso está comparado entero.

**2. El régimen fiscal del §7.2 entra en el 1.6, y el corte que se sostiene son tres condiciones de
cuatro.** Contesta la pregunta que el 1.5 dejó abierta (decisión 10 de aquel ítem). **No es alcance
nuevo:** está en el modelo de dominio del plan maestro y se quedó sin ítem al desglosar la fase —
alcance comprometido sin casa—, y su sitio es el tercero, porque dejarlo para la fase 3 obliga a
migrar un maestro con datos dentro. El corte es el de `LimiteCredito`: lista cerrada de valores con
su `CHECK` y nada más. Entra `TerritorioFiscal` (cinco valores) y entran los tres booleanos —recargo
de equivalencia, criterio de caja, sujeto a retención de IRPF—, y **se sostiene porque los cuatro son
hechos declarados del tercero**, no resultados de un cálculo: que un minorista esté en recargo es
algo que él dice y que su ficha registra, igual que su domicilio. **Lo que no se sostiene y se deja
fuera es el tipo de retención**: ahí sí es cierto que el valor no significa nada sin la regla que lo
aplica, y además ya tiene dueño desde el 1.2 —`Impuesto` con `TipoDeImpuesto.Retencion`—, así que
meterlo aquí obligaría a declarar un cruce entre módulos (ADR-0024). El booleano dice **si** se le
retiene; **cuánto**, quien ya lo sabe.

**3. `LimiteCredito` lleva su divisa; no la hereda de la empresa.** El criterio del ítem dice «solo el
importe» y no cierra esto, así que hacía falta decidirlo o escribir por qué no. Un importe sin divisa
sería **la única cantidad de dinero del sistema que no dice de qué es**. Heredar `Empresa.DivisaBase`
—que es como se llama; **`DivisaPreferidaId` no existe**, era el nombre que traía la nota— tiene un
modo de fallo mudo: cambiar la divisa base de la empresa reinterpretaría todos los límites ya
guardados sin tocar una fila y sin que nada fallara. Así que el límite es un `Importe?`, con
`numeric(18,4)` —nunca coma flotante, nunca el `money` del motor— y un `CHECK` que obliga a que la
cantidad y la divisa estén o falten **juntas**. Lo que la pantalla ofrezca por omisión es de la
pantalla. Consumo, disponible y bloqueo por exceso no son de este ítem: esa es la raya de P6.

**4. La cuenta preferente por tercero es una restricción, y ve las filas bloqueadas: es la misma
decisión del 1.5, sin matices.** Índice único parcial sobre `(tercero_id)` con filtro `es_preferente`,
más el invariante en el agregado. El índice es de la base y no conoce el filtro global, igual que el
de (empresa, identificador). **Y aquí no podría ser de otra manera**, que es lo interesante: el
bloqueo del art. 32 vive en el **padre**, no en la cuenta, así que dos cuentas que compiten por ser
la preferente están siempre en el mismo estado. La pregunta que en el 1.5 tenía dos respuestas
posibles con costes distintos, aquí solo tiene una — y por eso no se rehace el razonamiento: se cita.

**5. `Contacto` no tiene campo de notas, y es una decisión.** Los cuatro campos —nombre, cargo,
correo, teléfono— pasaron las cuatro preguntas (finalidad, plazo, quién lo ve, categoría especial) y
están contestadas una a una en la cabecera de la entidad, con la base escrita: el art. 19 de la
LOPDGDD ampara los datos de contacto de quien presta servicios en una persona jurídica **siempre que
se refieran a esa actividad**. Eso cubre el teléfono del departamento de compras; **no cubre el móvil
personal de nadie**, y como el modelo no puede distinguir un número de otro, lo que hace es no
invitar a guardarlo —un solo teléfono, el del puesto—. El campo de notas se descarta porque un `text`
libre en la ficha de una persona es donde acaban «está de baja», «es el yerno del dueño» y «no lo
llames los viernes»: categorías especiales que **ninguna** de las cuatro preguntas podría contestar.
Cuando haga falta anotar el trato comercial, su sitio es el CRM del §7.11.

**6. La tabla de longitudes del IBAN es la zona SEPA —37 países—, no el registro entero de la ISO.**
Aceptar los más de ochenta del registro haría creer que esta instalación puede domiciliar un adeudo
contra cualquiera de ellos, y es falso: fuera de SEPA hace falta una transferencia internacional, con
su comisión y su plazo. Añadir un país es cambiar la tabla, con su motivo. Los territorios de ultramar
no llevan código propio y por eso no aparecen.

**7. El barrido de datos con forma de real declara lo que se PERMITE, no lo que se prohíbe.** Es la
decisión de diseño de `NingunDatoConFormaDeRealTests` y sale de la propia restricción del encargo: una
lista de valores prohibidos tendría que llevarlos escritos, y **escribir un IBAN real para prohibirlo
es exactamente el daño que se está evitando** —«tampoco para señalarlos»—. Así que cada forma declara
su espacio inventado y todo lo que tenga la forma y esté fuera es rojo. Por lo mismo, los mensajes de
fallo dan fichero, línea y forma **enmascarada**: un registro de CI que imprime lo que ha cazado
contiene justo lo que la regla existe para sacar del repositorio.

**8. Las siete escrituras nuevas exigen `If-Match` y ninguna admite `Idempotency-Key`.** No son altas
de un recurso con vida propia: son modificaciones de un agregado que ya existe, y citan **el testigo
de la ficha**, no uno suyo. De ahí que las entidades hijas no lleven testigo de concurrencia propio —
colgar un contacto mientras otro cambia la razón social es un choque de verdad y se quiere que la
segunda escritura se lleve el `412`—. El censo se mueve **+11 acciones, +7 escrituras, +7 `If-Match`,
cero idempotencia y cero exenciones**, y esa forma del movimiento es la comprobación de que la
decisión se aplicó: si alguna hubiera acabado en el cajón de las exentas, se vería.

**9. Un controlador para lo que cuelga, no cuatro.** `LoQueCuelgaDelTerceroController` monta las once
rutas bajo `/terceros/{terceroId:guid}/…`. Son subrecursos de **una** ficha y comparten el
`[Route]`, la versión que exigen y el 404 de la ficha inexistente; partirlo en cuatro habría
duplicado esas tres cosas cuatro veces. La regla que vigila que escribir y modificar no compartan
permiso sigue cumpliéndose porque los permisos son **siete**, uno por escritura, no uno por
controlador.

**10. `ObtenerConLoQueCuelgaAsync` es un método aparte del repositorio, no un `Include` metido en el
de siempre.** Cargar las tres colecciones en toda lectura de un tercero son tres consultas por nada
en el caso común —el listado, la ficha, el límite de crédito—. Y al revés, operar sobre el agregado
**sin** sus hijos es peor que lento: `AgregarCuentaBancaria` decidiría sobre una lista vacía y creería
que no hay ninguna preferente. Los dos caminos existen y cada uno dice cuál usa.

**11. Las tres tablas del censo del 0.9 se han rehecho enteras, y el motivo es una lección.** Llevaban
desde el **0.10** con sus números —46 acciones, 13 · 6 · 13— mientras el test iba subiendo hasta 93 y
30 · 13 · 17. Nada se puso rojo porque **una tabla en un `.md` no es una fuente que ningún test
compare**: es prosa, y la prosa no falla. Es la misma familia que el «antes» mal medido de los
`act()` del 1.5, y la salida es la misma que allí: la cifra que se publica va con lo que la produce.
Aquí, con la lista enumerada al lado del número, para que la próxima copia se pueda contrastar.

**12. Ninguna dependencia nueva, y la frase sale del diff.** Conjuntos de paquetes **resueltos** de
todos los `packages.lock.json` a los dos lados: **150 en `main` y 150 en la rama**, sin una sola
entrada de diferencia en ninguno de los dos sentidos. Los `.csproj` que cambian mueven la referencia
a Npgsql a `BuildingBlocks.Infrastructure` —que ya estaba en el grafo por transitividad—, y
`Directory.Packages.props`, `frontend/package.json` y `package-lock.json` no se tocan. **No hay
licencias que comprobar porque no hay paquete que comprobar.**


### Tomadas por el agente de desarrollo — ítem 1.7 (2026-09-07)

**1. La retirada se ve en la colección con un parámetro de listado, `?retiradas=true`, y no con un
extremo aparte.** El ADR-0023 dice «el `GET` de la colección la excluye **por omisión**», y «por
omisión» implica que hay un modo de verlas: quien tecleó `EURO` en vez de `EUR` y la retiró tiene
que poder encontrarla para arreglarla. Las dos formas posibles eran un parámetro o un extremo
nominativo, y el proyecto ya tiene el segundo montado —el listado del artículo 32 del ADR-0027—, así
que la pregunta es **por qué aquel es un extremo aparte y este no**. La respuesta es entera del
ADR-0027 y no una preferencia: aquel camino existe porque lo que enseña son **datos personales
reservados**, y `proteccion-datos.md` exige para el art. 32 «una vía de acceso separada, nominativa
y trazada»; por eso tiene permiso propio, motivo propio de apertura del ámbito y queda anotado en el
registro. Una divisa retirada no tiene nada de eso: **no hay dato personal, no hay art. 32, no hay
nada que trazar**. Copiar allí la forma del bloqueo sería copiar su coste —cuatro permisos nuevos,
cuatro rutas nuevas, cuatro consultas nuevas— para proteger algo que no necesita protección, y
además diría en el contrato que ver una divisa retirada es un acto reservado, que es falso.

  Consecuencias, todas queridas:

  - **Pasa por el contrato de listado del 1.3**: es un `[FromQuery]` de un modelo que hereda de
    `ConsultaPaginada`, con lo que se valida como los otros cuatro y hereda el tope de `size`.
    Construido a mano en el controlador se saltaría la validación entera, que es lo que el propio
    `ConsultaPaginada` explica que no puede pasar.
  - **No es un criterio sensible**, y eso no se afirma de palabra: `NingunCriterioSensibleViajaEnLaUrl`
    compara la lista **entera** de parámetros de todos los listados contra `{page, q, size, sort}`,
    así que el quinto nombre **pone esa regla roja** y obliga a declararlo ahí, junto a los motivos.
    Es exactamente el disparador que esa regla dice tener: «un parámetro nuevo —sensible o no— pone
    esto rojo y obliga a decidirlo aquí».
  - **Solo lo declaran los cuatro maestros retirables.** El parámetro vive en un modelo de consulta
    aparte (`ConsultaDeMaestro`), no en `ConsultaPaginada`: metido ahí, los doce listados —roles,
    usuarios, terceros— publicarían en el OpenAPI un `?retiradas=` que no miran, que es una mentira
    del contrato y de las que no fallan.
  - **Dos valores y no tres.** `?retiradas=true` incluye las retiradas junto a las demás; omitido o
    `false`, no. No hay un tercer «solo las retiradas»: cada elemento publica su `retirada`, así que
    ese caso lo compone el cliente filtrando, y un tercer valor sería una segunda manera de decir lo
    mismo con su propia rama que probar.

**2. Una fila retirada SIGUE restringiendo a su inversa.** Es la pregunta que abre la decisión 2 del
ADR-0023 en cuanto se cruza con la 1, y la respuesta no es obvia, así que va escrita y no puesta por
omisión. Si `A→B` está retirada y alguien da de alta o modifica `B→A`, la tolerancia
`|f·g − 1| ≤ 5·10⁻⁷·(f + g)` **se comprueba igual**, contra el factor de la fila retirada.

  El motivo es literal del ADR: una fila retirada «**sigue resolviendo** para lo que ya apunta a
  ella». Sigue resolviendo significa que **sigue siendo verdad**, y dos filas que se contradicen
  siguen contradiciéndose aunque una de ellas ya no se ofrezca: el albarán de hace tres años que
  resuelve por `A→B` y el que se emita mañana por `B→A` darían cantidades que no cuadran, que es
  exactamente el descuadre sin autor que la decisión 2 existe para impedir. La alternativa —que
  retirar una fila la saque de la comprobación— convierte la retirada en **una manera de saltarse la
  regla**: bastaría retirar el sentido incómodo para poder declarar cualquier número en el otro. Una
  salida que la propia regla ofrece no es una salida: es un agujero con permiso.

  Lo que **sí** cambia con la retirada es la otra mitad, y por el mismo motivo: la fila retirada **no
  se ofrece**, así que no se puede dar de alta una conversión nueva sobre una unidad retirada. Las
  dos frases son la misma —«no se ofrece para lo nuevo, sigue resolviendo lo viejo»— leídas en los
  dos sentidos.

**3. La retirada es un booleano, sin fecha y sin motivo.** Es la tercera decisión, la de forma, y
tiene su porqué. El bloqueo lleva fecha y motivo porque el art. 32 los exige —de la fecha cuelga el
plazo de prescripción y del motivo cuelga si vence—, y nada de eso aplica aquí. Una fecha, además,
tendría un coste que no se ve: invitaría a un `EstadoDeAsync(id, fecha)` en los puertos y convertiría
la retirada en **una línea temporal**, que es justo lo que el ADR-0023 dice que la retirada **no
es** —«no es un cierre; una unidad de medida no se sucede: deja de usarse»—. Quién la retiró y cuándo
ya está guardado: `ModificadoEn` lo pone el interceptor de marcas y la traza de auditoría guarda el
cambio de columna con su autor. Un motivo enumerado sería una lista cerrada más, sin ningún consumidor
que ramifique sobre ella, que es la vacuidad del ADR-0020 en forma de columna.

**4. La retirada se deshace, con `DELETE .../{id}/retirada`.** No lo dice el ADR, y hay que decidirlo:
el motivo por el que la retirada existe es que «el alta equivocada es permanente **y visible desde
todas las empresas de la instalación**», y una retirada irreversible sería un error nuevo con
exactamente el mismo radio. La forma no se inventa: es la del cierre de un ejercicio,
`POST`/`DELETE` sobre el mismo subrecurso, que ya está en el módulo. Son dos permisos por maestro
—`retirar` y `reincorporar`—, como `bloquear`/`desbloquear` y `cerrar`/`reabrir`, porque deshacer una
retirada no es la misma facultad que hacerla.

  Y no es un `DELETE` del recurso: **ninguno de los cuatro tiene `DELETE` sobre su elemento, nunca**,
  que es lo que el ADR prohíbe. La distinción es de ruta y así se comprueba: lo prohibido es
  `DELETE /{recurso}/{id}`; `DELETE /{recurso}/{id}/retirada` es el verbo que levanta un subrecurso,
  igual que `DELETE /ejercicios/{id}/cierre` convive con el `DELETE` del ejercicio sin ser el mismo.

**5. `TipoCambio` y `ConversionUM` no tienen puerto de consumo, y su retirada no es vacía.** Hay que
escribirlo porque es justo la pregunta que el ADR-0020 obliga a hacerse —«¿esto lo mira alguien?»— y
la respuesta no se ve de un vistazo. `Divisa` y `UnidadMedida` tienen `IConsultaDeDivisas` e
`IConsultaDeUnidadesDeMedida`, así que su retirada es observable desde **otro módulo**: Catálogo
pregunta y recibe `SoloResuelveLoViejo`. Los otros dos no tienen puerto: nadie fuera de Organización
pregunta hoy por una cotización ni por una conversión. **Eso no las hace vacías.** Su retirada es
observable por **sus propios `GET`**, que es un camino real y ejercitado: la colección deja de
traerlas, el elemento las sigue devolviendo, y el resolutor de conversiones —que es de este mismo
ítem— deja de ofrecer una conversión retirada para una operación nueva. La diferencia con el caso
vacío del ADR-0020 es exactamente esa: allí la columna existía y **ningún camino la leía**; aquí hay
tres caminos que la leen, y ninguno de ellos necesita que exista un puerto entre módulos.

**6. Dos defectos del factor, encontrados al leer para la decisión 2 y arreglados aquí.** No estaban
en el encargo; salen de que la decisión 2 del ADR se apoya en el rango del factor para afirmar que
«la regla es **total**, no tiene casos en los que se calle».

  - **`FactorValido` redondea DESPUÉS de comprobar el cero.** El orden era: rechazar `factor <= 0`,
    y luego `decimal.Round(factor, 6, AwayFromZero)`. Con eso, `0,0000001` pasa la comprobación
    —es mayor que cero— y se **guarda como cero**, que es literalmente el fallo que el comentario
    de al lado dice impedir: «cero convertiría cualquier existencia en nada, y en silencio». Se
    invierte el orden: se redondea primero y se comprueba el número que se va a guardar.
  - **Y hay que decir hasta dónde llegaba, porque no llegaba a la API.** El `[Range(0.000001,
    1000000)]` de `CrearConversionUmDto` y `ModificarConversionUmDto` rechaza `0,0000001` antes de
    llegar al dominio, así que **por HTTP el defecto no era alcanzable**. Lo era por cualquier otro
    camino que construya la entidad —una semilla, otro módulo, una prueba—, y sobre todo lo era
    como *promesa*: la guarda del dominio decía proteger de algo de lo que no protegía, y quien la
    leyera lo daría por cerrado. Un defecto tapado por una validación de otra capa sigue siendo un
    defecto, y además del tipo peor: el día que alguien llame al dominio desde donde no hay `[Range]`
    aparece sin previo aviso.
  - **El rango `[0,000001, 1000000]` que el ADR cita solo estaba en el borde, y escrito a mano dos
    veces.** El dominio no lo comprobaba: el suelo lo daba de hecho el redondeo a seis decimales una
    vez corregido lo anterior, y el techo no existía en ninguna capa por debajo de la anotación.
    Importa porque el argumento de totalidad de la decisión 2 —«la inversa de cualquier factor
    válido cae también dentro del rango»— se apoya en él: sin techo, un factor de `10¹⁵` tiene una
    inversa que no se puede declarar con seis decimales, y la regla tendría un caso en el que se
    calla. Queda comprobado **en el dominio**, con el rango escrito una sola vez como constantes de
    `ConversionUM` y citado el ADR; y como las dos anotaciones del borde tienen que llevar
    literales de compilación —`[Range]` no admite otra cosa—, hay una regla que compara los cuatro
    literales contra las dos constantes, que es la única manera de que dos fuentes escritas en
    sitios distintos no se separen en silencio.



### Tomadas por el agente de desarrollo — ítem 1.8 (2026-09-10)

**1. El árbol de categorías se modela como LISTA DE ADYACENCIA, y la alternativa está costeada.**
`Categoria` guarda `PadreId` y nada más: una columna que apunta a la misma tabla, con clave ajena a
sí misma y dentro del mismo esquema. La alternativa seria era una *closure table* —una fila por cada
par (antepasado, descendiente) con su distancia—, que es lo que se pone cuando lo que se consulta de
verdad son subárboles.

El criterio para elegir fue **qué va a consultar este módulo**, no qué es más general. El consumidor
real es el **listado de artículos filtrado por categoría**, y ese filtro es `WHERE categoria_id = ?`:
una igualdad sobre una columna indexada, que la lista de adyacencia sirve directamente y para la que
una *closure table* no aporta nada. Lo segundo que se consulta es **el árbol entero de una empresa**
para pintarlo, que en una lista de adyacencia es un `SELECT` plano y se compone en el cliente. La
*closure table* cobra su precio en la escritura: mover una rama reescribe todas las filas de cierre
de su subárbol, y esa escritura hay que mantenerla coherente con la jerarquía en cada alta y cada
modificación. Se pagaría desde el primer día por una consulta que este módulo no hace.

**Y qué pasaría el día que haga falta el subárbol.** Pasaría lo que ya está dicho en pantalla: hoy el
filtro **acota por la categoría dicha y no por lo que cuelga de ella**, y las dos pantallas lo avisan
con esas palabras en vez de dejar que parezca un fallo. El día que el subárbol haga falta de verdad
—el candidato es la **precedencia de tarifas del 1.9**, que asciende de la categoría del artículo
hasta la raíz— hay dos salidas y ninguna obliga a migrar el modelo: un `WITH RECURSIVE` de PostgreSQL
sobre la misma columna, que con un árbol acotado a once niveles recorre como mucho once filas por
consulta; o una *closure table* **derivada**, que se añade al lado sin tocar `PadreId` porque la
jerarquía sigue viviendo ahí. Lo que no se puede hacer barato es al revés: quitar una *closure table*
que ya se está manteniendo. Por eso se empieza por la que no cobra por adelantado.

**2. La cota del recorrido de padres es `Categoria.ProfundidadMaxima`, y vale diez.** Diez niveles
**por debajo** de la raíz, o sea un árbol completo de once categorías desde la raíz hasta la hoja más
honda. El número vive en el dominio, junto a su motivo, y el caso de uso lo lee de allí: repetirlo
sería tener dos fuentes que se separan sin que nada avise.

La cota tiene **dos motivos y hay que decir los dos**. El primero es de negocio: a más de diez
niveles, lo que hay debajo de una categoría casi nunca es una clasificación —es un atributo del
artículo disfrazado de categoría, que es donde se acaban metiendo el color y el tamaño—, y la cota lo
dice con un error con nombre en vez de dejar crecer un árbol que nadie puede recorrer con la vista.
El segundo es que **la cota es lo único que convierte un cuelgue en un error**: el ascenso gasta una
consulta por nivel, y sobre datos que ya tuvieran un ciclo —una restauración a medias, un `UPDATE` a
mano, una importación de otro sistema— un ascenso sin cota no da error, **gira**, dentro de la
petición y con la conexión abierta. Un servidor que no contesta es peor que cualquier rechazo, porque
no dice de qué venía. Con la cota puesta el peor caso son once viajes a la base por alta o
modificación con padre, y el ciclo ya guardado sale como `categoria-demasiado-profunda` con la cadena
escrita.

**Y la cota tiene un caso que la ejerce sin colgar la suite**, que es la otra mitad de la decisión:
`Un_ciclo_YA_GUARDADO_no_deja_el_recorrido_dando_vueltas` monta dos categorías que se apuntan la una
a la otra, comprueba que el recorrido termina **y cuenta cuántos eslabones ha pedido**. El doble de
repositorio lleva un tope del doble —cien— que lanza con una frase que dice qué ha pasado. Sin ese
tope, quitar la cota no daría un caso rojo: daría un caso que no termina, y el síntoma sería «la CI
tarda», que no señala a nadie.

**3. La comprobación de ciclos vive en la capa de APLICACIÓN, y se hace en las DOS puertas.** Dónde
vive, por lo mismo que `LaInversaEsPlausible` del 1.7 —el patrón se reutiliza, no se inventa otro—:
la regla relaciona **varias instancias** del agregado, y la R12 dice una transacción, un agregado. Un
invariante de dominio que tuviera que cargar el resto del árbol sería justo la grieta que la R12
cierra. Tampoco es una restricción de la base: tendría que mirar otras filas, o sea un disparador
recursivo, con el coste y la invisibilidad que eso tiene.

Y en las dos puertas porque **el hueco está en la modificación**. En el alta la rama del ciclo es
estructuralmente inalcanzable —la categoría que nace todavía no está en el árbol, así que ningún
antepasado suyo puede ser ella— y conviene decirlo en voz alta en vez de contar el alta como el sitio
donde se atrapan los ciclos. Lo que el alta sí ejerce son las otras dos ramas: que el padre exista y
que colgar de él no pase de la profundidad máxima. El ciclo lo cierra **reasignar el padre** de una
rama debajo de su propia descendencia, y eso solo se puede hacer modificando. Es la misma forma del
hueco que el 1.7 le encontró a la inversa, por su otra cara.

**4. El caso degenerado —una categoría con ella misma como padre— NO tiene tratamiento aparte, y es
una decisión.** Es un ciclo de longitud uno: sale por la primera vuelta del mismo recorrido, con el
mismo `categoria-ciclo` y el mismo mensaje. Para quien lo provoca es el mismo problema, y una rama
propia solo daría un sitio más donde equivocarse. La mutación 4 lo comprueba por el otro lado:
tratarlo aparte —dejarlo pasar en su propia rama— pone rojos dos casos, uno por cada puerta.

**5. La pregunta del art. 32 se ha hecho, no se ha supuesto: `Articulo` NO es bloqueable.** El
artículo 32 de la LOPDGDD habla del bloqueo de **datos personales**, que es lo que hace bloqueables a
la empresa, al usuario y al tercero. Un artículo guarda código, descripción, tipo y tres
identificadores, y nada más: ni nombre, ni identificador fiscal, ni dirección, ni cuenta. Lo mismo la
categoría. La respuesta está escrita en `Articulo`, en `ArticuloDto` y en `CatalogoDbContext`, y
además **comprobada**: `ElCatalogoNoGuardaDatosDeNadieTests` recorre el modelo y afirma que ninguna
entidad de Catálogo guarda un dato personal, y `ModuloDeCatalogo` **no** registra un aportador al
listado del art. 32 —registrar uno vacío sería peor que no registrarlo: pondría en el informe un
módulo que no tiene nada que aportar—.

**6. Ni la unidad ni el impuesto son claves ajenas, y esa es la §5 entera.** Viven en el esquema de
Organización y aquí se guardan como `uuid` desnudos, validados por los puertos del 1.2
(`IConsultaDeUnidadesDeMedida`, `IConsultaDeImpuestos`). Sin esas dos preguntas las dos columnas
aceptarían cualquier identificador, compilarían, migrarían y pasarían los tests —es la «cuarta vía»
del ADR-0024—, y el fallo aparecería en la factura que los usa, tres fases después. Las mutaciones 6
y 8 son las dos caras de esto: quitar la pregunta, y poner la clave ajena.

**7. Catálogo es el consumidor para el que se construyó la retirada, y las tres casillas de
`EstadoDeMaestro` significan tres cosas distintas en un camino de negocio.** `SeOfreceParaLoNuevo` →
el alta pasa. `SoloResuelveLoViejo` → el alta se rechaza **y el artículo que ya la usaba sigue
resolviéndola**: las dos mitades, que es lo que distingue una retirada de un borrado. `NoExiste` → el
alta se rechaza con un `type` **distinguible** del anterior, porque son dos arreglos distintos: quien
teclea un identificador inventado corrige el identificador, quien apunta a uno retirado elige otro
que sí se ofrezca.

La segunda mitad consiste en algo que **no se hace**: `ModificarArticulo` vuelve a preguntar
únicamente por lo que **ha cambiado**. Revalidar el maestro que nadie ha tocado convertiría la
retirada en un **congelador** —el día que un tramo deja de regir, cada artículo que lo propusiera se
quedaría sin poder corregir ni su propia descripción—. Y la unidad base es el caso extremo: no se
revalida porque **no se puede cambiar**, no está en el DTO ni en `Articulo.Modificar`.

> **Y aquí la tanda de mutaciones destapó una aserción sin escenario.** El caso por HTTP que afirma
> esto —`Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_...`— llegaba a la modificación con
> el tramo del artículo **todavía vigente**, y con eso revalidarlo y no revalidarlo se ven
> exactamente igual desde fuera: la mutación 2 lo dejaba verde. Arreglado en `1eab397`, cerrando el
> tramo del artículo —`POST /impuestos/{id}/cierre` con el último día en ayer— antes de la
> modificación que no lo toca. Es la misma familia que el «antes» mal medido de los `act()` del 1.5:
> una afirmación cuyo escenario no la ejerce sale verde por la peor de las razones.

**8. En `/articulos` no hay desplegable de categorías, y el filtro por rama se pone desde
`/categorias`.** Un desplegable obligaría a traerse el catálogo de categorías entero en cada visita a
la pantalla de artículos, y a quedarse corto —sin decirlo— en la empresa que tuviera más de las que
caben en una página. Lo que sí hay en `/articulos` es el filtro **puesto**, con el nombre de la rama
resuelto por un `GET` por identificador y la salida para quitarlo. Ese `GET` se lanza **solo** si la
sesión concede `catalogo.categoria.ver`: la interfaz esconde y el servidor autoriza, pero pedir lo
que se sabe que va a contestar `403` no es esconder, es hacer ruido. Y si el nombre no llega, el
filtro se anuncia igual sin nombre: callarlo dejaría una tabla con menos filas de las que hay y
ninguna explicación a la vista.

**9. En `/categorias` no hay filtro de texto, y también es una decisión.** Filtrar un árbol por texto
destruye justo lo que esa pantalla enseña: al quitar las filas que no casan, lo que queda son ramas
sueltas con sus niveles descolgados. La pantalla es la forma del árbol; quien busca un artículo lo
busca en `/articulos`.


### Tomadas por el agente de desarrollo — ítem 1.9 (2026-09-11)

**1. La ascendencia se pide con el ÁRBOL PLANO en UNA consulta, y no con `WITH RECURSIVE`.** El
cierre del 1.8 dejó nombrado el día en que haría falta el subárbol —«el candidato es la precedencia
de tarifas del 1.9»— y dos salidas costeadas: un `WITH RECURSIVE` o una *closure table* derivada.
**No se ha elegido ninguna de las dos**, y el motivo no es de gusto: es la **prohibición de SQL
crudo del 0.6**. `WITH RECURSIVE` no tiene forma en LINQ, así que entraría por `FromSqlRaw`, que es
exactamente la puerta por la que el filtro de inquilinato **no pasa** —lo vigila
`ElFiltroNoSeSaltaPorAhiTests`—. Una consulta recursiva escrita a mano sobre `catalogo.categorias`
tendría que repetir `WHERE empresa_id = @empresa` en el término base y confiar en que nadie lo
olvide en el recursivo; y lo que se filtra por olvido en un ascenso es la **rama de otra empresa**,
que sale como un precio ajeno y no como un error.

Lo que se hace en su lugar: **una** consulta trae `(id, padre_id)` de las categorías que el contexto
deja ver —el inquilinato lo pone el filtro global, no una condición que alguien pueda olvidar— y el
ascenso ocurre en memoria, con la **misma cota** `Categoria.ProfundidadMaxima` leída de donde está
escrita. El árbol de una empresa es una clasificación, no un histórico: cabe de sobra. Y una
categoría que no está en el mapa corta el ascenso sin una comparación de empresa escrita aquí.

**Resolver un precio cuesta CUATRO consultas, y no crecen con la profundidad**: la tarifa vigente,
el artículo, la ascendencia y las candidatas —la quinta, `ExisteElCodigoAsync`, solo se gasta por el
camino de fallo, para separar «no hay tarifa» de «no rige hoy»—. La afirmación **no es de color, es
una cota**, y por eso el caso se ejerce **dos veces con profundidades distintas** comparando los
contadores entre sí: un número absoluto escrito a mano se puede actualizar al romperlo; la igualdad
entre profundidad 1 y profundidad 11, no. Sin esa afirmación, la implementación natural —reusar el
ascenso por padres de `ElArbolSigueSiendoUnArbol`, que gasta una consulta por nivel— **pasa todos
los demás casos**: devuelve el precio correcto y ninguna de sus consultas es lenta por separado. Lo
que produce es un N+1 en el camino más caliente del módulo: un documento de cuarenta líneas por
hasta once niveles son cuatrocientas cuarenta consultas para valorar un albarán, y nada en el
registro señala a ninguna.

**2. «Más cercana» y «más profunda» no son lo mismo, y el modelo lo hace imposible de confundir.**
Si el artículo cuelga de una categoría a profundidad 5 y hay líneas en una categoría a profundidad 3
y en otra a profundidad 7, **gana la de 3 y la de 7 no compite siquiera**: no es antepasada suya,
sino algo que cuelga por otro lado del árbol. Una implementación que ordenara por profundidad
descendente pasa el caso fácil de dos líneas en la misma rama y devuelve la equivocada en cuanto hay
una rama hermana más honda.

La defensa no es una comprobación, es el tipo: **`LineaCandidata.Nivel` es el salto desde el
artículo, no la profundidad absoluta de la categoría**. Las candidatas ya vienen de un ascenso, así
que lo que no es antepasado **no llega** hasta la regla, y entre las que llegan el desempate es un
mínimo. La regla no puede elegir una categoría que no sea antepasada porque no la tiene delante.

Y la precedencia vive en la **capa de aplicación** y no dentro del `SELECT` que trae las candidatas,
que es donde habría sido más cómodo: en SQL, la precedencia y el tramo solo se podrían poner rojos
con Docker levantado, y son justo las dos reglas que tienen que poder ponerse rojas en segundos.

**3. La restricción de exclusión la crea una MIGRACIÓN, con su extensión, y se comprueba por el
efecto contra el compose.** Lo que no debe repetirse es un **rango**, no un valor, así que un índice
único no puede expresarlo: `EXCLUDE USING gist (empresa_id WITH =, codigo WITH =, daterange(…) WITH
&&)`. `btree_gist` hace falta porque `=` sobre `uuid` y sobre texto no son GiST de serie, y se crea
con `CREATE EXTENSION IF NOT EXISTS` **en la migración** `20260911064712_TarifasYSusLineas` —no a
mano en la base de desarrollo, que es lo que deja verde el local y revienta el estreno con un
mensaje que no menciona ni la tarifa ni el solape—. El `IF NOT EXISTS` es necesario: Organización ya
la crea en el 0.15 y el orden entre migraciones de módulos distintos no está garantizado, así que
una base que solo tuviera Catálogo tiene que funcionar igual.

**La empresa va DENTRO de la restricción**, y no es decoración: sin `empresa_id WITH =`, la tarifa
`PVP` de una ferretería impediría que la imprenta de al lado abriera la suya, con un rechazo que
habla de una fila que quien lo recibe no puede ni ver (R8).

El rango es `daterange(vigente_desde, vigente_hasta, '[]')`: **cerrado por los dos extremos**, y
sobre `date` y no `timestamptz`, porque la vigencia es una fecha de negocio (R14) y un `tstzrange`
haría que el día de la frontera dependiera del huso de quien pregunta. La inclusividad está escrita
**tres veces y es la misma a propósito**: el `'[]'` de la restricción, el `<=` de `Tarifa.RigeEl` y
el `vigenteHasta < hoy` de `estadoDeVigencia` en el frontal. Con `[)` en un sitio y `[]` en otro, el
día en que un tramo acaba valdría una cosa al resolver un precio y otra al comprobar el solape, y
dos tramos consecutivos que compartieran ese día pasarían la comprobación de la aplicación para
chocar contra la base convertidos en un 500.

**Y se comprueba por el efecto, contra el compose**, no por inspección: el caso lee el `image:` del
servicio `postgres` de `deploy/docker-compose.yml` y lo compara con la imagen del contenedor contra
el que corre la suite —una etiqueta copiada a mano en dos sitios es lo que se queda atrás cuando uno
de los dos sube—; y en esa imagen pregunta a `pg_extension` por `btree_gist` y a `pg_constraint` por
`contype = 'x'`. El contenedor no ha corrido nada más que las migraciones, así que lo que hay en él
lo puso una migración.

**4. Precio o descuento: LOS DOS negativos, con la puerta doble del ADR-0004 y una tercera hoja.**
Que no se puedan poner los dos es evidente en cuanto se escribe el tipo —el orden en que se
aplicarían no lo dice nadie— y por eso casi siempre está. **El que se olvida es el segundo**: que no
se pueda dejar **ninguno**. Parece inofensivo, una fila «sin precio todavía». No lo es: esa fila casa
con el artículo, gana la precedencia y devuelve un importe que nadie escribió, y el descuadre aparece
semanas después sin autor. Es el precio cero **por la puerta de atrás**.

Las tres hojas, y no sobra ninguna: `PrecioODescuento.De` **lanza**, porque llegar ahí con el par mal
formado es un programa mal escrito; `CrearLineaTarifa` **contesta** un error de negocio con nombre,
porque quien manda el par es un cliente HTTP; y el `CHECK ck_lineas_tarifa_precio_o_descuento` lo
impide en filas que no pasaron por el constructor. En el caso de uso las dos mitades se escriben
como **una igualdad** —`peticion.Precio is not null == (peticion.DescuentoPorcentaje is not null)`—
que no se puede dejar a medias por descuido: partirla en dos condiciones es lo que permite borrar una
sin que la otra proteste.

**Lo prohibido no es el cero, es el cero que nadie escribió.** Una muestra comercial se factura a
cero a propósito, con su línea en el documento y su firma detrás, así que `DePrecio(0)` vale.

**5. Sin línea aplicable: error con nombre, y NUNCA un cero.** Es la **decisión 3 del ADR-0023** con
otro sujeto —un rechazo con nombre para el trabajo en el sitio exacto donde falta el dato, en vez de
un valor supuesto que se propaga sin ruido—. Y «no hay tarifa» y «hay tarifa pero su vigencia no
cubre esta fecha» son **dos `type` distintos**, porque se arreglan distinto: la primera escribiendo
bien el código, la segunda abriendo el tramo que falta. Sus códigos HTTP también difieren, y con
motivo: `tarifa-no-encontrada` es **404** —el recurso de la ruta no está—, mientras que
`tarifa-no-vigente` y `tarifa-sin-linea-aplicable` son **400**, porque la tarifa sí está delante de
quien pregunta. Sin la separación, el descuido normal —una tarifa caducada sin sucesora— saldría como
«esa tarifa no existe» delante de alguien que la tiene en pantalla.

**6. LA DIVISA CRUZADA SE ACEPTA** —la primera de las dos decisiones que el criterio del ítem no
cerraba—. De las tres salidas posibles —rechazar una tarifa en divisa distinta de la de la empresa;
aceptarla y marcarla; convertir— se elige **aceptar**. Convertir no es de este ítem, y rechazar sería
prohibir un caso real: una tarifa de exportación en dólares en una empresa que factura en euros.

Es la misma clase de decisión que la del `LimiteCredito` del 1.6 **y la respuesta es la contraria, con
motivo**: allí un límite de crédito **sin divisa** no era legítimo —un importe sin moneda no dice
nada— y por eso se rechaza; aquí la tarifa **sí** dice en qué moneda está, solo que en otra. Lo que
hace que aceptarla no sea peligroso es que **el precio resuelto viaja siempre con su divisa pegada**
(`PrecioResueltoDto.DivisaId`), así que quien lo recibe no puede sumarlo a un total en otra por
descuido.

Y la forma en que se afirma es la única que no se puede fingir: **el caso de uso no tiene por dónde**
saber cuál es la divisa de la empresa —no recibe ese puerto— y a la de la tarifa solo le pregunta el
**estado**. Si algún día alguien quisiera añadir la comparación, tendría que añadir antes la
dependencia. A la divisa se le aplica el ADR-0023 entero: `NoExiste` → `tarifa-divisa-no-encontrada`
(**400**, porque el identificador venía en el CUERPO y no en la ruta, que es el criterio del 1.7 y
del 1.8 y aquí no se reabre) y `SoloResuelveLoViejo` → `tarifa-divisa-retirada` (**409**: retirada
sigue resolviendo lo viejo y no se ofrece para lo nuevo, y abrir hoy una tarifa es exactamente lo
nuevo).

**7. LOS TRAMOS: la frontera cae hacia arriba, y el hueco se cierra por delante** —la segunda
decisión abierta—. Un tramo es `[desde, siguiente)`: rige la línea de `CantidadDesde` más alta que no
pase de la cantidad pedida (`CantidadDesde <= cantidad`, y de las que quedan la mayor). Con tramos en
0 y en 100, **pedir exactamente 100 aplica el de 100**, que es lo que significa una tabla que dice «a
partir de 100». El `<=` es lo que mete el borde en el tramo de arriba; con `<` caería en el de abajo
y nadie lo vería hasta que alguien pidiera justo esa cantidad.

**Y el hueco es peor que el solape.** Un solape da dos respuestas y se nota; un hueco da **ninguna**,
y «ninguna» sale por el mismo sitio que «esta tarifa no cubre este artículo» —`tarifa-sin-linea-
aplicable`—, así que una tabla mal escrita se lee como una tarifa incompleta y se arregla en el sitio
equivocado. Por eso el hueco se cierra **por delante, al escribir**: el primer tramo de cada destino
tiene que empezar en cero (`tarifa-linea-primer-tramo-sin-cero`) y dos líneas del mismo destino no
pueden compartir `CantidadDesde` (`tarifa-linea-tramo-duplicado`). Con esas dos, la cobertura de
`[0, ∞)` es total por construcción y no hay cantidad que se caiga entre dos tramos.

El desempate se hace **por destino y no por línea suelta**, y el orden importa: primero se decide
QUIÉN pone el precio —el artículo, o la categoría antepasada más cercana— y solo después, entre las
líneas de ese destino, cuál tramo. Al revés —coger la mayor `CantidadDesde` de todas las candidatas—
una línea de categoría con un tramo alto le ganaría a la línea del propio artículo, que es justo lo
que la precedencia existe para impedir. Y el ascenso **no se corta en el primer antepasado que tenga
alguna línea**: se recorren los niveles de menor a mayor y gana el primero con un tramo **aplicable**,
porque la precedencia es sobre el par **(destino, tramo)** y no sobre el destino a secas.

> **Corregido el 2026-09-13, en el ítem 1.10.** Aquí decía que el ascenso sigue «porque una categoría
> cercana cuya tabla empiece por encima de la cantidad pedida no pone precio y cortar ahí dejaría sin
> resolver un artículo que la categoría madre sí cubre». **Ese estado no existe**, y lo impide el
> cierre por delante de dos párrafos más arriba, que es de esta misma decisión: el motivo se
> contradecía con la regla a la que servía. Lo que se corrige es **el hecho**, no la decisión —el
> ascenso sigue haciendo exactamente lo mismo y no se mueve ni una prueba—, y por eso se corrige en
> el sitio y no se sustituye (ADR-0033: si alguien pudo escribir código distinto por creerlo es una
> decisión; si lo único que pudo hacer es creerlo, es un hecho). El motivo bueno, la conducta querida
> el día que el cero deje de ser obligatorio y qué la sostiene, en *Decisiones tomadas → ítem 1.10*.

**8. `Tarifa` y `LineaTarifa` viven DENTRO de Catálogo, y por eso sus claves ajenas son legítimas.**
La alternativa era un módulo propio de precios, y se descarta porque lo que una tarifa clasifica son
artículos y categorías: fuera de Catálogo, `articulo_id` y `categoria_id` serían identificadores
desnudos vigilados solo por puertos, como la unidad y el impuesto (§5, regla 4). Dentro, **las claves
foráneas a `articulo` y a `categoria` son del mismo esquema y son legítimas**: la base impide por sí
sola que una línea apunte a un artículo que no existe, sin que nadie tenga que acordarse de
preguntar.

`Tercero.TarifaAsignada` —qué tarifa se le aplica a cada cliente— y `ArticuloProveedor` son del ítem
**1.10** y no se adelantan: son cruces **mutuos** entre módulos y eso se ve entero o no se ve.

**9. La pantalla de tarifas no suma al arranque, y el arranque se mide.** La ruta `/tarifas` es
**perezosa** (ADR-0028): su fragmento —`PaginaDeTarifas-CVnNXvw6.js`, 5,48 kB— no se descarga hasta
que alguien la abre. Lo único que la pantalla añade al arranque son los **diccionarios**, la fila de
la tabla de rutas y la clave del permiso. La pantalla **no pinta precios ni líneas** a propósito: el
listado de tarifas es el maestro —código, nombre, vigencia y estado—, y los tramos son otra pantalla;
por eso el modelo de vista del frontal **no lleva divisa**, y es seguro precisamente porque no hay
ningún importe que pudiera aparecer sin su moneda al lado (mismo criterio que la unidad y el impuesto
en `articulos`, ADR-0022).

### Tomadas por el agente de desarrollo — ítem 1.10 (2026-09-13)

**0. LA PRECEDENCIA CUANDO EL ANTEPASADO MÁS CERCANO NO CUBRE LA CANTIDAD** —viene del ítem 1.9, y
lo que faltaba no era código sino la decisión—. La regla se dice en voz alta en su **forma corta**
—«gana el artículo, y si no el antepasado más cercano»— y lo que el código hace es la **forma
larga**: gana el antepasado más cercano **que cubra la cantidad pedida**. Hoy las dos dan siempre lo
mismo, así que la diferencia no se ve; pero son reglas distintas y hay que decir cuál se quiere.

**Se quiere la larga: la precedencia es sobre el par (destino, tramo), no sobre el destino.** Una
tabla que empieza en 100 **no ha dicho que 5 no se venda**: ha dicho que no habla de 5. Lo más
específico que existe para 5 es entonces lo que diga de 5 el antepasado más cercano que hable de 5, y
cortar en el primer antepasado que tenga *alguna* línea devolvería `tarifa-sin-linea-aplicable`
teniendo la madre un precio escrito para esa cantidad. Que **nadie** cubra la cantidad sigue siendo
error con nombre y nunca cero: eso no cambia, y es lo que afirma
`La_cantidad_por_debajo_del_primer_tramo_no_encuentra_nada`.

**Y esto no choca con «el hueco es peor que el solape» del 1.9, que habla de otra cosa.** Aquella
decisión es sobre el hueco que deja a una cantidad **sin ninguna respuesta**, que sale por el mismo
`type` que «esta tarifa no cubre este artículo» y por eso se arregla en el sitio equivocado. Aquí hay
respuesta, escrita por una persona, un nivel más arriba.

**Lo que sostiene la forma corta es una comprobación que vive en otro fichero.** El primer tramo de
cada destino tiene que empezar en cero (`tarifa-linea-primer-tramo-sin-cero`, en `CrearLineaTarifa`),
`CantidadDesde` no se modifica, no se borran líneas y la cantidad pedida no puede ser negativa. Con
eso, **un destino que tenga alguna línea las cubre todas** y el ascenso nunca pasa de nivel: la rama
del bucle que sube al siguiente **no se alcanza por la API**.

**Relajar el cero obligatorio no pone roja ni una prueba, y cambia de quién viene el precio.** Por eso
la nota va **donde se impone el cero** y no solo donde se usa: quien lo relaje no va a leer los dos
ficheros. Y por eso la rama **no se borra** —es el único sitio donde la conducta querida está dicha
en código ejecutable—, sino que se marca:

```
[Trait("Alcance", "NoAlcanzablePorLaApi")]
  Una_categoria_cercana_sin_tramo_aplicable_deja_pasar_a_la_de_arriba
  La_cantidad_por_debajo_del_primer_tramo_no_encuentra_nada
```

**La marca es el cuarto diagnóstico de la lectura de cobertura**, y es distinta de los otros tres: no
dice que falte un caso ni que sobre, dice que ese caso **prueba qué se quiere que pase** si una
validación de escritura se relajara, y no un camino que alguien pueda recorrer hoy. Sin la marca se
lee como cobertura de un camino vivo; sin el motivo escrito al lado, la conducta se vuelve a discutir
desde cero la próxima vez. El filtro de los carriles es por `Category`, así que `Alcance` es una
dimensión ortogonal y no cambia qué corre en ningún sitio.

**1. LOS DOS CRUCES, CADA UNO POR EL `Contracts` DE SU DUEÑO, Y DICHOS EN CUATRO LISTAS.**
`ArticuloProveedor.TerceroId` es `Catalogo.Application → Bastion.Terceros.Contracts`, por
`IConsultaDeTerceros`; `Tercero.TarifaAsignadaId` es `Terceros.Application → Bastion.Catalogo.Contracts`,
por `IConsultaDeTarifas`. Los dos se resuelven en proceso —ni un `JOIN` entre esquemas ni una
llamada HTTP— y ninguno tiene clave ajena. Cada uno está escrito en las cuatro listas de
`tests/Arquitectura.Tests/Inventario.cs` que lo pueden ver: `AristasDeProyecto` (la referencia),
`CrucesDeclarados` (el cruce), `PuertasPublicas` (el puerto, diciendo que lee) e
`IdentificadoresDeclarados` (la propiedad, con su puerto). La segunda de las cuatro es la que
`TarifaAsignadaId` **necesitaba** y la heurística por nombre no le daba: se llama así y no
`TarifaId`, así que el barrido la sacó como **huérfana** y no como cruce —la infradetección del
ADR-0024 por tercera vez—.

**El censo compara en los dos sentidos, y eso son las mutaciones 1 y 2.**
`El_unico_cruce_entre_modulos_va_por_contratos` compara la lista **entera** de lo que el barrido
encuentra con la declarada: un cruce que existe y no está escrito sale rojo, y uno escrito sin
código detrás, también. Una comparación de inclusión en un solo sentido habría dejado verde una de
las dos.

**Ningún `Contracts` ve a otro, y no por acuerdo.** El barrido anterior recorre **todos** los
ensamblados con tipos, `Contracts` incluidos, así que un `Terceros.Contracts → Bastion.Catalogo.Contracts`
sería un cruce no declarado; `Las_referencias_de_proyecto_son_las_declaradas` lo ve además como una
arista de proyecto de más. Y las dos a la vez ni siquiera compilan: se probó y MSBuild contestó
«dependencia circular» desde `_GenerateRestoreProjectPathWalk`. Por eso por los dos puertos
**solo cruzan `Guid`, `DateOnly` y enumerados propios**.

**Un commit para los dos cruces, y dentro de él cada uno con su puerto, su primer consumidor y su
línea del censo** (`e8dd07b`). Ninguna de las tres piezas de ninguno de los dos cruces existe en un
commit sin las otras dos.

**2. LOS PUERTOS PREGUNTAN POR EL ESTADO, NO POR LA EXISTENCIA — Y ESTOS SON LOS ESTADOS DE CADA
LADO.** Un «¿existe?» deja pasar lo que existe y no se puede usar: en un tercero, al que no hace el
papel o al bloqueado; en una tarifa, a la que ya no rige.

| Sentido | Puerto | Estado | Qué contesta quien pregunta | Caso que lo afirma contra PostgreSQL |
|---|---|---|---|---|
| Catálogo → Terceros | `IConsultaDeTerceros.EstadoDeAsync(id, rol)` | `Disponible` | 201 al alta del proveedor | `ElPuertoDeTercerosContraLaBaseTests.El_papel_por_el_que_se_pregunta_decide_entre_Disponible_y_NoHaceEseRol` |
| | | `NoHaceEseRol` | 400 `articulo-proveedor-tercero-no-valido` | el mismo caso |
| | | `NoExiste` | el **mismo** 400, cuerpo idéntico | `Uno_inventado_y_uno_de_otra_empresa_NoExisten_y_el_ajeno_si_en_la_suya` y `Un_bloqueado_NoExiste_desde_fuera_y_desbloquearlo_lo_devuelve_Disponible` |
| Terceros → Catálogo | `IConsultaDeTarifas.EstadoDeAsync(id, fecha)` | `RigeEnEsaFecha` | 200 al asignar | `ContratoDeLosCrucesTests.Una_tarifa_que_rige_hoy_se_asigna_se_lee_y_con_nulo_se_quita` |
| | | `SoloResuelveLoViejo` | 409 `tercero-tarifa-no-vigente` | `Una_tarifa_que_ya_no_rige_y_una_que_aun_no_son_409_y_la_ficha_sigue_con_la_suya` |
| | | `NoExiste` | 400 `tercero-tarifa-no-encontrada` | `Una_tarifa_inventada_y_una_de_otra_empresa_son_400_y_la_ajena_vale_en_la_suya` |

**Seis casillas, las seis con dueño, y dos de ellas son las que devuelven error en cada sentido.**
En `NoExiste` de terceros caben tres situaciones —no hay ficha, es de otra empresa (R8), está
bloqueada (R16)— y caben a propósito: desde fuera de Terceros tienen que ser lo mismo. En
`SoloResuelveLoViejo` caben dos —la vigencia acabó, o todavía no empieza—, y el caso prueba **los
dos extremos** porque un adaptador que mirase un solo lado de la fecha pasaría con uno: es la
mutación 6, que solo cae por el de la tarifa acabada. La fecha con la que se pregunta es **hoy en
UTC**, del `TimeProvider` inyectado: lo que se guarda es la lista que se le propone al cliente a
partir de ahora, y el documento que la use volverá a preguntar con **su** fecha.

**Los dos noes de la tarifa se distinguen y los del tercero no, y la asimetría es el art. 32.** Al
otro lado de una tarifa hay una lista de precios de la propia empresa; al otro lado de un tercero,
la ficha de una persona. Quien teclea mal un identificador de tarifa y quien apunta a una caducada
tienen dos arreglos distintos; distinguir un tercero inventado de uno bloqueado convertiría el
formulario del proveedor en el censo de las bajas.

**3. `Bloqueado` NO ES UN VALOR, Y LLEGÓ A SERLO.** `EstadoDelTercero` se diseñó con cuatro, y la
matriz de la decisión 4 lo encontró antes de que existiera un solo caso contra la base: la casilla
`IConsultaDeTerceros → Bloqueado` no tenía dueño, y al escribirle uno el puerto contestó `NoExiste`.
El filtro de repositorio del art. 32 esconde la ficha antes de que el `switch` la vea; era un valor
que ningún productor produce, el defecto que abrió el 1.7. Hacerlo alcanzable exigía abrir en el
adaptador un ámbito de `IAccesoALoBloqueado` con un **cuarto motivo** en la lista cerrada, para
producir una diferencia que el único consumidor **tiene que** volver a juntar. La lista admite un
motivo cuando un camino necesita **ver** lo bloqueado, y este no lo necesita. Se quitó el valor, y
con él dos cosas que solo existían por él: la rama muerta del `switch` y un `!EstaBloqueado`
redundante con el filtro en `CualesSePuedenTratarAsync`. Lo que queda no depende de un orden de
comprobaciones que alguien pueda invertir: del bloqueado no sale qué papeles hace porque la fila no
llega. Y no se puede esquivar desde ese fichero sin que lo diga el carril rápido:
`ElFiltroNoSeSaltaPorAhiTests` cayó en las mutaciones 4 y 5 **antes** que PostgreSQL.

**4. LA MATRIZ DE PUERTO × ESTADO NO VEÍA LOS PUERTOS NUEVOS, Y AHORA LOS DESCUBRE POR LA FORMA
GENERAL.** `LaMatrizDePuertoYEstadoTests` (Organización, ítem 1.7) descubre en un ensamblado y por
un tipo de retorno, `Task<EstadoDeMaestro>`; los dos puertos de este ítem devuelven otros
enumerados desde otros `Contracts`, así que entraron con aquella matriz en verde y sus casillas sin
mirar. `LaMatrizDeLosPuertosDeEstadoTests` mira **toda interfaz de un `Bastion.*.Contracts` con un
método que devuelva `Task` de un enumerado**, exige una marca `[CubreEstadoDelPuerto]` por casilla
en un caso con el rasgo `Integracion`, y **delega** la familia de `EstadoDeMaestro` en su matriz
—con el fichero nombrado y comprobado en disco— para que cada familia tenga exactamente una. Corre
en el carril rápido, porque comparar conjuntos no necesita contenedor, y ancla los dos puertos del
cruce: un descubrimiento que solo encontrara los de Organización saldría verde por no tener nada
que comparar.

**5. LA RESPUESTA DEL ART. 32 AL CRUCE NUEVO: el listado esconde, el alta contesta igual, y lo que
colgaba se queda.** La pregunta —qué enseña la lista de proveedores de un artículo cuando uno de
ellos se bloquea— se contesta con cuatro casos contra PostgreSQL y no con una frase:

- **El listado lo esconde, y desbloquearlo devuelve la MISMA fila**, con el mismo identificador:
  `El_listado_esconde_al_proveedor_bloqueado_y_desbloquearlo_devuelve_la_MISMA_fila`. El filtro no
  cabe en un `WHERE` —el bloqueo está en otro esquema—, así que el caso de uso pregunta **una vez
  por el conjunto** con `CualesSePuedenTratarAsync`, que devuelve **los que sí**: un fallo que
  contestara de menos enseña de menos, que es el lado por el que tiene que caerse un filtro de
  protección de datos. Reservar no es borrar: la fila no se toca.
- **Al alta, bloqueado, inventado, de otra empresa y solo cliente contestan el MISMO 400**, con el
  cuerpo del `ProblemDetails` comparado entero —solo el `traceId` sustituido— y sin dejar fila:
  `Inventado_ajeno_bloqueado_y_solo_cliente_contestan_el_MISMO_400_y_no_dejan_fila`.
- **Volver a añadir a un bloqueado que YA suministraba es ese mismo 400, y no un 409**:
  `Volver_a_anadir_a_un_bloqueado_que_ya_suministraba_es_el_MISMO_400_y_no_un_409`. Y al
  desbloquearlo, vuelve a ser 409.
- **El puerto contesta `NoExiste` al bloqueado y `Disponible` al desbloquearlo**, contra la base y
  sin la API delante: `Un_bloqueado_NoExiste_desde_fuera_y_desbloquearlo_lo_devuelve_Disponible`.

**El tercer punto cambió el código, y el argumento que había era malo.** `AgregarProveedorAlArticulo`
miraba primero el duplicado y después el estado, con este razonamiento: «un 409 solo se da sobre
algo que ya se aceptó una vez, así que no cuenta nada nuevo». Sí cuenta algo nuevo: **lo que el
listado acaba de callar**. Si el listado esconde el suministro de un bloqueado y el alta contesta
«ya está», la diferencia entre las dos respuestas dice exactamente quién está bloqueado. Ahora se
pregunta el estado primero y el 409 queda para quien el listado sí enseña; es la mutación 3 la que
lo sostiene desde el carril rápido, con
`El_estado_se_pregunta_antes_que_el_duplicado_y_lo_escondido_no_es_un_409`.

**Lo que NO se filtra, y por qué.** La lectura de un suministro por su identificador
(`GET …/proveedores/{id}`) no pregunta por el estado: a ese identificador solo se llega desde el
listado —que ya filtró— o desde la respuesta de haberlo creado, lo único que publica del tercero es
su `Guid`, que quien pregunta ya tenía, y un 404 sobre una fila que existe impediría quitarla.
Tampoco lo hace la modificación de la referencia: el bloqueo de un tercero no puede congelar las
filas que ya apuntaban a él. Catálogo no implementa `IBloqueable` en `ArticuloProveedor` porque no
guarda datos de nadie: guarda un hecho entre dos.

**6. LA DIVISA: dos representaciones en el mismo agregado, no se comparan, no se unifican ahora, y
no se convierte nada.** El `Tercero` lleva **dos** divisas desde este ítem, y de maneras distintas:
`LimiteCredito` es un `Importe`, con el **código ISO 4217** dentro; `TarifaAsignadaId` apunta a una
`Tarifa` cuya `DivisaId` es un **`Guid`** al maestro `Divisa` de Organización.

- **(a) No se comparan**, y no es un olvido. Un límite en dólares y una tarifa en euros no son una
  contradicción: el límite es cuánto se le fía, la tarifa es cómo se le pone precio, y en este
  agregado **no nace ningún importe** que tuviera que estar en las dos. Lo que decide en qué divisa
  se emite un documento es el documento (fase 5). Hasta este ítem la no comparación estaba
  garantizada por una ausencia —Terceros no tenía puerto con el que preguntar una divisa—; desde
  `AsignarTarifa` la tiene a un paso, así que queda escrita como decisión en su comentario y
  **probada por el efecto**:
  `Un_limite_en_dolares_y_una_tarifa_en_euros_conviven_porque_nadie_compara_divisas`, en los dos
  órdenes —asignar la tarifa con el límite puesto, y cambiar el límite con la tarifa asignada—,
  con el límite en **USD** y la tarifa en **EUR**, que es la combinación inversa al ejemplo del
  comentario de `AsignarTarifa` y la misma afirmación vista desde el otro lado. Visto rojo antes de
  darlo por bueno, con un canario en cada orden (en *Verificado — ítem 1.10*).
  Es la misma decisión que la 6 del 1.9 («la divisa cruzada se acepta») con otro sujeto, y el mismo
  argumento estructural: el puerto de tarifas **devuelve un estado y nada más**.
- **(b) No se unifican ahora, y la buena es el código ISO.** Los motivos, por orden de peso: la
  **semántica de una divisa sale de su código** —hasta el maestro lo reconoce: `Divisa.Decimales`
  es `CatalogoDeDivisas.UnidadMinima(Codigo)`—; `Importe` y la regla del redondeo (R6) **solo
  conocen códigos**; los formatos fiscales que vienen (fase 5) hablan ISO 4217; y el código es una
  **clave natural, inmutable y la misma en toda instalación**, así que un `Guid` delante solo añade
  una indirección que hay que resolver para saber de qué moneda se habla. Lo que el maestro aporta
  —que una divisa esté dada de alta y no retirada en esta empresa— se puede preguntar **por
  código**. Lo que se movería, entonces, es el `Guid`: `Tarifa.DivisaId`, `PrecioResueltoDto.DivisaId`
  y los dos de `TipoCambio`. **No ahora**: hoy ningún importe cruza de una representación a la otra,
  y migrar tres agregados de dos módulos sin ese cruce es trabajo sin defecto que arregle. **El
  disparador**, escrito para que no se olvide: antes de que la primera línea de un documento
  convierta un precio resuelto en un `Importe`, que es el primer sitio donde las dos formas
  tendrían que encontrarse.
- **(c) Ninguna conversión.** Convertir es de `TipoCambio`, necesita una fecha y un cambio, y no es
  de la fase 1.

**7. EL PRECIO POR CLIENTE: decidido y no hecho.** Con `Tercero.TarifaAsignada` existe ya la
tarifa por cliente del §15, y la tentación es un «resuelve el precio de este artículo para este
cliente». **No se hace aquí, ni en Catálogo ni en Terceros.** En Catálogo exigiría un tercer puerto
hacia Terceros para leer la asignación, y la mutua pasaría a ser un flujo de datos de ida y vuelta
dentro de una sola petición; en Terceros, llamar al resolutor de Catálogo desde el módulo que no
sabe de precios. Y las dos esconderían dos decisiones que son de la venta: **con qué fecha** se
resuelve —la del documento, no la de hoy: la tarifa que regía al asignarla puede no regir el día
del pedido, y entonces es `SoloResuelveLoViejo`— y **qué pasa si el cliente no tiene tarifa**. El
sitio es **4 · Ventas**: el documento lee la tarifa asignada por `Terceros.Contracts` y le pide el
precio a Catálogo con **su** fecha, por la resolución por código del 1.9. Por eso `TarifaAsignadaId`
es anulable y su lectura no pregunta por el estado.

**8. EL LISTADO DE PROVEEDORES VA SIN PAGINAR, Y EL DELETE BORRA.** Sin paginar a propósito: el
filtro del art. 32 se aplica **después** de leer, así que paginar daría páginas de tamaño variable
y un total que miente —y restando de un total que miente se cuenta cuántos hay bloqueados—; lo que
acota el tamaño es el modelo, los proveedores de **un** artículo. Y `DELETE …/proveedores/{id}` es el
único del módulo y **borra la fila**: lo que desaparece no es la ficha de nadie sino un hecho entre
dos que ha dejado de ser verdad, y el rastro de quién lo quitó está en la traza (ADR-0012).

**9. EL REPARTO DE SEMILLAS, ESCRITO DONDE SE LEE.** El NIF de una empresa es único en toda la
instalación y el de un tercero dentro de su empresa, y un choque no sale por el fichero que llega
segundo sino por el otro (lo aprendido en el 1.9). Este ítem toma el bloque de empresas **200-215**
—200-205 en `ElPuertoDeTercerosContraLaBaseTests`, 206-215 en `ContratoDeLosCrucesTests`— y el de
terceros **32 000 001-32 000 022** —001-009 allí, 010-022 aquí—, sin pisar ninguno de los que ya
había (102-106, 110-124, 131-145, 170-183, 190-193; terceros 30 000 xxx y 31 000 xxx). El reparto
está en el comentario de clase de `ContratoDeLosCrucesTests`, que es donde lo va a leer el
siguiente que necesite un bloque. Todos los NIF salen de `Escenario.NifInventado`: ninguno es real.

**10. EL DISPARADOR DE LOS 430 KiB, ESCRITO Y NO DE MEMORIA.** El margen de arranque del frontal
quedó en 34 KiB en el 1.9 (416/450). **Regla de este ítem:** si el fragmento de arranque pasa de
**430 KiB**, se saca del arranque el diccionario del idioma no activo **antes** de seguir con el
ítem. Medido en `3a7ba3e`: **418/450 KiB** —+2 por los cinco `type` de error nuevos en las dos
lenguas, y ninguna pantalla—, así que **no ha saltado**. La regla se queda para el 1.11, que sí trae
pantalla.

**11. FUERA DEL ÍTEM, y no «de paso»:** la importación CSV (**1.11**), `CodigoBarras` (fase 2, con
su import), la conversión de divisas, las facturas y las existencias. La resolución de precio por
cliente, como decisión escrita en la 7 y sin código.

### Tomadas por el agente de desarrollo — ítem 1.11 (2026-09-14)

> **Escritas antes del *endpoint*, como pidió el usuario**, y con su ADR: el
> [ADR-0034](../adr/adr-0034-la-importacion-escribe-el-fichero-entero-y-el-recibo-caduca.md), que
> **corrige** dos frases del ADR-0026 §1 —«cada fila en su propia unidad de trabajo» y «el valor que
> lo provocó»— y deja el resto en pie. La corrección va también en el párrafo del ADR-0026.

**0. LO QUE EL ÍTEM EMPEZÓ ARREGLANDO, ANTES DE LA IMPORTACIÓN.** Tres cabos del 1.10, cada uno en su
commit. La lista de rastros prohibidos de los sondeos es **una** (`RastrosProhibidos.Todos`) y ningún
rastro puede estar hecho solo de cifras hexadecimales, porque «5432» cabía en un `traceId` y tumbó
la batería base. La marca `Alcance=NoAlcanzablePorLaApi` tiene **lector** y cada marca, su motivo
comprobado contra el OpenAPI y contra el caso que la sostiene. La clave ajena entre esquemas la vigila
una **pregunta a `pg_constraint`** que exige el conjunto vacío, con un canario escrito a mano. Y el
DTO de proveedores dice por qué su lectura de una fila no pregunta al art. 32.

**Y la convención del recuento del frontal, fijada y que no se vuelve a cambiar:** `licencias.py`
cuenta **sin la raíz** (`bastion-web` no es una dependencia de sí mismo). El 1.9 dio 548 sin raíz y el
1.10, 549 con ella; la cifra buena de las dos es 548.

**1. SE IMPORTAN TERCEROS, Y SOLO TERCEROS.** Traen las dos cosas que el mecanismo tiene que
aguantar: el **NIF validado** y el **art. 32** —una fila contra una ficha bloqueada contesta lo mismo
que contra una activa—. Los artículos traen **otra**: resolver por código la unidad, el impuesto y la
categoría, y los puertos de estado del 1.2 y del 1.9 solo reciben identificadores. Es un contrato
nuevo entre módulos, y queda abierto para la fase 2 con ese motivo.

**2. LA FILA DECIDE, EL FICHERO ESCRIBE.** Cada fila se valida y se decide por separado; las altas
aceptadas se confirman en **un** `SaveChanges`, en la transacción del filtro y con el recibo, **sin
puntos de guardado**. Todo lo que puede rechazar una fila se comprueba **leyendo** antes de escribir:
formato, contrato, NIF, régimen, límite, repetición en el fichero y existencia en la base (una sola
consulta, dentro de `ViendoLoBloqueado`).
**Si el proceso cae a mitad, no hay nada importado:** ni filas ni recibo, y el reintento con la misma
clave es la primera vez. Lo comprueba un disparador que revienta en mitad del `INSERT`.
**La R12 se rompe a sabiendas, y está escrito por qué** (ADR-0034 §1): no se modifica ningún agregado,
se crean N independientes, y ningún invariante los cruza. Una transacción por fila cumpliría la letra
de la R12 y rompería la invariante de R10: el reintento diría que ya existen las filas que él mismo
dejó a medias.

**3. LOS DOS TOPES, ANTES DEL *ENDPOINT*.**
- **Tamaño: 2 MiB** (2 097 152 bytes), declarado con `[TopeDelCuerpo]` en la acción e impuesto por el
  **lector acotado del borde**, que es quien vuelca: el filtro de idempotencia y el formateador de
  `text/csv`. `Content-Length` mayor → `413` sin leer un byte; sin `Content-Length`, se leen tope + 1
  bytes y se corta. **El caso de uso no lo comprueba**: cuando le llega el fichero, la memoria ya se
  ha gastado.
- **Filas: 5 000, contando las vacías**, con `413` y otro código. Una fila típica son 150 bytes: en
  2 MiB caben más de diez mil, así que para un fichero normal manda el de filas.
- **El informe**, agrupado por **(columna, motivo) con su lista de líneas**, y sin ningún valor de la
  entrada. Crece a unos seis bytes por par (fila, motivo): el peor caso, 5 000 × 18 × 6 ≈ **530 KiB**
  en la columna `cuerpo`; lo normal, un kilobyte. Fila por fila con sus motivos pasaría de 4 MiB.
- **nginx tenía un tope menor y nadie lo había mirado:** `client_max_body_size` vale 1 MiB por omisión,
  y un fichero de 1,5 MiB no habría llegado a la API. Se sube a 3 MiB en `/api/`, por encima del de la
  API para que el `413` con nombre lo dé ella. **Lo comprueba un paso del humo por el puerto del
  frontal:** 2 MiB + 1 byte vuelve con el `413` de la API y su `type`; 3 MiB + 1 byte lo corta nginx,
  sin `type`. Por encima de 3 MiB la pantalla enseña el texto genérico y ofrece reintentar, que no
  sirve de nada: queda anotado abajo como abierto.
- **El tope se mide sin el cliente de la fábrica de pruebas.** El `RedirectHandler` que mete
  `WebApplicationFactory.CreateClient` copia el cuerpo entero antes de mandarlo, y la primera versión del
  caso sin `Content-Length` contó 64 MiB entregados con el lector acotado funcionando. Con
  `Server.CreateHandler()` a secas se cuenta lo que pide el servidor.

**4. EL PLAZO DE LA TABLA DE IDEMPOTENCIA: 24 HORAS, EN LA FILA, CON PURGA CADA HORA.** La nota que
el 0.9 dejó abierta —«se decidirá con datos»— se cierra sin ellos, y se dice: el dato que faltaba
(cuánto tarda un cliente real en reintentar) sigue sin estar en el código, pero la tabla guarda **la
ficha de un tercero** en cada `201` de `POST /terceros/terceros`, y un dato personal sin plazo no se
puede quedar esperando a una medición. Veinticuatro horas cubren el reintento de red y el de un
cliente que pasó la tarde sin conexión.
- Cada fila lleva **`caduca_en`**, calculado al reclamar: el plazo es parte del modelo
  (`proteccion-datos.md`), y cambiarlo no altera lo que ya se prometió.
- **La purga vive en Auditoría**, dueña de la tabla, corre al arrancar y cada hora, y abre un ámbito
  sin inquilino con motivo propio en la lista cerrada. Un recibo dura **al menos 24 horas y como mucho
  25** con la API en marcha.
- **Vencido, se borra.** Y una clave caducada es una clave nueva: reintentar pasadas 24 horas vuelve
  a hacer el trabajo. En el alta de un tercero, eso da `ya existe`; en un alta sin clave natural, un
  segundo recurso. Queda dicho en el contrato.

**5. EL DIALECTO, ESCRITO ENTERO** (detalle en el ADR-0034 §5).
- Separador **`;`**, sin detector. Si la cabecera casaría separada por `,` o por tabulador, el error se
  llama `importacion-separador-no-admitido` —es un diagnóstico, no se lee con otro separador—. **Un
  fichero de una sola columna** no casa con nada: `importacion-cabecera-no-valida`.
- **Cabecera fija** de dieciocho columnas, en su orden.
- **UTF-8** con BOM o sin él; si no es UTF-8 válido, **Windows-1252**. UTF-16, UTF-32 o un `NUL`:
  `importacion-codificacion-no-admitida`. **El detector falla** con un Windows-1252 cuyos bytes forman
  UTF-8 válido por casualidad (`Ã±`, `Â¿`): lo lee como UTF-8. En texto español real no ocurre, y
  ASCII es igual en las dos.
- CRLF o LF; CR suelto, `importacion-fin-de-linea-no-admitido`. Comillas RFC 4180 con saltos de
  línea dentro; sin cerrar, `importacion-comillas-sin-cerrar`.
- Importes con **coma decimal** y punto de miles solo en grupos de tres; `1.5`, `1e3` o `1.234,56 €`
  son `formato-no-valido`, no un número adivinado.
- Sí y no: `sí`, `si`, `no`, `VERDADERO`, `FALSO`; vacío es no.

**6. LO QUE YA EXISTE NO SE TOCA, Y LOS PERMISOS VAN TIPO POR VERBO.** La importación **solo da
altas**. La identificación que ya existe —activa o bloqueada, sin distinguir— es `ya-existe`, y la que
se repite dentro del fichero, `repetida-en-el-fichero`. **Nunca actualiza**: el permiso de crear no es
el de modificar, y una fila de CSV no trae la versión de la ficha.
**Corregido al escribir el *endpoint*:** la acción exige un permiso **propio**,
`terceros.tercero.importar`, y no `terceros.tercero.crear`. La regla `CadaAccionDeclaraSuPermiso` no
deja que dos acciones de escritura compartan permiso, y la diferencia es real: importar es dar de alta
cinco mil de golpe. **El caso de uso exige además `crear`** —importar es dar de alta, y quien no puede
dar de alta uno no puede darlos de alta en bloque— con `403 importacion-sin-permiso-de-alta`; y si
**alguna** fila trae límite de crédito, `terceros.limite-credito.fijar`, con
`403 importacion-sin-permiso-de-limite`. Los dos `403` rechazan **el fichero**, antes de decidir ninguna
fila: rechazar esas filas mezclaría un permiso con un dato malo, e importarlas sin el límite las dejaría
a medias. El ADR-0034 lleva la misma corrección.

**7. LA LÍNEA DE LOS CAMPOS QUE EMPIEZAN POR `=`, `+`, `-` O `@`.** El informe no lleva ningún campo
que haya escrito el usuario —columnas y motivos los escribe Bastion—, así que descargado o pegado en
una hoja no puede ejecutar nada. Este ítem **no** ofrece la descarga ni exporta terceros. El día que
exista una exportación que saque datos del usuario, cada campo que empiece por uno de esos cuatro
caracteres, o por tabulador o retorno, sale con un apóstrofo delante.

**8. EL SONDEO, CON LOS CUATRO CRITERIOS Y LA PREMISA CONTRA UN EXCEL DE VERDAD.**
- **Se contesta o se cierra, y nunca 5xx.** El test es **más estricto que eso**, y a propósito: cada uno
  de los 29 ficheros hostiles lleva su estado y su `type` esperados, no «cualquier cosa menos 5xx». Un
  `400 datos-no-validos` en vez de `importacion-cabecera-no-valida` también es «contestar», y es
  exactamente lo que devolvía el fichero vacío (abajo). **No se filtra el interior**, con `RastrosProhibidos.Todos`.
  **No se filtra el dato del usuario:** un canario en cada campo que no puede aparecer ni en la
  respuesta ni en el recibo guardado ni en el registro. **Y se sigue atendiendo:** detrás de cada
  entrada mala va una buena, que tiene que entrar.
- **La premisa del dialecto se comprueba contra dos ficheros exportados por Excel** con
  automatización COM desde este equipo (Excel 16, configuración regional es-ES): «CSV (delimitado por
  comas)» y «CSV UTF-8». Van al repositorio tal como salen. **Los NIF no están en ellos**: llevan
  marcadores que el test sustituye por `Escenario.NifInventado`, así que un NIF real no puede colarse
  en un fichero de prueba sin que se note.
- **Lo que el sondeo encontró:** un fichero de **cero bytes** no llegaba al lector del dialecto. La base
  de `InputFormatter` contesta «sin valor» a un cuerpo vacío sin llamar a `ReadRequestBodyAsync`, y el
  enlace lo convertía en `400 datos-no-validos` con el «field is required» del marco, en inglés. El
  formateador sobrescribe `ReadAsync`, y un fichero vacío es `importacion-cabecera-no-valida`.
- **Y lo que encontró el test del formateador, que no estaba previsto:** `LectorAcotadoDelCuerpo.Leido`
  **nunca devolvía nulo**. `Get<CuerpoLeido>()?.Bytes` es un `byte[]` nulo, y convertido a
  `ReadOnlyMemory<byte>?` sale **con valor**, vacío. La guarda del formateador —«si la acción no
  declaró tope, lanza»— no podía saltar, y una acción sin `[TopeDelCuerpo]` habría recibido un fichero
  vacío en silencio. No lo recorría nada porque la única acción con CSV declara su tope. Se corrige con
  un `if`, y `ElFormateadorDeCsvNoLeeLaRedTests` fija las dos promesas: sin tope lanza sin tocar la
  corriente, y cero bytes es un fichero vacío y no «sin valor».
- **Y lo que encontró la batería entera: la puerta iba detrás del tipo de contenido.**
  `LaPuertaDeCadaAccionTests` contó `TercerosController.Importar → 415` con un permiso que no era el
  suyo. `[Consumes("text/csv")]` es también metadato del enrutador: el `415` sale al **elegir la ruta**,
  antes de `UseAuthorization`, y a esa respuesta solo le alcanza la política por omisión —estar
  autenticado—. Cualquiera que hubiera entrado sabía, sin permiso, que la acción existe y qué tipo
  espera. Se sustituye por **`[TipoDelCuerpo]`**, hermano de `[TopeDelCuerpo]`: un filtro de recurso
  —la autorización ya ha decidido cuando corre— que va **el primero**, antes que el tope, porque
  rechazar por el tipo no necesita leer; y que da el tipo al explorador de la API, así que el OpenAPI no
  cambia. Lo fijan dos casos nuevos: `Un_cuerpo_que_no_es_un_CSV_es_415_antes_de_leer_nada` (64 MiB de
  JSON sin `Content-Length`: con el orden cambiado a mano sale **`413`**, no `415`) y el fichero hostil
  «un CSV sin decir su tipo». Y el tipo que publica el contrato, cambiado a mano, pone rojo
  `generar-openapi.sh --comprobar` con su diff.

**9. EL REPARTO DE SEMILLAS.** Empresas **230-259** y terceros **33 000 001-33 000 999**, sin pisar los
bloques que ya había (el último, 200-215 y 32 000 xxx del 1.10).

**10. EL FRONTAL: 418/450 Y LA REGLA DE LOS 430.** La pantalla va en una ruta diferida; lo que sube
el arranque son sus textos en los dos diccionarios. Si pasa de **430 KiB**, se saca del arranque el
idioma no activo antes de seguir.
**Medido con la pantalla hecha: 426/450 de arranque y 592/900 servidos** (antes, 418/577 sobre
`051449d`), con `bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900`. No pasa de 430, así
que los dos idiomas siguen en el arranque; la pantalla, diferida, pesa 6,7 kB.
- **La pantalla manda los bytes y no el texto**, con `bodySerializer`: leer el fichero en el navegador
  lo decodificaría como UTF-8, y cada «ñ» de un CSV en Windows-1252 llegaría rota. Ni separador ni
  cabecera se comprueban en el frontal: una segunda opinión podría no coincidir con la que decide.
- **La clave de idempotencia es de la elección del fichero**, no del envío: reintentar repite clave;
  elegir otro, la estrena. Reintentar solo se ofrece ante un fallo sin nombre.
- **La cabecera de la plantilla está copiada** en `model/importacion.ts`, y no queda en promesa:
  `LaPlantillaDelFrontalEsLaDeLaApiTests` la extrae del fuente —y la extracción señala lo que no sea
  una cadena literal en vez de saltárselo— y la compara con `ImportacionDeTerceros.Cabecera`.
- La ruta `/terceros/importacion` exige `terceros.tercero.importar`, no sale en la navegación y se
  llega por un enlace del listado que solo ve quien tiene ese permiso.

**11. FUERA DEL ÍTEM, y no «de paso»:** `CodigoBarras`, la conversión de divisas, las facturas, las
existencias, el precio por cliente, la importación de artículos (decisión 1), la exportación, y
traducir a `409` la carrera de unicidad del alta (hoy es un `500` limpio, igual en el alta suelta y en
la importación).

### Tomadas por el agente de desarrollo — addenda de la fase 1, ítems 1.12 a 1.14 (2026-09-16)

> **Los tres ítems los abrió el usuario** al verificar el cierre de la fase 1 contra la API, y se
> numeran como el 0.14–0.16: son del cierre de la fase, y la fase 2 no se abre arrastrándolos. Las
> once casillas de la puerta de clarificación no se tocan. Rama `feature/1.12-addendum`, con el
> arnés **antes** que el arreglo en la historia: `d7e2d91` rojo y `7c1b723` verde.

**Tres premisas del enunciado, corregidas contra el repositorio antes de escribir nada:**

- **Los números del catálogo.** El de la fase 0 (`fe7059d`) tenía **55** permisos y el de la fase 1
  tiene **93**: faltan **38**, no los que suponía el enunciado. Contados con
  `git show fe7059d:…/PermisosDeIdentidad.cs` y `…/PermisosDeOrganizacion.cs`, y los 93 con
  `GET /api/v1/identidad/roles/permisos` contra el *compose*.
- **El fichero de variables sí existe**, y se llama `deploy/.env.example`, no `.env.ejemplo`. Ya
  documentaba las ocho `BASTION_SEMILLA_*`, con un valor con espacios a propósito. Lo que faltaba no
  era el fichero, sino que algo lo comparase.
- **Once más tres son catorce**, no trece: la fase 1 la cierran **catorce** ítems.

**1. EL ROL DEL SISTEMA: LA SALIDA (c)** —actualizar siempre **y** cerrar la edición—, con el
[ADR-0035](../adr/adr-0035-el-rol-del-sistema-lleva-el-catalogo-desplegado-y-el-arranque-se-prueba-arrancando.md).
El migrador alinea cada rol marcado como del sistema con el catálogo desplegado, en cada despliegue;
`PUT` de un rol del sistema con otra lista es `409 permisos-de-rol-del-sistema`.
**Por qué no (b):** avisar deja el `403` donde estaba, con una línea más en un registro. **Por qué no
(a) sola:** deshace en silencio un recorte deliberado, y sostenerlo exigiría guardar qué catálogo vio
cada rol. **El coste:** el rol del sistema no se recorta; quien quiera menos poderes crea un rol
propio, y un recorte que ya existiera lo deshace el primer despliegue, permiso a permiso en el
registro y con una fila de traza por permiso.
**La gravedad, exacta:** regresión de privilegios **silenciosa** —ni error ni aviso, y `403` en todas
las pantallas de la fase 1— y **recuperable a mano**, porque `ModificarRol` valida contra el catálogo y
la fase 0 ya tenía `identidad.rol.modificar`. Y por el otro lado, un permiso retirado se quedaba
concedido para siempre.

**2. EL ESTADO QUE CONSTRUYE EL SEGUNDO ARRANQUE**, y no una versión vieja instalada:
(i) el primer arranque de esta versión —el migrador dice `SinRolesDelSistema` y la API siembra con las
ocho variables—, afirmado: rol = catálogo; (ii) por SQL, el rol con los **55** permisos de `fe7059d`
más uno bien formado y de ningún catálogo, afirmado: 56, y `GET /api/v1/terceros/terceros` → `403`
con un inicio de sesión nuevo; (iii) `up --no-deps --force-recreate migraciones api` sobre el mismo
volumen con las ocho `BASTION_SEMILLA_*` vacías, afirmado: migrador con 0, `RolDelSistemaAlineado`,
**un** `PermisoRetiradoDelRolDelSistema` y `SemillaSinVariables` en la API; (iv) inicio de sesión nuevo:
rol = catálogo en los dos sentidos y la misma lectura → `200`. Rojo sobre `7f676e2` con el arnés solo
(«le faltan 38 y le sobra 1»), verde con el arreglo.
**Y las migraciones sobre filas**, la otra mitad de «arrancar sobre algo que ya existe»:
`LasMigracionesSobreTablasConFilasTests` aplica las migraciones de los cinco contextos **una a una**,
en el orden de su marca de tiempo, inventando antes de cada paso una fila en cada tabla que existe, y
con el contraste sobre tablas vacías.

**3. LO QUE ENCONTRÓ EL ARREGLO Y NO ESTABA PEDIDO.** `LosIdentificadoresAjenosTests` daba un **falso
rojo** con la primera lambda que capturaba un `Guid` en el dominio: la clase que genera el compilador
tiene un campo `…Id` que no es de nadie. Ahora excluye los tipos `[CompilerGenerated]`.

**4. LAS DIECISIETE REGLAS: `docs/dominio/reglas-duras.md`.** Copiadas del §6 del plan maestro
**literalmente**: sha256 `86aa53a0…a2e108`, «Revisión de 2026-08-25 (tarde)», con los enunciados
comparados carácter a carácter con un guion. **La numeración llega a diecisiete y no salta**: R1…R17
contiguas, con R1, R7 y R13 presentes en el plan maestro, así que se copiaron y no hay nada que
inventar. Estado: **viva** 7 (R6, R8, R10, R11, R12, R16, R17), **aplazada** 10 (R1, R2, R3, R5, R9 y
R13 a la fase 2; R7 y R14 a la 3; R15 a la 5; R4 a la 7), **no aplica** 0.
`LasDiecisieteReglasTests` compara la tabla con el repositorio **en los dos sentidos**: las filas son
R1…R17, una vez y en orden; toda regla citada en `src`, `tests`, `frontend/src`, `docs`, `db`,
`deploy`, `scripts`, `.github` y los tres `.md` de la raíz tiene su fila —incluidas las etiquetas `rNN`
de la cabecera de los ADR, que es de donde sale R13—; cada fila viva nombra al menos un `…Tests`, cada
aplazada una fase entre la 2 y la 11; y todo tipo, miembro o ADR que la tabla nombra existe.
**Lo que no comprueba, y no se disimula:** que el enunciado siga siendo el del plan maestro. El plan
maestro no está en el repositorio, así que ninguna regla puede leerlo: la tabla dice de qué versión se
copió y cómo se contrasta **a mano en cada puerta de fase**.

**5. LAS HIGIENES.**
- **La línea de estado del README** sale de las casillas de este checklist, y la compara
  `ElEstadoDelReadmeEsElDelPlanTests`: la fase más alta con casillas, su nombre del encabezado, sus
  marcadas y sus totales, y «cerrada» o «en curso».
- **Las variables del despliegue**: `LasVariablesDelDespliegueTests` compara el ejemplo con lo que
  interpola el *compose*, en los dos sentidos; las de la semilla, con las constantes de
  `SemillaDeArranque`; y las que escribe la CI, contenidas en el ejemplo. El README dice que hacen falta
  las ocho para entrar la primera vez, y el ejemplo, que retirarlas no deja atrás al rol.
- **`EsDelSistema`**, bajo (c), solo había que anotarlo: su texto prometía «no se puede suprimir» y
  ninguna operación suprime roles; ahora dice lo que se cumple (ADR-0035 §4).
- **La raíz del repositorio**, en un solo sitio: las seis copias, borradas, y sus comentarios que
  explicaban algo, movidos a `tests/Comun/RaizDelRepositorio.cs`. La guardia existe —
  `LaRaizDelRepositorioSeBuscaEnUnSitioTests` busca en `tests/**/*.cs` el literal `"Bastion.sln"` y
  el paso `.Parent;`— y tiene un límite escrito: una búsqueda que suba por otro camino no la ve.
- **La convención del recuento del frontal** —**sin la raíz**— está ahora donde se mide: en la
  cabecera de `scripts/dependencias-por-conjuntos.py`, que la batería de `AGENTS.md` nombra.

**6. FUERA DEL ADDENDUM:** nada de la fase 2 —ni Inventario, ni `CodigoBarras`, ni su import—;
ninguna decisión sobre R5 ni sobre reabrir un ejercicio, que van como preguntas a la puerta de la fase
2 en el traspaso; `CLAUDE.md` §5; la destrucción al vencer del art. 32; y la correlación buscar/alta.

## Estado actual

**FASE 1 CERRADA — las catorce casillas marcadas y el run que lo certifica:**
run **35103339786** sobre `f3c749e`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3`): Frontal `104818073051` ✓, Backend `104818073414` ✓ y Humo `104820147951` ✓. Es el de
las **addenda 1.12–1.14**, las tres últimas de las catorce —once de la puerta de clarificación y tres
que abrió la verificación de su cierre; hasta el 2026-09-16 este anuncio decía «once»—, y lo que
certifica es el árbol entero de la fase: **1 261 casos** de .NET —862 en el carril rápido y 399
contra PostgreSQL— con las dos listas de ensamblados comparadas enteras, **128 operaciones** en 74
rutas y **90 `type`** de error, cada artefacto al día con el versionado, el entorno del *compose*
sirviendo datos, y un segundo arranque sobre la misma base.

**Cada casilla tiene su run.** Los once de la puerta se volvieron a pedir a la API al cerrarla, el
2026-09-15 —`GET /repos/AOjeda006/Bastion/actions/runs/<id>`, leyendo `head_sha` y `conclusion`, no
la memoria— con el guion `runs-fase-1.py` del directorio de trabajo de la sesión; el de las addenda,
de la misma API al cerrarlas, con `vigilar-ci.py`:

```
 1.1  run 33779064545  head_sha c3e0119 casa  completed/success  feature/fase-1-desglose
 1.2  run 33806271861  head_sha c54f783 casa  completed/success  feature/1.2-puertos-de-lectura-y-la-cuarta-via
 1.3  run 33831276412  head_sha edd0048 casa  completed/success  main
 1.4  run 33906512809  head_sha 73c2836 casa  completed/success  main
 1.5  run 34046817118  head_sha 279b8c7 casa  completed/success  feature/1.5-terceros-el-agregado-y-su-identidad
 1.6  run 34125157573  head_sha 911ba5c casa  completed/success  feature/1.6-terceros-lo-que-cuelga
 1.7  run 34177571670  head_sha dd89e9f casa  completed/success  feature/1.7-la-retirada-y-las-dos-conversiones
 1.8  run 34424407829  head_sha f90dbed casa  completed/success  feature/1.8-catalogo-articulo-y-categoria
 1.9  run 34599130092  head_sha eadb7b9 casa  completed/success  main
1.10  run 34833598837  head_sha 958c339 casa  completed/success  main
1.11  run 34939033897  head_sha 73d1db7 casa  completed/success  main
1.12  run 35103339786  head_sha f3c749e casa  completed/success  feature/1.12-addendum
1.13  run 35103339786  head_sha f3c749e casa  completed/success  feature/1.12-addendum
1.14  run 35103339786  head_sha f3c749e casa  completed/success  feature/1.12-addendum
```

De los once de la puerta, seis corrieron sobre su rama y cinco sobre `main`, y sus once `head_sha`
están en la historia de `main`: `git merge-base --is-ancestor <sha> main` sale bien para cada uno. El
de las addenda corrió sobre su rama, y `f3c749e` es antepasado de este cierre, que llega a `main` por
avance rápido. Antes hubo uno rojo sobre la misma rama, 35101097378 sobre `1f69ba5`, contado en
*Lo que la CI encontró y en local no se veía — addenda*. El repaso de cada criterio
contra algo observable, el veredicto de las nueve notas abiertas y el traspaso van en sus tres
secciones, detrás de *Las ocho mutaciones del 1.11*. **Dónde retomar exactamente:** la **fase 2 ·
Inventario**, por su puerta de clarificación y no por código, con las dos preguntas de R9/R14 y R5
en su tanda (*Traspaso a la fase 2*).

**Puerta de clarificación de la fase 1 cerrada — el desglose existe y es una decisión escrita:**
doce preguntas planteadas juntas y contestadas juntas, con su motivo, en *Decisiones tomadas →
puerta de clarificación de la fase 1*; **once ítems** con criterio verificable en el *Checklist* →
*Fase 1 · Maestros*; y **seis ADR** nuevos, del **0024** al **0029**. `CLAUDE.md` estrena los dos
imports del Anexo A.2.3 que entran con la fase y se quedan: `proteccion-datos.md` y
`soft-delete.md`.

Dos cosas que salieron de comprobar las decisiones **contra el código** y que no estaban en la
tanda, las dos averías mudas que la fase 1 activa: el ítem 1.4 **caduca la mitad de las cuatro
cláusulas «DEPENDE DE»** del ADR-0017 (por eso construye un listado y no un `GET` individual), y
`TodaEscrituraDiceComoSeProtegeTests` **fija sus ensamblados a mano**, así que el primer controlador
de Terceros quedaría fuera del barrido sin que nada se pusiera rojo — se arregla en el **1.3**, o
sea antes de que ese controlador exista.

**La decisión que quedaba abierta ya está contestada** (2026-09-03, con el 1.2): quién escribe el
texto que lee una persona cuando falla una validación → **el frontal**, mapeando el `type` estable del
`ProblemDetails`; la API no negocia idioma por `Accept-Language`. **ADR-0030**, con sus tres criterios
en el ítem **1.5** —movidos ahí en el 1.3, y con el mecanismo antes que su primer uso, porque el
catálogo de `type` **no está vacío hoy**— y el motivo del movimiento en *Decisiones tomadas → ítem
1.2*.

**Addenda 1.12 a 1.14 cerradas — el segundo arranque, las diecisiete reglas y las higienes del cierre:**
run **35103339786** sobre `f3c749e`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `104818073414` ✓ (22 pasos, 0 omitidos),
Frontal `104818073051` ✓ (17 pasos, 0 omitidos) y
Humo `104820147951` ✓ (26 pasos, 1 omitido:
el «Diagnóstico», que lleva `if: failure()`). Humo tiene un paso más que en el 1.11:
«El segundo arranque deja al rol del sistema con el catálogo entero». **Es el segundo run de la rama:**
el primero, 35101097378 sobre `1f69ba5`, salió rojo en ese paso por el propio arnés —`logs | grep -q` con
`pipefail`, que en Linux es rojo con la línea encontrada—, y el arreglo, con su verificación en Linux
contra la pila real, está en *Lo que la CI encontró y en local no se veía — addenda*. Los carriles tal
como el run los publica, contra las líneas base de `7f676e2`:

```
Dominio y arquitectura: 862 casos (862 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.UnitTests 183, BuildingBlocks.UnitTests 215, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 154, Identidad.UnitTests 61, Api.IntegrationTests 13, Terceros.UnitTests 85,
    Arquitectura.Tests 51, Catalogo.UnitTests 78
    (845 en `7f676e2`, en 9 ensamblados: +17 — Identidad.UnitTests +3 y Arquitectura.Tests +14)

Integración (Testcontainers): 399 casos (399 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 325, y 0 en los otros siete
    (392 en `7f676e2`: +7, todos en Api.IntegrationTests)

Frontal · arranque 426/450 KiB en 3 ficheros · total servido 592/900 KiB  (los mismos en `7f676e2`)
OpenAPI: 128 operaciones, 74 rutas /api/v1/  (las mismas en `7f676e2`: el `409` nuevo es de un `PUT`
         que ya existía)
Catálogo de errores: 90 tipos, de 96 sitios de llamada  (89 de 95: +1 tipo, +1 sitio,
                     `permisos-de-rol-del-sistema`)
Humo: esquema aplicado por los cinco contextos · 12 tramos de impuesto y 15 unidades, IVA general
      vigente del 21.00 % · sesión con un testigo de 4273 caracteres · 1 empresa servida
Segundo arranque: 93 permisos tras el primero · el estado viejo con los 55 de la fase 0 y uno
      retirado, GET /api/v1/terceros/terceros → 403 · 93 tras el segundo · la misma lectura con la
      semilla fuera → 200
```

Las líneas base son las que dio el usuario sobre `7f676e2` —el `main` que abrió la rama, el cierre de la
fase 1—, y son las que publicó el run del 1.11 sobre `73d1db7`, porque entre los dos solo cambia la
documentación (`git diff --name-only 73d1db7 7f676e2` → `docs/PLAN.md`). El reparto de cada delta
**por clase** está en *Verificado — addenda 1.12 a 1.14*.

**El cero de los `act()` no lo publica el run, y se dice de dónde sale.** La cifra —**15 ficheros,
101 casos, 0 avisos**, las tres iguales que en `7f676e2`: las addenda no añaden ningún caso al frontal—
es de la batería local sobre `0d4062f`, medida con `grep -c "not wrapped in act"` sobre la salida de la
suite. El árbol de `0d4062f` solo difiere del de `f3c749e` en la documentación y en el guion del segundo
arranque, que la suite del frontal no ejecuta
(`git diff --name-only 0d4062f f3c749e` → `docs/PLAN.md` y `scripts/ci/segundo-arranque.sh`).

**Lo que hay que leer primero de las addenda.** **El segundo arranque, medido y no leído** (1.12): al
rol del sistema de una base de la fase 0 le faltaban **38** permisos del catálogo de la fase 1 y le
sobraba el retirado, y el arnés —`segundo-arranque.sh`, un paso del Humo— lo vio **rojo** en `d7e2d91`,
antes del arreglo. La salida es la **(c)** del ADR-0035: el migrador alinea el rol del sistema con el
catálogo desplegado en **cada** despliegue, con o sin la semilla, y la API ya no edita sus permisos. El
estado que construye el segundo arranque está en la decisión 2, paso a paso, y no es una versión vieja
instalada: es el primer arranque de esta, el rol devuelto por SQL a los 55 permisos de `fe7059d` más uno
retirado, y el mismo volumen levantado otra vez con las ocho `BASTION_SEMILLA_*` vacías. Y la otra mitad
de «arrancar sobre algo que ya existe»: las migraciones de los cinco contextos, una a una sobre tablas
con filas.

**Las diecisiete reglas, con su estado** (1.13): `docs/dominio/reglas-duras.md`, copiada del §6 del plan
maestro y comparada con el repositorio en los dos sentidos. Lo que **no** compara —el enunciado contra el
plan maestro, que no está en el repositorio— lo dice la tabla y se contrasta a mano en cada puerta de
fase. **Las higienes** (1.14): la línea del README vigilada contra este checklist, las variables del
despliegue comparadas en cuatro fuentes, `EsDelSistema` diciendo lo que se cumple, la raíz del
repositorio en un solo sitio, el frontal contado sin la raíz, y **dos preguntas** para la puerta de la
fase 2 —R9/R14 y R5— con su coste y sin contestar.

**Las decisiones** están en *Decisiones tomadas → addenda de la fase 1*, con el **ADR-0035**. **Las ocho
mutaciones** están en *Las ocho mutaciones de las addenda*, con la **1**, la **2** y la **4** enteras. La
1 es la que el arnés existe para cazar: el alineado que no confirma deja el primer arranque **verde** —la
semilla siembra el catálogo entero— y el migrador del segundo dice haber concedido 38 y retirado 1, y el
rol sigue igual; solo lo ve preguntarle a la base después. La 2, la columna `NOT NULL` sin valor, es
**roja con filas y verde en vacío**: en todas las bases de los tests habría pasado.

**Ninguna dependencia nueva, y la frase sale de comparar conjuntos** —con el guion que ahora fija la
convención del frontal—:

```
python scripts/dependencias-por-conjuntos.py 7f676e2 f3c749e

7f676e2: 39 packages.lock.json · 125 pares nombre/versión · 30 Project · frontal 548 entradas sin la raíz
f3c749e: 39 packages.lock.json · 125 pares nombre/versión · 30 Project · frontal 548 entradas sin la raíz
7f676e2 → f3c749e:
  pares añadidos    []
  pares retirados   []
  Project añadidos  []
  Project retirados []
  frontal añadidas  []
  frontal retiradas []
```

Las **549** de la línea base son las mismas **548 más la raíz**, así que **ninguna licencia nueva que
revisar**.

**Semillas, identificadores y secretos.** Ni un NIF, NIE, CIF o IBAN en las addenda, tampoco en
`deploy/.env.example`, que es donde apetece poner uno que parezca de verdad: sus tres líneas nuevas son
de comentario. El barrido, sobre las líneas añadidas del rango entero:

```
git diff 7f676e2..f3c749e -U0 | grep -E '^\+' | grep -v '^+++' | grep -ciE 'password|passwd|secret|api[_-]?key|token\s*[:=]|BEGIN [A-Z ]*PRIVATE|ghp_|github_pat_|AKIA[0-9A-Z]{16}|\bES[0-9]{2}[ ]?([0-9]{4}[ ]?){5}\b|\b[0-9]{8}[A-HJ-NP-TV-Z]\b|\b[A-HJNPQRSUVW][0-9]{7}[0-9A-J]\b|\b[XYZ][0-9]{7}[A-Z]\b|BASTION_SEMILLA_[A-Z_]+=\S'
  → 1
```

**Se ha leído, y no es un dato:** `# No imprime secretos: la contraseña viaja de fichero a fichero…`, el
comentario de cabecera de `scripts/ci/segundo-arranque.sh`. El segundo arranque de la CI corre sobre el
`deploy/.env` que el job Humo genera con claves aleatorias, y el humo local, con un fichero de entorno
de valores aleatorios generado para la pasada y borrado al terminar, sin tocar el `deploy/.env` de
desarrollo ni sus volúmenes.

**Commits, firmas y trailers.** `git rev-list --count 7f676e2..f3c749e` → **11**, los once firmados
(`%G?` = `G`), con autor y *committer* el usuario y **solo** sus credenciales:
`git log --format='%(trailers:only)' 7f676e2..f3c749e` no imprime **ni una línea** sobre el rango
entero, y este cierre se comprueba igual antes de empujarlo. Sin PR; la rama `feature/1.12-addendum`
—empujada, con su run— se lleva a `main` por avance rápido con este cierre dentro, y se borra en local y
en el remoto.

**Fuera de las addenda, y no «de paso»:** nada de la fase 2 —Inventario, `CodigoBarras` y su import—;
ninguna decisión sobre R5 ni sobre reabrir un ejercicio; `CLAUDE.md` §5; la destrucción al vencer del
art. 32; y la correlación buscar/alta.

**Ítem 1.11 cerrado — la importación CSV: la fila decide, el fichero escribe, y el recibo caduca:**
run **34939033897** sobre `73d1db7`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `104283219208` ✓ (22 pasos, 0 omitidos),
Frontal `104283218957` ✓ (17 pasos, 0 omitidos) y Humo `104284360891` ✓ (25 pasos, 1 omitido: el
«Diagnóstico», que lleva `if: failure()`). Humo tiene un paso más que en el 1.10: «El frontal deja
llegar a la API un cuerpo del tamaño de una importación». Los carriles tal como el run los publica,
contra las líneas base de `051449d`:

```
Dominio y arquitectura: 845 casos (845 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 215, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 85, Catalogo.UnitTests 78, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 154, Arquitectura.Tests 37, Api.IntegrationTests 13
    (748 en `051449d`, en 9 ensamblados: +97 — BuildingBlocks.UnitTests +83,
     Api.FunctionalTests +9, Arquitectura.Tests +3 y Api.IntegrationTests +2)

Integración (Testcontainers): 392 casos (392 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 318, y 0 en los otros siete
    (367 en `051449d`: +25, todos en Api.IntegrationTests)

Frontal · arranque 426/450 KiB en 3 ficheros · total servido 592/900 KiB  (418 y 577 en `051449d`)
OpenAPI: 128 operaciones, 74 rutas /api/v1/  (127 en 73: +1 operación, +1 ruta)
Catálogo de errores: 89 tipos, de 95 sitios de llamada  (80 de 86: +9 tipos, +9 sitios)
Humo: esquema aplicado por los cinco contextos · 12 tramos de impuesto y 15 unidades, IVA general
      vigente del 21.00 % · sesión con un testigo de 4273 caracteres · 1 empresa servida
```

Las líneas base son las que publicó el run del 1.10 sobre `958c339`, y valen para `051449d` porque
entre los dos solo cambia la documentación (`git diff --name-only 958c339 051449d` → `docs/PLAN.md`).
El reparto de cada delta **por clase** está en *Verificado — ítem 1.11*.

**El cero de los `act()` no lo publica el run, y se dice de dónde sale.** La cifra —**15 ficheros,
101 casos, 0 avisos**; 14 y 95 en `051449d`, y los seis casos nuevos son los de
`LaImportacionDeTerceros.test.tsx`— es de la batería local sobre `5aca832`, medida con
`grep -c "not wrapped in act"` sobre la salida de la suite. El árbol de `5aca832` solo difiere del de
`73d1db7` en la documentación y en un comentario de test
(`git diff --name-only 5aca832 73d1db7` → `docs/PLAN.md` y `ElReciboCaducaYSeBorraTests.cs`).

**Lo que hay que leer primero de este ítem.** **La fila decide y el fichero escribe** (ADR-0034 §1):
cada fila se valida y se decide por separado, **leyendo** —formato, contrato, NIF, régimen, límite,
repetición en el fichero y existencia en la base, con una sola consulta dentro de
`ViendoLoBloqueado`—, y las altas aceptadas se confirman en **un** `SaveChanges`, en la transacción del
filtro de idempotencia y con el recibo. Si el proceso cae a mitad no queda **nada** —ni filas ni
recibo—, y el reintento es la primera vez: lo prueba un disparador que revienta en mitad del `INSERT`.
**La R12 se rompe a sabiendas y con el motivo escrito:** una transacción por fila cumpliría su letra y
rompería la R10, porque el reintento vería como ya existentes las filas que él mismo dejó a medias.

**Idempotencia por fichero, y comprobada por el efecto.** La misma `Idempotency-Key` con el mismo
fichero devuelve el informe guardado **sin volver a preguntar a la base**, y un informe igual no lo
prueba —reimportar un fichero entero de filas malas daría **el mismo**—, así que el caso mira el suceso
`8401`, que solo deja la consulta de identificadores ocupados. Con otro fichero,
`409 idempotencia-cuerpo-distinto`, y el otro fichero no entra.

**El informe no repite un solo dato de la entrada:** va agrupado por **(columna, motivo)** con la lista
de líneas de Excel, y columnas y motivos los escribe Bastion, así que no hay nada que filtrar al
guardarlo en el recibo ni nada que ejecutar si se pega en una hoja. Los topes de la API, escritos
**antes del *endpoint***: 2 MiB impuestos por el lector acotado del borde —que es quien vuelca— y
5 000 filas; y nginx, que cortaba en 1 MiB por omisión sin que nadie lo hubiera mirado, sube a 3 MiB
para que el `413` con nombre lo dé la API. Y **el recibo de idempotencia caduca a
las 24 horas**, con `caduca_en` en la fila y una purga cada hora en Auditoría: cierra la nota que el
0.9 dejó abierta, y **sin el dato** que pedía, porque la tabla guarda la ficha de un tercero en cada
`201`.

**Las once decisiones** están en *Decisiones tomadas → ítem 1.11*, detrás de la 0, con el
**ADR-0034**. **Las ocho mutaciones** están en *Las ocho mutaciones del 1.11*, con la **4**, la **5** y
la **7** enteras. La 5 es la que el dialecto existe para impedir: el importe leído con la cultura
invariante no revienta nada, la fila **entra** con un límite mil veces menor. La 7 devuelve el mismo
`413`, y la cazan cuatro casos porque miden los bytes pedidos a la corriente y no el código —con la
mutación, el servidor se llevó los 67 108 864 de una corriente de 64 MiB—. Y la 8 cayó en un solo
caso y **desmintió el comentario de su clase**, que afirmaba un rojo que no se da; corregido en
`b44041e`. Lo que la batería encontró y no estaba previsto —`[Consumes]` contestando el `415` en el
enrutador, **antes de la autorización**, y `LectorAcotadoDelCuerpo.Leido` que **nunca devolvía nulo**,
así que la guarda del formateador no podía saltar— está en la decisión 8, con sus casos.

**Ninguna dependencia nueva, y la frase sale de comparar conjuntos.** Un solo `packages.lock.json` se
mueve —el de `BuildingBlocks.UnitTests`— y ningún par `nombre/versión` cambia:

```
python licencias.py 051449d 73d1db7
  051449d → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
            · package-lock.json del frontal: 548 dependencias sin la raíz
  73d1db7 → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
            · package-lock.json del frontal: 548 dependencias sin la raíz
  051449d → 73d1db7: pares añadidos [] · retirados [] · Project añadidos [] · retirados []
            · frontal añadidas [] · retiradas []

git diff --name-only 051449d 73d1db7 -- Directory.Packages.props frontend/package.json \
  frontend/package-lock.json                                                → vacío
```

De los tres `.csproj` que cambian, uno gana un `ProjectReference` —`BuildingBlocks.UnitTests →
BuildingBlocks.Application`, por el lector de CSV—, otro copia a la salida los dos CSV de Excel
(`Api.IntegrationTests`) y el tercero solo cambia un comentario (`BuildingBlocks.Contracts`), así que
**ninguna licencia nueva que revisar**.

**Semillas, identificadores y secretos.** El ítem toma el bloque de empresas **230-259** y el de
terceros **33 000 001-33 000 999**, sin pisar ninguno anterior (decisión 9), y el reparto está escrito
en el comentario de `Importaciones.cs`, que es donde lo lee el siguiente. **Los dos CSV que escribió
Excel no llevan un solo NIF:** llevan marcadores `{{NIF:n}}` que el test sustituye por
`Escenario.NifInventado`. Y el barrido, sobre las líneas añadidas del rango entero, con los CSV
dentro:

```
git diff 051449d..73d1db7 | grep '^+' | grep -cE '\b[A-HJ-NP-SUVW][0-9]{7}[0-9A-J]\b|\b[0-9]{8}[A-Z]\b|\b[XYZ][0-9]{7}[A-Z]\b|\bES[0-9]{2} ?[0-9]{4}|IBAN|-----BEGIN|[Pp]assword\s*=|secret\s*=|api[_-]?key|eyJ[A-Za-z0-9]{20}'
  → 2
```

**Las dos se han leído, y ninguna es un dato:** `67108864L`, en `docs/PLAN.md`, es la cuenta de bytes
de la mutación 7 —ocho cifras y una letra tienen la forma de un DNI—, y `"Password=",` es
`RastrosProhibidos.cs:44`, un rastro que las respuestas **no pueden** contener. Ni un NIF, NIE, CIF o
IBAN, ni una clave privada, ni un testigo JWT. El humo local usó un fichero de entorno de valores
aleatorios generado para la pasada y borrado al terminar, sin tocar el `deploy/.env` de desarrollo
ni sus volúmenes.

**Commits, firmas y trailers.** `git rev-list --count 051449d..73d1db7` → **12**, los doce firmados
(`%G?` = `G`), con autor y *committer* el usuario y **solo** sus credenciales:
`git log --format='%(trailers:only)' 051449d..73d1db7` no imprime **ni una línea** sobre el rango
entero. Sin PR; la rama `feature/1.11-importacion-csv` se llevó a `main` por fast-forward y se borró
en `73d1db743ef9ad1bd3cb3919bcf65f98438aa1ac` —solo existía en local: nunca se empujó—.

**Fuera del ítem, y no «de paso»:** `CodigoBarras` (fase 2, con su import), la conversión de divisas,
las facturas, las existencias, el precio por cliente, la importación de artículos (decisión 1), la
exportación —solo como regla escrita, la decisión 7— y traducir a `409` la carrera de unicidad del
alta.

**Ítem 1.10 cerrado — los dos cruces mutuos, y los estados que hay al otro lado de cada puerto:**
run **34833598837** sobre `958c339`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `103942313057` ✓ (22 pasos, 0 omitidos),
Frontal `103942312704` ✓ (17 pasos, 0 omitidos) y Humo `103943421447` ✓ (24 pasos, 1 omitido). Los carriles tal como el run los publica,
contra las líneas base de `d9e4a46`:

```
Dominio y arquitectura: 748 casos (748 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 85, Catalogo.UnitTests 78, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Arquitectura.Tests 34, Api.IntegrationTests 11
    (723 en `d9e4a46`, en 9 ensamblados: +25 — los 8 de `LaTarifaAsignadaTests`, los 12 de
     `ProveedoresDelArticuloTests` y los 5 de `LaMatrizDeLosPuertosDeEstadoTests`)

Integración (Testcontainers): 367 casos (367 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 293, y 0 en los otros siete
    (355 en `d9e4a46`: +12 — los 8 de `ContratoDeLosCrucesTests` y los 4 de
     `ElPuertoDeTercerosContraLaBaseTests`)

Frontal · arranque 418/450 KiB en 3 ficheros · total servido 577/900 KiB  (416 y 575 en `d9e4a46`)
OpenAPI: 127 operaciones, 73 rutas /api/v1/  (120 en 70: +7 operaciones, +3 rutas)
Catálogo de errores: 80 tipos, de 86 sitios de llamada  (75 de 81: +5 tipos, +5 sitios)
Humo: esquema aplicado por los cinco contextos · 12 tramos de impuesto y 15 unidades, IVA general
      vigente del 21.00 % · sesión con un testigo de 4236 caracteres · 1 empresa servida
```

**El cero de los `act()` no lo publica el run, y se dice de dónde sale.** El job Frontal no anota ni
el recuento de casos ni los avisos de `act()`, y su registro no se lee sin credenciales. La cifra
—**14 ficheros, 95 casos, 0 avisos**, las mismas que en `d9e4a46`— es de la batería local sobre
`3a7ba3e`, cuyo árbol solo difiere de `958c339` en la documentación
(`git diff --name-only 3a7ba3e 958c339` → `docs/PLAN.md`). Sigue siendo canal: se mide en la salida
de la suite con `grep -c "not wrapped in act"`, y **en una pasada sin competencia**, porque bajo
contención la base también da avisos (*Verificado — ítem 1.10*).

**Lo que hay que leer primero de este ítem.** Un puerto que pregunta «¿existe?» deja pasar lo que
existe y **no se puede usar**, así que los dos preguntan por el **estado**, y cada estado tiene su
caso contra PostgreSQL. **Catálogo → Terceros**, `IConsultaDeTerceros.EstadoDeAsync(id, rol)`:
`Disponible` → 201; `NoHaceEseRol` → 400 `articulo-proveedor-tercero-no-valido`; `NoExiste` → **el
mismo 400, cuerpo idéntico**, y en `NoExiste` caben a propósito tres cosas —no hay ficha, es de otra
empresa (R8), está bloqueada (R16)— porque desde fuera de Terceros tienen que ser lo mismo.
**Terceros → Catálogo**, `IConsultaDeTarifas.EstadoDeAsync(id, fecha)` con la fecha de hoy en UTC:
`RigeEnEsaFecha` → 200; `SoloResuelveLoViejo` → 409 `tercero-tarifa-no-vigente`, probado por **los dos
extremos** —acabada y todavía no empezada—; `NoExiste` → 400 `tercero-tarifa-no-encontrada`. Los dos
noes de la tarifa se distinguen y los del tercero no, y la asimetría es el art. 32: al otro lado de
una tarifa hay una lista de precios de la empresa; al otro lado de un tercero, una persona.

**`Bloqueado` fue un valor del enumerado y dejó de serlo.** La matriz nueva
—`LaMatrizDeLosPuertosDeEstadoTests`, que descubre **todo** puerto de un `Contracts` que devuelva
un enumerado, porque la del 1.7 solo veía `EstadoDeMaestro` y los dos puertos del ítem entraron con
ella en verde— pidió un caso para `IConsultaDeTerceros → Bloqueado`, y al escribirlo el puerto
contestó `NoExiste`: el filtro del art. 32 esconde la fila antes de que el `switch` la vea. Era un
valor que ningún productor produce, y se quitó con la rama muerta que lo acompañaba.

**La respuesta del art. 32 al cruce nuevo, con cuatro casos y no con una frase:** el listado de
proveedores de un artículo **esconde** al bloqueado y desbloquearlo devuelve **la misma fila**; al
alta, bloqueado, inventado, de otra empresa y solo cliente contestan **el mismo 400** sin dejar
fila; volver a añadir a un bloqueado que **ya** suministraba es ese 400 **y no un 409** —el 409 diría
«ya está» de algo que el listado calla, que es decir quién está bloqueado, y ese argumento cambió el
orden de las comprobaciones en el caso de uso—; y el puerto contesta `NoExiste` al bloqueado contra la
base, sin la API delante.

**La divisa: dos representaciones conviviendo en el mismo agregado.** El `Tercero` lleva desde este
ítem el **código ISO 4217** dentro del `Importe` de su `LimiteCredito` y un **`Guid`** al maestro
`Divisa` a través de la `Tarifa` asignada. **No se comparan** —un límite es cuánto se le fía y una
tarifa cómo se le pone precio; ningún importe nace aquí en las dos—, y está **probado por el efecto**
con un límite en USD y una tarifa en EUR en los dos órdenes, visto rojo antes con un canario en cada
lado. **No se unifican ahora, y la buena es el código ISO**: la semántica sale del código —hasta
`Divisa.Decimales` lo lee de ahí—, `Importe` y el redondeo (R6) solo conocen códigos, los formatos
fiscales hablan ISO 4217 y el código es una clave natural e inmutable. Lo que se movería es el
`Guid`, y el disparador está escrito: antes de que la primera línea de un documento convierta un
precio resuelto en un `Importe`. **Ninguna conversión**: es de `TipoCambio` y no es de la fase 1.

**Las once decisiones** están en *Decisiones tomadas → ítem 1.10*, detrás de la 0. **Las ocho
mutaciones** están en *Las ocho mutaciones del 1.10*, con la **2**, la **5** y la **7** enteras. La 7
es la que merece leerse: quitar el registro de `IConsultaDeTerceros` pone rojos 307 casos, pero por
la validación del contenedor al arrancar en Development, que es justo la inspección que el criterio
descarta; la **7b** apaga esa validación como en Production y enseña el efecto en peticiones de
verdad —500 al crear un artículo, porque el radio del fallo es el controlador entero y no el
cruce—. Y la 8 dejó un **hallazgo abierto**: la clave ajena entre esquemas escrita con
`migrationBuilder.Sql` solo la ve `EsquemaDeIdentidadTests`, por ser la única consulta sin filtro
de esquema.

**Ninguna dependencia nueva, y la frase sale de comparar conjuntos.** Doce `packages.lock.json` se
mueven y ningún par `nombre/versión` cambia:

```
python licencias.py d9e4a46 3a7ba3e
  d9e4a46 → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
            · package-lock.json del frontal: 549 entradas
  3a7ba3e → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
            · package-lock.json del frontal: 549 entradas
  d9e4a46 → 3a7ba3e: pares añadidos [] · retirados [] · Project añadidos [] · retirados []

git diff --name-only d9e4a46 958c339 -- Directory.Packages.props frontend/package.json \
  frontend/package-lock.json                                                → vacío
```

Las tres líneas nuevas de los `.csproj` son `ProjectReference` —las dos de los cruces y la de
`Terceros.UnitTests → Terceros.Application`—, así que **ninguna licencia nueva que revisar**.

**Semillas, identificadores y secretos.** El ítem toma el bloque de empresas **200-215** y el de
terceros **32 000 001-32 000 022**, sin pisar ninguno anterior, y el reparto está escrito en el
comentario de clase de `ContratoDeLosCrucesTests`, que es donde lo lee el siguiente. Todos los NIF
salen de `Escenario.NifInventado`. Y el barrido, sobre las líneas añadidas del rango entero,
ficheros generados incluidos:

```
git diff d9e4a46..958c339 | grep '^+' | grep -cE '\b[A-HJ-NP-SUVW][0-9]{7}[0-9A-J]\b|\b[0-9]{8}[A-Z]\b|\b[XYZ][0-9]{7}[A-Z]\b|\bES[0-9]{2} ?[0-9]{4}|IBAN|-----BEGIN|[Pp]assword\s*=|secret\s*=|api[_-]?key|eyJ[A-Za-z0-9]{20}'
  → 0
```

NIF, NIE y CIF con su forma, IBAN español, claves privadas, contraseñas en asignación y testigos
JWT: **ninguna coincidencia**. El humo local usó un fichero de entorno de valores aleatorios fuera
del repositorio, borrado al terminar.

**Commits, firmas y trailers.** `git rev-list --count d9e4a46..958c339` → **7**, los siete firmados
(`%G?` = `G`), con autor y *committer* el usuario y **solo** sus credenciales:
`git log --format='%(trailers:only)' d9e4a46..958c339` no imprime **ni una línea** sobre el rango
entero. Sin PR; la rama `feature/1.10-cruces` se llevó a `main` por fast-forward y se borró en
`958c33982410f8be1f195a651b047fe9e7b17358` —solo existía en local: nunca se empujó—.

**Fuera del ítem, y no «de paso»:** la importación CSV (**1.11**), `CodigoBarras` (fase 2, con su
import), la conversión de divisas, las facturas y las existencias. El precio por cliente, **solo
como decisión escrita** —la 7—: el sitio es Ventas, que leerá la tarifa asignada por
`Terceros.Contracts` y resolverá con la fecha **del documento**.

**Ítem 1.9 cerrado — las tarifas, y la trampa que «más específica» no nombra:**
run **34599130092** sobre `eadb7b9`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `103261880142` ✓ (22 pasos, 0 omitidos),
Frontal `103261880427` ✓ (17 pasos, 0 omitidos) y Humo `103263491090` ✓ (24 pasos, 1 omitido).
Los tres carriles tal como el run los publica:

```
Dominio y arquitectura: 723 casos (723 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 77, Catalogo.UnitTests 66, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Arquitectura.Tests 34, Api.IntegrationTests 6
    (682 en el 1.8 sobre `8851c5f`, en 9 ensamblados: +41 casos, todos en Catalogo.UnitTests)

Integración (Testcontainers): 355 casos (355 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 281, y 0 en los otros siete
    (348 en el 1.8 sobre `8851c5f`: +7 casos)

Frontal: 14 ficheros de prueba, 95 casos, 0 avisos de `act()`
  (13 y 81 en el 1.8; el canal sigue a cero, que es lo que hace que un solo aviso sea un hallazgo)

Frontal · arranque 416/450 KiB en 3 ficheros · total servido 575/900 KiB  (410 y 564 en el 1.8)
OpenAPI: 120 operaciones en 70 rutas  (110 en 64: +10 operaciones, +6 rutas)
Catálogo de errores: 75 tipos, de 81 sitios de llamada  (63 de 68: +12 tipos, +13 sitios)
```

**Lo que hay que leer primero de este ítem.** «Gana la más específica» **no basta**, y el criterio
lo dice por su nombre al pedir la precedencia completa. Como la categoría es jerárquica, un artículo
casa a la vez con una línea de su propia categoría y con otra de la madre, y **las dos son «por
categoría»**: sin la regla escrita, el desempate lo decide el orden en que salgan las filas, que es
un no-determinismo silencioso —el mismo artículo, dos precios, según el plan de ejecución del día—.
Y la trampa está un paso más allá: **«más cercana» y «más profunda» no son lo mismo**. Una
implementación que ordene por profundidad descendente pasa el caso fácil de dos líneas en la misma
rama y devuelve la equivocada en cuanto hay una rama **hermana** más honda. La defensa no es una
comprobación sino el tipo: `LineaCandidata.Nivel` es el salto **desde el artículo**, así que lo que
no es antepasado no llega hasta la regla. Es la **mutación 1**, y para escribirla hay que forzar el
modelo.

**Las nueve decisiones del ítem están en *Decisiones tomadas → ítem 1.9*, y dos de ellas el enunciado
las dejaba abiertas a propósito.** La **divisa cruzada se acepta** —ni se rechaza ni se convierte—,
que es la respuesta **contraria** a la del `LimiteCredito` del 1.6 y con motivo: un límite de crédito
**sin divisa** no era legítimo, mientras que una tarifa de exportación en dólares es un caso real de
una empresa que factura en euros. Lo que hace segura la decisión es que el precio resuelto viaja
**siempre** con su divisa pegada; y lo que impide revertirla por descuido es que el caso de uso **no
tiene el puerto** para preguntar la divisa de la empresa —a la de la tarifa solo le pregunta el
estado—, así que añadir la comparación exigiría añadir antes la dependencia. La **frontera de los
tramos cae hacia arriba**: `[desde, siguiente)`, de modo que pedir exactamente 100 con tramos en 0 y
100 aplica el de 100, que es lo que significa una tabla que dice «a partir de 100»; y **el hueco se
cierra por delante** —primer tramo en cero, sin `CantidadDesde` repetida en el mismo destino—, porque
un hueco es peor que un solape: el solape da dos respuestas y se nota, el hueco da **ninguna** y sale
por el mismo `type` que «esta tarifa no cubre este artículo», así que una tabla mal escrita se
arregla en el sitio equivocado.

**Y el ítem tiene un carril nuevo por una razón que no es de gusto.** `ContratoDeTarifasTests` existe
porque hay dos cosas que el carril rápido **no puede** ejercer: la restricción de exclusión, que es
DDL —o está en la base o no está—, y el SQL que alimenta la precedencia. La restricción se ejerce
escribiendo **sin pasar por el caso de uso**, porque la comprobación previa cubre al cliente educado
y no a dos peticiones simultáneas, que es el caso real. Y `btree_gist` se comprueba **por el efecto y
contra el compose**: el caso lee el `image:` del servicio `postgres` de `deploy/docker-compose.yml`,
lo compara con la imagen del contenedor de pruebas, y en esa imagen pregunta a `pg_extension` y a
`pg_constraint`. El contenedor no ha corrido nada más que migraciones, así que lo que hay en él lo
puso una migración y no una mano.

**Las ocho mutaciones**, cada una sobre árbol verde, aplicada, compilada, corrida y revertida
restaurando copia, están en *Las ocho mutaciones del 1.9*, con la **1**, la **5** y la **8** enteras.
La 8 es la que merece leerse: **no cambia ningún resultado** —devuelve el precio correcto en los
cinco casos y ninguna de sus consultas es lenta por separado—; lo único que cambia es el número de
viajes, y que ahora crece con la profundidad.

**Ninguna dependencia nueva, y la frase sale del diff.** Conjuntos de paquetes idénticos a los de
`8851c5f`:

```
git diff 8851c5f..eadb7b9 -- '*.csproj' 'Directory.Packages.props' \
  'frontend/package.json' 'frontend/package-lock.json'   → vacío
```

**Licencias: cero movimiento, y la frase sale de comparar CONJUNTOS, no de mirar el diff.** Un
`packages.lock.json` puede cambiar sin que cambie nada que se resuelva —quién pide un paquete no es
qué se resuelve—, así que lo que se compara es el conjunto de pares `nombre/versión` de **todos** los
ficheros de bloqueo a los dos lados:

```
git ls-tree -r --name-only <ref> | grep 'packages.lock.json$'   (y de cada uno, sus `resolved`)
  8851c5f → 125 pares nombre/versión distintos
  eadb7b9 → 125   ·  diff de los dos conjuntos: IDÉNTICOS

git show <ref>:frontend/package-lock.json  (entradas de `packages`)
  8851c5f → 548   ·   eadb7b9 → 548
```

Cero paquetes de terceros añadidos y cero retirados en los dos ecosistemas, así que **ninguna
licencia nueva que revisar**. `btree_gist`, que es lo único que este ítem incorpora de fuera del
código, **no es una dependencia de terceros**: viene con PostgreSQL, cuya licencia —permisiva estilo
BSD— ya estaba declarada desde el 0.15, que es cuando Organización la creó por primera vez.

**Commits, firmas y trailers.** `git rev-list --count 8851c5f..eadb7b9` → **5**, los cinco firmados
(`%G?` = `G`) y los cinco con **solo** las credenciales del usuario:
`git log --format='%(trailers:only)' 8851c5f..eadb7b9` no imprime **ni una línea** sobre el rango
entero. Sin PR; la rama `feature/1.9-tarifas` se llevó a `main` por fast-forward y se borró en
`eadb7b9949d6e6c07f9bf27a75c2fb13c9c3caa6`.

**Fuera del ítem, y no «de paso»:** `Tercero.TarifaAsignada` y `ArticuloProveedor` (ítem **1.10**,
que es propio porque la dependencia es **mutua**), la importación CSV (**1.11**) y `CodigoBarras`
(fase 2, con su import). Ninguna conversión de divisa, ninguna factura y ninguna existencia: la
tarifa dice cuánto vale algo, y quién lo compra o cuánto queda es de otros módulos.

**Ítem 1.8 cerrado — el catálogo, y la afirmación que su propio escenario no ejercía:**
run **34424407829** sobre `f90dbed`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `102706467191` ✓ (22 pasos, 0 omitidos),
Frontal `102706467394` ✓ (17 pasos, 0 omitidos) y Humo `102707539606` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 682 casos (682 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Catalogo.UnitTests 25, Arquitectura.Tests 34, Api.IntegrationTests 6
    (651 en el 1.7 sobre `4960922`, en 8 ensamblados: +31 casos y un ensamblado nuevo)

Integración (Testcontainers): 348 casos (348 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 274, y 0 en los otros siete
    (334 en el 1.7 sobre `4960922`: +14 casos)

Frontal: 13 ficheros de prueba, 81 casos, 0 avisos de `act()`
         (109 avisos en 7 ficheros sobre `4960922`: el fondo entero, a cero)
         arranque 410/450 KiB en 3 ficheros · total servido 564/900 KiB  (403/549 en el 1.7)

OpenAPI:  110 operaciones en 64 rutas /api/v1/  (102 en 60 rutas sobre `4960922`)
Catálogo de `type`: 63 tipos, de 68 sitios de llamada  (51 de 56 sobre `4960922`)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 5, Identidad 3, Terceros 2, Catálogo 1
Censo de escrituras: 110 acciones, 72 cambian estado — 40 `If-Match`, 15 `Idempotency-Key`,
                     17 exentas  (102/68/38·13·17 sobre `4960922`)
Artefactos: test-results con 9 .trx y 682 casos (dominio) y 9 con 348 (integración)
            bastion-web-dist con 26 ficheros, 12 .js y 1 .css
Humo: el migrador aplica los CINCO contextos —Auditoría, Organización, Identidad, Terceros y
      Catálogo—, la cuenta sembrada inicia sesión (testigo de 3819 caracteres), el entorno
      desplegado sirve 1 empresa y las semillas cargan 12 tramos de impuesto y 15 unidades
```

> **Toda cifra de «antes y después» de arriba nombra sus dos commits**: el «antes» es `4960922`, que
> es donde el 1.7 dejó las líneas base, y el «después» es `c87a6d2`, que es donde se midieron todas
> en local. Entre `c87a6d2` y `f90dbed` —el commit del run— cambia **un solo fichero,
> `.github/workflows/ci.yml`**, así que ninguna de las cifras se mueve, y el propio run las vuelve a
> publicar idénticas. Las dos medidas de integración se tomaron **con Docker arrancado** —el carril
> corrió entero, 348 de 348— y las del carril rápido con los dos `recuento-de-tests.sh` del workflow,
> que son los que deciden el desenlace del paso y no `dotnet test`.

**Y el ítem destapó un guardián que se había quedado corto dos módulos atrás.** El paso del humo
comprueba **por nombre** que sobre una base recién creada cada contexto con persistencia se migra —por
nombre y no por cuenta, para que el que falte se pueda nombrar—, y su lista seguía siendo
`AuditoriaDbContext, OrganizacionDbContext, IdentidadDbContext` mientras `MigradorDeArranque` había
pasado a **cinco**: Terceros entró en el 1.5 y Catálogo en el 1.8. No fallaba nada; lo que pasaba es
que durante dos módulos **quitar del migrador el contexto de Terceros o el de Catálogo habría dejado
el humo en verde y el contenedor sin su esquema**. Es la misma familia que las tablas del censo y que
la aserción de la mutación 2: una lista escrita a mano solo sabe lo que alguien se acordó de escribir.
Se completa en `f90dbed`, con su propio modo de fallo escrito al lado, y **comprobado por el efecto
antes de tocarlo**: `docker compose -f deploy/docker-compose.yml up --build migraciones` sobre una
base recién creada emite `EsquemaMigrado` para los cinco y el contenedor sale con código 0.

**Las nueve decisiones del ítem están en *Decisiones tomadas → ítem 1.8*, y dos de ellas el enunciado
las pedía nombradas.** El **modelo del árbol** es una **lista de adyacencia** —`PadreId` y nada más—
elegida por lo que este módulo consulta de verdad: el filtro del listado es `WHERE categoria_id = ?`,
una igualdad sobre columna indexada, y el árbol entero de una empresa cabe en una página que el
frontal recompone. La *closure table* cobra su precio en la escritura —mover una rama reescribe las
filas de cierre de todo su subárbol— por una consulta que hoy no se hace; y el día que el subárbol
haga falta (el candidato es la precedencia de tarifas del 1.9) entran un `WITH RECURSIVE` o una
*closure table* **derivada**, ninguna de las dos obliga a migrar el modelo. La consecuencia de la
elección **se dice en pantalla** en vez de esconderse: el filtro acota por la categoría dicha y no
por lo que cuelga de ella, y el mensaje de «ninguno en esta categoría» lo avisa con esas palabras.
La **cota del ascenso** es `Categoria.ProfundidadMaxima = 10` —diez niveles por debajo de la raíz, un
árbol de once— con dos motivos: a más profundidad lo que hay ya no es una clasificación sino un
atributo disfrazado, y sobre todo, la cota es **lo único que convierte un cuelgue en un error** —el
ascenso gasta una consulta por nivel y sobre datos ya cíclicos, sin cota, no falla: gira—.

**Las ocho mutaciones**, cada una sobre árbol verde, aplicada, compilada, corrida y revertida
restaurando una copia. Al terminar, `git status --porcelain` **vacío** y `grep -rn "MUTACION" src
tests` y `grep -rn MUTACION frontend/src` **sin resultados**, los tres comprobados:

| # | Mutación | Dónde | Línea base | Qué se puso rojo |
|---|---|---|---|---|
| 1 | La casilla de `SoloResuelveLoViejo` de la unidad devuelve `Correcto()`: **el alta pasa con una unidad retirada** | `ElMaestroSeOfreceParaLoNuevo.cs` | `9f8ff56` | **Rojo ×2** en `Catalogo.UnitTests` |
| 2 | Se **revalida el maestro que nadie ha tocado** al modificar (fuera la condición `!=`) | `ModificarArticulo.cs` | `9f8ff56` (unidad) · `1eab397` (HTTP) | **Rojo ×1 + ×1** |
| 3 | El ciclo se comprueba **solo en el alta** (fuera la llamada de la modificación) | `ModificarCategoria.cs` | `1eab397` | **Rojo ×2 + ×2** |
| 4 | El **caso degenerado** —padre igual a sí misma— tratado aparte y dejado pasar | `ElArbolSigueSiendoUnArbol.cs` | `1eab397` | **Rojo ×2**: `ElArbolSigueSiendoUnArbolTests.Una_categoria_no_puede_ser_su_propia_madre` y `ElCicloSeCompruebaEnLasDosPuertasTests.Una_categoria_no_puede_pasar_a_colgar_de_si_misma` — uno por puerta |
| 5 | El **ascenso pierde la cota** (`for … <= ProfundidadMaxima` → `while (true)`) | `ElArbolSigueSiendoUnArbol.cs` | `1eab397` | **Rojo ×3, y el del ciclo en 1 ms**: `Un_ciclo_YA_GUARDADO_no_deja_el_recorrido_dando_vueltas` con «*el recorrido de padres ha pedido 101 eslabones, más de los 100 que este doble admite […] lo que hay es un ascenso SIN COTA sobre datos que ya tienen un ciclo*», más `Colgar_un_nivel_mas_abajo_se_rechaza_por_profundidad` y `El_alta_demasiado_honda_se_rechaza_tambien`, que se quedan sin rechazo. **El caso lo demuestra; no cuelga la suite** |
| 6 | El alta **guarda la unidad sin preguntarle a su puerto** | `CrearArticulo.cs` | `1eab397` | **Rojo ×4** en `LaCasillaDeLaRetiradaTests`: las tres casillas de la unidad más `Un_estado_que_esta_casilla_no_conoce_lanza_en_vez_de_dejar_pasar`. Y **`LosIdentificadoresAjenosTests` sigue en verde**, que es lo correcto y hay que decirlo: esa regla vigila que el cruce esté **declarado con su puerto**, no que la respuesta del puerto **decida**. Las dos mitades son de dos sitios distintos |
| 7 | `CatalogoDbContext` **deja de heredar de `ContextoDeModulo`** y se cae del universo descubierto | `CatalogoDbContext.cs` | `1eab397` | **Rojo ×4** en `Api.FunctionalTests`, con `El_universo_de_modelos_es_el_declarado` diciendo lo que encontró: `"ContextoDeLaBandeja"` donde esperaba los seis. Y con él, dos reglas de modelo y —lo que más importa— **el arnés** `ElCatalogoNoGuardaDatosDeNadieTests.El_barrido_ve_catalogo_y_sabe_reconocer_un_dato_personal`: sin ese arnés, las reglas de «cada entidad declara…» habrían seguido verdes mirando cinco módulos de seis |
| 8 | `UnidadBaseId` pasa a ser **clave ajena a `organizacion.unidades_de_medida`** | `CatalogoDbContext.cs` | `1eab397` | **Rojo ×3**, con `Ninguna_clave_foranea_cruza_de_esquema` nombrándola entera: `["Articulo.fk_articulos_unidades_de_medida_unidad_base_id: catalogo -> organizacion"]`. Y en el **carril rápido**, sin Docker: el modelo de EF sabe a qué esquema va cada tabla antes de que exista ninguna |

**La 1, la 2 y la 3, enteras.**

**Mutación 1 — el alta acepta una unidad retirada.** Cambiar la casilla de `SoloResuelveLoViejo` por
`Resultado.Correcto()` pone rojos **dos** casos, y el segundo es el que importa:
`Con_la_unidad_solo_resolviendo_lo_viejo_el_alta_se_rechaza`, que es el directo, y
`Con_la_unidad_inexistente_el_alta_se_rechaza_con_OTRO_error`, que compara los dos `type` y se cae
porque ya no hay dos. Esa segunda es la que sostiene el ADR-0030 aquí: quien teclea un identificador
inventado tiene que corregir el identificador y quien apunta a uno retirado tiene que elegir otra
unidad, son dos arreglos distintos, y con un solo `type` el frontal escribiría un texto que sirve
para uno y despista en el otro. La misma mutación por HTTP la caza
`ContratoDeCatalogoTests.Una_unidad_RETIRADA_no_vale_para_un_alta_y_el_articulo_que_ya_la_usa_sigue_resolviendola`,
que es el caso de las dos mitades contra PostgreSQL.

**Mutación 2 — el artículo que ya usaba el maestro deja de poder corregirse, y aquí la tanda encontró
un agujero de verdad.** Quitar la condición `!=` de `ModificarArticulo` —o sea revalidar el maestro
que nadie ha tocado— puso rojo `LaCasillaDeLaRetiradaTests.El_impuesto_que_no_se_toca_no_se_vuelve_a_preguntar`
y **dejó verde** el caso por HTTP que afirma lo mismo,
`Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_pero_cambiarlo_a_uno_caducado_SI_se_rechaza`.
El motivo: el artículo llegaba a la modificación con su tramo **todavía vigente**, y con eso
revalidarlo y no revalidarlo se ven exactamente igual desde fuera. La aserción afirmaba una decisión
que su propio escenario no ejercía — la misma familia que el «antes» mal medido de los `act()` del
1.5 y que las tablas del censo. Arreglado en `1eab397`: el tramo del artículo se cierra con
`POST /impuestos/{id}/cierre` y el último día en ayer, antes de la modificación que no lo toca. Con
el caso ya endurecido, la mutación lo pone en **409** y el caso en rojo.

> **Y la mitad de la UNIDAD no se puede romper desde `ModificarArticulo`, que es la forma más fuerte
> de la garantía y hay que decirla como es.** La unidad base no está en `ModificarArticuloDto` ni en
> `Articulo.Modificar`, y el puerto de unidades **no está inyectado** en ese caso de uso: unificar la
> comprobación entre el alta y la modificación no es una línea, es cambiar la firma. Por eso la
> mutación se ejerce por el maestro que **sí** se puede cambiar —el impuesto—, que es el mismo
> defecto por el único sitio por donde entra. La mitad de la unidad la sostiene, contra PostgreSQL,
> el caso de las dos mitades del contrato.

**Mutación 3 — el ciclo comprobado solo en el alta.** Quitar la llamada a
`ElArbolSigueSiendoUnArbol.ComprobarAsync` de `ModificarCategoria` pone rojos **dos casos en cada
carril**: en el rápido, `ElCicloSeCompruebaEnLasDosPuertasTests.Mover_una_rama_debajo_de_su_propia_descendencia_se_rechaza`
y `.Una_categoria_no_puede_pasar_a_colgar_de_si_misma`; por HTTP y contra PostgreSQL,
`ContratoDeCatalogoTests.Mover_una_categoria_debajo_de_su_propia_descendencia_es_409_con_type_categoria_ciclo`
y `.Una_categoria_no_puede_colgar_de_si_misma_ni_al_crearla_ni_al_moverla`. **Y el alta se queda
entera en verde**, que es exactamente el punto: en el alta la rama del ciclo es estructuralmente
inalcanzable —la categoría que nace todavía no está en el árbol—, así que una comprobación que solo
mirase ahí dejaría pasar **el único camino por el que un ciclo entra**, y lo dejaría pasar sin un
solo rojo. Es la misma forma del hueco que el 1.7 le encontró a la inversa, por su otra cara.

**Lo que el módulo nuevo tuvo que descubrirse solo, y se descubrió.** Ninguna de las reglas
transversales necesitó que se le apuntara Catálogo a mano: las cinco que recorren el modelo entero
—inquilinato, auditoría, claves, fechas y la de claves ajenas entre esquemas— lo ven por el universo
descubierto de `LosModelosDeCadaModulo`, que es «toda clase concreta que hereda de
`ContextoDeModulo`»; la matriz puerto × estado del 1.7 lo ve porque descubre las interfaces del
`Contracts` que devuelven `Task<EstadoDeMaestro>`; y los tres universos del 1.3 y el 1.4 —fronteras,
censos de carril y rutas del frontal— comparan sus listas contra el disco en los dos sentidos. Lo
único que se escribe a mano es el **censo de casos de `Api.FunctionalTests` y `Api.IntegrationTests`**
y la lista `Declarados`, y las dos son a propósito: son el arnés que impide que un descubrimiento que
deje de encontrar a nadie salga verde recorriendo una lista vacía. La mutación 7 es la prueba de que
ese arnés funciona.

**Ninguna dependencia nueva, y la frase sale del diff.** Conjuntos de paquetes **resueltos** de todos
los `packages.lock.json` a los dos lados: **150 en `main` y 155 en la rama**, y las cinco de
diferencia son `bastion.catalogo.domain`, `.contracts`, `.application`, `.endpoints` e
`.infrastructure`, las cinco con `"type": "Project"` — proyectos de este repositorio, no paquetes.
`Directory.Packages.props` no se toca, y `git diff --stat main..HEAD -- frontend/package.json
frontend/package-lock.json` no devuelve **ni una línea**. **No hay licencias que comprobar porque no
hay paquete de terceros que comprobar.**

**Commits, firmas y trailers.** `git rev-list --count main..HEAD` → **10** sobre `f90dbed`, que es el
commit del run; **11** contando el que escribe este bloque. Todos con `%G?` = `G`. Y el comprobante
que el ítem pedía, `git show -s --format='%(trailers:only)'` recorrido sobre el rango entero:
**ninguno devuelve nada** — ni un trailer, ni de sesión ni de prosa. El aviso de `SIGNING_KEY_B64` que
suelta el enganche de arranque es un falso positivo comprobado: `git config --global commit.gpgsign`
es `true` y las firmas lo confirman.

**Fuera del ítem, y no «de paso»:** `CodigoBarras` (fase 2, con su import), las tarifas (1.9), los dos
cruces mutuos incluido `ArticuloProveedor` (1.10), la importación CSV (1.11). Nada de existencias ni
de movimientos.

**Ítem 1.7 cerrado — la retirada, las dos conversiones, y la lección por cuarta vez:**
run **34177571670** sobre `dd89e9f`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `101909932819` ✓ (22 pasos, 0 omitidos),
Frontal `101909932714` ✓ (17 pasos, 0 omitidos) y Humo `101910637516` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 651 casos (651 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Organizacion.IntegrationTests 21,
    Api.FunctionalTests 140, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Arquitectura.Tests 34, Api.IntegrationTests 6
    (630 en el 1.6, los mismos 8 ensamblados: +21 casos)

Integración (Testcontainers): 334 casos (334 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 260, y 0 en los otros seis
    (318 en el 1.6: +16 casos, y esta vez los 334 se ejercieron TAMBIÉN en local)

Artefacto test-results: 8 ficheros .trx con 651 casos (dominio) y 8 con 334 (integración)

Frontal: arranque 403/450 KiB en 3 ficheros · total servido 549/900 KiB  (548 en el 1.6)
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  102 operaciones, 60 rutas /api/v1/, y el fichero del artefacto idéntico al del
          repositorio  (93 operaciones y 55 rutas en el 1.6)
Catálogo de `type`: 51 tipos, de 56 sitios de llamada  (49 de 54 en el 1.6)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 5, Identidad 3, Terceros 2
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 3601
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

> **El run de arriba es sobre `dd89e9f`, y a `main` va `f9b5825`: hay que decirlo y hay que
> justificarlo.** `f9b5825` es el commit que escribe este mismo bloque, así que no puede citar su
> propio run. Entre los dos, `git diff --stat dd89e9f f9b5825` da **un solo fichero, `docs/PLAN.md`**:
> ni una línea de código, de test, de contrato ni de migración. Y `f9b5825` tiene su propio run en
> verde, el **34178017980**, también con los 3 jobs contados en él — Backend `101911192453` ✓ (22
> pasos, 0 omitidos), Frontal `101911192553` ✓ (17, 0) y Humo `101911925167` ✓ (24, 1 omitido)—. Las
> cifras de arriba valen para el árbol que se lleva a `main` porque el código que las produce es
> idéntico, y la comprobación que lo sostiene está escrita aquí para que se pueda repetir. La
> alternativa —dar el run sin decir sobre qué árbol se midió— es justo lo que la regla del 1.5
> prohíbe.

**La lección, por cuarta vez, y la primera en la que se vio ANTES de escribir el arreglo.** Las tres
anteriores se descubrieron arreglando otra cosa; esta se buscó a propósito, porque el enunciado del
ítem ya la traía: *una obligación transversal enumerada a mano es ciega a lo que llegue después, y en
verde no la mira nadie*. Aquí la obligación era un **enumerado de tres valores** cuyo tercero
—`SoloResuelveLoViejo`— era inalcanzable para dos de sus tres productores, con el comentario de los
dos diciendo «la tercera llega con la retirada» y **nada** comprobando que llegara. La regla se
escribió primero, se commiteó **en rojo** (`cf46416`) nombrando las dos casillas, y se puso verde con
la retirada. Es la diferencia entre arreglar un defecto y **dejar puesto lo que lo habría avisado**.

**Y la forma que se lleva el ítem es la de la matriz, no la de la retirada.** `EstadoDeMaestro` es
una lista cerrada; los puertos que la producen, un conjunto abierto. La regla descubre los dos lados
—los valores del enumerado y las interfaces del `Contracts` que devuelven `Task<EstadoDeMaestro>`— y
exige que cada casilla del producto tenga un caso que la afirme, con la cobertura marcada **sobre el
caso** y no en una lista aparte: borrar el caso borra el cubrimiento, y la matriz se pone roja por la
casilla que se quedó sin nadie. Un puerto nuevo entra con sus tres casillas por cubrir sin que nadie
lo apunte. Es la misma pregunta del 1.6 —¿la lista cerrada y el conjunto que enumera se comparan
enteros?— por su otra cara: allí **faltaban** valores, aquí **sobraba** uno.

**Lo que este ítem añade al catálogo de trampas, y no estaba en el enunciado.** Con el `GET` por
identificador contestando 404 a una fila retirada —la mutación 3—, cae también **reincorporar**: la
puerta de vuelta necesita el `ETag`, y el `ETag` sale del `GET` que acaba de decir que no existe. En
cuatro maestros de instalación (R8), donde retirar por error deja sin esa fila a **todas** las
empresas, tratar la retirada como un bloqueo no solo esconde el dato: **convierte un error reversible
en uno permanente**. La línea que separa las dos cosas no es una cortesía con las facturas viejas —es
lo que sostiene la marcha atrás.

**Y una decisión de sitio que costó más ver que las tres del ADR.** La desigualdad de la inversa y la
negativa a encadenar tenían su **único** testigo detrás de Testcontainers. Ninguna de las dos abre una
conexión: son aritmética y una rama. Con Docker parado, cambiar el `≤` por un `<` compilaba, salía
verde, y el aviso llegaba en el *runner*. **Un caso frontera al que solo se llega levantando un
contenedor es un caso frontera que nadie ejerce mientras escribe el cambio que lo rompe** — y por eso
el testigo bajó al carril rápido, como segunda excepción declarada de `Organizacion.IntegrationTests`.

**Ítem 1.7 — la retirada y las dos conversiones.** El ADR-0023 entero, que llevaba escrito y sin
implementar desde la puerta de clarificación de la fase 1, y **antes** de que Catálogo referencie
`UnidadMedida`: ese era el disparador que el propio ADR dejó puesto.

**La lección del ítem, por cuarta vez, y esta vez se vio antes de escribir el arreglo.** Una
obligación transversal enumerada a mano es ciega a lo que llegue después, y en verde no la mira
nadie. Aquí la obligación era `EstadoDeMaestro`: tres valores, y `SoloResuelveLoViejo` lo contestaba
**solo** `ConsultaDeImpuestos`. Divisas y unidades no podían contestarlo —el comentario de las dos
lo decía, «la tercera llega con la retirada», sin que nada comprobara que llegaba—, así que la lista
cerrada tenía un valor que para dos de sus tres productores era inalcanzable. La regla que lo caza
—`LaMatrizDePuertoYEstadoTests`— se escribió **primero y se commiteó en rojo** (`cf46416`), con las
dos casillas nombradas, y se puso verde con la retirada.

**La forma de esa regla es lo que se lleva el ítem.** Los dos lados se descubren y ninguno es una
lista: los **valores**, del enumerado; los **puertos**, de lo que el `Contracts` publica —toda
interfaz con un método que devuelva `Task<EstadoDeMaestro>`—; y las **casillas cubiertas**, de los
`[CubreEstadoDeMaestro]` puestos **sobre el caso que las afirma**, no en una lista aparte. Un puerto
nuevo entra solo, con sus tres casillas por cubrir; un caso borrado se lleva su casilla por delante y
la matriz se pone roja. Y la matriz se afirma **no vacía** antes de compararla, que es la rendija por
la que un cero contra cero sale verde.

**La retirada, en los cuatro maestros de instalación.** `Divisa`, `TipoCambio`, `UnidadMedida` y
`ConversionUM` implementan `IRetirable` —la marca que hace de lista, igual que `IBloqueable`—. El
comportamiento es el literal del ADR: no se ofrece para operaciones nuevas, sigue resolviendo para lo
que ya apunta a ella; el `GET` de la colección la excluye por omisión y el `GET` por identificador
**la sigue devolviendo**. Y **ningún `DELETE` de la fila, para ninguno de los cuatro, nunca**: la
prohibición la vigila `NingunMaestroRetirableSeBorra`, que descubre los dos universos —las entidades
marcadas en los ensamblados de dominio del disco, y los recursos cuyo listado devuelve una página con
`Retirada`— y compara sus tamaños antes de recorrerlos.

**La exclusión vive en UN sitio.** `Paginador.SinLasRetiradas` monta la expresión `!fila.EstaRetirada`
cuando el elemento es `IRetirable` y no se han pedido las retiradas. No hay cuatro repositorios
acordándose: hay una consulta, y un quinto maestro retirable la hereda sin que nadie lo apunte.

> **Decisión A — cómo se ven las retiradas: un parámetro del listado de siempre,
> `?retiradas=true`, y no un extremo aparte.** Se decide **por contraste con el ADR-0027**, que es
> lo que impide copiarlo por parecido: lo que hace del listado de lo bloqueado un extremo separado,
> nominativo y trazado, **no** es que las filas estén escondidas — es que expone **datos del
> artículo 32**. Una divisa retirada no tiene datos de nadie: verla no es un acceso reservado, es
> mirar el maestro. Pasa por el contrato de listado del 1.3, y `retiradas` **no es un criterio
> sensible** —no lleva identificador, ni nombre, ni nada de una persona—, así que puede viajar en la
> URL; `NingunCriterioSensibleViajaEnLaUrl` declara ahora **cinco** nombres (`page`, `q`,
> `retiradas`, `size`, `sort`) con ese motivo escrito en el propio caso.

> **Decisión B — `TipoCambio` y `ConversionUM` no tienen puerto de consumo, y hay que decirlo.**
> `EstadoDeMaestro` lo contestan `IConsultaDeDivisas`, `IConsultaDeUnidadesDeMedida` e
> `IConsultaDeImpuestos`; los otros dos maestros no exponen puerto para otros módulos porque hoy no
> los consume nadie. **Esto no convierte su retirada en vacuidad del ADR-0020:** es observable por
> sus propios `GET` —el de la colección deja de ofrecerlas y el del identificador las sigue
> devolviendo—, y eso es un camino real, ejercido por HTTP contra PostgreSQL. Pero la frase queda
> escrita, porque el día que Catálogo o Facturación pidan un puerto para conversiones, la matriz
> puerto × estado les exigirá sus tres casillas sin que nadie tenga que acordarse.

**La inversa plausible (decisión 2 del ADR).** Con `f` y `g` ya redondeados a seis decimales,
`|f·g − 1| ≤ 5·10⁻⁷·(f + g)`. En la **capa de aplicación** y al escribir, no como invariante de
dominio: la regla relaciona **dos instancias distintas** del agregado, y un invariante que cargara
otro agregado sería justo la grieta que la R12 cierra. El fallo es un error de negocio con nombre
—`conversion-um-inversa-implausible`, 409—, no una excepción (ADR-0004). Y la tolerancia **se
calcula**: `new decimal(5, 0, 0, false, DecimalesDelFactor + 1)`, para que no queden dos sitios
diciendo lo mismo sin nada que los ate.

**Se comprueba en el alta Y en la modificación**, y lo segundo no es un extra: si se da de alta
`A→B`, luego `B→A`, y después alguien **modifica** la primera, el par vuelve a poder contradecirse.
Un test que solo ejerza el alta deja pasar exactamente el camino por el que se rompe, y lo deja pasar
en verde. La mutación 5 es esa.

> **Decisión C — una fila retirada SIGUE restringiendo a su inversa.** «Sigue resolviendo» significa
> que sigue siendo verdad, así que sigue acotando. Va escrita y no puesta por omisión, porque la
> alternativa tiene una consecuencia concreta: si retirar una fila la sacara de la comprobación,
> **retirar el sentido incómodo sería la manera de declarar cualquier número en el otro** — y una
> salida que la propia regla ofrece no es una salida, es un agujero con permiso. Traducido a
> consulta: `DelParAsync` trae también las retiradas, y sus dos clientes —el resolutor y la
> comprobación de la inversa— la necesitan así por motivos distintos.

> **El enunciado del ítem daba `g = 0,083334` por aceptada para `f = 12`, y la desigualdad la
> rechaza. Manda la desigualdad, que está fijada y no se toca.** Los números: `|12 × 0,083334 − 1| =
> 8·10⁻⁶`, contra un margen de `5·10⁻⁷ × 12,083334 = 6,0416670·10⁻⁶`. Y el rechazo **es lo
> correcto**: `1/12 = 0,08333333…`, así que `0,083334` se separa `6,67·10⁻⁷` del valor real —más de
> media unidad del último decimal— y por tanto **no es** un redondeo de `1/12` a seis decimales;
> `0,083333`, a `3,33·10⁻⁷`, sí lo es y **se acepta**. Ensanchar la tolerancia para que entrara sería
> exactamente el «número elegido por comodidad» que el ADR prohíbe. Los tres casos están escritos con
> su aritmética en `LaAritmeticaDeLaInversaTests`.

**El caso frontera, que es donde una desigualdad se equivoca de signo.** `f = 1,001` y `g = 0,999`:
`f·g = 0,999999`, se separan `10⁻⁶`, y el margen es `5·10⁻⁷ × 2,000 = 10⁻⁶` — **exactamente igual**.
Con `≤` entran; con `<` no. Y hay un caso aparte que **afirma esa igualdad**, porque un frontera que
no esté en la frontera no separa los dos signos: sería un par cualquiera dentro del margen, y la
mutación pasaría en verde. El vecino `g = 0,998999` se separa `2,001·10⁻⁶`, que además cae por debajo
de `10⁻⁵`: por eso el mismo caso caza también la tolerancia relajada un orden de magnitud.

**El resolutor (decisión 3 del ADR).** `GET /api/v1/organizacion/conversiones-um/resolucion` con las
dos unidades. Pedir un par no declarado es `conversion-um-no-declarada`, un **404**. **Nunca un cero,
nunca un nulo, nunca el producto de la cadena**: las tres son las tres peores maneras de equivocarse
en un inventario —un cero convierte las existencias en nada, un nulo se propaga sin ruido, y encadenar
multiplica el error de redondeo y hace que el número dependa de qué camino elija el buscador—. El caso
que importa **no** es el par declarado: es `kg→g` y `g→mg` declarados, `kg→mg` preguntado, y que salga
el error con nombre y **no un 1000000** —comprobado también sobre el cuerpo de la respuesta, que no
puede llevar ese número ni de adorno—. Ni identidad ni simetría: `kg→kg` falla igual, y `B→A` no se
deduce invirtiendo `A→B`.

**La ruta nueva pasa por los tres universos del 1.3 y el 1.4 y por el censo, sin apuntarla a mano en
ninguno**: tabla de enrutado, testigo de versión en el cuerpo, criterio sensible en la URL, y el
censo de reglas de su carril. Su `type` entra en el catálogo del ADR-0030 y el frontal escribe el
texto desde el `type`, en `es.ts` y `en.ts`.

**Nueve puertas nuevas, cada una con su permiso y su prohibición.** Ocho constantes nuevas en
`PermisosDeOrganizacion` —retirar y reincorporar son permisos **distintos**, como `cerrar`/`reabrir`:
los cuatro maestros son de instalación (R8), así que retirar por error deja **a todas las empresas**
sin esa fila, y deshacerlo tiene que poder autorizarse aparte—. `TodaEscrituraDiceComoSeProtege`
cuenta ahora 102 acciones, 68 que cambian estado, con 38 exigiendo versión, 13 admitiendo
idempotencia y 17 exentas declaradas: `38 + 13 + 17 = 68`, y esa suma es la aserción.

**Un defecto que salió de comprobar la decisión 6 contra el código, y que hay que decir entero.** El
`[Range(0.000001, 1000000)]` de los DTO significaba que el redondeo-a-cero del factor **no era
alcanzable por HTTP**. Lo era desde cualquier otro llamante y, sobre todo, era una **promesa falsa**:
una guarda de dominio que decía impedir algo que solo impedía el borde. El rango vive ahora en el
dominio —`FactorMinimo` y `FactorMaximo`, con su porqué escrito— y la validación redondea **primero**
y comprueba el número que se va a guardar; una regla barrida compara los cuatro literales del
contrato contra las dos constantes, sin enumerar DTO.

**Y una decisión de sitio, que es la que más cuesta ver.** La desigualdad de la inversa y la negativa
a encadenar tenían su **único** testigo en `Api.IntegrationTests`, detrás de Testcontainers. Pero ni
una comparación entre dos `decimal` ni la rama que decide no componer una cadena abren una conexión:
son *la parte del carril que no necesita el carril*, exactamente como `LaTraduccionASqlTests` desde el
1.3. Con Docker parado —que es como estaba esta máquina al empezar—, cambiar el `≤` por un `<`
compilaba, salía verde, y el aviso llegaba minutos después y en otro sitio. **Un caso frontera al que
solo se llega levantando un contenedor es un caso frontera que nadie ejerce mientras escribe el cambio
que lo rompe.** Se añade `LaAritmeticaDeLaInversaTests` como **segunda excepción declarada** de
`Organizacion.IntegrationTests`, con `InternalsVisibleTo` de la capa de aplicación hacia ese
ensamblado nombrado. No sustituye a los casos por HTTP —allí se comprueba que el rechazo llega como
409 y que el resolutor contesta 404—: aquí se comprueba el número, y ninguna de las dos afirmaciones
implica a la otra.

**Lo que el ítem no toca**, y se comprueba en el `git diff`: catálogo, artículo, categoría, tarifas,
cruces mutuos, importación. **Nada de `Impuesto`**: su `/cierre` es correcto y el ADR dice
expresamente que se queda como está.

**Ítem 1.6 cerrado — lo que cuelga del tercero, y la misma lección tres veces:**
run **34125157573** sobre `911ba5c`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Backend `101752010394` ✓ (22 pasos, 0 omitidos),
Frontal `101752010729` ✓ (17 pasos, 0 omitidos) y Humo `101753229252` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 630 casos (630 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.UnitTests 182, BuildingBlocks.UnitTests 132, Organizacion.IntegrationTests 4,
    Identidad.UnitTests 58, Api.FunctionalTests 137, Api.IntegrationTests 6,
    Arquitectura.Tests 34, Terceros.UnitTests 77
    (576 en el 1.5, los mismos 8 ensamblados: +54 casos)

Integración (Testcontainers): 318 casos (318 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 246, y 0 en los otros seis
    (271 en el 1.5: +47 casos, que son los que en esta máquina NO se pudieron ejecutar)

Frontal: arranque 403/450 KiB en 3 ficheros · total servido 548/900 KiB  (402 en el 1.5)
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  93 operaciones, 55 rutas /api/v1/, y el fichero del artefacto idéntico al del
          repositorio  (82 operaciones y 47 rutas en el 1.5)
Catálogo de `type`: 49 tipos, de 54 sitios de llamada  (47 de 52 en el 1.5)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
             — Auditoría 3, Organización 4, Identidad 3, Terceros 2
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 3204
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

**El ítem se cierra con una lección que le salió tres veces, y la tercera ya sin excusa.** Las tres
son la misma: **una obligación transversal enumerada a mano es ciega a lo que llegue después**, y
mientras esté en verde nadie la mira. (1) El listado del artículo 32 enumeraba tres agregados de
cinco, y el enumerado vivía dentro de un módulo. (2) Las cinco reglas que recorren el modelo entero
enumeraban sus contextos a mano y ninguna incluía Terceros, así que cuatro reglas que dicen «cada
entidad declara…» lo estuvieron diciendo de dos módulos de cinco. (3) Los dos ADR del propio ítem
salieron con el mismo número, porque la numeración no la miraba nadie. Las tres se cierran igual:
**el universo se descubre**, con su lista declarada al lado para que un descubrimiento corto salga
rojo en vez de silencioso. Lo que no arregla ninguna de las tres es la cuarta que todavía no se ha
visto — y por eso la forma importa más que los arreglos.

**La primera mitad — el agujero del art. 32, que es un defecto de cumplimiento en código
ya entregado.** Cinco agregados del proyecto se bloquean (`Empresa`, `Almacen`, `Ubicacion`,
`Tercero`, `Usuario`) y el listado del art. 32 veía **tres**. `Usuario` faltaba desde el **1.4** —una
persona física que ejerce su derecho de supresión, el caso más nítido del artículo— y `Tercero` desde
el **1.5**. Está contado entero en **ADR-0031**; lo esencial:

- **La regla va primero y se mira roja.** `LoBloqueadoSeVeEnteroTests` compara
  `TipoDeRecursoBloqueado` contra los tipos que implementan `IBloqueable`, en las dos direcciones y
  con su ancla. Escrita **antes** del arreglo sale roja con **dos supervivientes a la vez**, que es
  la mutación 1 de la tabla del ítem. La doctrina ya estaba escrita —en el comentario de la propia
  `IBloqueable`, desde el día que se escribió— y el barrido que la ejerce no existía: la lista se
  quedó vieja **dos ítems seguidos** con la suite en verde.
- **Y una segunda regla, porque la primera se puede cumplir sin arreglar nada.**
  `Todo_modulo_que_bloquea_contesta_por_lo_suyo`: el enumerado es una lista de nombres y se pone
  verde en cuanto alguien añade el valor. Un valor declarado que ningún módulo contesta es **peor**
  que el agujero — el contrato promete un tipo de recurso que no aparece nunca, así que quien audite
  leerá «no hay usuarios bloqueados» en vez de «nadie los está buscando».
- **El arreglo no inventa arquitectura.** Cada módulo que bloquea expone `IConsultaDeLoBloqueado` y
  el listado compone. El puerto vive en el **bloque común** y no en el `Contracts` de nadie, porque
  aquí hay varios dueños y un consumidor; el consumidor resuelve `IEnumerable<…>`, así que un módulo
  nuevo entra registrando su implementación. Sin `JOIN` entre esquemas y sin llamada HTTP — y **sin
  cruce nuevo que declarar**: al estar el puerto en el bloque común no aparece ninguna referencia de
  proyecto entre módulos.
- **El repositorio anterior llevaba escrito por qué esto «no se podía hacer», y la mitad era falsa.**
  Decía que no hay forma de saber cuántas filas traer de cada fuente sin traerlas todas. Para la
  página *k* bastan las `salto + tamaño` primeras de **cada** fuente: es la cota de una fusión
  k-vías, y no es asintóticamente peor que el `UNION` con `OFFSET` que sustituye.
- **Dos defectos los encontró el barrido nuevo, antes de salir de la rama.** El de traducción a SQL
  se mudó de `Organizacion.IntegrationTests` a `Api.IntegrationTests` —allí «entero» habría pasado a
  significar un tercio, que es el mismo defecto en pequeño— y en cuanto miró los tres módulos puso
  rojas las nueve combinaciones de orden de Identidad: **(a)** una condición de membresía escrita a
  mano duplicaba un invariante que el filtro global de `Usuario` ya impone, y sumada a la navegación
  que ese filtro expande no se traducía; **(b)** `Codigo = usuario.Correo.Valor` tampoco, porque
  `Correo` se mapea con conversor y `.Valor` no es una columna. Habrían sido nueve `?sort=`
  convertidos en un 500 justo en la pantalla que se abre para rectificar un bloqueo por error.
- **En este listado no viaja ningún identificador de persona:** ni el NIF del tercero ni el correo
  del usuario. Para levantar un bloqueo hecho por error bastan el nombre y el identificador de la
  fila, que es lo que el desbloqueo pide.
- **`BuildingBlocks.Infrastructure` estrena el proveedor de Npgsql**, porque el filtro compartido usa
  `ILIKE`. Su `.csproj` decía «EF Core, no el proveedor: este proyecto no sabe contra qué base
  corre», y la frase **ya era falsa cuando se escribió** —dos párrafos más abajo admitía mapear una
  columna `jsonb`—. Se corrige la frase en vez de esconder el hecho detrás de tres copias del filtro.

Carril rápido en local sobre esta mitad, con el comando que lo mide
(`dotnet test Bastion.sln -c Release --no-build --filter "Category!=Integracion" --logger trx` +
`scripts/ci/recuento-de-tests.sh`):

```
Dominio y arquitectura: 582 casos (582 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Terceros.UnitTests 39,
    Organizacion.UnitTests 182, Organizacion.IntegrationTests 4, Arquitectura.Tests 27,
    Api.FunctionalTests 136, Api.IntegrationTests 4
```

Contra los **576 en 8 ensamblados** del 1.5 (`279b8c7`): +1 regla de arquitectura (23→27 incluye las
tres del barrido nuevo del art. 32 más la del puerto), +3 casos en `Api.IntegrationTests` (el barrido
de traducción, que corre sin Docker) y −1 en `Organizacion.IntegrationTests` (el caso que se mudó).
**Integración no se ha podido ejercer en local: no hay Docker en esta máquina.** Lo que sí se ejerció
en su lugar está en el informe del ítem.

Mutaciones de esta mitad, **cada una con su línea base nombrada**, sobre árbol limpio y revertidas
sin `git checkout --`:

| # | Mutación | Línea base | Resultado |
|---|---|---|---|
| 1 | La regla de la lista cerrada, **antes del arreglo** | `e3a9e9e` (main al abrir la rama) | 🔴 `["Tercero", "Usuario"]` — los dos supervivientes a la vez |
| 2 | `ConsultaDeLoBloqueadoDeTerceros` deja de implementar el puerto (y se comenta su `AddScoped`) | `f9913d9` (la rama, con el arreglo) | 🔴 `mudos: ["Terceros"]` |

La **1** se midió en un árbol aparte de `e3a9e9e` con la regla copiada dentro y nada más: es
literalmente «la regla, antes del arreglo». Salieron **2 verdes y 1 roja** de 3 — los verdes son el
ancla (ve tres agregados bloqueables, así que no se cumple en vacío) y la dirección «ningún valor
sobra».

La **2** deja ver por qué hacen falta las dos reglas:
`Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32` se quedó **verde** con la mutación
puesta. El valor `Tercero` seguía declarado y nadie lo contestaba — el falso verde exacto que la
segunda regla existe para cazar.

**El «antes» de los `act()`, medido donde toca.** La regla nueva del ítem dice que toda cifra de
antes y después nombra el commit de cada extremo, y que el «antes» es **main al abrir la rama**:

```
e3a9e9e (main al abrir) — 109 avisos `not wrapped in act`, en 7 ficheros de prueba
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
(11 ficheros, 63 casos, todos verdes)
```

Y eso **corrige el «antes» que se dio en el 1.5**: allí se apuntaron «91 en seis ficheros» como
punto de partida, pero se midieron sobre un árbol que ya llevaba el código del ítem. Medido sobre
main limpio salen **109 en siete**, que es la misma cifra que el «después» de aquel ítem: los
dieciocho avisos **no los trajo el 1.5**. La comparación de entonces no demostraba lo que decía —
que es exactamente el motivo por el que la regla se escribió.

**Ítem 1.6, segunda mitad — lo que cuelga del tercero.** Tres entidades hijas (`Contacto`,
`CuentaBancaria`, `CondicionPago`), un valor opcional en la propia ficha (`LimiteCredito`) y el
régimen fiscal del §7.2. Once casos de uso, un controlador, once rutas nuevas y siete permisos.

- **El IBAN se valida de verdad, y la batería prueba las tres negativas por separado.** `Iban` vive
  en `BuildingBlocks.Domain.Identificacion`, al lado de `Nif`: mod-97-10 sobre la cadena reordenada
  con las letras convertidas a dígitos, **más la longitud por país**, que no es la misma en todos.
  La tabla son los **37 países de la zona SEPA** y no el registro entero de la ISO, con el motivo
  escrito: aceptar los ochenta y pico haría creer que esta instalación puede domiciliar un adeudo
  contra cualquiera de ellos. Los casos se generan **con su control calculado** —`IbanesInventados`—
  y se ejercen en las dos direcciones: que acepte los válidos de los 37 países, y que rechace **por
  cada motivo distinto** con su caso propio (`Un_control_cambiado_se_rechaza`,
  `Una_longitud_de_otro_pais_se_rechaza_aunque_el_control_cuadre`,
  `Un_pais_que_no_existe_se_rechaza_aunque_el_control_cuadre`). Un negativo genérico habría pasado
  con cualquiera de las tres comprobaciones puesta y las otras dos quitadas.
- **El barrido del 1.5 se extiende al IBAN, y cambia de forma para poder hacerlo.**
  `NingunDatoConFormaDeRealTests` es una **lista cerrada de formas** —hoy dos: IBAN e identificador
  fiscal español— comparada entera en los dos sentidos contra los detectores implementados, con su
  afirmación de que cada detector encuentra algo y de que el barrido está leyendo ficheros de verdad.
  La decisión de diseño está escrita en su cabecera: **la lista se declara por lo que se PERMITE**,
  no por lo que se prohíbe, porque una lista de valores prohibidos tendría que llevarlos escritos —y
  escribir un IBAN real para prohibirlo es exactamente el daño—. Cada forma declara su **espacio
  inventado** y todo lo que tenga la forma y esté fuera es rojo. Por lo mismo, **los mensajes no
  publican lo que encuentran**: fichero, línea y forma enmascarada, porque un registro de CI que
  imprime el IBAN cazado contiene justo lo que la regla existe para sacar del repositorio.
- **Los sesenta días son un invariante del dominio, no un valor por omisión.** `CondicionPago`
  guarda **el plazo** —`DiasMaximosDePlazo = 60`— y lo exige el dominio lanzando, la aplicación
  adelantándolo como error de campo para que se pueda explicar, y la base con
  `ck_condiciones_pago_plazo_legal`. Tres sitios, y el que manda es el del dominio: la interfaz que
  no ofrece 90 no impide guardarlo desde una importación de CSV o desde un test. Los dos lados del
  límite se prueban **en el mismo fichero** (`[InlineData(0, 30, 60)]` acepta,
  `[InlineData(61, 90, 120)]` rechaza), para que un `>` escrito donde va un `>=` no se pueda
  esconder. **Y aquí no se calcula ningún vencimiento:** Terceros no tiene entregas.
- **Hasta dónde llega el invariante, dicho sin exagerarlo.** Con `DiaDePagoFijo` puesto, la fecha
  efectiva de pago **puede pasar de 60 días** aunque el plazo guardado sean 30: un plazo de 30 días
  sobre una entrega del día 2, redondeado al día de pago 25 del mes siguiente, se va más allá. Esa
  comprobación es de donde se calcula el vencimiento —la fase 3— y allí tiene que **recortar al día
  60**, no rechazar. Escribirlo aquí sería computar fechas, que es meterse en la fase 3; callarlo
  sería dejar creer que el tope está cerrado por este ítem, y no lo está.
- **`LimiteCredito` lleva su divisa, y no la hereda.** Es un `Importe?` —`numeric(18,4)` más el
  código ISO, nunca coma flotante ni el `money` del motor—, con `ck_terceros_limite_credito_completo`
  para que las dos columnas estén o falten juntas. La alternativa era heredar `Empresa.DivisaBase`
  (el campo se llama así; **`DivisaPreferidaId` no existe**, y era el nombre que traía la nota), y se
  descarta por su **modo de fallo mudo**: el día que alguien cambie la divisa base de la empresa,
  todos los límites guardados cambiarían de significado sin que se toque una sola fila y sin que
  falle nada. Lo que la pantalla ofrezca por omisión es otra cosa y es de la pantalla. Consumo,
  disponible y bloqueo por exceso **no son de este ítem**: esa es la raya de P6.
- **Una sola cuenta preferente por tercero, y es una restricción.** Índice único parcial
  `ix_cuentas_bancarias_una_preferente_por_tercero` sobre `(tercero_id)` con filtro `es_preferente`,
  más el invariante en el agregado: la primera cuenta nace preferente, marcar otra degrada a la
  anterior y quitar la preferente promueve a otra. **¿La restricción ve las filas bloqueadas? Es la
  misma decisión del 1.5** —la de (empresa, NIF)—: el índice es de la base y no conoce el filtro
  global, así que las ve todas. Y aquí, además, **no podría ser de otra manera y eso es lo
  interesante**: el bloqueo del art. 32 vive en el **padre**, no en la cuenta, así que dos cuentas
  que compiten por ser la preferente están siempre en el mismo estado. La pregunta que en el 1.5
  tenía dos respuestas posibles, aquí solo tiene una.
- **El régimen fiscal entra, con el corte que se sostiene — tres de cuatro.** Está en el §7.2 del
  plan maestro y se quedó sin ítem al desglosar la fase: alcance comprometido sin casa, y su sitio es
  el tercero. `RegimenFiscal` es un valor con `TerritorioFiscal` (cinco valores, `CHECK`
  `ck_terceros_territorio_fiscal`) y tres booleanos: recargo de equivalencia, criterio de caja y
  sujeto a retención de IRPF. **El corte se sostiene porque los cuatro son hechos declarados del
  tercero**, no resultados de un cálculo — que un minorista esté en recargo de equivalencia es algo
  que él dice y que su ficha registra, igual que su domicilio. **Lo que NO entra, y se dice: el tipo
  de retención.** Ese sí es un valor sin significado sin la regla que lo aplica, y además ya tiene
  dueño desde el 1.2 (`Impuesto` con `TipoDeImpuesto.Retencion`), así que ponerlo aquí obligaría a
  declarar un cruce entre módulos (ADR-0024) — bastante más que «el campo y nada más». El booleano
  dice *si* se le retiene; *cuánto* es de quien ya lo sabe.
- **Los tres universos del 1.3 y el 1.4 miraron solos, sin que hubiera que apuntarlos**, que era la
  prueba: la tabla de enrutado descubrió las once rutas nuevas (`TodaEscrituraDiceComoSeProtege`
  pasó de 82 a 93 acciones y puso rojo `El_barrido_encuentra_el_inventario_entero`), el testigo de
  versión en el cuerpo siguió verde porque ninguno de los cuatro DTO nuevos lo lleva, y el criterio
  sensible en la URL **no necesitó ni una línea nueva en el censo**: `iban` ya estaba en
  `s_sensibles` desde el 1.3, y **no se ha añadido ninguna ruta que busque por IBAN**. Si algún día
  se busca por él, va por cuerpo.
- **Y una regla del carril que NO se ha podido ver roja en local, dicha como tal.**
  `EsquemaDeTercerosTests` compara la lista de tablas del esquema **entera** y afirma que no hay
  **ninguna** clave ajena; con tres tablas nuevas y sus tres claves en cascada, iba a ponerse roja
  por partida doble en la CI. La vigilancia funcionó —nadie tuvo que apuntarla— pero **está en el
  carril de integración**, así que aquí se detectó leyéndola, no ejecutándola. Aprovechando que es
  el único sitio que ve la base de verdad, se ha extendido de 5 casos a 15: el índice **parcial** de
  la cuenta preferente (contrastado a propósito con el del identificador, que no puede llevar
  predicado), la unicidad de (tercero, IBAN) —y **no** global, porque la misma cuenta puede ser de
  dos terceros—, los tres `CHECK`, el valor 60 del tope escrito en el suyo, las cuatro columnas del
  régimen y las dos del límite con su escala, el `ON DELETE CASCADE` de los tres hijos por el
  derecho de supresión, y la regla de `NOT NULL` **sin `DEFAULT`** sobre las cuatro columnas que
  este ítem añadió a una tabla con filas dentro — que es exactamente la que dice si la migración de
  tres pasos hizo lo que promete. Y se dice lo que **no** son: leen la definición de la restricción,
  no meten una fila para ver si muerde. Cazan que se borre o se escriba mal, no un motor que la
  ignore.
- **Y dos reglas más se pusieron rojas sin que nadie las llamara**, las dos por lo mismo —una
  entidad nueva es un compromiso que hay que declarar en varios sitios—: `ElGlosarioDelDominioTests`
  exigió las tres filas de las entidades hijas, y
  `CadaAccionDeclaraSuPermisoTests.Escribir_y_modificar_no_comparten_permiso` cazó que
  `terceros.tercero.modificar` estaba abriendo **ocho** acciones. De ahí los siete permisos nuevos,
  uno por escritura, cada uno con su motivo escrito.

**Los `Idempotency-Key` no se mueven, y eso es la mitad del resultado.** El ítem suma **+11
acciones**, **+7 que cambian estado**, **+7 `If-Match`**, y **cero** al cajón de idempotencia y cero
al de las exentas. Las siete escrituras nuevas son modificaciones de un agregado que ya existe, no
altas de un recurso nuevo, así que el mecanismo que les toca es el otro —y las tablas del 0.9, que
llevaban desde el 0.10 sin rehacerse, se han rehecho enteras en *Decisiones tomadas → ítem 0.9*.

Carril rápido en local con el ítem entero, con el comando que lo mide —**el mismo del *workflow***,
que es lo que `AGENTS.md` exige desde el 1.3—:

```
dotnet test Bastion.sln --filter "Category!=Integracion" \
  --logger trx --results-directory artifacts/test-results/dominio
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio \
  "Dominio y arquitectura" 300 "<los ocho ensamblados de ci.yml>"

Dominio y arquitectura: 624 casos (624 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Terceros.UnitTests 77,
    Organizacion.UnitTests 182, Organizacion.IntegrationTests 4, Api.FunctionalTests 136,
    Arquitectura.Tests 31, Api.IntegrationTests 4
```

Contra los **576 en 8 ensamblados** de `main` al abrir la rama (`e3a9e9e`): **+48 casos**, y el
reparto dice de dónde sale cada uno — Terceros.UnitTests 39→77 (los 27 de lo que cuelga y los 11 de
la batería del IBAN), Arquitectura.Tests 23→31 (las tres del art. 32, la del puerto y las cuatro del
barrido de datos con forma de real), Api.IntegrationTests 1→4 y Organizacion.IntegrationTests 5→4
(el caso que se mudó de proyecto). Lo demás no se mueve.

**Integración no se ha podido ejercer en local, y esta es la firma:** `docker info` responde, pero
`docker version` contesta `failed to connect to the docker API at
npipe:////./pipe/dockerDesktopLinuxEngine; check if the path is correct and if the daemon is running:
open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.` — el cliente está
instalado y el demonio no arranca en esta máquina.

**Lo que se ejerció en su lugar, y lo que queda para el run.** En lugar de los tests que necesitan
contenedor: `scripts/comprobar-migraciones.sh` (el modelo no tiene cambios pendientes y la migración
cuadra con él), `CadaEntidadDeclaraSuAuditoriaTests` en el carril rápido (las cuatro configuraciones
nuevas declaran la auditoría de **cada** propiedad), los 27 casos de `LoQueCuelgaDelTerceroTests`
sobre los invariantes del agregado, y la compilación del proyecto de integración con sus **veinte
casos nuevos** —los 15 de `EsquemaDeTercerosTests` y los 10 de `ContratoDeLoQueCuelgaTests`, de los
que 5 ya existían—, que al menos garantiza que las consultas están escritas y el ensamblado carga.
**Lo que solo el run puede decir** es lo que de verdad falta: que la migración se **aplique** —los
tres pasos de añadir, rellenar y cerrar—, que los tres `CHECK` y el índice parcial estén en la base
después de aplicarla, que los 15 casos de esquema pasen, y que los 10 de contrato pasen —entre ellos
el que tapa el agujero de la mutación 7, que **ninguna máquina ha visto correr todavía**—. Se dice
antes de empujar, no después.

**Y el run lo dijo: 34097268237 salió ROJO, con dos defectos de producto que ningún carril local
podía ver.** Se anota entero porque es el resultado del párrafo anterior: lo que se dijo que solo el
run podía decir, lo dijo, y dijo que no. Los **tres jobs contados en el run** (`total_count: 3`):
Backend `101663584575` **failure** en 22 pasos, Frontal `101663584755` success en 17, Humo
`101664678865` skipped. Recuentos que publicó: **dominio 624 casos en 8 ensamblados, 0 rojos**;
**integración 313 casos en 8 ensamblados, 7 rojos**. Los 7 son dos defectos, no siete.

- **Defecto A — la migración dejó las cuatro columnas del régimen ANULABLES, y el modelo decía
  `NOT NULL`.** Cuatro filas rojas de
  `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT(tabla: "terceros", columna: …)`, con
  el mensaje `should be (NO, ) but was (YES, )` sobre `territorio_fiscal`,
  `recargo_de_equivalencia`, `criterio_de_caja` y `sujeto_a_retencion_irpf`. **La causa es una
  omisión que no da error ni aviso:** `MigrationBuilder.AlterColumn(..., nullable: false)` **sin**
  `oldNullable: true` construye una operación cuya columna «vieja» tiene los valores por omisión del
  tipo, así que el generador de Npgsql compara `IsNullable` nuevo (`false`) contra
  `OldColumn.IsNullable` (`false`), no ve diferencia y **no emite `SET NOT NULL`**. La migración se
  aplica entera, sin fallar, y deja el tercer paso sin hacer.
- **Y esto es exactamente el hueco que la regla existía para tapar.** El modelo sí decía `NOT NULL`
  —`b1.Property<string>("Territorio").IsRequired()` en el *snapshot*—, así que
  `has-pending-model-changes` no tenía nada que ver, y **ningún barrido sobre el modelo podía
  encontrarlo**: el modelo estaba bien y el esquema estaba mal. Solo lo ve quien mira la base
  después de aplicar la migración, que es el único sitio donde vive la diferencia.
- **Arreglado, y comprobado en local sin contenedor** — que es lo que aquí sí se puede ejercer: las
  cuatro `AlterColumn` llevan ya sus parámetros de columna vieja, y `dotnet ef migrations script`
  —que no se conecta a ninguna base— emite ahora las cuatro sentencias que faltaban:

  ```
  dotnet ef migrations script 20260905044708_EsquemaInicialDeTerceros \
    20260907025348_LoQueCuelgaDelTercero --context TercerosDbContext --no-build

  UPDATE terceros.terceros SET territorio_fiscal = 'PeninsulaYBaleares', … WHERE territorio_fiscal IS NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN territorio_fiscal SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN recargo_de_equivalencia SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN criterio_de_caja SET NOT NULL;
  ALTER TABLE terceros.terceros ALTER COLUMN sujeto_a_retencion_irpf SET NOT NULL;
  ```

  Y `grep -i default` sobre el guion entero no encuentra nada: los tres pasos de añadir, rellenar y
  cerrar, sin `DEFAULT` que se quede detrás.

- **Defecto B — citar la versión de la ficha para colgarle algo daba 412 SIEMPRE.** Tres filas rojas
  —`Una_segunda_cuenta_preferente_baja_a_la_primera_y_solo_queda_UNA`,
  `El_mismo_IBAN_dos_veces_en_la_misma_ficha_es_409_y_sin_el_numero_dentro` y
  `Sesenta_dias_clavados_se_aceptan_porque_el_tope_es_el_maximo_y_no_un_veto`—, todas con
  `should be HttpStatusCode.OK but was HttpStatusCode.PreconditionFailed` y el cuerpo
  `{"type":"/errors/version-obsoleta","title":"La versión ya no es la actual","status":412,…}`.
- **La atribución sale de una partición limpia, no de una corazonada.** De los diez casos de
  contrato, los tres rojos son **exactamente** los que llegan a `SaveChanges` tocando **solo hijos**;
  los cuatro del límite de crédito pasaron porque `FijarLimiteDeCredito` modifica la propia ficha, y
  los otros tres pasaron porque nunca llegan a guardar. La causa: `Versiones.Exigir` ponía el
  `OriginalValue` del testigo, pero una escritura que solo inserta un hijo deja la fila del agregado
  en `Unchanged`; EF Core arma entonces un mandato sin columnas que asignar, la sentencia no toca
  ninguna fila, el guardado sale por `DbUpdateConcurrencyException` y el borde lo traduce a un 412
  **perpetuo** — la escritura correcta era imposible, y el mensaje decía que otro la había pisado,
  que era mentira.
- **DESMENTIDO por el run siguiente — se deja escrito, no se borra.** Lo que sigue se escribió como el arreglo del defecto B y no lo era; el bloque de abajo lo cuenta. El cambio se queda en el código por otro motivo, que también está abajo.
- **Arreglado en `Versiones.Exigir`, y no en los seis casos de uso.** Quien **exige** una versión
  para escribir está diciendo que escribe: ahora, además del `OriginalValue`, marca `ModificadoEn`
  como modificada cuando la entidad es una `EntidadBase`. El interceptor de marcas de tiempo le pone
  entonces un valor nuevo de verdad, el `UPDATE` tiene algo que asignar y el testigo avanza. Es el
  mismo argumento que ya estaba escrito en `InterceptorDeMarcasDeTiempo`: **sostenerlo a mano
  significa que el día que alguien escriba la séptima ruta que cuelga algo y no se acuerde, la
  versión deja de moverse sin que nada falle.** Para las 27 escrituras que ya modifican el agregado
  no cambia una sola sentencia, y para `EliminarSerie` tampoco, porque `Remove` manda sobre el
  estado. Los treinta llamantes de `Exigir` se han comprobado uno a uno: los treinta son casos de
  uso de escritura, ninguno es una lectura.
- **Y de paso tapa un silencio de la bandeja de salida, que nadie había pedido mirar.**
  `InterceptorDeLaBandeja` solo publica los eventos de un agregado si además `SeEstaGuardando` —es
  decir, si su estado es `Added`, `Modified` o `Deleted`—. Con la raíz en `Unchanged`, una escritura
  que solo tocara hijos y registrara un evento de dominio en la raíz **se habría guardado sin
  publicar nada, y sin fallar**. Hoy ninguna lo hace, así que no es un defecto observado: es un
  filo que estaba puesto y que ya no está. Los otros dos interceptores se han mirado igual:
  `InterceptorDeAuditoria` escribirá ahora una fila de traza para la ficha además de la del hijo
  —que es lo correcto, porque la ficha ha cambiado—, y ninguna prueba existente cuenta filas de
  traza de escrituras de este tipo, porque hasta ahora todas fallaban.
- **Y el caso que faltaba es el que habría ahorrado el run.** Los diez de contrato miraban cada ruta
  por su lado; ninguno miraba el invariante que las cruza. `ContratoDeLoQueCuelgaTests` pasa de 10 a
  **14 casos**: `Colgar_un_contacto_MUEVE_la_version_de_la_ficha_y_la_vieja_ya_no_vale` —que afirma
  las dos direcciones, porque cada una caza un fallo distinto: que la etiqueta cambie caza la versión
  quieta, y que la vieja dé 412 caza un borde que devolviera etiquetas nuevas sin relación con el
  testigo— más los tres recorridos de escritura que nada ejercía (alta y baja de contacto, ascenso a
  preferente, baja de cuenta). Los cuatro, en el censo de su carril.
- **El límite honesto, dicho igual que el de la mutación 7:** los cuatro casos nuevos y el arreglo
  del defecto B viven en el **carril de integración**, que esta máquina no puede ejecutar, y el
  proyecto rechaza a propósito EF Core InMemory y no tiene arnés de SQLite —así que no hay dónde
  ponerlos en el carril rápido sin inventar arquitectura—. Del arreglo A sí hay prueba local (el
  guion SQL de arriba); del B, **hasta que el run siguiente esté leído, lo único que se puede afirmar
  es que la causa está identificada por partición y el arreglo escrito**, no que se haya visto morder.

**El run 34101097530 sobre `57838b2`: el defecto A está cerrado y el B NO, y mi atribución del B era
falsa.** Se escribe entero porque la parte que importa es la segunda. Los **tres jobs contados en el
run** (`total_count: 3`): Frontal `101675496804` success en 17 pasos, Backend `101675497155`
**failure** en 22, Humo `101676750179` skipped. Recuentos que publicó el propio run: **dominio 624
casos en 8 ensamblados, 0 rojos**; **integración 317 casos en 8 ensamblados, 7 rojos**. Los tres
pasos rojos del Backend son uno: «Tests de integración», y detrás «Bajar el artefacto del OpenAPI» y
«El artefacto del OpenAPI contiene el contrato», que caen porque el anterior no llegó a subirlo.

- **Defecto A, CERRADO.** Las cuatro filas de
  `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT` **han desaparecido** del run. Es la
  confirmación por el efecto de lo que el guion SQL ya decía en local: los `oldNullable: true`
  emiten las cuatro sentencias y la migración cierra su tercer paso.
- **Defecto B, ABIERTO — y con la atribución anterior desmentida.** Los 7 rojos son ahora los 3 de
  antes más los 4 casos nuevos, todos con `should be OK but was PreconditionFailed` y **todos en la
  PRIMERA escritura** de su caso. Es decir: el cambio en `Versiones.Exigir` no movió nada. La
  explicación que se escribió —la ficha en `Unchanged`, el mandato sin columnas, el `UPDATE` de cero
  filas— **era falsa**, y conviene decir por qué se creyó: la partición encajaba perfectamente (los
  cuatro casos del límite pasaban, los que solo tocan hijos no), y una partición limpia que encaja
  con una explicación plausible se parece mucho a una prueba sin serlo.
- **Lo que el run SÍ deja probado, y apunta a otro sitio.** El cuerpo del 412 es
  `ObsoletaYSinRecurso` —«Este recurso ha cambiado o ha desaparecido»— y no `Obsoleta(actual)`.
  `ManejadorDeVersionObsoleta.ActualAsync` solo devuelve nulo cuando **ninguna** de las entradas del
  choque tiene la propiedad del testigo. **La ficha la tiene.** Luego el `DbUpdateConcurrencyException`
  no lo levanta la ficha: lo levanta una entrada sin testigo —un hijo, o una fila de traza—. Eso
  descarta la ficha entera, que era donde estaba mirando.
- **Y una hipótesis descartada con su experimento, para que nadie la repita.** Se sospechó que la
  columna de sistema `xmin` no sobreviviera a la subconsulta que EF Core genera al mezclar
  `Include` de colecciones con `FirstOrDefault` —`FROM (SELECT … LIMIT 1) AS t0`—, lo que habría
  hecho que la versión leída fuese basura. Se comprobó **en local y sin base de datos**, con una
  sonda temporal que imprime `ToQueryString()` de las dos consultas del repositorio: `t.xmin` está
  en la proyección de la consulta simple, en la de los tres `LEFT JOIN` y **también dentro de la
  subconsulta con `LIMIT`**. La hipótesis es falsa. La sonda se ha borrado; lo que queda es esto
  escrito.
- **Lo que falta para cerrarlo es el texto de la excepción**, que dice qué entidad esperaba afectar
  a una fila y afectó a cero. Está en el registro del job `101675497155`, y
  `/actions/jobs/{id}/logs` contesta **403** sin credencial. Las anotaciones del *check run* —que sí
  son públicas y de donde sale todo lo anterior— publican el nombre del caso y la aserción, pero no
  el registro de la aplicación. **Pedido al usuario**, que lo había ofrecido — y su respuesta fue que el registro **no contiene ninguna traza del choque**: ni una linea. Lo que sigue sale de ahi.

**Y la partición que se dio por buena estaba mal leída, que es la segunda vez que pasa lo mismo con
distinta cara.** Se dijo que pasaban «los cuatro casos del límite» y fallaban «los que solo tocan
hijos». Falso: `Sesenta_dias_clavados_se_aceptan…` es de condiciones de pago y estaba en rojo. La
partición **real** del run es otra y es más limpia: **los siete rojos son exactamente los siete que
esperan un OK en una ruta de colección hija, y ninguno de los que esperan un rechazo está en rojo**
—no lo están porque fallan la validación y nunca llegan a escribir—. Reenunciada: *todo camino que
llega a escribir un hijo falla; ninguno que no llega, falla*. Y de ahí lo que de verdad duele: **no
queda ni un verde que demuestre que una escritura de hijo funciona.** La lección no es «mira mejor»;
es que **una partición se comprueba enumerando los dos lados, y el lado verde no se enumeró**.

**La enumeración que sí se hizo, y lo que tumba.** Se enumeraron desde el código todos los caminos
que producen el cuerpo `ObsoletaYSinRecurso`, en vez de suponer el primero que encajaba:

| Pregunta | Comando | Resultado |
|---|---|---|
| ¿Quién llama a `ObsoletaYSinRecurso()`? | `grep -rn ObsoletaYSinRecurso src/ tests/ frontend/src` | **Un solo llamante**: `ManejadorDeVersionObsoleta.cs:78` |
| ¿Quién produce ese texto de `detail`? | `grep -rn "ha cambiado o ha desaparecido" src/ tests/ frontend/src` | **Un solo sitio**, el de la constante |
| ¿Quién produce `TipoDeError.VersionObsoleta`? | `grep -rn "VersionObsoleta(" src/` | Solo `ErroresDeConcurrencia`, sus dos fábricas |
| ¿Quién trata `DbUpdateConcurrencyException`? | `grep -rn DbUpdateConcurrencyException src/` | Solo el manejador |

Y ese único llamante está **detrás de un `return false`** que descarta toda excepción que no sea
`DbUpdateConcurrencyException`. Conclusión: **hubo choque**, y ahora está probado por enumeración y
no supuesto. Con eso cae la hipótesis alternativa —«no hubo choque, la ficha no se encontró»—:
una ficha que no aparece devuelve `ErroresDeTercero.NoEncontrado`, que es un **404**, no este cuerpo.
Un filtro global que la escondiera daría 404 igual.

**El agujero de la política de errores, cerrado — y no como instrumentación de este fallo.** El
registro del job no traía ni una línea del choque, y aunque la hubiera traído no habría servido: el
mensaje era *«la versión del cliente ya no era la actual»* y **no nombraba nada**. Un 412 es de las
pocas respuestas que no puede decir en el cuerpo **qué** chocó, así que el único sitio donde eso
puede constar es la traza; y el cuerpo publica un `traceId` precisamente para que alguien lo pegue y
aparezca la petición. Sin la anotación, ese `traceId` era **una promesa que el sistema no cumplía**.
Ahora hay dos sucesos, y son dos porque no son la misma cosa:

- **8500, `Information` — el choque que el diseño espera.** Dos personas editando la misma ficha. El
  cliente tiene en el cuerpo el `versionActual` que necesita para resolverlo.
- **8501, `Warning` — el choque SIN versión actual.** Ninguna entrada lleva testigo, así que el 412
  sale **sin** el `versionActual` que el contrato promete. Eso no es tráfico normal, es una
  anomalía, y por eso no se queda en el nivel de información entre todas las consultas del día.

Los dos llevan método, ruta, **la misma traza que publica el cuerpo** y las entradas del choque como
`Tipo:Estado`. **Nombres y estados, y ningún valor**, enmascarado en el punto de escritura y no
confiando en el destino: entre lo que puede chocar hay tablas con datos personales —un contacto
lleva nombre, correo y teléfono de una persona identificada— y esto va a un registro que se agrega,
se replica y se conserva con menos ceremonia que la base de datos. Por construcción no puede salir un
valor: el ayudante solo lee `Metadata` y `State`. Lo guarda
`Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado`, que afirma **las dos mitades** —que
nombra `Tercero:Modified` y la traza, y que no lleva ni el identificador fiscal ni la razón social—.

**Y dos defectos del arnés, que el usuario señaló y se confirman desde arriba.**

- **La cascada del OpenAPI era cascada, y ya no.** «Publicar el OpenAPI» **no llevaba `always()`**
  —con razón: de un job que falló no hay nada que publicar—, así que un rojo en los tests lo dejaba
  sin ejecutar; pero los dos pasos que lo verifican **sí** llevaban `always()` y se ejecutaban
  igual, buscando un artefacto que nadie había subido. Un rojo de tests fabricaba dos errores más
  que **parecen del contrato y no lo son**. Ahora los dos cuelgan de
  `steps.publicar_openapi.outcome == 'success'`: si no hubo publicación, no hay nada que verificar.
  El arnés que existe para que la CI no mienta no puede ser el que miente.
- **La tubería que se cerraba antes de tiempo.** `grep -o … | head -1`, tres veces en
  `recuento-de-tests.sh`: `head` cierra la tubería y `grep` muere escribiendo en ella —de ahí los
  `grep: write error: Broken pipe` del registro—. Hoy no rompía nada porque de una tubería solo
  cuenta el último estado, pero **este guion decide el rojo y el verde del carril**, y el día que
  alguien le añadiera `set -o pipefail` empezaría a fallar el **contador** en vez de lo contado.
  Sustituido por un `awk` con `exit`, que no abre tubería, se para en la primera coincidencia igual
  y devuelve 0 aunque no encuentre nada. Comprobado con `pipefail` puesto a propósito: mismo
  recuento —**624 casos en 8 ensamblados**—, código 0 y sin una línea de ruido.

**Los `act()` del frontal, con los dos extremos nombrados.** La regla que salió del 1.5 se aplica
desde aquí: toda cifra de «antes y después» dice sobre qué commit se midió cada extremo, y el
«antes» es `main` al abrir la rama.

```
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
```

- **Antes — `e3a9e9e`** (main al abrir): **109 avisos en 7 ficheros**.
- **Después — `57838b2`** (el árbol que se lleva a main al cerrar): **109 avisos en 7 ficheros**,
  con el mismo reparto: `ElCambioDeIdioma` 17, `ElCambioDeRuta` 14, `ElSelectorDeEmpresa` 14,
  `LasRutasProtegidas` 17, `LaPantallaDeAcceso` 10, `ElListadoDeAlmacenes` 24, `ElTestigoDeAcceso` 13.

> **Y el extremo «después» se midió en `c7b9f80`, no en `57838b2`: hay que decirlo y hay que
> justificarlo.** Entre los dos hay dos commits que arreglan lo que destapó el run 34097268237, y
> ninguno toca el frontal — `git diff --stat c7b9f80 57838b2 -- frontend/` sale **vacío**, es decir,
> el árbol de `frontend/` es idéntico byte a byte. La cifra vale para `57838b2` porque el código que
> la produce es el mismo, y la comprobación que lo sostiene está escrita aquí para que se pueda
> repetir. La alternativa —dar el número sin decir en qué commit se midió— es justo lo que la regla
> que salió del 1.5 prohíbe.

**No se mueve, y el motivo está en el `git diff`:** el ítem toca del frontal solo `esquema.ts`
—generado—, dos textos de `i18n` y las *fixtures*. Ni un componente, ni una pantalla. Un cambio en
esta cifra habría sido la señal de que el ítem se metió donde no le tocaba.

**Las ocho primeras mutaciones del ítem, cada una con su línea base nombrada** —las seis
restantes van con la segunda mitad del ítem, más abajo—**.** Todas sobre árbol limpio,
aplicadas con copia de respaldo y revertidas **restaurando esa copia** —nunca con `git checkout --`,
que se llevaría por delante todo lo no commiteado del fichero—, y con `git status --porcelain` vacío
comprobado después de cada una. Cuando la mutación toca el modelo, se **reconstruye Debug** antes de
medir y antes de dar por revertida: `comprobar-migraciones.sh` corre con `--no-build` y lee los
binarios del disco, no el código.

| # | Mutación | Línea base | Resultado |
|---|---|---|---|
| 1 | La regla de la lista cerrada de tipos bloqueables, **escrita antes del arreglo** | `e3a9e9e` (main al abrir la rama) | **Rojo**, con los dos supervivientes nombrados |
| 2 | Un módulo que bloquea y **no expone su puerto** de consulta | `f9913d9` | **Rojo**, con el módulo nombrado |
| 3 | Un IBAN con el control válido y la **longitud de otro país** | `81b44fe` | **Rojo** |
| 4 | Un IBAN **con forma de real** en una *fixture* del frontal | `81b44fe` | **Rojo**, con el sitio y el valor enmascarado |
| 5 | `CondicionPago` aceptando **90 días** | `81b44fe` | **Rojo** en el dominio, en el borde y en el `CHECK` de la migración |
| 6 | La cuenta **por omisión duplicada** | `81b44fe` | **Rojo** por el efecto: dos preferentes en la ficha |
| 7 | `LimiteCredito` **sin divisa**, heredando en silencio la de la empresa | `81b44fe`, y otra vez sobre `c7b9f80` | **VERDE las dos veces en el carril rápido** — el hallazgo del ítem, abajo entero |
| 8 | Una **regla nueva fuera del censo** de su carril | `c7b9f80` | **Rojo**, con la regla nombrada |

**La 1, entera.** No es una mutación del código: es la regla escrita **contra el árbol tal como
estaba**, que es el orden que pedía el encargo —«el trabajo no empieza por añadir dos valores al
enum; empieza por la pregunta de por qué nada se puso rojo»—. Sobre `e3a9e9e`, `TipoDeRecursoBloqueado`
tenía tres valores y cinco agregados implementaban el bloqueo. Con
`dotnet test tests/Arquitectura.Tests/Bastion.Arquitectura.Tests.csproj --filter LoBloqueadoSeVeEntero`:

```
[xUnit.net]     Bastion.Arquitectura.Tests.LoBloqueadoSeVeEnteroTests
                .Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32 [FAIL]
  Shouldly.ShouldAssertException : sinSitio
      should be empty but had
  2
      items and was
  ["Tercero", "Usuario"]

  Additional Info:
      estos agregados se pueden bloquear y NO aparecen en `TipoDeRecursoBloqueado`, así que lo
      que se les reserva no sale en el listado del art. 32 y nadie puede consultarlo. No basta
      con añadir el valor: el enumerado vive en un módulo y la obligación es transversal, así
      que el módulo dueño tiene que exponer su puerto:
  · Tercero
  · Usuario
  Stack Trace:
    tests/Arquitectura.Tests/LoBloqueadoSeVeEnteroTests.cs(108,0)

Con error! - Con error: 1, Superado: 2, Omitido: 0, Total: 3
```

Los dos supervivientes son los que el listado no veía, y el mensaje ya dice que el arreglo **no** es
añadir dos valores al enumerado. Ese rojo doble —el de aquí y el de la mutación 2— es el entregable
del primer trozo del ítem; el arreglo vino después, en `f9913d9`.

**La 4, entera.** Sobre `81b44fe`, se cambió en `frontend/src/pruebas/datos.ts` un IBAN de relleno
por uno con el cuerpo variado y el control calculado —es decir, indistinguible de uno de verdad—.
El valor no se escribe aquí, que es precisamente lo que la regla defiende:

```
MUTACION 4 aplicada en frontend/src/pruebas/datos.ts
  Con error Bastion.Arquitectura.Tests.NingunDatoConFormaDeRealTests
            .Ningun_dato_con_forma_de_real_se_queda_escrito [355 ms]
  Mensaje de error:
   Shouldly.ShouldAssertException : hallazgos
    should be empty but had
1
    item and was
["frontend/src/pruebas/datos.ts:231 · IBAN «ES********************98»"]

Additional Info:
    estos sitios llevan escrito un dato con forma de real que no sale del espacio inventado de
    este proyecto. Un identificador válido es de alguien, y de un repositorio no se borra:
    sustitúyelo por un valor de relleno con su control calculado, como hace el resto de la suite.

Con error! - Con error: 1, Superado: 3, Omitido: 0, Total: 4
```

Tres cosas que este rojo demuestra y que un negativo genérico no habría demostrado: que el barrido
**alcanza al frontal** y no solo a `src/` y `tests/` de C#; que **enmascara lo que encuentra** —el
mensaje enseña el país y dos cifras, no el número—; y que la extensión al IBAN reutiliza el mismo
mecanismo del identificador fiscal en vez de duplicarlo, porque la lista de formas prohibidas se
compara **entera y en los dos sentidos** contra lo que el barrido sabe detectar.

**La 7 salió verde, y ese es el segundo hallazgo del ítem.** La mutación era la del encargo: quitar
la exigencia de divisa en `FijarLimiteCredito.LeerImporte` y devolver `Importe.De(cantidad, "EUR")`
cuando el cuerpo no la trae. Con ella puesta, el carril rápido entero siguió en **624/624**, sin un
solo caso rojo. La decisión «la divisa NO se hereda en silencio» estaba escrita en un `remarks` y en
un comentario, y **no la guardaba ningún test**: `Terceros.UnitTests` no referencia `Application` a
propósito (§13 — «dominio puro, sin infraestructura y sin E/S»), y `Api.FunctionalTests` no puede
llegar al caso de uso porque este carga el agregado **antes** de validar, que es el orden 404-antes-
que-400 que sigue todo el código. Su única casa posible es el contrato con base de verdad.

Escrito en `c7b9f80`: `ContratoDeLoQueCuelgaTests`, diez casos, y el que cierra el agujero afirma
**el efecto** y no solo el código —400, el campo `divisa`, y que al releer el recurso **sigue sin
haber límite**—, porque el modo de fallo que importa no es dejar de contestar 400: es contestar 200
con una divisa que nadie escribió. **Y aquí está el límite honesto: esa regla vive en el carril de
integración, que esta máquina no puede ejecutar.** Reaplicada la mutación sobre `c7b9f80`, el carril
rápido volvió a salir verde —lo esperado, porque los diez casos nuevos no corren en él—. El run de
la CI es el primero que los ejecuta.

**Y leído el run 34097268237, esto es lo que se puede decir de la 7 y no más.** Los cuatro casos del
límite de crédito **corrieron y pasaron** sobre el código bueno, así que la regla existe, se ejecuta
y afirma algo — que es lo que faltaba por saber. Lo que **sigue sin haberse visto** es la regla
mordiendo: para eso habría que reaplicar la mutación en un run, y no se ha hecho. Así que de la 7 se
afirma que **el agujero está identificado, la regla escrita y el caso verde sobre código correcto**;
no que se haya visto rojo con la mutación puesta. La diferencia importa: un caso que pasa sobre
código bueno todavía puede ser un caso que pasa siempre.

**El culpable, nombrado por el registro que faltaba.** El suceso 8501 salió en las anotaciones del
[run 34120375406](https://github.com/AOjeda006/Bastion/actions/runs/34120375406) con lo que hacía
falta y nada más: `Contacto:Modified`, `CuentaBancaria:Modified`, `CondicionPago:Modified`. Los tres
hijos, en estado de **modificación** en el momento del choque. Y el caso permanente que lo escribe
—`Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado`— salió **verde** en ese mismo run:
el desenlace 8500 sobre la raíz, con `Tercero:Modified` y su `versionActual`, funciona.

**Y una corrección antes de que la corrigiera el run: la inferencia tenía tres ramas y se enunció
una.** Que `ActualAsync` devuelva nulo puede salir porque (a) ninguna entrada lleve testigo, porque
(b) lo lleve y `GetDatabaseValuesAsync` devuelva nulo, o porque (c) lo lleve, se lea, y el valor no
case con `is uint`. Se afirmó (a) como si fuera la única —el mismo error de estrechar una
enumeración, tercera vez—. El suceso 8501 las distingue por construcción, y sus entradas dijeron que
era (a): entre las entradas del choque no aparece `Tercero`.

**El mecanismo, y el ADR que ya lo contaba.** EF Core decide si una entidad recién aparecida en la
colección de un padre ya seguido es un alta o una fila que ya existía mirando si su clave **se
rellena al insertar**. Por convención marca toda clave `Guid` como `OnAdd`; aquí la pone la fábrica
del dominio —`Guid.CreateVersion7()`, a propósito, por localidad de índice—, así que venía siempre
puesta, EF concluía «ya existía» y emitía un `UPDATE` contra una fila que no estaba: cero filas,
choque, 412.

Eso es **exactamente el ADR-0010**, aceptado el 26-08-2026, cuatro fases antes. Allí salía `500`
porque no existía el manejador de choques; aquí sale `412`, y es **peor**: un 500 se investiga, un
412 se lee como «alguien guardó antes que tú», que es plausible y falso.

**Lo que el ADR-0010 daba por sabido y no lo era.** Descartaba por escrito una alternativa:
*«`ValueGeneratedNever()` en el mapeo. No sirve: la regla de EF Core es "la clave está puesta", y con
generación desactivada está puesta siempre»*. Es falso, y se midió sobre el modelo real y sin base
de datos:

| Declaración de la clave | Estado del hijo tras `DetectChanges()` |
|---|---|
| `ValueGeneratedOnAdd` (la convención de EF para `Guid`) | `Modified` → `UPDATE` → 412 |
| `ValueGeneratedNever` | `Added` → `INSERT` |

La pregunta que EF se hace no es si el valor está puesto, sino si la clave es **de las que se
rellenan al insertar**. Declarada `Never` no lo es. No cambió EF Core: cambió que el modelo dejó de
decirle algo que no era verdad. Queda en el **ADR-0032**, que sustituye esa frase del ADR-0010 y
deja en pie todo lo demás —incluido `Registrar`, que ahora es redundante y no se retira aquí: eso es
tocar un camino de Identidad que este ítem no tiene por qué tocar—.

**El arreglo es una declaración, no un rodeo.** La convención de cierre `LaClaveLaPoneElDominio`
marca `ValueGeneratedNever` toda propiedad `Guid` de una clave primaria, en los cinco contextos que
heredan de `ContextoDeModulo`. Una convención y no una línea por configuración porque no es mapeo de
una entidad: es una propiedad del sistema entero, y una lista por configuración es una lista de la
que la próxima entidad se cae sin ruido. **Sin migración**: `ValueGenerated` sobre una clave `Guid`
no produce diferencia relacional, y `comprobar-migraciones.sh` sigue diciendo que modelo y
migraciones coinciden en los cuatro módulos.

**El fallo pasó de necesitar PostgreSQL a durar un segundo.** `LoQueCuelgaNaceComoAltaTests` cuelga
los tres hijos de una ficha seguida y afirma el estado de la entrada. Sin contenedor, en el carril
rápido, y con su canario al lado —que el modelo tenga las tres entidades, que la ficha quede
`Unchanged`, y que una modificación de verdad salga `Modified`—, porque si nada se detectara,
`Added` podría salir por no haberse decidido nada.

**Y el hallazgo de segundo orden, que es el que más vale.** La regla que debía cazar esto existía,
miraba **justo esta propiedad** y la daba por buena: `Toda_entidad_tiene_su_clave_completa_antes_de_guardar`
filtraba por `ValueGenerated.OnAdd` y después exoneraba en bloque a toda clave `Guid`, con el motivo
escrito de que en un `Guid` eso no significa que la ponga la base. Cierto **del `INSERT`**, y ciego
al otro uso de `OnAdd`. Hoy no exime: exige la declaración positiva.

**Pero es que además no miraba Terceros.** Cinco reglas recorren el modelo entero —inquilinato,
auditoría, fechas, claves, tipos complejos— y cada una traía su lista de contextos **escrita a
mano**, con dos o tres dentro. Terceros entró en la fase 1 y no se añadió a ninguna: durante todo el
módulo, cuatro reglas que dicen «cada entidad declara…» lo estuvieron diciendo de **dos módulos de
cinco**, en verde. Es el defecto del artículo 32 con otra cara y en el mismo ítem —una obligación
transversal enumerada a mano, ciega a lo que llegue después—. Ahora el universo **se descubre**
(toda clase concreta que hereda de `ContextoDeModulo`) con su lista declarada al lado, para que un
descubrimiento corto salga rojo en vez de silencioso.

Lo que encontró al mirar Terceros por primera vez, todo declarado ya:

- **`Tercero.Version` faltaba** del inventario del ADR-0015, que se compara entero.
- **Los cinco tipos complejos de `Tercero`** no estaban en el barrido de auditoría —`LimiteCredito`
  y `RegimenFiscal`, del propio 1.6, incluidos—.
- **Los tres hijos heredan del tipo base y no llevan testigo.** Es exactamente la divergencia que
  ese caso anunciaba que algún día habría que declarar; ahora está declarada con su motivo —el
  testigo que gobierna su edición es el del tercero— en vez de permitida en blanco.
- **Y los tres no filtraban por empresa ni declaraban por qué**, que es un hueco de R8 del propio
  1.6. Se cierra como `RolDeMembresia`: motivo escrito **más** su `Set<>` prohibido por nombre. El
  de `Contacto` pesa más que los otros dos, porque esa tabla lleva nombre, correo y teléfono de
  personas identificadas y una consulta suelta los serviría de todas las empresas a la vez.

**Y una tercera vez lo mismo, esta ya sin excusa: dos ADR con el mismo número.** El ítem escribió
dos —el del artículo 32 y el de la clave— y los dos salieron `ADR-0031`. No lo cazó nadie: son dos
ficheros de texto, y ningún barrido miraba su numeración. No es cosmético, porque **el número es la
manera de citar un ADR** —lo citan los `remarks` del código, los mensajes de las aserciones, el PLAN
y los demás ADR—, así que un número compartido vuelve ambigua toda cita ya escrita: quien busque
«ADR-0031» encuentra dos decisiones distintas y no sabe cuál avalaba lo que estaba leyendo. Se
renumera el más nuevo a **ADR-0032** —fichero, título, las dos citas del ADR-0010 y las dos de
`LasPertenenciasNuevasSeInsertanTests`— y, antes de arreglarlo, se escribe la regla y **se ve roja
con la colisión puesta**: `ElNumeroDeUnAdrEsSuyoYDeNadieMasTests`. Tres afirmaciones, porque
renumerar es renombrar un fichero y renombrar un fichero deja la cabecera diciendo el número viejo,
que es la misma colisión escondida dentro.

**Seis mutaciones más, sobre árbol limpio**, revertidas restaurando copia —nunca con
`git checkout --`— y con `git status --porcelain` vacío y `grep -rn "MUTACION" src tests` sin
resultados después. Las cuatro primeras sobre la línea base `ecdd796`; las dos últimas sobre
`f2ab8d1`:

| # | Mutación | Línea base | Rojo |
|---|---|---|---|
| 9 | La convención **no se registra** | `ecdd796` | **Triple, y en dos módulos**: el efecto en Terceros (`Lo_que_se_cuelga…`), el canario del ADR-0010 en Identidad (`Colgarla_del_usuario…`) y la declaración (`Toda_entidad_tiene_su_clave_completa…`) |
| 10 | `TercerosDbContext` fuera de la lista declarada | `ecdd796` | `El_universo_de_modelos_es_el_declarado` |
| 11 | La ficha del arnés **no queda seguida** | `ecdd796` | Los **dos** casos de `LoQueCuelgaNaceComoAltaTests` — el canario es lo que impide que el principal salga verde por no haber decidido nada |
| 12 | `Contacto` sin su motivo declarado | `ecdd796` | `Ninguna_entidad_del_modelo_se_queda_sin_filtro_y_sin_motivo` |
| 13 | La cabecera de un ADR deja de decir el número de su fichero | `f2ab8d1` | `Cada_ADR_lleva_en_su_titulo_el_numero_de_su_nombre_de_fichero` |
| 14 | El barrido de ADR mira la carpeta equivocada (`docs/adr` → `docs`) | `f2ab8d1` | `El_barrido_encuentra_los_ADR_del_repositorio` |

La afirmación que falta en esa tabla es la de la colisión, y falta porque **no hizo falta mutarla**:
se vio roja contra el árbol real, con los dos `ADR-0031` dentro, antes de renumerar nada.

**Ítem 1.5 cerrado — el agregado, su identidad fiscal, y un conflicto que no revela:**
run **34046817118** sobre `279b8c7`, **success**, con **3 jobs contados en el propio run**
(`total_count: 3` de la API, no de la memoria): Frontal `101523261565` ✓ (17 pasos, 0 omitidos),
Backend `101523261629` ✓ (22 pasos, 0 omitidos) y Humo `101523797385` ✓ (24 pasos, 1 omitido). Los
tres carriles tal como el run los publica:

```
Dominio y arquitectura: 576 casos (576 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 182, Organizacion.IntegrationTests 5,
    Api.FunctionalTests 136, Api.IntegrationTests 1, Identidad.UnitTests 58,
    Arquitectura.Tests 23, Terceros.UnitTests 39
    (528 en 7 ensamblados en el 1.4: +48 casos y +1 ensamblado)

Integración (Testcontainers): 271 casos (271 correctos, 0 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 199, y 0 en los otros seis
    (241 en el 1.4: +30 casos, que son los que en esta máquina NO se pudieron ejecutar)

Frontal: arranque 402/450 KiB en 3 ficheros · total servido 548/900 KiB
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 20 ficheros, 9 .js y 1 .css

OpenAPI:  82 operaciones, 47 rutas /api/v1/, y el fichero del artefacto idéntico al del repositorio
Catálogo de `type`: 47 tipos, de 52 sitios de llamada — el artefacto que estrena el ítem (ADR-0030)
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 2903
      caracteres), el entorno desplegado sirve 1 empresa y las semillas cargan 12 tramos de
      impuesto y 15 unidades
```

**Y este ítem se cierra con una advertencia que vale más que el verde.** El primer run del ítem —el
[34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496)— salió **rojo por un
caso de 271**, y el caso acusaba al producto: la traza no anotaba lo que el art. 32 obliga a anotar.
No era el producto: era el **captador**, que llevaba desde que se escribió sin recibir una sola línea.
Está contado entero en *Verificado en local* → *La avería que la CI destapó*, y arreglado en
`dbabdea` con su canario en el carril rápido. Dos consecuencias que sobreviven al ítem: **(a)** las
dos filas de la tabla de mutaciones que ejecutó la CI se compararon contra una **línea base roja**, y
lo que las sostiene es el conjunto de casos rojos **nombrados**, no el color del *job* — corregido en
`279b8c7` en vez de dejado escrito; **(b)** un arnés de pruebas es código sin nadie detrás, así que
la pregunta que faltaba no era «¿el dominio cumple la regla?» sino «¿la regla mira lo que dice
mirar?».

**Y una consecuencia del 1.5 que se cobró fuera del 1.5: ningún identificador fiscal real, tampoco
en los ficheros que el ítem no tocaba.** El 1.5 dejó escrito en `NifTests` que el DNI de ocho cifras
consecutivas de todos los manuales **existe y es de alguien**, y que el CIF que había antes era el de
una empresa de verdad — y a la vez los dejó vivos en cinco sitios fuera de Terceros: la **semilla** de
la CI (`BASTION_SEMILLA_EMPRESA_NIF`), la *fixture* de MSW del frontal, dos ficheros de test de
Organización, la **prosa** del ADR-0025, y hasta un comentario que los citaba para señalarlos. El
cierre del ítem lo pedía con esas tres palabras exactas —«fixtures, semillas ni prosa»—, así que se
cierra: todo pasa a números inventados con su carácter de control **calculado** (`B99999997`,
`00000001R`, y `00000001A` donde el caso pedía un control equivocado a propósito), y las citas se
reescriben para **describirlos sin escribirlos**.

Lo que **no** se toca, y con motivo: las semillas numéricas de `LaBateriaGeneradaTests`. Un generador
que recorre quince números por las veintitrés letras produce identificadores válidos, y **todo
identificador válido es de alguien o lo será** — eso no lo arregla ningún generador, y pretenderlo
sería una promesa falsa. La regla que sí se puede cumplir es la que el ítem escribió: **no pegar el
ejemplo conocido**. Por eso la única semilla que se movió es la que materializaba como literal
esperado justo el número canónico.

**Ítem 1.4 cerrado — lo bloqueado se puede mirar, y mirarlo no devuelve la llave:**
run **33906512809** sobre `73c2836`, en `main`, **success**, con **3 jobs contados en el propio
run** (`total_count: 3` de la API, no de la memoria): Backend `101132593744` ✓, Frontal
`101132594066` ✓ y Humo `101133688191` ✓ — el de Humo esta vez **corrió**, no se omitió. Los tres
carriles tal como el run los publica:

```
Dominio y arquitectura: 528 casos (528 correctos, 0 con error, 0 omitidos) en 7 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 180, Organizacion.IntegrationTests 5,
    Identidad.UnitTests 58, Api.FunctionalTests 129, Api.IntegrationTests 1, Arquitectura.Tests 23

Integración (Testcontainers): 241 casos (241 correctos, 0 con error, 0 omitidos) en 7 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 169, y 0 en los otros cinco

Frontal: arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB
         `esquema.ts` al día con `docs/api/openapi.json`
         artefacto: 18 ficheros, 8 .js y 1 .css

OpenAPI:  75 operaciones, 43 rutas /api/v1/, y el fichero del artefacto idéntico al del repositorio
Migraciones: modelo y migraciones coinciden en todos los módulos con persistencia
Humo: el migrador aplica los tres contextos, la cuenta sembrada inicia sesión (testigo de 2721
      caracteres) y el entorno desplegado sirve 1 empresa
```

El run **anterior**, `33904700389` en `main` (y su gemelo `33904703923` en la rama), salió **rojo**
por un fallo de composición que en local no miraba nadie. Está contado entero abajo, en
*Verificado en local → La sexta, que no puse yo*.
`GET /api/v1/organizacion/bloqueados` entrega las empresas, almacenes y ubicaciones bloqueados de la
empresa activa, con su fecha de bloqueo y su **fecha de vencimiento**, a quien tenga
`organizacion.bloqueado.ver` y dejando en el registro **quién** ha preguntado. Es el **quinto** sitio
de `s_aperturasDeBloqueoPermitidas`, declarado con lo que lo distingue de los otros cuatro, y el
primero que no desbloquea.

**Lo que hay que leer primero de este ítem.** La afirmación del enunciado —«algo que se ponga rojo si
un camino de lectura de la API entrega un recurso bloqueado con testigo de versión»— se escribió
**antes** que el endpoint y se miró en rojo **con un DTO que sí lo llevaba**: la mutación 1 de la
tabla, entera más abajo. Salen dos rojos y no uno, y son preguntas distintas:
`Ningun_camino_que_ve_lo_bloqueado_emite_un_testigo_de_version` mira el **código fuente** —los
ficheros que abren el ámbito y los que producen `ConVersion<T>` tienen que ser disjuntos— y
`Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo` mira el **contrato**, recorriendo
los tipos que cada acción declara devolver. La segunda es la que nombra el endpoint y el campo.

**El motivo nuevo, y por qué no se reutilizó el que había.** El ADR-0027 decía en su decisión 1 que
el listado abriría el ámbito «con el motivo que ya existe». Al montarlo se vio que son dos cosas
distintas y que **la traza tiene que distinguirlas**: `AdministracionDelBloqueo` es el sistema
operando sobre el bloqueo, y esto es una persona **mirando datos personales reservados**. Bajo la
misma etiqueta, el registro que el art. 32 obliga a llevar diría lo mismo para las dos. El ADR lleva
su **corrección fechada** en vez de reescribir el texto de aquel día. Y con el segundo valor, el
enumerado pasa a ser una lista cerrada de dos con su propia regla, comparada entera y en los dos
sentidos: un valor declarado que nadie usa es una rama que nadie prueba.

**El vencimiento, que no estaba en el enunciado del ítem y sí en la nota abierta.** Un bloqueo del
art. 32 sin fecha de fin convierte una conservación acotada en indefinida, que es la infracción por
el otro lado; enseñar el listado sin esa columna habría sido enseñar lo segundo como si fuera lo
primero. `PoliticaDeRetencion` la contesta, y el plazo cuelga **del motivo**:
`SupresionSolicitada` vence, `CeseDeUso` **no vence nunca** —un almacén retirado se conserva por
razón contable y sus datos no son de nadie—, seis años por omisión (art. 30 del Código de Comercio,
el suelo más largo de los que le aplican a una pyme) y configurable por instalación. El `switch` es
exhaustivo y **lanza**: un tercer motivo tendría que decidir si vence en vez de heredar el «no
vence» que nadie habría decidido, y eso lo afirma un test con un motivo fuera del enumerado.

**El censo, autorizado explícitamente para este ítem.** `LasReglasDeEsteCarrilTests` censaba
**Arquitectura.Tests y solo ese ensamblado** desde el 0.12; los otros tres carriles con reglas
—`Api.FunctionalTests`, `Api.IntegrationTests` y `Organizacion.IntegrationTests`— tenían la rendija
abierta con **287 casos** dentro. Ahora los cuatro se censan contra su lista escrita, **310 nombres**
en total, con la consulta que los descubre en **un solo sitio** (`tests/Comun/CensoDeReglas.cs`,
enlazado con `Compile Include`): tres copias divergirían el día que una contara los `[Theory]` y otra
no. Las tres UnitTests se quedan fuera a propósito —son tests de dominio, no reglas de frontera— y
eso es una raya que se puede mover, no un olvido.

**Y el censo trajo un falso verde que llevaba ahí desde el 0.12.** `LasReglasDeEsteCarrilTests`
ordenaba con `orderby nombre, StringComparer.Ordinal`, que **no** es «ordena por nombre con este
comparador»: en la sintaxis de consulta son **dos claves de orden** —el nombre con el comparador de
la **cultura** y, a igualdad, una constante—, así que el ordinal no se aplicaba nunca. Con los 23
nombres de Arquitectura los dos órdenes coinciden y el carril salía verde; con 133 dejan de
coincidir, porque la cultura ordena «El_contador» antes que «El_NIF» y el ordinal al revés. Es la
misma familia que la nota de la cultura del ejecutor: verde justo en la máquina que tenía que avisar.
Corregido con `Order(StringComparer.Ordinal)` explícito, en el fichero compartido.

**Ítem 1.3 cerrado — el universo se descubre, y la copia divergente ya no compila:**
[run 33831276412](https://github.com/AOjeda006/Bastion/actions/runs/33831276412) sobre `edd0048`,
en verde con sus **tres jobs contados en el propio run** (`total_count: 3`) y ni un paso no verde:
Frontal 17 pasos, Backend 21, Humo 24. Los tres carriles con la cifra que publicó el run, no la que
se recuerda: **dominio y arquitectura 503 casos** (503 correctos, 0 con error) en 7 ensamblados,
**integración 237** (237 correctos, 0 con error) en 7, y el frontal con el contrato al día y el
presupuesto en 391/450 KiB de arranque y 532/900 KiB de total. El humo levantó el compose entero:
una empresa servida, sesión iniciada, y los maestros del migrador —12 tramos de impuesto, 15
unidades, IVA general vigente al 21,00 %—.
Los cuatro tipos de paginación viven en `Bastion.BuildingBlocks.Contracts` y las **ocho copias** de
Identidad y Organización están borradas; los doce listados aceptan `?sort=` y `?q=` con su lista
blanca y su tope; `POST /api/v1/organizacion/empresas/buscar` busca por **cuerpo** con cursor opaco,
con su exención escrita **en el mismo commit que el endpoint**; y `Todas()` lee su universo de la
tabla de enrutado del host, contrastada con los ensamblados `Endpoints` del disco.

**Lo que hay que leer primero de este ítem.** La **cuarta mutación** —divergir una copia
consolidada, `TamanioMaximo = 5000` en un módulo— **no sale roja: no llega a existir**. Se
intentaron las tres formas que hay y las tres las para el **compilador** (`CS0104` la copia del
contrato, `CS1503` la del borde, `CS1503` la que se salta el ayudante común y llama al caso de uso
directamente). Es la diferencia entre consolidar y copiar a un sitio nuevo, y está contada entera en
*Decisiones tomadas → ítem 1.3*. La **primera** también está entera, y por el motivo contrario: con
el universo escrito a mano restaurado y un `POST` sin proteger atendiendo peticiones, el fichero da
**siete verdes**; con el universo descubierto, **cuatro rojos**.

**Y una rendija que este ítem deja abierta a propósito:** `LasReglasDeEsteCarrilTests` censa
**Arquitectura.Tests y solo ese ensamblado** —sigue en **23** reglas, y allí lo único que cambió fue
la línea del test renombrado—. Las seis afirmaciones nuevas del 1.3 viven en `Api.FunctionalTests` y
en `Organizacion.IntegrationTests`, que **no tienen censo**: borrar una de ellas dejaría la suite
verde. No entra en el criterio del 1.3 y el checklist no se amplía por iniciativa propia, así que
queda anotado con su motivo, no tapado.

**Y una segunda rendija, esta descubierta en rojo y no de cabeza.** El primer run de este ítem
—[33830689761](https://github.com/AOjeda006/Bastion/actions/runs/33830689761)— salió **rojo**, y salió
rojo *bien*: `LaTraduccionASqlTests` hace que `Bastion.Organizacion.IntegrationTests.dll` empiece a
ejecutar casos en el **carril rápido**, y la lista de ensamblados declarados de ese paso —la defensa
que el 0.13 puso justamente para esto— exige declararlo. Se declaró, con su motivo escrito donde está
la lista.

Lo que importa no es el arreglo, que es una línea: es que **la batería local de `AGENTS.md` no podía
cazarlo**. La batería ejecuta los tests, pero no ejecuta `scripts/ci/recuento-de-tests.sh`, que es el
guion que decide el desenlace del paso en la CI. Así que hoy hay una clase entera de rojo —«un ítem
cambia qué ensamblados corren en un carril»— que pasa la batería entera en verde y solo aparece en el
run. El propio `AGENTS.md` dice que su lista «tiene que seguir siendo la de `.github/workflows/ci.yml`»
y se llama a sí mismo «el peor sitio posible para una mentira»; esto es una omisión de esa lista, no
una diferencia de comandos. Se reprodujo en local con el guion real sobre los `.trx` de esta máquina
—rojo nombrando el ensamblado, verde tras declararlo— así que no hace falta la CI para arreglarlo,
solo para haberlo visto. **Candidato para el ítem que toque el carril**, no para este: el 1.3 no lo
lleva en su criterio.

**Ítem 1.2 cerrado — la cuarta vía tiene quien la pare, y la quinta afirmación es la que la para:**
[run 33806271861](https://github.com/AOjeda006/Bastion/actions/runs/33806271861) sobre `c54f783`,
los **tres** *jobs* en `success` —Backend, Frontal y Humo— y **237 de 237** en el carril de
integración, que es donde viven los siete casos nuevos que aquí no se podían ejecutar. El mismo
árbol, ya en `main`, en el
[run 33806915660](https://github.com/AOjeda006/Bastion/actions/runs/33806915660).
`Organizacion.Contracts` publica `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e
`IConsultaDeDivisas`, los tres implementados, registrados y probados contra PostgreSQL; el cruce de
Identidad a `Organizacion.Contracts` está declarado; y `LosIdentificadoresAjenosTests` afirma las
cinco cosas de la tabla de *Decisiones tomadas → ítem 1.2*.

**Lo que hay que leer primero de este ítem.** La segunda mutación —un identificador de otro módulo
con **nombre que no casa** y **sin declarar**— **se queda verde con las cuatro afirmaciones del
enunciado**. La caza una sola cosa, y es la quinta afirmación, que no estaba: «ningún `Guid …Id` del
dominio se queda sin clasificar». Sin ella el ADR-0024 **describiría** la cuarta vía sin cerrarla,
porque el descubrimiento por nombre es exactamente lo que un identificador mal nombrado esquiva. Con
ella, el hueco está cerrado y comprobado: la mutación cae en un test y solo en uno.

El carril de arquitectura pasa de **18 a 23** casos.

### Verificado en local, con la salida real — addenda 1.12 a 1.14

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `7f676e2` (`main` al abrir la
rama, el cierre de la fase 1), con las líneas base que dio el usuario; el «después», `0d4062f`, sobre el
que corrió la batería entera de `AGENTS.md` **con Docker arrancado**, con el guion `bateria-112.sh` del
directorio de trabajo de la sesión, y la primera línea de su salida:
`HEAD 0d4062f · SDK 10.0.401 · docker 29.7.2`. Y de los commits de en medio, lo que se comprobó de
cada uno: `d7e2d91` es el arnés, y **se vio rojo** sobre el árbol de `7f676e2` —«le faltan 38 y le sobra
1», guion con 1—; `7c1b723`, el arreglo, con el mismo guion en verde; y `cb141e5` y `0e96a57`, que
tocan a la vez código de tests y censos, en un *worktree* aparte: `dotnet build` con 0 errores y
`Arquitectura.Tests` 44 de 44 y 49 de 49.

```
npm --prefix frontend run api + git status -- esquema.ts   → sin cambios
bash scripts/comprobar-migraciones.sh                      → modelo y migraciones coinciden en los 5
                                                             (Auditoría 4, Organización 5, Identidad 3,
                                                              Terceros 3, Catálogo 3): ninguna migración nueva
bash scripts/generar-openapi.sh --comprobar                → al día: 128 operaciones, en 74 rutas
bash scripts/generar-errores.sh --comprobar                → al día: 90 tipos, de 96 sitios de llamada
dotnet build Bastion.sln                                   → 0 errores, 0 advertencias
dotnet format Bastion.sln --verify-no-changes              → sin cambios

en `7f676e2`: 128 operaciones en 74 rutas · 89 tipos de 95 sitios
  → las mismas operaciones —el `409` nuevo es de un `PUT` que ya existía— y +1 tipo en +1 sitio:
    `permisos-de-rol-del-sistema`, con su texto en `es` y en `en`
```

Los dos carriles, con el recuento que decide el desenlace y las listas de ensamblados de la CI:

```
Dominio y arquitectura: 862 casos (862 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Identidad.UnitTests 61, Organizacion.UnitTests 183, BuildingBlocks.UnitTests 215,
    Terceros.UnitTests 85, Catalogo.UnitTests 78, Organizacion.IntegrationTests 22,
    Arquitectura.Tests 51, Api.FunctionalTests 154, Api.IntegrationTests 13
    (845 en `7f676e2`: +17, y cada uno con su clase —
     Identidad.UnitTests 58 → 61, los 3 nuevos de `RolTests` (`Alinear`);
     Arquitectura.Tests 37 → 51, los 5 de `LasDiecisieteReglasTests`, los 5 de
       `LasVariablesDelDespliegueTests`, los 2 de `LaRaizDelRepositorioSeBuscaEnUnSitioTests` y los 2
       de `ElEstadoDelReadmeEsElDelPlanTests`)

Integración (Testcontainers): 399 casos (399 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 325, y 0 en los otros siete
    (392 en `7f676e2`: +7, los 3 de `LasMigracionesSobreTablasConFilasTests` y los 4 de
     `ElRolDelSistemaTests`)

Frontal: 15 ficheros de prueba, 101 casos, 0 avisos de `act()`
  (15, 101 y 0 en `7f676e2`: ningún caso nuevo; el ítem solo toca `i18n/es.ts`, `i18n/en.ts` y el
   `esquema.ts` generado)
```

El reparto sale de los `.trx` del propio recuento, agrupados por `className`. **El canal de `act()`
sigue a cero**, medido en la salida de la suite: `grep -c "not wrapped in act"` → **0**.

```
npm --prefix frontend run typecheck · lint · format:check · build   → limpios
bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900

Frontal · arranque 426/450 KiB en 3 ficheros · total servido 592/900 KiB
  (426/450 y 592/900 en `7f676e2`: el mismo KiB; `index.js` 420 454 → 420 788 B, +334 B, los textos
   del `type` nuevo en los dos diccionarios)
```

**Dependencias, por conjuntos, con el guion que fija la convención** —el frontal **sin la raíz**—:

```
python scripts/dependencias-por-conjuntos.py 7f676e2 HEAD

7f676e2: 39 packages.lock.json · 125 pares nombre/versión · 30 Project · frontal 548 entradas sin la raíz
HEAD: 39 packages.lock.json · 125 pares nombre/versión · 30 Project · frontal 548 entradas sin la raíz
7f676e2 → HEAD:
  pares añadidos    []
  pares retirados   []
  Project añadidos  []
  Project retirados []
  frontal añadidas  []
  frontal retiradas []
```

Las **549** de la línea base son las mismas **548 más la raíz**: la misma cifra contada con la otra
convención, que es lo que el 1.14 deja escrito en el guion. Ninguna licencia nueva que revisar.

**El humo, porque el ítem toca el arranque.** El job «Humo» de la CI paso a paso, en local, en un
proyecto de *compose* aparte —`docker compose -p bastion-humo-112`— con un fichero de entorno de valores
aleatorios generado para la pasada y borrado al terminar. El paso 09 no está en la CI: pregunta a la base
por lo que el ítem cambia. El 18b es el paso nuevo de la CI, el segundo arranque, tal cual.

```
HEAD 0d4062f · docker 29.7.2 · proyecto bastion-humo-112
01 config               compose válido
02 construir            exit 0 (24 s)
03-05 levantar          api, postgres y web (healthy) · jaeger y otel-collector running
                        · migraciones Exited (0)
06 migrador             código 0 · esquema aplicado por los cinco contextos
07 semillas en imagen   impuestos.json unidades-de-medida.json
08 maestros en base     impuestos=12 unidades=15 iva_general_vigente=21.00
09 lo del addendum      el migrador del primer arranque dice SinRolesDelSistema: 1 · permisos del rol
                        del sistema en la base: 93 · claves ajenas entre esquemas: 0
10-11 salud             /health/live → Healthy · /health/ready → base-de-datos Healthy
12 sin credenciales     401
13-14 sesión y lectura  POST /api/v1/identidad/sesiones → 200 · GET empresas con testigo → 200, total=1
15-16 frontal           carga · por el proxy sin credenciales → 401
16b cuerpo por nginx    2 MiB + 1 byte → 413 type=[/errors/cuerpo-demasiado-grande] · 3 MiB + 1 byte → 413 type=[]
16c importación         una fila mala → 200 con su rechazo · misma clave y fichero → 200, mismos bytes
                        · misma clave y otro fichero → 409 idempotencia-cuerpo-distinto
16d purga               líneas de la purga de recibos en el registro de la API: 1
17 sin mapas            mapas dentro de la imagen: []
18 trazas               Jaeger conoce bastion-api (intento 1)
18b segundo arranque    exit 0 (13 s)
  ::notice::Tras el primer arranque, el rol del sistema «administracion» concede el catálogo entero: 93 permisos.
  ::notice::Estado viejo construido: el rol del sistema con los 55 permisos de la fase 0 y uno retirado; GET /api/v1/terceros/terceros -> 403.
  ::notice::Tras el segundo arranque, el rol del sistema «administracion» concede el catálogo entero: 93 permisos.
  ::notice::Tras el segundo arranque, con la semilla fuera, GET /api/v1/terceros/terceros -> 200.
FALLOS: 0
19 desmontar            down -v --remove-orphans
```

Después, `docker compose ls -a` sin ningún proyecto y ningún volumen ni contenedor con `humo` en el
nombre. Y `git status --porcelain` vacío al terminar la batería.

### Las ocho mutaciones de las addenda, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio y commiteado**, línea base `a1b2294`, el 2026-09-16. Cada una con copia de
respaldo y cambio anclado —el guion aborta si el texto que sustituye no aparece **exactamente una
vez**—, y revertida **restaurando la copia y tocando su fecha**. Después de cada una: el fichero igual
al original, `dotnet build` con 0 errores, `git status --porcelain` **vacío** y `grep -rn MUTACION` sobre
`src tests frontend/src db scripts deploy .github` **sin resultados** (en `docs/PLAN.md` sí hay: son las
actas de las tandas anteriores). Tres guiones del directorio de trabajo de la sesión:
`mut1.sh` (1, 1a y 8, contra el *compose*), la clase del arnés para la 2, y `mutar37.py` (3 a 7, con el
**carril rápido entero** y los rojos leídos del `.trx`).

**Qué corre cada una, y por qué no siempre los dos carriles enteros.** La 1 y la 8 no las ve ningún
carril de .NET —por eso existe el arnés—, así que corren el segundo arranque de verdad, con
`segundo-112.sh`: construye la imagen, levanta un *compose* aparte (`bastion-humo-mut1`…) con un
fichero de entorno aleatorio, llama a `scripts/ci/segundo-arranque.sh` y lo desmonta con sus
volúmenes. La 2 corre la clase del arnés, **sus tres casos**, y no el carril de integración entero:
el resto de ese carril migra siempre sobre bases vacías, que es justo el camino del caso de contraste
de la misma clase. Las 3 a 7 tocan documentos o fuentes de tests que solo lee `Arquitectura.Tests`, y
corren el carril rápido entero. Referencia sin mutar en `a1b2294`: rápido **862**, sin un rojo.

| # | Mutación | Dónde | Qué corre | Resultado |
|---|---|---|---|---|
| 1 | **El refresco de permisos, deshecho**: el rol se alinea en memoria, se cuenta y **no se escribe** | `ActualizarRolesDelSistema.cs` | segundo arranque | **rojo en el segundo arranque, verde en el primero**; entera abajo |
| 1a | **El refresco, deshecho del todo**: el rol se lista y no se alinea | `ActualizarRolesDelSistema.cs` | segundo arranque | primer arranque verde; rojo en el segundo, en `El migrador del segundo arranque no dice haber alineado el rol del sistema (suceso RolDelSistemaAlineado).` |
| 2 | **Una migración con una columna `NOT NULL` sin valor por omisión** | `db/migraciones/Terceros/` (fichero nuevo) | la clase del arnés | **rojo con filas, verde sobre vacías**; entera abajo |
| 3 | **Un identificador quitado de la tabla**: la fila de R13 | `docs/dominio/reglas-duras.md` | rápido: **2** de 862 | `LasDiecisieteReglasTests.Las_filas_son_las_diecisiete_una_vez_y_en_orden` —«Sobran: []. Faltan: [R13].»— y `.Toda_regla_que_cita_el_repositorio_tiene_su_fila` —«R13 en docs/adr/adr-0015-…md, tests/Arquitectura.Tests/LasDiecisieteReglasTests.cs»—: la etiqueta `r13` del ADR es la que la sigue citando |
| 4 | **Un identificador añadido a la tabla sin nada detrás**: una fila R(18) | `docs/dominio/reglas-duras.md` | rápido: **1** de 862 | entera abajo |
| 5 | **Una regla viva cuyo sitio de cumplimiento se borra**: `DireccionEstructuradaR17Tests.cs` | `tests/Organizacion.UnitTests/` | rápido: **1** de 843 | `LasDiecisieteReglasTests.Lo_que_la_tabla_nombra_existe` —«R17: `DireccionEstructuradaR17Tests` no está declarado en src/ ni en tests/»—. 843 porque el fichero borrado llevaba 19 casos |
| 6 | **La línea del README desalineada**: la casilla 1.11 desmarcada en el PLAN y el README igual | `docs/PLAN.md` | rápido: **1** de 862 | `ElEstadoDelReadmeEsElDelPlanTests.La_linea_de_estado_del_readme_es_la_del_checklist` —«Dice: … cerrada: 11 de 11 ítems. … Debe empezar por: … en curso: 10 de 11 ítems.»— |
| 7 | **Una copia de la búsqueda de la raíz, reintroducida**: una clase con su bucle de `.Parent` hasta `Bastion.sln` | `tests/Api.FunctionalTests/` (fichero nuevo) | rápido: **1** de 862 | `LaRaizDelRepositorioSeBuscaEnUnSitioTests.Nadie_mas_busca_la_raiz_del_repositorio`, con **las dos agujas**: «el nombre de la solución» y «el paso a la carpeta madre». La recogida lleva guardia, así que la mutación es real |
| 8 | **El primer arranque sobre base vacía sigue sembrando** —el control: el árbol sin mutar— | — | segundo arranque | **verde**: el primer arranque con el rol en 93 y `SinRolesDelSistema` del migrador, y el segundo, entero |

**La 1a existe porque la 1 sola dejaría una duda.** La 1 deja al migrador **decir** que ha alineado
—38 concedidos, 1 retirado y `RolDelSistemaAlineado`— sin escribir nada, así que pasa todas las
afirmaciones sobre el registro y la caza la comparación con el catálogo leída por la API. La 1a quita el
alineamiento entero, y la caza antes la afirmación sobre el suceso. Entre las dos: ni un migrador que
calla ni uno que miente dan verde. Deshacerlo **en la llamada** del migrador no era la mutación pedida:
se lleva también el `SinRolesDelSistema` del primer arranque, y el rojo saldría en el primero.

**La 1, la 2 y la 4, enteras.**

**Mutación 1 — el refresco de permisos, deshecho.** Es la más peligrosa de las dos variantes, porque
no deja rastro en el registro: el migrador cuenta lo que haría.

```diff
         if (resultado.Exists(actualizado => actualizado.Concedidos.Count > 0 || actualizado.Retirados.Count > 0))
         {
-            await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);
+            // MUTACION 1: el refresco deshecho; se calcula y se cuenta, y no se escribe.
+            _ = unidadTrabajo;
         }
```

```
HEAD a1b2294 + árbol de trabajo · proyecto bastion-humo-mut1
construida la imagen de la API
primer arranque levantado
::notice title=Segundo arranque::Tras el primer arranque, el rol del sistema «administracion» concede el catálogo entero: 93 permisos.
::notice title=Segundo arranque::Estado viejo construido: el rol del sistema con los 55 permisos de la fase 0 y uno retirado; GET /api/v1/terceros/terceros -> 403.
    (registro del migrador del segundo arranque: 38 × PermisoConcedidoAlRolDelSistema,
     1 × PermisoRetiradoDelRolDelSistema, 1 × RolDelSistemaAlineado con "Concedidos":38,"Retirados":1,"Permisos":93;
     y SemillaSinVariables en la API)
::error title=Segundo arranque::Tras el segundo arranque, el rol del sistema «administracion» no es el catálogo: le faltan 38 ['catalogo.articulo-proveedor.agregar', 'catalogo.articulo-proveedor.modificar', 'catalogo.articulo-proveedor.quitar', 'catalogo.articulo.crear', 'catalogo.articulo.modificar', 'catalogo.articulo.ver', 'catalogo.categoria.crear', 'catalogo.categoria.modificar', 'catalogo.categoria.ver', 'catalogo.linea-tarifa.agregar', 'catalogo.linea-tarifa.modificar', 'catalogo.tarifa.cerrar', 'catalogo.tarifa.crear', 'catalogo.tarifa.modificar', 'catalogo.tarifa.ver', 'organizacion.bloqueado.ver', 'organizacion.conversion-um.reincorporar', 'organizacion.conversion-um.retirar', 'organizacion.divisa.reincorporar', 'organizacion.divisa.retirar', 'organizacion.tipo-cambio.reincorporar', 'organizacion.tipo-cambio.retirar', 'organizacion.unidad-medida.reincorporar', 'organizacion.unidad-medida.retirar', 'terceros.condicion-pago.fijar', 'terceros.contacto.agregar', 'terceros.contacto.quitar', 'terceros.cuenta-bancaria.agregar', 'terceros.cuenta-bancaria.preferente', 'terceros.cuenta-bancaria.quitar', 'terceros.limite-credito.fijar', 'terceros.tarifa-asignada.fijar', 'terceros.tercero.bloquear', 'terceros.tercero.crear', 'terceros.tercero.desbloquear', 'terceros.tercero.importar', 'terceros.tercero.modificar', 'terceros.tercero.ver'] y le sobran 1 ['organizacion.permiso-de-una-version-anterior.ver'].
GUION exit 1 (14 s)
--- restaurada: build 0 Errores · porcelain:[] · grep MUTACION exit 1 (1 = limpio)
```

El primer arranque, verde: la semilla de la API crea el rol con el catálogo entero y el migrador no
tenía nada que alinear. El rojo es **exactamente** el que dio el arnés sobre `7f676e2` antes del
arreglo —38 que faltan y el retirado que sobra—, que es lo que dice que la mutación deshace el arreglo y
no otra cosa.

**Mutación 2 — una columna `NOT NULL` sin valor para las filas que ya están.** Una migración escrita a
mano en Terceros, con la marca de tiempo más nueva para que se aplique la última:

```diff
+// MUTACION 2 del ítem 1.12: columna NOT NULL sin valor por omisión.
+    [DbContext(typeof(TercerosDbContext))]
+    [Migration("20260916000000_MutacionColumnaObligatoria")]
+    public partial class MutacionColumnaObligatoria : Migration
+    {
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<string>(
+                name: "mutacion_obligatoria",
+                schema: "terceros",
+                table: "terceros",
+                type: "text",
+                nullable: false);
+        }
```

```
dotnet test tests/Api.IntegrationTests --filter "FullyQualifiedName~LasMigracionesSobreTablasConFilasTests"

Failed  Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila
   Shouldly.ShouldAssertException : recorrido.Fallos
    should be empty but had
1
    item and was
["20260916000000_MutacionColumnaObligatoria no se aplica con filas: 23502: column "mutacion_obligatoria" of relation "terceros" contains null values"]
    una instalación con datos no pasaría de aquí: 20260916000000_MutacionColumnaObligatoria no se aplica con filas: …
Passed  Y_sobre_tablas_vacias_se_aplican_igual_que_en_la_base_de_los_tests
Passed  Recorre_todos_los_contextos_que_tienen_migraciones
Con error! - Con error: 1, Superado: 2, Omitido: 0, Total: 3

--- restaurada (fichero borrado): build 0 Errores · porcelain:[] · MUTACION: nada en el código
```

Rojo sobre la tabla con filas y verde sobre la vacía, **en la misma pasada y la misma clase**: si los dos
se hubieran puesto rojos, lo roto sería el recorrido. El `23502` es el que daría el `migraciones` del
*compose* en la primera instalación con un tercero dentro, y la API no llegaría a arrancar.

**Mutación 4 — una regla que el §6 no tiene.** La fila nombra un sitio que **sí** existe, para que el
rojo no pueda venir de la comprobación de existencia sino de la de las diecisiete. El identificador
inventado va escrito en esta acta como R(18), salvo donde la salida lo entrecomilla: con la R y las
cifras pegadas, el acta **citaría** una regla sin fila, y la regla que describe se pondría roja —la
misma mutación, sin revertir—.

```diff
 | R17 | Las direcciones se guardan en campos estructurados. | viva | `Direccion` guarda los campos … |
+| R(18) | MUTACION 4: una regla que el sexto apartado no tiene. | viva | `ElFiltroNoSeSaltaPorAhiTests`. |
```

```
carril rápido: exit 1 · 862 casos, 861 correctos, 1 con error
  [Failed] Bastion.Arquitectura.Tests.LasDiecisieteReglasTests.Las_filas_son_las_diecisiete_una_vez_y_en_orden
      Shouldly.ShouldAssertException : enLaTabla
          should be
      ["R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "R16", "R17"]
          but was (case sensitive comparison)
      ["R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "R16", "R17", "R18"]
          difference
      ["R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "R16", "R17", *"R18"*]

      Additional Info:
          las filas de docs/dominio/reglas-duras.md no son R1 a R17, una vez cada una y en orden. Sobran: [R(18)]. Faltan: [].
--- restaurada: original=True · build exit 0 · porcelain=[] · MUTACION=[] · sin seguir=[]
```

Un solo rojo, y es el de la lista: la fila está bien formada, su estado es uno de los tres y lo que nombra
existe, así que las otras cuatro afirmaciones siguen en verde. Lo que la tabla no puede afirmar es que
R1…R17 sean **las** del plan maestro, y eso está escrito en la propia tabla, no disimulado aquí.

### Lo que la CI encontró y en local no se veía — addenda 1.12 a 1.14

El primer *run* de la rama —[35101097378](https://github.com/AOjeda006/Bastion/actions/runs/35101097378)
sobre `1f69ba5`— salió **rojo en Humo**, en el paso nuevo, y el rojo **no era del arreglo**: Frontal
`104810441337` y Backend `104810441683` verdes, con 862 y 399 casos, y dentro del paso el migrador del
segundo arranque dijo lo que tenía que decir —38 concedidos, 1 retirado y `RolDelSistemaAlineado`, en
la anotación del «Diagnóstico»—. Lo que falló fue la última comprobación del guion, con el aviso «La API
del segundo arranque no dice que la semilla se queda fuera: las variables no se han retirado».

**El aviso afirmaba una causa, y la causa era otra.** Dos hipótesis, cada una con su prueba:

- **Que las variables no se retirasen en la CI**, que lee `deploy/.env` por omisión y no con
  `--env-file`, como el humo local. **Descartada:** `docker compose config api` sobre una copia del
  *compose* con un `.env` de marcadores y las ocho vacías en el entorno da siete `""` y `ES` —el `:-ES`
  del *compose*—, igual que con `--env-file`.
- **Que `logs | grep -q` fuese rojo por sí mismo.** Con `pipefail` —que el guion pone en su cabecera—,
  `grep -q` sale en la primera coincidencia, y `compose`, si aún le queda algo
  por escribir, recibe SIGPIPE: la tubería es roja **con la línea encontrada**. **Confirmada en Linux**,
  en un contenedor `docker:cli` (Compose v5.5.1), con un servicio que escribe la línea y N líneas
  detrás, treinta intentos por forma:

```
N detrás   logs | grep -q    logs > fichero; grep -q fichero
   20        0 rojos de 30     0 rojos de 30
  100       30 rojos de 30     0 rojos de 30
  400       30 rojos de 30     0 rojos de 30
```

Depende de cuánto venga **detrás** de la línea, y eso explica los dos lados del mismo guion: la
comprobación del migrador en el primer arranque pasó en la CI porque `SinRolesDelSistema` es la
**última** línea de su registro —la 60 de 60, 44 571 bytes, medido levantando solo PostgreSQL y el
migrador—, y la de la API falló porque la API sigue escribiendo después de decirlo. **En
Windows no se reprodujo:** el humo local de `0d4062f` corrió el mismo guion en Git Bash y salió verde.

**El arreglo:** los dos registros se vuelcan a un fichero y se busca en el fichero, que es lo
que ya hacía la comprobación del migrador del segundo arranque, con el porqué en un comentario. Y el
aviso deja de afirmar una causa que no ha comprobado: dice qué sucesos de la semilla **sí** escribió la
API, porque el registro de un *job* no se lee sin credenciales y la anotación es lo único a la vista.

**Verificado en Linux contra la pila real**, y no solo en Windows: un proyecto de *compose* aparte
(`bastion-humo-113`), levantado de cero para cada guion, y el guion ejecutado desde un contenedor
`docker:cli` con `bash`, `python3` y `curl`, que es la plataforma de la CI:

```
bash humo-113.sh   (el guion de 1f69ba5 y el arreglado, cada uno sobre una pila de cero)
docker 29.7.2 · Linux 6.18.33.2-microsoft-standard-WSL2 · Docker Compose version v5.5.1

guion de 1f69ba5    exit 0   93 permisos tras el primer arranque · 55 y uno retirado, 403
                             · 93 tras el segundo · 200 con la semilla fuera
guion arreglado     exit 0   los mismos cuatro avisos

el registro REAL de la API tras el segundo arranque: 41 líneas, 40 detrás de SemillaSinVariables
  logs | grep -q                      rojo 25 de 30
  logs > fichero; grep -q fichero     rojo  0 de 30
```

**El guion viejo salió verde aquí, y eso es lo que hay que leer bien:** no era un rojo seguro, era un
rojo cinco de cada seis veces sobre este registro, y el run de la CI cayó en el rojo. Un verde suelto del
guion viejo no dice nada; los treinta intentos sobre el mismo registro, sí.

**Las otras tres búsquedas con `| grep -q` de `ci.yml` no tienen esta avería, y se ha mirado por qué:**
«El migrador ha dejado el esquema al día», la sonda de disponibilidad y «Las trazas llegan al visor»
no ponen `pipefail`. Solo lo ponen, a mano, los tres pasos que construyen y levantan, y esos no buscan
nada, así que en las tres el estado de la tubería es el de `grep`. Si un paso de esos lo pusiera algún
día, la avería volvería.

**La regla que queda:** un guion que decide el desenlace de la CI se ejecuta en local **en la
plataforma de la CI**, no solo en la de desarrollo —en Git Bash, esta tubería salía verde—; y una
comprobación que puede fallar por azar se mide repitiéndola, no ejecutándola una vez.

### Verificado en local, con la salida real — ítem 1.11

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `051449d` (main al abrir
la rama); el «después», `5aca832`, sobre el que corrió la batería entera. Detrás solo va `b44041e`,
que cambia **un comentario** de `ElReciboCaducaYSeBorraTests` —el que la mutación 8 desmintió, abajo—
y se verificó por su cuenta: `dotnet build` con 0 errores, `dotnet format --verify-no-changes`
limpio y la clase ejecutada, 4 de 4. Batería completa de `AGENTS.md` **con Docker arrancado**, las
dos pasadas con el mismo guion, y cada una imprime en su primera línea contra qué corrió:
`HEAD 051449d · SDK 10.0.401 · docker 29.7.2` y `HEAD 5aca832 · SDK 10.0.401 · docker 29.7.2`.

```
dotnet build Bastion.sln                                   → 0 errores, 0 advertencias
dotnet format Bastion.sln --verify-no-changes              → sin cambios (salida vacía)
bash scripts/generar-openapi.sh --comprobar                → al día: 128 operaciones
python -c "…len(json.load(open('docs/api/openapi.json'))['paths'])"  → 74 rutas
bash scripts/generar-errores.sh --comprobar                → al día: 89 tipos, de 95 sitios de llamada
bash scripts/comprobar-migraciones.sh                      → modelo y migraciones coinciden en los 5
                                                             módulos con persistencia
                                                             (Auditoría: 4, Organización: 5,
                                                              Identidad: 3, Terceros: 3, Catálogo: 3)

en `051449d`: 127 operaciones en 73 rutas · 80 tipos de 86 sitios · Auditoría 3
  → +1 operación en +1 ruta (`POST /api/v1/terceros/terceros/importacion`), +9 tipos, +9 sitios,
    y una migración en Auditoría (`20260914152255_CaducidadDeLosRecibos`)
```

Los nueve `type` nuevos son el del tope y los de la importación, y nada más: `cuerpo-demasiado-grande`
en el bloque común; `importacion-cabecera-no-valida`, `-codificacion-no-admitida`,
`-comillas-sin-cerrar`, `-demasiadas-filas`, `-fin-de-linea-no-admitido` y `-separador-no-admitido`
del dialecto; y `importacion-sin-permiso-de-alta` e `importacion-sin-permiso-de-limite`, los dos
`403` del caso de uso.

Los dos carriles, con el recuento que es **quien decide el desenlace** —las mismas órdenes que la
CI, con la lista de ensamblados esperados escrita entera—:

```
dotnet test Bastion.sln --filter 'Category!=Integracion' --logger trx \
  --results-directory artifacts/test-results/dominio
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio 'Dominio y arquitectura' 300 \
  'Bastion.Api.FunctionalTests.dll,Bastion.Api.IntegrationTests.dll,Bastion.Arquitectura.Tests.dll,…'

Dominio y arquitectura: 845 casos (845 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 215, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 85, Catalogo.UnitTests 78, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 154, Arquitectura.Tests 37, Api.IntegrationTests 13
    (748 en `051449d`, en 9 ensamblados: +97 casos, y cada uno con su clase —
     BuildingBlocks.UnitTests 132 → 215: los 45 de `CamposCsvTests`, los 34 de `LectorCsvTests`,
       los 3 de `RechazosDeImportacionTests` y 1 fila más en `ResultadoTests` (`DemasiadoGrande`);
     Api.FunctionalTests 145 → 154: los 4 de `ElTopeSeImponeAntesDelVolcadoTests`, los 2 de
       `ElFormateadorDeCsvNoLeeLaRedTests`, los 2 de `LaPlantillaDelFrontalEsLaDeLaApiTests` y 1 fila
       más en `PoliticaDeErroresTests` (la ruta que da `413`);
     Arquitectura.Tests 34 → 37, los 3 de `LasMarcasDeAlcanceTests`;
     Api.IntegrationTests 11 → 13, los 2 de `LaListaDeRastrosProhibidosTests`)

dotnet test Bastion.sln --filter 'Category=Integracion' --logger trx \
  --results-directory artifacts/test-results/integracion
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/integracion 'Integración (Testcontainers)' 100 \
  'Bastion.Api.IntegrationTests.dll,Bastion.Organizacion.IntegrationTests.dll'

Integración (Testcontainers): 392 casos (392 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 318, y 0 en los otros siete
    (367 en `051449d`, CON 1 ROJO —abajo—: +25 casos, los 9 de `LaImportacionDeTercerosTests`,
     los 8 de `LaImportacionEsUnaOperacionTests`, el de `LaImportacionAguantaFicherosHostilesTests`,
     los 4 de `ElReciboCaducaYSeBorraTests` y los 3 de `NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests`)

Frontal: 15 ficheros de prueba, 101 casos, 0 avisos de `act()`
  (14 y 95 en `051449d`: +1 fichero y +6 casos, los de `LaImportacionDeTerceros.test.tsx`)
```

El reparto por clase sale de los `.trx` del propio recuento, agrupando los `UnitTest` por
ensamblado y `className`, y **cuadra entero**: las clases nuevas suman sus casos y las modificadas
que no son `ResultadoTests` ni `PoliticaDeErroresTests` —`EntradaHostilTests`, `EsquemaDeIdentidadTests`,
los dos censos, `LasReglasDeEsteCarrilTests`…— no añaden ni quitan ningún `[Fact]`, `[Theory]` ni
`[InlineData]` en el diff.

**La base no estaba en verde, y se dice.** La batería de `051449d` salió con **un** caso rojo de 367:
`EntradaHostilTests.Un_tipo_de_contenido_que_no_es_JSON_es_415_y_tampoco_cuenta_nada`, con
`cuerpo should not contain "5432"` sobre una respuesta cuyo único `5432` estaba **dentro del
`traceId`** aleatorio (`7d91c1e17494b73e495432077f393034`). Es el rastro prohibido que cabía en un
identificador, arreglado en `a7dc05f` (*Decisiones tomadas → ítem 1.11*, decisión 0), y en `5aca832`
**ese mismo caso, por nombre, está en verde**. No es un rojo que el ítem tape: es uno que el ítem
empezó arreglando.

**El canal de `act()` sigue a cero**, medido en la salida de la suite y no contando llamadas:
`grep -c "not wrapped in act"` sobre la de `npm --prefix frontend run test` → **0** en las dos
pasadas.

```
npm --prefix frontend run api          → `shared/api/esquema.ts` sin cambios (git status limpio)
npm --prefix frontend run typecheck    → limpio
npm --prefix frontend run lint         → limpio
npm --prefix frontend run format:check → All matched files use Prettier code style!
bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900

Frontal · arranque 426/450 KiB en 3 ficheros · total servido 592/900 KiB
  (418/450 y 577/900 en `051449d`: +8 KiB de arranque y +15 de total)
    index.js   412 868 → 420 454 B   ·   index.css   13 810 → 14 421 B
    PaginaDeImportacion-*.js  6 702 B, diferida: cuenta en el total y no en el arranque
```

**El disparador de los 430 KiB no ha saltado, por 4.** Lo que sube el arranque son los textos de la
pantalla y de los nueve `type` en los dos diccionarios —`i18n/es.ts` +75 líneas, `i18n/en.ts` +72—;
la pantalla va en su ruta diferida. La regla (decisión 10) sigue escrita para quien la cruce.

**Licencias, por conjuntos y con el comando.** Un solo `packages.lock.json` cambia en el rango, el
de `BuildingBlocks.UnitTests`, porque ahora referencia `BuildingBlocks.Application`:

```
python licencias.py 051449d 5aca832      (pares `nombre/versión` de los `resolved` de TODOS los
                                          packages.lock.json, y las dependencias del package-lock.json
                                          del frontal SIN la raíz)

051449d → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
          · package-lock.json del frontal: 548 dependencias sin la raíz
5aca832 → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
          · package-lock.json del frontal: 548 dependencias sin la raíz
051449d → 5aca832: pares añadidos [] · retirados [] · Project añadidos [] · retirados []
                   · frontal añadidas [] · retiradas []

git diff --name-only 051449d 5aca832 -- Directory.Packages.props frontend/package.json \
  frontend/package-lock.json                                               → vacío
git diff 051449d 5aca832 -- '*.csproj' | awk '/^diff --git/{f=$4} /^[+-].*(PackageReference|ProjectReference)/{print f" -> "$0}'
  Bastion.BuildingBlocks.UnitTests.csproj -> + ProjectReference …/Bastion.BuildingBlocks.Application.csproj
```

Cero pares añadidos y cero retirados en los dos ecosistemas, y la única línea del diff es una
**referencia de proyecto**. El lector de CSV es código propio: ninguna licencia nueva que revisar.

**El humo, porque el ítem toca esquema y nginx.** El job «Humo» de la CI, paso a paso y **en local**,
en un proyecto de compose aparte —`docker compose -p bastion-humo-111`— con un fichero de entorno de
valores aleatorios generado para la pasada y borrado al terminar: ni el `deploy/.env` de desarrollo
ni sus volúmenes se tocan. Tres pasos **no están en la CI** y se añadieron para esta pasada: el 09,
que pregunta a la base por lo que el ítem migra; el 16c, una importación de verdad por el frontal; y
el 16d, que la purga arranca con la API. El 16b es el paso de la CI tal cual.

```
HEAD 5aca832 · docker 29.7.2 · proyecto bastion-humo-111
01 config               compose válido
02 construir            exit 0 (31 s)
03-05 levantar          api, postgres y web (healthy) · jaeger y otel-collector running
                        · migraciones Exited (0)
06 migrador             código 0 · esquema aplicado por los cinco contextos: Auditoría,
                        Organización, Identidad, Terceros y Catálogo
07 semillas en imagen   impuestos.json unidades-de-medida.json
08 maestros en base     impuestos=12 unidades=15 iva_general_vigente=21.00
09 lo del ítem en base  auditoria.claves_de_idempotencia.caduca_en: NO nulable, timestamp with time zone
                        · su índice: 1 · migración del ítem: 1 · claves ajenas entre esquemas: 0
10-11 salud             /health/live → Healthy · /health/ready → base-de-datos Healthy
12 sin credenciales     401
13-14 sesión y lectura  POST /api/v1/identidad/sesiones → 200 · GET empresas con testigo → 200, total=1
15-16 frontal           carga · por el proxy sin credenciales → 401
16b cuerpo por nginx    importación de 2 MiB + 1 byte → 413 type=[/errors/cuerpo-demasiado-grande]
                        · cuerpo de 3 MiB + 1 byte → 413 type=[]
16c importación         una fila con el número `NO-ES-UN-NIF` → 200; {"leidas": 1, "importadas": 0,
                        "rechazadas": 1, "rechazos": [{"columna": "identificacion_numero",
                        "motivo": "no-valido", "lineas": [2]}]}
                        · la misma clave con el mismo fichero → 200; mismos bytes: True
                        · la misma clave con otro fichero → 409; type=/errors/idempotencia-cuerpo-distinto
16d purga               líneas de la purga de recibos en el registro de la API: 1
17 sin mapas            mapas dentro de la imagen: []
18 trazas               Jaeger conoce bastion-api (intento 1)
FALLOS: 0
19 desmontar            down -v --remove-orphans: volumen bastion-humo-111_postgres-datos Removed
```

El 16c **afirma** la ausencia, no la deja a la vista: el guion hace `assert b'NO-ES-UN-NIF' not in b1`
sobre los bytes de la respuesta, y lo mismo con la razón social de la fila. Y después,
`docker compose ls -a` sin ningún proyecto, ningún volumen ni contenedor con `humo` en el nombre, y
el fichero de entorno y el testigo de la pasada borrados.

**El `415` que la batería encontró** —`[Consumes]` contestaba en el enrutador, antes de la
autorización— está contado entero en la decisión 8, con su arreglo en `d4f3ebb` y sus dos casos.

### Las ocho mutaciones del 1.11, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio y commiteado**, línea base `5aca832`, en **una sola tanda** que empezó el
2026-09-15 a las 07:52:26 con el guion `mutaciones.py` del directorio de trabajo de la sesión: copia
de respaldo, cambio anclado —el guion aborta si el texto que sustituye no aparece **exactamente una
vez**—, `git diff` guardado, `dotnet build`, **los dos carriles enteros** con `--logger trx` —no solo
el que la mutación parece alcanzar— y los casos rojos leídos **del `.trx`**, no de la consola.
Revertidas **restaurando la copia y tocando su fecha** (`shutil.copyfile` y `os.utime`: `copy2`
conserva la fecha vieja y deja la mutación anterior dentro del `.dll`). Después de cada una, el guion
comprueba que el fichero es el original carácter a carácter, que
`git status --porcelain --untracked-files=no` sale **vacío** y que
`grep -rn MUTACION src tests frontend/src` **no encuentra nada**; y al final de la tanda, `dotnet build`
del árbol restaurado:

```
##### final: build del árbol restaurado exit=0 porcelana=[] marcas=[]
```

Referencia sin mutar en `5aca832`: rápido **845**, integración **392**, los dos sin un solo rojo (la
batería de arriba). Los nombres van sin el espacio de nombres.

| # | Mutación | Dónde | Rápido | Integración | Qué se pone rojo |
|---|---|---|---|---|---|
| 1 | **Una fila mala aborta el fichero**: con un rechazo, el caso de uso devuelve el informe sin escribir ninguna | `ImportarTerceros.cs` | 0 | **3** | `LaImportacionDeTercerosTests.Las_filas_malas_no_impiden_que_entren_las_buenas_…` —`should be 3L but was 0L`, «una fila mala ha impedido que entraran las buenas»—, y `.Lo_que_ya_existe_activo_o_bloqueado_sale_en_el_mismo_grupo_…` y `.Una_fila_con_el_limite_mal_escrito_…`, las dos con `1L but was 0L`: la fila buena de su fichero tampoco entra |
| 2 | **Una fila mala se importa a medias**: los errores del límite de crédito no rechazan la fila, que entra sin límite | `FilasDeTerceros.cs` | 0 | **1** | `LaImportacionDeTercerosTests.Una_fila_con_el_limite_mal_escrito_no_deja_el_tercero_dado_de_alta_sin_limite` —`should be 0L but was 1L`, «una fila con el límite mal escrito ha entrado a medias»— |
| 3 | **La misma clave con otro fichero se acepta**: la comparación de huellas nunca da distinto | `FiltroDeIdempotencia.cs` | 0 | **2** | `LaImportacionEsUnaOperacionTests.La_misma_clave_con_otro_fichero_es_un_409_y_el_otro_fichero_no_entra` —`OK` en vez de `Conflict`, con `{"leidas":1,"importadas":1,"rechazadas":0,"rechazos":[]}`: el otro fichero **ha entrado**— y, del 0.9, `LaMismaClaveDevuelveElMismoRecursoTests.La_misma_clave_con_otro_cuerpo_es_409` (`Created`) |
| 4 | **La misma clave con el mismo fichero reimporta** | `FiltroDeIdempotencia.cs` | 0 | **3** | entera abajo |
| 5 | **Separador o cultura cambiados**: el importe se lee con la cultura invariante | `CamposCsv.cs` | **11** | **5** | entera abajo |
| 6 | **El informe pierde el número de línea**: cada grupo sale con la lista de líneas vacía | `RechazosDeImportacion.cs` | **1** | **4** | `RechazosDeImportacionTests.Se_agrupan_por_columna_y_motivo_en_el_orden_de_la_cabecera_con_las_lineas_ordenadas`; contra la base, los tres de `LaImportacionDeTercerosTests` que comparan el informe **serializado entero** —`"lineas":[3]` esperado, `"lineas":[]` recibido— y `LaImportacionEsUnaOperacionTests.El_peor_informe_posible_tiene_techo_…`, que pide `rechazo.Lineas.Count == 5000` |
| 7 | **El tope, comprobado después del volcado** | `LectorAcotadoDelCuerpo.cs` | **3** | **1** | entera abajo |
| 8 | **El plazo de la tabla de idempotencia, quitado**: el recibo nace con `CaducaEn = DateTimeOffset.MaxValue` | `RegistroDeIdempotencia.cs` | 0 | **1** | `ElReciboCaducaYSeBorraTests.El_recibo_nace_con_su_caducidad_a_las_24_horas_de_reclamarse` —`recibo.CaducaEn - recibo.CreadaEn should be 1.00:00:00 but was 2912185.17:28:52.4285789`, leído **de la fila guardada**— |

**Cinco de las ocho —1, 2, 3, 4 y 8— no las ve el carril rápido**, y no es un hueco sino dónde viven:
aislamiento por fila, idempotencia y caducidad son propiedades de lo que queda en PostgreSQL, y el
carril de integración corre en cada *run*.

**La 8 cae en un solo caso, y eso desmintió un comentario.** El de la clase decía que un recibo que no
caduca nunca «sale rojo en el segundo borde» de la purga. No sale: los casos de la purga toman el
vencimiento **que dice la fila**, sea el que sea, y purgar en `DateTimeOffset.MaxValue` también se lo
lleva. Lo único que ve el plazo quitado es la resta de las dos fechas guardadas, que es el caso que se
puso rojo. El comentario está corregido en `b44041e`; la cobertura no cambia, porque ya estaba donde
tenía que estar.

**El ruido en el mensaje de la 3 no es de la mutación.** El rojo de
`La_misma_clave_con_otro_fichero_…` trae pegado un «registro del servidor» con
`P0001: la escritura revienta a mitad` y su `DbUpdateException`. Es del caso vecino
`Si_la_escritura_revienta_a_mitad_no_queda_ni_una_fila_ni_el_recibo_…`, el único que crea ese
disparador, que corre antes en la misma clase y contra el mismo servidor; las dos peticiones del
caso mutado contestaron `200`.

**La 4, la 5 y la 7, enteras.** Las salidas son las del informe de la tanda
(`$TEMP/mutaciones/informe.txt`) y las de `python mensajes-trx.py N`, que lee los `.trx` de la
mutación N y saca de cada caso rojo su nombre y su mensaje; recortadas donde dice «…» y sin las
duraciones.

**Mutación 4 — la misma clave con el mismo fichero vuelve a hacer el trabajo.** El criterio pide que
el reenvío devuelva el resultado guardado **sin reimportar**, y un informe igual no lo prueba: un
fichero que ya entró, reimportado, daría otro informe —todo `ya-existe`—, pero uno entero de filas
malas daría **el mismo**. Por eso el caso de la importación mira **el efecto** antes que la respuesta:
si se ha vuelto a preguntar a la base qué identificadores están ocupados.

```diff
             if (!mia)
             {
+                // MUTACION 4: la misma clave con el mismo cuerpo vuelve a hacer el trabajo.
+                RegistroDeIdempotencia? previa = await almacen.BuscarAsync(clave, cancelacion).ConfigureAwait(false);
+
+                if (previa is not null && previa.CoincideElCuerpo(huella.Valor))
+                {
+                    await next().ConfigureAwait(false);
+                    return;
+                }
+
                 context.Result = await YaAtendidaAsync(almacen, clave, huella.Valor, cancelacion)
                     .ConfigureAwait(false);
                 return;
```

```
python mutaciones.py      (la tanda) → ===== MUTACION 4: misma clave con el mismo fichero reimporta
build exit=0
dominio: exit=0 casos=845 rojos=0
integracion: exit=1 casos=392 rojos=3
restaurado=True porcelana=[] marcas=[]

python mensajes-trx.py 4
== integracion: 392 casos, 389 correctos, 3 con error
[Failed] LaImportacionEsUnaOperacionTests.La_misma_clave_con_el_mismo_fichero_devuelve_el_mismo_informe_sin_volver_a_mirar_la_base
  Shouldly.ShouldAssertException : RegistroDeSucesos.Con(SucesoDeLaImportacionConOcupados)
      should be empty but had
  1
      item and was
  [Suceso { EventId = 8401, Mensaje = Importación de terceros con identificadores ocupados.
    Empresa: 01a0a3b0-8d7c-7994-9d3a-0d905c1c1aad. Ocupados: 3. De ellos, en fichas bloqueadas: 0. }]
  Additional Info:
      la repetición ha vuelto a mirar qué identificadores están ocupados: se ha ejecutado otra vez
[Failed] LaMismaClaveDevuelveElMismoRecursoTests.El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro
  Shouldly.ShouldAssertException : segunda.StatusCode
      should be
  HttpStatusCode.Created
      but was
  HttpStatusCode.Conflict
  Additional Info:
      {"type":"/errors/almacen-duplicado",…,"status":409,"detail":"La empresa ya tiene un almacén con el código IDEM-UNO.",…}
[Failed] LaMismaClaveDevuelveElMismoRecursoTests.De_dos_peticiones_simultaneas_con_la_misma_clave_solo_una_hace_el_trabajo
  Shouldly.ShouldAssertException : respuesta.StatusCode
      should be
  HttpStatusCode.Created
      but was
  HttpStatusCode.Conflict
  Additional Info:
      {"type":"/errors/almacen-duplicado",…,"status":409,"detail":"La empresa ya tiene un almacén con el código IDEM-A-LA-VEZ.",…}
```

**Rojo por el efecto, y el efecto se lee en el suceso.** El `8401` lo anota `RepositorioDeTerceros`
**cuando la consulta de ocupados encuentra alguno**, y la repetición lo anotó con `Ocupados: 3`: los
tres terceros que la primera petición del caso acababa de dar de alta. Es una lectura que la
repetición no hace, y la única manera de que aparezca es que el trabajo se haya ejecutado otra vez.
Los dos del 0.9 caen por el otro lado del mismo contrato: el almacén repetido **choca consigo mismo**
y contesta `409` donde tenía que devolver los bytes guardados.

**Mutación 5 — el importe se lee con la cultura invariante.** Es la que el dialecto existe para
impedir: el separador decimal adivinado. Nada revienta; los números cambian.

```diff
-        string invariante = coma < 0 ? cifras : $"{cifras}.{decimales}";
-        valor = decimal.Parse(invariante, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
+        // MUTACION 5: el importe se lee con la cultura invariante: punto decimal y coma de miles.
+        if (!decimal.TryParse(resto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor))
+        {
+            return false;
+        }
```

```
python mutaciones.py      (la tanda) → ===== MUTACION 5: separador o cultura cambiados
build exit=0
dominio: exit=1 casos=845 rojos=11
integracion: exit=1 casos=392 rojos=5
restaurado=True porcelana=[] marcas=[]

python mensajes-trx.py 5
== integracion: 392 casos, 387 correctos, 5 con error
[Failed] LaImportacionDeTercerosTests.Los_importes_se_leen_con_la_coma_decimal_y_se_ve_en_el_limite_guardado
  Shouldly.ShouldAssertException : await LimiteAsync(cliente, conMiles.Id)
      should be
  (1234, EUR)
      but was
  (1,2340, EUR)
  Additional Info:
      «1.234» es mil doscientos treinta y cuatro
[Failed] LaImportacionDeTercerosTests.Los_dos_CSV_que_escribe_Excel_en_espanol_entran_enteros_y_con_cada_valor_en_su_sitio(fichero: "terceros-csv-delimitado-por-comas.csv", empresa: 234, primerNif: 1)
  Shouldly.ShouldAssertException : informe
      should be
  InformeDeImportacionDto { Leidas = 3, Importadas = 3, Rechazadas = 0, … }
      but was
  InformeDeImportacionDto { Leidas = 3, Importadas = 2, Rechazadas = 1, … }
[Failed] … el mismo caso con (fichero: "terceros-csv-utf8.csv", empresa: 235, primerNif: 4): Importadas = 3 → 2
[Failed] LaImportacionDeTercerosTests.Las_filas_malas_no_impiden_que_entren_las_buenas_y_el_informe_dice_su_linea_de_Excel
  … should be 3L but was 2L · Additional Info: una fila mala ha impedido que entraran las buenas
[Failed] LaImportacionAguantaFicherosHostilesTests.Ningun_fichero_hostil_tumba_la_importacion_ni_cuenta_nada_ni_devuelve_lo_que_le_mandaron
  informe should be { Leidas = 3, Importadas = 3, Rechazadas = 0, … } but was { Leidas = 3, Importadas = 2, Rechazadas = 1, … }
  Additional Info: detrás de «vacío», el fichero bueno no entra entero

== dominio: 845 casos, 834 correctos, 11 con error      (CamposCsvTests)
Los_importes_a_la_espanola_se_leen_con_su_valor(campo: "1234,5", …)              valor should be 1234,5m  but was 12345m
Los_importes_a_la_espanola_se_leen_con_su_valor(campo: "-1,5", …)                valor should be -1,5m    but was -15m
Los_importes_a_la_espanola_se_leen_con_su_valor(campo: "99999999999999,9999", …) valor should be 99999999999999,9999m
                                                                                   but was 999999999999999999m
Los_importes_a_la_espanola_se_leen_con_su_valor(campo: "1.234", …)               valor should be 1234m    but was 1,234m
Los_importes_a_la_espanola_se_leen_con_su_valor(campo: "1.234,56"), ("12.345.678,9012") y ("99.999.999.999.999")
                                          CamposCsv.IntentarLeerImporte(campo, out decimal valor) should be True but was False
El_valor_leido_no_depende_de_la_cultura_de_quien_ejecuta(cultura: "", "de-CH", "en-US" y "es-ES")
                                   CamposCsv.IntentarLeerImporte("1.234,5", out decimal valor) should be True but was False
```

**Rojo por el valor importado, que es lo que se pidió.** El caso que manda es el primero: el límite
de crédito **guardado** vale `1,2340` —uno coma dos tres cuatro, impreso en la cultura de la máquina—
donde el fichero decía mil doscientos treinta y cuatro. No lo rechaza nada: la fila **entra**, con un
límite mil veces menor. Los dos ficheros exportados por Excel pierden **una fila cada uno**, y es la
del `1.234,56` —en cada fichero, la única con miles y decimales; la otra con límite dice `2500`—, que
la cultura invariante no sabe leer. Y el carril rápido enseña las dos caras del mismo error: `1234,5` **se
lee**, como doce mil trescientos cuarenta y cinco —la coma tomada por separador de miles—, y
`1.234,56` **no se lee**. La primera es la peligrosa, porque no avisa; el caso la fija por el valor.

**Mutación 7 — el cuerpo se vuelca entero y el tope se comprueba después.** Es el error natural al
escribir un lector acotado, y el resultado **parece** correcto: el fichero demasiado grande sigue
recibiendo su `413` con nombre. Lo que no se ve es que antes se ha leído entero.

```diff
         HttpRequest peticion = contexto.Request;
 
-        if (peticion.ContentLength > tope)
-        {
-            return Resultado.Fallo<ReadOnlyMemory<byte>>(ErroresDelCuerpo.DemasiadoGrande(tope));
-        }
-
-        int limite = (int)tope + 1;
-        using var acumulado = new MemoryStream((int)Math.Min(peticion.ContentLength ?? Trozo, limite));
-        byte[] trozo = new byte[Math.Min(Trozo, limite)];
-
-        while (acumulado.Length < limite)
-        {
-            int pedir = (int)Math.Min(trozo.Length, limite - acumulado.Length);
-            int leidos = await peticion.Body.ReadAsync(trozo.AsMemory(0, pedir), cancelacion).ConfigureAwait(false);
-
-            if (leidos == 0)
-            {
-                break;
-            }
-
-            acumulado.Write(trozo, 0, leidos);
-        }
+        // MUTACION 7: el cuerpo se vuelca entero y el tope se comprueba después.
+        using var acumulado = new MemoryStream();
+        await peticion.Body.CopyToAsync(acumulado, Trozo, cancelacion).ConfigureAwait(false);
 
         if (acumulado.Length > tope)
```

```
python mutaciones.py      (la tanda) → ===== MUTACION 7: tope comprobado después del volcado
build exit=0
dominio: exit=1 casos=845 rojos=3
integracion: exit=1 casos=392 rojos=1
restaurado=True porcelana=[] marcas=[]

python mensajes-trx.py 7
== dominio: 845 casos, 842 correctos, 3 con error
[Failed] ElTopeSeImponeAntesDelVolcadoTests.Con_un_Content_Length_mayor_que_el_tope_se_contesta_sin_leer_un_byte
  Shouldly.ShouldAssertException : cuerpo.Entregados
      should be
  0L
      but was
  262144L
  Additional Info:
      la petición declaraba más que el tope y aun así se ha leído
[Failed] ElTopeSeImponeAntesDelVolcadoTests.El_filtro_de_idempotencia_lee_con_el_tope_aunque_corra_sin_el_filtro_del_tope_delante
  Shouldly.ShouldAssertException : cuerpo.Entregados
      should be
  1025L
      but was
  262144L
  Additional Info:
      el filtro de idempotencia ha volcado el cuerpo sin tope para calcular la huella
[Failed] ElTopeSeImponeAntesDelVolcadoTests.Sin_Content_Length_se_leen_como_mucho_tope_mas_uno_bytes
  Shouldly.ShouldAssertException : cuerpo.Entregados
      should be
  1025L
      but was
  262144L
  Additional Info:
      el lector ha pedido a la red más de lo que hacía falta para saber que el cuerpo no cabía: un tope comprobado después del volcado no es un tope
== integracion: 392 casos, 391 correctos, 1 con error
[Failed] LaImportacionEsUnaOperacionTests.Sin_Content_Length_el_servidor_deja_de_leer_en_cuanto_el_fichero_no_cabe
  Shouldly.ShouldAssertException : corriente.Entregados
      should be less than
  3145728L
      but was
  67108864L
  Additional Info:
      el servidor ha seguido pidiendo el cuerpo después de pasarse del tope: se ha volcado antes de mirarlo
```

**Los cuatro miden lo mismo —cuántos bytes ha pedido el servidor a la corriente— y no el código de
respuesta**, que con la mutación sigue siendo `413`. Los tres del carril rápido usan un tope de
**1 KiB** y una corriente de **256 KiB** que cuenta lo que entrega: con el lector acotado, **0** bytes si
el `Content-Length` ya pasa del tope y **1 025** —el tope más uno— si no lo trae; con la mutación, los
262 144 enteros en los tres. El de integración lo mide con el tope de verdad, 2 MiB más 1 MiB de
holgura para los búferes, contra una corriente de **64 MiB sin `Content-Length`**, y con el manejador
desnudo del servidor de pruebas —el cliente de la fábrica lleva el de redirecciones, que copia el
cuerpo entero antes de mandarlo—: el servidor se llevó **los 67 108 864**. Y el del filtro de
idempotencia es el que justifica que el lector sea uno solo: ese filtro lee el cuerpo **antes** que el
formateador para calcular la huella, y sin el lector acotado sería él el primero en volcarlo.

### La fase 1, criterio por criterio, comprobado por algo observable

El cierre **no se apoya en que las catorce casillas estén marcadas**. Cada criterio —los once que
acordó la puerta de clarificación, los tres de las addenda y el de la fase entera del §15— se ha
vuelto a comprobar contra algo que se puede mirar hoy: un caso nombrado de los **1 261** que corren
en los dos carriles de .NET, cuyas
listas de ensamblados compara la CI **enteras** —así que en un *run* verde «existe» y «ha corrido» son
la misma cosa—, un fichero de la suite del frontal, o la salida de un paso del *run* que cierra la
fase. Cada nombre de la tabla se ha buscado en el árbol de `73d1db7`, uno por uno, antes de escribirlo
aquí, y otra vez en el de `f3c749e` al cerrar las addenda, que mueven ficheros de tests: siguen todos.

| Ítem | Lo observable |
|---|---|
| **1.1** el presupuesto del frontal, remedido | El paso «Presupuesto de tamaño» del *job* Frontal: `presupuesto-del-frontal.sh dist 450 900` publica **las dos métricas** —arranque y total servido— con el desglose fichero a fichero, y falla si no ha mirado nada. En el *run* de cierre: `arranque 426/450 KiB en 3 ficheros · total servido 592/900 KiB`. |
| **1.2** los puertos de Organización y la cuarta vía | `LosIdentificadoresAjenosTests.Ningun_identificador_del_dominio_se_queda_sin_clasificar`, `.Todo_identificador_de_otro_modulo_esta_declarado_con_su_puerto` y `.Cada_modulo_de_la_lista_tiene_su_cruce_y_su_puerto`: la lista declarada y el descubrimiento por reflexión, en los dos sentidos. |
| **1.3** el contrato de listado | `NingunCriterioSensibleViajaEnLaUrlTests.Ningun_listado_recibe_un_criterio_sensible_por_la_url`, sobre el explorador de API; `ContratoDeTercerosTests.El_cursor_del_tramo_anterior_trae_el_siguiente_y_no_repite`, que ejerce el cursor opaco con la primera búsqueda por cuerpo que existe; y `TodaEscrituraDiceComoSeProtegeTests.El_universo_cubre_a_todos_los_modulos_montados`. La mitad de «copias borradas» no tiene caso, y es lo correcto: la divergencia **no compila**. |
| **1.4** el camino de lectura de lo bloqueado | `ElAccesoReservadoDelArticulo32Tests.Un_almacen_bloqueado_desaparece_de_los_caminos_ordinarios_y_aparece_en_este`, `.El_listado_de_lo_bloqueado_no_devuelve_ninguna_llave_de_concurrencia` y `.Una_supresion_del_articulo_32_si_vence_y_la_fecha_sale_en_el_listado`; y `ElFiltroNoSeSaltaPorAhiTests.El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado`, que compara los sitios enteros. |
| **1.5** Terceros: el agregado y su identidad | `ContratoDeTercerosTests.El_identificador_espanol_se_valida_de_verdad_y_nace_verificado` y `.El_listado_viene_paginado_con_su_total_y_filtra_por_nombre`; `LaBateriaGeneradaTests.Todo_identificador_generado_con_el_control_movido_se_rechaza`; `ElConflictoQueNoRevelaTests.El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`. Las tres cláusulas del ADR-0030: el paso «Catálogo de errores» (`generar-errores.sh --comprobar`) y `ElCambioDeIdioma.test.tsx`. |
| **1.6** Terceros: lo que cuelga | `ContratoDeLoQueCuelgaTests.Noventa_dias_de_plazo_son_400_y_el_mensaje_lleva_el_tope_y_la_norma`, `.Sesenta_dias_clavados_se_aceptan_porque_el_tope_es_el_maximo_y_no_un_veto` y `.Un_limite_sin_divisa_es_400_y_despues_NO_hay_limite_heredado_de_la_empresa`; `LaBateriaDeIbanTests.Un_control_cambiado_se_rechaza` y `.Una_longitud_de_otro_pais_se_rechaza_aunque_el_control_cuadre`. |
| **1.7** la retirada y las dos conversiones | `LaInversaYElResolutorTests.El_par_que_habria_que_encadenar_no_se_resuelve` y `.Una_conversion_retirada_sigue_restringiendo_a_su_inversa`; `LaRetiradaNoEsUnBloqueoTests.La_coleccion_excluye_lo_retirado_por_omision_y_lo_trae_al_pedirlo`. |
| **1.8** Catálogo: artículo y categoría | `ContratoDeCatalogoTests.Crear_un_articulo_devuelve_201_con_Location_que_lleva_al_recurso`, `.Una_unidad_RETIRADA_no_vale_para_un_alta_y_el_articulo_que_ya_la_usa_sigue_resolviendola` y `.Mover_una_categoria_debajo_de_su_propia_descendencia_es_409_con_type_categoria_ciclo`; y `ElCicloSeCompruebaEnLasDosPuertasTests`, el lado del dominio. |
| **1.9** tarifas | `ContratoDeTarifasTests.Dos_tramos_del_mismo_codigo_que_se_pisan_los_rechaza_la_BASE`, `.Sin_linea_aplicable_hay_error_con_nombre_y_NUNCA_un_precio_cero` y `.Gana_el_antepasado_MAS_CERCANO_y_la_mas_honda_de_otra_rama_no_compite`; `PrecioODescuentoTests.Con_los_dos_puestos_no_se_construye`. |
| **1.10** los dos cruces mutuos | `ContratoDeLosCrucesTests.Inventado_ajeno_bloqueado_y_solo_cliente_contestan_el_MISMO_400_y_no_dejan_fila` y `.Una_tarifa_que_rige_hoy_se_asigna_se_lee_y_con_nulo_se_quita`; `LaMatrizDeLosPuertosDeEstadoTests.Cada_casilla_de_puerto_por_estado_esta_cubierta`. |
| **1.11** importación CSV | Las tres cláusulas, cada una con su caso: la fila como unidad, `LaImportacionDeTercerosTests.Las_filas_malas_no_impiden_que_entren_las_buenas_y_el_informe_dice_su_linea_de_Excel`, que también fija **línea y motivo**; y la idempotencia por fichero con su huella, `LaImportacionEsUnaOperacionTests.La_misma_clave_con_el_mismo_fichero_devuelve_el_mismo_informe_sin_volver_a_mirar_la_base` y `.La_misma_clave_con_otro_fichero_es_un_409_y_el_otro_fichero_no_entra`. Fuera del banco de pruebas, el paso de Humo «El frontal deja llegar a la API un cuerpo del tamaño de una importación». |
| **1.12** el segundo arranque | El paso «El segundo arranque deja al rol del sistema con el catálogo entero» del *job* Humo (`segundo-arranque.sh`), con sus cuatro avisos en el *run* de cierre: el catálogo entero tras el primer arranque, el estado viejo con `403`, el catálogo entero tras el segundo y la lectura con `200`. `ElRolDelSistemaTests.Recortado_y_con_un_permiso_retirado_el_despliegue_lo_deja_con_el_catalogo_y_lo_dice` y `.Cambiarle_la_lista_al_rol_del_sistema_es_409_y_no_toca_nada`; `RolTests.Alinear_ConcedeLoQueFaltaRetiraLoQueSobraYDiceCadaCosa`; y `LasMigracionesSobreTablasConFilasTests.Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila` y `.Recorre_todos_los_contextos_que_tienen_migraciones`. |
| **1.13** las diecisiete reglas | `LasDiecisieteReglasTests.Las_filas_son_las_diecisiete_una_vez_y_en_orden`, `.Toda_regla_que_cita_el_repositorio_tiene_su_fila`, `.Cada_estado_es_uno_de_los_tres_y_dice_donde_o_por_que` y `.Lo_que_la_tabla_nombra_existe`, sobre `docs/dominio/reglas-duras.md`. La mitad de «el enunciado es el del plan maestro» **no tiene caso, y no lo finge**: el plan maestro no está en el repositorio, y se contrasta a mano en cada puerta de fase. |
| **1.14** las higienes del cierre | `ElEstadoDelReadmeEsElDelPlanTests.La_linea_de_estado_del_readme_es_la_del_checklist`; `LasVariablesDelDespliegueTests.El_ejemplo_documenta_lo_que_interpola_el_compose_y_nada_mas`, `.Las_de_la_semilla_son_las_que_lee_su_codigo` y `.La_CI_solo_escribe_variables_que_el_ejemplo_documenta`; `LaRaizDelRepositorioSeBuscaEnUnSitioTests.Nadie_mas_busca_la_raiz_del_repositorio`; y la convención del frontal, en la cabecera de `scripts/dependencias-por-conjuntos.py`. Las dos preguntas de R9/R14 y R5 están en *Traspaso a la fase 2*: son preguntas, y no tienen caso. |
| **§15** el criterio de la fase | **Alta de cliente/proveedor con NIF validado:** `ContratoDeTercerosTests.El_identificador_espanol_se_valida_de_verdad_y_nace_verificado`. **Y de artículo con unidad, impuesto y tarifa:** `ContratoDeCatalogoTests.Crear_un_articulo_devuelve_201_con_Location_que_lleva_al_recurso` —unidad e impuesto validados por sus puertos— y `ContratoDeTarifasTests.La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba`. **Listados paginados y filtrados en servidor:** `ContratoDeTercerosTests.El_listado_viene_paginado_con_su_total_y_filtra_por_nombre` y `ContratoDeCatalogoTests.El_listado_de_articulos_viene_paginado_con_su_total_y_filtra_por_categoria`. **Dominio cubierto por tests:** `Terceros.UnitTests` con 85 casos y `Catalogo.UnitTests` con 78, en el carril rápido; y **ochenta y cinco mutaciones** repartidas por los once ítems —7, 8, 5, 5, 6, 14, 8, 8, 8, 8 y 8—, cada una con su resultado y su línea base en su sección de *Estado actual*. |

**Lo que el ítem y este repaso encontraron y no estaba marcado:** dos cosas, las dos de fuera de los
once criterios, y ninguna se arregla aquí. La primera, que el `SembrarAdministrador` no da los permisos
nuevos a una instalación que ya tiene usuarios, aunque su comentario diga que sí —en el traspaso,
como lo primero que mirar—. La segunda, que la búsqueda de la raíz del repositorio está copiada **seis**
veces en los tests, dos de ellas en el ensamblado que ya enlaza la compartida: contada con la orden
que va en el traspaso. **Las dos se cerraron en las addenda de la fase:** el rol, en el 1.12, y las
copias, en el 1.14.

### El veredicto de las notas abiertas

Ninguna se queda sin veredicto. Son **nueve**: las cinco que el cierre de la fase 0 dejó abiertas y
las cuatro que se abrieron después, y cada cabecera de *Notas / riesgos* dice ya el suyo. Cuatro se
cierran, y cada una dice dónde; cuatro siguen abiertas **y eso es la decisión, no un descuido**; una
es de vigilancia.

| Nota | Veredicto |
|---|---|
| `auditoria.claves_de_idempotencia` crece sin límite (0.9) | **Cerrada en el 1.11** (`5f36468`, ADR-0034 §4). Veinticuatro horas en la fila —`caduca_en`, calculado al reclamar— y una purga cada hora que vive en Auditoría. Se cierra **sin el dato** que la nota pedía, y se dice por qué: la tabla guarda la ficha de un tercero en cada `201`, y un dato personal sin plazo no puede esperar a una medición. La vigila la **mutación 8** del 1.11. |
| lo bloqueado sigue sin camino de lectura (0.11) | **Cerrada en el 1.4**. `GET /api/v1/organizacion/bloqueados`, con permiso y motivo propios, y desde el 1.6 recorriendo **todos** los módulos que bloquean por sus puertos: `ElAccesoReservadoDelArticulo32Tests.Un_almacen_bloqueado_desaparece_de_los_caminos_ordinarios_y_aparece_en_este`. |
| el día que un mensaje del servidor se enseñe, la i18n vuelve a la mesa | **Cerrada en el 1.2 y construida en el 1.5** (ADR-0030): el frontal escribe el texto desde el `type`. La sostienen el paso «Catálogo de errores» de la CI (`generar-errores.sh --comprobar`) y `ElCambioDeIdioma.test.tsx`, que compara los `type` contra los dos diccionarios. |
| los tests del frontal sueltan 109 avisos de `act()` | **Cerrada en el 1.8** (`9f8ff56`, 2026-09-10): venían del desmontaje, no del armazón. **Cero** en cada batería desde entonces, medido en la salida de la suite con `grep -c "not wrapped in act"`. Con la salvedad que dejó el 1.10 y que no la reabre: **bajo contención** también los da la base, así que un aviso solo es un hallazgo en una pasada sin competencia. |
| el bloqueo del art. 32 no tiene proceso de destrucción | **Abierta, con la mitad cerrada.** El plazo existe desde el 1.4 (`PoliticaDeRetencion`) y el vencimiento sale en el listado; lo que no existe es quien **destruya** al vencer. El 1.11 deja construida la forma —un trabajo de fondo en su módulo, con su ventana y su suceso sin datos: `PurgadorDeRecibos`—, pero destruir una ficha no es borrar un recibo: hay que decidir qué pasa con lo que la referencia, y a un tercero ya lo referencia `ArticuloProveedor` desde el 1.10. Necesita su sitio en la hoja de ruta, y ese sitio se pregunta, no se le da de paso. |
| el frontal no puede abrir la ficha de lo bloqueado | **Abierta, con la decisión tomada.** El precio del listado sin `GET` individual (ADR-0017): abrir la ficha es volver a exigir `If-Match` en **cinco** acciones —los cuatro desbloqueos de la fase 0 y, desde el 1.5 (`c69e03c`), el de Terceros—. Nada en la fase 1 lo ha pedido. |
| la renovación de sesión asierta el cuerpo | **Abierta, con la decisión tomada** (ADR-0019). No ha aparecido el generador de esquemas de Zod en el que confiar. Y el otro disparador —«que las aserciones dejen de ser una»— no ha saltado, aunque ya hay **dos** `as` sobre un cuerpo de respuesta: desde el 1.5, `problema as CuerpoDeProblema` en `shared/api/errores.ts` (`b7607e0`). Ese no afirma nada que no se mire —sus dos campos son `unknown` y cada uno pasa por un `typeof` antes de usarse— y lleva su test, `ElTextoDeUnFallo.test.tsx`, como pide el ADR. |
| el contrato describe los enteros como `integer \| string` | **Abierta, con la decisión tomada.** Estrechar el servidor para que el cliente quede cómodo sería mover el contrato para que encaje el consumidor. Hoy son **106** apariciones de `integer \| string` en el documento, y 14 de `number \| string`, contadas con la orden de la nota. |
| en Node 25 el `localStorage` de los tests no es el de jsdom | **De vigilancia, no de arreglo.** La máquina de desarrollo sigue en Node **25.6.1** (`node --version`) y la CI en **22** (`node-version: '22'` en `ci.yml`): el remiendo de `setupTests.ts` sigue haciendo falta aquí. El disparador es subir de Node, no una fecha. |

Y la que **no** es una nota sino una pregunta: **la correlación buscar/alta** (*Decisiones tomadas →
ítem 1.6*, decisión 1) sigue sin contestar, y **la importación la hereda tal cual**: una fila contra
una ficha bloqueada da `ya-existe`, igual que contra una activa, así que quien tenga `importar` y
`ver` aprende lo mismo que quien tenga `crear` y `ver`. No abre un canal nuevo —es el mismo cruce—,
pero lo ensancha a cinco mil identificadores por petición. Las tres salidas siguen escritas con su
coste; ninguna la puede tomar el agente.

### Traspaso a la fase 2

**Qué hereda.** Cinco módulos con persistencia —Organización, Identidad, Auditoría, Terceros y
Catálogo—, cada uno en su esquema y con sus migraciones al día (`comprobar-migraciones.sh`); el
contrato en **128 operaciones sobre 74 rutas** bajo `/api/v1/`, con el cliente del frontal
generado de él y **90 `type`** de error en el artefacto que la CI compara; `features/terceros/`
y `features/catalogo/` en el frontal; y una CI cuyos dos carriles de .NET suman **1 261 casos**
con las listas de ensamblados comparadas enteras, y cuyo Humo arranca dos veces sobre la misma base.
Y un mecanismo de importación que no es de Terceros
sino del bloque común —el lector acotado del cuerpo con `[TopeDelCuerpo]` y `[TipoDelCuerpo]`, el
formateador de `text/csv`, `CamposCsv` y `RechazosDeImportacion`—, listo para el siguiente maestro
que se importe.

**Qué import se añade a `CLAUDE.md` al empezar** (Anexo A.2.3, y solo este):
`@../BibliotecaDocumentacion/negocio/identificacion-articulos/convenciones.md`. Con él entra
`CodigoBarras`, que los ítems de la fase 1 que rozaban el artículo dejaron fuera con ese motivo.

**Qué invariantes de la fase 1 son portantes** —romper uno no da un fallo local, da una avería muda:

1. **Un puerto entre módulos pregunta por el estado, no por la existencia.** «¿Existe?» deja pasar lo
   que existe y no se puede usar. `LaMatrizDeLosPuertosDeEstadoTests.Cada_casilla_de_puerto_por_estado_esta_cubierta`
   **descubre** todo puerto de un `Contracts` que devuelva un enumerado y exige un caso por casilla:
   un puerto nuevo de la fase 2 entra en la matriz el día que se escribe, y un valor que ningún
   productor produce se ve, como `Bloqueado` en el 1.10.
2. **Desde fuera de un módulo, bloqueado, inventado y de otra empresa contestan lo mismo.** Es el
   art. 32 hecho contrato: el `NoExiste` de los puertos de Terceros cubre las tres cosas a propósito,
   y el alta que cruza contesta el mismo `400` con el cuerpo idéntico. Un módulo nuevo que distinga
   «no está» de «está bloqueado» en su respuesta revela lo que el bloqueo reserva.
3. **Todo cruce está declarado y la declaración se compara entera, en los dos sentidos.**
   `LasFronterasEntreModulosTests.El_unico_cruce_entre_modulos_va_por_contratos` y
   `LosIdentificadoresAjenosTests`: un cruce sin declarar es rojo, y una declaración sin cruce detrás
   también. Inventario referenciará artículos y almacenes; cada referencia, con su puerto.
4. **La retirada no es el bloqueo** (ADR-0023): `EstadoDeMaestro` tiene tres casillas, y
   `SoloResuelveLoViejo` significa que lo que ya lo usa lo sigue resolviendo y lo nuevo no lo puede
   elegir. Un documento de la fase 2 que lea una unidad retirada tiene que poder leerla.
5. **Ninguna clave ajena cruza esquemas, y se pregunta a la base.**
   `NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests` mira `pg_constraint` sin filtro de esquema, así que
   una clave escrita a mano en una migración —la que el modelo de EF no ve— también sale roja.
6. **Un cuerpo que se lee entero declara su tope y su tipo con los atributos del bloque común, y
   nunca con `[Consumes]`**, que contesta `415` en el enrutador, antes de la autorización. Lo vigila
   `LaPuertaDeCadaAccionTests` **por el efecto**, y con un límite que conviene saber: solo ve el
   `[Consumes]` de un tipo distinto del JSON con el que sondea.
7. **Un recibo de idempotencia dura veinticuatro horas, y una clave caducada es una clave nueva.**
   Toda acción con `Idempotency-Key` lo hereda sin escribir nada, y el contrato ya lo dice.
8. **Dos representaciones de la divisa conviven en el `Tercero` y no se comparan** (1.10, decisión 6):
   el código ISO dentro de `Importe` y el `Guid` de la tarifa asignada. La buena es el código, y el
   disparador para mover el `Guid` está escrito —antes de que la primera línea de un documento
   convierta un precio resuelto en un `Importe`—, y quien lo cruce lo mueve antes de escribir esa línea.
9. **El rol del sistema es el catálogo de la versión desplegada, y no se edita** (1.12, ADR-0035). Un
   permiso que declare la fase 2 le llega al administrador en el primer despliegue, sin migración ni
   semilla, y uno que se retire se le retira; `PUT` con otra lista es `409`. Un rol **propio** no lo
   toca ningún despliegue: los permisos nuevos no le llegan solos. El Humo lo prueba arrancando dos
   veces (`scripts/ci/segundo-arranque.sh`).
10. **Una migración se aplica sobre tablas con filas.** `LasMigracionesSobreTablasConFilasTests`
   recorre las de los cinco contextos una a una, inventando una fila en cada tabla antes de cada paso:
   una columna `NOT NULL` sin valor para las filas que ya están es roja ahí, aunque migre en verde en
   todas las bases vacías. Un contexto nuevo de la fase 2 entra en su lista o el caso que la compara
   con las migraciones compiladas sale rojo.

**Lo que la fase 1 deja abierto, cada cosa con su motivo** —ninguna es un olvido y ninguna se hace «de
paso» en la fase 2—:

- **La importación de artículos.** Un artículo se importa resolviendo por **código** su unidad, su
  impuesto y su categoría, y los puertos de estado solo reciben identificadores: es un contrato nuevo
  entre módulos (*Decisiones tomadas → ítem 1.11*, decisión 1).
- **La exportación**, con la regla del apóstrofo ya escrita para los campos que empiecen por `=`, `+`,
  `-`, `@`, tabulador o retorno (decisión 7 del 1.11).
- **La carrera de unicidad del alta** contesta un `500` limpio en vez de un `409`, igual en el alta
  suelta que en la importación: dos altas simultáneas del mismo identificador pasan las dos la
  lectura y la segunda choca con el índice.
- **Un cuerpo de más de 3 MiB** lo corta nginx con su `413` en HTML y sin `type`: la pantalla de
  importación enseña el texto genérico y ofrece reintentar, que no sirve de nada. El humo de la CI
  fija que eso es lo que pasa; lo que falta es que la pantalla lo sepa.
- **La destrucción al vencer el bloqueo del art. 32**, arriba en los veredictos.
- **La correlación buscar/alta**, pregunta sin contestar, y ahora también buscar/importar.
- **El arranque del frontal, a 4 KiB de la regla de los 430** (decisión 10 del 1.11): 426/450 en el
  *run* de cierre. La primera pantalla de la fase 2 que sume textos a los dos diccionarios la cruza, y
  lo que toca entonces ya está escrito —sacar del arranque el idioma no activo—, no subir el tope.
- **Ya no: las seis búsquedas privadas de la raíz del repositorio.** Borradas en el 1.14, con sus
  comentarios movidos a `tests/Comun/RaizDelRepositorio.cs`, y una séptima es roja
  (`LaRaizDelRepositorioSeBuscaEnUnSitioTests`).

**Lo que la fase 2 tenía que mirar el primer día ya no está abierto** (ítem 1.12). El rol de
administración no recibía los permisos nuevos en una instalación con usuarios, y ahora está medido, no
leído: a una base con el rol de la fase 0 le faltaban **38** permisos y le sobraba el retirado, y el
arnés lo vio en rojo antes del arreglo. Lo cierra la salida (c) del ADR-0035 —el migrador alinea el
rol del sistema en cada despliegue y la API no edita sus permisos—, que es el invariante 9 de arriba.

**Dos preguntas más para la puerta de clarificación de la fase 2** (ítem 1.14), con su coste y **sin
contestar**: no son deuda de la fase 1, son decisiones que la primera escritura con fecha y el primer
documento numerado vuelven reales. Lo que afirman está comprobado en el código de este cierre.

- **R9/R14 — el ejercicio cerrado.** `Ejercicio` guarda `Modificar` cuando está cerrado, pero
  `Cerrar()` y `Reabrir()` son dos asignaciones de una línea sin guarda (`Ejercicio.cs`), y nadie fuera
  de Organización pregunta si el ejercicio está abierto —`EstadoDeEjercicio` no aparece fuera de su
  módulo—, porque todavía no se escribe nada con fecha. El primer documento la vuelve real.
  **Qué hay que contestar:** qué exige cerrar, quién puede reabrir y con qué rastro, y quién pregunta
  antes de escribir con fecha. **El coste de contestarla:** un puerto nuevo en
  `Organizacion.Contracts` que conteste por el **estado** del ejercicio de una fecha —con su cruce
  declarado y sus casillas en `LaMatrizDeLosPuertosDeEstadoTests`—, unas condiciones de cierre que
  dependen de qué documentos existan, y una operación de reapertura que hoy no pide nada.
  **El de no contestarla:** el primer movimiento de stock se escribe en un ejercicio cerrado sin que
  nada lo impida, y «cerrado» deja de significar algo fuera de la pantalla de ejercicios.
- **R5 — la numeración sin huecos.** `Serie` ya lleva `Contador`, `RegistrarNumeroAsignado` y
  `SePuedeSuprimir => Contador == 0`; lo que no existe es la lectura-modificación-escritura bajo
  cerrojo que la hace sin huecos, y `RegistrarNumeroAsignado` solo se llama desde tests —uno de los
  cuales, `ContratoDeOrganizacionTests`, ya dice que «probaría un estado que el sistema no sabe
  producir»—. **Qué hay que contestar:** si los documentos de la fase 2 se numeran con `Serie`, y
  dónde vive el cerrojo. **El coste de contestarla:** la fila de la serie leída con bloqueo en la
  misma transacción que confirma el documento, lo que serializa las altas de esa serie; un caso contra
  PostgreSQL con dos confirmaciones a la vez; y qué pasa con el número de un documento que falla
  después de pedirlo. Una secuencia de PostgreSQL no lo resuelve por sí sola: no se deshace con la
  transacción, y deja el hueco. **El de no contestarla:** o los documentos de la fase 2 se numeran de
  otra manera y `Serie` se queda con un contador que nadie sube, o se numeran con ella sin cerrojo y
  dos altas simultáneas se llevan el mismo número. La numeración bajo concurrencia sigue siendo
  criterio de la fase 5 (`docs/dominio/reglas-duras.md`).

**Dónde retomar exactamente:** la **fase 2 · Inventario**, y **no por código**. Igual que la fase 1,
no tiene un Anexo A.3, así que lo primero es la **puerta de clarificación** del `CLAUDE.md` §2 —el
desglose en ítems con criterio verificable, acordado con el usuario y escrito aquí—, con las dos
preguntas de R9/R14 y R5 en su tanda, y con ella el import de arriba. Y el `CLAUDE.md` §5 sigue
diciendo que el objetivo del encargo es completar la fase 1: cambiarlo es del usuario.

> **Hecho el 2026-09-18.** La puerta está pasada: las dos preguntas que este traspaso dejó sin
> contestar son las decisiones **3** (R5) y **4** (R9/R14) de *Decisiones tomadas → puerta de la fase
> 2*, el import está en `CLAUDE.md`, el §5 lo cambió el usuario, y el desglose son los catorce ítems
> del *Checklist*. Este párrafo se queda como lo que era: el estado en el que la fase 1 entregó.

### Verificado en local, con la salida real — ítem 1.10

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `d9e4a46` (main al abrir
la rama); el «después», `3a7ba3e` —el último commit de código de la rama; lo que viene detrás solo
toca `docs/PLAN.md`—. Batería completa de `AGENTS.md` **con Docker arrancado**, y cada cifra con la
orden que la mide.

**El SDK cambió a mitad del ítem, y la batería entera es posterior al cambio.** Una actualización
de Visual Studio dejó la máquina en el SDK **10.0.401** (runtime 10.0.12) con la rama abierta. No
hizo falta tocar nada: `global.json` fija `10.0.100` con `rollForward: latestFeature`, que admite esa
banda. Lo que sí hizo falta es no mezclar: ninguna cifra de esta sección sale de antes del cambio, y
la propia batería lo imprime en su primera línea —`HEAD 3a7ba3e · SDK 10.0.401 · docker 29.7.2`—.

```
dotnet build Bastion.sln                                   → 0 errores, 0 advertencias
dotnet format Bastion.sln --verify-no-changes              → sin cambios (salida vacía)
bash scripts/generar-openapi.sh --comprobar                → al día: 127 operaciones
python -c "…len(json.load(open('docs/api/openapi.json'))['paths'])"  → 73 rutas
bash scripts/generar-errores.sh --comprobar                → al día: 80 tipos, de 86 sitios de llamada
bash scripts/comprobar-migraciones.sh                      → modelo y migraciones coinciden en los 5
                                                             módulos con persistencia
                                                             (Catálogo: 3, Terceros: 3)

en `d9e4a46`: 120 operaciones en 70 rutas · 75 tipos de 81 sitios · Catálogo 2, Terceros 2
  → +7 operaciones, +3 rutas, +5 tipos, +5 sitios, y una migración a cada lado del cruce
```

Los cinco `type` nuevos son los de los dos cruces y nada más: `articulo-proveedor-duplicado`,
`articulo-proveedor-no-encontrado` y `articulo-proveedor-tercero-no-valido` en Catálogo;
`tercero-tarifa-no-encontrada` y `tercero-tarifa-no-vigente` en Terceros.

Los dos carriles, con el recuento que es **quien decide el desenlace** —las mismas órdenes que la
CI, con la lista de ensamblados esperados escrita entera—:

```
dotnet test Bastion.sln --filter 'Category!=Integracion' --logger trx \
  --results-directory artifacts/test-results/dominio
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio 'Dominio y arquitectura' 300 \
  'Bastion.Api.FunctionalTests.dll,Bastion.Api.IntegrationTests.dll,Bastion.Arquitectura.Tests.dll,…'

Dominio y arquitectura: 748 casos (748 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 85, Catalogo.UnitTests 78, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Arquitectura.Tests 34, Api.IntegrationTests 11
    (723 en `d9e4a46`, en 9 ensamblados: +25 casos, y cada uno con su clase —
     Terceros.UnitTests 77 → 85, los 8 de `LaTarifaAsignadaTests`;
     Catalogo.UnitTests 66 → 78, los 12 de `ProveedoresDelArticuloTests`;
     Api.IntegrationTests 6 → 11, los 5 de `LaMatrizDeLosPuertosDeEstadoTests`)

dotnet test Bastion.sln --filter 'Category=Integracion' --logger trx \
  --results-directory artifacts/test-results/integracion
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/integracion 'Integración (Testcontainers)' 100 \
  'Bastion.Api.IntegrationTests.dll,Bastion.Organizacion.IntegrationTests.dll'

Integración (Testcontainers): 367 casos (367 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 293, y 0 en los otros siete
    (355 en `d9e4a46`: +12 casos, los 8 de `ContratoDeLosCrucesTests` y los 4 de
     `ElPuertoDeTercerosContraLaBaseTests`)

Frontal: 14 ficheros de prueba, 95 casos, 0 avisos de `act()`
  (los mismos 14 y 95 que en `d9e4a46`: el ítem no trae pantalla)
```

El reparto por clase no sale de la consola sino de los `.trx` del propio recuento, agrupando los
`UnitTest` por ensamblado y `className`. `Arquitectura.Tests` se queda en 34 aunque
`Inventario.cs` cambia: las listas declaradas crecen y los casos que las comparan son los mismos.

**El canal de `act()` sigue a cero, y se sigue midiendo en la salida de la suite**, no contando
llamadas en el código: `grep -c "not wrapped in act"` sobre la salida de
`npm --prefix frontend run test` → **0**.

```
npm --prefix frontend run api          → `shared/api/esquema.ts` sin cambios (git status limpio)
npm --prefix frontend run typecheck    → limpio
npm --prefix frontend run lint         → limpio
npm --prefix frontend run format:check → All matched files use Prettier code style!
bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900

Frontal · arranque 418/450 KiB en 3 ficheros · total servido 577/900 KiB
  (416/450 y 575/900 en `d9e4a46`: +2 KiB de arranque y +2 de total)
```

**El disparador de los 430 KiB no ha saltado, y lo que sumó está en el diff.** El frontal del ítem
son tres ficheros —`app/i18n/es.ts` (+18 líneas), `app/i18n/en.ts` (+12) y `shared/api/esquema.ts`,
que es generado y solo tipos—: los textos de los cinco `type` nuevos en las dos lenguas, y ninguna
pantalla. Es el mismo reparto que el 1.9 atribuyó módulo a módulo: lo que cuesta arranque no son las
pantallas, son sus textos.

**Licencias, por conjuntos y con el comando.** Doce `packages.lock.json` cambian en el rango, y por
eso mirar el diff no sirve: las dos referencias nuevas entre módulos y la de
`Terceros.UnitTests → Terceros.Application` meten entradas `Project` y hacen aparecer en ficheros de
bloqueo paquetes **que ya se resolvían en otros** —`Microsoft.Extensions.DependencyInjection.Abstractions`
10.0.9 entra en el de `Terceros.UnitTests`, por ejemplo—. Lo que se compara es el conjunto:

```
python licencias.py d9e4a46 3a7ba3e      (pares `nombre/versión` de los `resolved` de TODOS los
                                          packages.lock.json, y las entradas del package-lock.json)

d9e4a46 → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
          · package-lock.json del frontal: 549 entradas
3a7ba3e → 39 packages.lock.json · 125 pares nombre/versión distintos · 30 entradas de tipo Project
          · package-lock.json del frontal: 549 entradas
d9e4a46 → 3a7ba3e: pares añadidos [] · retirados [] · Project añadidos [] · retirados []

git diff --name-only d9e4a46 3a7ba3e -- Directory.Packages.props frontend/package.json \
  frontend/package-lock.json                                               → vacío
git diff d9e4a46 3a7ba3e -- '*.csproj' | awk '/^diff --git/{f=$4} /^[+-].*(PackageReference|ProjectReference)/{print f" -> "$0}'
  Bastion.Catalogo.Application.csproj  -> + ProjectReference …/Bastion.Terceros.Contracts.csproj
  Bastion.Terceros.Application.csproj  -> + ProjectReference …/Bastion.Catalogo.Contracts.csproj
  Bastion.Terceros.UnitTests.csproj    -> + ProjectReference …/Bastion.Terceros.Application.csproj
```

Cero pares añadidos y cero retirados en los dos ecosistemas, cero `PackageReference` en el diff: las
tres líneas son **referencias de proyecto**, dos de ellas los cruces del ítem. Ninguna licencia nueva
que revisar.

**El humo, porque el ítem toca esquema.** El job «Humo» de la CI, paso a paso y **en local**, en un
proyecto de compose aparte —`docker compose -p bastion-humo-110`— con un fichero de entorno de
valores aleatorios generado para la pasada y borrado al terminar: ni el `deploy/.env` de desarrollo
ni sus volúmenes se tocan. El paso 09 **no está en la CI**; se añadió para esta pasada, y pregunta a
la base del compose por lo que el ítem migra:

```
HEAD 3a7ba3e · docker 29.7.2 · proyecto bastion-humo-110
01 config               compose válido
02 construir            exit 0 (168 s)
03-05 levantar          api, postgres y web (healthy) · jaeger y otel-collector running
                        · migraciones Exited (0)
06 migrador             código 0 · EsquemaMigrado por los cinco contextos: Auditoría,
                        Organización, Identidad, Terceros y Catálogo
07 semillas en imagen   impuestos.json unidades-de-medida.json
08 maestros en base     impuestos=12 unidades=15 iva_general_vigente=21.00
09 lo del ítem en base  catalogo.articulos_proveedor: 1 tabla · terceros.terceros.tarifa_asignada_id: uuid
                        · migraciones del ítem: catalogo 1, terceros 1 · claves ajenas entre esquemas: 0
10-11 salud             /health/live → Healthy · /health/ready → base-de-datos Healthy
12 sin credenciales     401
13-14 sesión y lectura  POST /api/v1/identidad/sesiones → 200 · GET empresas con testigo → 200, total=1
15-17 frontal           carga · por el proxy sin credenciales → 401 · mapas dentro de la imagen: ninguno
18 trazas               Jaeger conoce bastion-api (intento 2)
FALLOS: 0
19 desmontar            down -v --remove-orphans: volumen bastion-humo-110_postgres-datos Removed
```

Y después, `docker compose ls -a` sin ningún proyecto y `docker volume ls` sin ningún volumen
`bastion-humo-110`.

**Lo que la batería destapó del arnés, y no del proyecto.** La primera pasada se paró a mitad para
corregir un comentario (`3a7ba3e`), y la segunda salió con **`dotnet build` en rojo: 52 errores
MSB3021**, todos «el proceso no tiene acceso al archivo porque está siendo utilizado por otro proceso»
sobre `tests/Api.IntegrationTests/bin`. No era el código: **parar la tarea de fondo no había parado
sus hijos**. El guion de la primera pasada seguía vivo con su `dotnet test` de integración, y al
matar a mano el `dotnet format` de la segunda, su guion **siguió al paso siguiente** en vez de
morir. Se localizó listando procesos con su línea de órdenes, se mataron los dos árboles con
`taskkill /T` empezando por el `bash` de cada guion, se comprobó que no quedaba ninguno y se relanzó
la batería **entera** desde limpio. Queda anotado porque un rojo leído en ese estado se atribuye al
código con toda naturalidad, y porque dos carriles contra el mismo `bin/` y el mismo
`artifacts/test-results` no solo rompen el build: contaminan el recuento.

**Y un rojo del frontal que era contención, reproducido en la base y comparado por nombre.** Una
pasada de `npm --prefix frontend run test` anterior a la batería salió con **2 ficheros rojos y 3
avisos de `act()`**: `ElSelectorDeEmpresa` y `ElTestigoDeAcceso`, los dos con `Test timed out in
5000ms`. Sola, **dos veces seguidas**: 95 de 95 y 0 avisos. Para no quedarse en «ha pasado a la
segunda», la suite entera en **cuatro copias en paralelo** (`npx vitest run` ×4), primero sobre el
árbol del ítem y después sobre `e48c409` —sacado con `git archive` a un directorio aparte, con el
mismo `node_modules`—, cuyo frontal es **idéntico** al de `d9e4a46`
(`git diff --name-only d9e4a46 e48c409 -- frontend` → 0 ficheros):

```
árbol del ítem   exit 1 ×4 · casos rojos por copia 1 · 1 · 3 · 6   · avisos de act() 0 · 2 · 5 · 1
e48c409 (base)   exit 1 ×4 · casos rojos por copia 1 · 24 · 12 · 11 · avisos de act() 0 · 5 · 6 · 6
```

Todos los rojos, en los dos lados, por plazo agotado pasados los 5 s. Y la comparación **por
nombre**, no por recuento: los seis casos distintos que se pusieron rojos en el árbol del ítem
—`El testigo de acceso > no llega nunca a localStorage ni a sessionStorage`, `El listado de
almacenes > mientras llega, dice que está cargando`, `Las rutas protegidas > sin sesión, cualquier
ruta protegida lleva a la pantalla de acceso`, `El cambio de ruta > anuncia, retitula y mueve el foco
al navegar`, `El selector de empresa > al cambiar de empresa, la lista pasa a ser la de la otra` y
`El cambio de idioma > arranca en el idioma que se le diga, y el documento lo declara`— están **los
seis** entre los 24 de la segunda copia de la base. No es del ítem: es la suite del frontal bajo
contención, que ya estaba así. Y deja una consecuencia para el canal: **un aviso de `act()` solo es
un hallazgo en una pasada sin competencia**, porque bajo contención la base también los da.

**La divisa, probada por el efecto y con dos canarios.** El caso
`Un_limite_en_dolares_y_una_tarifa_en_euros_conviven_porque_nadie_compara_divisas` afirma una
**ausencia** —nadie compara—, y una afirmación de ausencia que no ha visto nunca la presencia no
prueba nada. Así que se le puso delante lo que dice que no hay, una vez en cada orden, y se retiró.
Los dos canarios rechazan con un error que **ya existía** —`ErroresDeTercero.NoEncontrado`— para no
mover el catálogo de `type`; lo que cuenta es que el caso pide un 200 y ve otra cosa:

```
canario a — en `AsignarTarifa`, rechazar si hay límite y no está en EUR:
  Con error ContratoDeLosCrucesTests.Un_limite_en_dolares_y_una_tarifa_en_euros_conviven_porque_nadie_compara_divisas
  should be HttpStatusCode.OK but was HttpStatusCode.NotFound
  Additional Info: asignar una tarifa en euros a un tercero con el límite en dólares se ha
  rechazado: alguien compara las dos divisas, y la decisión del 1.10 es que no se comparan.
  {"type":"/errors/tercero-no-encontrado",…,"status":404,…,"instance":"/api/v1/terceros/terceros/…/tarifa-asignada"}

canario b — en `FijarLimiteCredito`, rechazar un límite que no esté en EUR si hay tarifa asignada:
  Con error ContratoDeLosCrucesTests.Un_limite_en_dolares_y_una_tarifa_en_euros_conviven_porque_nadie_compara_divisas
  should be HttpStatusCode.OK but was HttpStatusCode.NotFound
  Additional Info: fijar un límite en dólares a un tercero con una tarifa en euros se ha
  rechazado: la comparación está del otro lado.
  {"type":"/errors/tercero-no-encontrado",…,"status":404,…,"instance":"/api/v1/terceros/terceros/…/limite-credito"}

restaurado: build exit 0 · grep CANARIO: limpio · git status --porcelain:
   M tests/Api.IntegrationTests/Contrato/ContratoDeLosCrucesTests.cs
   M tests/Api.IntegrationTests/ElCensoDeEsteCarrilTests.cs
```

El `git status` **no** sale vacío, y lo que queda es exactamente lo que tenía que quedar: los
canarios se pusieron **antes** de commitear el caso, y esos dos ficheros son el caso y su línea del
censo —los dos únicos de `6781389`—. Ningún fichero de `src/` en la lista: los dos canarios se
fueron.

### Las ocho mutaciones del 1.10, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio y commiteado**, línea base `710532d`, con el guion
`mutaciones.py` del directorio de trabajo de la sesión: copia de respaldo, cambio, `git diff`
guardado, `dotnet build`, los carriles que la mutación puede alcanzar con `--logger trx`, y los
casos rojos leídos **del `.trx`** y no de la consola. Revertidas **restaurando la copia y tocando su
fecha** —la lección del 1.9: con la fecha vieja MSBuild no recompila y la mutación anterior se
queda dentro del `.dll`—, **recompilando** (`exit 0` las nueve veces), y después de cada una
`git status --porcelain` **vacío** y `grep -rn MUTACION src tests frontend/src db` **sin
resultados**, las dos cosas comprobadas por el guion, que se para si alguna falla. Carriles de
referencia sin mutar en `710532d`: rápido **748**, integración **366** —el 367 de la batería es
el caso de la divisa, que llegó después en `6781389`—.

| # | Mutación | Dónde | Carril rápido | Integración | Qué se pone rojo |
|---|---|---|---|---|---|
| 1 | Quitada de `CrucesDeclarados` la línea `Catalogo.Application -> Bastion.Terceros.Contracts` | `Inventario.cs` | **2** rojos | — | `LosIdentificadoresAjenosTests.Cada_modulo_de_la_lista_tiene_su_cruce_y_su_puerto` («`ArticuloProveedor.TerceroId`: Catalogo guarda un identificador de Terceros y no tiene ningún cruce declarado») **y** `LasFronterasEntreModulosTests.El_unico_cruce_entre_modulos_va_por_contratos` |
| 2 | Añadida una línea `Terceros.Application -> Bastion.Identidad.Contracts` **sin código detrás** | `Inventario.cs` | **1** | — | `El_unico_cruce_entre_modulos_va_por_contratos` |
| 3 | El alta acepta `NoExiste`: `!= Disponible` → `== NoHaceEseRol` | `ProveedoresDelArticulo.cs` | **2** | **2** | `ProveedoresDelArticuloTests.Los_estados_que_no_autorizan_contestan_lo_MISMO` y `El_estado_se_pregunta_antes_que_el_duplicado_y_lo_escondido_no_es_un_409`; contra la base, `Inventado_ajeno_bloqueado_y_solo_cliente_…` (el inventado da **201**) y `Volver_a_anadir_a_un_bloqueado_…` (da **409**) |
| 4 | El puerto de terceros esquiva el filtro de **empresa** (R8): `.IgnoreQueryFilters(["Inquilinato"])` | `ConsultaDeTerceros.cs` | **1** | **2** | `ElFiltroNoSeSaltaPorAhiTests.Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo`; contra la base, `Inventado_ajeno_…` («el de otra empresa no contesta 400», **201**) y `ElPuertoDeTercerosContraLaBaseTests.Uno_inventado_y_uno_de_otra_empresa_NoExisten_…` (`Disponible` en vez de `NoExiste`) |
| 5 | El puerto de terceros esquiva el filtro del **art. 32** en sus dos consultas: `.IgnoreQueryFilters(["Bloqueo"])` | `ConsultaDeTerceros.cs` | **1** | **5** | enteras abajo |
| 6 | El puerto de tarifas deja de cerrar la vigencia por arriba: una tarifa acabada **rige** | `ConsultaDeTarifas.cs` | 0 | **1** | `Una_tarifa_que_ya_no_rige_y_una_que_aun_no_son_409_…` — «la que ya no rige se ha asignado», **200**; la futura sigue siendo 409 y por eso el caso prueba **los dos extremos** |
| 7 | Sin registrar el adaptador de `IConsultaDeTerceros` | `ModuloDeTerceros.cs` | **101** | **206** | el host **no arranca**: enteras abajo, con la variante **7b** |
| 8 | Una clave ajena `catalogo.articulos_proveedor(tercero_id) → terceros.terceros(id)`, puesta por `migrationBuilder.Sql` en la migración | `20260913071145_ProveedoresDelArticulo.cs` | 0 | **1** | `EsquemaDeIdentidadTests.La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena` — «`[(catalogo, terceros)]` … no puede haber claves ajenas entre esquemas» |

**La 2, la 5 y la 7, enteras.**

**Mutación 2 — un cruce declarado que ningún código hace.** Es la mitad del censo que una
comparación de inclusión se dejaría: la 1 prueba que un cruce sin declarar sale rojo; esta, que una
declaración sin cruce detrás también.

```diff
+            ["Terceros.Application -> Bastion.Identidad.Contracts"] =
+                "MUTACION 2: un cruce declarado que ningún código hace.",
```

```
carril rápido: exit 1, 748 casos, 747 correctos, 1 con error

[Failed] LasFronterasEntreModulosTests.El_unico_cruce_entre_modulos_va_por_contratos
  Shouldly.ShouldAssertException : ordenados
      should be
  ["Catalogo.Application -> Bastion.Organizacion.Contracts", "Catalogo.Application -> Bastion.Terceros.Contracts",
   "Identidad.Application -> Bastion.Organizacion.Contracts", "Terceros.Application -> Bastion.Catalogo.Contracts",
   "Terceros.Application -> Bastion.Identidad.Contracts", "Terceros.Application -> Bastion.Organizacion.Contracts"]
      but was (case sensitive comparison)
  ["Catalogo.Application -> Bastion.Organizacion.Contracts", "Catalogo.Application -> Bastion.Terceros.Contracts",
   "Identidad.Application -> Bastion.Organizacion.Contracts", "Terceros.Application -> Bastion.Catalogo.Contracts",
   "Terceros.Application -> Bastion.Organizacion.Contracts"]

  Additional Info:
      los cruces entre módulos no son los declarados:
  1. Catalogo.Application -> Bastion.Organizacion.Contracts
  2. Catalogo.Application -> Bastion.Terceros.Contracts
  3. Identidad.Application -> Bastion.Organizacion.Contracts
  4. Terceros.Application -> Bastion.Catalogo.Contracts
  (…)
--- restaurada: compila exit 0; git status --porcelain: vacío; grep MUTACION: limpio
```

Un caso rojo y solo uno, y **no** es `Cada_modulo_de_la_lista_tiene_su_cruce_y_su_puerto`: esa regla
va del identificador al cruce, y una declaración huérfana no tiene identificador. La comparación
entera es la única que la ve, y por eso no puede degradarse a un `ShouldContain`.

**Mutación 5 — el puerto de terceros esquiva el filtro del art. 32 en sus dos consultas.** Es la
que convierte el cruce en una fuga de datos reservados: el alta dejaría colgar un suministro de un
bloqueado y el listado lo enseñaría. Cae **dos veces por caminos independientes**: primero el carril
rápido, que no deja escribir la llamada; y aunque alguien sortease esa regla, cinco casos contra
PostgreSQL.

```diff
         bool? haceElRol = await contexto.Terceros
+            .IgnoreQueryFilters(["Bloqueo"]) // MUTACION 5
             .Where(tercero => tercero.Id == terceroId)
 …
         List<Guid> tratables = await contexto.Terceros
+            .IgnoreQueryFilters(["Bloqueo"]) // MUTACION 5
             .Where(tercero => preguntados.Contains(tercero.Id))
```

```
carril rápido: exit 1, 748 casos, 747 correctos, 1 con error
[Failed] ElFiltroNoSeSaltaPorAhiTests.Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo
  hallazgos should be empty but had 1 item and was
  ["src/Modules/Terceros/Bastion.Terceros.Infrastructure/Persistencia/Repositorios/ConsultaDeTerceros.cs usa .IgnoreQueryFilters("]

carril de integración: exit 1, 366 casos, 361 correctos, 5 con error
[Failed] ContratoDeLosCrucesTests.Inventado_ajeno_bloqueado_y_solo_cliente_contestan_el_MISMO_400_y_no_dejan_fila
  alta.StatusCode should be HttpStatusCode.BadRequest but was HttpStatusCode.Created
  Additional Info: el bloqueado no contesta 400. {"id":"01a09ead-…","terceroId":"01a09ead-…","referenciaDelProveedor":null}

[Failed] ContratoDeLosCrucesTests.Volver_a_anadir_a_un_bloqueado_que_ya_suministraba_es_el_MISMO_400_y_no_un_409
  delBloqueado.StatusCode should be HttpStatusCode.BadRequest but was HttpStatusCode.Conflict
  Additional Info: volver a añadir a un bloqueado que ya suministraba el artículo no contesta como uno
  inventado. Si es un 409, el alta dice «ya está» de un suministro que el listado esconde, y eso es
  decir que está bloqueado. {"type":"/errors/articulo-proveedor-duplicado",…,"status":409,…}

[Failed] ElPuertoDeTercerosContraLaBaseTests.De_un_conjunto_se_tratan_los_de_aqui_no_bloqueados_hagan_el_papel_que_hagan
  tratables.OrderBy(id => id) should be [01a09ead-7ef8-…, 01a09ead-7efe-…]
  but was [01a09ead-7ef8-…, 01a09ead-7efe-…, 01a09ead-7f0c-…]
  Additional Info: el conjunto no deja pasar justo a los dos que se pueden tratar. Si sobra el
  bloqueado, el listado de proveedores de un artículo enseña a quien tiene sus datos reservados; (…)

[Failed] ContratoDeLosCrucesTests.El_listado_esconde_al_proveedor_bloqueado_y_desbloquearlo_devuelve_la_MISMA_fila
  await IdsDelListadoAsync(cliente, articulo.Id) should be [01a09ead-49d1-…]
  but was [01a09ead-49bb-…, 01a09ead-49d1-…]
  Additional Info: el listado de proveedores de un artículo enseña a un tercero bloqueado. Es la
  manera de leer, desde Catálogo, quién tiene sus datos reservados (art. 32 LOPDGDD)

[Failed] ElPuertoDeTercerosContraLaBaseTests.Un_bloqueado_NoExiste_desde_fuera_y_desbloquearlo_lo_devuelve_Disponible
  (await new ConsultaDeTerceros(contexto).EstadoDeAsync(tercero.Id, RolDeTercero.Proveedor, …))
  should be EstadoDelTercero.NoExiste but was EstadoDelTercero.Disponible
  Additional Info: desde fuera de Terceros se ve que la ficha bloqueada existe. (…)
--- restaurada: compila exit 0; git status --porcelain: vacío; grep MUTACION: limpio
```

Los cinco rojos son **las cuatro respuestas del art. 32** de la decisión 5 más el conjunto del
puerto, cada uno con su mensaje: el alta del bloqueado, el re-alta que no puede ser 409, el listado
que no puede enseñarlo y el puerto que no puede contestar `Disponible`. Ninguno es un efecto
colateral de otro: cada uno ejerce un camino distinto.

**Mutación 7 — sin registrar el adaptador de `IConsultaDeTerceros`, y la 7b.** El criterio pide que
esto se ponga rojo **por el efecto, en una petición de verdad**, y la primera lectura honesta es que
la 7 **no** lo hace así:

```diff
-        servicios.AddScoped<IConsultaDeTerceros, ConsultaDeTerceros>();
+        // MUTACION 7: servicios.AddScoped<IConsultaDeTerceros, ConsultaDeTerceros>();
```

```
carril rápido:      exit 1, 748 casos, 647 correctos, 101 con error
carril integración: exit 1, 366 casos, 160 correctos, 206 con error (18 s)

[Failed] ContratoDeLosCrucesTests.Un_proveedor_de_aqui_se_anade_con_201_y_el_listado_lo_trae
  System.AggregateException : Some services are not able to be constructed
  (Error while validating the service descriptor 'ServiceType: Bastion.Catalogo.Application.Catalogo.IListarProveedoresDelArticulo
   Lifetime: Scoped ImplementationType: Bastion.Catalogo.Application.Catalogo.ListarProveedoresDelArticulo':
   Unable to resolve service for type 'Bastion.Terceros.Contracts.Terceros.IConsultaDeTerceros' while attempting
   to activate 'Bastion.Catalogo.Application.Catalogo.ListarProveedoresDelArticulo'.)
  (Error while validating the service descriptor '… IAgregarProveedorAlArticulo …': Unable to resolve service
   for type 'Bastion.Terceros.Contracts.Terceros.IConsultaDeTerceros' …)
  … 303 de los 307 rojos con esta misma excepción; los otros 4 son su consecuencia:
  [Failed] LaTrazaEsDeSoloAnadidoTests.Un_DELETE_sobre_una_fila_de_traza_lo_rechaza_el_motor
    id should not be null but was  ·  la tabla de traza está vacía: no hay contra qué probar el solo añadido
  (y Un_UPDATE_… del mismo fichero, LaTrazaNoGuardaSecretosTests.Ningun_valor_… «sin trazas no hay nada
   que comprobar» y UnCambioEnUnMaestroDejaSuRastroTests.Lo_que_se_escribe_sin_empresa_… «la semilla de
   arranque escribe sin empresa: tiene que haber trazas así»)
--- restaurada: compila exit 0; git status --porcelain: vacío; grep MUTACION: limpio
```

**Sí falló la validación del contenedor al arrancar, y eso es lo que se ve.** `WebApplicationFactory`
arranca el host en **Development**, y en ese entorno ASP.NET Core construye el contenedor con
`ValidateOnBuild`: el host no llega a arrancar y **ninguna petición sale**. Son 307 rojos —101 en el
carril rápido y 206 en el de integración, este en 18 s—: **303** por la misma excepción y **4** por
su consecuencia, casos de la traza que necesitan lo que la semilla de arranque escribe y se
encuentran la tabla vacía, porque sin host no hay arranque. Es un rojo fuerte, pero es exactamente la «inspección del contenedor» que el criterio
descarta como prueba, hecha por el marco en vez de por un test. Y en **Production** `ValidateOnBuild`
viene apagado por omisión, así que ese rojo no existe allí.

**La 7b quita esa red para ver qué queda**: la misma mutación y, en `ApiDeVerdad`, el contenedor
construido como en Production.

```diff
         builder.UseSetting("ConnectionStrings:Bastion", postgres.CadenaDeConexion);
+        builder.UseDefaultServiceProvider(opciones => opciones.ValidateOnBuild = false); // MUTACION 7b
```

```
carril integración: exit 1, 366 casos, 347 correctos, 19 con error (211 s)

[Failed] ContratoDeLosCrucesTests.Un_proveedor_de_aqui_se_anade_con_201_y_el_listado_lo_trae
  alta.StatusCode should be HttpStatusCode.Created but was HttpStatusCode.InternalServerError
  Additional Info:
    {"type":"/errors/error-interno","title":"Error interno del servidor","status":500,
     "instance":"/api/v1/catalogo/articulos",…}
    · registro del servidor: [ManejadorDeExcepcionesNoControladas] Excepción no controlada al atender
      "POST" "/api/v1/catalogo/articulos". — InvalidOperationException: Unable to resolve service for
      type 'Bastion.Terceros.Contracts.Terceros.IConsultaDeTerceros' while attempting to activate
      'Bastion.Catalogo.Application.Catalogo.ListarProveedoresDelArticulo'.

[Failed] LaPuertaDeCadaAccionTests.Ninguna_accion_contesta_con_un_fallo_del_servidor_al_sondeo
  reventadas should be empty but had 9 items and was
  ["ArticulosController.Listar → 500", "ArticulosController.Obtener → 500", "ArticulosController.Crear → 500",
   "ArticulosController.Modificar → 500", "ArticulosController.ListarProveedores → 500",
   "ArticulosController.ObtenerProveedor → 500", "ArticulosController.AgregarProveedor → 500",
   "ArticulosController.ModificarProveedor → 500", "ArticulosController.QuitarProveedor → 500"]

y además: ContratoDeLosCrucesTests ×4 (los cuatro del proveedor; los tres de la tarifa, VERDES),
ContratoDeCatalogoTests ×10 y ContratoDeTarifasTests ×4 — todos por un 500 al crear el artículo
--- restaurada: compila exit 0; git status --porcelain: vacío; grep MUTACION: limpio
```

**Esto sí es el efecto, en peticiones de verdad**, y dice dos cosas que la 7 escondía:

- **Los casos del cruce se bastan sin la validación del marco.** Caen los cuatro del proveedor y
  **no** caen los tres de la tarifa, cuyo adaptador sigue registrado: el rojo es de este cruce y no
  de «algo no arranca». `ElPuertoDeTercerosContraLaBaseTests` sigue verde, y debe: construye el
  adaptador con `new` y prueba el SQL, no la composición.
- **El radio del fallo es el controlador entero, no el cruce.** `ArticulosController` recibe por
  constructor los nueve casos de uso, y activar el controlador exige resolverlos todos: sin el
  adaptador de Terceros, en Production **no se puede ni crear un artículo**. El comentario del
  registro decía «la primera petición que intente añadir un proveedor revienta», y era falso por
  los dos lados; está corregido en `92afe10`. Del otro lado, **medido con un canario (7c)** y no
  supuesto: sin `IConsultaDeTarifas` y con el contenedor sin validar, caen las **trece** acciones de
  `LoQueCuelgaDelTerceroController` —contactos, cuentas, condiciones y límite incluidos—.

**Y lo que destapó la 8, que salió roja por un caso que no se llama como lo que guarda.** La clave
ajena entre esquemas puesta con `migrationBuilder.Sql` cayó en **un solo caso**:
`EsquemaDeIdentidadTests.La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena`. Y
cayó ahí **por cómo está redactada su consulta**: es la única de las tres a `information_schema` que
no filtra por esquema de origen. Las otras dos —`EsquemaDelModuloTests.No_hay_ninguna_clave_foranea_que_salga_del_esquema_del_modulo`,
con origen `organizacion`, y `EsquemaDeTercerosTests.La_empresa_se_guarda_como_identificador_y_NO_como_clave_ajena`,
con origen `terceros`— no miran una clave que sale de `catalogo`; **Catálogo no tiene fichero de
esquema**; y `NingunaClaveForaneaCruzaDeEsquemaTests`, la del carril rápido, recorre el **modelo** de
EF Core, que no ve una sentencia SQL escrita a mano en una migración: el carril rápido salió **748 de
748 en verde** con la mutación puesta. Ella misma lo avisa en su comentario —no sustituye a las de
integración, porque solo esas miran lo que hay de verdad en la base—.

O sea: la regla 4 del §5, contra la base de verdad, **la sostiene hoy un caso que habla de
membresías**, y quien le añadiera `origen.table_schema = 'identidad'` para que hiciera lo que su
nombre dice la dejaría sin guardia, con todo en verde. **Hallazgo anotado y no arreglado en este
ítem**: no es de los cruces sino del arnés, y lo que pide —una comprobación contra PostgreSQL sin
filtro de esquema, con un nombre que diga lo que guarda y su línea en el censo del carril— es un
cambio que necesita su propia mutación.

### Verificado en local, con la salida real — ítem 1.9

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `8851c5f` (main al abrir
la rama); el «después», `6e147c2` —el último commit de código de la rama; lo que viene detrás solo
toca `docs/PLAN.md`—. Batería completa de `AGENTS.md` **con Docker arrancado**, y cada cifra con la
orden que la mide.

```
dotnet build Bastion.sln                                   → 0 errores, 0 advertencias
dotnet format Bastion.sln --verify-no-changes              → sin cambios
bash scripts/generar-openapi.sh --comprobar                → al día: 120 operaciones (en 70 rutas)
bash scripts/generar-errores.sh --comprobar                → al día: 75 tipos, de 81 sitios de llamada
bash scripts/comprobar-migraciones.sh                      → modelo y migraciones coinciden en los 5
                                                             módulos con persistencia (Catálogo: 2)
```

Los dos carriles, con el recuento que es **quien decide el desenlace** —`dotnet test` dice «ningún
caso falla»; el recuento dice «han corrido exactamente los ensamblados que tenían que correr»—:

```
bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio "Dominio y arquitectura" 300 …

Dominio y arquitectura: 723 casos (723 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — BuildingBlocks.UnitTests 132, Organizacion.UnitTests 183, Identidad.UnitTests 58,
    Terceros.UnitTests 77, Catalogo.UnitTests 66, Organizacion.IntegrationTests 22,
    Api.FunctionalTests 145, Arquitectura.Tests 34, Api.IntegrationTests 6
    (682 en el 1.8 sobre `8851c5f`, en 9 ensamblados: +41 casos, todos en Catalogo.UnitTests,
     que pasa de 25 a 66)

bash scripts/ci/recuento-de-tests.sh artifacts/test-results/integracion "Integración (Testcontainers)" 100 …

Integración (Testcontainers): 355 casos (355 correctos, 0 con error, 0 omitidos) en 9 ensamblados
  — Organizacion.IntegrationTests 74, Api.IntegrationTests 281, y 0 en los otros siete
    (348 en el 1.8 sobre `8851c5f`: +7 casos, los siete de `ContratoDeTarifasTests`)

Frontal: 14 ficheros de prueba, 95 casos, 0 avisos de `act()`
  (13 ficheros y 81 casos en el 1.8: +1 fichero, +14 casos)
```

**El canal de `act()` sigue a cero, y se mide leyendo la salida de la suite**, no contando llamadas
en el código: `npm --prefix frontend run test` no emite ni un «not wrapped in act». Desde `9f8ff56`
un solo aviso es un hallazgo, no ruido.

```
npm --prefix frontend run api          → `shared/api/esquema.ts` sin cambios (git status limpio)
npm --prefix frontend run typecheck    → limpio
npm --prefix frontend run lint         → limpio
npm --prefix frontend run format:check → All matched files use Prettier code style!
bash scripts/ci/presupuesto-del-frontal.sh frontend/dist 450 900

Frontal · arranque 416/450 KiB en 3 ficheros · total servido 575/900 KiB
  (410/450 y 564/900 en el 1.8: +6 KiB de arranque y +11 de total)
```

**Y por qué Catálogo sumó siete KiB al arranque en el 1.8 — medido, no supuesto.** La pregunta
quedaba abierta del ítem anterior (403 → 410 KiB) y se contesta atribuyendo el crecimiento del
fragmento de arranque **por módulo**, con los mapas de origen del propio `dist`. Del crecimiento de
JavaScript, **5 349 B —el 88 %— son los dos diccionarios de idioma**; `app/rutas.tsx` puso 647 B y
`shared/sesion/permisos.ts`, 74 B. El 17 % restante del total no era JavaScript: era la hoja de
estilos.

Este ítem **confirma la atribución en lugar de repetirla**, porque su pantalla vuelve a hacer lo
mismo y sale el mismo reparto:

```
diferencia por módulo en el fragmento de arranque (B), de `3d1faff` a `d0ab790`:
   +2643  src/app/i18n/es.ts
   +2628  src/app/i18n/en.ts
    +304  src/app/rutas.tsx
     +32  src/shared/sesion/permisos.ts
suma atribuida: 5607
```

La pantalla en sí **no está ahí**: `PaginaDeTarifas-CVnNXvw6.js` son 5,48 kB que el navegador no pide
hasta que alguien abre `/tarifas`. Así que lo que una pantalla nueva cuesta en el arranque **no es la
pantalla, son sus textos** —los de las dos lenguas, y los doce `type` de error que el barrido del
ADR-0030 obliga a escribir—. La palanca, el día que el margen se estreche: **sacar del arranque el
idioma que no está activo**, que hoy son ~2,6 KiB y mañana todo el crecimiento de una de las dos
lenguas. No se hace ahora porque quedan 34 KiB de margen y cargar el diccionario aparte mete una
espera antes de pintar el primer texto.

**Dos hallazgos del arnés, ninguno del dominio, y los dos se vieron por el efecto.**

**El choque de NIF entre dos ficheros del mismo carril.** Las semillas de `Escenario.NifInventado`
escriben el NIF de la empresa que cada caso da de alta, y el NIF es único en toda la instalación. El
censo que hice para elegir un bloque libre buscaba `NifInventado(N)` y **no vio** las llamadas de
`ContratoDeCatalogoTests`, que pasa el número por un ayudante privado —`EnUnaEmpresaNuevaAsync(170)`—
y solo dentro compone el NIF. Elegí 180-183, que eran suyos. Y **el rojo no salió por el fichero
nuevo**: salió por `ContratoDeCatalogoTests.El_tipo_del_articulo_viaja_como_TEXTO_y_no_como_numero`
con un `empresa-ya-registrada`, porque quien recibe el 409 es el que corre **después**. Se lee como
un fallo ajeno. El bloque de este fichero es ahora el **190-193**, y el reparto está escrito en su
comentario de clase, que es donde lo va a leer el siguiente.

**Y cinco rojos del frontal que eran contención, no regresión.** La primera pasada de
`npm --prefix frontend run test` salió con 5 ficheros y 5 casos en rojo mientras la suite entera de
.NET corría al fondo; la propia salida lo decía —`setup 91.70s`, `environment 189.11s`— y sola, la
misma suite da **14 ficheros y 95 casos verdes**. Queda anotado porque la conclusión fácil era la
contraria: cinco rojos nuevos sobre un árbol que acababa de tocar el frontal parecen una regresión, y
no lo eran.

### Las ocho mutaciones del 1.9, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio**, línea base `d0ab790`, aplicadas con copia de respaldo y revertidas
**restaurando esa copia** —nunca con `git checkout --`, que se llevaría por delante todo lo no
commiteado del fichero—. Después de la tanda: `git status --porcelain` sin más que el trabajo del
propio ítem y `grep -rn MUTACION src tests frontend/src db` **sin resultados**, los dos comprobados.

| # | Mutación | Dónde | Qué se pone rojo |
|---|---|---|---|
| 1a | La precedencia ordenando por **profundidad de la categoría** en vez de por salto desde el artículo, de modo que lo que no es antepasado se cuela como «lo más hondo» | `ElAntepasadoMasCercanoGana.cs` | `ElAntepasadoMasCercanoGanaTests.La_categoria_mas_profunda_que_no_es_antepasada_no_compite` |
| 1b | El ascenso al revés (`.Order()` → `.OrderDescending()`): gana el antepasado **más lejano** | `ElAntepasadoMasCercanoGana.cs` | `ElAntepasadoMasCercanoGanaTests.Entre_dos_categorias_antepasadas_gana_la_mas_cercana` |
| 2 | La línea del **artículo** deja de ganar y pasa a ser el último recurso | `ElAntepasadoMasCercanoGana.cs` | `La_linea_del_articulo_le_gana_a_la_de_la_categoria` **y** `Un_tramo_de_categoria_alto_no_le_gana_a_la_linea_del_articulo` |
| 3 | Sin línea aplicable, un DTO con precio **cero** en vez del error con nombre | `ResolverPrecio.cs` | `ResolverPrecioTests.Sin_linea_aplicable_hay_error_con_nombre_y_NUNCA_un_cero` |
| 4 | Precio **y** descuento, los dos puestos, aceptados — **las dos hojas de la puerta a la vez** | `PrecioODescuento.cs` **y** `CrearLineaTarifa.cs` | `PrecioODescuentoTests.Con_los_dos_puestos_no_se_construye` **y** `CrearLineaTarifaTests.Con_precio_Y_descuento_se_rechaza` |
| 5 | **Ninguno** de los dos, aceptado — otra vez las dos hojas | `PrecioODescuento.cs` **y** `CrearLineaTarifa.cs` | `PrecioODescuentoTests.Sin_ninguno_de_los_dos_tampoco` **y** `CrearLineaTarifaTests.Sin_precio_NI_descuento_tambien_y_ESTE_es_el_que_se_olvida` |
| 6 | La **restricción de exclusión** fuera de la migración (la extensión se queda) | `20260911064712_TarifasYSusLineas.cs` | `ContratoDeTarifasTests.Dos_tramos_…_los_rechaza_la_BASE`, `El_dia_en_que_un_tramo_ACABA_todavia_cuenta_para_el_solape` **y** `La_extension_btree_gist_la_puso_la_MIGRACION_en_la_imagen_del_compose` |
| 7 | La frontera del tramo corrida un paso (`<=` → `<`) | `ElAntepasadoMasCercanoGana.cs` | `El_tramo_se_elige_con_la_frontera_hacia_arriba(cantidad: 100, esperado: 8)` — **solo la fila del borde** |
| 8 | La precedencia resuelta **subiendo de uno en uno** con `EslabonAsync`, reusando el ascenso de `ElArbolSigueSiendoUnArbol` | `ResolverPrecio.cs` | `Resolver_cuesta_los_mismos_viajes_sea_cual_sea_la_profundidad` en **las dos** profundidades (1 y 11) |

**La 1, la 5 y la 8, enteras.**

**Mutación 1 — la precedencia ordenando por profundidad.** Es la trampa del ítem: pasa el caso fácil
—dos líneas en la misma rama— y devuelve la equivocada en cuanto hay una rama hermana más honda.

```
Con error Bastion.Catalogo.UnitTests.Catalogo.ElAntepasadoMasCercanoGanaTests
          .La_categoria_mas_profunda_que_no_es_antepasada_no_compite
  should be    LineaCandidata { … CategoriaId = a56826c5…, Nivel = 2, CantidadDesde = 0, Precio = 12 }
  but was      LineaCandidata { … CategoriaId = 2e81fecc…, Nivel = , CantidadDesde = 0, Precio = 1 }

  Additional Info:
    ha ganado una línea de una categoría que no es antepasada del artículo. Es la mutación 1:
    ordenar por profundidad pasa el caso de dos líneas en la misma rama y devuelve el precio de
    otra rama en cuanto hay una hermana más honda
```

Un caso rojo y **solo uno**: los otros sesenta y cinco siguen verdes, que es lo que dice que la
afirmación que la caza no es un efecto colateral de otra.

**Mutación 5 — ni precio ni descuento, aceptado.** Es el negativo que se olvida, y **hace falta
mutar las dos hojas a la vez** para que el árbol siga compilando y la mutación signifique algo: el
caso de uso rechaza con un error con nombre y el dominio lanza, así que quitar solo una deja la otra
contestando y la mutación no llega a la fila. Las dos hojas caen, cada una por su lado:

```
Con error Bastion.Catalogo.UnitTests.Catalogo.PrecioODescuentoTests.Sin_ninguno_de_los_dos_tampoco
  Shouldly.ShouldAssertException : `) => PrecioODescuento.De(null, null` should throw
    System.ArgumentException
  but did not

Con error Bastion.Catalogo.UnitTests.Catalogo.CrearLineaTarifaTests
          .Sin_precio_NI_descuento_tambien_y_ESTE_es_el_que_se_olvida
  Shouldly.ShouldAssertException : resultado.EsCorrecto should be False but was True

  Additional Info:
    ha entrado una línea sin precio y sin descuento. No da error al guardarla ni al leerla: da un
    cero al facturar, y el descuadre aparece semanas después sin autor
```

**Mutación 8 — la precedencia resuelta con una consulta por nivel.** Es la única de las ocho que
**no cambia ningún resultado**: el precio que devuelve es el correcto en los cinco casos del fichero,
y ninguna de sus consultas es lenta por separado. Lo único que cambia es el **número de viajes**, y
que ahora crece con la profundidad. Cae en las dos profundidades del `[Theory]`:

```
Con error Bastion.Catalogo.UnitTests.Catalogo.ResolverPrecioTests
          .Resolver_cuesta_los_mismos_viajes_sea_cual_sea_la_profundidad(niveles: 11)
  Shouldly.ShouldAssertException : categorias.LlamadasDeAscendencia should be 1 but was 0

  Additional Info:
    la ascendencia se pide UNA vez por resolución, con toda la cadena dentro
```

Que caiga también en `niveles: 1` —donde el número de viajes ni siquiera empeora— es la prueba de
que la afirmación **no es un umbral** sino una cota sobre qué puerto se usa: con un solo nivel, subir
de uno en uno cuesta lo mismo y sigue siendo el patrón que a once niveles cuesta once veces más.

**Lo que la tanda destapó del arnés, y no del dominio.** Revertir con `shutil.copy2` conserva la
fecha del fichero, así que el `.cs` restaurado quedaba **más viejo** que el ensamblado ya construido
y MSBuild se saltaba la recompilación de ese proyecto: la mutación anterior seguía dentro del `.dll`.
Se vio porque la mutación 7 salió roja **en dos casos** y uno de ellos era de la 5, que ya estaba
revertida en disco —`git status --porcelain` decía que el árbol estaba limpio y el rojo era del
binario—. Solo pasa cuando lo revertido y lo mutado están en **ensamblados distintos**: si comparten
uno, la recompilación del proyecto entero arrastra el fichero restaurado. Se arregla tocando la fecha
al restaurar (`os.utime(ruta, None)`), y queda anotado porque el modo de fallo simétrico es peor: una
mutación que se copiara con fecha vieja **no se compilaría**, la suite saldría verde y la conclusión
sería «ningún test la caza».

### Verificado en local, con la salida real — ítem 1.7

**Toda cifra de «antes y después» nombra sus dos commits.** El «antes» es `3a937a3` (main al abrir
la rama); el «después», `b9ca5d6` (el árbol que se lleva a main al cerrar).

**Y esta vez el carril de integración SÍ se pudo ejercer en local.** Docker Desktop estaba
instalado y **parado**, no ausente: la firma con la que fallaba era
`DotNet.Testcontainers.Builders.DockerUnavailableException : Docker is either not running or
misconfigured … Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine' … ----
System.TimeoutException : The operation has timed out`, con **334 casos rojos de 334**. Arrancado el
motor, los 334 salen verdes. Es la primera vez desde el 1.4 que las dos mitades de este informe se
miden en la misma máquina, y es lo que hace que las **ocho** mutaciones sean atribuibles aquí en vez
de solo en el *runner*.

```
batería:      dotnet build  ->  0 Advertencia(s), 0 Errores
              dotnet format --verify-no-changes  ->  exit 0
              generar-errores.sh --comprobar  ->  al día, 51 tipos de 56 sitios  (49 de 54 en el 1.6)
              generar-openapi.sh --comprobar  ->  al día, 102 operaciones  (93 en el 1.6)
              comprobar-migraciones.sh  ->  Organizacion 5, Identidad 3, Terceros 2, y el modelo
                                            coincide con ellas  (Organizacion eran 4)

dominio y arquitectura:
  bash scripts/ci/recuento-de-tests.sh artifacts/test-results/dominio "Dominio y arquitectura" 300 \
    "Bastion.Api.FunctionalTests.dll,Bastion.Api.IntegrationTests.dll,Bastion.Arquitectura.Tests.dll,\
Bastion.BuildingBlocks.UnitTests.dll,Bastion.Identidad.UnitTests.dll,\
Bastion.Organizacion.IntegrationTests.dll,Bastion.Organizacion.UnitTests.dll,Bastion.Terceros.UnitTests.dll"

  651 casos (651 correctos, 0 con error, 0 omitidos) en 8 ensamblados
    — BuildingBlocks.UnitTests 132, Identidad.UnitTests 58, Organizacion.UnitTests 183,
      Terceros.UnitTests 77, Organizacion.IntegrationTests 21, Arquitectura.Tests 34,
      Api.FunctionalTests 140, Api.IntegrationTests 6
    (630 en el 1.6 sobre `3a937a3`, los mismos 8 ensamblados: +21 casos)

integración (Testcontainers):
  bash scripts/ci/recuento-de-tests.sh artifacts/test-results/integracion "Integración (Testcontainers)" 100 \
    "Bastion.Api.IntegrationTests.dll,Bastion.Organizacion.IntegrationTests.dll"

  334 casos (334 correctos, 0 con error, 0 omitidos) en 8 ensamblados
    — Organizacion.IntegrationTests 74, Api.IntegrationTests 260, y 0 en los otros seis
    (318 en el 1.6 sobre `3a937a3`: +16 casos)

  El +16, atribuido entero: `LaInversaYElResolutorTests` 10 (seis `[Fact]` y dos `[Theory]` de dos
  filas), `LaRetiradaNoEsUnBloqueoTests` 4, y `LosPuertosDeLecturaTests` +2 (los dos casos de
  `SoloResuelveLoViejo`, de `93b8e5f`).

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (los mismos que en el 1.6)
         presupuesto  ->  arranque 403/450 KiB en 3 ficheros · total servido 549/900 KiB  (548 en el 1.6)

  cd frontend && npm test -- --run 2>&1 | grep -c "not wrapped in act"
    Antes  — `3a937a3`: 109 avisos en 7 ficheros
    Después — `b9ca5d6`: 109 avisos, la MISMA cifra.
  No se mueve, y el motivo está en el `git diff --stat 3a937a3..b9ca5d6 -- frontend/`: solo
  `esquema.ts` (generado), `es.ts` y `en.ts`. Ni un componente, ni una pantalla.
```

**Licencias — ningún paquete nuevo, con el comando pegado y su salida.**

```
git diff 3a937a3..b9ca5d6 -- '*.csproj' | awk '/^diff --git/{f=$4} /^\+.*PackageReference/{print f" -> "$0}'
    (sin salida)
```

Cero líneas: el ítem no añade ni una `PackageReference`. El único cambio en un `.csproj` es un
`InternalsVisibleTo` hacia un ensamblado del propio repositorio. Se mide con `awk` sobre el
`diff --git` y **no** con `grep -B`, que atribuye por proximidad y le cuelga el paquete al fichero
equivocado en cuanto hay dos `.csproj` seguidos.

### Las ocho mutaciones del 1.7, cada una aplicada, ejecutada y revertida

Todas sobre **árbol limpio**, línea base `b9ca5d6`, aplicadas con copia de respaldo y revertidas
**restaurando esa copia** —nunca con `git checkout --`, que se llevaría por delante todo lo no
commiteado del fichero—. Después de la tanda: `git status --porcelain` **vacío** y
`grep -rn MUTACION` **sin resultados**, los dos comprobados.

| # | Mutación | Dónde | Qué se pone rojo |
|---|---|---|---|
| 1 | Quitar las dos marcas `[CubreEstadoDeMaestro(…, SoloResuelveLoViejo)]` de divisa y unidad — el estado *antes* del arreglo | `LosPuertosDeLecturaTests.cs` | `LaMatrizDePuertoYEstadoTests.Cada_casilla_de_puerto_por_estado_esta_cubierta`, nombrando **las dos casillas** |
| 2 | Publicar `DELETE /divisas/{id}` | `DivisasController.cs` | `NingunMaestroRetirableSeBorraTests.Ningun_recurso_retirable_publica_el_borrado_de_una_fila` |
| 3 | El `GET` por identificador de una fila retirada devolviendo **404** | `ConsultasDeDivisas.cs` | `LaRetiradaNoEsUnBloqueoTests.El_GET_por_identificador_sigue_devolviendo_la_fila_retirada` (y de rebote `Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva`) |
| 4 | La colección incluyendo lo retirado por omisión | `Paginador.cs` | `LaRetiradaNoEsUnBloqueoTests.La_coleccion_excluye_lo_retirado_por_omision_y_lo_trae_al_pedirlo` |
| 5 | Quitar la comprobación de la inversa de la **modificación**, dejándola solo en el alta | `ModificarConversionUm.cs` | `LaAritmeticaDeLaInversaTests.La_modificacion_vuelve_a_comprobar_la_inversa` **y** `LaInversaYElResolutorTests.La_modificacion_vuelve_a_comprobar_la_inversa` (409 → 200) |
| 6a | `<` donde va `≤` | `LaInversaEsPlausible.cs` | `La_desigualdad_…(1.001, 0.999, casan: True)` **y** `El_par_de_la_frontera_se_separa_EXACTAMENTE_el_margen` |
| 6b | La tolerancia relajada un orden de magnitud (`5·10⁻⁶`) | `LaInversaEsPlausible.cs` | `El_margen_sale_de_la_escala_…`, `El_par_de_la_frontera_…` y **dos** filas del `[Theory]`: `(1.001, 0.998999)` y `(12, 0.083334)`, que pasan a aceptarse |
| 7 | El resolutor componiendo la cadena por el listado en vez de fallar | `ResolverConversionUm.cs` | `LaInversaYElResolutorTests.El_par_que_habria_que_encadenar_no_se_resuelve` (404 → 200) y, en el carril rápido, el doble negándose a listar |
| 8 | El resolutor devolviendo una resolución vacía en vez del error con nombre | `ResolverConversionUm.cs` | `El_par_que_habria_que_encadenar_no_se_resuelve` en **los dos** carriles, más `El_sentido_contrario_no_se_deduce_invirtiendo` |

**La 1 entera, porque es el hallazgo del ítem.** Es la única que no es una mutación inventada: es el
estado **real** del repositorio antes de este trabajo, y por eso se commiteó en rojo en `cf46416`
antes de arreglar nada.

```
Con error Bastion.Organizacion.IntegrationTests.Persistencia.LaMatrizDePuertoYEstadoTests
          .Cada_casilla_de_puerto_por_estado_esta_cubierta
  Shouldly.ShouldAssertException : sinDuenio
    should be empty but had 2 items and was
["IConsultaDeDivisas -> SoloResuelveLoViejo", "IConsultaDeUnidadesDeMedida -> SoloResuelveLoViejo"]

  Additional Info:
    hay casillas de la matriz puerto × estado que ningún caso afirma. Una casilla sin dueño
    significa una de dos cosas, y las dos son malas: o el puerto no puede contestar ese valor —y
    entonces la lista cerrada tiene un valor inalcanzable, que es el defecto que abrió el ítem 1.7—
    o puede y nadie lo comprueba. Se cierra marcando con `CubreEstadoDeMaestro` el caso que lo
    afirma, y si ese caso no existe es que hay que escribirlo
```

**La 3 entera, porque es la única línea que separa retirada de bloqueo y no la vigila nada más.**
Las dos cosas quitan una fila de en medio sin borrarla, las dos son reversibles, y las dos se leen
igual en un diagrama; al que venga después le va a parecer natural «unificar» los dos
comportamientos, y el 404 es lo más fácil de copiar porque **ya está escrito** para el bloqueo.

```
Con error Bastion.Api.IntegrationTests.Retiradas.LaRetiradaNoEsUnBloqueoTests
          .El_GET_por_identificador_sigue_devolviendo_la_fila_retirada
  Shouldly.ShouldAssertException : lectura.StatusCode
    should be HttpStatusCode.OK but was HttpStatusCode.NotFound

  Additional Info:
    una divisa retirada NO desaparece: se sigue devolviendo por su identificador. Eso es lo que la
    distingue de una fila bloqueada, que responde 404 a propósito.
    {"type":"/errors/divisa-no-encontrada","title":"Recurso no encontrado","status":404,
     "detail":"No hay ninguna divisa con el identificador 01a07ea3-9e87-72e7-9178-91ca5a3290fc.",
     "instance":"/api/v1/organizacion/divisas/01a07ea3-9e87-72e7-9178-91ca5a3290fc", …}

Con error … .Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva
  Shouldly.ShouldAssertException : lectura.StatusCode
    should be HttpStatusCode.OK but was HttpStatusCode.NotFound
```

**Y el segundo rojo de la 3 dice algo que no estaba en el enunciado:** con el 404 puesto, la retirada
deja de poder **deshacerse**. Reincorporar necesita el `ETag` de la fila, y el `ETag` sale del `GET`
que acaba de contestar 404. En cuatro maestros de instalación (R8) —donde retirar por error deja sin
esa fila a **todas** las empresas— eso convierte un error reversible en uno permanente. La línea que
separa retirada de bloqueo no es solo una cortesía con las facturas viejas: es lo que sostiene la
puerta de vuelta.

### Verificado en local, con la salida real — ítem 1.6

Medido sobre `911ba5c`, que es el árbol que se lleva a `main`, con la batería de `AGENTS.md`
entera y en su orden.

```
contrato:     npm run api  ->  `esquema.ts` regenerado y SIN cambios
                              (`git status --porcelain` sobre el fichero, vacío)
migraciones:  Auditoria 3, Organizacion 4, Identidad 3, Terceros 2 — y el modelo
              coincide con ellas en los cuatro. La convención `LaClaveLaPoneElDominio`
              NO añade migración: `ValueGenerated` sobre una clave `Guid` no produce
              diferencia relacional
openapi:      documento versionado al día — 93 operaciones (82 en el 1.5)
catálogo de `type`: al día — 49 tipos, de 54 sitios de llamada (47 de 52 en el 1.5)

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (los mismos que en el 1.5)
         presupuesto  ->  arranque 403/450 KiB en 3 ficheros · total servido 548/900 KiB
         act()  ->  109 avisos en 7 ficheros

backend: dotnet build  ->  0 Advertencia(s), 0 Errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido  ->  630 casos (630 correctos, 0 con error, 0 omitidos) en 8
                            ensamblados, medido con el guion de la CI sobre los .trx
                            de esta máquina y con `artifacts/test-results` borrado antes:
                            BuildingBlocks.UnitTests 132, Terceros.UnitTests 77,
                            Identidad.UnitTests 58, Organizacion.UnitTests 182,
                            Organizacion.IntegrationTests 4, Arquitectura.Tests 34,
                            Api.FunctionalTests 137, Api.IntegrationTests 6
                            (576 en el 1.5, los mismos 8 ensamblados: +54 casos)
         carril integración -> NO EJECUTADO AQUÍ. Abajo, con firma.

licencias:   una sola línea de `PackageReference` añadida en todo el `git diff` del ítem
             —`Npgsql.EntityFrameworkCore.PostgreSQL`, en `BuildingBlocks.Infrastructure`,
             que es el bloque común y NO un módulo—, y **no es un paquete nuevo**: ya
             estaba en `Directory.Packages.props` (10.0.3), que el ítem no toca, y su
             licencia —PostgreSQL, permisiva estilo BSD— está ya declarada allí.
             `frontend/package.json` y `package-lock.json`, sin mover. Ninguna licencia
             nueva entra con este ítem. Y el comando importa, porque el primero que usé
             —`grep -B25`— atribuye por CERCANÍA y me hizo nombrar dos módulos que no
             eran; la atribución sale de la cabecera del propio diff:
             `git diff e3a9e9e..3a937a3 -- '*.csproj' | awk '/^diff --git/{f=$4} /^\+.*PackageReference/{print f" -> "$0}'`
```

**Los `act()`, con los dos extremos nombrados y esta vez medidos los dos.** El «después» del ítem se
había medido en `c7b9f80` y se sostenía sobre un `git diff` vacío de `frontend/`. Al cerrar se
vuelve a medir, ya sobre el árbol final:

```
cd frontend && npm ci && npm test -- --run 2>&1 | grep -c "not wrapped in act"
```

- **Antes — `e3a9e9e`** (main al abrir): **109 avisos en 7 ficheros**.
- **Después — `911ba5c`** (el árbol que se lleva a main): **109 avisos en 7 ficheros**, y el mismo
  reparto exacto: `ElListadoDeAlmacenes` 24, `ElCambioDeIdioma` 17, `LasRutasProtegidas` 17,
  `ElSelectorDeEmpresa` 14, `ElCambioDeRuta` 14, `ElTestigoDeAcceso` 13, `LaPantallaDeAcceso` 10.

`git diff --stat c7b9f80 911ba5c -- frontend/` sigue saliendo **vacío**, así que la medida de
`c7b9f80` valía; lo que la sostiene ahora no es ese argumento sino la medición del extremo real.

**El carril de integración no se ha ejecutado aquí, y esto es lo que hay que creerse y lo que no.**
El demonio de Docker sigue parado en esta máquina. Ejecutado de todos modos, el guion de la CI
contesta:

```
Integración (Testcontainers): 318 casos (0 correctos, 318 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 246, y 0 en los otros seis
```

Los 318 son **el mismo error repetido**, no 318 fallos: `DotNet.Testcontainers.Builders.
DockerUnavailableException : Docker is either not running or misconfigured … Details: Failed to
connect to Docker endpoint at 'npipe://./pipe/docker_engine' … System.TimeoutException : The
operation has timed out.` **Ni uno solo de esos casos se ha ejecutado.** Lo que sí dice esa salida
—y es lo único que dice— es que el barrido local **alcanza exactamente los mismos 318 casos** que
la CI ejecutó en verde: no falta ninguno por descubrir, falta el demonio.

**Y lo que se ejerció en su lugar, que es la parte que este ítem sí puede reclamar.** El defecto
central del ítem se veía **solo** en ese carril —siete rutas contestando 412 contra PostgreSQL— y
resultó no necesitarlo: decidir si una entrada es alta o modificación necesita el proveedor y el
modelo, no un servidor. `LoQueCuelgaNaceComoAltaTests` lo comprueba con la cadena de conexión
apuntando al puerto 1, en el carril rápido, en un segundo, y con su canario al lado. Lo mismo hacen
las cinco reglas de modelo, que leen el modelo ya construido sin abrir conexión. La regla nueva de
los ADR (`ElNumeroDeUnAdrEsSuyoYDeNadieMasTests`) tampoco necesita nada: lee el árbol.

### Verificado en local, con la salida real — ítem 1.5

```
contrato:     npm run api  ->  `esquema.ts` regenerado y SIN cambios
                              (`git status --porcelain` sobre el fichero, vacío)
migraciones:  Auditoria 3, Organizacion 4, Identidad 3, Terceros 1 — y el modelo
              coincide con ellas en los cuatro
openapi:      documento versionado al día — 82 operaciones (75 en el 1.4: las SIETE
              del módulo son las de Terceros)
catálogo de `type`: al día — 47 tipos, de 52 sitios de llamada. Es el artefacto que
              este ítem estrena (ADR-0030), y su paso EXISTÍA en la CI y NO en la
              batería de AGENTS.md: arreglado en el primer commit de esta tanda

frontal: typecheck / lint / format:check / test / build  ->  exit 0 los cinco
         test  ->  11 ficheros, 63 casos, 0 en rojo (eran 9 y 46 en el 1.4)
         presupuesto  ->  arranque 402/450 KiB en 3 ficheros · total servido 548/900 KiB
                          el trozo nuevo es `PaginaDeTerceros-CiAyuBs4.js`, 4,64 kB
         act()  ->  109 avisos en 7 ficheros, la MISMA cifra antes y después.
                    Los dos ficheros de test nuevos —`ElListadoDeTerceros.test.tsx` y
                    `ElTextoDeUnFallo.test.tsx`— no aparecen en el desglose: entran
                    con CERO

backend: dotnet build  ->  0 Advertencia(s), 0 Errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido  ->  576 casos (576 correctos, 0 con error, 0 omitidos) en 8
                            ensamblados, medido con el guion de la CI sobre los .trx
                            de esta máquina y con `artifacts/test-results` borrado antes:
                            BuildingBlocks.UnitTests 132, Organizacion.UnitTests 182,
                            Identidad.UnitTests 58, Terceros.UnitTests 39,
                            Organizacion.IntegrationTests 5, Arquitectura.Tests 23,
                            Api.FunctionalTests 136, Api.IntegrationTests 1
                            (528 en 7 ensamblados en el 1.4: +48 casos y +1 ensamblado.
                            Los DOS últimos son el canario del captador de registro, que
                            entró después de que la CI destapara el falso verde)
         carril integración -> NO EJECUTADO AQUÍ. Abajo, con firma.

licencias:   los conjuntos de paquetes resueltos de TODOS los `packages.lock.json` a
             los dos lados: 145 en `main`, 150 en la rama, y las cinco de diferencia
             son los cinco proyectos nuevos del propio módulo. CERO paquetes de
             terceros añadidos y cero retirados; `Directory.Packages.props` sin tocar
             y `frontend/package*.json` sin mover. Sale del diff, no de la memoria.
             Un `packages.lock.json` SÍ cambia —el de `Api.IntegrationTests`, donde
             Serilog pasa de `CentralTransitive` a `Direct` al arreglar los captadores—
             y no mueve la cifra: mismo nombre, misma versión, mismo `contentHash`.
             Cambia quién lo pide, no qué se resuelve, y por tanto ninguna licencia
```

**El carril de integración no se ha ejecutado aquí, y esto es lo que hay que creerse y lo que no.**
El demonio de Docker sigue parado en esta máquina. Ejecutado de todos modos, el guion de la CI
contesta:

```
Integración (Testcontainers): 271 casos (0 correctos, 271 con error, 0 omitidos) en 8 ensamblados
  — Organizacion.IntegrationTests 72, Api.IntegrationTests 199, y 0 en los otros seis
```

Los 271 son **el mismo error repetido**, no 271 fallos: `DotNet.Testcontainers.Builders.
DockerUnavailableException : Docker is either not running or misconfigured … Failed to connect to
Docker endpoint at 'npipe://./pipe/docker_engine'`. **Ni uno solo de esos casos se ha ejecutado**, así
que de este carril no se afirma nada desde aquí: eran 241 en el 1.4 y son 271, o sea que el ítem
añade **30 casos de integración** que en esta máquina no han corrido. Los verifica la CI.

**Con qué se ha ejercido en su lugar.** Dos cosas, y conviene separarlas porque valen distinto:

1. **Lo que no necesita servidor se ha ejercido de verdad, y es la mitad más algorítmica del ítem.**
   La validación de NIF, NIE y CIF vive entera en `Terceros.UnitTests` —39 casos, sin contenedor—, y
   con ella la batería generada en los dos sentidos. Las reglas de contrato del ítem —que ninguna
   respuesta lleve testigo de versión en el cuerpo, que cada acción declare su permiso, que el
   catálogo de `type` sea el que la API emite— corren en `Api.FunctionalTests`, con el host en pie y
   sin una sola dependencia externa: 134 casos, todos aquí.
2. **Lo que sí necesita PostgreSQL se ha ejercido EN LA CI, y a propósito.** Las dos mutaciones de la
   tabla que solo el carril de integración puede cazar —la 1 y la 3— se empujaron cada una en su
   rama para que la CI las ejecutara de verdad, en vez de darlas por rojas. Los dos runs, sus números
   y su desenlace están en la tabla. Es la diferencia entre «esto lo cazaría el carril de
   integración» y «esto lo cazó el carril de integración, run tal».

Lo que queda sin ejercer en ningún sitio hasta el run del ítem son los **30 casos nuevos en verde**:
que el alta devuelva 201 con `Location`, que el domicilio vaya y vuelva en los seis campos de R17, que
el cursor no repita, que el listado pagine con su total. Esos los dice el run, y solo el run.

#### Las seis mutaciones del 1.5, cada una aplicada, ejecutada y revertida

Las cuatro primeras filas se ejecutaron **en esta máquina**; la 1 y la 3 necesitan PostgreSQL, así que
se empujaron en su propia rama y las ejecutó la CI. Todas se aplicaron sobre **árbol limpio y con el
trabajo del ítem ya commiteado**, y se revirtieron con `git restore --source=HEAD` o borrando el
fichero — nunca con `git checkout --`, que es la orden que en el 1.4 se llevó por delante 128 líneas
de trabajo sin guardar.

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | **El conflicto del alta distingue lo bloqueado de lo que ya existe**: `IdentificacionDuplicada(bool bloqueada)` con dos redacciones, y una segunda consulta fuera del ámbito para saber cuál es | **Rojo en la CI, con la línea base ya en rojo por otra cosa** (aviso debajo de la tabla), [run 33974541031](https://github.com/AOjeda006/Bastion/actions/runs/33974541031) — Backend en `failure` en el paso 13, *Tests de integración (Testcontainers)*. Compila con 0 avisos, y el **carril rápido entero, el formato, las migraciones, el OpenAPI y el catálogo de `type` pasan en verde**: no la ve nadie más | `ElConflictoQueNoRevelaTests.El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`. Entera abajo, con las dos respuestas puestas una al lado de la otra |
| 2 | **El CIF acepta el carácter de control de la clase equivocada**: la rama de iniciales alfabéticas admite además el dígito | **Rojo ×2** en el carril rápido | `LaBateriaGeneradaTests.El_control_de_la_clase_equivocada_se_rechaza` (Terceros) y `NifTests.Un_identificador_con_el_caracter_de_control_equivocado_se_rechaza(valor: "P12345674")` (Organización). Dos carriles distintos y ninguno de los dos escrito para el otro |
| 3 | **La unicidad del alta deja de ver las fichas bloqueadas**: `&& !tercero.Bloqueo.EstaBloqueado` en el `Where` del repositorio | **Rojo en la CI, con la línea base ya en rojo por otra cosa** (aviso debajo de la tabla), [run 33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347) — Backend en `failure` en el mismo paso 13. Igual que la 1: compila, y el carril rápido entero sale verde | `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca` y `El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`. El alta pasa el filtro y choca contra el índice único, que sigue siendo incondicional |
| 4 | **Un código de error nuevo sin regenerar el artefacto** (`regimen-fiscal-no-admitido`) | **Rojo ×1**, y antes de compilar nada | `scripts/generar-errores.sh --comprobar`: «El catálogo de errores ha cambiado y `docs/api/errores.json` se ha quedado atrás», con el diff de la entrada que falta. Es el paso que la batería de `AGENTS.md` no nombraba |
| 5 | **`TerceroDto` con un campo de versión**, copiado a mano como `uint` para que no dependa de que exista un tipo | **Rojo ×1** en el carril rápido, nombrando **las cinco** respuestas | `NingunaLecturaEntregaTestigoDeVersionTests.Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo`, la regla del 1.4: «estas respuestas entregan un testigo de versión en el cuerpo, así que la llave que las cuatro exenciones de `If-Match` de los desbloqueos dan por inalcanzable ya se puede conseguir leyendo» |
| 6 | **Una regla nueva en un carril, sin su línea en el censo** (`MutacionSeisTests` en `Api.FunctionalTests`) | **Rojo ×1** | `ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados`: «estas reglas corren en `Bastion.Api.FunctionalTests` y no están declaradas: `MutacionSeisTests.Una_regla_nueva_que_nadie_ha_declarado`» |

**Un intento fallido de la 5, que dice algo del proyecto.** La primera forma —poner en el DTO el tipo
`VersionDeRecurso` de BuildingBlocks— **no llega a compilar**, y no por la regla sino por la
arquitectura: ese tipo vive en `BuildingBlocks.Application`, y `Terceros.Contracts` no puede
referenciar Application. La segunda —copiar el `uint` a mano— sí compila, y es además «la forma en que
esto se cuela de verdad», que es lo que dice el comentario de la propia regla. La tercera cosa que
apareció es de rutina: sin `<param name="Version">` el compilador para con `CS1573` antes de que la
regla llegue a mirar nada, o sea que la mutación hay que **terminarla** para que la comprobación
signifique algo.

**Y lo que dicen las mutaciones 1 y 3 juntas.** Las dos compilan, las dos pasan `dotnet format`, las
dos pasan el carril rápido entero —574 casos entonces, 576 ahora—, las dos pasan las migraciones, el OpenAPI y el catálogo
de `type`, y las dos rompen la propiedad del ítem. O sea que **de estas dos averías no avisa nada que
se pueda ejecutar sin Docker**, y esa es exactamente la razón por la que valía la pena gastar dos runs
en verlas rojas en vez de razonarlas.

> **Aviso sobre las dos filas que ejecutó la CI: la línea base contra la que se compararon NO
> estaba en verde.** Cuando se empujaron las mutaciones 1 y 3, el run del propio ítem —el
> [34045461496](https://github.com/AOjeda006/Bastion/actions/runs/34045461496)— ya salía rojo por
> **un caso de 271**: `ElConflictoQueNoRevelaTests.La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_
> el_identificador_dentro`, con `anotados.Count should be 2 but was 0`. Un rojo comparado contra un
> rojo no demuestra por sí solo lo que dice la tabla, y decirlo de otro modo sería exactamente el
> falso verde con los colores cambiados. Lo que sostiene las dos filas es el **recuento de casos
> rojos**, leído en las anotaciones de cada run: la base tiene **1**, la mutación 1 tiene **2** —el
> caso añadido es `El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO`, y su
> mensaje enseña los dos `detail` distintos— y la mutación 3 tiene **3** —los mismos, más
> `Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca`, los dos con
> `500 Internal Server Error`, que es el choque contra el índice único que la mutación predice—.
> Cada mutación añade **sus** rojos, nombrados, y ninguno de ellos es el de la base. La atribución
> se sostiene; la afirmación «sobre verde», no, y por eso se corrige aquí en vez de dejarla escrita.
>
> El rojo de la base tenía causa propia y está arreglado en `dbabdea`, en su fila de abajo.

#### La avería que la CI destapó y que ningún carril miraba: **el arnés no tenía arnés**

El caso rojo de la base no era un fallo del producto. Era el **captador** el que no capturaba.

`src/Api/Program.cs` hace `builder.Logging.ClearProviders()` y a continuación
`builder.Services.AddSerilog(...)`, que instala `SerilogLoggerFactory` — una fábrica de registro que
**ignora todo `ILoggerProvider` que no sea el suyo**. `RegistroDeSucesos` y `RegistroDeFallos` eran
proveedores, enganchados con `builder.ConfigureLogging(r => r.AddProvider(...))`. Entre la API que
escribía y el captador que existía **no había ningún cable**.

Lo que hace esto peor que un test mal escrito es cómo se lee el resultado. El test preguntaba
«¿cuántos sucesos se anotaron?», recibía **cero**, y cero tiene dos lecturas indistinguibles desde
fuera: «la API no anota» —un fallo del producto, que es lo que el rojo parecía decir— y «no hay
cable». La segunda es la verdadera, y no había nada que las separara.

**Y el gemelo llevaba roto desde el día que se escribió.** `RegistroDeFallos` existe por un motivo
muy concreto: los registros de un *job* de la CI devuelven 403 sin autenticar, así que un `500` allí
es una pared, y este captador adjunta el registro del servidor al mensaje de la aserción —que sí sale
publicado como anotación—. En el run de la mutación 3, el
[33974591347](https://github.com/AOjeda006/Bastion/actions/runs/33974591347), hay un `500` de verdad
y detrás **ni una línea** de `· registro del servidor:`. O sea que el diagnóstico falló en el
escenario exacto para el que fue construido, y no se supo hasta ahora. Eso no es una deducción: es lo
que enseña la anotación de aquel run.

**El arreglo, y por qué está donde está.** Los dos captadores pasan a ser `ILogEventSink` de Serilog,
que es lo que esa fábrica sí reparte. El enganche vive en `tests/Comun/CapturaDeRegistro.cs`,
**compartido y no copiado**, y esa decisión es la que da valor al canario: lo que se rompió no fue el
captador sino **cómo se enganchaba**, así que un canario con su propio enganche habría comprobado su
propia copia y habría dejado sin vigilar la línea que falla.
`ElCaptadorDeRegistroCapturaTests` ejecuta **esa misma línea** y vive en `Api.FunctionalTests`, el
carril rápido, porque un cable roto no necesita PostgreSQL para notarse — puesto donde solo corre con
Docker delante, habría avisado en la CI y no en la máquina de quien lo rompe, que es literalmente lo
que acaba de pasar.

Sus dos casos se vieron rojos, cada uno con su mutación sobre árbol limpio y revertida con
`git restore --source=HEAD`:

| Mutación sobre `CapturaDeRegistro` | Desenlace |
|---|---|
| El sumidero se registra pero no se engancha (`WriteTo.Sink` fuera del bucle) | **Rojo ×1** — `Lo_que_la_API_escribe_llega_al_sumidero_con_su_identificador_de_suceso`. Es la avería original, reproducida |
| `EventIdDe` se queda con el primer entero que encuentre, se llame como se llame | **Rojo ×1** — `Un_evento_sin_identificador_de_suceso_no_se_lee_como_si_tuviera_uno`. Un captador que recoge de más miente igual que uno que no recoge |

**Lo que este ítem aprende y no estaba escrito en ninguna regla.** Un arnés de pruebas —un captador,
un espía, un doble— es código sin nadie detrás: si deja de funcionar, lo que se ve no es un rojo sino
un verde, o un rojo que acusa al sitio equivocado. La pregunta que faltaba hacerse no era «¿el
dominio cumple la regla?» sino «¿la regla mira lo que dice mirar?». Aquí la respuesta llevaba siendo
que no desde el ítem que lo escribió.

#### La primera, entera

Es la del ítem, así que va con las dos respuestas puestas una al lado de la otra, que es lo que la
propiedad exige comparar. Sin mutar, el cuerpo que contesta el alta contra un tercero **activo** y el
que contesta el alta contra uno **bloqueado** son el mismo carácter a carácter salvo `traceId`:

```json
{
  "type":     "/errors/tercero-duplicado",
  "title":    "Conflicto",
  "status":   409,
  "detail":   "Esta empresa ya tiene un tercero con ese identificador fiscal.",
  "instance": "/api/v1/terceros/terceros",
  "traceId":  "«el de cada petición»"
}
```

La mutación le da al error el parámetro que no tenía y una redacción por caso:

```csharp
internal static ErrorDeOperacion IdentificacionDuplicada(bool bloqueada) =>
    ErrorDeOperacion.Conflicto(
        "tercero-duplicado",
        bloqueada
            ? "Ese identificador fiscal pertenece a una ficha dada de baja."
            : "Esta empresa ya tiene un tercero con ese identificador fiscal.");
```

y en el caso de uso, la segunda consulta —fuera del ámbito— que le dice cuál es, con la excusa más
razonable que hay escrita en su propio comentario, «un mensaje más útil»:

```csharp
bool visibleSinAbrirElAmbito = await terceros
    .ExisteLaIdentificacionAsync(empresaId, identificacion.Pais, identificacion.Numero, cancelacion)
    .ConfigureAwait(false);

return Resultado.Fallo<TerceroDto>(
    ErroresDeTercero.IdentificacionDuplicada(!visibleSinAbrirElAmbito));
```

Con eso, las dos respuestas dejan de ser la misma en el campo `detail`, y el test falla comparándolas
enteras — no leyendo una y buscándole la palabra «bloqueado» dentro:

```
las dos respuestas de conflicto se distinguen, así que el formulario de alta dice quién está bloqueado.
   contra el activo:    …"detail":"Esta empresa ya tiene un tercero con ese identificador fiscal."…
   contra el bloqueado: …"detail":"Ese identificador fiscal pertenece a una ficha dada de baja."…
```

**Por qué el test compara y no lee.** Un caso que afirmara «el mensaje no dice *bloqueado*» se quedaría
verde con esta mutación cambiándole tres palabras a la redacción —«ese identificador está reservado»—
y la fuga seguiría abierta: quien tenga el formulario de alta puede recorrer identificadores fiscales
y sacar la lista de quién está dado de baja en esa empresa, que es justo lo que el art. 32 de la
LOPDGDD manda reservar. Por eso lo que se compara es el **estado, el `Content-Type`, y el cuerpo
entero con las claves ordenadas y solo `traceId` normalizado** — y por eso el `detail` no lleva el
número dentro, que si no las dos respuestas no serían comparables enteras y habría que decidir qué
trozos no cuentan.

**Y el testigo que impide el falso verde.** El mismo caso da de alta antes contra un identificador
**libre** y exige `201`. Sin esa línea, un servidor que contestara 409 a todo —o que estuviera roto de
la misma manera en los dos caminos— pasaría por indistinguible sin haber comprobado nada. La otra
mitad va en dirección contraria y es un caso aparte: `La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_
el_identificador_dentro` exige **dos** anotaciones del suceso 8400, una con `bloqueada: True` y otra
con `bloqueada: False`, y que ninguna de las dos lleve el número fiscal. Sin él, «la traza lo
registra» sería una promesa escrita en un comentario.


### Verificado en local, con la salida real — ítem 1.4

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts regenerado desde openapi.json y SIN cambios (el DTO nuevo ya
         estaba dentro): `git status --porcelain` sobre el fichero, vacío
         test  ->  9 ficheros, 46 casos, 0 en rojo
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB
                          (el ítem no toca el frontal: los mismos números que el 1.3)

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
              (este ítem NO toca el esquema: añade una lectura sobre lo que ya hay)
openapi:      el documento versionado está al día — 75 operaciones, una más que el 1.3,
              y es GET /api/v1/organizacion/bloqueados. El 75 sale de dos fuentes que no
              se derivan una de otra: el guion del contrato y el recuento de
              `El_barrido_encuentra_el_inventario_entero`

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0 (tres avisos corregidos antes:
                               IDE0037, IDE0007 e IDE0270 en el código nuevo)
         carril rápido     ->  132 + 180 + 58 + 1 + 23 + 5 + 129 = 528 casos, 0 con error
                               (BuildingBlocks / Organizacion.Unit / Identidad /
                                Api.Integration SIN Docker / Arquitectura /
                                Organizacion.Integration SIN Docker / Functional)
                               Eran 525 con 126 en Functional cuando se empujó; los tres
                               de más son `ElInquilinatoSeConstruyeSoloTests`, la regla
                               que salió del rojo de la CI — abajo, «La sexta»
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine; ... open
                               //./pipe/dockerDesktopLinuxEngine: The system cannot find
                               the file specified». Cliente 29.7.2 presente, servidor no.
                               Lo verifica la CI, en el run de arriba.

recuento:     el guion de la CI sobre los .trx de esta máquina, y esta vez ANTES de
              empujar y no después: ROJO nombrando «Bastion.Api.IntegrationTests.dll»
              como ensamblado no declarado, y VERDE —525 casos en 7 ensamblados— tras
              declararlo. Es la pareja que dice que la línea del workflow es el arreglo.
              Repetido al final, ya con la regla de composición dentro: 528 casos en 7
              ensamblados, y la lista declarada del workflow casa en los dos sentidos

licencias:    `git diff --name-only b75b312..HEAD -- '*packages.lock.json'` -> CERO
              ficheros. Los cuatro `.csproj` tocados añaden un `<Compile Include>` y
              ninguna `PackageReference`; `frontend/package*.json` tampoco se mueven.
              La frase sale del diff, no de la memoria
```

**Lo del carril parado, con nombre y apellidos.** De lo que este ítem añade y necesitaría PostgreSQL,
**la parte que no necesita el servidor se ejerció aquí**, y es justo la que más podía romperse:
`LaTraduccionASqlTests.El_listado_de_lo_bloqueado_se_traduce_entero` ejecuta el `ListarAsync` real
contra `Host=127.0.0.1;Port=1` y comprueba que **llegó a la base** — o sea que la consulta se tradujo
entera y lo único que falló fue la conexión. Eso cubre las dos trampas que este listado tenía:
`Concat` de tres ramas después de una proyección (EF no lo traduce si la proyección es a un tipo
propio) y el `ORDER BY` a través de esa proyección (solo lo traduce si es un inicializador de
objeto, no una llamada a constructor). Las dos aparecieron **en rojo** al montarlo y están contadas
en *Decisiones tomadas → ítem 1.4*. Lo que queda para el carril de integración es lo que de verdad
necesita filas: los **cuatro** casos de `ElAccesoReservadoDelArticulo32Tests` —que la fila bloqueada
desaparezca del camino ordinario y aparezca en este, que la supresión traiga su vencimiento a seis
años, que la respuesta **no lleve `ETag`** ni un testigo dentro del JSON, y que lo bloqueado de otra
empresa no asome— y el censo de los 134 casos de ese carril.

#### Las cinco mutaciones del 1.4, cada una aplicada, ejecutada y revertida

| # | Mutación | Desenlace | Quién la caza |
|---|---|---|---|
| 1 | **`BloqueadoDto` con `string Version` por fila**, rellenado desde el instante del bloqueo | **Rojo ×1** en el carril rápido | `Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo`, nombrando la ruta y el campo. Entera abajo |
| 2 | El quinto `ViendoLoBloqueado(...)` **sin declarar** en `s_aperturasDeBloqueoPermitidas` | **Rojo ×1** | `El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado`, con las dos listas enteras y el fichero de más |
| 3 | El motivo nuevo **declarado y sin usar** (el listado vuelve a abrir con `AdministracionDelBloqueo`) | **Rojo ×1** | `Cada_motivo_para_ver_lo_bloqueado_tiene_su_sitio_y_cada_sitio_su_motivo`: «Usados: AdministracionDelBloqueo. Declarados: AccesoReservadoDelArticulo32, AdministracionDelBloqueo» |
| 4 | El listado **sirviendo lo bloqueado sin registrar quién pregunta**: fuera el ámbito, y `.IgnoreQueryFilters()` en las tres ramas del repositorio | **Rojo ×3** | `Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo` (nombrando el fichero y la llamada), más las dos de arriba, que dejan de tener quinta apertura y motivo usado |
| 5 | Una regla **borrada** de su ensamblado (`El_barrido_ve_los_cuerpos_y_reconoce_un_testigo`, en `Api.FunctionalTests`) | **Rojo ×1**, y es el ÚNICO | `ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados`. Sin el censo del §4, el carril habría salido verde con 125 casos en vez de 126 |

**Una variante de la 5 que no llega a ser mutación:** quitar el `[Fact]` y dejar el método público
—la forma más silenciosa de apagar una regla— **no compila**: `xUnit1013` la para en el analizador.
La que sí compila es borrar el método entero, que es la que está en la tabla.

**Y un arreglo que salió de mirar el rojo de la 5.** El censo se ponía rojo, sí, pero volcaba los
**115 nombres** de las dos listas y marcaba el punto en el que se desplazan — cuando su propio
encabezado dice que existe para decir **cuál** falta y no cuántos. Ahora compara primero las
diferencias en los dos sentidos y el rojo empieza por el nombre: «estas reglas están declaradas en
`Bastion.Api.FunctionalTests` y ya no las ejecuta nadie: `NingunaLecturaEntregaTestigoDeVersionTests.El_barrido_ve_los_cuerpos_y_reconoce_un_testigo`».
La comparación entera se queda detrás, que es la que no depende de que las diferencias estén bien
calculadas.

#### La primera, entera

Es la del ítem, así que va con lo que la provocó. El DTO del listado nació **sin** ningún campo de
versión, y su documentación dice por qué; la mutación le añade uno con la excusa más razonable que
hay —«para poder desbloquear con `If-Match` desde aquí mismo»—:

```csharp
public sealed record BloqueadoDto(
    Guid Id,
    string Tipo,
    string? Codigo,
    string Nombre,
    DateTimeOffset BloqueadoEn,
    string Motivo,
    DateTimeOffset? VenceEn,
    string Version);          // <- la mutación
```

y en el caso de uso, rellenándolo:

```csharp
retencion.VenceEn(...),
recurso.BloqueadoEn.Ticks.ToString(CultureInfo.InvariantCulture));   // <- la mutación
```

Compila, pasa el formateador, y el listado sigue funcionando igual de bien. El carril rápido:

```
Bastion.Api.FunctionalTests.Bloqueos.NingunaLecturaEntregaTestigoDeVersionTests
  .Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo [FAIL]

  Shouldly.ShouldAssertException : fugas should be empty but had 1 item and was
  ["GET /api/v1/organizacion/bloqueados -> BloqueadoDto.Version (es el testigo de
    concurrencia optimista tal cual. En el cuerpo obliga a que esté también en las
    listas, que se leen sin rastreo y no lo traen: el mismo campo valdría una cosa en
    una respuesta y cero en otra)"]

  Additional Info:
    estas respuestas entregan un testigo de versión en el cuerpo, así que la llave que
    las cuatro exenciones de If-Match de los desbloqueos dan por inalcanzable ya se
    puede conseguir leyendo. O se quita el campo, o esas cuatro exenciones caducan y
    hay que volver a exigir If-Match

Con error! - Con error: 1, Superado: 125, Total: 126 - Bastion.Api.FunctionalTests.dll
```

Eso es exactamente lo que el ítem pedía: la mitad que sobrevive de las cuatro cláusulas «DEPENDE DE»
convertida en algo que **se pone rojo**, y roja **antes** de que el endpoint existiera. En el carril
de integración hay además la misma pregunta hecha por el cable —`ETag` ausente y ninguno de
`version/etag/xmin/rowversion/concurrencia` dentro del JSON—, con su comprobación de no vaciedad para
que no la conteste una página vacía.

#### La sexta, que no puse yo: la que encontró la CI

Las cinco de arriba son mutaciones **provocadas**. Hubo una sexta que no provoqué, y es la que más
dice del ítem: el run **33904703923** salió **rojo**. No en el carril rápido, donde todo estaba verde,
sino en Integración —job `101126778489`, paso 12— con **diez** casos caídos y siempre el mismo
mensaje:

```
System.InvalidOperationException : Unable to resolve service for type
'Bastion.BuildingBlocks.Application.Autorizacion.IUsuarioActual' while attempting to
activate 'Bastion.BuildingBlocks.Infrastructure.Bloqueos.AccesoALoBloqueado'.
```

Los de `ReprocesarNoDuplicaTests`, `ElTrabajoDeFondoVaciaLaColaTests` y
`LaEdadDelMasViejoSeMideTests`, más los dos de `SinLaTablaElPublicadorSeParaTests` que caen detrás. Y
los pasos 15 y 16 —el artefacto de OpenAPI— rojos por arrastre, no por sí mismos.

**Qué pasó.** El requisito 2 del enunciado —que quede registrado **quién** pregunta, no solo que se
preguntó— le dio a `AccesoALoBloqueado` una dependencia nueva: `IUsuarioActual`. Ese servicio lo
registra `AgregarAutorizacionPorPermisos()`; `AccesoALoBloqueado` lo registra `AgregarInquilinato()`.
Dos métodos de extensión distintos. La API llama a los dos y no notó nada. El **publicador de la
bandeja de salida** corre fuera de una petición, no necesita permisos y llama solo al primero: se cayó
al construir el primer `DbContext`.

**Lo que el comentario ya prometía.** `AgregarInquilinato` dice desde el 0.9 que el inquilinato y el
acceso a lo bloqueado van juntos «porque los dos son cosas que un `DbContext` de módulo necesita para
construirse, y separarlos dejaría un host que registra una y se olvida de la otra reventando al
resolver el primer contexto». El paquete dejó de ser un paquete y **el comentario siguió diciendo que
lo era**. Una promesa escrita en prosa no se pone roja.

**El arreglo, y la regla que lo sostiene.** El arreglo es una línea: `AgregarInquilinato` registra
también `IUsuarioActual` con `TryAddScoped`. `TryAdd` y no `Add`, porque la API llama además a
`AgregarAutorizacionPorPermisos` y con `Add` a secas habría dos descriptores del mismo servicio —quien
resuelve se queda con el último, o sea que el **orden de las llamadas del `Program.cs`** elegiría
implementación, en silencio—. La regla es `ElInquilinatoSeConstruyeSoloTests`, tres casos en el carril
rápido: se mira qué descriptores añade la llamada, se **descubren** en vez de teclearse —un servicio
nuevo entra en la regla el día que se registra, no el día que alguien se acuerde—, se construyen todos
en un ámbito con `validateScopes: true`, y se afirma que la lista no está vacía (ADR-0020: si la
extensión dejara de registrar algo, el bucle recorrería cero servicios y su verde no significaría
nada). El tercer caso llama dos veces y exige que el recuento de descriptores no se mueva, que es la
afirmación del `TryAdd`.

**Por qué en local no se veía, dicho sin excusa.** No es que Docker estuviera parado: un fallo de
composición no necesita base de datos, necesita que **alguien intente construir lo registrado**, y eso
no lo hacía nadie. La prueba es que la regla nueva reproduce el mensaje exacto de la CI en el carril
rápido y sin contenedor. Comentando la línea del arreglo:

```
ElInquilinatoSeConstruyeSoloTests.Y_el_acceso_a_lo_bloqueado_se_construye_nombrandolo [FAIL]
ElInquilinatoSeConstruyeSoloTests.Todo_lo_que_registra_el_inquilinato_se_puede_construir_sin_nada_mas [FAIL]
  System.InvalidOperationException : Unable to resolve service for type
  'Bastion.BuildingBlocks.Application.Autorizacion.IUsuarioActual' while attempting to
  activate 'Bastion.BuildingBlocks.Infrastructure.Bloqueos.AccesoALoBloqueado'.

Con error! - Con error: 2, Superado: 1, Total: 3
```

Lo que la CI tardó minutos y un contenedor en decir, el carril rápido lo dice antes de empujar. Ojo:
esto **no cierra** la nota del carril de integración parado. Dice que el carril rápido caza una clase
más de fallo, no que sustituya al otro.

### Verificado en local, con la salida real — ítem 1.3

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts regenerado desde openapi.json (el POST /buscar y TramoDeEmpresaDto)
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
              (este ítem no toca el esquema: consolida tipos y añade una LECTURA)
openapi:      el documento versionado está al día — 74 operaciones, una más que el 1.2,
              y es POST /api/v1/organizacion/empresas/buscar. El 74 sale de dos fuentes
              que no se derivan una de otra: el guion del contrato y el recuento de
              `El_barrido_encuentra_el_inventario_entero`

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 180 + 58 + 3 + 121 + 23 = 503 casos, 0 con error
                               (BuildingBlocks / Organizacion.Unit / Identidad /
                                Organizacion.Integration SIN Docker / Functional /
                                Arquitectura)
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine; ... open
                               //./pipe/dockerDesktopLinuxEngine: The system cannot find
                               the file specified». Cliente 29.7.2 presente, servidor no.
                               Lo verifica la CI, en el run de arriba.

recuento:     el guion de la CI, `scripts/ci/recuento-de-tests.sh`, sobre los .trx de
              esta máquina: ROJO nombrando «Bastion.Organizacion.IntegrationTests.dll»
              como ensamblado no declarado, y VERDE —503 casos en 7 ensamblados—
              tras declararlo. Es la pareja que dice que la línea añadida al
              workflow es el arreglo y no una coincidencia
```

**Lo del carril parado, con nombre y apellidos.** De las tres consultas nuevas que sí necesitarían
PostgreSQL para ejercerse enteras, **la parte que no necesita el servidor se ejerció aquí**:
`LaTraduccionASqlTests` genera el SQL de los diez listados de Organización —orden de omisión, cada
campo ordenable en los dos sentidos, y el filtro— y de la búsqueda con su cursor, sin contenedor y en
dos segundos. Lo que queda para el carril de integración es lo que de verdad necesita filas: que el
`ILIKE` encuentre lo que tiene que encontrar y que el «después de esto» del cursor no repita ni se
salte ninguna. Eso lo verifica la CI. **Lo de Identidad no tiene ese ejercicio local**: ningún
proyecto de pruebas ve sus repositorios, así que sus tres listados solo se traducen de verdad en el
carril de integración.

### Verificado en local, con la salida real — ítem 1.2

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts sin cambios tras regenerar el cliente
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Auditoria 3, Organizacion 4, Identidad 3, y el modelo coincide con ellas
openapi:      el documento versionado está al día — 73 operaciones (este ítem no añade
              endpoints: un puerto de lectura entre módulos se resuelve EN PROCESO, no
              por HTTP, y que el contrato no se mueva es la comprobación de eso)

backend: dotnet build      ->  0 errores, 0 advertencias
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 58 + 180 + 23 + 118 = 497 casos, 0 con error
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker sigue parado.
                               `docker info` -> «failed to connect to the docker API at
                               npipe:////./pipe/dockerDesktopLinuxEngine». Los 237 casos
                               (72 + 165) «fallan» en 101 ms y 209 ms con
                               DockerUnavailableException: «Docker is either not running
                               or misconfigured — Failed to connect to Docker endpoint at
                               'npipe://./pipe/docker_engine'». Es la forma que tiene
                               Testcontainers de decir que no hay dónde levantar PostgreSQL,
                               no un fallo del código. Los SIETE casos nuevos de
                               `LosPuertosDeLecturaTests` están entre esos 237 y quien los
                               verifica es la CI.
```

### Lo que la CI encontró y en local no se veía

El primer *run* del 1.2 —[33805185736](https://github.com/AOjeda006/Bastion/actions/runs/33805185736)
sobre `e087c56`— salió **rojo en el carril de integración**, y con razón: `Divisa.Crear("PTX", …)`
**lanza**. El catálogo de divisas de los bloques comunes rechaza lo que no sabe redondear, porque una
divisa guardada sin saber con cuántos decimales redondea es una factura mal calculada esperando. O
sea que el código inventado que valía para un impuesto —`PTO-VIG`, que solo mide 20 caracteres— **no
existe** para una divisa.

**Lo que enseña no es el fallo, es cómo se encontró.** Un test que no se puede ejecutar aquí —el
demonio de Docker está parado— se empujó **compilando**, y compilar no ejerce nada: `Crear` lanza en
ejecución, no en el compilador. La regla que queda escrita: **cuando un carril no se puede ejecutar
en local, hay que ejercer en local todo lo del carril que no necesita el carril.** Aquí eso eran las
cuatro llamadas a las factorías del dominio, que no tocan la base de datos y tardan 19 ms. Se hizo
con un test canario temporal, que confirmó que de las cuatro **solo fallaba una** — y eso es lo que
convierte «hay algo roto en 237 casos» en «está roto esto». Corregido con `JPY`, que además es el
contraejemplo de cero decimales del propio catálogo.

**Los tres *runs* de esta rama, y por qué son tres.** El primero
([33805047002](https://github.com/AOjeda006/Bastion/actions/runs/33805047002), sobre `b6aaf60`) salió
**cancelado**, no verde: lo reemplazó el empujón siguiente, y un *run* cancelado no es un *run* verde
—la lección del 0.16—. El segundo (33805185736, sobre `e087c56`) es el rojo de arriba. El tercero
([33806271861](https://github.com/AOjeda006/Bastion/actions/runs/33806271861), sobre `c54f783`) es el
que cierra.

Y no se arregló mirando la salida de la CI: el registro del *job* pide permisos de administrador
(`403 Must have admin rights to Repository`) y el artefacto tampoco se puede bajar sin credenciales.
Lo que sí se puede leer sin ellas son los **pasos** del *job*, y ahí estaba dicho: falló «Tests de
integración (Testcontainers)», y los tres rojos de detrás —«Publicar el OpenAPI» omitido, «Bajar el
artefacto» y «El artefacto contiene el contrato»— eran **consecuencia**, no causa. La anotación «No
hay openapi.json dentro del artefacto» era la más ruidosa y la que menos decía.

### Las ocho mutaciones del 1.2, cada una aplicada, ejecutada y revertida

| # | Mutación | Rojo por | Qué enseña |
|---|---|---|---|
| 1 | `DivisaId` en un agregado de Identidad, sin puerto | **Afirmación 1** | La del enunciado: el descubrimiento por nombre encuentra el identificador ajeno y exige su declaración |
| 2 | `DivisaPreferidaId` en un agregado de Identidad, **nombre que no casa y sin declarar** | **Afirmación 5, y solo ella** | La que decide el ítem: con las cuatro del enunciado **habría salido verde**. Los otros 22 casos pasan |
| 3 | El arnés: romper el patrón de descubrimiento (`EndsWith("Id")` → `EndsWith("Idx")`) sin tocar el dominio | **Afirmación 3** (y la 2 de rebote) | El arnés no tiene arnés: una regla que deja de mirar se pone **verde**, no roja, salvo que alguien afirme que encuentra algo |
| 4 | Borrar la declaración de `TokenDeRefresco.EmpresaActivaId` | **Afirmación 5** | El identificador que el descubrimiento **no** ve queda igualmente cubierto |
| 5 | Dejar una declaración con la clave de una propiedad que no existe | **Afirmaciones 2 y 5** | La lista no puede engordar con inventario muerto |
| 6 | Declarar un cruce con el campo `Puerto` vacío | **Afirmación 1** | Declarar no basta: hay que decir **qué** lo valida |
| 7 | Apuntar el `Puerto` a `…Sesiones.IPuertoInventado` | **Afirmación 4** | Un puerto tecleado que no existe no es un puerto: «no está entre las puertas públicas declaradas» |
| 8 | Cambiar el cruce declarado por otro que no cubre el identificador | **Afirmación 4** | El permiso de cruzar y el identificador que lo necesita son **el mismo** hecho, y se comparan |

Y el control después de la batería, con el árbol restaurado: **23 de 23 en verde**.

**Ítem 1.1 cerrado — el presupuesto ya mide lo que dice medir:**
[run 33779064545](https://github.com/AOjeda006/Bastion/actions/runs/33779064545) sobre `c3e0119`,
los **tres** *jobs* en `success` (y el mismo árbol, ya en `main`, en el
[run 33779690080](https://github.com/AOjeda006/Bastion/actions/runs/33779690080)).

El paso de la CI pasa de una cifra a dos, calculadas por `scripts/ci/presupuesto-del-frontal.sh`:
**arranque 391/450 KiB** (lo que `index.html` referencia, que es lo que se paga antes de pintar
nada) y **total servido 532/900 KiB**. Se suman **bytes** y no bloques de disco, así que la cifra
local y la del *runner* son la misma y un ajuste de presupuesto ya no cuesta un *run* (el 0.1 dejó
escrito lo contrario: 1097 kB en local contra 1104 en el *runner*).

**Y esa promesa se comprobó, no se supuso.** El `::notice::` del *runner* dice, palabra por palabra,
lo mismo que la ejecución local: `Frontal · arranque 391/450 KiB en 3 ficheros · total servido
532/900 KiB`. Es la primera medida del proyecto en la que las dos máquinas dan el mismo número.

### Verificado en local, con la salida real

```
frontal: api / typecheck / lint / format:check / build   ->  exit 0 los cinco
         esquema.ts sin cambios tras regenerar el cliente
         test  ->  9 ficheros, 46 casos
         presupuesto  ->  arranque 391/450 KiB en 3 ficheros · total servido 532/900 KiB

migraciones:  Organizacion 4, Identidad 3, y el modelo coincide con ellas
openapi:      el documento versionado está al día — 73 operaciones

backend: dotnet build      ->  0 errores
         dotnet format --verify-no-changes  ->  exit 0
         carril rápido     ->  118 + 58 + 180 + 18 + 118 = 492 casos
         carril integración -> NO EJECUTADO AQUÍ: el demonio de Docker está parado
                               (`docker info` -> exit 1, cliente 29.7.2 sin servidor).
                               Los 230 casos «fallan» en 112 ms y 224 ms, que es la forma
                               que tiene Testcontainers de decir que no hay dónde levantar
                               PostgreSQL. Este ítem no toca ni una línea de backend
                               —documentación, un guion de bash y un paso del workflow—,
                               así que la verificación de ese carril es la de la CI, y la
                               dio: 230 casos, 230 correctos, 0 con error.
```

### Las siete mutaciones del 1.1, cada una aplicada, ejecutada y revertida

| # | Mutación | Qué hizo |
|---|---|---|
| 1 | `src=` → `data-src=` en `index.html` | **rojo**: «el arranque no incluye ningún .js» |
| 2 | quitar el `<script type="module">` entero | **rojo**: el mismo, por el otro camino |
| 3 | borrar `index.html` | **rojo**: «sin él no se sabe qué descarga el navegador» |
| 4 | apuntar a un `dist` que no existe | **rojo**: «¿se ha ejecutado el build?» |
| 5 | bajar el tope de arranque a 300 KiB | **rojo**, y solo el de arranque |
| 6 | bajar el tope total a 400 KiB | **rojo**, y solo el total |
| 7 | **hacer estática una ruta diferida** | **rojo**: arranque **391 → 507 KiB** |

**La séptima es el ítem entero.** Es un empeoramiento real —116 KiB más antes de poder pintar nada—
y la métrica anterior no solo no lo cazaba: **bajaba**, de 554 a 546 kB, porque con un fragmento
menos `du` redondea a menos bloques. O sea que el presupuesto viejo se ponía **más verde** ante un
empeoramiento. Esa es la clase de falso verde que el ADR-0022 y el ADR-0020 persiguen, aquí en el
paso que decide si el frontal ha engordado.

**Y la primera mutación destapó un fallo del propio guion**, que es para lo que existen: `data-src=`
casaba con `src=` porque el patrón no exigía nada delante, así que la mutación que pretendía romper
el parseo salió **verde**. El patrón pasa a exigir un espacio, y el motivo queda escrito en el
guion.

**Dónde retomar exactamente:** el ítem **1.2**, los puertos de lectura de Organización y la cuarta
vía en rojo (ADR-0024).

## Checklist

### Fase 1 · Maestros (2026-09-03)

> **La fase 1 no tiene Anexo A.3.** El §15 da el criterio de la fase entera —«alta de
> cliente/proveedor con NIF validado y de artículo con unidad, impuesto y tarifa; listados paginados
> y filtrados en servidor; dominio cubierto por tests»— y el desglose en ítems lo acordaron usuario
> y agente en la **puerta de clarificación de la fase 1**, cuyas doce respuestas con su motivo están
> en *Decisiones tomadas*. Este checklist es el resultado; **no se reordena ni se amplía** por
> iniciativa propia, igual que el A.3.
>
> **Cerrada con catorce ítems, y no con once** (corregido el 2026-09-16). Las once casillas de la
> puerta de clarificación se marcaron el 2026-09-15, y las certificó el run **34939033897** sobre
> `73d1db7`, el del 1.11; la verificación de ese cierre abrió las tres addenda 1.12–1.14, y la fase
> la certifica ahora el run **35103339786** sobre `f3c749e`, que cierra las tres. Los runs de las
> catorce casillas —uno por casilla, y uno solo para las tres addenda—, el repaso criterio por
> criterio y el traspaso a la fase 2 están en *Estado actual*.

- [x] **1.1 · El presupuesto del frontal, remedido** — criterio de aceptación: dos métricas
  —**arranque** frente a **total**— en vez de una; topes **450 KiB** y **900 KiB** con el cálculo
  escrito (qué tarda en cargar y con qué red); y la CI midiendo **lo que dice medir**, comprobado
  enseñando la cifra de antes y la de después. Motivo del orden: la métrica de hoy suma fragmentos
  que el navegador no descarga al arrancar, y la fase 1 añade pantallas diferidas — el tope saltaría
  por lo que no es el problema, y en mitad de Catálogo.
  Lo mide `scripts/ci/presupuesto-del-frontal.sh`, que suma **bytes** y no bloques de disco, publica
  el desglose fichero a fichero y **falla si no ha mirado nada**. Decisión en el **ADR-0028**.
  Probado con **siete mutaciones**, y la séptima es la que enseña por qué existía el ítem: hacer
  estática una ruta diferida sube el arranque de **391 a 507 KiB** (rojo) mientras el `du` de antes
  **baja de 554 a 546 kB** — o sea que la métrica vieja se ponía **más verde** ante un empeoramiento
  real.
- [x] **1.2 · Los puertos de lectura de Organización, y la cuarta vía en rojo** — criterio de
  aceptación: `IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e `IConsultaDeDivisas` en
  `Organizacion.Contracts`, con lo mínimo que cada consumidor necesita; el cruce declarado en
  `s_crucesDeclarados` **en el mismo commit** que su primer consumidor; y la **regla de dos
  fuentes** —lista declarada más descubrimiento por reflexión, comparadas enteras y en los dos
  sentidos— vista en **rojo** con su mutación: un `DivisaId` en un agregado sin puerto.
  Los tres puertos contestan un `EstadoDeMaestro` de **tres valores** —no un `bool`—, porque el
  ADR-0023 obliga a distinguir «existe» de «vale para un alta nueva». La regla vive en
  `LosIdentificadoresAjenosTests` y **no es una igualdad**: son **cinco** afirmaciones, y la quinta
  no estaba en el enunciado — se añadió porque es la única que caza un identificador ajeno con
  **nombre que no casa y sin declarar**. Ocho mutaciones, ocho rojos, cada una con la afirmación que
  la cazó, en *Estado actual*.
- [x] **1.3 · El contrato de listado, y los tipos de paginación consolidados** — criterio de
  aceptación: filtro y orden en servidor con tope; la búsqueda por **cuerpo** con cursor opaco, de
  modo que **ni la URL ni el enlace a la página siguiente** lleven el criterio, con su exención de
  `s_exentas` escrita **con el endpoint**; `Bastion.BuildingBlocks.Contracts` con `Paginacion` y
  `PaginaDe`, `ConsultaPaginada` y `Paginador` en los comunes que sus capas ya referencian, y las
  copias de Identidad y Organización **borradas**; y `TodaEscrituraDiceComoSeProtegeTests`
  **descubriendo** sus ensamblados de `Bastion.Api` en vez de tenerlos tecleados.
  Los tipos de recorrido son **cuatro y dos parejas**: `Paginacion`/`PaginaDe` para el listado y
  `Recorrido`/`TramoDe` para la búsqueda, en vez de un tipo con la mitad de los campos vacíos en
  cada respuesta. Las **ocho copias** están borradas y la divergencia ya **no compila** —tres formas
  intentadas, tres errores del compilador, ninguna prueba en rojo—. El universo de `Todas()` sale de
  la **tabla de enrutado** y se compara con una segunda fuente en disco; el criterio sensible lo
  vigila un barrido nuevo sobre el **explorador de API**, que reconoce los listados por lo que
  **devuelven** y no por cómo se llaman. Cinco mutaciones en *Estado actual*.
- [x] **1.4 · El camino de lectura de lo bloqueado** — criterio de aceptación: **listado** —y no
  `GET` individual, a propósito— de lo bloqueado para un rol nominativo y trazado, con su permiso y
  su ADR; `s_aperturasDeBloqueoPermitidas` comparada entera con **cinco** sitios; y reescrita la
  mitad que caduca de las cuatro cláusulas «DEPENDE DE» del ADR-0017, porque una condición que ya no
  es cierta y sigue escrita es una exención que **parece** razonada.
  `GET /api/v1/organizacion/bloqueados` con permiso propio (`organizacion.bloqueado.ver`), motivo
  propio (`AccesoReservadoDelArticulo32`, el **segundo** valor del enumerado, que pasa a ser lista
  cerrada comparada en los dos sentidos) y la traza anotando **quién** pregunta y no solo con qué
  motivo. El DTO **no lleva versión**, y eso ya no es una nota de confianza: son **dos** reglas que
  se ponen rojas —una sobre el contrato entero y otra sobre la respuesta de verdad—. La mitad
  caducada de las cuatro cláusulas está reescrita diciendo qué la sustituye. Y el bloqueo estrena
  **fecha de vencimiento** (`PoliticaDeRetencion`: seis años del art. 30 del Código de Comercio,
  colgando del motivo, configurable por instalación), que cierra media nota abierta desde el 0.11.
  Cinco mutaciones en *Estado actual*, y la primera entera con el DTO que la provocó.
- [x] **1.5 · Terceros: el agregado y su identidad** — criterio de aceptación: alta con **NIF, NIE o
  CIF validados de verdad** (letra de control), extranjero como identificador **opaco con país** y
  con estado de verificación; dirección **estructurada** (R17); roles sobre un solo agregado (§7.2);
  unicidad por **(empresa, NIF)**; el alta contra un identificador bloqueado devuelve un conflicto
  **que no revela**; listado paginado y filtrado por el contrato del 1.3; y `features/terceros/`.
  Y, por el **ADR-0030**, lo que hace falta para que el frontal escriba el texto de una validación
  desde el `type` del `ProblemDetails`: (a) el conjunto de `type` emitibles llega al frontal en un
  **artefacto generado y versionado** con el mismo trato que `openapi.json` —se genera, se versiona,
  la CI lo vuelve a generar y compara—; (b) el barrido de diccionarios enteros
  (`ElCambioDeIdioma.test.tsx`) pasa a comparar **también** los `type` contra sus entradas, de modo
  que un `type` sin texto sea rojo el día que se escribe y no el día que alguien ve una clave sin
  traducir; y (c) en ejecución, un `type` desconocido cae al mensaje genérico **con el identificador
  de traza** que el `ProblemDetails` ya lleva. Los errores **por campo** no entran: ahí el servidor
  nombra el campo y el frontal pone su etiqueta, y eso ya estaba resuelto.
  **Dentro del 1.5, el mecanismo va antes que el primer texto que lo use**: primero (a) y (b) —el
  artefacto y el barrido comparando—, y solo después las pantallas de Terceros. Al revés, el ítem
  escribiría los textos a mano y el barrido nacería ya con excepciones que justificar.
  **Hecho, y el orden se respetó**: los dos primeros commits son el generador de
  `docs/api/errores.json`, su paso en la CI y el barrido de diccionarios comparándolo; las pantallas
  vinieron después, así que ningún `type` de Terceros nació sin texto. El módulo estrena **siete
  rutas** —listado, alta, búsqueda por cuerpo, ficha, modificación, bloqueo y desbloqueo—, la
  identificación es un **tipo complejo de tres columnas** (país, número, verificación) validado de
  verdad para `ES` y opaco para el resto, y el domicilio fiscal es **obligatorio**, al revés que el de
  un almacén. La unicidad por (empresa, identificador) **abarca lo bloqueado** y es una decisión
  escrita, no la del índice por omisión: se comprueba en la base con `indpred IS NULL` y por el efecto
  con el desbloqueo que no choca. El conflicto que no revela se afirma **comparando las dos respuestas
  enteras**, no leyendo una, y la traza sí distingue por dentro sin llevar el número fiscal. La
  batería del carácter de control está **generada** —ni un identificador fiscal real en fixtures,
  semillas ni prosa— y recorre las dos direcciones. Seis mutaciones en *Estado actual*, dos de ellas
  ejecutadas en la CI porque solo el carril de integración las caza, y la primera entera con las dos
  respuestas puestas una al lado de la otra. Queda anotado lo que NO entra: la pantalla de búsqueda
  por cuerpo, el régimen fiscal del §7.2 —que no está en el criterio de ningún ítem— y que un tercero
  bloqueado no aparece en ningún listado, porque el del art. 32 solo recorre empresas, almacenes y
  ubicaciones.
- [x] **1.6 · Terceros: lo que cuelga** — criterio de aceptación: `Contacto`, `CuentaBancaria` con
  IBAN validado, `CondicionPago` con el tope de **60 días de la Ley 3/2004 contado desde la
  entrega**, y `LimiteCredito` **solo como importe**. Fuera, por la raya de la P6: `MandatoSEPA`
  (fase 6) y el riesgo vivo (fase 4).
  **El ítem empezó por otro sitio, y esa es la primera anotación**: antes de lo que cuelga se cerró
  el **agujero del art. 32** —cinco agregados implementan el bloqueo y el listado solo veía tres—,
  y se cerró en el orden que corresponde: primero la regla que pregunta *por qué nada se puso rojo*,
  vista roja con los dos supervivientes nombrados, y el arreglo después. No se añadieron dos valores
  a un enumerado: el enumerado vive en un módulo y la obligación es transversal, así que cada módulo
  que bloquea expone su **puerto de consulta** y la composición los junta —sin `JOIN` entre esquemas
  y sin llamada HTTP—, con una segunda regla que lo exige.
  **Lo que cuelga, hecho**: `Contacto` (cuatro campos, **sin campo de notas**, y su ausencia es la
  decisión), `CuentaBancaria` con IBAN validado por **mod-97 y longitud por país** sobre los 37 de
  SEPA —batería generada, en las dos direcciones y con **un negativo por cada motivo distinto**:
  longitud, país inexistente y control malo—, `CondicionPago` con el tope de 60 días puesto en los
  **tres sitios** (dominio, borde y `CHECK` de la base) y sin calcular ningún vencimiento, y
  `LimiteCredito` con **su divisa explícita**, `numeric(18,4)` por R6, que **no se hereda en
  silencio** de `Empresa.DivisaBase`. Entra también el **régimen fiscal del §7.2** —alcance
  comprometido sin casa—, con el corte de `LimiteCredito`: tres de sus cuatro condiciones como
  columnas con `CHECK` y **ningún comportamiento**; el tipo de retención se queda fuera porque ya
  tiene dueño desde el 1.2.
  **Las tres notas abiertas, resueltas**: la correlación buscar/alta pasa a *Decisiones* con sus
  tres salidas costeadas —no la puede tomar el agente—; el art. 32, cerrado en código; y el régimen
  fiscal, con el corte que se sostiene y el trozo que no, dicho.
  **Catorce mutaciones con su línea base nombrada**, doce rojas y **dos verdes que valen más**: la 7
  destapó que la decisión de la divisa no la guardaba ningún test, y su regla —escrita ya— vive en el
  carril de integración, que esta máquina no ejecuta.
  **Y el ítem no terminó donde parecía**: contra PostgreSQL, las **siete** rutas que escriben un
  hijo contestaban `412`, y ninguna de las que rechazan antes de escribir fallaba. La causa era el
  modelo diciendo que las claves `Guid` se rellenan al insertar cuando las pone la fábrica del
  dominio, y con eso EF marcaba los tres hijos como modificación. **ADR-0032**, con su convención
  de cierre, su caso de un segundo y sin Docker, y los cuatro huecos de declaración que aparecieron
  al descubrir el universo de modelos en vez de enumerarlo.
- [x] **1.7 · La retirada y las dos conversiones** — criterio de aceptación: el **ADR-0023
  implementado entero** —retirada en los cuatro maestros de instalación, tolerancia de la conversión
  inversa, resolutor de conversiones encadenadas con **error con nombre**—, y **antes** de que
  Catálogo referencie `UnidadMedida`, que es el disparador que el propio ADR dejó escrito.
  Cerrado con el run **34177571670** sobre `dd89e9f`. Las tres decisiones del ADR, más las **tres**
  que dejaba abiertas y aquí se cierran con su motivo: cómo se ven las retiradas (parámetro del
  listado, por contraste con el ADR-0027), que `TipoCambio` y `ConversionUM` no tienen puerto de
  consumo sin que eso las haga vacuas, y que una fila retirada **sigue restringiendo** a su inversa.
  Ocho mutaciones en *Estado actual*, con la 1 y la 3 enteras; la 3 destapa que un 404 ahí deja la
  retirada sin marcha atrás.
- [x] **1.8 · Catálogo: artículo y categoría** — criterio de aceptación: alta de artículo con unidad
  e impuesto **validados por los puertos del 1.2** (no guardados a ciegas); `Categoria` jerárquica
  con **comprobación de ciclos**; listado paginado y filtrado; y `features/catalogo/`. Fuera:
  `CodigoBarras`, que es de la **fase 2** con su import.
  Cerrado con el run **34424407829** sobre `f90dbed`. Catálogo es el consumidor para el que se
  construyó la retirada del ADR-0023: las tres casillas de `EstadoDeMaestro` significan tres cosas
  distintas en un camino de negocio, y `SoloResuelveLoViejo` con sus **dos mitades**. Las nueve
  decisiones en *Decisiones tomadas → ítem 1.8*, con las dos que el enunciado pedía nombradas —el
  árbol como **lista de adyacencia** y la **cota** del ascenso— y su motivo. Ocho mutaciones en
  *Estado actual*, con la 1, la 2 y la 3 enteras; la 2 destapa una aserción por HTTP cuyo escenario
  no la ejercía, arreglada en `1eab397`.
- [x] **1.9 · Tarifas** — criterio de aceptación: `Tarifa` con vigencia y divisa y `LineaTarifa` por
  artículo o categoría con escalado por cantidad (§7.3); **precio o descuento excluyentes** en el
  objeto de valor; **solape de vigencias prohibido por restricción de exclusión**; sin tarifa
  aplicable, **error de negocio con nombre y nunca precio cero**; y la **precedencia completa**
  —artículo > categoría más cercana > … > raíz— probada con el caso que la distingue: **dos líneas
  de categoría a distinta profundidad**.
  Cerrado con el run **34599130092** sobre `eadb7b9`. «Más específica» no basta, porque la categoría es
  jerárquica y **«más cercana» y «más profunda» no son lo mismo**: lo que impide que una rama hermana
  más honda se cuele es el tipo —`LineaCandidata.Nivel` es el salto **desde el artículo**—, no una
  comprobación. Las nueve decisiones en *Decisiones tomadas → ítem 1.9*, con las dos que el enunciado
  dejaba abiertas ya contestadas: la **divisa cruzada se acepta** —respuesta contraria a la del
  `LimiteCredito` del 1.6, y con motivo— y la **frontera del tramo cae hacia arriba**, con el hueco
  cerrado por delante porque un hueco es peor que un solape. Ocho mutaciones en *Estado actual*, con
  la 1, la 5 y la 8 enteras; la 8 **no cambia ningún resultado** —solo el número de viajes—, así que
  quien la caza es una afirmación sobre el puerto y no sobre el precio. Carril nuevo,
  `ContratoDeTarifasTests`, porque la restricción de exclusión es DDL y `btree_gist` se comprueba
  **por el efecto y contra el compose**.
- [x] **1.10 · Los dos cruces mutuos** — criterio de aceptación: `ArticuloProveedor` (Catálogo →
  `Terceros.Contracts`) y `Tercero.TarifaAsignada` (Terceros → `Catalogo.Contracts`), cada uno por
  el `Contracts` de su dueño, resuelto en proceso y **declarado**. Es ítem propio porque la
  dependencia es **mutua** y eso se ve, no se reparte.
  Cerrado con el run **34833598837** sobre `958c339`. Los dos puertos preguntan por el **estado** y no por
  la existencia, con sus seis casillas probadas contra PostgreSQL —las de error de cada sentido
  incluidas—, y `Bloqueado` fuera del enumerado porque ningún productor lo producía: lo encontró la
  matriz nueva, que descubre todo puerto de estado y no solo los de `EstadoDeMaestro`. El art. 32,
  contestado con cuatro casos: el listado de proveedores esconde al bloqueado, el alta contesta lo
  mismo que a uno inventado y volver a añadirlo **no es un 409**. La divisa, en dos representaciones
  dentro del `Tercero`: no se comparan —probado por el efecto, con un canario en cada orden—, no se
  unifican ahora —la buena es el código ISO, con el disparador escrito— y no se convierte nada. Once
  decisiones en *Decisiones tomadas → ítem 1.10*; ocho mutaciones en *Estado actual*, con la 2, la 5
  y la 7 enteras: la 7b enseña que sin un registro el radio del fallo es el controlador entero, y la
  8 deja abierto que la clave ajena entre esquemas escrita a mano solo la ve un caso de Identidad.
- [x] **1.11 · Importación CSV** — criterio de aceptación: **unidad de aislamiento = la fila**, de
  modo que un fichero con filas malas importa las buenas; **idempotencia por fichero** —una
  importación es UNA operación, y el reenvío con la misma `Idempotency-Key` devuelve el resultado
  guardado sin reimportar—, con el **hash del contenido** guardado al lado, de manera que la misma
  clave con **otro fichero** sea un error explícito; e informe con **línea y motivo** de cada fila
  rechazada, que es lo único que permite corregir y reimportar solo esas.
  Cerrado con el run **34939033897** sobre `73d1db7`, que es también el que cierra la **fase 1**. La
  fila decide y el fichero escribe: las altas aceptadas se confirman en un solo `SaveChanges`, con el
  recibo, y si el proceso cae a mitad no queda nada. La misma clave con el mismo fichero devuelve el
  informe guardado sin volver a preguntar a la base —visto por el suceso que deja esa consulta, no por
  la respuesta—, y con otro fichero es un `409`. El informe va por (columna, motivo) con sus líneas de
  Excel y sin un solo valor de la entrada; los topes —2 MiB y 5 000 filas— se escribieron antes del
  *endpoint*; y el recibo de idempotencia caduca a las 24 horas, que cierra la nota del 0.9. Once
  decisiones en *Decisiones tomadas → ítem 1.11*, con el ADR-0034; ocho mutaciones en *Estado
  actual*, con la 4, la 5 y la 7 enteras, y la 8 desmintiendo un comentario de test que afirmaba un
  rojo que no se da.

### Addenda de la fase 1 — los tres ítems que abre la verificación del cierre (2026-09-16)

> **Estos tres NO son de la puerta de clarificación de la fase 1.** Aquellos once están cerrados y
> no se tocan. Los abrió el usuario al verificar el cierre contra la API, y el porqué de cada uno
> está en *Decisiones tomadas → addenda de la fase 1*. Se numeran 1.12–1.14 por el mismo motivo que
> el 0.14–0.16: son del cierre de la fase, y la fase 2 no se abre arrastrándolos.

- [x] **1.12 · El segundo arranque** — criterio de aceptación: el arnés **primero, y visto en rojo**
  —las migraciones aplicadas una a una sobre tablas con filas, y un segundo arranque del *compose*
  sobre una base con datos y la semilla retirada—; el rol de administración con los permisos de la
  versión desplegada, con **una** de las tres salidas elegida y su porqué escrito; la gravedad,
  exacta; y en el ADR, que el código que solo corre al arrancar se prueba desde fuera, o no se prueba.
  Cerrado con el run **35103339786** sobre `f3c749e`, el mismo para las tres addenda y el que certifica
  ahora la **fase 1**. El arnés entró primero y se vio rojo sobre el árbol de `7f676e2` —al rol del
  sistema con los 55 permisos de la fase 0 «le faltan 38 y le sobra 1»—, y lo cierra la salida **(c)**
  del ADR-0035: el migrador alinea el rol del sistema con el catálogo desplegado en cada despliegue, y
  `PUT` con otra lista es `409 permisos-de-rol-del-sistema`. La gravedad, exacta: una regresión de
  privilegios **silenciosa** y **recuperable a mano**, y por el otro lado un permiso retirado que se
  quedaba concedido. El segundo arranque es un paso del Humo, sobre el mismo volumen y con la semilla
  vacía, y las migraciones se aplican una a una sobre tablas con filas. El ADR deja escrito que el
  código que solo corre al arrancar se prueba desde fuera, o no se prueba. Decisiones 1 a 3 en
  *Decisiones tomadas → addenda de la fase 1*; la mutación 1 —el alineado sin confirmar, verde en el
  primer arranque y rojo en el segundo— y la 2 —la columna obligatoria, roja con filas y verde en
  vacío—, enteras en *Las ocho mutaciones de las addenda*. El primer run de la rama salió rojo por el
  propio arnés, y está contado en *Lo que la CI encontró y en local no se veía — addenda*.
- [x] **1.13 · Las diecisiete reglas, con su estado** — criterio de aceptación: una tabla en `docs/`
  con las diecisiete, una línea cada una —enunciado breve y estado: **viva** (dónde se hace cumplir),
  **aplazada a la fase N** (con el motivo) o **no aplica** (con el porqué)—; una regla de
  descubrimiento que la compare entera y **en los dos sentidos**; anotado de qué versión del plan
  maestro se copió y que se contrasta a mano en cada puerta de fase; y ningún enunciado inventado.
  Cerrado con el run **35103339786** sobre `f3c749e`. `docs/dominio/reglas-duras.md` copia las diecisiete
  del §6 del plan maestro, «Revisión de 2026-08-25 (tarde)», sin inventar un enunciado: R1, R7 y R13
  están en el plan maestro. **Viva** 7, **aplazada** 10 y **no aplica** 0, cada viva con dónde se hace
  cumplir y cada aplazada con su fase y su motivo. `LasDiecisieteReglasTests` la compara con el
  repositorio en los dos sentidos —con las etiquetas `rNN` de los ADR dentro—, y lo que ninguna regla
  puede comprobar, que el enunciado siga siendo el del plan maestro, está escrito como contraste a mano
  en cada puerta de fase, no disimulado. Las mutaciones 3, 4 y 5 —una fila menos, una de más y un sitio
  de cumplimiento borrado— salen rojas, con la 4 entera.
- [x] **1.14 · Las higienes del cierre** — criterio de aceptación: la línea de estado del README al
  día **y vigilada** contra este checklist; las variables del despliegue documentadas y comparadas;
  `EsDelSistema` anotado; la búsqueda de la raíz del repositorio en **un** sitio, con las copias
  borradas; el traspaso a la fase 2 con R9/R14 y R5 como **preguntas** de su puerta, con su coste y
  sin contestar; y la convención del recuento del frontal escrita donde se mide.
  Cerrado con el run **35103339786** sobre `f3c749e`. La línea de estado del README sale de este
  checklist y la vigila `ElEstadoDelReadmeEsElDelPlanTests` (mutación 6); las variables del
  despliegue están en `deploy/.env.example` —que ya existía— y `LasVariablesDelDespliegueTests` lo
  compara con el *compose*, la semilla y la CI; `EsDelSistema` dice lo que se cumple bajo (c); la raíz
  del repositorio se busca en `tests/Comun/RaizDelRepositorio.cs` y una copia nueva es roja
  (mutación 7); R9/R14 y R5 van al traspaso como preguntas de la puerta de la fase 2, con su coste y
  sin contestar; y el frontal se cuenta **sin la raíz**, escrito en la cabecera de
  `scripts/dependencias-por-conjuntos.py` —las 549 de la línea base son 548 más la raíz—.

## Notas / riesgos

- **TRASLADADA A *DECISIONES* (2026-09-07, ítem 1.6) · el conflicto no revela, pero DOS respuestas
  juntas sí.** El hecho sigue siendo el que se anotó en el 1.5 y no ha cambiado: la búsqueda no
  encuentra lo bloqueado, el alta contesta `409` igual esté la ficha activa o bloqueada, y quien
  tenga los dos permisos deduce lo que ninguna de las dos respuestas dice por separado. Deja de estar
  aquí porque **no es un riesgo que se descubra**: es un cruce conocido, medido y con las tres
  salidas posibles escritas —separar permisos, que la búsqueda mienta, o que el alta deje de
  distinguir— cada una con lo que cuesta. Ninguna la puede tomar el agente, así que la pregunta queda
  planteada entera en *Decisiones tomadas → ítem 1.6*, decisión **1**.

- **CERRADA (2026-09-07, ítem 1.6) · un tercero bloqueado no aparecía en NINGÚN listado — y un
  usuario bloqueado tampoco, desde el 1.4.** La nota se abrió en el 1.5 mirando a `Tercero` y **se
  quedaba corta**: el listado nació corto en el **1.4**, sin los usuarios bloqueados, que son personas
  físicas y el caso más nítido del artículo. Y el motivo no era un olvido, era **estructural** —el
  enumerado vivía en `Organizacion.Application` y el repositorio unía tres tablas de un solo esquema,
  así que por construcción no alcanzaba a Identidad ni a Terceros—. Cerrada con la regla que lo pone
  rojo escrita **antes** del arreglo (roja con los dos supervivientes) y con el patrón de puertos del
  1.2: cada módulo que bloquea contesta por lo suyo y el listado compone. **ADR-0031**, y el detalle
  en *Estado actual* → *Ítem 1.6*.

- **CERRADA (2026-09-07, ítem 1.6) · el régimen fiscal del §7.2 ya tiene ítem, y es el 1.6.** Era una
  pregunta —«si es del 1.6 hay que escribirlo allí; si es de facturación, de qué fase»— y se contesta
  por lo que era: **alcance comprometido sin casa**, no alcance nuevo. Su sitio es el tercero, y
  dejarlo para la fase 3 obligaría a migrar un maestro con datos dentro. Entra con el corte de
  `LimiteCredito` —lista cerrada de valores con su `CHECK` y ningún comportamiento— y el corte se
  sostiene en **tres condiciones de cuatro**: territorio, recargo de equivalencia, criterio de caja y
  el booleano de si se le retiene. **El tipo de retención se queda fuera**, y ese es el trozo donde el
  corte no aguantaba: no significa nada sin la regla que lo aplica, y ya tiene dueño desde el 1.2.
  Razonamiento entero en *Decisiones tomadas → ítem 1.6*, decisión **2**.

- **CERRADO (2026-09-10, en el ítem 1.8) · los tests del frontal soltaban 109 avisos de
  `act(...)`, en SIETE ficheros.** `An update to <X> inside a test was not wrapped in act(...)` sale
  **109** veces en una ejecución limpia. Medido nueve veces: 109 en ocho de ellas y **105** en una,
  o sea que la cifra es estable de sobra para servir de línea base pero **no es determinista** —y esa
  variación es, ella misma, la prueba de lo que la nota dice: hay actualizaciones de estado que caen
  fuera de la ventana que el test controla, y a veces llegan y a veces no.
  El comando, escrito para que no haya que fiarse de la cifra:
  `npm --prefix frontend run test 2>&1 | grep -c "was not wrapped in act"`, y el reparto por fichero
  atribuyendo cada aviso al último encabezado `stderr | <fichero>` **después de quitar los códigos
  ANSI** (sin quitarlos, el encabezado no casa y todo cae en un cajón). Reparto:
  `ElListadoDeAlmacenes.test.tsx` 24, `ElCambioDeIdioma.test.tsx` 17, `LasRutasProtegidas.test.tsx` 17,
  `ElCambioDeRuta.test.tsx` 14, `ElSelectorDeEmpresa.test.tsx` 14, `ElTestigoDeAcceso.test.tsx` 13 y
  `LaPantallaDeAcceso.test.tsx` 10. Componentes: `SelectorDeEmpresa` 25, `Guarda` 25, `Disposicion` 25,
  `RouterProvider` 24, `PaginaDeInicio` 9 y `PaginaNoEncontrada` 1.
  **Las dos cifras anteriores estaban mal y por la misma causa.** El 91 «en seis ficheros» del 1.4 se
  midió sobre un registro **truncado por la cola**: perdía `ElTestigoDeAcceso.test.tsx` entero (13) y
  cinco avisos de `ElCambioDeRuta` (91 + 13 + 5 = 109). El 92 del 0.15 viene del mismo vicio. La
  lección no es la cifra, es que **una cifra medida se publica con el comando que la mide**; está
  escrita como regla en `AGENTS.md`.
  Los componentes que los provocan son pocos y se repiten — o sea que **no** son siete problemas, es un puñado de
  efectos que se resuelven después de que la aserción ya haya mirado. **Por qué importa y no es
  ruido:** es la misma familia que el flake del 0.14 —`document.title` mirado antes de que el efecto
  lo pusiera, rojo en la CI sobre un commit que solo tocaba un `.md`—, y un aviso de `act()` dice
  literalmente que hay una actualización de estado fuera de la ventana que el test controla. Hoy los
  46 casos pasan; el día que el *runner* vaya cargado, alguno de estos es el candidato. **No entra en
  el criterio del 1.4** —el ítem no toca el frontal— y queda anotado con los ficheros medidos, no
  recordados. **Lo que lo cerraría:** envolver la interacción que dispara el efecto, no envolverlo
  todo: `waitFor` sobre lo que pone un efecto y nada más, que es la regla que dejó el 0.14.
  **Cerrado en el 1.8** (`9f8ff56`, 2026-09-10), y **no por donde decía «lo que lo cerraría»**: no
  eran efectos sin esperar, venían del **desmontaje**. El `afterEach` de `setupTests.ts` vaciaba el
  depósito de sesión antes de que Testing Library desmontara —los `afterEach` corren en orden inverso
  al de registro—, y ese aviso a los oyentes repintaba los componentes suscritos a la sesión con la
  pantalla todavía en pie. Con `cleanup()` al principio del `afterEach`, **cero** en la suite entera,
  y cero en cada batería desde entonces.
  **Repasada al cerrar la fase 1: cerrada, con una salvedad que no la reabre.** Bajo contención la
  base también da avisos (*Verificado — ítem 1.10*), así que un aviso solo es un hallazgo en una
  pasada sin competencia.

- **CERRADO (2026-09-04, en el ítem 1.4) · el censo de reglas cubría un carril de cuatro.** Anotado
  al cerrar el 1.3 como candidato, y autorizado por el usuario para este ítem. `LasReglasDeEsteCarrilTests`
  censaba `Arquitectura.Tests` y solo ese ensamblado desde el 0.12; los otros tres carriles con reglas
  tenían **287 casos** que se podían borrar dejando la suite verde, más rápida y con una frontera sin
  guardián. **Cerrado** con `tests/Comun/CensoDeReglas.cs` compartido por `Compile Include` y una lista
  escrita por ensamblado: **316 nombres** censados en los cuatro carriles (134 + 118 + 41 + 23,
  contados con grep sobre las cuatro listas), comparados enteros y en los
  dos sentidos. Comprobado por el efecto: borrar `El_barrido_ve_los_cuerpos_y_reconoce_un_testigo` deja
  el carril en 125 casos verdes y **solo** el censo se pone rojo, nombrando la regla. Y de paso salió
  un falso verde de la versión original —`orderby nombre, StringComparer.Ordinal` son dos claves de
  orden y el comparador ordinal no se aplicaba nunca— que era invisible con 23 nombres y deja de serlo
  con 133.

- **CERRADO (2026-09-04, en el ítem 1.4) · la batería local de `AGENTS.md` no ejecutaba el guion que
  decide el desenlace de la CI.** Anotado al cerrar el 1.3, cuando el run 33830689761 salió rojo con
  la batería entera en verde: `dotnet test` dice «ningún caso falla» y `scripts/ci/recuento-de-tests.sh`
  dice «han corrido exactamente los ensamblados que tenían que correr», y un cambio que altera **qué**
  corre es invisible para el primero por construcción. Se dejó como candidato «para el ítem que toque
  el carril», y este lo toca: el censo hace que `Bastion.Api.IntegrationTests.dll` empiece a correr en
  el carril rápido. **Cerrado** añadiendo los dos recuentos a la batería de `AGENTS.md`, con las
  listas del *workflow* literalmente y con los `--logger trx --results-directory` que hacen falta para
  poder ejecutarlos. Verificado en este mismo ítem antes de empujar: rojo con la lista vieja
  nombrando el ensamblado, verde con la nueva.


- **CERRADO (2026-09-03) · un test del frontal miraba `document.title` sin esperar al efecto que lo
  pone.** Saltó donde peor se interpreta: en la CI, sobre un commit que **solo tocaba un `.md`**
  —`ElCambioDeIdioma.test.tsx:41`, «expected `''` to be `'Almacenes · Bastion'`»—, después de pasar
  en todas las ejecuciones anteriores. No era una regresión: el `<h1>` y el `<title>` salen del
  mismo componente pero **por caminos distintos**, el primero en el pintado y el segundo en un
  `useEffect`, así que `findByRole` puede resolver **antes** de que el efecto haya corrido. Se
  reprodujo en local lanzando seis copias del fichero a la vez —falló una, con el mismo mensaje—,
  que es la contención que tiene el *runner* cuando va cargado. Arreglado esperando, como ya hacían
  los otros dos sitios que miran el título; el `lang` de la línea de al lado se queda **sin**
  esperar a propósito, porque lo pone `crearI18n` antes del `render`. **La regla que deja:** una
  aserción sobre algo que pone un efecto se espera; una sobre algo que pasó antes de montar, no.

- **CERRADO (2026-09-03, en el ítem 1.1) · El presupuesto de tamaño del frontal
  se queda corto — y además mide otra cosa.** Al razonarlo en la puerta de la fase 1 apareció lo que
  esta nota no veía: el `dist` está **partido en fragmentos y las rutas se cargan tarde**, así que
  `du -sk --exclude='*.map' dist` **suma también lo que el navegador NO descarga al arrancar**
  (554 kB de total contra ≈400 kB de arranque). El paso contradice su propio comentario, igual que
  lo contradecía cuando medía los `.map`. **Decidido: dos métricas y dos topes —arranque ≤ 450 kB,
  total ≤ 900 kB— con el cálculo escrito**, y el criterio no es nuevo: es el mismo con el que ya se
  excluyeron los mapas, aplicado donde dejó de aplicarse solo (ADR-0028).
  **Hecho:** `scripts/ci/presupuesto-del-frontal.sh`, con siete mutaciones, y la séptima enseñando
  que la métrica vieja **bajaba** (554 → 546 kB) mientras el arranque empeoraba (391 → 507 KiB).
  Texto original: La CI corta en
  **600 kB** (`du -sk --exclude='*.map' dist`). Tras el 0.14 son **554 kB**: la i18n costó 64, de
  490 a 554, medido a los dos lados. Quedan **46 kB** y la fase 1 trae Terceros y Catálogo enteros,
  con sus formularios y sus tablas. Hay que revisar el tope **con un número razonado** —qué tarda en
  cargar y con qué red— y no subirlo el día que salte, que es como un presupuesto deja de serlo.

- **CERRADO (2026-09-03, en el ítem 1.2; construido en el 1.5) · El día que un mensaje del servidor
  se le enseñe al usuario, la i18n vuelve a la mesa.** Este era el octavo tema que el usuario puso sobre
  la mesa al abrir la fase 1, y **se cayó de la tanda de preguntas al redactarla**: no se contestó
  porque no se preguntó. **No se hereda el default**; se pregunta antes de empezar el **1.5**, que
  es el primer ítem con una validación —un NIF con la letra mal— que apetece enseñar con el texto
  del servidor. Texto original: Hoy el frontal **no pinta nunca** `ProblemDetails.detail`: todo lo que lee una
  persona está escrito en el frontal, y por eso el 0.14 no tocó la API. Pero las validaciones de la
  fase 1 —un NIF con la letra mal, un código de artículo repetido— son justo las que apetece
  enseñar con el texto que manda el servidor. Ese día hay que decidir entre que la API traduzca por
  `Accept-Language` o que devuelva códigos y traduzca el frontal. **La respuesta por defecto es la
  segunda**, porque mantiene la API sin saber de presentación; pero se decide, no se hereda.
  **Cerrado en el 1.2** (2026-09-03), preguntado y no heredado: escribe el texto **el frontal**,
  mapeando el `type` estable del `ProblemDetails`, y la API no negocia idioma por `Accept-Language`
  (**ADR-0030**). **Construido en el 1.5**, con el mecanismo antes que su primer uso, y sostenido por
  el paso «Catálogo de errores» de la CI (`generar-errores.sh --comprobar`) y por
  `ElCambioDeIdioma.test.tsx`, que compara los `type` del artefacto contra los dos diccionarios.

- **CERRADO (2026-09-04, en el ítem 1.4) · lo bloqueado seguía sin camino de lectura, y por tanto
  sin camino de desbloqueo desde el frontal.** Decidido en la puerta del 0.11 y sin cambios: una fila bloqueada
  contesta 404 a su propio `GET` (R16, ADR-0016), así que no aparece en ningún listado y la interfaz
  no tiene desde dónde ofrecer el desbloqueo. **La respuesta NO es un `IgnoreQueryFilters` en el
  frontal ni ensanchar el ámbito existente**: los tres sitios que abren `ViendoLoBloqueado` siguen
  siendo los tres desbloqueos y se siguen comparando enteros. Lo que haría falta es un motivo nuevo
  —administración—, su permiso, y un ADR que argumente por qué el art. 32 admite esa visualización.
  Trabajo de dominio y de derecho, no de armazón.
  **Repasada al cerrar la fase 0: sigue abierta, y eso es la decisión.** La fase 0 no trae ese
  motivo; lo mirará la fase que lo traiga, con su ADR.
  **La trae la fase 1: es el ítem 1.4** (2026-09-03), con **dos correcciones de hecho** sobre lo que
  decía esta nota. Primera: **el motivo ya existe** —`MotivoParaVerLoBloqueado.AdministracionDelBloqueo`
  está construido y en uso—, así que el ítem no lo crea, crea el **camino de listado**. Segunda:
  **son cuatro sitios, no tres** —`DesbloquearAlmacen`, `DesbloquearEmpresa`, `AdministracionDeUsuarios`
  y `BloquearUbicacion`—, y Terceros será el quinto; como la lista se compara entera y en los dos
  sentidos, escribir «tres» ahí es escribir un rojo. Y una consecuencia que no se veía: las cuatro
  exenciones del ADR-0017 llevan escrita la condición de la que dependen —«que ninguna lectura de la
  API entregue una fila bloqueada… con su ETag»—, así que el 1.4 **caduca su primera mitad**. Por eso
  construye **un listado y no un `GET` individual**: el listado no emite ETag, o sea que no resucita
  la llave y las cuatro exenciones siguen en pie; reescribir la mitad caducada entra en el criterio
  del ítem.
  **Cerrado en el 1.4** (run 33906512809 sobre `73c2836`): `GET /api/v1/organizacion/bloqueados`,
  con permiso y motivo propios y el vencimiento de cada bloqueo en su fila, y desde el 1.6 recorriendo
  **todos** los módulos que bloquean. Lo sostienen
  `ElAccesoReservadoDelArticulo32Tests.Un_almacen_bloqueado_desaparece_de_los_caminos_ordinarios_y_aparece_en_este`
  y `.El_listado_de_lo_bloqueado_no_devuelve_ninguna_llave_de_concurrencia`, que es lo que deja en pie
  las exenciones del ADR-0017.
- **CERRADA (2026-09-14, en el 1.11) · `auditoria.claves_de_idempotencia` crecía sin límite, y era a propósito.**
  El 0.9 no trae política de retención, y no por olvido. Un recibo de idempotencia es lo que impide
  que un reintento duplique un alta: **borrarlo reabre exactamente la ventana que la tabla cierra**,
  así que una limpieza mal calibrada no deja la tabla más pequeña, deja el sistema sin la garantía.
  Lo que hace falta para decidirlo no está en el código sino en el uso: **cuánto tarda un cliente
  real en reintentar**. Hasta que haya ese dato, borrar sería adivinar. Dos cosas que ya se saben y
  ahorran trabajo el día que toque: la fila es pequeña y anónima —identidad, huella, código y bytes
  de la respuesta, nada del contenido de negocio (ADR-0014 §5)—, y la clave primaria empieza por
  `empresa_id`, así que un borrado por antigüedad dentro de una empresa recorre un rango contiguo
  del índice. El momento natural de decidirlo es el **0.13**, junto con quién aplica las migraciones
  en un despliegue; no antes.
  **Repasada al cerrar la fase 0: sigue abierta, y la mirada del 0.13 ya ha ocurrido.** La
  segunda mitad de la cita —quién aplica las migraciones— está resuelta (**ADR-0021**), así
  que el **mecanismo ya existe**: una limpieza sería una migración más, aplicada por el mismo
  paso del despliegue. Lo que sigue faltando no es mecanismo sino el dato —cuánto tarda un
  cliente real en reintentar—, y ese no está en el código. Sin él, borrar es adivinar; y
  adivinar mal no encoge la tabla, quita la garantía.
  **Cerrada en el 1.11** (`5f36468`, **ADR-0034 §4**), y **sin el dato** que la nota pedía, por un
  motivo que ella no podía prever: la fila ya no es anónima. El recibo de un `201` guarda la
  respuesta, y para `POST /api/v1/terceros/terceros` la respuesta es la ficha de un tercero —nombre,
  NIF y domicilio de personas identificables—. Un dato personal sin plazo no puede esperar a una
  medición de uso. **Veinticuatro horas** en la propia fila —`caduca_en`, calculado al reclamar el
  recibo, así que un cambio de plazo no toca lo ya prometido— y **una purga cada hora** en Auditoría,
  dueña de la tabla (`PurgadorDeRecibos`, suceso `8600`, sin datos). Lo que cuesta está en el
  contrato: pasado el plazo, la misma clave es una clave nueva. Lo sostienen
  `ElReciboCaducaYSeBorraTests` y la **mutación 8** del 1.11, y el humo local lo vio en la base del
  compose: `caduca_en` no nulable, con su índice y su migración.
