import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'
import sonarjs from 'eslint-plugin-sonarjs'

export default [
  { ignores: ['www/dist/**', 'node_modules/**'] },

  // TypeScript recommended rules scoped to .ts files only.
  // Must NOT include .vue here - tseslint.configs.recommended sets the parser
  // globally in its first element, which would shadow vue-eslint-parser.
  ...tseslint.configs.recommended.map(config =>
    config.files ? config : { ...config, files: ['www/src/**/*.ts'] }
  ),

  // Vue 3 essential rules. Sets up vue-eslint-parser for .vue files.
  // Placed after TypeScript so vue-eslint-parser wins for .vue files.
  ...pluginVue.configs['flat/essential'],

  // Wire up typescript-eslint as the <script lang="ts"> sub-parser inside .vue.
  {
    files: ['www/src/**/*.vue'],
    languageOptions: {
      parserOptions: {
        parser: tseslint.parser,
      },
    },
  },

  // Rules applied to all TypeScript/Vue source files
  {
    files: ['www/src/**/*.{ts,vue}'],
    plugins: { sonarjs },
    rules: {
      // Ban window.X member access - use globalThis for lorca bindings.
      // All lorca bindings are declared as globals in www/src/types/bridge.ts,
      // making them accessible via globalThis without losing type safety.
      //
      // Also ban .indexOf() used as an existence check in comparisons.
      // Use .includes() instead (SonarQube S7765). Note: .indexOf() is still
      // allowed when the numeric index value itself is needed (e.g. assignment).
      'no-restricted-syntax': [
        'error',
        {
          selector: 'MemberExpression[object.name="window"]',
          message: 'Use globalThis instead of window for lorca bindings.',
        },
        {
          selector: "BinaryExpression:matches([operator='!=='], [operator='!='], [operator='==='], [operator='=='], [operator='>'], [operator='>='], [operator='<'], [operator='<=']) > CallExpression[callee.property.name='indexOf']",
          message: "Use .includes() instead of .indexOf() when checking for existence (SonarQube S7765).",
        },
      ],

      // Ban bare parseInt/parseFloat - use Number.parseInt/Number.parseFloat.
      // Mirrors SonarQube S7773.
      'no-restricted-globals': [
        'error',
        { name: 'parseInt', message: 'Use Number.parseInt instead.' },
        { name: 'parseFloat', message: 'Use Number.parseFloat instead.' },
      ],

      // Trailing commas on all multiline constructs (functions, objects, arrays,
      // imports). Catches missing trailing commas in function parameter lists.
      'comma-dangle': ['error', 'always-multiline'],

      // Disallow void operator. Mirrors SonarQube S3735.
      'no-void': 'error',

      // Prefer positive conditions over negated ones. Mirrors SonarQube S7735.
      'no-negated-condition': 'warn',

      // Cognitive complexity (mirrors SonarQube S3776, threshold 15)
      'sonarjs/cognitive-complexity': ['warn', 15],

      // Identical function bodies in different branches (SonarQube S1871)
      'sonarjs/no-identical-functions': 'warn',

      // Functions/callbacks nested more than 4 levels deep (SonarQube S2004)
      'sonarjs/no-nested-functions': ['warn', { threshold: 4 }],

      // Nested ternary operations (SonarQube S3358)
      'sonarjs/no-nested-conditional': 'warn',
    },
  },
]
