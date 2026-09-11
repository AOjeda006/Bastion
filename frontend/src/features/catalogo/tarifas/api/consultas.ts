import type { ListadoDeTarifas } from '../model/listado.ts';
import type { PaginaDeTarifas, Tarifa } from '../model/tarifa.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de las tarifas con el contrato.
 *
 * `TarifaDto` viene de `esquema.ts`, que se GENERA de `docs/api/openapi.json` con `npm run api`.
 * Nunca se escribe a mano un tipo del contrato: si mañana un tramo dejara de traer la fecha de fin,
 * esto deja de compilar aquí —en una función de ocho líneas— y no en la tabla que lo pintaba.
 */
type TarifaDto = components['schemas']['TarifaDto'];

function traducir(dto: TarifaDto): Tarifa {
  return {
    id: dto.id,
    codigo: dto.codigo,
    nombre: dto.nombre,
    vigenteDesde: dto.vigenteDesde,
    vigenteHasta: dto.vigenteHasta,
  };
}

/**
 * Pide una página de tramos de tarifa de la empresa con la que se está operando.
 *
 * Los dos filtros van por separado porque son dos preguntas distintas, y así los separa la API:
 * `q` es «los que digan esto» —texto parcial sobre el código y el nombre— y `codigo` es «los tramos
 * de ÉSTA», por igualdad exacta. La segunda es la que de verdad se hace aquí: una tarifa son varias
 * filas con el mismo código, y verlas juntas y en orden es cómo se comprueba que la sucesión no ha
 * dejado ningún día sin cubrir.
 *
 * **El orden lo pone el servidor** —código, y dentro de cada uno la vigencia de la más reciente a
 * la más antigua— y aquí no se reordena nada: reordenar una página es reordenar veinte filas de
 * las que hay ciento, que es peor que no ordenar.
 */
export async function consultarTarifas(listado: ListadoDeTarifas): Promise<PaginaDeTarifas> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/tarifas', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.busqueda === '' ? {} : { q: listado.busqueda }),
        ...(listado.codigo === null ? {} : { codigo: listado.codigo }),
      },
    },
  });

  if (data === undefined) {
    // El cuerpo del error va con el estado: de él salen el `type` y la traza, que son lo que decide
    // qué frase lee una persona (ADR-0030, y `useTextoDeFallo`).
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}
