import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// One IIFE script and one stylesheet for the static site (no build step there)
export default defineConfig({
  plugins: [react()],
  define: { 'process.env.NODE_ENV': '"production"' },
  build: {
    outDir: '../../docs/assets',
    emptyOutDir: false,
    lib: { entry: 'main.tsx', formats: ['iife'], name: 'WeekmarkHero', fileName: () => 'hero.js', cssFileName: 'hero' },
  },
})
