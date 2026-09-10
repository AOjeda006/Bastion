import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { clavesDeArticulos } from '../api/claves.ts';
import { consultarArticulos } from '../api/consultas.ts';
import { PARAMETRO_DE_BUSQUEDA, PARAMETRO_DE_CATEGORIA, leerListado } from '../model/listado.ts';
import type { TipoDeArticulo } from '../model/articulo.ts';
import { clavesDeCategorias } from '../../categorias/api/claves.ts';
import { consultarCategoria } from '../../categorias/api/consultas.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Listado de artículos de la empresa activa, paginado y filtrado por texto y por rama del árbol.
 *
 * Los tres estados están los tres —cargando, error con salida y vacío con motivo—, y tanto la
 * página como los dos filtros viven en la URL: los datos están en la caché de consultas y no se
 * copian a ningún estado local, que sería una segunda verdad que envejece justo al cambiar de
 * empresa.
 *
 * **El filtro por categoría no se elige aquí: se llega a él desde el árbol** (`/categorias`), que
 * es donde una rama se ve en su sitio. Aquí lo que hay es el filtro puesto, con su nombre y con la
 * salida para quitarlo. Un desplegable con todas las categorías obligaría a traerse el catálogo
 * entero en cada visita a esta pantalla para rellenarlo, y a quedarse corto —sin decirlo— en la
 * empresa que tuviera más de las que caben en una página.
 */
export function PaginaDeArticulos(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const sesion = useSesionAbierta();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const consulta = useQuery({
    queryKey: clavesDeArticulos.lista(listado),
    queryFn: () => consultarArticulos(listado),
    // Un artículo es dato maestro: se da de alta y se queda ahí. Minutos, no segundos.
    staleTime: 5 * 60 * 1000,
  });

  // El nombre de la rama por la que se filtra. Va aparte y no bloquea la tabla: es un adorno del
  // filtro, y una pantalla que no enseñara los artículos porque no ha podido resolver un nombre
  // estaría cambiando lo importante por lo accesorio. Y se pide SOLO si la sesión concede ver
  // categorías —la interfaz esconde, el servidor autoriza—, porque si no, cada visita con filtro
  // se llevaría un 403 seguro.
  const rama = useQuery({
    queryKey: clavesDeCategorias.una(listado.categoriaId ?? ''),
    queryFn: () => consultarCategoria(listado.categoriaId ?? ''),
    enabled: listado.categoriaId !== null && concede(sesion, PERMISOS.categoriaVer),
    staleTime: 5 * 60 * 1000,
  });

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  // Cambiar un filtro devuelve a la primera página. Quedarse en la séptima enseña una página vacía
  // de un resultado que sí tiene filas, y quien lo ve entiende que no hay ninguno.
  const cambiarFiltro = (cambio: (siguientes: URLSearchParams) => void): void => {
    const siguientes = new URLSearchParams(parametros);
    cambio(siguientes);
    siguientes.delete('pagina');
    setParametros(siguientes);
  };

  const filtrarPor = (texto: string): void => {
    cambiarFiltro((siguientes) => {
      const limpio = texto.trim();

      if (limpio === '') {
        siguientes.delete(PARAMETRO_DE_BUSQUEDA);
      } else {
        siguientes.set(PARAMETRO_DE_BUSQUEDA, limpio);
      }
    });
  };

  const cabecera = (
    <>
      <form
        role="search"
        className="mt-4 flex items-end gap-2"
        onSubmit={(evento) => {
          evento.preventDefault();
          const escrito = new FormData(evento.currentTarget).get(PARAMETRO_DE_BUSQUEDA);

          // `FormData` devuelve texto o fichero, y de un fichero saldría «[object Object]». Aquí no
          // puede haberlo —el campo es un `<input type="search">`— pero eso es una promesa del JSX
          // de arriba, no del tipo, y las promesas de ese tamaño se comprueban.
          filtrarPor(typeof escrito === 'string' ? escrito : '');
        }}
      >
        <label className="flex flex-col gap-1 text-sm">
          {t('catalogo.articulos.filtro')}
          <input
            type="search"
            name={PARAMETRO_DE_BUSQUEDA}
            defaultValue={listado.busqueda}
            // La clave lleva el filtro dentro: al cambiarlo, el recuadro se vuelve a montar con lo
            // que dice la URL. Sin esto, la flecha de atrás cambiaría la tabla y dejaría escrito el
            // filtro anterior, que es peor que no tener flecha de atrás.
            key={listado.busqueda}
            className="rounded border border-neutral-300 px-2 py-1.5"
          />
        </label>
        <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
          {t('catalogo.articulos.filtrar')}
        </button>
      </form>

      {listado.categoriaId !== null && (
        <p className="mt-3 flex items-center gap-2 text-sm text-neutral-700">
          <span>
            {rama.data === undefined
              ? // Sin nombre —todavía no ha llegado, no hay permiso para pedirlo, o la rama ya no
                // existe— se dice que HAY un filtro puesto igualmente. Callarlo dejaría una tabla
                // con menos filas de las que hay y ninguna explicación a la vista.
                t('catalogo.articulos.filtradaPorUnaCategoria')
              : t('catalogo.articulos.filtradaPor', { categoria: rama.data.nombre })}
          </span>
          <button
            type="button"
            onClick={() => {
              cambiarFiltro((siguientes) => {
                siguientes.delete(PARAMETRO_DE_CATEGORIA);
              });
            }}
            className="rounded border border-neutral-300 px-2 py-1 text-xs"
          >
            {t('catalogo.articulos.quitarLaCategoria')}
          </button>
        </p>
      )}
    </>
  );

  if (consulta.isPending) {
    return (
      <>
        {cabecera}
        <Cargando que={t('catalogo.articulos.cargando')} />
      </>
    );
  }

  if (consulta.isError) {
    return (
      <>
        {cabecera}
        <Fallo
          mensaje={textoDeFallo(consulta.error)}
          alReintentar={() => {
            void consulta.refetch();
          }}
        />
      </>
    );
  }

  if (consulta.data.elementos.length === 0) {
    // Qué está vacío, dicho con precisión: no es lo mismo «no hay artículos» que «ninguno de esta
    // rama» o «ninguno que case con lo que has escrito», y confundirlos manda a dar de alta algo
    // que ya existe.
    const vacio =
      listado.busqueda !== ''
        ? t('catalogo.articulos.ningunoConEsteFiltro', { filtro: listado.busqueda })
        : listado.categoriaId !== null
          ? t('catalogo.articulos.ningunoEnEstaCategoria')
          : listado.pagina > 1
            ? t('catalogo.articulos.paginaVacia')
            : t('catalogo.articulos.ningunoTodavia');

    return (
      <>
        {cabecera}
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      {cabecera}

      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('catalogo.articulos.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.articulos.codigo')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.articulos.descripcion')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.articulos.tipo')}
            </th>
          </tr>
        </thead>
        <tbody>
          {consulta.data.elementos.map((articulo) => (
            <tr key={articulo.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4 font-mono">{articulo.codigo}</td>
              <td className="py-2 pr-4">{articulo.descripcion}</td>
              <td className="py-2 pr-4">
                <Tipo tipo={articulo.tipo} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <Paginador paginacion={listado} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}

/**
 * Mercancía o prestación, y qué se dice cuando llega un tercer valor.
 *
 * `desconocido` no es defensa por si acaso: el tipo viaja como texto y el frontal se despliega
 * aparte del backend, así que un valor nuevo llega antes de que este fichero lo conozca. Lo que no
 * se puede interpretar se dice, con su explicación en el título; la alternativa —la celda en
 * blanco— es lo que se ve cuando algo está roto, y no distingue una cosa de la otra.
 */
function Tipo({ tipo }: { tipo: TipoDeArticulo }): React.JSX.Element {
  const { t } = useTranslation();

  if (tipo === 'desconocido') {
    return (
      <span
        title={t('catalogo.articulos.tipos.desconocidoDetalle')}
        className="rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-xs text-amber-900"
      >
        {t('catalogo.articulos.tipos.desconocido')}
      </span>
    );
  }

  return (
    <>
      {tipo === 'bien'
        ? t('catalogo.articulos.tipos.bien')
        : t('catalogo.articulos.tipos.servicio')}
    </>
  );
}
