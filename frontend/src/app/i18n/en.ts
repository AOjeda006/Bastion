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
      'articulo-duplicado': 'There is already an item with that code at this company.',
      'articulo-impuesto-no-encontrado': 'That tax does not exist. Pick one from the tax list.',
      'articulo-impuesto-no-vigente':
        'That tax period no longer applies. Pick one that is currently in force.',
      'articulo-no-encontrado': 'That item no longer exists. Go back to the list and refresh.',
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
    },
  },
};
