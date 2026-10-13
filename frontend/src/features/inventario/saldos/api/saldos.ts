import type { ListadoDeSaldos, Saldo, SaldosAUnaFecha } from '../model/saldo.ts';
import type { Maestro } from '../../recuentos/api/maestros.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

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
        ...(listado.fecha === null ? {} : { fecha: listado.fecha }),
        ...(listado.almacenId === null ? {} : { almacen: listado.almacenId }),
        ...(listado.articuloId === null ? {} : { articulo: listado.articuloId }),
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
