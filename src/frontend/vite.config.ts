import path from 'path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      // Proxy API calls to the app service
      '/api': {
        target: process.env.services__api__https__0 || process.env.services__api__http__0,
        changeOrigin: true,
        secure: false
      }
    }
  }
});
