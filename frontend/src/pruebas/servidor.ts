import { HttpResponse, http } from 'msw';
import { setupServer } from 'msw/node';

import { ALFA, almacenesDe, sesionDto, tercerosDe } from './datos.ts';
import type { components } from '@/shared/api/esquema.ts';
import { traducirSesion } from '@/shared/api/traduccion.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';

/**
 * El servidor simulado, EN LA FRONTERA DE RED.
 *
 * No se dobla el hook de datos ni la función de consulta: se dobla HTTP. Así el test ejercita la
 * pantalla de verdad con su caché de verdad, y lo que se prueba incluye la clave de consulta, el
 * `staleTime` y el vaciado al cambiar de empresa — que es justo donde están los fallos de este
 * ítem. Un doble del hook los saltaría todos.
 */

/**
 * El estado del «servidor». Es mutable a propósito: el cambio de empresa CAMBIA lo que las mismas
 * URL devuelven, y eso es exactamente lo que hay que poder simular para probarlo por el efecto.
 */
export const servidorSimulado = {
  sesion: null as components['schemas']['SesionDto'] | null,
  /** Cuántas veces se ha pedido el listado. Delata a una caché que no se ha vaciado. */
  peticionesDeAlmacenes: 0,
  /** Si el listado responde con un fallo, y con cuál. */
  falloDeAlmacenes: null as number | null,
  /**
   * El `q` con el que se ha pedido el listado de terceros, una entrada por petición.
   *
   * Es la lista y no un contador: lo que hay que poder afirmar es que el filtro LLEGA al servidor,
   * y una pantalla que filtrara en el navegador pintaría exactamente lo mismo con la lista vacía.
   */
  busquedasDeTerceros: [] as string[],
  falloDeTerceros: null as number | null,
};

/** Deja al servidor sin sesión y sin cuentas pendientes. Se llama entre test y test. */
export function reiniciarServidor(): void {
  servidorSimulado.sesion = null;
  servidorSimulado.peticionesDeAlmacenes = 0;
  servidorSimulado.falloDeAlmacenes = null;
  servidorSimulado.busquedasDeTerceros = [];
  servidorSimulado.falloDeTerceros = null;
}

/** Abre sesión en el servidor simulado, como si ya se hubiera entrado en una recarga anterior. */
export function abrirSesionSimulada(empresaId = ALFA.id, permisos?: string[]): void {
  servidorSimulado.sesion =
    permisos === undefined ? sesionDto(empresaId) : sesionDto(empresaId, permisos);
}

/**
 * Como la anterior, pero con la sesión YA en el depósito del navegador.
 *
 * Es lo que hace falta para `montarPantalla`, que no monta `ProveedorDeSesion` y por tanto no pide
 * la sesión con la cookie: se deja escrita donde la habría dejado esa recuperación. Y se escribe
 * **traduciendo el DTO**, con el mismo traductor que usa la aplicación, no componiendo a mano un
 * objeto con la forma que le conviene al test — que es el falso verde clásico de las fixtures.
 */
export function abrirSesionYaRecuperada(empresaId = ALFA.id, permisos?: string[]): void {
  const dto = permisos === undefined ? sesionDto(empresaId) : sesionDto(empresaId, permisos);

  servidorSimulado.sesion = dto;
  escribirSesion(traducirSesion(dto));
}

export const servidor = setupServer(
  // La renovación con la cookie: es lo que hace la aplicación al arrancar. Sin sesión abierta
  // contesta 401, que no es un error sino «aquí no había nadie».
  http.post('/api/v1/identidad/sesiones/renovacion', () =>
    servidorSimulado.sesion === null
      ? new HttpResponse(null, { status: 401 })
      : HttpResponse.json(servidorSimulado.sesion),
  ),

  http.post('/api/v1/identidad/sesiones', async ({ request }) => {
    const cuerpo = (await request.json()) as { correo: string; contrasena: string };

    if (cuerpo.contrasena !== 'la-buena') {
      return new HttpResponse(null, { status: 401 });
    }

    servidorSimulado.sesion = sesionDto(ALFA.id);
    return HttpResponse.json(servidorSimulado.sesion);
  }),

  http.put('/api/v1/identidad/sesiones/actual/empresa', async ({ request }) => {
    const cuerpo = (await request.json()) as { empresaId: string };
    const permisos = servidorSimulado.sesion?.permisos;

    servidorSimulado.sesion =
      permisos === undefined ? sesionDto(cuerpo.empresaId) : sesionDto(cuerpo.empresaId, permisos);

    return HttpResponse.json(servidorSimulado.sesion);
  }),

  http.delete('/api/v1/identidad/sesiones/actual', () => {
    servidorSimulado.sesion = null;
    return new HttpResponse(null, { status: 204 });
  }),

  // El listado responde SEGÚN EL TESTIGO que trae la petición, igual que la API de verdad, donde la
  // empresa activa va dentro del token y la decide el filtro de inquilinato (R8). Si el frontal
  // reutiliza una respuesta cacheada de la empresa anterior, aquí no se entera nadie — y por eso el
  // test mira lo que se PINTA, no cuántas veces se ha llamado.
  http.get('/api/v1/organizacion/almacenes', ({ request }) => {
    servidorSimulado.peticionesDeAlmacenes += 1;

    if (servidorSimulado.falloDeAlmacenes !== null) {
      return new HttpResponse(null, { status: servidorSimulado.falloDeAlmacenes });
    }

    const testigo = request.headers.get('Authorization')?.replace('Bearer testigo-de-', '') ?? '';

    return HttpResponse.json(almacenesDe(testigo));
  }),

  // Igual que el de almacenes, responde SEGÚN EL TESTIGO: la empresa activa va dentro del token y
  // el filtro de inquilinato (R8) es cosa del servidor. Y anota el `q` que le llega, que es lo
  // único que distingue una pantalla que filtra en el servidor de una que se trae todo y filtra en
  // el navegador — las dos pintan lo mismo.
  http.get('/api/v1/terceros/terceros', ({ request }) => {
    const consulta = new URL(request.url).searchParams;
    servidorSimulado.busquedasDeTerceros.push(consulta.get('q') ?? '');

    if (servidorSimulado.falloDeTerceros !== null) {
      return new HttpResponse(null, { status: servidorSimulado.falloDeTerceros });
    }

    const testigo = request.headers.get('Authorization')?.replace('Bearer testigo-de-', '') ?? '';

    return HttpResponse.json(tercerosDe(testigo, consulta.get('q') ?? ''));
  }),

  http.get('/api/v1/organizacion/empresas', () =>
    HttpResponse.json({
      elementos: [
        {
          id: ALFA.id,
          nif: 'B12345674',
          razonSocial: ALFA.razonSocial,
          domicilioFiscal: {
            calle: 'Calle Uno',
            numero: '1',
            codigoPostal: '41001',
            poblacion: 'Sevilla',
            subdivision: 'SE',
            pais: 'ES',
          },
          divisaBase: 'EUR',
          regimenDeIva: 'General',
        },
      ],
      pagina: 1,
      tamanio: 20,
      total: 1,
    }),
  ),
);
