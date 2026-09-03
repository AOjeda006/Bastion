---
tipo: referencia
stack: [csharp, dotnet]
aplica_a: [arquitectura, clean-architecture, ddd]
tags: [adr, capas, contracts, building-blocks, duplicacion, monolito-modular]
revisado: 2026-09-03
---

# ADR-0029: `Contracts` sí puede ver el bloque común — la regla no era la que parecía

- **Estado:** aceptado
- **Fecha:** 2026-09-03
- **Relacionado:** corrige una creencia sobre el §4 y sobre `LasCapasVanHaciaDentroTests`. Se
  implementa en el **ítem 1.3**.

## Contexto

Los tipos de paginación —`Paginacion`, `PaginaDe<T>`, `ConsultaPaginada` y `Paginador`— están
**duplicados** en Identidad y en Organización. La fase 1 añade dos módulos, o sea que serían
**dieciséis ficheros** —cuatro tipos por cuatro módulos— justo cuando hay que ampliar ese mismo
contrato con el vocabulario de filtrado.

La pregunta se planteó así: *«¿seguimos copiando, o tocamos el §4 para que `Contracts` pueda
referenciar un común?»*. Y se planteó sobre **una premisa falsa**. Este ADR existe sobre todo para
dejar escrito eso, porque es la corrección de una creencia y no solo una decisión: la creencia era
que había una regla que prohibía la referencia, y no la hay.

## Los cuatro hechos, comprobados en los ficheros

**Uno. `Contracts` ya referencia el bloque común, y solo en un módulo.**

```
Bastion.Organizacion.Contracts.csproj  ->  Bastion.BuildingBlocks.Domain.csproj
Bastion.Identidad.Contracts.csproj     ->  (ninguna)
Bastion.Auditoria.Contracts.csproj     ->  (ninguna)
```

O sea que hoy **no hay regla**: hay una **incoherencia entre dos módulos**, con la puerta abierta y
uno solo entrando por ella. No se puede «no abrir» algo que está abierto; lo que se puede es decidir
si se cierra o se usa igual en todas partes.

**Dos. La regla de capas no dice lo que la premisa suponía.** `LasCapasVanHaciaDentroTests` prohíbe
que `Contracts` vea `Domain`, `Application`, `Infrastructure` y `Endpoints` **de su propio módulo**, y
el motivo está escrito ahí mismo:

> «si el contrato arrastrase el `Domain`, cualquier módulo que lo referenciara —que es lo que la
> regla 1 le PERMITE hacer— acabaría viendo el dominio ajeno por transitividad, sin escribir ni una
> línea prohibida».

El argumento es de **transitividad hacia un interior ajeno**. El bloque común **no es interior de
nadie**: todos los módulos pueden verlo por diseño, así que arrastrarlo no le enseña a nadie nada que
no pudiera ver ya. La condición que justifica la regla no se cumple en este caso, y una regla
aplicada donde su motivo no existe deja de ser una regla y pasa a ser una costumbre.

**Tres. Dos de los cuatro tipos ni siquiera están en `Contracts`.** `ConsultaPaginada` vive en
`Endpoints` y `Paginador` en `Infrastructure`, y las dos capas **ya** referencian
`BuildingBlocks.Infrastructure`. Para esos dos no había discusión que tener.

**Cuatro. Son duplicados literales.** `diff` entre los dos `Paginacion.cs` devuelve **una sola
línea**, la del `namespace`; los dos `PaginaDe.cs`, lo mismo.

## Decisión

Se consolidan.

- **`Bastion.BuildingBlocks.Contracts`**, nuevo y mínimo, con `Paginacion` y `PaginaDe<T>`. Es su
  sitio semántico: son tipos **de contrato**, no de dominio — por eso no van al `BuildingBlocks.Domain`
  que Organización ya referencia.
- **`ConsultaPaginada` y `Paginador`** se van a los comunes que sus capas ya referencian.
- **Identidad y Organización pasan a usarlos y sus copias se borran.** Que digan lo mismo **por
  construcción**, no por vigilancia.

## Alternativas descartadas

**Seguir copiando, con un barrido que compare las copias enteras.** Era la opción que parecía barata
mientras la premisa se sostenía. Con los cuatro hechos delante deja de serlo: sería institucionalizar
dieciséis ficheros idénticos más un barrido que los vigile para siempre, contra un principio que la
biblioteca declara sin matices. **Un barrido que compara copias no es una defensa: es la cuota anual
de una deuda que no hacía falta contraer.**

**Generar las copias.** Añade un generador y un paso de build para producir ficheros que no tienen
por qué existir.

**Cerrar la puerta**, quitándole a `Organizacion.Contracts` su referencia a `BuildingBlocks.Domain`.
Es la lectura coherente de la premisa falsa, y lo que costaría es empujar los tipos comunes de vuelta
a cada módulo — más duplicación, no menos, y sin ganar la transitividad que la regla protege.

## Consecuencias

- **`Inventario.ComunesConTipos` se pone rojo**, y está previsto. Declara tres ensamblados comunes con
  el comentario «son tres y no van a crecer con las fases». El cuarto obliga a actualizar la línea —el
  mecanismo funcionando como se diseñó— y a corregir el comentario, que resultó ser **una predicción y
  no una regla**.
- El vocabulario de **filtrado** que la fase 1 estrena nace en **un** sitio, que era la mitad del
  motivo para hacerlo ahora y no después.
- La regla de capas **no cambia**: sigue prohibiendo que `Contracts` vea el interior de su módulo.
  Lo que cambia es que deja de leerse como si prohibiera también el bloque común.
- Un módulo nuevo hereda los tipos sin copiar nada. Terceros y Catálogo nacen ya así.
