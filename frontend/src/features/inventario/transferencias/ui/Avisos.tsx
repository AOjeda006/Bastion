import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import type { ResumenDeTransferencia } from '../model/transferencia.ts';
import { diaLegible } from '@/shared/lib/dias.ts';

/** Lo que la ficha acaba de hacer, o por qué no ha podido. Uno a la vez: el último. */
export type Aviso =
  | { readonly clase: 'enviada'; readonly numero: number | null }
  | { readonly clase: 'recibida'; readonly dia: string }
  | { readonly clase: 'anulada'; readonly inverso: ResumenDeTransferencia }
  | { readonly clase: 'descartada' }
  | { readonly clase: 'fallo'; readonly error: unknown };

/**
 * El aviso de lo último que ha pasado en la ficha.
 *
 * **La región de estado está siempre montada**, vacía si no hay nada que decir: un lector de
 * pantalla no anuncia de forma fiable una región que nace ya con el texto dentro. Un fallo sí nace
 * con él, porque un `alert` se anuncia al aparecer.
 *
 * **El foco viene aquí siempre**: tras cada acción, sus botones cambian o desaparecen, y tras un
 * fallo, lo que se intentaba ya no se ofrece igual. La anulación lleva el enlace al inverso que la
 * compensa, que es el documento que se acaba de crear.
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
    if (aviso !== null) {
      parrafo.current?.focus();
    }
  }, [aviso, numero]);

  const texto = (hecho: Exclude<Aviso, { clase: 'fallo' }>): React.ReactNode => {
    switch (hecho.clase) {
      case 'enviada':
        return t('inventario.transferencias.ficha.enviadaAviso', { numero: hecho.numero });
      case 'recibida':
        return t('inventario.transferencias.ficha.recibidaAviso', {
          dia: diaLegible(hecho.dia, i18n.language),
        });
      case 'anulada':
        return (
          <>
            {t('inventario.transferencias.ficha.anuladaAviso', { numero: hecho.inverso.numero })}{' '}
            <Link to={`/transferencias/${hecho.inverso.id}`} className="underline">
              {t('inventario.transferencias.ficha.verInverso')}
            </Link>
          </>
        );
      case 'descartada':
        return t('inventario.transferencias.ficha.descartadaAviso');
    }
  };

  return (
    <>
      <div role="status">
        {aviso !== null && aviso.clase !== 'fallo' && (
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

      {aviso?.clase === 'fallo' && (
        <p
          key={numero}
          role="alert"
          tabIndex={-1}
          ref={parrafo}
          className="mt-4 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(aviso.error)}
        </p>
      )}
    </>
  );
}
