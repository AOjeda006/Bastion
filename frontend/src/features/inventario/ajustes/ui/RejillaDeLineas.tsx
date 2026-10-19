import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import {
  Controller,
  useFieldArray,
  type UseFormReturn,
  type UseFormRegisterReturn,
} from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { BuscadorDeArticulo } from './BuscadorDeArticulo.tsx';
import type { Maestro, Trazabilidad, Ubicacion } from '../../maestros/api/maestros.ts';
import {
  LINEAS_COMO_MUCHO,
  LINEA_EN_BLANCO,
  rutaDeLaLinea,
  type DatosDelAlta,
} from '../model/formularios.ts';
import type { CampoDeLaLinea } from '../model/rechazos.ts';

/** Lo que la celda de un campo hace con Intro: añadir una línea, o pasar a la siguiente. */
type AlPulsarIntro = (evento: React.KeyboardEvent<HTMLElement>, indice: number) => void;

/** Lo que comparten las celdas: su nombre, su error y lo que hace Intro. */
interface PropiedadesDeCelda {
  readonly id: string;
  readonly 'aria-label': string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
  readonly onKeyDown: (evento: React.KeyboardEvent<HTMLElement>) => void;
}

/** Los controles de la celda de una fila que se pueden usar, en el orden del tabulador. */
const CELDAS_USABLES = 'input:not([disabled]), select:not([disabled])';

/**
 * Las líneas del alta, para teclado (§10 del plan maestro, ADR-0065 §8).
 *
 * - **El tabulador** pasa de una celda a la siguiente, en el orden en que se leen.
 * - **Intro en la última celda** de la última línea añade otra, con el foco en su ubicación; en la
 *   de otra línea, pasa a la siguiente. En el resto de celdas no hace nada: una rejilla que mandara
 *   el formulario con cada Intro lo mandaría a medio escribir.
 * - **El lote y el número de serie** solo se escriben si el artículo elegido se sigue así; si no, la
 *   celda está deshabilitada y no cuenta como la última.
 *
 * Cada celda lleva su nombre entero, con la línea («Cantidad de la línea 2»), porque un lector de
 * pantalla que entra en ella no lee la cabecera de la columna. Y su error debajo, que es donde lo
 * pone también el rechazo del servidor (ADR-0065 §3).
 */
export function RejillaDeLineas({
  formulario,
  ubicaciones,
  unidades,
  hayAlmacen,
  texto,
}: {
  formulario: UseFormReturn<DatosDelAlta>;
  /** Las del almacén elegido. */
  ubicaciones: readonly Ubicacion[];
  unidades: readonly Maestro[];
  hayAlmacen: boolean;
  texto: (mensaje: string | undefined) => string | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const { fields, append, remove } = useFieldArray({
    control: formulario.control,
    name: 'lineas',
  });
  // La trazabilidad del artículo de cada línea, por la identidad de la línea y no por su posición:
  // quitar una no le cambia la marca a las de debajo.
  const [marcas, setMarcas] = useState<Readonly<Record<string, Trazabilidad | null>>>({});
  const llenas = fields.length >= LINEAS_COMO_MUCHO;
  const cuerpo = useRef<HTMLTableSectionElement>(null);

  // `append` también sabe llevar el foco a la línea nueva, pero desde un efecto, en mitad del
  // pintado: la celda que se deja pierde el foco ahí dentro, y quien escuche su `blur` corre dentro
  // del pintado de otro (en las pruebas, el `change` que emula user-event abría un `act` dentro de
  // otro, y React lo avisaba). Con `flushSync`, la línea está pintada al volver, y el foco se mueve
  // desde el manejador de la tecla o del clic, como lo movería la persona.
  const anadirLinea = (): void => {
    flushSync(() => {
      append(LINEA_EN_BLANCO, { shouldFocus: false });
    });
    cuerpo.current?.lastElementChild?.querySelector<HTMLElement>(CELDAS_USABLES)?.focus();
  };

  const alPulsarIntro: AlPulsarIntro = (evento, indice) => {
    if (evento.key !== 'Enter') {
      return;
    }

    evento.preventDefault();
    const fila = evento.currentTarget.closest('tr');

    if (fila === null) {
      return;
    }

    const celdas = fila.querySelectorAll<HTMLElement>(CELDAS_USABLES);

    if (celdas[celdas.length - 1] !== evento.currentTarget) {
      return;
    }

    if (indice < fields.length - 1) {
      fila.nextElementSibling?.querySelector<HTMLElement>(CELDAS_USABLES)?.focus();
    } else if (!llenas) {
      anadirLinea();
    }
  };

  return (
    <div className="space-y-2">
      <p id="pista-de-la-rejilla" className="text-sm text-neutral-600">
        {t('inventario.ajustes.alta.pistaDeLaRejilla')}
      </p>

      {/* La rejilla es más ancha que un móvil: se desplaza ella, y no la página. */}
      <div className="overflow-x-auto">
        <table className="border-collapse text-sm" aria-describedby="pista-de-la-rejilla">
          <caption className="sr-only">{t('inventario.ajustes.alta.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              {(
                [
                  'numero',
                  'ubicacion',
                  'articulo',
                  'cantidad',
                  'unidad',
                  'coste',
                  'lote',
                  'numeroDeSerie',
                ] as const
              ).map((columna) => (
                <th key={columna} scope="col" className="py-2 pr-2 font-medium">
                  {t(`inventario.ajustes.alta.${columna}`)}
                </th>
              ))}
              <td />
            </tr>
          </thead>
          <tbody ref={cuerpo}>
            {fields.map((campo, indice) => (
              <Fila
                key={campo.id}
                indice={indice}
                formulario={formulario}
                ubicaciones={ubicaciones}
                unidades={unidades}
                hayAlmacen={hayAlmacen}
                marca={marcas[campo.id] ?? null}
                alCambiarDeMarca={(marca) => {
                  setMarcas((anteriores) => ({ ...anteriores, [campo.id]: marca }));
                }}
                texto={texto}
                alPulsarIntro={alPulsarIntro}
                quitar={
                  fields.length > 1
                    ? () => {
                        remove(indice);
                      }
                    : null
                }
              />
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          disabled={llenas}
          aria-describedby={llenas ? 'tope-de-lineas' : undefined}
          onClick={anadirLinea}
          className="rounded border border-neutral-300 px-3 py-1.5 text-sm disabled:opacity-50"
        >
          {t('inventario.ajustes.alta.anadir')}
        </button>
        {llenas && (
          <p id="tope-de-lineas" className="text-sm text-neutral-600">
            {t('inventario.ajustes.alta.topeDeLineas')}
          </p>
        )}
      </div>
    </div>
  );
}

function Fila({
  indice,
  formulario,
  ubicaciones,
  unidades,
  hayAlmacen,
  marca,
  alCambiarDeMarca,
  texto,
  alPulsarIntro,
  quitar,
}: {
  indice: number;
  formulario: UseFormReturn<DatosDelAlta>;
  ubicaciones: readonly Ubicacion[];
  unidades: readonly Maestro[];
  hayAlmacen: boolean;
  /** La trazabilidad del artículo elegido, o nula si no hay ninguno. */
  marca: Trazabilidad | null;
  alCambiarDeMarca: (marca: Trazabilidad | null) => void;
  texto: (mensaje: string | undefined) => string | null;
  alPulsarIntro: AlPulsarIntro;
  quitar: (() => void) | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const {
    control,
    register,
    setValue,
    formState: { errors },
  } = formulario;
  const numero = indice + 1;
  const deLaLinea = errors.lineas?.[indice];

  const nombreDe = (campo: CampoDeLaLinea): string => `linea-${String(indice)}-${campo}`;
  const etiqueta = (columna: string): string =>
    t('inventario.ajustes.alta.celda', { campo: columna, numero });

  const celda = (
    campo: CampoDeLaLinea,
    columna: string,
  ): { readonly propiedades: PropiedadesDeCelda; readonly error: string | null } => {
    const error = texto(deLaLinea?.[campo]?.message);

    return {
      propiedades: {
        id: nombreDe(campo),
        'aria-label': etiqueta(columna),
        'aria-invalid': error !== null,
        'aria-describedby': error === null ? undefined : `${nombreDe(campo)}-error`,
        onKeyDown: (evento) => {
          alPulsarIntro(evento, indice);
        },
      },
      error,
    };
  };

  const ubicacion = celda('ubicacionId', t('inventario.ajustes.alta.ubicacion'));
  const articulo = celda('articuloId', t('inventario.ajustes.alta.articulo'));
  const cantidad = celda('cantidadIntroducida', t('inventario.ajustes.alta.cantidad'));
  const unidad = celda('unidadIntroducidaId', t('inventario.ajustes.alta.unidad'));
  const coste = celda('costeUnitario', t('inventario.ajustes.alta.coste'));
  const lote = celda('codigoDeLote', t('inventario.ajustes.alta.lote'));
  const serie = celda('numeroDeSerie', t('inventario.ajustes.alta.numeroDeSerie'));

  return (
    <tr className="border-b border-neutral-200 align-top">
      <th scope="row" className="py-2 pr-2 font-normal">
        {numero}
      </th>

      <Celda error={ubicacion.error} id={ubicacion.propiedades.id}>
        <select
          {...ubicacion.propiedades}
          {...register(rutaDeLaLinea(indice, 'ubicacionId'))}
          className="w-44 rounded border border-neutral-300 px-2 py-1"
        >
          <option value="">
            {hayAlmacen
              ? t('inventario.ajustes.alta.sinElegir')
              : t('inventario.ajustes.alta.antesElAlmacen')}
          </option>
          {ubicaciones.map((opcion) => (
            <option key={opcion.id} value={opcion.id}>
              {opcion.nombre === null ? opcion.codigo : `${opcion.codigo} · ${opcion.nombre}`}
            </option>
          ))}
        </select>
      </Celda>

      <Celda error={articulo.error} id={articulo.propiedades.id}>
        <Controller
          control={control}
          name={rutaDeLaLinea(indice, 'articuloId')}
          render={({ field }) => (
            <BuscadorDeArticulo
              id={articulo.propiedades.id}
              etiqueta={articulo.propiedades['aria-label']}
              articuloId={field.value}
              error={articulo.error}
              idDelError={`${articulo.propiedades.id}-error`}
              referencia={field.ref}
              alElegir={(elegido) => {
                field.onChange(elegido?.id ?? '');
                alCambiarDeMarca(elegido?.trazabilidad ?? null);

                if (elegido === null) {
                  return;
                }

                // La unidad que se ofrece primero es la base del artículo, que no necesita
                // conversión; el lote y la serie de otro artículo no le valen a este.
                setValue(rutaDeLaLinea(indice, 'unidadIntroducidaId'), elegido.unidadBaseId);

                if (elegido.trazabilidad !== 'porLote') {
                  setValue(rutaDeLaLinea(indice, 'codigoDeLote'), '');
                }

                if (elegido.trazabilidad !== 'porNumeroSerie') {
                  setValue(rutaDeLaLinea(indice, 'numeroDeSerie'), '');
                }
              }}
              alSalir={field.onBlur}
              alPulsarIntro={articulo.propiedades.onKeyDown}
            />
          )}
        />
      </Celda>

      <Celda error={cantidad.error} id={cantidad.propiedades.id}>
        <Entrada
          celda={cantidad.propiedades}
          registro={register(rutaDeLaLinea(indice, 'cantidadIntroducida'))}
          ancho="w-28"
        />
      </Celda>

      <Celda error={unidad.error} id={unidad.propiedades.id}>
        <select
          {...unidad.propiedades}
          {...register(rutaDeLaLinea(indice, 'unidadIntroducidaId'))}
          className="w-36 rounded border border-neutral-300 px-2 py-1"
        >
          <option value="">{t('inventario.ajustes.alta.sinElegir')}</option>
          {unidades.map((opcion) => (
            <option key={opcion.id} value={opcion.id}>
              {opcion.nombre === null ? opcion.codigo : `${opcion.codigo} · ${opcion.nombre}`}
            </option>
          ))}
        </select>
      </Celda>

      <Celda error={coste.error} id={coste.propiedades.id}>
        <Entrada
          celda={coste.propiedades}
          registro={register(rutaDeLaLinea(indice, 'costeUnitario'))}
          ancho="w-28"
        />
      </Celda>

      <Celda error={lote.error} id={lote.propiedades.id}>
        <Entrada
          celda={lote.propiedades}
          registro={register(rutaDeLaLinea(indice, 'codigoDeLote'))}
          ancho="w-32"
          deshabilitada={marca !== 'porLote'}
        />
      </Celda>

      <Celda error={serie.error} id={serie.propiedades.id}>
        <Entrada
          celda={serie.propiedades}
          registro={register(rutaDeLaLinea(indice, 'numeroDeSerie'))}
          ancho="w-32"
          deshabilitada={marca !== 'porNumeroSerie'}
        />
      </Celda>

      <td className="py-2">
        {quitar !== null && (
          <button
            type="button"
            onClick={quitar}
            className="rounded border border-neutral-300 px-2 py-1 text-xs"
          >
            {t('inventario.ajustes.alta.quitar', { numero })}
          </button>
        )}
      </td>
    </tr>
  );
}

function Celda({
  id,
  error,
  children,
}: {
  id: string;
  error: string | null;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <td className="py-2 pr-2">
      {children}
      {error !== null && (
        <p id={`${id}-error`} role="alert" className="mt-1 max-w-56 text-xs text-red-800">
          {error}
        </p>
      )}
    </td>
  );
}

function Entrada({
  celda,
  registro,
  ancho,
  deshabilitada = false,
}: {
  celda: PropiedadesDeCelda;
  registro: UseFormRegisterReturn;
  ancho: string;
  deshabilitada?: boolean;
}): React.JSX.Element {
  return (
    <input
      type="text"
      autoComplete="off"
      disabled={deshabilitada}
      {...celda}
      {...registro}
      className={`${ancho} rounded border border-neutral-300 px-2 py-1 disabled:bg-neutral-100`}
    />
  );
}
