import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import viteTsconfigPaths from 'vite-tsconfig-paths';
import checker from 'vite-plugin-checker';
import path from 'path';

// https://vitejs.dev/config/
export default defineConfig({
  // Use virtual host for production builds (WebView2 SetVirtualHostNameToFolderMapping)
  // This makes all asset paths resolve correctly when served from embedded resources
  base: process.env.NODE_ENV === 'production' ? 'https://app.local/' : '/',
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
    port: 3000,
    open: false,
  },
  build: {
    outDir: 'build',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router-dom'],
          antd: ['antd'],
          i18n: ['i18next', 'react-i18next', 'i18next-http-backend'],
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
