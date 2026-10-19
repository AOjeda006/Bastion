import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useForm, useWatch, type UseFormRegisterReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';

import { RejillaDeLineas } from './RejillaDeLineas.tsx';
import { clavesDeMaestros } from '../../maestros/api/claves.ts';
import {
  SERIE_DE_AJUSTES,
  consultarAlmacenes,
  consultarSeries,
  consultarUbicaciones,
  consultarUnidades,
} from '../../maestros/api/maestros.ts';
import { VIDA_DE_UN_MAESTRO } from '../../maestros/ui/nombres.ts';
import { clavesDeAjustes } from '../api/claves.ts';
import { abrirAjuste } from '../api/ajustes.ts';
import {
  LINEA_EN_BLANCO,
  altaDe,
  esAvisoDelFormulario,
  esquemaDelAlta,
  rutaDeLaLinea,
  type DatosDelAlta,
} from '../model/formularios.ts';
import { esRechazoDeCampo, rechazoEnLaLinea } from '../model/rechazos.ts';
import { intentoPara, type Intento } from '@/shared/api/intento.ts';
import { hoyEnElServidor } from '@/shared/lib/dias.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Los maestros que el alta ofrece, cada uno con el permiso de verlo. */
const PERMISOS_DE_LOS_MAESTROS = [
  PERMISOS.almacenVer,
  PERMISOS.serieVer,
  PERMISOS.ubicacionVer,
  PERMISOS.unidadMedidaVer,
  PERMISOS.articuloVer,
];

/**
 * El alta de un ajuste a mano: el almacén, la serie, el día, el motivo y las líneas (ítem 2.17,
 * ADR-0065).
 *
 * **Las opciones salen de sus dueños.** Los almacenes, las series, las ubicaciones y las unidades son
 * de la organización, y los artículos, del catálogo: se preguntan por su API, y hace falta poder
 * verlos. Sin eso, el formulario no se ofrece y lo dice, en vez de enseñar listas vacías que
 * parecerían una empresa sin almacenes.
 *
 * **El factor no se escribe**: lo pone el servidor, con la conversión del día (ADR-0065 §2), y se
 * ve en la ficha. Una unidad sin conversión a la base del artículo vuelve como rechazo, en su
 * campo y con el foco.
 */
export function PaginaDelAlta(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerLosMaestros = PERMISOS_DE_LOS_MAESTROS.every((permiso) => concede(sesion, permiso));

  return (
    <>
      {puedeVerLosMaestros ? (
        <Formulario />
      ) : (
        <p className="mt-4 text-sm text-neutral-600">{t('inventario.ajustes.alta.sinMaestros')}</p>
      )}
      <p className="mt-6 text-sm">
        <Link to="/ajustes" className="underline">
          {t('inventario.ajustes.volver')}
        </Link>
      </p>
    </>
  );
}

function Formulario(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const navegar = useNavigate();
  const [rechazo, setRechazo] = useState<unknown>(null);
  // En una referencia: no se pinta, y el doble clic tiene que verla ya. La clave es obligatoria
  // (ADR-0065 §6), y repetir el mismo envío repite la clave: el servidor devuelve el borrador de la
  // primera vez en vez de abrir otro.
  const intento = useRef<Intento | null>(null);

  const almacenes = useQuery({
    queryKey: clavesDeMaestros.almacenes(),
    queryFn: consultarAlmacenes,
    staleTime: VIDA_DE_UN_MAESTRO,
  });
  const series = useQuery({
    queryKey: clavesDeMaestros.series(),
    queryFn: consultarSeries,
    staleTime: VIDA_DE_UN_MAESTRO,
  });
  const ubicaciones = useQuery({
    queryKey: clavesDeMaestros.ubicaciones(),
    queryFn: consultarUbicaciones,
    staleTime: VIDA_DE_UN_MAESTRO,
  });
  const unidades = useQuery({
    queryKey: clavesDeMaestros.unidades(),
    queryFn: consultarUnidades,
    staleTime: VIDA_DE_UN_MAESTRO,
  });

  const formulario = useForm<DatosDelAlta>({
    resolver: zodResolver(esquemaDelAlta),
    defaultValues: {
      almacenId: '',
      serieId: '',
      fechaDeOperacion: hoyEnElServidor(),
      motivo: '',
      lineas: [LINEA_EN_BLANCO],
    },
  });
  const {
    register,
    handleSubmit,
    setError,
    getValues,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = formulario;
  const almacenId = useWatch({ control, name: 'almacenId' });
  const delAlmacen = (ubicaciones.data ?? []).filter(
    (ubicacion) => ubicacion.almacenId === almacenId,
  );

  const abrir = async (datos: DatosDelAlta): Promise<void> => {
    setRechazo(null);
    const alta = altaDe(datos);
    intento.current = intentoPara(alta, intento.current);

    let id: string;

    try {
      ({ id } = await abrirAjuste(alta, intento.current.clave));
    } catch (error) {
      const enLaLinea = rechazoEnLaLinea(error);

      if (enLaLinea !== null && enLaLinea.indice < datos.lineas.length) {
        setError(
          rutaDeLaLinea(enLaLinea.indice, enLaLinea.campo),
          { type: 'servidor', message: enLaLinea.tipo },
          { shouldFocus: true },
        );
      } else {
        // Lo que no es de una línea va arriba: el almacén, la serie, el día, el motivo y las
        // líneas que faltan o sobran (ADR-0065 §3).
        setRechazo(error);
      }

      return;
    }

    void cache.invalidateQueries({ queryKey: clavesDeAjustes.listas() });
    void navegar(`/ajustes/${id}`);
  };

  const texto = (mensaje: string | undefined): string | null => {
    if (mensaje === undefined) {
      return null;
    }

    if (esRechazoDeCampo(mensaje)) {
      return t(`errores.tipos.${mensaje}`);
    }

    return esAvisoDelFormulario(mensaje) ? t(`inventario.ajustes.alta.${mensaje}`) : mensaje;
  };

  // Un fallo al leer las opciones se dice arriba, como un rechazo: el formulario sigue ahí, y
  // enviarlo sin elegir dice qué falta.
  const falloDeLasOpciones = almacenes.error ?? series.error ?? ubicaciones.error ?? unidades.error;
  const seriesDeAjustes = (series.data ?? []).filter(
    (serie) => serie.activa && serie.tipoDeDocumento === SERIE_DE_AJUSTES,
  );

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(abrir)(evento);
      }}
      aria-label={t('rutas.nuevoAjuste')}
      className="mt-4 space-y-4"
    >
      <p className="text-sm text-neutral-600">{t('inventario.ajustes.alta.pista')}</p>

      {(rechazo ?? falloDeLasOpciones) !== null && (
        <p
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(rechazo ?? falloDeLasOpciones)}
        </p>
      )}

      <div className="grid max-w-md gap-4">
        <Lista
          campo="almacenId"
          etiqueta={t('inventario.ajustes.almacen')}
          error={texto(errors.almacenId?.message)}
          registro={register('almacenId', {
            // Las ubicaciones que se ofrecen son las del almacén: al cambiarlo, la línea que tenía
            // una de otro se queda sin elegir, en vez de mandar una que el servidor rechazaría.
            onChange: () => {
              const elegido = getValues('almacenId');

              getValues('lineas').forEach((linea, indice) => {
                const sigue = (ubicaciones.data ?? []).some(
                  (ubicacion) =>
                    ubicacion.id === linea.ubicacionId && ubicacion.almacenId === elegido,
                );

                if (!sigue && linea.ubicacionId !== '') {
                  setValue(rutaDeLaLinea(indice, 'ubicacionId'), '');
                }
              });
            },
          })}
          opciones={(almacenes.data ?? []).map((almacen) => ({
            id: almacen.id,
            texto:
              almacen.nombre === null ? almacen.codigo : `${almacen.codigo} · ${almacen.nombre}`,
          }))}
        />

        <Lista
          campo="serieId"
          etiqueta={t('inventario.ajustes.alta.serie')}
          error={texto(errors.serieId?.message)}
          registro={register('serieId')}
          opciones={seriesDeAjustes.map((serie) => ({ id: serie.id, texto: serie.codigo }))}
          sinOpciones={series.isSuccess ? t('inventario.ajustes.alta.sinSeries') : null}
        />

        <Campo
          campo="fechaDeOperacion"
          etiqueta={t('inventario.ajustes.fecha')}
          error={texto(errors.fechaDeOperacion?.message)}
        >
          {(propiedades) => (
            <input
              type="date"
              max={hoyEnElServidor()}
              {...propiedades}
              {...register('fechaDeOperacion')}
              className="rounded border border-neutral-300 px-2 py-1.5"
            />
          )}
        </Campo>

        <Campo
          campo="motivo"
          etiqueta={t('inventario.ajustes.motivo')}
          error={texto(errors.motivo?.message)}
        >
          {(propiedades) => (
            <textarea
              rows={2}
              {...propiedades}
              {...register('motivo')}
              className="rounded border border-neutral-300 px-2 py-1.5"
            />
          )}
        </Campo>
      </div>

      <RejillaDeLineas
        formulario={formulario}
        ubicaciones={delAlmacen}
        unidades={unidades.data ?? []}
        hayAlmacen={almacenId !== ''}
        texto={texto}
      />

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting ? t('inventario.ajustes.alta.abriendo') : t('inventario.ajustes.alta.abrir')}
      </button>
    </form>
  );
}

/** Lo que un campo de la cabecera le pasa a su control: el nombre y a qué se refiere su error. */
interface PropiedadesDelCampo {
  readonly id: string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
}

/** Un campo de la cabecera, con su etiqueta encima y su error debajo. */
function Campo({
  campo,
  etiqueta,
  error,
  children,
}: {
  campo: string;
  etiqueta: string;
  error: string | null;
  children: (propiedades: PropiedadesDelCampo) => React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={campo} className="font-medium">
        {etiqueta}
      </label>
      {children({
        id: campo,
        'aria-invalid': error !== null,
        'aria-describedby': error === null ? undefined : `${campo}-error`,
      })}
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}

/**
 * Un desplegable de la cabecera, con su error debajo.
 *
 * La primera opción es «Sin elegir» y no la primera serie: abrir con la que el navegador eligió por
 * su cuenta numeraría el ajuste donde nadie lo pidió.
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
        <option value="">{t('inventario.ajustes.alta.sinElegir')}</option>
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
