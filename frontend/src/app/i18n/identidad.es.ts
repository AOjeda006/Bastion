/**
 * Los textos de Identidad en castellano, y **la forma** que el inglés tiene que cumplir.
 *
 * **Viajan con las rutas de Identidad, y no en el diccionario del arranque** (ADR-0071).
 * Hasta el 3.1 eran el espacio de nombres `identidad` de `es.ts`. Ahora los trae el enrutador
 * junto con la pantalla, en el idioma activo, y entran en el mismo espacio de nombres:
 * `t('identidad.…')` sigue siendo la misma clave. Por eso solo los lee el código de su
 * funcionalidad: el armazón, o una pantalla de otro módulo, los encontraría sin cargar, y
 * `LosTextosViajanConSusRutas` lo comprueba.
 *
 * Se importan por su TIPO desde donde haga falta, y por su VALOR solo desde `diccionarios.ts` y
 * los tests, como el diccionario: lo vigila el *linter*.
 */
export const identidad = {
  acceso: {
    correo: 'Correo',
    contrasena: 'Contraseña',
    entrar: 'Entrar',
    entrando: 'Entrando…',
    credenciales: 'El correo o la contraseña no son correctos.',
    sinRed: 'No se ha podido contactar con el servidor. Inténtalo de nuevo.',
    escribeTuCorreo: 'Escribe tu correo.',
    correoDemasiadoLargo: 'El correo no puede pasar de 254 caracteres.',
    correoConFormatoMalo: 'Eso no parece un correo electrónico.',
    escribeTuContrasena: 'Escribe tu contraseña.',
    contrasenaDemasiadoLarga: 'La contraseña no puede pasar de 128 caracteres.',
  },
};

export type TextosDeIdentidad = typeof identidad;
