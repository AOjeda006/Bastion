import { estadoDelContrato, estadoEnElContrato } from '../model/listado.ts';
import type { ListadoDeLineas, ListadoDeRecuentos } from '../model/listado.ts';
import type {
  FichaDeLinea,
  FichaDeRecuento,
  LineaDeRecuento,
  Pagina,
  Recuento,
  ResumenDeRecuento,
} from '../model/recuento.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de los recuentos con el contrato (ADR-0055).
 *
 * Los tipos del contrato no pasan de aquí: cada respuesta se traduce al modelo de la pantalla. Los
 * enteros y las cantidades llegan como `number | string`, porque la API admite las dos formas al
 * leer y el documento describe un solo esquema (`enteros.ts`); se estrechan aquí, una vez.
 */
type RecuentoDto = components['schemas']['RecuentoDto'];
type RecuentoResumenDto = components['schemas']['RecuentoResumenDto'];
type LineaDeRecuentoDto = components['schemas']['LineaDeRecuentoDto'];
type AbrirRecuentoDto = components['schemas']['AbrirRecuentoDto'];

const CABECERA_DE_IDEMPOTENCIA = 'Idempotency-Key';

function enteroOpcional(valor: number | string | null): number | null {
  return valor === null ? null : enteroDelContrato(valor);
}

/** Una cantidad del contrato. Solo se pinta: la aritmética que cuenta la hace el servidor. */
function cantidad(valor: number | string | null): number | null {
  return valor === null ? null : Number(valor);
}

function traducirResumen(dto: RecuentoResumenDto): ResumenDeRecuento {
  return {
    id: dto.id,
    numero: enteroOpcional(dto.numero),
    almacenId: dto.almacenId,
    fechaDeApertura: dto.fechaDeApertura,
    fechaDeConfirmacion: dto.fechaDeConfirmacion,
    estado: estadoDelContrato(dto.estado),
    motivo: dto.motivo,
  };
}

function traducir(dto: RecuentoDto): Recuento {
  return {
    ...traducirResumen(dto),
    motivoDelDescarte: dto.motivoDelDescarte,
    motivoDeLaAnulacion: dto.motivoDeLaAnulacion,
    ajusteId: dto.ajusteId,
    lineas: enteroDelContrato(dto.lineas),
    lineasSinContar: enteroDelContrato(dto.lineasSinContar),
    lineasConElTeoricoCambiado: enteroOpcional(dto.lineasConElTeoricoCambiado),
    lineasConTransito: enteroOpcional(dto.lineasConTransito),
    huellaDelTeorico: dto.huellaDelTeorico,
  };
}

function traducirLinea(dto: LineaDeRecuentoDto): LineaDeRecuento {
  return {
    id: dto.id,
    numero: enteroDelContrato(dto.numero),
    ubicacionId: dto.ubicacionId,
    articuloId: dto.articuloId,
    codigoDeLote: dto.codigoDeLote,
    numeroDeSerie: dto.numeroDeSerie,
    unidadBaseId: dto.unidadBaseId,
    contado: cantidad(dto.contado),
    teoricoAlContar: cantidad(dto.teoricoAlContar),
    teorico: cantidad(dto.teorico),
    enTransito: cantidad(dto.enTransito),
    diferencia: cantidad(dto.diferencia),
    teoricoCambiado: dto.teoricoCambiado,
  };
}

/**
 * La versión de una respuesta, o un fallo de carga si no la trae.
 *
 * Sin `ETag` no se puede escribir: lo siguiente sería un `428` seguro. Se trata como la ficha del
 * artículo: un intermediario que lo quita es un fallo de carga, no un formulario que miente.
 */
function versionDe(respuesta: Response): string {
  const version = respuesta.headers.get('ETag');

  if (version === null) {
    throw fallo(respuesta.status);
  }

  return version;
}

/** Una página de recuentos, del más reciente al más antiguo, como los ordena la API. */
export async function consultarRecuentos(
  listado: ListadoDeRecuentos,
): Promise<Pagina<ResumenDeRecuento>> {
  const { data, error, response } = await api.GET('/api/v1/inventario/recuentos', {
    params: {
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.estado === null ? {} : { estado: estadoEnElContrato(listado.estado) }),
        ...(listado.almacenId === null ? {} : { almacen: listado.almacenId }),
      },
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducirResumen), total: enteroDelContrato(data.total) };
}

/** La ficha de uno, con la versión que citan confirmar, anular y descartar. */
export async function consultarRecuento(id: string): Promise<FichaDeRecuento> {
  const { data, error, response } = await api.GET('/api/v1/inventario/recuentos/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { recuento: traducir(data), version: versionDe(response) };
}

/** Abre un recuento del almacén entero, con la clave del intento. Devuelve el recuento abierto. */
export async function abrirRecuento(cuerpo: AbrirRecuentoDto, clave: string): Promise<Recuento> {
  const { data, error, response } = await api.POST('/api/v1/inventario/recuentos', {
    body: cuerpo,
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}

/** Una página de líneas, por su número, o solo las de una vista parcial. */
export async function consultarLineas(
  id: string,
  listado: ListadoDeLineas,
): Promise<Pagina<LineaDeRecuento>> {
  const { data, error, response } = await api.GET('/api/v1/inventario/recuentos/{id}/lineas', {
    params: {
      path: { id },
      query: {
        page: listado.pagina,
        size: listado.tamanio,
        ...(listado.solo === null ? {} : { solo: listado.solo }),
      },
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { elementos: data.elementos.map(traducirLinea), total: enteroDelContrato(data.total) };
}

/**
 * Una línea sola, con su versión.
 *
 * **La versión se lee justo antes de contar.** El listado no la trae, y cada línea tiene la suya:
 * contar una no cambia la de las demás. Lo que el `If-Match` para es pisar lo que otra persona
 * acaba de contar en la misma línea.
 */
export async function consultarLinea(id: string, lineaId: string): Promise<FichaDeLinea> {
  const { data, error, response } = await api.GET(
    '/api/v1/inventario/recuentos/{id}/lineas/{lineaId}',
    { params: { path: { id, lineaId } } },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { linea: traducirLinea(data), version: versionDe(response) };
}

/** Anota lo contado en una línea. `contado` va como texto, tal como sale de `contadoEscrito`. */
export async function contarLinea(
  id: string,
  lineaId: string,
  version: string,
  contado: string,
): Promise<LineaDeRecuento> {
  const { data, error, response } = await api.PUT(
    '/api/v1/inventario/recuentos/{id}/lineas/{lineaId}',
    {
      params: { path: { id, lineaId }, header: { 'If-Match': version } },
      body: { contado },
    },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirLinea(data);
}

/**
 * Lo que confirmar, anular y descartar tienen en común: el recuento, la versión que se vio, la
 * clave del intento y lo que se manda.
 */
interface Cierre {
  readonly id: string;
  readonly version: string;
  readonly clave: string;
}

/**
 * Confirma: congela el teórico y genera el ajuste de la diferencia (ADR-0055, ADR-0057).
 *
 * Lleva la huella del teórico que se vio. Si el almacén se ha movido desde entonces, el servidor
 * contesta `409` `recuento-teorico-cambiado` antes de escribir nada.
 */
export async function confirmarRecuento(
  cierre: Cierre & { readonly huellaDelTeorico: string },
): Promise<Recuento> {
  const { data, error, response } = await api.POST(
    '/api/v1/inventario/recuentos/{id}/confirmacion',
    {
      params: { path: { id: cierre.id }, header: { 'If-Match': cierre.version } },
      body: { huellaDelTeorico: cierre.huellaDelTeorico },
      headers: { [CABECERA_DE_IDEMPOTENCIA]: cierre.clave },
    },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}

/** Anula uno confirmado: su ajuste se deshace con un inverso, con la fecha de hoy. */
export async function anularRecuento(
  cierre: Cierre & { readonly motivo: string },
): Promise<Recuento> {
  const { data, error, response } = await api.POST('/api/v1/inventario/recuentos/{id}/anulacion', {
    params: { path: { id: cierre.id }, header: { 'If-Match': cierre.version } },
    body: { motivo: cierre.motivo },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: cierre.clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}

/** Descarta uno en curso: no ha movido nada, y deja libre el almacén para otro. */
export async function descartarRecuento(
  cierre: Cierre & { readonly motivo: string },
): Promise<Recuento> {
  const { data, error, response } = await api.POST('/api/v1/inventario/recuentos/{id}/descarte', {
    params: { path: { id: cierre.id }, header: { 'If-Match': cierre.version } },
    body: { motivo: cierre.motivo },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: cierre.clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}
