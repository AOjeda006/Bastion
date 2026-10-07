import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { useNombre } from './nombres.ts';
import { clavesDeRecuentos } from '../api/claves.ts';
import { consultarLinea, consultarLineas, contarLinea } from '../api/recuentos.ts';
import {
  cambioDelTeorico,
  cantidadLegible,
  contadoEscrito,
  diferenciaLegible,
} from '../model/cantidad.ts';
import {
  SIN_CONTADO,
  esquemaDeContado,
  esquemaDeContadoDeUnaSerie,
  type DatosDeContado,
} from '../model/formularios.ts';
import type { ListadoDeLineas } from '../model/listado.ts';
import { RECHAZO_DE_LO_CONTADO } from '../model/rechazos.ts';
import type { LineaDeRecuento, Recuento } from '../model/recuento.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Las líneas de un recuento, página a página, y lo contado en cada una.
 *
 * <b>La diferencia se dice en la pantalla.</b> Cada línea lleva el teórico, lo contado y lo que va
 * de uno a otro, en la unidad base del artículo, que también se dice. Mientras está en curso, el
 * teórico es el de ahora: si ha cambiado desde que se contó la línea, la celda dice cuánto era y
 * cuánto ha cambiado (ADR-0055 §2), y lo que viaja hacia la clave sale en su columna.
 *
 * <b>Contar es en la fila.</b> El campo sale en la celda de las acciones, con lo contado si ya lo
 * había. Antes de escribir se lee la versión de la línea, que es la que cita el `PUT`: el listado no
 * la trae.
 */
export function LineasDelRecuento({
  recuento,
  listado,
  puedeContar,
  alCambiarDePagina,
  alContar,
  alFallar,
}: {
  recuento: Recuento;
  listado: ListadoDeLineas;
  puedeContar: boolean;
  alCambiarDePagina: (pagina: number) => void;
  /** Tras contar una línea. `sale` dice si va a salir de la vista que se mira. */
  alContar: (numero: number, sale: boolean) => Promise<void>;
  alFallar: (error: unknown) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();

  const lineas = useQuery({
    queryKey: clavesDeRecuentos.lineas(recuento.id, listado),
    queryFn: () => consultarLineas(recuento.id, listado),
  });

  const enCurso = recuento.estado === 'enCurso';
  const conAcciones = enCurso && puedeContar;
  // En las dos vistas parciales, contar saca la línea: deja de estar sin contar, y su teórico al
  // contar pasa a ser el de ahora.
  const sale = listado.solo !== null;

  if (lineas.isPending) {
    return <Cargando que={t('inventario.recuentos.ficha.cargando')} />;
  }

  const fallo = lineas.isError ? (
    <Fallo
      mensaje={textoDeFallo(lineas.error)}
      alReintentar={() => {
        void lineas.refetch();
      }}
    />
  ) : null;

  if (lineas.data === undefined) {
    return <>{fallo}</>;
  }

  const { elementos, total } = lineas.data;

  if (elementos.length === 0) {
    const vacio =
      listado.solo !== null
        ? t('inventario.recuentos.ficha.ningunaEnLaVista')
        : listado.pagina > 1
          ? t('inventario.recuentos.ficha.paginaVacia')
          : t('inventario.recuentos.ficha.ninguna');

    return (
      <>
        {fallo}
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
      </>
    );
  }

  const columnas = [
    'numero',
    'articulo',
    'ubicacion',
    'loteOSerie',
    'unidad',
    'teorico',
    ...(enCurso ? (['enTransito'] as const) : []),
    'contado',
    'diferencia',
    ...(conAcciones ? (['acciones'] as const) : []),
  ] as const;

  return (
    <>
      {/* Con datos, el fallo de volver a leer va encima y no en su lugar. */}
      {fallo}

      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('inventario.recuentos.ficha.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            {columnas.map((columna) => (
              <th key={columna} scope="col" className="py-2 pr-4 font-medium">
                {t(`inventario.recuentos.ficha.${columna}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {elementos.map((linea) => (
            <Fila
              key={linea.id}
              recuentoId={recuento.id}
              linea={linea}
              enCurso={enCurso}
              conAcciones={conAcciones}
              sale={sale}
              alContar={alContar}
              alFallar={alFallar}
            />
          ))}
        </tbody>
      </table>

      <Paginador paginacion={listado} total={total} alCambiar={alCambiarDePagina} />
    </>
  );
}

function Fila({
  recuentoId,
  linea,
  enCurso,
  conAcciones,
  sale,
  alContar,
  alFallar,
}: {
  recuentoId: string;
  linea: LineaDeRecuento;
  enCurso: boolean;
  conAcciones: boolean;
  sale: boolean;
  alContar: (numero: number, sale: boolean) => Promise<void>;
  alFallar: (error: unknown) => void;
}): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const articulo = useNombre('articulo', linea.articuloId);
  const ubicacion = useNombre('ubicacion', linea.ubicacionId);
  const unidad = useNombre('unidad', linea.unidadBaseId);
  const [editando, setEditando] = useState(false);
  const boton = useRef<HTMLButtonElement>(null);
  // Al cerrar el campo, el foco vuelve al botón de la fila, que es de donde salió. Salvo que la
  // fila vaya a desaparecer, o que lo que se cierre sea un fallo: entonces va al aviso.
  const devolverElFoco = useRef(false);

  useEffect(() => {
    if (!editando && devolverElFoco.current) {
      devolverElFoco.current = false;
      boton.current?.focus();
    }
  }, [editando]);

  const legible = (valor: number | null): string =>
    valor === null ? '' : cantidadLegible(valor, i18n.language);
  const cambio = cambioDelTeorico(linea);

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4">{linea.numero}</td>
      <td className="py-2 pr-4">{articulo.largo}</td>
      <td className="py-2 pr-4">{ubicacion.largo}</td>
      <td className="py-2 pr-4 font-mono">{linea.numeroDeSerie ?? linea.codigoDeLote}</td>
      <td className="py-2 pr-4">{unidad.corto}</td>
      <td className="py-2 pr-4">
        {legible(linea.teorico)}
        {cambio !== null && (
          <span className="block text-xs text-amber-800">
            {t('inventario.recuentos.ficha.alContar', {
              antes: legible(linea.teoricoAlContar),
              cambio: diferenciaLegible(cambio, i18n.language),
            })}
          </span>
        )}
      </td>
      {enCurso && <td className="py-2 pr-4">{legible(linea.enTransito)}</td>}
      <td className="py-2 pr-4">
        {linea.contado === null ? (
          <span className="text-neutral-500">{t('inventario.recuentos.ficha.sinContar')}</span>
        ) : (
          legible(linea.contado)
        )}
      </td>
      <td className="py-2 pr-4">
        {linea.diferencia !== null && diferenciaLegible(linea.diferencia, i18n.language)}
      </td>
      {conAcciones && (
        <td className="py-2 pr-4">
          {editando ? (
            <EditorDeContado
              recuentoId={recuentoId}
              linea={linea}
              unidad={unidad.corto}
              alCancelar={() => {
                devolverElFoco.current = true;
                setEditando(false);
              }}
              alGuardar={async () => {
                devolverElFoco.current = !sale;
                setEditando(false);
                await alContar(linea.numero, sale);
              }}
              alFallar={(error) => {
                setEditando(false);
                alFallar(error);
              }}
            />
          ) : (
            <button
              type="button"
              ref={boton}
              // El texto visible es uno por fila; el nombre accesible dice cuál.
              aria-label={
                linea.contado === null
                  ? t('inventario.recuentos.ficha.contarLa', { numero: linea.numero })
                  : t('inventario.recuentos.ficha.corregirLa', { numero: linea.numero })
              }
              onClick={() => {
                setEditando(true);
              }}
              className="rounded border border-neutral-300 px-2 py-1 text-xs"
            >
              {linea.contado === null
                ? t('inventario.recuentos.ficha.contar')
                : t('inventario.recuentos.ficha.corregir')}
            </button>
          )}
        </td>
      )}
    </tr>
  );
}

/**
 * El campo de lo contado en una línea, con Guardar y Cancelar.
 *
 * El rechazo de la cifra va en el campo, lo diga el esquema o el servidor. Cualquier otro —la línea
 * que otro acaba de contar, el recuento que ya no está en curso— cierra el campo y va al aviso, y la
 * ficha se lee otra vez: lo que se estaba corrigiendo ya no es lo que hay.
 */
function EditorDeContado({
  recuentoId,
  linea,
  unidad,
  alCancelar,
  alGuardar,
  alFallar,
}: {
  recuentoId: string;
  linea: LineaDeRecuento;
  unidad: string;
  alCancelar: () => void;
  alGuardar: () => Promise<void>;
  alFallar: (error: unknown) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const esUnaSerie = linea.numeroDeSerie !== null;
  const campo = `contado-${linea.id}`;

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeContado>({
    resolver: zodResolver(esUnaSerie ? esquemaDeContadoDeUnaSerie : esquemaDeContado),
    defaultValues: { contado: linea.contado === null ? '' : String(linea.contado) },
  });

  useEffect(() => {
    setFocus('contado', { shouldSelect: true });
  }, [setFocus]);

  const guardar = async ({ contado }: DatosDeContado): Promise<void> => {
    // El esquema ya lo ha validado con la misma regla: aquí solo se convierte.
    const cifra = contadoEscrito(contado, esUnaSerie) ?? contado;

    try {
      const { version } = await consultarLinea(recuentoId, linea.id);
      await contarLinea(recuentoId, linea.id, version, cifra);
    } catch (error) {
      if (tipoDeFallo(error) === RECHAZO_DE_LO_CONTADO) {
        setError(
          'contado',
          { type: 'servidor', message: RECHAZO_DE_LO_CONTADO },
          { shouldFocus: true },
        );
      } else {
        alFallar(error);
      }

      return;
    }

    await alGuardar();
  };

  const mensaje = errors.contado?.message;
  const error =
    mensaje === undefined
      ? null
      : mensaje === SIN_CONTADO
        ? t(`inventario.recuentos.ficha.${SIN_CONTADO}`)
        : mensaje === RECHAZO_DE_LO_CONTADO
          ? t(`errores.tipos.${RECHAZO_DE_LO_CONTADO}`)
          : mensaje;

  // Escape cancela desde cualquiera de los tres controles, mientras no se esté guardando.
  const alPulsar = (evento: React.KeyboardEvent): void => {
    if (evento.key === 'Escape' && !isSubmitting) {
      alCancelar();
    }
  };

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(guardar)(evento);
      }}
      className="flex flex-col gap-1"
    >
      <label htmlFor={campo} className="text-xs font-medium">
        {t('inventario.recuentos.ficha.campoContado', { numero: linea.numero, unidad })}
      </label>
      <div className="flex flex-wrap items-center gap-2">
        <input
          id={campo}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          aria-invalid={error !== null}
          aria-describedby={error === null ? undefined : `${campo}-error`}
          onKeyDown={alPulsar}
          {...register('contado')}
          className="w-32 rounded border border-neutral-300 px-2 py-1"
        />
        <button
          type="submit"
          disabled={isSubmitting}
          onKeyDown={alPulsar}
          className="rounded bg-neutral-900 px-2 py-1 text-xs text-white disabled:opacity-50"
        >
          {isSubmitting
            ? t('inventario.recuentos.ficha.guardando')
            : t('inventario.recuentos.ficha.guardar')}
        </button>
        <button
          type="button"
          disabled={isSubmitting}
          onClick={alCancelar}
          onKeyDown={alPulsar}
          className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50"
        >
          {t('inventario.recuentos.ficha.cancelar')}
        </button>
      </div>
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-xs text-red-800">
          {error}
        </p>
      )}
    </form>
  );
}
