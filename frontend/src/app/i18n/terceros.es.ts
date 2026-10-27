/**
 * Los textos de Terceros en castellano, y **la forma** que el inglés tiene que cumplir.
 *
 * **Viajan con las rutas de Terceros, y no en el diccionario del arranque** (ADR-0071).
 * Hasta el 3.1 eran el espacio de nombres `terceros` de `es.ts`. Ahora los trae el enrutador
 * junto con la pantalla, en el idioma activo, y entran en el mismo espacio de nombres:
 * `t('terceros.…')` sigue siendo la misma clave. Por eso solo los lee el código de su
 * funcionalidad: el armazón, o una pantalla de otro módulo, los encontraría sin cargar, y
 * `LosTextosViajanConSusRutas` lo comprueba.
 *
 * Se importan por su TIPO desde donde haga falta, y por su VALOR solo desde `diccionarios.ts` y
 * los tests, como el diccionario: lo vigila el *linter*.
 */
export const terceros = {
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
        repetidaEnElFichero: 'Una fila anterior del mismo fichero ya da de alta ese identificador.',
        desconocido:
          'Esta versión de la pantalla no sabe explicar este motivo. Avisa a quien administre Bastion.',
      },
    },
  },
};

export type TextosDeTerceros = typeof terceros;
