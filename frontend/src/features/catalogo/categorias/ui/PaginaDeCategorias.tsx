import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { clavesDeCategorias } from '../api/claves.ts';
import { consultarCategorias } from '../api/consultas.ts';
import { componerElArbol } from '../model/arbol.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * El árbol de categorías de la empresa activa, compuesto aquí a partir de la lista plana.
 *
 * **Sin recuadro de filtro, y es una decisión.** Filtrar por texto un árbol deja las coincidencias
 * sueltas y sin sus padres, que es justo lo que este listado sirve para enseñar: dónde está cada
 * rama. Quien busca un artículo lo busca en `/articulos`, que sí filtra por texto; quien mira esto
 * mira la forma de la clasificación.
 *
 * **La sangría es decoración; el nivel es el dato.** Un lector de pantalla no ve el relleno de la
 * izquierda, así que la profundidad va además en su propia columna. Sin ella, el árbol solo
 * existiría para quien lo mira con los ojos.
 */
export function PaginaDeCategorias(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);

  const consulta = useQuery({
    queryKey: clavesDeCategorias.lista(paginacion),
    queryFn: () => consultarCategorias(paginacion),
    // Una categoría es dato maestro, y de los que menos se tocan: el árbol se monta una vez.
    staleTime: 5 * 60 * 1000,
  });

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  if (consulta.isPending) {
    return <Cargando que={t('catalogo.categorias.cargando')} />;
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

  if (consulta.data.elementos.length === 0) {
    return (
      <>
        <Vacio
          mensaje={
            paginacion.pagina > 1
              ? t('catalogo.categorias.paginaVacia')
              : t('catalogo.categorias.ningunaTodavia')
          }
        />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  const filas = componerElArbol(consulta.data.elementos);

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('catalogo.categorias.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.categorias.codigo')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.categorias.nombre')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.categorias.nivel')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.categorias.articulos')}
            </th>
          </tr>
        </thead>
        <tbody>
          {filas.map((fila) => (
            <tr key={fila.categoria.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4 font-mono">{fila.categoria.codigo}</td>
              <td className="py-2 pr-4" style={{ paddingLeft: `${String(fila.profundidad)}rem` }}>
                {fila.categoria.nombre}{' '}
                {fila.huerfana && (
                  <span
                    title={t('catalogo.categorias.sueltaDetalle')}
                    className="rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-xs text-amber-900"
                  >
                    {t('catalogo.categorias.suelta')}
                  </span>
                )}
              </td>
              <td className="py-2 pr-4 text-neutral-600">{fila.profundidad}</td>
              <td className="py-2 pr-4">
                <Link
                  to={`/articulos?categoria=${encodeURIComponent(fila.categoria.id)}`}
                  className="text-blue-800 underline"
                >
                  {t('catalogo.categorias.verSusArticulos', { categoria: fila.categoria.nombre })}
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}
