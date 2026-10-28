/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "true" shows the Serious Bingo teaser tile (Full profile only; ADR 0011). */
  readonly VITE_SERIOUS_BINGO_TEASER?: string
  /** The person or organization running the site, named on the legal pages (`web/.env`). */
  readonly VITE_OPERATOR_NAME: string
  /** Where legal and privacy questions go. */
  readonly VITE_LEGAL_EMAIL: string
  /** Where people get help with a game. */
  readonly VITE_SUPPORT_EMAIL: string
  /** "adsense" shows real ads (the live site, once approved); anything else shows placeholders (ADR 0012). */
  readonly VITE_AD_PROVIDER?: string
  /** The AdSense publisher id, `ca-pub-` and 16 digits; also writes `ads.txt` and the site-verification tag. */
  readonly VITE_ADSENSE_CLIENT?: string
  /** The AdSense ad unit for the 320 x 50 phone banners. */
  readonly VITE_ADSENSE_SLOT_BANNER?: string
  /** The AdSense ad unit for the 160 x 600 desktop side banners. */
  readonly VITE_ADSENSE_SLOT_TOWER?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

/** AdSense's queue, and Google's consent messages (Privacy & messaging), once their scripts load. */
interface Window {
  adsbygoogle?: object[]
  googlefc?: { callbackQueue?: (() => void)[]; showRevocationMessage?: () => void }
}
