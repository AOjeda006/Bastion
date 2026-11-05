---
tipo: referencia
stack: [dotnet, efcore, postgresql, docker, react, typescript]
aplica_a: [fechas, inventario, organizacion, multiempresa, contrato, testing]
tags: [adr, r14, dia-de-negocio, zona-horaria, iana, tzdata, transferencia, corte, cuadre, saldo, adr-0044, adr-0047, adr-0053, adr-0055, adr-0063, adr-0064, adr-0073]
revisado: 2026-11-04
---

# ADR-0079: El día de negocio es el de la zona de su almacén, y lo que abarca varios elige entre ellas

- **Estado:** aceptado. La cabecera se reescribió en el commit siguiente al de su alta,
  `docs(adr): la cabecera del ADR-0079 nombra los siete que enmienda`, para que la regla de las
  enmiendas los leyera en la primera cláusula de su viñeta. No cambia ninguna decisión.
- **Fecha:** 2026-11-04
- **Sale del ítem 3.5**, la zona, que la puerta de la fase 3 puso antes del pedido (respuesta 11):
  una zona por empresa, `Europe/Madrid` por omisión, que el almacén hereda y puede cambiar. Su
  propia puerta (`docs/PLAN.md`, *La puerta del 3.5, contestada*) fijó seis cosas: cambiar una
  zona con movimientos se admite siempre, la herencia es en vivo, la guarda de la transferencia va
  por solapamiento de días, lo que abarca varios almacenes elige entre sus zonas, la lista es
  cerrada, y ningún test hereda la zona de la máquina.
- **Enmienda el ADR-0044, el ADR-0047, el ADR-0053, el ADR-0055, el ADR-0063, el ADR-0064 y el
  ADR-0073**, con la nota en cada uno:
  - el **§4 del [ADR-0044](adr-0044-la-existencia-se-mueve-en-la-sentencia-que-anota-el-libro.md)**:
    «hoy» deja de ser el día UTC (puntos 3 y 9);
  - el **§4 del [ADR-0047](adr-0047-ninguna-fecha-anterior-al-ultimo-movimiento-de-su-clave.md)**,
    «Anular hoy»: anular sí puede chocar con la fecha del último movimiento, durante la hora que
    sigue a un cambio de zona (punto 7);
  - el **§3 y el §5 del
    [ADR-0053](adr-0053-el-transito-vive-en-el-destino-y-el-valor-viaja-con-la-linea.md)**: la
    fila «Contra la otra fecha» y las dos fechas del inverso (puntos 5 y 6);
  - el **§13 del
    [ADR-0055](adr-0055-el-recuento-congela-el-teorico-al-confirmar-y-su-ajuste-es-el-que-mueve.md)**:
    la fecha de apertura del recuento (punto 3);
  - el **§3, el §7 y el §11 del
    [ADR-0063](adr-0063-el-saldo-a-una-fecha-es-la-instantanea-mas-su-tramo-y-el-cerrojo-es-de-la-empresa.md)**:
    la fecha de los saldos, el mes del trabajo mensual y la nota abierta (puntos 3, 8 y 10);
  - el **§3 del
    [ADR-0064](adr-0064-el-avance-del-corte-se-mide-contra-el-plazo-de-cada-orden.md)**: el
    indicador del retraso mide cada corte contra el mes de su empresa (punto 8);
  - las *Consecuencias* del
    [ADR-0073](adr-0073-la-exportacion-recorre-el-listado-en-una-foto-y-escribe-el-dialecto-de-la-importacion.md):
    el día del nombre del fichero (punto 3).
- **Cierra la nota «hoy es el día UTC»**, que abrió el 2.7, el ADR-0063 §11 dejó abierta y el
  repaso de la fase 2 decidió hacer antes de la fase 5.

## Contexto

Desde el 2.16, el día de negocio salía de un solo sitio, `DiaDeNegocio`, y era el día UTC. En la
península eso dejaba dos horas en verano, de 00:00 a 02:00, y una en invierno, en las que un ajuste
fechado hoy se rechazaba como futuro. Era una molestia y no una discrepancia: lo rechazado no
entraba en el libro.

Deja de ser una molestia con el pedido. La recepción del 3.8 es el primer documento cuya fecha será
un devengo, y con el día UTC una entrada a las 00:30 del 1 de enero en Madrid propondría el 31 de
diciembre, que es otro trimestre.

Y la zona no es una. Canarias va una hora por detrás de la península, y una misma empresa puede
tener almacenes en los dos sitios. Durante una hora cada noche, esa empresa tiene dos «hoy». Una
transferencia entre ellos tiene una fecha en cada uno. Y lo que abarca varios almacenes —el corte,
el cuadre, los saldos— necesita uno solo.

## Decisión

### 1. Tres zonas, en una lista cerrada

`ZonaHoraria`, en `BuildingBlocks.Domain.Fechas`, admite tres: `Europe/Madrid`, la de la península
y Baleares; `Atlantic/Canary`; y `Africa/Ceuta`, la de Ceuta y Melilla, que hoy dice lo mismo que
la peninsular y tiene su propio identificador. Madrid es la de por omisión.

- **Se lee solo por su identificador IANA exacto**, en ordinal. «europe/madrid», «UTC» o «Romance
  Standard Time» no son una zona admitida.
- **Sus reglas se piden al sistema cada vez.** El tipo no guarda un `TimeZoneInfo`, así que un
  `tzdata` nuevo entra sin tocar el código.
- **El contrato es `ZonaHorariaDto`**, un enumerado que se escribe con el identificador IANA y no
  lee números. OpenAPI lo publica como enumerado de tres.
- **La base lleva un `CHECK`** en cada columna, con la lista sacada de `ZonaHoraria.Admitidas`.
- **Ampliar la lista es una pregunta al usuario**, no una línea que se añade. Con
  `America/Bogota`, el día de un almacén iría cinco horas por detrás, y ninguna regla de las fechas
  está escrita para eso.

### 2. La zona es de la empresa, y el almacén la hereda en vivo

- `organizacion.empresas.zona_horaria` es obligatoria, con `'Europe/Madrid'` por omisión.
  `organizacion.almacenes.zona_horaria` admite nulo, y el nulo es la de su empresa, leída cada vez:
  cambiar la de la empresa cambia la de todos los almacenes que heredan.
- La API publica en el almacén `zonaHoraria`, la propia, nula si hereda, y `zonaEfectiva`, la que
  rige. Un almacén puede volver a heredar.
- **Cambiar una zona se admite siempre**, aunque haya movimientos: es como se corrige una zona mal
  puesta. Lo ya fechado no cambia.
- **Las zonas se preguntan por un puerto del bloque común**, `IZonasDeLaEmpresa`: la de la empresa,
  la efectiva de un almacén, y todas las de la empresa, la suya primero y sin repetir. Lo
  implementa Organización, que es de quien son, y ve lo bloqueado: un documento de un almacén que
  se bloqueó después sigue necesitando saber qué día es allí.

### 3. Cada día, en la zona de su almacén o en la de su empresa

`DiaDeNegocio` pide la zona en todo lo que publica: `De(instante, zona)`, `Hoy(reloj, zona)`,
`Inicio(dia, zona)`, `NoAcabaAntesDe`, `ElMasAtrasado` y `ElMasAdelantado`. No queda un día sin zona
que alguien pueda pedir por comodidad, y que sería el UTC de antes con otro nombre.

- **Con la zona del almacén**, lo que pasa en un almacén: el ajuste al confirmarse y al anularse;
  el recuento al abrirse, al confirmarse y al anularse; la reserva al consumirse; el envío, con la
  del origen, y la recepción, con la del destino. **Enmienda el ADR-0044 §4**: lo futuro sigue sin
  confirmarse, y «hoy» es el de la zona del almacén del documento. **Y el ADR-0055 §13**: la fecha
  de apertura del recuento es el día del alta en la zona de su almacén.
- **Con la zona de la empresa**, lo que no tiene almacén: el alta y la modificación de un artículo,
  el precio y las tarifas de un tercero sin fecha, los saldos sin fecha y su exportación, y el día
  del nombre del fichero de toda exportación. **Enmienda el ADR-0073**: el fichero lleva el día de
  la empresa, y la ventana entre la medianoche de España y la del UTC desaparece.
- **Los saldos con fecha**: una fecha es futura si lo es en todas las zonas de la empresa, con el
  día más adelantado, para no rechazar nada que ya sea hoy en alguno de sus almacenes. **Enmienda
  el ADR-0063 §3.**

### 4. Un solo sitio, y ningún test con la zona de la máquina

- `ElDiaDeNegocioSaleDeUnSoloSitioTests` busca en la IL de todo el backend dos agujas,
  `DateOnly.FromDateTime` y las conversiones de `TimeZoneInfo`, y su pareja afirma las tres
  llamadas de `DiaDeNegocio` y en qué método está cada una. `Todo_lo_que_da_un_dia_recibe_su_zona`
  afirma que todo método público del sitio recibe una zona o una colección de ellas.
- **Ningún test hereda la zona de la máquina.** Los de dominio dicen la suya, con los dobles de
  `tests/Comun/LasZonasDeLaPrueba.cs`. Los de integración piden `ElDiaDeLaPrueba.Hoy()`, el de
  Madrid, que es la de las empresas de la prueba: con el día UTC, entre las 22:00 y las 24:00 UTC
  un caso daría por futuro lo que allí ya es hoy. `HoyEnLaEmpresaTests` usa un reloj cuya zona
  local va catorce horas por delante, para que leerla diera otro día.

### 5. La transferencia compara instantes, por solapamiento de días

**Enmienda el ADR-0053 §3**, la fila «Contra la otra fecha». La recepción es un día de la zona del
destino, y el envío, uno de la del origen. Se admite si el día de la recepción **no acaba antes de
que empiece** el del envío: `DiaDeNegocio.NoAcabaAntesDe`. En una sola zona, es la comparación de
siempre.

- **Las zonas son las de cuando se comprueba.** No se guardan con la transferencia: se leen al
  recibir, como están entonces.
- De Madrid a Canarias, la recepción puede ir un día por detrás. Al revés, no. Entre Madrid y
  Ceuta, nunca. Un barrido de 2000 a 2100 por los nueve pares afirma que una recepción admitida va
  como mucho un día por detrás, y nombra los dos pares en que va, los dos hacia Canarias.
- **La base admite un día de holgura, y ni uno más.** El `CHECK`
  `ck_transferencias_recepcion_no_antes_del_envio` pasa a
  `fecha_de_recepcion >= fecha_de_envio - 1`, con la holgura de
  `Transferencia.DiasQueLaRecepcionPuedeIrPorDetras`. La base no sabe de zonas: el dominio compara
  instantes, y la base se queda en la cota que mide el barrido.

### 6. El inverso lleva cada fecha a su zona

**Enmienda el ADR-0053 §5**, «Sus dos fechas son la de hoy, en UTC». El inverso nace con su envío
hoy en la zona del origen y su recepción hoy en la del destino, las dos del mismo instante, y cada
pata del libro va con la suya. La R9 pregunta por los dos días: en Nochevieja, a las 23:30 UTC, la
península ya escribe en el año nuevo y Canarias todavía en el viejo.

### 7. La hora que sigue a un cambio de zona

Lo ya fechado no cambia, y las guardas miran las zonas como están cuando comprueban. Entre Madrid y
Canarias hay una hora, así que un cambio deja una ventana de **como mucho una hora** en la que
«hoy» en el almacén es el día anterior a lo último que se fechó allí.

- **Un documento nuevo** fechado hoy en esa ventana va antes que el último movimiento de su clave:
  es el `422` del ADR-0047, y pasada la hora, entra.
- **El inverso de una transferencia** lo comprueba antes de construir, `AdmiteUnInversoEn`: cada
  fecha suya contra cada una del original, con las zonas de ahora. Si alguna va antes, es el `422`
  `transferencia-fecha-anterior-al-ultimo-movimiento`, el código del ADR-0047, con un mensaje que
  dice el cambio de zona y que dura como mucho una hora. **Enmienda el ADR-0047 §4**: anular hoy ya
  no está a salvo siempre de esa regla; lo está fuera de esa hora.
- **El cuadre** suma el libro hasta el día más adelantado de las zonas (punto 9). Si la ventana le
  pilla, lo fechado con el día de la zona de antes cae fuera, y sale un descuadre (*Consecuencias*).

### 8. El corte, con el día más atrasado de las zonas de la empresa

**Enmienda el ADR-0063 §7**, «El mes que toca es el anterior al de hoy en UTC». El mes que toca es
el anterior al del día **más atrasado** entre las zonas de la empresa: la suya y la efectiva de cada
almacén. El corte cierra un mes, y mientras un almacén siga en su último día todavía puede entrar
algo de ese mes.

- **Las zonas se leen una vez por empresa y vuelta**, por `IAvanceDelCorte.ZonasAsync`, que abre el
  ámbito sin inquilino del motivo `AvanceDelCorte` y le pregunta a `IZonasDeLaEmpresa.TodasAsync`.
  Es la tercera apertura de `ElAvanceDelCorte.cs`, y el motivo lo dice. **Las mismas zonas van al
  avance y al cuadre**: leídas dos veces, un cambio de zona entre las dos lecturas daría a cada
  mitad unas distintas.
- **El retraso se mide por empresa**, contra su mes, y el indicador es el de la más retrasada.
  **Enmienda el ADR-0064 §3**, que lo medía en el corte más viejo contra un mes de toda la vuelta.
  La que falla antes de leer sus zonas se mide contra el mes de todas las admitidas, el más
  atrasado posible, que es el que menos retraso le cuenta: el fallo ya lo cuenta su registro, y no
  infla la métrica.
- **Los registros 8700 y 8702 dejan de decir el mes**, porque la vuelta ya no tiene uno. El de cada
  avance, sí.

### 9. El cuadre, con el más adelantado

El cuadre suma el libro **hasta el día más adelantado** de las zonas de la empresa. Lo de hoy en
cualquiera de sus almacenes entra. Una fila con el día de Madrid en una empresa que solo está en
Canarias, no: es una fila del futuro, y para eso está el cuadre. **Enmienda el ADR-0044 §4**, en su
segundo párrafo: «hasta hoy» es hasta ese día.

### 10. Lo que abarca la empresa, con la zona de la empresa

Los saldos sin fecha y los listados van con la zona de la empresa, y la fecha de los saldos con la
más adelantada (punto 3). **Enmienda el ADR-0063 §11**: la nota «hoy es el día UTC» se cierra.

### 11. La imagen y el *runner* conocen las tres

- **El *runner***: `Esta_maquina_conoce_cada_admitida_con_sus_reglas`, en el carril rápido,
  resuelve las tres con su desplazamiento de invierno y de verano. Corre en Linux en la CI y en
  Windows aquí, donde .NET traduce el identificador IANA con ICU.
- **La imagen**: `mcr.microsoft.com/dotnet/aspnet:10.0-noble`, la de `deploy/Dockerfile.api`, trae
  el paquete `tzdata`. Comprobado el 2026-11-04 sobre
  `sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee`, creada el 2026-10-26:
  `tzdata 2026e-0ubuntu0.24.04.1`, los tres ficheros en `/usr/share/zoneinfo`, la zona local UTC, y
  ni `TZ` ni `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` puestas. La orden:

  ```bash
  MSYS_NO_PATHCONV=1 docker run --rm --entrypoint sh mcr.microsoft.com/dotnet/aspnet:10.0-noble \
    -c 'dpkg-query -W -f="${Package} ${Version}\n" tzdata;
        ls -l /usr/share/zoneinfo/Europe/Madrid /usr/share/zoneinfo/Atlantic/Canary \
              /usr/share/zoneinfo/Africa/Ceuta'
  ```

  El `MSYS_NO_PATHCONV` es para Git Bash, que si no reescribe las rutas. La imagen no se fija por
  resumen, así que un `tzdata` distinto entra con la siguiente que se descargue.
- **El humo resuelve las tres en la imagen** que construye, en cada run. Lo añade el cierre del
  3.5, la parte F, y el PLAN dice con qué paso: una comprobación que mira los ficheros y no lo que
  .NET resuelve con ellos no basta.
- **Disparador**: una imagen sin `tzdata`, como las *chiseled* sin el sufijo `-extra`. El humo se
  pone rojo, y la decisión es añadir el paquete o cambiar de imagen.

### 12. El frontal recibe el día, y la fecha sigue al almacén

- **El frontal no calcula el día: lo recibe del servidor**, como decidió la puerta del 2.17
  (respuesta 6), para que la regla viva en un solo sitio. Cada formulario lleva el de la zona de su
  almacén, y lo que abarca la empresa, el de la empresa.
- **La fecha por omisión de un formulario sigue al almacén elegido**, y el tope de la fecha,
  también.
- **Las pantallas distinguen «de la empresa» de «propia»**, y el formulario avisa cuando la zona
  cambia.

Cómo lo publica la API lo decide la parte E del ítem, y lo escribe en el PLAN y, si cambia el
contrato de una forma que valga más allá de este proyecto, en su propio ADR.

## Alternativas descartadas

- **Una zona por instalación**, una variable de configuración. Una instalación con una empresa
  peninsular y otra canaria daría a la segunda el día de Madrid una hora cada noche.
- **Cualquier zona que conozca el sistema.** Ninguna regla de las fechas está escrita para un día
  que vaya cinco horas por detrás, y la respuesta 5 cerró la lista.
- **Comparar las dos fechas de la transferencia en una sola zona**, la del origen. Rechazaría una
  llegada a Las Palmas a las 23:45 de un envío desde Madrid a las 00:30 del día siguiente, un cuarto
  de hora después.
- **Una holgura de un día en todas las transferencias**, sin mirar las zonas. Admitiría una
  recepción en Madrid un día antes de su envío desde Madrid.
- **El corte con la zona de la empresa.** Cerraría el mes de una empresa canaria mientras su
  almacén peninsular sigue en él, o al revés.
- **El cuadre con el día más adelantado de las tres admitidas**, que es siempre el de Madrid. No
  vería una fila con el día de Madrid en una empresa que solo está en Canarias.

## Lo que no cubre, dicho

- **El `current_date` de `inventario.asegurar_particiones_de_movimientos`** sigue con la zona de la
  sesión del migrador. No es un día de negocio: decide desde qué mes se mantienen las particiones,
  con doce por delante, y ninguna fecha se acepta ni se rechaza por él. Lo cuenta la nota de la
  zona en el PLAN.
- **Las marcas de tiempo** son instantes, y no cambian.
- **Las fechas de la factura**, la de expedición y la de devengo (R14), son de la fase 5. Saldrán de
  `DiaDeNegocio`, y con qué zona, lo dirá su puerta.

## Consecuencias

- **Desaparece la ventana de la madrugada** en la península: lo de hoy, allí, es de hoy.
- **Se acepta la hora que sigue a un cambio de zona** (punto 7). En un documento es un `422` que
  pasa solo. En el cuadre, un descuadre que no se repite: el cuadre marca el mes como cuadrado
  igual, así que es una alarma, una vez, y una comprobación a mano media hora después sale limpia.
  Lo prueba
  `Con_el_almacen_de_vuelta_en_Canarias_la_entrada_de_Madrid_descuadra_hasta_que_alli_es_su_dia`.
- **El saldo a una fecha de toda la empresa puede contar dos veces.** El día en que una recepción va
  un día por detrás de su envío, la mercancía ya está en el destino, en su día, y todavía en el
  origen, en el suyo: sumada por la empresa, sale doble ese día. Cada almacén está bien en su
  calendario.
- **El tránsito del destino empieza el día del envío**, que es de la otra zona, así que a un día D
  puede dejarse o llevarse hasta una hora. Es lo mismo que el punto anterior: lo que significa que
  cada fecha sea la de su almacén.
- **Un número es un `400` en cualquier enumerado de la API.** System.Text.Json elige el conversor
  de las opciones antes que el que declara el tipo, y el de las opciones leía números: un
  `"zonaHoraria": 7` llegaba al dominio como una zona que no existe y contestaba un `500`. El
  arreglo, `allowIntegerValues: false` en las opciones, vale para todos. Ningún cliente mandaba
  números.
- **El generador de OpenAPI añade `null` a un enumerado compartido** en cuanto una propiedad que lo
  usa admite nulo, y el esquema es uno. `ContratoDeLaApi` lo quita con un transformador del
  documento, y el nulo se queda en la propiedad.
- **`has-pending-model-changes` no ve una tabla que el contexto excluye de sus migraciones.** La
  instantánea de Inventario no tenía las tres columnas de la causa que el 3.4 añadió a
  `bandeja_de_salida`, y `comprobar-migraciones.sh` salió verde. EF la puso al día con la siguiente
  migración del módulo.

## Cómo se prueba

| Lo que se fija | Dónde |
|---|---|
| La lista, la lectura exacta y las reglas de cada zona en esta máquina | `ZonaHorariaTests` |
| El enumerado y su traducción, ida y vuelta, por nombre | `MapeoDeZonasTests` |
| Tres zonas en el contrato, la de la empresa sin nulo y la del almacén con él | `ElContratoPublicaLasZonasAdmitidasTests` |
| Fin de año, verano, los dos cambios de hora de 2026 y la medianoche de 2000 a 2100 | `DiaDeNegocioTests` |
| Un solo sitio en la IL, y todo lo que da un día recibe su zona | `ElDiaDeNegocioSaleDeUnSoloSitioTests` |
| Hoy en la empresa, con la zona local a +14 | `HoyEnLaEmpresaTests` |
| La zona de la empresa y la del almacén por la API, la herencia, el puerto y los `CHECK` | `LaZonaDeLaEmpresaYDelAlmacenTests` |
| La guarda por solapamiento en diez filas, y el barrido de los nueve pares | `LaTransferenciaEntreDosZonasTests` (Inventario) |
| La recepción un día antes, el `CHECK`, la anulación en dos zonas y la hora del cambio | `LaTransferenciaEntreDosZonasTests` (integración) |
| El mes de cada empresa con sus zonas, su cuadre con las mismas, y el retraso | `ElTrabajoMensualAvanzaElCorteTests` |
| Las zonas sin petición, el cuadre con dos zonas y con una, y la hora del cambio | `ElCuadreEnLasZonasDeLaEmpresaTests` |
| Las tres, resueltas en la imagen | El humo, desde el cierre del 3.5 |

La tanda de mutaciones del cierre del ítem, con sus dos listas, está en el PLAN y, al cerrarse la
fase, en `docs/historia/fase-3.md`.

## Procedencia

Las seis respuestas de la puerta del 3.5, del usuario, el 2026-11-04. La zona por empresa, la
herencia y la guarda por instantes vienen de la respuesta 11 de la puerta de la fase 3, y antes, de
la respuesta 2 del repaso de la fase 2 y de la respuesta 6 de la puerta del 2.17. Lo que decidió el
agente al escribirlo está en el PLAN, *Tomadas por el agente de desarrollo — ítem 3.5*.
