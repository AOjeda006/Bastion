import type { TextosDeInventario } from './inventario.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a missing key —or one that nobody removed
 * from here after removing it from `inventario.es.ts`— is a compile error.
 */
export const inventario: TextosDeInventario = {
  lineas: {
    sinElegir: 'Not chosen',
    pistaDeLaRejilla: 'Tab moves between cells, and Enter in the last cell adds a line.',
    numero: 'No.',
    ubicacion: 'Location',
    articulo: 'Item',
    cantidad: 'Quantity',
    unidad: 'Unit',
    lote: 'Lot',
    numeroDeSerie: 'Serial number',
    celda: '{{campo}} of line {{numero}}',
    antesElAlmacen: 'Choose the warehouse first',
    buscando: 'Searching…',
    ningunArticulo: 'No item matches.',
    anadir: 'Add a line',
    quitar: 'Remove line {{numero}}',
  },

  ajustes: {
    cargando: 'the stock adjustments',
    tabla: 'Stock adjustments of the active company',
    ajuste: 'Adjustment',
    almacen: 'Warehouse',
    fecha: 'Posting date',
    estado: 'Status',
    motivo: 'Reason',
    numerado: 'Adjustment {{numero}}',
    sinNumero: 'Draft for {{almacen}} on {{dia}}',

    todos: 'All',
    buscar: 'Search the reason',
    orden: 'Sort by',
    ordenes: {
      masRecientes: 'Date, latest first',
      masAntiguos: 'Date, earliest first',
      numeroDescendente: 'Number, highest first',
      numeroAscendente: 'Number, lowest first',
    },
    filtrar: 'Filter',
    nuevo: 'New adjustment',
    paginaVacia: 'This page has no adjustments. Go back to the previous one.',
    ningunoTodavia: 'This company has no stock adjustments yet.',
    ningunoConEsteFiltro: 'No adjustment matches the filter.',
    volver: 'Back to stock adjustments',

    estados: {
      borrador: 'Draft',
      confirmado: 'Confirmed',
      anulado: 'Voided',
      desconocido: 'Unrecognised',
      desconocidoDetalle: 'This version of the screen does not know that status. Reload the page.',
    },

    alta: {
      pista:
        'Each line carries a signed quantity: positive goes in, negative goes out. It gets its ' +
        'number when confirmed.',
      serie: 'Series',
      sinSeries: 'There is no active adjustment series. Create one in the series master.',
      tabla: 'Adjustment lines',
      coste: 'Cost per base unit',
      eligeElAlmacen: 'Choose the warehouse of the adjustment.',
      eligeLaSerie: 'Choose a series.',
      escribeLaFecha: 'Enter the day of the adjustment.',
      eligeLaUbicacion: 'Choose the location.',
      eligeElArticulo: 'Choose an item from the list.',
      eligeLaUnidad: 'Choose the unit.',
      escribeLaCantidad: 'Enter the quantity: negative, if it goes out.',
      costeMalEscrito: 'The cost is an unsigned figure with at most four decimals.',
      topeDeLineas: 'An adjustment has at most 200 lines.',
      abrir: 'Open the adjustment',
      abriendo: 'Opening…',
      sinMaestros:
        'Opening an adjustment requires permission to view warehouses, series, locations, units ' +
        'and items.',
    },

    ficha: {
      cargando: 'the adjustment',
      anulaA: 'Offsets',
      verOriginal: 'View the original adjustment',
      recuento: 'Comes from',
      verRecuento: 'View its stock count',
      deUnRecuento: 'A stock count',
      lineas: 'Lines',
      tabla: 'Adjustment lines',
      numero: 'No.',
      ubicacion: 'Location',
      articulo: 'Item',
      cantidad: 'Quantity',
      factor: 'Conversion factor',
      enUnidadBase: 'In base unit',
      coste: 'Unit cost',
      loteOSerie: 'Lot or serial',
      valor: 'Value',
      nota:
        'The server set the factor and the base-unit quantity when it was opened, with the ' +
        'conversion of that day.',
      notaDelBorrador: 'The value is calculated when it is confirmed.',

      confirmar: 'Confirm the adjustment',
      preguntaConfirmar: 'Confirming it gives it its number and moves the stock.',
      siConfirmar: 'Yes, confirm',
      confirmando: 'Confirming…',
      confirmadoAviso: 'Adjustment confirmed with number {{numero}}.',
      anular: 'Void the adjustment',
      preguntaAnular:
        'Voiding it creates a reverse adjustment, already confirmed, that offsets it.',
      siAnular: 'Yes, void',
      anulando: 'Voiding…',
      anuladoAviso: 'Adjustment voided. Adjustment {{numero}} offsets it.',
      verInverso: 'View the reverse',
      descartar: 'Discard the draft',
      preguntaDescartar:
        'Discarding it removes it from the list. It has moved nothing, and it cannot be undone.',
      siDescartar: 'Yes, discard',
      descartando: 'Discarding…',
      descartadoAviso: 'Draft discarded.',
      cancelar: 'Cancel',
    },
  },

  recuentos: {
    cargando: 'the stock counts',
    tabla: 'Stock counts of the active company',
    recuento: 'Stock count',
    almacen: 'Warehouse',
    abierto: 'Opened on',
    confirmado: 'Confirmed on',
    estado: 'Status',
    motivo: 'Reason',

    numerado: 'Stock count {{numero}}',
    sinNumero: 'Stock count of {{almacen}} on {{dia}}',

    todos: 'All',
    filtrar: 'Filter',
    paginaVacia: 'This page has no stock counts. Go back to the previous one.',
    ningunoTodavia: 'There are no stock counts at this company yet.',
    ningunoConEsteFiltro: 'No stock count matches the filter.',

    estados: {
      enCurso: 'In progress',
      confirmado: 'Confirmed',
      anulado: 'Reversed',
      descartado: 'Discarded',
      desconocido: 'Unrecognised',
      desconocidoDetalle: 'This version of the screen does not know that status. Reload the page.',
    },

    apertura: {
      titulo: 'Open a stock count',
      pista:
        'The whole warehouse is counted, in the base unit of each item. It gets its number when ' +
        'it is confirmed.',
      serie: 'Stock count series',
      serieDelAjuste: 'Adjustment series',
      sinElegir: 'Not chosen',
      sinSeries: 'There is no active series of this kind. Create one in the series master data.',
      eligeElAlmacen: 'Choose the warehouse to count.',
      eligeLaSerie: 'Choose a series.',
      abrir: 'Open the stock count',
      abriendo: 'Opening…',
      sinMaestros: 'To open a stock count you need to be able to see warehouses and series.',
    },

    ficha: {
      cargando: 'the stock count',
      volver: 'Back to stock counts',
      lineas: 'Lines',
      cuentas: '{{lineas}} in total, {{sinContar}} not counted',
      cuentasEnCurso:
        '{{lineas}} in total, {{sinContar}} not counted, {{cambiadas}} with a changed book ' +
        'quantity and {{transito}} with goods in transit',
      motivoDelDescarte: 'Reason for discarding',
      motivoDeLaAnulacion: 'Reason for reversing',
      ajuste: 'Adjustment',
      conAjuste: 'It posted the difference',
      sinAjuste: 'None: the count matched',
      notaEnCurso:
        'Each line is counted in the base unit of its item. The book quantity is the current ' +
        'one, and the difference is what the adjustment would post on confirming.',
      notaCerrado:
        'The book quantity is the one frozen on confirming, and the difference is what the ' +
        'adjustment posted.',
      notaDescartado: 'It was discarded without moving anything.',

      vistas: 'Which lines',
      todas: 'All lines',
      sinContar: 'Not counted',
      teoricoCambiado: 'With a changed book quantity',

      tabla: 'Stock count lines',
      numero: 'No.',
      articulo: 'Item',
      ubicacion: 'Location',
      loteOSerie: 'Batch or serial',
      unidad: 'Unit',
      teorico: 'Book quantity',
      enTransito: 'In transit',
      contado: 'Counted',
      diferencia: 'Difference',
      acciones: 'Actions',
      alContar: 'When counted it was {{antes}} ({{cambio}})',
      ninguna: 'This stock count has no lines: the warehouse had no stock when it was opened.',
      ningunaEnLaVista: 'No lines in this view.',
      paginaVacia: 'This page has no lines. Go back to the previous one.',

      contar: 'Count',
      corregir: 'Correct',
      abriendo: 'Opening…',
      contarLa: 'Count line {{numero}}',
      corregirLa: 'Correct the count of line {{numero}}',
      campoContado: 'Counted on line {{numero}}, in {{unidad}}',
      escribeLoContado: 'Enter the count; if there is none, a 0.',
      guardar: 'Save',
      guardando: 'Saving…',
      cancelar: 'Cancel',
      contada: 'Line {{numero}} counted.',
      contadaPorOtraPersona:
        'Someone else counted line {{numero}} while you had it open: {{contado}} {{unidad}}. ' +
        'What you entered has not been saved.',

      confirmar: 'Confirm the stock count',
      preguntaConfirmar:
        'Confirming freezes the book quantity, posts the adjustment for the difference and ' +
        'gives it its number.',
      siConfirmar: 'Yes, confirm',
      confirmando: 'Confirming…',
      confirmadoAviso: 'Stock count confirmed with number {{numero}}.',
      anular: 'Reverse the stock count',
      preguntaAnular: 'Reversing it undoes its adjustment with an inverse one, dated today.',
      siAnular: 'Yes, reverse it',
      anulando: 'Reversing…',
      anuladoAviso: 'Stock count reversed.',
      descartar: 'Discard the stock count',
      preguntaDescartar:
        'Discarding it closes it without moving anything, and frees the warehouse for another ' +
        'stock count.',
      siDescartar: 'Yes, discard',
      descartando: 'Discarding…',
      descartadoAviso: 'Stock count discarded.',
    },
  },

  saldos: {
    cargando: 'the stock balance',
    tabla: 'Stock balance at the end of {{dia}}, by item and warehouse',
    dia: 'At the end of',
    articulo: 'Item',
    almacen: 'Warehouse',
    cantidad: 'Quantity',
    valor: 'Value',
    enTransito: 'In transit',
    valorEnTransito: 'Value in transit',
    transitoAparte:
      'Stock in transit left another warehouse for this one and had not arrived by the end of ' +
      'the day. It is not added to the quantity or to the value.',
    todos: 'All',
    consultar: 'Show',
    paginaVacia: 'This page has no balances. Go back to the previous one.',
    ningunoTodavia: 'This company has not moved any stock yet.',
    ningunoConEsteFiltro: 'No balance matches the filter.',
  },

  transferencias: {
    cargando: 'the stock transfers',
    tabla: 'Stock transfers of the active company',
    transferencia: 'Transfer',
    origen: 'From',
    destino: 'To',
    envio: 'Shipping date',
    recepcion: 'Receipt date',
    estado: 'Status',
    motivo: 'Reason',
    ubicacionOrigen: 'Origin location',
    ubicacionDestino: 'Destination location',
    numerada: 'Transfer {{numero}}',
    sinNumero: 'Draft from {{origen}} to {{destino}} on {{dia}}',

    todas: 'All',
    todos: 'All',
    orden: 'Sort by',
    ordenes: {
      envioReciente: 'Shipping date, latest first',
      envioAntiguo: 'Shipping date, earliest first',
      recepcionReciente: 'Receipt date, latest first',
      recepcionAntigua: 'Receipt date, earliest first',
      numeroDescendente: 'Number, highest first',
      numeroAscendente: 'Number, lowest first',
    },
    filtrar: 'Filter',
    paginaVacia: 'This page has no transfers. Go back to the previous one.',
    ningunaTodavia: 'This company has no stock transfers yet.',
    ningunaConEsteFiltro: 'No transfer matches the filter.',
    nueva: 'New stock transfer',
    volver: 'Back to stock transfers',

    avisos: {
      eligeElOrigen: 'Choose the warehouse it leaves from.',
      eligeElDestino: 'Choose the warehouse it goes to.',
      eligeLaSerie: 'Choose a series.',
      escribeElEnvio: 'Enter the day it leaves.',
      escribeLaRecepcion: 'Enter the day it arrives.',
      eligeLaUbicacion: 'Choose the location.',
      eligeElArticulo: 'Choose an item from the list.',
      eligeLaUnidad: 'Choose the unit.',
      escribeLaCantidad: 'Enter the quantity.',
      topeDeLineas: 'A stock transfer has at most 200 lines.',
    },

    alta: {
      pista:
        'Each line leaves a location at the origin and enters one at the destination. It gets ' +
        'its number when it is shipped.',
      serie: 'Series',
      sinSeries: 'There is no active stock transfer series. Create one in the series master data.',
      tabla: 'Lines of the transfer',
      abrir: 'Open the transfer',
      abriendo: 'Opening…',
      sinMaestros:
        'Opening a stock transfer requires being able to see the warehouses, the series, the ' +
        'locations, the units and the items.',
    },

    ficha: {
      cargando: 'the stock transfer',
      anulaA: 'Offsets',
      verOriginal: 'View the original transfer',
      lineas: 'Lines',
      tabla: 'Lines of the transfer',
      numero: 'No.',
      articulo: 'Item',
      cantidad: 'Quantity',
      factor: 'Conversion factor',
      enUnidadBase: 'In base unit',
      loteOSerie: 'Batch or serial',
      valor: 'Value',
      nota:
        'The server set the factor and the base-unit quantity when the transfer was opened, ' +
        'with the conversion of that day.',
      notaDelBorrador: 'The value is calculated when it is shipped.',

      enviar: 'Ship the transfer',
      preguntaEnviar: 'Once shipped it gets its number and the goods leave the origin.',
      siEnviar: 'Yes, ship',
      enviando: 'Shipping…',
      enviadaAviso: 'Transfer shipped with number {{numero}}.',
      recibir: 'Receive the transfer',
      preguntaRecibir: 'Once received, the goods enter the destination on the day you give.',
      siRecibir: 'Yes, receive',
      recibiendo: 'Receiving…',
      recibidaAviso: 'Transfer received on {{dia}}.',
      anular: 'Void the transfer',
      preguntaAnular: 'Voiding it creates a reverse transfer, already received, that offsets it.',
      siAnular: 'Yes, void',
      anulando: 'Voiding…',
      anuladaAviso: 'Transfer voided. Transfer {{numero}} offsets it.',
      verInverso: 'View the reverse',
      descartar: 'Discard the draft',
      preguntaDescartar:
        'Discarding it removes it from the list. It has not moved anything, and it cannot be undone.',
      siDescartar: 'Yes, discard',
      descartando: 'Discarding…',
      descartadaAviso: 'Draft discarded.',
      cancelar: 'Cancel',
    },

    estados: {
      borrador: 'Draft',
      enviada: 'Shipped',
      recibida: 'Received',
      anulada: 'Voided',
      desconocido: 'Unrecognised',
      desconocidoDetalle: 'This version of the screen does not know that status. Reload the page.',
    },
  },
};
