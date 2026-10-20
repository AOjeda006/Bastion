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

/**
 * Las líneas del alta del ajuste: el marco compartido, con las columnas del ajuste (ADR-0065 §8,
 * ADR-0068 §8).
 *
 * - **Una ubicación**, de las del almacén elegido.
 * - **El coste**, que solo lleva lo que entra.
 * - **El lote y el número de serie** solo se escriben si el artículo elegido se sigue así; si no, la
 *   celda está deshabilitada y no cuenta como la última.
 */
export function RejillaDelAjuste({
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
  const marcas = useMarcasDeLasLineas();

  return (
    <MarcoDeLineas
      titulo={t('inventario.ajustes.alta.tabla')}
      columnas={[
        t('inventario.lineas.ubicacion'),
        t('inventario.lineas.articulo'),
        t('inventario.lineas.cantidad'),
        t('inventario.lineas.unidad'),
        t('inventario.ajustes.alta.coste'),
        t('inventario.lineas.lote'),
        t('inventario.lineas.numeroDeSerie'),
      ]}
      tope={t('inventario.ajustes.alta.topeDeLineas')}
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
          hayAlmacen={hayAlmacen}
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
  hayAlmacen,
  marca,
  alCambiarDeMarca,
  texto,
  alPulsarIntro,
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

  return (
    <>
      <CeldaDeLista
        celda={celda(
          'ubicacionId',
          t('inventario.lineas.ubicacion'),
          texto(deLaLinea?.ubicacionId?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'ubicacionId'))}
        opciones={ubicaciones}
        vacia={
          hayAlmacen ? t('inventario.lineas.sinElegir') : t('inventario.lineas.antesElAlmacen')
        }
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
          'costeUnitario',
          t('inventario.ajustes.alta.coste'),
          texto(deLaLinea?.costeUnitario?.message),
        )}
        registro={register(rutaDeLaLinea(indice, 'costeUnitario'))}
        ancho="w-28"
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
