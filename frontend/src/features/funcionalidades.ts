/**
 * LAS FUNCIONALIDADES DEL FRONTAL, escritas a mano.
 *
 * Una funcionalidad **es un módulo del backend** (§10 del plan maestro), no un recurso: `almacenes`
 * y `empresas` son dos recursos del mismo módulo y viven los dos dentro de `organizacion/`. Dentro
 * de una funcionalidad no hay fronteras; entre dos, no se importa nada (`docs/adr/adr-0021`).
 *
 * **Por qué está escrita a mano si el disco ya lo dice.** Porque una lista que se descubre sola no
 * puede desmentir al disco: si el barrido se rompe, o alguien renombra una carpeta, la lista
 * descubierta cambia con ella y todo sigue verde sin haber comprobado nada. Esta lista es lo que
 * alguien AFIRMA que hay, y `ElBarridoDeLasFronteras` la compara entera contra `src/features/` en
 * los dos sentidos: una carpeta nueva sin declarar es roja, y una declaración sin carpeta también.
 *
 * `eslint.config.js` no la lee —es JavaScript y no puede importar TypeScript—: descubre las
 * carpetas por su cuenta y genera una regla por funcionalidad. Que las dos cosas coincidan es
 * precisamente lo que el barrido comprueba, y además lo comprueba **por el efecto**: pide a ESLint
 * que linte un import prohibido entre cada par y exige que lo marque.
 */
export const FUNCIONALIDADES = ['identidad', 'organizacion'] as const;

/** Una de las funcionalidades que existen. No es `string`: una errata no compila. */
export type Funcionalidad = (typeof FUNCIONALIDADES)[number];
