import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import i18next from 'eslint-plugin-i18next';
import prettier from 'eslint-config-prettier';

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
      jsxA11y.flatConfigs.strict,
      // Prettier va SIEMPRE el último: apaga las reglas de formato para que no se peleen.
      prettier,
    ],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
      parserOptions: {
        project: ['./tsconfig.app.json', './tsconfig.node.json'],
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
);
