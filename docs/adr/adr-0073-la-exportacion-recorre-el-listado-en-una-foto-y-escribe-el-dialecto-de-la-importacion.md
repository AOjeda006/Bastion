---
tipo: referencia
stack: [dotnet, aspnetcore, efcore, postgresql]
aplica_a: [api-rest, exportacion, catalogo, inventario, organizacion, terceros]
tags: [adr, exportacion, csv, listado, paginacion, transaccion, repeatable-read, flujo, adr-0034, adr-0072]
revisado: 2026-10-27
---

# ADR-0073: La exportación recorre el listado por páginas dentro de una foto, y escribe el dialecto de la importación

- **Estado:** aceptado. **Enmendado por el ADR-0075** en el §6: el apóstrofo es el séptimo
  disparador.
- **Fecha:** 2026-10-27
- **Sale del ítem 3.2**, la exportación de los listados. Su criterio, en `docs/PLAN.md`, pide que el
  servidor genere el fichero «con los filtros y el orden del listado, **todas sus páginas**, el
  permiso del listado y sin lo bloqueado», «por páginas internas y en flujo, nunca de una sentencia
  ni entero en memoria», en el dialecto de la importación, con su BOM y la regla del apóstrofo.
- **Se apoya en** el §5 del
  [ADR-0034](adr-0034-la-importacion-escribe-el-fichero-entero-y-el-recibo-caduca.md), el dialecto
  que lee la importación, y en el
  [ADR-0072](adr-0072-un-listado-enlaza-tres-piezas-y-el-texto-solo-quien-lo-mira.md), que separa la
  página del orden y del texto. La regla del apóstrofo es la decisión 7 del 1.11, en
  `docs/historia/fase-1.md`.

## Contexto

Un listado ya sabe dar una página: aplica el permiso, filtra lo bloqueado en el repositorio, ordena,
acota y cuenta. La exportación necesita **todas**, y eso trae tres problemas que una página no tiene.

- **Las páginas se piden en momentos distintos.** Con `OFFSET`, una fila que entra entre la primera
  y la segunda empuja a la última de la primera a salir otra vez; una que se borra hace que otra no
  salga en ninguna. Y el total que dice cuántas páginas pedir puede cambiar a mitad.
- **El fallo después del primer byte no se puede contestar.** Cuando han salido el `200` y las
  cabeceras, ya no hay un problema que mandar. Si el servidor cierra el fichero como si estuviera
  entero, quien lo descarga se lleva un fichero corto sin saberlo.
- **El fichero tiene que volver a entrar, y abrirse en una hoja de cálculo.** Los terceros se
  importan con el dialecto del ADR-0034, y lo que se exporta de ellos tiene que poder importarse.
  Un Excel en español lee un CSV sin BOM como Windows-1252. Y un texto que empieza por `=` es una
  fórmula para quien lo abre.

## Decisión

1. **La exportación llama al caso de uso del listado, una vez por página interna**, de
   `Paginacion.TamanioMaximo` (200) en 200, con el orden, los criterios y el texto de la URL. El
   permiso, lo bloqueado y los filtros son los del listado porque el código es el suyo: la
   exportación no tiene consulta propia que pueda divergir. Cuántas páginas pedir lo dice el total
   de la primera. La página la pone la exportación, que no enlaza `page` ni `size` (ADR-0072), y no
   puede pedir nada que un cliente no pudiera pedir ya.
2. **Todas las páginas se leen dentro de una foto** (`IFotoDeLectura`): una transacción
   `REPEATABLE READ` y `READ ONLY` sobre el contexto del módulo, el mismo con el que lee el listado.
   En PostgreSQL esa transacción toma su instantánea con la primera orden y la usa en todas las
   demás, así que cada página sale del mismo instante y el total de la primera es el de todas.
   - **De solo lectura**, para que algo que escriba en el camino de una exportación lo rechace la
     base con un `25006`, en vez de escribirlo una transacción cuyo nivel nadie eligió para eso.
     EF Core no abre una transacción de solo lectura, así que el `SET TRANSACTION READ ONLY` es SQL
     crudo, y está declarado entre los saltos de `ElFiltroNoSeSaltaPorAhiTests`: no lee ni escribe
     ninguna fila.
   - **No se abre dentro de otra transacción**: heredaría su nivel, su instante y lo que hubiera
     escrito. Lanza.
   - **Se deshace al cerrarla**, porque no hay nada que confirmar.
   - **Una por módulo**, como la unidad de trabajo: el módulo declara `IFotoDeX : IFotoDeLectura`
     en su aplicación, y su infraestructura la abre con `FotoDeLectura.AbrirAsync` sobre su
     contexto. Llega con la primera exportación del módulo, con su registro y su prueba de
     construcción.
3. **Lo que el listado rechaza, se rechaza antes del primer byte.** Primero los criterios: un
   `?sort=` que no se admite o un `?q=` a un listado que no lo mira son su `400`, sin abrir la foto.
   Después, la primera página se pide antes de escribir nada, así que un `404` del recurso del que
   cuelga el listado, o un `403`, salen con su problema, como en el listado. Desde ahí la respuesta
   ha empezado, y **una página que falla a mitad lanza**: ASP.NET Core corta la conexión sin cerrar
   el fichero, `fetch` rechaza la lectura del cuerpo, y quien descarga ve un error y no un fichero
   corto. La foto se cierra igual.
4. **En flujo**: el escritor escribe la cabecera y cada página, y vacía la corriente al terminar
   cada una. En memoria hay una página de elementos y el búfer del escritor (16 KiB), no el fichero.
5. **El dialecto es el de la importación, con lo que allí se admite fijado aquí**:
   - UTF-8 **con** BOM, separador `;`, fin de línea **CRLF**;
   - un campo va entre comillas si lleva `;`, `"`, CR o LF, con cada comilla doblada (RFC 4180);
   - un número, con coma decimal, sin punto de miles y sin ceros a la derecha; el cero, `0`, sin
     signo;
   - `sí` y `no`; una fecha, `AAAA-MM-DD`; un identificador, en su forma de 36 caracteres; un valor
     de una enumeración, por su nombre;
   - la cabecera, en `snake_case`, la escribe Bastion.
6. **La regla del apóstrofo solo va en el texto.** Un campo de texto que empieza por `=`, `+`, `-`,
   `@`, un tabulador o un retorno sale con un apóstrofo delante, antes de entrecomillarlo. Un número,
   una fecha o un identificador los escribe Bastion, y un `-12,5` con apóstrofo dejaría de ser un
   número al abrirlo. El tipo del campo (`CampoCsv`) es el que lo dice, no lo que lleva escrito.

   > **Enmendado por el ADR-0075 (2026-10-27).** Los disparadores son siete: también un texto que
   > ya empieza por un apóstrofo sale con otro delante, para que la importación pueda quitar el
   > que se puso sin quitar nada más, y lo que sale vuelva igual. La regla vive en
   > `ApostrofoDelDialecto`, que usan los dos lados.
7. **El fichero se llama `<recurso>-<AAAA-MM-DD>.csv`**, con el día de negocio de `DiaDeNegocio`, y
   sale como adjunto, con `filename` y `filename*`. El tipo es `text/csv; charset=utf-8`.
8. **La ruta es `GET …/{recurso}/exportacion`**, con los parámetros del listado menos la página.

### Cómo se vigila

| Qué | Dónde | Carril |
|---|---|---|
| El dialecto, el apóstrofo, las comillas, los números, y que el lector de la importación lea lo que se escribe | `EscritorCsvTests` | rápido |
| Que una página está en la corriente antes de pedir la siguiente | `EscritorCsvTests.Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente` | rápido |
| Las páginas de 200, todas con la foto abierta y con los criterios del listado, por las dos puertas; el `400` sin abrirla; el fallo de la primera antes del primer byte, y el de a mitad, que lanza | `LaExportacionRecorreUnaFotoTests` | rápido |
| Que la foto no ve lo que otra conexión confirma, no escribe y no se abre dentro de otra | `LaFotoDeLecturaTests` | integración |

La prueba de que el dialecto es el de la importación no la da una cadena esperada: la da el lector
de la importación. Si uno de los dos lados cambia, el fichero exportado deja de volver a entrar, y
es ahí donde se pone rojo. Cada exportación de un módulo trae sus casos: lo que sale, el permiso y
lo bloqueado, contra la base.

## Medido

Los casos de las tres clases, contados en los `.trx` de los dos carriles de la batería:

```bash
python -X utf8 - <<'PY'
import glob, collections, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
c = collections.Counter()
for f in glob.glob("artifacts/test-results/*/*.trx"):
    for r in ET.parse(f).getroot().findall(".//t:UnitTestResult", ns):
        for k in ("EscritorCsvTests", "LaExportacionRecorreUnaFotoTests", "LaFotoDeLecturaTests"):
            if f".{k}." in r.get("testName"):
                c[k, r.get("outcome")] += 1
print(c)
PY
```

Da 24 de `EscritorCsvTests`, 13 de `LaExportacionRecorreUnaFotoTests` y 3 de `LaFotoDeLecturaTests`,
todos `Passed`. El tiempo por sentencia sobre una empresa del tamaño de la C se mide cuando estén
las exportaciones, y va al PLAN con su orden.

## Alternativas descartadas

- **Una sola consulta en flujo**, con `AsAsyncEnumerable`. El criterio la descarta: el tope es de
  30 s por sentencia, y una sola sentencia lleva el listado entero, sea cual sea el tamaño de la
  empresa. Y sería otra consulta, al lado de la del listado, con su permiso y su filtro de lo
  bloqueado por duplicado.
- **Paginación por clave**, que no se degrada con la profundidad como el `OFFSET`. Cada listado se
  ordena por cualquiera de sus campos ordenables, así que la clave sería una por orden, y los casos
  de uso hablan de página y tamaño. Dentro de la foto, el `OFFSET` da las mismas filas. Si la medida
  sobre una empresa del tamaño de la C del ADR-0064 no cabe en 30 s por sentencia, se pregunta.
- **Sin foto**, en `READ COMMITTED`, que es el nivel por omisión: cada orden toma su instantánea, y
  dos páginas pueden ver dos tablas distintas.
- **`SERIALIZABLE READ ONLY DEFERRABLE`.** Espera a una instantánea segura, que solo importa cuando
  hay escrituras que podrían entrar en conflicto. Una lectura no las tiene.
- **Juntar el fichero en memoria y contestar al final**, que permitiría contestar un problema hasta
  la última página. El criterio lo descarta, y la respuesta pesaría megas por petición. El corte de
  la conexión da lo mismo que importa: que nadie se quede con un fichero corto creyendo que está
  entero.
- **El apóstrofo en todos los campos**, como decía al pie de la letra la decisión 7. Los números
  negativos dejarían de ser números en la hoja.

## Consecuencias

- **La foto deja una transacción abierta mientras dura la exportación**, con una conexión del
  grupo. En PostgreSQL eso retiene el horizonte de limpieza: lo que muere después de abrirla no se
  recoge hasta cerrarla. Son segundos, y la medida del criterio los acota.
- **Lo que se lee de otro módulo no está en la foto.** El código y el nombre de un maestro de otro
  módulo llegan por su puerto, que lee su propio contexto. Un nombre que cambia a mitad puede salir
  viejo en unas filas y nuevo en otras; las filas, que son lo que la foto protege, no.
- **Un precio unitario sale con sus seis decimales, y la importación lee cuatro.** Hoy solo se
  importan terceros, y sus números son importes, de cuatro, así que su fichero vuelve a entrar. La
  importación que llegue con seis tendrá que leerlos.
- **Un fallo a mitad deja su traza en el registro**, la de la excepción que corta la conexión, con
  la página y el código del fallo. Dentro de la foto, una página que ha salido bien no tiene por
  qué fallar en la siguiente: si pasa, es un fallo del código.
- **El día del nombre es el UTC**, como todo día de negocio, hasta que cambie la regla de
  `DiaDeNegocio`. Entre la medianoche de España y la del UTC, el fichero lleva el día anterior.
