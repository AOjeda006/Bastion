# `inicio` — el punto de partida y el callejón sin salida

**Propósito.** Las dos pantallas que no consultan nada: la portada de quien acaba de entrar y la de
una dirección que no existe.

## Rutas

| Ruta | Exigencia            | Título               |
| ---- | -------------------- | -------------------- |
| `/`  | sesión (sin permiso) | Inicio               |
| `*`  | pública              | Página no encontrada |

`/` no exige ningún permiso a propósito: es lo primero que ve alguien recién entrado, y una portada
que se puede negar dejaría a un usuario sin sitio al que ir. Lo que sí hace es enseñar **solo** lo
que sus permisos alcanzan.

`*` es pública porque una dirección equivocada la teclea también quien no ha entrado, y contestarle
con la pantalla de acceso en vez de con «esto no existe» es contestarle otra cosa.

## Claves de consulta

Ninguna.
