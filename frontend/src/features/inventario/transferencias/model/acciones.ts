import type { Transferencia } from './transferencia.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

/** Lo que se puede hacer con una transferencia desde su ficha. */
export type AccionDeLaTransferencia = 'enviar' | 'descartar' | 'recibir' | 'anular';

/**
 * La barra de acciones de la ficha, del estado de la transferencia y de los permisos (ADR-0068
 * §8).
 *
 * - **Un borrador** se envía o se descarta.
 * - **Una enviada** se recibe, o se anula.
 * - **Una recibida** se anula, salvo que sea un inverso, que no se deshace con otro
 *   (`transferencia-no-se-anula`). El inverso nace recibido.
 * - **Una anulada, o en un estado que esta versión no conoce**, no ofrece nada.
 *
 * Cada acción, además, con su permiso. La interfaz esconde; quien autoriza es el servidor, que
 * contesta lo mismo a quien llegue con la petición escrita a mano.
 */
export function accionesDisponibles(
  transferencia: Pick<Transferencia, 'estado' | 'anulaAId'>,
  permisos: readonly string[],
): readonly AccionDeLaTransferencia[] {
  const puede = (permiso: string): boolean => permisos.includes(permiso);
  const seAnula =
    transferencia.anulaAId === null && puede(PERMISOS.transferenciaAnular)
      ? (['anular'] as const)
      : [];

  switch (transferencia.estado) {
    case 'borrador':
      return [
        ...(puede(PERMISOS.transferenciaEnviar) ? (['enviar'] as const) : []),
        ...(puede(PERMISOS.transferenciaDescartar) ? (['descartar'] as const) : []),
      ];
    case 'enviada':
      return [...(puede(PERMISOS.transferenciaRecibir) ? (['recibir'] as const) : []), ...seAnula];
    case 'recibida':
      return seAnula;
    case 'anulada':
    case 'desconocido':
      return [];
  }
}
