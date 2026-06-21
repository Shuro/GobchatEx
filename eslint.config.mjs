// ESLint for the GobchatEx TypeScript UI (src/Gobchat.App/resources/ui).
//
// A *behavioural* linter, not a style/formatting one. `tsc --noEmit` already enforces strict types, so this
// only adds a small, hand-picked set of bug-catching checks tsc doesn't. It deliberately does NOT use
// typescript-eslint's full `recommended` preset: that flags this fork's established conventions (the
// `module X {}` namespaces used by CssClass/Databinding/Locale, intentional `{}`/`any` slots) as errors,
// which is noise, not signal. Instead it starts from the parser-only `base` config and enables just the
// rules below — all as warnings, so the large pre-existing legacy backlog never blocks CI; promote
// individual rules to `error` as the code is cleaned up.
//
// Lives at the repo root because an ESLint flat config can only lint files at or below its own directory,
// and the sources sit under src/ (Vitest's own Node project stays in tests/ui). Run via `npm run lint`.
// Scope: `.ts` under resources/ui; the emitted `.js` (gitignored), the legacy `gobchat/*.js` layer, and
// `.d.ts` declarations are not linted.
//
// Follow-up: type-aware rules (e.g. @typescript-eslint/no-floating-promises) need typescript-eslint's
// `projectService` pointed at resources/ui/tsconfig.json; left as a deliberate next step.
import tseslint from 'typescript-eslint'

export default tseslint.config(
    {
        ignores: ['**/*.js', '**/*.mjs', '**/*.cjs', '**/*.d.ts', '**/node_modules/**'],
    },
    {
        files: ['src/Gobchat.App/resources/ui/**/*.ts'],
        extends: [tseslint.configs.base],
        rules: {
            eqeqeq: ['warn', 'smart'],
            'no-var': 'warn',
            'prefer-const': 'warn',
            'no-fallthrough': 'warn',
            'no-constant-condition': ['warn', { checkLoops: false }],
            '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
        },
    },
)
