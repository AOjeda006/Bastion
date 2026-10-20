import { useState } from 'react';

import type { Trazabilidad } from '../../maestros/api/maestros.ts';

/** La trazabilidad del artículo de cada línea, y cómo anotarla al elegir otro. */
export interface MarcasDeLasLineas {
  /** La de la línea, o nula si no tiene artículo. */
  readonly de: (linea: string) => Trazabilidad | null;
  readonly anotar: (linea: string, marca: Trazabilidad | null) => void;
}

/**
 * La trazabilidad del artículo elegido en cada línea, que dice si su lote y su serie se escriben.
 *
 * Va **por la identidad de la línea y no por su posición**: quitar una no le cambia la marca a las
 * de debajo. No es un dato del formulario, porque no viaja: es lo que el buscador sabe del artículo
 * que se eligió.
 */
export function useMarcasDeLasLineas(): MarcasDeLasLineas {
  const [marcas, setMarcas] = useState<Readonly<Record<string, Trazabilidad | null>>>({});

  return {
    de: (linea) => marcas[linea] ?? null,
    anotar: (linea, marca) => {
      setMarcas((anteriores) => ({ ...anteriores, [linea]: marca }));
    },
  };
}
