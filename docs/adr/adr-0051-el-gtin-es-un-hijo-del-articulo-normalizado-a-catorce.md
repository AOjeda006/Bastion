---
tipo: referencia
stack: [dotnet, efcore, postgresql, react]
aplica_a: [ddd, sql, catalogo, api-rest, gs1]
tags: [adr, gtin, gs1, value-object, indice-unico, idempotencia, if-match, prefijos, rcn, r10, r11]
revisado: 2026-10-02
---

# ADR-0051: El GTIN es un hijo del artículo, normalizado a catorce y con su prefijo leído

- **Estado:** aceptado
- **Fecha:** 2026-10-02
- **Sale del 2.10**, que el usuario encargó el 2026-10-02 (PLAN, *El 2.10: el GTIN del artículo*).
  La puerta de clarificación se contestó ese día (PLAN, *La puerta del 2.10, contestada*). El
  punto 11 dice qué decidió el usuario y qué el agente.
- **Aplica** `negocio/identificacion-articulos/convenciones.md`, que es el import de la fase 2.

## Contexto

El artículo tiene un código interno, que es la clave del ERP, y ningún identificador externo. Las
recepciones de la fase 3 llegarán con un lector de códigos de barras, y lo que lea será un GTIN: el
de la unidad, el de la caja o el del palé. Si el artículo no sabe cuáles son los suyos ni cuántas
unidades lleva cada uno, el lector no sirve.

La convención dice lo que no se discute:

- un GTIN es una cadena, no un número;
- se guarda en 14, rellenando con ceros por la izquierda, y se compara sobre esa forma;
- su dígito de control se valida al entrar;
- nunca es clave primaria;
- un artículo tiene varios.

Lo que deja abierto es dónde vive, cómo se protege su escritura y qué números con forma de GTIN no
lo son.

## Decisión

### 1. `Gtin` es un *value object* de Catálogo

Se construye una sola vez, en la frontera, y lo que construye ya es válido:

- **se recortan los espacios de los extremos, y nada más.** Lo que queda tiene que ser solo dígitos
  ASCII. Una letra, un guion o un espacio dentro se rechazan, no se quitan;
- **el largo es 8, 12, 13 o 14.** Cualquier otro se rechaza: 11 dígitos no son un GTIN-12 al que le
  falte uno;
- **se rellena a 14 con ceros por la izquierda.** Los ceros de un GTIN-12 que empiece por ellos son
  parte del prefijo de empresa U.P.C., y se conservan;
- **el dígito de control se comprueba sobre los 14**, con el algoritmo de la §7.9.1: de derecha a
  izquierda y sin contar el de control, pesos 3 y 1 alternos, empezando por 3. El de control es lo
  que falta hasta la decena. El relleno con ceros no cambia la suma, así que la cuenta sobre los 14
  vale para las cuatro longitudes;
- **y se lee el prefijo**, como dice el punto 4.

Un GTIN de 12 dígitos y el mismo con un cero delante son el mismo GTIN, y el índice los ve iguales,
porque los dos llegan como la misma cadena de 14.

El dominio no devuelve `Resultado` (ADR-0004). `Gtin.Leer` devuelve la lectura, con el GTIN o con el
motivo del rechazo, y la aplicación convierte cada motivo en su error. Construir un `Gtin` desde un
texto que no lo es lanza: si se llega ahí, es un defecto.

### 2. Vive en su propia fila, hija del artículo, como los proveedores

Lo eligió el usuario. Es un agregado propio, `ArticuloGtin`, en `catalogo.articulos_gtin`, con su
`empresa_id` (R8), su artículo y su versión. Es lo mismo que el ADR-0010 decidió para el proveedor
del artículo y la línea de tarifa: dar de alta un GTIN no carga los demás.

- **El GTIN es una columna de texto de 14**, `varchar(14)`, con un `CHECK` de que son 14 dígitos.
  Clave primaria es el `id`, que es un `uuid` v7. El GTIN no es clave de nada.
- **No hay `PUT`.** Un GTIN no se edita: si estaba mal, se quita y se da de alta el bueno. Y sus
  unidades tampoco cambian (punto 3).
- **`ArticuloDto` no lleva los GTIN**, así que darlos de alta o quitarlos no mueve la versión del
  artículo, y su ETag sigue diciendo la verdad. Se leen en su propio recurso.

### 3. El nivel se declara, y la caja y el palé dicen cuántas unidades llevan

El nivel es `Base`, `Caja` o `Palet`, y lo declara quien da de alta el GTIN. **No se deduce del
primer dígito**: el indicador de un GTIN-14 lo asigna el dueño de la marca y no es un código de
nivel, y una caja puede llevar un GTIN-13.

Las unidades las eligió el usuario:

- **la base vale 1**, y puede haber más de una: un EAN-13 y un UPC-12 del mismo producto;
- **la caja y el palé llevan un entero de unidades base, de 2 en adelante**;
- **no cambian.** Según el *GTIN Management Standard*, cambiar el número de unidades de una
  agrupación exige un GTIN nuevo. Por eso no hay `PUT`;
- **puede haber varios GTIN por nivel**, como una caja de 6 y otra de 12, y no hay regla entre la caja
  y el palé.

El motor también lo guarda: un `CHECK` sobre el nivel y otro sobre las unidades. Si saltan, es un
defecto, como los de la trazabilidad, porque el caso de uso ya lo comprobó.

### 4. Lo que tiene forma de GTIN y no es el GTIN de un artículo

La fuente es la tabla 1-4 (*GS1 Prefix*), la 1-5 (*GS1-8 Prefix*) y la 1-6 (*U.P.C. Prefix*) de las
*GS1 General Specifications*, Release 26.0, Ratified, Jan 26, §1.2.3, páginas 26 a 28. Sobre la
medida variable, sus §2.1.10 y §2.1.12. El prefijo se lee en los 13 dígitos que siguen al indicador.
Si esos 13 empiezan por `00000` sin ser `0000000`, el número es un GTIN-8 y se lee en sus 8 dígitos
con la tabla 1-5. La tabla 1-4 reserva de `0000001` a `0000099` justo para eso, para no chocar con
los GTIN-8.

| Motivo | Prefijos | Por qué no es de un artículo |
|---|---|---|
| **Circulación restringida** | `0000000`, `02`, `04` y del `20` al `29`; en un GTIN-8, del `000` al `099` y del `200` al `299` | Son RCN. Las define la empresa o la organización de cada país, y fuera de ese ámbito no son únicas (§1.2.2.2.1). La etiqueta de peso que imprime la tienda lleva una (§2.1.12.2) |
| **Medida variable** | un GTIN-14 que empieza por `9` | Identifica un artículo de medida variable, y le falta la medida para estar entero (§2.1.10). Sin ella, el lector no sabe cuánto entra |
| **Cupón o vale** | `980`, del `981` al `983`, y el `99` | Son cupones y vales de devolución, no artículos comerciales. GS1 los lleva hacia su propia clave, el GCN (§2.6.2) |
| **Sin asignar** | `951`, del `984` al `989`; en un GTIN-8, del `977` al `999` | Están reservados o retirados, y no los ha recibido nadie. El `951` daba números de gestor del EPC, y nunca GTIN |

Cada motivo tiene su `type`, para que la pantalla diga cuál es. Se admiten:

- el `952`, que GS1 reserva para demostraciones y ejemplos. Así escribe sus ejemplos la propia
  especificación, y no estaba en lo que el encargo pedía mirar;
- el `977` (ISSN) y el `978` y `979` (ISBN e ISMN), que son GTIN-13 de publicaciones de verdad.

**El `05` se admite, aunque las dos tablas no digan lo mismo de él.** La 1-4 lo da para prefijos de
empresa. La 1-6 dice que el prefijo U.P.C. `5` está reservado. Se sigue la 1-4, que es la de los
prefijos GS1: rechazar un GTIN válido es peor que admitir uno que nadie ha recibido.

**Se empieza estricto porque es lo reversible**, como el lote del ADR-0048 §2: aflojar después es
quitar una línea de la tabla, y apretar después es limpiar datos. **El disparador** de la medida
variable y de la circulación restringida es **el primer artículo que se venda al peso con etiqueta
de tienda**. Ese día, el `2x` y el `9` necesitan su medida, y eso es el analizador del punto 9.

### 5. Un GTIN, un artículo, en cada empresa

El índice único `ix_articulos_gtin_uno_por_empresa`, sobre `(empresa_id, gtin)`, es quien decide.
Se traduce por su nombre al `409` `articulo-gtin-duplicado`, en `RestriccionesQueGuardanUnaRegla`.
La comprobación previa del caso de uso es cortesía: da el mismo `type` sin llegar al motor, y
entre ella y la escritura cabe otra transacción. Lo demuestra un caso con dos altas del mismo GTIN a
la vez, en dos artículos distintos, con dos transacciones de verdad: una entra, y la otra recibe el
`409`.

En otra empresa, el mismo GTIN entra: el índice lleva `empresa_id` delante.

### 6. Cómo se protege la escritura (R10 y R11)

- **El alta**, `POST /api/v1/catalogo/articulos/{id}/gtins`, admite `Idempotency-Key`. Un reintento
  con la misma clave devuelve la misma respuesta, sin una segunda fila y sin un `409` contra sí
  mismo.
- **La baja**, `DELETE /api/v1/catalogo/articulos/gtins/{id}`, exige el `If-Match` de la fila. La
  fila no cambia nunca, así que lo que protege es la regla uniforme: nadie borra lo que no ha visto.
  Una baja repetida da `404`.
- Las dos se declaran en `TodaEscrituraDiceComoSeProtegeTests`: +2 escrituras, +1 a `If-Match` y +1
  a `Idempotency-Key`.
- **Los permisos son propios**, como los del proveedor: `catalogo.articulo-gtin.agregar` y
  `catalogo.articulo-gtin.quitar`. Leerlos es `catalogo.articulo.ver`.

### 7. Se lee por artículo, y se busca por GTIN

- `GET /api/v1/catalogo/articulos/{id}/gtins` da la lista de un artículo, y un `404` si el artículo
  no existe, como la de proveedores.
- `GET /api/v1/catalogo/articulos/gtins/{id}` da una fila con su ETag, que es la que pide la baja.
- `GET /api/v1/catalogo/articulos/gtins?gtin=…` busca. **Normaliza lo que entra** con el mismo
  `Gtin.Leer`, así que el GTIN-12 y su forma de 13 encuentran lo mismo, y un texto que no es un GTIN
  recibe el `400` de su motivo, no una lista vacía. Devuelve la fila del GTIN, con su artículo, su
  nivel y sus unidades. Es lo que necesitará el lector de la fase 3, en una sola llamada.

### 8. Quitar un GTIN lo borra, y la empresa puede volver a asignarlo

- La fila se borra, como la del proveedor, y el rastro queda en la traza (ADR-0012).
- **La regla de los 48 meses está derogada desde el 1 de enero de 2019**, y no se implementa. El
  GTIN quitado puede volver a darse de alta en otro artículo enseguida.
- **Ningún documento guarda el GTIN en lugar del artículo.** Hoy no hay ninguno que lo lleve, y se
  comprueba con un barrido de los esquemas. El día que un documento lo guarde, guardará también el
  artículo, y quitar el GTIN no lo dejará huérfano.

### 9. Lo que no entra, con su disparador

- **El analizador de GS1-128.** Su disparador es la primera pantalla que reciba la lectura de un
  código compuesto.
- **La regla del contenido neto del *GTIN Management Standard***: cambiar el contenido neto declarado
  exige un GTIN nuevo. El artículo no tiene ese campo. Su disparador es el primer campo de contenido
  neto.
- **La medida variable y la circulación restringida**, con el disparador del punto 4.
- **La ficha del artículo**: la pantalla es una página propia, `/articulos/:id/gtin`, como la de la
  trazabilidad. Su disparador es la tercera página propia del artículo, que ya pide un sitio que
  las reúna.

### 10. Los casos y las mutaciones

- **Los casos dorados** van en el dominio y en el carril rápido:
  - un GTIN-12 con ceros a la izquierda, que se conservan;
  - un GTIN-8, un GTIN-13 y un GTIN-14;
  - el mismo GTIN con 12 y con 13 dígitos, que da la misma cadena;
  - un dígito de control mal;
  - 11 dígitos;
  - letras;
  - un caso por cada fila de la tabla del punto 4, y su vecino admitido.
- **El `409` del mismo GTIN con 12 y con 13 dígitos** va en la API, con la base de verdad.
- **La tanda** va sobre las líneas que deciden: el relleno, los pesos del dígito de control, cada
  línea de la tabla de prefijos, el índice y su traducción. Empieza en la 119.

### 11. Qué decidió el usuario y qué el agente

El usuario fijó el encargo entero y contestó la puerta: el GTIN como hijo del artículo, el entero de
unidades con varios GTIN por nivel, y la página propia. El agente decidió:

- recortar los extremos y nada más;
- la tabla del punto 4: qué se rechaza, con qué motivo, y admitir el `952`, el `977`-`979` y el `05`;
- la búsqueda en el recurso del GTIN y no en el listado de artículos;
- los nombres: la tabla, el índice, los `CHECK`, los permisos y los `type`;
- `Palet` en el código y «palé» en la pantalla;
- que el tipo de artículo no limite el GTIN. GS1 llama artículo comercial también a un servicio.

## Consecuencias

- **Una tabla y una migración de Catálogo**, con su índice único y sus `CHECK`.
- **Cinco rutas nuevas**: tres lecturas y dos escrituras. El OpenAPI y el catálogo de errores
  crecen, y el contrato del frontal se regenera.
- **El rol «administracion» concede dos permisos más**, porque concede el catálogo entero. El humo lo
  cuenta.
- **Un artículo con GTIN no se puede borrar sin quitarlos antes.** Hoy no hay baja de artículo, así
  que no cambia nada todavía.

## Procedencia

Lo fijó el usuario en el encargo y en la puerta del 2026-10-02. El punto 11 dice qué decidió el
agente. Las *GS1 General Specifications*, Release 26.0, se leyeron de
<https://ref.gs1.org/standards/genspecs/> el 2026-10-02. La regla de los 48 meses y el *GTIN
Management Standard* son los de la convención.
