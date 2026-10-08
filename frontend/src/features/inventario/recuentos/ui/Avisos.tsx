import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

import { cantidadLegible } from '../model/cantidad.ts';

/** Lo que la ficha acaba de hacer, o por qué no ha podido. Uno a la vez: el último. */
export type Aviso =
  | {
      readonly clase: 'contada';
      readonly numero: number;
      /** Si el foco viene aquí: cuando la fila contada va a salir de la vista que se mira. */
      readonly enfocar: boolean;
    }
  | { readonly clase: 'confirmado'; readonly numero: number | null }
  | { readonly clase: 'anulado' | 'descartado' }
  | {
      /** El `412` de contar: otra persona contó la línea con el campo abierto, y esto contó. */
      readonly clase: 'contadaPorOtraPersona';
      readonly numero: number;
      readonly contado: number;
      readonly unidad: string;
    }
  | { readonly clase: 'fallo'; readonly error: unknown };

/** Las dos que dicen que lo intentado no se hizo: son un `alert`. */
type Rechazo = Extract<Aviso, { readonly clase: 'fallo' | 'contadaPorOtraPersona' }>;

function esRechazo(aviso: Aviso): aviso is Rechazo {
  return aviso.clase === 'fallo' || aviso.clase === 'contadaPorOtraPersona';
}

/**
 * El aviso de lo último que ha pasado en la ficha.
 *
 * <b>La región de estado está siempre montada</b>, vacía si no hay nada que decir: un lector de
 * pantalla no anuncia de forma fiable una región que nace ya con el texto dentro. Lo que no se pudo
 * hacer —un fallo, o lo contado que otra persona guardó antes— sí nace con él, porque un `alert` se
 * anuncia al aparecer.
 *
 * <b>El foco viene aquí cuando el control que lo tenía desaparece</b>: tras confirmar, anular o
 * descartar, los botones de la acción cambian; tras un fallo, lo que se intentaba ya no se ofrece
 * igual. Tras contar no, salvo que la fila vaya a salir de la vista: lo normal es contar varias
 * seguidas, y el foco vuelve a la fila.
 */
export function Avisos({
  aviso,
  numero,
  textoDeFallo,
}: {
  aviso: Aviso | null;
  /** Cuántos avisos ha habido: cada uno monta su párrafo, aunque repita el texto del anterior. */
  numero: number;
  textoDeFallo: (error: unknown) => string;
}): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const parrafo = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    if (aviso !== null && (aviso.clase !== 'contada' || aviso.enfocar)) {
      parrafo.current?.focus();
    }
  }, [aviso, numero]);

  const rechazo = (no: Rechazo): string =>
    no.clase === 'fallo'
      ? textoDeFallo(no.error)
      : t('inventario.recuentos.ficha.contadaPorOtraPersona', {
          numero: no.numero,
          contado: cantidadLegible(no.contado, i18n.language),
          unidad: no.unidad,
        });

  const texto = (hecho: Exclude<Aviso, Rechazo>): string => {
    switch (hecho.clase) {
      case 'contada':
        return t('inventario.recuentos.ficha.contada', { numero: hecho.numero });
      case 'confirmado':
        return t('inventario.recuentos.ficha.confirmadoAviso', { numero: hecho.numero });
      case 'anulado':
        return t('inventario.recuentos.ficha.anuladoAviso');
      case 'descartado':
        return t('inventario.recuentos.ficha.descartadoAviso');
    }
  };

  return (
    <>
      <div role="status">
        {aviso !== null && !esRechazo(aviso) && (
          <p
            key={numero}
            tabIndex={-1}
            ref={parrafo}
            className="mt-4 rounded border border-green-300 bg-green-50 p-3 text-sm text-green-900"
          >
            {texto(aviso)}
          </p>
        )}
      </div>

      {aviso !== null && esRechazo(aviso) && (
        <p
          key={numero}
          role="alert"
          tabIndex={-1}
          ref={parrafo}
          className="mt-4 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {rechazo(aviso)}
        </p>
      )}
    </>
  );
}
