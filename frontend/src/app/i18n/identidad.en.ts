import type { TextosDeIdentidad } from './identidad.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a missing key —or one that nobody removed
 * from here after removing it from `identidad.es.ts`— is a compile error.
 */
export const identidad: TextosDeIdentidad = {
  acceso: {
    correo: 'Email',
    contrasena: 'Password',
    entrar: 'Sign in',
    entrando: 'Signing in…',
    credenciales: 'The email or the password is not correct.',
    sinRed: 'The server could not be reached. Try again.',
    escribeTuCorreo: 'Enter your email.',
    correoDemasiadoLargo: 'The email cannot be longer than 254 characters.',
    correoConFormatoMalo: 'That does not look like an email address.',
    escribeTuContrasena: 'Enter your password.',
    contrasenaDemasiadoLarga: 'The password cannot be longer than 128 characters.',
  },
};
