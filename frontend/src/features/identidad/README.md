# `identidad` — quién entra

Espeja el módulo **Identidad** del backend (`Bastion.Identidad.*`, `/api/v1/identidad/`). Dentro,
una carpeta por recurso; entre funcionalidades, nada: `identidad` no importa de `organizacion` ni al
revés, y eso lo impide una regla de ESLint, no un acuerdo (`docs/adr/adr-0022`).

Lo que esta funcionalidad necesita saber de la empresa activa lo lee de `shared/sesion/`, que es
donde vive. Bajar la sesión aquí dentro sería obligar a `organizacion` a importar de `identidad`
para saber con qué empresa opera — justo lo que la frontera prohíbe.

## `acceso` — la puerta

**Propósito.** Abrir sesión con correo y contraseña, y dejar a la persona donde iba.

### Rutas

| Ruta      | Exigencia | Título         |
| --------- | --------- | -------------- |
| `/acceso` | pública   | Iniciar sesión |

Es una de las dos rutas públicas del armazón (la otra es la de «no encontrada»), y su motivo está
escrito en `app/rutas.tsx`: sin ella no se puede llegar a ninguna de las demás.

### Claves de consulta

Ninguna. Aquí no se consulta nada: se envía un formulario. Lo que sí hace al entrar es **vaciar la
caché** (`clear()`), porque en esta pestaña puede quedar lo que consultó quien estuviera antes.

### Lo que no es evidente

- El mensaje de credenciales malas es **uno solo** para «ese correo no existe» y para «la contraseña
  no es esa». Distinguirlos le regalaría a quien prueba correos la lista de los que existen.
- El destino al que volver viaja en el estado de la navegación, que lo pone la guarda. Viene de
  fuera, así que se valida (`destinoSeguro`): `//otro-dominio.example` es una URL absoluta y
  aceptarla convertiría el acceso en un redirector abierto.
- La validación de este formulario es **comodidad**, no autoridad: replica las reglas del servidor
  para no ir y volver, y quien decide es la API.
- Los mensajes del esquema de Zod son **claves** del diccionario, no frases: el esquema es una
  constante de módulo y se evalúa antes de que exista ningún idioma. Se traducen al pintarlos.
