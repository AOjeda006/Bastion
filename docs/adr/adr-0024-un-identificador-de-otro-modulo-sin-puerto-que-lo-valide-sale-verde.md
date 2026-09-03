---
tipo: referencia
stack: [csharp, dotnet]
aplica_a: [arquitectura, ddd, testing]
tags: [adr, fronteras, monolito-modular, netarchtest, reflexion, vacuidad, r14]
revisado: 2026-09-03
---

# ADR-0024: Un identificador de otro módulo sin puerto que lo valide sale verde en todas partes

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** cierra el hueco que dejan las quince reglas del **ADR-0020** y el §4 del plan
  maestro. Se apoya en el **ADR-0023** (los maestros de instalación) y en el patrón de dos fuentes
  que el ítem 0.16 estrenó con `funcionalidades.ts`. Se implementa en el **ítem 1.2**.

## Contexto

Catálogo necesita, el primer día, apuntar a cosas que son de Organización: la **unidad base** del
artículo, su **impuesto por defecto** y —vía tarifa— una **divisa**. Hay cuatro maneras de hacerlo, y
esta es la cuenta exacta de cuáles avisan:

| Camino | Qué lo rechaza | ¿Avisa? |
|---|---|---|
| Clave foránea entre esquemas | el SQL, y antes la regla 3 del §4 | **sí** |
| Referenciar `Organizacion.Domain` | el compilador, y `LasCapasVanHaciaDentroTests` | **sí** |
| Referenciar `Organizacion.Contracts` sin declarar el cruce | `LasFronterasEntreModulosTests` | **sí** |
| **Guardar `unidad_id` sin validarlo contra nadie** | **nada** | **no** |

El cuarto es una columna `uuid` con un valor dentro. Compila, migra, pasa los tests de arquitectura,
pasa los de integración y sirve peticiones. Lo único que no hace es garantizar que ese identificador
corresponda a una unidad que existe, que está activa y que no está retirada — y el día que no lo
sea, el fallo no aparece en Catálogo: aparece en la factura que la usa, tres fases después.

**El problema no es escribir la validación una vez.** Es que nada obliga a escribirla la **próxima**
vez. Un agregado nuevo con un identificador ajeno hereda el verde entero.

## Decisión

Una regla nueva del carril de arquitectura: **todo identificador de otro módulo que viva en un
agregado de dominio obliga a que exista un cruce declarado hacia el `Contracts` de ese módulo**.

Y su mitad importante: la regla se alimenta de **dos fuentes independientes, comparadas enteras y en
los dos sentidos**.

1. **Declarada a mano** — la lista de propiedades que son identificadores de otro módulo, con su
   dueño. Entra por aquí lo que el nombre no delata.
2. **Descubierta por reflexión** sobre el dominio compilado — todo `Guid` de un agregado cuyo nombre
   case con el de un agregado de otro módulo. Entra por aquí lo que nadie declare.

### Por qué dos fuentes y no una

Porque cada una tiene un agujero, y son agujeros distintos.

**La heurística de nombres sola falla en el primer caso real.** El §7.3 le da al artículo una
*unidad base*, y esa propiedad se llamará **`UnidadBaseId`**, no `UnidadMedidaId`. No casa con
`UnidadMedida`, la regla calla, y el agujero queda abierto **exactamente donde se abrió para
cerrarlo**. Es la misma familia de fallo que el ADR-0020 describe: una regla cuyo patrón no casa con
nada pasa, y pasa en verde.

**La lista declarada sola tiene el agujero simétrico:** un `DivisaId` que nadie apunte no existe para
la regla. Y es el caso que más va a ocurrir, porque declarar algo a mano depende de que quien
escriba el agregado se acuerde — que es justo la disciplina que el §4 dice no querer.

Comparadas enteras y en los dos sentidos, cada una tapa el fallo de la otra: lo que el nombre no
delata entra por la lista, lo que nadie declara entra por la reflexión, y una entrada que sobra en
cualquiera de las dos también es roja, porque una declaración que ya no corresponde a nada es un
permiso concedido sobre algo que cambió.

### Actualización (2026-09-03, al implementarlo en el ítem 1.2): no es una igualdad

Lo de arriba dice «**comparadas enteras y en los dos sentidos**», que es la forma que usan los otros
seis barridos del proyecto. **Para esta regla no vale, y aplicada así sale roja el primer día.**

El motivo es que aquí las dos fuentes **no describen lo mismo**. En un barrido corriente —las rutas
del frontal, las reglas de este carril— las dos listas son dos vistas del mismo conjunto, así que la
igualdad es la afirmación correcta. Aquí no: el descubrimiento por reflexión **infradetecta por
diseño**, porque solo encuentra el identificador cuando el nombre de la propiedad casa con el del
módulo dueño. Y ya hay un caso en el repositorio: `TokenDeRefresco.EmpresaActivaId` apunta a
Organización desde el **0.5** y no casa. Exigir la igualdad haría roja una lista que está bien.

Lo correcto es **contención con simetría por el otro lado**, y son **cinco** afirmaciones. La quinta
no estaba en este ADR y es la que decide si la regla protege o solo describe:

1. Todo lo **descubierto** está en la lista, con su puerto.
2. Toda **declaración** sigue correspondiendo a una propiedad del dominio.
3. Las **dos fuentes** encuentran algo — la afirmación de conjunto no vacío del ADR-0020.
4. Cada módulo de la lista tiene su **cruce declarado** y su **puerto**, y el puerto existe, es
   público y es del dueño.
5. **Ningún** `Guid …Id` del dominio se queda sin clasificar: o casa por nombre, o está declarado.

Sin la quinta, un identificador ajeno con **nombre que no casa y sin declarar** —`DivisaPreferidaId`,
`UnidadBaseId`— se queda **verde**, que es exactamente la cuarta vía que este ADR abrió para cerrar.
Comprobado por mutación: cae en un test y en uno solo. La tabla de las ocho mutaciones está en
`docs/PLAN.md` → *Estado actual* → ítem 1.2.

### La mutación que la valida

Añadir un `DivisaId` a un agregado que no tenga puerto, ejecutar, ver el rojo, revertir. Sin ese
rojo la regla no se acepta: es la doctrina del **ADR-0020**, y aquí importa más que en ninguna otra
porque la regla existe precisamente para cazar lo que sale verde solo.

### Los puertos que la fase 1 necesita

En `Bastion.Organizacion.Contracts`, con lo mínimo que cada consumidor necesita y no más:
`IConsultaDeImpuestos`, `IConsultaDeUnidadesDeMedida` e `IConsultaDeDivisas`. Lecturas, por el
contrato del dueño, resueltas **en proceso** — ni un `JOIN` entre esquemas ni una llamada HTTP (§4).

## Alternativas descartadas

**Declarar la línea de `s_crucesDeclarados` antes que el código**, para que el rojo llegue el día que
Catálogo compile sin usar el puerto. Suena bien y **no se puede pagar**: la comparación de cruces es
entera y en los dos sentidos, así que una línea declarada sin código que la ejerza deja el carril
**en rojo desde el momento en que se escribe** hasta que aparezca el consumidor. Eso choca de frente
con «cada commit deja el árbol coherente y el build/tests en verde» (`AGENTS.md`), que no es un
adorno: es lo que hace que cada commit sea un punto de retorno. El puerto, su primer consumidor y su
línea de cruce van en el **mismo commit**.

**Solo un test funcional** —«dar de alta un artículo con una unidad inexistente devuelve error de
negocio con nombre»—. Se hace igualmente, pero no basta: comprueba **un** caso de uso, no la
ausencia del hueco. El siguiente agregado no lo tiene y nadie se entera.

**Una clave foránea entre esquemas**, que sería lo natural en un monolito no modular. La prohíbe la
regla 3 del §4, y con motivo: es la dependencia que impide separar módulos después.

## Consecuencias

- Un agregado nuevo con un identificador ajeno **no compila en verde** hasta que su módulo declare el
  cruce y use el puerto. Es un rojo el día que se escribe, que es cuando es barato.
- El coste recurrente es una línea en la lista declarada por cada identificador que el nombre no
  delate. A cambio, el modo de fallo que quedaba sin vigilar deja de existir.
- La regla es de **dominio compilado**, así que vive en `tests/Arquitectura.Tests/` y se ejecuta en
  el carril rápido, sin Docker.
- Cuando llegue la fase 2 y `Inventario` apunte a artículos, la regla ya está: no hay que acordarse.
