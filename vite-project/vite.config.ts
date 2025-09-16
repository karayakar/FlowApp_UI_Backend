import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // /api ile başlayan istekleri .NET backend’e yönlendir
      '/api': {
        target: 'http://localhost:53750',
        changeOrigin: true,
        // gerekiyorsa:
        // secure: false,
      },
    },
  },
})