import type { CodigoDeBarras, NivelDeGtin, NivelElegible } from '../model/codigoDeBarras.ts';
import type { DatosDeAltaDeGtin } from '../model/esquemaDeAltaDeGtin.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

type CodigoBarrasDto = components['schemas']['CodigoBarrasDto'];
type AgregarCodigoBarrasDto = components['schemas']['AgregarCodigoBarrasDto'];

/** La cabecera de la clave de idempotencia, tal como la lee el filtro del servidor. */
const CABECERA_DE_IDEMPOTENCIA = 'Idempotency-Key';

/** Del valor de la pantalla al texto del contrato: `Palet` en el código, «palé» en la pantalla. */
const EN_EL_CONTRATO: Record<NivelElegible, string> = {
  base: 'Base',
  caja: 'Caja',
  palet: 'Palet',
};

/**
 * El nivel que dice el contrato, o «desconocido» si esta versión no lo conoce.
 *
 * Con un `switch` y no con un registro y `??`: un registro es un objeto, y `constructor` o
 * `toString` están en él por herencia. Un nivel nuevo con ese nombre saldría como una función.
 */
function nivelDe(texto: string): NivelDeGtin {
  switch (texto) {
    case 'Base':
      return 'base';
    case 'Caja':
      return 'caja';
    case 'Palet':
      return 'palet';
    default:
      return 'desconocido';
  }
}

function traducir(dto: CodigoBarrasDto): CodigoDeBarras {
  return {
    id: dto.id,
    gtin: dto.gtin,
    nivel: nivelDe(dto.nivel),
    unidades: enteroDelContrato(dto.unidades),
  };
}

/** Los códigos de barras de un artículo, de la base a las agrupaciones, como los ordena la API. */
export async function consultarCodigosDeBarras(articuloId: string): Promise<CodigoDeBarras[]> {
  const { data, error, response } = await api.GET('/api/v1/catalogo/articulos/{articuloId}/gtins', {
    params: { path: { articuloId } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return data.map(traducir);
}

/**
 * El cuerpo que se manda para dar de alta lo que dice el formulario.
 *
 * La base no manda unidades: lleva una y la pone el servidor. Es también lo que la pantalla compara
 * para saber si un envío repite el intento anterior o es otro (la clave de idempotencia).
 */
export function cuerpoDelAlta(datos: DatosDeAltaDeGtin): AgregarCodigoBarrasDto {
  return {
    gtin: datos.gtin.trim(),
    nivel: EN_EL_CONTRATO[datos.nivel],
    ...(datos.nivel === 'base' ? {} : { unidades: Number.parseInt(datos.unidades.trim(), 10) }),
  };
}

/**
 * Da de alta un código de barras en un artículo, con la clave del intento.
 *
 * Los rechazos salen como `FalloDeApi` con su `type`, y la pantalla decide en qué campo van.
 */
export async function agregarCodigoDeBarras(
  articuloId: string,
  cuerpo: AgregarCodigoBarrasDto,
  clave: string,
): Promise<CodigoDeBarras> {
  const { data, error, response } = await api.POST(
    '/api/v1/catalogo/articulos/{articuloId}/gtins',
    {
      params: { path: { articuloId } },
      body: cuerpo,
      headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
    },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}

/**
 * Quita un código de barras, con la versión de su fila.
 *
 * <b>La versión se lee justo antes.</b> El listado no la trae, y la fila no cambia nunca: la versión
 * que hay es la de su alta. Lo que el `If-Match` sigue parando es borrar una fila que ya no es la
 * que se vio. Si ya no está, la lectura contesta el `404` de quien llegó antes, y eso es lo que se
 * dice.
 */
export async function quitarCodigoDeBarras(id: string): Promise<void> {
  const lectura = await api.GET('/api/v1/catalogo/articulos/gtins/{id}', {
    params: { path: { id } },
  });

  if (lectura.data === undefined) {
    throw fallo(lectura.response.status, lectura.error);
  }

  const version = lectura.response.headers.get('ETag');

  // Sin ETag la baja sería un 428 seguro; se trata como un fallo de carga, como la ficha.
  if (version === null) {
    throw fallo(lectura.response.status);
  }

  const { error, response } = await api.DELETE('/api/v1/catalogo/articulos/gtins/{id}', {
    params: { path: { id }, header: { 'If-Match': version } },
  });

  if (!response.ok) {
    throw fallo(response.status, error);
  }
}
