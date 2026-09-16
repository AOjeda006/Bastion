---
tipo: referencia
stack: [csharp, dotnet, aspnetcore, postgresql, docker]
aplica_a: [autenticacion, seguridad, entrega-continua, testing]
tags: [adr, r8, rbac, semilla, migrador, segundo-arranque, humo, adr-0021]
revisado: 2026-09-16
---

# ADR-0035: El rol del sistema lleva el catálogo de la versión desplegada, y el código que solo corre al arrancar se prueba arrancando

- **Estado:** aceptado
- **Fecha:** 2026-09-16
- **Amplía** el [ADR-0021](adr-0021-el-esquema-lo-aplica-un-paso-del-despliegue-no-el-arranque-de-la-api.md): el migrador,
  además de migrar y cargar los maestros, pone al día los permisos del rol del sistema.
- Se implementa en el **ítem 1.12**.

## Contexto

`SembrarAdministrador` crea la primera cuenta y el rol `administracion`, marcado como del sistema,
con **todos** los permisos del catálogo. Y solo entra mientras no hay **ningún** usuario: es la
puerta de arranque, y se cierra sola en cuanto existe la primera cuenta.

Dentro de esa puerta había un comentario que decía lo contrario de lo que pasa:

> Se fijan SIEMPRE, también si el rol ya existía: el catálogo crece con cada módulo que se añade, y
> un rol de administración que se quedó con los permisos de la fase 0 dejaría la fase 1 sin nadie
> que pudiera conceder los suyos.

La línea que lo seguía sí fija los permisos siempre… **que se llega a ella**, y en una instalación en
marcha no se llega: el `return false` de «ya hay usuarios» va antes. Nada lo desmentía porque todo lo
que corre al arrancar se había ejercido **una** vez, sobre una base vacía: la CI levanta el *compose*
de cero, y los tests de integración también.

**Medido, no supuesto.** El catálogo con el que cerró la fase 0 (`fe7059d`) tenía **55** permisos; el
de la fase 1 tiene **93**. Los **38** nuevos son 9 de Organización —`organizacion.bloqueado.ver` y
`retirar`/`reincorporar` de conversiones, divisas, tipos de cambio y unidades—, 14 de Terceros y 15
de Catálogo. Con el arnés del 1.12 sobre `7f676e2`, y antes de tocar nada: primer arranque, el rol
con 93; estado de la fase 0, 55 más uno retirado, y `GET /api/v1/terceros/terceros` → `403`; segundo
arranque con la semilla retirada, **le faltan 38 y le sobra 1**, y el guion sale con 1.

**La gravedad, exacta.** Es una **regresión de privilegios silenciosa**: ni un error, ni un aviso, ni
una línea en el registro; la administración recibe `403` en todas las pantallas nuevas. Y es
**recuperable a mano**: `CrearRol` y `ModificarRol` validan la lista contra el catálogo, no contra lo
que tiene quien la escribe, así que un administrador con `identidad.rol.modificar` —que la fase 0 sí
tenía— puede reconstruir el rol. Silenciosa y recuperable no la hace menor: la descubre quien intenta
trabajar, y el arreglo exige saber que existe. Y por el otro lado, un permiso que una versión retira
se queda concedido para siempre, y aparecería activo el día que alguien volviera a registrar ese
nombre.

## Decisión

### 1. Siempre, y con la edición cerrada — la opción (c)

Había tres opciones: **(a)** actualizar siempre; **(b)** avisar, nombrando los permisos que faltan;
**(c)** actualizar siempre **y** cerrar la edición de los permisos del rol del sistema. Se elige la
**(c)**.

- **El migrador alinea** cada rol marcado como del sistema con el catálogo de la versión desplegada,
  **en cada despliegue**: concede lo que falta y retira lo que sobra (`ActualizarRolesDelSistema`,
  llamado desde `MigradorDeArranque` detrás de las semillas).
- **La API no deja cambiarle la lista**: `PUT /api/v1/identidad/roles/{id}` sobre un rol del sistema
  con una lista distinta de la que tiene → `409 permisos-de-rol-del-sistema`. La lista se compara
  **como conjunto**, así que el formulario que la reenvía en otro orden puede renombrarlo.
- **Quien quiera una administración con menos poderes crea un rol propio.** Un rol propio no lo toca
  ningún despliegue: los permisos nuevos no le llegan solos, que es lo correcto para un rol que
  alguien recortó a propósito.

**Por qué no (b).** Es quedarse donde estamos con una línea más en un registro que nadie lee a la hora
en que importa. El `403` sigue ahí; solo cambia que ahora hay un sitio donde habría podido leerse.

**Por qué no (a) sola.** Deshace en silencio cualquier recorte deliberado: si alguien quitó al rol del
sistema `terceros.tercero.importar`, el siguiente despliegue se lo devuelve sin decírselo a nadie, y
el recorte era mentira. Para que un recorte sobreviviera habría que distinguir «lo retiraste tú» de
«esto es nuevo», o sea, guardar qué catálogo vio el rol la última vez: un estado más, por rol y por
versión, para sostener un rol del sistema que no es el sistema. Con (c) la pregunta no existe: un
recorte o es imposible o es un rol propio.

**El coste de (c), escrito.** El rol del sistema no se puede recortar. Si una instalación lo había
recortado, el **primer despliegue con esta versión lo deshace**, y lo dice permiso a permiso: un
aviso `PermisoConcedidoAlRolDelSistema` o `PermisoRetiradoDelRolDelSistema` por línea en el registro
del migrador, y una fila de traza por permiso con el motivo `ActualizacionDeRolesDelSistema`.

### 2. En el migrador, y no en la API ni en una migración

- **No en el arranque de la API**, por lo mismo que las semillas (ADR-0021): con dos réplicas, dos
  procesos escribirían el mismo rol a la vez, y lo haría «la réplica que arranque primero».
- **No quitando el `return` de la semilla**: mezclaría la puerta de arranque, que crea, con la
  actualización, que corrige, y la movería al arranque de cada réplica.
- **No en una migración con `INSERT`**: el catálogo es código, no datos (`ICatalogoDePermisos`). Una
  migración sería una copia del catálogo del día en que se escribió, y la copia que se desincroniza
  es justo la avería que se está cerrando.

En el primer arranque el migrador corre **antes** que la API, así que no hay rol que alinear: lo dice
(`SinRolesDelSistema`) y el rol lo crea la semilla, con el catálogo entero, como siempre.

### 3. Tocar solo lo que cambia, y con motivo propio

`Rol.Alinear` deja lo mismo que `FijarPermisos` y se distingue por lo que no toca: las filas que ya
estaban siguen siendo las mismas, así que la traza solo nombra lo que la versión trajo o se llevó.
Retira por el texto guardado, sin exigir que lo que sobra tenga forma de permiso. Y escribe con su
propio motivo sin inquilino, no con `SemillaDeArranque`: si un día el rol amanece distinto, la columna
tiene que decir cuál de los dos pasó.

### 4. `EsDelSistema` dice lo que garantiza

El texto de la marca prometía «no se puede suprimir». **Ninguna operación suprime un rol, de ninguno**,
así que la promesa no la cumplía la marca. El texto dice ahora lo que sí se cumple, y dónde: sus
permisos los fija el despliegue y la API no los edita. El día que exista la supresión, esta marca es lo
que tiene que mirar.

### 5. El código que solo corre al arrancar se prueba desde fuera, o no se prueba

Es la lección del ítem, y la razón de que el arreglo llegara **después** de ver el arnés en rojo. Un
test de integración no puede ver un segundo arranque: su base nace vacía en cada ejecución, y la
fixture migra sin pasar por el migrador. Lo que se probó durante toda la fase 1 era cierto —la semilla
fija el catálogo entero— bajo una condición que en una instalación en marcha no se da nunca.

Dos arneses, uno por cada mitad de «arrancar sobre algo que ya existe»:

**El segundo arranque** (`scripts/ci/segundo-arranque.sh`, último paso del Humo). Construye
exactamente este estado:

1. El primer arranque de **esta** versión, el del Humo: el *compose* de cero, el migrador —que dice
   `SinRolesDelSistema`— y la API sembrando con las ocho variables. Se afirma: el rol del sistema es
   el catálogo, en los dos sentidos, con un inicio de sesión nuevo.
2. **El estado viejo, por SQL**: se le quita al rol del sistema todo permiso que no esté entre los 55
   del catálogo de `fe7059d` —escritos en el guion, porque la CI clona sin historia y son un hecho del
   pasado— y se le añade `organizacion.permiso-de-una-version-anterior.ver`, bien formado y de ningún
   catálogo. Se afirma que tiene 56, y **el arnés del arnés**: con un inicio de sesión nuevo,
   `GET /api/v1/terceros/terceros` → `403`. Si no, el estado no es viejo y el verde de abajo no
   probaría nada.
3. **El segundo arranque de verdad**: `up --no-deps --force-recreate migraciones api` sobre el
   **mismo volumen**, con las ocho `BASTION_SEMILLA_*` vacías —la instalación en marcha que las
   retiró—. Se afirma: el migrador sale con 0, dice `RolDelSistemaAlineado` y **un** solo
   `PermisoRetiradoDelRolDelSistema`; la API dice `SemillaSinVariables`, así que la semilla no entró.
4. Con un inicio de sesión nuevo, el rol es el catálogo en los dos sentidos y la lectura de la fase 1
   → `200`.

**Un estado y no una versión.** Instalar una imagen de la fase 0 costaría otra construcción por run y
dependería de que aquel árbol siguiera construyendo. Lo único que el arranque ve es el estado. **El
límite, dicho:** el estado viejo es el del rol, no el de la base entera; las tablas tienen el esquema
de esta versión. La otra mitad la cubre el segundo arnés.

**Las migraciones sobre tablas con filas** (`LasMigracionesSobreTablasConFilasTests`, carril de
integración). Aplica las migraciones de los cinco contextos **una a una**, en el orden de sus
identificadores, y antes de cada paso mete una fila inventada en **cada** tabla que exista, respetando
claves foráneas y restricciones `CHECK` —una restricción que el arnés no conoce es un fallo, no un
salto—. Afirma que ninguna migración falla y que ninguna se lleva una fila, y contrasta la misma
pasada sobre tablas vacías. Aquí el arnés **no encontró avería**: las migraciones de la fase 1 pasan
con filas. Que mira lo que dice mirar lo prueba la mutación: una columna `NOT NULL` sin valor por
defecto sale roja con filas (`23502`) y verde sin ellas.

**Por qué en el Humo y no en un job aparte.** El guion tarda unos 13 s sobre el entorno que el Humo ya
tiene levantado. Un job propio tendría que volver a construir las dos imágenes, que es la mayor parte
del tiempo del Humo: duplicaría el coste para ejercer lo mismo. Va **al final** porque recrea la API, y
el frontal resuelve su dirección al arrancar: sus pasos ya han corrido.

## Alternativas descartadas

**(a) y (b)**, arriba. **Actualizar en la API**, **en la semilla** o **en una migración**, arriba.

**Un test de integración que llame a la semilla dos veces.** Probaría el método, no el arranque: no
pasaría por el migrador, ni por el *compose*, ni por unas variables retiradas. Es la forma de volver a
probar lo cierto bajo una condición que no se da.

**Construir la imagen de la fase 0 en la CI.** Arriba: el coste y la fragilidad, para ver el mismo
estado.

## Consecuencias

- `MotivoSinInquilino` gana `ActualizacionDeRolesDelSistema`, y la lista blanca de ámbitos sin
  inquilino, una entrada más: la cuarta sin petición detrás, en la cuenta que lleva esa lista.
- `IRepositorioDeRoles` gana `DelSistemaAsync`, que busca por la marca y no por el código.
- El contrato publica un `409` en `PUT /roles/{id}` y un código nuevo en `docs/api/errores.json`, con
  su texto en los dos diccionarios del frontal.
- El Humo tiene un paso más, y `AGENTS.md` lo nombra.
- `LosIdentificadoresAjenosTests` deja fuera los tipos que genera el compilador: la primera lambda sin
  captura de `Identidad.Domain` —la de `Rol.Alinear`— creó una `<>c` que chocaba por nombre con las de
  `BuildingBlocks.Domain`, y ningún `XId` puede apuntar a un tipo sin nombre en el código.
