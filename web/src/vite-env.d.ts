/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "true" shows the Serious Bingo teaser tile (Full profile only; ADR 0011). */
  readonly VITE_SERIOUS_BINGO_TEASER?: string
  /** The person or organisation running the site, named on the legal pages (`web/.env`). */
  readonly VITE_OPERATOR_NAME: string
  /** Where legal and privacy questions go. */
  readonly VITE_LEGAL_EMAIL: string
  /** Where people get help with a game. */
  readonly VITE_SUPPORT_EMAIL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
