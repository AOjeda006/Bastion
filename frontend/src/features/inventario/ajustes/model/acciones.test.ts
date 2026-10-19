import { describe, expect, it } from 'vitest';

import { accionesDisponibles } from './acciones.ts';
import type { Ajuste } from './ajuste.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

const TODOS = [
  PERMISOS.ajusteVer,
  PERMISOS.ajusteConfirmar,
  PERMISOS.ajusteAnular,
  PERMISOS.ajusteDescartar,
];

const ORIGINAL = 'adadadad-0000-0000-0000-000000000001';
const RECUENTO = '7e7e7e7e-0000-0000-0000-000000000008';

function ajuste(
  estado: Ajuste['estado'],
  cambios: Partial<Pick<Ajuste, 'anulaAId' | 'recuentoId'>> = {},
): Pick<Ajuste, 'estado' | 'anulaAId' | 'recuentoId'> {
  return { estado, anulaAId: null, recuentoId: null, ...cambios };
}

describe('Lo que ofrece la ficha de un ajuste (ADR-0065 §8)', () => {
  it('un borrador se confirma o se descarta, y uno confirmado se anula', () => {
    expect(accionesDisponibles(ajuste('borrador'), TODOS)).toEqual(['confirmar', 'descartar']);
    expect(accionesDisponibles(ajuste('confirmado'), TODOS)).toEqual(['anular']);
  });

  it('un inverso no se anula, y el ajuste de un recuento se anula desde su recuento', () => {
    expect(accionesDisponibles(ajuste('confirmado', { anulaAId: ORIGINAL }), TODOS)).toEqual([]);
    expect(accionesDisponibles(ajuste('confirmado', { recuentoId: RECUENTO }), TODOS)).toEqual([]);
  });

  it('uno anulado, o en un estado que esta versión no conoce, no ofrece nada', () => {
    expect(accionesDisponibles(ajuste('anulado'), TODOS)).toEqual([]);
    expect(accionesDisponibles(ajuste('desconocido'), TODOS)).toEqual([]);
  });

  it('cada acción, con su permiso y no con el de otra', () => {
    expect(accionesDisponibles(ajuste('borrador'), [PERMISOS.ajusteConfirmar])).toEqual([
      'confirmar',
    ]);
    expect(accionesDisponibles(ajuste('borrador'), [PERMISOS.ajusteDescartar])).toEqual([
      'descartar',
    ]);
    expect(accionesDisponibles(ajuste('borrador'), [PERMISOS.ajusteAnular])).toEqual([]);
    expect(
      accionesDisponibles(ajuste('confirmado'), [
        PERMISOS.ajusteConfirmar,
        PERMISOS.ajusteDescartar,
      ]),
    ).toEqual([]);
    expect(accionesDisponibles(ajuste('borrador'), [PERMISOS.ajusteVer])).toEqual([]);
  });
});
