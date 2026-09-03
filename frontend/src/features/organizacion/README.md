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

## `almacenes` — los almacenes de la empresa activa

**Propósito.** Enseñar los almacenes con los que se está operando, paginados.

### Rutas

| Ruta         | Exigencia                          | Título    |
| ------------ | ---------------------------------- | --------- |
| `/almacenes` | permiso `organizacion.almacen.ver` | Almacenes |

Parámetros de URL: `?pagina=` y `?tamanio=`. **Están en la URL y no en un `useState`**: así el
enlace se puede compartir, la flecha de atrás hace lo que se espera y una recarga no pierde el
sitio. Se leen con un esquema con valores por omisión, porque la URL la escribe cualquiera.

### Claves de consulta

`almacenes/api/claves.ts`, jerárquicas:

```
['almacenes']                          → clavesDeAlmacenes.todo
['almacenes', 'lista']                 → clavesDeAlmacenes.listas()
['almacenes', 'lista', { pagina, tamanio }] → clavesDeAlmacenes.lista(paginacion)
```

`staleTime` de cinco minutos: un almacén es dato maestro, se da de alta y se queda ahí.

### Lo que no es evidente

- **La empresa NO forma parte de la clave.** Va dentro del testigo, y el servidor filtra por
  inquilinato (R8). Por eso el cambio de empresa no invalida estas claves a mano: reinicia la caché
  **entera** (`resetQueries()` en `app/SelectorDeEmpresa.tsx`). Una lista de claves elegidas a mano
  es una lista que alguien olvidará ampliar.
- `AlmacenDto` sale de `shared/api/esquema.ts`, que se **genera** (`npm run api`). Se traduce a
  `almacenes/model/almacen.ts` en `almacenes/api/consultas.ts`: los tipos del contrato no salen de
  la capa `api`.

## `empresas` — las empresas del grupo

**Propósito.** Enseñar las empresas dadas de alta. No es el selector.

### Rutas

| Ruta        | Exigencia                          | Título   |
| ----------- | ---------------------------------- | -------- |
| `/empresas` | permiso `organizacion.empresa.ver` | Empresas |

Parámetros de URL: `?pagina=` y `?tamanio=`.

### Claves de consulta

`empresas/api/claves.ts`, jerárquicas:

```
['empresas']
['empresas', 'lista']
['empresas', 'lista', { pagina, tamanio }]
```

### Lo que no es evidente

- El listado devuelve **las empresas que el usuario alcanza**, que no tienen por qué ser todas: lo
  decide el servidor. La interfaz no filtra nada por su cuenta.
- Una empresa bloqueada (R16) contesta 404 a su propio `GET`, así que **no aparece** aquí ni hay
  forma de desbloquearla desde el frontal. Está anotado en `docs/PLAN.md`; ensanchar el ámbito de
  lectura desde el navegador sería exactamente lo contrario de lo que el filtro protege.
