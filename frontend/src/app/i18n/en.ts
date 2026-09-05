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
    desconocido:
      'The operation could not be completed. If it happens again, quote this reference: {{traza}}.',

    // Ver `es.ts`: las claves son las del artefacto `docs/api/errores.json`, sin camelizar.
    tipos: {
      'almacen-duplicado': 'There is already a warehouse with that code at this company.',
      'almacen-no-encontrado': 'That warehouse no longer exists. Go back to the list and refresh.',
      'codigo-de-rol-ya-usado': 'There is already a role with that code. Pick another one.',
      'contrasena-actual-incorrecta': 'Your current password is not correct.',
      'conversion-um-duplicada': 'There is already a conversion between those two units.',
      'conversion-um-no-encontrada': 'That unit conversion no longer exists.',
      'correo-ya-registrado': 'There is already an account with that email address.',
      'credenciales-no-validas': 'The email address or the password is not correct.',
      'datos-no-validos': 'Some fields are not valid. Check the ones marked below.',
      'divisa-duplicada': 'There is already a currency with that code.',
      'divisa-no-encontrada': 'That currency no longer exists.',
      'ejercicio-cerrado': 'The financial year is closed and cannot be changed.',
      'ejercicio-con-series':
        'A financial year with document series cannot be deleted. Delete the series first.',
      'ejercicio-duplicado': 'There is already a financial year with those dates at this company.',
      'ejercicio-no-encontrado':
        'That financial year no longer exists. Go back to the list and refresh.',
      'empresa-activa-no-operativa':
        'The company you are working with is no longer available. Sign in again.',
      'empresa-ajena': 'That company is not yours, so you cannot work on it.',
      'empresa-destino-no-operativa':
        'The company you picked does not accept new people: it does not exist or it is blocked.',
      'empresa-no-encontrada': 'That company no longer exists. Go back to the list and refresh.',
      'empresa-no-pertenece': 'You do not belong to that company, so you cannot work with it.',
      'empresa-ya-registrada': 'There is already a company with that tax number.',
      'falta-if-match':
        'Saving requires saying which version you are writing over. Open the form again.',
      'idempotencia-clave-no-valida':
        'The application sent a retry key that is not valid. Please try again.',
      'idempotencia-cuerpo-distinto':
        'An operation was retried with the same send key but different content. Start again.',
      'idempotencia-no-admitida': 'This operation cannot be retried safely. Please try again.',
      'idempotencia-sin-empresa-activa':
        'Your session has no active company. Choose one and try again.',
      'if-match-no-valido': 'The version the form carried is not well formed. Open it again.',
      'impuesto-con-tramos-solapados': 'The validity ranges of that tax overlap. Check the dates.',
      'impuesto-no-encontrado': 'That tax no longer exists.',
      'orden-no-admitido': 'That field cannot be used for sorting.',
      'pertenencia-no-encontrada': 'That person does not belong to the company you named.',
      'rol-no-encontrado': 'That role no longer exists. Go back to the list and refresh.',
      'serie-cerrada': 'The document series is closed and cannot be changed.',
      'serie-duplicada': 'There is already a series with that code in that financial year.',
      'serie-no-encontrada': 'That series no longer exists. Go back to the list and refresh.',
      'serie-ya-numerada': 'The series has already numbered documents, so that cannot be changed.',
      'sesion-no-renovable': 'Your session could not be renewed. Sign in again.',
      'tipo-cambio-duplicado': 'There is already an exchange rate for that currency on that date.',
      'tipo-cambio-no-encontrado': 'That exchange rate no longer exists.',
      'ubicacion-duplicada': 'There is already a location with that code in that warehouse.',
      'ubicacion-no-encontrada': 'That location no longer exists. Go back to the list and refresh.',
      'unidad-medida-duplicada': 'There is already a unit of measure with that code.',
      'unidad-medida-no-encontrada': 'That unit of measure no longer exists.',
      'usuario-no-encontrado': 'That person no longer exists. Go back to the list and refresh.',
      'version-obsoleta':
        'Someone saved before you did. Open the form again so you do not overwrite their changes.',
    },
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
