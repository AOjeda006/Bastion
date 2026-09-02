import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';

import { clavesDeAlmacenes } from '../api/claves.ts';
import { consultarAlmacenes } from '../api/consultas.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';

/**
 * Listado de almacenes de la empresa activa.
 *
 * Los tres estados están los tres: cargando, error y vacío. Y la paginación vive en la URL, no en
 * un `useState`; los datos viven en la caché de consultas y no se copian a ningún estado local —una
 * copia sería una segunda verdad que envejece justo cuando se cambia de empresa—.
 */
export function PaginaDeAlmacenes(): React.JSX.Element {
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);

  const consulta = useQuery({
    queryKey: clavesDeAlmacenes.lista(paginacion),
    queryFn: () => consultarAlmacenes(paginacion),
    // Un almacén es dato maestro: se da de alta y se queda ahí. Minutos, no segundos.
    staleTime: 5 * 60 * 1000,
  });

  if (consulta.isPending) {
    return <Cargando que="los almacenes" />;
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
        <Vacio
          mensaje={
            paginacion.pagina > 1
              ? 'Esta página no tiene almacenes. Vuelve a la anterior.'
              : 'Todavía no hay ningún almacén dado de alta en esta empresa.'
          }
        />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">Almacenes de la empresa activa</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              Código
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Nombre
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Tipo
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Población
            </th>
          </tr>
        </thead>
        <tbody>
          {consulta.data.elementos.map((almacen) => (
            <tr key={almacen.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4 font-mono">{almacen.codigo}</td>
              <td className="py-2 pr-4">{almacen.nombre}</td>
              <td className="py-2 pr-4">{almacen.tipo}</td>
              <td className="py-2 pr-4 text-neutral-600">{almacen.poblacion ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}
