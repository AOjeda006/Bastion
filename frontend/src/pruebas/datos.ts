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
