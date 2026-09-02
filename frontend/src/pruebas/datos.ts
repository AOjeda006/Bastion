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

/** Dos empresas de verdad, no una empresa y una variable. Se opera en las dos. */
export const ALFA = {
  id: '11111111-1111-1111-1111-111111111111',
  razonSocial: 'Alfa Materiales SL',
};
export const BETA = {
  id: '22222222-2222-2222-2222-222222222222',
  razonSocial: 'Beta Suministros SL',
};

export const PERMISOS_DE_LECTURA = ['organizacion.almacen.ver', 'organizacion.empresa.ver'];

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
