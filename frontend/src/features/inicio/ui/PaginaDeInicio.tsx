import { empresaActiva } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/**
 * Lo primero que se ve al entrar: quién eres y con qué empresa estás operando.
 *
 * Parece poco y no lo es. En un ERP multiempresa, la pregunta «¿en qué empresa estoy?» es la que
 * está detrás de la mitad de los errores de datos, y el sitio donde se contesta tiene que ser
 * evidente sin buscarlo (`ux-ipo`: reconocer mejor que recordar).
 */
export function PaginaDeInicio(): React.JSX.Element {
  const sesion = useSesionAbierta();
  const empresa = empresaActiva(sesion);

  return (
    <div className="mt-4 max-w-prose space-y-3 text-sm">
      <p>
        Hola, <strong>{sesion.nombre}</strong>.
      </p>
      <p>
        Estás operando con{' '}
        <strong>{empresa?.razonSocial ?? 'una empresa que ya no está visible'}</strong>
        {sesion.empresas.length > 1
          ? '. Puedes cambiar de empresa en el selector de la cabecera.'
          : '.'}
      </p>
      <p className="text-neutral-500">
        Esto es el armazón de la fase 0: acceso, selector de empresa, rutas protegidas y dos
        listados de solo lectura. Los módulos de negocio llegan en las fases siguientes.
      </p>
    </div>
  );
}
