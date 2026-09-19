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
  `README.md` es una fila de la tabla, y también cada etiqueta `rNN` de la línea `tags:` de un ADR
  —la R13 solo se cita así—. Un `R18` citado en un comentario es tan rojo como una fila que sobra.
- **El estado.** Es uno de los tres; el de «aplazada» nombra una fase de la 2 a la 11; el texto no
  está vacío; y una regla viva nombra al menos un test.

**No comprueba** que los tests nombrados hagan cumplir la regla —eso lo dice su lectura, y la
revisión del ítem que los escribió—, ni nada que dependa del plan maestro.

## La tabla

| Regla | Enunciado (§6, literal) | Estado | Dónde se hace cumplir, o por qué todavía no |
|---|---|---|---|
| R1 | Todo documento es una máquina de estados explícita. | viva | El primer documento es el `Ajuste` del ítem 2.3, y su estado lo mueve `DocumentoBase`: `LaMaquinaDeEstadosDelAjusteTests`, ocho casos —nace en borrador; cada transición pide su estado de partida **y** su evento; confirmar dos veces y anular un borrador fallan por separado, porque un cambio que dejara de mirar el estado de partida rompe los dos a la vez y uno que solo tocara el ajuste rompe uno—. Lo que la regla quiere impedir, la asignación directa del estado, **no compila**, y lo que no compila no se puede escribir como caso: lo que se afirma son sus dos condiciones exactas, y las afirma `LaMaquinaDeEstadosDelAjusteTests.El_estado_no_se_asigna_se_transita` sobre el ensamblado compilado —el asignador no es público y lo declara otro ensamblado—, después de afirmar que ese asignador existe (ADR-0020): sin asignador ninguno, el caso daría verde sin mirar nada. |
| R2 | Un documento confirmado no se edita ni se borra. | aplazada a la fase 2 | **El motivo de antes —«sin documentos no hay nada confirmado»— lo dejó viejo el ítem 2.3**: ya hay un documento que se confirma, y un `Ajuste` confirmado no admite líneas nuevas (`LaMaquinaDeEstadosDelAjusteTests`). Lo que falta es la otra mitad del enunciado: «ni se borra» se hace cumplir con el documento **inverso** que compensa, y `Ajuste.Anular` hoy mueve el estado del original y no crea nada. Ese inverso es el ítem 2.5, y hasta entonces la regla está a medias. |
| R3 | El stock es un libro mayor, no un contador. | aplazada a la fase 2 | **«Hoy no hay ninguna tabla de stock» dejó de ser verdad en el ítem 2.3**, y el estado no cambia con ella: el libro ya existe —`MovimientoStock`, particionada por mes y de solo añadido en el motor (`ElLibroNoSePuedeLimpiarTests`, los seis caminos cerrados)—, pero el enunciado no dice «hay un libro»: dice que el stock **es** ese libro **y no un contador**, y eso solo se puede poner en rojo habiendo un contador que discrepe. El saldo proyectado llega en el **2.7**, con el test de propiedad que compara su valor con la suma del libro; hasta entonces no hay dos cifras que comparar, así que la regla no está viva: está sin poder romperse. Lo que el 2.3 sí deja hecho cumplir es su condición previa —la fila no cambia nunca—, que es de lo que el 2.7 dependerá. |
| R4 | La contabilidad funciona igual, y además cuadra. | aplazada a la fase 7 | No hay asientos ni apuntes hasta el módulo de Contabilidad. |
| R5 | La numeración de documentos es legal: por serie y ejercicio, correlativa y sin huecos. | aplazada a la fase 2 | El contador ya no es una columna de la serie: vive en su propia fila (`ContadorDeSerie`, ADR-0039), y quien lo sube es `NumeradorDeSerie`, que toma el cerrojo en el motor con un incremento condicionado dentro de la transacción de quien confirma. **Lo que el ítem 2.4 deja viejo del motivo anterior son sus dos mitades**: ya no hay un método del dominio que solo llamen los tests —el método del dominio que subía el contador se borró, porque ningún módulo podía verlo— y ya hay bloqueo en la base. Lo que falta para que la regla esté viva es el **llamante**: el `Ajuste` sigue confirmándose **sin número**, que es justo lo que la regla no permite, y el número entra en la confirmación en este mismo ítem. |
| R6 | El dinero es `decimal`, con divisa, y con una regla de redondeo escrita. | viva | `Importe` y `PrecioUnitario` imponen las dos escalas (`ImporteTests`, `PrecioUnitarioTests`); el redondeo por base y tipo, con su caso dorado (`ReglaDeRedondeoR6Tests`); las columnas `numeric` en la base (`EsquemaDeTercerosTests`, `MaestrosDelSeptimoApartadoTests`). |
| R7 | Los precios, descuentos e impuestos se congelan en la línea al confirmar. | aplazada a la fase 3 | Congelar en la línea pide una línea con precio, y la primera es la del pedido de compra. Lo que ya existe es de dónde saldrá ese precio: `ResolverPrecio`, que lo resuelve y no lo guarda. |
| R8 | Multiempresa desde la primera tabla. | viva | `ElFiltroNoSeSaltaPorAhiTests` (el filtro global y cada camino que podría saltárselo), `CadaEntidadDeclaraSuInquilinatoTests` (toda entidad dice si es de una empresa o compartida), `NingunaPeticionNombraLaEmpresaTests` (la empresa sale del claim, nunca de la petición). |
| R9 | Los periodos se abren y se cierran. | aplazada a la fase 2 | `Ejercicio` tiene sus estados, y `ICerrarEjercicio` e `IReabrirEjercicio` los cambian, pero cerrar no comprueba nada y reabrir no tiene guarda. **Lo que el ítem 2.3 deja viejo del motivo anterior es «todavía no se registra nada dentro de un periodo»**: ya se registra —`MovimientoStock` lleva su fecha de operación, que es además la clave por la que se particiona— y confirmar un ajuste **no pregunta** si ese periodo está cerrado. Quien lo pregunte es el ítem **2.6**. |
| R10 | Toda escritura que crea un documento es idempotente. | viva | `TodaEscrituraDiceComoSeProtegeTests` (cada escritura de la API declara cómo se protege), `LaMismaClaveDevuelveElMismoRecursoTests` (el mismo intento devuelve lo ya creado). Hoy protege altas de maestros: documentos no hay. |
| R11 | Concurrencia optimista en todos los documentos. | viva | `LaVersionViajaDeLaLecturaALaEscrituraTests`: la versión que emite la lectura es la que acepta la escritura, una obsoleta es `412` con la actual, sin cabecera es `428`, y de dos que leyeron lo mismo solo guarda el primero. |
| R12 | Una transacción modifica un solo agregado. | viva | La consistencia entre agregados va por la bandeja de salida, en la misma transacción y sin duplicar al reprocesar (`ElEventoVaEnLaMismaTransaccionTests`, `ReprocesarNoDuplicaTests`). La otra mitad, un solo agregado por transacción, es convención sin regla, y se rompe **a sabiendas dos veces**: la importación (ADR-0034) y, desde el ítem 2.3, la confirmación de un `Ajuste`, que escribe además sus filas de `MovimientoStock` —que no son del agregado— dentro de la **misma** transacción. El motivo es que la alternativa rompe una regla dura en vez de una convención: con dos transacciones hay un instante con el documento confirmado y el libro sin mover, y durante ese instante la R3 es falsa. El evento, en cambio, va **por documento y no por movimiento**, que es la mitad que sí se respeta. |
| R13 | Todo movimiento y todo apunte apuntan a su documento origen, y viceversa. | viva | El primer movimiento es el de stock, del ítem 2.3, y la flecha es un **par** —`TipoDeDocumentoOrigen` más el identificador—, así que **ninguna clave ajena puede expresarla**: el destino cambia con el tipo, y son cinco documentos. Lo que se pone rojo es un caso por sentido, en `LaDobleFlechaDelLibroTests`. La **ida**, `LaDobleFlechaDelLibroTests.Ninguna_fila_del_libro_apunta_a_un_documento_que_no_existe`: una fila del libro cuyo documento origen no está en la tabla de ajustes. La **vuelta**, `LaDobleFlechaDelLibroTests.Ningun_ajuste_confirmado_se_queda_sin_una_sola_fila_del_libro`: un ajuste confirmado sin ni una fila. Los dos afirman **primero** que su barrido ha mirado algo (ADR-0020) —sobre cero filas los dos saldrían verdes—, y los dos se han visto en rojo con un arnés que rompe su mitad. Y la vuelta la sostiene además el dominio, **antes** de que la fila exista: `LaMaquinaDeEstadosDelAjusteTests.Un_ajuste_sin_lineas_no_se_confirma`. |
| R14 | Cada operación tiene tres fechas, y son tres columnas. | aplazada a la fase 3 | No hay operaciones: la primera con entrega, que es la fecha de devengo, es la recepción de compras; la expedición llega con la fase 5 y el cobro con la 6. Lo que el código llama R14 desde la fase 0 es su condición previa y no la regla: un instante lleva zona y una fecha de negocio no (`LasFechasDicenDeQueTipoSonTests`). |
| R15 | La cadena de registros de facturación es única por sistema, y serializa la emisión. | aplazada a la fase 5 | Es la del registro fiscal encadenado, que llega con Facturación; hoy no hay ningún registro. |
| R16 | Suprimir no es borrar: es bloquear. | viva | `LaFilaBloqueadaSigueEnLaBaseTests` (suprimir deja la fila con su motivo y su fecha), `ElFiltroNoSeSaltaPorAhiTests` (el filtro es de repositorio), `ElAccesoReservadoDelArticulo32Tests` (la consulta reservada). Falta la destrucción al vencer: el plazo existe (`PoliticaDeRetencion`), quien destruye no, y no tiene fase. |
| R17 | Las direcciones se guardan en campos estructurados. | viva | `Direccion` guarda los campos por separado y compone la línea (`DireccionEstructuradaR17Tests`); de ida y vuelta por la API, `ContratoDeOrganizacionTests`. |
