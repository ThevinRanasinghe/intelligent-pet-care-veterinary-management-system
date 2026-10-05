/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/tests/setup.ts'],
    css: true,
    // Multi-step userEvent flows (e.g. RegisterPage.location) can exceed the
    // 5s default when test files run in parallel under CPU contention.
    testTimeout: 20000,
  },
});
