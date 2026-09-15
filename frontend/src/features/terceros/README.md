# `terceros` — clientes, proveedores, o las dos cosas

Espeja el módulo **Terceros** del backend (`Bastion.Terceros.*`, `/api/v1/terceros/`). Dentro, una
carpeta por recurso; entre funcionalidades, nada: `terceros` no importa de `organizacion` ni al
revés, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

Un tercero puede ser cliente y proveedor **a la vez** —la ferretería a la que se le compra y a la
que se le vende es una sola ficha con un solo identificador fiscal—, y por eso no hay dos recursos
sino uno. Repartirlo en `clientes/` y `proveedores/` obligaría a subir la ficha común a `shared/`,
que es el sistema de componentes y no un almacén de dominio (la misma decisión del ítem 0.16).

## `terceros` — los terceros de la empresa activa

**Propósito.** Enseñar los terceros con los que se está operando, paginados y filtrados, y darlos
de alta en bloque desde un CSV de Excel.

### Rutas

| Ruta                    | Exigencia                           | Título            |
| ----------------------- | ----------------------------------- | ----------------- |
| `/terceros`             | permiso `terceros.tercero.ver`      | Terceros          |
| `/terceros/importacion` | permiso `terceros.tercero.importar` | Importar terceros |

Parámetros de URL: `?pagina=`, `?tamanio=` y `?busqueda=`. Están en la URL y no en un `useState`
por lo de siempre: el listado filtrado se puede pegar en un correo, la flecha de atrás deshace el
filtro y una recarga no pierde el sitio.

`/terceros/importacion` no sale en la navegación: se llega por el enlace del listado, que solo se
pinta a quien tiene el permiso de importar. El servidor exige además el de dar de alta —y el de
fijar el límite si alguna fila lo trae—, pero eso lo contesta con un error con nombre; ocultar la
ruta por esos permisos escondería la pantalla a quien sí puede importar filas sin límite.

### Claves de consulta

`terceros/api/claves.ts`, jerárquicas:

```
['terceros']                                        → clavesDeTerceros.todo
['terceros', 'lista']                               → clavesDeTerceros.listas()
['terceros', 'lista', { pagina, tamanio, busqueda }] → clavesDeTerceros.lista(listado)
```

`staleTime` de cinco minutos: un tercero es dato maestro, se da de alta y se queda ahí.

### Lo que no es evidente

- **El recuadro de filtro NO busca por identificador fiscal, y es una decisión, no un olvido**
  (`docs/adr/adr-0025`). Lo que se teclea ahí acaba en la URL, o sea en el historial del navegador,
  en el enlace que se copia por chat, en la cabecera `Referer` y en el registro de acceso del
  servidor de delante — que se guarda más tiempo y con menos cuidado que la base de datos. Un trozo
  de nombre comercial es un dato de pantalla; el NIF de un cliente es muy a menudo el DNI de una
  persona física. Buscar por él existe en la API (`POST /api/v1/terceros/terceros/buscar`, con el
  criterio en el cuerpo y cursor opaco), y **esta pantalla todavía no lo usa**: está anotado en
  `docs/PLAN.md`. Si alguien añade aquí un campo «NIF», lo que hay que cambiar es por dónde viaja.
- **El estado de verificación se pinta siempre, también cuando está comprobado.** Enseñar el sello
  solo cuando algo va mal deja a quien mira sin saber si la ausencia significa «comprobado» o «esta
  versión no lo enseñaba», y esa duda vale lo mismo que no decir nada.
- **`Verificacion` tiene un tercer valor que la API no emite**, `desconocida`. No es defensa por si
  acaso: el enumerado viaja como texto y el frontal se despliega aparte del backend, así que un
  valor nuevo llega antes de que `api/consultas.ts` lo conozca. Sin ese caso, la traducción
  devolvería `undefined` y la celda saldría vacía — que es lo que se ve cuando algo se rompe— en
  vez de decir que no se sabe. Lo que no se puede validar se marca como no validado.
- **Un tercero bloqueado (R16) no aparece aquí** y no hay forma de desbloquearlo desde el frontal.
  El alta contra un identificador ocupado contesta `409` **sin decir si quien lo ocupa está activo
  o bloqueado**, y eso es una propiedad del servidor —las dos respuestas son idénticas byte a
  byte—, no una redacción que se pueda deshacer desde aquí.
- **La importación manda los bytes del fichero, nunca su texto** (`terceros/api/importacion.ts`).
  El servidor decide la codificación mirando los bytes —UTF-8, o Windows-1252 si no lo son—, y
  leer el fichero en el navegador lo decodificaría como UTF-8: cada «ñ» de un CSV de Excel en
  Windows-1252 llegaría como carácter de reemplazo. Tampoco se adivina aquí el separador ni se
  valida la cabecera: eso lo dice el servidor con un error con nombre (`docs/adr/adr-0034`), y una
  segunda opinión en la pantalla podría no coincidir con la que decide.
- **La clave de idempotencia es de la elección del fichero, no del envío.** Reintentar tras un
  fallo de red repite la clave y el servidor devuelve el informe guardado sin importar dos veces;
  elegir otro fichero estrena clave. Volver a intentarlo solo se ofrece cuando el fallo no tiene
  nombre: con nombre, el mismo fichero daría el mismo error.
- **La cabecera de la plantilla está copiada en `terceros/model/importacion.ts`**, porque el
  contrato no la publica como dato y la pantalla tiene que enseñarla. No se queda en promesa:
  `LaPlantillaDelFrontalEsLaDeLaApiTests`, en el carril rápido de la API, compara esa lista con la
  del servidor.
- **El informe nunca trae un valor del fichero**: columna, motivo y líneas de la hoja (la cabecera
  es la 1). El motivo tiene un valor `desconocido` por lo mismo que `Verificacion`.
- Importar invalida `clavesDeTerceros.todo` solo si ha entrado alguna fila.
- `TerceroDto` sale de `shared/api/esquema.ts`, que se **genera** (`npm run api`). Se traduce a
  `terceros/model/tercero.ts` en `terceros/api/consultas.ts`: los tipos del contrato no salen de la
  capa `api`.
- La empresa **no** forma parte de la clave de consulta: va dentro del testigo y quien filtra es el
  servidor (R8). Por eso el cambio de empresa no invalida esto a mano — vacía la caché entera.
