import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { cambiarEmpresa } from '@/shared/api/sesiones.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/**
 * El selector de empresa: la R8 vista desde el navegador.
 *
 * La empresa activa **viaja dentro del testigo**, no en una cabecera ni en un parámetro. Cambiarla
 * es pedir un testigo nuevo, y a partir de ese momento las mismas URL devuelven otras filas.
 *
 * DE AHÍ SALE LA REGLA IMPORTANTE: al cambiar de empresa **se tira la caché entera**, no se
 * invalidan unas claves elegidas a mano. Elegirlas es apostar a que uno se acuerda de todas, y esa
 * apuesta se pierde con la primera pantalla que añada otro. Además `invalidateQueries` deja lo
 * viejo en pantalla mientras refresca: durante ese rato la cabecera diría «Beta» y la tabla estaría
 * enseñando filas de «Alfa». Eso no es un parpadeo, es una fuga entre inquilinos.
 *
 * Y se tira con `resetQueries()`, NO con `clear()`. Parece que `clear()` es lo más contundente y es
 * lo contrario: vacía el almacén pero no toca a los observadores ya montados, que se quedan
 * enseñando su último resultado —el de la empresa anterior— y sin pedir nada nuevo, porque su
 * consulta ya no existe. `resetQueries()` los devuelve a su estado inicial Y vuelve a pedir lo que
 * está en pantalla. Lo cazó el test mirando lo que se pinta; un test de «se ha llamado a clear()»
 * lo habría dado por bueno.
 *
 * `<select>` nativo y no un menú de ARIA: `stacks/react` manda elemento nativo antes que ARIA, y
 * aquí no hay nada que un `<select>` con su `<label>` no haga ya bien —teclado, lector de pantalla,
 * móvil—.
 */
export function SelectorDeEmpresa(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const cache = useQueryClient();

  const cambio = useMutation({
    mutationFn: cambiarEmpresa,
    onSuccess: (resultado) => {
      if (!('sesion' in resultado)) {
        return;
      }

      // El orden NO es indiferente. Primero el testigo nuevo y después el reinicio: al revés, las
      // consultas montadas volverían a pedir sus datos con el testigo viejo y repoblarían la caché
      // con filas de la empresa que se acaba de dejar.
      escribirSesion(resultado.sesion);
      void cache.resetQueries();
    },
  });

  const motivo = cambio.data !== undefined && 'error' in cambio.data ? cambio.data.error : null;

  if (sesion.empresas.length <= 1) {
    const unica = sesion.empresas[0];

    return (
      <p className="text-sm text-neutral-700">
        <span className="text-neutral-500">{t('sesion.empresaEtiqueta')}</span>
        {unica?.razonSocial ?? '—'}
      </p>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <label htmlFor="empresa-activa" className="text-sm text-neutral-500">
        {t('sesion.empresa')}
      </label>
      <select
        id="empresa-activa"
        value={sesion.empresaActivaId}
        disabled={cambio.isPending}
        onChange={(evento) => {
          cambio.mutate(evento.target.value);
        }}
        className="rounded border border-neutral-300 px-2 py-1 text-sm"
      >
        {sesion.empresas.map((empresa) => (
          <option key={empresa.id} value={empresa.id}>
            {empresa.razonSocial}
          </option>
        ))}
      </select>
      {motivo !== null && (
        <span role="alert" className="text-sm text-red-800">
          {t(`sesion.${motivo}`)}
        </span>
      )}
    </div>
  );
}
