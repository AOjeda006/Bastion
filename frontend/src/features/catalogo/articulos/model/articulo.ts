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

/** Una página de artículos. */
export interface PaginaDeArticulos {
  readonly elementos: readonly Articulo[];
  readonly total: number;
}
