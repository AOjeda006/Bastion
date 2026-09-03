# `app/paginas` — las pantallas que no son de ningún módulo

**Propósito.** Las dos pantallas que no consultan nada: la portada de quien acaba de entrar y la de
una dirección que no existe.

**Por qué están en el armazón y no en `features/`.** Desde el ítem 0.16, una carpeta de `features/`
espeja un módulo del backend, y estas dos no son de ninguno: la portada dice quién ha entrado y con
qué empresa opera —lo cual es el armazón hablando de sí mismo—, y «no encontrada» es la respuesta
del enrutador a una URL que no casa con nada. Meterlas en `identidad` o en `organizacion` les daría
un dueño que no tienen, y una funcionalidad no puede importar de otra: la primera pantalla de
cualquier módulo futuro que quisiera enlazar aquí se encontraría con la frontera.

`shared/` tampoco vale: ahí va lo que dos funcionalidades necesitan, y esto no lo necesita ninguna.
El armazón, en cambio, ya monta la disposición, la navegación y el selector de empresa una sola vez;
la portada es una pieza más de eso.

Que estén aquí no las deja fuera de la tabla: siguen declaradas en `app/rutas.tsx` como todas, con
`duenio: 'armazon'`, y el barrido comprueba que el módulo que cargan vive donde su dueño dice.

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
