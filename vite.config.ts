import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  root: path.resolve(__dirname, 'www/src'),
  publicDir: path.resolve(__dirname, 'www/images'),
  build: {
    outDir: path.resolve(__dirname, 'www/dist'),
    emptyOutDir: true,
  },
  plugins: [vue()],
})
