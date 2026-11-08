import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { DialogoDeZonaDelAlmacen } from './DialogoDeZonaDelAlmacen.tsx';
import { clavesDeAlmacenes } from '../api/claves.ts';
import { consultarAlmacenes, exportarAlmacenes } from '../api/consultas.ts';
import type { Almacen } from '../model/almacen.ts';
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
 * Listado de almacenes de la empresa activa.
 *
 * Los tres estados están los tres: cargando, error y vacío. Y la paginación vive en la URL, no en
 * un `useState`; los datos viven en la caché de consultas y no se copian a ningún estado local —una
 * copia sería una segunda verdad que envejece justo cuando se cambia de empresa—.
 *
 * Con el permiso de modificar, cada fila cambia su zona en un diálogo (`DialogoDeZonaDelAlmacen`).
 * Tras un cambio se invalida la caché ENTERA, y no estas claves: la zona mueve el «hoy» de los
 * formularios y el día de lo que se haya leído con ella, en otras funcionalidades, y una lista de
 * claves elegidas a mano es una lista que alguien se olvidará de ampliar.
 */
export function PaginaDeAlmacenes(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const puedeModificar = concede(useSesionAbierta(), PERMISOS.almacenModificar);
  const [parametros, setParametros] = useSearchParams();
  const paginacion = leerPaginacion(parametros);
  const [abierto, setAbierto] = useState<Almacen | null>(null);
  const [aviso, setAviso] = useState<{ frase: string; numero: number } | null>(null);

  const consulta = useQuery({
    queryKey: clavesDeAlmacenes.lista(paginacion),
    queryFn: () => consultarAlmacenes(paginacion),
    // Un almacén es dato maestro: se da de alta y se queda ahí. Minutos, no segundos.
    staleTime: 5 * 60 * 1000,
  });

  if (consulta.isPending) {
    return <Cargando que={t('organizacion.almacenes.cargando')} />;
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
        <Vacio
          mensaje={
            paginacion.pagina > 1
              ? t('organizacion.almacenes.paginaVacia')
              : t('organizacion.almacenes.ningunoTodavia')
          }
        />
        <Paginador paginacion={paginacion} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      <BotonDeExportar exportar={exportarAlmacenes} />
      <ElAvisoDeLaZona aviso={aviso} />
      {/* A 320 px, la tabla corre ella, y no la página (WCAG 1.4.10, ítem 2.20). `relative`
          recorta también lo que va en absoluto, como un `sr-only`, que si no la empuja. */}
      <div className="relative mt-4 overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{t('organizacion.almacenes.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.almacenes.codigo')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.almacenes.nombre')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.almacenes.tipo')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.almacenes.poblacion')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('organizacion.almacenes.zona')}
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
                <td className="py-2 pr-4">
                  <LaZonaEnLaFila
                    zona={almacen.zona}
                    propia={almacen.zonaPropia}
                    nombre={almacen.codigo}
                    alCambiar={
                      puedeModificar
                        ? () => {
                            setAbierto(almacen);
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

      {abierto !== null && (
        <DialogoDeZonaDelAlmacen
          almacen={abierto}
          alCerrar={() => {
            setAbierto(null);
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
