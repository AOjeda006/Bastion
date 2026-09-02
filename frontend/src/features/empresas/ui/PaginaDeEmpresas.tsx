import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';

import { clavesDeEmpresas } from '../api/claves.ts';
import { consultarEmpresas } from '../api/consultas.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';

/** Listado de empresas. Mismos tres estados y misma paginación en la URL que el de almacenes. */
export function PaginaDeEmpresas(): React.JSX.Element {
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);

  const consulta = useQuery({
    queryKey: clavesDeEmpresas.lista(paginacion),
    queryFn: () => consultarEmpresas(paginacion),
    staleTime: 5 * 60 * 1000,
  });

  if (consulta.isPending) {
    return <Cargando que="las empresas" />;
  }

  if (consulta.isError) {
    return (
      <Fallo
        mensaje={consulta.error.message}
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
        <Vacio mensaje="No hay ninguna empresa que puedas ver." />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">Empresas dadas de alta</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              NIF
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Razón social
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Población
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Divisa
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
