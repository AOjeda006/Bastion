# Las diecisiete reglas duras — dónde está cada una

El §6 del plan maestro fija diecisiete reglas que no dependen del módulo. Esta tabla dice, de cada
una, **en qué estado está hoy**, con una de tres respuestas y nada en medio:

- **viva** — se aplica a lo que ya existe, y la columna nombra lo que se pondría en rojo si se rompe;
- **aplazada a la fase N** — todavía no hay nada a lo que aplicarla, y la columna dice por qué;
- **no aplica** — y la columna dice por qué. Hoy ninguna.

Nació en el ítem **1.13**. Una regla cambia de estado en el ítem que la hace viva, y ese ítem
cambia su fila.

## De dónde se copió, y lo que eso no garantiza

Los enunciados están copiados **literalmente** del §6 de `ERP-PLAN-MAESTRO.md`, en su
**«Revisión de 2026-08-25 (tarde)»**, con este `sha256`:

```
86aa53a007c074cc051d90d978b41742880f9ee6272016e8200c7cf244a2e108
```

**El límite, dicho.** El plan maestro vive **fuera del repositorio**: lo aporta el usuario y el
`.gitignore` lo excluye. Así que ningún test puede comparar esta tabla con él, ni en local ni en la
CI, y ninguno lo intenta. Que los enunciados sean los del §6 y que las reglas sean diecisiete —el
número está transcrito en la regla, como los dieciséis módulos del §5— lo sostiene **una lectura a
mano**, no una comprobación.

**Se revisa en cada puerta de fase**, con el plan maestro delante:

```bash
sha256sum ERP-PLAN-MAESTRO.md
grep -E '^\*\*R[0-9]+ · ' ERP-PLAN-MAESTRO.md
```

Si la huella coincide con la de arriba, la tabla se copió de este mismo texto y no hay nada que
releer. Si no coincide, se comparan los enunciados de la segunda orden con la tabla, se corrige lo
que haya cambiado y se escribe aquí la huella y la revisión nuevas.

## Qué comprueba la regla, y qué no

`LasDiecisieteReglasTests`, en el carril rápido, compara esta tabla con el repositorio **en los dos
sentidos**:

- **De la tabla hacia fuera.** Las filas son `R1` a `R17`, cada una una vez y en orden: una fila que
  falta es roja, y una fila `R18` —un identificador sin regla detrás— también. Cada tipo que la
  cuarta columna nombra entre comillas invertidas está declarado en `src/` o en `tests/`, y cada ADR
  que cita tiene su fichero: un sitio que se borra o se renombra deja la fila en rojo.
- **Del repositorio hacia la tabla.** Todo identificador de regla que aparece en `src/`, `tests/`,
  `frontend/src/`, `docs/`, `db/`, `deploy/`, `scripts/`, `.github/`, `AGENTS.md`, `CLAUDE.md` o
  `README.md` es una fila de la tabla. Un `R18` citado en un comentario es tan rojo como una fila
  que sobra.
- **El estado.** Es uno de los tres; el de «aplazada» nombra una fase de la 2 a la 11; el texto no
  está vacío; y una regla viva nombra al menos un test.

**No comprueba** que los tests nombrados hagan cumplir la regla —eso lo dice su lectura, y la
revisión del ítem que los escribió—, ni nada que dependa del plan maestro.

## La tabla

| Regla | Enunciado (§6, literal) | Estado | Dónde se hace cumplir, o por qué todavía no |
|---|---|---|---|
| R1 | Todo documento es una máquina de estados explícita. | aplazada a la fase 2 | No hay documentos: la fase 1 cerró con maestros. Los primeros llegan con Inventario (ajustes, recuentos, transferencias). |
| R2 | Un documento confirmado no se edita ni se borra. | aplazada a la fase 2 | Sin documentos no hay nada confirmado; el primero que se confirme será de Inventario. |
| R3 | El stock es un libro mayor, no un contador. | aplazada a la fase 2 | Es el libro de movimientos de la fase 2, cuyo criterio de aceptación es que el saldo proyectado coincida siempre con la suma del libro. Hoy no hay ninguna tabla de stock. |
| R4 | La contabilidad funciona igual, y además cuadra. | aplazada a la fase 7 | No hay asientos ni apuntes hasta el módulo de Contabilidad. |
| R5 | La numeración de documentos es legal: por serie y ejercicio, correlativa y sin huecos. | aplazada a la fase 2 | La `Serie` existe con su contador en una columna (ADR-0007), pero sin bloqueo en la base, y `Serie.RegistrarNumeroAsignado` solo lo llaman los tests: nada confirma todavía un documento. Si los de la fase 2 se numeran con ella es pregunta de su puerta, sin decidir; la numeración sin huecos bajo concurrencia es criterio de la fase 5. |
| R6 | El dinero es `decimal`, con divisa, y con una regla de redondeo escrita. | viva | `Importe` y `PrecioUnitario` imponen las dos escalas (`ImporteTests`, `PrecioUnitarioTests`); el redondeo por base y tipo, con su caso dorado (`ReglaDeRedondeoR6Tests`); las columnas `numeric` en la base (`EsquemaDeTercerosTests`, `MaestrosDelSeptimoApartadoTests`). |
| R7 | Los precios, descuentos e impuestos se congelan en la línea al confirmar. | aplazada a la fase 3 | Congelar en la línea pide una línea con precio, y la primera es la del pedido de compra. Lo que ya existe es de dónde saldrá ese precio: `ResolverPrecio`, que lo resuelve y no lo guarda. |
| R8 | Multiempresa desde la primera tabla. | viva | `ElFiltroNoSeSaltaPorAhiTests` (el filtro global y cada camino que podría saltárselo), `CadaEntidadDeclaraSuInquilinatoTests` (toda entidad dice si es de una empresa o compartida), `NingunaPeticionNombraLaEmpresaTests` (la empresa sale del claim, nunca de la petición). |
| R9 | Los periodos se abren y se cierran. | aplazada a la fase 2 | `Ejercicio` tiene sus estados, y `ICerrarEjercicio` e `IReabrirEjercicio` los cambian, pero todavía no se registra nada dentro de un periodo, cerrar no comprueba nada y reabrir no tiene guarda. El primer registro con fecha es el movimiento de stock; qué exige el cierre y quién reabre es pregunta de la puerta de la fase 2, sin decidir. |
| R10 | Toda escritura que crea un documento es idempotente. | viva | `TodaEscrituraDiceComoSeProtegeTests` (cada escritura de la API declara cómo se protege), `LaMismaClaveDevuelveElMismoRecursoTests` (el mismo intento devuelve lo ya creado). Hoy protege altas de maestros: documentos no hay. |
| R11 | Concurrencia optimista en todos los documentos. | viva | `LaVersionViajaDeLaLecturaALaEscrituraTests`: la versión que emite la lectura es la que acepta la escritura, una obsoleta es `412` con la actual, sin cabecera es `428`, y de dos que leyeron lo mismo solo guarda el primero. |
| R12 | Una transacción modifica un solo agregado. | viva | La consistencia entre agregados va por la bandeja de salida, en la misma transacción y sin duplicar al reprocesar (`ElEventoVaEnLaMismaTransaccionTests`, `ReprocesarNoDuplicaTests`). La otra mitad, un solo agregado por transacción, es convención sin regla, y la importación la rompe a sabiendas (ADR-0034). |
| R13 | Todo movimiento y todo apunte apuntan a su documento origen, y viceversa. | aplazada a la fase 2 | La doble flecha une un movimiento o un apunte con su documento, y el primer movimiento es el de stock. |
| R14 | Cada operación tiene tres fechas, y son tres columnas. | aplazada a la fase 3 | No hay operaciones: la primera con entrega, que es la fecha de devengo, es la recepción de compras; la expedición llega con la fase 5 y el cobro con la 6. Lo que el código llama R14 desde la fase 0 es su condición previa y no la regla: un instante lleva zona y una fecha de negocio no (`LasFechasDicenDeQueTipoSonTests`). |
| R15 | La cadena de registros de facturación es única por sistema, y serializa la emisión. | aplazada a la fase 5 | Es la del registro fiscal encadenado, que llega con Facturación; hoy no hay ningún registro. |
| R16 | Suprimir no es borrar: es bloquear. | viva | `LaFilaBloqueadaSigueEnLaBaseTests` (suprimir deja la fila con su motivo y su fecha), `ElFiltroNoSeSaltaPorAhiTests` (el filtro es de repositorio), `ElAccesoReservadoDelArticulo32Tests` (la consulta reservada). Falta la destrucción al vencer: el plazo existe (`PoliticaDeRetencion`), quien destruye no, y no tiene fase. |
| R17 | Las direcciones se guardan en campos estructurados. | viva | `Direccion` guarda los campos por separado y compone la línea (`DireccionEstructuradaR17Tests`); de ida y vuelta por la API, `ContratoDeOrganizacionTests`. |
