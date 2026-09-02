# `acceso` — la puerta

**Propósito.** Abrir sesión con correo y contraseña, y dejar a la persona donde iba.

## Rutas

| Ruta      | Exigencia | Título         |
| --------- | --------- | -------------- |
| `/acceso` | pública   | Iniciar sesión |

Es una de las dos rutas públicas del armazón (la otra es la de «no encontrada»), y su motivo está
escrito en `app/rutas.tsx`: sin ella no se puede llegar a ninguna de las demás.

## Claves de consulta

Ninguna. Aquí no se consulta nada: se envía un formulario. Lo que sí hace al entrar es **vaciar la
caché** (`clear()`), porque en esta pestaña puede quedar lo que consultó quien estuviera antes.

## Lo que no es evidente

- El mensaje de credenciales malas es **uno solo** para «ese correo no existe» y para «la contraseña
  no es esa». Distinguirlos le regalaría a quien prueba correos la lista de los que existen.
- El destino al que volver viaja en el estado de la navegación, que lo pone la guarda. Viene de
  fuera, así que se valida (`destinoSeguro`): `//otro-dominio.example` es una URL absoluta y
  aceptarla convertiría el acceso en un redirector abierto.
- La validación de este formulario es **comodidad**, no autoridad: replica las reglas del servidor
  para no ir y volver, y quien decide es la API.
