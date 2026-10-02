import { z } from 'zod';

import { NIVELES_DE_GTIN } from './codigoDeBarras.ts';
import type { Diccionario } from '@/app/i18n/es.ts';

type CodigoConTexto = keyof Diccionario['errores']['tipos'];

/**
 * Los rechazos del GTIN que el formulario reconoce antes de ir al servidor: los mismos `type` que
 * devuelve la API, para que el texto sea el mismo lo diga quien lo diga.
 */
const MOTIVOS_DEL_FORMULARIO = [
  'gtin-no-son-digitos',
  'gtin-largo-no-admitido',
  'gtin-digito-de-control',
] as const satisfies readonly CodigoConTexto[];

type MotivoDelFormulario = (typeof MOTIVOS_DEL_FORMULARIO)[number];

/**
 * Los rechazos que van en el campo del GTIN, vengan del esquema o del servidor.
 *
 * Los cuatro últimos solo los dice el servidor: la tabla de prefijos que no son el GTIN de un
 * artículo (ADR-0051 §4) no se copia aquí, porque tendría que seguir a la del dominio línea a línea.
 * Y el duplicado, porque solo el servidor sabe qué GTIN lleva otro artículo.
 */
export const RECHAZOS_DEL_GTIN = [
  ...MOTIVOS_DEL_FORMULARIO,
  'gtin-medida-variable',
  'gtin-circulacion-restringida',
  'gtin-cupon',
  'gtin-sin-asignar',
  'codigo-barras-duplicado',
] as const satisfies readonly CodigoConTexto[];

/** Lo que va en el campo de las unidades, venga del esquema o del servidor. */
export const RECHAZO_DE_LAS_UNIDADES = 'codigo-barras-unidades-no-validas' satisfies CodigoConTexto;

/** Lo que va en el grupo del nivel. Solo lo diría el servidor: el formulario no deja elegir otro. */
export const RECHAZO_DEL_NIVEL = 'codigo-barras-nivel-no-valido' satisfies CodigoConTexto;

/** Un rechazo que se pinta en un campo del formulario. */
export type RechazoDeCampo =
  (typeof RECHAZOS_DEL_GTIN)[number] | typeof RECHAZO_DE_LAS_UNIDADES | typeof RECHAZO_DEL_NIVEL;

const RECHAZOS_DE_CAMPO: ReadonlySet<string> = new Set<RechazoDeCampo>([
  ...RECHAZOS_DEL_GTIN,
  RECHAZO_DE_LAS_UNIDADES,
  RECHAZO_DEL_NIVEL,
]);

/** Si un mensaje de error del formulario es uno de los suyos, estrechado para traducirlo. */
export function esRechazoDeCampo(mensaje: string | undefined): mensaje is RechazoDeCampo {
  return mensaje !== undefined && RECHAZOS_DE_CAMPO.has(mensaje);
}

/** Los largos con los que entra un GTIN: GTIN-8, GTIN-12, GTIN-13 y GTIN-14. */
const LARGOS: ReadonlySet<number> = new Set([8, 12, 13, 14]);

/**
 * Si la última cifra es la que sale de las demás (GS1, §7.9.1).
 *
 * De derecha a izquierda y sin contar la de control, pesos 3 y 1 alternos empezando por 3; el
 * control es lo que falta hasta la decena. Es la misma cuenta que `Gtin.DigitoDeControl` del
 * dominio, y vale para los cuatro largos sin rellenar: los ceros de delante no suman.
 */
export function cuadraElDigitoDeControl(cifras: string): boolean {
  let suma = 0;

  for (let posicion = cifras.length - 2, peso = 3; posicion >= 0; posicion -= 1, peso = 4 - peso) {
    suma += Number(cifras[posicion]) * peso;
  }

  return (10 - (suma % 10)) % 10 === Number(cifras.at(-1));
}

/**
 * Por qué un texto no es un GTIN, en el orden en que lo pregunta el servidor, o `null` si puede
 * serlo.
 *
 * Se recortan los extremos y nada más, como en `Gtin.Leer`: un espacio o un guion por dentro no se
 * quitan en silencio. Y solo cifras ASCII. El vacío no son cifras que falten sino un largo que no
 * vale, igual que en el servidor.
 */
export function motivoDelGtin(texto: string): MotivoDelFormulario | null {
  const recortado = texto.trim();

  if (!/^[0-9]*$/.test(recortado)) {
    return 'gtin-no-son-digitos';
  }

  if (!LARGOS.has(recortado.length)) {
    return 'gtin-largo-no-admitido';
  }

  return cuadraElDigitoDeControl(recortado) ? null : 'gtin-digito-de-control';
}

/** El mayor `int` de 32 bits, que es lo que el contrato admite en las unidades. */
const MAXIMO_DEL_CONTRATO = 2_147_483_647;

/**
 * Si unas unidades valen para una caja o un palé: un entero de dos en adelante, en cifras, que quepa
 * en el `int` del contrato.
 *
 * La base no pasa por aquí. Lleva una unidad y no se manda ninguna: la pone el servidor. El tope es
 * el del enlace del modelo: por encima, el servidor no llega a mirar el nivel y contesta
 * `datos-no-validos`, arriba del formulario y no en el campo.
 */
export function sonUnidadesDeUnaAgrupacion(texto: string): boolean {
  const recortado = texto.trim();

  if (!/^[0-9]+$/.test(recortado)) {
    return false;
  }

  const unidades = Number.parseInt(recortado, 10);

  return unidades >= 2 && unidades <= MAXIMO_DEL_CONTRATO;
}

const nivel = z.enum(NIVELES_DE_GTIN, { error: RECHAZO_DEL_NIVEL });

/**
 * La regla del formulario de alta: un GTIN que puede serlo, un nivel, y las unidades de una caja o
 * de un palé.
 *
 * <b>Replica, no sustituye.</b> El dígito de control y el largo se dicen aquí sin ir al servidor;
 * la tabla de prefijos y el duplicado, solo allí. Los mensajes son los `type` de la API y no claves
 * propias, así que el mismo error se lee igual lo pare quien lo pare.
 *
 * <b>Las unidades se miran aunque el GTIN esté mal.</b> El rechazo del GTIN es un `addIssue` de un
 * refinamiento, y Zod 4 lo da por continuable: el refinamiento del objeto corre igual, y quien
 * teclea un GTIN mal y una caja sin unidades ve los dos errores a la vez. Lo que sí lo para es un
 * nivel que no está en la lista, y entonces no hay unidades que mirar.
 */
export const esquemaDeAltaDeGtin = z
  .object({
    gtin: z.string().superRefine((texto, contexto) => {
      const motivo = motivoDelGtin(texto);

      if (motivo !== null) {
        contexto.addIssue({ code: 'custom', message: motivo });
      }
    }),
    nivel,
    unidades: z.string(),
  })
  .refine((datos) => datos.nivel === 'base' || sonUnidadesDeUnaAgrupacion(datos.unidades), {
    message: RECHAZO_DE_LAS_UNIDADES,
    path: ['unidades'],
  });

export type DatosDeAltaDeGtin = z.infer<typeof esquemaDeAltaDeGtin>;
