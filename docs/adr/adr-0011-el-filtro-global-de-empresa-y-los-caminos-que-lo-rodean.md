---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, seguridad, multiempresa, testing]
revisado: 2026-08-27
tags: [adr, r8, multiempresa, query-filters, ef-core, 404, fail-closed]
---

# ADR-0011: El filtro global de empresa, y los caminos que lo rodean

- **Estado:** aceptado
- **Fecha:** 2026-08-27

## Contexto

R8 dice que la empresa sale del *claim* y de ningún otro sitio, y que ninguna consulta ve filas de
otra empresa. La forma barata de cumplirlo —que cada repositorio escriba `.Where(x => x.EmpresaId
== empresaId)`— falla por donde fallan todas las reglas que dependen de que alguien se acuerde:
basta un listado nuevo al que se le olvide, y el síntoma no es un error sino un `200` con los datos
del vecino dentro.

Así que el filtro es **global**, va en el modelo de EF Core, y lo que hay que decidir no es si
ponerlo, sino: qué entidades filtra, qué pasa cuando no hay empresa, y por dónde se le puede dar
esquinazo.

## 1. Qué es de inquilino y qué no

La clasificación va **antes** que el código, porque decidirla mal no se nota: una entidad global de
más es una fuga permanente, y una de menos es una funcionalidad que no se puede construir.

| Entidad | ¿De inquilino? | Por qué |
|---|---|---|
| `Empresa` | Sí, **por su propia clave** | Es la raíz: no lleva `empresa_id` porque ella *es* el inquilino. Se filtra por `Id`, y el efecto buscado es que el padrón de empresas deje de ser legible desde dentro de cualquiera de ellas. |
| `Ejercicio` | Sí | Un ejercicio contable es de una empresa. Dos empresas tienen su 2026 cada una. |
| `Serie` | Sí | La numeración de facturas es de una empresa y de un ejercicio; compartirla sería un problema fiscal, no de privacidad. |
| `Almacen` | Sí | Existencias, ubicaciones, movimientos: el dato de negocio más obvio. |
| `Membresia` | Sí — **es el puente** | Lleva `empresa_id` y es la relación que dice quién está en qué empresa. Es la entidad sobre la que se apoya el filtro de `Usuario`. |
| `Usuario` | Global, con **consulta acotada** | Una cuenta es una, con un correo, y puede pertenecer a varias empresas: no puede llevar `empresa_id`. Pero «global» no puede significar «consultable desde cualquier empresa», así que el filtro va por la pertenencia: `usuario.Membresias.Any(m => m.EmpresaId == …)`. |
| `Rol` | Global | Decisión: un rol es un catálogo de permisos de la instalación. **Consecuencia asumida:** un rol creado desde una empresa se ve y se asigna desde las demás. Está escrito aquí para que no se descubra por sorpresa. |
| `PermisoDeRol` | Global | Es parte del rol. |
| `RolDeMembresia` | Global **de hecho** | Depende de la pertenencia, que sí filtra, y no tiene navegación de vuelta con la que escribir un filtro propio. Es seguro **mientras no se consulte por su cuenta**, y eso no se confía: se comprueba (§4). |
| `TokenDeRefresco` | Global | Una emisión de refresco es de una sesión, no de una empresa: se busca por su resumen antes de que haya empresa activa. La empresa con la que se estaba operando va dentro de la fila y la comprueba `RenovarSesion`. |

Que no falte ninguna no lo garantiza la buena voluntad: lo garantiza
`CadaEntidadDeclaraSuInquilinatoTests`, que recorre el modelo **ya construido** y compara, en los
dos sentidos, contra esta misma lista.

## 2. Falla cerrado: «sin inquilino» no degrada a «sin filtro»

`IInquilinoActual.EmpresaDelFiltro` **lanza** `FaltaLaEmpresaActivaException` cuando no hay empresa
activa. No devuelve nulo, ni `Guid.Empty`, ni se salta el filtro: las tres serían un valor por
omisión que rellena el hueco y lo esconde, y el síntoma —«no tienes almacenes», o peor, «aquí están
los de todos»— no se distingue de un dato correcto.

Pero hay caminos legítimos sin principal, y negarlos sería negar el arranque. Para esos, y solo para
esos, existe un ámbito **explícito, con nombre y anotado en el registro**:

```csharp
using (inquilino.SinInquilino(MotivoSinInquilino.UnicidadGlobal))
{
    ocupado = await empresas.ExisteConNifAsync(nif, cancelacion);
}
```

Los motivos son un enumerado cerrado, no una cadena libre: añadir uno obliga a tocar el tipo, que es
un cambio que se ve en la revisión; un `string` dejaría abrir el ámbito con «temporal».

| Motivo | Para qué | Dónde |
|---|---|---|
| `SemillaDeArranque` | La primera empresa y la primera cuenta. Todavía no existe nadie que pueda tener un *claim*. | `SemillaDeArranque.SembrarAsync` |
| `AutenticacionYSesion` | Entrar, renovar y cambiar de empresa: la empresa es lo que la operación **decide**, así que no puede ser lo que la filtra. | `IniciarSesion`, `RenovarSesion`, `CambiarEmpresaActiva` |
| `UnicidadGlobal` | El NIF de una empresa y el correo de un usuario son únicos en toda la instalación. Filtrada, la comprobación diría «libre» sobre un valor ocupado y el alta se estrellaría contra el índice único: un `500` donde toca un `409`. | `CrearEmpresa`, `CrearUsuario` |
| `AdministracionDePertenencias` | Es la operación que **por definición** habla de una empresa que no es la activa: el arranque en frío de una empresa recién creada. Quién puede nombrar qué empresa no lo decide el filtro, lo decide `ErroresDePertenencia.PuedeAdministrarAsync`. | `ConcederPertenencia`, `RetirarPertenencia`, `AsignarRol`, `RetirarRol` |

El ámbito vive en un `AsyncLocal`, **no** en un campo estático a secas: el host atiende varias
peticiones a la vez en el mismo proceso, y un estático convertiría «la semilla está sembrando» en
«nadie filtra, para todos». Anidar está permitido y al cerrarse se recupera el de fuera —la semilla
abre el suyo y por dentro llama a la comprobación de unicidad, que abre el suyo—.

Para el **0.8**: el trabajo de fondo que publique la bandeja de salida corre sin petición y por
tanto sin *claim*. Su sitio es un motivo nuevo en este enumerado, no un `IgnoreQueryFilters` ni un
contexto sin filtro. Queda dicho aquí para que el 0.8 no tenga que inventarlo.

## 3. La trampa del filtro congelado

EF Core cachea el modelo por tipo de contexto y opciones. Si el filtro se construyera con un
**valor** —el identificador copiado en el constructor, o una expresión armada por reflexión sobre
`IDeInquilino` con `Expression.Constant(this)` dentro—, el modelo se quedaría con el inquilino del
**primer** contexto que lo construyera y se lo serviría a todos los siguientes. Nadie vería un
error: la segunda empresa recibiría las filas de la primera con un `200`.

Por eso:

- El filtro lee `EmpresaDelFiltro`, que es una **propiedad de instancia** del contexto, evaluada en
  cada consulta.
- Los filtros se escriben **a mano**, una línea por entidad, en el `OnModelCreating` de cada
  contexto. Un barrido por reflexión sería más corto y peor. Lo que la reflexión iba a garantizar
  —que no falte ninguna— lo garantiza el test que recorre el modelo.
- Los contextos se registran con **`AddDbContext`** (uno por petición), **no** con
  `AddDbContextPool`. Con agrupación, un contexto reutilizado que hubiera copiado el inquilino se
  lo serviría al siguiente. Hoy la trampa no está armada; la propiedad es lo que hace que siga sin
  estarlo el día que alguien active la agrupación buscando rendimiento.

Lo fija `ElFiltroSeLeeEnCadaConsultaTests`, con tres casos. Y hay que decir lo que **no** cubrían
los dos primeros: mientras cada consulta abra su propio ámbito de servicios, un identificador
copiado en el constructor se comporta igual que leerlo cada vez, y ninguno de los dos lo
distinguiría —se comprobó por mutación y salieron verdes—. El tercero reutiliza la **misma**
instancia de contexto con dos empresas, que es lo que haría la agrupación, y ese sí lo distingue.

## 4. Los caminos que rodean el filtro

Un filtro de consulta protege lo que pasa por el traductor de consultas y nada más. La lista es
corta, es conocida, y cada línea tiene un test que la ejerce o una prohibición comprobada. Un camino
sin ninguna de las dos cosas es un camino abierto.

| Camino | Qué lo impide |
|---|---|
| Listado sin filtro explícito | **Test**: `Un_listado_sin_filtro_explicito_no_devuelve_datos_de_otra_empresa`. |
| El total de la paginación (otra consulta distinta de la de los elementos) | **Test**: `El_total_de_la_pagina_tampoco_cuenta_las_filas_de_otra_empresa`. |
| Lectura por identificador de una fila ajena | **Test**: `Una_fila_de_otra_empresa_no_se_distingue_de_una_que_no_existe`. |
| **Escritura** por identificador contra una fila ajena (`PUT`) | **Test**: `Una_escritura_por_identificador_contra_una_fila_de_otra_empresa_es_404`, que además comprueba que la fila no ha cambiado. |
| **Borrado** por identificador contra una fila ajena (`DELETE`) | **Test**: `Un_borrado_por_identificador_contra_una_fila_de_otra_empresa_es_404`, que además comprueba que sigue activa. |
| La empresa raíz: leer el padrón desde otra empresa | **Test**: `El_padron_de_empresas_no_se_lee_desde_otra_empresa`. |
| Un usuario con el que no se comparte empresa | **Test**: `Un_usuario_que_no_comparte_empresa_no_se_ve`. |
| Navegaciones y claves que apuntan fuera (`Include`, `ejercicioId` de otra empresa) | **Test**: `Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400_del_campo_ejercicioId`. Desde 0.6 el ejercicio ajeno además ya **no se ve**, así que la comprobación explícita pasa a ser la segunda línea de defensa, no la única. |
| Una entidad nueva a la que se le olvide el filtro | **Test**: `CadaEntidadDeclaraSuInquilinatoTests`, en los dos sentidos. Es lo único de esta tabla que escala a los dieciséis módulos del §5. |
| `IgnoreQueryFilters()` | **Prohibición comprobada** (`ElFiltroNoSeSaltaPorAhiTests`). Se puede prohibir del todo porque el ámbito auditado cubre todo lo que hacía falta. |
| SQL crudo: `FromSql*`, `ExecuteSql*`, `SqlQuery` | **Prohibición comprobada**: no pasan por el traductor, así que el filtro no se les aplica. |
| `ExecuteUpdate` / `ExecuteDelete` | **Prohibición comprobada**: respetan el filtro de la consulta, pero saltan el rastreador y la unidad de trabajo, así que ni la auditoría (0.7) ni la concurrencia (0.9) los verían pasar. |
| `Find` / `FindAsync` y el rastreador de cambios | **Prohibición comprobada**: buscan por clave y pueden contestar desde el rastreador **sin consultar**; cuando contestan desde ahí, no hay consulta que filtrar. |
| Consultar una entidad dependiente global por su cuenta (`Set<RolDeMembresia>`, `Set<PermisoDeRol>`) | **Prohibición comprobada**, con una excepción anotada: `RepositorioDeRoles.PermisosDeAsync` usa `Set<PermisoDeRol>` para armar los permisos del token. Los identificadores de rol no vienen de la petición: los pone `ConstructorDeSesion` a partir de la membresía, que sí filtra. |
| Abrir un ámbito sin inquilino donde no toca | **Prohibición comprobada**: la lista de ficheros y el número de aperturas de cada uno se comparan enteros, en los dos sentidos. |
| Definir un filtro global fuera de los contextos de módulo | **Prohibición comprobada**: repartirlos no rompería nada, solo haría imposible contestar «¿qué filtra y qué no?» leyendo un sitio. |
| No tener empresa y que eso pase desapercibido | **Test**: `SinEmpresaNoSeConsultaTests`, cuatro casos sobre el `IInquilinoActual` que resuelve el contenedor de verdad. |

El barrido de prohibiciones lee los ficheros de `src/` **con los comentarios quitados** y solo los
que ven EF Core: lo que se prohíbe es llamar, no nombrar, y un `.Find(` en el dominio es el de
`List<T>`.

## 5. La forma fuerte de «el identificador del cuerpo se ignora»

Comprobar que un campo se ignora exige que el campo exista, y un campo que existe es un campo que
algún día alguien lee «solo para este caso». La regla de verdad es la **ausencia**: ninguna acción
tiene por dónde recibir la empresa, ni en el cuerpo, ni en la ruta, ni en la cadena de consulta, ni
en una cabecera. Lo comprueba `NingunaPeticionNombraLaEmpresaTests` sobre la tabla de rutas que el
host construye de verdad, así que un endpoint nuevo entra solo.

Las excepciones son seis acciones y todas de la misma familia: operaciones cuyo **sujeto** es la
empresa —a cuál se entra, en cuál se da de alta a alguien—, no operaciones sobre filas de una
empresa. En todas ellas el valor recibido **no se usa como inquilino**: se contrasta contra el
*claim* antes de tocar nada, y la lista dice quién lo hace en cada caso.

Y encima de la ausencia, el efecto: `El_identificador_de_empresa_que_venga_en_la_peticion_se_ignora`
manda el identificador de la otra empresa por los tres sitios a la vez y comprueba las dos mitades
de «no cambia nada» —la fila nace en la empresa de quien llamaba, y en la otra no aparece—.

## 6. Una fila ajena es un `404`, no un `403`

Un `403` sobre una fila de otra empresa contesta «eso existe y no es tuyo», y con eso se enumera el
negocio del vecino sin leer un solo dato suyo: cuántos almacenes tiene, con qué identificadores, si
la factura que se busca existe. Así que **una fila ajena y una inexistente devuelven exactamente lo
mismo**: el mismo código y el mismo `ProblemDetails`, `type` incluido.

Esto **no** contradice el `403 /errors/empresa-ajena` del 0.5, y la diferencia es la que importa:

- **`404` (0.6):** la petición nombra una **fila** por su identificador. Que esa fila sea de otra
  empresa es información que no se da. La empresa no la nombra la petición: sale del *claim*.
- **`403 /errors/empresa-ajena` (0.5):** la petición nombra una **empresa** —es una de las seis
  acciones del §5—, y lo que se niega es la operación, no la existencia de nada. Quien la recibe ya
  sabía qué empresa había escrito; no aprende nada nuevo.

## Consecuencias

- **El padrón de empresas deja de ser legible desde dentro de una empresa.** `GET /empresas/{otra}`
  es `404`, y el listado solo trae la activa. Eso alcanza al arranque en frío: quien crea una
  empresa recibe un `201` con un `Location` que **todavía no puede seguir**, porque aún no está
  dentro. Cuatro tests de contrato del 0.5 pasaron a entrar en la empresa antes de operar sobre
  ella; no se relajó ninguna aserción.
- **Un rol creado desde una empresa se ve y se asigna desde las demás.** Es la consecuencia directa
  de que `Rol` sea global, y hoy es el mayor cruce que queda entre empresas. No es un descuido: está
  decidido, está escrito y tiene su línea en la tabla del §1.
- **Un usuario con el que no se comparte empresa da `404`**, no `403`, por lo del §6.
- El acceso, el refresco y el cambio de empresa corren **enteros** dentro de un ámbito sin
  inquilino. Es más de lo estrictamente necesario, y es a propósito: armar una sesión vuelve a leer
  la pertenencia de la empresa destino, y trocear el ámbito dejaría un camino sutil por el que la
  empresa que se está abandonando decidiría qué se puede ver de la que se abre.
- **Lo que sigue sin estar, y no es de este ítem:** la auditoría de accesos (0.7), la bandeja de
  salida y su trabajo de fondo (0.8, que necesitará su motivo en el enumerado), la idempotencia y
  el `If-Match` (0.9), el tipo base de entidad (0.10), la pantalla de cambio de empresa (0.11) y los
  tests de arquitectura con NetArchTest (0.12). Las prohibiciones de este ADR se comprueban hoy
  leyendo los fuentes; cuando exista el proyecto del 0.12 será el momento de decidir si alguna se
  expresa mejor allí.
