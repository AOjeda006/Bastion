import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { clavesDeAlmacenes } from '../api/claves.ts';
import { cambiarZonaDelAlmacen, consultarFichaDeAlmacen } from '../api/zona.ts';
import type { Almacen } from '../model/almacen.ts';
import { ZONAS_DE_ALMACEN, esquemaDeZonaDeAlmacen } from '../../zonas/model/zona.ts';
import { Cancelar, FormularioDeZona } from '../../zonas/ui/FormularioDeZona.tsx';
import { useNombreDeZona } from '../../zonas/ui/nombres.ts';
import { Dialogo } from '@/shared/ui/Dialogo.tsx';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * La zona de un almacén: la de su empresa, que hereda en vivo, o una de las tres (ADR-0079 §2).
 *
 * **Lee el almacén al abrirse, cada vez**, por su versión: el `PUT` lo reemplaza entero, y lo que
 * no es la zona vuelve tal como está ahora, no como estaba en el listado. Que la lea cada vez lo
 * hace el `staleTime`, que no se pone y es cero. Y lo leído no se guarda al cerrar (`gcTime: 0`): al
 * volver a abrirla, la ventana espera a la lectura de ahora, y no ofrece la de antes mientras llega.
 *
 * `alCambiar` recibe la frase que dice cómo queda, con la zona que rige, también cuando la hereda.
 */
export function DialogoDeZonaDelAlmacen({
  almacen,
  alCerrar,
  alCambiar,
}: {
  almacen: Almacen;
  alCerrar: () => void;
  alCambiar: (frase: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const nombreDeZona = useNombreDeZona();

  const consulta = useQuery({
    queryKey: clavesDeAlmacenes.ficha(almacen.id),
    queryFn: () => consultarFichaDeAlmacen(almacen.id),
    gcTime: 0,
  });

  return (
    <Dialogo
      titulo={t('organizacion.zonas.titulo', { nombre: almacen.codigo })}
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
        // La clave es la versión: tras el 412, la lectura de ahora monta el formulario otra vez,
        // con la zona que tiene y sin el rechazo de antes.
        <FormularioDeZona
          key={consulta.data.version}
          esquema={esquemaDeZonaDeAlmacen}
          opciones={ZONAS_DE_ALMACEN}
          actual={consulta.data.almacen.zonaPropia ? consulta.data.almacen.zona : 'deLaEmpresa'}
          guardar={async (zona) => {
            const queda = await cambiarZonaDelAlmacen(consulta.data, zona);
            const nombres = { nombre: queda.codigo, zona: nombreDeZona(queda.zona) };
            alCambiar(
              queda.zonaPropia
                ? t('organizacion.zonas.cambiada', nombres)
                : t('organizacion.zonas.heredada', nombres),
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
