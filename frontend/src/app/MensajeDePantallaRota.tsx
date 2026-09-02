import { useTranslation } from 'react-i18next';

/**
 * El mensaje que se enseña cuando una pantalla se rompe.
 *
 * Vive aparte del límite de error por dos motivos que van juntos. Un límite TIENE que ser una
 * clase —`getDerivedStateFromError` no existe en hooks— y una clase no puede llamar a
 * `useTranslation`; y `react-refresh` avisa en cuanto un fichero mezcla la clase con un componente
 * de función, porque entonces deja de poder recargar en caliente. Sacar el mensaje resuelve las
 * dos: el límite se queda con el mecanismo, y aquí queda lo único que hay que traducir.
 */
export function MensajeDePantallaRota(): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <div role="alert" className="my-6 max-w-prose rounded border border-red-300 bg-red-50 p-4">
      <p className="text-sm text-red-900">{t('comun.pantallaRota')}</p>
    </div>
  );
}
