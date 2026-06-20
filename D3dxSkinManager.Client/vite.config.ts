import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import viteTsconfigPaths from 'vite-tsconfig-paths';
import checker from 'vite-plugin-checker';
import path from 'path';

// https://vitejs.dev/config/
export default defineConfig({
  base: 'https://app.local/',
  plugins: [
    react(),
    viteTsconfigPaths(),
    checker({
      typescript: true,
      overlay: {
        initialIsOpen: false,
        position: 'br',
      },
      enableBuild: false, // Only check during dev, not build (build already checks)
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    // Unique dev port (NOT the common 3000) so this app's dev server doesn't collide with other
    // local WebView2/React apps (e.g. SiblingApp, also on 3000). The backend navigates to this exact port
    // in dev mode (WebViewInitializer / SecondaryWindowService) — keep all three in sync.
    // strictPort: fail loudly instead of drifting to 3518+ (which the hardcoded nav wouldn't find).
    port: 3517,
    strictPort: true,
    open: false,
    // Allow the dev server to read the sibling backend Languages/*.json so the DEV fake-bridge can
    // serve real translations in pure-UI (plain-Chrome) mode. '..' = the repo root (parent of Client).
    fs: { allow: ['..'] },
  },
  build: {
    outDir: 'build',
    sourcemap: false, // Disable source maps for faster parsing in production
    minify: 'esbuild', // Use esbuild (default, faster than terser)
    target: 'esnext', // Modern browsers only - smaller bundle
    rollupOptions: {
      input: {
        main: path.resolve(__dirname, 'index.html'),
        capture: path.resolve(__dirname, 'capture.html'),
      },
      output: {
        // Stable vendor chunks (separate from app code) so they keep their content hash across app
        // updates → WebView2's HTTP cache + V8 IsolatedCodeCache reuse them without re-download/re-parse.
        // NOTE: antd (~1.2MB) is used by the eager "mods" tab + app shell, so it cannot be deferred —
        // measured auto-splitting only saved ~60KB of it while folding it into `main` (which re-hashes
        // every release, defeating the code cache). Keep it pinned. The win comes from lazy-loading the
        // non-default tabs/dialogs (App.tsx), which removes THEIR code from the initial parse.
        manualChunks: {
          'antd': ['antd'],
          'react-vendor': ['react', 'react-dom'],
          'i18n': ['i18next', 'react-i18next'],
        },
      },
    },
  },
  css: {
    modules: {
      localsConvention: 'camelCase',
    },
  },
});
