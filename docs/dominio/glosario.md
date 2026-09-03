# Glosario del lenguaje ubicuo — Bastion

El vocabulario del dominio, con **una sola** palabra por concepto. Lo que está aquí es lo que se
escribe en el código, en la API, en los mensajes al usuario y en las conversaciones. Si una palabra
significa dos cosas, o una cosa se dice de dos maneras, es un error y se arregla aquí antes que en
el código (`principios/ddd.md`: el lenguaje ubicuo va en la lista de «siempre»).

**Regla de este fichero: solo entra lo que ya existe construido.** Un glosario que inventa términos
por adelantado es peor que no tenerlo, porque fija nombres antes de conocer el dominio. Se amplía al
abrir cada módulo, no antes. Lo que el plan maestro ya nombra pero aún no está construido va al
final, en *Reservado*, sin definición y con el puntero a dónde se define.

> Nació en la auditoría previa a la fase 1 (2026-09-02), con Organización, Identidad y Auditoría
> construidos. El **0.16** le añadió la tabla de *Agregados*, que ya no es prosa —se compara contra
> el dominio compilado—, el vocabulario del frontal, y las tres decisiones que hizo falta tomar
> para poder definir cuatro términos. El §12 del plan maestro le pide además los diagramas E-R y de
> estados; llegan con los módulos que los necesiten.

---

## Qué de esto está comprobado y qué no

La tabla de **agregados** está comparada contra el dominio compilado, entera y en los dos sentidos
(`ElGlosarioDelDominioTests`): un agregado nuevo sin entrada aquí pone el carril en rojo, y una
entrada de aquí que ya no exista en el dominio, también. Se comprueba además que cada uno diga el
módulo en el que de verdad vive. Es la única lista con esa garantía, y la tiene porque es la que más
caro sale desactualizada: es la que dice **qué cosas hay**.

El resto —objetos de valor, entidades hijas, conceptos— es **prosa**, y envejece como toda prosa. Se
decidió así porque la clasificación de un objeto de valor no es una propiedad que la reflexión pueda
leer sin una lista de excepciones escrita a mano, y una lista de excepciones a mano es exactamente
lo que convierte una comprobación en una convención con pasos extra.

---

## Agregados

Un **agregado** es una entidad que es un recurso por sí misma: se da de alta, se consulta y se edita
por su cuenta, tiene su propio identificador y sus dos marcas de tiempo. En el código son los tipos
que heredan de `EntidadBase` —directamente o a través de `RaizAgregado`, que es la variante que
además emite eventos de integración—. Lo que no aparece aquí y sí existe en el dominio son las
**entidades hijas**, que no se dan de alta solas: viven dentro del agregado que las contiene.

La columna **ámbito** es la R8 dicha en una palabra: *empresa* significa que la fila lleva
`empresa_id` y el filtro global la recorta; *instalación* significa que es un maestro compartido por
todas las sociedades, declarado como tal y con su motivo escrito en
`CadaEntidadDeclaraSuInquilinatoTests`.

| Término | Tipo | Módulo | Ámbito | Qué es, y qué **no** es |
| --- | --- | --- | --- | --- |
| Almacén | `Almacen` | Organización | Empresa | El sitio al que apunta cada movimiento de existencias: empresa, código, dirección, tipo. **No es la ubicación dentro de él.** La dirección es opcional, porque un almacén virtual o de tránsito no está en ningún sitio. No se borra: se bloquea, porque el histórico de valoración apunta a él para siempre. |
| Conversión de unidades | `ConversionUM` | Organización | Instalación | Cuántas unidades de destino hay en **una** de origen: una caja son doce unidades. La dirección está escrita y **no se invierte sola**; encadenar dos **no** da una tercera (decisión 3, más abajo). |
| Divisa | `Divisa` | Organización | Instalación | La fila que dice con qué monedas opera esta instalación, por su código ISO-4217 y con el nombre que se enseña. **No es «la divisa» que acompaña a cada importe**: esa es el código del bloque común, y el reparto entre las dos está en *Lo transversal*. Con cuántos decimales se redondea **tampoco está aquí**: es una regla fiscal y vive en `CatalogoDeDivisas`, en código. |
| Ejercicio | `Ejercicio` | Organización | Empresa | El periodo contable de una empresa, con estado `Abierto` o `Cerrado`. **No es «el año»**: puede no coincidir con el natural, y por eso lleva fecha de inicio y de fin. Sus dos fechas son días del calendario, no instantes. Se cierra; no se bloquea, porque un intervalo de fechas no tiene datos personales. |
| Empresa | `Empresa` | Organización | Es el inquilino | La entidad jurídica que opera en Bastion: NIF, razón social, dirección fiscal, divisa base, régimen de IVA. **No es «la instalación» ni «el cliente del ERP»**: en una misma instalación conviven varias. Es la raíz del multiempresa —toda entidad transaccional lleva su identificador— y por eso no lleva `empresa_id` propio. Sí se bloquea: un empresario individual es persona física. |
| Impuesto | `Impuesto` | Organización | Instalación | Un **tramo vigente** de un tipo impositivo, con su porcentaje y sus fechas. **No es «el IVA»**: el código no es único, y `IVA-GENERAL` tiene tantas filas como veces cambió el tipo. Un impuesto no se edita, se sucede: se cierra el tramo vigente y se crea el siguiente. |
| Rol | `Rol` | Identidad | Instalación | Un manojo de permisos con nombre. **No es un permiso, y no es un poder**: la autorización nunca pregunta por el rol, pregunta por el permiso, para poder cambiar el reparto sin tocar el código. Quién tiene qué rol se dice **por empresa**, en la membresía. |
| Serie | `Serie` | Organización | Empresa | La numeración de un tipo de documento en una empresa y un ejercicio, con su **contador**. El contador **no es una `SEQUENCE` de PostgreSQL**: es una columna. Una secuencia no es transaccional y deja huecos al hacer *rollback*, y la R5 exige correlativa y **sin huecos** (ADR-0007 §5). |
| Tipo de cambio | `TipoCambio` | Organización | Instalación | Cuánto valía una divisa en otra un día concreto. Lleva **las dos divisas escritas**: la base es un campo de cada empresa, así que una sola columna dejaría la tasa sin dirección. Se lee «tantas unidades de destino por una de origen». |
| Ubicación | `Ubicacion` | Organización | Empresa | Un hueco concreto dentro de un almacén: pasillo, estante, hueco. Es opcional **para el almacén**, no para el sistema. Lleva su propia `empresa_id` aunque su almacén ya la tenga, para que el filtro sea el mismo que el de todas las demás tablas. |
| Unidad de medida | `UnidadMedida` | Organización | Instalación | Una unidad en la que se cuenta la mercancía: unidad, kilogramo, metro, caja. Los decimales **sí** son una columna aquí, al revés que en la divisa: cuántos admite el kilo lo decide quien monta el catálogo, no el BOE. |
| Usuario | `Usuario` | Identidad | Instalación | Quien entra en Bastion: quién es, el resumen de su contraseña, en qué empresas está y con qué roles. **No pertenece a una empresa** —lo que pertenece a una empresa es su membresía—, y **aquí no hay ni una contraseña**, solo su resumen; quién sabe calcularlo es Infrastructure. |

---

## Lo transversal (el bloque común)

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Importe** | Cantidad de dinero con divisa, **4 decimales**, redondeo `AwayFromZero` explícito. | No es un `decimal`. Un `decimal` suelto no lleva divisa, y sumar euros con dólares tiene que ser imposible de escribir, no solo estar mal. |
| **PrecioUnitario** | Precio por unidad, con divisa y **6 decimales**. | **No es un `Importe` con más decimales**: es un tipo distinto. Multiplicarlo por una cantidad devuelve un `Importe`, y ahí es donde se redondea — una vez, sobre el producto exacto. |
| **Divisa** (el código) | El **código** ISO-4217 de tres letras que acompaña a todo importe, validado contra `CatalogoDeDivisas` —el catálogo en código que dice qué código existe y con cuántos decimales redondea cada uno—. | No es el símbolo (`€`), que es presentación. Y **no es el agregado `Divisa`** de Organización: el 0.15 construyó los dos, y el choque que este glosario avisó se resolvió así — el código viaja en cada importe y lo fija el BOE; la fila dice con qué monedas opera esta instalación y la elige quien la administra. |
| **Bloqueo** | Que un registro no admita nuevas operaciones: `EstaBloqueado`, `Desde` y `Motivo` (R16). Es la respuesta al artículo 32 de la LOPDGDD, que obliga a reservar los datos e impedir su tratamiento, no a destruirlos. Un `DELETE` de la API bloquea. | **No es un borrado lógico.** No existe ningún `Activo` en todo `src/` a propósito: un registro bloqueado se ve, se consulta y se lista; simplemente no deja operar. Y **no es el cierre**: no habla de datos personales. |
| **Cierre** | El final de vida de algo que tiene una línea temporal: un ejercicio se cierra, una serie se cierra, un tramo de impuesto se cierra. | No es el bloqueo. Mezclarlos haría que dos máquinas de estados distintas compartieran columna. |
| **NIF** | Objeto de valor con validación **real** de la letra, no un `string` con formato. | No es «el CIF». En castellano administrativo conviven NIF, CIF y NIE; aquí la palabra es **NIF** y cubre las tres formas. |
| **Correo** | Objeto de valor con la forma comprobada y el límite de 254 posiciones. | No comprueba que el buzón exista. Eso solo lo sabe un correo enviado. |
| **Dirección** | Objeto de valor en **campos separados** (R17): calle, número, código postal, población, subdivisión y país. | No es una entidad ni una cadena de texto. Y el campo es **subdivisión**, no «provincia»: fuera de España la división de primer nivel no se llama así, y el nombre del campo no puede suponer el país. |
| **Permiso** | La unidad atómica de autorización (`modulo.recurso.accion`). | No es un rol. Un permiso no se le da a nadie directamente. |
| **Resultado** | El retorno de una operación que puede fallar por una regla de negocio. | No es una excepción. La frontera entre los dos está escrita en el **ADR-0004**: la excepción es para lo que no estaba previsto. |
| **Inquilinato** (R8) | Que una fila pertenece a una empresa. Se declara por entidad: o lleva `empresa_id` y el filtro global la recorta, o está en la lista de maestros de instalación **con su motivo escrito**. | No hay una tercera opción, y no es algo que se deduzca mirando la tabla: se declara, y el barrido lo comprueba. |
| **Maestro de instalación** | Una fila compartida por todas las sociedades. Un kilo es un kilo en todas, y el tipo general del IVA lo fija el BOE. | No es «una tabla sin empresa porque todavía no hacía falta». Es una decisión con motivo, y el motivo está escrito. |
| **Concurrencia optimista** (R11) | Cada recurso lleva una versión que el cliente devuelve al editar. Dos ediciones simultáneas: la segunda se entera. | No es un bloqueo de fila. Nadie espera a nadie. |
| **Idempotencia** (R10) | Una escritura repetida con la misma clave de cliente no ocurre dos veces. | No es «que dé el mismo resultado». Es que **no vuelva a pasar**. |
| **Bandeja de salida** | El evento de integración se escribe en la misma transacción que el cambio que lo provoca, y se publica después. | No es una cola. Un evento no puede existir sin su escritura, y por eso no vive fuera de la base. |
| **Las tres fechas** (R14) | Un **instante** (`timestamptz`) para cuándo pasó algo en el sistema; una **fecha de negocio** (`date`) para un día del calendario que no tiene hora ni zona; y una **fecha local** para lo que se le enseña a una persona. | No son tres formatos de lo mismo. Confundirlas es lo que hace que un asiento cambie de día al cruzar una zona horaria. |

## Organización

Sus agregados están arriba. Aquí, lo que no lo es:

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Régimen de IVA** | El régimen fiscal bajo el que tributa una empresa. | No es el tipo impositivo de una línea. |

## Identidad

Sus dos agregados —**Usuario** y **Rol**— están arriba. Aquí, lo que vive dentro de ellos y lo que
solo existe durante una sesión:

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Membresía** | Lo que ata un usuario a una empresa, con los roles que tiene **en esa** empresa. Es una entidad hija: no se da de alta sola. | No es un rol. El mismo usuario puede ser administrador en una empresa y solo lectura en otra: eso es una membresía por empresa, no un rol global. |
| **Rol de la membresía** | Un rol concedido a un usuario en una empresa concreta (`RolDeMembresia`). Es una fila, no una lista en memoria. | No es el rol. El rol existe una vez; la concesión, una por empresa. |
| **Permiso del rol** | Un permiso que concede un rol (`PermisoDeRol`). | No es el permiso, que es un objeto de valor. Esta es la fila que dice que **este** rol lo tiene. |
| **Testigo de acceso** | El *token* corto con el que se llama a la API. | No es el de refresco. Se dicen distinto porque duran distinto y se guardan en sitios distintos. |
| **Token de refresco** | El que canjea un testigo nuevo, con rotación y motivo de revocación. Se guarda **su resumen**, no el token: la fila es tan sensible como una contraseña. | No viaja en cada petición. |
| **Empresa activa** | Aquella con la que un usuario está operando **ahora**, dentro de la sesión. | No es «la empresa del usuario»: un usuario con varias membresías cambia de empresa activa sin volver a entrar. |

## Auditoría

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Registro de auditoría** | La anotación *append-only* de quién cambió qué, cuándo y desde qué empresa. **Una fila por entidad cambiada**, no por propiedad. | No se modifica ni se borra jamás, ni siquiera para corregirlo. Una corrección es **otro** registro. |

## El vocabulario del frontal

Las palabras del frontal son las mismas, y su reparto en carpetas también:

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Funcionalidad** | Una carpeta de `src/features/`, que **espeja un módulo del backend**. Hoy son `identidad` y `organizacion`. Una funcionalidad nunca importa de otra, y eso lo ejecuta ESLint (**ADR-0022**). | No es una pantalla ni un recurso. Si algo parece pedir dos funcionalidades a la vez, o sube a `shared/`, o es que no eran dos módulos. |
| **Recurso** | Una subcarpeta dentro de una funcionalidad: `organizacion/almacenes`, `organizacion/empresas`, `identidad/acceso`. | No es una frontera. Dentro de la misma funcionalidad no hay ninguna. |
| **Armazón** | `src/app/`: lo que se monta una vez para todas las pantallas —disposición, navegación, enrutador, idioma, selector de empresa— y las dos pantallas que no son de ningún módulo (`app/paginas/`). | No es una funcionalidad, y por eso no está en `features/`. Una pantalla que no es de ningún módulo metida en uno le presta un dueño que no tiene. |
| **Compartido** | `src/shared/`: lo que dos funcionalidades necesitan. Es el sistema de componentes, el cliente de la API y la sesión. | No es un almacén de dominio. Que una funcionalidad pueda importar de `shared/` no convierte a `shared/` en el sitio donde dejar lo que no se sabe dónde va. |

---

## Reservado por el plan maestro, todavía sin construir

Estas palabras ya tienen dueño en el §7 y **no se pueden usar para otra cosa**, ni buscarles
sinónimo. Se definen aquí cuando se construyan, no antes:

- **Tercero** — Terceros (§7.2). Es **un solo agregado** con roles `EsCliente` y `EsProveedor`:
  «cliente» y «proveedor» son **papeles del mismo tercero**, no dos entidades. Fase 1.
- **Artículo**, **Categoría**, **Tarifa** — Catálogo (§7.3). Fase 1.
- **Retirada** — el final de vida de un maestro de instalación. La palabra **ya está decidida y
  todavía no existe en el código**: es la decisión 1 de aquí abajo, escrita en el **ADR-0023**, y se
  implementa en la fase 1. No es un bloqueo ni un cierre, y no se le puede llamar de otra manera.

> Lo que este apartado reservaba para el ítem 0.15 —**Impuesto**, **Divisa** + **TipoCambio**,
> **UnidadMedida** + **ConversionUM**, **Ubicacion**— ya está construido y ha subido a la tabla de
> agregados.

---

## Lo que este glosario ha dejado decidido

Escribir la definición de cuatro términos obligó a decidir tres cosas que no lo estaban. Están en el
**ADR-0023** y en `docs/PLAN.md`, y **no están implementadas**: se implementan en la fase 1, que es
cuando empiezan a producir números.

1. **Los cuatro maestros globales no tienen salida, y la van a tener.** `Divisa`, `TipoCambio`,
   `UnidadMedida` y `ConversionUM` se crean y se editan, pero no se cierran, ni se bloquean, ni se
   borran — al contrario que su hermano `Impuesto`, que sí tiene `/cierre`. Y como son de
   instalación, un alta equivocada es permanente y visible desde todas las empresas. La salida se
   llamará **retirada** y no será ni un bloqueo ni un cierre.
2. **La inversa de una conversión tiene que ser la inversa.** Los dos sentidos son filas
   independientes y nada impedía declarar `caja→unidad = 12` junto a `unidad→caja = 0,5`.
3. **Una conversión encadenada no compone, y preguntarla es un error con nombre.** Con `kg→g` y
   `g→mg` pero sin `kg→mg`, el sistema dice que no lo sabe en vez de multiplicar.

---

## Cómo se escribe «Bastion»

Sin tilde, siempre, en todas partes: código, prosa, interfaz y base de datos (Anexo A.1). La base de
datos es `bastion` (`bastion_dev` en desarrollo), un esquema PostgreSQL por módulo en `snake_case`, y
la raíz de espacios de nombres es `Bastion` sin repetirla dentro.
