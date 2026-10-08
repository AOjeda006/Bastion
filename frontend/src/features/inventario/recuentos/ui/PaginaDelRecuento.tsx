import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams, useSearchParams } from 'react-router';

import { AccionesDelRecuento } from './AccionesDelRecuento.tsx';
import { Avisos, type Aviso } from './Avisos.tsx';
import { Estado } from './Estado.tsx';
import { LineasDelRecuento } from './LineasDelRecuento.tsx';
import { useNombre, useNombreDelRecuento } from './nombres.ts';
import { clavesDeRecuentos } from '../api/claves.ts';
import { consultarRecuento } from '../api/recuentos.ts';
import {
  PARAMETRO_DE_VISTA,
  leerLineas,
  type ListadoDeLineas,
  type VistaDeLineas,
} from '../model/listado.ts';
import type { Recuento } from '../model/recuento.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Las tres vistas de las líneas, con la clave de su nombre. */
const VISTAS: readonly {
  readonly vista: VistaDeLineas | null;
  readonly clave: 'todas' | 'sinContar' | 'teoricoCambiado';
}[] = [
  { vista: null, clave: 'todas' },
  { vista: 'sin-contar', clave: 'sinContar' },
  { vista: 'teorico-cambiado', clave: 'teoricoCambiado' },
];

/**
 * La ficha de un recuento: su cabecera, sus líneas y lo que se puede hacer con él (ítem 2.12,
 * ADR-0055).
 *
 * <b>Ver es de su permiso; contar, confirmar, anular y descartar, de los suyos.</b> Quien solo ve
 * lee la ficha entera y no encuentra ni un botón. La interfaz esconde, el servidor autoriza.
 *
 * <b>La vista y la página de las líneas viven en la URL</b>, con el nombre de la API (`?solo=`).
 * Un rechazo de la confirmación lleva a la vista que lo explica, y la flecha de atrás vuelve.
 *
 * <b>Lo que ya se enseñaba no se retira al volver a leerlo.</b> Tras contar o cerrar, la ficha se
 * lee otra vez; si esa lectura falla, la ficha y el aviso se quedan, con el fallo encima.
 */
export function PaginaDelRecuento(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const sesion = useSesionAbierta();
  const cache = useQueryClient();
  const { id = '' } = useParams();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerLineas(parametros, leerPaginacion(parametros));
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [numeroDeAviso, setNumeroDeAviso] = useState(0);

  const ficha = useQuery({
    queryKey: clavesDeRecuentos.ficha(id),
    queryFn: () => consultarRecuento(id),
  });

  const avisar = (siguiente: Aviso): void => {
    setAviso(siguiente);
    setNumeroDeAviso((numero) => numero + 1);
  };

  // La ficha y sus líneas, y los listados, que enseñan su estado. No los nombres: no han cambiado.
  const releer = async (): Promise<void> => {
    await Promise.all([
      cache.invalidateQueries({ queryKey: clavesDeRecuentos.una(id) }),
      cache.invalidateQueries({ queryKey: clavesDeRecuentos.listas() }),
    ]);
  };

  const conParametros = (cambiar: (siguientes: URLSearchParams) => void): URLSearchParams => {
    const siguientes = new URLSearchParams(parametros);
    cambiar(siguientes);

    return siguientes;
  };

  const deLaVista = (vista: VistaDeLineas | null): URLSearchParams =>
    conParametros((siguientes) => {
      siguientes.delete('pagina');

      if (vista === null) {
        siguientes.delete(PARAMETRO_DE_VISTA);
      } else {
        siguientes.set(PARAMETRO_DE_VISTA, vista);
      }
    });

  const volver = (
    <p className="mt-6 text-sm">
      <Link to="/recuentos" className="underline">
        {t('inventario.recuentos.ficha.volver')}
      </Link>
    </p>
  );

  if (ficha.isPending) {
    return (
      <>
        <Cargando que={t('inventario.recuentos.ficha.cargando')} />
        {volver}
      </>
    );
  }

  const fallo = ficha.isError ? (
    <Fallo
      mensaje={textoDeFallo(ficha.error)}
      alReintentar={() => {
        void ficha.refetch();
      }}
    />
  ) : null;

  if (ficha.data === undefined) {
    return (
      <>
        {fallo}
        {volver}
      </>
    );
  }

  const { recuento } = ficha.data;
  const enCurso = recuento.estado === 'enCurso';
  // Las vistas parciales son de un recuento en curso: cerrado, el teórico ya no se mueve y no queda
  // nada por contar. Un enlace viejo con `?solo=` enseña todas las líneas.
  const deLasLineas: ListadoDeLineas = enCurso ? listado : { ...listado, solo: null };

  return (
    <>
      <Cabecera recuento={recuento} />

      {/* Con datos, el fallo de volver a leer va encima y no en su lugar. */}
      {fallo}

      <Avisos aviso={aviso} numero={numeroDeAviso} textoDeFallo={textoDeFallo} />

      <h3 className="mt-6 text-base font-semibold">{t('inventario.recuentos.ficha.lineas')}</h3>

      {enCurso && <Vistas listado={listado} deLaVista={deLaVista} />}

      <LineasDelRecuento
        recuento={recuento}
        listado={deLasLineas}
        puedeContar={concede(sesion, PERMISOS.recuentoContar)}
        alCambiarDePagina={(pagina) => {
          setParametros(
            conParametros((siguientes) => {
              siguientes.set('pagina', String(pagina));
            }),
          );
        }}
        alContar={async (numero, sale) => {
          avisar({ clase: 'contada', numero, enfocar: sale });
          await releer();
        }}
        alContarOtraPersona={(numero, contado, unidad) => {
          avisar({ clase: 'contadaPorOtraPersona', numero, contado, unidad });
          void releer();
        }}
        alFallar={(error) => {
          avisar({ clase: 'fallo', error });
          void releer();
        }}
      />

      <AccionesDelRecuento
        ficha={ficha.data}
        ocupada={ficha.isFetching}
        alHacer={async (clase, hecho) => {
          avisar(clase === 'confirmado' ? { clase, numero: hecho.numero } : { clase });
          // Cerrado, se enseñan todas sus líneas desde la primera página.
          setParametros(deLaVista(null));
          await releer();
        }}
        alFallar={async (error) => {
          avisar({ clase: 'fallo', error });
          await releer();
        }}
        alIrALaVista={(vista) => {
          setParametros(deLaVista(vista));
        }}
      />

      {volver}
    </>
  );
}

/** El nombre, el estado, el almacén, las fechas, los motivos y las cuentas de las líneas. */
function Cabecera({ recuento }: { recuento: Recuento }): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const nombre = useNombreDelRecuento(recuento);
  const almacen = useNombre('almacen', recuento.almacenId);
  const porQueElEstado = useId();

  const dato = (termino: string, valor: React.ReactNode, describe?: string): React.JSX.Element => (
    <>
      <dt className="font-medium">{termino}</dt>
      <dd aria-describedby={describe}>{valor}</dd>
    </>
  );

  const cerrado = recuento.estado === 'confirmado' || recuento.estado === 'anulado';

  return (
    <>
      <h2 className="mt-4 text-lg font-semibold">{nombre}</h2>

      <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        {dato(
          t('inventario.recuentos.estado'),
          <Estado estado={recuento.estado} />,
          recuento.estado === 'desconocido' ? porQueElEstado : undefined,
        )}
        {dato(t('inventario.recuentos.almacen'), almacen.largo)}
        {dato(
          t('inventario.recuentos.abierto'),
          diaLegible(recuento.fechaDeApertura, i18n.language),
        )}
        {recuento.fechaDeConfirmacion !== null &&
          dato(
            t('inventario.recuentos.confirmado'),
            diaLegible(recuento.fechaDeConfirmacion, i18n.language),
          )}
        {dato(t('inventario.recuentos.motivo'), recuento.motivo)}
        {recuento.motivoDeLaAnulacion !== null &&
          dato(t('inventario.recuentos.ficha.motivoDeLaAnulacion'), recuento.motivoDeLaAnulacion)}
        {recuento.motivoDelDescarte !== null &&
          dato(t('inventario.recuentos.ficha.motivoDelDescarte'), recuento.motivoDelDescarte)}
        {dato(
          t('inventario.recuentos.ficha.lineas'),
          recuento.estado === 'enCurso'
            ? t('inventario.recuentos.ficha.cuentasEnCurso', {
                lineas: recuento.lineas,
                sinContar: recuento.lineasSinContar,
                cambiadas: recuento.lineasConElTeoricoCambiado ?? 0,
                transito: recuento.lineasConTransito ?? 0,
              })
            : t('inventario.recuentos.ficha.cuentas', {
                lineas: recuento.lineas,
                sinContar: recuento.lineasSinContar,
              }),
        )}
        {cerrado &&
          dato(
            t('inventario.recuentos.ficha.ajuste'),
            recuento.ajusteId === null
              ? t('inventario.recuentos.ficha.sinAjuste')
              : t('inventario.recuentos.ficha.conAjuste'),
          )}
      </dl>

      {recuento.estado === 'desconocido' && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.recuentos.estados.desconocidoDetalle')}
        />
      )}

      <Nota recuento={recuento} />
    </>
  );
}

/** Qué quieren decir el teórico y la diferencia en el estado en que está. */
function Nota({ recuento }: { recuento: Recuento }): React.JSX.Element | null {
  const { t } = useTranslation();

  const nota =
    recuento.estado === 'enCurso'
      ? t('inventario.recuentos.ficha.notaEnCurso')
      : recuento.estado === 'confirmado' || recuento.estado === 'anulado'
        ? t('inventario.recuentos.ficha.notaCerrado')
        : recuento.estado === 'descartado'
          ? t('inventario.recuentos.ficha.notaDescartado')
          : null;

  return nota === null ? null : <p className="mt-3 text-sm text-neutral-700">{nota}</p>;
}

/** Todas, las que faltan por contar y las del teórico cambiado. La que se mira lo dice. */
function Vistas({
  listado,
  deLaVista,
}: {
  listado: ListadoDeLineas;
  deLaVista: (vista: VistaDeLineas | null) => URLSearchParams;
}): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <nav aria-label={t('inventario.recuentos.ficha.vistas')} className="mt-2">
      <ul className="flex flex-wrap gap-3 text-sm">
        {VISTAS.map(({ vista, clave }) => {
          const busqueda = deLaVista(vista).toString();

          return (
            <li key={clave}>
              <Link
                to={{ search: busqueda === '' ? '' : `?${busqueda}` }}
                aria-current={listado.solo === vista ? 'page' : undefined}
                className="underline aria-[current=page]:font-semibold aria-[current=page]:no-underline"
              >
                {t(`inventario.recuentos.ficha.${clave}`)}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
