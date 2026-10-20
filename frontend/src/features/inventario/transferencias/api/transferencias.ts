import type { AltaDeTransferencia } from '../model/formularios.ts';
import { estadoDelContrato, estadoEnElContrato } from '../model/listado.ts';
import type { ListadoDeTransferencias } from '../model/listado.ts';
import type {
  Anulacion,
  FichaDeTransferencia,
  LineaDeTransferencia,
  Pagina,
  ResumenDeTransferencia,
  Transferencia,
} from '../model/transferencia.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

/**
 * La frontera de las transferencias con el contrato (ADR-0068).
 *
 * Los tipos del contrato no pasan de aquí: cada respuesta se traduce al modelo de la pantalla. Los
 * enteros y las cantidades llegan como `number | string`, porque la API admite las dos formas al
 * leer y el documento describe un solo esquema (`enteros.ts`); se estrechan aquí, una vez.
 */
type FichaDeTransferenciaDto = components['schemas']['FichaDeTransferenciaDto'];
type LineaDeFichaDeTransferenciaDto = components['schemas']['LineaDeFichaDeTransferenciaDto'];
type TransferenciaDto = components['schemas']['TransferenciaDto'];
type TransferenciaResumenDto = components['schemas']['TransferenciaResumenDto'];

const CABECERA_DE_IDEMPOTENCIA = 'Idempotency-Key';

function traducirResumen(
  dto: TransferenciaResumenDto | TransferenciaDto | FichaDeTransferenciaDto,
): ResumenDeTransferencia {
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

function traducirLinea(dto: LineaDeFichaDeTransferenciaDto): LineaDeTransferencia {
  return {
    numero: enteroDelContrato(dto.numero),
    ubicacionOrigenId: dto.ubicacionOrigenId,
    ubicacionDestinoId: dto.ubicacionDestinoId,
    articuloId: dto.articuloId,
    cantidadIntroducida: Number(dto.cantidadIntroducida),
    unidadIntroducidaId: dto.unidadIntroducidaId,
    factorAUnidadBase: Number(dto.factorAUnidadBase),
    cantidadEnUnidadBase: Number(dto.cantidadEnUnidadBase),
    codigoDeLote: dto.codigoDeLote,
    numeroDeSerie: dto.numeroDeSerie,
    // Solo se pinta: la aritmética que cuenta la hace el servidor.
    valor: dto.valor === null ? null : Number(dto.valor),
  };
}

function traducir(dto: FichaDeTransferenciaDto): Transferencia {
  return {
    ...traducirResumen(dto),
    divisa: dto.divisa,
    motivo: dto.motivo,
    lineas: dto.lineas.map(traducirLinea),
  };
}

/**
 * La versión de una respuesta, o un fallo de carga si no la trae.
 *
 * Sin `ETag` no se puede descartar: lo siguiente sería un `428` seguro. Se trata como en el ajuste:
 * un intermediario que lo quita es un fallo de carga, no una ficha que miente.
 */
function versionDe(respuesta: Response): string {
  const version = respuesta.headers.get('ETag');

  if (version === null) {
    throw fallo(respuesta.status);
  }

  return version;
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

/**
 * Abre un borrador con sus líneas. Con la clave del intento, que es obligatoria (ADR-0068 §1):
 * repetir el mismo envío devuelve el borrador de la primera vez en vez de abrir otro.
 */
export async function abrirTransferencia(
  alta: AltaDeTransferencia,
  clave: string,
): Promise<ResumenDeTransferencia> {
  const { data, error, response } = await api.POST('/api/v1/inventario/transferencias', {
    body: { ...alta, lineas: alta.lineas.map((linea) => ({ ...linea })) },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirResumen(data);
}

/** La ficha de una, con todas sus líneas y la versión que cita el descarte. */
export async function consultarTransferencia(id: string): Promise<FichaDeTransferencia> {
  const { data, error, response } = await api.GET('/api/v1/inventario/transferencias/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { transferencia: traducir(data), version: versionDe(response) };
}

/**
 * Envía un borrador: recibe su número, sale del origen y queda en tránsito. Con la clave del
 * intento y sin `If-Match`, porque lo que la ficha enseña de un borrador solo cambia de estado
 * (ADR-0068 §7).
 */
export async function enviarTransferencia(
  id: string,
  clave: string,
): Promise<ResumenDeTransferencia> {
  const { data, error, response } = await api.POST('/api/v1/inventario/transferencias/{id}/envio', {
    params: { path: { id } },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirResumen(data);
}

/** Recibe una enviada el día `fechaDeRecepcion`: sale del tránsito y entra en el destino. */
export async function recibirTransferencia(
  id: string,
  fechaDeRecepcion: string,
  clave: string,
): Promise<ResumenDeTransferencia> {
  const { data, error, response } = await api.POST(
    '/api/v1/inventario/transferencias/{id}/recepcion',
    {
      params: { path: { id } },
      body: { fechaDeRecepcion },
      headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
    },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirResumen(data);
}

/** Anula una enviada o una recibida: el inverso que la compensa nace recibido y numerado. */
export async function anularTransferencia(
  id: string,
  motivo: string,
  clave: string,
): Promise<Anulacion> {
  const { data, error, response } = await api.POST(
    '/api/v1/inventario/transferencias/{id}/anulacion',
    {
      params: { path: { id } },
      body: { motivo },
      headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
    },
  );

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { original: traducirResumen(data.original), inverso: traducirResumen(data.inverso) };
}

/**
 * Descarta un borrador con la versión que se vio (ADR-0068 §5). Contesta `204`, sin cuerpo: el
 * borrador deja de existir para la API, y lo que queda es saber que salió bien.
 */
export async function descartarTransferencia(id: string, version: string): Promise<void> {
  const { error, response } = await api.DELETE('/api/v1/inventario/transferencias/{id}', {
    params: { path: { id }, header: { 'If-Match': version } },
  });

  if (!response.ok) {
    throw fallo(response.status, error);
  }
}
