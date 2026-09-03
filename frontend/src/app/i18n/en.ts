import type { Diccionario } from './es.ts';

/**
 * English. The type does the checking: a missing key —or one that nobody removed from here after
 * removing it from `es.ts`— is a compile error, not a screen that shows Spanish to an English
 * reader the day somebody opens it.
 */
export const en: Diccionario = {
  comun: {
    tituloDeDocumento: '{{titulo}} · Bastion',
    saltarAlContenido: 'Skip to content',
    estadoDeLaNavegacion: 'Navigation status',
    paginaCargada: 'The {{titulo}} page has loaded.',
    navegacionPrincipal: 'Main',
    salir: 'Sign out',
    idioma: 'Language',
    cargando: 'Loading {{que}}…',
    laPantalla: 'the screen',
    volverAIntentarlo: 'Try again',
    pantallaRota:
      'This screen could not be shown. You can keep using the rest of Bastion from the menu; if ' +
      'it happens again, let us know what you were doing.',
  },

  paginacion: {
    nombre: 'Pagination',
    anterior: 'Previous',
    siguiente: 'Next',
    sinResultados: 'No results',
    rango: '{{primero}}–{{ultimo}} of {{total}}',
  },

  rutas: {
    acceso: 'Sign in',
    inicio: 'Home',
    almacenes: 'Warehouses',
    empresas: 'Companies',
    noEncontrada: 'Page not found',
  },

  sesion: {
    empresa: 'Company',
    empresaEtiqueta: 'Company: ',
    sinPermiso:
      'Your user does not have permission to see this screen at the company you are working ' +
      'with. If you think you should, ask whoever administers Bastion.',
    cambioDeEmpresa: 'The company could not be changed. Try again.',
  },

  errores: {
    sinPermiso: 'You do not have permission to view this at the company you are working with.',
    sesionCaducada: 'Your session has expired. Sign in again.',
    servidor: 'The server could not respond. Try again.',
    carga: 'The data could not be loaded. Try again.',
  },

  inicio: {
    saludo: 'Hello, <strong>{{nombre}}</strong>.',
    operandoCon: 'You are working with <strong>{{empresa}}</strong>.',
    operandoConYPuedesCambiar:
      'You are working with <strong>{{empresa}}</strong>. You can switch company from the ' +
      'selector in the header.',
    empresaNoVisible: 'a company that is no longer visible',
    armazon:
      'This is the phase 0 shell: sign-in, company selector, protected routes and two read-only ' +
      'listings. The business modules arrive in the following phases.',
    noEncontrada: 'This address does not match any Bastion screen.',
    irAlAcceso: 'Go to the sign-in screen',
    volverAlInicio: 'Back to home',
  },

  identidad: {
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
  },

  organizacion: {
    almacenes: {
      cargando: 'the warehouses',
      tabla: 'Warehouses of the active company',
      codigo: 'Code',
      nombre: 'Name',
      tipo: 'Type',
      poblacion: 'Town',
      paginaVacia: 'This page has no warehouses. Go back to the previous one.',
      ningunoTodavia: 'No warehouse has been registered at this company yet.',
    },

    empresas: {
      cargando: 'the companies',
      tabla: 'Registered companies',
      nif: 'Tax ID',
      razonSocial: 'Legal name',
      poblacion: 'Town',
      divisa: 'Currency',
      ningunaVisible: 'There is no company you can see.',
    },
  },
};
