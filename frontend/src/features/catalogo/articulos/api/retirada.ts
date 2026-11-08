import type { FichaDeArticulo } from '../model/articulo.ts';
import { api } from '@/shared/api/cliente.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * Retirar y reincorporar un artículo (ADR-0082): poner y quitar el sub-recurso `retirada`, con la
 * versión de la ficha leída.
 *
 * **La versión es la de la ficha que enseña la ventana**, leída al abrirla, y no una leída justo
 * antes de escribir: si alguien ha cambiado el artículo mientras tanto, el `If-Match` lo para con
 * un `412`, y la ventana ofrece cargar la de ahora. Las dos transiciones son idempotentes: retirar
 * uno que ya está retirado contesta `204` igual, y queda como se pidió.
 */
export async function retirarArticulo(ficha: FichaDeArticulo): Promise<void> {
  const { error, response } = await api.POST('/api/v1/catalogo/articulos/{id}/retirada', {
    params: { path: { id: ficha.articulo.id }, header: { 'If-Match': ficha.version } },
  });

  if (!response.ok) {
    throw fallo(response.status, error);
  }
}

/** Lo contrario, con el mismo cuidado: el `DELETE` borra la retirada, no la ficha. */
export async function reincorporarArticulo(ficha: FichaDeArticulo): Promise<void> {
  const { error, response } = await api.DELETE('/api/v1/catalogo/articulos/{id}/retirada', {
    params: { path: { id: ficha.articulo.id }, header: { 'If-Match': ficha.version } },
  });

  if (!response.ok) {
    throw fallo(response.status, error);
  }
}
