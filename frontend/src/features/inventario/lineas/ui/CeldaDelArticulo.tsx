import { BuscadorDeArticulo } from './BuscadorDeArticulo.tsx';
import { Celda } from './Celda.tsx';
import type { CeldaDeLaLinea } from './useCeldasDeLaLinea.ts';
import type { Trazabilidad } from '../../maestros/api/maestros.ts';

/** Los campos de la línea que cambian al elegir un artículo. */
export type CampoQueCambiaConElArticulo = 'unidadIntroducidaId' | 'codigoDeLote' | 'numeroDeSerie';

/**
 * La celda del artículo, con su buscador.
 *
 * Elegir uno **ofrece primero su unidad base**, que no necesita conversión, y vacía el lote y la
 * serie que no le valen: los de otro artículo no son de este.
 */
export function CeldaDelArticulo({
  celda,
  campo,
  poner,
  alCambiarDeMarca,
}: {
  celda: CeldaDeLaLinea;
  /** El campo del artículo, como lo da el `Controller` del formulario. */
  campo: {
    readonly value: string;
    readonly onChange: (articuloId: string) => void;
    readonly onBlur: () => void;
    readonly ref: React.Ref<HTMLInputElement>;
  };
  /** Escribe un campo de esta misma línea. */
  poner: (campo: CampoQueCambiaConElArticulo, valor: string) => void;
  /** Avisa de la trazabilidad del artículo elegido, o nula si se deja sin ninguno. */
  alCambiarDeMarca: (marca: Trazabilidad | null) => void;
}): React.JSX.Element {
  const { id } = celda.propiedades;

  return (
    <Celda error={celda.error} id={id}>
      <BuscadorDeArticulo
        id={id}
        etiqueta={celda.propiedades['aria-label']}
        articuloId={campo.value}
        error={celda.error}
        idDelError={`${id}-error`}
        referencia={campo.ref}
        alElegir={(elegido) => {
          campo.onChange(elegido?.id ?? '');
          alCambiarDeMarca(elegido?.trazabilidad ?? null);

          if (elegido === null) {
            return;
          }

          poner('unidadIntroducidaId', elegido.unidadBaseId);

          if (elegido.trazabilidad !== 'porLote') {
            poner('codigoDeLote', '');
          }

          if (elegido.trazabilidad !== 'porNumeroSerie') {
            poner('numeroDeSerie', '');
          }
        }}
        alSalir={campo.onBlur}
        alPulsarIntro={celda.propiedades.onKeyDown}
      />
    </Celda>
  );
}
