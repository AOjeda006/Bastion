import type { components } from '@/shared/api/esquema.ts';

/**
 * Los datos con los que responde el servidor simulado.
 *
 * Están tipados con los tipos GENERADOS del contrato, no con tipos escritos para los tests. Es lo
 * que impide el falso verde clásico: un mock con la forma que le viene bien al test, que pasa
 * mientras la API de verdad devuelve otra cosa.
 */

type SesionDto = components['schemas']['SesionDto'];
type AlmacenDto = components['schemas']['AlmacenDto'];
type PaginaDeAlmacenDto = components['schemas']['PaginaDeAlmacenDto'];
type TerceroDto = components['schemas']['TerceroDto'];
type PaginaDeTerceroDto = components['schemas']['PaginaDeTerceroDto'];
type ArticuloDto = components['schemas']['ArticuloDto'];
type PaginaDeArticuloDto = components['schemas']['PaginaDeArticuloDto'];
type CategoriaDto = components['schemas']['CategoriaDto'];
type PaginaDeCategoriaDto = components['schemas']['PaginaDeCategoriaDto'];

/** Dos empresas de verdad, no una empresa y una variable. Se opera en las dos. */
export const ALFA = {
  id: '11111111-1111-1111-1111-111111111111',
  razonSocial: 'Alfa Materiales SL',
};
export const BETA = {
  id: '22222222-2222-2222-2222-222222222222',
  razonSocial: 'Beta Suministros SL',
};

export const PERMISOS_DE_LECTURA = [
  'catalogo.articulo.ver',
  'catalogo.categoria.ver',
  'organizacion.almacen.ver',
  'organizacion.empresa.ver',
  'terceros.tercero.ver',
];

/** Una sesión abierta en `empresaActivaId`, con el selector de las dos empresas. */
export function sesionDto(empresaActivaId: string, permisos = PERMISOS_DE_LECTURA): SesionDto {
  return {
    // El testigo cambia con la empresa: es la pieza que hace que las mismas URL devuelvan otras
    // filas, y por eso el valor lleva dentro con cuál se emitió.
    tokenDeAcceso: `testigo-de-${empresaActivaId}`,
    expiraEn: '2099-01-01T00:00:00+00:00',
    usuarioId: '99999999-9999-9999-9999-999999999999',
    nombre: 'Ana Contable',
    empresaActivaId,
    empresas: [ALFA, BETA],
    permisos,
  };
}

const NAVES: Record<string, AlmacenDto[]> = {
  [ALFA.id]: [
    {
      id: 'aaaaaaa1-0000-0000-0000-000000000001',
      empresaId: ALFA.id,
      codigo: 'ALM-ALFA',
      nombre: 'Nave central de Alfa',
      direccion: {
        calle: 'Calle Uno',
        numero: '1',
        codigoPostal: '41001',
        poblacion: 'Sevilla',
        subdivision: 'SE',
        pais: 'ES',
      },
      tipo: 'Fisico',
    },
  ],
  [BETA.id]: [
    {
      id: 'bbbbbbb1-0000-0000-0000-000000000001',
      empresaId: BETA.id,
      codigo: 'ALM-BETA',
      nombre: 'Nave central de Beta',
      direccion: {
        calle: 'Calle Dos',
        numero: '2',
        codigoPostal: '48001',
        poblacion: 'Bilbao',
        subdivision: 'BI',
        pais: 'ES',
      },
      tipo: 'Fisico',
    },
  ],
};

/** Los almacenes de una empresa, paginados como los pagina la API. */
export function almacenesDe(empresaId: string): PaginaDeAlmacenDto {
  const elementos = NAVES[empresaId] ?? [];

  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}

/**
 * Los terceros con los que responde el servidor simulado.
 *
 * **Ningún identificador fiscal de aquí es de nadie.** Los españoles se han fabricado con el
 * algoritmo —ocho dígitos y la letra que sale de `"TRWAGMYFPDXBNJZSQVHLCKE"[n % 23]`, así que
 * `00000001` da `R` y `00000002` da `W`—, y el extranjero es opaco a propósito. Un NIF de verdad en
 * una fixture es un dato personal que se queda en el repositorio, en el artefacto de resultados y
 * en el registro de la CI, para siempre y sin plazo de borrado; y aquí no hace ninguna falta,
 * porque quien valida es el servidor y esta pantalla solo pinta lo que le dicen.
 *
 * Los tres casos que la pantalla distingue están los tres: comprobado y sin comprobar, y las tres
 * combinaciones de papel que el dominio permite. El cuarto estado de verificación —el que esta
 * versión no sabe leer— no se puede poner aquí sin mentir sobre el contrato, así que se prueba
 * desde el test con una respuesta suya.
 */
const FICHAS: Record<string, TerceroDto[]> = {
  [ALFA.id]: [
    {
      id: 'ccccccc1-0000-0000-0000-000000000001',
      empresaId: ALFA.id,
      identificacion: { pais: 'ES', numero: '00000001R', verificacion: 'VerificadoPorAlgoritmo' },
      razonSocial: 'Ferretería Industrial del Sur SL',
      nombreComercial: 'Ferrisur',
      domicilioFiscal: {
        calle: 'Calle Tres',
        numero: '3',
        codigoPostal: '41003',
        poblacion: 'Sevilla',
        subdivision: 'SE',
        pais: 'ES',
      },
      esCliente: true,
      esProveedor: false,
      regimenFiscal: {
        territorio: 'PeninsulaYBaleares',
        recargoDeEquivalencia: false,
        criterioDeCaja: false,
        sujetoARetencionIrpf: false,
      },
    },
    {
      id: 'ccccccc1-0000-0000-0000-000000000002',
      empresaId: ALFA.id,
      identificacion: { pais: 'FR', numero: 'PRUEBAFR0001', verificacion: 'NoVerificado' },
      razonSocial: 'Outillage Girondin SARL',
      nombreComercial: null,
      domicilioFiscal: {
        calle: 'Rue Quatre',
        numero: '4',
        codigoPostal: '33000',
        poblacion: 'Burdeos',
        subdivision: 'NA',
        pais: 'FR',
      },
      esCliente: false,
      esProveedor: true,
      regimenFiscal: {
        territorio: 'UnionEuropea',
        recargoDeEquivalencia: false,
        criterioDeCaja: false,
        sujetoARetencionIrpf: false,
      },
    },
    {
      id: 'ccccccc1-0000-0000-0000-000000000003',
      empresaId: ALFA.id,
      identificacion: { pais: 'ES', numero: '00000002W', verificacion: 'VerificadoPorAlgoritmo' },
      razonSocial: 'Transportes Alfa y Omega SL',
      nombreComercial: null,
      domicilioFiscal: {
        calle: 'Calle Cinco',
        numero: '5',
        codigoPostal: '41005',
        poblacion: 'Sevilla',
        subdivision: 'SE',
        pais: 'ES',
      },
      esCliente: true,
      esProveedor: true,
      regimenFiscal: {
        territorio: 'PeninsulaYBaleares',
        recargoDeEquivalencia: false,
        criterioDeCaja: true,
        sujetoARetencionIrpf: false,
      },
    },
  ],
  [BETA.id]: [
    {
      id: 'ddddddd1-0000-0000-0000-000000000001',
      empresaId: BETA.id,
      identificacion: { pais: 'ES', numero: '00000003A', verificacion: 'VerificadoPorAlgoritmo' },
      razonSocial: 'Suministros del Norte SL',
      nombreComercial: 'Sumnorte',
      domicilioFiscal: {
        calle: 'Calle Seis',
        numero: '6',
        codigoPostal: '48006',
        poblacion: 'Bilbao',
        subdivision: 'BI',
        pais: 'ES',
      },
      esCliente: true,
      esProveedor: false,
      regimenFiscal: {
        territorio: 'Canarias',
        recargoDeEquivalencia: true,
        criterioDeCaja: false,
        sujetoARetencionIrpf: false,
      },
    },
  ],
};

/**
 * Los terceros de una empresa, filtrados y paginados como los pagina la API.
 *
 * El filtro mira razón social y nombre comercial, **y nada más**: si el doble buscara también por
 * identificador fiscal, un test podría pasar contra un servidor que no lo hace, y la pantalla
 * acabaría ofreciendo por la URL justo lo que el ADR-0025 saca de ella.
 */
export function tercerosDe(empresaId: string, busqueda = ''): PaginaDeTerceroDto {
  const todos = FICHAS[empresaId] ?? [];
  const buscado = busqueda.trim().toLocaleLowerCase('es');

  const elementos =
    buscado === ''
      ? todos
      : todos.filter((tercero) =>
          [tercero.razonSocial, tercero.nombreComercial ?? '']
            .join(' ')
            .toLocaleLowerCase('es')
            .includes(buscado),
        );

  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}

/**
 * El árbol de categorías con el que responde el servidor simulado.
 *
 * **Es un árbol de verdad y no una lista**: tres niveles encadenados —FERR › TORN › TUER— y una
 * segunda raíz. Con un solo nivel, una composición que ignorara el padre pintaría exactamente lo
 * mismo, y el caso que comprueba la sangría estaría comprobando nada.
 *
 * Vienen ordenadas por código, que es como las ordena la API por omisión: así el orden en el que
 * llegan NO es el del árbol —SERV llega la segunda y se pinta la última—, que es justo lo que
 * distingue componer el árbol de pintar la lista tal cual.
 */
const RAMAS: Record<string, CategoriaDto[]> = {
  [ALFA.id]: [
    {
      id: 'eeeeeee1-0000-0000-0000-000000000001',
      empresaId: ALFA.id,
      codigo: 'FERR',
      nombre: 'Ferretería',
      padreId: null,
    },
    {
      id: 'eeeeeee1-0000-0000-0000-000000000004',
      empresaId: ALFA.id,
      codigo: 'SERV',
      nombre: 'Servicios',
      padreId: null,
    },
    {
      id: 'eeeeeee1-0000-0000-0000-000000000002',
      empresaId: ALFA.id,
      codigo: 'TORN',
      nombre: 'Tornillería',
      padreId: 'eeeeeee1-0000-0000-0000-000000000001',
    },
    {
      id: 'eeeeeee1-0000-0000-0000-000000000003',
      empresaId: ALFA.id,
      codigo: 'TUER',
      nombre: 'Tuercas',
      padreId: 'eeeeeee1-0000-0000-0000-000000000002',
    },
  ],
  [BETA.id]: [
    {
      id: 'eeeeeee2-0000-0000-0000-000000000001',
      empresaId: BETA.id,
      codigo: 'ELEC',
      nombre: 'Electricidad',
      padreId: null,
    },
  ],
};

/** Las categorías de una empresa, paginadas como las pagina la API. */
export function categoriasDe(empresaId: string): PaginaDeCategoriaDto {
  const elementos = RAMAS[empresaId] ?? [];

  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}

/** Una categoría suelta de una empresa, o `undefined` si esa empresa no la tiene. */
export function categoriaDe(empresaId: string, id: string): CategoriaDto | undefined {
  return (RAMAS[empresaId] ?? []).find((categoria) => categoria.id === id);
}

/**
 * Los artículos con los que responde el servidor simulado.
 *
 * El `tipo` viaja como TEXTO —`Bien` o `Servicio`—, igual que en el contrato: escribirlo aquí como
 * número dejaría verde una pantalla que luego pintaría celdas vacías contra la API de verdad. Y hay
 * de los dos, y uno sin clasificar, porque son los tres casos que la tabla distingue.
 *
 * La unidad y el impuesto son identificadores de maestros de OTRO módulo y esta pantalla no los
 * pinta; están porque el contrato los exige, y con valores que no son de ningún maestro real.
 */
const PIEZAS: Record<string, ArticuloDto[]> = {
  [ALFA.id]: [
    {
      id: 'fffffff1-0000-0000-0000-000000000001',
      empresaId: ALFA.id,
      codigo: 'TOR-M6',
      descripcion: 'Tornillo M6 zincado',
      tipo: 'Bien',
      unidadBaseId: 'aaaa0001-0000-0000-0000-000000000001',
      impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
      categoriaId: 'eeeeeee1-0000-0000-0000-000000000002',
    },
    {
      id: 'fffffff1-0000-0000-0000-000000000002',
      empresaId: ALFA.id,
      codigo: 'TUE-M6',
      descripcion: 'Tuerca M6 zincada',
      tipo: 'Bien',
      unidadBaseId: 'aaaa0001-0000-0000-0000-000000000001',
      impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
      categoriaId: 'eeeeeee1-0000-0000-0000-000000000003',
    },
    {
      id: 'fffffff1-0000-0000-0000-000000000003',
      empresaId: ALFA.id,
      codigo: 'MO-TALLER',
      descripcion: 'Mano de obra de taller',
      tipo: 'Servicio',
      unidadBaseId: 'aaaa0001-0000-0000-0000-000000000002',
      impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
      categoriaId: null,
    },
  ],
  [BETA.id]: [
    {
      id: 'fffffff2-0000-0000-0000-000000000001',
      empresaId: BETA.id,
      codigo: 'CAB-25',
      descripcion: 'Cable 2,5 mm²',
      tipo: 'Bien',
      unidadBaseId: 'aaaa0001-0000-0000-0000-000000000003',
      impuestoPorDefectoId: 'aaaa0002-0000-0000-0000-000000000001',
      categoriaId: 'eeeeeee2-0000-0000-0000-000000000001',
    },
  ],
};

/**
 * Los artículos de una empresa, filtrados y paginados como los pagina la API.
 *
 * **Acota por la categoría dicha, no por su subárbol**, exactamente como el servidor: si este doble
 * bajara por el árbol, la pantalla podría prometer algo que la API no hace, y el aviso de «los de
 * las categorías que cuelgan de esta no salen aquí» sería mentira contra el servidor de verdad.
 */
export function articulosDe(
  empresaId: string,
  busqueda = '',
  categoriaId: string | null = null,
): PaginaDeArticuloDto {
  const todos = PIEZAS[empresaId] ?? [];
  const buscado = busqueda.trim().toLocaleLowerCase('es');

  const elementos = todos
    .filter((articulo) => categoriaId === null || articulo.categoriaId === categoriaId)
    .filter(
      (articulo) =>
        buscado === '' ||
        [articulo.codigo, articulo.descripcion].join(' ').toLocaleLowerCase('es').includes(buscado),
    );

  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}
