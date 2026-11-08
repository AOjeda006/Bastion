# `organizacion` — la estructura de la empresa

Espeja el módulo **Organización** del backend (`Bastion.Organizacion.*`, `/api/v1/organizacion/`).
Dentro, una carpeta por recurso; entre funcionalidades, nada: `organizacion` no importa de
`identidad` ni al revés, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

**El corte es el módulo y no el recurso**, y esa es la decisión del ítem 0.16. `almacenes` y
`empresas` son dos recursos del mismo módulo, así que comparten funcionalidad y pueden compartir
código sin pedirle permiso a nadie. Repartir por recurso obligaría a subir a `shared/` todo lo común
—en la fase 1, la ficha entera de un tercero que es cliente y proveedor a la vez—, y `shared/` es el
sistema de componentes, no un almacén de dominio.

Con qué empresa se opera lo dice `shared/sesion/`. El **selector** vive en el armazón
(`app/SelectorDeEmpresa.tsx`) porque decide para todas las pantallas, no solo para estas dos.

**Y las dos tienen zona horaria** (ítem 3.5, ADR-0079): una de tres, que dice qué día es en cada
sitio. Los dos listados la enseñan, y con el permiso de modificar la cambian en una ventana modal,
la misma para los dos, en `zonas/` (más abajo).

## `almacenes` — los almacenes de la empresa activa

**Propósito.** Enseñar los almacenes con los que se está operando, paginados, y cambiar la zona
horaria de cada uno.

### Rutas

| Ruta         | Exigencia                          | Título    |
| ------------ | ---------------------------------- | --------- |
| `/almacenes` | permiso `organizacion.almacen.ver` | Almacenes |

«Cambiar», en la columna de la zona, solo con `organizacion.almacen.modificar`. La interfaz lo
oculta y el servidor lo autoriza.

Parámetros de URL: `?pagina=` y `?tamanio=`. **Están en la URL y no en un `useState`**: así el
enlace se puede compartir, la flecha de atrás hace lo que se espera y una recarga no pierde el
sitio. Se leen con un esquema con valores por omisión, porque la URL la escribe cualquiera.

### Claves de consulta

`almacenes/api/claves.ts`, jerárquicas:

```
['almacenes']                          → clavesDeAlmacenes.todo
['almacenes', 'lista']                 → clavesDeAlmacenes.listas()
['almacenes', 'lista', { pagina, tamanio }] → clavesDeAlmacenes.lista(paginacion)
['almacenes', 'ficha', id]             → clavesDeAlmacenes.ficha(id)
```

`staleTime` de cinco minutos: un almacén es dato maestro, se da de alta y se queda ahí. La ficha,
con su `ETag`, la lee la ventana de la zona cada vez que se abre, porque su `staleTime` es el de
por omisión, cero: el `PUT` reemplaza el almacén entero, y lo que no es la zona tiene que volver
como está ahora. Y no se guarda al cerrarla (`gcTime: 0`): al volver a abrirla, la ventana espera
a la lectura de ahora, y no ofrece la de antes mientras llega.

### Lo que no es evidente

- **La empresa NO forma parte de la clave.** Va dentro del testigo, y el servidor filtra por
  inquilinato (R8). Por eso el cambio de empresa no invalida estas claves a mano: reinicia la caché
  **entera** (`resetQueries()` en `app/SelectorDeEmpresa.tsx`). Una lista de claves elegidas a mano
  es una lista que alguien olvidará ampliar.
- `AlmacenDto` sale de `shared/api/esquema.ts`, que se **genera** (`npm run api`). Se traduce a
  `almacenes/model/almacen.ts` en `almacenes/api/consultas.ts`: los tipos del contrato no salen de
  la capa `api`.
- **La zona de un almacén es la suya o la de su empresa**, que hereda en vivo: el contrato trae
  las dos, la propia (`zonaHoraria`, nula si hereda) y la que rige (`zonaEfectiva`). El modelo se
  queda con la que rige y con si es propia, que es lo que enseña la fila: «Canarias · propia», o
  «Península y Baleares · de la empresa». Volver a heredar es mandar la propia nula.
- **Cambiar una zona invalida la caché entera**, con `invalidateQueries()` y no estas claves: mueve
  el «hoy» de los formularios de Inventario (`['hoy']`, en `shared/hoy/`) y el día de lo que se
  haya leído con ella, y esas claves son de otras funcionalidades. Invalidar, y no reiniciar como
  el cambio de empresa: el listado sigue en pantalla mientras se vuelve a pedir, y el foco vuelve a
  su «Cambiar».

## `empresas` — las empresas del grupo

**Propósito.** Enseñar las empresas dadas de alta, y cambiar la zona horaria de cada una. No es
el selector.

### Rutas

| Ruta        | Exigencia                          | Título   |
| ----------- | ---------------------------------- | -------- |
| `/empresas` | permiso `organizacion.empresa.ver` | Empresas |

Parámetros de URL: `?pagina=` y `?tamanio=`. «Cambiar», en la columna de la zona, solo con
`organizacion.empresa.modificar`.

### Claves de consulta

`empresas/api/claves.ts`, jerárquicas:

```
['empresas']
['empresas', 'lista']
['empresas', 'lista', { pagina, tamanio }]
['empresas', 'ficha', id]
```

La ficha, como la del almacén: la lee la ventana de la zona cada vez, con su `ETag`.

### Lo que no es evidente

- El listado devuelve **las empresas que el usuario alcanza**, que no tienen por qué ser todas: lo
  decide el servidor. La interfaz no filtra nada por su cuenta.
- Una empresa bloqueada (R16) contesta 404 a su propio `GET`, así que **no aparece** aquí ni hay
  forma de desbloquearla desde el frontal. Está anotado en `docs/PLAN.md`; ensanchar el ámbito de
  lectura desde el navegador sería exactamente lo contrario de lo que el filtro protege.
- **La zona de la empresa es siempre suya**, y la fila no dice de dónde le viene. Cambiarla cambia
  también la de los almacenes que la heredan, y la ventana lo avisa.

## `zonas` — lo común de las dos zonas

No es un recurso: es lo que comparten las dos ventanas, y vive aquí porque las dos son del mismo
módulo.

- **`model/zona.ts`**, las tres de la lista cerrada (ADR-0079 §1) con el nombre que les da la
  pantalla —`peninsula`, `canarias`, `ceuta`—, y, para un almacén, `deLaEmpresa`. Ampliar la lista
  es una pregunta para el usuario.
- **`api/contrato.ts`**, la traducción de y hacia la zona del contrato (`Europe/Madrid`…). Un
  `Record` por sentido: una zona nueva en el contrato no compila hasta que tiene nombre.
- **`ui/FormularioDeZona.tsx`**, lo de dentro de la ventana. **Avisa antes de las opciones**, y en
  la descripción del grupo: lo ya fechado no cambia, y durante la hora que sigue al cambio una
  operación fechada hoy puede no admitirse (ADR-0079 §7). Guardar la que ya tenía cierra sin
  escribir; un `412` lo dice arriba y ofrece cargar la versión de ahora.
- **`ui/LaZonaEnLaFila.tsx`**, la celda de la fila. La región que dice cómo ha quedado, que
  existe antes de tener texto para que el lector de pantalla la anuncie, subió en el 3.6 a
  `shared/ui/ElAviso.tsx`, porque la retirada del artículo, en Catálogo, también la usa.

**La ventana es el `<dialog>` del navegador** (`shared/ui/Dialogo.tsx`, ADR-0080): modal con
`showModal()`, Escape la cierra y el foco vuelve a su «Cambiar». Se desmonta con el evento `close`,
y no antes; desde dentro se cierra con `cerrarElDialogo`. jsdom no la sabe abrir: `setupTests.ts`
le pone lo justo, y lo que hace el navegador lo prueba `Dialogo.navegador.test.tsx`, en Chromium.
