import { readdirSync } from 'node:fs';

import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import jsxA11y from 'eslint-plugin-jsx-a11y-x';
import i18next from 'eslint-plugin-i18next';
import prettier from 'eslint-config-prettier';

// LAS FUNCIONALIDADES, DESCUBIERTAS DEL DISCO.
//
// Se leen las carpetas de `src/features/` en vez de escribirlas aquí para que una funcionalidad
// nueva quede vallada por existir, y no por que alguien se acuerde de venir a este fichero. Lo que
// alguien AFIRMA que hay está en `src/features/funcionalidades.ts`, y el barrido compara las dos
// listas enteras contra el disco.
const FUNCIONALIDADES = readdirSync(new URL('./src/features', import.meta.url), {
  withFileTypes: true,
})
  .filter((entrada) => entrada.isDirectory())
  .map((entrada) => entrada.name)
  .sort();

// LA AFIRMACIÓN DE CONJUNTO NO VACÍO, Y VA AQUÍ PORQUE AQUÍ ES DONDE PUEDE FALLAR EN SILENCIO.
//
// Una regla de ESLint cuyo patrón no case con nada PASA. Si esta lectura devolviera cero carpetas
// —otro directorio de trabajo, una carpeta renombrada, un `src/features/` que se ha quedado sin
// subcarpetas—, el bucle de abajo generaría cero reglas y `npm run lint` saldría verde sin prohibir
// nada. Con menos de dos funcionalidades no hay ninguna frontera que vigilar, así que si eso pasa
// es que el descubrimiento está roto: mejor reventar la configuración que lintar sin regla.
if (FUNCIONALIDADES.length < 2) {
  throw new Error(
    'La regla de fronteras no ha encontrado al menos dos funcionalidades en src/features/ ' +
      `(encontradas: ${JSON.stringify(FUNCIONALIDADES)}). Sin ellas no prohibiría nada y el lint ` +
      'saldría verde sin comprobar la frontera. Revisa src/features/ y el directorio de trabajo.',
  );
}

/**
 * UNA FUNCIONALIDAD NUNCA IMPORTA DE OTRA — una regla por funcionalidad.
 *
 * El §10 del plan maestro y `stacks/react/convenciones.md`:39 lo mandan; hasta el 0.16 era un
 * acuerdo escrito, que es otra manera de decir que no lo comprobaba nadie. Ahora lo dice ESLint,
 * y `ElBarridoDeLasFronteras` comprueba —lintando de verdad un import prohibido entre cada par—
 * que lo dice **para todas** las funcionalidades que hay en disco.
 *
 * Lo que la regla NO prohíbe, y es a propósito: `@/shared/**` y `@/app/**`. La frontera va de
 * funcionalidad a funcionalidad. `organizacion` necesita saber con qué empresa se opera y eso vive
 * en `shared/sesion/`; si la regla obligara a bajar la sesión dentro de `identidad`, la regla
 * estaría mal, no la estructura.
 *
 * Dos patrones por funcionalidad prohibida, y los dos hacen falta —se ha comprobado quitando cada
 * uno y viendo cuál se queda corto—:
 *
 * - El del alias. Los patrones se leen como los de `.gitignore`, así que `@/features/otra` a secas
 *   ya cubre todo lo que cuelga: no hace falta escribir la cola.
 * - El ancho, el que solo pide que el nombre aparezca como carpeta a cualquier profundidad. Es el
 *   que atrapa el camino relativo que sube y vuelve a bajar (`../../../otra/loQueSea.ts`), donde la
 *   palabra `features` ni siquiera aparece. Sin él, esa forma pasa: el lint sale verde y la
 *   frontera no existe.
 *
 * El precio del ancho es que una carpeta de `shared/` o de `app/` que se llamara igual que una
 * funcionalidad quedaría prohibida sin querer. Que no exista ninguna es una de las cosas que
 * comprueba el barrido, así que el precio está vigilado.
 */
const fronterasEntreFuncionalidades = FUNCIONALIDADES.map((funcionalidad) => ({
  files: [`src/features/${funcionalidad}/**/*.{ts,tsx}`],
  rules: {
    'no-restricted-imports': [
      'error',
      {
        patterns: FUNCIONALIDADES.filter((otra) => otra !== funcionalidad).map((otra) => ({
          group: [`@/features/${otra}`, `**/${otra}/**`],
          message:
            `Una funcionalidad no importa de otra: '${funcionalidad}' no puede usar ` +
            `'${otra}'. Si las dos necesitan lo mismo, sube ESO a shared/ —no la pantalla—, o ` +
            'pregúntate si de verdad son dos módulos distintos (docs/adr/adr-0022).',
        })),
      },
    ],
  },
}));

/**
 * LAS TRES FORMAS DE LEER EL RELOJ QUE TIENE `Date`: construirlo sin argumentos, llamarlo como
 * función y su `now`, llamado o pasado como función. Lo que construye una fecha a partir de algo
 * —una cadena, un número, `Date.UTC`— no lee nada, y pasa.
 */
const MENSAJE_DEL_RELOJ =
  'El reloj solo se lee en src/shared/lib/dias.ts. Para el día de hoy, hoyEnElServidor(); un ' +
  'segundo «hoy» volvería a separar la etiqueta de una tarifa de su precio (ítem 2.16).';
const LECTURAS_DEL_RELOJ = [
  "NewExpression[callee.name='Date'][arguments.length=0]",
  "CallExpression[callee.name='Date']",
  "MemberExpression[object.name='Date'][property.name='now']",
].map((selector) => ({ selector, message: MENSAJE_DEL_RELOJ }));

export default tseslint.config(
  // `src/shared/api/esquema.ts` se GENERA con `npm run api` desde `docs/api/openapi.json`.
  // No se linta lo que no se escribe: las reglas de estilo se aplican a las decisiones de
  // quien escribe, y aqui no hay ninguna que corregir — habria que corregir el generador.
  {
    ignores: ['dist', 'coverage', 'node_modules', '*.tsbuildinfo', 'src/shared/api/esquema.ts'],
  },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      // v7: la config PLANA vive bajo `configs.flat`; `configs['recommended-latest']`
      // a secas sigue siendo la de eslintrc y ESLint 9 la rechaza.
      reactHooks.configs.flat['recommended-latest'],
      // El bifurcado de es-tooling y no el original, que no admite ESLint 10 y no se publica desde
      // 2024. Trae las mismas 33 reglas de `strict`, con los mismos niveles y opciones; solo cambia
      // el prefijo, `jsx-a11y-x/`, y su config plana vive en `configs`.
      jsxA11y.configs.strict,
      // Prettier va SIEMPRE el último: apaga las reglas de formato para que no se peleen.
      prettier,
    ],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
      parserOptions: {
        // Un fichero que no esté en ninguno de los tres no se puede analizar con tipos, y el lint
        // falla. Es lo que avisa si un test se queda fuera de `tsconfig.test.json`.
        project: ['./tsconfig.app.json', './tsconfig.test.json', './tsconfig.node.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      'react-refresh': reactRefresh,
      i18next,
    },
    rules: {
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      // `stacks/typescript/convenciones.md`: prohibido `any`, `unknown` + estrechamiento.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/explicit-function-return-type': [
        'error',
        { allowExpressions: true, allowTypedFunctionExpressions: true },
      ],
      // Interfaces SIN prefijo `I` (convención TS, al revés que C#).
      '@typescript-eslint/naming-convention': [
        'error',
        {
          selector: 'interface',
          format: ['PascalCase'],
          custom: { regex: '^I[A-Z]', match: false },
        },
        { selector: 'typeLike', format: ['PascalCase'] },
        { selector: 'variable', format: ['camelCase', 'PascalCase', 'UPPER_CASE'] },
        { selector: 'function', format: ['camelCase', 'PascalCase'] },
      ],
      // `import type` explícito: lo que solo son tipos no debe acabar en el bundle.
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],

      // NINGÚN TEXTO VISIBLE ESCRITO EN UN COMPONENTE (ítem 0.14).
      //
      // `eslint-plugin-i18next` lleva licencia **ISC** (permisiva), comprobada antes de
      // adoptarlo. Anotada aquí y no en el mensaje del commit: el dato tiene que estar donde
      // se lee la decisión, no en un historial al que hay que saber preguntar.
      //
      // El §3 del plan maestro manda los textos fuera del codigo «desde el primer día», y la
      // biblioteca cuenta «literales de texto sin i18n» entre sus antipatrones. Que eso sea una
      // regla que se ejecuta y no un acuerdo escrito es la diferencia entre cumplirlo y creer que
      // se cumple: la fase 1 trae decenas de pantallas nuevas, y basta que a UNA se le olvide para
      // que la aplicación quede medio traducida.
      //
      // Cubre el texto de JSX y los atributos que LEE una persona. `className`, `id`, `to`, `type`
      // y compañía quedan fuera adrede: no son texto, y meterlos obligaría a una excepcion por
      // linea que acabaría apagando la regla de hecho.
      'i18next/no-literal-string': [
        'error',
        {
          mode: 'jsx-text-only',
          'should-validate-template': true,
          message: 'Texto suelto en un componente: llévalo al diccionario de app/i18n y usa t().',
          callees: { exclude: ['t', 'i18n.t'] },
          words: {
            // Lo que no es una frase: símbolos sueltos, separadores y signos de puntuacion.
            // Y la MARCA: «Bastion» se escribe igual en los dos idiomas (glosario, Anexo
            // A.1). Meterla en el diccionario sería invitar a que alguien la traduzca.
            exclude: ['^[^a-zA-Z\u00C0-\u017F]+$', '^Bastion$'],
          },
        },
      ],
    },
  },
  {
    // El diccionario ES el texto: prohibirle literales sería prohibirle existir. Y los tests
    // comprueban texto que sale en pantalla, así que tienen que poder escribirlo.
    files: ['src/app/i18n/**/*.ts', '**/*.test.{ts,tsx}', 'src/pruebas/**/*.{ts,tsx}'],
    rules: {
      'i18next/no-literal-string': 'off',
    },
  },
  {
    // Los tests pueden usar aserciones no nulas: el fallo del test ES el diagnóstico.
    files: ['**/*.test.{ts,tsx}', '**/setupTests.ts'],
    rules: {
      '@typescript-eslint/no-non-null-assertion': 'off',
      '@typescript-eslint/no-unsafe-assignment': 'off',
    },
  },
  {
    // LOS DICCIONARIOS SE IMPORTAN POR SU TIPO (ADR-0063 §9). Por su valor solo los trae
    // `diccionarios.ts`, con `import()`, que es lo que los deja en un fragmento por idioma. Un
    // import estático de valor desde cualquier otro sitio ata ese fragmento a quien lo hace: si es
    // algo del arranque, desaparece dentro del de entrada; si es una página, se descarga con ella
    // y en el idioma que sea. Las dos cosas pasaron: la primera la midió el ADR-0036, y la segunda
    // estuvo en `useTextoDeFallo` hasta el 2.14, con el diccionario castellano dentro de once
    // páginas. El presupuesto ve la primera —el fragmento deja de existir—, pero no la segunda,
    // que no cambia ningún tamaño: por eso la regla.
    //
    // Es la regla de `typescript-eslint` y no la del núcleo, por dos motivos: la del núcleo no
    // sabe dejar pasar `import type`, y es la que llevan las fronteras entre funcionalidades, que
    // van al final y le sustituirían las opciones a todo fichero de una funcionalidad. Con dos
    // reglas, cada una tiene las suyas. `ElDiccionarioSeImportaPorSuTipo` comprueba que marca lo
    // que tiene que marcar y deja pasar lo que no.
    //
    // Los tests y `src/pruebas/` quedan fuera: no se empaquetan, y el i18n de prueba necesita los
    // diccionarios por valor para montar sin esperar.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/app/i18n/diccionarios.ts', '**/*.test.{ts,tsx}', 'src/pruebas/**'],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              // `…/i18n/es.ts` con alias o con camino relativo, y `./es.ts` desde la propia
              // carpeta del i18n. Lo mismo con `en`, con los textos de los `type` y, desde el
              // 3.1, con los de cada funcionalidad (`inventario.es.ts`, ADR-0071): el prefijo es
              // cualquier nombre, para que una funcionalidad nueva no tenga que acordarse de él.
              regex: '^(?:.*/i18n/|\\./)(?:[a-zA-Z]+\\.)?[a-z]{2}\\.ts$',
              allowTypeImports: true,
              message:
                'Un diccionario se importa por su tipo (`import type`). Por su valor solo lo trae ' +
                'src/app/i18n/diccionarios.ts, con import(): un import estático lo ata a este ' +
                'fragmento y lo descarga en el idioma que sea (docs/adr/adr-0063, §9).',
            },
          ],
        },
      ],
    },
  },
  {
    // EL RELOJ SOLO SE LEE EN `shared/lib/dias.ts` (encargo del 2026-10-18, respuesta 7).
    //
    // El 2.16 dejó un solo «hoy» en el frontal, `hoyEnElServidor`, y es el único sitio que lee el
    // reloj. La regla hace que lo siga siendo: un `new Date()` o un `Date.now()` en una pantalla
    // traería de vuelta el «hoy» del calendario de quien mira, y la etiqueta de una tarifa volvería
    // a decir otra cosa que su precio. `ElRelojSoloSeLeeEnDias` comprueba que marca cada forma
    // desde cada zona, y su pareja: el contenido de verdad de `dias.ts`, lintado desde cualquier
    // otro sitio, sale marcado. Lo que no ve está escrito allí.
    //
    // Alcanza también a los tests y a `src/pruebas/`: un test fija su instante y se lo pasa a quien
    // lo use, no lo lee.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/shared/lib/dias.ts'],
    rules: {
      'no-restricted-syntax': ['error', ...LECTURAS_DEL_RELOJ],
    },
  },
  // Las fronteras van LAS ÚLTIMAS: así ningún bloque posterior las apaga sin querer. Y alcanzan
  // también a los tests que viven dentro de una funcionalidad, que son código de esa funcionalidad
  // y no un sitio donde la frontera se relaje.
  ...fronterasEntreFuncionalidades,
);
