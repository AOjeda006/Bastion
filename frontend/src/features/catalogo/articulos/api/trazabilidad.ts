import type { FichaDeArticulo, TrazabilidadElegible } from '../model/articulo.ts';
import { api } from '@/shared/api/cliente.ts';
import { fallo } from '@/shared/api/errores.ts';

/** Del valor de la pantalla al texto del contrato. */
const EN_EL_CONTRATO: Record<TrazabilidadElegible, string> = {
  ninguna: 'Ninguna',
  porLote: 'PorLote',
  porNumeroSerie: 'PorNumeroSerie',
};

/**
 * Guarda la ficha leída con otra trazabilidad, sobre la versión con la que se leyó.
 *
 * Lo demás va tal como llegó (`FichaDeArticulo.intacto`). La respuesta no trae la versión nueva
 * —el `200` del `PUT` no lleva `ETag`—, así que quien llama invalida la ficha y la vuelve a leer.
 *
 * Los rechazos salen como `FalloDeApi`, con su `type`: la pantalla decide si van en el campo —los
 * de la trazabilidad— o arriba, como la versión obsoleta.
 */
export async function cambiarTrazabilidad(
  ficha: FichaDeArticulo,
  trazabilidad: TrazabilidadElegible,
): Promise<void> {
  const { data, error, response } = await api.PUT('/api/v1/catalogo/articulos/{id}', {
    params: { path: { id: ficha.articulo.id }, header: { 'If-Match': ficha.version } },
    body: {
      descripcion: ficha.intacto.descripcion,
      tipo: ficha.intacto.tipo,
      trazabilidad: EN_EL_CONTRATO[trazabilidad],
      impuestoPorDefectoId: ficha.intacto.impuestoPorDefectoId,
      categoriaId: ficha.intacto.categoriaId,
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }
}
