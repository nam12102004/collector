import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    port: 4210,
    strictPort: true,
    proxy: {
      '/auth': { target: 'http://localhost:5200', changeOrigin: true },
      '/api': { target: 'http://localhost:5200', changeOrigin: true },
      '/health': { target: 'http://localhost:5200', changeOrigin: true },
      '/signout-callback-oidc': { target: 'http://localhost:5200', changeOrigin: true },
    },
  },
});
