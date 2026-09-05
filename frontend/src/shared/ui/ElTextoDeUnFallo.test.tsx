import { renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, it } from 'vitest';

import { crearI18n } from '@/app/i18n/index.ts';
import type { Idioma } from '@/app/i18n/idioma.ts';
import { FalloDeApi, fallo } from '@/shared/api/errores.ts';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * EL TEXTO DE UN FALLO — la pieza (c) del ADR-0030.
 *
 * El artefacto y el barrido (piezas a y b) garantizan que en el REPOSITORIO no hay ningún `type`
 * sin texto. Lo que se comprueba aquí es lo otro: qué pasa EN EJECUCIÓN cuando aun así llega uno
 * desconocido, que es lo que ocurre durante un despliegue escalonado o con una pestaña que lleva
 * abierta desde la versión anterior.
 *
 * Sin `act(...)` en ninguna parte y a propósito: esto no monta pantalla ni dispara efectos, es
 * una función y un diccionario. Los 109 avisos de `act()` que el frontal arrastra son deuda
 * anotada, y lo que un ítem nuevo no puede hacer es aumentarlos.
 */
function conIdioma(idioma: Idioma) {
  const i18n = crearI18n(idioma);

  return function Envoltorio({ children }: { children: ReactNode }) {
    return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
  };
}

function texto(error: unknown, idioma: Idioma = 'es'): string {
  const { result } = renderHook(() => useTextoDeFallo(), { wrapper: conIdioma(idioma) });

  return result.current(error);
}

describe('El texto de un fallo', () => {
  it('un `type` conocido se lee como su instrucción, y en el idioma que toca', () => {
    const conflicto = fallo(409, { type: '/errors/almacen-duplicado', traceId: 'abc123' });

    expect(texto(conflicto)).toBe('Ya hay un almacén con ese código en esta empresa.');
    expect(texto(conflicto, 'en')).toBe(
      'There is already a warehouse with that code at this company.',
    );
  });

  it('un `type` DESCONOCIDO cae al genérico y se lleva la referencia de traza consigo', () => {
    // El caso que justifica la pieza (c). Lo que NO puede pasar es que salga la clave cruda
    // —`t()` con una clave inexistente devuelve la propia clave— ni que salga el genérico pelado,
    // porque entonces nadie puede ir al registro a ver qué fue.
    const raro = fallo(409, { type: '/errors/algo-que-este-frontal-no-conoce', traceId: 'tr-42' });

    const frase = texto(raro);

    expect(frase).not.toContain('errores.');
    expect(frase).toContain('tr-42');
    expect(frase).toBe(
      'No se ha podido completar la operación. Si vuelve a pasar, indica esta referencia: tr-42.',
    );
  });

  it('un `type` desconocido SIN traza no enseña un hueco donde iba la referencia', () => {
    const sinTraza = fallo(409, { type: '/errors/algo-que-este-frontal-no-conoce' });

    const frase = texto(sinTraza);

    expect(frase).not.toContain('{{traza}}');
    expect(frase).toBe('No se han podido cargar los datos. Inténtalo de nuevo.');
  });

  it('una respuesta sin `type` sigue cayendo en el motivo de siempre, por código de estado', () => {
    // El camino de antes del ADR-0030, que no se toca: un 502 de un intermediario o un fallo de
    // red no traen ProblemDetails y no hay nada que mapear.
    expect(texto(fallo(500))).toBe('El servidor no ha podido responder. Inténtalo de nuevo.');
    expect(texto(fallo(403))).toBe(
      'No tienes permiso para consultar esto con la empresa con la que estás operando.',
    );
    expect(texto(new Error('esto no es un fallo de la API'))).toBe(
      'No se han podido cargar los datos. Inténtalo de nuevo.',
    );
  });

  it('un cuerpo que no es un ProblemDetails no inventa ni `type` ni traza', () => {
    // La página de error de un intermediario, un JSON de otra forma, una cadena suelta. Lo que se
    // afirma es que nada de eso se cuela como código: `tipo` queda en null y el texto es el
    // genérico por estado, no una clave compuesta con basura.
    for (const cuerpo of [
      '<html>502</html>',
      42,
      { mensaje: 'vaya' },
      { type: 'otra-cosa' },
      null,
    ]) {
      const roto = fallo(502, cuerpo);

      expect(roto.tipo).toBeNull();
      expect(texto(roto)).toBe('El servidor no ha podido responder. Inténtalo de nuevo.');
    }
  });

  it('el `type` se guarda ya sin la base, que es como se indexa el diccionario', () => {
    // Si esto se guardara entero —`/errors/almacen-duplicado`— la clave saldría
    // `errores.tipos./errors/almacen-duplicado` y no existiría ninguna. Es la clase de fallo que
    // se ve solo cuando algo va mal, así que se afirma aquí y no cuando toque.
    expect(fallo(409, { type: '/errors/almacen-duplicado' }).tipo).toBe('almacen-duplicado');

    // Y un `type` que no viene de esta API no se recorta a lo bruto: o empieza por la base, o no
    // es de los nuestros.
    expect(fallo(409, { type: 'https://ejemplo.test/otra-cosa' }).tipo).toBeNull();
    expect(fallo(409, { type: '/errors/' }).tipo).toBeNull();
  });

  it('la traza solo se enseña cuando hace falta, y nunca con un `type` conocido', () => {
    // La traza es diagnóstico, no conversación. Enseñarla siempre convierte cada aviso de negocio
    // —«ya hay un almacén con ese código»— en un vertedero de identificadores que nadie lee.
    const conocido = new FalloDeApi(409, 'carga', 'almacen-duplicado', 'tr-99');

    expect(texto(conocido)).not.toContain('tr-99');
  });
});
