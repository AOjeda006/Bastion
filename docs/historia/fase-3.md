# PLAN — Bastion · Fase 3 (Compras)

> **La historia de los ítems cerrados de la fase 3, movida del PLAN desde el epílogo del 3.4
> (2026-11-04).** La fase sigue abierta, así que aquí está solo lo largo de cada ítem cerrado: su
> puerta contestada, lo que el agente decidió al escribirlo y al medirlo, y su entrada de *Estado
> actual*. Es lo que `docs/PLAN.md` decía de ellos en `b79d0bf`, línea a línea y en su orden, bajo
> el encabezado de la sección de la que sale cada bloque. Se movió, no se reescribió: lo comprueba
> `python scripts/comprobar-el-plan-partido.py b79d0bf <commit>`, con el commit que creó este
> fichero, el que da `git log --diff-filter=A --format=%h -- docs/historia/fase-3.md`. Lo único
> que se añade es esta cabecera y una nota tras la tabla de las tandas del 3.2.
>
> Cada ítem que se cierre suma aquí lo suyo en su commit de cierre: sus bloques, al final de
> *Decisiones tomadas*, y su entrada, al principio de *Estado actual*, como estaban en el PLAN. El
> checklist y las notas de la fase siguen en el PLAN hasta que la fase se cierre.
> Una referencia a otra sección, como «ver *Notas / riesgos*» o «el *Checklist*», es a la del PLAN.

## Decisiones tomadas

### Tomadas por el agente de desarrollo — ítem 3.1, al escribirlo y al medirlo (2026-10-27)

Cada tema del criterio fue en su subida, como pedía el orden de trabajo, y algunos en más de un
commit: los runs y las respuestas, en `4a51ad0`; la auditoría, en `5661033`; las enmiendas, en
`a5e086b` y `de17700`; el PLAN partido, en `73adc0e` y `fa07a91`; el presupuesto, en `6af95ca`,
`571ece4`, `301a631`, `241750d` y `3ee6082`; y las cuatro notas, en `c99cd54`. Lo que se decidió
sin preguntar, porque el criterio no lo decía o porque salió al escribirlo:

1. **`npm audit fix` a secas no bastaba** (`5661033`). Subió `vite` a 7.3.7 y `undici` a 7.30.1,
   y los cuatro paquetes de `vitest` se quedaron, porque `vitest` 4.1.12 pide
   `@vitest/browser-playwright` 4.1.12 exacto, y al revés. Subieron juntos con un `npm install -D`
   de los dos, que deja los rangos en `^4.1.12`. Ninguno es versión mayor: doce paquetes cambian
   de versión, todos MIT, y no entra ni sale ninguno por nombre.
2. **La regla de las enmiendas lee también las ampliaciones** (`a5e086b`). Una declaración es una
   viñeta de la cabecera que empieza por «Enmienda» o «Amplía», con o sin etiqueta delante. Al
   leerlas salieron seis huecos más que los dos del encargo, y se cerraron en el mismo commit:
   - sin anotar en el *Estado*: el 0035 amplía el 0021, el 0043 el 0040, y el 0016 el 0011;
   - anotadas y sin nota en el punto: el 0042 al 0041, y el 0063 al 0030 y al 0036.

   Cada anotación nueva dice que es del 2026-10-26, porque el enmendado no lo decía, y ninguna
   cambia lo que el ADR decidió.
3. **Las cinco viñetas que nombran una enmienda sin declararla van en una lista cerrada**, cada
   una con su motivo, y la lista se compara entera: una que sobra o que falta es un rojo.
4. **La tanda de las enmiendas cambió el arnés dos veces** (`de17700`). La 752 salió verde, como
   se escribió, porque ningún caso tenía un *Estado* anotado sin nota en el punto: el caso
   sintético gana ese tercer estado. La 753 quitaba un filtro que nada ejercía, y el filtro se
   borra: un ADR que se nombrara a sí mismo saldría como una anotación que nadie declara.
5. **Cada bloque de la historia va a su fase** (`73adc0e`). Las decisiones, el estado y el
   checklist, por sus encabezados. Las notas cerradas, a la fase de la fecha de su etiqueta, y las
   quince sin etiqueta, del 2026-08-25, a la fase 0, con el título, el objetivo y el alcance de
   antes, que eran de la fase 0.
6. **`scripts/comprobar-el-plan-partido.py` cuenta las líneas como multiconjunto**, y lo único que
   deshace antes de comparar son los enlaces a los ADR, que en la historia ganan un `../`. Sin
   argumentos compara el commit que creó `docs/historia/` con su padre, así que se repite.
7. **`ElEstadoDelReadmeEsElDelPlanTests` gana dos casos**: el PLAN lleva casillas de una sola fase,
   y las fases con historia son exactamente las de antes de la vigente, cada una con sus casillas
   marcadas. La pareja de las casillas de la fase 0 se busca ahora en su historia.
8. **Los textos siguen en un solo espacio de nombres**, `traduccion` (`6af95ca`), así que
   `t('inventario.…')` no cambia: cambia cuándo llegan. Los trae el `lazy` de la ruta, a la vez
   que la pantalla, con el dueño que declara la tabla de rutas. El enrutador se crea en `main.tsx`
   con la promesa de la instancia, para que la pantalla y el diccionario sigan bajando a la vez.
9. **La ruta se mide con el manifiesto de Vite** (`301a631`, ADR-0071), porque en el `dist` un
   fragmento compartido no dice quién lo importa: su cierre estático, menos el arranque, más los
   textos de su dueño en el mayor de sus idiomas. El manifiesto no se sirve: `Dockerfile.web` lo
   borra con los mapas, y el humo de la imagen lo comprueba, con el `index.html` como pareja.
10. **Lo comprimido se mide con `gzip` de nivel 1**, el que usa nginx cuando su configuración no
    dice `gzip_comp_level`, y `deploy/nginx.conf` no lo dice.
11. **Las etiquetas de las descargas retenidas llevan barra** (`3ee6082`): `organizacion/es`, y no
    `organizacion.es`. `LosPermisosQueNombraElFrontalTests`, en el carril de integración, vio
    diecisiete literales con forma de permiso en los casos nuevos, y el barrido no se tocó: son
    nombres del arnés, y la barra no tiene forma de permiso. Dos aserciones que tecleaban una clave
    suelta se quitaron, porque otra del mismo caso ya las implica.
12. **Se arregló en un commit nuevo, y no reescribiendo los cuatro**, que son la base de las
    tandas de la 765 a la 791. Así que `6af95ca`, `571ece4`, `301a631` y `241750d` tienen ese caso
    en rojo, cada uno por separado. Ningún run los vio rojos, porque fueron en la misma subida que
    `3ee6082`: lo vio la batería sobre `241750d`, antes de subir.
13. **Las cuatro notas nuevas llevan la orden que comprueba lo que dicen** (`c99cd54`), y el
    disparador del encargo. Ninguna se hace ahora.
14. **La nota de la auditoría se cierra aquí y se queda en el PLAN**, con su CERRADA, hasta que la
    fase se cierre y su historia se mueva. Así se hizo en las fases de antes.
15. **La debilidad que encontraron la 790 y la 791 va a una nota abierta**, y no se arregla, porque
    ningún criterio del 3.1 la pide: *Notas / riesgos*, la quinta.

**Las cinco tandas, de la 745 a la 791, se escribieron antes de medir**, cada una sobre el commit
de su tema, en el *scratchpad*: `tanda-enmiendas.md`, `tanda-partido.md` y `tanda-presupuesto.md`,
que lleva también la de los textos y la del ADR-0071. Cada mutación se aplicó sola en el árbol
limpio, se revirtió con `git restore --source=HEAD` y se tocó con la fecha de ahora:

```bash
python -X utf8 mutar-enmiendas.py 745 746 747 748 749 750 751 752 753  # a5e086b, y de17700
python -X utf8 mutar-partido.py 754 755 756 757 758 759 760 761 762 763 764   # sobre fa07a91
python -X utf8 mutar-textos.py <salida>    # 765-775, sobre 6af95ca, 571ece4 y 3ee6082
python -X utf8 solo-768.py                 # la 768, con cada caso solo y tres rondas
python -X utf8 mutar-presupuesto.py <salida> 776 777 778 779 780 781 782 783   # sobre 301a631
python -X utf8 mutar-presupuesto.py <salida> 784 785 786 787 788
python -X utf8 canario-adr.py <salida>     # 790 y 791, sobre 241750d
```

Los carriles: las de las enmiendas y las del ADR-0071, con `dotnet test tests/Arquitectura.Tests`
filtrado a la clase `UnaEnmiendaSeAnotaEnLosDosAdrTests`; las del PLAN partido, con la clase
`ElEstadoDelReadmeEsElDelPlanTests` o con el guion que compara;
las de los textos, con `npx vitest run` entero y su informe JSON, contando los casos por nombre; y
las del presupuesto, con el guion entero dos veces, con el tope por ruta en la ruta más pesada,
173, y en 172. Las bases, en verde: las enmiendas, 0 de 8; el PLAN partido, 0 de 4, y el guion,
verde; los textos, 0 de 400 sobre `571ece4` y sobre `3ee6082`; el presupuesto, 0 con 173 y 1 con
172, por la transferencia y el recuento; y el ADR-0071, 0 de 8. Al acabar cada tanda, la porcelana,
vacía. Por los casos de los textos: (a) `una ruta trae los textos de su funcionalidad, y solo los
suyos`; (b) `mientras sus textos viajan, la pantalla no se pinta…`; (c) `cambiar de idioma trae los
textos de lo visitado…`; (d) `si el idioma cambia mientras los textos de la ruta viajan…`; y (e)
`si los textos de la ruta llegan mientras el idioma cambia…`.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 745 | El *Estado* del ADR-0007 vuelve a «aceptado» a secas | **1 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda` |
| 746 | Sin la nota del §5 del ADR-0007 | **1 de 8**: `El_enmendado_lleva_la_nota_en_el_punto_que_cambia` |
| 747 | El ADR-0016 dice «aplica el» en vez de «amplía el» | **1 de 8**: `Lo_que_anota_un_estado_lo_declara_quien_enmienda` |
| 748 | **Arnés**: la declaración ya no admite etiqueta delante | **3 de 8**: `Lo_que_anota_un_estado_lo_declara_quien_enmienda`, `Ninguna_mencion_de_una_enmienda_queda_sin_clasificar` y `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 749 | **Arnés**: «por el ADR-» pasa a «por la ADR-» | **4 de 8**: `Cada_enmienda_nombra_lo_que_enmienda`, `El_barrido_ve_las_enmiendas_y_la_pareja_anotada_en_los_dos_lados`, `El_enmendado_anota_en_su_estado_quien_lo_enmienda` y `Una_enmienda_que_el_enmendado_no_anota_se_ve` |
| 750 | **Arnés**: la cláusula no se corta nunca | **2 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda` y `El_enmendado_lleva_la_nota_en_el_punto_que_cambia` |
| 751 | Sale de la lista cerrada la mención del ADR-0055 | **1 de 8**: `Ninguna_mencion_de_una_enmienda_queda_sin_clasificar` |
| 752 | **Arnés**: el cuerpo empieza en el título, con la cabecera dentro | sobre `a5e086b`, **0 de 8**, como se escribió; sobre `de17700`, **1 de 8**: `Una_enmienda_que_el_enmendado_no_anota_se_ve`, por el tercer estado del caso sintético (punto 4) |
| 753 | **Arnés**: el número propio no se aparta | sobre `a5e086b`, **0 de 8**, como se escribió; sobre `de17700` no cambia nada, porque el filtro ya no está (punto 4) |
| 754 | Una casilla de `fase-1.md` se desmarca, la del 1.5 | **1 de 4**: `Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero` |
| 755 | Sin `fase-0.md` | **2 de 4**: el mismo y `El_checklist_del_plan_se_lee` |
| 756 | El checklist del PLAN gana una casilla marcada de la fase 2 | **1 de 4**: `El_plan_lleva_las_casillas_de_una_sola_fase` |
| 757 | `docs/historia/` gana un `notas.md` | **1 de 4**: `Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero` |
| 758 | La historia de la fase 1 gana una casilla de la fase 2 | **1 de 4**: el mismo |
| 759 | **Arnés**: la historia se busca como `fase-N.markdown` | **1 de 4**: el mismo, porque ninguna fase tiene historia |
| 760 | **Arnés**: las fases debidas son `Range(0, vigente - 1)` | **1 de 4**: el mismo |
| 761 | Una línea única, quitada de `fase-2.md` | **el guion, con 1**: falta `### Tomadas en la puerta de clarificación de la fase 2 (2026-09-18)` |
| 762 | **Arnés**: el guion no deshace el `../` de los enlaces a los ADR | **el guion, con 1**: faltan 20 líneas, las que enlazan un ADR |
| 763 | **Arnés**: el guion busca la historia en `docs/historias` | **el guion, con 1**: «el después no tiene ningún fichero en docs/historias/» |
| 764 | Una de las dos copias de una línea que el PLAN de antes tenía dos veces | **el guion, con 1**: falta una vez `- rama propia por unidad, y commits pequeños y firmados.`; cuenta, y no solo mira si está |
| 765 | `cargarTextosDe` sin el bucle: no mira si el idioma cambió | **1**: (d) |
| 766 | `cambiarIdioma` sin la última mirada antes de cambiar | **1**: (e) |
| 767 | `cambiarIdioma` no pide los textos de lo visitado | **1**: (c). Sobre `6af95ca`, 3: el hallazgo de abajo |
| 768 | El `lazy` no espera a los textos | **2**: (b), y (d) solo en la pasada entera. Con el caso solo, (b) rojo en 3 de 3 y (d) verde en 3 de 3: el hallazgo de abajo |
| 769 | El `lazy` no trae los textos | **5**: de la (a) a la (e) |
| 770 | `es.ts` vuelve a llevar un espacio de una funcionalidad | **7**: el del arranque, el barrido de los espacios, y de la (a) a la (e), porque con el espacio en el armazón nadie lo trae |
| 771 | La expresión del *linter*, con `tiposDeError\.` como único prefijo | **1**: `marca el valor desde el armazón…`, de `ElDiccionarioSeImportaPorSuTipo` |
| 772 | El armazón nombra una clave de una funcionalidad | **1**: `ningún fichero del armazón ni de otra funcionalidad nombra una clave suya` |
| 773 | **Arnés**: el barrido deja de casar las plantillas | **1**: `y la ve si alguien la nombra: desde el armazón, desde otra, y en una plantilla` |
| 774 | **Arnés**: la lista de fuentes sale vacía, con `.jsx` | **1**: `ningún fichero del armazón…`, por su pareja, `FUENTES.length > 50` |
| 775 | **Arnés**: la retención del cargador no retiene | **4**: de la (b) a la (e), los que retienen |
| 776 | El tope por ruta compara con `>=` | **con 173, 1**: la del recuento, en su tope |
| 777 | **Arnés**: el guion de las rutas sale siempre con 0 | **con 172, 0**, aunque escribe su `::error::` |
| 778 | La ruta deja de sumar los textos de su dueño | **con 172, 0**: la más pesada baja a 161 |
| 779 | El cierre no sigue las importaciones estáticas | **con 172, 0**: la más pesada baja a 29 |
| 780 | La ruta no resta el arranque | **con 173, 1**: la más pesada sube a 577 |
| 781 | El guion del arranque no pasa lo que lee del índice | **con 173, 1**: «el manifiesto pone en el arranque … y el guion no lo ha contado» |
| 782 | **Arnés**: el guion del arranque se traga la salida 1 de las rutas | **con 172, 0** |
| 783 | `build.manifest` apagado | **con 173, 1**: «no existe … manifest.json» |
| 784 | El armazón importa por su valor los textos de una funcionalidad | **con 173, 1**: «no hay fragmento de los textos de 'organizacion' en 'es'» |
| 785 | La entrada importa en diferido algo sin clasificar | **con 173, 1**: «la entrada importa en diferido src/app/sobrante.ts…» |
| 786 | Una pantalla importa algo en diferido | **con 173, 1**: «…PaginaDeAlmacenes.tsx importa algo en diferido» |
| 787 | Una pantalla que no está en `rutas.tsx` | **con 173, 1**: «Sobran: [src/features/organizacion/PaginaSobrante.tsx]» |
| 788 | Una pantalla engorda 30 000 B | **con 173, 1**: el recuento, 202 KiB |
| 789 | `Dockerfile.web` sin el `rm` del manifiesto | **a mano, con Docker**: la imagen construida con `Dockerfile.web.sin-rm`, del *scratchpad*, tiene `/usr/share/nginx/html/.vite`, y la de `Dockerfile.web`, no, con el `index.html` en su sitio |
| 790 | El 0036 pierde de su *Estado* la frase del 0071 | **1 de 8**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda`, en `ADR-0071 → ADR-0036` |
| 791 | El 0028 pierde sus dos notas del 0071 | **1 de 8**: `El_enmendado_lleva_la_nota_en_el_punto_que_cambia`, en `ADR-0071 → ADR-0028` |

- **Lo que salió distinto de lo escrito, y por qué**:
  - **la 752 y la 753, verdes, como se escribió**, y cada una dejó su cambio en `de17700`: el
    punto 4;
  - **la 767 dio tres rojos sobre `6af95ca`, y no uno**. El `afterEach` soltaba lo que un caso
    rojo había dejado retenido, y eso corría dentro del caso siguiente. Arreglado en `571ece4`: lo
    retenido se olvida sin soltarse;
  - **la 768, con el caso solo, salía verde sobre `6af95ca`**: la pantalla tardaba más en
    compilarse que los textos en llegar. Desde `571ece4`, el caso carga antes la pantalla, y afirma
    que el enrutador no ha terminado su primera carga. El rojo de (d) en la pasada entera es la
    pantalla caliente que deja (b), y no su sujeto;
  - **la 784 y la 788 no compilaban** en su primera forma: `tsc -b` pide `['…']`, por
    `noPropertyAccessFromIndexSignature`. Se reescribieron y se midieron en la segunda pasada;
  - **la 790 y la 791 salieron verdes en su primera forma, por diseño**, y no cuentan. Una cambiaba
    «enmendado» por «corregido» en el *Estado* del 0036, y la viñeta seguía diciendo «Ampliado» por
    el 0063 y nombrando «por el ADR-0071». La otra quitaba una sola de las dos notas del 0028, y la
    regla pide la nota en el cuerpo, no en qué punto. Se reescribieron sobre el sujeto. La primera
    es la nota abierta del punto 15;
  - **el barrido de permisos del carril de integración vio lo que el carril rápido no ve**: un
    literal de un caso del frontal con forma de permiso. Es el punto 11, y por eso la tanda de los
    textos se midió otra vez sobre `3ee6082`, con los mismos rojos, caso por caso
    (`tanda-textos-3.log`).

La siguiente mutación es la **792**, y el siguiente ADR, el **0072**.

### La puerta del 3.2, contestada (2026-10-27)

El criterio deja abiertas cuatro cosas con más de una salida, y se preguntaron juntas antes de
escribir nada. Las cuatro respuestas son la opción recomendada:

1. **Lo que en pantalla es un maestro de otro módulo va con su código y su nombre, por puertos.**
   El artículo, el almacén, la ubicación, la unidad, el impuesto y la divisa van en dos columnas,
   código y nombre, como los enseña la pantalla. Los resuelve un método nuevo por lotes en el
   `Contracts` de su dueño, una vez por página interna. Sin el permiso de ver ese maestro va su
   identificador, como en la pantalla, que lo pide a su dueño solo con permiso (ADR-0055 §12).
2. **Terceros sale con la cabecera de su importación**: las dieciocho columnas de
   `ImportacionDeTerceros.Cabecera`, en su orden y con el límite de crédito, para que un fichero
   exportado se vuelva a importar.
3. **El navegador descarga con `fetch` y un `Blob`**, por el cliente de siempre, con su reintento
   tras un `401`. El servidor manda en flujo, y el navegador junta el fichero antes de guardarlo.
4. **La cabecera de los demás va en `snake_case`**, como la de la importación: sin tildes, sin
   traducir, y la escribe el servidor.

Lo que no se preguntó, porque tiene un precedente o una salida obvia, y se puede cambiar sin tocar
datos:

- **La ruta es hermana del listado**, `GET …/{recurso}/exportacion`, con sus mismos parámetros
  salvo `page` y `size`, como la importación es `POST /api/v1/terceros/terceros/importacion`;
- **los valores van en el dialecto de la importación**: importes y cantidades con coma decimal,
  sin punto de miles y sin ceros a la derecha; `sí` y `no`; las fechas, en `AAAA-MM-DD`; y los
  estados y los tipos, con el valor del DTO, sin traducir. El fin de línea es CRLF (RFC 4180);
- **la regla del apóstrofo va sobre los campos de texto.** Un número lo escribe Bastion, y un
  `-12,5` con apóstrofo dejaría de ser un número;
- **todas las páginas internas salen de la misma foto**: una transacción de solo lectura en
  `REPEATABLE READ`, en el contexto del listado. Así, una fila que entra a mitad no empuja a otra
  a salir dos veces;
- **lo que el listado rechaza, la exportación lo rechaza antes del primer byte.** La primera
  página se pide antes de escribir la cabecera, y un `4xx` sale como en el listado;
- **el fichero se llama `<recurso>-<AAAA-MM-DD>.csv`**, con el día de negocio.

### La pregunta del apóstrofo, contestada (2026-10-27)

Salió al escribir la exportación de terceros, y se preguntó porque el dato no volvía igual: la
importación guardaba el apóstrofo que la exportación había puesto, y «=Foo» volvía como «'=Foo».
La respuesta es la recomendada:

5. **El apóstrofo hace el viaje de vuelta.** Un texto que empieza por `=`, `+`, `-`, `@`, un
   tabulador, un retorno **o un apóstrofo** sale con un apóstrofo delante, y la importación de
   terceros quita uno solo, el primero, cuando lo sigue uno de esos siete. «=Foo» sale «'=Foo» y
   vuelve «=Foo»; «'=Foo» sale «''=Foo» y vuelve «'=Foo». Las salidas eran (a) esta; (b) que la
   importación lo quite sin que cambie el escritor, y un valor que empieza por «'=» perdería su
   apóstrofo; (c) dejarlo y escribirlo; (d) terceros sin apóstrofo, contra la decisión 7 del 1.11.
   Lo recoge el **ADR-0075**, que enmienda el §6 del 0073 y el §5 del 0034.

### Tomadas por el agente de desarrollo — ítem 3.2, al escribirlo y al medirlo (2026-10-28)

Cada tema del orden de trabajo fue en su subida, y algunos en más de un commit: la puerta, en
`bb5d843`; las tres piezas, en `ace1b7c` y `3a03002`; el mecanismo, en `823fa7f`, `f1c3244` y
`619190b`; los puertos, en `b1e726f`, `0e16aa6` y `f0f1909`; Organización, en `77568cd` y
`a995ed0`; el apóstrofo, en `c4c4650` y `18c29ae`; Terceros, en `b81c67c` y `de5d431`; Catálogo,
en `9b31c16`; Inventario, en `3a78bda` y `0b7661c`; y el botón, en `f912e12` y `a6666ea`. Lo que
se decidió sin preguntar, porque el criterio no lo decía o porque salió al escribirlo:

1. **Un listado enlaza tres piezas, y el texto, solo si lo mira** (`ace1b7c`, ADR-0072): la página,
   `PaginaPedida`; el orden con sus criterios, `ConsultaDeListado`, que antes se llamaba
   `ConsultaPaginada`; y el texto, `FiltroDeTexto`. Diecisiete lo enlazan y seis no: las líneas de
   tarifa, el saldo, las transferencias, las conversiones de unidades, los ejercicios y los tipos
   de cambio, que dejan de publicar `q`. Un `?q=` con algo escrito a uno de los seis es un `400`
   `filtro-no-admitido`, con `errors.q`, y en blanco pasa. Lo decide `RespuestasDeListado`, antes
   que el orden, y el paginador lanza si le llega un texto sin filtro declarado. El ADR enmienda
   los puntos 1 y 3 del 0069 y el «Sin `?q=`» del §1 del 0068.
2. **El `SET TRANSACTION READ ONLY` de la foto es SQL crudo, y entra entre los saltos de
   `ElFiltroNoSeSaltaPorAhiTests`** (`823fa7f`), con su argumento: no lee ni escribe ninguna fila.
   La batería lo encontró. Esquivar la regla con un `DbCommand` a mano habría escondido el SQL
   crudo de la misma regla que lo vigila.
3. **Las dos puertas de `ExportarAsync`, la del `IListado` y la de la función, no las ejercía
   ningún caso**, y se añadió uno antes de la tanda, que ven la 826 y la 827.
4. **Dos guardas que repetían a otro se quitaron** (`619190b`): la del cero negativo, que el formato
   de .NET ya escribe «0» —medido con `dotnet fsi`: `decimal.Parse("-0.0")` guarda el signo, con los
   bits `0x80010000`, y se escribe «0»—, y la de la foto dentro de otra transacción, que EF Core ya
   rechaza con una `InvalidOperationException`. La 828 y la 829 miden que los casos ven lo que las
   guardas decían defender.
5. **Un maestro de otro módulo se nombra con lo que enseña su ficha** (`b1e726f`, ADR-0074): los
   seis puertos estrenan `NombresDeAsync`, con los filtros del contexto puestos y sin abrir el
   ámbito del art. 32. El almacén y la ubicación bloqueados no vuelven, y la ubicación de un almacén
   bloqueado sí, porque su ficha no une el almacén; lo retirado y los tramos que ya no rigen,
   también. El permiso lo mira quien pregunta, y no el puerto. `NombreDeMaestro` es un registro
   común en el bloque de contratos, y con él `Catalogo.Contracts` estrena su primera arista. El ADR
   enmienda el §12 del 0055.
6. **El «Cómo se vigila» del ADR-0074 decía algo que no se cumplía** entre `0e16aa6` y `f0f1909`:
   que un puerto que devolviera todo no pasaba. Pasaba en cuatro de seis, porque cada caso
   preguntaba por todos los suyos y, corrido solo, no hay otros en la base. El ADR no se tocó: lo
   cumplen los casos desde `f0f1909`, con uno de cada fuera del lote. La divisa de fuera es el
   EUR, porque el catálogo solo tiene cinco y las otras cuatro ya son de casos de ese carril.
7. **Las columnas de Organización son lo que publica el DTO, sin sus identificadores** (`77568cd`).
   La dirección va en seis columnas, con los nombres de la importación de terceros y el prefijo de
   quien la lleva (`ColumnasDeDireccion`). Los valores de un conjunto —el tipo del almacén, la
   divisa, el régimen de IVA— van sin traducir y sin apóstrofo, con `CampoCsv.Valor`, porque no
   los escribió una persona. **La exportación de empresas es la de la empresa activa** (R8), como su
   listado, y no el padrón de la instalación: la otra no sale, y con la propia bloqueada sale solo
   la cabecera.
8. **La foto de cada módulo se mide contra la base** (`a995ed0`), porque la tanda dejó verde la
   858. `LaFotoDeCadaModuloEsUnaFotoTests` resuelve del *host* la foto de cada exportación, la abre
   y pregunta en el contexto del módulo el nivel y si es de solo lectura, con la misma pregunta sin
   foto como pareja. Las exportaciones se reconocen en un solo sitio, `ExportacionesDeLaApi`, en
   `tests/Comun`, para que la hermana del listado y este caso no puedan ver listas distintas.
9. **El apóstrofo es el séptimo disparador** (`c4c4650`, ADR-0075, la respuesta 5):
   `ApostrofoDelDialecto`, en `BuildingBlocks.Application`, lleva la regla en los dos sentidos.
   Los sí y no y los importes se leen tal como llegan, así que «'-5» sigue siendo
   `formato-no-valido`. La vuelta se afirma sobre un universo de 7381 textos, con su tamaño
   afirmado.
10. **Terceros sale con el límite de crédito de la misma fila** (`b81c67c`): `ListarTerceros`
    implementa también `IListarTercerosConLimite`, sobre la misma lectura del repositorio. No
    salen el identificador, la empresa ni la verificación del identificador fiscal, que la
    importación no lee. La prueba es el viaje entero: se exporta de una empresa, se importa en otra
    el fichero tal como bajó, con su BOM, y la segunda exportación es la primera, byte a byte. Lo
    que envuelve el fichero —el BOM, el tipo y el nombre del adjunto— pasa a `Exportaciones.cs`,
    compartido.
11. **Cada «sí» y «no» de la importación, en su campo** (`de5d431`): la 879, el recargo siempre en
    «no», solo la veía el viaje de ida y vuelta contra la base. `CadaSiNoDeLaFilaVaASuCampoTests`
    decide una fila por cada combinación con un solo «sí» entre las tres columnas fiscales y uno
    entre cliente y proveedor, así que dos columnas cambiadas tienen una fila en la que difieren.
12. **Un maestro de otro módulo se nombra solo con el permiso de verlo; uno del propio listado, sin
    mirar** (`9b31c16`): la unidad, el impuesto, la divisa y la categoría del artículo, con el suyo,
    y sin él sale su identificador, como en la pantalla; la madre de una categoría y el artículo de
    un código de barras, con el de la exportación. `MaestroCitado`, `ColumnasDeMaestro` y
    `NombresDeMaestros` van a `BuildingBlocks`, y cada exportación es un caso de uso, `IExportarX`.
13. **Lo que no tiene listado paginado se parte en tramos**: los códigos de barras de un artículo
    (`9b31c16`) y las líneas de una transferencia (`3a78bda`), que van dentro de su ficha. Cada una
    contesta el `404` de lo que la contiene, rechaza un `?q=` y no lee `?sort=`, como su pantalla.
    La hermana del listado las declara aparte, en `s_sinPaginar`, los listados, y en
    `s_deUnaFicha`, las exportaciones. Se pone roja si un listado sin paginar no está declarado o
    uno declarado se pagina, y si una exportación sin listado no está declarada o una declarada
    tiene listado.
14. **Inventario pregunta los cuatro maestros con su permiso, en `LosNombresDeLosMaestros`**
    (`3a78bda`), porque ninguno es suyo, y tiene foto propia, `IFotoDeInventario`, porque el saldo
    se lee con SQL crudo en su contexto. Cada línea de una transferencia lleva la divisa de la
    transferencia, porque su valor sin ella no se lee. No salen la transferencia que anula ni la
    línea de ajuste de un recuento: son el identificador de otro documento, sin código que escribir.
15. **El saldo fija el día antes de la primera página**: sin `?fecha=` es hoy, y un fichero que
    cruzara la medianoche no lleva dos días en la misma columna. Una fecha futura es el `400` de la
    consulta. **El nombre del fichero lleva el día de la exportación, y no el de `?fecha=`**: es el
    `<recurso>-<AAAA-MM-DD>.csv` de la puerta, y el día pedido va en la columna `fecha`.
16. **`ConPermisosEnAsync` da la cuenta recortada en la empresa de la escena** (`3a78bda`), que es
    la única suya, y lo afirma al entrar: la 946 lo ve. Las acciones del borde pasan de 158 a 171,
    lecturas que no cambian estado, y el inventario de la idempotencia sube el total y nada más.
17. **El botón sale solo con filas** (`f912e12`, y `a6666ea`): «Exportar a CSV» pide la exportación
    con una mutación de la caché, se apaga mientras llega, lo dice con un `role="status"` y, si
    falla, enseña la frase del motivo y deja reintentar. La exportación arma su consulta con el
    mismo `filtrosDe` que el listado, tipado del contrato y sin página ni tamaño; la de saldos manda
    el día que contestó el listado, no el del campo; y la de un recuento cerrado exporta todas sus
    líneas, como las enseña, aunque el enlace traiga una vista.
18. **El nombre del fichero lo pone el servidor**: `ficheroDe` lee el `Content-Disposition`, con
    `filename*` primero (RFC 6266), y sin ninguno guarda `exportacion.csv`. Un rechazo sale por el
    mismo `FalloDeApi` que un listado, con su `type` y su traza, porque el cliente lee el cuerpo
    del error como JSON aunque se pida un `blob`. `guardarFichero` revoca la URL `blob:` en el acto,
    tras el `click()`, porque la descarga lee el `href` dentro de esa misma llamada.
19. **Los casos del frontal pulsan el botón en la pantalla montada**, con `exportarYComprobar` de
    `frontend/src/pruebas/exportaciones.ts`, que contesta con `servidor.use` y pone en el nombre el
    1999-12-31, un día que el navegador no puede haber calculado: que el fichero se guarde con él
    demuestra que el nombre sale de la cabecera. Afirman la petición, la consulta entera, el nombre
    y los bytes, con el BOM. Y un censo, `CadaExportacionTieneSuBoton`, exige que las exportaciones
    del contrato, las que pide el frontal y las que pulsa algún caso sean las mismas trece.
20. **La guarda de «sin líneas» de la ficha de la transferencia se quitó** (`a6666ea`), porque la
    966 salió verde y no podía salir de otra forma: una transferencia no se abre sin líneas
    (`transferencia-sin-lineas`), y ninguna operación se las quita. El comentario dice quién lo
    impide.

**Las tandas, de la 792 a la 971, se escribieron antes de medir**, una por tema y sobre su commit,
alguna con una segunda pasada sobre el commit que cubría lo que la primera dejó en verde, y cada
mutación con su «espera», en el *scratchpad*: `tandaNN.json` y un guion `mutarNN.py` por tanda.
Cada mutación se aplicó sola sobre el árbol limpio de un *worktree* (`wt-mut32` y `wt-mut33`; la
del frontal, sobre el repositorio, con el árbol limpio), se revirtió con
`git restore --source=HEAD`, se tocó con la fecha de ahora, y al acabar cada tanda el árbol
restaurado compiló y la porcelana salió vacía. Los registros, `tandaNN.log`:

```bash
python -X utf8 mutar32.py tanda32.json base 792-802            # sobre 3a03002
python -X utf8 mutar33.py tanda33.json base 803-827            # sobre f1c3244
python -X utf8 mutar33.py tanda33b.json base 828-829           # sobre 619190b
python -X utf8 mutar34.py tanda34.json base 830-843            # sobre 0e16aa6
python -X utf8 mutar35.py tanda35.json 838 844-848             # sobre 0e16aa6
python -X utf8 mutar36.py tanda35.json base 838 844-848        # sobre f0f1909
python -X utf8 mutar37.py tanda37.json base 849-861            # sobre 77568cd
python -X utf8 mutar38.py tanda38.json base 851 858 862 863    # sobre a995ed0, y la 862 otra vez
python -X utf8 mutar39.py tanda39.json base 864-872            # sobre c4c4650
python -X utf8 mutar40.py tanda40.json base 873-882            # sobre b81c67c
python -X utf8 mutar41.py tanda41.json base 883-887            # sobre de5d431
python -X utf8 mutar42.py tanda42.json base 888-911            # sobre 9b31c16; la 912, con tanda42b
python -X utf8 mutar43.py tanda43.json base 913-946            # sobre 3a78bda
python -X utf8 mutar43b.py tanda43b.json base 926 930 942r     # sobre 0b7661c
python -X utf8 mutar44-web.py base 947-971                     # sobre f912e12
python -X utf8 mutar44-web.py base 965 967                     # sobre a6666ea
```

Los carriles son `dotnet test` de un proyecto, sin compilar y filtrado a las clases que la tanda
puede ver, y en el frontal, `vitest run` sobre los quince ficheros del botón, con los rojos por
nombre y los avisos de `act()` de los siete trozos del canal (ADR-0066). En la tabla, `fun` es
`Api.FunctionalTests`; `int`, `Api.IntegrationTests`; `org`, `Organizacion.IntegrationTests`;
`bb`, `BuildingBlocks.UnitTests`; `ter`, `cat` e `inv`, los `UnitTests` de Terceros, Catálogo e
Inventario; `arq`, `Arquitectura.Tests`; y `web`, el frontal. Las bases, todas a cero, con los casos
de cada carril:

- la 32: `fun` 263, `org` 4, e `int` 9 y 20, en dos filtros;
- la 33 y su segunda pasada: `bb` 256, `fun` 276 y `org` 3;
- la 34 y la 35, antes y después: `org` 3, `int` 4 y `arq` 67;
- la 37: `fun` 3, `int` 4 y `bb` 27; y la 38, con el caso nuevo de la foto, `int` 5;
- la 39: `bb` 128, `ter` 88 e `int` 14;
- la 40: `fun` 11, `ter` 88 e `int` 7; y la 41, con la teoría nueva, `ter` 93;
- la 42: `fun` 11, `cat` 271, `bb` 39 e `int` 12;
- la 43 y su segunda pasada: `fun` 11, `cat` 271, `bb` 39, `inv` 14 e `int` 16;
- y la 44 y la suya: `web` 174, en 15 ficheros, con 0 avisos de `act()` y 0 plazos agotados, en
  la base y en cada una de las 27 pasadas.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 792 | El borde deja pasar un `?q=` escrito a un listado sin texto | **fun 2**: `Un_orden_que_no_se_admite_sigue_siendo_su_400` y `Un_texto_escrito_a_un_listado_sin_texto_es_un_400_con_su_codigo`; **int 2**: `Cada_listado_contesta_al_texto_segun_lo_publique` y `La_lista_va_de_la_ultima_fecha_de_envio_a_la_primera_se_acota_por_cada_punta_y_no_trae_la_descartada` |
| 793 | El texto no llega al listado que lo mira (`Filtro = null`) | **fun 1**: `Un_listado_con_texto_lo_compone_con_la_pagina_y_el_orden`; **int 4**: `Abrir_con_numeros_de_serie_precarga_una_linea_por_unidad`, `La_lista_acota_por_estado_por_almacen_y_por_motivo`, `Las_lineas_se_paginan_se_ordenan_y_se_acotan_en_el_servidor` y `La_lista_va_de_la_ultima_fecha_a_la_primera_se_acota_y_no_trae_el_descartado` |
| 794 | Un `?q=` en blanco cuenta como escrito | **fun 1**: `Sin_texto_escrito_un_listado_sin_texto_compone_la_pagina`; **int 2**: `Cada_listado_contesta_al_texto_segun_lo_publique` y `La_lista_va_de_la_ultima_fecha_de_envio_a_la_primera_se_acota_por_cada_punta_y_no_trae_la_descartada` |
| 795 | El paginador no lanza con un texto y sin filtro declarado | **org 1**: `Un_texto_sin_filtro_declarado_lanza_antes_de_llegar_a_la_base` |
| 796 | Un listado sin filtro de texto lo enlaza | **fun 1**: `Los_listados_sin_filtro_de_texto_son_los_seis_declarados`; **int 1**: `Cada_listado_contesta_al_texto_segun_lo_publique` |
| 797 | Un listado con texto cuyo repositorio no declara el filtro | **int 1**: `Cada_listado_contesta_al_texto_segun_lo_publique` |
| 798 | **Arnés**: el barrido toma por listado cualquier verbo, no solo `GET` | **fun 2**: `El_barrido_ve_listados_con_texto_y_sin_texto` y `Los_listados_sin_filtro_de_texto_son_los_seis_declarados`; **int 1**: `Cada_listado_contesta_al_texto_segun_lo_publique` |
| 799 | **Arnés**: el parámetro del texto se busca como `qq` | **fun 2**: `El_barrido_ve_listados_con_texto_y_sin_texto` y `Los_listados_sin_filtro_de_texto_son_los_seis_declarados`; **int 1**: `Cada_listado_contesta_al_texto_segun_lo_publique` |
| 800 | El texto se decide antes que el orden | **fun 1**: `Un_orden_que_no_se_admite_sigue_siendo_su_400` |
| 801 | El paginador no lanza con un texto en blanco | **org 1**: `Un_texto_sin_filtro_declarado_lanza_antes_de_llegar_a_la_base` |
| 802 | Un listado con texto deja de enlazarlo | **fun 1**: `Los_listados_sin_filtro_de_texto_son_los_seis_declarados` |
| 803 | El escritor no vacía la corriente al final de cada página | **bb 1**: `Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente` |
| 804 | Sin la foto: la exportación no abre transacción | **org 1**: `Dentro_de_la_foto_no_se_ve_lo_que_otra_conexion_confirma_despues` |
| 805 | La foto no es de solo lectura | **org 1**: `Dentro_de_la_foto_no_se_puede_escribir` |
| 806 | Sin el apóstrofo delante de una fórmula | **bb 2**: `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor` y `Un_texto_que_parece_una_formula_sale_con_un_apostrofo_delante` |
| 807 | El apóstrofo, en todos los campos | **bb 4**: `El_apostrofo_solo_lo_lleva_el_texto_que_empieza_por_uno_de_los_seis`, `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf`, `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor` y `Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha` |
| 808 | Una página interna de más (`<=` por `<`) | **fun 1**: `Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta` |
| 809 | Las páginas internas, de 50 y no del tamaño máximo | **fun 4**: `Cada_pagina_lleva_el_orden_y_el_texto_del_listado`, `Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta`, `Un_fallo_a_mitad_lanza_con_el_fichero_empezado_y_cierra_la_foto` y `Un_listado_que_no_falla_entra_por_su_interfaz_con_sus_criterios` |
| 810 | La foto se abre antes de mirar los criterios | **fun 2**: `Un_orden_que_no_se_admite_es_su_400` y `Un_texto_que_el_listado_no_mira_es_su_400_sin_abrir_la_foto_ni_escribir` |
| 811 | Las cabeceras del adjunto, antes de pedir la primera página | **fun 1**: `Un_fallo_en_la_primera_pagina_sale_como_en_el_listado_antes_del_primer_byte` |
| 812 | Sin el BOM | **bb 2**: `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf` y `Sin_elementos_el_fichero_es_el_bom_y_la_cabecera`; **fun 2**: `El_fichero_se_llama_por_el_recurso_y_el_dia_de_negocio` y `Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta` |
| 813 | LF en vez de CRLF | **bb 3**: `Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente`, `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf` y `Sin_elementos_el_fichero_es_el_bom_y_la_cabecera`; **fun 3**: `Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta`, `Un_fallo_a_mitad_lanza_con_el_fichero_empezado_y_cierra_la_foto` y `Un_listado_que_no_falla_entra_por_su_interfaz_con_sus_criterios` |
| 814 | El cero con signo: sin la rama que lo quita | **ninguno**, en los tres carriles: la guarda estaba muerta, porque el formato ya escribe «0» del cero negativo. Se quitó en `619190b`, y lo mide la 829 |
| 815 | La foto, sin la guarda de «dentro de otra transacción» | **ninguno**, en los tres carriles: EF Core ya lanza al abrir una transacción con otra abierta. Se quitó en `619190b`, y lo mide la 828 |
| 816 | Un fallo a mitad cierra el fichero, corto, en vez de lanzar | **fun 1**: `Un_fallo_a_mitad_lanza_con_el_fichero_empezado_y_cierra_la_foto` |
| 817 | El fallo de la primera página no se contesta | **fun 1**: `Un_fallo_en_la_primera_pagina_sale_como_en_el_listado_antes_del_primer_byte` |
| 818 | El adjunto, `inline` | **fun 1**: `El_fichero_se_llama_por_el_recurso_y_el_dia_de_negocio` |
| 819 | Un salto de línea sin comillas | **bb 1**: `Un_campo_va_entre_comillas_si_lleva_separador_comilla_o_salto` |
| 820 | Punto decimal en vez de coma | **bb 3**: `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf`, `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor` y `Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha` |
| 821 | El texto, sin tildes | **bb 3**: `Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente`, `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf` y `Los_demas_tipos_salen_en_la_forma_del_dialecto` |
| 822 | Los números, con cuatro decimales | **bb 1**: `Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha` |
| 823 | **Arnés**: la otra conexión de `LaFotoDeLecturaTests` no confirma nada | **org 1**: `Dentro_de_la_foto_no_se_ve_lo_que_otra_conexion_confirma_despues` |
| 824 | **Arnés**: el caso de la corriente no mira lo escrito al pedir la página | **bb 1**: `Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente` |
| 825 | **Arnés**: las páginas del listado de pega repiten filas | **fun 1**: `Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta` |
| 826 | La puerta del `IListado` pierde el orden | **fun 1**: `Un_listado_que_no_falla_entra_por_su_interfaz_con_sus_criterios` |
| 827 | La puerta del `IListado` pierde el texto | **fun 1**: `Un_listado_que_no_falla_entra_por_su_interfaz_con_sus_criterios` |
| 828 | La foto se cuela en una transacción abierta (sobre `619190b`) | **org 1**: `Una_foto_no_se_abre_dentro_de_otra_transaccion` |
| 829 | El cero negativo sale «-0» (sobre `619190b`) | **bb 1**: `Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha` |
| 830 | El almacén abre el ámbito de lo bloqueado | **org 1**: `El_almacen_se_nombra_y_ni_el_bloqueado_ni_el_ajeno_ni_el_inventado_vuelven` |
| 831 | La ubicación abre el ámbito de lo bloqueado | **org 1**: `La_ubicacion_se_nombra_por_su_propio_bloqueo_y_no_por_el_de_su_almacen` |
| 832 | La ubicación une su almacén, y la de una nave cerrada no vuelve | **org 1**: `La_ubicacion_se_nombra_por_su_propio_bloqueo_y_no_por_el_de_su_almacen` |
| 833 | El artículo, sin el filtro de empresa | **int 1**: `El_codigo_y_la_descripcion_salen_por_lotes_y_los_de_otra_empresa_no_vuelven` |
| 834 | El artículo se nombra con otra columna | **int 1**: `El_codigo_y_la_descripcion_salen_por_lotes_y_los_de_otra_empresa_no_vuelven` |
| 835 | La unidad retirada no vuelve | **org 1**: `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien` |
| 836 | El tramo de impuesto cerrado no vuelve | **org 1**: `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien` |
| 837 | La divisa retirada no vuelve | **org 1**: `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien` |
| 838 | La unidad, sin el `Contains` del lote: vuelven todas | sobre `0e16aa6`, **ninguno**, dos veces; sobre `f0f1909`, **org 1**: `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien` (punto 6) |
| 839 | **Arnés**: el caso no bloquea el almacén | **org 1**: `El_almacen_se_nombra_y_ni_el_bloqueado_ni_el_ajeno_ni_el_inventado_vuelven` |
| 840 | El ADR-0055, sin la nota en su §12 | **arq 1**: `El_enmendado_lleva_la_nota_en_el_punto_que_cambia` |
| 841 | El ADR-0055, sin la anotación en su *Estado* | **arq 1**: `El_enmendado_anota_en_su_estado_quien_lo_enmienda` |
| 842 | El ADR-0074 deja de declarar la enmienda | **arq 1**: `Lo_que_anota_un_estado_lo_declara_quien_enmienda` |
| 843 | El inventario de arquitectura, sin la arista nueva de `Catalogo.Contracts` | **arq 1**: `Las_referencias_de_proyecto_son_las_declaradas` |
| 844 | El almacén, sin el `Contains` del lote | sobre `0e16aa6` y sobre `f0f1909`, **org 1**: `El_almacen_se_nombra_y_ni_el_bloqueado_ni_el_ajeno_ni_el_inventado_vuelven`, por su primera pregunta |
| 845 | La ubicación, sin el `Contains` del lote | sobre `0e16aa6` y sobre `f0f1909`, **org 1**: `La_ubicacion_se_nombra_por_su_propio_bloqueo_y_no_por_el_de_su_almacen`, por su primera pregunta |
| 846 | El artículo, sin el `Contains` del lote | sobre `0e16aa6`, **ninguno**; sobre `f0f1909`, **int 1**: `El_codigo_y_la_descripcion_salen_por_lotes_y_los_de_otra_empresa_no_vuelven` |
| 847 | El impuesto, sin el `Contains` del lote | sobre `0e16aa6`, **ninguno**; sobre `f0f1909`, **org 1**: `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien` |
| 848 | La divisa, sin el `Contains` del lote | sobre `0e16aa6`, **ninguno**; sobre `f0f1909`, **org 1**: el mismo |
| 849 | Sin el registro de `IFotoDeOrganizacion` | **fun 1**: `Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye`; **int 4**: `La_exportacion_lleva_el_orden_y_el_texto_del_listado`, `Las_empresas_salen_con_el_texto_del_listado_sin_las_ajenas_y_sin_la_bloqueada`, `Los_almacenes_salen_enteros_en_el_dialecto_y_sin_el_bloqueado` y `Mas_de_una_pagina_sale_entera_y_en_el_orden_de_las_paginas_del_listado` |
| 850 | La exportación de almacenes, con el permiso de otra acción | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 851 | La exportación de almacenes, sin el texto | sobre `77568cd`, **no compiló** (CS1572: el `<param>` del texto se quedaba); sin el comentario, sobre `a995ed0`, **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado`, e **int 1**: `La_exportacion_lleva_el_orden_y_el_texto_del_listado` |
| 852 | Un valor de un conjunto, con la regla del apóstrofo | **bb 1**: `El_apostrofo_solo_lo_lleva_el_texto_que_empieza_por_uno_de_los_seis` |
| 853 | La calle, con otro nombre de columna | **bb 1**: `Las_columnas_son_las_seis_de_la_importacion_con_el_prefijo`; **int 2**: `Las_empresas_salen_con_el_texto_del_listado_sin_las_ajenas_y_sin_la_bloqueada` y `Los_almacenes_salen_enteros_en_el_dialecto_y_sin_el_bloqueado` |
| 854 | La población, con el campo de la subdivisión | **bb 1**: `Cada_columna_lleva_su_campo_como_texto` |
| 855 | El tipo del almacén, con otro campo | **int 1**: `Los_almacenes_salen_enteros_en_el_dialecto_y_sin_el_bloqueado` |
| 856 | Las empresas, sin una columna | **int 1**: `Las_empresas_salen_con_el_texto_del_listado_sin_las_ajenas_y_sin_la_bloqueada` |
| 857 | El contrato de las empresas, con otro tipo de contenido | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 858 | La foto de Organización, una transacción cualquiera | sobre `77568cd`, **ninguno**, en los tres carriles; sobre `a995ed0`, **int 1**: `La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read`, el caso nuevo (punto 8) |
| 859 | Una exportación sin declarar en la lista cerrada | **fun 3**: `Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye`, `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` y `Las_exportaciones_son_las_declaradas` |
| 860 | **Arnés**: la página se llama `pagina` en la hermana | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 861 | **Arnés**: la hermana busca `/exportaciones` | **fun 3**: `Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye`, `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` y `Las_exportaciones_son_las_declaradas` |
| 862 | La foto de Organización en `REPEATABLE READ`, pero de escritura | **int 1**: `La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read`. No compiló a la primera (CS1501: la sobrecarga con el nivel es una extensión) |
| 863 | **Arnés**, el compartido: `ExportacionesDeLaApi` busca `/exportaciones` | **fun 3**: `Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye`, `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` y `Las_exportaciones_son_las_declaradas`; **int 1**: `La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read` |
| 864 | Seis disparadores, sin el apóstrofo | **bb 4**: `Lo_que_empieza_por_uno_de_los_siete_sale_con_un_apostrofo_y_vuelve`, `Todo_texto_sale_y_vuelve_igual`, `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor` y `Un_texto_que_parece_una_formula_sale_con_un_apostrofo_delante`; **ter 1**: `Los_textos_vuelven_sin_el_apostrofo_que_les_puso_la_exportacion` |
| 865 | La importación no quita el apóstrofo | **ter 1**: `Los_textos_vuelven_sin_el_apostrofo_que_les_puso_la_exportacion` |
| 866 | La importación quita el apóstrofo de un importe | **ter 1**: `Un_importe_con_apostrofo_no_se_lee` |
| 867 | La importación quita cualquier apóstrofo | **bb 1**: `Un_apostrofo_que_no_puso_la_exportacion_se_queda`; **ter 1**: `Un_apostrofo_que_no_puso_la_exportacion_se_queda` |
| 868 | La importación quita todos los de delante (`TrimStart`) | **bb 3**: `Lo_que_empieza_por_uno_de_los_siete_sale_con_un_apostrofo_y_vuelve`, `Todo_texto_sale_y_vuelve_igual` y `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor`; **ter 1**: `Los_textos_vuelven_sin_el_apostrofo_que_les_puso_la_exportacion` |
| 869 | No quita el de un campo de dos caracteres (`Length > 2`) | **bb 2**: `Lo_que_empieza_por_uno_de_los_siete_sale_con_un_apostrofo_y_vuelve` y `Todo_texto_sale_y_vuelve_igual` |
| 870 | El apóstrofo, a todo texto | **bb 7**: los del escritor que leen un texto y los dos del universo; e **int 4**: las cuatro de Organización |
| 871 | **Arnés**: el alfabeto del universo, sin el apóstrofo | **bb 1**: `Todo_texto_sale_y_vuelve_igual` |
| 872 | El apóstrofo, también a un número | **bb 4**: `El_apostrofo_solo_lo_lleva_el_texto_que_empieza_por_uno_de_los_siete`, `El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf`, `El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor` y `Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha` |
| 873 | Sin el registro de `IFotoDeTerceros` | **fun 1**: `Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye`; **int 3**: `La_exportacion_lleva_el_orden_y_el_texto_del_listado_y_no_busca_por_el_identificador`, `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` y `La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read` |
| 874 | La columna de cliente, con el valor de proveedor | **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 875 | El límite de crédito no sale | **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 876 | El territorio, siempre el de la península | **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 877 | La exportación de terceros, sin el texto del listado | **int 1**: `La_exportacion_lleva_el_orden_y_el_texto_del_listado_y_no_busca_por_el_identificador` |
| 878 | La exportación de terceros, con otro permiso que el del listado | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 879 | La importación no lee el recargo de equivalencia | **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado`. **ter 0**: ningún caso del carril rápido miraba que el recargo se leyera (punto 11) |
| 880 | La 865 otra vez: la importación no quita el apóstrofo | **ter 1**: `Los_textos_vuelven_sin_el_apostrofo_que_les_puso_la_exportacion`; **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 881 | **Arnés**: `Exportaciones.cs` pide el listado en vez del fichero | **int 6**: las seis de Organización y de Terceros, todas menos la de la foto |
| 882 | El fichero de terceros, con otro nombre | **int 2**: `La_exportacion_lleva_el_orden_y_el_texto_del_listado_y_no_busca_por_el_identificador` y `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 883 | La 879 otra vez, sobre `de5d431` | **ter 1**: `Cada_columna_llega_a_su_campo`; **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 884 | La retención, leída de la columna del criterio de caja | **ter 1**: `Cada_columna_llega_a_su_campo`; **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 885 | La retención, siempre «no» | **ter 1**: `Cada_columna_llega_a_su_campo`; **int 1**: `Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado` |
| 886 | Cliente, leído de la columna de proveedor | **ter 4**: `Cada_columna_llega_a_su_campo` y los tres del apóstrofo, que llevan cliente sí y proveedor no; e **int 1**: el de ida y vuelta |
| 887 | **Arnés**: la fila del caso, con el recargo y la caja cambiados | **ter 1**: `Cada_columna_llega_a_su_campo` |
| 888 | La unidad del artículo, nombrada con los impuestos | **cat 2**: `Cada_maestro_del_articulo_se_nombra_solo_con_el_permiso_de_verlo` y `El_articulo_sale_con_la_pagina_del_listado_y_sus_tres_maestros_nombrados`; **int 2**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Los_articulos_salen_con_sus_maestros_nombrados_y_los_filtros_del_listado` |
| 889 | La unidad, con el permiso de las categorías | **cat 1**: `Cada_maestro_del_articulo_se_nombra_solo_con_el_permiso_de_verlo`; **int 1**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` |
| 890 | La categoría, sin mirar el permiso | **cat 1**: `Cada_maestro_del_articulo_se_nombra_solo_con_el_permiso_de_verlo`; **int 1**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` |
| 891 | La madre de una categoría, citada con el identificador de la hija | **cat 1**: `La_categoria_nombra_a_su_madre_sin_mirar_permisos_y_la_raiz_no_tiene`; **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` |
| 892 | Se pregunta por las categorías de la página y no por sus madres | **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` |
| 893 | La divisa de la tarifa, sin mirar el permiso | **no compiló** (CS9113: `usuarioActual` sin leer). La sustituye la 912 |
| 894 | El tramo de los códigos de barras empieza una página tarde | **cat 1**: `Los_codigos_de_barras_salen_por_tramos_de_la_lista_entera_con_su_longitud_de_total`; **int 1**: `Los_codigos_de_barras_salen_en_el_orden_del_listado_con_su_articulo_nombrado` |
| 895 | El total de los códigos de barras, el del tramo | **cat 1**: `Los_codigos_de_barras_salen_por_tramos_de_la_lista_entera_con_su_longitud_de_total` |
| 896 | El artículo que no existe da una página vacía | **cat 1**: `Los_codigos_de_barras_de_un_articulo_que_no_existe_son_el_error_del_listado`; **int 1**: `Los_codigos_de_barras_salen_en_el_orden_del_listado_con_su_articulo_nombrado` |
| 897 | El tramo de los códigos de barras, sin tope | **cat 1**: `Los_codigos_de_barras_salen_por_tramos_de_la_lista_entera_con_su_longitud_de_total` |
| 898 | La exportación de artículos, sin la categoría pedida | **int 2**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Los_articulos_salen_con_sus_maestros_nombrados_y_los_filtros_del_listado` |
| 899 | La exportación de tarifas, sin el código pedido | **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` |
| 900 | La exportación de categorías, sin su texto | **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` |
| 901 | `vigente_hasta` escribe la fecha de inicio | **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` |
| 902 | El maestro nombrado sale por su identificador | **bb 2**: `Un_maestro_nombrado_sale_con_su_codigo_y_su_nombre_como_texto` y `Un_maestro_sin_nombre_sale_con_su_codigo_y_el_nombre_vacio`; **int 4**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver`, `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden`, `Los_articulos_salen_con_sus_maestros_nombrados_y_los_filtros_del_listado` y `Los_codigos_de_barras_salen_en_el_orden_del_listado_con_su_articulo_nombrado` |
| 903 | La fila sin maestro escribe un identificador vacío | **bb 1**: `Una_fila_que_no_apunta_a_ninguno_sale_con_los_dos_campos_vacios`; **int 2**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden` y `Los_articulos_salen_con_sus_maestros_nombrados_y_los_filtros_del_listado` |
| 904 | `NombresDeMaestros` no mira el permiso | **bb 1**: `Sin_el_permiso_no_pregunta_y_no_hay_nombres`; **cat 2**: `Cada_maestro_del_articulo_se_nombra_solo_con_el_permiso_de_verlo` y `La_tarifa_nombra_su_divisa_solo_con_el_permiso_de_verla`; **int 1**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` |
| 905 | Se pregunta con repetidos | **bb 2**: `Con_el_permiso_pregunta_una_vez_con_los_distintos_y_devuelve_lo_que_vuelve` y `Sin_permiso_que_mirar_pregunta_por_los_distintos_y_sin_identificadores_no` |
| 906 | Se pregunta aunque no haya identificadores | **bb 2**: `Sin_identificadores_no_pregunta_aunque_tenga_el_permiso` y `Sin_permiso_que_mirar_pregunta_por_los_distintos_y_sin_identificadores_no` |
| 907 | El repositorio de categorías nombra todas las de la empresa | **int 1**: `Los_nombres_de_las_categorias_salen_por_lotes_y_los_ajenos_no_vuelven` |
| 908 | El repositorio de categorías, sin el filtro de empresa | **int 1**: `Los_nombres_de_las_categorias_salen_por_lotes_y_los_ajenos_no_vuelven` |
| 909 | **Arnés**: los códigos de barras, fuera de `s_sinPaginar` | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 910 | **Arnés**: un listado paginado, declarado sin paginar | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 911 | **Arnés**: el caso de los códigos de barras espera dos unidades | **int 1**: `Los_codigos_de_barras_salen_en_el_orden_del_listado_con_su_articulo_nombrado` |
| 912 | La divisa de la tarifa, con el permiso de la propia exportación (en lugar de la 893) | **cat 1**: `La_tarifa_nombra_su_divisa_solo_con_el_permiso_de_verla` |
| 913 | El ajuste, sin el estado pedido | **inv 1**: `El_ajuste_sale_con_la_pagina_del_listado_y_su_almacen_nombrado`; **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 914 | El total de los ajustes, el de la página | **inv 1**: `El_ajuste_sale_con_la_pagina_del_listado_y_su_almacen_nombrado` |
| 915 | El recuento pregunta por su identificador y no por su almacén | **inv 1**: `El_recuento_sale_con_la_pagina_del_listado_y_su_almacen_nombrado`; **int 2**: `Cada_maestro_del_recuento_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 916 | La unidad de la línea de recuento, nombrada con los artículos | **inv 1**: `La_linea_de_recuento_sale_con_sus_tres_maestros_nombrados_cada_uno_por_su_dueno`; **int 2**: `Cada_maestro_del_recuento_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 917 | Las líneas de recuento, sin el `?solo=` pedido | **inv 1**: `La_linea_de_recuento_sale_con_sus_tres_maestros_nombrados_cada_uno_por_su_dueno`; **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 918 | El recuento que no existe da una página vacía | **inv 1**: `Las_lineas_de_un_recuento_que_no_existe_son_el_error_del_listado`; **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 919 | El destino de la transferencia, nombrado con el origen | **inv 1**: `La_transferencia_nombra_sus_dos_almacenes_en_una_sola_pregunta`; **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 920 | Solo se pregunta por el almacén de origen | **inv 1**: `La_transferencia_nombra_sus_dos_almacenes_en_una_sola_pregunta`; **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 921 | La transferencia, sin el destino pedido | **inv 1**: `La_transferencia_nombra_sus_dos_almacenes_en_una_sola_pregunta`; **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 922 | El tramo de las líneas de la transferencia empieza una página tarde | **inv 1**: `Las_lineas_de_la_transferencia_salen_por_tramos_de_su_ficha_con_su_divisa`; **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 923 | El total de las líneas, el del tramo | **inv 1**: `Las_lineas_de_la_transferencia_salen_por_tramos_de_su_ficha_con_su_divisa` |
| 924 | El tramo de las líneas, sin tope | **inv 1**: `Las_lineas_de_la_transferencia_salen_por_tramos_de_su_ficha_con_su_divisa` |
| 925 | La transferencia que no existe da una página vacía | **inv 1**: `Las_lineas_de_una_transferencia_que_no_existe_son_el_error_de_la_ficha`; **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 926 | La divisa de la línea, escrita a mano | sobre `3a78bda`, **ninguno**: la ficha del caso iba en euros. Sobre `0b7661c`, **inv 1**: `Las_lineas_de_la_transferencia_salen_por_tramos_de_su_ficha_con_su_divisa` |
| 927 | `cantidad_en_transito` escribe la cantidad | **int 1**: `Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados` |
| 928 | El artículo del saldo escribe el almacén | **int 1**: `Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados` |
| 929 | `teorico_al_contar` escribe el teórico | **int 2**: `Cada_maestro_del_recuento_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 930 | `teorico_cambiado`, siempre «no» | sobre `3a78bda`, **ninguno**: ningún teórico cambiaba después de contar. Sobre `0b7661c`, **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 931 | `fecha_de_recepcion` escribe la de envío | **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 932 | `ubicacion_destino` escribe la de origen | **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 933 | El controlador de ajustes, sin el estado | **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 934 | El controlador de las líneas de recuento, sin el `?solo=` | **int 1**: `Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado` |
| 935 | El controlador de transferencias cambia el origen por el destino | **int 1**: `Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados` |
| 936 | El controlador de saldos no lee la fecha | **int 1**: `Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados` |
| 937 | El día de la exportación de saldos, siempre hoy | **inv 1**: `El_dia_de_la_exportacion_es_el_pedido_y_sin_pedir_ninguno_hoy_en_utc`; **int 1**: `Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados` |
| 938 | Una fecha futura da una página vacía | **inv 1**: `Los_saldos_a_una_fecha_futura_son_el_error_de_la_consulta`; **int 1**: `Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados` |
| 939 | El saldo, sin el artículo pedido | **inv 1**: `El_saldo_sale_con_el_dia_que_contesta_la_consulta_y_sus_dos_maestros_nombrados` |
| 940 | Las ubicaciones, con el permiso de los almacenes | **inv 1**: `Cada_maestro_se_nombra_solo_con_el_permiso_de_verlo` |
| 941 | Los artículos, con el permiso de las unidades | **inv 1**: `Cada_maestro_se_nombra_solo_con_el_permiso_de_verlo` |
| 942 | Las unidades se preguntan a Catálogo | **no compiló** (CS9113: `unidades` sin leer): la ve el compilador. Se mide la 942r |
| 942r | La 942 sin el CS9113: los artículos y las unidades, cada uno al puerto del otro | sobre `0b7661c`, **inv 4**: la teoría de los permisos, el saldo, la línea de recuento y las líneas de la transferencia; e **int 4**: las cuatro de Inventario que nombran un artículo |
| 943 | La foto de Inventario no abre transacción | **int 1**: `La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read` |
| 944 | Las líneas de la transferencia piden otro permiso que su ficha | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 945 | **Arnés**: la lista de las de una ficha, vacía | **fun 1**: `Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado` |
| 946 | **Arnés**: la cuenta recortada espera otra empresa | **int 2**: `Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver` y `Cada_maestro_del_recuento_se_nombra_solo_si_quien_exporta_lo_puede_ver` |
| 947 | La URL `blob:` no se revoca | **web 14**: los catorce de `exportarYComprobar`, en 13 ficheros |
| 948 | El enlace se pulsa sin ponerlo en la página | **web 14**: los catorce de `exportarYComprobar`, en 13 ficheros |
| 949 | El fichero se guarda sin el BOM | **web 14**: los catorce de `exportarYComprobar`, en 13 ficheros, por los bytes |
| 950 | `filename*` no se lee | **web 1**: «El nombre del adjunto es el de `filename*` cuando vienen los dos, que es el que manda (RFC 6266)» |
| 951 | `filename*`, sin descodificar | **web 1**: «El nombre del adjunto es el de `filename*` cuando vienen los dos, que es el que manda (RFC 6266)» |
| 952 | Sin nombre en la cabecera, el fichero se guarda sin nombre | **web 1**: «El nombre del adjunto sin ninguno, o sin cabecera, el fichero se guarda igual, con el nombre de reserva» |
| 953 | Un rechazo se toma por un fichero | **web 2**: «El fichero de una exportación un rechazo pedido como blob trae su ProblemDetails, por el cliente de verdad» y «El listado de almacenes mientras se prepara, el botón se apaga y lo dice; si falla, lo dice y deja reintentar» |
| 954 | El rechazo pierde su estado | **web 1**: «El fichero de una exportación un rechazo pedido como blob trae su ProblemDetails, por el cliente de verdad» |
| 955 | El botón no se apaga mientras llega el fichero | **web 1**: «El listado de almacenes mientras se prepara, el botón se apaga y lo dice; si falla, lo dice y deja reintentar» |
| 956 | La espera no se dice | **web 1**: «El listado de almacenes mientras se prepara, el botón se apaga y lo dice; si falla, lo dice y deja reintentar» |
| 957 | El fallo no se dice | **web 1**: «El listado de almacenes mientras se prepara, el botón se apaga y lo dice; si falla, lo dice y deja reintentar» |
| 958 | El fichero llega y no se guarda | **web 15**: los catorce de `exportarYComprobar`, en 13 ficheros, y el de la espera de almacenes |
| 959 | Saldos exporta el día del campo, y no el que contestó el listado | **web 1**: «El saldo a una fecha sin día, exportar se lleva el que contestó el listado, con los dos filtros y sin la página» |
| 960 | Ajustes exporta sin el orden | **web 1**: «El listado de ajustes exportar se lleva los filtros y el orden del listado con los nombres de la API, sin la página» |
| 961 | Las líneas del recuento, sin la vista | **web 1**: «La ficha del recuento exportar se lleva las líneas de la vista que se mira, sin la página» |
| 962 | Transferencias: el destino es el origen | **web 3**: «El listado de transferencias cada punta acota sola: lo que llega a la central es lo que sale del norte», «El listado de transferencias exportar se lleva los filtros y el orden del listado con los nombres de la API, sin la página» y «El listado de transferencias los filtros van a la URL y al servidor con los nombres de la API, desde la primera página» |
| 963 | Artículos exporta con la página | **web 1**: «El listado de artículos exportar se lleva los dos filtros del listado, y no su página» |
| 964 | Empresas ofrece exportar sin filas | **web 1**: «El listado de empresas sin empresas que ver, no ofrece exportar» |
| 965 | Saldos ofrece exportar sin filas | sobre `f912e12`, **ninguno**; sobre `a6666ea`, **web 1**: «El saldo a una fecha sin saldos lo dice, … y no ofrece exportar» |
| 966 | La ficha de la transferencia ofrece exportar sin líneas | **ninguno**: la guarda no podía saltar, porque una transferencia no se abre sin líneas (`transferencia-sin-lineas`). Se quitó en `a6666ea` |
| 967 | Los códigos de barras ofrecen exportar sin ninguno | sobre `f912e12`, **ninguno**; sobre `a6666ea`, **web 1**: «Los códigos de barras del artículo sin ninguno, … y no ofrece exportar» |
| 968 | Categorías, sin botón | **web 1**: «El árbol de categorías exportar se lleva todas las categorías, no la página» |
| 969 | **Arnés**: el servidor de pega no contesta a las exportaciones | **web 15**: los catorce de `exportarYComprobar`, en 13 ficheros, y el de la espera de almacenes |
| 970 | **Arnés**: el censo lee del contrato otra cosa | **web 3**: «Cada exportación tiene su botón cada exportación del contrato la pulsa un caso, desde su pantalla», «Cada exportación tiene su botón el contrato tiene las exportaciones que dice la entrega, sin repetir ninguna» y «Cada exportación tiene su botón el frontal pide todas las del contrato, y ninguna más» |
| 971 | **Arnés**: el censo se lee a sí mismo | **web 1**: «Cada exportación tiene su botón ninguna exportación escrita se queda fuera de lo que leen las expresiones» |

> **Nota del epílogo del 3.4 (2026-11-04), al mover esta tabla: dos nombres de caso cambiaron
> después, y las filas se dejan como estaban.** Desde `c12f3e2`, del 3.3 (ADR-0076), la tarifa
> guarda su divisa por el código ISO, que ya es lo que se exporta, y Organización no nombra
> divisas por lotes. Así que:
>
> - `Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien`, el de
>   la 835 a la 838, la 847 y la 848, es hoy
>   `Las_unidades_y_los_tramos_se_nombran_sin_empresa_y_lo_retirado_tambien`, en
>   `LosNombresPorLotesTests`. La 837 y la 848, las de la divisa, se quedaron sin sujeto;
> - `La_tarifa_nombra_su_divisa_solo_con_el_permiso_de_verla`, de la 904 y la 912, se quitó en
>   ese commit, con la exportación de la tarifa que nombraba su divisa. La 912 se quedó sin
>   sujeto, y la 904 la siguen viendo los otros tres casos de su fila.

- **Lo que salió distinto de lo escrito, y por qué**:
  - **doce verdes, y cada una dejó su cambio**: la 814 y la 815, dos guardas muertas que se
    quitaron (punto 4); la 838, que llevó a medir las otras cinco, y de ellas la 846, la 847 y la
    848 también salieron verdes (punto 6); la 858, la foto que nadie miraba (punto 8); la 926 y la
    930, apuntadas antes de medir como hallazgo si salían verdes, porque los casos no podían
    distinguirlas, y cubiertas en `0b7661c`; y la 965, la 966 y la 967, «¿sale el botón sin
    filas?» en tres pantallas: dos casos que no lo preguntaban y una guarda que no podía saltar
    (punto 20). Cada una se midió otra vez sobre el commit que la cubre, salvo las dos guardas
    quitadas, que se midieron con su contraria, la 828 y la 829, y la 966, que ya no tiene línea
    que mutar;
  - **la 879 dio `ter 0`, y no «los suyos»**, como decía su espera: ningún caso del carril rápido
    miraba que el recargo se leyera. Lo cubre `de5d431` (punto 11), y lo mide la 883;
  - **cuatro no compilaron en su primera forma**: la 851 (CS1572, el `<param>` del texto), que se
    midió sin el comentario sobre `a995ed0`; la 862 (CS1501, la sobrecarga con el nivel es una
    extensión), en una segunda pasada; la 893 (CS9113: sin mirar el permiso, `usuarioActual` se
    queda sin leer), a la que sustituye la 912, que mira otro permiso; y la 942 (CS9113,
    `unidades` sin leer), que la ve el compilador y se midió como la 942r;
  - **la 846 se escribió con un `viejo` que salía dos veces** en `ConsultaDeArticulos.cs`, y el
    guion paró sin aplicarla (`tanda35-antes.log`); con el trozo único, la 846, la 847 y la 848 se
    midieron en `tanda35-antes-2.log`;
  - **los ceros de un carril que su espera ya decía no son verdes**: la 797, que solo ve el barrido
    de integración; la 802, solo `SoloPublicaElTextoQuienLoMiraTests`; la 850 y la 878, `int 0`,
    porque el usuario de los casos de integración tiene todos los permisos; la 854, que su espera
    daba solo al caso de las columnas; la 892, `cat 0`, porque en el caso la madre está en la
    página; la 905 y la 906, `int 0`, porque solo cambian cuántas veces se pregunta; la 939,
    `int 0`, porque el caso de integración tiene un solo artículo; la 940 y la 941, `int 0`,
    porque su usuario tiene los dos permisos o ninguno; y las de una sola página contra la base
    —la 895, la 897, la 914, la 923 y la 924—, `int 0`. Cada una se pone roja en el carril que la
    ve por diseño;
  - **la 942r puso rojos más casos de los que decía su espera**: la línea de recuento y la teoría en
    el carril de Inventario, y las semillas 950, 951 y 953 en el de integración. Salieron cuatro y
    cuatro: también el saldo y las líneas de la transferencia, y la semilla 952, la de los saldos.
    Los cuatro nombran un artículo, y la 942r manda los artículos al puerto de las unidades: la
    espera se quedó corta, y la mutación, no (`tanda43b.out`);
  - **la primera pasada de la medida no llegó a la API en tres de sus cuatro exportaciones**
    (`medir-1.txt`): Git Bash convirtió la ruta `/api/v1/…` en `/Program Files/Git/api/v1/…` al
    pasarla a `python`, y `urllib` la rechazó por el espacio. La que llegó, la del 2026-03-15, es la
    primera fila de esa fecha en la tabla; las otras cuatro son de la segunda pasada (`medir.txt`),
    con `humo32.sh` llamando a `python` con `MSYS2_ARG_CONV_EXCL="*"`;
  - **la batería del frontal del tema 8 corrió mientras la tanda 43 compilaba en su copia**, contra
    la regla de no lanzar una batería con otra carga en la máquina (`frontal-t8.log`). Dio dos
    rojos. Uno era de verdad, y la contención solo lo adelantó: el `TS2322` de
    `ElListadoDeAlmacenes.test.tsx`, una `contestacion` sin tipo a la que el caso le ponía `null`,
    que se arregló antes de `f912e12` con el tipo `Contestacion` de `pruebas/exportaciones.ts`. El
    otro, no: «si el stock se mueve, lleva a lo que cambió…», de `LaFichaDelRecuento.test.tsx`,
    pasó del plazo de 5 s (5169 ms), con la preparación de la suite en 111 s y el entorno en 153.
    El mismo registro trae **un aviso de `act()`**, de `PaginaDelRecuento`, en «descartar pide el
    motivo…», un caso posterior del mismo fichero. **El aviso no se reprodujo**: el fichero solo,
    tres veces, 20 de 20 y ningún aviso; la suite entera, ya sin la tanda, 426 de 426 y el canal a
    cero; y 13 y 36 copias del fichero a la vez, 260 y 720 casos en verde y ningún aviso
    (`ficha-contencion-1.log` y `-2.log`). Que lo dejara la cola del caso que pasó del plazo, que
    sigue en vuelo cuando empieza el siguiente, es una hipótesis que no se pudo poner roja, así que
    no se arregló nada. Va al informe.

La siguiente mutación es la **972**, y el siguiente ADR, el **0076**.

**La medida, el tema 9: la exportación más grande cabe en el plazo de 30 s por orden.** Sobre
`0b7661c`, en un proyecto de *compose* aparte, `bastion-humo-32`, con su entorno generado en el
*scratchpad*, los puertos en el 45xxx y PostgreSQL 17.6. El guion es `humo32.sh`, del *scratchpad*,
con sus cuatro pasos:

```bash
bash humo32.sh montar      # la pila, el migrador y la salud
bash humo32.sh sembrar     # los maestros por la API, la empresa C y su corte
bash humo32.sh medir       # cada exportación, con sus órdenes leídas del registro de la API
bash humo32.sh desmontar   # down -v de ese proyecto, y el entorno, borrado
```

- **La empresa C del ADR-0064, con sus maestros**: la unidad, el impuesto, cinco almacenes con su
  ubicación y un artículo, por la API; y con `sembrar32.sql`, el artículo clonado hasta 20 000 y la
  semilla del 2.15 sobre ellos, en 88 s: 10⁶ movimientos del 2025-10-01 al 2026-09-30, y 10⁵
  existencias y valoraciones. La exportación nombra los maestros, y nombrar 10⁵ claves contra
  maestros que no existen no mide nada. El corte, con su primera vuelta al arrancar: doce meses,
  992 332 instantáneas, y el cuadre, 20 772 ms y ningún descuadre.
- **Cada exportación**, con `api32.py <entorno> <api> exportar <ruta> <fichero>`, que la baja en
  flujo y mide el primer byte y el total; sus órdenes, del registro de la API, con
  `registro32.py <registro> <inicio> <fin>`, que lee el evento 20101 de EF Core con su `elapsed` y
  su `commandTimeout`; y la memoria de la API, con `docker stats` mientras dura.

| Exportación | Primer byte | Total | Fichero | Órdenes | La más larga | Memoria de la API |
|---|---|---|---|---|---|---|
| Saldos, hoy | 0,42 s | 86,33 s | 6 940 133 B, 100 001 líneas | 1630 | 268 ms | 157–170 MiB |
| Saldos, hoy, otra vez | 0,49 s | 80,17 s | el mismo | 1621 | 235 ms | 132–155 MiB |
| Saldos, `?fecha=2026-03-15` | 0,70 s | 84,06 s | 6 881 502 B, 100 001 líneas | 1627 | 257 ms | 148–172 MiB |
| Saldos, `?fecha=2026-03-15`, otra vez | 0,44 s | 83,19 s | el mismo | 1627 | 243 ms | 139–158 MiB |
| Artículos | 0,15 s | 2,45 s | 1 660 169 B, 20 001 líneas | 404 | 37 ms | 154 MiB, una muestra |
| Saldos, hoy, por nginx | 0,46 s | 77,71 s | el mismo que el directo, byte a byte | — | — | — |

- **Cabe**: la orden más larga son 268 ms, contra un plazo de 30 s, que es el `commandTimeout` de
  todas. De las 1630 órdenes de saldos de hoy, 500 son las páginas, que suman 84 164 ms con una
  mediana de 179 ms; 500 nombran los artículos y 500 los almacenes, de 1 ms cada una; una es el
  `SET TRANSACTION READ ONLY`; y las otras 129 no son de la exportación, sino del despachador de la
  bandeja, que sigue con su sondeo: 43 vueltas de su cerrojo, su lectura y su liberación. Ninguna
  falló, y la ventana no tiene errores. Las 404 de artículos son 100 páginas, con su `count(*)`
  cada una, que en el saldo va dentro de la página; 200 nombres; la foto; y tres del despachador.
- **El fichero cuadra con el libro**: el de hoy tiene 100 000 filas, 100 000 pares distintos y
  ninguna sin nombre, y suma 3 000 000 de cantidad y 7 500 000 de valor, que es el total del libro.
  El del 2026-03-15 suma 1 358 911 y 3 397 277,5, que es lo que da
  `SELECT sum(cantidad_en_unidad_base), sum(valor) FROM inventario.movimiento_stock` en la empresa
  hasta ese día.
- **El nombre del fichero es `saldos-2026-10-28.csv` en los dos**, con el día en que se exporta, y
  el día pedido va en la columna `fecha` (punto 15).
- **Al acabar**, `down -v` salió con 0, el único proyecto que queda es `bastion`, y el entorno está
  borrado.

**La descarga entera de saldos tarda entre 78 y 86 s**, y la memoria no crece con ella. Cada página
cuesta lo mismo desde el principio —en la del 2026-03-15, 75 ms la primera y 105, 183, 182 y 184 de
media por cuartos—, así que el coste es por página y no por profundidad. Qué parte es el `count(*)`
que cada página rehace sobre los pares y qué parte las sumas laterales no se midió. Va a una nota
abierta, la sexta de *Notas / riesgos*.

### La puerta del 3.3, contestada (2026-11-02)

El criterio deja abiertas dos cosas que no salen sin elegir qué se conserva de lo que ya está
escrito, y se preguntaron juntas antes de escribir nada. Las dos respuestas son la opción
recomendada:

1. **La divisa de las tarifas que ya existen la copia la migración de Catálogo desde
   `organizacion.divisas`**, una sola vez, con un `UPDATE … FROM`. Es la única lectura entre
   esquemas, en una migración y nunca en ejecución, y va con su ADR. Organización migra siempre
   antes que Catálogo (`MigradorDeArranque`), así que no se pierde nada. Las otras salidas eran un
   paso del migrador entre dos migraciones, que pregunta los códigos al puerto, y pasarlas todas a
   `EUR`, que convertiría una tarifa en dólares sin aviso.
2. **Toda tarifa de hoy pasa a ser de venta, y toda asignación, a la tarifa de venta del
   tercero**, sea cliente o no. No se pierde nada. Que la de venta pida un cliente lo mira cada
   asignación nueva, igual que un cliente que deja de serlo conserva la suya; y como es de venta,
   nunca propone un precio de compra. Las otras eran quitar la de quien solo es proveedor, que
   pierde el dato, y sacar el tipo de quién la usa, que cruza esquemas en los dos sentidos.

Lo que no se preguntó, porque tiene un precedente o una salida obvia, y se puede cambiar sin tocar
datos:

- **el tipo va con el código**: los tramos de un código son todos del mismo tipo, y el de un tramo
  no cambia, como su divisa;
- **la divisa se escribe y se publica con su código ISO**, en `TarifaDto` y en
  `PrecioResueltoDto`, y se valida como hoy, ahora por su código: que Organización la tenga y no
  esté retirada;
- **una tarifa sin su divisa en `organizacion.divisas` no puede existir**, porque una divisa no se
  borra, se retira. La migración lo afirma: para con su motivo, en vez de darle una por omisión;
- **el tercero lleva `tarifa_de_venta_id`, que es `tarifa_asignada_id` renombrada sin tocar una
  fila, y `tarifa_de_compra_id`, nueva y nula.** La de venta pide un cliente y una tarifa de
  venta, y la de compra, un proveedor y una tarifa de compra. El puerto de las tarifas dice
  también su tipo;
- **la API del tercero tiene dos subrecursos, `tarifa-de-venta` y `tarifa-de-compra`**, con el
  `GET` y el `PUT` de `tarifa-asignada`, que se retira, y su ficha enseña las dos;
- **el puerto del precio toma el tramo que lleva el tercero**, un artículo, una cantidad y una
  fecha, y resuelve con el tramo de su código que rige ese día, como hoy
  `GET /api/v1/catalogo/tarifas/{codigo}/precio`. Devuelve el precio y el descuento solo con una
  tarifa de compra en la divisa base de la empresa, y si no, el motivo por el que no propone. La
  tarifa de compra de un proveedor la dice `Terceros.Contracts`;
- **el invariante 8 queda desarmado** cuando las dos divisas del tercero, la de su límite y la de
  su tarifa, son un código ISO. El traspaso de la fase 1 no se reescribe: lo dicen este PLAN y el
  ADR;
- **la exportación de tarifas lleva el tipo, y la divisa en una columna con su código**, como la
  de empresas, sin nombre;
- **Compras gana el esquema `compras`, `ComprasDbContext` con la bandeja mapeada como los demás, y
  una primera migración que solo crea el esquema**, porque su primer documento es el pedido del
  3.7. Entra en el migrador después de Inventario, en `LasMigracionesSobreTablasConFilasTests`, en
  `comprobar-migraciones.sh` y en los censos de los tests de arquitectura.

### Tomadas por el agente de desarrollo — ítem 3.3, al escribirlo y al medirlo (2026-11-02)

Cada tema del orden de trabajo fue en su subida, y tres de ellos en dos commits: la puerta, en
`1fd5efb`; la tarifa, en `c12f3e2` y `7f867bb`; sus pantallas, en `eead063`; el tercero, en
`e26217d`; los puertos, en `ae253c1` y `1e47d47`; y Compras, en `82cf007` y `4955575`. Lo que se
decidió sin preguntar, porque el criterio no lo decía o porque salió al escribirlo:

1. **El tipo se lee por su nombre exacto, y la base impide que un código mezcle dos** (`c12f3e2`,
   ADR-0076). `tarifas_un_tipo_por_codigo` es un `EXCLUDE` gemelo del de los solapes, y el alta
   pregunta antes, para contestar el `409` `tarifa-tipo-distinto-del-codigo`. `Venta` y `Compra`
   se comparan con `Ordinal`, y no con `Enum.TryParse`, que acepta «1» y «Venta,Compra»: lo demás
   es el `400` `tarifa-tipo-no-valido`. La divisa se valida por su código con
   `IConsultaDeDivisas`, que ya no nombra por lotes, y por eso el ADR enmienda el §1, el §2 y el
   *Cómo se vigila* del 0074.
2. **La migración se para con un código que no es transitorio** (`c12f3e2`). La primera versión
   paraba con `55000`, `object_not_in_prerequisite_state`, que Npgsql cuenta como transitorio, y
   EF Core lo envolvía en una `InvalidOperationException` que proponía activar los reintentos.
   Ahora para con `42P01` si hay tramos y no está Organización, y con `23503` si la divisa de un
   tramo no está, y nombra el tramo y su código. Los casos afirman el código y que la parada no es
   transitoria. El recorrido con filas aprende la referencia de `catalogo.tarifas` a
   `organizacion.divisas`, que la base no declara, y la ven la 991 y la 992, por su arnés.
3. **La vuelta se para como la ida** (`7f867bb`): la tanda dejó en verde la 979, la 980 y la 981,
   porque la vuelta solo se ejercía con todas sus divisas en el maestro. Dos casos nuevos en
   `LaDivisaDeLaTarifaSeTraduceAlMigrarTests`: la vuelta de un tramo cuya divisa no está se para
   con `23503` y no se lleva nada, y sin Organización se para si hay tramos y pasa si no los hay.
4. **No hay pantalla de alta de tarifas, así que el tipo se elige en la API** (`eead063`). El
   listado enseña el tipo de cada tramo y su divisa por el código, sin preguntar a Organización.
   Un tipo que no es ninguno de los dos sale «Sin reconocer», con su explicación debajo de la
   tabla, y la celda la señala. El servidor de pega gana una tarifa de compra en dólares, para que
   una tabla que pintara la divisa de la empresa, o el mismo tipo en todas las filas, no acierte.
   Lo dice el README de la funcionalidad. La pantalla de alta se propone: no está en el PLAN.
5. **El tercero no tiene ficha en el frontal**, solo su listado y su importación, así que «su ficha
   enseña las dos» se cumple en la API: cada tarifa se lee por su subrecurso, como se leía la
   asignada. La pantalla de la ficha también se propone.
6. **El puerto de las tarifas recibe el tipo que se busca, en vez de decir el suyo** (`e26217d`), y
   contesta `EsDeOtroTipo` antes que la vigencia. La aplicación traduce el rol al tipo, así que el
   controlador no ve nada de Catálogo. Dos `409` nuevos: `tercero-tarifa-de-otro-tipo`, aunque
   además esté caducada, y `tercero-tarifa-sin-su-rol`, que se mira antes de preguntar al puerto.
   Quitar con nulo no pide el rol: quien dejó de ser cliente tiene que poder soltar la suya.
7. **Un permiso para cada tarifa** (`e26217d`): la de venta conserva el código de antes, que es el
   que guardan los roles, y la de compra estrena `terceros.tarifa-de-compra.fijar`. Con uno para
   los dos `PUT` se puso roja la regla de que dos escrituras no comparten permiso, y con razón:
   con qué precios se compra lo decide compras, y con cuáles se vende, ventas. El rol del sistema
   lo recibe al arrancar, y uno propio, cuando alguien se lo conceda. Las acciones del borde pasan
   de 171 a 173.
8. **La vuelta de la migración del tercero pierde la de compra** (`e26217d`), por lo mismo que la
   del ADR-0076 pierde el tipo: devuelve su nombre a la columna de venta y borra la de compra.
   `LaTarifaDelTerceroSeConservaAlMigrarTests` hace la ida y la vuelta sobre tres fichas.
9. **El precio de compra lo propone un puerto que dice por qué no** (`ae253c1`, ADR-0077).
   `IConsultaDePrecios.DeCompraAsync` contesta un `PrecioPropuesto`, sellado, con el precio o con
   el motivo y nunca los dos, y sus dos fábricas son las únicas puertas. Los motivos se miran de la
   tarifa hacia la línea, y se para en el primero que se sabe. El precio sale del tramo de su
   código que rige ese día, no del asignado, y la divisa que se compara es la de ese tramo: los de
   un código comparten el tipo, pero no la divisa. Una empresa bloqueada contesta nulo, que no es
   la divisa de ningún tramo, y por eso el motivo es `NoEstaEnLaDivisaBase`. La implementación vive
   en la aplicación, porque es la precedencia de las líneas, y comparte `ElPrecioDelTramo` con
   `ResolverPrecio`. No tiene consumidor hasta el 3.7, así que un caso le pregunta al *host* de
   verdad si está registrado. La tanda vio la 1043 solo contra la base, y `1e47d47` la baja al
   carril rápido con un caso que pide dos cantidades sobre dos líneas.
10. **La tarifa de compra de un proveedor la dice `Terceros.Contracts`, con el papel en el `WHERE`**
    (`ae253c1`): quien deja de ser proveedor conserva la tarifa en su ficha, y el puerto no la
    contesta. La matriz de puerto por estado reconoce la respuesta con motivo, una clase sellada de
    un `Contracts` con un solo enumerado público, y un anclaje lo afirma: con la forma de antes, el
    puerto habría entrado con la matriz en verde y sus siete casillas sin mirar. Para componer el
    puerto con los adaptadores de verdad, `Api.IntegrationTests` ve lo interno de
    `Catalogo.Application`.
11. **El invariante 8 queda desarmado** (`ae253c1`): la divisa de la tarifa, la del límite del
    tercero y la base de la empresa son un código ISO, y el puerto compara la del tramo con la de
    la empresa sin traducir. El traspaso de la fase 1 no se reescribe: lo dicen este punto y el
    ADR-0077.
12. **Compras se monta con una sola capa con tipos** (`82cf007`), la infraestructura, con
    `ComprasDbContext` y `ModuloDeCompras`. Las otras cuatro compilan vacías hasta el pedido del
    3.7, y el inventario de arquitectura lo dice en `EnsambladosConTipos`, como en Auditoría. El
    contexto no llama a `ApplyConfigurationsFromAssembly`, porque sobre un ensamblado sin
    configuraciones EF Core avisa cada vez que construye el modelo. El `EnsureSchema` de la primera
    migración está escrito a mano, porque EF Core la generó vacía: la 1063 lo quitó y nada se puso
    rojo, ni el humo, como dice su comentario, porque el historial crea el esquema al aplicarla.
13. **La 1073 y la 1074 son equivalentes hoy**: sin el interceptor de la bandeja y sin la convención
    de nombres no cambia nada, porque Compras no escribe y no tiene tablas propias. Dejan de serlo
    con el primer `SaveChanges` y la primera tabla del pedido del 3.7, y su tanda tiene que verlas.
    Ninguna regla mira qué interceptores lleva un contexto: se propone.
14. **La lista de `comprobar-migraciones.sh` se compara con el disco** (`4955575`): la 1065 quitó
    Compras del bucle y el guion salió con 0. Ahora `MODULOS` se compara, entera, con los módulos
    que tienen su `<Modulo>DbContext.cs`, y aparte se afirma que el patrón encuentra alguno: lo ven
    la 1076, por su arnés, y la 1077, con la comparación al revés. El paso del humo de `ci.yml`
    tiene otra lista de contextos tecleada, y la lee con `echo "$REGISTRO" | grep -q` bajo
    `pipefail`, la forma del SIGPIPE del 1.12. Se propone: no está en el PLAN.

**Las tandas, de la 972 a la 1077, se escribieron antes de medir**, una por tema y sobre su commit,
tres de ellas con una segunda pasada sobre el commit que cubría lo que la primera dejó en verde, y
cada mutación con su «espera», en el *scratchpad*: un guion `mutarNN.py` por tanda, con sus
mutaciones dentro. Cada una se aplicó sola sobre el árbol limpio del repositorio, se revirtió con
`git restore --source=HEAD`, se tocó con la fecha de ahora, y al acabar cada tanda el árbol
restaurado compiló y la porcelana salió vacía. Los registros, `tandaNN.out` y `tandaNN.log`:

```bash
python -X utf8 mutar33.py base 972-993          # sobre c12f3e2
python -X utf8 mutar33.py 979-981               # sobre 7f867bb
python -X utf8 mutar33-web.py base 994-1004     # sobre eead063
python -X utf8 mutar34.py base 1005-1031        # sobre e26217d
python -X utf8 mutar35.py base 1032-1057        # sobre ae253c1
python -X utf8 mutar35.py 1043                  # sobre 1e47d47
python -X utf8 mutar36.py base 1058-1075        # sobre 82cf007
python -X utf8 mutar36.py 1065 1076 1077        # sobre 4955575
```

Los carriles son `dotnet test` de un proyecto, sin compilar y filtrado a las clases que la tanda
puede ver; en el frontal, `vitest run` sobre el fichero del listado de tarifas, con los rojos por
nombre y los avisos de `act()` de los siete trozos del canal (ADR-0066); `mig`, el guion
`comprobar-migraciones.sh`; y `humo`, `humo33.sh`, que levanta el *compose* en su propio proyecto,
`bastion-humo-33`, lee del motor el esquema y el historial de Compras, hace el segundo arranque y
lo desmonta. En la tabla, `fun` es `Api.FunctionalTests`; `int`, `Api.IntegrationTests`; `org`,
`Organizacion.IntegrationTests`; `ter` y `cat`, los `UnitTests` de Terceros y de Catálogo; `arq`,
`Arquitectura.Tests`; y `web`, el frontal. Las bases, todas a cero, con los casos de cada carril:

- la 33: `cat` 287, `org` 10 e `int` 34; y su segunda pasada, con los dos casos nuevos, `int` 36;
- la del frontal: `web` 19, en 1 fichero, con 0 avisos de `act()` y 0 plazos agotados, en la base
  y en cada una de las once pasadas;
- la 34: `ter` 113, `fun` 279, `arq` 67 e `int` 19;
- la 35: `cat` 309, `arq` 67 e `int` 26; y la pasada de la 1043, con el caso nuevo, `cat` 310;
- y la 36: `arq` 67, `fun` 279, `int` 81, filtrado a `Persistencia`, y `mig`, con 0. El humo no
  corrió en la base: la suya es la pasada del cierre, sobre `4955575`, con 0 fallos.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 972 | La ida, sin la salida de la tabla vacía: sin Organización se para aunque no haya tramos | **int 1**: `Sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola` |
| 973 | La ida, sin la guarda de Organización: llega el `42P01` de la base, sin el mensaje que la pide | **int 1**: `Sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola` |
| 974 | La ida, sin la parada del tramo cuya divisa no está: el `23502` de la columna, sin nombrar el tramo | **int 1**: `Un_tramo_cuya_divisa_no_esta_para_la_migracion_diciendo_cual_y_no_se_pierde_nada` |
| 975 | La ida traduce todos los tramos a la primera divisa del maestro | **int 2**: `Cada_tramo_recibe_el_codigo_de_su_divisa_y_la_vuelta_le_devuelve_su_identificador` y `Un_tramo_cuya_divisa_no_esta_para_la_migracion_diciendo_cual_y_no_se_pierde_nada` |
| 976 | La ida pasa los tramos de hoy a compra | **int 1**: `Cada_tramo_recibe_el_codigo_de_su_divisa_y_la_vuelta_le_devuelve_su_identificador` |
| 977 | La ida, sin la exclusión `tarifas_un_tipo_por_codigo` | **int 1**: `Dos_tramos_del_mismo_codigo_y_distinto_tipo_los_rechaza_la_BASE` |
| 978 | La vuelta devuelve a todos los tramos el primer identificador del maestro | **int 1**: `Cada_tramo_recibe_el_codigo_de_su_divisa_y_la_vuelta_le_devuelve_su_identificador` |
| 979 | La vuelta, sin la parada del tramo cuya divisa no está | sobre `c12f3e2`, **ninguno**, en los tres carriles; sobre `7f867bb`, **int 1**: `La_vuelta_de_un_tramo_cuya_divisa_no_esta_se_para_diciendo_cual_y_no_se_pierde_nada`, el caso nuevo (punto 3) |
| 980 | La vuelta, sin la guarda de Organización | sobre `c12f3e2`, **ninguno**, en los tres carriles; sobre `7f867bb`, **int 1**: `La_vuelta_sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola`, el caso nuevo (punto 3) |
| 981 | La vuelta, sin la salida de la tabla vacía | sobre `c12f3e2`, **ninguno**, en los tres carriles; sobre `7f867bb`, **int 1**: `La_vuelta_sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola`, el caso nuevo (punto 3) |
| 982 | El alta no mira el tipo de los tramos de su código | **cat 1**: `Un_tramo_de_otro_tipo_que_los_de_su_codigo_no_entra`; **int 1**: `La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type` |
| 983 | El repositorio no encuentra el tipo del código | **int 1**: `La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type` |
| 984 | La divisa se busca con el código tal como llega, sin normalizar | **org 1**: `La_divisa_se_busca_por_su_codigo_como_se_guarda_y_lo_que_no_es_un_codigo_no_existe`; **int 2**: `La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type` y `Las_categorias_nombran_a_su_madre_y_las_tarifas_salen_con_su_tipo_y_su_divisa` |
| 985 | La tarifa guarda la divisa sin normalizar | **cat 3**: `La_divisa_se_pregunta_como_llego_y_se_guarda_en_mayusculas`, `La_divisa_se_guarda_con_su_codigo_en_mayusculas_y_sin_blancos` y `Una_divisa_sin_forma_de_codigo_ISO_no_abre_una_tarifa`; **int 2**: `La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type` y `Las_categorias_nombran_a_su_madre_y_las_tarifas_salen_con_su_tipo_y_su_divisa` |
| 986 | El tipo se lee sin distinguir mayúsculas | **cat 1**: `Un_tipo_que_no_es_venta_ni_compra_se_contesta_antes_de_preguntar_la_divisa` |
| 987 | El tipo se lee con `Enum.TryParse`, que acepta «1» y «Venta,Compra» | **cat 1**: `Un_tipo_que_no_es_venta_ni_compra_se_contesta_antes_de_preguntar_la_divisa` |
| 988 | La exportación cruza el tipo y la divisa | **int 1**: `Las_categorias_nombran_a_su_madre_y_las_tarifas_salen_con_su_tipo_y_su_divisa` |
| 989 | El DTO dice siempre «Venta» | **cat 2**: `El_tipo_se_guarda_tal_cual_se_pidio` y `Un_tramo_del_mismo_tipo_que_los_de_su_codigo_entra`; **int 2**: `La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type` y `Las_categorias_nombran_a_su_madre_y_las_tarifas_salen_con_su_tipo_y_su_divisa` |
| 990 | La divisa retirada se ofrece como vigente | **org 1**: `La_divisa_retirada_deja_de_ofrecerse_y_sigue_resolviendo` |
| 991 | **Arnés**: la referencia que la base no declara, con una letra cambiada | **int 1**: `Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila` |
| 992 | **Arnés**: el recorrido no apunta como usadas las claves declaradas | **int 1**: `Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila` |
| 993 | La ida, con el `CHECK` del tipo abierto | **int 1**: `Un_tipo_que_no_es_venta_ni_compra_lo_rechaza_la_BASE` |
| 994 | Todo es de venta | **web 2**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» y «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 995 | La de compra no se reconoce | **web 1**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» |
| 996 | Lo desconocido se toma por compra | **web 1**: «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 997 | La divisa no llega del contrato: siempre `EUR` | **web 1**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» |
| 998 | La celda pinta `EUR`, y no la divisa del tramo | **web 1**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» |
| 999 | Las etiquetas del tipo, cruzadas | **web 2**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» y «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 1000 | Lo desconocido sale en bruto, sin marca | **web 1**: «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 1001 | La celda no señala la explicación (`aria-describedby`) | **web 1**: «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 1002 | Sin la explicación debajo de la tabla | **web 1**: «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 1003 | La explicación, siempre | **web 1**: «El listado de tarifas cada tramo dice si es de venta o de compra, y su divisa por su código» |
| 1004 | **Arnés**: el servidor de pega no cambia ningún tipo | **web 1**: «El listado de tarifas un tipo que esta versión no conoce se marca y se explica, y no sale en bruto» |
| 1005 | El hueco del cliente pregunta por una tarifa de compra | **ter 1**: `Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco`; **int 5**, todos de `ContratoDeLosCrucesTests` |
| 1006 | La aplicación no mira el rol del hueco, y lanza el dominio | **ter 1**: `Sin_el_rol_del_hueco_es_409_y_no_se_pregunta_ni_se_toca_la_ficha`; **int 1**: `Sin_el_rol_del_hueco_es_409_y_quitar_la_de_ese_hueco_no_lo_pide` |
| 1007 | El rol de cada hueco, cruzado | **ter 4**: `El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia`, `Los_tres_noes_de_la_tarifa_se_distinguen_entre_si`, `Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco` y `Sin_el_rol_del_hueco_es_409_y_no_se_pregunta_ni_se_toca_la_ficha`; **int 5**, todos de `ContratoDeLosCrucesTests` |
| 1008 | El rol se mira después de preguntar al puerto | **ter 1**: `Sin_el_rol_del_hueco_es_409_y_no_se_pregunta_ni_se_toca_la_ficha` |
| 1009 | La escritura va al hueco del otro rol | **ter 4**: `El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia`, `Los_dos_huecos_no_se_pisan`, `Quitar_la_tarifa_no_pregunta_por_ninguna_ni_pide_el_rol` y `Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco`; **int 5**, todos de `ContratoDeLosCrucesTests` |
| 1010 | La de otro tipo se asigna | **ter 2**: `Los_tres_noes_de_la_tarifa_se_distinguen_entre_si` y `Un_estado_que_no_autoriza_no_toca_la_ficha_ni_confirma_nada`; **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1011 | La de otro tipo contesta el `type` de la que no rige | **ter 1**: `Los_tres_noes_de_la_tarifa_se_distinguen_entre_si`; **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1012 | El adaptador no mira el tipo | **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1013 | El adaptador mira la vigencia antes que el tipo | **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1014 | El tipo buscado, traducido al revés | **int 5**, todos de `ContratoDeLosCrucesTests` |
| 1015 | La lectura del hueco de compra trae la de venta | **ter 2**: `El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia` y `La_lectura_devuelve_la_de_su_hueco_aunque_su_vigencia_haya_terminado`; **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte`. La espera nombraba `Los_dos_huecos`, y manda lo medido |
| 1016 | El dominio, sin la guarda del rol | **ter 1**: `Cada_tarifa_pide_el_rol_de_su_hueco` |
| 1017 | El dominio escribe la de compra en la de venta | **ter 4**: `El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia`, `La_lectura_devuelve_la_de_su_hueco_aunque_su_vigencia_haya_terminado`, `Los_dos_huecos_no_se_pisan` y `Cada_tarifa_pide_el_rol_de_su_hueco`; **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1018 | La de compra pide un cliente | **ter 3**: `El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia`, `Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco` y `Cada_tarifa_pide_el_rol_de_su_hueco` |
| 1019 | Una tarifa vacía, `Guid.Empty`, se acepta | **ter 1**: `Una_tarifa_vacia_no_es_ninguna` |
| 1020 | La migración da de baja y de alta la columna en vez de renombrarla | **int 1**: `Cada_ficha_conserva_su_tarifa_como_la_de_venta_y_la_vuelta_se_la_devuelve` |
| 1021 | La vuelta no devuelve el nombre de la columna | **int 1**: `Cada_ficha_conserva_su_tarifa_como_la_de_venta_y_la_vuelta_se_la_devuelve` |
| 1022 | El `GET` de compra lee el hueco de venta | **int 1**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` |
| 1023 | El `PUT` de compra escribe el hueco de venta | **int 2**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` y `Sin_el_rol_del_hueco_es_409_y_quitar_la_de_ese_hueco_no_lo_pide` |
| 1024 | El `PUT` de compra exige el permiso de la de venta | **fun 2**: `Escribir_y_modificar_no_comparten_permiso_aunque_los_escriba_el_mismo_codigo` y `Todo_permiso_del_catalogo_lo_exige_alguna_accion` |
| 1025 | El permiso nuevo, fuera del catálogo | **fun 1**: `Todo_permiso_exigido_existe_en_el_catalogo_que_registra_el_host`; **int 2**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` y `Sin_el_rol_del_hueco_es_409_y_quitar_la_de_ese_hueco_no_lo_pide` |
| 1026 | La tarifa de compra, sin auditar | **fun 1**: `Ninguna_propiedad_de_una_entidad_auditada_se_queda_sin_clasificar` |
| 1027 | El cruce de la tarifa de compra, sin su entrada en el inventario | **arq 1**: `Ningun_identificador_del_dominio_se_queda_sin_clasificar` |
| 1028 | **Arnés**: la casilla `EsDeOtroTipo`, sin su caso declarado | **int 1**: `Cada_casilla_de_puerto_por_estado_esta_cubierta` |
| 1029 | **Arnés**: el doble no apunta el tipo que se le pregunta | **ter 1**: `Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco` |
| 1030 | **Arnés**: la lectura del caso de la migración no lee la columna | **int 1**: `Cada_ficha_conserva_su_tarifa_como_la_de_venta_y_la_vuelta_se_la_devuelve` |
| 1031 | **Arnés**: el `PUT` del caso va siempre al hueco de venta | **int 2**: `Cada_tarifa_en_el_hueco_del_otro_tipo_es_409_y_en_el_suyo_se_asigna_y_se_lee_aparte` y `Sin_el_rol_del_hueco_es_409_y_quitar_la_de_ese_hueco_no_lo_pide` |
| 1032 | El puerto pide una tarifa de venta | **cat 10**, todos de `ElPrecioDeCompraTests`; **int 5**, todos de `ElPrecioDeCompraContraLaBaseTests` |
| 1033 | El tipo se mira después de la vigencia | **cat 1**: `Con_varios_motivos_gana_el_que_va_primero` |
| 1034 | El precio sale del tramo asignado, y no del que rige | **cat 1**: `Resuelve_con_el_tramo_de_su_codigo_que_rige_ese_dia_y_no_con_el_asignado`; **int 1**: `El_precio_sale_del_tramo_que_rige_ese_dia_y_antes_del_primero_no_rige_ninguno` |
| 1035 | La divisa que se compara es la del tramo asignado | **cat 1**: `La_divisa_que_se_compara_es_la_del_tramo_que_rige_y_no_la_del_asignado` |
| 1036 | Una empresa sin divisa base propone | **cat 1**: `Un_tramo_fuera_de_la_divisa_base_no_propone` |
| 1037 | La divisa no se compara | **cat 3**: `Con_varios_motivos_gana_el_que_va_primero`, `La_divisa_que_se_compara_es_la_del_tramo_que_rige_y_no_la_del_asignado` y `Un_tramo_fuera_de_la_divisa_base_no_propone`; **int 1**: `En_una_empresa_en_dolares_una_tarifa_en_euros_no_propone_aunque_su_precio_se_resuelva` |
| 1038 | El artículo se mira antes que la divisa | **cat 1**: `Con_varios_motivos_gana_el_que_va_primero` |
| 1039 | La divisa se mira antes que la vigencia | **cat 2**: `Con_varios_motivos_gana_el_que_va_primero` y `Una_tarifa_de_compra_en_la_divisa_base_propone_el_precio_de_su_linea` |
| 1040 | El tramo que rige se busca un día después | **cat 4**: `Con_varios_motivos_gana_el_que_va_primero`, `La_divisa_que_se_compara_es_la_del_tramo_que_rige_y_no_la_del_asignado`, `Resuelve_con_el_tramo_de_su_codigo_que_rige_ese_dia_y_no_con_el_asignado` y `Si_ningun_tramo_de_su_codigo_rige_ese_dia_no_propone`; **int 1**: `El_precio_sale_del_tramo_que_rige_ese_dia_y_antes_del_primero_no_rige_ninguno` |
| 1041 | Una cantidad cero pasa | **cat 1**: `Una_cantidad_que_no_es_positiva_lanza` |
| 1042 | El origen del precio, al revés | **cat 3**: `Una_linea_de_la_categoria_del_articulo_tambien_propone`, `Una_tarifa_de_compra_en_la_divisa_base_propone_el_precio_de_su_linea` y `El_precio_resuelto_lleva_SIEMPRE_la_divisa_de_la_tarifa`; **int 2**: `Gana_el_antepasado_MAS_CERCANO_y_la_mas_honda_de_otra_rama_no_compite` y `La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba` |
| 1043 | `ResolverPrecio` le pasa a la línea otra cantidad | sobre `ae253c1`, **int 1**: `La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba`, solo contra la base; sobre `1e47d47`, **cat 1**: `La_cantidad_que_se_pide_es_la_que_elige_la_linea_y_la_que_vuelve`; **int 1**: `La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba`, el caso nuevo (punto 9) |
| 1044 | El puerto le pasa a la línea otra cantidad | **cat 1**: `Una_tarifa_de_compra_en_la_divisa_base_propone_el_precio_de_su_linea` |
| 1045 | El puerto contesta con otro día | **cat 1**: `Una_tarifa_de_compra_en_la_divisa_base_propone_el_precio_de_su_linea` |
| 1046 | `Sin` acepta `Propone` | **cat 1**: `La_propuesta_no_se_construye_con_un_precio_y_un_motivo_a_la_vez` |
| 1047 | `Sin` acepta un valor fuera del enumerado | **cat 1**: `La_propuesta_no_se_construye_con_un_precio_y_un_motivo_a_la_vez` |
| 1048 | `Con` acepta un precio nulo | **cat 1**: `La_propuesta_no_se_construye_con_un_precio_y_un_motivo_a_la_vez` |
| 1049 | La tarifa de compra la contesta quien dejó de ser proveedor | **int 1**: `La_tarifa_de_compra_la_contesta_el_proveedor_que_la_lleva_y_nadie_mas` |
| 1050 | El puerto de Terceros contesta la tarifa de venta | **int 2**: `La_tarifa_de_compra_la_contesta_el_proveedor_que_la_lleva_y_nadie_mas` y `Un_cliente_que_tambien_es_proveedor_recibe_su_precio_de_compra_y_no_el_de_venta` |
| 1051 | El puerto de Terceros, sin los filtros: el bloqueado y el de otra empresa | **int 1**: `La_tarifa_de_compra_la_contesta_el_proveedor_que_la_lleva_y_nadie_mas` |
| 1052 | El puerto del precio, sin registrar | **int 1**: `El_host_de_verdad_registra_el_puerto_del_precio_de_compra` |
| 1053 | La puerta pública, sin declarar en el inventario | **arq 1**: `Las_puertas_publicas_de_los_contratos_son_las_declaradas` |
| 1054 | **Arnés**: la matriz deja de reconocer la respuesta sellada con motivo | **int 2**: `Cada_cubrimiento_nombra_un_puerto_de_aqui_y_un_valor_de_SU_enumerado` y `La_matriz_no_esta_vacia_y_ve_los_dos_sentidos_del_cruce` |
| 1055 | **Arnés**: la casilla `NoRigeEseDia`, sin su caso declarado | **int 1**: `Cada_casilla_de_puerto_por_estado_esta_cubierta` |
| 1056 | **Arnés**: el doble contesta siempre euros | **cat 1**: `Un_tramo_fuera_de_la_divisa_base_no_propone` |
| 1057 | **Arnés**: el ayudante pregunta siempre por el mismo día | **int 1**: `El_precio_sale_del_tramo_que_rige_ese_dia_y_antes_del_primero_no_rige_ninguno` |
| 1058 | El migrador no migra Compras | **humo**: «sin EsquemaMigrado para ComprasDbContext», «no existe el esquema compras» y los dos del historial; los carriles, en verde, porque solo el humo arranca el migrador |
| 1059 | **Arnés**: el *fixture* no migra Compras | **int 1**: `Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema` |
| 1060 | La traza, migrada desde Compras (`migra: true`) | **mig**: «El modelo de Compras tiene cambios sin migrar», que lo ve por diseño; e **int 38**, en siete clases, todos por el `MigrateAsync` del *fixture*, que lanza `PendingModelChangesWarning` |
| 1061 | La bandeja de Compras, sin el filtro de inquilinato | **fun 1**: `Ninguna_entidad_del_modelo_se_queda_sin_filtro_y_sin_motivo` |
| 1062 | El *host* no registra el módulo | **mig**: «dotnet ef no arranca para Compras», que lo ve por diseño; y **fun 29**, en nueve clases, todos con «No service for type `ComprasDbContext`», del *host* que resuelve cada contexto |
| 1063 | La migración, sin su `EnsureSchema` | **ninguno**, con el humo: el historial crea el esquema al aplicar la migración, y la línea solo lo declara, como dice su comentario. Equivalente por diseño (punto 12) |
| 1064 | Las migraciones de Compras, fuera del ensamblado: sin su `<Compile Include>` | **mig**: «EF no encuentra NINGUNA migración de Compras en el ensamblado»; e **int 1**: `Recorre_todos_los_contextos_que_tienen_migraciones` |
| 1065 | Compras, fuera de la lista de `comprobar-migraciones.sh` | sobre `82cf007`, **ninguno**: la lista era tecleada (punto 14); sobre `4955575`, **mig**: «La lista MODULOS de este guion no es la de los contextos que hay en el disco» |
| 1066 | El historial, en `public` | **int 2**: `Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema` y `En_public_no_queda_ni_una_tabla_de_ningun_modulo` |
| 1067 | Una arista de Compras, sin declarar | **arq 1**: `Las_referencias_de_proyecto_son_las_declaradas` |
| 1068 | Compras, andamio | **arq 3**: `Los_ensamblados_modulares_de_la_salida_son_los_declarados`, `Las_referencias_de_proyecto_son_las_declaradas` y `Ningun_modulo_ve_el_interior_de_otro` |
| 1069 | La infraestructura de Compras, fuera de las capas con tipos | **arq 2**: `Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara` y `Ningun_modulo_ve_el_interior_de_otro` |
| 1070 | El contexto, fuera del universo de modelos | **fun 1**: `El_universo_de_modelos_es_el_declarado` |
| 1071 | El contexto, fuera del recorrido con filas | **int 1**: `Recorre_todos_los_contextos_que_tienen_migraciones` |
| 1072 | El contexto, fuera de los sitios que declaran un filtro | **fun 1**: `Los_filtros_globales_se_definen_solo_en_los_contextos_de_modulo` |
| 1073 | Sin el interceptor de la bandeja | **ninguno**: Compras no escribe nada, así que el interceptor no tiene qué hacer. Equivalente hoy; lo verá el primer `SaveChanges` del pedido del 3.7 (punto 13) |
| 1074 | Sin la convención de nombres | **ninguno**, ni `mig`: las tres tablas que mapea no se migran desde aquí y no tiene ninguna propia. Equivalente hoy; lo verá la primera tabla del pedido (punto 13) |
| 1075 | **Arnés**: el historial de Compras, sin declarar en `EsquemaDeIdentidadTests` | **int 1**: `Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema` |
| 1076 | **Arnés**: el patrón de los contextos, con una letra cambiada | sobre `4955575`, **mig**: «No hay ningún contexto de módulo en src/Modules/*/Bastion.*.Infrastructure/Persistencia: el patrón de este guion no mira donde están» |
| 1077 | La comparación de la lista, al revés | sobre `4955575`, **mig**: «La lista MODULOS de este guion no es la de los contextos que hay en el disco», con la lista buena |

### La puerta del 3.4, contestada (2026-11-02)

De los cinco huecos de *El orden de trabajo, C*, tres tienen más de una salida viable, y se
preguntaron juntos antes de escribir nada, como pide el propio orden de trabajo. Las cuatro
respuestas son la opción recomendada:

1. **La transacción del manejador es la del contexto de su módulo** (hueco 5). El manejador
   declara su contexto, `ManejadorDeEvento<TEvento, TContexto>`, y el despachador lo resuelve en
   un ámbito nuevo, abre en él la transacción, mira la huella dentro, llama al manejador, añade la
   huella a ese contexto y guarda. El interceptor del contexto escribe en el mismo `SaveChanges` el
   evento de respuesta y la auditoría: una conexión y un módulo, con la huella y la bandeja ya
   mapeadas en cada contexto de módulo (ADR-0013 §3 y §6). La otra era una conexión compartida en
   la que se enganchan todos los contextos del ámbito con `UseTransaction`, más general, y que deja
   sin vigilar que un manejador escriba solo en su módulo.
2. **Un choque de versión se reintenta en línea, y si no, a la vuelta** (hueco 5). Ante un
   `DbUpdateConcurrencyException` se deshace la transacción y se reintenta en el acto con un ámbito
   y un contexto nuevos, hasta tres veces, con espera exponencial y *jitter*. Si sigue chocando, la
   fila se queda pendiente sin contar intento, y la vuelta siguiente lo vuelve a probar. Un choque
   no aparca nunca, y cualquier otro fallo cuenta como hoy. Un manejador que chocara siempre, por
   un error suyo, no se aparcaría: lo dirían el registro y la antigüedad, que sube. Las otras eran
   esperar siempre a la vuelta siguiente, que deja el evento detrás de los que vienen después en el
   lote, y contar el choque como un fallo después de los tres, que puede aparcar un documento muy
   disputado.
3. **Quién lo causó y su correlación van en tres columnas de la fila** (hueco 3): el usuario, la
   correlación de la auditoría y el `traceparent` W3C. El interceptor de la bandeja las llena desde
   el ámbito, y el despachador las restaura en el del manejador. La auditoría de su efecto lleva el
   mismo usuario y la misma correlación, su actividad es hija de la traza original y no un enlace,
   y su evento de respuesta hereda las tres sin que el manejador copie nada. La otra era llevarlas
   en `EventoDeIntegracion`, dentro del cuerpo, con el manejador encargado de pasarlas a su
   respuesta.
4. **Una fila sin empresa no se entrega a ningún manejador** (hueco 2). La empresa de una fila es
   la empresa desde la que se actuó (ADR-0013 §8), y el ámbito con esa empresa lo abre solo el
   despachador. Es interno a `BuildingBlocks.Infrastructure` y no está en `IInquilinoActual`, así
   que ningún caso de uso puede cambiar de empresa. Los filtros y la guarda de escribir en otra
   empresa funcionan como en una petición. Una fila escrita bajo `SinInquilino`, como las de la
   semilla, lanza con su motivo, se reintenta y a la quinta se aparca, y se decide con el primer
   manejador que la necesite delante. La otra era un motivo nuevo, `ManejoDeEventos`, con el que
   ese manejador vería todas las empresas, que es justo lo que el hueco cierra.

Lo que no se preguntó, porque tiene una salida obvia y se puede cambiar sin tocar datos:

- **el ámbito es uno por manejador y por intento, no solo por evento** (hueco 1): cada manejador
  tiene su transacción, y cada reintento, su contexto limpio. Así, lo que deja a medias un manejador
  no se lo guarda ni el siguiente evento ni el siguiente manejador del mismo evento;
- **el indicador de aparcados sin resolver es un indicador observable con su recuento** (hueco 4),
  `bastion.bandeja_de_salida.aparcados_sin_resolver`, que se lee de la base en cada vuelta, para
  todas las empresas. El contador de aparcados que ya existe se reinicia con el proceso, y este no;
- **el manejador de prueba vive en los tests de integración**, y la carrera es de dos
  transacciones de verdad sobre un documento con su `xmin`.

### Tomadas por el agente de desarrollo — ítem 3.4, al escribirlo y al medirlo (2026-11-03)

Cada tema del orden de trabajo fue en su subida, y el segundo en dos commits: la puerta, en
`ef05f8a`; cada manejador en su ámbito y en su transacción, en `e840456` y `bacf441`; la empresa de
su fila, en `ce3d7dd`; quién lo causó, en `4f26e2b`; el choque, en `691e07d`; los aparcados, en
`5ce629d`; y el ADR-0078, en `2371e9a`. La tanda del cierre dejó dos en verde, y cada una se cubrió
en su commit y en su subida, `3f7d6e6` y `a70195b`. Lo que se decidió sin preguntar, porque el
criterio no lo decía o porque salió al escribirlo o al medirlo:

1. **El contexto lo declara el registro, y no el manejador** (`e840456`, ADR-0078 §1). La puerta
   decía `ManejadorDeEvento<TEvento, TContexto>`, pero el manejador vive en Application, que no ve
   EF Core. Lo declara el cableado del módulo, en Infrastructure:
   `AgregarManejadorDeEvento<TManejador, TContexto>()`. A qué evento atiende lo dice su base, leída
   al registrarlo y sin construirlo, y uno que no la hereda falla en el arranque, que ve la 1084.
   `EventoQueAtiende` desaparece, y el ADR enmienda el §4 del 0013.
2. **La huella se añade antes de llamar al manejador** (`e840456`), para que entre en el primer
   `SaveChanges` de su efecto; la respuesta de la puerta la añadía después. Si el manejador no
   confirma, el contexto se vacía y la huella se guarda sola: lo que no confirma no lo guarda nadie,
   como detrás de HTTP. Lo ven la 1080, que no vacía el contexto, y la 1081, que devuelve la huella
   a su sitio de antes, aparte y después.
3. **Sin puntos de guardado, y con un caso que confirma dos veces** (`bacf441`). Con un solo
   `SaveChanges` por manejador se abre un solo punto de guardado, las cuatro filas siguen llevando
   el mismo `xmin`, y la 1082, que deja de apagarlos, salió verde. El caso nuevo usa un manejador
   que confirma dos veces: la huella entra con el primer guardado y las otras tres filas con el
   segundo. En el mismo commit, un manejador recibe un evento al que no atiende, que la 1085 y la
   1086 habían dejado sin ver.
4. **El publicador marca cada fila en un ámbito propio, releyéndola** (`e840456`). El de la vuelta
   guarda el cerrojo y lee el lote sin rastrearlo; con su contexto compartido, un fallo al guardar
   la marca dejaría la fila publicada y con error a la vez. La 1125 marca en el contexto de la
   vuelta y sale en verde: solo se distingue si falla guardar la marca, y ningún caso sabe hacerlo
   fallar. Un caso que lo viera necesita un arnés que inyecte ese fallo en el guardado del
   publicador, y se propone: no está en el PLAN.
5. **El despachador recibe la fila, vive en Infrastructure y es *singleton*** (`ce3d7dd`, ADR-0078
   §2). La empresa sale de la fila, así que quien llama no la elige, y ningún caso de uso lo
   alcanza. Lo ve la 1090: con un despachador de ámbito dentro del publicador, que es *singleton*,
   el anfitrión no arranca, y lo dicen los tres casos de `LaTrazaVaEnLaMismaTransaccionTests`, que
   validan los ámbitos al arrancar. El carril funcional no la ve.
6. **Una fila sin empresa lanza con su motivo, y los cero manejadores se miran antes** (`ce3d7dd`,
   ADR-0078 §4). El alta que hace la semilla no tiene empresa ni manejadores, y se publica igual. Lo
   ven la 1095, que entrega la fila sin empresa con `Guid.Empty`, y la 1096, que mira los
   manejadores después de la empresa.
7. **La correlación de la auditoría pasa de una por `SaveChanges` a una por ámbito** (`4f26e2b`,
   ADR-0078 §5): con la de antes, la fila de la bandeja solo habría podido llevar la de su guardado,
   y no la de su operación. Lo ven la 1097, por el alta por la API y por el manejador, y la 1098,
   que da una nueva en cada lectura. Los casos de la auditoría, en el carril `aud`, no ven la 1097:
   la ven los de la bandeja.
8. **La actividad del manejador es hija de la traza de la fila, y la API escucha su fuente**
   (`4f26e2b`, `a70195b`). Los casos de integración ponen su propio oyente, así que quitar de
   `Program.cs` la línea que da de alta la fuente, la 1107, salió verde en los dos carriles.
   `a70195b` levanta la API en el carril rápido y afirma que una fuente con el nombre de la de la
   bandeja tiene oyentes, y su pareja, que una que nadie da de alta no los tiene: si un oyente
   escuchara todas las fuentes, la primera afirmación no diría nada.
9. **Al cerrarse, los dos ámbitos internos devuelven lo de fuera, aunque en el camino del
   despachador no haga falta** (`ce3d7dd`, `4f26e2b`). La 1094 quita esa devolución en el de la
   empresa, y la 1105 en el de la causa, y salen en verde, como se esperaba: lo que un método
   `async` pone en un `AsyncLocal` no sube a quien lo llama, y el despachador abre los dos dentro de
   uno. Se quedan, porque quien abriera uno y siguiera en el mismo método después de cerrarlo
   seguiría dentro sin ella. Son equivalentes hoy.
10. **Un choque no cuenta intento, y la captura del publicador cubre también la marca** (`691e07d`,
    ADR-0078 §7). No se escribió una guarda de «ya despachado» para un choque al marcar: la fila de
    la bandeja no tiene testigo de concurrencia, se relee con `Single` y el publicador tiene el
    cerrojo, así que ningún caso la podría ejercer. La espera va por el `TimeProvider` del
    contenedor, y los casos leen cada temporizador que apunta, no un cronómetro: lo ven la 1112, la
    1113 y la 1114. El azar es el de `Random.Shared`, que basta porque solo separa en el tiempo a
    los que chocan, y CA5394 no salta con él.
11. **El espía de los aparcados ve un medidor de antes, porque lo pone el caso** (`3f7d6e6`). La
    1124 quitó el filtro con el que aparta los medidores que ya estaban al estrenarse, y salió
    verde: los casos de `ColeccionDeLaApi` van de uno en uno y cierran su host, así que al llegar a
    este no quedaba ninguno. El comentario del caso decía que en el proceso estaba el host de la
    API, con su publicador, y no estaba. Ahora el caso arranca antes del espía otra bandeja con su
    publicador, sobre otra base, y afirma que el espía la ha visto. **El ADR-0078 dice lo mismo que
    el comentario viejo**, en el último párrafo de *Cómo se prueba*, y el mensaje de `5ce629d`
    también. El ADR está aceptado y no se reescribe: o lo enmienda uno nuevo, o se corrige por
    dentro si el usuario lo encarga, y se le pregunta. El commit no se toca.
12. **Tres mutaciones no compilaban en su primera forma**, con `TreatWarningsAsErrors`: la 1098 daba
    CA1822, la 1099 CS9113 y la 1116 CS8604. Se reescribieron para que compilen sin cambiar lo que
    rompen, y se midieron sobre `a70195b`: las tres, en rojo.
13. **Lo que queda abierto lo dice el ADR-0078 en *Lo que no cubre***: dentro de un manejador,
    `IUsuarioActual` no tiene token, así que su `EmpresaId` y su `UsuarioId` lanzan y `Tiene` no
    concede nada; un caso de uso que convirtiera un choque en un `Resultado` se saltaría el
    reintento en el acto; el `EventId` 8200 lo usan dos registros; y la retención de la bandeja
    sigue pendiente. Las dos primeras se deciden con el primer manejador real, el de la saga del
    3.8; el `EventId`, aparte, porque renumerarlo cambia lo que filtran los paneles.

**Las tandas, de la 1078 a la 1125, se escribieron antes de medir**, cada mutación con su «espera»,
en el *scratchpad*: un guion `mutarNN.py` por tanda, con sus mutaciones dentro. La del tema 2 fue
sobre su commit, con una segunda pasada sobre el que cubría lo que dejó en verde; la de los temas 3
a 6 fue una sola, sobre el commit del ADR, con otra pasada sobre `a70195b` para las dos que dejó en
verde y las tres que no compilaban. Cada una se aplicó sola sobre el árbol limpio del repositorio,
se revirtió con `git restore --source=HEAD`, se tocó con la fecha de ahora, y al acabar cada tanda
el árbol restaurado compiló y la porcelana salió vacía. Los registros, `tandaNN.log`:

```bash
python -X utf8 mutar37.py base 1078-1091                  # sobre e840456
python -X utf8 mutar37b.py base 1082 1085 1086            # sobre bacf441
python -X utf8 mutar38.py base 1092-1125                  # sobre 2371e9a
python -X utf8 mutar39.py base 1098 1099 1107 1116 1124   # sobre a70195b
```

Los carriles son `dotnet test` de un proyecto, sin compilar y con plazo de cuelgue: `int`,
`Api.IntegrationTests` filtrado al espacio de nombres `BandejaDeSalida`; `aud`, el mismo proyecto
filtrado a `Auditoria`; `api`, `LaTrazaVaEnLaMismaTransaccionTests`, que levanta un anfitrión de
verdad, con base, y valida los ámbitos al arrancar; y `fun`, `Api.FunctionalTests`, entero. Las
bases, todas a cero, con los casos de cada carril:

- la 37: `int` 25, `fun` 279 y `api` 3; y su segunda pasada, con los dos casos nuevos, `int` 27;
- la 38: `int` 43, `aud` 20 y `fun` 285;
- y la 39: `int` 43 y `fun` 286, con el caso nuevo de `a70195b`.

| # | Mutación | Lo que la vio, por nombre |
|---|---|---|
| 1078 | Un ámbito para todos los manejadores del evento | **int 1**: `Lo_que_deja_a_medias_un_manejador_no_lo_guarda_el_siguiente` |
| 1079 | Sin la transacción: ni `BeginTransaction` ni `Commit` | **int 1**: `Si_el_manejador_falla_despues_de_confirmar_no_queda_nada_de_lo_confirmado` |
| 1080 | Sin vaciar el contexto cuando el manejador no confirma | **int 1**: `Lo_que_el_manejador_no_confirma_no_lo_guarda_nadie_y_su_huella_si` |
| 1081 | La huella, después del manejador y aparte, como antes del 3.4 | **int 2**: `Lo_que_deja_a_medias_un_manejador_no_lo_guarda_el_siguiente` y `Lo_que_el_manejador_no_confirma_no_lo_guarda_nadie_y_su_huella_si` |
| 1082 | Con puntos de guardado | sobre `e840456`, **ninguno**; sobre `bacf441`, **int 1**: `Dos_confirmaciones_de_un_manejador_son_de_la_misma_transaccion`, el caso nuevo (punto 3) |
| 1083 | La mirada a la huella busca otro consumidor | **int 3**: `Lo_que_el_manejador_no_confirma_no_lo_guarda_nadie_y_su_huella_si`, `Cada_consumidor_tiene_su_turno_aunque_sea_el_mismo_evento` y `El_mismo_evento_dos_veces_deja_su_efecto_una_sola` |
| 1084 | El registro acepta un manejador que no hereda de la base | **int 1**: `Un_manejador_que_no_hereda_de_la_base_no_llega_a_registrarse` |
| 1085 | El registro lee de la base cualquier evento | sobre `e840456`, **ninguno**; sobre `bacf441`, **int 1**: `Un_manejador_no_recibe_un_evento_al_que_no_atiende`, el caso nuevo (punto 3) |
| 1086 | El despachador no filtra por el evento que atiende cada manejador | sobre `e840456`, **ninguno**; sobre `bacf441`, **int 1**: `Un_manejador_no_recibe_un_evento_al_que_no_atiende`, el caso nuevo (punto 3) |
| 1087 | La transacción, en el contexto de otro ámbito | **int 2**: `El_efecto_su_traza_su_huella_y_su_respuesta_son_de_la_misma_transaccion` y `Si_el_manejador_falla_despues_de_confirmar_no_queda_nada_de_lo_confirmado` |
| 1088 | El publicador no guarda la marca | **int 7**: `Dar_de_alta_una_empresa_deja_su_evento_en_la_cola_y_el_host_lo_publica`, `El_alta_que_hace_la_semilla_se_publica_igual_y_dice_por_que_no_tiene_empresa`, `El_fallo_de_uno_no_es_el_fallo_de_la_vuelta`, `Lo_que_esta_pendiente_acaba_publicado`, `Un_manejador_que_falla_la_primera_vez_acaba_recibiendo_el_evento`, `Y_con_la_cola_vacia_la_edad_vuelve_a_cero` y `Reabrir_con_motivo_lo_abre_y_deja_el_evento_con_el_motivo_dentro` |
| 1089 | El publicador no guarda el fallo | **int 3**: `El_fallo_de_uno_no_es_el_fallo_de_la_vuelta`, `Un_manejador_que_falla_la_primera_vez_acaba_recibiendo_el_evento` y `Un_manejador_que_no_funciona_nunca_acaba_aparcando_su_evento` |
| 1090 | El despachador, de ámbito y no *singleton* | **api 3**: `La_fila_y_su_traza_las_escribe_LA_MISMA_transaccion`, `Un_guardado_que_revienta_no_deja_ni_la_fila_ni_su_traza` y `Y_uno_que_va_bien_deja_las_dos_cosas`, porque el anfitrión no arranca; **int 4**: `Dar_de_alta_una_empresa_deja_su_evento_en_la_cola_y_el_host_lo_publica`, `El_alta_que_hace_la_semilla_se_publica_igual_y_dice_por_que_no_tiene_empresa`, `Reabrir_con_motivo_lo_abre_y_deja_el_evento_con_el_motivo_dentro` y `Reabrir_sin_motivo_es_400_y_deja_el_ejercicio_cerrado`; `fun`, ninguno (punto 5) |
| 1091 | **Arnés**: el contexto de prueba, sin el interceptor de la bandeja | **int 1**: `El_efecto_su_traza_su_huella_y_su_respuesta_son_de_la_misma_transaccion` |
| 1092 | El despachador no abre el ámbito de la empresa de la fila | **int 10**: `El_efecto_su_traza_y_su_respuesta_llevan_la_empresa_de_la_fila`, `El_manejador_solo_ve_lo_de_la_empresa_de_su_fila`, `Escribir_en_otra_empresa_lo_para_la_guarda_de_la_auditoria`, `El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila`, `La_actividad_del_manejador_es_hija_de_la_traza_de_su_fila`, `Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores`, `Dos_confirmaciones_de_un_manejador_son_de_la_misma_transaccion`, `El_efecto_su_traza_su_huella_y_su_respuesta_son_de_la_misma_transaccion`, `Lo_que_deja_a_medias_un_manejador_no_lo_guarda_el_siguiente` y `Lo_que_el_manejador_no_confirma_no_lo_guarda_nadie_y_su_huella_si`: los diez que entregan una fila con empresa a un manejador que escribe |
| 1093 | El ámbito de la empresa no aparta el sin inquilino de la vuelta | **int 10**: los mismos diez que la 1092 |
| 1094 | Al cerrarse el ámbito de la empresa, no devuelve lo de fuera | **ninguno**: equivalente en el camino del despachador (punto 9) |
| 1095 | Una fila sin empresa se entrega con `Guid.Empty` | **int 2**: `El_publicador_aparca_a_la_quinta_una_fila_sin_empresa_con_su_motivo` y `Una_fila_sin_empresa_no_se_entrega_y_dice_su_motivo` |
| 1096 | Los cero manejadores se miran después de la empresa | **int 2**: `El_alta_que_hace_la_semilla_se_publica_igual_y_dice_por_que_no_tiene_empresa` y `El_fallo_de_uno_no_es_el_fallo_de_la_vuelta` |
| 1097 | La correlación de la auditoría, una por `SaveChanges` | **int 3**: `El_alta_lleva_quien_la_pidio_la_correlacion_de_su_auditoria_y_su_traza`, `El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila` y `Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores`; `aud`, ninguno (punto 7) |
| 1098 | La correlación, una nueva en cada lectura | sobre `2371e9a`, no compila (punto 12); sobre `a70195b`, **fun 1**: `La_correlacion_es_una_por_ambito_y_no_una_por_lectura`; **int 1**: `El_alta_lleva_quien_la_pidio_la_correlacion_de_su_auditoria_y_su_traza` |
| 1099 | El interceptor de la bandeja escribe la fila sin su causa | sobre `2371e9a`, no compila (punto 12); sobre `a70195b`, **int 4**: `El_alta_lleva_quien_la_pidio_la_correlacion_de_su_auditoria_y_su_traza`, `El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila`, `La_actividad_del_manejador_es_hija_de_la_traza_de_su_fila` y `Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores` |
| 1100 | El despachador no restaura la causa de la fila | **int 2**: `El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila` y `Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores` |
| 1101 | La fila sin correlación recibe una nueva por manejador | **int 1**: `Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores` |
| 1102 | La actividad del manejador, sin padre | **int 1**: `La_actividad_del_manejador_es_hija_de_la_traza_de_su_fila` |
| 1103 | La actividad de un manejador que falla, sin el estado de error | **int 1**: `La_actividad_de_un_manejador_que_falla_queda_en_error` |
| 1104 | La respuesta hereda la traza de la fila, y no la de la actividad del manejador | **int 1**: `La_actividad_del_manejador_es_hija_de_la_traza_de_su_fila` |
| 1105 | Al cerrarse el ámbito de la causa, no devuelve la de fuera | **ninguno**, en los dos carriles: equivalente, como la 1094 (punto 9) |
| 1106 | El usuario de la fila no se restaura | **int 1**: `El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila` |
| 1107 | La API no da de alta la fuente de la bandeja | sobre `2371e9a`, **ninguno**, en los dos carriles; sobre `a70195b`, **fun 1**: `Y_la_actividad_de_cada_manejador_la_escucha_la_telemetria_de_la_api`, el caso nuevo (punto 8); `int`, ninguno |
| 1108 | Dos reintentos, y no tres | **int 3**: `El_cuarto_choque_sale_del_despachador_y_no_deja_huella`, `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar` y `Tres_choques_se_reintentan_en_el_acto_cada_uno_con_un_contexto_nuevo` |
| 1109 | Sin tope de reintentos | **int 2**: `El_cuarto_choque_sale_del_despachador_y_no_deja_huella` y `Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica` |
| 1110 | Cualquier excepción se reintenta en el acto | **int 3**: `La_actividad_de_un_manejador_que_falla_queda_en_error`, `Un_manejador_que_falla_la_primera_vez_acaba_recibiendo_el_evento` y `Otro_fallo_no_se_reintenta_en_el_acto` |
| 1111 | El reintento reutiliza el ámbito del intento que chocó | **int 4**: `El_cuarto_choque_sale_del_despachador_y_no_deja_huella`, `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar`, `Tres_choques_se_reintentan_en_el_acto_cada_uno_con_un_contexto_nuevo` y `Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica` |
| 1112 | La espera no se dobla | **int 1**: `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar` |
| 1113 | La espera, sin azar | **int 1**: `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar` |
| 1114 | La espera, sin el reloj inyectado | **int 1**: `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar` |
| 1115 | El reintento se anota con otro `EventId` | **int 2**: `El_cuarto_choque_sale_del_despachador_y_no_deja_huella` y `Tres_choques_se_reintentan_en_el_acto_cada_uno_con_un_contexto_nuevo` |
| 1116 | El publicador cuenta como un fallo el choque que no cesa | sobre `2371e9a`, no compila (punto 12); sobre `a70195b`, **int 1**: `Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica` |
| 1117 | El choque que no cesa se anota con otro `EventId` | **int 1**: `Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica` |
| 1118 | La primera espera, de 100 ms | **fun 1**: `Un_choque_se_reintenta_tres_veces_y_todas_sus_esperas_caben_en_una_vuelta`; `int`, ninguno |
| 1119 | **Arnés**: el manejador de prueba no choca nunca | **int 4**: `El_cuarto_choque_sale_del_despachador_y_no_deja_huella`, `La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar`, `Tres_choques_se_reintentan_en_el_acto_cada_uno_con_un_contexto_nuevo` y `Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica` |
| 1120 | El publicador no anota los aparcados | **int 1**: `El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno` |
| 1121 | Se cuentan los pendientes, y no los aparcados | **int 1**: `El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno` |
| 1122 | Sin los aparcados que no tienen empresa | **int 1**: `El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno` |
| 1123 | El indicador no publica lo que le dicen | **fun 1**: `Y_dice_cuantos_aparcados_hay_sin_resolver_con_lo_que_lee_el_publicador`; **int 1**: `El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno` |
| 1124 | **Arnés**: el espía lee también los medidores de antes | sobre `2371e9a`, **ninguno**; sobre `a70195b`, **int 1**: `El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno`, con el medidor de antes que pone el caso desde `3f7d6e6` (punto 11) |
| 1125 | El publicador marca en el contexto de la vuelta | **ninguno**: solo se distingue si falla guardar la marca (punto 4) |

## Estado actual

**El 3.4, hecho el 2026-11-03**, en la rama `3.4-la-bandeja`, abierta el 2026-11-02 desde `main` en
`3af3039`. Su puerta, en *Decisiones tomadas → La puerta del 3.4, contestada*; lo que se decidió al
escribirlo y al medirlo, con sus tandas, en *Decisiones tomadas → Tomadas por el agente de
desarrollo — ítem 3.4*. Lleva un ADR, el 0078, así que el siguiente es el **0079**. Sus mutaciones
van de la **1078** a la **1125**, y las siguientes empiezan en la **1126**.

**Los commits, por este orden, y las subidas, una por tema, cada una con su run de rama** sobre su
último commit (los trabajos, Backend, Frontal y Humo). Todos en verde:

1. `ef05f8a`, «docs(plan): la puerta del 3.4, contestada, y los runs del cierre del 3.3»:
   **41489659317**, Backend `125429031925`, Frontal `125429032594` y Humo `125436726969`.
2. `e840456`, «feat(bandeja): cada manejador en su ámbito y en su transacción»: **41493657823**,
   Backend `125442860642`, Frontal `125442861255` y Humo `125448039775`.
3. `bacf441`, «test(bandeja): dos confirmaciones, y el evento al que no se atiende»:
   **41496524414**, Backend `125452810905`, Frontal `125452811196` y Humo `125460851090`.
4. `ce3d7dd`, «feat(bandeja): el manejador, con la empresa de su fila»: **41499393725**, Backend
   `125462655542`, Frontal `125462655940` y Humo `125470257768`.
5. `4f26e2b`, «feat(bandeja): quién lo causó, su correlación y su traza»: **41505945696**, Backend
   `125485181649`, Frontal `125485182089` y Humo `125494130458`.
6. `691e07d`, «feat(bandeja): un choque de versión se reintenta en el acto»: **41554530787**,
   Backend `125650061494`, Frontal `125650062051` y Humo `125655429906`.
7. `5ce629d`, «feat(bandeja): cuántos aparcados hay sin resolver»: **41556584416**, Backend
   `125656842288`, Frontal `125656842654` y Humo `125662112444`.
8. `2371e9a`, «docs(adr): el manejador corre en su transacción, con su fila»: **41558631762**,
   Frontal `125663569615`, Backend `125663569893` y Humo `125667627178`.
9. `3f7d6e6`, «test(bandeja): el espía de los aparcados ve un medidor de antes»: **41562447563**,
   Backend `125675966957`, Frontal `125675967254` y Humo `125680837086`.
10. `a70195b`, «test(bandeja): la API escucha la fuente de las trazas de la bandeja»:
    **41564360535**, Backend `125682181561`, Frontal `125682182003` y Humo `125687005550`.
11. `c2ce759`, «docs(plan): cierra el 3.4, con sus tandas y la batería»: **41566323913**, Backend
    `125688640063`, Frontal `125688640334` y Humo `125693191744`. Y `main` avanzó con `--ff-only`
    de `3af3039` a `c2ce759`, once commits, y su run, el **41568164543**, con Backend
    `125694574526`, Frontal `125694574685` y Humo `125699134592`, en verde. La rama se borró
    después, en local y en el remoto. Los anota el primer commit del epílogo del 3.4.

Los runs se leyeron con `python -X utf8 vigilar31.py <sha> <rama>`, el guion del *scratchpad* que
pregunta con `curl`.

**La batería de `AGENTS.md`, entera y en su orden, sobre `a70195b`**, con `bash bateria32.sh`, el
guion del *scratchpad* que deja cada paso en `artifacts/bateria-<paso>.log` y su código de salida en
una línea. Los diecinueve pasos salen con 0, y la porcelana, vacía. El cierre solo toca el PLAN y el
README:

- `npm run api`, sin cambios en `esquema.ts`; las migraciones, al día; **173** operaciones en
  **109** rutas y **245** tipos de **253** sitios, como la base, con los dos generadores en
  `--comprobar`: el 3.4 no toca el borde HTTP;
- `typecheck`, `lint` y `format:check`, limpios; **428** casos de jsdom en **51** ficheros, como la
  base; `canal-de-la-consola.sh`, con la marca, ningún aviso de `act()` y siete trozos para las ocho
  cadenas; y el carril del navegador, **22** casos en **1** fichero, todos en verde;
- el arranque, **408/450 KiB** en 4 ficheros; la ruta diferida más pesada, `PaginaDelRecuento`,
  **175/192 KiB**, de 21 rutas; el total servido, **838 KiB**, informativo; y lo comprimido,
  **145**, **62** y **302 KiB**, también informativo. Todo como la base: el 3.4 no toca el frontal;
- el build, con 0 errores y 0 avisos, y `dotnet format`, limpio;
- **1733** casos en el carril rápido y **806** en el de integración, todos en verde y contados con
  `recuento-de-tests.sh` y las dos listas del *workflow*: 7 más en el rápido, los de
  `Api.FunctionalTests`, que pasa de 279 a 286; y 23 más en el de integración, todos de
  `Api.IntegrationTests`, de 688 a 711, con `Organizacion.IntegrationTests` en 95. La batería tardó
  14 min, 10 de ellos en integración;
- y las dependencias, con `python -X utf8 scripts/dependencias-por-conjuntos.py 3af3039 HEAD`: 125
  pares, 40 `Project` y 50 `packages.lock.json` en los dos lados, sin nada añadido ni retirado; y en
  el frontal, 443 entradas en los dos, sin nada añadido ni retirado.

**El humo, con Docker, sobre `a70195b`**, con `bash humo34.sh`, el del 3.3 con un paso más, en su
propio proyecto de *compose*, `bastion-humo-34`, con el entorno generado en el *scratchpad* y sus
puertos en el 45xxx. El migrador sale con 0 y escribe siete `EsquemaMigrado`, en su orden. El paso
nuevo lee del motor que el historial de Auditoría tiene `LaCausaDeCadaEvento`, una vez; que las tres
columnas de la causa son las del ADR, `contexto_de_traza` de 55 caracteres y `correlacion_id` y
`usuario_id` de tipo `uuid`, las tres nulas; que la fila de la bandeja que deja la semilla está
publicada, sin empresa y con su correlación; y que no hay aparcados. Compras sigue con su historial
y su fila, `public`, sin historial, y las cuatro compartidas, en `auditoria`; la salud, `Healthy`, y
sin credenciales, `401`; y el segundo arranque sale con 0, con el rol del sistema con sus 116
permisos y la partición del libro vuelta a crear. Después, el `down -v` de su proyecto, el de por
omisión intacto y el entorno borrado. **0 fallos**, en `humo34-a70195b.log`.

**El 3.3, hecho el 2026-11-02**, en la rama `3.3-los-puertos`, abierta el mismo día desde `main` en
`714cc6e`. Su puerta, en *Decisiones tomadas → La puerta del 3.3, contestada*; lo que se decidió al
escribirlo y al medirlo, con sus tandas, en *Decisiones tomadas → Tomadas por el agente de
desarrollo — ítem 3.3*. Lleva dos ADR, el 0076 y el 0077, así que el siguiente es el **0078**. Sus
mutaciones van de la **972** a la **1077**, y las siguientes empiezan en la **1078**.

**Los commits, por este orden, y las subidas, una por tema, cada una con su run de rama** sobre su
último commit (los trabajos, Backend, Frontal y Humo). Todos en verde:

1. `1fd5efb`, «docs(plan): la puerta del 3.3, contestada, y los runs del cierre del 3.2»:
   **41406461687**, Frontal `125149480802`, Backend `125149481059` y Humo `125154172979`.
2. `c12f3e2`, «feat(catalogo): la tarifa, de venta o de compra, y su divisa en ISO», y `7f867bb`,
   «test(catalogo): la vuelta de la migración de la divisa, con sus paradas», en una subida:
   **41417657688**, Frontal `125185337223`, Backend `125185337542` y Humo `125190096746`.
3. `eead063`, «feat(catalogo): el listado de tarifas, con su tipo y su divisa»: **41419690432**,
   Backend `125191987417`, Frontal `125191987728` y Humo `125198207960`.
4. `e26217d`, «feat(terceros): una tarifa por rol, la de venta y la de compra»: **41439001428**,
   Backend `125255856982`, Frontal `125255857396` y Humo `125259830236`.
5. `ae253c1`, «feat(catalogo): el precio de compra, o el motivo por el que no lo hay», y
   `1e47d47`, «test(catalogo): la cantidad del precio por HTTP, en el carril rápido», en una
   subida: **41450137647**, Frontal `125293022371`, Backend `125293022894` y Humo `125298014263`.
6. `82cf007`, «feat(compras): el esquema, el contexto y su primera migración»: **41458877521**,
   Frontal `125322497896`, Backend `125322497989` y Humo `125330167929`.
7. `4955575`, «ci(migraciones): la lista de módulos, contra los contextos del disco»:
   **41478853277**, Frontal `125391517899`, Backend `125391518430` y Humo `125399695148`.
8. `3af3039`, «docs(plan): cierra el 3.3, con sus tandas y la batería», el que lo cierra: en la
   rama, **41482866156**, Frontal `125405467025`, Backend `125405467241` y Humo
   `125413836234`; y en `main`, tras el avance rápido, **41486166333**, Frontal `125416978916`,
   Backend `125416978259` y Humo `125425145922`. La rama se borró después, en local y en el remoto.

Los runs se leyeron con `python -X utf8 vigilar31.py <sha> <rama>`, el guion del *scratchpad* que
pregunta con `curl`.

**La batería de `AGENTS.md`, entera y en su orden, sobre `4955575`**, con `bash bateria32.sh`, el
guion del *scratchpad* que deja cada paso en `artifacts/bateria-<paso>.log` y su código de salida
en una línea. Los diecinueve pasos salen con 0, y la porcelana, vacía. El cierre solo toca el PLAN
y el README:

- `npm run api`, sin cambios en `esquema.ts`; las migraciones, al día, con la lista de módulos
  igual a la de los contextos del disco; **173** operaciones en **109** rutas, dos y una más que la
  base: los dos subrecursos del tercero, en lugar del de la tarifa asignada; y **245** tipos de
  **253** sitios, cuatro más: `tarifa-tipo-no-valido`, `tarifa-tipo-distinto-del-codigo`,
  `tercero-tarifa-de-otro-tipo` y `tercero-tarifa-sin-su-rol`; con los dos generadores en
  `--comprobar`. Las rutas, contadas sobre `docs/api/openapi.json`;
- `typecheck`, `lint` y `format:check`, limpios; **428** casos de jsdom en **51** ficheros, dos más
  que la base; `canal-de-la-consola.sh`, con la marca, ningún aviso de `act()` y siete trozos para
  las ocho cadenas; y el carril del navegador, **22** casos en **1** fichero, todos en verde;
- el arranque, **408/450 KiB** en 4 ficheros, como la base; la ruta diferida más pesada,
  `PaginaDelRecuento`, **175/192 KiB**, de 21 rutas; el total servido, **838 KiB**, informativo; y
  lo comprimido, **145**, **62** y **302 KiB**, también informativo;
- el build, con 0 errores y 0 avisos, y `dotnet format`, limpio;
- **1726** casos en el carril rápido y **783** en el de integración, todos en verde y contados con
  `recuento-de-tests.sh` y las dos listas del *workflow*: 59 más en el rápido, y en el de
  integración, 19, de ellos 18 en `Api.IntegrationTests` (688) y uno en
  `Organizacion.IntegrationTests` (95). La batería tardó 14 min, 10 de ellos en integración;
- y las dependencias, con `python -X utf8 scripts/dependencias-por-conjuntos.py 714cc6e HEAD`: 125
  pares en los dos lados, sin nada añadido ni retirado; de 35 `Project` a 40, y de 45
  `packages.lock.json` a 50, los cinco de Compras, que deja de ser un andamio; y en el frontal, 443
  entradas en los dos, sin nada añadido ni retirado.

**El humo, con Docker, sobre `4955575`**, con `bash humo33.sh`, un guion del *scratchpad*, en su
propio proyecto de *compose*, `bastion-humo-33`, con el entorno generado en el *scratchpad* y sus
puertos en el 45xxx. El migrador sale con 0 y escribe siete `EsquemaMigrado`, en su orden, con
Compras el último; el esquema `compras` existe y tiene solo su historial, con una fila, la de su
primera migración; `public` no tiene historial, y las cuatro tablas compartidas siguen en
`auditoria`; la salud, `Healthy`, y sin credenciales, `401`; y el segundo arranque sale con 0, con
el rol del sistema con el catálogo entero, 116 permisos, y Compras igual que antes. Después, el
`down -v` de su proyecto, el de por omisión intacto y el entorno borrado. **0 fallos**, en
`humo33-cierre.txt`.

**El 3.2, hecho el 2026-11-02**, en la rama `3.2-la-exportacion`, abierta el 2026-10-27 desde
`main` en `eef05f7`. Su puerta y la pregunta del apóstrofo, en *Decisiones tomadas → La puerta del
3.2, contestada* y *→ La pregunta del apóstrofo, contestada*; lo que se decidió al escribirlo y al
medirlo, con sus tandas y la medida sobre la empresa C, en *Decisiones tomadas → Tomadas por el
agente de desarrollo — ítem 3.2*. Lleva cuatro ADR, del 0072 al 0075, así que el siguiente es el
**0076**. Sus mutaciones van de la **792** a la **971**, y las siguientes empiezan en la **972**.

**Los commits, por este orden, y las subidas, una por tema, cada una con su run de rama** sobre su
último commit (los trabajos, Backend, Frontal y Humo). Todos en verde:

1. `bb5d843`, «docs(plan): la puerta del 3.2, contestada, y los runs del cierre del 3.1»:
   **40453767870**, Backend `122022231824`, Frontal `122022232124` y Humo `122026331496`.
2. `ace1b7c`, «feat(listados): tres piezas por listado, y un ?q= que no mira es 400», y
   `3a03002`, «docs(adr): un listado enlaza tres piezas, y el texto, quien lo mira», en una
   subida: **40457645447**, Backend `122034601177`, Frontal `122034601625` y Humo `122038630171`.
3. `823fa7f`, «feat(exportacion): el escritor del CSV, la foto y el recorrido», y `f1c3244`,
   «docs(adr): la exportación recorre el listado en una foto»: **40461092260**, Backend
   `122045624765`, Frontal `122045624988` y Humo `122050001565`.
4. `619190b`, «fix(exportacion): dos guardas que la tanda encontró muertas»: **40462895062**,
   Backend `122051399953`, Frontal `122051400134` y Humo `122054877625`.
5. `b1e726f`, «feat(maestros): seis puertos nombran por lotes lo que enseña la ficha», y
   `0e16aa6`, «docs(adr): un maestro de otro módulo se nombra por lotes»: **40538124733**, Frontal
   `122308829045`, Backend `122308829487` y Humo `122315074976`.
6. `f0f1909`, «test(maestros): lo que está y no se pregunta, en cuatro puertos»: **40540684711**,
   Backend `122317919078`, Frontal `122317919322` y Humo `122325568354`.
7. `77568cd`, «feat(exportacion): los almacenes y las empresas, enteros en CSV»: **40547362364**,
   Backend `122341451129`, Frontal `122341451578` y Humo `122349404805`.
8. `a995ed0`, «test(exportacion): la foto de cada módulo, contra la base»: **40550364991**,
   Frontal `122352119827`, Backend `122352120450` y Humo `122359502425`.
9. `c4c4650`, «feat(csv): el apóstrofo hace el viaje de vuelta»: **40553216465**, Backend
   `122362240668`, Frontal `122362241157` y Humo `122368604729`.
10. `18c29ae`, «docs(adr): el apóstrofo hace el viaje de vuelta»: **40555626373**, Backend
    `122370805048`, Frontal `122370805731` y Humo `122378260803`.
11. `b81c67c`, «feat(exportacion): los terceros, con la cabecera de su importación»:
    **40558531161**, Backend `122380978386`, Frontal `122380978789` y Humo `122388596442`.
12. `de5d431`, «test(terceros): cada sí o no de la importación, en su campo»: **40581871478**,
    Backend `122463099382`, Frontal `122463099992` y Humo `122470428769`.
13. `9b31c16`, «feat(exportacion): las de Catálogo, con sus maestros nombrados»: **40589679323**,
    Backend `122490332784`, Frontal `122490333083` y Humo `122496382978`.
14. `3a78bda`, «feat(exportacion): las de Inventario, con sus maestros nombrados»:
    **40623207072**, Backend `122601910207`, Frontal `122601910413` y Humo `122606211595`.
15. `f912e12`, «feat(exportacion): el botón, en las trece pantallas»: **40627305546**, Frontal
    `122615205585`, Backend `122615205802` y Humo `122619497585`.
16. `a6666ea`, «fix(exportacion): lo que la tanda del botón dejó en verde»: **40629222458**,
    Backend `122621400588`, Frontal `122621400826` y Humo `122624057434`.
17. `0b7661c`, «test(inventario): lo que la tanda de sus exportaciones dejó en verde»:
    **40630426657**, Frontal `122625263719`, Backend `122625264035` y Humo `122629348734`.
18. `714cc6e`, «docs(plan): cierra el 3.2, con sus tandas, la medida y la batería», el que lo
    cierra: en la rama, **41399969691**, Backend `125129465606`, Frontal `125129465771` y Humo
    `125132226168`; y en `main`, tras el avance rápido, **41401353186**, Backend `125133648911`,
    Frontal `125133649287` y Humo `125138147969`. La rama se borró después, en local y en el
    remoto.

Los runs se leyeron con `python -X utf8 vigilar31.py <sha> <rama>`, un guion del *scratchpad* que
pregunta con `curl`.

**La batería de `AGENTS.md`, entera y en su orden, sobre `0b7661c`**, con `bash bateria32.sh`, un
guion del *scratchpad* que deja cada paso en `artifacts/bateria-<paso>.log` y su código de salida
en una línea. Los diecinueve pasos salen con 0, y la porcelana, vacía. El cierre solo toca el PLAN
y el README:

- `npm run api`, sin cambios en `esquema.ts`; las migraciones, al día; **171** operaciones en
  **108** rutas, trece más que la base, las trece exportaciones; y **241** tipos de **249** sitios,
  uno más, `filtro-no-admitido`; con los dos generadores en `--comprobar`. Las rutas, contadas
  sobre `docs/api/openapi.json`;
- `typecheck`, `lint` y `format:check`, limpios; **426** casos de jsdom en **51** ficheros, 26
  casos y tres ficheros más que la base; `canal-de-la-consola.sh`, con la marca, ningún aviso de
  `act()` y siete trozos para las ocho cadenas; y el carril del navegador, **22** casos en **1**
  fichero, todos en verde;
- el arranque, **408/450 KiB** en 4 ficheros, uno más que la base; la ruta diferida más pesada,
  `PaginaDelRecuento`, **175/192 KiB**, de 21 rutas; el total servido, **836 KiB**, informativo; y
  lo comprimido, **145**, **62** y **301 KiB**, también informativo;
- el build, con 0 errores y 0 avisos, y `dotnet format`, limpio;
- **1667** casos en el carril rápido y **764** en el de integración, todos en verde y contados con
  `recuento-de-tests.sh` y las dos listas del *workflow*: 113 más en el rápido, y en el de
  integración, 24, de ellos 18 en `Api.IntegrationTests` (670) y 6 en
  `Organizacion.IntegrationTests` (94). La batería tardó 24 min, 15 de ellos en integración;
- y las dependencias, con `python -X utf8 scripts/dependencias-por-conjuntos.py eef05f7 HEAD`: 125
  pares y 35 `Project` en los dos lados, y en el frontal, 443 entradas en los dos, sin nada
  añadido ni retirado.

**El 3.1, hecho el 2026-10-27**, en la rama `3.1-la-casa-en-orden`, abierta el 2026-10-26 desde
`main` en `ec023f5`. Lo que pide la respuesta 13 está en el ADR-0071, que enmienda el 0028 y el
0036; lo que se decidió al escribirlo y al medirlo, en *Decisiones tomadas → Tomadas por el agente
de desarrollo — ítem 3.1*. Lleva un ADR, así que el siguiente es el **0072**. Sus mutaciones van de
la **745** a la **791**, y las siguientes empiezan en la **792**.

**Los commits, por este orden, y las subidas, una por tema, cada una con su run de rama** sobre su
último commit (los trabajos, Backend, Frontal y Humo). Todos en verde:

1. `4a51ad0`, «docs(plan): la puerta de la fase 3, contestada, y su checklist»: **40415382989**,
   Frontal `121896286522`, Backend `121896286797` y Humo `121901754984`.
2. `5661033`, «fix(frontal): vitest 4.1.12 y vite 7.3.7, y la auditoría a cero»:
   **40417648295**, Backend `121903848260`, Frontal `121903848630` y Humo `121909207661`.
3. `a5e086b`, «docs(adr): las enmiendas, anotadas en los dos lados, y su regla», y `de17700`,
   «test(arquitectura): la nota del punto no es la del Estado», en una subida: **40419763345**,
   Frontal `121910970468`, Backend `121910970829` y Humo `121915165263`.
4. `73adc0e`, «docs(plan): el PLAN partido, con cada fase cerrada en su historia», y `fa07a91`,
   «docs(agents): la numeración de las mutaciones busca en la historia»: **40421394770**, Backend
   `121916433084`, Frontal `121916433460` y Humo `121919992685`.
5. `6af95ca`, «feat(frontal): los textos de cada módulo viajan con sus rutas»; `571ece4`,
   «test(frontal): un caso rojo no suelta sus cargas en el siguiente»; `301a631`, «ci(frontal):
   un tope por ruta, el total informativo y lo comprimido»; `241750d`, «docs(adr): el presupuesto
   vigila el arranque y cada ruta (ADR-0071)»; y `3ee6082`, «test(frontal): los casos de los
   textos no teclean forma de permiso», en una subida: **40435035345**, Backend `121961570006`,
   Frontal `121961570175` y Humo `121965776910`. Los cuatro primeros tienen en rojo
   `LosPermisosQueNombraElFrontalTests`, y el quinto lo arregla: el punto 12 de las decisiones.
6. `c99cd54`, «docs(plan): cuatro notas abiertas de seguridad, para antes de desplegar»:
   **40436956193**, Frontal `121967812130`, Backend `121967812439` y Humo `121972126362`.
7. `eef05f7`, el que lo cierra, «docs(plan): cierra el 3.1, con sus tandas y la batería», por el
   flujo de la rama: **40449192600**, Backend `122007628995`, Frontal `122007629369` y Humo
   `122011687631`. Y el de `main`, sobre el mismo commit: **40452123280**, Backend
   `122017035207`, Frontal `122017035464` y Humo `122020743870`. Con él en verde, la rama se
   borró, la local y la remota.

Los runs se leyeron con `python -X utf8 vigilar212.py <sha> <rama>`, un guion del *scratchpad*, y
el de `c99cd54`, con su copia `vigilar31.py`, que pregunta con `curl`: el 2026-10-27 el `urllib`
de Python dejó de verificar el certificado de `api.github.com`, y el guion salió con su «sondeo
roto», como debe.

**El tamaño del PLAN**, con `python -X utf8 scripts/comprobar-el-plan-partido.py`, sin argumentos:
de 2 036 368 bytes en 25 280 líneas a 103 376 en 1298, y la historia, 346 621, 523 775 y
1 083 260 bytes en las tres fases. El guion dice también que no se perdió ninguna línea.

**La batería de `AGENTS.md`, entera y en su orden, sobre `3ee6082`**, con `bash bateria31.sh`, un
guion del *scratchpad* que deja cada paso en `artifacts/bateria-<paso>.log` y su código de salida
en una línea. Los diecinueve pasos salen con 0, y la porcelana, vacía. `c99cd54` y el cierre solo
tocan el PLAN y el README:

- `npm run api`, sin cambios en `esquema.ts`; las migraciones, al día; **158** operaciones en
  **95** rutas y **240** tipos de **248** sitios, como en la base, con los dos generadores en
  `--comprobar`. Las rutas, contadas sobre `docs/api/openapi.json`;
- `typecheck`, `lint` y `format:check`, limpios; **400** casos de jsdom en **48** ficheros, ocho
  casos y un fichero más que la base; `canal-de-la-consola.sh`, con la marca, ningún aviso de
  `act()` y siete trozos para las ocho cadenas; y el carril del navegador, **22** casos en **1**
  fichero, todos en verde;
- el arranque, **407/450 KiB** en 4 ficheros, 19 menos que la base; la ruta diferida más pesada,
  `PaginaDelRecuento`, **173/192 KiB**, de 21 rutas; el total servido, **831 KiB**, informativo; y
  lo comprimido, **145**, **61** y **298 KiB**, también informativo;
- el build, con 0 errores y 0 avisos, y `dotnet format`, limpio;
- **1554** casos en el carril rápido y **740** en el de integración, 652 de `Api.IntegrationTests`
  y 88 de `Organizacion.IntegrationTests`, todos en verde y contados con `recuento-de-tests.sh` y
  las dos listas del *workflow*. Son diez más en el rápido, los ocho de
  `UnaEnmiendaSeAnotaEnLosDosAdrTests` y los dos nuevos de `ElEstadoDelReadmeEsElDelPlanTests`;
- y las dependencias, con `python -X utf8 scripts/dependencias-por-conjuntos.py ec023f5 HEAD`: 125
  pares y 35 `Project` en los dos lados, sin nada añadido ni retirado; y en el frontal, 443
  entradas en los dos, con doce que cambian de versión y ninguna de nombre: `vitest` y nueve de
  `@vitest/` de 4.1.11 a 4.1.12, `vite` de 7.3.6 a 7.3.7 y `undici` de 7.30.0 a 7.30.1.

**La auditoría de la herramienta**, con `npm --prefix frontend audit --json` sobre `c99cd54`: 0 en
todas las gravedades. La puerta, `npm --prefix frontend audit --omit=dev --audit-level=high`, sale
con 0.
