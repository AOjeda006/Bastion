// La primera pantalla del inventario (ítem 2.12, ADR-0055). Las cantidades van en la unidad
// base de cada artículo, y la pantalla lo dice: un recuento contado en cajas cuadraría mal.

/**
 * Los textos de Inventario en castellano, y **la forma** que el inglés tiene que cumplir.
 *
 * **Viajan con las rutas de Inventario, y no en el diccionario del arranque** (ADR-0071).
 * Hasta el 3.1 eran el espacio de nombres `inventario` de `es.ts`. Ahora los trae el enrutador
 * junto con la pantalla, en el idioma activo, y entran en el mismo espacio de nombres:
 * `t('inventario.…')` sigue siendo la misma clave. Por eso solo los lee el código de su
 * funcionalidad: el armazón, o una pantalla de otro módulo, los encontraría sin cargar, y
 * `LosTextosViajanConSusRutas` lo comprueba.
 *
 * Se importan por su TIPO desde donde haga falta, y por su VALOR solo desde `diccionarios.ts` y
 * los tests, como el diccionario: lo vigila el *linter*.
 */
export const inventario = {
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
      sinSeries: 'No hay ninguna serie de ajustes activa. Da una de alta en el maestro de series.',
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
    motivo: 'Motivo',
    ubicacionOrigen: 'Ubicación de origen',
    ubicacionDestino: 'Ubicación de destino',
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
    nueva: 'Nueva transferencia',
    volver: 'Volver a las transferencias',

    // Lo que solo dice el formulario: no haber elegido o no haber escrito, que el servidor nunca
    // llega a ver. Lo que rechaza el servidor dice su texto de `errores.tipos`.
    avisos: {
      eligeElOrigen: 'Elige el almacén del que sale.',
      eligeElDestino: 'Elige el almacén al que llega.',
      eligeLaSerie: 'Elige una serie.',
      escribeElEnvio: 'Escribe el día en que sale.',
      escribeLaRecepcion: 'Escribe el día en que llega.',
      eligeLaUbicacion: 'Elige la ubicación.',
      eligeElArticulo: 'Elige un artículo de la lista.',
      eligeLaUnidad: 'Elige la unidad.',
      escribeLaCantidad: 'Escribe la cantidad.',
      topeDeLineas: 'Una transferencia lleva como mucho 200 líneas.',
    },

    alta: {
      pista:
        'Cada línea sale de una ubicación del origen y entra en una del destino. El número lo ' +
        'recibe al enviarla.',
      serie: 'Serie',
      sinSeries:
        'No hay ninguna serie de transferencias activa. Da una de alta en el maestro de series.',
      tabla: 'Líneas de la transferencia',
      abrir: 'Abrir la transferencia',
      abriendo: 'Abriendo…',
      sinMaestros:
        'Para abrir una transferencia hace falta poder ver los almacenes, las series, las ' +
        'ubicaciones, las unidades y los artículos.',
    },

    ficha: {
      cargando: 'la transferencia',
      anulaA: 'Compensa a',
      verOriginal: 'Ver la transferencia original',
      lineas: 'Líneas',
      tabla: 'Líneas de la transferencia',
      numero: 'Nº',
      articulo: 'Artículo',
      cantidad: 'Cantidad',
      factor: 'Factor de conversión',
      enUnidadBase: 'En unidad base',
      loteOSerie: 'Lote o serie',
      valor: 'Valor',
      // El factor lo pone el servidor (ADR-0068 §2), con la conversión del día del alta.
      nota:
        'El factor y la cantidad en unidad base los puso el servidor al abrirla, con la ' +
        'conversión de ese día.',
      notaDelBorrador: 'El valor se calcula al enviarla.',

      enviar: 'Enviar la transferencia',
      preguntaEnviar: 'Al enviarla recibe su número y la mercancía sale del origen.',
      siEnviar: 'Sí, enviar',
      enviando: 'Enviando…',
      enviadaAviso: 'Transferencia enviada con el número {{numero}}.',
      recibir: 'Recibir la transferencia',
      preguntaRecibir: 'Al recibirla, la mercancía entra en el destino el día que digas.',
      siRecibir: 'Sí, recibir',
      recibiendo: 'Recibiendo…',
      recibidaAviso: 'Transferencia recibida el {{dia}}.',
      anular: 'Anular la transferencia',
      preguntaAnular: 'Anularla crea una transferencia inversa, ya recibida, que la compensa.',
      siAnular: 'Sí, anular',
      anulando: 'Anulando…',
      anuladaAviso: 'Transferencia anulada. La compensa la transferencia {{numero}}.',
      verInverso: 'Ver la inversa',
      descartar: 'Descartar el borrador',
      preguntaDescartar:
        'Descartarlo lo quita del listado. No ha movido nada, y no se puede deshacer.',
      siDescartar: 'Sí, descartar',
      descartando: 'Descartando…',
      descartadaAviso: 'Borrador descartado.',
      cancelar: 'Cancelar',
    },

    estados: {
      borrador: 'Borrador',
      enviada: 'Enviada',
      recibida: 'Recibida',
      anulada: 'Anulada',
      desconocido: 'Sin reconocer',
      desconocidoDetalle: 'Esta versión de la pantalla no conoce ese estado. Recarga la página.',
    },
  },
};

export type TextosDeInventario = typeof inventario;
