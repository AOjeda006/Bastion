import { useRef } from 'react';
import { flushSync } from 'react-dom';
import { useTranslation } from 'react-i18next';

import { LINEAS_COMO_MUCHO } from '../model/lineas.ts';

/** Lo que una celda hace con una tecla: Intro en la última añade una línea, o pasa a la siguiente. */
export type AlPulsarIntro = (evento: React.KeyboardEvent<HTMLElement>) => void;

/** Una línea del formulario, con la identidad que le da `useFieldArray` y su posición. */
export interface LineaDelMarco {
  readonly indice: number;
  readonly id: string;
}

/** Los controles de la celda de una fila que se pueden usar, en el orden del tabulador. */
const CELDAS_USABLES = 'input:not([disabled]), select:not([disabled])';

/**
 * El marco de las líneas de un alta, para teclado (§10 del plan maestro, ADR-0065 §8, ADR-0068 §8).
 *
 * - **El tabulador** pasa de una celda a la siguiente, en el orden en que se leen.
 * - **Intro en la última celda** de la última línea añade otra, con el foco en su primera celda; en
 *   la de otra línea, pasa a la siguiente. En el resto de celdas no hace nada: una rejilla que
 *   mandara el formulario con cada Intro lo mandaría a medio escribir.
 * - **Una celda deshabilitada no cuenta como la última**: el lote de un artículo que no se sigue por
 *   lote no se puede escribir, y no se puede esperar a que alguien pulse Intro en él.
 *
 * El marco pone la cabecera, el número de cada línea, el botón de quitarla, el de añadir otra y el
 * tope. **Cada documento pone sus columnas**: el ajuste, una ubicación y el coste; la transferencia,
 * una ubicación por punta.
 */
export function MarcoDeLineas({
  titulo,
  columnas,
  tope,
  lineas,
  anadir,
  quitar,
  children: celdas,
}: {
  /** El nombre de la tabla, que lee el lector de pantalla. */
  titulo: string;
  /** Las cabeceras de las columnas del documento, después de la del número. */
  columnas: readonly string[];
  /** Lo que se dice al llegar al tope de líneas. */
  tope: string;
  lineas: readonly { readonly id: string }[];
  /** Añade una línea en blanco al formulario, sin mover el foco: lo mueve el marco. */
  anadir: () => void;
  quitar: (indice: number) => void;
  /** Las celdas de una línea, en el orden de `columnas`. */
  children: (linea: LineaDelMarco, alPulsarIntro: AlPulsarIntro) => React.ReactNode;
}): React.JSX.Element {
  const { t } = useTranslation();
  const llenas = lineas.length >= LINEAS_COMO_MUCHO;
  const cuerpo = useRef<HTMLTableSectionElement>(null);

  // `append` también sabe llevar el foco a la línea nueva, pero desde un efecto, en mitad del
  // pintado: la celda que se deja pierde el foco ahí dentro, y quien escuche su `blur` corre dentro
  // del pintado de otro (en las pruebas, el `change` que emula user-event abría un `act` dentro de
  // otro, y React lo avisaba). Con `flushSync`, la línea está pintada al volver, y el foco se mueve
  // desde el manejador de la tecla o del clic, como lo movería la persona.
  //
  // El cuerpo de la tabla llega del manejador, y no se lee aquí de la referencia: las celdas las
  // pinta cada documento con lo que le da el marco, y el compilador de React no puede saber que no
  // llamarán a Intro mientras pintan.
  const anadirLinea = (cuerpoDeLaTabla: Element | null): void => {
    flushSync(anadir);
    cuerpoDeLaTabla?.lastElementChild?.querySelector<HTMLElement>(CELDAS_USABLES)?.focus();
  };

  const alPulsarIntro = (evento: React.KeyboardEvent<HTMLElement>, indice: number): void => {
    if (evento.key !== 'Enter') {
      return;
    }

    evento.preventDefault();
    const fila = evento.currentTarget.closest('tr');

    if (fila === null) {
      return;
    }

    const usables = fila.querySelectorAll<HTMLElement>(CELDAS_USABLES);

    if (usables[usables.length - 1] !== evento.currentTarget) {
      return;
    }

    if (indice < lineas.length - 1) {
      fila.nextElementSibling?.querySelector<HTMLElement>(CELDAS_USABLES)?.focus();
    } else if (!llenas) {
      anadirLinea(fila.parentElement);
    }
  };

  return (
    <div className="space-y-2">
      <p id="pista-de-la-rejilla" className="text-sm text-neutral-600">
        {t('inventario.lineas.pistaDeLaRejilla')}
      </p>

      {/* La rejilla es más ancha que un móvil: se desplaza ella, y no la página. `relative`
          recorta también lo que va en absoluto, como un `sr-only`, que si no la empuja. */}
      <div className="relative overflow-x-auto">
        <table className="border-collapse text-sm" aria-describedby="pista-de-la-rejilla">
          <caption className="sr-only">{titulo}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              {[t('inventario.lineas.numero'), ...columnas].map((columna) => (
                <th key={columna} scope="col" className="py-2 pr-2 font-medium">
                  {columna}
                </th>
              ))}
              <td />
            </tr>
          </thead>
          <tbody ref={cuerpo}>
            {lineas.map((linea, indice) => (
              <tr key={linea.id} className="border-b border-neutral-200 align-top">
                <th scope="row" className="py-2 pr-2 font-normal">
                  {indice + 1}
                </th>

                {celdas({ indice, id: linea.id }, (evento) => {
                  alPulsarIntro(evento, indice);
                })}

                <td className="py-2">
                  {lineas.length > 1 && (
                    <button
                      type="button"
                      onClick={() => {
                        quitar(indice);
                      }}
                      className="rounded border border-neutral-300 px-2 py-1 text-xs"
                    >
                      {t('inventario.lineas.quitar', { numero: indice + 1 })}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          disabled={llenas}
          aria-describedby={llenas ? 'tope-de-lineas' : undefined}
          onClick={() => {
            anadirLinea(cuerpo.current);
          }}
          className="rounded border border-neutral-300 px-3 py-1.5 text-sm disabled:opacity-50"
        >
          {t('inventario.lineas.anadir')}
        </button>
        {llenas && (
          <p id="tope-de-lineas" className="text-sm text-neutral-600">
            {tope}
          </p>
        )}
      </div>
    </div>
  );
}
