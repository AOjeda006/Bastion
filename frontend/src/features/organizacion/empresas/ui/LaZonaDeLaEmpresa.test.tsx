import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { components } from '@/shared/api/esquema.ts';
import { EMPRESA_ALFA, PERMISOS_DE_LECTURA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada, servidor } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

type EmpresaDto = components['schemas']['EmpresaDto'];

/** La empresa del lado del servidor, con su versión, y lo que ha llegado a cada `PUT`. */
const servidorDeEmpresas = {
  empresa: EMPRESA_ALFA,
  version: 9,
  escrituras: [] as { ifMatch: string | null; cuerpo: Record<string, unknown> }[],
};

function etiqueta(version: number): string {
  return `"${String(version)}"`;
}

beforeEach(() => {
  servidorDeEmpresas.empresa = { ...EMPRESA_ALFA };
  servidorDeEmpresas.version = 9;
  servidorDeEmpresas.escrituras = [];

  servidor.use(
    http.get('/api/v1/organizacion/empresas', () =>
      HttpResponse.json({
        elementos: [servidorDeEmpresas.empresa],
        pagina: 1,
        tamanio: 20,
        total: 1,
      }),
    ),

    http.get('/api/v1/organizacion/empresas/:id', () =>
      HttpResponse.json(servidorDeEmpresas.empresa, {
        headers: { ETag: etiqueta(servidorDeEmpresas.version) },
      }),
    ),

    http.put('/api/v1/organizacion/empresas/:id', async ({ request }) => {
      const ifMatch = request.headers.get('If-Match');
      const cuerpo = (await request.json()) as Record<string, unknown>;
      servidorDeEmpresas.escrituras.push({ ifMatch, cuerpo });

      if (ifMatch !== etiqueta(servidorDeEmpresas.version)) {
        return HttpResponse.json(
          { type: '/errors/version-obsoleta', status: 412 },
          { status: 412 },
        );
      }

      servidorDeEmpresas.empresa = {
        ...servidorDeEmpresas.empresa,
        zonaHoraria: cuerpo['zonaHoraria'] as EmpresaDto['zonaHoraria'],
      };
      servidorDeEmpresas.version += 1;

      return HttpResponse.json(servidorDeEmpresas.empresa);
    }),
  );
});

/** La celda de la zona de la única empresa. */
async function zonaDeAlfa(): Promise<HTMLElement> {
  const fila = (await screen.findByRole('cell', { name: EMPRESA_ALFA.razonSocial })).closest('tr');

  if (fila === null) {
    throw new Error('Alfa no está en ninguna fila.');
  }

  return within(fila).getAllByRole('cell').at(-1)!;
}

/**
 * La zona de la empresa, en su listado (ítem 3.5, ADR-0079 §12 y ADR-0080): la misma ventana que la
 * del almacén (`LaZonaDelAlmacen.test.tsx`), con las tres zonas y un aviso más.
 */
describe('La zona de la empresa', () => {
  it('el listado dice la zona, sin de dónde le viene, y sin el permiso no la ofrece', async () => {
    abrirSesionSimulada();
    montarAplicacion('/empresas');

    const zona = await zonaDeAlfa();
    expect(zona).toHaveTextContent('Península y Baleares');
    // La de la empresa es siempre suya: «propia» o «de la empresa» es cosa del almacén.
    expect(zona).not.toHaveTextContent('·');
    expect(screen.queryByRole('button', { name: /^Cambiar/ })).not.toBeInTheDocument();
  });

  it('guarda la empresa entera sobre la versión leída, con el aviso de sus almacenes', async () => {
    const usuario = userEvent.setup();
    abrirSesionSimulada(undefined, [...PERMISOS_DE_LECTURA, 'organizacion.empresa.modificar']);
    montarAplicacion('/empresas');

    await usuario.click(
      await screen.findByRole('button', { name: `Cambiar la zona de ${EMPRESA_ALFA.razonSocial}` }),
    );

    const ventana = await screen.findByRole('dialog', {
      name: `Zona horaria de ${EMPRESA_ALFA.razonSocial}`,
    });
    const grupo = await within(ventana).findByRole('group', { name: 'Zona horaria' });
    expect(
      within(grupo)
        .getAllByRole('radio')
        .map((opcion) => opcion.closest('label')?.textContent),
    ).toEqual(['Península y Baleares', 'Canarias', 'Ceuta y Melilla']);
    expect(within(grupo).getByRole('radio', { name: 'Península y Baleares' })).toBeChecked();
    expect(grupo).toHaveAccessibleDescription(
      expect.stringContaining(
        'También cambia el día de los almacenes de esta empresa que no tienen zona propia.',
      ),
    );

    await usuario.click(within(grupo).getByRole('radio', { name: 'Canarias' }));
    await usuario.click(within(ventana).getByRole('button', { name: 'Guardar la zona' }));

    expect(
      await screen.findByText(`La zona de ${EMPRESA_ALFA.razonSocial} es ahora Canarias.`),
    ).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(servidorDeEmpresas.escrituras).toEqual([
      {
        ifMatch: etiqueta(9),
        cuerpo: {
          razonSocial: EMPRESA_ALFA.razonSocial,
          domicilioFiscal: EMPRESA_ALFA.domicilioFiscal,
          divisaBase: EMPRESA_ALFA.divisaBase,
          regimenDeIva: EMPRESA_ALFA.regimenDeIva,
          zonaHoraria: 'Atlantic/Canary',
        },
      },
    ]);

    await waitFor(async () => {
      expect(await zonaDeAlfa()).toHaveTextContent('Canarias');
    });
  });
});
