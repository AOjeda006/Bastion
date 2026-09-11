import { HttpResponse, http } from 'msw';
import { setupServer } from 'msw/node';

import {
  ALFA,
  almacenesDe,
  articulosDe,
  categoriaDe,
  categoriasDe,
  sesionDto,
  tarifasDe,
  tercerosDe,
} from './datos.ts';
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
  /**
   * Con qué criterios se ha pedido el listado de artículos, una entrada por petición.
   *
   * Los dos filtros juntos y no dos listas: lo que hay que poder afirmar es que la pantalla manda
   * al servidor lo que dice la URL, y una que se trajera todo y filtrara en el navegador pintaría
   * exactamente lo mismo con la lista vacía.
   */
  articulosPedidos: [] as { busqueda: string; categoria: string | null }[],
  falloDeArticulos: null as number | null,
  falloDeCategorias: null as number | null,
  /**
   * Con qué criterios se ha pedido el listado de tarifas, una entrada por petición.
   *
   * Los dos juntos y no dos listas, por lo mismo que en artículos: lo que hay que poder afirmar es
   * que la pantalla manda al servidor lo que dice la URL, y una que se trajera todo y filtrara en
   * el navegador pintaría exactamente lo mismo con la lista vacía.
   */
  tarifasPedidas: [] as { busqueda: string; codigo: string | null }[],
  falloDeTarifas: null as number | null,
};

/** Deja al servidor sin sesión y sin cuentas pendientes. Se llama entre test y test. */
export function reiniciarServidor(): void {
  servidorSimulado.sesion = null;
  servidorSimulado.peticionesDeAlmacenes = 0;
  servidorSimulado.falloDeAlmacenes = null;
  servidorSimulado.busquedasDeTerceros = [];
  servidorSimulado.falloDeTerceros = null;
  servidorSimulado.articulosPedidos = [];
  servidorSimulado.falloDeArticulos = null;
  servidorSimulado.falloDeCategorias = null;
  servidorSimulado.tarifasPedidas = [];
  servidorSimulado.falloDeTarifas = null;
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

/** Con qué empresa se está operando, según el testigo que trae la petición. */
function empresaDe(peticion: Request): string {
  return peticion.headers.get('Authorization')?.replace('Bearer testigo-de-', '') ?? '';
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

  // Los cuatro de Catálogo responden SEGÚN EL TESTIGO, como los anteriores: la empresa activa va
  // dentro del token y quien filtra es el servidor (R8).
  http.get('/api/v1/catalogo/articulos', ({ request }) => {
    const consulta = new URL(request.url).searchParams;

    servidorSimulado.articulosPedidos.push({
      busqueda: consulta.get('q') ?? '',
      categoria: consulta.get('categoria'),
    });

    if (servidorSimulado.falloDeArticulos !== null) {
      return new HttpResponse(null, { status: servidorSimulado.falloDeArticulos });
    }

    return HttpResponse.json(
      articulosDe(empresaDe(request), consulta.get('q') ?? '', consulta.get('categoria')),
    );
  }),

  http.get('/api/v1/catalogo/categorias', ({ request }) =>
    servidorSimulado.falloDeCategorias === null
      ? HttpResponse.json(categoriasDe(empresaDe(request)))
      : new HttpResponse(null, { status: servidorSimulado.falloDeCategorias }),
  ),

  http.get('/api/v1/catalogo/categorias/:id', ({ params, request }) => {
    const id = typeof params['id'] === 'string' ? params['id'] : '';
    const categoria = categoriaDe(empresaDe(request), id);

    // Una categoría de otra empresa sale como «no existe», igual que en la API: el filtro de
    // inquilinato no distingue «no está» de «no es tuya», y contestar cosas distintas diría a
    // cualquiera si un identificador existe en otra empresa.
    return categoria === undefined
      ? new HttpResponse(null, { status: 404 })
      : HttpResponse.json(categoria);
  }),

  http.get('/api/v1/catalogo/tarifas', ({ request }) => {
    const consulta = new URL(request.url).searchParams;

    servidorSimulado.tarifasPedidas.push({
      busqueda: consulta.get('q') ?? '',
      codigo: consulta.get('codigo'),
    });

    if (servidorSimulado.falloDeTarifas !== null) {
      return new HttpResponse(null, { status: servidorSimulado.falloDeTarifas });
    }

    return HttpResponse.json(
      tarifasDe(empresaDe(request), consulta.get('q') ?? '', consulta.get('codigo')),
    );
  }),

  http.get('/api/v1/organizacion/empresas', () =>
    HttpResponse.json({
      elementos: [
        {
          id: ALFA.id,
          nif: 'B99999997',
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
