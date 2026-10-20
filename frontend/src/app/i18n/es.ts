/**
 * El diccionario en castellano, y **la forma** que el resto tienen que cumplir.
 *
 * No lleva `as const` a propósito: sin él, `typeof es` tiene las claves literales —que es lo que
 * hace que `t('comun.salir')` se compruebe al compilar— y los valores como `string` —que es lo que
 * deja que `en.ts` traiga otros textos con las mismas claves—. Con `as const` los valores serían
 * literales y el inglés no podría cumplir el tipo sin repetir el castellano.
 *
 * Es un módulo de TypeScript y no un `.json` por lo mismo: un JSON no se comprueba. Una clave que
 * falte en un idioma tiene que ser un error de compilación, no un texto que sale en el idioma
 * equivocado el día que alguien abre esa pantalla.
 *
 * **Los espacios de nombres de primer nivel son una partición, y está comprobada.** O son del
 * armazón —`comun`, `paginacion`, `rutas`, `sesion`, `errores`, `inicio`: lo que no es de ningún
 * módulo— o son una funcionalidad de `src/features/`, y entonces el nombre es EL DE LA CARPETA y
 * dentro hay un espacio por recurso. `ElBarridoDeLasFronteras` compara las dos listas enteras
 * contra el disco, en los dos sentidos: renombrar una carpeta sin renombrar su espacio de nombres
 * deja un diccionario que describe una estructura que ya no existe, y el compilador no dice nada
 * porque una clave es una cadena.
 */
export const es = {
  comun: {
    tituloDeDocumento: '{{titulo}} · Bastion',
    saltarAlContenido: 'Saltar al contenido',
    estadoDeLaNavegacion: 'Estado de la navegación',
    paginaCargada: 'La página {{titulo}} se ha cargado.',
    navegacionPrincipal: 'Principal',
    salir: 'Salir',
    idioma: 'Idioma',
    cargando: 'Cargando {{que}}…',
    laPantalla: 'la pantalla',
    volverAIntentarlo: 'Volver a intentarlo',
    pantallaRota:
      'Esta pantalla no se ha podido mostrar. Puedes seguir usando el resto de Bastion desde el ' +
      'menú; si vuelve a pasar, avisa indicando qué estabas haciendo.',
  },

  paginacion: {
    nombre: 'Paginación',
    anterior: 'Anterior',
    siguiente: 'Siguiente',
    sinResultados: 'Sin resultados',
    rango: '{{primero}}–{{ultimo}} de {{total}}',
  },

  rutas: {
    acceso: 'Iniciar sesión',
    inicio: 'Inicio',
    ajustes: 'Ajustes de inventario',
    nuevoAjuste: 'Nuevo ajuste',
    ajuste: 'Ficha del ajuste',
    almacenes: 'Almacenes',
    articulos: 'Artículos',
    categorias: 'Categorías',
    empresas: 'Empresas',
    recuentos: 'Recuentos',
    recuento: 'Ficha del recuento',
    saldos: 'Saldo de stock',
    tarifas: 'Tarifas',
    terceros: 'Terceros',
    importarTerceros: 'Importar terceros',
    trazabilidadDelArticulo: 'Trazabilidad del artículo',
    codigosDeBarrasDelArticulo: 'Códigos de barras del artículo',
    transferencias: 'Transferencias',
    noEncontrada: 'Página no encontrada',
  },

  sesion: {
    empresa: 'Empresa',
    empresaEtiqueta: 'Empresa: ',
    sinPermiso:
      'Tu usuario no tiene permiso para ver esta pantalla en la empresa con la que estás ' +
      'operando. Si crees que debería tenerlo, pídeselo a quien administre Bastion.',
    cambioDeEmpresa: 'No se ha podido cambiar de empresa. Vuelve a intentarlo.',
  },

  errores: {
    sinPermiso: 'No tienes permiso para consultar esto con la empresa con la que estás operando.',
    sesionCaducada: 'Tu sesión ha caducado. Vuelve a entrar.',
    servidor: 'El servidor no ha podido responder. Inténtalo de nuevo.',
    carga: 'No se han podido cargar los datos. Inténtalo de nuevo.',
    // El camino (c) del ADR-0030: la API ha contestado con un `type` que este frontal no
    // conoce, o sea que las dos partes se han desincronizado. La referencia es el `traceId`
    // del ProblemDetails, el mismo que Serilog escribe: es lo único que permite ir al
    // registro y ver qué pasó de verdad.
    desconocido:
      'No se ha podido completar la operación. Si vuelve a pasar, indica esta referencia: {{traza}}.',
  },

  // Las dos pantallas del armazón (`app/paginas/`). No son de ningún módulo, así que su espacio de
  // nombres tampoco lo es: el porqué está en el README de esa carpeta.
  inicio: {
    saludo: 'Hola, <strong>{{nombre}}</strong>.',
    operandoCon: 'Estás operando con <strong>{{empresa}}</strong>.',
    operandoConYPuedesCambiar:
      'Estás operando con <strong>{{empresa}}</strong>. Puedes cambiar de empresa en el selector ' +
      'de la cabecera.',
    empresaNoVisible: 'una empresa que ya no está visible',
    armazon:
      'Esto es el armazón de la fase 0: acceso, selector de empresa, rutas protegidas y dos ' +
      'listados de solo lectura. Los módulos de negocio llegan en las fases siguientes.',
    noEncontrada: 'Esta dirección no corresponde a ninguna pantalla de Bastion.',
    irAlAcceso: 'Ir a la pantalla de acceso',
    volverAlInicio: 'Volver al inicio',
  },

  catalogo: {
    articulos: {
      cargando: 'los artículos',
      tabla: 'Artículos de la empresa activa',
      codigo: 'Código',
      descripcion: 'Descripción',
      tipo: 'Tipo',
      trazabilidad: 'Trazabilidad',
      cambiar: 'Cambiar',
      // El nombre accesible del enlace lleva el código: en una tabla, veinte enlaces que se
      // llaman «Cambiar» no dicen cuál es cuál a quien los recorre con el lector de pantalla.
      cambiarLaDe: 'Cambiar la trazabilidad de {{codigo}}',
      codigosDeBarras: 'Códigos de barras',
      verCodigosDeBarras: 'Ver',
      // Por lo mismo que «Cambiar»: veinte enlaces que se llaman «Ver» no dicen de qué artículo.
      verCodigosDeBarrasDe: 'Ver los códigos de barras de {{codigo}}',

      // El filtro dice por dónde busca. Quien lee «Buscar» a secas prueba con la unidad o con el
      // impuesto —que ni se enseñan ni se filtran— y concluye que el artículo no está.
      filtro: 'Buscar por código o descripción',
      filtrar: 'Buscar',

      filtradaPor: 'Filtrando por la categoría «{{categoria}}».',
      filtradaPorUnaCategoria: 'Filtrando por una categoría.',
      quitarLaCategoria: 'Quitar el filtro de categoría',

      paginaVacia: 'Esta página no tiene artículos. Vuelve a la anterior.',
      ningunoTodavia: 'Todavía no hay ningún artículo dado de alta en esta empresa.',
      ningunoConEsteFiltro: 'Ningún artículo coincide con «{{filtro}}».',

      // Dice la consecuencia de acotar por la categoría dicha y no por su subárbol: sin esa frase,
      // una rama con hijos llenos de artículos sale vacía y parece un fallo.
      ningunoEnEstaCategoria:
        'Esta categoría no tiene ningún artículo. Los de las categorías que cuelgan de ella no ' +
        'salen aquí: míralas una a una.',

      tipos: {
        bien: 'Bien',
        servicio: 'Servicio',
        desconocido: 'Sin reconocer',
        desconocidoDetalle:
          'Esta versión de la pantalla no sabe interpretar el tipo que ha llegado. Avisa a quien ' +
          'administre Bastion.',
      },

      trazabilidades: {
        ninguna: 'Ninguna',
        porLote: 'Por lote',
        porNumeroSerie: 'Por número de serie',
        desconocida: 'Sin reconocer',
        desconocidaDetalle:
          'Esta versión de la pantalla no sabe interpretar la trazabilidad que ha llegado. Avisa ' +
          'a quien administre Bastion.',
      },

      // La pantalla de cambio. Aquí y no en `catalogo.trazabilidad`: los espacios de `catalogo` son
      // sus recursos en disco, y la pantalla es de `articulos`.
      cambioDeTrazabilidad: {
        cargando: 'el artículo',
        articulo: 'Artículo',
        actual: 'Trazabilidad guardada',
        leyenda: 'Trazabilidad',
        // Lo que para el cambio, dicho antes de intentarlo y no solo en el error de después.
        pista: 'Solo se puede cambiar mientras el artículo no tenga movimientos de stock.',
        elige: 'Elige una de las tres.',
        guardar: 'Guardar',
        guardando: 'Guardando…',
        guardada: 'Guardado. La trazabilidad es ahora «{{trazabilidad}}».',
        recargar: 'Cargar la versión actual',
        volver: 'Volver a los artículos',
      },

      // `Palet` en el código y «palé» en la pantalla (ADR-0051 §11).
      niveles: {
        base: 'Unidad base',
        caja: 'Caja',
        palet: 'Palé',
        desconocido: 'Sin reconocer',
        desconocidoDetalle:
          'Esta versión de la pantalla no sabe interpretar el nivel que ha llegado. Avisa a quien ' +
          'administre Bastion.',
      },

      // La pantalla de los códigos de barras (ítem 2.10). Los rechazos no están aquí: son los
      // `errores.tipos` de la API, los diga el formulario o el servidor.
      gestionDeCodigosDeBarras: {
        cargando: 'los códigos de barras',
        articulo: 'Artículo',
        tabla: 'Códigos de barras de {{codigo}}',
        gtin: 'GTIN',
        nivel: 'Nivel',
        unidades: 'Unidades base',
        acciones: 'Acciones',
        ninguno: 'Este artículo todavía no tiene ningún código de barras.',
        quitar: 'Quitar',
        quitarElDe: 'Quitar el GTIN {{gtin}}',
        // La baja borra de verdad: se confirma en la misma fila antes de mandarla.
        confirmar: '¿Quitar el GTIN {{gtin}} de este artículo?',
        siQuitar: 'Sí, quitarlo',
        cancelar: 'Cancelar',
        quitando: 'Quitando…',
        quitado: 'Quitado el GTIN {{gtin}}.',
        alta: 'Dar de alta un código de barras',
        campoGtin: 'GTIN',
        pistaGtin: 'Las 8, 12, 13 o 14 cifras que van bajo las barras.',
        leyendaNivel: 'Nivel',
        pistaNivel: 'Dónde va impreso: en la unidad que se vende suelta, en la caja o en el palé.',
        campoUnidades: 'Unidades base que contiene',
        pistaUnidades: 'Dos o más. Si cambian, es otro GTIN.',
        agregar: 'Dar de alta',
        agregando: 'Dando de alta…',
        agregado: 'Dado de alta el GTIN {{gtin}}.',
        volver: 'Volver a los artículos',
      },
    },

    categorias: {
      cargando: 'las categorías',
      tabla: 'Árbol de categorías de la empresa activa',
      codigo: 'Código',
      nombre: 'Nombre',
      nivel: 'Nivel',
      articulos: 'Artículos',
      verSusArticulos: 'Ver los artículos de {{categoria}}',

      suelta: 'Sin su sitio',
      sueltaDetalle:
        'La categoría de la que cuelga no está en esta página, o el árbol guardado tiene un ' +
        'ciclo. Se muestra igualmente: una categoría que existe y no sale es una que alguien da ' +
        'de alta por segunda vez.',

      paginaVacia: 'Esta página no tiene categorías. Vuelve a la anterior.',
      ningunaTodavia: 'Todavía no hay ninguna categoría dada de alta en esta empresa.',
    },

    tarifas: {
      cargando: 'las tarifas',
      tabla: 'Tramos de tarifa de la empresa activa',
      codigo: 'Código',
      nombre: 'Nombre',
      vigencia: 'Vigencia',
      estado: 'Estado',
      acciones: 'Acciones',

      // El filtro dice por dónde busca. «Buscar» a secas manda a probar con la divisa o con el
      // precio —que ni se enseñan ni se filtran— y a concluir que la tarifa no está.
      filtro: 'Buscar por código o nombre',
      filtrar: 'Buscar',

      // Las dos formas de un periodo. Enteras y con sus huecos, no a trozos: el orden de las
      // partes cambia de un idioma a otro.
      desde: 'Desde el {{desde}}',
      entre: 'Del {{desde}} al {{hasta}}',

      verSusTramos: 'Ver los tramos de {{codigo}}',

      // Explica la FORMA del dato, no solo que hay un filtro puesto: quien llega aquí desde una
      // fila ve por primera vez que un código son varias filas, y sin esta frase parece que la
      // tarifa esté duplicada.
      tramosDe:
        'Mostrando los tramos de la tarifa «{{codigo}}», del más reciente al más antiguo. Una ' +
        'tarifa son varias filas: una por cada periodo de vigencia, y no se solapan nunca.',
      quitarElCodigo: 'Quitar el filtro de código',

      estados: {
        rige: 'Rige hoy',
        futura: 'Todavía no rige',
        caducada: 'Ya no rige',
        rigeDetalle:
          'El último día de vigencia está incluido: un tramo que acaba hoy sigue poniendo precio ' +
          'hoy, y deja de hacerlo mañana.',
      },

      paginaVacia: 'Esta página no tiene tramos de tarifa. Vuelve a la anterior.',
      ningunaTodavia: 'Todavía no hay ninguna tarifa dada de alta en esta empresa.',
      ningunaConEsteFiltro: 'Ninguna tarifa coincide con «{{filtro}}».',

      // Acotar por un código que no existe devuelve lo mismo que una tarifa recién abierta y
      // todavía sin tramos. Decirlo evita dar de alta una tarifa que ya existe con otro código.
      ningunTramoConEseCodigo: 'Ninguna tarifa de esta empresa tiene el código «{{codigo}}».',
    },
  },

  identidad: {
    acceso: {
      correo: 'Correo',
      contrasena: 'Contraseña',
      entrar: 'Entrar',
      entrando: 'Entrando…',
      credenciales: 'El correo o la contraseña no son correctos.',
      sinRed: 'No se ha podido contactar con el servidor. Inténtalo de nuevo.',
      escribeTuCorreo: 'Escribe tu correo.',
      correoDemasiadoLargo: 'El correo no puede pasar de 254 caracteres.',
      correoConFormatoMalo: 'Eso no parece un correo electrónico.',
      escribeTuContrasena: 'Escribe tu contraseña.',
      contrasenaDemasiadoLarga: 'La contraseña no puede pasar de 128 caracteres.',
    },
  },

  // La primera pantalla del inventario (ítem 2.12, ADR-0055). Las cantidades van en la unidad
  // base de cada artículo, y la pantalla lo dice: un recuento contado en cajas cuadraría mal.
  inventario: {
    // Lo que comparten las altas con líneas, el ajuste y la transferencia (ADR-0068 §8): el marco
    // de la rejilla, sus celdas, el buscador del artículo y los campos de la cabecera. Cada
    // documento pone el nombre de su tabla, sus columnas propias y su tope.
    lineas: {
      sinElegir: 'Sin elegir',
      pistaDeLaRejilla:
        'El tabulador pasa de una celda a otra, e Intro en la última celda añade una línea.',
      numero: 'Nº',
      ubicacion: 'Ubicación',
      articulo: 'Artículo',
      cantidad: 'Cantidad',
      unidad: 'Unidad',
      lote: 'Lote',
      numeroDeSerie: 'Número de serie',
      celda: '{{campo}} de la línea {{numero}}',
      antesElAlmacen: 'Elige antes el almacén',
      buscando: 'Buscando…',
      ningunArticulo: 'Ningún artículo coincide.',
      anadir: 'Añadir una línea',
      quitar: 'Quitar la línea {{numero}}',
    },

    // El ajuste a mano (ítem 2.17, ADR-0065). El alta va en su propia ruta, porque la rejilla de
    // líneas es lo que más pesa y el listado no tiene por qué cargarla.
    ajustes: {
      cargando: 'los ajustes',
      tabla: 'Ajustes de la empresa activa',
      ajuste: 'Ajuste',
      almacen: 'Almacén',
      fecha: 'Fecha de operación',
      estado: 'Estado',
      motivo: 'Motivo',
      // Se numera al confirmar. Un borrador se nombra por su almacén y su día, y puede haber dos
      // iguales: los distingue el motivo.
      numerado: 'Ajuste {{numero}}',
      sinNumero: 'Borrador de {{almacen}} del {{dia}}',

      todos: 'Todos',
      buscar: 'Buscar en el motivo',
      orden: 'Orden',
      ordenes: {
        masRecientes: 'Fecha, del último día al primero',
        masAntiguos: 'Fecha, del primer día al último',
        numeroDescendente: 'Número, de mayor a menor',
        numeroAscendente: 'Número, de menor a mayor',
      },
      filtrar: 'Filtrar',
      nuevo: 'Nuevo ajuste',
      paginaVacia: 'Esta página no tiene ajustes. Vuelve a la anterior.',
      ningunoTodavia: 'Todavía no hay ningún ajuste en esta empresa.',
      ningunoConEsteFiltro: 'Ningún ajuste coincide con el filtro.',
      volver: 'Volver a los ajustes',

      estados: {
        borrador: 'Borrador',
        confirmado: 'Confirmado',
        anulado: 'Anulado',
        desconocido: 'Sin reconocer',
        desconocidoDetalle: 'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
      },

      alta: {
        pista:
          'Cada línea lleva su cantidad con signo: en positivo entra, en negativo sale. El número lo ' +
          'recibe al confirmarlo.',
        serie: 'Serie',
        sinSeries:
          'No hay ninguna serie de ajustes activa. Da una de alta en el maestro de series.',
        tabla: 'Líneas del ajuste',
        coste: 'Coste por unidad base',
        eligeElAlmacen: 'Elige el almacén del ajuste.',
        eligeLaSerie: 'Elige una serie.',
        escribeLaFecha: 'Escribe el día del ajuste.',
        eligeLaUbicacion: 'Elige la ubicación.',
        eligeElArticulo: 'Elige un artículo de la lista.',
        eligeLaUnidad: 'Elige la unidad.',
        escribeLaCantidad: 'Escribe la cantidad: en negativo, si sale.',
        costeMalEscrito: 'El coste es una cifra sin signo, con cuatro decimales como mucho.',
        topeDeLineas: 'Un ajuste lleva como mucho 200 líneas.',
        abrir: 'Abrir el ajuste',
        abriendo: 'Abriendo…',
        sinMaestros:
          'Para abrir un ajuste hace falta poder ver los almacenes, las series, las ubicaciones, ' +
          'las unidades y los artículos.',
      },

      ficha: {
        cargando: 'el ajuste',
        anulaA: 'Compensa a',
        verOriginal: 'Ver el ajuste original',
        recuento: 'Viene de',
        verRecuento: 'Ver su recuento',
        deUnRecuento: 'Un recuento',
        lineas: 'Líneas',
        tabla: 'Líneas del ajuste',
        numero: 'Nº',
        ubicacion: 'Ubicación',
        articulo: 'Artículo',
        cantidad: 'Cantidad',
        factor: 'Factor de conversión',
        enUnidadBase: 'En unidad base',
        coste: 'Coste unitario',
        loteOSerie: 'Lote o serie',
        valor: 'Valor',
        // El factor lo pone el servidor (ADR-0065 §2), con la conversión del día del alta.
        nota:
          'El factor y la cantidad en unidad base los puso el servidor al abrirlo, con la ' +
          'conversión de ese día.',
        notaDelBorrador: 'El valor se calcula al confirmarlo.',

        confirmar: 'Confirmar el ajuste',
        preguntaConfirmar: 'Al confirmarlo recibe su número y mueve el stock.',
        siConfirmar: 'Sí, confirmar',
        confirmando: 'Confirmando…',
        confirmadoAviso: 'Ajuste confirmado con el número {{numero}}.',
        anular: 'Anular el ajuste',
        preguntaAnular: 'Anularlo crea un ajuste inverso, ya confirmado, que lo compensa.',
        siAnular: 'Sí, anular',
        anulando: 'Anulando…',
        anuladoAviso: 'Ajuste anulado. Lo compensa el ajuste {{numero}}.',
        verInverso: 'Ver el inverso',
        descartar: 'Descartar el borrador',
        preguntaDescartar:
          'Descartarlo lo quita del listado. No ha movido nada, y no se puede deshacer.',
        siDescartar: 'Sí, descartar',
        descartando: 'Descartando…',
        descartadoAviso: 'Borrador descartado.',
        cancelar: 'Cancelar',
      },
    },

    recuentos: {
      cargando: 'los recuentos',
      tabla: 'Recuentos de la empresa activa',
      recuento: 'Recuento',
      almacen: 'Almacén',
      abierto: 'Abierto el',
      confirmado: 'Confirmado el',
      estado: 'Estado',
      motivo: 'Motivo',

      // Se numera al confirmar (ADR-0055 §1.4). Hasta entonces se nombra por su almacén y su día:
      // con un solo recuento en curso por almacén, no hay otro que se llame igual.
      numerado: 'Recuento {{numero}}',
      sinNumero: 'Recuento de {{almacen}} del {{dia}}',

      todos: 'Todos',
      filtrar: 'Filtrar',
      paginaVacia: 'Esta página no tiene recuentos. Vuelve a la anterior.',
      ningunoTodavia: 'Todavía no hay ningún recuento en esta empresa.',
      ningunoConEsteFiltro: 'Ningún recuento coincide con el filtro.',

      estados: {
        enCurso: 'En curso',
        confirmado: 'Confirmado',
        anulado: 'Anulado',
        descartado: 'Descartado',
        desconocido: 'Sin reconocer',
        desconocidoDetalle: 'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
      },

      apertura: {
        titulo: 'Abrir un recuento',
        pista:
          'Se cuenta el almacén entero, en la unidad base de cada artículo. El número lo recibe al ' +
          'confirmarlo.',
        serie: 'Serie del recuento',
        serieDelAjuste: 'Serie del ajuste',
        sinElegir: 'Sin elegir',
        sinSeries:
          'No hay ninguna serie activa de esta clase. Da una de alta en el maestro de series.',
        eligeElAlmacen: 'Elige el almacén que se cuenta.',
        eligeLaSerie: 'Elige una serie.',
        abrir: 'Abrir el recuento',
        abriendo: 'Abriendo…',
        sinMaestros: 'Para abrir un recuento hace falta poder ver los almacenes y las series.',
      },

      ficha: {
        cargando: 'el recuento',
        volver: 'Volver a los recuentos',
        lineas: 'Líneas',
        cuentas: '{{lineas}} en total, {{sinContar}} sin contar',
        cuentasEnCurso:
          '{{lineas}} en total, {{sinContar}} sin contar, {{cambiadas}} con el teórico cambiado y ' +
          '{{transito}} con mercancía en tránsito',
        motivoDelDescarte: 'Motivo del descarte',
        motivoDeLaAnulacion: 'Motivo de la anulación',
        ajuste: 'Ajuste',
        conAjuste: 'Movió la diferencia',
        sinAjuste: 'Ninguno: lo contado cuadraba',
        notaEnCurso:
          'Cada línea se cuenta en la unidad base de su artículo. El teórico es el de ahora, y la ' +
          'diferencia, lo que movería el ajuste al confirmar.',
        notaCerrado:
          'El teórico es el que quedó al confirmar, y la diferencia, lo que movió el ajuste.',
        notaDescartado: 'Se descartó sin mover nada.',

        vistas: 'Qué líneas',
        todas: 'Todas',
        sinContar: 'Sin contar',
        teoricoCambiado: 'Con el teórico cambiado',

        tabla: 'Líneas del recuento',
        numero: 'Nº',
        articulo: 'Artículo',
        ubicacion: 'Ubicación',
        loteOSerie: 'Lote o serie',
        unidad: 'Unidad',
        teorico: 'Teórico',
        enTransito: 'En tránsito',
        contado: 'Contado',
        diferencia: 'Diferencia',
        acciones: 'Acciones',
        // El teórico de cuando se contó y cuánto ha cambiado desde entonces (ADR-0055 §2).
        alContar: 'Al contar era {{antes}} ({{cambio}})',
        ninguna: 'Este recuento no tiene líneas: el almacén no tenía existencias al abrirlo.',
        ningunaEnLaVista: 'Ninguna línea en esta vista.',
        paginaVacia: 'Esta página no tiene líneas. Vuelve a la anterior.',

        contar: 'Contar',
        corregir: 'Corregir',
        abriendo: 'Abriendo…',
        contarLa: 'Contar la línea {{numero}}',
        corregirLa: 'Corregir lo contado en la línea {{numero}}',
        campoContado: 'Contado en la línea {{numero}}, en {{unidad}}',
        // Una línea sin contar no es un cero (ADR-0055 §6): el cero se escribe.
        escribeLoContado: 'Escribe lo contado; si no hay ninguno, un 0.',
        guardar: 'Guardar',
        guardando: 'Guardando…',
        cancelar: 'Cancelar',
        contada: 'Línea {{numero}} contada.',
        // El `412` de contar: el campo se abrió con una versión de la línea que ya no es la de ahora.
        contadaPorOtraPersona:
          'Otra persona ha contado la línea {{numero}} mientras la tenías abierta: ' +
          '{{contado}} {{unidad}}. Lo que habías escrito no se ha guardado.',

        confirmar: 'Confirmar el recuento',
        preguntaConfirmar:
          'Al confirmarlo se congela el teórico, se genera el ajuste de la diferencia y recibe su ' +
          'número.',
        siConfirmar: 'Sí, confirmar',
        confirmando: 'Confirmando…',
        confirmadoAviso: 'Recuento confirmado con el número {{numero}}.',
        anular: 'Anular el recuento',
        preguntaAnular: 'Anularlo deshace su ajuste con un inverso, con la fecha de hoy.',
        siAnular: 'Sí, anular',
        anulando: 'Anulando…',
        anuladoAviso: 'Recuento anulado.',
        descartar: 'Descartar el recuento',
        preguntaDescartar:
          'Descartarlo lo cierra sin mover nada, y el almacén queda libre para otro recuento.',
        siDescartar: 'Sí, descartar',
        descartando: 'Descartando…',
        descartadoAviso: 'Recuento descartado.',
      },
    },

    saldos: {
      cargando: 'el saldo',
      // El día lo dice la respuesta, no el campo: sin fecha, contesta el servidor con su hoy.
      tabla: 'Saldo al final del {{dia}}, por artículo y almacén',
      dia: 'Al final del día',
      articulo: 'Artículo',
      almacen: 'Almacén',
      cantidad: 'Cantidad',
      valor: 'Valor',
      enTransito: 'En tránsito',
      valorEnTransito: 'Valor en tránsito',
      transitoAparte:
        'Lo que está en tránsito salió de otro almacén hacia este y todavía no había llegado al ' +
        'final del día. No está sumado ni en la cantidad ni en el valor.',
      todos: 'Todos',
      consultar: 'Consultar',
      paginaVacia: 'Esta página no tiene saldos. Vuelve a la anterior.',
      ningunoTodavia: 'Esta empresa todavía no ha movido stock.',
      ningunoConEsteFiltro: 'Ningún saldo coincide con el filtro.',
    },

    // La transferencia entre almacenes (ítem 2.19, ADR-0068). Se nombra, se acota y se lee como el
    // ajuste, pero con dos almacenes, y sin búsqueda: no tiene ningún texto que escriba una persona.
    transferencias: {
      cargando: 'las transferencias',
      tabla: 'Transferencias de la empresa activa',
      transferencia: 'Transferencia',
      origen: 'Origen',
      destino: 'Destino',
      envio: 'Fecha de envío',
      recepcion: 'Fecha de recepción',
      estado: 'Estado',
      // Se numera al enviarla. Un borrador se nombra por sus dos almacenes y el día en que sale.
      numerada: 'Transferencia {{numero}}',
      sinNumero: 'Borrador de {{origen}} a {{destino}} del {{dia}}',

      todas: 'Todas',
      todos: 'Todos',
      orden: 'Orden',
      ordenes: {
        envioReciente: 'Envío, del último día al primero',
        envioAntiguo: 'Envío, del primer día al último',
        recepcionReciente: 'Recepción, del último día al primero',
        recepcionAntigua: 'Recepción, del primer día al último',
        numeroDescendente: 'Número, de mayor a menor',
        numeroAscendente: 'Número, de menor a mayor',
      },
      filtrar: 'Filtrar',
      paginaVacia: 'Esta página no tiene transferencias. Vuelve a la anterior.',
      ningunaTodavia: 'Todavía no hay ninguna transferencia en esta empresa.',
      ningunaConEsteFiltro: 'Ninguna transferencia coincide con el filtro.',

      estados: {
        borrador: 'Borrador',
        enviada: 'Enviada',
        recibida: 'Recibida',
        anulada: 'Anulada',
        desconocido: 'Sin reconocer',
        desconocidoDetalle: 'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
      },
    },
  },

  organizacion: {
    almacenes: {
      cargando: 'los almacenes',
      tabla: 'Almacenes de la empresa activa',
      codigo: 'Código',
      nombre: 'Nombre',
      tipo: 'Tipo',
      poblacion: 'Población',
      paginaVacia: 'Esta página no tiene almacenes. Vuelve a la anterior.',
      ningunoTodavia: 'Todavía no hay ningún almacén dado de alta en esta empresa.',
    },

    empresas: {
      cargando: 'las empresas',
      tabla: 'Empresas dadas de alta',
      nif: 'NIF',
      razonSocial: 'Razón social',
      poblacion: 'Población',
      divisa: 'Divisa',
      ningunaVisible: 'No hay ninguna empresa que puedas ver.',
    },
  },

  terceros: {
    terceros: {
      cargando: 'los terceros',
      tabla: 'Terceros de la empresa activa',
      identificador: 'Identificador fiscal',
      razonSocial: 'Razón social',
      poblacion: 'Población',
      papel: 'Papel',

      // El filtro dice por qué busca, y no es un adorno: quien lee «Buscar» prueba con el NIF, no
      // lo encuentra y concluye que el tercero no existe. Decir por dónde busca este recuadro
      // ahorra esa alta duplicada. Por NIF se busca desde la ficha, y va por el cuerpo (ADR-0025).
      filtro: 'Buscar por razón social o nombre comercial',
      filtrar: 'Buscar',

      paginaVacia: 'Esta página no tiene terceros. Vuelve a la anterior.',
      ningunoTodavia: 'Todavía no hay ningún tercero dado de alta en esta empresa.',
      ningunoConEsteFiltro: 'Ningún tercero coincide con «{{filtro}}».',

      verificacion: {
        verificado: 'Comprobado',
        verificadoDetalle: 'El carácter de control del identificador cuadra.',
        sinVerificar: 'Sin comprobar',
        sinVerificarDetalle:
          'Este identificador no se puede comprobar por su forma —es extranjero, o no sigue el ' +
          'formato español—, así que puede estar mal tecleado. Revísalo antes de facturar.',
        desconocida: 'Sin comprobar',
        desconocidaDetalle:
          'Esta versión de la pantalla no sabe interpretar el estado de comprobación que ha ' +
          'llegado. Trátalo como no comprobado y avisa a quien administre Bastion.',
      },

      papeles: {
        cliente: 'Cliente',
        proveedor: 'Proveedor',
        ambos: 'Cliente y proveedor',
      },

      enlaceAImportar: 'Importar desde un CSV',

      // La importación es una pantalla de este mismo recurso, y sus textos cuelgan de él: el segundo
      // nivel del diccionario son las carpetas de la funcionalidad (`ElBarridoDeLasFronteras`).
      importacion: {
        explicacion:
          'Cada fila del fichero da de alta un tercero nuevo. Las filas con algún error no entran, y ' +
          'el informe dice en qué línea y por qué; las demás sí. Un tercero que ya existe no se ' +
          'modifica: su fila sale rechazada.',
        plantilla: 'La plantilla',
        plantillaDetalle:
          'La primera fila tiene que ser esta cabecera, tal cual y en este orden. Son obligatorios ' +
          'el identificador, la razón social y la calle, el código postal, la población y el país ' +
          'del domicilio; lo demás puede ir vacío.',
        reglaFormato:
          'Guárdalo desde Excel con la configuración regional de España, como «CSV (delimitado por ' +
          'comas)» o «CSV UTF-8»: las columnas van separadas por punto y coma.',
        reglaValores:
          'Los sí o no se escriben «sí», «no», «VERDADERO» o «FALSO», y vacío es no. Los importes ' +
          'llevan coma decimal y hasta cuatro decimales, como 1.234,50.',
        reglaTope: 'Como mucho 2 MB y 5000 filas por fichero. Si tienes más, pártelo en varios.',
        fichero: 'Fichero CSV',
        importar: 'Importar',
        importando: 'Importando el fichero…',
        resultado: 'Resultado de la importación',
        leidas: 'Filas leídas',
        importadas: 'Importadas',
        rechazadas: 'Rechazadas',
        sinFilas: 'El fichero solo trae la cabecera: no había ninguna fila que importar.',
        todasDentro: 'Han entrado todas las filas.',
        rechazos: 'Por qué no han entrado',
        columna: 'Columna',
        motivo: 'Motivo',
        lineas: 'Líneas',
        filaEntera: 'La fila entera',
        lineasDeLaHoja:
          'Las líneas son las de la hoja de cálculo: la cabecera es la 1 y la primera fila de datos, ' +
          'la 2. Corrige esas filas y vuelve a importar solo ellas, que las demás ya están dentro.',
        motivos: {
          numeroDeCamposDistinto: 'La fila no tiene tantas columnas como la cabecera.',
          comillasMalColocadas: 'Hay unas comillas en mitad del campo.',
          obligatorio: 'Está vacío y es obligatorio.',
          demasiadoLargo: 'Es más largo de lo que admite la columna.',
          formatoNoValido:
            'No se puede leer como lo que espera la columna: un importe, un sí o un no.',
          noValido:
            'Se lee, pero no es un valor válido: por ejemplo, un NIF con la letra que no le toca.',
          niClienteNiProveedor: 'Tiene que ser cliente, proveedor o las dos cosas.',
          yaExiste: 'Ya hay un tercero con ese identificador en la empresa.',
          repetidaEnElFichero:
            'Una fila anterior del mismo fichero ya da de alta ese identificador.',
          desconocido:
            'Esta versión de la pantalla no sabe explicar este motivo. Avisa a quien administre Bastion.',
        },
      },
    },
  },
};

/**
 * La forma del diccionario. Todo idioma la cumple ENTERA: una clave de menos o una de más es un
 * error de compilación, no un hueco que se descubre en pantalla.
 */
export type Diccionario = typeof es;
