import type { ListadoDeSaldos, Saldo, SaldosAUnaFecha } from '../model/saldo.ts';
import type { Maestro } from '../../maestros/api/maestros.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components, operations } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';
import { ficheroDe, type Fichero } from '@/shared/api/exportacion.ts';

/**
 * La frontera del saldo con el contrato (ADR-0055, y ADR-0063 §8).
 *
 * Los tipos del contrato no pasan de aquí. Las cantidades y los importes llegan como
 * `number | string`, porque la API admite las dos formas al leer y el documento describe un solo
 * esquema (`enteros.ts`); se estrechan aquí, una vez. **Solo se pintan**: la aritmética que cuenta
 * la hizo el servidor.
 */
type SaldoDto = components['schemas']['SaldoDto'];

function traducir(dto: SaldoDto): Saldo {
  return {
    articuloId: dto.articuloId,
    almacenId: dto.almacenId,
    cantidad: Number(dto.cantidad),
    valor: Number(dto.valor),
    enTransito: Number(dto.cantidadEnTransito),
    valorEnTransito: Number(dto.valorEnTransito),
    divisa: dto.divisa,
  };
}

/** Lo que admite la exportación, que son los filtros del listado sin `page` ni `size`. */
type Filtros = NonNullable<operations['Saldos_Exportar']['parameters']['query']>;

/** El día y los filtros del listado, sin la página: los que llevan el listado y su exportación. */
function filtrosDe(listado: ListadoDeSaldos): Filtros {
  return {
    ...(listado.fecha === null ? {} : { fecha: listado.fecha }),
    ...(listado.almacenId === null ? {} : { almacen: listado.almacenId }),
    ...(listado.articuloId === null ? {} : { articulo: listado.articuloId }),
  };
}

/**
 * Una página de saldos al final del día pedido, por artículo y almacén, como los ordena la API.
 *
 * Sin día, no se manda ninguno: contesta el servidor con el suyo, y la respuesta dice cuál fue.
 * Así la pantalla enseña el día que de verdad se ha contestado, y no el que creía el navegador.
 */
export async function consultarSaldos(listado: ListadoDeSaldos): Promise<SaldosAUnaFecha> {
  const { data, error, response } = await api.GET('/api/v1/inventario/saldos', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...filtrosDe(listado),
      },
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return {
    fecha: data.fecha,
    elementos: data.saldos.elementos.map(traducir),
    total: enteroDelContrato(data.saldos.total),
  };
}

/**
 * Los saldos del listado, con sus filtros y todas sus páginas, en el CSV de la exportación.
 *
 * **El día va siempre, y es el que contestó el listado**, no el del campo: sin fecha, el listado
 * dice el día del servidor, y la exportación pedida sin ella diría el suyo, que pasada la
 * medianoche ya es otro. El fichero tiene que ser la tabla que se está mirando.
 *
 * @param listado - Los filtros de la pantalla.
 * @param dia - El día que dice la tabla, el que contestó el servidor al listado.
 */
export async function exportarSaldos(listado: ListadoDeSaldos, dia: string): Promise<Fichero> {
  return ficheroDe(
    await api.GET('/api/v1/inventario/saldos/exportacion', {
      params: { query: filtrosDe({ ...listado, fecha: dia }) },
      parseAs: 'blob',
    }),
  );
}

/**
 * El tope de la página del desplegable, que es el del servidor.
 *
 * Una empresa con más de doscientos artículos no verá los siguientes en el filtro. Queda anotado
 * en el README de la funcionalidad, con su disparador, como el de los almacenes del recuento.
 */
const LOS_QUE_CABEN = 200;

/** Los artículos que se ofrecen para acotar, preguntados a su dueño (ADR-0055 §12). */
export async function consultarArticulos(): Promise<Maestro[]> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/articulos', {
    params: { query: { page: 1, size: LOS_QUE_CABEN } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.elementos.map((a) => ({ id: a.id, codigo: a.codigo, nombre: a.descripcion }));
}
