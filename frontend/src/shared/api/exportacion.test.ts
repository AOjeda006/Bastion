import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';

import { api } from './cliente.ts';
import { FalloDeApi } from './errores.ts';
import { NOMBRE_SIN_ADJUNTO, ficheroDe, nombreDelAdjunto } from './exportacion.ts';
import { servidor } from '@/pruebas/servidor.ts';

/**
 * La exportación del lado del navegador (ítem 3.2): de qué cabecera sale el nombre del fichero, y
 * que un rechazo del servidor sale por el mismo `FalloDeApi` que el de un listado.
 */
describe('El nombre del adjunto', () => {
  it('es el de `filename*` cuando vienen los dos, que es el que manda (RFC 6266)', () => {
    expect(
      nombreDelAdjunto(
        "attachment; filename=almacenes-2026-09-11.csv; filename*=UTF-8''almac%C3%A9n-2026-09-11.csv",
      ),
    ).toBe('almacén-2026-09-11.csv');
  });

  it('es el de `filename` si no viene el otro, con comillas o sin ellas', () => {
    expect(nombreDelAdjunto('attachment; filename="saldos-2026-09-11.csv"')).toBe(
      'saldos-2026-09-11.csv',
    );
    expect(nombreDelAdjunto('attachment; filename=saldos-2026-09-11.csv')).toBe(
      'saldos-2026-09-11.csv',
    );
  });

  it('sin ninguno, o sin cabecera, el fichero se guarda igual, con el nombre de reserva', () => {
    expect(nombreDelAdjunto('attachment')).toBe(NOMBRE_SIN_ADJUNTO);
    expect(nombreDelAdjunto(null)).toBe(NOMBRE_SIN_ADJUNTO);
    expect(NOMBRE_SIN_ADJUNTO).toBe('exportacion.csv');
  });
});

describe('El fichero de una exportación', () => {
  it('lleva el contenido y el nombre de la respuesta', () => {
    const contenido = new Blob(['codigo\r\n']);
    const respuesta = new Response(null, {
      headers: { 'Content-Disposition': 'attachment; filename=terceros-2026-09-11.csv' },
    });

    expect(ficheroDe({ data: contenido, response: respuesta })).toEqual({
      contenido,
      nombre: 'terceros-2026-09-11.csv',
    });
  });

  it('un rechazo pedido como blob trae su ProblemDetails, por el cliente de verdad', async () => {
    // Lo que el cliente hace con el cuerpo de un error no depende de `parseAs`: lo lee como JSON.
    // Si dejara de hacerlo, el fallo llegaría sin `type` ni traza, y la pantalla no podría decir
    // la frase del motivo, solo la genérica.
    servidor.use(
      http.get('/api/v1/organizacion/almacenes/exportacion', () =>
        HttpResponse.json(
          { type: '/errors/empresa-ajena', status: 403, traceId: '00-traza-de-la-prueba-00' },
          { status: 403 },
        ),
      ),
    );

    const respuesta = await api.GET('/api/v1/organizacion/almacenes/exportacion', {
      parseAs: 'blob',
    });

    let rechazo: unknown;
    try {
      ficheroDe(respuesta);
    } catch (error) {
      rechazo = error;
    }

    expect(rechazo).toBeInstanceOf(FalloDeApi);
    expect(rechazo).toMatchObject({
      estado: 403,
      motivo: 'sinPermiso',
      tipo: 'empresa-ajena',
      traza: '00-traza-de-la-prueba-00',
    });
  });
});
