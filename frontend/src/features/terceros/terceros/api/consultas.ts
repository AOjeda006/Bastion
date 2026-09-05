import type { ListadoDeTerceros } from '../model/listado.ts';
import type { PaginaDeTerceros, Tercero, Verificacion } from '../model/tercero.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de esta funcionalidad con el contrato.
 *
 * `TerceroDto` viene de `esquema.ts`, que se GENERA de `docs/api/openapi.json` con `npm run api`.
 * Nunca se escribe a mano un tipo del contrato: si mañana la identificación dejara de traer el
 * estado de verificación, esto deja de compilar aquí —en una función de diez líneas— y no en la
 * tabla que lo pintaba.
 */
type TerceroDto = components['schemas']['TerceroDto'];

/**
 * Del texto del contrato al valor que esta funcionalidad pinta.
 *
 * El enumerado viaja como texto, así que traducirlo es leer un dato de fuera: todo lo que no sea
 * uno de los dos valores conocidos sale como `desconocida`, y la tabla lo dice. La alternativa
 * —dar por bueno lo que no se reconoce— es justo lo que este módulo existe para impedir.
 */
function verificacionDe(texto: string): Verificacion {
  if (texto === 'VerificadoPorAlgoritmo') {
    return 'verificado';
  }

  return texto === 'NoVerificado' ? 'sinVerificar' : 'desconocida';
}

function traducir(dto: TerceroDto): Tercero {
  return {
    id: dto.id,
    pais: dto.identificacion.pais,
    numero: dto.identificacion.numero,
    verificacion: verificacionDe(dto.identificacion.verificacion),
    razonSocial: dto.razonSocial,
    nombreComercial: dto.nombreComercial,
    poblacion: dto.domicilioFiscal.poblacion,
    esCliente: dto.esCliente,
    esProveedor: dto.esProveedor,
  };
}

/**
 * Pide una página de terceros de la empresa con la que se está operando.
 *
 * El filtro viaja como `q`, que en esta ruta busca por razón social y nombre comercial. **Lo que no
 * puede viajar por aquí es el identificador fiscal** (ADR-0025): para eso está
 * `POST /api/v1/terceros/terceros/buscar`, que lo lleva en el cuerpo.
 */
export async function consultarTerceros(listado: ListadoDeTerceros): Promise<PaginaDeTerceros> {
  const { data, error, response } = await api.GET('/api/v1/terceros/terceros', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.busqueda === '' ? {} : { q: listado.busqueda }),
      },
    },
  });

  if (data === undefined) {
    // El cuerpo del error va con el estado: de él salen el `type` y la traza, que son
    // lo que decide qué frase lee una persona (ADR-0030, y `useTextoDeFallo`).
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducir), total: enteroDelContrato(data.total) };
}
