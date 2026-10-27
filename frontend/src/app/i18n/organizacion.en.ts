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
};
