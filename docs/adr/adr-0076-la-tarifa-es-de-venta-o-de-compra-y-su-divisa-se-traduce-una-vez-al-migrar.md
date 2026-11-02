---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [catalogo, organizacion, migraciones, contratos, exportacion]
tags: [adr, tarifa, divisa, iso-4217, migraciones, esquemas, exclude, puertos, adr-0021, adr-0074]
revisado: 2026-11-02
---

# ADR-0076: La tarifa es de venta o de compra, y su divisa se traduce una vez al migrar

- **Estado:** aceptado
- **Fecha:** 2026-11-02
- **Sale del ítem 3.3**, los puertos, con el tipo de la tarifa. Su puerta (PLAN, *La puerta del
  3.3, contestada*) fija las dos cosas que no salían sin elegir qué se conserva: la divisa de las
  tarifas que ya existen la copia la migración desde `organizacion.divisas`, una sola vez, y toda
  tarifa de hoy pasa a ser de venta.
- **Enmienda el [ADR-0074](adr-0074-un-maestro-de-otro-modulo-se-nombra-por-lotes-con-lo-que-ensena-su-ficha.md)**
  en el §1, el §2 y *Cómo se vigila*: el puerto de divisas ya no nombra por lotes, porque su único
  consumidor, la exportación de tarifas, escribe ahora el código que guarda la tarifa.
- **Se apoya en** el [ADR-0021](adr-0021-el-esquema-lo-aplica-un-paso-del-despliegue-no-el-arranque-de-la-api.md):
  el esquema lo aplica un paso del despliegue, y `MigradorDeArranque` migra Organización antes que
  Catálogo.

## Contexto

Hasta el 3.2, `Tarifa` guardaba la divisa como `DivisaId`, el identificador del maestro de
Organización, y no tenía tipo. El resto del sistema nombra la divisa por su código ISO 4217: la
divisa base de la empresa (`EmpresaDto.DivisaBase`), el límite de crédito del tercero, los
documentos y el libro de Inventario, e `Importe`. Esa diferencia es el invariante 8 del traspaso de
la fase 1: comparar la divisa de un precio con la de la empresa pedía traducir una de las dos por
un puerto, en cada lectura.

Compras necesita además que la tarifa diga si es para vender o para comprar. Sin tipo, un tercero
que es cliente y proveedor llevaría una sola tarifa, y el pedido de compra le propondría su precio
de venta.

Y las tarifas que ya existen guardan `divisa_id`. El código está en `organizacion.divisas`, otro
esquema, y ninguna consulta cruza esquemas (§5, regla 4).

## Decisión

1. **La divisa se guarda por su código ISO 4217**, en `catalogo.tarifas.divisa`, de tres letras y
   en mayúsculas. Sin clave ajena, y sin poder tenerla, como la divisa base de la empresa y la del
   límite del tercero. El alta la valida por su código con `IConsultaDeDivisas`: que Organización
   la tenga y no esté retirada. Un código sin forma de código no es ninguna de las que tiene, así
   que contesta `tarifa-divisa-no-encontrada` y no un `500`. `CrearTarifaDto`, `TarifaDto` y
   `PrecioResueltoDto` llevan `Divisa`, una cadena, en lugar de `DivisaId`.
2. **El puerto de divisas pregunta por el código y ya no nombra.** `EstadoDeAsync` recibe el
   código y lo normaliza como lo guarda Organización (`Divisa.NormalizarCodigo`), así que «usd»
   encuentra la del dólar. `NombresDeAsync` sale del puerto: lo usaba solo la exportación de
   tarifas, y el código ya dice qué divisa es. Los otros cinco puertos del ADR-0074 siguen igual.
3. **La tarifa es de venta o de compra, y el tipo va con el código.** `TipoDeTarifa` tiene `Venta`
   y `Compra`, y se guarda como texto, con el `CHECK` `ck_tarifas_tipo` escrito desde los nombres
   del enumerado. Los tramos de un código son todos del mismo tipo, y lo impide la base:
   `tarifas_un_tipo_por_codigo`, un `EXCLUDE USING gist (empresa_id WITH =, codigo WITH =, tipo
   WITH <>)`, gemelo de `tarifas_sin_tramos_solapados`. El caso de uso pregunta antes
   (`TipoDelCodigoAsync`) para contestar el `409` con su motivo, `tarifa-tipo-distinto-del-codigo`,
   y un tipo que no es ninguno de los dos es el `400` `tarifa-tipo-no-valido`. El tipo de un tramo
   no cambia, como su divisa: ningún camino lo escribe después del alta.
4. **La migración traduce leyendo `organizacion.divisas`, una sola vez, y se para si no puede.**
   Es la única lectura entre esquemas del repositorio, y no es la que prohíbe la regla: la regla
   habla del módulo en marcha, y esto lo hace el despliegue, que aplica Organización antes que
   Catálogo. La migración, `ElTipoYLaDivisaDeLaTarifa`:
   - añade `tipo` nula, pone `Venta` en todos los tramos y la cierra. Todo tramo de hoy es de venta:
     era la única clase que había;
   - añade `divisa` nula y la rellena con un `UPDATE … FROM organizacion.divisas`, dentro de un
     bloque `DO` con tres guardas: sin tramos no lee nada, así que una base con solo Catálogo migra;
     con tramos y sin `organizacion.divisas` se para pidiendo las migraciones de Organización; y si
     un tramo apunta a una divisa que no está, se para nombrando el tramo y la divisa;
   - cierra `divisa`, suelta `divisa_id` y pone la restricción de exclusión del tipo.

   **Cada parada lleva el código de lo que le pasa, y ninguno es transitorio**: `42P01`,
   `undefined_table`, si falta Organización, y `23503`, `foreign_key_violation`, si un tramo apunta
   a una divisa que no está, que es lo que habría contestado una clave ajena. La primera versión
   paraba con `55000`, `object_not_in_prerequisite_state`, que es el que dice el nombre, y el primer
   run de sus casos enseñó por qué no vale: **Npgsql lo cuenta como transitorio**
   (`PostgresException.IsTransient`), y la estrategia de ejecución de EF Core, que no reintenta,
   envuelve todo fallo transitorio en un `InvalidOperationException` que dice que probablemente lo
   es y propone activar `EnableRetryOnFailure`. El que lea el registro del migrador buscaría un
   corte de red, y con los reintentos puestos la migración repetiría una parada que no se arregla
   sola. Los casos afirman el código y que `IsTransient` es falso.

   EF Core aplica cada migración en una transacción, así que una parada deja la tabla como estaba,
   con su `divisa_id` y sin el tipo. La vuelta hace la traducción contraria con las mismas guardas,
   y pierde el tipo, porque el esquema de antes no lo tiene.

   **Parar, y no poner una divisa por omisión**, porque un tramo sin su divisa en Organización no
   puede existir: una divisa no se borra, se retira, y la retirada no la saca de la tabla. Si
   aparece uno, es un dato roto, y el euro por omisión convertiría en silencio una tarifa en
   dólares.
5. **La exportación de tarifas lleva el tipo, y la divisa en una columna con su código**:
   `codigo;nombre;tipo;divisa;vigente_desde;vigente_hasta`, como la de empresas, sin nombre de
   divisa.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| El tipo y la divisa en el dominio, y el alta con sus rechazos | `ElTipoYLaDivisaDeLaTarifaTests`, `CrearTarifaTests` | rápido |
| El `CHECK` y las dos exclusiones en la base, y cada rechazo de la API con su `type` | `ContratoDeTarifasTests` | integración |
| El puerto por código: la normalización, y que lo que no es un código no existe | `LosPuertosDeLecturaTests` | integración |
| La migración: la traducción, la vuelta y las dos paradas, sin perder nada | `LaDivisaDeLaTarifaSeTraduceAlMigrarTests` | integración |
| La migración sobre tablas con filas, con la referencia que la base no declara | `LasMigracionesSobreTablasConFilasTests` | integración |
| Las columnas de la exportación | `LasExportacionesDeCatalogoTests`, en los dos carriles | rápido e integración |
| La descripción del puerto de divisas | `Inventario` de `Arquitectura.Tests` | rápido |

**El recorrido con filas necesita saber de la referencia.** Sus filas son inventadas, y un
`divisa_id` de `gen_random_uuid()` no apunta a ninguna divisa: la parada, que es lo que se quiere de
la migración con datos reales, lo pondría rojo. Así que el recorrido lleva una lista de referencias
que la base no declara, al lado de la de los `CHECK`, y rellena `divisa_id` desde la divisa más
nueva, como una clave ajena. Afirma que la referencia se usó al menos una vez: con una letra
cambiada, no se aplicaría nunca y la migración se pararía en el recorrido.

## Alternativas descartadas

- **Un paso del migrador entre dos migraciones, que pregunte los códigos al puerto.** Partiría una
  migración en dos sitios, y un despliegue que se parase entre los dos dejaría la columna a medias.
- **Pasar todas las tarifas a `EUR`.** Convertiría una tarifa en dólares sin aviso.
- **Dejar `divisa_id` y traducir al leer.** Cada comparación con la divisa base pediría el puerto,
  y el invariante 8 seguiría armado.
- **Una clave ajena hacia `organizacion.divisas`.** No puede haberla entre esquemas.
- **El tipo en cada tramo, libre.** Un código con tramos de los dos tipos cambiaría de tipo de un
  día para otro, al empezar a regir el siguiente, y la tarifa asignada a un tercero dejaría de valer
  sin que nadie la tocara.
- **Deducir el tipo de quién usa la tarifa.** Cruza esquemas en los dos sentidos, y una tarifa sin
  usar no tendría tipo.

## Consecuencias

- **El tercero puede llevar una tarifa por rol** y comprobar su tipo por el puerto de las tarifas,
  que es lo siguiente del 3.3.
- **La divisa de la tarifa no está atada a Organización por la base**, como no lo están la divisa
  base ni la del límite. La valida el alta, y como Organización no borra divisas, el código no se
  queda colgando.
- **La migración depende del orden del despliegue.** Una base con tramos y sin Organización no
  migra, y el error lo dice.
- **El recorrido con filas lleva dos listas declaradas**, la de los `CHECK` y la de las
  referencias. Una migración futura que lea otro esquema añade la suya, o se para en el recorrido.
- **Del ADR-0074, cinco puertos nombran por lotes**, y el de divisas ya no.
