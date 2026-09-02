import type { RouteObject } from 'react-router';
import { describe, expect, it } from 'vitest';

import { RUTAS, type DeclaracionDeRuta } from './rutas.tsx';
import { crearRutas } from './enrutador.tsx';
import { en } from './i18n/en.ts';
import { es, type Diccionario } from './i18n/es.ts';
import { IDIOMAS, type Idioma } from './i18n/idioma.ts';

const DICCIONARIOS: Record<Idioma, Diccionario> = { es, en };

/**
 * EL QUINTO BARRIDO — toda ruta declara qué exige.
 *
 * Es el mismo mecanismo que los cuatro del backend: no se comprueba «que exista una declaración»,
 * se compara LA LISTA ENTERA. Una ruta nueva no se pone verde por parecerse a las que ya hay; o
 * está en la tabla o el barrido la nombra.
 *
 * Lo que persigue de verdad es la ruta metida a mano en `enrutador.tsx` sin pasar por la tabla:
 * como el enrutador se construye con un `map` sobre `RUTAS`, esa es la única forma de que exista
 * una ruta sin declaración, y este test la recorre después de construida para verla.
 *
 * Y lo de siempre, que conviene no perder de vista: esto no es un control de acceso. Es la
 * interfaz escondiendo lo que no toca. Quien escriba la URL a mano llega, y quien le dice que no es
 * el servidor.
 */

interface RutaRecorrida {
  readonly ruta: string;
  readonly declaracion: DeclaracionDeRuta | null;
}

function recorrer(rutas: readonly RouteObject[]): RutaRecorrida[] {
  const encontradas: RutaRecorrida[] = [];

  for (const ruta of rutas) {
    if (ruta.path !== undefined) {
      const manejo = ruta.handle as { declaracion?: DeclaracionDeRuta } | undefined;

      encontradas.push({ ruta: ruta.path, declaracion: manejo?.declaracion ?? null });
    }

    if (ruta.children !== undefined) {
      encontradas.push(...recorrer(ruta.children));
    }
  }

  return encontradas;
}

describe('El barrido de rutas', () => {
  const recorridas = recorrer(crearRutas());

  it('el enrutador no tiene ninguna ruta que no esté en la tabla', () => {
    const sinDeclarar = recorridas
      .filter((r) => r.declaracion === null || !RUTAS.includes(r.declaracion))
      .map((r) => r.ruta);

    expect(
      sinDeclarar,
      'Hay rutas en el enrutador que no salen de app/rutas.tsx. Toda ruta se declara ahí, ' +
        'con el permiso que exige o el motivo por el que no exige ninguno.',
    ).toEqual([]);
  });

  it('la tabla no tiene ninguna ruta que el enrutador no monte', () => {
    const montadas = recorridas.map((r) => r.ruta);
    const declaradas = RUTAS.map((r) => r.ruta);

    expect([...montadas].sort()).toEqual([...declaradas].sort());
  });

  it('la partición cuadra: 5 rutas = 2 públicas + 1 de sesión + 2 de permiso', () => {
    const porClase = {
      publica: RUTAS.filter((r) => r.exigencia.clase === 'publica'),
      sesion: RUTAS.filter((r) => r.exigencia.clase === 'sesion'),
      permiso: RUTAS.filter((r) => r.exigencia.clase === 'permiso'),
    };

    // Contada, como las del backend: si mañana hay seis rutas, este número obliga a mirar en cuál
    // de las tres clases ha caído la nueva en vez de dejar que se cuele en la más cómoda.
    expect(RUTAS).toHaveLength(5);
    expect(porClase.publica.map((r) => r.ruta)).toEqual(['/acceso', '*']);
    expect(porClase.sesion.map((r) => r.ruta)).toEqual(['/']);
    expect(porClase.permiso.map((r) => r.ruta)).toEqual(['/almacenes', '/empresas']);
    expect(porClase.publica.length + porClase.sesion.length + porClase.permiso.length).toBe(
      RUTAS.length,
    );
  });

  it('lo que no exige permiso explica por qué, con una frase de verdad', () => {
    for (const ruta of RUTAS) {
      if (ruta.exigencia.clase === 'permiso') {
        expect(ruta.exigencia.permiso, `${ruta.ruta} declara un permiso vacío`).toMatch(/^\w+\./);
        continue;
      }

      // Veinte caracteres no es un umbral mágico: es lo que hace falta para que «porque sí» no
      // pase. Un motivo que no explica nada es peor que ninguno, porque parece que sí lo hace.
      expect(
        ruta.exigencia.motivo.length,
        `${ruta.ruta} no explica por qué no exige permiso`,
      ).toBeGreaterThan(20);
    }
  });

  it('cada ruta tiene un título distinto EN TODOS LOS IDIOMAS', () => {
    // No es cosmético: el título es el `<title>`, el `<h1>` y el mensaje que se le anuncia al
    // lector de pantalla al navegar. Dos vistas con el mismo título dejan ese anuncio inservible
    // —dice lo mismo antes y después— y quitan la única pista de en qué pantalla se está.
    //
    // Se comprueban los títulos TRADUCIDOS y en los dos idiomas, no las claves. Que las claves
    // sean distintas no prueba nada: es el texto lo que lee una persona, y dos claves distintas
    // pueden traducirse igual sin que nada proteste. El bucle recorre `IDIOMAS`, así que un
    // idioma nuevo entra en la comprobación por existir, no por acordarse de añadirlo aquí.
    for (const idioma of IDIOMAS) {
      const diccionario = DICCIONARIOS[idioma];
      const titulos = RUTAS.map((r) => diccionario.rutas[r.claveDeTitulo]);

      expect(new Set(titulos).size, `${idioma}: hay dos rutas con el mismo título`).toBe(
        titulos.length,
      );
    }
  });

  it('los dos diccionarios traen las mismas claves de título, y no están vacías', () => {
    // El tipo ya obliga a que `en` cumpla la forma de `es`, así que esto no es un duplicado del
    // compilador: es la parte que el compilador NO puede afirmar —que el conjunto no esté vacío—.
    // Un diccionario de rutas sin ninguna clave cumpliría el tipo y dejaría el bucle de arriba
    // recorriendo cero elementos: verde sin haber comprobado nada.
    for (const idioma of IDIOMAS) {
      const claves = Object.keys(DICCIONARIOS[idioma].rutas);

      expect(claves.length, `${idioma}: el diccionario de rutas está vacío`).toBe(RUTAS.length);
      expect([...claves].sort()).toEqual([...RUTAS.map((r) => r.claveDeTitulo)].sort());
    }
  });
});
