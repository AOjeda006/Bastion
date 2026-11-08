---
tipo: referencia
stack: [csharp, dotnet, api-rest]
aplica_a: [ddd, dominio, api-rest, puertos, inventario]
tags: [adr, retirada, articulo, maestros, puerto, recuento, conflicto, adr-0023]
revisado: 2026-11-08
---

# ADR-0082: El artículo también se retira, y lo nuevo no lo elige salvo el recuento

- **Estado:** aceptado.
- **Fecha:** 2026-11-08
- **Sale del ítem 3.6**, de su criterio y de las dos preguntas de su puerta: qué deja de admitir un
  artículo retirado, y dónde se retira.
- **Amplía el [ADR-0023](adr-0023-los-maestros-de-instalacion-se-retiran-y-una-conversion-ni-se-invierte-ni-se-encadena.md)**
  en su decisión 1: la retirada deja de ser solo de los cuatro maestros de instalación.

## Contexto

El ADR-0023 inventó la **retirada** para los cuatro maestros de instalación —la divisa, el tipo de
cambio, la unidad de medida y la conversión— y la definió en una frase: una fila retirada **no se
ofrece para operaciones nuevas, pero sigue resolviendo** para lo que ya apunta a ella. Desde la
fase 1 la llevan los cuatro, con la marca `IRetirable`, y el ítem 2.2 dejó escrito en el puerto
del artículo, `AptitudParaMoverExistencias`, que sus casillas eran tres y que la cuarta llegaría el
día en que el artículo tuviera su final de vida.

El artículo no tenía ninguno. Se crea, se modifica y se lee, y no hay nada más que hacer con él: ni
bloqueo, que es de los datos personales (R16), ni cierre, que es de lo que se sucede, ni borrado,
que rompería el histórico. Un artículo que se deja de vender se queda en todos los buscadores para
siempre. El criterio del 3.6 pide la casilla: `IConsultaDeArticulos` produce `SoloResuelveLoViejo`,
una línea nueva no elige un artículo retirado, y el retirado sigue contestando su unidad base, para
que un recuento pueda sacar sus existencias.

La frase del ADR-0023 tiene dos mitades, y con el artículo hay que decidir dónde acaba cada una.
Con los maestros de instalación era fácil, porque nadie mueve existencias de una divisa. Con el
artículo no: cuando se retira puede tener existencias, reservas y borradores en curso.

## Decisión

1. **El artículo declara `IRetirable`, la misma marca de los cuatro maestros.** Un booleano,
   `retirada`, con sus dos transiciones, idempotentes, que no tocan nada más de la ficha. Sin fecha
   ni motivo: cuándo y quién ya lo guardan `ModificadoEn` y la traza. No es un bloqueo ni un
   cierre, por las mismas razones que da el ADR-0023 para los cuatro. Lo que cambia es el alcance:
   la retirada deja de ser de los maestros **de instalación** y pasa a ser de los maestros, que el
   artículo es de empresa (R8) y no por eso deja de necesitar una salida.

2. **Se retira por dos puertas, las de los cuatro**: `POST` y `DELETE`
   `/api/v1/catalogo/articulos/{id}/retirada`, con `If-Match` y un `204`, cada una con su permiso.
   **Retirar no mira nada**: ni existencias, ni reservas, ni borradores. Lo que ya está en curso
   sigue su camino (punto 4), y lo que quede en las estanterías sale con un recuento (punto 5).

3. **El listado lo deja fuera salvo que se pida** (`?retiradas=`), y la exportación lo sigue. El
   `GET` por identificador lo sigue devolviendo, con su campo `Retirada`. Los dos puertos de lectura
   por lotes, la unidad base y los nombres, siguen contestando por un retirado: son lo viejo que
   resuelve. Los buscadores de las líneas del frontal dejan de ofrecerlo sin más trabajo, porque
   buscan en la colección.

4. **El puerto gana su cuarto valor, `SoloResuelveLoViejo`, y cada consumidor decide.** Lo produce
   `ConsultaDeArticulos` cuando el artículo existe, se almacena y está retirado, y en este orden: no
   existe, es un servicio, está retirado, se ofrece. **Un servicio retirado sigue contestando
   `NoSeAlmacena`**: esa respuesta ya no autoriza nada, y si contestara la nueva, el recuento, que
   la admite, contaría un servicio. Los cuatro consumidores que lo rechazan solo preguntan **al
   darse de alta**, con un `409` con nombre:

   | Consumidor | `type` | Lo que sigue su camino |
   |---|---|---|
   | Ajuste | `ajuste-articulo-retirado`, con la línea | el borrador se confirma |
   | Transferencia | `transferencia-articulo-retirado`, con la línea | el borrador se envía y se recibe |
   | Reserva | `reserva-articulo-retirado` | la reserva se consume o se libera |
   | Línea de tarifa | `tarifa-articulo-retirado` | la línea se modifica y sigue dando su precio |

   Confirmar, enviar y consumir no preguntan por la aptitud, y es lo que hace que la mitad vieja no
   necesite ninguna excepción escrita. La línea de tarifa de una categoría tampoco pregunta: no
   nombra ningún artículo. El pedido de compra del 3.7 se suma a los cuatro con su propio `type`.

5. **El recuento lo admite**, en las líneas de su foto y en la que se añade a mano, y el ajuste que
   genera al confirmarse saca sus existencias. Es la salida de lo que quede, y la única: con el
   ajuste y la transferencia cerrados a lo nuevo, si el recuento también lo rechazara, las
   existencias de un retirado no tendrían más camino que reincorporarlo, contarlo y volver a
   retirarlo. El ajuste del recuento no pasa por el alta del ajuste, así que no necesita la
   excepción allí.

6. **La regla de los retirables que no se borran mira la colección, no el prefijo.** En cuanto el
   artículo fue retirable, `NingunMaestroRetirableSeBorraTests` encontró dos `DELETE` bajo
   `/articulos`: el del proveedor y el del GTIN. Son colecciones hermanas, con su propio borrado de
   fila. La regla deja fuera lo que va tras un segmento literal después de la colección, y un caso
   afirma que lo excluido no está vacío y que cada uno es el borrado de una fila de su colección.

## Alternativas descartadas

- **Retirar solo sin existencias, sin reservas y sin borradores.** Parece prudente y deja al
  usuario en un callejón: para retirar un artículo con existencias, primero tendría que sacarlas, y
  mientras tanto el artículo se seguiría ofreciendo para lo nuevo. Y es una comprobación con
  carrera: entre la consulta y la retirada puede entrar un alta. Preguntar en cada alta no la tiene.
- **Rechazarlo también al confirmar, al enviar y al consumir.** Dejaría varados los borradores y
  las reservas que ya lo llevaban, que son justo lo que la mitad vieja del ADR-0023 protege. Es lo
  que ya hace la unidad retirada: solo pregunta el alta.
- **Rechazarlo también en el recuento.** Ver el punto 5: las existencias se quedarían sin salida.
- **Contestar `SoloResuelveLoViejo` también para un servicio retirado.** El recuento lo admitiría,
  y un servicio entraría en un recuento por la puerta del retirado.
- **Reutilizar el bloqueo (R16) o el cierre.** Las razones son las del ADR-0023, y valen igual para
  el artículo: no tiene datos personales, y no se sucede.
- **Una columna con la fecha de la retirada.** La guardan `ModificadoEn` y la traza, y una columna
  más sería una segunda fuente de lo mismo.

## Consecuencias

- **El puerto tiene cuatro casillas**, y `LaMatrizDeLosPuertosDeEstadoTests` exige la cuarta contra
  la base: `ElPuertoDelArticuloContraLaBaseTests` la cubre con el bien y el servicio retirados, y
  con el bien reincorporado.
- **Cada consumidor lleva su caso con las dos mitades**, en `LoNuevoNoEligeUnArticuloRetiradoTests`:
  el rechazo de lo nuevo y lo de antes, que sigue. Sin la segunda, un consumidor que rechazara el
  artículo en todas sus operaciones pasaría la primera.
- **El catálogo de errores gana cuatro `type`**, cada uno con su texto en los dos diccionarios.
- **La pantalla va en el listado de artículos**: cada fila lleva *Retirar* o *Reincorporar*,
  confirmado en la ventana del ADR-0080, y la marca «Retirado», y el listado gana un conmutador
  para ver los retirados.
- **El glosario no cambia de palabra, y sí de alcance.** «Retirada» sigue significando lo que dijo
  el ADR-0023, y su entrada en `docs/dominio/glosario.md` deja de decir que es de los maestros de
  instalación. Decía, además, que la palabra «todavía no existe en el código», y eso dejó de ser
  verdad en la fase 1, con los cuatro.
