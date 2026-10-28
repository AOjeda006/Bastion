import type { TextosDeCatalogo } from './catalogo.es.ts';
import type { TextosDeIdentidad } from './identidad.es.ts';
import type { TextosDeInventario } from './inventario.es.ts';
import type { TextosDeOrganizacion } from './organizacion.es.ts';
import type { TextosDeTerceros } from './terceros.es.ts';

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
 *
 * **Desde el 3.1, este fichero solo lleva el armazón** (ADR-0071). Los espacios de cada
 * funcionalidad viven en `<funcionalidad>.es.ts` y viajan con sus rutas: el arranque espera a este
 * diccionario, y cada documento nuevo que trajera aquí sus textos lo haría crecer para todo el que
 * entra, abra o no esa pantalla. La partición sigue siendo la misma, y `Diccionario`, de abajo, la
 * junta: es la forma contra la que se comprueba `t()`, así que una clave sigue viviendo en un solo
 * sitio y se escribe igual que antes.
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
    exportar: 'Exportar a CSV',
    exportando: 'Preparando el fichero…',
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
    nuevaTransferencia: 'Nueva transferencia',
    transferencia: 'Ficha de la transferencia',
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
};

/**
 * La forma del diccionario del arranque. Todo idioma la cumple ENTERA: una clave de menos o una de
 * más es un error de compilación, no un hueco que se descubre en pantalla.
 */
export type DiccionarioDelArmazon = typeof es;

/**
 * Los textos de cada funcionalidad, que viajan con sus rutas. Una funcionalidad de
 * `FUNCIONALIDADES` sin su entrada aquí no compila: `diccionarios.ts` pide un cargador por cada una,
 * con el tipo que dice esta lista.
 */
export interface TextosDeLasFuncionalidades {
  catalogo: TextosDeCatalogo;
  identidad: TextosDeIdentidad;
  inventario: TextosDeInventario;
  organizacion: TextosDeOrganizacion;
  terceros: TextosDeTerceros;
}

/**
 * El diccionario ENTERO, el del armazón más los textos de todas las funcionalidades: la forma
 * contra la que se comprueba `t()`. Ninguna instancia de la aplicación lo tiene entero de golpe
 * —cada funcionalidad entra con su primera ruta—, pero una clave existe si está aquí.
 */
export type Diccionario = DiccionarioDelArmazon & TextosDeLasFuncionalidades;
