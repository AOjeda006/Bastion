import type { Ajuste } from './ajuste.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

/** Lo que se puede hacer con un ajuste desde su ficha. */
export type AccionDelAjuste = 'confirmar' | 'descartar' | 'anular';

/**
 * La barra de acciones de la ficha, del estado del ajuste y de los permisos (ADR-0065 §8, §10 del
 * plan maestro).
 *
 * - **Un borrador** se confirma o se descarta.
 * - **Uno confirmado** se anula, salvo que sea un inverso, que no se anula
 *   (`ajuste-inverso-no-se-anula`), o el ajuste de un recuento, que se anula desde su recuento
 *   (`ajuste-de-un-recuento-no-se-anula`, ADR-0055 §9).
 * - **Uno anulado, o en un estado que esta versión no conoce**, no ofrece nada.
 *
 * Cada acción, además, con su permiso. La interfaz esconde; quien autoriza es el servidor, que
 * contesta lo mismo a quien llegue con la petición escrita a mano.
 */
export function accionesDisponibles(
  ajuste: Pick<Ajuste, 'estado' | 'anulaAId' | 'recuentoId'>,
  permisos: readonly string[],
): readonly AccionDelAjuste[] {
  const puede = (permiso: string): boolean => permisos.includes(permiso);

  if (ajuste.estado === 'borrador') {
    return [
      ...(puede(PERMISOS.ajusteConfirmar) ? (['confirmar'] as const) : []),
      ...(puede(PERMISOS.ajusteDescartar) ? (['descartar'] as const) : []),
    ];
  }

  if (
    ajuste.estado === 'confirmado' &&
    ajuste.anulaAId === null &&
    ajuste.recuentoId === null &&
    puede(PERMISOS.ajusteAnular)
  ) {
    return ['anular'];
  }

  return [];
}
