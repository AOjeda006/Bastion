/**
 * Un artículo, tal como lo pinta esta funcionalidad. No es el DTO: lo traduce `api/consultas.ts`.
 *
 * La unidad y el impuesto **no están aquí**, y no es un recorte de la pantalla: son
 * identificadores de dos maestros de OTRO módulo —Organización—, y esta funcionalidad no importa
 * de aquélla (`docs/adr/adr-0022`). Pintar un `uuid` en una columna no le dice nada a nadie, así
 * que hasta que haya una pantalla que los resuelva por su cuenta, el listado enseña lo que se lee:
 * el código, la descripción, el tipo y dónde está clasificado.
 */
export interface Articulo {
  readonly id: string;
  readonly codigo: string;
  readonly descripcion: string;
  readonly tipo: TipoDeArticulo;
  readonly trazabilidad: Trazabilidad;
  /** La categoría en la que está clasificado, o nula. Es el identificador, no el nombre. */
  readonly categoriaId: string | null;
}

/**
 * Si es mercancía o prestación.
 *
 * Los dos valores que la API emite hoy, más `desconocido` para lo que llegue mañana. El enumerado
 * **viaja como texto** —eso lo fija el contrato y lo vigila un caso de integración—, y el frontal
 * se despliega aparte del backend: un valor nuevo llega antes de que este fichero lo conozca. Sin
 * el tercer caso la traducción devolvería `undefined` y la celda saldría en blanco, que es lo que
 * se ve cuando algo está roto.
 */
export type TipoDeArticulo = 'bien' | 'servicio' | 'desconocido';

/**
 * Si sus existencias van por lote, por número de serie o sin ninguno de los dos (ADR-0048).
 *
 * Las tres que la API emite hoy, más `desconocida`, por lo mismo que el tipo: viaja como texto y el
 * frontal se despliega aparte.
 */
export type Trazabilidad = 'ninguna' | 'porLote' | 'porNumeroSerie' | 'desconocida';

/** Las tres que se pueden elegir, en el orden en que se ofrecen. */
export const TRAZABILIDADES = ['ninguna', 'porLote', 'porNumeroSerie'] as const;

/** Una trazabilidad que se puede elegir: todas menos `desconocida`, que solo se lee. */
export type TrazabilidadElegible = (typeof TRAZABILIDADES)[number];

/**
 * La ficha de un artículo tal como se leyó, para volver a escribirla.
 *
 * <b>El `PUT` sustituye la ficha entera</b>, así que la pantalla que cambia la trazabilidad manda
 * también lo demás, tal como llegó. Si alguien lo ha cambiado entre medias, la versión ya no es la
 * misma y el servidor contesta `412` en vez de pisarlo. Por eso `version` va con la ficha y no
 * aparte: una ficha sin su versión no se puede guardar.
 */
export interface FichaDeArticulo {
  readonly articulo: Articulo;
  /** El `ETag` de la lectura, que vuelve en el `If-Match`. */
  readonly version: string;
  /**
   * Lo que el `PUT` exige y esta pantalla no toca, en la forma del contrato. El tipo va con su
   * texto y no con el valor traducido: uno `desconocido` se devuelve tal como llegó.
   */
  readonly intacto: {
    readonly descripcion: string;
    readonly tipo: string;
    readonly impuestoPorDefectoId: string;
    readonly categoriaId: string | null;
  };
}

/** Una página de artículos. */
export interface PaginaDeArticulos {
  readonly elementos: readonly Articulo[];
  readonly total: number;
}
