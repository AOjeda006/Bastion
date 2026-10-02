/** Los tres sitios donde va impreso un código de barras, en el orden en que se ofrecen. */
export const NIVELES_DE_GTIN = ['base', 'caja', 'palet'] as const;

/** Un nivel que se puede elegir al dar de alta. */
export type NivelElegible = (typeof NIVELES_DE_GTIN)[number];

/**
 * El nivel de un código de barras que ya existe.
 *
 * `desconocido`, por lo mismo que el tipo y la trazabilidad del artículo: el nivel viaja como texto
 * y el frontal se despliega aparte del backend, así que un nivel nuevo llega antes de que esta
 * versión lo conozca.
 */
export type NivelDeGtin = NivelElegible | 'desconocido';

/**
 * Un código de barras de un artículo, como lo enseña la pantalla (ítem 2.10, ADR-0051).
 *
 * El GTIN llega en catorce cifras, con los ceros de relleno delante, que es la forma en que el
 * servidor lo guarda y lo compara. Se enseña así: es el número, y el que va impreso es el mismo sin
 * esos ceros.
 */
export interface CodigoDeBarras {
  readonly id: string;
  readonly gtin: string;
  readonly nivel: NivelDeGtin;
  /** Unidades base que lleva: una en la base, dos o más en una caja o un palé. */
  readonly unidades: number;
}
