---
tipo: referencia
stack: [csharp, dotnet, aspnetcore, postgresql]
aplica_a: [seguridad, rgpd, api-rest, ddd]
tags: [adr, r16, lopdgdd, art-32, bloqueo, datos-personales, if-match, etag]
revisado: 2026-09-04
---

# ADR-0027: Lo bloqueado se lee por un camino nominativo — y el alta no dice si va por ahí

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** consecuencia de **ADR-0016** (el bloqueo es uno y tapa a las tres) y de
  **ADR-0017** (el desbloqueo no puede pedir una llave que el bloqueo esconde), al que **caduca la
  mitad de sus cláusulas de dependencia**. Aplica `herramientas/proteccion-datos.md`. Cierra una nota
  abierta desde el 0.11. Se implementa en el **ítem 1.4**.

## Contexto

La R16 y el art. 32 de la LOPDGDD dicen lo mismo: bloquear es impedir el tratamiento **incluida la
visualización**, y el filtro va **en el repositorio**, no en la interfaz. El proyecto lo cumple desde
el 0.9: una fila bloqueada contesta `404` a su propio `GET`, no sale en ningún listado y solo se ve
desde los **cuatro** sitios que abren `ViendoLoBloqueado(...)`, declarados y comparados enteros en
`s_aperturasDeBloqueoPermitidas`.

Con un puñado de empresas eso es tolerable: se bloquea poco y se desbloquea a mano. **Con miles de
terceros deja de serlo.** Una fila bloqueada es invisible para la interfaz, así que no hay desde
dónde ofrecer el desbloqueo, y un bloqueo por error se convierte en un dato inalcanzable.

Y aparece un segundo problema, más agudo, que no es de comodidad: **el alta**. La identidad de un
tercero es única por (empresa, NIF) (§7.2), así que dar de alta un NIF que ya existe **bloqueado**
choca contra un índice único sobre una fila que quien da el alta **no puede ver**. El mensaje
honesto —«ya existe»— señalaría a nada; y el mensaje explícito —«está bloqueado»— **publicaría la
existencia de un dato del art. 32 a alguien sin derecho a saberlo**.

## Decisión

### 1. Hay un camino de lectura de lo bloqueado, y es un **listado**

Una consulta que enseñe lo bloqueado, con su **permiso propio**, abriendo el ámbito con **su
propio motivo** (`MotivoParaVerLoBloqueado.AccesoReservadoDelArticulo32` — ver la *Corrección del
2026-09-04*, que sustituye a lo que aquí decía) y quedando **anotada en el registro**, que es lo que
ese ámbito ya hace. Es lo que `proteccion-datos.md` exige para el art. 32:
«una vía de acceso **separada, nominativa y trazada**, no el rol de administrador de siempre».

Ese sitio es el **quinto** de `s_aperturasDeBloqueoPermitidas`, y la lista se compara entera y en los
dos sentidos, así que entra declarándose o no entra.

### 2. Es un listado y **no** un `GET` individual, y el motivo está escrito en el propio ADR-0017

Las cuatro exenciones de `If-Match` de los desbloqueos llevan dentro **la condición de la que
dependen**, escrita a propósito para que caduque en voz alta:

> «DEPENDE DE que el filtro `Bloqueo` siga tapando la empresa en **TODA lectura que llegue por la
> API**: el día que un endpoint abra `ViendoLoBloqueado(...)` y devuelva una empresa bloqueada **con
> su ETag**, la llave vuelve a existir, esta exención caduca y hay que volver a exigir `If-Match`
> aquí.»

La cláusula tiene **dos mitades**. Un listado rompe la primera —ya no es «toda lectura»— pero **no
emite `ETag`**, que es lo que resucita la llave. Un `GET` individual rompe las dos y obliga a
devolver el `If-Match` a los cuatro desbloqueos.

**Se elige el listado**, y no por evitar trabajo: es que **basta**. Del listado sale el
identificador, y el desbloqueo no pide etiqueta. La ficha individual de lo bloqueado no hace falta
para levantar un bloqueo, que es el caso de uso que existe.

**Y la mitad caducada de las cuatro cláusulas se reescribe.** Una condición que ha dejado de ser
cierta y sigue escrita es peor que no haberla escrito: la exención **parece** razonada, y el
siguiente que la lea creerá que sigue apoyada donde ya no se apoya.

### 3. El alta y la administración son **dos caminos distintos**, y el alta no revela por cuál va

- **El alta** devuelve un conflicto que **no confirma ni desmiente** que exista una fila: «no se
  puede dar de alta con ese identificador; consulta con administración». Sin decir que está
  bloqueado, sin decir que existe, sin distinguirlo de otras causas.
- **La administración**, con su permiso y por el camino de la decisión 1, sí la encuentra y puede
  levantarla.

Quien da un alta normalmente **no** tendrá el permiso de ver lo bloqueado. Decirle «está bloqueado»
es un tratamiento de datos del art. 32 hacia alguien sin derecho a él — pequeño, cotidiano, y
exactamente el que nadie ve. Es la misma forma que el proyecto ya usa en dos sitios: el `404` que no
distingue una fila ajena de una que no existe (ADR-0011) y la respuesta única del acceso
(ADR-0008).

## Alternativas descartadas

**`IgnoreQueryFilters` en el camino de lectura, o ensanchar el ámbito existente.** Descartado por
escrito desde el 0.11 y sin cambios: convertiría cuatro aperturas declaradas en una puerta, y el
filtro dejaría de ser del repositorio para pasar a ser de quien se acuerde.

**No hacer nada y devolver un error genérico en el alta.** Es defendible en privacidad —de hecho es
la mitad 3 de esta decisión— pero deja el sistema **sin forma de deshacer un bloqueo hecho por
error**, y eso también incumple: el art. 32 obliga a poder rectificar, no solo a tapar.

**Resolverlo solo para el caso del alta, sin pantalla de administración.** Tapa el síntoma agudo y
deja el crónico.

## Consecuencias

- Aparece un permiso nuevo y un rol que lo tiene; el catálogo de permisos y su barrido lo recogen
  solos.
- `s_aperturasDeBloqueoPermitidas` pasa a **cinco** entradas.
- Las cuatro cláusulas «DEPENDE DE» del ADR-0017 se reescriben: la protección de `If-Match` sigue
  fuera **porque no hay `ETag`**, y ya no porque «no haya ninguna lectura».
- **Precio aceptado:** desde el listado se puede desbloquear, pero **no abrir la ficha** de lo
  bloqueado. Si algún día hiciera falta, no es un cambio de pantalla: es volver a exigir `If-Match`
  en cuatro acciones y reescribir el ADR-0017. Queda anotado en *Notas / riesgos*.
- **Lo que este ADR NO resuelve, y hay que decir:** el art. 32 exige que el bloqueo tenga **fecha de
  vencimiento y proceso de destrucción**, y eso no existe. No entra aquí porque el plazo es materia
  de retención y no se decide leyendo código. Queda como riesgo abierto con su fecha, no en
  silencio.

## Corrección del 2026-09-04 (al implementarlo, ítem 1.4)

Dos cosas que este ADR daba por decididas cambiaron al montarlo. Se escriben aquí en vez de
reescribir el texto de arriba: lo que se decidió el día 3 con la información del día 3 sigue siendo
lo que se decidió, y lo que hace falta saber es **qué lo sustituye y por qué**.

**1. El listado NO reutiliza el motivo que ya existía: estrena el segundo valor del enum.** La
decisión 1 decía «el motivo que ya existe (`AdministracionDelBloqueo`)». Al escribirlo se vio que
son **dos cosas distintas y la traza tiene que distinguirlas**: `AdministracionDelBloqueo` es el
sistema operando sobre el bloqueo —bloquear, desbloquear, comprobar—, y esto es **una persona
mirando datos personales reservados por el art. 32**. Meterlas bajo la misma etiqueta hace ilegible
justo el registro que el art. 32 obliga a llevar: el día que haya que responder «quién ha visto
esto», la traza diría «administración del bloqueo» para las dos. Y era además la deuda que el propio
enum tenía escrita: su comentario prometía un segundo valor «cuando exista un camino de lectura»,
que es este ítem. `MotivoParaVerLoBloqueado` pasa a ser una lista cerrada de **dos**, comparada
entera y en los dos sentidos por su propia regla.

**2. El «lo que este ADR NO resuelve» del final queda resuelto, y en el mismo ítem.** Decía que el
art. 32 exige fecha de vencimiento y que eso no existía. Existe desde el 1.4:
`PoliticaDeRetencion` (en `BuildingBlocks.Domain`) responde **cuándo vence un bloqueo**, el plazo
cuelga **del motivo** —`SupresionSolicitada` vence, `CeseDeUso` no vence nunca, porque un almacén
retirado se conserva por razón contable y sus datos no son de nadie—, el plazo por omisión son
**seis años** (art. 30 del Código de Comercio, el suelo más largo de los que le aplican a una pyme) y
es configurable por instalación. La fecha de vencimiento **sale en el listado**: sin ella, la lectura
del art. 32 enseñaría una conservación acotada como si fuera indefinida, que es la infracción por el
otro lado. Lo que **sigue sin existir** es el *proceso de destrucción* al vencer: hoy el vencimiento
se ve, no se ejecuta. Eso continúa como riesgo abierto en `docs/PLAN.md`, con su fecha.

Lo que **no** cambió: sigue siendo un listado y no un `GET`, sigue sin emitir `ETag`, y las cuatro
exenciones de `If-Match` del ADR-0017 siguen en pie por la segunda mitad de su cláusula. Esa mitad
ya no es una nota de confianza: la sostienen dos reglas que se ponen rojas si un DTO de lectura
lleva un testigo de versión o si la respuesta del listado emite la cabecera.
