import { useQuery } from '@tanstack/react-query';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { Estado } from './Estado.tsx';
import { useNombreDeLaTransferencia } from './nombres.ts';
import { clavesDeMaestros } from '../../maestros/api/claves.ts';
import { consultarAlmacenes, type Maestro } from '../../maestros/api/maestros.ts';
import { VIDA_DE_UN_MAESTRO, useNombre } from '../../maestros/ui/nombres.ts';
import { clavesDeTransferencias } from '../api/claves.ts';
import { consultarTransferencias } from '../api/transferencias.ts';
import {
  PARAMETRO_DE_DESTINO,
  PARAMETRO_DE_ESTADO,
  PARAMETRO_DE_ORDEN,
  PARAMETRO_DE_ORIGEN,
  estaAcotado,
  estadoEnElContrato,
  leerListado,
  type ListadoDeTransferencias,
  type Orden,
} from '../model/listado.ts';
import { ESTADOS_DE_TRANSFERENCIA, type ResumenDeTransferencia } from '../model/transferencia.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Los órdenes que se ofrecen, con la clave de su nombre. El primero es el de siempre. */
const ORDENES_CON_NOMBRE: readonly {
  readonly valor: Orden | '';
  readonly clave:
    | 'envioReciente'
    | 'envioAntiguo'
    | 'recepcionReciente'
    | 'recepcionAntigua'
    | 'numeroDescendente'
    | 'numeroAscendente';
}[] = [
  { valor: '', clave: 'envioReciente' },
  { valor: 'fechaDeEnvio', clave: 'envioAntiguo' },
  { valor: '-fechaDeRecepcion', clave: 'recepcionReciente' },
  { valor: 'fechaDeRecepcion', clave: 'recepcionAntigua' },
  { valor: '-numero', clave: 'numeroDescendente' },
  { valor: 'numero', clave: 'numeroAscendente' },
];

/** Lo que el formulario de filtros manda: los cuatro, como texto, y vacío si no se acota. */
interface Filtrado {
  readonly estado: string;
  readonly origen: string;
  readonly destino: string;
  readonly orden: string;
}

/**
 * Las transferencias de la empresa activa: el listado, acotable por estado, por el almacén del que
 * salen y por el que llegan, y ordenable por las dos fechas o por el número (ítem 2.19, ADR-0068).
 *
 * Los filtros viven en la URL. Quien recibe busca lo que llega a su almacén, y quien envía, lo que
 * sale del suyo; los dos juntos dan lo que va de uno a otro.
 */
export function PaginaDeTransferencias(): React.JSX.Element {
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  return (
    <>
      <Filtros
        listado={listado}
        alFiltrar={(filtrado) => {
          // Cambiar un filtro devuelve a la primera página: quedarse en la séptima enseñaría una
          // página vacía de un resultado que sí tiene filas.
          const siguientes = new URLSearchParams(parametros);
          siguientes.delete('pagina');
          poner(siguientes, PARAMETRO_DE_ESTADO, filtrado.estado);
          poner(siguientes, PARAMETRO_DE_ORIGEN, filtrado.origen);
          poner(siguientes, PARAMETRO_DE_DESTINO, filtrado.destino);
          poner(siguientes, PARAMETRO_DE_ORDEN, filtrado.orden);
          setParametros(siguientes);
        }}
      />
      <Contenido listado={listado} alCambiarDePagina={irA} />
    </>
  );
}

/** El listado en sus tres estados: cargando, fallido y con datos o sin ellos. */
function Contenido({
  listado,
  alCambiarDePagina,
}: {
  listado: ListadoDeTransferencias;
  alCambiarDePagina: (pagina: number) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const porQueElEstado = useId();

  const consulta = useQuery({
    queryKey: clavesDeTransferencias.lista(listado),
    queryFn: () => consultarTransferencias(listado),
  });

  if (consulta.isPending) {
    return <Cargando que={t('inventario.transferencias.cargando')} />;
  }

  if (consulta.isError) {
    return (
      <Fallo
        mensaje={textoDeFallo(consulta.error)}
        alReintentar={() => {
          void consulta.refetch();
        }}
      />
    );
  }

  const { elementos, total } = consulta.data;

  if (elementos.length === 0) {
    const vacio = estaAcotado(listado)
      ? t('inventario.transferencias.ningunaConEsteFiltro')
      : listado.pagina > 1
        ? t('inventario.transferencias.paginaVacia')
        : t('inventario.transferencias.ningunaTodavia');

    return (
      <>
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
      </>
    );
  }

  return (
    <>
      {/* Con dos almacenes y dos fechas, la tabla es más ancha que un móvil: se desplaza ella, y
          no la página (WCAG 1.4.10, ADR-0068 §8). */}
      <div className="mt-4 overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{t('inventario.transferencias.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              {(
                ['transferencia', 'origen', 'destino', 'envio', 'recepcion', 'estado'] as const
              ).map((columna) => (
                <th key={columna} scope="col" className="py-2 pr-4 font-medium">
                  {t(`inventario.transferencias.${columna}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {elementos.map((transferencia) => (
              <Fila
                key={transferencia.id}
                transferencia={transferencia}
                porQueElEstado={porQueElEstado}
              />
            ))}
          </tbody>
        </table>
      </div>

      {elementos.some((transferencia) => transferencia.estado === 'desconocido') && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.transferencias.estados.desconocidoDetalle')}
        />
      )}

      <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
    </>
  );
}

function poner(parametros: URLSearchParams, nombre: string, valor: string): void {
  if (valor === '') {
    parametros.delete(nombre);
  } else {
    parametros.set(nombre, valor);
  }
}

function Fila({
  transferencia,
  porQueElEstado,
}: {
  transferencia: ResumenDeTransferencia;
  porQueElEstado: string;
}): React.JSX.Element {
  const { i18n } = useTranslation();
  const nombre = useNombreDeLaTransferencia(transferencia);
  const origen = useNombre('almacen', transferencia.almacenOrigenId);
  const destino = useNombre('almacen', transferencia.almacenDestinoId);
  const idioma = i18n.language;

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4">
        <Link to={`/transferencias/${transferencia.id}`} className="underline">
          {nombre}
        </Link>
      </td>
      <td className="py-2 pr-4">{origen.largo}</td>
      <td className="py-2 pr-4">{destino.largo}</td>
      <td className="py-2 pr-4 whitespace-nowrap">
        {diaLegible(transferencia.fechaDeEnvio, idioma)}
      </td>
      <td className="py-2 pr-4 whitespace-nowrap">
        {transferencia.fechaDeRecepcion === null
          ? ''
          : diaLegible(transferencia.fechaDeRecepcion, idioma)}
      </td>
      <td
        className="py-2 pr-4"
        aria-describedby={transferencia.estado === 'desconocido' ? porQueElEstado : undefined}
      >
        <Estado estado={transferencia.estado} />
      </td>
    </tr>
  );
}

/**
 * El estado, el origen, el destino y el orden, y el botón que los aplica.
 *
 * Los dos almacenes se ofrecen solo a quien puede ver los almacenes, que es de donde salen las
 * opciones. Los campos se vuelven a montar al cambiar la URL: así la flecha de atrás deja escrito el
 * filtro que se está viendo, y no el anterior. El formulario no: el foco se queda en el botón.
 */
function Filtros({
  listado,
  alFiltrar,
}: {
  listado: ListadoDeTransferencias;
  alFiltrar: (filtrado: Filtrado) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerAlmacenes = concede(sesion, PERMISOS.almacenVer);

  const almacenes = useQuery({
    queryKey: clavesDeMaestros.almacenes(),
    queryFn: consultarAlmacenes,
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: puedeVerAlmacenes,
  });

  const estado = listado.estado === null ? '' : estadoEnElContrato(listado.estado);
  const orden = listado.orden ?? '';

  return (
    <form
      role="search"
      className="mt-4 flex flex-wrap items-end gap-2"
      onSubmit={(evento) => {
        evento.preventDefault();
        const datos = new FormData(evento.currentTarget);
        const leer = (nombre: string): string => {
          const valor = datos.get(nombre);

          return typeof valor === 'string' ? valor : '';
        };

        alFiltrar({
          estado: leer(PARAMETRO_DE_ESTADO),
          origen: leer(PARAMETRO_DE_ORIGEN),
          destino: leer(PARAMETRO_DE_DESTINO),
          orden: leer(PARAMETRO_DE_ORDEN),
        });
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.transferencias.estado')}
        <select
          key={estado}
          name={PARAMETRO_DE_ESTADO}
          defaultValue={estado}
          className="rounded border border-neutral-300 px-2 py-1.5"
        >
          <option value="">{t('inventario.transferencias.todas')}</option>
          {ESTADOS_DE_TRANSFERENCIA.map((valor) => (
            <option key={valor} value={estadoEnElContrato(valor)}>
              {t(`inventario.transferencias.estados.${valor}`)}
            </option>
          ))}
        </select>
      </label>

      {puedeVerAlmacenes && (
        <>
          <ListaDeAlmacenes
            etiqueta={t('inventario.transferencias.origen')}
            nombre={PARAMETRO_DE_ORIGEN}
            elegido={listado.origenId ?? ''}
            almacenes={almacenes.data}
          />
          <ListaDeAlmacenes
            etiqueta={t('inventario.transferencias.destino')}
            nombre={PARAMETRO_DE_DESTINO}
            elegido={listado.destinoId ?? ''}
            almacenes={almacenes.data}
          />
        </>
      )}

      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.transferencias.orden')}
        <select
          key={orden}
          name={PARAMETRO_DE_ORDEN}
          defaultValue={orden}
          className="rounded border border-neutral-300 px-2 py-1.5"
        >
          {ORDENES_CON_NOMBRE.map(({ valor, clave }) => (
            <option key={clave} value={valor}>
              {t(`inventario.transferencias.ordenes.${clave}`)}
            </option>
          ))}
        </select>
      </label>

      <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
        {t('inventario.transferencias.filtrar')}
      </button>
    </form>
  );
}

function ListaDeAlmacenes({
  etiqueta,
  nombre,
  elegido,
  almacenes,
}: {
  etiqueta: string;
  nombre: string;
  elegido: string;
  almacenes: readonly Maestro[] | undefined;
}): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <label className="flex flex-col gap-1 text-sm">
      {etiqueta}
      <select
        // Con las opciones dentro de la clave: hasta que llegan, la lista no puede enseñar el
        // almacén de la URL, y montada antes se quedaría en «Todos».
        key={`${elegido}|${String(almacenes?.length ?? 0)}`}
        name={nombre}
        defaultValue={elegido}
        className="rounded border border-neutral-300 px-2 py-1.5"
      >
        <option value="">{t('inventario.transferencias.todos')}</option>
        {almacenes?.map((opcion) => (
          <option key={opcion.id} value={opcion.id}>
            {opcion.nombre === null ? opcion.codigo : `${opcion.codigo} · ${opcion.nombre}`}
          </option>
        ))}
      </select>
    </label>
  );
}
