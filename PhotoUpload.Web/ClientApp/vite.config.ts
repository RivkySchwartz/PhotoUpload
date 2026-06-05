import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [
    react(),
    {
      name: 'configure-upload-timeout',
      configureServer(server) {
        // Node.js 18+ defaults requestTimeout to 300s — too short for large RAW uploads.
        // Setting to 0 disables it so uploads never get cut off in dev.
        server.httpServer?.setTimeout(0);
        if (server.httpServer) (server.httpServer as any).requestTimeout = 0;
      },
    },
  ],
  server: {
    port: 3000,
    strictPort : true,
    proxy: {
      '/api' : {
        target: 'http://localhost:5292',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api/, '/api'),
        proxyTimeout: 600000,
        timeout: 600000
      }
    }
  }
})
