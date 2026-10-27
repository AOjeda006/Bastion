import type { TextosDeCatalogo } from './catalogo.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a missing key —or one that nobody removed
 * from here after removing it from `catalogo.es.ts`— is a compile error.
 */
export const catalogo: TextosDeCatalogo = {
  articulos: {
    cargando: 'the items',
    tabla: 'Items of the active company',
    codigo: 'Code',
    descripcion: 'Description',
    tipo: 'Type',
    trazabilidad: 'Traceability',
    cambiar: 'Change',
    cambiarLaDe: 'Change the traceability of {{codigo}}',
    codigosDeBarras: 'Barcodes',
    verCodigosDeBarras: 'View',
    verCodigosDeBarrasDe: 'View the barcodes of {{codigo}}',

    filtro: 'Search by code or description',
    filtrar: 'Search',

    filtradaPor: 'Filtering by category “{{categoria}}”.',
    filtradaPorUnaCategoria: 'Filtering by a category.',
    quitarLaCategoria: 'Clear the category filter',

    paginaVacia: 'This page has no items. Go back to the previous one.',
    ningunoTodavia: 'No item has been registered at this company yet.',
    ningunoConEsteFiltro: 'No item matches “{{filtro}}”.',
    ningunoEnEstaCategoria:
      'This category has no items. The ones in the categories hanging from it do not show up ' +
      'here: look at them one by one.',

    tipos: {
      bien: 'Goods',
      servicio: 'Service',
      desconocido: 'Unrecognised',
      desconocidoDetalle:
        'This version of the screen does not know how to read the type that arrived. Tell ' +
        'whoever administers Bastion.',
    },

    trazabilidades: {
      ninguna: 'None',
      porLote: 'By batch',
      porNumeroSerie: 'By serial number',
      desconocida: 'Unrecognised',
      desconocidaDetalle:
        'This version of the screen does not know how to read the traceability that arrived. ' +
        'Tell whoever administers Bastion.',
    },

    cambioDeTrazabilidad: {
      cargando: 'the item',
      articulo: 'Item',
      actual: 'Saved traceability',
      leyenda: 'Traceability',
      pista: 'It can only be changed while the item has no stock movements.',
      elige: 'Choose one of the three.',
      guardar: 'Save',
      guardando: 'Saving…',
      guardada: 'Saved. The traceability is now “{{trazabilidad}}”.',
      recargar: 'Load the current version',
      volver: 'Back to the items',
    },

    niveles: {
      base: 'Base unit',
      caja: 'Case',
      palet: 'Pallet',
      desconocido: 'Unrecognised',
      desconocidoDetalle:
        'This version of the screen does not know how to read the level that arrived. Tell ' +
        'whoever administers Bastion.',
    },

    gestionDeCodigosDeBarras: {
      cargando: 'the barcodes',
      articulo: 'Item',
      tabla: 'Barcodes of {{codigo}}',
      gtin: 'GTIN',
      nivel: 'Level',
      unidades: 'Base units',
      acciones: 'Actions',
      ninguno: 'This item has no barcode yet.',
      quitar: 'Remove',
      quitarElDe: 'Remove GTIN {{gtin}}',
      confirmar: 'Remove GTIN {{gtin}} from this item?',
      siQuitar: 'Yes, remove it',
      cancelar: 'Cancel',
      quitando: 'Removing…',
      quitado: 'GTIN {{gtin}} removed.',
      alta: 'Add a barcode',
      campoGtin: 'GTIN',
      pistaGtin: 'The 8, 12, 13 or 14 digits under the bars.',
      leyendaNivel: 'Level',
      pistaNivel: 'Where it is printed: on the unit sold on its own, on the case or on the pallet.',
      campoUnidades: 'Base units it contains',
      pistaUnidades: 'Two or more. If they change, it is another GTIN.',
      agregar: 'Add',
      agregando: 'Adding…',
      agregado: 'GTIN {{gtin}} added.',
      volver: 'Back to the items',
    },
  },

  categorias: {
    cargando: 'the categories',
    tabla: 'Category tree of the active company',
    codigo: 'Code',
    nombre: 'Name',
    nivel: 'Level',
    articulos: 'Items',
    verSusArticulos: 'See the items in {{categoria}}',

    suelta: 'Out of place',
    sueltaDetalle:
      'The category it hangs from is not on this page, or the stored tree has a cycle. It is ' +
      'shown anyway: a category that exists and does not show up is one somebody registers a ' +
      'second time.',

    paginaVacia: 'This page has no categories. Go back to the previous one.',
    ningunaTodavia: 'No category has been registered at this company yet.',
  },

  tarifas: {
    cargando: 'the price lists',
    tabla: 'Price list periods of the active company',
    codigo: 'Code',
    nombre: 'Name',
    vigencia: 'In force',
    estado: 'Status',
    acciones: 'Actions',

    filtro: 'Search by code or name',
    filtrar: 'Search',

    desde: 'From {{desde}}',
    entre: '{{desde}} to {{hasta}}',

    verSusTramos: 'See the periods of {{codigo}}',

    tramosDe:
      'Showing the periods of price list “{{codigo}}”, newest first. A price list is several ' +
      'rows: one per period in force, and they never overlap.',
    quitarElCodigo: 'Clear the code filter',

    estados: {
      rige: 'In force today',
      futura: 'Not in force yet',
      caducada: 'No longer in force',
      rigeDetalle:
        'The last day in force is included: a period ending today still sets prices today, ' +
        'and stops tomorrow.',
    },

    paginaVacia: 'This page has no price list periods. Go back to the previous one.',
    ningunaTodavia: 'No price list has been registered at this company yet.',
    ningunaConEsteFiltro: 'No price list matches “{{filtro}}”.',
    ningunTramoConEseCodigo: 'No price list at this company has the code “{{codigo}}”.',
  },
};
