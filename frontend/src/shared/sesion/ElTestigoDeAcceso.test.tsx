import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { ALFA, almacenesDe, sesionDto } from '@/pruebas/datos.ts';
import { abrirSesionSimulada, servidor } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';
import { api } from '@/shared/api/cliente.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';
import { traducirSesion } from '@/shared/api/traduccion.ts';

/**
 * Dónde vive el testigo de acceso y qué pasa cuando caduca.
 *
 * El primer test es el que importa y es el que menos parece un test: no comprueba una pantalla,
 * comprueba una AUSENCIA. Guardar el testigo en `localStorage` funciona perfectamente —la sesión
 * hasta sobrevive a una recarga, que es la excusa con la que entra— y lo deja legible para
 * cualquier script que llegue a la página. Lo que sobrevive a la recarga es la cookie `HttpOnly`,
 * que este código no puede leer ni queriendo.
 */
describe('El testigo de acceso', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('no llega nunca a localStorage ni a sessionStorage', async () => {
    const usuario = userEvent.setup();
    montarAplicacion('/acceso');

    await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' });

    await usuario.type(screen.getByLabelText('Correo'), 'ana@ejemplo.es');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la-buena');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    // Con la sesión ya abierta y funcionando: si el testigo se guardara en algún sitio, es AHORA
    // cuando estaría guardado.
    expect(await screen.findByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();

    expect(volcado(localStorage)).toBe('');
    expect(volcado(sessionStorage)).toBe('');
  });

  it('un 401 pide una sesión nueva y repite la petición una sola vez', async () => {
    abrirSesionSimulada();

    let renovaciones = 0;
    let intentos = 0;

    servidor.use(
      http.post('/api/v1/identidad/sesiones/renovacion', () => {
        renovaciones += 1;
        return HttpResponse.json(sesionDto(ALFA.id));
      }),
      http.get('/api/v1/organizacion/almacenes', () => {
        intentos += 1;
        return intentos === 1
          ? new HttpResponse(null, { status: 401 })
          : HttpResponse.json(almacenesDe(ALFA.id));
      }),
    );

    montarAplicacion('/almacenes');

    // El usuario no se entera de nada: ni ve un error ni le echan a la pantalla de acceso.
    expect(await screen.findByText('Nave central de Alfa')).toBeVisible();

    expect(intentos).toBe(2);
    // Dos: la del arranque, que es como se recupera la sesión de la cookie, y la de la caducidad.
    expect(renovaciones).toBe(2);
  });

  it('si la renovación tampoco vale, se deja de insistir y se manda a la pantalla de acceso', async () => {
    abrirSesionSimulada();

    let renovaciones = 0;
    let intentos = 0;

    servidor.use(
      http.post('/api/v1/identidad/sesiones/renovacion', () => {
        renovaciones += 1;
        // La primera es la del arranque y sí vale; a partir de ahí, la cookie ya no sirve.
        return renovaciones === 1
          ? HttpResponse.json(sesionDto(ALFA.id))
          : new HttpResponse(null, { status: 401 });
      }),
      http.get('/api/v1/organizacion/almacenes', () => {
        intentos += 1;
        return new HttpResponse(null, { status: 401 });
      }),
    );

    montarAplicacion('/almacenes');

    expect(await screen.findByRole('heading', { level: 1, name: 'Iniciar sesión' })).toBeVisible();

    // Uno, no dos ni infinitos: sin sesión en el depósito la petición ya no es reintentable, que es
    // la condición que rompe el bucle.
    expect(intentos).toBe(1);
    expect(renovaciones).toBe(2);
  });

  it('dos peticiones que caducan a la vez piden UNA sola sesión', async () => {
    escribirSesion(traducirSesion(sesionDto(ALFA.id)));

    let renovaciones = 0;
    const caducadas = new Set<string>();

    servidor.use(
      http.post('/api/v1/identidad/sesiones/renovacion', () => {
        renovaciones += 1;
        return HttpResponse.json(sesionDto(ALFA.id));
      }),
      http.get('/api/v1/organizacion/:recurso', ({ params }) => {
        const recurso = String(params['recurso']);

        if (!caducadas.has(recurso)) {
          caducadas.add(recurso);
          return new HttpResponse(null, { status: 401 });
        }

        return HttpResponse.json(
          recurso === 'almacenes'
            ? almacenesDe(ALFA.id)
            : { elementos: [], pagina: 1, tamanio: 20, total: 0 },
        );
      }),
    );

    // Aquí no hay pantalla: se ejercita el cliente directamente, porque lo que se prueba es la
    // renovación compartida, y en una pantalla real es una carrera que a veces sale bien sola.
    await Promise.all([
      api.GET('/api/v1/organizacion/almacenes', {}),
      api.GET('/api/v1/organizacion/empresas', {}),
    ]);

    expect(caducadas.size).toBe(2);
    expect(renovaciones).toBe(1);
  });
});

/**
 * Todo lo que hay en un almacén del navegador, en una sola cadena. Vacío = cadena vacía.
 *
 * Se recorre con `length` y `key(i)`, la API estándar, y NO con `{ ...almacen }`: el contenido de un
 * `Storage` no son propiedades propias enumerables del objeto, así que la versión con propagación
 * devuelve `{}` siempre —también cuando dentro hay un testigo— y el test se pondría verde sin mirar
 * nada. Es la misma trampa que el `clear()` del selector: la comprobación que parece más cómoda es
 * la que no comprueba.
 */
function volcado(almacen: Storage): string {
  const lineas: string[] = [];

  for (let indice = 0; indice < almacen.length; indice += 1) {
    const clave = almacen.key(indice) ?? '';
    lineas.push(`${clave}=${almacen.getItem(clave) ?? ''}`);
  }

  return lineas.join('\n');
}
