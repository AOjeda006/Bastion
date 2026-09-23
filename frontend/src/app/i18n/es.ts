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
    almacenes: 'Almacenes',
    articulos: 'Artículos',
    categorias: 'Categorías',
    empresas: 'Empresas',
    tarifas: 'Tarifas',
    terceros: 'Terceros',
    importarTerceros: 'Importar terceros',
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

    // Un texto por cada `type` que la API puede emitir. Las claves son el CÓDIGO tal cual, sin
    // camelizar: la correspondencia con `docs/api/errores.json` tiene que poder mirarse a ojo y
    // compararse entera, y cualquier transformación en medio es una regla más que se puede
    // equivocar. El barrido de `ElCambioDeIdioma` compara este objeto contra el artefacto en los
    // dos sentidos, así que un `type` nuevo sin texto es rojo el día que se escribe (ADR-0030).
    tipos: {
      // Las doce del ajuste (ítem 2.3). Las cuatro de un maestro bloqueado o retirado dicen las
      // DOS mitades del ADR-0037 y del ADR-0023 —lo que ya está escrito se sigue leyendo, lo
      // nuevo no entra—, porque un texto que solo dijera «no se puede» manda a quien lo lee a
      // buscar una ficha que sí existe.
      'ajuste-almacen-bloqueado':
        'Ese almacén está bloqueado: sus movimientos anteriores se siguen leyendo y valorando, ' +
        'pero no admite un ajuste nuevo. Elige otro almacén o pide que lo desbloqueen.',
      'ajuste-almacen-no-encontrado': 'Ese almacén no existe. Elige uno del maestro de almacenes.',
      'ajuste-articulo-no-encontrado':
        'Ese artículo no existe. Elige uno del maestro de artículos.',
      'ajuste-articulo-no-se-almacena':
        'Ese artículo es un servicio: no tiene existencias que ajustar. Quita esa línea o cambia ' +
        'el artículo.',
      'ajuste-motivo-no-valido':
        'Escribe por qué anulas el ajuste, en 300 caracteres o menos. Es lo único que quedará ' +
        'para entender la corrección dentro de dos años.',
      'ajuste-no-encontrado': 'Ese ajuste ya no existe. Vuelve al listado y actualiza.',
      'ajuste-no-esta-confirmado':
        'Ese ajuste no está confirmado, así que no hay nada que anular: un borrador no ha movido ' +
        'el libro. Actualiza la pantalla para ver en qué estado está.',
      'ajuste-no-esta-en-borrador':
        'Ese ajuste ya no está en borrador: uno confirmado no se vuelve a confirmar, porque sus ' +
        'filas del libro ya están escritas y el libro no se reescribe. Actualiza la pantalla.',
      'ajuste-serie-cerrada':
        'Esa serie está cerrada: sigue resolviendo los documentos que ya numeró, pero no entrega ' +
        'ni un número más. Elige otra serie.',
      'ajuste-serie-no-encontrada': 'Esa serie no existe. Elige una del maestro de series.',
      'ajuste-sin-lineas':
        'Un ajuste necesita al menos una línea: un documento que no mueve nada no ajusta nada.',
      'ajuste-ubicacion-bloqueada':
        'Esa ubicación está bloqueada: lo que ya hay apuntado a ella se sigue leyendo, pero no se ' +
        'mueve nada nuevo a ese hueco. Elige otra ubicación.',
      'ajuste-ubicacion-no-encontrada':
        'Esa ubicación no existe en ese almacén. Elige una de las suyas.',
      'ajuste-unidad-no-encontrada':
        'Esa unidad de medida no existe. Elige una del maestro de unidades.',
      'ajuste-unidad-retirada':
        'Esa unidad está retirada: los movimientos que ya se escribieron en ella se siguen ' +
        'leyendo, pero no se escribe uno nuevo con ella. Elige otra unidad.',
      'almacen-duplicado': 'Ya hay un almacén con ese código en esta empresa.',
      'almacen-no-encontrado': 'Ese almacén ya no existe. Vuelve al listado y actualiza.',
      'articulo-duplicado': 'Ya hay un artículo con ese código en esta empresa.',
      'articulo-impuesto-no-encontrado':
        'Ese impuesto no existe. Elige uno del maestro de impuestos.',
      'articulo-impuesto-no-vigente':
        'Ese tramo de impuesto ya no rige. Elige uno que esté vigente.',
      'articulo-no-encontrado': 'Ese artículo ya no existe. Vuelve al listado y actualiza.',
      'articulo-proveedor-duplicado': 'Ese proveedor ya está en la lista de este artículo.',
      'articulo-proveedor-no-encontrado':
        'Ese proveedor ya no está en la lista de este artículo. Actualiza la pantalla.',
      // El texto que NO distingue, y por eso está escrito así de largo. Detrás hay tres
      // situaciones —la ficha no existe, está reservada por el artículo 32, o no está marcada
      // como proveedora— y la API contesta lo mismo en las tres a propósito: separarlas
      // convertiría este formulario en una manera de averiguar quién está dado de baja. Así que
      // aquí tampoco se separan, y lo que se le ofrece a quien lo lee es lo que puede hacer.
      'articulo-proveedor-tercero-no-valido':
        'Ese tercero no se puede poner como proveedor de este artículo. Comprueba en el maestro ' +
        'de terceros que la ficha existe y que está marcada como proveedora.',
      'articulo-tipo-no-valido': 'El tipo de un artículo solo puede ser «Bien» o «Servicio».',
      'articulo-unidad-no-encontrada':
        'Esa unidad de medida no existe. Elige una del maestro de unidades.',
      // La retirada del ADR-0023 dicha entera, con sus dos mitades: por eso no vale «esa unidad
      // no existe». Quien lee esto tiene delante artículos que la siguen usando, y una frase que
      // dijera que no existe le mandaría a buscar un fallo que no hay.
      'articulo-unidad-retirada':
        'Esa unidad está retirada: los artículos que ya la usan siguen contándose en ella, pero ' +
        'no se puede dar de alta uno nuevo. Elige otra.',
      'categoria-ciclo':
        'Una categoría no puede colgar de sí misma ni de ninguna de las que cuelgan de ella.',
      'categoria-demasiado-profunda':
        'El árbol de categorías no admite más niveles por debajo de ese sitio.',
      'categoria-duplicada': 'Ya hay una categoría con ese código en esta empresa.',
      'categoria-no-encontrada': 'Esa categoría ya no existe. Vuelve al listado y actualiza.',
      'categoria-padre-no-encontrado': 'La categoría de la que quieres colgar esta no existe.',
      'codigo-de-rol-ya-usado': 'Ya hay un rol con ese código. Elige otro.',
      'contrasena-actual-incorrecta': 'La contraseña actual no es correcta.',
      'conversion-um-duplicada': 'Ya hay una conversión entre esas dos unidades.',
      'conversion-um-inversa-implausible':
        'El sentido contrario ya está declarado y ese factor no es su inverso.',
      'conversion-um-no-declarada':
        'No hay conversión declarada entre esas dos unidades. Dala de alta.',
      'conversion-um-no-encontrada': 'Esa conversión de unidades ya no existe.',
      'correo-ya-registrado': 'Ya hay una cuenta con ese correo electrónico.',
      'credenciales-no-validas': 'El correo o la contraseña no son correctos.',
      'cuenta-bancaria-duplicada': 'Esta ficha ya tiene esa cuenta bancaria.',
      'cuenta-bancaria-no-encontrada': 'Esta ficha no tiene esa cuenta bancaria.',
      'cuerpo-demasiado-grande':
        'Lo que intentas enviar es demasiado grande. Pártelo en varios envíos más pequeños.',
      'datos-no-validos': 'Algunos campos no son válidos. Revisa los que aparecen marcados.',
      'divisa-duplicada': 'Ya hay una divisa con ese código.',
      'divisa-no-encontrada': 'Esa divisa ya no existe.',
      'ejercicio-cerrado': 'El ejercicio está cerrado y no admite cambios.',
      'ejercicio-con-borradores':
        'Quedan documentos en borrador con fecha dentro del ejercicio. Confírmalos o bórralos antes de cerrar.',
      'ejercicio-con-series':
        'No se puede eliminar un ejercicio que tiene series. Elimina antes las series.',
      'ejercicio-duplicado': 'Ya hay un ejercicio con ese año en esta empresa.',
      'ejercicio-no-encontrado': 'Ese ejercicio ya no existe. Vuelve al listado y actualiza.',
      'ejercicio-solapado':
        'Esas fechas se pisan con las de otro ejercicio de la empresa. Una fecha tiene que caer en un solo ejercicio.',
      'ejercicio-ya-abierto': 'Ese ejercicio ya estaba abierto, así que no se ha reabierto.',
      'ejercicio-ya-cerrado':
        'Ese ejercicio ya estaba cerrado, así que no lo has cerrado tú. Vuelve a leerlo antes de decidir.',
      'empresa-activa-no-operativa':
        'La empresa con la que estás operando ya no está disponible. Vuelve a entrar.',
      'empresa-ajena': 'Esa empresa no es la tuya, así que no puedes operar sobre ella.',
      'empresa-destino-no-operativa':
        'La empresa que has elegido no admite altas: no existe o está bloqueada.',
      'empresa-no-encontrada': 'Esa empresa ya no existe. Vuelve al listado y actualiza.',
      'empresa-no-pertenece': 'No perteneces a esa empresa, así que no puedes operar con ella.',
      'empresa-ya-registrada': 'Ya hay una empresa con ese NIF.',
      'falta-if-match':
        'Para guardar hay que decir sobre qué versión se escribe. Vuelve a abrir el formulario.',
      'idempotencia-clave-no-valida':
        'La aplicación ha enviado una clave de repetición que no vale. Inténtalo otra vez.',
      'idempotencia-cuerpo-distinto':
        'Se ha repetido una operación con los mismos datos de envío pero distinto contenido. Vuelve a empezar.',
      'idempotencia-no-admitida': 'Esta operación no admite repetición segura. Inténtalo otra vez.',
      'idempotencia-obligatoria':
        'Esta operación solo se hace con una clave de repetición, porque a medias dejaría un ' +
        'hueco en la numeración. Vuelve a intentarlo.',
      'idempotencia-sin-empresa-activa':
        'Tu sesión no tiene ninguna empresa activa. Elige una y vuelve a intentarlo.',
      'if-match-no-valido':
        'La versión que traía el formulario no tiene forma válida. Vuelve a abrirlo.',
      'importacion-cabecera-no-valida':
        'La primera fila del fichero no es la cabecera de la plantilla. Cópiala tal cual, en su orden.',
      'importacion-codificacion-no-admitida':
        'El fichero no está guardado como CSV de Excel. Guárdalo como «CSV (delimitado por comas)» o «CSV UTF-8».',
      'importacion-comillas-sin-cerrar':
        'El fichero tiene unas comillas que se abren y no se cierran. Revísalo en la hoja de cálculo.',
      'importacion-demasiadas-filas':
        'El fichero tiene demasiadas filas. Pártelo en varios de 5000 filas como mucho.',
      'importacion-fin-de-linea-no-admitido':
        'El fichero separa las filas de una forma que no admitimos. Ábrelo y guárdalo otra vez como CSV.',
      'importacion-separador-no-admitido':
        'El fichero no separa las columnas con punto y coma. Guárdalo desde Excel con la configuración regional de España.',
      'importacion-sin-permiso-de-alta':
        'Importar terceros es darlos de alta, y no tienes permiso para dar de alta terceros.',
      'importacion-sin-permiso-de-limite':
        'Alguna fila trae límite de crédito y no tienes permiso para fijarlo. Deja vacías esas columnas.',
      'impuesto-con-tramos-solapados':
        'Los tramos de vigencia de ese impuesto se solapan. Revisa las fechas.',
      'impuesto-no-encontrado': 'Ese impuesto ya no existe.',
      'orden-no-admitido': 'No se puede ordenar por ese campo.',
      'permisos-de-rol-del-sistema':
        'Los permisos del rol del sistema los fija cada actualización. Puedes cambiarle el nombre; para dar menos permisos, crea un rol propio.',
      'pertenencia-no-encontrada': 'Esa persona no pertenece a la empresa indicada.',
      'rol-no-encontrado': 'Ese rol ya no existe. Vuelve al listado y actualiza.',
      'serie-cerrada': 'La serie está cerrada y no admite cambios.',
      'serie-duplicada': 'Ya hay una serie con ese código en ese ejercicio.',
      'serie-no-encontrada': 'Esa serie ya no existe. Vuelve al listado y actualiza.',
      'serie-no-numera':
        'Esa serie ya no entrega números: la han cerrado o la han borrado mientras tenías el ' +
        'borrador abierto. Abre el ajuste con otra serie.',
      'serie-ya-numerada': 'La serie ya ha numerado documentos, así que eso no se puede cambiar.',
      'sesion-no-renovable': 'Tu sesión no se ha podido renovar. Vuelve a entrar.',
      // Las doce de la tarifa. La de la divisa retirada dice las DOS mitades del ADR-0023, igual
      // que la de la unidad: quien la lee tiene delante tarifas que la siguen usando, y una frase
      // que dijera que no existe le mandaría a buscar un fallo que no hay.
      'tarifa-divisa-no-encontrada': 'Esa divisa no existe. Elige una del maestro de divisas.',
      'tarifa-divisa-retirada':
        'Esa divisa está retirada: las tarifas que ya la usan se siguen expresando en ella, pero ' +
        'no se puede abrir una nueva. Elige otra.',
      'tarifa-linea-articulo-o-categoria':
        'Una línea de tarifa le pone precio a un artículo o a una categoría, y hay que elegir uno ' +
        'de los dos: ni los dos, ni ninguno.',
      'tarifa-linea-no-encontrada':
        'Esa línea de tarifa ya no existe. Vuelve al listado y actualiza.',
      // Los dos negativos dichos enteros. El segundo es el que se olvida, y es el que dejaría
      // entrar una línea que devuelve un importe que nadie escribió.
      'tarifa-linea-precio-o-descuento':
        'Una línea de tarifa lleva un precio o un descuento, y hay que poner uno de los dos: ni ' +
        'los dos, ni ninguno.',
      'tarifa-linea-primer-tramo-sin-cero':
        'El primer tramo de cantidad tiene que empezar en cero. Empezando más arriba, las ' +
        'cantidades por debajo se quedarían sin precio.',
      'tarifa-linea-tramo-duplicado':
        'Ya hay un tramo que empieza en esa cantidad para ese artículo o esa categoría.',
      'tarifa-no-encontrada': 'Esa tarifa ya no existe. Vuelve al listado y actualiza.',
      // «No hay tarifa» y «la hay pero no cubre ese día» no se arreglan igual, y por eso son dos
      // frases distintas: la primera se corrige escribiendo bien el código, la segunda abriendo el
      // tramo que falta.
      'tarifa-no-vigente':
        'Esa tarifa existe, pero ninguno de sus tramos cubre la fecha pedida. Abre el tramo que ' +
        'falta o pregunta por otra fecha.',
      'tarifa-sin-linea-aplicable':
        'Esa tarifa no dice nada de ese artículo para esa cantidad, ni suya ni de ninguna de sus ' +
        'categorías. Añade la línea que falta: aquí no hay precio que aplicar.',
      'tarifa-vigencia-al-reves': 'La vigencia acaba antes de empezar. Revisa las dos fechas.',
      'tarifa-vigencias-solapadas':
        'Ya hay otro tramo de esa tarifa que cubre alguno de esos días. Los periodos de una ' +
        'tarifa no se pueden solapar.',
      'tercero-duplicado': 'Esta empresa ya tiene un tercero con ese identificador fiscal.',
      'tercero-no-encontrado': 'Ese tercero ya no existe. Vuelve al listado y actualiza.',
      'tercero-tarifa-no-encontrada': 'Esa tarifa no existe. Elige una del maestro de tarifas.',
      // La retirada del ADR-0023 otra vez, con sujeto nuevo: la tarifa caducada NO desaparece
      // —sigue diciendo a qué precio se vendió lo ya emitido—, lo que no se puede es empezar a
      // aplicarla hoy. Un «esa tarifa no existe» mandaría a buscar un fallo que no hay.
      'tercero-tarifa-no-vigente':
        'Esa tarifa ya no está vigente: sigue valiendo para lo que se emitió mientras regía, ' +
        'pero no se puede asignar hoy. Elige una que esté vigente.',
      'tipo-cambio-duplicado': 'Ya hay un tipo de cambio para esa divisa en esa fecha.',
      'tipo-cambio-no-encontrado': 'Ese tipo de cambio ya no existe.',
      'ubicacion-duplicada': 'Ya hay una ubicación con ese código en ese almacén.',
      'ubicacion-no-encontrada': 'Esa ubicación ya no existe. Vuelve al listado y actualiza.',
      'unidad-medida-duplicada': 'Ya hay una unidad de medida con ese código.',
      'unidad-medida-no-encontrada': 'Esa unidad de medida ya no existe.',
      'usuario-no-encontrado': 'Esa persona ya no existe. Vuelve al listado y actualiza.',
      'version-obsoleta':
        'Alguien ha guardado antes que tú. Vuelve a abrir el formulario para no pisar sus cambios.',
    },
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
