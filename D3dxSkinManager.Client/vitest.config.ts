import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import viteTsconfigPaths from 'vite-tsconfig-paths';
import path from 'path';

// Vitest runner config — reuses Vite's esbuild/TSX/ESM pipeline so antd 6 + lodash-es + the `@/`
// alias resolve the same as the app (jest fights antd's ESM; vitest doesn't). jsdom + jest-compatible
// `vi` globals so the existing @testing-library tests run with minimal changes.
export default defineConfig({
  plugins: [react(), viteTsconfigPaths()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    css: false,
  },
});
