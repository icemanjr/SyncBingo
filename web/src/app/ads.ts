/**
 * Which ads the banner slots show (FR-52, ADR 0012), from the build's settings: placeholders unless the
 * build sets `VITE_AD_PROVIDER=adsense` and every AdSense id, so a half-configured build never loads a
 * third-party script. Read when used, so tests can stub them.
 */
export type AdConfig =
  | { provider: 'placeholder' }
  | { provider: 'adsense'; client: string; bannerSlot: string; towerSlot: string }

export function adConfig(): AdConfig {
  const env = import.meta.env
  const client = env.VITE_ADSENSE_CLIENT
  const bannerSlot = env.VITE_ADSENSE_SLOT_BANNER
  const towerSlot = env.VITE_ADSENSE_SLOT_TOWER
  if (env.VITE_AD_PROVIDER !== 'adsense' || !client || !bannerSlot || !towerSlot)
    return { provider: 'placeholder' }
  return { provider: 'adsense', client, bannerSlot, towerSlot }
}

const scriptId = 'adsense'

/** Loads AdSense's script once, when the first ad appears (so never on the TV display, which has none). */
export function loadAdSense(client: string): void {
  if (document.getElementById(scriptId)) return
  const script = document.createElement('script')
  script.id = scriptId
  script.async = true
  script.crossOrigin = 'anonymous'
  script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${client}`
  document.head.append(script)
}

/**
 * Opens Google's consent message again, so a visitor in the EU, the UK or Switzerland can change their
 * choice (the footer's "Privacy and cookie settings"). Google's own floating button would sit over the page.
 */
export function showConsentSettings(): void {
  const fc = (window.googlefc ??= {})
  ;(fc.callbackQueue ??= []).push(() => window.googlefc?.showRevocationMessage?.())
}
