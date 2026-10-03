import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { NombreDeTrazabilidad } from './NombreDeTrazabilidad.tsx';
import { ExplicacionDeSinReconocer } from './SinReconocer.tsx';
import { clavesDeArticulos } from '../api/claves.ts';
import { consultarFicha } from '../api/consultas.ts';
import { cambiarTrazabilidad } from '../api/trazabilidad.ts';
import {
  TRAZABILIDADES,
  type FichaDeArticulo,
  type TrazabilidadElegible,
} from '../model/articulo.ts';
import { esquemaDeTrazabilidad, type DatosDeTrazabilidad } from '../model/esquemaDeTrazabilidad.ts';
import type { Diccionario } from '@/app/i18n/es.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Las claves que el esquema puede poner en `message`, por lo mismo que en la pantalla de acceso. */
type ClaveDeMensajeDeTrazabilidad =
  `catalogo.articulos.cambioDeTrazabilidad.${keyof Diccionario['catalogo']['articulos']['cambioDeTrazabilidad']}`;

/**
 * Los rechazos que son de la trazabilidad elegida, y por eso van en su campo. Los demás —la versión
 * obsoleta, el artículo que ya no existe, el permiso— no son de ningún campo y van arriba.
 */
const DEL_CAMPO: ReadonlySet<string> = new Set([
  'articulo-trazabilidad-con-movimientos',
  'articulo-servicio-con-trazabilidad',
  'articulo-trazabilidad-no-valida',
]);

/**
 * La trazabilidad de un artículo: la que tiene guardada, y el cambio (ítem 2.9, ADR-0048).
 *
 * <b>Solo la trazabilidad, y la ficha entera por debajo.</b> El `PUT` sustituye la ficha, así que
 * lo demás se manda tal como se leyó, sobre la versión con la que se leyó. Si alguien la ha tocado
 * entre medias, el servidor contesta `412` en vez de pisarla, y la pantalla ofrece cargar la
 * versión actual.
 *
 * <b>Lo que el formulario no sabe, lo dice el servidor en el campo.</b> Si el artículo ya tiene
 * movimientos, la marca no se puede cambiar, y eso lo sabe Inventario, no esta pantalla. La pista
 * lo avisa antes; el rechazo, después, en el mismo sitio y en el idioma de quien lo lee (ADR-0030).
 */
export function PaginaDeTrazabilidad(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const { id = '' } = useParams();
  const [guardada, setGuardada] = useState<TrazabilidadElegible | null>(null);
  const porQueLaActual = useId();

  const consulta = useQuery({
    queryKey: clavesDeArticulos.una(id),
    queryFn: () => consultarFicha(id),
  });

  const volver = (
    <p className="mt-6 text-sm">
      <Link to="/articulos" className="underline">
        {t('catalogo.articulos.cambioDeTrazabilidad.volver')}
      </Link>
    </p>
  );

  if (consulta.isPending) {
    return (
      <>
        <Cargando que={t('catalogo.articulos.cambioDeTrazabilidad.cargando')} />
        {volver}
      </>
    );
  }

  if (consulta.isError) {
    return (
      <>
        <Fallo
          mensaje={textoDeFallo(consulta.error)}
          alReintentar={() => {
            void consulta.refetch();
          }}
        />
        {volver}
      </>
    );
  }

  const { articulo } = consulta.data;
  const actualSinReconocer = articulo.trazabilidad === 'desconocida';

  return (
    <>
      <dl className="mt-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="font-medium">{t('catalogo.articulos.cambioDeTrazabilidad.articulo')}</dt>
        <dd>
          <span className="font-mono">{articulo.codigo}</span> · {articulo.descripcion}
        </dd>
        <dt className="font-medium">{t('catalogo.articulos.cambioDeTrazabilidad.actual')}</dt>
        <dd aria-describedby={actualSinReconocer ? porQueLaActual : undefined}>
          <NombreDeTrazabilidad trazabilidad={articulo.trazabilidad} />
        </dd>
      </dl>

      {actualSinReconocer && (
        <ExplicacionDeSinReconocer
          id={porQueLaActual}
          marca={t('catalogo.articulos.trazabilidades.desconocida')}
          detalle={t('catalogo.articulos.trazabilidades.desconocidaDetalle')}
        />
      )}

      {guardada !== null && (
        <p
          role="status"
          className="mt-4 rounded border border-green-300 bg-green-50 p-3 text-sm text-green-900"
        >
          {t('catalogo.articulos.cambioDeTrazabilidad.guardada', {
            trazabilidad: t(`catalogo.articulos.trazabilidades.${guardada}`),
          })}
        </p>
      )}

      {/* LA CLAVE ES LA VERSIÓN: la ficha que vuelve a leerse tras guardar, o tras el 412, trae
          otra, y el formulario se monta de nuevo con lo guardado y sin el rechazo de antes. */}
      <Formulario
        key={consulta.data.version}
        ficha={consulta.data}
        alEmpezar={() => {
          setGuardada(null);
        }}
        alGuardar={setGuardada}
      />

      {volver}
    </>
  );
}

function Formulario({
  ficha,
  alEmpezar,
  alGuardar,
}: {
  ficha: FichaDeArticulo;
  alEmpezar: () => void;
  alGuardar: (trazabilidad: TrazabilidadElegible) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const [rechazo, setRechazo] = useState<unknown>(null);

  const actual = ficha.articulo.trazabilidad;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeTrazabilidad>({
    resolver: zodResolver(esquemaDeTrazabilidad),
    // Una guardada que esta versión no reconoce no se marca: se enseña arriba con su aviso, y
    // guardar sin elegir lo para el esquema.
    ...(actual === 'desconocida' ? {} : { defaultValues: { trazabilidad: actual } }),
  });

  const guardar = async (datos: DatosDeTrazabilidad): Promise<void> => {
    setRechazo(null);
    alEmpezar();

    try {
      await cambiarTrazabilidad(ficha, datos.trazabilidad);
    } catch (error) {
      setRechazo(error);
      return;
    }

    alGuardar(datos.trazabilidad);

    // Todo lo de los artículos: la ficha, que vuelve con su versión nueva, y los listados, que
    // enseñan la trazabilidad.
    await cache.invalidateQueries({ queryKey: clavesDeArticulos.todo });
  };

  const tipo = rechazo === null ? null : tipoDeFallo(rechazo);
  const delCampo = tipo !== null && DEL_CAMPO.has(tipo);

  const errorDelCampo =
    errors.trazabilidad?.message !== undefined
      ? t(errors.trazabilidad.message as ClaveDeMensajeDeTrazabilidad)
      : delCampo
        ? textoDeFallo(rechazo)
        : null;

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(guardar)(evento);
      }}
      className="mt-6 max-w-md space-y-4"
    >
      {rechazo !== null && !delCampo && (
        <div
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          <p>{textoDeFallo(rechazo)}</p>
          {tipo === 'version-obsoleta' && (
            <button
              type="button"
              onClick={() => {
                void cache.invalidateQueries({
                  queryKey: clavesDeArticulos.una(ficha.articulo.id),
                });
              }}
              className="mt-2 rounded border border-red-300 bg-white px-3 py-1.5 text-sm"
            >
              {t('catalogo.articulos.cambioDeTrazabilidad.recargar')}
            </button>
          )}
        </div>
      )}

      {/* EL ERROR VA EN LA DESCRIPCIÓN DEL GRUPO, y no en un `aria-invalid`: ARIA 1.2 no lo admite
          ni en un botón de opción ni en un grupo. Descrito y en texto, que es lo que pide ux-ipo, y
          anunciado al aparecer. */}
      <fieldset
        aria-describedby={
          errorDelCampo === null ? 'trazabilidad-pista' : 'trazabilidad-pista trazabilidad-error'
        }
      >
        <legend className="text-sm font-medium">
          {t('catalogo.articulos.cambioDeTrazabilidad.leyenda')}
        </legend>
        <p id="trazabilidad-pista" className="mt-1 text-sm text-neutral-600">
          {t('catalogo.articulos.cambioDeTrazabilidad.pista')}
        </p>

        <div className="mt-2 space-y-1">
          {TRAZABILIDADES.map((valor) => (
            <label key={valor} className="flex items-center gap-2 text-sm">
              <input type="radio" value={valor} {...register('trazabilidad')} />
              {t(`catalogo.articulos.trazabilidades.${valor}`)}
            </label>
          ))}
        </div>

        {errorDelCampo !== null && (
          <p id="trazabilidad-error" role="alert" className="mt-1 text-sm text-red-800">
            {errorDelCampo}
          </p>
        )}
      </fieldset>

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting
          ? t('catalogo.articulos.cambioDeTrazabilidad.guardando')
          : t('catalogo.articulos.cambioDeTrazabilidad.guardar')}
      </button>
    </form>
  );
}
