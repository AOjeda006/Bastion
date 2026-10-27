---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [exportacion, catalogo, organizacion, inventario, contratos]
tags: [adr, exportacion, maestros, puertos, lotes, contracts, bloqueo, retirada, adr-0024, adr-0055, adr-0073]
revisado: 2026-10-27
---

# ADR-0074: Un maestro de otro módulo se nombra por lotes desde su dueño, con lo que enseña su ficha

- **Estado:** aceptado
- **Fecha:** 2026-10-27
- **Sale del ítem 3.2**, la exportación de los listados. La respuesta 1 de su puerta (PLAN, *La
  puerta del 3.2, contestada*) fija lo que se escribe de un maestro de otro módulo: dos columnas,
  código y nombre. «Los resuelve un método nuevo por lotes en el `Contracts` de su dueño, una vez
  por página interna. Sin el permiso de ver ese maestro va su identificador, como en la pantalla,
  que lo pide a su dueño solo con permiso (ADR-0055 §12).»
- **Enmienda el §12 del [ADR-0055](adr-0055-el-recuento-congela-el-teorico-al-confirmar-y-su-ajuste-es-el-que-mueve.md)**,
  en su primera viñeta: el puerto de Catálogo no publicaba ni el código ni la descripción del
  artículo, y desde el 3.2 los publica, por lotes y solo para enseñarlos. La pantalla sigue
  nombrando por la API, con caché, como decía el resto del punto.
- **Se apoya en** el [ADR-0073](adr-0073-la-exportacion-recorre-el-listado-en-una-foto-y-escribe-el-dialecto-de-la-importacion.md),
  que recorre el listado por páginas internas de 200 y deja fuera de la foto lo que se lee de otro
  módulo, y en el §4 del plan maestro: la lectura entre módulos es una interfaz del `Contracts` del
  dueño, resuelta en proceso.

## Contexto

Varios de los trece listados apuntan a maestros de otro módulo por su identificador: el artículo, el
almacén, la ubicación, la unidad, el impuesto y la divisa. La pantalla los nombra preguntando a la
API de su dueño, uno a uno y con caché (ADR-0055 §12). La exportación no puede hacer eso:

- **Corre en el servidor, dentro de una petición.** Llamarse a sí misma por HTTP sería cruzar el
  §4 por fuera, y lo que el §4 da para leer de otro módulo es su `Contracts`.
- **Uno a uno serían 600 consultas por página interna**: 200 filas por hasta tres maestros.
- **Los puertos que había contestan otra pregunta.** `EstadoDeAsync` dice si un maestro vale para
  lo nuevo o solo resuelve lo viejo, y para eso abre el ámbito del art. 32 y ve lo bloqueado. Y el
  de artículos no publicaba ni el código ni la descripción, a propósito: para decidir no hacen
  falta.

## Decisión

1. **Un método por lotes en cada uno de los seis puertos.** `NombresDeAsync`, con los
   identificadores de una página y la cancelación, devuelve un diccionario con el código y el
   nombre de cada uno que vuelve. Está en `IConsultaDeArticulos`, `IConsultaDeAlmacenes`,
   `IConsultaDeUbicaciones`, `IConsultaDeUnidadesDeMedida`, `IConsultaDeImpuestos` e
   `IConsultaDeDivisas`. Es una consulta por llamada, y la exportación hace una por maestro y página
   interna, con los identificadores distintos de esa página.
2. **Nombra lo que enseña la ficha del maestro**, la de su `GET …/{id}`: el mismo contexto, con sus
   filtros globales puestos, y sin abrir el ámbito del art. 32. Lo que la ficha no enseña, el puerto
   no lo nombra, y la exportación escribe su identificador. Así un fichero no enseña nada que la
   pantalla no enseñe.

   | Maestro | Vuelve | No vuelve |
   |---|---|---|
   | Almacén | el de la empresa | el bloqueado, el de otra empresa |
   | Ubicación | la de la empresa, también la de un almacén bloqueado | la bloqueada, la de otra empresa |
   | Artículo | el de la empresa | el de otra empresa |
   | Unidad, divisa | todas, también las retiradas | — |
   | Impuesto | todos los tramos, rijan o no | — |

   **La ubicación de un almacén bloqueado vuelve** porque su ficha la enseña: el filtro de R16 de
   las ubicaciones mira su propio bloqueo, y la ficha no une el almacén. `EstadoDeAsync` contesta lo
   contrario, el peor de los dos, y está bien que lo haga: aquel decide si un movimiento puede
   apuntar a ese hueco, y este solo lo nombra. **Lo retirado vuelve** porque la retirada no es un
   filtro: sigue resolviendo lo viejo, y su ficha lo enseña.
3. **El permiso lo mira quien pregunta, antes de preguntar.** El puerto no sabe quién pregunta, y no
   tiene por qué: es una lectura entre módulos. La exportación comprueba el permiso de ver cada
   maestro, el mismo que pide su ficha, y sin él no pregunta y escribe el identificador, como la
   pantalla, que sin ese permiso no se lo pide a su dueño.
4. **El nombre es un registro común**, `NombreDeMaestro(Codigo, Nombre)`, en
   `BuildingBlocks.Contracts`, como `DireccionDto`. El nombre admite nulo porque la descripción de
   una ubicación es opcional. Con uno por dueño serían dos copias de la misma forma, y Catálogo no
   puede tomar prestado el de Organización: un módulo solo referencia el `Contracts` de otro, y los
   dos `Contracts` de la pareja no pueden referenciarse entre sí. Así que `Catalogo.Contracts`
   estrena su primera arista, hacia un bloque que no referencia nada, y el inventario de
   arquitectura la escribe con su motivo.
5. **Un lote vacío también va a la base.** El atajo sería una rama más que solo se ejerce si un
   consumidor pregunta sin nada que preguntar, y la tanda del ítem anterior encontró dos guardas
   así, muertas. El consumidor que no tiene identificadores no pregunta.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| Qué nombran el almacén y la ubicación: lo bloqueado, lo de otra empresa, lo inventado y la ubicación de un almacén bloqueado | `LosNombresPorLotesTests` | integración |
| Que la unidad, el tramo y la divisa se nombran sin empresa, y lo retirado también | `LosNombresPorLotesTests` | integración |
| Que el artículo se nombra con su descripción, y el de otra empresa no vuelve | `ElPuertoDelArticuloContraLaBaseTests` | integración |
| La arista nueva y la descripción de los seis puertos | `Inventario` de `Arquitectura.Tests` | rápido |

Cada ausencia tiene su contraria. El almacén y la ubicación bloqueados se nombran antes de
bloquearlos, así que lo que los saca es el bloqueo. El de otra empresa se nombra en la suya. Y las
afirmaciones comparan el diccionario entero: un puerto que devolviera todo, o nada, no pasa ninguna.
Que la exportación mire el permiso y escriba el identificador lo prueba cada exportación con sus
casos.

## Alternativas descartadas

- **Un `JOIN` contra el esquema del dueño**, que daría la página nombrada en una sola sentencia.
  Ninguna consulta cruza esquemas: lo prohíbe el §4.
- **Un puerto nuevo, solo de nombres**, al lado de cada uno de los seis. Serían seis interfaces más
  para una pregunta sobre el mismo maestro, y el puerto de cada maestro ya es el sitio donde el
  dueño dice qué enseña de él.
- **Nombrar con el ámbito abierto, como `EstadoDeAsync`.** La exportación enseñaría un almacén
  bloqueado que la pantalla no enseña, y el bloqueo de R16 existe justo para que no se vea.
- **Que el puerto mire el permiso.** Tendría que conocer al usuario, y cada consumidor, la
  respuesta de cada permiso que le falta. Quien pregunta ya sabe qué permisos tiene.

## Consecuencias

- **El nombre se lee fuera de la foto** (ADR-0073, *Consecuencias*): un nombre que cambia a mitad
  de una exportación puede salir viejo en unas filas y nuevo en otras.
- **Un maestro bloqueado sale con su identificador** en las filas que lo citan, como en la pantalla:
  su ficha contesta que no está, y el fichero no sabe más que ella.
- **El puerto de artículos publica dos datos más**, y el `remarks` de la interfaz dice para qué:
  para enseñarlos. Nada decide con ellos.
