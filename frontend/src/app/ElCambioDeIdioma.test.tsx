import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { en } from './i18n/en.ts';
import { es } from './i18n/es.ts';
import { IDIOMAS } from './i18n/idioma.ts';
import { ALFA } from '@/pruebas/datos.ts';
import { abrirSesionSimulada } from '@/pruebas/servidor.ts';
import { montarAplicacion } from '@/pruebas/montar.tsx';

/**
 * EL CAMBIO DE IDIOMA — lo que el §3 pedía «desde el primer día» y llegó en el ítem 0.14.
 *
 * Lo que se comprueba es lo que VE una persona, no que se haya llamado a `changeLanguage`. Un test
 * de «se ha llamado» seguiría verde el día que un componente se quedara con su literal escrito a
 * mano, que es justo el fallo que esto persigue.
 *
 * Y se comprueba también el `lang` del documento, que no se ve en ninguna captura de pantalla y de
 * él depende con qué voz lee un lector de pantalla (WCAG 3.1.1).
 */

/** Todas las claves del diccionario, en profundidad y con su camino: `comun.salir`, `rutas.inicio`… */
function clavesEnProfundidad(objeto: object, prefijo = ''): string[] {
  return Object.entries(objeto).flatMap(([clave, valor]) => {
    const camino = prefijo === '' ? clave : `${prefijo}.${clave}`;

    return typeof valor === 'object' && valor !== null
      ? clavesEnProfundidad(valor as object, camino)
      : [camino];
  });
}

describe('El cambio de idioma', () => {
  it('arranca en el idioma que se le diga, y el documento lo declara', async () => {
    abrirSesionSimulada(ALFA.id);
    montarAplicacion('/almacenes', 'es');

    expect(await screen.findByRole('heading', { level: 1, name: 'Almacenes' })).toBeInTheDocument();

    // El `lang` sí se mira a pelo, y no por descuido: lo pone `crearI18n`, que corre ANTES del
    // `render`. Cuando el `<h1>` existe, el `lang` lleva puesto desde antes de que hubiera árbol.
    expect(document.documentElement.lang).toBe('es');

    // El `<title>` no. Lo pone un efecto de `Disposicion`, y un efecto se ejecuta DESPUÉS del
    // pintado — o sea, después del instante en que `findByRole` ha podido resolver. Mirarlo sin
    // esperar es una carrera que se gana casi siempre: pasó en todas las ejecuciones de esta suite
    // hasta que la perdió en la CI, sobre un commit que solo tocaba un `.md`. Se reprodujo aquí
    // lanzando seis copias de este fichero a la vez —falló una, con este mismo mensaje—, que es lo
    // que hace la CI cuando el runner va cargado. Los otros dos sitios que miran el título ya
    // esperaban así; este era el único que no.
    await waitFor(() => {
      expect(document.title).toBe('Almacenes · Bastion');
    });
  });

  it('al elegir English cambia la pantalla ENTERA, sin recargar', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/almacenes', 'es');

    expect(await screen.findByRole('heading', { level: 1, name: 'Almacenes' })).toBeInTheDocument();

    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');

    // El encabezado, que sale de la CLAVE de la ruta: si el título fuera una frase escrita en la
    // tabla de rutas, esto se quedaría en «Almacenes» y el caso saldría rojo aquí.
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Warehouses' }),
    ).toBeInTheDocument();

    // La navegación principal, los rótulos de la tabla y el botón de salir: tres sitios distintos
    // —`rutas`, `almacenes` y `comun`— para que un solo grupo del diccionario traducido no baste.
    expect(screen.getByRole('link', { name: 'Warehouses' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Code' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();

    // Y lo que no se ve: el `<title>` y el `lang`.
    await waitFor(() => {
      expect(document.title).toBe('Warehouses · Bastion');
    });
    expect(document.documentElement.lang).toBe('en');
  });

  it('el anuncio que oye un lector de pantalla cambia de idioma con lo demás', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/almacenes', 'es');

    const anuncio = await screen.findByRole('status', { name: 'Estado de la navegación' });
    expect(anuncio).toHaveTextContent('La página Almacenes se ha cargado.');

    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Idioma' }), 'en');

    // Se busca por el nombre NUEVO: el `aria-label` de la región también se traduce, y si se
    // quedara en castellano un lector anunciaría la región en un idioma y su contenido en otro.
    const anuncioEnIngles = await screen.findByRole('status', { name: 'Navigation status' });
    expect(anuncioEnIngles).toHaveTextContent('The Warehouses page has loaded.');
  });

  it('la elección se recuerda para la próxima visita', async () => {
    abrirSesionSimulada(ALFA.id);
    const usuario = userEvent.setup();
    montarAplicacion('/', 'es');

    await usuario.selectOptions(await screen.findByRole('combobox', { name: 'Idioma' }), 'en');

    expect(window.localStorage.getItem('bastion.idioma')).toBe('en');
  });

  it('los dos diccionarios traen EXACTAMENTE las mismas claves, y no están vacíos', () => {
    // El tipo `Diccionario` ya obliga a que `en` cumpla la forma de `es`, así que esto no repite
    // al compilador: afirma lo que el compilador NO puede: que el conjunto no esté vacío. Dos
    // diccionarios vacíos cumplen el tipo, casan clave a clave, y dejarían verdes todos los demás
    // casos de este fichero sin haber comprobado una sola traducción.
    const enCastellano = clavesEnProfundidad(es).sort();
    const enIngles = clavesEnProfundidad(en).sort();

    expect(enCastellano.length).toBeGreaterThan(40);
    expect(enIngles).toEqual(enCastellano);
    expect(IDIOMAS).toHaveLength(2);
  });

  it('todo `type` que la API puede emitir tiene texto, y ningún texto sobra', () => {
    // ADR-0030: desde el ítem 1.5, el texto que lee una persona cuando algo falla lo escribe el
    // FRONTAL a partir del `type` del ProblemDetails. Eso convierte el conjunto de `type` en
    // contrato entre los dos lados, y un contrato que nadie compara se desincroniza sin ruido: el
    // backend estrena un código, aquí no hay texto, y quien lo sufre ve el mensaje genérico. No
    // hay error, no hay aviso, no hay nada — solo una frase peor.
    //
    // Se compara ENTERO y en los DOS sentidos, no «que no falte ninguno»:
    //   · un `type` sin texto es el fallo de arriba;
    //   · un texto sin `type` es un código que se borró o se renombró en el backend y dejó aquí
    //     una entrada muerta que nadie volverá a ver, y que en la siguiente revisión alguien
    //     copiará creyendo que sigue viva.
    //
    // La fuente es el artefacto versionado, el mismo fichero que la CI regenera y compara contra
    // el código fuente (`scripts/generar-errores.sh`). Aquí NO se vuelve a barrer el backend: eso
    // sería repetir el guion y las dos copias se separarían. Se lee lo que el guion dejó.
    const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
    const artefacto = resolve(raiz, 'docs/api/errores.json');

    const catalogo = JSON.parse(readFileSync(artefacto, 'utf8')) as {
      base: string;
      tipos: { codigo: string; type: string; clase: string }[];
    };

    // El ancla contra el falso verde. Si el fichero se moviera, o el `JSON.parse` devolviera algo
    // con otra forma, `tipos` sería `undefined` y el `.map` reventaría — bien. Pero un artefacto
    // con `"tipos": []` es JSON válido, cumple el tipo, y dejaría las dos listas vacías casando
    // perfectamente. Eso es lo que esta línea impide.
    expect(
      catalogo.tipos.length,
      `${artefacto} no trae ni un tipo: se genera con \`bash scripts/generar-errores.sh\``,
    ).toBeGreaterThan(20);

    const queEmiteLaApi = catalogo.tipos.map((tipo) => tipo.codigo).sort();
    const queTienenTexto = Object.keys(es.errores.tipos).sort();

    expect(queTienenTexto).toEqual(queEmiteLaApi);

    // Y el inglés por su cuenta. El caso de arriba ya exige que los dos diccionarios tengan las
    // mismas claves, así que esto es redundante HOY; se escribe igual porque lo que aquí importa
    // no es que los idiomas casen entre sí, sino que CADA UNO cubra el contrato. El día que
    // aquella comparación se relaje —o que entre un tercer idioma— esta seguiría diciendo la
    // verdad.
    expect(Object.keys(en.errores.tipos).sort()).toEqual(queEmiteLaApi);
  });

  it('ninguna traducción se ha quedado sin traducir', () => {
    // Copiar el castellano en `en.ts` cumple el tipo, cumple la comparación de claves de arriba, y
    // deja la mitad de la aplicación en castellano para quien lee inglés. Esto lo caza: se exige
    // que la inmensa mayoría de los textos SEAN distintos. No todos —«Bastion», «NIF» o «{{titulo}}
    //  · Bastion» se escriben igual en los dos—, y por eso el umbral es alto y no total.
    const claves = clavesEnProfundidad(es);
    const iguales = claves.filter((clave) => {
      const leer = (d: object): unknown =>
        clave.split('.').reduce<unknown>((valor, parte) => (valor as never)[parte], d);

      return leer(es) === leer(en);
    });

    expect(
      iguales.length,
      `Hay ${String(iguales.length)} textos idénticos en los dos idiomas: ${iguales.join(', ')}`,
    ).toBeLessThan(4);
  });
});
