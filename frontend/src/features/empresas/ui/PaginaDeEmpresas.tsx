import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { clavesDeEmpresas } from '../api/claves.ts';
import { consultarEmpresas } from '../api/consultas.ts';
import { motivoDeFallo } from '@/shared/api/errores.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';

/** Listado de empresas. Mismos tres estados y misma paginación en la URL que el de almacenes. */
export function PaginaDeEmpresas(): React.JSX.Element {
  const { t } = useTranslation();
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);

  const consulta = useQuery({
    queryKey: clavesDeEmpresas.lista(paginacion),
    queryFn: () => consultarEmpresas(paginacion),
    staleTime: 5 * 60 * 1000,
  });

  if (consulta.isPending) {
    return <Cargando que={t('empresas.cargando')} />;
  }

  if (consulta.isError) {
    return (
      <Fallo
        mensaje={t(`errores.${motivoDeFallo(consulta.error)}`)}
        alReintentar={() => {
          void consulta.refetch();
        }}
      />
    );
  }

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  if (consulta.data.elementos.length === 0) {
    return (
      <>
        <Vacio mensaje={t('empresas.ningunaVisible')} />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('empresas.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('empresas.nif')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('empresas.razonSocial')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('empresas.poblacion')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('empresas.divisa')}
            </th>
          </tr>
        </thead>
        <tbody>
          {consulta.data.elementos.map((empresa) => (
            <tr key={empresa.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4 font-mono">{empresa.nif}</td>
              <td className="py-2 pr-4">{empresa.razonSocial}</td>
              <td className="py-2 pr-4 text-neutral-600">{empresa.poblacion}</td>
              <td className="py-2 pr-4 font-mono">{empresa.divisaBase}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}
