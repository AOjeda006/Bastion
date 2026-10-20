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

  catalogo: {
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
        pistaNivel:
          'Where it is printed: on the unit sold on its own, on the case or on the pallet.',
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

  inventario: {
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
        desconocidoDetalle:
          'This version of the screen does not know that status. Reload the page.',
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
        desconocidoDetalle:
          'This version of the screen does not know that status. Reload the page.',
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
        sinSeries:
          'There is no active stock transfer series. Create one in the series master data.',
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
        desconocidoDetalle:
          'This version of the screen does not know that status. Reload the page.',
      },
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

  terceros: {
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
          formatoNoValido:
            'It cannot be read as what the column expects: an amount, a yes or a no.',
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
  },
};
