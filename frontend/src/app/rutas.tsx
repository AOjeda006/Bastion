import type { Diccionario } from './i18n/es.ts';
import type { Funcionalidad } from '@/features/funcionalidades.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';

/**
 * LA TABLA DE RUTAS. Cada una declara qué exige para poder entrar.
 *
 * Es el quinto barrido del proyecto, el primero que vive en el frontal: igual que toda escritura
 * dice cómo se protege y toda consulta dice bajo qué inquilinato corre, **toda ruta dice qué exige
 * o por qué no exige nada**. El test `ElBarridoDeRutas` compara la lista ENTERA contra el enrutador
 * ya construido, así que una ruta añadida a mano en `enrutador.tsx` —sin pasar por aquí— sale roja.
 *
 * Y lo de siempre: esto es la interfaz escondiendo lo que no toca, no un control de acceso. Quien
 * escriba la URL a mano llega, y es el servidor quien le dice que no.
 */

/**
 * Cada pantalla se carga cuando hace falta: división del empaquetado por ruta.
 *
 * La carga la hace el ENRUTADOR (`lazy` de la ruta) y no `React.lazy`, porque en un enrutador de
 * datos la carga de una ruta es asunto suyo: la espera antes de confirmar la navegación y la
 * publica en `useNavigation()`, en vez de suspender por debajo y dejar a React decidiendo qué
 * enseñar mientras tanto. El precio es que la primera visita necesita un `HydrateFallback`, que es
 * el «cargando» de siempre y está en `enrutador.tsx`.
 */
type CargaDePagina = () => Promise<React.ComponentType>;

/**
 * Qué hace falta para entrar en una ruta. Tres clases, y las dos que no piden permiso tienen que
 * escribir POR QUÉ: «se me olvidó» y «no hace falta» se escriben igual si no se obliga a razonarlo.
 */
export type Exigencia =
  | { readonly clase: 'publica'; readonly motivo: string }
  | { readonly clase: 'sesion'; readonly motivo: string }
  | { readonly clase: 'permiso'; readonly permiso: string };

/** Las claves de título que existen. Sale del diccionario, no de una lista escrita aparte. */
export type ClaveDeTitulo = keyof Diccionario['rutas'];

/**
 * De quién es una pantalla: de una funcionalidad, o del armazón.
 *
 * `'armazon'` no es un cajón de sastre. Son las pantallas que **no son de ningún módulo** —la
 * portada y la de «no encontrada»—, y viven en `app/paginas/` por eso mismo. Meterlas en una
 * funcionalidad les daría un dueño que no tienen, y como una funcionalidad no importa de otra, la
 * primera pantalla de otro módulo que quisiera enlazarlas se daría contra la frontera.
 */
export type Duenio = 'armazon' | Funcionalidad;

/** Una ruta de la aplicación, con todo lo que hace falta saber de ella. */
export interface DeclaracionDeRuta {
  /** El camino, tal como se declara en el enrutador. */
  readonly ruta: string;
  /**
   * Quién es el dueño de la pantalla, y **dónde tiene que vivir su módulo**: bajo
   * `@/features/<duenio>/` si es de una funcionalidad, bajo `@/app/` si es del armazón.
   *
   * No es documentación: `ElBarridoDeRutas` saca del `cargar` la ruta del módulo que importa y la
   * compara con esto. Mover una pantalla del armazón a dentro de una funcionalidad —o al revés—
   * sin cambiar aquí quién manda pone el barrido en rojo. Con cambiarlo, deja de ser un descuido y
   * pasa a ser una decisión, que es justo lo que una declaración sirve para distinguir.
   */
  readonly duenio: Duenio;
  /**
   * La CLAVE del título, no el título. Se traduce al pintar, y eso es lo que hace que cambiar de
   * idioma repinte la cabecera, el `<title>` y el anuncio sin recargar la página.
   *
   * Es `keyof Diccionario['rutas']` y no `string` a propósito: así una ruta nueva no puede traer
   * aquí una frase escrita a mano —no compilaría— ni una clave que no exista en el diccionario.
   * El título ya traducido se usa en TRES sitios —`<title>`, el `<h1>` del `<main>` y el mensaje
   * que se anuncia al navegar— y por eso es uno solo: repetir título en dos vistas deja inservible
   * el anuncio que lo usa (`ux-ipo`, SPA, punto 2).
   */
  readonly claveDeTitulo: ClaveDeTitulo;
  readonly exigencia: Exigencia;
  /** Si sale en la navegación principal. Una ruta puede existir sin ser un enlace visible. */
  readonly enLaNavegacion: boolean;
  readonly cargar: CargaDePagina;
}

export const RUTAS: readonly DeclaracionDeRuta[] = [
  {
    ruta: '/acceso',
    duenio: 'identidad',
    claveDeTitulo: 'acceso',
    exigencia: {
      clase: 'publica',
      motivo: 'Es la puerta. Exigir sesión para poder abrirla no dejaría entrar a nadie.',
    },
    enLaNavegacion: false,
    cargar: async () =>
      (await import('@/features/identidad/acceso/ui/PaginaDeAcceso.tsx')).PaginaDeAcceso,
  },
  {
    ruta: '/',
    duenio: 'armazon',
    claveDeTitulo: 'inicio',
    exigencia: {
      clase: 'sesion',
      motivo:
        'Solo dice quién ha entrado y con qué empresa está operando. No enseña ningún dato de ' +
        'negocio, así que no hay permiso que pedir; lo que sí hace falta es haber entrado.',
    },
    enLaNavegacion: true,
    cargar: async () => (await import('@/app/paginas/PaginaDeInicio.tsx')).PaginaDeInicio,
  },
  {
    ruta: '/almacenes',
    duenio: 'organizacion',
    claveDeTitulo: 'almacenes',
    exigencia: { clase: 'permiso', permiso: PERMISOS.almacenVer },
    enLaNavegacion: true,
    cargar: async () =>
      (await import('@/features/organizacion/almacenes/ui/PaginaDeAlmacenes.tsx'))
        .PaginaDeAlmacenes,
  },
  {
    ruta: '/empresas',
    duenio: 'organizacion',
    claveDeTitulo: 'empresas',
    exigencia: { clase: 'permiso', permiso: PERMISOS.empresaVer },
    enLaNavegacion: true,
    cargar: async () =>
      (await import('@/features/organizacion/empresas/ui/PaginaDeEmpresas.tsx')).PaginaDeEmpresas,
  },
  {
    ruta: '*',
    duenio: 'armazon',
    claveDeTitulo: 'noEncontrada',
    exigencia: {
      clase: 'publica',
      motivo:
        'Una ruta que no existe no puede exigir un permiso que tampoco existe. Y responder «no ' +
        'autorizado» a una URL mal escrita mandaría a buscar un permiso en vez de una errata.',
    },
    enLaNavegacion: false,
    cargar: async () => (await import('@/app/paginas/PaginaNoEncontrada.tsx')).PaginaNoEncontrada,
  },
];

/** Las que se pintan en la navegación, filtradas además por lo que la sesión permite ver. */
export function navegablesPara(permisos: readonly string[]): readonly DeclaracionDeRuta[] {
  return RUTAS.filter(
    (ruta) =>
      ruta.enLaNavegacion &&
      (ruta.exigencia.clase !== 'permiso' || permisos.includes(ruta.exigencia.permiso)),
  );
}
