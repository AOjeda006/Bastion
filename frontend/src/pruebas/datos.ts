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
type TarifaDto = components['schemas']['TarifaDto'];
type PaginaDeTarifaDto = components['schemas']['PaginaDeTarifaDto'];

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
  'catalogo.tarifa.ver',
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

/**
 * El día que los tests de tarifas fijan como HOY, y del que depende el caso frontera de abajo.
 *
 * Está aquí y no en el test porque el caso que importa —un tramo que acaba **hoy** y que por tanto
 * todavía rige— exige que el reloj simulado y la fixture digan el mismo día. Con el día escrito en
 * dos sitios, basta con tocar uno para que el caso siga verde sin comprobar nada: el tramo pasaría
 * a acabar ayer, «ya no rige» sería la respuesta correcta, y la inclusividad del último día dejaría
 * de estar ejercida sin que nada se pusiera rojo.
 */
export const HOY_SIMULADO = '2026-09-11';

/**
 * Las tarifas con las que responde el servidor simulado.
 *
 * **Un código son varias filas**, que es lo que esta pantalla tiene que enseñar: `PVP` tiene dos
 * tramos encadenados —2025 cerrado y 2026 abierto— que no se solapan, porque la base lo impide con
 * una restricción de exclusión. Con un solo tramo por código, una pantalla que ignorara la vigencia
 * pintaría exactamente lo mismo.
 *
 * Y están los tres estados que la tabla distingue: uno caducado, uno futuro, y **dos que rigen**,
 * de los cuales uno acaba justamente hoy. Ese último es el caso frontera: el final está incluido,
 * así que sigue rigiendo hoy y dejará de hacerlo mañana.
 *
 * La divisa es un identificador de un maestro de OTRO módulo y esta pantalla no la pinta; está
 * porque el contrato la exige, con un valor que no es de ningún maestro real.
 */
const LISTAS_DE_PRECIOS: Record<string, TarifaDto[]> = {
  [ALFA.id]: [
    {
      id: 'aaaabbb1-0000-0000-0000-000000000001',
      empresaId: ALFA.id,
      codigo: 'PVP',
      nombre: 'Precio de venta al público 2026',
      divisaId: 'aaaa0003-0000-0000-0000-000000000001',
      vigenteDesde: '2026-01-01',
      vigenteHasta: null,
    },
    {
      id: 'aaaabbb1-0000-0000-0000-000000000002',
      empresaId: ALFA.id,
      codigo: 'PVP',
      nombre: 'Precio de venta al público 2025',
      divisaId: 'aaaa0003-0000-0000-0000-000000000001',
      vigenteDesde: '2025-01-01',
      vigenteHasta: '2025-12-31',
    },
    {
      id: 'aaaabbb1-0000-0000-0000-000000000003',
      empresaId: ALFA.id,
      codigo: 'MAYORISTA',
      nombre: 'Mayorista, a partir de octubre',
      divisaId: 'aaaa0003-0000-0000-0000-000000000001',
      vigenteDesde: '2026-10-01',
      vigenteHasta: null,
    },
    {
      id: 'aaaabbb1-0000-0000-0000-000000000004',
      empresaId: ALFA.id,
      codigo: 'PROMO',
      nombre: 'Promoción de septiembre',
      divisaId: 'aaaa0003-0000-0000-0000-000000000001',
      vigenteDesde: '2026-09-01',
      // Acaba HOY, y por eso todavía rige. Es el caso que separa `<=` de `<`.
      vigenteHasta: HOY_SIMULADO,
    },
  ],
  [BETA.id]: [
    {
      id: 'aaaabbb2-0000-0000-0000-000000000001',
      empresaId: BETA.id,
      codigo: 'EXPORT',
      nombre: 'Exportación',
      divisaId: 'aaaa0003-0000-0000-0000-000000000002',
      vigenteDesde: '2026-01-01',
      vigenteHasta: null,
    },
  ],
};

/**
 * Las tarifas de una empresa, filtradas, **ordenadas como las ordena la API** y paginadas.
 *
 * El orden no es un detalle del doble: el servidor las devuelve por código y, dentro de cada uno,
 * de la vigencia más reciente a la más antigua. Si aquí salieran en el orden en que están escritas,
 * la pantalla podría prometer un orden que la API no da, y el rótulo que dice «del más reciente al
 * más antiguo» sería mentira contra el servidor de verdad.
 *
 * Los dos filtros son dos preguntas distintas, como en la API: `codigo` es igualdad exacta sobre el
 * código ya normalizado, y `busqueda` es texto parcial sobre el código y el nombre.
 */
export function tarifasDe(
  empresaId: string,
  busqueda = '',
  codigo: string | null = null,
): PaginaDeTarifaDto {
  const todas = LISTAS_DE_PRECIOS[empresaId] ?? [];
  const buscado = busqueda.trim().toLocaleLowerCase('es');
  const acotado = codigo === null ? null : codigo.trim().toUpperCase();

  // `[...].sort()` y no `toSorted()`: el objetivo de compilación de este frontal no llega a
  // ES2023, y ordenar sobre una copia es lo mismo sin cambiarlo por una fixture.
  const elementos = [
    ...todas
      .filter((tarifa) => acotado === null || tarifa.codigo === acotado)
      .filter(
        (tarifa) =>
          buscado === '' ||
          [tarifa.codigo, tarifa.nombre].join(' ').toLocaleLowerCase('es').includes(buscado),
      ),
  ].sort(
    (una, otra) =>
      una.codigo.localeCompare(otra.codigo, 'es') ||
      otra.vigenteDesde.localeCompare(una.vigenteDesde),
  );

  return { elementos, pagina: 1, tamanio: 20, total: elementos.length };
}
