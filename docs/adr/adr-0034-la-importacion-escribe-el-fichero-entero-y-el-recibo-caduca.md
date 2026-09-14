---
tipo: referencia
stack: [csharp, dotnet, aspnetcore, postgresql]
aplica_a: [api-rest, manejo-errores, idempotencia, proteccion-datos, seguridad]
tags: [adr, r10, r12, idempotencia, importacion, csv, retencion, dialecto, tope, adr-0026]
revisado: 2026-09-14
---

# ADR-0034: La importación escribe el fichero entero o nada, su informe no repite ningún dato, y el recibo caduca

- **Estado:** aceptado
- **Fecha:** 2026-09-14
- **Corrige, y no sustituye**, dos frases del
  [ADR-0026](adr-0026-una-importacion-es-una-operacion-y-su-recibo-lleva-el-hash-del-fichero.md)
  §1: «cada fila se procesa en su propia unidad de trabajo» y «el valor que lo provocó». El resto
  del ADR-0026 —la fila como unidad de aislamiento, una importación como una operación, la huella
  del fichero en el recibo y la reimportación como operación nueva— sigue en pie **entero**. La
  corrección va también **en el párrafo** del ADR-0026, con una nota que apunta aquí: un texto
  falso que se deja en pie no se lee como historia, se lee como permiso (ADR-0033).
- **Cierra** la nota abierta desde el 0.9 sobre el crecimiento sin límite de
  `auditoria.claves_de_idempotencia`.
- Se implementa en el **ítem 1.11**.

## Contexto

El ADR-0026 decidió **qué** es una importación. Al ir a construirla aparecen cinco preguntas que no
contestó, y dos frases suyas que no se pueden cumplir a la vez que el resto.

**La primera frase.** «Cada fila en su propia unidad de trabajo» choca con el mecanismo que el mismo
ADR elige para la idempotencia. El `FiltroDeIdempotencia` es dueño de **una** transacción: reclama
la clave, deja trabajar y guarda la respuesta dentro de ella, y de ahí sale la invariante de la
tabla —**la clave existe si y solo si el trabajo ocurrió**—. Con una transacción por fila, un
proceso que se cae en la fila 1 500 deja 1 499 terceros confirmados y ningún recibo: el reintento
con la misma clave vuelve a trabajar y contesta que esas 1 499 «ya existen». El cliente recibe un
informe que desmiente al primero, con cara de acierto — exactamente lo que el §3 del ADR-0026 dice
impedir.

**La segunda frase.** «El valor que lo provocó» choca con la consecuencia del propio ADR-0026:
«nada de datos personales». El valor de la columna `identificacion` de una fila rechazada **es** un
NIF, y el informe se guarda entero en `auditoria.claves_de_idempotencia.cuerpo`.

## Decisión

### 1. La fila es la unidad de DECISIÓN; el fichero, la de ESCRITURA

Cada fila se lee, se valida y se decide **por separado**: una mala nunca impide que entren las
buenas, que es lo que pide `manejo-errores.md` y lo que el ítem exige. Pero la escritura es **una**:
todas las altas aceptadas se confirman en un solo `SaveChanges`, dentro de la transacción del filtro,
junto con el recibo.

Lo que lo hace posible es que **ninguna fila puede fallar al escribir por algo suyo**. Todo lo que
puede rechazar una fila —formato, validación del contrato, NIF, régimen, límite, duplicado en el
fichero y duplicado en la base— se comprueba **leyendo**, antes de escribir nada. La unicidad contra
la base es una sola consulta para todas las identificaciones del fichero, dentro de
`ViendoLoBloqueado`: contesta lo mismo para una ficha activa que para una bloqueada (art. 32,
ADR-0027) y tarda lo mismo, porque es la misma consulta.

**Sin puntos de guardado.** El filtro los apaga a propósito (ADR-0014): con ellos, un fallo deja
viva una transacción que ya lleva la clave reclamada.

**Qué pasa si el proceso se cae a mitad:** no hay nada importado. La transacción no se confirma, la
clave no queda reclamada, y el reintento con la misma clave es la primera vez. Lo único que puede
fallar al escribir es ajeno a las filas: la base, la red, o una carrera —otra petición da de alta el
mismo NIF entre la consulta y el `INSERT`—. Las tres abortan el fichero entero con la misma
respuesta que daría hoy el alta de un tercero en la misma carrera.

**Y la R12, dicha:** «una transacción, un agregado» se rompe aquí a sabiendas. Lo que la regla
protege es que **ningún invariante cruce agregados** —que nadie cargue otro agregado para decidir—
y que las transacciones no se ensanchen hasta chocar entre sí. Aquí no se **modifica** ningún
agregado: se **crean** N independientes, cada uno por su fábrica y con sus invariantes, sin filas
existentes que bloquear. Nada lee después «el fichero» como unidad. La transacción agrupa las altas
solo para que la operación y su recibo sean atómicos, que es lo que pide R10. La otra lectura —una
transacción por fila— cumple la letra de la R12 y rompe la invariante de R10, y de las dos es la
única que produce un dato falso.

### 2. El informe no repite ningún dato de la entrada

Por cada fila rechazada, **línea, columna y motivo**. Nunca el valor. El criterio del ítem pide línea
y motivo; con eso se corrige el fichero, porque el fichero lo tiene quien lo mandó.

- **La línea es la fila que enseña Excel al abrir el fichero:** la cabecera es la 1. Un campo entre
  comillas con un salto de línea dentro ocupa dos líneas en un editor de texto y una fila en Excel;
  se cuenta como Excel, porque es donde se va a corregir.
- **La columna es un nombre de la cabecera, y el motivo, un código de una lista cerrada.** Los dos
  los escribe Bastion, no el usuario, así que un informe descargado o pegado en una hoja nunca
  lleva un campo que empiece por `=`, `+`, `-` o `@` que haya puesto otro.
- **Agrupado por (columna, motivo), con la lista de líneas.** Así su tamaño crece con el número de
  pares (fila, motivo), a unos seis bytes cada uno, y no con el texto. Peor caso con los topes de la
  decisión 3: 5 000 filas × 18 columnas × 6 bytes ≈ **530 KiB**. Lo normal —un centenar de filas
  malas con uno o dos motivos— es **un kilobyte**.

### 3. Dos topes, y el del tamaño se impone donde se vuelca

- **2 MiB por fichero** (2 097 152 bytes). Una fila típica de terceros ocupa unos 150 bytes, así que
  caben más de diez mil; la más ancha que admite el contrato, del orden de un kilobyte.
- **5 000 filas, contando las vacías.** Más que el maestro de terceros de una pyme, que son cientos o unos
  pocos miles; por encima se parte en ficheros, y cada uno es su propia operación con su clave.

**El tope de tamaño no lo comprueba el caso de uso.** Lo impone el lector acotado del borde, que es
el que vuelca: el filtro de idempotencia, para calcular la huella, y el formateador de `text/csv`,
para entregar los bytes. Si la petición declara un `Content-Length` mayor, se contesta `413` **sin
leer un byte**; si no lo declara, se leen como mucho tope + 1 bytes y se corta. Un tope comprobado
después de copiar el cuerpo a memoria no es un tope: la memoria ya se gastó.

**El proxy tiene su propio tope, y era menor.** `client_max_body_size` de nginx vale **1 MiB** por
omisión: un fichero de 1,5 MiB nunca llegaría a la API. Se sube a **3 MiB** en `/api/`: por encima
del tope de la API, para que el `413` con nombre lo dé la API y no la página HTML de nginx.

### 4. El recibo caduca a las 24 horas, y el plazo va en la fila

`auditoria.claves_de_idempotencia` guarda la respuesta de cada escritura idempotente, y para
`POST /api/v1/terceros/terceros` esa respuesta es **la ficha de un tercero**: nombre, NIF y
domicilio de personas identificables, sin plazo. `proteccion-datos.md` no deja lugar a dudas: sin
plazo, no va.

- **Plazo: 24 horas.** El recibo existe para contestar a un reintento. Un reintento de red llega en
  segundos; el de un cliente que se quedó sin conexión, en horas. Un día laborable los cubre a los
  dos, y es la cifra que publican las API de referencia de este mecanismo.
- **El plazo forma parte del modelo:** cada fila lleva su `caduca_en`, calculado al reclamar. Si el
  plazo cambia algún día, las filas que ya existen conservan el que se les prometió.
- **Una purga cada hora** en Auditoría, dueña de la tabla, borra lo caducado de todas las empresas.
  Así un recibo dura **al menos 24 horas** —lo que se promete al cliente— y **como mucho 25** —lo que
  se promete a la persona cuyos datos lleva— mientras la API esté en marcha. Parada no purga; la
  primera vuelta al arrancar se lleva lo pendiente.
- **Vencido, se borra**, no se anonimiza: no queda nada que haga falta conservar.
- **Una clave caducada es una clave nueva.** Si el cliente reintenta pasadas 24 horas, el trabajo se
  vuelve a hacer. En un alta con clave natural —el NIF de un tercero— eso es un duplicado con nombre;
  en una sin ella, un segundo recurso. Queda dicho y documentado en el contrato.

### 5. El dialecto que se acepta, y lo que no es de él se rechaza con nombre

- **Separador `;`**, siempre. No se detecta. Si la primera línea no casa con la cabecera pero **sí**
  casaría separada por `,` o por tabulador, el error se llama `importacion-separador-no-admitido`.
  Es un diagnóstico, no una detección: nunca se lee el fichero con otro separador. Un fichero de
  una sola columna no casa con nada y es `importacion-cabecera-no-valida`.
- **Cabecera fija**: los dieciocho nombres, en su orden y escritos tal cual.
- **Codificación:** UTF-8, con BOM o sin él; si los bytes no son UTF-8 válido, **Windows-1252**, que
  es lo que escribe «CSV (delimitado por comas)» en un Excel en español. Un BOM de UTF-16 o UTF-32, o
  un byte `NUL`, es `importacion-codificacion-no-admitida`.
  **La detección es código y falla, y así:** un fichero en Windows-1252 cuyos bytes forman
  **por casualidad** UTF-8 válido se lee como UTF-8. Para que pase hace falta una letra entre `Â` y
  `ô` seguida de uno o dos caracteres entre `€` y `¿` —`Ã±`, `Â¿`—, que en un texto en español no se
  escribe. Un texto español real en Windows-1252 —`á`, `ñ`, `é` seguidas de una letra— nunca es
  UTF-8 válido. Un fichero solo ASCII es el mismo en las dos.
- **Fin de línea** CRLF o LF. Un CR suelto, fuera de comillas, es
  `importacion-fin-de-linea-no-admitido`.
- **Comillas** según la RFC 4180: un campo entre comillas puede llevar `;`, saltos de línea y `""`
  por cada comilla. Unas comillas que no se cierran se tragan el resto del fichero, así que son
  error de fichero (`importacion-comillas-sin-cerrar`). Una comilla en mitad de un campo sin
  comillas, o texto detrás de la que cierra, es motivo de fila.
- **Importes** en la forma española: coma decimal, hasta cuatro decimales, y punto de miles **solo**
  en grupos de tres (`1.234,56`). `1.5`, `1,234.56`, `1e3` o `1.234,56 €` no se interpretan: son
  `formato-no-valido`.
- **Sí y no:** `sí`, `si`, `no`, `VERDADERO` y `FALSO`, sin distinguir mayúsculas; vacío es no.
- Una fila con **todos los campos vacíos** no cuenta: es la que Excel exporta de una fila con
  formato y sin datos. Una fila con **otro número de campos** que la cabecera es motivo de fila.

### 6. Qué se importa, y qué pasa con lo que ya existe

**Solo terceros.** Traen las dos cosas difíciles que el mecanismo tiene que aguantar: el NIF validado
y el art. 32 —un duplicado contra una ficha bloqueada tiene que contestar lo mismo que contra una
activa—. Los artículos traen otra: resolver por **código** la unidad, el impuesto y la categoría,
que hoy los puertos de estado del 1.2 y del 1.9 solo reciben por identificador. Eso es un contrato
nuevo entre módulos, y queda para cuando haga falta.

**La importación solo da altas.** Una fila cuya identificación ya existe —activa o bloqueada, sin
distinguir— es `ya-existe`; la segunda aparición en el mismo fichero, `repetida-en-el-fichero`.
**Nunca actualiza**, por dos motivos: el permiso de crear no es el de modificar, y una fila de CSV no
trae la versión de la ficha, así que actualizar sería pisar a ciegas lo que otro cambió.

**Los permisos, tipo por verbo.** La acción exige `terceros.tercero.crear`. Si alguna fila trae
límite de crédito, hace falta **además** `terceros.limite-credito.fijar`, y sin él se rechaza el
**fichero** —`403`, antes de mirar ninguna fila—. No se rechazan esas filas: eso mezclaría un
permiso con un dato malo; ni se importan sin el límite: quedarían a medias.

## Alternativas descartadas

**Una transacción por fila.** Cumple la letra de la R12 y rompe la invariante de R10, como se
cuenta arriba. Para arreglarlo, la tabla necesitaría un estado «en curso» que hoy no tiene a
propósito (ADR-0014), y el filtro, dos transacciones.

**Una transacción por fichero con puntos de guardado por fila.** No hay nada que aislar con ellos:
ninguna fila falla al escribir por algo suyo. Encenderlos reabriría el hueco que el filtro cierra.

**Detectar el separador.** Un detector adivina, y se equivoca justo con los ficheros difíciles: los
de una columna y los que llevan `,` dentro de los datos. Un fichero de Excel en español siempre usa
`;`, y el que no, merece que se le diga.

**Guardar el informe fila por fila con sus motivos.** Con los mismos topes, el peor caso pasa de
530 KiB a más de 4 MiB, en una fila de una tabla de servicio.

**No borrar nunca y anonimizar el cuerpo.** El recibo sin cuerpo no sirve para repetir la respuesta,
que es para lo único que existe.

## Consecuencias

- `RegistroDeIdempotencia` gana `caduca_en`, con su migración en Auditoría, y la purga es la segunda
  apertura sin inquilino que no tiene petición detrás, con su motivo en la lista cerrada.
- El filtro de idempotencia deja de volcar el cuerpo sin límite en las acciones que declaran un tope.
- El contrato publica un cuerpo `text/csv` y un `413` con nombre.
- Los errores de fichero y los motivos de fila son contrato: los primeros están en
  `docs/api/errores.json`, y los segundos, en el enumerado del informe que genera el cliente.
