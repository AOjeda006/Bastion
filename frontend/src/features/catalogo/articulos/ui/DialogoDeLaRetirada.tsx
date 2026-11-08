import { useMutation, useQuery } from '@tanstack/react-query';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';

import { clavesDeArticulos } from '../api/claves.ts';
import { consultarFicha } from '../api/consultas.ts';
import { reincorporarArticulo, retirarArticulo } from '../api/retirada.ts';
import type { Articulo, FichaDeArticulo } from '../model/articulo.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { cerrarElDialogo } from '@/shared/ui/cerrarElDialogo.ts';
import { Dialogo } from '@/shared/ui/Dialogo.tsx';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Retirar o reincorporar un artículo, confirmado en la ventana del ADR-0080 (ítem 3.6, ADR-0082).
 *
 * **Lo que se pide sale de la fila**: retirar si el listado lo trae sin retirar, y reincorporar si
 * lo trae retirado. Si alguien lo ha cambiado desde que se pintó el listado, las dos transiciones
 * son idempotentes, así que lo que se confirma queda como se dijo, y el aviso no miente.
 *
 * **Lee la ficha al abrirse, cada vez**, por su versión, como la ventana de la zona: el `If-Match`
 * para lo que se haya cambiado del artículo mientras la ventana estaba abierta. Que la lea cada vez
 * lo hace el `staleTime`, que no se pone y es cero; y lo leído no se guarda al cerrar (`gcTime: 0`),
 * para que al volver a abrirla no se confirme sobre la versión de antes mientras llega la de ahora.
 *
 * `alCambiar` recibe la frase que dice cómo queda.
 */
export function DialogoDeLaRetirada({
  articulo,
  alCerrar,
  alCambiar,
}: {
  articulo: Articulo;
  alCerrar: () => void;
  alCambiar: (frase: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const retirar = !articulo.retirada;

  const consulta = useQuery({
    queryKey: clavesDeArticulos.una(articulo.id),
    queryFn: () => consultarFicha(articulo.id),
    gcTime: 0,
  });

  return (
    <Dialogo
      titulo={
        retirar
          ? t('catalogo.articulos.retiradaDelArticulo.tituloRetirar', { codigo: articulo.codigo })
          : t('catalogo.articulos.retiradaDelArticulo.tituloReincorporar', {
              codigo: articulo.codigo,
            })
      }
      alCerrar={alCerrar}
    >
      {consulta.isPending ? (
        <>
          <Cargando que={t('catalogo.articulos.retiradaDelArticulo.cargando')} />
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
        // La clave es la versión: tras el 412, la lectura de ahora monta la confirmación otra vez,
        // sin el rechazo de antes.
        <Confirmacion
          key={consulta.data.version}
          ficha={consulta.data}
          retirar={retirar}
          alCambiar={alCambiar}
          recargar={() => {
            void consulta.refetch();
          }}
        />
      )}
    </Dialogo>
  );
}

/**
 * Lo que pasa, dicho antes del botón que lo hace, y el botón. Si sale bien, dice cómo queda y
 * cierra la ventana, que es lo que devuelve el foco a quien la abrió. Si no, el rechazo va arriba,
 * y la versión obsoleta ofrece cargar la de ahora (`recargar`).
 */
function Confirmacion({
  ficha,
  retirar,
  alCambiar,
  recargar,
}: {
  ficha: FichaDeArticulo;
  retirar: boolean;
  alCambiar: (frase: string) => void;
  recargar: () => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const consecuencias = useId();
  const { codigo, descripcion } = ficha.articulo;

  const cambio = useMutation({
    mutationFn: () => (retirar ? retirarArticulo(ficha) : reincorporarArticulo(ficha)),
  });

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        evento.preventDefault();
        const formulario = evento.currentTarget;

        cambio.mutate(undefined, {
          onSuccess: () => {
            alCambiar(
              retirar
                ? t('catalogo.articulos.retiradaDelArticulo.retirado', { codigo })
                : t('catalogo.articulos.retiradaDelArticulo.reincorporado', { codigo }),
            );
            cerrarElDialogo(formulario);
          },
        });
      }}
      className="mt-4 space-y-4"
    >
      {cambio.isError && (
        <div
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          <p>{textoDeFallo(cambio.error)}</p>
          {tipoDeFallo(cambio.error) === 'version-obsoleta' && (
            <button
              type="button"
              onClick={recargar}
              className="mt-2 rounded border border-red-300 bg-white px-3 py-1.5 text-sm"
            >
              {t('catalogo.articulos.retiradaDelArticulo.recargar')}
            </button>
          )}
        </div>
      )}

      <dl className="text-sm">
        <dt className="font-medium">{t('catalogo.articulos.retiradaDelArticulo.articulo')}</dt>
        <dd className="break-words">
          <span className="font-mono">{codigo}</span> · {descripcion}
        </dd>
      </dl>

      <ul id={consecuencias} className="list-disc space-y-1 pl-5 text-sm text-neutral-700">
        {retirar ? (
          <>
            <li>{t('catalogo.articulos.retiradaDelArticulo.retirarLoNuevo')}</li>
            <li>{t('catalogo.articulos.retiradaDelArticulo.retirarLoDeAntes')}</li>
            <li>{t('catalogo.articulos.retiradaDelArticulo.retirarLasExistencias')}</li>
            <li>{t('catalogo.articulos.retiradaDelArticulo.retirarElListado')}</li>
          </>
        ) : (
          <li>{t('catalogo.articulos.retiradaDelArticulo.reincorporarLoNuevo')}</li>
        )}
      </ul>

      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={cambio.isPending}
          aria-describedby={consecuencias}
          className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
        >
          {retirar
            ? cambio.isPending
              ? t('catalogo.articulos.retiradaDelArticulo.retirando')
              : t('catalogo.articulos.retiradaDelArticulo.siRetirar')
            : cambio.isPending
              ? t('catalogo.articulos.retiradaDelArticulo.reincorporando')
              : t('catalogo.articulos.retiradaDelArticulo.siReincorporar')}
        </button>
        <Cancelar />
      </div>
    </form>
  );
}

/** La salida que no cambia nada. Cierra la ventana, como Escape, y el foco vuelve a quien la abrió. */
function Cancelar(): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <button
      type="button"
      onClick={(evento) => {
        cerrarElDialogo(evento.currentTarget);
      }}
      className="rounded border border-neutral-300 px-4 py-2 text-sm"
    >
      {t('catalogo.articulos.retiradaDelArticulo.cancelar')}
    </button>
  );
}
