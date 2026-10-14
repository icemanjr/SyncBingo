/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "true" shows the Serious Bingo teaser tile (Full profile only; ADR 0011). */
  readonly VITE_SERIOUS_BINGO_TEASER?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
