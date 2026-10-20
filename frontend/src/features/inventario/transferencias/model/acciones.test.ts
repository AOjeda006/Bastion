import { describe, expect, it } from 'vitest';

import { accionesDisponibles } from './acciones.ts';
import type { Transferencia } from './transferencia.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

const TODOS = [
  PERMISOS.transferenciaVer,
  PERMISOS.transferenciaEnviar,
  PERMISOS.transferenciaRecibir,
  PERMISOS.transferenciaAnular,
  PERMISOS.transferenciaDescartar,
];

const ORIGINAL = '7a7a7a7a-0000-0000-0000-000000000015';

function transferencia(
  estado: Transferencia['estado'],
  anulaAId: string | null = null,
): Pick<Transferencia, 'estado' | 'anulaAId'> {
  return { estado, anulaAId };
}

describe('Lo que ofrece la ficha de una transferencia (ADR-0068 §8)', () => {
  it('un borrador se envía o se descarta; una enviada se recibe o se anula; una recibida, se anula', () => {
    expect(accionesDisponibles(transferencia('borrador'), TODOS)).toEqual(['enviar', 'descartar']);
    expect(accionesDisponibles(transferencia('enviada'), TODOS)).toEqual(['recibir', 'anular']);
    expect(accionesDisponibles(transferencia('recibida'), TODOS)).toEqual(['anular']);
  });

  it('un inverso, que nace recibido, no se anula', () => {
    expect(accionesDisponibles(transferencia('recibida', ORIGINAL), TODOS)).toEqual([]);
  });

  it('una anulada, o en un estado que esta versión no conoce, no ofrece nada', () => {
    expect(accionesDisponibles(transferencia('anulada'), TODOS)).toEqual([]);
    expect(accionesDisponibles(transferencia('desconocido'), TODOS)).toEqual([]);
  });

  it('cada acción, con su permiso y no con el de otra', () => {
    expect(accionesDisponibles(transferencia('borrador'), [PERMISOS.transferenciaEnviar])).toEqual([
      'enviar',
    ]);
    expect(
      accionesDisponibles(transferencia('borrador'), [PERMISOS.transferenciaDescartar]),
    ).toEqual(['descartar']);
    expect(accionesDisponibles(transferencia('enviada'), [PERMISOS.transferenciaRecibir])).toEqual([
      'recibir',
    ]);
    expect(accionesDisponibles(transferencia('enviada'), [PERMISOS.transferenciaAnular])).toEqual([
      'anular',
    ]);
    expect(
      accionesDisponibles(transferencia('borrador'), [
        PERMISOS.transferenciaRecibir,
        PERMISOS.transferenciaAnular,
      ]),
    ).toEqual([]);
    expect(
      accionesDisponibles(transferencia('enviada'), [
        PERMISOS.transferenciaEnviar,
        PERMISOS.transferenciaDescartar,
      ]),
    ).toEqual([]);
    expect(accionesDisponibles(transferencia('recibida'), [PERMISOS.transferenciaVer])).toEqual([]);
  });
});
