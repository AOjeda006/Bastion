import type { TextosDeOrganizacion } from './organizacion.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a missing key —or one that nobody removed
 * from here after removing it from `organizacion.es.ts`— is a compile error.
 */
export const organizacion: TextosDeOrganizacion = {
  almacenes: {
    cargando: 'the warehouses',
    tabla: 'Warehouses of the active company',
    codigo: 'Code',
    nombre: 'Name',
    tipo: 'Type',
    poblacion: 'Town',
    zona: 'Time zone',
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
    zona: 'Time zone',
    ningunaVisible: 'There is no company you can see.',
  },

  zonas: {
    nombres: {
      peninsula: 'Mainland Spain and Balearic Islands',
      canarias: 'Canary Islands',
      ceuta: 'Ceuta and Melilla',
    },
    deLaEmpresa: "the company's",
    propia: 'its own',
    cambiar: 'Change',
    cambiarLaDe: 'Change the time zone of {{nombre}}',
    titulo: 'Time zone of {{nombre}}',
    cargando: 'the time zone',
    leyenda: 'Time zone',
    laDeLaEmpresa: "The company's",
    avisoLoFechado:
      'What is already dated does not change: every document and every movement keeps the day it ' +
      'was saved with.',
    avisoLaHora:
      'For up to an hour after the change, an operation dated today may fall before the latest one ' +
      'at its warehouse. It is then not accepted until that hour has passed.',
    avisoLosAlmacenes:
      "It also changes the day of this company's warehouses that have no time zone of their own.",
    guardar: 'Save the time zone',
    guardando: 'Saving…',
    cancelar: 'Cancel',
    recargar: 'Load the current version',
    cambiada: 'The time zone of {{nombre}} is now {{zona}}.',
    heredada: "{{nombre}} now takes its company's time zone: {{zona}}.",
  },
};
