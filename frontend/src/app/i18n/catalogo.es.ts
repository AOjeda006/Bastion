/**
 * Los textos de Catálogo en castellano, y **la forma** que el inglés tiene que cumplir.
 *
 * **Viajan con las rutas de Catálogo, y no en el diccionario del arranque** (ADR-0071).
 * Hasta el 3.1 eran el espacio de nombres `catalogo` de `es.ts`. Ahora los trae el enrutador
 * junto con la pantalla, en el idioma activo, y entran en el mismo espacio de nombres:
 * `t('catalogo.…')` sigue siendo la misma clave. Por eso solo los lee el código de su
 * funcionalidad: el armazón, o una pantalla de otro módulo, los encontraría sin cargar, y
 * `LosTextosViajanConSusRutas` lo comprueba.
 *
 * Se importan por su TIPO desde donde haga falta, y por su VALOR solo desde `diccionarios.ts` y
 * los tests, como el diccionario: lo vigila el *linter*.
 */
export const catalogo = {
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

    // La retirada (ítem 3.6, ADR-0082). La marca es texto, y no un color: se lee igual sin verlo.
    retirado: 'Retirado',
    retirada: 'Retirada',
    retirar: 'Retirar',
    reincorporar: 'Reincorporar',
    // Por lo mismo que «Cambiar»: veinte botones que se llaman «Retirar» no dicen cuál es cuál.
    retirarElDe: 'Retirar {{codigo}}',
    reincorporarElDe: 'Reincorporar {{codigo}}',
    conRetirados: 'Ver también los retirados',

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
    // Sin los retirados, un listado vacío no dice que no haya ninguno dado de alta.
    ningunoSinContarLosRetirados:
      'Esta empresa no tiene ningún artículo en el catálogo, sin contar los retirados.',
    losRetiradosNoSalen:
      'Los retirados no salen en el listado: para verlos, marca «Ver también los retirados».',

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

    // La ventana de la retirada (ítem 3.6). Lo que pasa se dice antes del botón que lo hace, y
    // con las dos mitades del ADR-0023: lo nuevo no lo elige, y lo de antes sigue su camino.
    retiradaDelArticulo: {
      tituloRetirar: 'Retirar {{codigo}}',
      tituloReincorporar: 'Reincorporar {{codigo}}',
      cargando: 'el artículo',
      articulo: 'Artículo',
      retirarLoNuevo:
        'Deja de ofrecerse para lo nuevo: no entra en un ajuste, una transferencia, una reserva ' +
        'ni una línea de tarifa nuevos.',
      retirarLoDeAntes:
        'Lo que ya lo lleva sigue su camino: los borradores se confirman, las reservas se ' +
        'consumen y sus líneas de tarifa siguen dando su precio.',
      retirarLasExistencias:
        'Lo que le quede en los almacenes sale con un recuento, que sí lo cuenta.',
      retirarElListado: 'Sale del listado, y se ve marcando «Ver también los retirados».',
      reincorporarLoNuevo:
        'Vuelve a ofrecerse para lo nuevo, y a salir en el listado y en los buscadores de las ' +
        'líneas.',
      siRetirar: 'Retirar el artículo',
      siReincorporar: 'Reincorporar el artículo',
      retirando: 'Retirando…',
      reincorporando: 'Reincorporando…',
      cancelar: 'Cancelar',
      recargar: 'Cargar la versión actual',
      retirado: '{{codigo}} queda retirado: ya no se ofrece para lo nuevo.',
      reincorporado: '{{codigo}} vuelve a ofrecerse para lo nuevo.',
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
    tipo: 'Tipo',
    divisa: 'Divisa',
    vigencia: 'Vigencia',
    estado: 'Estado',
    acciones: 'Acciones',

    // El filtro dice por dónde busca. «Buscar» a secas manda a probar con la divisa, que se enseña
    // y no se filtra, o con el precio, que ni se enseña, y a concluir que la tarifa no está.
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

    tipos: {
      venta: 'Venta',
      compra: 'Compra',
      desconocido: 'Sin reconocer',
      desconocidoDetalle:
        'Esta versión de la pantalla no sabe interpretar el tipo de tarifa que ha llegado. Avisa ' +
        'a quien administre Bastion.',
    },

    paginaVacia: 'Esta página no tiene tramos de tarifa. Vuelve a la anterior.',
    ningunaTodavia: 'Todavía no hay ninguna tarifa dada de alta en esta empresa.',
    ningunaConEsteFiltro: 'Ninguna tarifa coincide con «{{filtro}}».',

    // Acotar por un código que no existe devuelve lo mismo que una tarifa recién abierta y
    // todavía sin tramos. Decirlo evita dar de alta una tarifa que ya existe con otro código.
    ningunTramoConEseCodigo: 'Ninguna tarifa de esta empresa tiene el código «{{codigo}}».',
  },
};

export type TextosDeCatalogo = typeof catalogo;
