import type { components } from './esquema.ts';
import type { EmpresaDeLaSesion, Sesion } from '@/shared/sesion/sesion.ts';

/**
 * La frontera del contrato: aquí entran los tipos GENERADOS y salen los modelos de vista.
 *
 * Los tipos de `esquema.ts` no cruzan esta línea (`stacks/react`: «los tipos generados no salen de
 * la capa `api`»). El motivo no es purismo: es que un campo que cambia de nombre en el contrato
 * rompa la compilación en UN sitio —este— y no en cada componente que lo leía.
 *
 * `esquema.ts` se genera con `npm run api` desde `docs/api/openapi.json`, que a su vez se genera al
 * compilar la API. Nunca se escribe a mano un tipo del contrato.
 */

type SesionDto = components['schemas']['SesionDto'];
type EmpresaDeSesionDto = components['schemas']['EmpresaDeSesionDto'];

function traducirEmpresa(dto: EmpresaDeSesionDto): EmpresaDeLaSesion {
  return { id: dto.id, razonSocial: dto.razonSocial };
}

/** Del DTO de sesión al modelo con el que trabaja la aplicación. */
export function traducirSesion(dto: SesionDto): Sesion {
  return {
    testigo: dto.tokenDeAcceso,
    expiraEn: dto.expiraEn,
    usuarioId: dto.usuarioId,
    nombre: dto.nombre,
    empresaActivaId: dto.empresaActivaId,
    empresas: dto.empresas.map(traducirEmpresa),
    permisos: [...dto.permisos],
  };
}

/**
 * El cuerpo de una respuesta HTTP llega como `unknown`: aquí es donde se le pone el tipo.
 *
 * Es la ÚNICA aserción de tipo de la capa, y va contra el tipo generado, no contra uno escrito a
 * mano. Se usa solo en la renovación, que va con `fetch` pelado —el cliente tipado no puede
 * hacerla sin morderse la cola— y por eso no llega ya tipada por el contrato.
 */
export function traducirCuerpoDeSesion(cuerpo: unknown): Sesion {
  return traducirSesion(cuerpo as SesionDto);
}
