import { useQuery } from '@tanstack/react-query';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { Estado } from './Estado.tsx';
import { FormularioDeApertura } from './FormularioDeApertura.tsx';
import { VIDA_DE_UN_MAESTRO, useNombre, useNombreDelRecuento } from './nombres.ts';
import { clavesDeRecuentos } from '../api/claves.ts';
import { consultarAlmacenes } from '../api/maestros.ts';
import { consultarRecuentos } from '../api/recuentos.ts';
import {
  PARAMETRO_DE_ALMACEN,
  PARAMETRO_DE_ESTADO,
  estadoEnElContrato,
  leerListado,
  type ListadoDeRecuentos,
} from '../model/listado.ts';
import { ESTADOS_DE_RECUENTO, type ResumenDeRecuento } from '../model/recuento.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Los recuentos de la empresa activa: el listado, acotable por estado y por almacén, y el alta
 * (ítem 2.12, ADR-0055).
 *
 * Los tres estados están los tres, y los dos filtros viven en la URL con los nombres de la API.
 * Abrir es de su permiso: quien solo ve no encuentra el formulario. La interfaz esconde, el
 * servidor autoriza.
 */
export function PaginaDeRecuentos(): React.JSX.Element {
  const sesion = useSesionAbierta();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  // El alta va siempre en el mismo sitio del árbol. Si cambiara de rama al llegar el listado, React
  // la montaría otra vez y se llevaría lo escrito y los avisos de sus campos.
  return (
    <>
      <Filtros
        listado={listado}
        alFiltrar={(estado, almacen) => {
          // Cambiar un filtro devuelve a la primera página: quedarse en la séptima enseñaría una
          // página vacía de un resultado que sí tiene filas.
          const siguientes = new URLSearchParams(parametros);
          siguientes.delete('pagina');
          poner(siguientes, PARAMETRO_DE_ESTADO, estado);
          poner(siguientes, PARAMETRO_DE_ALMACEN, almacen);
          setParametros(siguientes);
        }}
      />
      <Contenido listado={listado} alCambiarDePagina={irA} />
      {concede(sesion, PERMISOS.recuentoAbrir) && <FormularioDeApertura />}
    </>
  );
}

/** El listado en sus tres estados: cargando, fallido y con datos o sin ellos. */
function Contenido({
  listado,
  alCambiarDePagina,
}: {
  listado: ListadoDeRecuentos;
  alCambiarDePagina: (pagina: number) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  // Por qué sale «Sin reconocer»: escrito una vez debajo de la tabla, y cada celda que lo lleva lo
  // señala.
  const porQueElEstado = useId();

  const consulta = useQuery({
    queryKey: clavesDeRecuentos.lista(listado),
    queryFn: () => consultarRecuentos(listado),
  });

  if (consulta.isPending) {
    return <Cargando que={t('inventario.recuentos.cargando')} />;
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
    const vacio =
      listado.estado !== null || listado.almacenId !== null
        ? t('inventario.recuentos.ningunoConEsteFiltro')
        : listado.pagina > 1
          ? t('inventario.recuentos.paginaVacia')
          : t('inventario.recuentos.ningunoTodavia');

    return (
      <>
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
      </>
    );
  }

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('inventario.recuentos.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            {(['recuento', 'almacen', 'abierto', 'confirmado', 'estado', 'motivo'] as const).map(
              (columna) => (
                <th key={columna} scope="col" className="py-2 pr-4 font-medium">
                  {t(`inventario.recuentos.${columna}`)}
                </th>
              ),
            )}
          </tr>
        </thead>
        <tbody>
          {elementos.map((recuento) => (
            <Fila key={recuento.id} recuento={recuento} porQueElEstado={porQueElEstado} />
          ))}
        </tbody>
      </table>

      {elementos.some((recuento) => recuento.estado === 'desconocido') && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.recuentos.estados.desconocidoDetalle')}
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
  recuento,
  porQueElEstado,
}: {
  recuento: ResumenDeRecuento;
  porQueElEstado: string;
}): React.JSX.Element {
  const { i18n } = useTranslation();
  const nombre = useNombreDelRecuento(recuento);
  const almacen = useNombre('almacen', recuento.almacenId);

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4">
        <Link to={`/recuentos/${recuento.id}`} className="underline">
          {nombre}
        </Link>
      </td>
      <td className="py-2 pr-4">{almacen.largo}</td>
      <td className="py-2 pr-4">{diaLegible(recuento.fechaDeApertura, i18n.language)}</td>
      <td className="py-2 pr-4">
        {recuento.fechaDeConfirmacion !== null &&
          diaLegible(recuento.fechaDeConfirmacion, i18n.language)}
      </td>
      <td
        className="py-2 pr-4"
        aria-describedby={recuento.estado === 'desconocido' ? porQueElEstado : undefined}
      >
        <Estado estado={recuento.estado} />
      </td>
      <td className="py-2 pr-4">{recuento.motivo}</td>
    </tr>
  );
}

/**
 * El estado y el almacén por los que se acota, y el botón que los aplica.
 *
 * El almacén se ofrece solo a quien puede ver los almacenes, que es de donde salen las opciones.
 * Las dos listas se vuelven a montar al cambiar la URL: así la flecha de atrás deja escrito el
 * filtro que se está viendo, y no el anterior. El formulario no: el foco se queda en el botón.
 */
function Filtros({
  listado,
  alFiltrar,
}: {
  listado: ListadoDeRecuentos;
  alFiltrar: (estado: string, almacen: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerAlmacenes = concede(sesion, PERMISOS.almacenVer);

  const almacenes = useQuery({
    queryKey: clavesDeRecuentos.almacenes(),
    queryFn: consultarAlmacenes,
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: puedeVerAlmacenes,
  });

  const estado = listado.estado === null ? '' : estadoEnElContrato(listado.estado);
  const almacen = listado.almacenId ?? '';

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

        alFiltrar(leer(PARAMETRO_DE_ESTADO), leer(PARAMETRO_DE_ALMACEN));
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.recuentos.estado')}
        <select
          key={estado}
          name={PARAMETRO_DE_ESTADO}
          defaultValue={estado}
          className="rounded border border-neutral-300 px-2 py-1.5"
        >
          <option value="">{t('inventario.recuentos.todos')}</option>
          {ESTADOS_DE_RECUENTO.map((valor) => (
            <option key={valor} value={estadoEnElContrato(valor)}>
              {t(`inventario.recuentos.estados.${valor}`)}
            </option>
          ))}
        </select>
      </label>

      {puedeVerAlmacenes && (
        <label className="flex flex-col gap-1 text-sm">
          {t('inventario.recuentos.almacen')}
          <select
            // Con las opciones dentro de la clave: hasta que llegan, la lista no puede enseñar el
            // almacén de la URL, y montada antes se quedaría en «Todos».
            key={`${almacen}|${String(almacenes.data?.length ?? 0)}`}
            name={PARAMETRO_DE_ALMACEN}
            defaultValue={almacen}
            className="rounded border border-neutral-300 px-2 py-1.5"
          >
            <option value="">{t('inventario.recuentos.todos')}</option>
            {almacenes.data?.map((opcion) => (
              <option key={opcion.id} value={opcion.id}>
                {opcion.codigo} · {opcion.nombre}
              </option>
            ))}
          </select>
        </label>
      )}

      <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
        {t('inventario.recuentos.filtrar')}
      </button>
    </form>
  );
}
