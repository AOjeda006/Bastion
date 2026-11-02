---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [catalogo, terceros, organizacion, contratos, compras]
tags: [adr, puertos, precio, tarifa, divisa, iso-4217, matriz, internalsvisibleto, adr-0076]
revisado: 2026-11-02
---

# ADR-0077: El precio de compra lo propone un puerto que dice por qué no

- **Estado:** aceptado
- **Fecha:** 2026-11-02
- **Sale del ítem 3.3**, los puertos, con el tipo de la tarifa. Es su quinto tema: el precio de
  compra, en `Catalogo.Contracts`, y la tarifa de compra de un proveedor, en `Terceros.Contracts`.
  La puerta (PLAN, *La puerta del 3.3, contestada*) fija que el puerto toma el tramo que lleva el
  tercero y resuelve con el de su código que rige ese día, y que solo propone una tarifa de compra
  en la divisa base de la empresa.
- **Se apoya en** el [ADR-0076](adr-0076-la-tarifa-es-de-venta-o-de-compra-y-su-divisa-se-traduce-una-vez-al-migrar.md):
  la tarifa ya tiene tipo, el tipo va con el código, y su divisa se guarda por su código ISO 4217.

## Contexto

Hasta el 3.3, ningún puerto resolvía un precio. `IConsultaDeTarifas` solo publica `EstadoDeAsync`,
para que Terceros compruebe una tarifa al asignarla, y `PrecioResueltoDto` solo lo devolvía el caso
de uso de `GET /api/v1/catalogo/tarifas/{codigo}/precio`. El pedido de compra del 3.7 necesita que
Compras pregunte el precio de un artículo a la tarifa de compra de su proveedor, sin leer el
esquema de Catálogo ni el de Terceros.

Esa pregunta tiene más respuestas que un precio. La tarifa puede no existir, ser de venta, no regir
ese día, estar en otra divisa, o no tener línea para el artículo. Lo que no se propone no es un
cero: es una línea que alguien escribe a mano, y cada motivo se arregla de una manera distinta.

Y la comparación de divisas era el invariante 8 del traspaso de la fase 1: la tarifa guardaba la
suya como `Guid` y la empresa como código ISO. El ADR-0076 movió la de la tarifa al código, y este
puerto es el primero que las compara.

## Decisión

1. **`IConsultaDePrecios.DeCompraAsync` contesta el precio o el motivo, nunca los dos.** Recibe el
   tramo que lleva el tercero, el artículo, la cantidad y la fecha del documento, y devuelve un
   `PrecioPropuesto`: un registro sellado con `Propuesta`, de tipo `PropuestaDePrecio`, y `Precio`,
   que solo lleva valor con `Propone`. Sus dos fábricas son las únicas puertas: `Con` no acepta un
   precio nulo, y `Sin` no acepta `Propone` ni un valor fuera del enumerado. Un precio al lado de un
   motivo para no proponer sería un número que alguien acabaría copiando.
2. **Los motivos se miran de la tarifa hacia la línea**, y cada uno para en cuanto se sabe:
   1. `NoExisteLaTarifa`, el valor cero, si el tramo no está en esta empresa;
   2. `EsDeVenta`, si su tipo no es `Compra`. El tipo es del código (ADR-0076 §3), así que el del
      tramo asignado es el del que rija;
   3. `NoRigeEseDia`, si ningún tramo del código rige ese día;
   4. `NoEstaEnLaDivisaBase`, si el tramo que rige no está en la divisa base de la empresa;
   5. `NoExisteElArticulo`;
   6. `SinLineaParaElArticulo`, si ninguna línea del tramo alcanza al artículo con esa cantidad.

   Así, una tarifa de venta contesta `EsDeVenta` pregunte por el artículo que pregunte. Una
   cantidad que no es mayor que cero no es un motivo: es un error de quien llama, y lanza.
3. **El precio sale del tramo que rige ese día, no del asignado.** El tercero guarda el tramo que se
   le asignó, y ese puede haber caducado y tener sucesor. El puerto busca el vigente de su código,
   como el precio por HTTP, y `PrecioResueltoDto.TarifaId` dice cuál contestó. La divisa que se
   compara es la de ese tramo: los tramos de un código comparten el tipo, pero no la divisa.
4. **La divisa se compara por su código, ordinal, y el invariante 8 queda desarmado.** La de la
   empresa la contesta `IConsultaDeEmpresas.DivisaBaseDeAsync`, y las dos son códigos ISO 4217 en
   mayúsculas desde el ADR-0076. Una empresa bloqueada contesta nulo, y nulo no es la divisa de
   ningún tramo: el motivo es el mismo, y por eso se llama `NoEstaEnLaDivisaBase` y no
   `OtraDivisa`. Convertir un precio en dólares pide el tipo de cambio de ese día, que es de la
   fase 5; hasta entonces se escribe a mano, y no se propone convertido a ojo. El traspaso de la
   fase 1 no se reescribe: el desarme lo dicen este ADR y el PLAN.
5. **La implementación vive en la aplicación, y se registra con los demás puertos.**
   `ConsultaDePrecios` es la precedencia de las líneas —el artículo antes que la categoría, y el
   antepasado más cercano— sobre los mismos repositorios que usa el precio por HTTP. En la
   aplicación se prueba sin base, motivo por motivo, en `Catalogo.UnitTests`. Se registra en
   `ModuloDeCatalogo`, al lado de `IConsultaDeTarifas` y `IConsultaDeArticulos`, porque ese es el
   sitio donde se lee lo que el módulo expone, y la infraestructura ve lo interno de la aplicación.
6. **La resolución de la línea se comparte, no se copia.** `ElPrecioDelTramo.ResolverAsync` recibe
   el tramo y el artículo ya leídos y hace lo que queda: la ascendencia, las candidatas, la línea
   que gana y la respuesta. Lo usan el precio por HTTP (`ResolverPrecio`) y este puerto. Con dos
   copias, la que se corrigiera un día dejaría a la otra con la regla de antes.
7. **La tarifa de compra de un proveedor la dice `Terceros.Contracts`.**
   `IConsultaDeTerceros.TarifaDeCompraDeAsync` devuelve su identificador, o nulo. Es una consulta,
   por los dos filtros, el de la empresa y el del bloqueo, y con el papel en el `WHERE`: quien deja
   de ser proveedor conserva la tarifa en su ficha, y sin esa condición la propondría. Nulo junta a
   propósito cuatro casos —la ficha no se puede tratar, no es proveedor, o lo es y no lleva tarifa—
   porque para quien compra son la misma línea escrita a mano. Lo que cruza entre los dos puertos
   es un `Guid`, y quien compra lo lleva de uno al otro.
8. **La matriz de puerto × estado reconoce la respuesta con motivo.** Hasta ahora contaba las
   familias de los métodos que devuelven `Task<TEnumerado>`. `PrecioPropuesto` no es un enumerado,
   así que con esa forma el puerto habría entrado con la matriz en verde y sus siete casillas sin
   mirar. La forma admite ahora también una clase sellada, no genérica, de un `Bastion.*.Contracts`,
   con una sola propiedad pública de instancia que sea un enumerado. Un anclaje nuevo afirma que el
   descubrimiento ve `IConsultaDePrecios` y que su familia es `PropuestaDePrecio`.
9. **`Bastion.Api.IntegrationTests` ve lo interno de `Catalogo.Application`.** Los motivos los
   decide una consulta con el filtro de la empresa, y un doble contestaría lo que se le programe.
   Ese carril compone el puerto a mano con los adaptadores de verdad sobre PostgreSQL, como ya hace
   con los de Terceros.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| Cada motivo, su orden, el tramo sucesor y su divisa, y las guardas de las fábricas | `ElPrecioDeCompraTests` | rápido |
| Las siete casillas contra la base, y el cliente que también es proveedor | `ElPrecioDeCompraContraLaBaseTests` | integración |
| La tarifa de compra de un proveedor: sin tarifa, inventado, ex proveedor, bloqueado, de otra empresa | `ElPrecioDeCompraContraLaBaseTests` | integración |
| Que el host de verdad registra el puerto, que no tiene consumidor hasta el 3.7 | `ElPrecioDeCompraContraLaBaseTests` | integración |
| Que la matriz ve el puerto y su familia, y que cada casilla tiene dueño | `LaMatrizDeLosPuertosDeEstadoTests` | integración |
| La descripción de los dos puertos y el cruce hacia Organización | `Inventario` de `Arquitectura.Tests` | rápido |

**El cliente que también es proveedor** es el caso que da sentido al ítem: lleva una tarifa de
venta y una de compra, el puerto de Terceros le contesta la de compra, y la de venta, preguntada al
de precios, contesta `EsDeVenta` aunque tenga línea para el artículo.

## Alternativas descartadas

- **Un `Resultado` con un error por motivo.** Los motivos no son errores de quien pregunta: son
  respuestas que el pedido de compra enseña o calla. Y la matriz cuenta familias de estados, no
  códigos de error.
- **El precio en `IConsultaDeTarifas`.** Aquel contesta si una tarifa se puede asignar, y este qué
  precio propone. Son dos familias, y la matriz las cuenta por puerto: en uno solo se pisarían.
- **Un precio de cero o nulo sin motivo.** Un cero se copia en la línea como si fuera un precio, y
  un nulo no dice qué arreglar.
- **Resolver con el tramo asignado.** Propondría el precio de un tramo que ya no rige, y el precio
  por HTTP contestaría otro el mismo día.
- **Convertir la divisa.** Pide el tipo de cambio, que llega en la fase 5.
- **Implementar el puerto en la infraestructura, como los otros.** Sería copiar la precedencia de
  las líneas en otra capa, o probarla solo contra la base.
- **Que Terceros compruebe el papel en quien pregunta.** Cada consumidor tendría que acordarse, y
  el que no lo hiciera propondría la tarifa de un ex proveedor.

## Consecuencias

- **El pedido de compra del 3.7 tiene sus dos preguntas**: la tarifa del proveedor y el precio que
  propone. Ninguno de los dos puertos tiene consumidor hasta entonces.
- **Catálogo pregunta la divisa base a Organización**, y el cruce declarado de su aplicación lo
  dice.
- **La matriz admite dos formas de respuesta.** Un puerto futuro que conteste un estado con otra
  forma tiene que ampliarla, con su anclaje, o entra sin que nadie mire sus casillas.
- **Una corrección de la precedencia de las líneas vale para los dos caminos** a la vez.
