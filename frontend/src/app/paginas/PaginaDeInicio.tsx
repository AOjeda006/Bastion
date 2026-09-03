import { Trans, useTranslation } from 'react-i18next';

import { empresaActiva } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/**
 * Lo primero que se ve al entrar: quién eres y con qué empresa estás operando.
 *
 * Parece poco y no lo es. En un ERP multiempresa, la pregunta «¿en qué empresa estoy?» es la que
 * está detrás de la mitad de los errores de datos, y el sitio donde se contesta tiene que ser
 * evidente sin buscarlo (`ux-ipo`: reconocer mejor que recordar).
 *
 * `<Trans>` y no `t()` en las dos primeras frases porque llevan `<strong>` DENTRO de la frase, y
 * dónde cae el énfasis cambia con el idioma. La alternativa —partir la frase en tres cachos y
 * concatenarlos— deja al traductor sin la frase entera y se rompe en cuanto un idioma pone el
 * nombre al final.
 *
 * Y son dos claves enteras —con y sin la coletilla del selector— en vez de una frase más un trozo
 * cosido: por lo mismo. La segunda oración puede necesitar ir antes, o unida con otra conjunción.
 */
export function PaginaDeInicio(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const empresa = empresaActiva(sesion);

  const nombreDeLaEmpresa = empresa?.razonSocial ?? t('inicio.empresaNoVisible');

  return (
    <div className="mt-4 max-w-prose space-y-3 text-sm">
      <p>
        <Trans
          i18nKey="inicio.saludo"
          values={{ nombre: sesion.nombre }}
          components={{ strong: <strong /> }}
        />
      </p>
      <p>
        <Trans
          i18nKey={
            sesion.empresas.length > 1 ? 'inicio.operandoConYPuedesCambiar' : 'inicio.operandoCon'
          }
          values={{ empresa: nombreDeLaEmpresa }}
          components={{ strong: <strong /> }}
        />
      </p>
      <p className="text-neutral-500">{t('inicio.armazon')}</p>
    </div>
  );
}
