import { estadoDelContrato, estadoEnElContrato } from '../model/listado.ts';
import type { ListadoDeTransferencias } from '../model/listado.ts';
import type { Pagina, ResumenDeTransferencia } from '../model/transferencia.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de las transferencias con el contrato (ADR-0068).
 *
 * Los tipos del contrato no pasan de aquí: cada respuesta se traduce al modelo de la pantalla. Los
 * enteros llegan como `number | string`, porque la API admite las dos formas al leer y el
 * documento describe un solo esquema (`enteros.ts`); se estrechan aquí, una vez.
 */
type TransferenciaResumenDto = components['schemas']['TransferenciaResumenDto'];

function traducirResumen(dto: TransferenciaResumenDto): ResumenDeTransferencia {
  return {
    id: dto.id,
    numero: dto.numero === null ? null : enteroDelContrato(dto.numero),
    almacenOrigenId: dto.almacenOrigenId,
    almacenDestinoId: dto.almacenDestinoId,
    fechaDeEnvio: dto.fechaDeEnvio,
    fechaDeRecepcion: dto.fechaDeRecepcion,
    estado: estadoDelContrato(dto.estado),
    anulaAId: dto.anulaAId,
  };
}

/** Una página de transferencias, en el orden pedido, o del último envío al primero, el de la API. */
export async function consultarTransferencias(
  listado: ListadoDeTransferencias,
): Promise<Pagina<ResumenDeTransferencia>> {
  const { data, error, response } = await api.GET('/api/v1/inventario/transferencias', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.estado === null ? {} : { estado: estadoEnElContrato(listado.estado) }),
        ...(listado.origenId === null ? {} : { origen: listado.origenId }),
        ...(listado.destinoId === null ? {} : { destino: listado.destinoId }),
        ...(listado.orden === null ? {} : { sort: listado.orden }),
      },
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducirResumen), total: enteroDelContrato(data.total) };
}
