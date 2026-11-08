import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { z } from 'zod';

import { useNombreDeZona } from './nombres.ts';
import { esUnaZona, type ZonaDeAlmacen } from '../model/zona.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { cerrarElDialogo } from '@/shared/ui/cerrarElDialogo.ts';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * El formulario del diálogo de la zona, el mismo para el almacén y para la empresa (ADR-0079 §12).
 *
 * **Avisa antes de los controles**: lo ya fechado no cambia, y la hora que sigue al cambio puede
 * rechazar lo que se feche hoy (§7). El grupo los lleva en su descripción, así que el lector de
 * pantalla los dice al entrar en él.
 *
 * **Guardar lo que ya había no escribe nada**: cierra. Lo demás lo guarda `guardar`, y, si sale
 * bien, el formulario cierra la ventana, que es lo que devuelve el foco a quien la abrió. Si no, el
 * rechazo se dice arriba, y la versión obsoleta ofrece cargar la de ahora (`recargar`).
 */
export function FormularioDeZona<O extends ZonaDeAlmacen>({
  esquema,
  opciones,
  actual,
  otroAviso,
  guardar,
  recargar,
}: {
  /** Lo que se marca es una de las cuatro; lo que sale del esquema, una de las que ofrece. */
  esquema: z.ZodType<{ zona: O }, { zona: ZonaDeAlmacen }>;
  opciones: readonly O[];
  actual: O;
  /** Lo que solo vale para uno de los dos, como lo que arrastra la zona de la empresa. */
  otroAviso?: string;
  guardar: (zona: O) => Promise<void>;
  recargar: () => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const nombreDeZona = useNombreDeZona();
  const avisos = useId();
  const formulario = useRef<HTMLFormElement>(null);
  const [rechazo, setRechazo] = useState<unknown>(null);

  const {
    register,
    handleSubmit,
    formState: { isSubmitting },
  } = useForm<{ zona: ZonaDeAlmacen }, unknown, { zona: O }>({
    resolver: zodResolver(esquema),
    defaultValues: { zona: actual },
  });

  const enviar = async ({ zona }: { zona: O }): Promise<void> => {
    setRechazo(null);

    if (zona !== actual) {
      try {
        await guardar(zona);
      } catch (error) {
        setRechazo(error);
        return;
      }
    }

    if (formulario.current !== null) {
      cerrarElDialogo(formulario.current);
    }
  };

  return (
    <form
      ref={formulario}
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(enviar)(evento);
      }}
      className="mt-4 space-y-4"
    >
      {rechazo !== null && (
        <div
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          <p>{textoDeFallo(rechazo)}</p>
          {tipoDeFallo(rechazo) === 'version-obsoleta' && (
            <button
              type="button"
              onClick={recargar}
              className="mt-2 rounded border border-red-300 bg-white px-3 py-1.5 text-sm"
            >
              {t('organizacion.zonas.recargar')}
            </button>
          )}
        </div>
      )}

      <ul id={avisos} className="list-disc space-y-1 pl-5 text-sm text-neutral-700">
        <li>{t('organizacion.zonas.avisoLoFechado')}</li>
        <li>{t('organizacion.zonas.avisoLaHora')}</li>
        {otroAviso !== undefined && <li>{otroAviso}</li>}
      </ul>

      <fieldset aria-describedby={avisos}>
        <legend className="text-sm font-medium">{t('organizacion.zonas.leyenda')}</legend>
        <div className="mt-2 space-y-1">
          {opciones.map((opcion) => (
            <label key={opcion} className="flex items-center gap-2 text-sm">
              <input type="radio" value={opcion} {...register('zona')} />
              {esUnaZona(opcion) ? nombreDeZona(opcion) : t('organizacion.zonas.laDeLaEmpresa')}
            </label>
          ))}
        </div>
      </fieldset>

      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
        >
          {isSubmitting ? t('organizacion.zonas.guardando') : t('organizacion.zonas.guardar')}
        </button>
        <Cancelar />
      </div>
    </form>
  );
}

/** La salida que no guarda nada. Cierra la ventana, como Escape, y el foco vuelve a quien la abrió. */
export function Cancelar(): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <button
      type="button"
      onClick={(evento) => {
        cerrarElDialogo(evento.currentTarget);
      }}
      className="rounded border border-neutral-300 px-4 py-2 text-sm"
    >
      {t('organizacion.zonas.cancelar')}
    </button>
  );
}
