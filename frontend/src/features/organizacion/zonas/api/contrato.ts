import type { Zona } from '../model/zona.ts';
import type { components } from '@/shared/api/esquema.ts';

type ZonaHorariaDto = components['schemas']['ZonaHorariaDto'];

/** De la zona del contrato a la de la pantalla. Una zona nueva en el contrato no compila aquí. */
const EN_LA_PANTALLA: Record<ZonaHorariaDto, Zona> = {
  'Europe/Madrid': 'peninsula',
  'Atlantic/Canary': 'canarias',
  'Africa/Ceuta': 'ceuta',
};

/** Y al revés, para escribirla. */
const EN_EL_CONTRATO: Record<Zona, ZonaHorariaDto> = {
  peninsula: 'Europe/Madrid',
  canarias: 'Atlantic/Canary',
  ceuta: 'Africa/Ceuta',
};

export function zonaDelContrato(zona: ZonaHorariaDto): Zona {
  return EN_LA_PANTALLA[zona];
}

export function zonaParaElContrato(zona: Zona): ZonaHorariaDto {
  return EN_EL_CONTRATO[zona];
}
