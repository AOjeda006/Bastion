import { Controller, useFieldArray, type UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import type { Maestro, Trazabilidad, Ubicacion } from '../../maestros/api/maestros.ts';
import { CeldaDeLista } from '../../lineas/ui/CeldaDeLista.tsx';
import { CeldaDeTexto } from '../../lineas/ui/CeldaDeTexto.tsx';
import { CeldaDelArticulo } from '../../lineas/ui/CeldaDelArticulo.tsx';
import { MarcoDeLineas, type AlPulsarIntro } from '../../lineas/ui/MarcoDeLineas.tsx';
import { useCeldasDeLaLinea } from '../../lineas/ui/useCeldasDeLaLinea.ts';
import { useMarcasDeLasLineas } from '../../lineas/ui/useMarcasDeLasLineas.ts';
import { LINEA_EN_BLANCO, rutaDeLaLinea, type DatosDelAlta } from '../model/formularios.ts';

/** Las ubicaciones de cada punta: las del almacén de origen y las del de destino. */
export interface UbicacionesDeLasPuntas {
  readonly origen: readonly Ubicacion[];
  readonly destino: readonly Ubicacion[];
  /** Si ya hay almacén elegido en cada punta: sin él, su celda pide elegirlo antes. */
  readonly hayOrigen: boolean;
  readonly hayDestino: boolean;
}

/**
 * Las líneas del alta de la transferencia: el marco compartido, con las columnas de la
 * transferencia (ADR-0068 §8).
 *
 * - **Una ubicación por punta**: la que deja, de las del almacén de origen, y la que ocupa, de las
 *   del de destino. La primera es la primera celda, y la que recibe el foco de la línea nueva.
 * - **Sin coste**: lo que viaja se valora al enviarlo, con lo que valía en el origen.
 * - **El lote y el número de serie** solo se escriben si el artículo elegido se sigue así; si no, la
 *   celda está deshabilitada y no cuenta como la última. Se escriben a mano (respuesta 2 de la
 *   puerta): elegirlos de lo que hay en el origen espera a la expedición de la fase 4.
 */
export function RejillaDeLaTransferencia({
  formulario,
  ubicaciones,
  unidades,
  texto,
}: {
  formulario: UseFormReturn<DatosDelAlta>;
  ubicaciones: UbicacionesDeLasPuntas;
  unidades: readonly Maestro[];
  texto: (mensaje: string | undefined) => string | null;
}): React.JSX.Element {
  const { t } = useTranslation();
  const { fields, append, remove } = useFieldArray({
    control: formulario.control,
    name: 'lineas',
  });
  const marcas = useMarcasDeLasLineas();

  return (
    <MarcoDeLineas
      titulo={t('inventario.transferencias.alta.tabla')}
      columnas={[
        t('inventario.transferencias.ubicacionOrigen'),
        t('inventario.transferencias.ubicacionDestino'),
        t('inventario.lineas.articulo'),
        t('inventario.lineas.cantidad'),
        t('inventario.lineas.unidad'),
        t('inventario.lineas.lote'),
        t('inventario.lineas.numeroDeSerie'),
      ]}
      tope={t('inventario.transferencias.avisos.topeDeLineas')}
      lineas={fields}
      anadir={() => {
        append(LINEA_EN_BLANCO, { shouldFocus: false });
      }}
      quitar={remove}
    >
      {(linea, alPulsarIntro) => (
        <CeldasDeLaLinea
          indice={linea.indice}
          formulario={formulario}
          ubicaciones={ubicaciones}
          unidades={unidades}
          marca={marcas.de(linea.id)}
          alCambiarDeMarca={(marca) => {
            marcas.anotar(linea.id, marca);
          }}
          texto={texto}
          alPulsarIntro={alPulsarIntro}
        />
      )}
    </MarcoDeLineas>
  );
}

function CeldasDeLaLinea({
  indice,
  formulario,
  ubicaciones,
  unidades,
  marca,
  alCambiarDeMarca,
  texto,
  alPulsarIntro,
}: {
  indice: number;
  formulario: UseFormReturn<DatosDelAlta>;
  ubicaciones: UbicacionesDeLasPuntas;
  unidades: readonly Maestro[];
  /** La trazabilidad del artículo elegido, o nula si no hay ninguno. */
  marca: Trazabilidad | null;
  alCambiarDeMarca: (marca: Trazabilidad | null) => void;
  texto: (mensaje: string | undefined) => string | null;
  alPulsarIntro: AlPulsarIntro;
}): React.JSX.Element {
  const { t } = useTranslation();
  const {
    control,
    register,
    setValue,
    formState: { errors },
  } = formulario;
  const deLaLinea = errors.lineas?.[indice];
  const celda = useCeldasDeLaLinea(indice, alPulsarIntro);
  const vacia = (hayAlmacen: boolean): string =>
    hayAlmacen ? t('inventario.lineas.sinElegir') : t('inventario.lineas.antesElAlmacen');

  return (
    <>
      <CeldaDeLista
        celda={celda(
          'ubicacionOrigenId',
          t('inventario.transferencias.ubicacionOrigen'),
          texto(deLaLinea?.ubicacionOrigenId?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'ubicacionOrigenId'))}
        opciones={ubicaciones.origen}
        vacia={vacia(ubicaciones.hayOrigen)}
        ancho="w-44"
      />

      <CeldaDeLista
        celda={celda(
          'ubicacionDestinoId',
          t('inventario.transferencias.ubicacionDestino'),
          texto(deLaLinea?.ubicacionDestinoId?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'ubicacionDestinoId'))}
        opciones={ubicaciones.destino}
        vacia={vacia(ubicaciones.hayDestino)}
        ancho="w-44"
      />

      <Controller
        control={control}
        name={rutaDeLaLinea(indice, 'articuloId')}
        render={({ field }) => (
          <CeldaDelArticulo
            celda={celda(
              'articuloId',
              t('inventario.lineas.articulo'),
              texto(deLaLinea?.articuloId?.message),
            )}
            campo={field}
            poner={(campo, valor) => {
              setValue(rutaDeLaLinea(indice, campo), valor);
            }}
            alCambiarDeMarca={alCambiarDeMarca}
          />
        )}
      />

      <CeldaDeTexto
        celda={celda(
          'cantidadIntroducida',
          t('inventario.lineas.cantidad'),
          texto(deLaLinea?.cantidadIntroducida?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'cantidadIntroducida'))}
        ancho="w-28"
      />

      <CeldaDeLista
        celda={celda(
          'unidadIntroducidaId',
          t('inventario.lineas.unidad'),
          texto(deLaLinea?.unidadIntroducidaId?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'unidadIntroducidaId'))}
        opciones={unidades}
        vacia={t('inventario.lineas.sinElegir')}
        ancho="w-36"
      />

      <CeldaDeTexto
        celda={celda(
          'codigoDeLote',
          t('inventario.lineas.lote'),
          texto(deLaLinea?.codigoDeLote?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'codigoDeLote'))}
        ancho="w-32"
        deshabilitada={marca !== 'porLote'}
      />

      <CeldaDeTexto
        celda={celda(
          'numeroDeSerie',
          t('inventario.lineas.numeroDeSerie'),
          texto(deLaLinea?.numeroDeSerie?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'numeroDeSerie'))}
        ancho="w-32"
        deshabilitada={marca !== 'porNumeroSerie'}
      />
    </>
  );
}
