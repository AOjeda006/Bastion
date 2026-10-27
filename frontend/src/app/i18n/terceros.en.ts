import type { TextosDeTerceros } from './terceros.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a missing key —or one that nobody removed
 * from here after removing it from `terceros.es.ts`— is a compile error.
 */
export const terceros: TextosDeTerceros = {
  terceros: {
    cargando: 'the business partners',
    tabla: 'Business partners of the active company',
    identificador: 'Tax ID',
    razonSocial: 'Legal name',
    poblacion: 'Town',
    papel: 'Role',

    filtro: 'Search by legal name or trading name',
    filtrar: 'Search',

    paginaVacia: 'This page has no business partners. Go back to the previous one.',
    ningunoTodavia: 'No business partner has been registered at this company yet.',
    ningunoConEsteFiltro: 'No business partner matches “{{filtro}}”.',

    verificacion: {
      verificado: 'Checked',
      verificadoDetalle: 'The check character of the tax ID adds up.',
      sinVerificar: 'Not checked',
      sinVerificarDetalle:
        'This tax ID cannot be checked from its shape —it is foreign, or it does not follow ' +
        'the Spanish format—, so it may contain a typo. Review it before invoicing.',
      desconocida: 'Not checked',
      desconocidaDetalle:
        'This version of the screen does not know how to read the check status that arrived. ' +
        'Treat it as unchecked and tell whoever administers Bastion.',
    },

    papeles: {
      cliente: 'Customer',
      proveedor: 'Supplier',
      ambos: 'Customer and supplier',
    },

    enlaceAImportar: 'Import from a CSV',

    importacion: {
      explicacion:
        'Each row of the file registers a new business partner. Rows with any error are left out, ' +
        'and the report says on which line and why; the rest go in. A business partner that ' +
        'already exists is not modified: its row is rejected.',
      plantilla: 'The template',
      plantillaDetalle:
        'The first row must be this header, exactly as written and in this order. The tax ID, ' +
        'the legal name and the street, postcode, town and country of the address are required; ' +
        'the rest may be empty.',
      reglaFormato:
        'Save it from Excel with Spanish regional settings, as “CSV (Comma delimited)” or ' +
        '“CSV UTF-8”: columns are separated by semicolons.',
      reglaValores:
        'Yes/no values are written “sí”, “no”, “VERDADERO” or “FALSO”, and empty means no. Amounts ' +
        'use a decimal comma and up to four decimals, like 1.234,50.',
      reglaTope: 'At most 2 MB and 5000 rows per file. If you have more, split it into several.',
      fichero: 'CSV file',
      importar: 'Import',
      importando: 'Importing the file…',
      resultado: 'Import result',
      leidas: 'Rows read',
      importadas: 'Imported',
      rechazadas: 'Rejected',
      sinFilas: 'The file only has the header: there were no rows to import.',
      todasDentro: 'Every row went in.',
      rechazos: 'Why they did not go in',
      columna: 'Column',
      motivo: 'Reason',
      lineas: 'Lines',
      filaEntera: 'The whole row',
      lineasDeLaHoja:
        'Lines are the spreadsheet ones: the header is 1 and the first data row is 2. Fix those ' +
        'rows and import only them again, because the rest are already in.',
      motivos: {
        numeroDeCamposDistinto: 'The row does not have as many columns as the header.',
        comillasMalColocadas: 'There are quotes in the middle of the field.',
        obligatorio: 'It is empty and it is required.',
        demasiadoLargo: 'It is longer than the column allows.',
        formatoNoValido: 'It cannot be read as what the column expects: an amount, a yes or a no.',
        noValido:
          'It can be read, but it is not a valid value: for example, a tax ID with the wrong check letter.',
        niClienteNiProveedor: 'It must be a customer, a supplier or both.',
        yaExiste: 'There is already a business partner with that tax ID at the company.',
        repetidaEnElFichero: 'An earlier row of the same file already registers that tax ID.',
        desconocido:
          'This version of the screen cannot explain this reason. Tell whoever administers Bastion.',
      },
    },
  },
};
