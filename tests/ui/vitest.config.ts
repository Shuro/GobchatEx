import { defineConfig } from 'vitest/config'

// The UI sources live in ../../Gobchat.App/resources/ui and are compiled to .js next to the .ts
// by MSBuild during the app build. Those emitted .js are git-ignored, so on a clean checkout only
// the .ts exist — prefer resolving .ts first so tests always run against source, not stale output.
export default defineConfig({
  test: {
    include: ['**/*.test.ts'],
    environment: 'node',
  },
  resolve: {
    extensions: ['.ts', '.js', '.json'],
  },
})
