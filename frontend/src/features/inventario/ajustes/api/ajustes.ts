import type {
  Ajuste,
  Anulacion,
  FichaDeAjuste,
  LineaDeAjuste,
  Pagina,
  ResumenDeAjuste,
} from '../model/ajuste.ts';
import type { AltaDeAjuste } from '../model/formularios.ts';
import { estadoDelContrato, estadoEnElContrato } from '../model/listado.ts';
import type { ListadoDeAjustes } from '../model/listado.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components, operations } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';
import { ficheroDe, type Fichero } from '@/shared/api/exportacion.ts';

/**
 * La frontera de los ajustes con el contrato (ADR-0065).
 *
 * Los tipos del contrato no pasan de aquí: cada respuesta se traduce al modelo de la pantalla. Los
 * enteros y las cantidades llegan como `number | string`, porque la API admite las dos formas al
 * leer y el documento describe un solo esquema (`enteros.ts`); se estrechan aquí, una vez.
 */
type AjusteDto = components['schemas']['AjusteDto'];
type AjusteResumenDto = components['schemas']['AjusteResumenDto'];
type FichaDeAjusteDto = components['schemas']['FichaDeAjusteDto'];
type LineaDeFichaDeAjusteDto = components['schemas']['LineaDeFichaDeAjusteDto'];

const CABECERA_DE_IDEMPOTENCIA = 'Idempotency-Key';

function enteroOpcional(valor: number | string | null): number | null {
  return valor === null ? null : enteroDelContrato(valor);
}

/** Una cifra del contrato. Solo se pinta: la aritmética que cuenta la hace el servidor. */
function cifra(valor: number | string | null): number | null {
  return valor === null ? null : Number(valor);
}

function traducirResumen(dto: AjusteResumenDto | AjusteDto): ResumenDeAjuste {
  return {
    id: dto.id,
    numero: enteroOpcional(dto.numero),
    almacenId: dto.almacenId,
    fechaDeOperacion: dto.fechaDeOperacion,
    estado: estadoDelContrato(dto.estado),
    motivo: dto.motivo,
  };
}

function traducirLinea(dto: LineaDeFichaDeAjusteDto): LineaDeAjuste {
  return {
    numero: enteroDelContrato(dto.numero),
    ubicacionId: dto.ubicacionId,
    articuloId: dto.articuloId,
    cantidadIntroducida: Number(dto.cantidadIntroducida),
    unidadIntroducidaId: dto.unidadIntroducidaId,
    factorAUnidadBase: Number(dto.factorAUnidadBase),
    cantidadEnUnidadBase: Number(dto.cantidadEnUnidadBase),
    costeUnitario: cifra(dto.costeUnitario),
    codigoDeLote: dto.codigoDeLote,
    numeroDeSerie: dto.numeroDeSerie,
    valor: cifra(dto.valor),
  };
}

function traducir(dto: FichaDeAjusteDto): Ajuste {
  return {
    ...traducirResumen(dto),
    divisa: dto.divisa,
    anulaAId: dto.anulaAId,
    recuentoId: dto.recuentoId,
    lineas: dto.lineas.map(traducirLinea),
  };
}

/**
 * La versión de una respuesta, o un fallo de carga si no la trae.
 *
 * Sin `ETag` no se puede descartar: lo siguiente sería un `428` seguro. Se trata como en el
 * recuento: un intermediario que lo quita es un fallo de carga, no una ficha que miente.
 */
function versionDe(respuesta: Response): string {
  const version = respuesta.headers.get('ETag');

  if (version === null) {
    throw fallo(respuesta.status);
  }

  return version;
}

/** Lo que admite la exportación, que son los filtros del listado sin `page` ni `size`. */
type Filtros = NonNullable<operations['Ajustes_Exportar']['parameters']['query']>;

/** Los filtros y el orden del listado, sin la página: lo que llevan él y su exportación. */
function filtrosDe(listado: ListadoDeAjustes): Filtros {
  return {
    ...(listado.estado === null ? {} : { estado: estadoEnElContrato(listado.estado) }),
    ...(listado.almacenId === null ? {} : { almacen: listado.almacenId }),
    ...(listado.busqueda === '' ? {} : { q: listado.busqueda }),
    ...(listado.orden === null ? {} : { sort: listado.orden }),
  };
}

/** Una página de ajustes, en el orden pedido, o del último día al primero, que es el de la API. */
export async function consultarAjustes(
  listado: ListadoDeAjustes,
): Promise<Pagina<ResumenDeAjuste>> {
  const { data, error, response } = await api.GET('/api/v1/inventario/ajustes', {
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

  return { elementos: data.elementos.map(traducirResumen), total: enteroDelContrato(data.total) };
}

/** Los ajustes del listado, con sus filtros, su orden y todas sus páginas, en el CSV. */
export async function exportarAjustes(listado: ListadoDeAjustes): Promise<Fichero> {
  return ficheroDe(
    await api.GET('/api/v1/inventario/ajustes/exportacion', {
      params: { query: filtrosDe(listado) },
      parseAs: 'blob',
    }),
  );
}

/** La ficha de uno, con todas sus líneas y la versión que cita el descarte. */
export async function consultarAjuste(id: string): Promise<FichaDeAjuste> {
  const { data, error, response } = await api.GET('/api/v1/inventario/ajustes/{id}', {
    params: { path: { id } },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { ajuste: traducir(data), version: versionDe(response) };
}

/**
 * Abre un borrador, con la clave del intento, que aquí es obligatoria (ADR-0065 §6): sin ella, un
 * reintento dejaría dos borradores iguales. Devuelve el ajuste abierto, sin su versión: la trae la
 * ficha.
 */
export async function abrirAjuste(alta: AltaDeAjuste, clave: string): Promise<ResumenDeAjuste> {
  const { data, error, response } = await api.POST('/api/v1/inventario/ajustes', {
    body: { ...alta, lineas: alta.lineas.map((linea) => ({ ...linea })) },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirResumen(data);
}

/**
 * Confirma un borrador: recibe su número y mueve el libro. Con la clave del intento y sin
 * `If-Match`, porque lo que la ficha enseña de un borrador solo cambia de estado (ADR-0065 §7).
 */
export async function confirmarAjuste(id: string, clave: string): Promise<ResumenDeAjuste> {
  const { data, error, response } = await api.POST('/api/v1/inventario/ajustes/{id}/confirmacion', {
    params: { path: { id } },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirResumen(data);
}

/** Anula uno confirmado: el inverso que lo compensa nace confirmado y numerado. */
export async function anularAjuste(id: string, motivo: string, clave: string): Promise<Anulacion> {
  const { data, error, response } = await api.POST('/api/v1/inventario/ajustes/{id}/anulacion', {
    params: { path: { id } },
    body: { motivo },
    headers: { [CABECERA_DE_IDEMPOTENCIA]: clave },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return { original: traducirResumen(data.original), inverso: traducirResumen(data.inverso) };
}

/**
 * Descarta un borrador con la versión que se vio (ADR-0065 §5). Contesta `204`, sin cuerpo: el
 * borrador deja de existir para la API, y lo que queda es saber que salió bien.
 */
export async function descartarAjuste(id: string, version: string): Promise<void> {
  const { error, response } = await api.DELETE('/api/v1/inventario/ajustes/{id}', {
    params: { path: { id }, header: { 'If-Match': version } },
  });

  if (!response.ok) {
    throw fallo(response.status, error);
  }
}
