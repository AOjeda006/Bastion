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
> construidos. El §12 del plan maestro le pide además los diagramas E-R y de estados; llegan con los
> módulos que los necesiten.

---

## Lo transversal (el bloque común)

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Importe** | Cantidad de dinero con divisa, **4 decimales**, redondeo `AwayFromZero` explícito. | No es un `decimal`. Un `decimal` suelto no lleva divisa, y sumar euros con dólares tiene que ser imposible de escribir, no solo estar mal. |
| **PrecioUnitario** | Precio por unidad, con divisa y **6 decimales**. | **No es un `Importe` con más decimales**: es un tipo distinto. Multiplicarlo por una cantidad devuelve un `Importe`, y ahí es donde se redondea — una vez, sobre el producto exacto. |
| **Divisa** | Hoy, el **código** ISO-4217 de tres letras que acompaña a todo importe, validado contra `Divisas` —el catálogo que dice qué código existe y con cuántos decimales redondea cada uno—. | No es el símbolo (`€`), que es presentación. Y **ojo al choque que viene**: el §7.1 da a Organización un agregado `Divisa` con su `TipoCambio` (ítem 0.15). Serán dos cosas distintas con la misma palabra —el código que viaja en cada importe y la fila de la tabla de divisas—, así que al construirlo hay que decidir aquí cómo se llama cada una antes de escribirlo. |
| **Bloqueo** | Que un registro no admita nuevas operaciones: `EstaBloqueado`, `Desde` y `Motivo` (R16). | **No es un borrado lógico.** No existe ningún `Activo` en todo `src/` a propósito: un registro bloqueado se ve, se consulta y se lista; simplemente no deja operar. El borrado lógico llega en la fase 1 y será **otra cosa, con otro nombre**. |
| **NIF** | Objeto de valor con validación **real** de la letra, no un `string` con formato. | No es «el CIF». En castellano administrativo conviven NIF, CIF y NIE; aquí la palabra es **NIF** y cubre las tres formas. |
| **Dirección** | Objeto de valor en **campos separados** (R17): calle, número, código postal, población, subdivisión y país. | No es una entidad ni una cadena de texto. Y el campo es **subdivisión**, no «provincia»: fuera de España la división de primer nivel no se llama así, y el nombre del campo no puede suponer el país. |
| **Permiso** | La unidad atómica de autorización (`modulo.recurso.accion`). | No es un rol. Un permiso no se le da a nadie directamente. |
| **Resultado** | El retorno de una operación que puede fallar por una regla de negocio. | No es una excepción. La frontera entre los dos está escrita en el **ADR-0004**: la excepción es para lo que no estaba previsto. |
| **Inquilino** | El eje de aislamiento de datos: toda fila de negocio pertenece a uno (R8). | No es la `Empresa`, aunque hoy coincidan. Es el **eje**; `Empresa` es el agregado. Se dicen distinto porque el filtro es transversal y la empresa es de Organización. |
| **CreadoEn / ModificadoEn** | **Instantes** (`DateTimeOffset`), en UTC, del registro. | No son fechas de negocio. La fecha de una factura es un **día del calendario** y no lleva hora ni zona (R14): son dos conceptos distintos y llevan tipos distintos. |
| **Evento de integración** | Lo que un módulo publica para que otros reaccionen, por la bandeja de salida. Es la consecuencia de R12: si una transacción toca un solo agregado, lo que pase en otro va diferido. | No es una llamada. El que publica no sabe quién escucha ni espera respuesta. |

## Organización

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Empresa** | La entidad jurídica que opera en Bastion: CIF, razón social, dirección fiscal, divisa base, régimen de IVA. | No es «la instalación» ni «el cliente del ERP»: en una misma instalación conviven varias. |
| **Ejercicio** | El periodo contable de una empresa, con estado `Abierto` o `Cerrado`. | No es «el año». Puede no coincidir con el año natural, y por eso lleva fecha de inicio y de fin. |
| **Serie** (documental) | La numeración de un tipo de documento en una empresa y un ejercicio, con su **contador**. | El contador **no es una `SEQUENCE` de PostgreSQL**: es una columna. Una secuencia no es transaccional y deja huecos al hacer *rollback*, y R5 exige correlativa y **sin huecos** (ADR-0007 §5). |
| **Almacén** | El sitio físico donde hay existencias: empresa, código, dirección, tipo. | No es la ubicación dentro de él. |
| **Régimen de IVA** | El régimen fiscal bajo el que tributa una empresa. | No es el tipo impositivo de una línea. |

## Identidad

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Usuario** | Quien entra en Bastion. Es **global**: existe con independencia de las empresas. | No pertenece a una empresa. Lo que pertenece a una empresa es su membresía. |
| **Membresía** | Lo que ata un usuario a una empresa, con los roles que tiene **en esa** empresa. | No es un rol. El mismo usuario puede ser administrador en una empresa y solo lectura en otra: eso es una membresía por empresa, no un rol global. |
| **Rol** | Un manojo de permisos con nombre. | No es un permiso, y no se comprueba nunca directamente: se autoriza **por permiso**, para poder cambiar el reparto sin tocar el código. |
| **Testigo de acceso** | El *token* corto con el que se llama a la API. | No es el de refresco. Se dicen distinto porque duran distinto y se guardan en sitios distintos. |
| **Token de refresco** | El que canjea un testigo nuevo, con rotación y motivo de revocación. | No viaja en cada petición. |
| **Empresa activa** | Aquella con la que un usuario está operando **ahora**, dentro de la sesión. | No es «la empresa del usuario»: un usuario con varias membresías cambia de empresa activa sin volver a entrar. |

## Auditoría

| Término | Qué es | Y qué **no** es |
|---|---|---|
| **Registro de auditoría** | La anotación *append-only* de quién cambió qué, cuándo y desde qué empresa. **Una fila por entidad cambiada**, no por propiedad. | No se modifica ni se borra jamás, ni siquiera para corregirlo. Una corrección es **otro** registro. |

---

## Reservado por el plan maestro, todavía sin construir

Estas palabras ya tienen dueño en el §7 y **no se pueden usar para otra cosa**, ni buscarles
sinónimo. Se definen aquí cuando se construyan, no antes:

- **Impuesto**, **Divisa** + **TipoCambio**, **UnidadMedida** + **ConversionUM**, **Ubicacion**
  — Organización (§7.1). Llegan en el ítem **0.15**.
- **Tercero** — Terceros (§7.2). Es **un solo agregado** con roles `EsCliente` y `EsProveedor`:
  «cliente» y «proveedor» son **papeles del mismo tercero**, no dos entidades. Fase 1.
- **Artículo**, **Categoría**, **Tarifa** — Catálogo (§7.3). Fase 1.

---

## Cómo se escribe «Bastion»

Sin tilde, siempre, en todas partes: código, prosa, interfaz y base de datos (Anexo A.1). La base de
datos es `bastion` (`bastion_dev` en desarrollo), un esquema PostgreSQL por módulo en `snake_case`, y
la raíz de espacios de nombres es `Bastion` sin repetirla dentro.
