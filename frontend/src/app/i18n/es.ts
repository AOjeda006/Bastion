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
    recuentos: 'Recuentos',
    recuento: 'Ficha del recuento',
    tarifas: 'Tarifas',
    terceros: 'Terceros',
    importarTerceros: 'Importar terceros',
    trazabilidadDelArticulo: 'Trazabilidad del artículo',
    codigosDeBarrasDelArticulo: 'Códigos de barras del artículo',
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
      'ajuste-con-fecha-futura':
        'La fecha de ese ajuste es posterior a hoy: el libro solo recoge lo que ya ha pasado. ' +
        'Confírmalo ese día o corrige la fecha.',
      'ajuste-coste-no-valido':
        'Una línea que saca existencias no lleva coste: se valora al precio medio. Y ningún ' +
        'coste puede ser negativo; una muestra o un regalo entran a cero.',
      'ajuste-de-un-recuento-no-se-anula':
        'Ese ajuste es el de la diferencia de un recuento, y no se anula por separado: el recuento ' +
        'seguiría confirmado con su diferencia deshecha. Anula el recuento, y su ajuste se anulará ' +
        'con él.',
      'ajuste-en-ejercicio-cerrado':
        'La fecha de ese ajuste cae en un ejercicio cerrado: ese periodo ya es definitivo y no ' +
        'admite documentos nuevos. Cambia la fecha o pide que se reabra el ejercicio.',
      'ajuste-entrada-sin-coste-ni-precio-medio':
        'Una línea mete un artículo que no tiene existencias en ese almacén, y sin existencias no ' +
        'hay precio medio al que valorarla. Escribe el coste de esa línea.',
      'ajuste-fecha-anterior-al-ultimo-movimiento':
        'Algún artículo del documento ya tiene un movimiento posterior a esa fecha en ese almacén, ' +
        'y un documento no puede ir por detrás del último movimiento de su artículo. Pon esa fecha ' +
        'o una posterior.',
      'ajuste-inverso-no-se-anula':
        'Ese ajuste es el inverso de otro, y un inverso no se deshace con otro: deshacerlo sería ' +
        'volver a hacer el ajuste. Si hace falta, se hace con un ajuste nuevo.',
      'ajuste-lote-no-valido':
        'Ese lote no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin espacios, ' +
        'con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el lote de esa ' +
        'línea.',
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
      'ajuste-numero-de-serie-no-valido':
        'Ese número de serie no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin ' +
        'espacios, con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el ' +
        'número de serie de esa línea.',
      'ajuste-serie-cerrada':
        'Esa serie está cerrada: sigue resolviendo los documentos que ya numeró, pero no entrega ' +
        'ni un número más. Elige otra serie.',
      'ajuste-serie-no-encontrada': 'Esa serie no existe. Elige una del maestro de series.',
      'ajuste-serie-no-unitaria':
        'Una línea con número de serie mueve exactamente una unidad, porque cada número es una ' +
        'pieza y no hay dos con el mismo. Pon una línea por pieza, con cantidad 1 en la unidad ' +
        'base del artículo.',
      'ajuste-serie-repetida':
        'Dos líneas del ajuste nombran el mismo número de serie, y cada pieza sale una sola vez ' +
        'por documento. Cambiarla de ubicación es una reubicación, que es otro documento.',
      'ajuste-sin-ejercicio':
        'La fecha de ese ajuste no cae en ningún ejercicio: sin periodo al que imputarlo, el ' +
        'documento no entraría en ninguna declaración. Abre el ejercicio que falta o corrige la ' +
        'fecha.',
      'ajuste-sin-lineas':
        'Un ajuste necesita al menos una línea: un documento que no mueve nada no ajusta nada.',
      'ajuste-trazabilidad-no-casa':
        'Alguna línea no casa con la trazabilidad de su artículo: le falta el lote o el número de ' +
        'serie que pide la ficha, o lleva uno que la ficha no pide. Corrige la línea, o la ' +
        'trazabilidad del artículo si todavía no se ha movido.',
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
      'ajuste-valoracion-en-otra-divisa':
        'Las existencias de algún artículo del documento están valoradas en otra divisa en ese ' +
        'almacén, y sumarlas pediría un tipo de cambio. Haz el ajuste en la divisa en que están ' +
        'valoradas.',
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
      'articulo-servicio-con-trazabilidad':
        'Un servicio no tiene existencias, así que no lleva lote ni número de serie.',
      'articulo-tipo-no-valido': 'El tipo de un artículo solo puede ser «Bien» o «Servicio».',
      // Un conflicto con el libro, no un error de lo tecleado: lo que hay que decir es por qué
      // ya no se puede, para que nadie lo reintente esperando otra respuesta.
      'articulo-trazabilidad-con-movimientos':
        'Este artículo ya tiene movimientos de stock, así que su trazabilidad no se puede cambiar.',
      'articulo-trazabilidad-no-valida':
        'La trazabilidad de un artículo solo puede ser ninguna, por lote o por número de serie.',
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
      'codigo-barras-duplicado':
        'Ese GTIN ya lo lleva un artículo de esta empresa. Si ahora es de otro, quítalo antes del que lo lleva.',
      'codigo-barras-nivel-no-valido':
        'Un código de barras va en la unidad base, en una caja o en un palé.',
      'codigo-barras-no-encontrado':
        'Ese código de barras ya no está en este artículo: alguien lo ha quitado antes.',
      'codigo-barras-unidades-no-validas':
        'La unidad base lleva una unidad. Una caja o un palé llevan las unidades base que ' +
        'contienen, que son dos o más.',
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
      'ejercicio-con-documentos':
        'El ejercicio tiene documentos con fecha dentro. Borrarlo los dejaría sin ejercicio al que pertenecer.',
      'ejercicio-con-series':
        'No se puede eliminar un ejercicio que tiene series. Elimina antes las series.',
      'ejercicio-dejaria-documentos-fuera':
        'Esas fechas dejarían fuera del ejercicio documentos que hoy caen dentro. Mueve antes los documentos o deja el intervalo donde está.',
      'ejercicio-duplicado': 'Ya hay un ejercicio con ese año en esta empresa.',
      'ejercicio-motivo-no-valido':
        'Escribe por qué reabres el ejercicio: es lo único que quedará para entenderlo.',
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
      'fecha-fuera-del-ejercicio-de-la-serie':
        'Esa serie es de otro ejercicio: no numera documentos con esta fecha. Abre el ajuste con ' +
        'una serie del ejercicio de su fecha.',
      'gtin-circulacion-restringida':
        'Es un número de circulación restringida: lo pone una tienda o un país para su uso ' +
        'interno, y fuera de ahí no identifica un artículo.',
      'gtin-cupon': 'Es el número de un cupón o de un vale de devolución, no el de un artículo.',
      'gtin-digito-de-control':
        'La última cifra no cuadra con las demás: el número está mal leído o mal tecleado.',
      'gtin-largo-no-admitido':
        'Un GTIN tiene 8, 12, 13 o 14 cifras. Si te salen otras, vuelve a mirar el código: falta ' +
        'o sobra alguna.',
      'gtin-medida-variable':
        'Es un código de peso o medida variable: sin la medida no está entero, y no identifica ' +
        'un artículo.',
      'gtin-no-son-digitos':
        'Un GTIN son solo cifras: sin letras, ni guiones, ni espacios por dentro.',
      'gtin-sin-asignar':
        'Ese prefijo está reservado, o es de algo que no es un artículo, así que no es el GTIN ' +
        'de ninguno.',
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
      'numero-de-serie-en-existencias':
        'Algún número de serie del documento ya está en existencias: cada pieza solo puede estar ' +
        'en un sitio, y una sola vez. No se ha guardado nada. Si la pieza ha cambiado de sitio, ' +
        'eso es una reubicación y no una entrada.',
      'orden-no-admitido': 'No se puede ordenar por ese campo.',
      'permisos-de-rol-del-sistema':
        'Los permisos del rol del sistema los fija cada actualización. Puedes cambiarle el nombre; para dar menos permisos, crea un rol propio.',
      'pertenencia-no-encontrada': 'Esa persona no pertenece a la empresa indicada.',
      'recuento-almacen-bloqueado':
        'Ese almacén está bloqueado: sus existencias se siguen leyendo, pero no admite un recuento ' +
        'nuevo. Pide que lo desbloqueen.',
      'recuento-almacen-no-encontrado':
        'Ese almacén no existe. Elige uno del maestro de almacenes.',
      'recuento-articulo-no-encontrado':
        'Ese artículo no existe. Elige uno del maestro de artículos.',
      'recuento-articulo-no-se-almacena':
        'Ese artículo es un servicio: no tiene existencias que contar. Elige un artículo que se ' +
        'almacene.',
      'recuento-clave-repetida':
        'El recuento ya lleva esa clave: la misma ubicación, el mismo artículo y el mismo lote o ' +
        'número de serie. Búscala en las líneas y cuéntala ahí.',
      'recuento-con-lineas-sin-contar':
        'Quedan líneas sin contar, y una línea sin contar no es un cero. Cuéntalas, o quita las que ' +
        'no vayas a contar: su existencia quedará como está.',
      'recuento-contado-no-valido':
        'Lo contado va en la unidad base del artículo: no es negativo, lleva seis decimales como ' +
        'mucho y, en un número de serie, es 0 o 1. Corrige la cifra.',
      'recuento-coste-no-valido':
        'El coste de una unidad no puede ser negativo ni pasar de catorce cifras enteras. ' +
        'Corrígelo, o déjalo vacío si no lo sabes.',
      'recuento-en-ejercicio-cerrado':
        'Hoy cae en un ejercicio cerrado, y lo que el recuento escribe lleva la fecha de hoy: su ' +
        'ajuste al confirmarlo y el inverso al anularlo. Ese periodo ya es definitivo y no admite ' +
        'documentos nuevos. Pide que se reabra el ejercicio.',
      'recuento-linea-no-encontrada':
        'Esa línea ya no está en el recuento: alguien la ha quitado antes. Vuelve a la ficha y actualiza.',
      'recuento-lote-no-valido':
        'Ese lote no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin espacios, ' +
        'con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el lote.',
      'recuento-motivo-no-valido':
        'Escribe el motivo, en 300 caracteres o menos. Es lo único que quedará para entender por ' +
        'qué se contó, se anuló o se descartó.',
      'recuento-no-encontrado': 'Ese recuento ya no existe. Vuelve al listado y actualiza.',
      'recuento-no-esta-confirmado':
        'Ese recuento no está confirmado, y solo se anula uno confirmado: uno en curso no ha movido ' +
        'nada y se descarta, y uno anulado o descartado ya está cerrado. Actualiza la pantalla para ' +
        'ver en qué estado está.',
      'recuento-no-esta-en-curso':
        'Ese recuento ya no está en curso: se confirmó, se anuló o se descartó, y lo contado ya no ' +
        'cambia. Actualiza la pantalla para ver en qué estado está.',
      'recuento-numero-de-serie-no-valido':
        'Ese número de serie no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin ' +
        'espacios, con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el ' +
        'número de serie.',
      'recuento-serie-cerrada':
        'Una de las dos series está cerrada: la del recuento o la del ajuste. Elige otra.',
      'recuento-serie-no-encontrada':
        'Una de las dos series no existe: la del recuento o la del ajuste. Elige una del maestro de series.',
      'recuento-serie-repetida':
        'Ese número de serie ya está en el recuento, en otra ubicación, y cada pieza se cuenta una ' +
        'sola vez. Cuéntala en la línea que ya la lleva, o quita esa línea antes.',
      'recuento-sin-ejercicio':
        'Hoy no cae en ningún ejercicio, y lo que el recuento escribe lleva la fecha de hoy: su ' +
        'ajuste al confirmarlo y el inverso al anularlo. Abre el ejercicio que falta y vuelve a ' +
        'intentarlo.',
      'recuento-sube-con-transito':
        'Hay líneas contadas por encima de lo que dice el sistema en huecos con mercancía en ' +
        'tránsito hacia ellos: si ya ha llegado y no se ha recibido, se sumaría dos veces. Recibe ' +
        'las transferencias y vuelve a confirmar.',
      'recuento-teorico-cambiado':
        'El almacén ha cambiado desde que abriste la ficha: alguien ha movido mercancía de las ' +
        'líneas marcadas. Revísalas, vuelve a contarlas si hace falta y confirma otra vez.',
      'recuento-trazabilidad-no-casa':
        'Esa clave no casa con la trazabilidad del artículo: le falta el lote o el número de serie ' +
        'que pide la ficha, o lleva uno que la ficha no pide. Corrige la clave.',
      'recuento-ubicacion-bloqueada':
        'Esa ubicación está bloqueada: lo que ya hay apuntado a ella se sigue leyendo, pero no se ' +
        'añade nada nuevo en ese hueco. Elige otra ubicación.',
      'recuento-ubicacion-no-encontrada':
        'Esa ubicación no existe en el almacén del recuento. Elige una de las suyas.',
      'recuento-ya-hay-uno-en-curso':
        'Ese almacén ya se está contando. Confirma o descarta el recuento en curso antes de abrir otro.',
      'rol-no-encontrado': 'Ese rol ya no existe. Vuelve al listado y actualiza.',
      'serie-cerrada': 'La serie está cerrada y no admite cambios.',
      'serie-de-otro-documento':
        'Esa serie numera otra clase de documentos. Abre el ajuste con una serie de ajustes de ' +
        'inventario.',
      'serie-duplicada': 'Ya hay una serie con ese código en ese ejercicio.',
      'serie-no-encontrada': 'Esa serie ya no existe. Vuelve al listado y actualiza.',
      'serie-no-numera':
        'Esa serie ya no entrega números: la han cerrado o la han borrado mientras tenías el ' +
        'borrador abierto. Abre el ajuste con otra serie.',
      'serie-ya-numerada': 'La serie ya ha numerado documentos, así que eso no se puede cambiar.',
      'sesion-no-renovable': 'Tu sesión no se ha podido renovar. Vuelve a entrar.',
      'stock-insuficiente':
        'No hay bastante stock en alguna ubicación del documento, y no se ha guardado nada. Si ' +
        'anulas una entrada, sus unidades ya han salido: anula antes esas salidas o registra la ' +
        'entrada que falta.',
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
      // Las veintinueve de la transferencia (ítem 2.11). Las de los maestros son las del ajuste
      // con sus dos mitades; las propias dicen qué punta del viaje falla, porque quien envía y
      // quien recibe suelen ser personas distintas, en almacenes distintos.
      'transferencia-almacen-bloqueado':
        'Ese almacén está bloqueado: sus movimientos anteriores se siguen leyendo y valorando, ' +
        'pero no admite una transferencia nueva, ni de salida ni de llegada. Elige otro almacén o ' +
        'pide que lo desbloqueen.',
      'transferencia-almacen-no-encontrado':
        'Ese almacén no existe. Elige uno del maestro de almacenes.',
      'transferencia-articulo-no-encontrado':
        'Ese artículo no existe. Elige uno del maestro de artículos.',
      'transferencia-articulo-no-se-almacena':
        'Ese artículo es un servicio: no tiene existencias que llevar de un almacén a otro. Quita ' +
        'esa línea o cambia el artículo.',
      'transferencia-cantidad-no-valida':
        'Alguna línea no lleva nada del origen al destino: la cantidad y el factor a unidad base ' +
        'tienen que ser positivos, y su producto no puede redondear a cero. Corrige la cantidad de ' +
        'esa línea.',
      'transferencia-con-fecha-futura':
        'Esa fecha es posterior a hoy: el libro solo recoge lo que ya ha pasado, y una ' +
        'transferencia se envía y se recibe el día en que ocurre. Hazlo ese día o corrige la fecha.',
      'transferencia-en-ejercicio-cerrado':
        'Esa fecha cae en un ejercicio cerrado: ese periodo ya es definitivo y no admite ' +
        'documentos nuevos. Cambia la fecha o pide que se reabra el ejercicio.',
      'transferencia-fecha-anterior-al-ultimo-movimiento':
        'Algún artículo del documento ya tiene un movimiento posterior a esa fecha en uno de los ' +
        'dos almacenes, y un documento no puede ir por detrás del último movimiento de su ' +
        'artículo. Pon esa fecha o una posterior.',
      'transferencia-lote-no-valido':
        'Ese lote no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin espacios, ' +
        'con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el lote de esa ' +
        'línea.',
      'transferencia-mismo-almacen':
        'El origen y el destino son el mismo almacén, y eso no es una transferencia. Cambiar la ' +
        'mercancía de hueco dentro de un almacén es una reubicación, que es otro documento.',
      'transferencia-motivo-no-valido':
        'Escribe por qué anulas la transferencia, en 300 caracteres o menos. Es lo único que ' +
        'quedará para entender la corrección dentro de dos años.',
      'transferencia-no-encontrada':
        'Esa transferencia ya no existe. Vuelve al listado y actualiza.',
      'transferencia-no-esta-en-borrador':
        'Esa transferencia ya no está en borrador: una enviada no se vuelve a enviar, porque lo ' +
        'que salió del origen ya está escrito en el libro. Actualiza la pantalla.',
      'transferencia-no-esta-enviada':
        'Esa transferencia no está en tránsito: un borrador todavía no ha salido del origen, y una ' +
        'recibida o anulada ya no vuela. Actualiza la pantalla para ver en qué estado está.',
      'transferencia-no-se-anula':
        'Esa transferencia no se puede anular: un borrador no ha movido el libro, una anulada ya ' +
        'tiene su inverso, y un inverso no se deshace con otro. Actualiza la pantalla para ver en ' +
        'qué estado está.',
      'transferencia-numero-de-serie-no-valido':
        'Ese número de serie no es un código que quepa en la etiqueta: de 1 a 20 caracteres, sin ' +
        'espacios, con letras sin tilde ni eñe, cifras y los signos que admite GS1. Corrige el ' +
        'número de serie de esa línea.',
      'transferencia-recepcion-antes-del-envio':
        'La fecha de recepción es anterior a la del envío, y nada llega antes de salir. Pon la ' +
        'fecha del envío o una posterior.',
      'transferencia-serie-cerrada':
        'Esa serie está cerrada: sigue resolviendo los documentos que ya numeró, pero no entrega ' +
        'ni un número más. Elige otra serie.',
      'transferencia-serie-no-encontrada': 'Esa serie no existe. Elige una del maestro de series.',
      'transferencia-serie-no-unitaria':
        'Una línea con número de serie mueve exactamente una unidad, porque cada número es una ' +
        'pieza y no hay dos con el mismo. Pon una línea por pieza, con cantidad 1 en la unidad ' +
        'base del artículo.',
      'transferencia-serie-repetida':
        'Dos líneas de la transferencia nombran el mismo número de serie, y cada pieza viaja una ' +
        'sola vez por documento. Quita una de las dos líneas.',
      'transferencia-sin-ejercicio':
        'Esa fecha no cae en ningún ejercicio: sin periodo al que imputarlo, el documento no ' +
        'entraría en ninguna declaración. Abre el ejercicio que falta o corrige la fecha.',
      'transferencia-sin-lineas':
        'Una transferencia necesita al menos una línea: un documento que no mueve nada no lleva ' +
        'nada de un almacén a otro.',
      'transferencia-trazabilidad-no-casa':
        'Alguna línea no casa con la trazabilidad de su artículo: le falta el lote o el número de ' +
        'serie que pide la ficha, o lleva uno que la ficha no pide. Corrige la línea, o la ' +
        'trazabilidad del artículo si todavía no se ha movido.',
      'transferencia-ubicacion-bloqueada':
        'Esa ubicación está bloqueada: lo que ya hay apuntado a ella se sigue leyendo, pero no se ' +
        'mueve nada nuevo desde ese hueco ni hacia él. Elige otra ubicación.',
      'transferencia-ubicacion-no-encontrada':
        'Esa ubicación no existe en ese almacén. Elige una de las suyas.',
      'transferencia-unidad-no-encontrada':
        'Esa unidad de medida no existe. Elige una del maestro de unidades.',
      'transferencia-unidad-retirada':
        'Esa unidad está retirada: los movimientos que ya se escribieron en ella se siguen ' +
        'leyendo, pero no se escribe uno nuevo con ella. Elige otra unidad.',
      'transferencia-valoracion-en-otra-divisa':
        'Las existencias de algún artículo del documento están valoradas en otra divisa en uno de ' +
        'los dos almacenes, y juntarlas pediría un tipo de cambio. La transferencia no se puede ' +
        'hacer mientras ese almacén las tenga valoradas así.',
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
        contarLa: 'Contar la línea {{numero}}',
        corregirLa: 'Corregir lo contado en la línea {{numero}}',
        campoContado: 'Contado en la línea {{numero}}, en {{unidad}}',
        // Una línea sin contar no es un cero (ADR-0055 §6): el cero se escribe.
        escribeLoContado: 'Escribe lo contado; si no hay ninguno, un 0.',
        guardar: 'Guardar',
        guardando: 'Guardando…',
        cancelar: 'Cancelar',
        contada: 'Línea {{numero}} contada.',

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
