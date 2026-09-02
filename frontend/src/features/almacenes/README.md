# `almacenes` — listado de los almacenes de la empresa activa

**Propósito.** Enseñar los almacenes con los que se está operando, paginados.

## Rutas

| Ruta         | Exigencia                          | Título    |
| ------------ | ---------------------------------- | --------- |
| `/almacenes` | permiso `organizacion.almacen.ver` | Almacenes |

Parámetros de URL: `?pagina=` y `?tamanio=`. **Están en la URL y no en un `useState`**: así el
enlace se puede compartir, la flecha de atrás hace lo que se espera y una recarga no pierde el
sitio. Se leen con un esquema con valores por omisión, porque la URL la escribe cualquiera.

## Claves de consulta

`api/claves.ts`, jerárquicas:

```
['almacenes']                          → clavesDeAlmacenes.todo
['almacenes', 'lista']                 → clavesDeAlmacenes.listas()
['almacenes', 'lista', { pagina, tamanio }] → clavesDeAlmacenes.lista(paginacion)
```

`staleTime` de cinco minutos: un almacén es dato maestro, se da de alta y se queda ahí.

## Lo que no es evidente

- **La empresa NO forma parte de la clave.** Va dentro del testigo, y el servidor filtra por
  inquilinato (R8). Por eso el cambio de empresa no invalida estas claves a mano: reinicia la caché
  **entera** (`resetQueries()` en `app/SelectorDeEmpresa.tsx`). Una lista de claves elegidas a mano
  es una lista que alguien olvidará ampliar.
- `AlmacenDto` sale de `shared/api/esquema.ts`, que se **genera** (`npm run api`). Se traduce a
  `model/almacen.ts` en `api/consultas.ts`: los tipos del contrato no salen de la capa `api`.
