import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Self-hosted, so no request leaves for a font service; browsers fetch only the Latin files they need.
import '@fontsource-variable/figtree'
import '@fontsource-variable/bricolage-grotesque'
import './index.css'
import App from './App.tsx'
import { applyTheme, loadThemePreference } from './app/theme'

// Before the first render, so a dark-mode page never flashes light.
applyTheme(loadThemePreference())

const root = document.getElementById('root')
if (!root) throw new Error('Missing #root element')

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
