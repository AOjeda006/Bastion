import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useForm, type UseFormRegisterReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { VIDA_DE_UN_MAESTRO } from './nombres.ts';
import { clavesDeRecuentos } from '../api/claves.ts';
import {
  SERIE_DE_AJUSTES,
  SERIE_DE_RECUENTOS,
  consultarAlmacenes,
  consultarSeries,
  type Serie,
} from '../api/maestros.ts';
import { abrirRecuento } from '../api/recuentos.ts';
import {
  SIN_ALMACEN,
  SIN_SERIE,
  esquemaDeApertura,
  type DatosDeApertura,
} from '../model/formularios.ts';
import { RECHAZO_DEL_MOTIVO, esDelAlmacen, esRechazoDeCampo } from '../model/rechazos.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { intentoPara, type Intento } from '@/shared/api/intento.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * El alta de un recuento: el almacén entero, las dos series y el motivo (ADR-0055 §1).
 *
 * <b>Las opciones salen de sus dueños.</b> Los almacenes y las series son de la organización, y se
 * preguntan por su API: hace falta poder verlos. Sin eso, el formulario no se ofrece y lo dice, en
 * vez de enseñar dos listas vacías que parecerían una empresa sin almacenes.
 *
 * <b>El rechazo que es de un campo va en ese campo.</b> El almacén que ya se está contando, el
 * bloqueado o el que ya no existe van en el almacén, con el foco; el motivo, en el motivo. El resto
 * —una serie cerrada o sin ejercicio—, arriba.
 */
export function FormularioDeApertura(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerLosMaestros =
    concede(sesion, PERMISOS.almacenVer) && concede(sesion, PERMISOS.serieVer);

  return (
    <section aria-labelledby="apertura-de-recuento" className="mt-8 max-w-md space-y-4">
      <h2 id="apertura-de-recuento" className="text-lg font-semibold">
        {t('inventario.recuentos.apertura.titulo')}
      </h2>
      {puedeVerLosMaestros ? (
        <Formulario />
      ) : (
        <p className="text-sm text-neutral-600">{t('inventario.recuentos.apertura.sinMaestros')}</p>
      )}
    </section>
  );
}

function Formulario(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const navegar = useNavigate();
  const [rechazo, setRechazo] = useState<unknown>(null);
  // En una referencia: no se pinta, y el doble clic tiene que verla ya. Si el alta sale bien, la
  // pantalla se va a la ficha y el formulario se desmonta con ella.
  const intento = useRef<Intento | null>(null);

  const almacenes = useQuery({
    queryKey: clavesDeRecuentos.almacenes(),
    queryFn: consultarAlmacenes,
    staleTime: VIDA_DE_UN_MAESTRO,
  });

  const series = useQuery({
    queryKey: clavesDeRecuentos.series(),
    queryFn: consultarSeries,
    staleTime: VIDA_DE_UN_MAESTRO,
  });

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeApertura>({
    resolver: zodResolver(esquemaDeApertura),
    defaultValues: { almacenId: '', serieId: '', serieDelAjusteId: '', motivo: '' },
  });

  const abrir = async (datos: DatosDeApertura): Promise<void> => {
    setRechazo(null);
    intento.current = intentoPara(datos, intento.current);

    let id: string;

    try {
      ({ id } = await abrirRecuento(datos, intento.current.clave));
    } catch (error) {
      const tipo = tipoDeFallo(error);

      if (tipo !== null && esDelAlmacen(tipo)) {
        setError('almacenId', { type: 'servidor', message: tipo }, { shouldFocus: true });
      } else if (tipo === RECHAZO_DEL_MOTIVO) {
        setError('motivo', { type: 'servidor', message: tipo }, { shouldFocus: true });
      } else {
        setRechazo(error);
      }

      return;
    }

    void cache.invalidateQueries({ queryKey: clavesDeRecuentos.listas() });
    void navegar(`/recuentos/${id}`);
  };

  const texto = (mensaje: string | undefined): string | null => {
    if (mensaje === undefined) {
      return null;
    }

    if (esRechazoDeCampo(mensaje)) {
      return t(`errores.tipos.${mensaje}`);
    }

    return mensaje === SIN_ALMACEN || mensaje === SIN_SERIE
      ? t(`inventario.recuentos.apertura.${mensaje}`)
      : mensaje;
  };

  // Un fallo al leer las opciones se dice arriba, como un rechazo: el formulario sigue ahí, y
  // enviarlo sin elegir dice qué falta.
  const falloDeLasOpciones = almacenes.error ?? series.error;
  const delTipo = (tipo: string): Serie[] =>
    (series.data ?? []).filter((serie) => serie.activa && serie.tipoDeDocumento === tipo);

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(abrir)(evento);
      }}
      aria-labelledby="apertura-de-recuento"
      className="space-y-4"
    >
      <p className="text-sm text-neutral-600">{t('inventario.recuentos.apertura.pista')}</p>

      {(rechazo ?? falloDeLasOpciones) !== null && (
        <p
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(rechazo ?? falloDeLasOpciones)}
        </p>
      )}

      <Lista
        campo="almacenId"
        etiqueta={t('inventario.recuentos.almacen')}
        error={texto(errors.almacenId?.message)}
        registro={register('almacenId')}
        opciones={(almacenes.data ?? []).map((almacen) => ({
          id: almacen.id,
          texto: almacen.nombre === null ? almacen.codigo : `${almacen.codigo} · ${almacen.nombre}`,
        }))}
      />

      <Lista
        campo="serieId"
        etiqueta={t('inventario.recuentos.apertura.serie')}
        error={texto(errors.serieId?.message)}
        registro={register('serieId')}
        opciones={delTipo(SERIE_DE_RECUENTOS).map((serie) => ({
          id: serie.id,
          texto: serie.codigo,
        }))}
        sinOpciones={series.isSuccess ? t('inventario.recuentos.apertura.sinSeries') : null}
      />

      <Lista
        campo="serieDelAjusteId"
        etiqueta={t('inventario.recuentos.apertura.serieDelAjuste')}
        error={texto(errors.serieDelAjusteId?.message)}
        registro={register('serieDelAjusteId')}
        opciones={delTipo(SERIE_DE_AJUSTES).map((serie) => ({ id: serie.id, texto: serie.codigo }))}
        sinOpciones={series.isSuccess ? t('inventario.recuentos.apertura.sinSeries') : null}
      />

      <Texto
        campo="motivo"
        etiqueta={t('inventario.recuentos.motivo')}
        error={texto(errors.motivo?.message)}
        registro={register('motivo')}
      />

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting
          ? t('inventario.recuentos.apertura.abriendo')
          : t('inventario.recuentos.apertura.abrir')}
      </button>
    </form>
  );
}

/**
 * Un desplegable del alta, con su error debajo.
 *
 * La primera opción es «Sin elegir» y no la primera serie: abrir con la serie que el navegador eligió
 * por su cuenta numeraría el recuento donde nadie lo pidió.
 */
function Lista({
  campo,
  etiqueta,
  error,
  registro,
  opciones,
  sinOpciones = null,
}: {
  campo: string;
  etiqueta: string;
  error: string | null;
  registro: UseFormRegisterReturn;
  opciones: readonly { id: string; texto: string }[];
  /** Lo que se dice si la lista llega vacía, o nulo si no hay nada que decir. */
  sinOpciones?: string | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const vacia = sinOpciones !== null && opciones.length === 0;
  const descripciones = [vacia && `${campo}-vacia`, error !== null && `${campo}-error`]
    .filter(Boolean)
    .join(' ');

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={campo} className="font-medium">
        {etiqueta}
      </label>
      <select
        id={campo}
        aria-invalid={error !== null}
        aria-describedby={descripciones === '' ? undefined : descripciones}
        {...registro}
        className="rounded border border-neutral-300 px-2 py-1.5"
      >
        <option value="">{t('inventario.recuentos.apertura.sinElegir')}</option>
        {opciones.map((opcion) => (
          <option key={opcion.id} value={opcion.id}>
            {opcion.texto}
          </option>
        ))}
      </select>
      {vacia && (
        <p id={`${campo}-vacia`} className="text-neutral-600">
          {sinOpciones}
        </p>
      )}
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}

function Texto({
  campo,
  etiqueta,
  error,
  registro,
}: {
  campo: string;
  etiqueta: string;
  error: string | null;
  registro: UseFormRegisterReturn;
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={campo} className="font-medium">
        {etiqueta}
      </label>
      <textarea
        id={campo}
        rows={2}
        aria-invalid={error !== null}
        aria-describedby={error === null ? undefined : `${campo}-error`}
        {...registro}
        className="rounded border border-neutral-300 px-2 py-1.5"
      />
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}
