import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: process.env.VITE_PORT ? Number(process.env.VITE_PORT) : 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_URL ?? 'http://localhost:5123',
        changeOrigin: true,
      },
    },
  },
})
