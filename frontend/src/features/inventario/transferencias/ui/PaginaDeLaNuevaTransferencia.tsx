import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';

import { RejillaDeLaTransferencia } from './RejillaDeLaTransferencia.tsx';
import { CampoDeLaCabecera } from '../../lineas/ui/CampoDeLaCabecera.tsx';
import { ListaDeLaCabecera } from '../../lineas/ui/ListaDeLaCabecera.tsx';
import { textoDeLaOpcion } from '../../lineas/model/lineas.ts';
import { clavesDeMaestros } from '../../maestros/api/claves.ts';
import {
  SERIE_DE_TRANSFERENCIAS,
  consultarAlmacenes,
  consultarSeries,
  consultarUbicaciones,
  consultarUnidades,
} from '../../maestros/api/maestros.ts';
import { VIDA_DE_UN_MAESTRO } from '../../maestros/ui/nombres.ts';
import { clavesDeTransferencias } from '../api/claves.ts';
import { abrirTransferencia } from '../api/transferencias.ts';
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
 * El alta de una transferencia: los dos almacenes, la serie, el día en que sale y las líneas (ítem
 * 2.19, ADR-0068).
 *
 * **Las opciones salen de sus dueños**, como en el ajuste: los almacenes, las series, las
 * ubicaciones y las unidades son de la organización, y los artículos, del catálogo. Sin poder
 * verlos, el formulario no se ofrece y lo dice.
 *
 * **El factor no se escribe**: lo pone el servidor, con la conversión del día (ADR-0068 §2), y se
 * ve en la ficha.
 */
export function PaginaDeLaNuevaTransferencia(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerLosMaestros = PERMISOS_DE_LOS_MAESTROS.every((permiso) => concede(sesion, permiso));

  return (
    <>
      {puedeVerLosMaestros ? (
        <Formulario />
      ) : (
        <p className="mt-4 text-sm text-neutral-600">
          {t('inventario.transferencias.alta.sinMaestros')}
        </p>
      )}
      <p className="mt-6 text-sm">
        <Link to="/transferencias" className="underline">
          {t('inventario.transferencias.volver')}
        </Link>
      </p>
    </>
  );
}

/** El almacén de cada punta, y la celda de la ubicación que va con él. */
const PUNTAS = [
  { almacen: 'almacenOrigenId', ubicacion: 'ubicacionOrigenId' },
  { almacen: 'almacenDestinoId', ubicacion: 'ubicacionDestinoId' },
] as const;

function Formulario(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const navegar = useNavigate();
  const [rechazo, setRechazo] = useState<unknown>(null);
  // En una referencia: no se pinta, y el doble clic tiene que verla ya. La clave es obligatoria, y
  // repetir el mismo envío repite la clave: el servidor devuelve el borrador de la primera vez en
  // vez de abrir otro.
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
      almacenOrigenId: '',
      almacenDestinoId: '',
      serieId: '',
      fechaDeEnvio: hoyEnElServidor(),
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
  const origen = useWatch({ control, name: 'almacenOrigenId' });
  const destino = useWatch({ control, name: 'almacenDestinoId' });
  const delAlmacen = (almacenId: string): NonNullable<typeof ubicaciones.data> =>
    (ubicaciones.data ?? []).filter((ubicacion) => ubicacion.almacenId === almacenId);

  const abrir = async (datos: DatosDelAlta): Promise<void> => {
    setRechazo(null);
    const alta = altaDe(datos);
    intento.current = intentoPara(alta, intento.current);

    let id: string;

    try {
      ({ id } = await abrirTransferencia(alta, intento.current.clave));
    } catch (error) {
      const enLaLinea = rechazoEnLaLinea(error);

      if (enLaLinea !== null && enLaLinea.indice < datos.lineas.length) {
        setError(
          rutaDeLaLinea(enLaLinea.indice, enLaLinea.campo),
          { type: 'servidor', message: enLaLinea.tipo },
          { shouldFocus: true },
        );
      } else {
        // Lo que no es de una línea va arriba: los dos almacenes, la serie, el día y las líneas
        // que faltan o sobran (ADR-0068 §6).
        setRechazo(error);
      }

      return;
    }

    void cache.invalidateQueries({ queryKey: clavesDeTransferencias.listas() });
    void navegar(`/transferencias/${id}`);
  };

  const texto = (mensaje: string | undefined): string | null => {
    if (mensaje === undefined) {
      return null;
    }

    if (esRechazoDeCampo(mensaje)) {
      return t(`errores.tipos.${mensaje}`);
    }

    return esAvisoDelFormulario(mensaje)
      ? t(`inventario.transferencias.avisos.${mensaje}`)
      : mensaje;
  };

  // Las ubicaciones que se ofrecen en cada punta son las de su almacén: al cambiarlo, la línea que
  // tenía una de otro se queda sin elegir en esa punta, en vez de mandar una que el servidor
  // rechazaría. La otra punta no se toca.
  const alCambiarDeAlmacen = (punta: (typeof PUNTAS)[number]) => (): void => {
    const elegido = getValues(punta.almacen);

    getValues('lineas').forEach((linea, indice) => {
      const ubicacionId = linea[punta.ubicacion];
      const sigue = delAlmacen(elegido).some((ubicacion) => ubicacion.id === ubicacionId);

      if (!sigue && ubicacionId !== '') {
        setValue(rutaDeLaLinea(indice, punta.ubicacion), '');
      }
    });
  };

  // Un fallo al leer las opciones se dice arriba, como un rechazo: el formulario sigue ahí, y
  // enviarlo sin elegir dice qué falta.
  const falloDeLasOpciones = almacenes.error ?? series.error ?? ubicaciones.error ?? unidades.error;
  const opcionesDeAlmacen = (almacenes.data ?? []).map((almacen) => ({
    id: almacen.id,
    texto: textoDeLaOpcion(almacen),
  }));
  const seriesDeTransferencias = (series.data ?? []).filter(
    (serie) => serie.activa && serie.tipoDeDocumento === SERIE_DE_TRANSFERENCIAS,
  );

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(abrir)(evento);
      }}
      aria-label={t('rutas.nuevaTransferencia')}
      className="mt-4 space-y-4"
    >
      <p className="text-sm text-neutral-600">{t('inventario.transferencias.alta.pista')}</p>

      {(rechazo ?? falloDeLasOpciones) !== null && (
        <p
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(rechazo ?? falloDeLasOpciones)}
        </p>
      )}

      <div className="grid max-w-md gap-4">
        <ListaDeLaCabecera
          campo="almacenOrigenId"
          etiqueta={t('inventario.transferencias.origen')}
          error={texto(errors.almacenOrigenId?.message)}
          registro={register('almacenOrigenId', { onChange: alCambiarDeAlmacen(PUNTAS[0]) })}
          opciones={opcionesDeAlmacen}
        />

        <ListaDeLaCabecera
          campo="almacenDestinoId"
          etiqueta={t('inventario.transferencias.destino')}
          error={texto(errors.almacenDestinoId?.message)}
          registro={register('almacenDestinoId', { onChange: alCambiarDeAlmacen(PUNTAS[1]) })}
          opciones={opcionesDeAlmacen}
        />

        <ListaDeLaCabecera
          campo="serieId"
          etiqueta={t('inventario.transferencias.alta.serie')}
          error={texto(errors.serieId?.message)}
          registro={register('serieId')}
          opciones={seriesDeTransferencias.map((serie) => ({ id: serie.id, texto: serie.codigo }))}
          sinOpciones={series.isSuccess ? t('inventario.transferencias.alta.sinSeries') : null}
        />

        <CampoDeLaCabecera
          campo="fechaDeEnvio"
          etiqueta={t('inventario.transferencias.envio')}
          error={texto(errors.fechaDeEnvio?.message)}
        >
          {(propiedades) => (
            <input
              type="date"
              max={hoyEnElServidor()}
              {...propiedades}
              {...register('fechaDeEnvio')}
              className="rounded border border-neutral-300 px-2 py-1.5"
            />
          )}
        </CampoDeLaCabecera>
      </div>

      <RejillaDeLaTransferencia
        formulario={formulario}
        ubicaciones={{
          origen: delAlmacen(origen),
          destino: delAlmacen(destino),
          hayOrigen: origen !== '',
          hayDestino: destino !== '',
        }}
        unidades={unidades.data ?? []}
        texto={texto}
      />

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting
          ? t('inventario.transferencias.alta.abriendo')
          : t('inventario.transferencias.alta.abrir')}
      </button>
    </form>
  );
}
