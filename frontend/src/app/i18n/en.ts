import type { DiccionarioDelArmazon } from './es.ts';

/**
 * English. The type does the checking: a missing key —or one that nobody removed from here after
 * removing it from `es.ts`— is a compile error, not a screen that shows Spanish to an English
 * reader the day somebody opens it.
 */
export const en: DiccionarioDelArmazon = {
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
    ajustes: 'Stock adjustments',
    nuevoAjuste: 'New adjustment',
    ajuste: 'Adjustment record',
    almacenes: 'Warehouses',
    articulos: 'Items',
    categorias: 'Categories',
    empresas: 'Companies',
    recuentos: 'Stock counts',
    recuento: 'Stock count record',
    saldos: 'Stock balance',
    tarifas: 'Price lists',
    terceros: 'Business partners',
    importarTerceros: 'Import business partners',
    trazabilidadDelArticulo: 'Item traceability',
    codigosDeBarrasDelArticulo: 'Item barcodes',
    transferencias: 'Stock transfers',
    nuevaTransferencia: 'New stock transfer',
    transferencia: 'Stock transfer record',
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
};
