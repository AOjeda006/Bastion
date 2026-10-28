import { useQuery } from '@tanstack/react-query';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { Estado } from './Estado.tsx';
import { useNombreDelAjuste } from './nombres.ts';
import { clavesDeMaestros } from '../../maestros/api/claves.ts';
import { consultarAlmacenes } from '../../maestros/api/maestros.ts';
import { VIDA_DE_UN_MAESTRO, useNombre } from '../../maestros/ui/nombres.ts';
import { clavesDeAjustes } from '../api/claves.ts';
import { consultarAjustes, exportarAjustes } from '../api/ajustes.ts';
import { ESTADOS_DE_AJUSTE, type ResumenDeAjuste } from '../model/ajuste.ts';
import {
  LARGO_DE_LA_BUSQUEDA,
  PARAMETRO_DE_ALMACEN,
  PARAMETRO_DE_BUSQUEDA,
  PARAMETRO_DE_ESTADO,
  PARAMETRO_DE_ORDEN,
  estaAcotado,
  estadoEnElContrato,
  leerListado,
  type ListadoDeAjustes,
  type Orden,
} from '../model/listado.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { BotonDeExportar } from '@/shared/ui/BotonDeExportar.tsx';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Los órdenes que se ofrecen, con la clave de su nombre. El primero es el de siempre. */
const ORDENES_CON_NOMBRE: readonly {
  readonly valor: Orden | '';
  readonly clave: 'masRecientes' | 'masAntiguos' | 'numeroDescendente' | 'numeroAscendente';
}[] = [
  { valor: '', clave: 'masRecientes' },
  { valor: 'fechaDeOperacion', clave: 'masAntiguos' },
  { valor: '-numero', clave: 'numeroDescendente' },
  { valor: 'numero', clave: 'numeroAscendente' },
];

/** Lo que el formulario de filtros manda: los cuatro, como texto, y vacío si no se acota. */
interface Filtrado {
  readonly estado: string;
  readonly almacen: string;
  readonly busqueda: string;
  readonly orden: string;
}

/**
 * Los ajustes de la empresa activa: el listado, acotable por estado, por almacén y por lo que dice
 * el motivo, y ordenable por la fecha o por el número (ítem 2.17, ADR-0065 §6).
 *
 * Los filtros viven en la URL. El alta va en su propia ruta (`/ajustes/nuevo`), con su permiso:
 * quien solo ve no encuentra el enlace. La interfaz esconde, el servidor autoriza.
 */
export function PaginaDeAjustes(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  return (
    <>
      {concede(sesion, PERMISOS.ajusteAbrir) && (
        <p className="mt-4">
          <Link
            to="/ajustes/nuevo"
            className="inline-block rounded bg-neutral-900 px-4 py-2 text-sm text-white"
          >
            {t('inventario.ajustes.nuevo')}
          </Link>
        </p>
      )}
      <Filtros
        listado={listado}
        alFiltrar={(filtrado) => {
          // Cambiar un filtro devuelve a la primera página: quedarse en la séptima enseñaría una
          // página vacía de un resultado que sí tiene filas.
          const siguientes = new URLSearchParams(parametros);
          siguientes.delete('pagina');
          poner(siguientes, PARAMETRO_DE_ESTADO, filtrado.estado);
          poner(siguientes, PARAMETRO_DE_ALMACEN, filtrado.almacen);
          poner(siguientes, PARAMETRO_DE_BUSQUEDA, filtrado.busqueda.trim());
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
  listado: ListadoDeAjustes;
  alCambiarDePagina: (pagina: number) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const porQueElEstado = useId();

  const consulta = useQuery({
    queryKey: clavesDeAjustes.lista(listado),
    queryFn: () => consultarAjustes(listado),
  });

  if (consulta.isPending) {
    return <Cargando que={t('inventario.ajustes.cargando')} />;
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
      ? t('inventario.ajustes.ningunoConEsteFiltro')
      : listado.pagina > 1
        ? t('inventario.ajustes.paginaVacia')
        : t('inventario.ajustes.ningunoTodavia');

    return (
      <>
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
      </>
    );
  }

  return (
    <>
      <BotonDeExportar exportar={() => exportarAjustes(listado)} />
      {/* A 320 px, la tabla corre ella, y no la página (WCAG 1.4.10, ítem 2.20). `relative`
          recorta también lo que va en absoluto, como un `sr-only`, que si no la empuja. */}
      <div className="relative mt-4 overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{t('inventario.ajustes.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              {(['ajuste', 'almacen', 'fecha', 'estado', 'motivo'] as const).map((columna) => (
                <th key={columna} scope="col" className="py-2 pr-4 font-medium">
                  {t(`inventario.ajustes.${columna}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {elementos.map((ajuste) => (
              <Fila key={ajuste.id} ajuste={ajuste} porQueElEstado={porQueElEstado} />
            ))}
          </tbody>
        </table>
      </div>

      {elementos.some((ajuste) => ajuste.estado === 'desconocido') && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.ajustes.estados.desconocidoDetalle')}
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
  ajuste,
  porQueElEstado,
}: {
  ajuste: ResumenDeAjuste;
  porQueElEstado: string;
}): React.JSX.Element {
  const { i18n } = useTranslation();
  const nombre = useNombreDelAjuste(ajuste);
  const almacen = useNombre('almacen', ajuste.almacenId);

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4">
        <Link to={`/ajustes/${ajuste.id}`} className="underline">
          {nombre}
        </Link>
      </td>
      <td className="py-2 pr-4">{almacen.largo}</td>
      <td className="py-2 pr-4">{diaLegible(ajuste.fechaDeOperacion, i18n.language)}</td>
      <td
        className="py-2 pr-4"
        aria-describedby={ajuste.estado === 'desconocido' ? porQueElEstado : undefined}
      >
        <Estado estado={ajuste.estado} />
      </td>
      <td className="py-2 pr-4">{ajuste.motivo}</td>
    </tr>
  );
}

/**
 * El estado, el almacén, lo que se busca en el motivo y el orden, y el botón que los aplica.
 *
 * El almacén se ofrece solo a quien puede ver los almacenes, que es de donde salen las opciones.
 * Los campos se vuelven a montar al cambiar la URL: así la flecha de atrás deja escrito el filtro
 * que se está viendo, y no el anterior. El formulario no: el foco se queda en el botón.
 */
function Filtros({
  listado,
  alFiltrar,
}: {
  listado: ListadoDeAjustes;
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
  const almacen = listado.almacenId ?? '';
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
          almacen: leer(PARAMETRO_DE_ALMACEN),
          busqueda: leer(PARAMETRO_DE_BUSQUEDA),
          orden: leer(PARAMETRO_DE_ORDEN),
        });
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.ajustes.estado')}
        <select
          key={estado}
          name={PARAMETRO_DE_ESTADO}
          defaultValue={estado}
          className="rounded border border-neutral-300 px-2 py-1.5"
        >
          <option value="">{t('inventario.ajustes.todos')}</option>
          {ESTADOS_DE_AJUSTE.map((valor) => (
            <option key={valor} value={estadoEnElContrato(valor)}>
              {t(`inventario.ajustes.estados.${valor}`)}
            </option>
          ))}
        </select>
      </label>

      {puedeVerAlmacenes && (
        <label
          // Un `<select>` mide lo que su opción más larga, y un maestro se llama como quiera la
          // empresa. `min-w-0` deja a la caja estrecharse hasta el ancho de la pantalla, y la lista
          // con ella (WCAG 1.4.10).
          className="flex min-w-0 flex-col gap-1 text-sm"
        >
          {t('inventario.ajustes.almacen')}
          <select
            // Con las opciones dentro de la clave: hasta que llegan, la lista no puede enseñar el
            // almacén de la URL, y montada antes se quedaría en «Todos».
            key={`${almacen}|${String(almacenes.data?.length ?? 0)}`}
            name={PARAMETRO_DE_ALMACEN}
            defaultValue={almacen}
            className="rounded border border-neutral-300 px-2 py-1.5"
          >
            <option value="">{t('inventario.ajustes.todos')}</option>
            {almacenes.data?.map((opcion) => (
              <option key={opcion.id} value={opcion.id}>
                {opcion.nombre === null ? opcion.codigo : `${opcion.codigo} · ${opcion.nombre}`}
              </option>
            ))}
          </select>
        </label>
      )}

      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.ajustes.buscar')}
        <input
          key={listado.busqueda}
          type="search"
          name={PARAMETRO_DE_BUSQUEDA}
          defaultValue={listado.busqueda}
          maxLength={LARGO_DE_LA_BUSQUEDA}
          className="rounded border border-neutral-300 px-2 py-1.5"
        />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.ajustes.orden')}
        <select
          key={orden}
          name={PARAMETRO_DE_ORDEN}
          defaultValue={orden}
          className="rounded border border-neutral-300 px-2 py-1.5"
        >
          {ORDENES_CON_NOMBRE.map(({ valor, clave }) => (
            <option key={clave} value={valor}>
              {t(`inventario.ajustes.ordenes.${clave}`)}
            </option>
          ))}
        </select>
      </label>

      <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
        {t('inventario.ajustes.filtrar')}
      </button>
    </form>
  );
}
