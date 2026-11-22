import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': 'http://localhost:5020', '/swagger': 'http://localhost:5020' },
  },
  build: {
    outDir: '../backend/src/StudyHub.Api/wwwroot',
    emptyOutDir: true,
  },
})
