import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';

import { ALFA, HOY_SIMULADO, tarifasDe } from '@/pruebas/datos.ts';
import { abrirSesionYaRecuperada, servidor, servidorSimulado } from '@/pruebas/servidor.ts';
import { montarPantalla } from '@/pruebas/montar.tsx';
import { diaLegible, estadoDeVigencia, hoyEnElCalendarioLocal } from '../model/tarifa.ts';
import type { Tarifa } from '../model/tarifa.ts';
import { PaginaDeTarifas } from './PaginaDeTarifas.tsx';

/**
 * La pantalla de tarifas: los tres estados, los dos filtros, y lo que este ítem estrena — **que una
 * tarifa son varias filas, y cuál de ellas rige hoy**.
 *
 * El caso que de verdad vigila este fichero es el del tramo que **acaba hoy**. Los dos extremos de
 * la vigencia están incluidos, así que ese tramo todavía rige y dejará de hacerlo mañana. Pintarlo
 * con `<` en vez de `<=` no da ningún error: da una pantalla que dice «ya no rige» mientras la API
 * sigue devolviendo su precio, y quien lo mire creerá que el precio que le están cobrando sale de
 * otro sitio. Es la misma convención que el `daterange(…, '[]')` de la restricción de exclusión y
 * que el `<=` de `Tarifa.RigeEl`: tres sitios, una sola convención.
 */

/**
 * EL RELOJ, PARADO — y sólo el reloj.
 *
 * `toFake: ['Date']` y no los temporizadores enteros: lo único que hay que fijar es qué día es hoy.
 * Falseando también `setTimeout` habría que devolverle el control a `userEvent` y a react-query a
 * mano, y un test que se cuelga por el arnés es peor que uno que no existe.
 *
 * El día sale de `HOY_SIMULADO`, el mismo del que la fixture cuelga el tramo que acaba hoy. Escrito
 * en dos sitios, tocar uno dejaría el caso frontera verde sin comprobar nada: el tramo pasaría a
 * acabar ayer y «ya no rige» sería la respuesta correcta.
 */
beforeAll(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(`${HOY_SIMULADO}T10:00:00`));
});

afterAll(() => {
  vi.useRealTimers();
});

/** La fila de la tabla en la que sale ese texto. */
async function filaCon(texto: string): Promise<HTMLElement> {
  const celda = await screen.findByText(texto);
  const fila = celda.closest('tr');

  expect(fila, `«${texto}» no está dentro de ninguna fila`).not.toBeNull();

  return fila as HTMLElement;
}

describe('El listado de tarifas', () => {
  it('mientras llegan, dice que está cargando', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/tarifas', async () => {
        await delay(80);
        return HttpResponse.json(tarifasDe(ALFA.id));
      }),
    );

    montarPantalla(<PaginaDeTarifas />, '/tarifas');

    expect(await screen.findByText('Cargando las tarifas…')).toBeVisible();
    expect(await screen.findByText('Precio de venta al público 2026')).toBeVisible();
  });

  it('si el servidor falla, lo dice y deja volver a intentarlo', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();
    servidorSimulado.falloDeTarifas = 500;

    montarPantalla(<PaginaDeTarifas />, '/tarifas');

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('El servidor no ha podido responder. Inténtalo de nuevo.');

    // La salida existe de verdad: no es un botón decorativo, vuelve a pedir. Y el recuadro de
    // filtro sigue en pie mientras tanto, que es lo que permite reintentar con otro criterio.
    expect(screen.getByRole('searchbox')).toBeVisible();

    servidorSimulado.falloDeTarifas = null;
    await usuario.click(screen.getByRole('button', { name: 'Volver a intentarlo' }));

    expect(await screen.findByText('Precio de venta al público 2026')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin tarifas, dice QUÉ está vacío y no «sin datos»', async () => {
    abrirSesionYaRecuperada();

    servidor.use(
      http.get('/api/v1/catalogo/tarifas', () =>
        HttpResponse.json({ elementos: [], pagina: 1, tamanio: 20, total: 0 }),
      ),
    );

    montarPantalla(<PaginaDeTarifas />, '/tarifas');

    expect(
      await screen.findByText('Todavía no hay ninguna tarifa dada de alta en esta empresa.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('vacío POR EL FILTRO no se dice igual que vacío del todo, y nombra el filtro', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeTarifas />, '/tarifas?busqueda=Loquesea');

    expect(await screen.findByText('Ninguna tarifa coincide con «Loquesea».')).toBeVisible();
    expect(
      screen.queryByText('Todavía no hay ninguna tarifa dada de alta en esta empresa.'),
    ).not.toBeInTheDocument();
  });

  it('vacío POR EL CÓDIGO se dice de una tercera manera, que es la que evita el alta repetida', async () => {
    abrirSesionYaRecuperada();

    // Acotar por un código que no existe devuelve exactamente lo mismo que una tarifa recién
    // abierta y todavía sin tramos: una página vacía con un 200. Decir «no hay ninguna tarifa»
    // mandaría a darla de alta cuando lo que hay es una errata en el código.
    montarPantalla(<PaginaDeTarifas />, '/tarifas?codigo=noexiste');

    expect(
      await screen.findByText('Ninguna tarifa de esta empresa tiene el código «NOEXISTE».'),
    ).toBeVisible();

    // Y el código se normaliza ANTES de pedirlo y de escribirlo: el rótulo y las filas tienen que
    // decir el mismo código, porque están guardados en mayúsculas.
    expect(servidorSimulado.tarifasPedidas).toEqual([{ busqueda: '', codigo: 'NOEXISTE' }]);
  });

  it('filtrar lo escribe EN LA URL, y el criterio llega al servidor', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(<PaginaDeTarifas />, '/tarifas');

    expect(await screen.findByText('Mayorista, a partir de octubre')).toBeVisible();

    await usuario.type(screen.getByRole('searchbox'), 'Promoción');
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByText('Promoción de septiembre')).toBeVisible();
    await waitFor(() => {
      expect(screen.queryByText('Mayorista, a partir de octubre')).not.toBeInTheDocument();
    });

    // Lo que distingue esto de un `useState`: el filtro está en la ubicación, así que el listado
    // filtrado se puede pegar en un correo y la flecha de atrás lo deshace.
    expect(enrutador.state.location.search).toBe('?busqueda=Promoci%C3%B3n');

    // Y llega al servidor. Sin esto, una pantalla que se trajera todo y filtrara en el navegador
    // pintaría exactamente lo mismo — hasta el día en que hay trescientos tramos.
    expect(servidorSimulado.tarifasPedidas).toEqual([
      { busqueda: '', codigo: null },
      { busqueda: 'Promoción', codigo: null },
    ]);
  });

  it('desde una fila se ven LOS TRAMOS de esa tarifa, y se dice que un código son varias filas', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(<PaginaDeTarifas />, '/tarifas');

    // DOS botones con el mismo nombre, y no es un descuido: PVP sale dos veces porque son dos
    // tramos, que es justo lo que esta pantalla existe para enseñar. Cada fila ofrece su acción, y
    // las dos llevan al mismo sitio porque el acotado es por CÓDIGO, no por tramo.
    const botones = await screen.findAllByRole('button', { name: 'Ver los tramos de PVP' });
    expect(botones).toHaveLength(2);

    await usuario.click(botones[0]!);

    // Los DOS tramos del mismo código, que es lo que esta pantalla existe para enseñar.
    expect(await screen.findByText('Precio de venta al público 2026')).toBeVisible();
    expect(screen.getByText('Precio de venta al público 2025')).toBeVisible();
    expect(screen.queryByText('Promoción de septiembre')).not.toBeInTheDocument();

    // Y se explica la forma del dato, no solo que hay un filtro puesto: sin esta frase, dos filas
    // con el mismo código parecen una tarifa duplicada.
    expect(
      screen.getByText(
        'Mostrando los tramos de la tarifa «PVP», del más reciente al más antiguo. Una tarifa ' +
          'son varias filas: una por cada periodo de vigencia, y no se solapan nunca.',
      ),
    ).toBeVisible();

    expect(enrutador.state.location.search).toBe('?codigo=PVP');
    expect(servidorSimulado.tarifasPedidas).toEqual([
      { busqueda: '', codigo: null },
      { busqueda: '', codigo: 'PVP' },
    ]);

    // Acotado a un código, el botón de cada fila sobra: llevaría al sitio en el que ya se está.
    expect(screen.queryByRole('button', { name: 'Ver los tramos de PVP' })).not.toBeInTheDocument();
  });

  it('quitar el filtro de código lo borra de la URL y devuelve el listado entero', async () => {
    const usuario = userEvent.setup();
    abrirSesionYaRecuperada();

    const { enrutador } = montarPantalla(<PaginaDeTarifas />, '/tarifas?codigo=PVP&pagina=2');

    await screen.findByText(/Mostrando los tramos de la tarifa «PVP»/);

    await usuario.click(screen.getByRole('button', { name: 'Quitar el filtro de código' }));

    expect(await screen.findByText('Promoción de septiembre')).toBeVisible();

    // La página también se va: quedarse en la segunda al quitar el filtro enseña una página vacía
    // de un resultado que sí tiene filas.
    expect(enrutador.state.location.search).toBe('');
  });

  it('EL TRAMO QUE ACABA HOY TODAVÍA RIGE, y los otros dos estados se dicen distinto', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeTarifas />, '/tarifas');

    // El caso frontera. La promoción acaba HOY: el último día de vigencia está incluido, así que
    // sigue poniendo precio hoy y dejará de hacerlo mañana.
    expect(await filaCon('Promoción de septiembre')).toHaveTextContent('Rige hoy');

    // Y los otros dos, para que «Rige hoy» no sea lo que pinta una pantalla que no mira las fechas.
    expect(await filaCon('Mayorista, a partir de octubre')).toHaveTextContent('Todavía no rige');
    expect(await filaCon('Precio de venta al público 2025')).toHaveTextContent('Ya no rige');
    expect(await filaCon('Precio de venta al público 2026')).toHaveTextContent('Rige hoy');
  });

  it('el periodo se pinta con sus dos formas: cerrado por los dos lados, o abierto por el final', async () => {
    abrirSesionYaRecuperada();

    montarPantalla(<PaginaDeTarifas />, '/tarifas');

    // El día se escribe entero, no en bruto: se comprueban el número y el año, que es lo que
    // significa la celda. La grafía del mes la pone `Intl` y depende de la versión de ICU de la
    // máquina; afirmarla aquí sería un test que se pone rojo al actualizar Node, no al romper nada.
    const abierto = await filaCon('Precio de venta al público 2026');
    expect(abierto.textContent).toMatch(/Desde el .*2026/);
    expect(abierto).not.toHaveTextContent('2026-01-01');

    const cerrado = await filaCon('Precio de venta al público 2025');
    expect(cerrado.textContent).toMatch(/Del .*2025.* al .*2025/);
  });
});

/**
 * La regla de la vigencia, ejercida en la frontera y con el día puesto a mano.
 *
 * Aquí es donde se separan `<=` y `<`. La pantalla lo enseña, pero la pantalla tiene un día solo —el
 * de hoy— y los casos que importan son los DOS bordes, que sólo se pueden poner uno al lado del otro
 * dándole el día como parámetro.
 */
describe('La vigencia de un tramo', () => {
  const tramo = (desde: string, hasta: string | null): Tarifa => ({
    id: 'da-igual',
    codigo: 'PVP',
    nombre: 'Un tramo',
    vigenteDesde: desde,
    vigenteHasta: hasta,
  });

  it('el primer día ya rige y el último todavía, porque los dos extremos están incluidos', () => {
    const cerrado = tramo('2026-01-01', '2026-12-31');

    expect(estadoDeVigencia(cerrado, '2025-12-31')).toBe('futura');
    expect(
      estadoDeVigencia(cerrado, '2026-01-01'),
      'el primer día de vigencia se ha pintado como futuro: el tramo empieza ESE día, no al ' +
        'siguiente',
    ).toBe('rige');
    expect(
      estadoDeVigencia(cerrado, '2026-12-31'),
      'el último día de vigencia se ha pintado como caducado. Es el borde que separa `<=` de `<`: ' +
        'ese día la API sigue devolviendo el precio de este tramo, y la pantalla estaría diciendo ' +
        'lo contrario',
    ).toBe('rige');
    expect(estadoDeVigencia(cerrado, '2027-01-01')).toBe('caducada');
  });

  it('un tramo sin fecha de fin no caduca nunca', () => {
    const abierto = tramo('2026-01-01', null);

    expect(estadoDeVigencia(abierto, '2026-01-01')).toBe('rige');
    expect(estadoDeVigencia(abierto, '2099-12-31')).toBe('rige');
  });

  it('el día de hoy es el del calendario de quien mira, no el de Greenwich', () => {
    // Las dos puntas del día. En cualquier huso distinto de UTC, una de las dos cae en otro día al
    // pasar por UTC: con `toISOString()` —el atajo de una línea que hace esto mismo— una de estas
    // dos aserciones sale roja se ejecute donde se ejecute, al este o al oeste. Por eso son dos y
    // no una: con una sola, el caso sería verde en media Tierra y rojo en la otra media, que es
    // exactamente el test que no avisa en la máquina en la que había que avisar.
    expect(hoyEnElCalendarioLocal(new Date(2026, 8, 11, 0, 30))).toBe('2026-09-11');
    expect(hoyEnElCalendarioLocal(new Date(2026, 8, 11, 23, 30))).toBe('2026-09-11');
  });

  it('un día que no tiene forma de día se pinta tal cual, en vez de romper la pantalla', () => {
    // Viene de la red. Una celda rara se ve, se cuenta y se arregla; una pantalla en blanco por un
    // `RangeError` a mitad del pintado se lleva por delante también las filas que estaban bien.
    expect(diaLegible('mañana', 'es')).toBe('mañana');
    expect(diaLegible('2026-09-11', 'es')).not.toBe('2026-09-11');
  });
});
