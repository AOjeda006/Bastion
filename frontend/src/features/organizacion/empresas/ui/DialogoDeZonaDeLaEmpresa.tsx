import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { clavesDeEmpresas } from '../api/claves.ts';
import { cambiarZonaDeLaEmpresa, consultarFichaDeEmpresa } from '../api/zona.ts';
import type { Empresa } from '../model/empresa.ts';
import { ZONAS, esquemaDeZonaDeEmpresa } from '../../zonas/model/zona.ts';
import { Cancelar, FormularioDeZona } from '../../zonas/ui/FormularioDeZona.tsx';
import { useNombreDeZona } from '../../zonas/ui/nombres.ts';
import { Dialogo } from '@/shared/ui/Dialogo.tsx';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * La zona de una empresa, una de las tres, como la del almacén (`DialogoDeZonaDelAlmacen`), y con un
 * aviso más: arrastra la de los almacenes que la heredan.
 */
export function DialogoDeZonaDeLaEmpresa({
  empresa,
  alCerrar,
  alCambiar,
}: {
  empresa: Empresa;
  alCerrar: () => void;
  alCambiar: (frase: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const nombreDeZona = useNombreDeZona();

  const consulta = useQuery({
    queryKey: clavesDeEmpresas.ficha(empresa.id),
    queryFn: () => consultarFichaDeEmpresa(empresa.id),
    gcTime: 0,
  });

  return (
    <Dialogo
      titulo={t('organizacion.zonas.titulo', { nombre: empresa.razonSocial })}
      alCerrar={alCerrar}
    >
      {consulta.isPending ? (
        <>
          <Cargando que={t('organizacion.zonas.cargando')} />
          <Cancelar />
        </>
      ) : consulta.isError ? (
        <>
          <Fallo
            mensaje={textoDeFallo(consulta.error)}
            alReintentar={() => {
              void consulta.refetch();
            }}
          />
          <Cancelar />
        </>
      ) : (
        <FormularioDeZona
          key={consulta.data.version}
          esquema={esquemaDeZonaDeEmpresa}
          opciones={ZONAS}
          actual={consulta.data.empresa.zona}
          otroAviso={t('organizacion.zonas.avisoLosAlmacenes')}
          guardar={async (zona) => {
            const queda = await cambiarZonaDeLaEmpresa(consulta.data, zona);
            alCambiar(
              t('organizacion.zonas.cambiada', {
                nombre: queda.razonSocial,
                zona: nombreDeZona(queda.zona),
              }),
            );
          }}
          recargar={() => {
            void consulta.refetch();
          }}
        />
      )}
    </Dialogo>
  );
}
