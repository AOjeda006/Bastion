import { api } from './cliente.ts';
import { fallo } from './errores.ts';
import type { components } from './esquema.ts';
import type { Hoy } from '@/shared/hoy/hoy.ts';

type HoyDto = components['schemas']['HoyDto'];

/** Hoy en la empresa activa y en cada zona, ya traducido (ADR-0079 §12). */
export async function consultarHoy(): Promise<Hoy> {
  const { data, error, response } = await api.GET('/api/v1/organizacion/hoy');

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducirHoy(data);
}

/** La frontera del contrato: el DTO entra, y sale el modelo, con las zonas por su identificador. */
export function traducirHoy(dto: HoyDto): Hoy {
  return {
    deLaEmpresa: dto.deLaEmpresa,
    elMasAdelantado: dto.elMasAdelantado,
    enCadaZona: new Map(
      dto.enCadaZona.map((enLaZona) => [
        enLaZona.zona,
        { dia: enLaZona.dia, acabaEn: enLaZona.acabaEn },
      ]),
    ),
  };
}
