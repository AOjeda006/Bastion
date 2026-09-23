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
    articulos: 'Items',
    categorias: 'Categories',
    empresas: 'Companies',
    tarifas: 'Price lists',
    terceros: 'Business partners',
    importarTerceros: 'Import business partners',
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
      // Ver `es.ts`: las doce del ajuste, y las cuatro de un maestro bloqueado o retirado con sus
      // dos mitades.
      'ajuste-almacen-bloqueado':
        'That warehouse is blocked: its earlier movements are still read and valued, but it does ' +
        'not accept a new adjustment. Pick another warehouse or ask for it to be unblocked.',
      'ajuste-almacen-no-encontrado':
        'That warehouse does not exist. Pick one from the warehouse list.',
      'ajuste-articulo-no-encontrado': 'That item does not exist. Pick one from the item list.',
      'ajuste-articulo-no-se-almacena':
        'That item is a service: it has no stock to adjust. Remove that line or change the item.',
      'ajuste-motivo-no-valido':
        'Write why you are reversing the adjustment, in 300 characters or fewer. It is the only ' +
        'thing left to explain the correction two years from now.',
      'ajuste-no-encontrado': 'That adjustment no longer exists. Go back to the list and refresh.',
      'ajuste-no-esta-confirmado':
        'That adjustment is not confirmed, so there is nothing to reverse: a draft has not moved ' +
        'the ledger. Refresh the screen to see what state it is in.',
      'ajuste-no-esta-en-borrador':
        'That adjustment is no longer a draft: a confirmed one is not confirmed twice, because ' +
        'its ledger rows are already written and the ledger is never rewritten. Refresh the screen.',
      'ajuste-serie-cerrada':
        'That series is closed: it still resolves the documents it already numbered, but it hands ' +
        'out no further numbers. Choose another series.',
      'ajuste-serie-no-encontrada': 'That series does not exist. Pick one from the series list.',
      'ajuste-sin-lineas':
        'An adjustment needs at least one line: a document that moves nothing adjusts nothing.',
      'ajuste-ubicacion-bloqueada':
        'That location is blocked: whatever is already assigned to it is still read, but nothing ' +
        'new moves into that slot. Pick another location.',
      'ajuste-ubicacion-no-encontrada':
        'That location does not exist in that warehouse. Pick one of its own.',
      'ajuste-unidad-no-encontrada':
        'That unit of measure does not exist. Pick one from the unit list.',
      'ajuste-unidad-retirada':
        'That unit has been withdrawn: movements already written in it are still read, but a new ' +
        'one is not written with it. Pick another unit.',
      'almacen-duplicado': 'There is already a warehouse with that code at this company.',
      'almacen-no-encontrado': 'That warehouse no longer exists. Go back to the list and refresh.',
      'articulo-duplicado': 'There is already an item with that code at this company.',
      'articulo-impuesto-no-encontrado': 'That tax does not exist. Pick one from the tax list.',
      'articulo-impuesto-no-vigente':
        'That tax period no longer applies. Pick one that is currently in force.',
      'articulo-no-encontrado': 'That item no longer exists. Go back to the list and refresh.',
      'articulo-proveedor-duplicado': 'That supplier is already on this item’s list.',
      'articulo-proveedor-no-encontrado':
        'That supplier is no longer on this item’s list. Refresh the screen.',
      // Same wording decision as in Spanish: three situations, one answer, on purpose.
      'articulo-proveedor-tercero-no-valido':
        'That business partner cannot be added as a supplier of this item. Check in the ' +
        'business partners master that the record exists and is marked as a supplier.',
      'articulo-tipo-no-valido': 'An item can only be of type “Bien” or “Servicio”.',
      'articulo-unidad-no-encontrada':
        'That unit of measure does not exist. Pick one from the unit list.',
      'articulo-unidad-retirada':
        'That unit has been withdrawn: items already using it are still counted in it, but a ' +
        'new one cannot be registered with it. Pick another one.',
      'categoria-ciclo':
        'A category cannot hang from itself, nor from any of the ones hanging from it.',
      'categoria-demasiado-profunda':
        'The category tree does not allow any more levels below that place.',
      'categoria-duplicada': 'There is already a category with that code at this company.',
      'categoria-no-encontrada': 'That category no longer exists. Go back to the list and refresh.',
      'categoria-padre-no-encontrado':
        'The category you want to hang this one from does not exist.',
      'codigo-de-rol-ya-usado': 'There is already a role with that code. Pick another one.',
      'contrasena-actual-incorrecta': 'Your current password is not correct.',
      'conversion-um-duplicada': 'There is already a conversion between those two units.',
      'conversion-um-inversa-implausible':
        'The opposite direction is already declared and that factor is not its inverse.',
      'conversion-um-no-declarada': 'No conversion is declared between those two units. Add one.',
      'conversion-um-no-encontrada': 'That unit conversion no longer exists.',
      'correo-ya-registrado': 'There is already an account with that email address.',
      'credenciales-no-validas': 'The email address or the password is not correct.',
      'cuenta-bancaria-duplicada': 'This record already has that bank account.',
      'cuenta-bancaria-no-encontrada': 'This record has no such bank account.',
      'cuerpo-demasiado-grande':
        'What you are trying to send is too large. Split it into smaller sends.',
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
      'idempotencia-obligatoria':
        'This operation only runs with a retry key, because stopping halfway would leave a gap in ' +
        'the numbering. Please try again.',
      'idempotencia-sin-empresa-activa':
        'Your session has no active company. Choose one and try again.',
      'if-match-no-valido': 'The version the form carried is not well formed. Open it again.',
      'importacion-cabecera-no-valida':
        'The first row of the file is not the template header. Copy it exactly, in its order.',
      'importacion-codificacion-no-admitida':
        'The file is not saved as an Excel CSV. Save it as "CSV (Comma delimited)" or "CSV UTF-8".',
      'importacion-comillas-sin-cerrar':
        'The file has a quote that opens and never closes. Check it in the spreadsheet.',
      'importacion-demasiadas-filas':
        'The file has too many rows. Split it into files of 5000 rows at most.',
      'importacion-fin-de-linea-no-admitido':
        'The file separates rows in a way we do not accept. Open it and save it again as CSV.',
      'importacion-separador-no-admitido':
        'The file does not separate columns with semicolons. Save it from Excel with Spanish (Spain) regional settings.',
      'importacion-sin-permiso-de-alta':
        'Importing third parties means creating them, and you are not allowed to create third parties.',
      'importacion-sin-permiso-de-limite':
        'Some rows carry a credit limit and you are not allowed to set it. Leave those columns empty.',
      'impuesto-con-tramos-solapados': 'The validity ranges of that tax overlap. Check the dates.',
      'impuesto-no-encontrado': 'That tax no longer exists.',
      'orden-no-admitido': 'That field cannot be used for sorting.',
      'permisos-de-rol-del-sistema':
        'The permissions of the system role are set by every update. You can rename it; to grant fewer permissions, create a role of your own.',
      'pertenencia-no-encontrada': 'That person does not belong to the company you named.',
      'rol-no-encontrado': 'That role no longer exists. Go back to the list and refresh.',
      'serie-cerrada': 'The document series is closed and cannot be changed.',
      'serie-duplicada': 'There is already a series with that code in that financial year.',
      'serie-no-encontrada': 'That series no longer exists. Go back to the list and refresh.',
      'serie-no-numera':
        'That series no longer hands out numbers: it was closed or deleted while your draft was ' +
        'open. Open the adjustment with another series.',
      'serie-ya-numerada': 'The series has already numbered documents, so that cannot be changed.',
      'sesion-no-renovable': 'Your session could not be renewed. Sign in again.',
      'tarifa-divisa-no-encontrada':
        'That currency does not exist. Pick one from the currency list.',
      'tarifa-divisa-retirada':
        'That currency has been withdrawn: price lists already using it still work, but a new ' +
        'one cannot be opened with it. Pick another one.',
      'tarifa-linea-articulo-o-categoria':
        'A price list line sets the price of an item or of a category, and you have to pick one ' +
        'of the two: not both, not neither.',
      'tarifa-linea-no-encontrada':
        'That price list line no longer exists. Go back to the list and refresh.',
      'tarifa-linea-precio-o-descuento':
        'A price list line carries a price or a discount, and you have to set one of the two: ' +
        'not both, not neither.',
      'tarifa-linea-primer-tramo-sin-cero':
        'The first quantity band has to start at zero. Starting higher would leave the ' +
        'quantities below it with no price.',
      'tarifa-linea-tramo-duplicado':
        'There is already a band starting at that quantity for that item or that category.',
      'tarifa-no-encontrada': 'That price list no longer exists. Go back to the list and refresh.',
      'tarifa-no-vigente':
        'That price list exists, but none of its periods covers the date asked for. Open the ' +
        'missing period or ask for another date.',
      'tarifa-sin-linea-aplicable':
        'That price list says nothing about that item at that quantity, neither its own line nor ' +
        'any of its categories. Add the missing line: there is no price to apply here.',
      'tarifa-vigencia-al-reves': 'The period ends before it starts. Check both dates.',
      'tarifa-vigencias-solapadas':
        'There is already another period of that price list covering some of those days. The ' +
        'periods of a price list cannot overlap.',
      'tercero-duplicado': 'This company already has a business partner with that tax identifier.',
      'tercero-no-encontrado':
        'That business partner no longer exists. Go back to the list and refresh.',
      'tercero-tarifa-no-encontrada':
        'That price list does not exist. Choose one from the price lists master.',
      'tercero-tarifa-no-vigente':
        'That price list is no longer in force: it still applies to whatever was issued while ' +
        'it ran, but it cannot be assigned today. Choose one that is in force.',
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

  catalogo: {
    articulos: {
      cargando: 'the items',
      tabla: 'Items of the active company',
      codigo: 'Code',
      descripcion: 'Description',
      tipo: 'Type',

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
