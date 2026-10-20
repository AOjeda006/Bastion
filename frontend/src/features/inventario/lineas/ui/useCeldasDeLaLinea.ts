import { useTranslation } from 'react-i18next';

import type { AlPulsarIntro } from './MarcoDeLineas.tsx';

/** Lo que comparten los controles de las celdas: su nombre, su error y lo que hace Intro. */
export interface PropiedadesDeCelda {
  readonly id: string;
  readonly 'aria-label': string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
  readonly onKeyDown: (evento: React.KeyboardEvent<HTMLElement>) => void;
}

/** Una celda de una línea: las propiedades de su control y el error que se pinta debajo. */
export interface CeldaDeLaLinea {
  readonly propiedades: PropiedadesDeCelda;
  readonly error: string | null;
}

/**
 * Las celdas de la línea `indice`, una por campo.
 *
 * Cada una lleva su nombre entero, con la línea («Cantidad de la línea 2»), porque un lector de
 * pantalla que entra en ella no lee la cabecera de la columna. Y su error debajo, que es donde lo
 * pone también el rechazo del servidor (ADR-0065 §3).
 */
export function useCeldasDeLaLinea(
  indice: number,
  alPulsarIntro: AlPulsarIntro,
): (campo: string, columna: string, error: string | null) => CeldaDeLaLinea {
  const { t } = useTranslation();
  const numero = indice + 1;

  return (campo, columna, error) => {
    const id = `linea-${String(indice)}-${campo}`;

    return {
      propiedades: {
        id,
        'aria-label': t('inventario.lineas.celda', { campo: columna, numero }),
        'aria-invalid': error !== null,
        'aria-describedby': error === null ? undefined : `${id}-error`,
        onKeyDown: alPulsarIntro,
      },
      error,
    };
  };
}
