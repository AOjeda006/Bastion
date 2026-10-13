import { useQuery } from '@tanstack/react-query';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { clavesDeRecuentos } from '../../recuentos/api/claves.ts';
import { consultarAlmacenes } from '../../recuentos/api/maestros.ts';
import { cantidadLegible } from '../../recuentos/model/cantidad.ts';
import { VIDA_DE_UN_MAESTRO, useNombre } from '../../recuentos/ui/nombres.ts';
import { clavesDeSaldos } from '../api/claves.ts';
import { consultarArticulos, consultarSaldos } from '../api/saldos.ts';
import {
  PARAMETRO_DE_ALMACEN,
  PARAMETRO_DE_ARTICULO,
  PARAMETRO_DE_FECHA,
  hoyEnElServidor,
  importeLegible,
  leerListado,
  type ListadoDeSaldos,
  type Saldo,
} from '../model/saldo.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * El saldo de la empresa activa al final de un día, por artículo y almacén (ítem 2.14, ADR-0063).
 *
 * El día va arriba y, sin tocarlo, es hoy. Debajo, el almacén y el artículo por los que se acota.
 * Los tres viven en la URL con los nombres de la API, así que un saldo pasado se puede pegar en un
 * correo. Solo se lee: no hay nada que escribir ni ningún botón que esconder.
 */
export function PaginaDeSaldos(): React.JSX.Element {
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
        alConsultar={(fecha, almacen, articulo) => {
          // Cambiar el día o un filtro devuelve a la primera página: quedarse en la séptima
          // enseñaría una página vacía de un resultado que sí tiene filas.
          const siguientes = new URLSearchParams(parametros);
          siguientes.delete('pagina');
          poner(siguientes, PARAMETRO_DE_FECHA, fecha);
          poner(siguientes, PARAMETRO_DE_ALMACEN, almacen);
          poner(siguientes, PARAMETRO_DE_ARTICULO, articulo);
          setParametros(siguientes);
        }}
      />
      <Contenido listado={listado} alCambiarDePagina={irA} />
    </>
  );
}

/** El saldo en sus tres estados: cargando, fallido y con filas o sin ellas. */
function Contenido({
  listado,
  alCambiarDePagina,
}: {
  listado: ListadoDeSaldos;
  alCambiarDePagina: (pagina: number) => void;
}): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  // Lo que viaja no está sumado: dicho una vez debajo de la tabla, y las dos columnas lo señalan.
  const porQueAparte = useId();

  const consulta = useQuery({
    queryKey: clavesDeSaldos.lista(listado),
    queryFn: () => consultarSaldos(listado),
  });

  if (consulta.isPending) {
    return <Cargando que={t('inventario.saldos.cargando')} />;
  }

  if (consulta.isError) {
    // Un día futuro no se arregla reintentando: el texto del `type` dice qué día elegir.
    const reintentable = tipoDeFallo(consulta.error) !== 'saldo-con-fecha-futura';

    return (
      <Fallo
        mensaje={textoDeFallo(consulta.error)}
        alReintentar={
          reintentable
            ? () => {
                void consulta.refetch();
              }
            : undefined
        }
      />
    );
  }

  const { fecha, elementos, total } = consulta.data;

  if (elementos.length === 0) {
    const vacio =
      listado.almacenId !== null || listado.articuloId !== null
        ? t('inventario.saldos.ningunoConEsteFiltro')
        : listado.pagina > 1
          ? t('inventario.saldos.paginaVacia')
          : t('inventario.saldos.ningunoTodavia');

    return (
      <>
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
      </>
    );
  }

  const numerica = 'py-2 pr-4 text-right font-medium';

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        {/* El día es el que contestó el servidor, no el del campo: sin fecha, contesta su hoy. */}
        <caption className="pb-2 text-left font-medium">
          {t('inventario.saldos.tabla', { dia: diaLegible(fecha, i18n.language) })}
        </caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('inventario.saldos.articulo')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('inventario.saldos.almacen')}
            </th>
            <th scope="col" className={numerica}>
              {t('inventario.saldos.cantidad')}
            </th>
            <th scope="col" className={numerica}>
              {t('inventario.saldos.valor')}
            </th>
            <th scope="col" className={numerica} aria-describedby={porQueAparte}>
              {t('inventario.saldos.enTransito')}
            </th>
            <th scope="col" className={numerica} aria-describedby={porQueAparte}>
              {t('inventario.saldos.valorEnTransito')}
            </th>
          </tr>
        </thead>
        <tbody>
          {elementos.map((saldo) => (
            <Fila key={`${saldo.articuloId}|${saldo.almacenId}`} saldo={saldo} />
          ))}
        </tbody>
      </table>

      <p id={porQueAparte} className="mt-2 text-sm text-neutral-600">
        {t('inventario.saldos.transitoAparte')}
      </p>

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

function Fila({ saldo }: { saldo: Saldo }): React.JSX.Element {
  const { i18n } = useTranslation();
  const articulo = useNombre('articulo', saldo.articuloId);
  const almacen = useNombre('almacen', saldo.almacenId);
  const idioma = i18n.language;
  const numerica = 'py-2 pr-4 text-right tabular-nums';

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4">{articulo.largo}</td>
      <td className="py-2 pr-4">{almacen.largo}</td>
      <td className={numerica}>{cantidadLegible(saldo.cantidad, idioma)}</td>
      <td className={numerica}>{importeLegible(saldo.valor, saldo.divisa, idioma)}</td>
      <td className={numerica}>{cantidadLegible(saldo.enTransito, idioma)}</td>
      <td className={numerica}>{importeLegible(saldo.valorEnTransito, saldo.divisa, idioma)}</td>
    </tr>
  );
}

/**
 * El día, el almacén y el artículo, y el botón que los aplica.
 *
 * El día no deja elegir uno posterior al de hoy **del servidor**, que es el UTC (ADR-0063 §3). El
 * almacén y el artículo se ofrecen solo a quien puede verlos, que es de donde salen las opciones.
 * Los tres campos se vuelven a montar al cambiar la URL: así la flecha de atrás deja escrito lo que
 * se está viendo, y no lo anterior.
 */
function Filtros({
  listado,
  alConsultar,
}: {
  listado: ListadoDeSaldos;
  alConsultar: (fecha: string, almacen: string, articulo: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const puedeVerAlmacenes = concede(sesion, PERMISOS.almacenVer);
  const puedeVerArticulos = concede(sesion, PERMISOS.articuloVer);
  const hoy = hoyEnElServidor();

  // Por el mismo camino y con la misma clave que el alta del recuento: es la misma lista.
  const almacenes = useQuery({
    queryKey: clavesDeRecuentos.almacenes(),
    queryFn: consultarAlmacenes,
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: puedeVerAlmacenes,
  });

  const articulos = useQuery({
    queryKey: clavesDeSaldos.articulos(),
    queryFn: consultarArticulos,
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: puedeVerArticulos,
  });

  const fecha = listado.fecha ?? hoy;
  const almacen = listado.almacenId ?? '';
  const articulo = listado.articuloId ?? '';

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

        alConsultar(
          leer(PARAMETRO_DE_FECHA),
          leer(PARAMETRO_DE_ALMACEN),
          leer(PARAMETRO_DE_ARTICULO),
        );
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        {t('inventario.saldos.dia')}
        <input
          key={fecha}
          type="date"
          name={PARAMETRO_DE_FECHA}
          defaultValue={fecha}
          max={hoy}
          className="rounded border border-neutral-300 px-2 py-1.5"
        />
      </label>

      {puedeVerAlmacenes && (
        <label className="flex flex-col gap-1 text-sm">
          {t('inventario.saldos.almacen')}
          <select
            // Con las opciones dentro de la clave: hasta que llegan, la lista no puede enseñar el
            // almacén de la URL, y montada antes se quedaría en «Todos».
            key={`${almacen}|${String(almacenes.data?.length ?? 0)}`}
            name={PARAMETRO_DE_ALMACEN}
            defaultValue={almacen}
            className="rounded border border-neutral-300 px-2 py-1.5"
          >
            <option value="">{t('inventario.saldos.todos')}</option>
            {almacenes.data?.map((opcion) => (
              <option key={opcion.id} value={opcion.id}>
                {opcion.codigo} · {opcion.nombre}
              </option>
            ))}
          </select>
        </label>
      )}

      {puedeVerArticulos && (
        <label className="flex flex-col gap-1 text-sm">
          {t('inventario.saldos.articulo')}
          <select
            key={`${articulo}|${String(articulos.data?.length ?? 0)}`}
            name={PARAMETRO_DE_ARTICULO}
            defaultValue={articulo}
            className="rounded border border-neutral-300 px-2 py-1.5"
          >
            <option value="">{t('inventario.saldos.todos')}</option>
            {articulos.data?.map((opcion) => (
              <option key={opcion.id} value={opcion.id}>
                {opcion.nombre === null ? opcion.codigo : `${opcion.codigo} · ${opcion.nombre}`}
              </option>
            ))}
          </select>
        </label>
      )}

      <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
        {t('inventario.saldos.consultar')}
      </button>
    </form>
  );
}
