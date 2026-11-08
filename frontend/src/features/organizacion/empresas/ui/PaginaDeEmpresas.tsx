import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { DialogoDeZonaDeLaEmpresa } from './DialogoDeZonaDeLaEmpresa.tsx';
import { clavesDeEmpresas } from '../api/claves.ts';
import { consultarEmpresas, exportarEmpresas } from '../api/consultas.ts';
import type { Empresa } from '../model/empresa.ts';
import { ElAvisoDeLaZona, LaZonaEnLaFila } from '../../zonas/ui/LaZonaEnLaFila.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { BotonDeExportar } from '@/shared/ui/BotonDeExportar.tsx';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';

/**
 * Listado de empresas. Mismos tres estados y misma paginación en la URL que el de almacenes, y el
 * mismo diálogo de la zona, que tras un cambio invalida la caché entera por lo mismo, y por una más:
 * la de la empresa arrastra la de todos los almacenes que la heredan.
 */
export function PaginaDeEmpresas(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const puedeModificar = concede(useSesionAbierta(), PERMISOS.empresaModificar);
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);
  const [abierta, setAbierta] = useState<Empresa | null>(null);
  const [aviso, setAviso] = useState<{ frase: string; numero: number } | null>(null);

  const consulta = useQuery({
    queryKey: clavesDeEmpresas.lista(paginacion),
    queryFn: () => consultarEmpresas(paginacion),
    staleTime: 5 * 60 * 1000,
  });

  if (consulta.isPending) {
    return <Cargando que={t('organizacion.empresas.cargando')} />;
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

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  if (consulta.data.elementos.length === 0) {
    return (
      <>
        <Vacio mensaje={t('organizacion.empresas.ningunaVisible')} />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      <BotonDeExportar exportar={exportarEmpresas} />
      <ElAvisoDeLaZona aviso={aviso} />
      {/* A 320 px, la tabla corre ella, y no la página (WCAG 1.4.10, ítem 2.20). `relative`
          recorta también lo que va en absoluto, como un `sr-only`, que si no la empuja. */}
      <div className="relative mt-4 overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{t('organizacion.empresas.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.empresas.nif')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.empresas.razonSocial')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.empresas.poblacion')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.empresas.divisa')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.empresas.zona')}
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
                <td className="py-2 pr-4">
                  <LaZonaEnLaFila
                    zona={empresa.zona}
                    nombre={empresa.razonSocial}
                    alCambiar={
                      puedeModificar
                        ? () => {
                            setAbierta(empresa);
                          }
                        : null
                    }
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {abierta !== null && (
        <DialogoDeZonaDeLaEmpresa
          empresa={abierta}
          alCerrar={() => {
            setAbierta(null);
          }}
          alCambiar={(frase) => {
            setAviso((antes) => ({ frase, numero: (antes?.numero ?? 0) + 1 }));
            void cache.invalidateQueries();
          }}
        />
      )}

      <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}
