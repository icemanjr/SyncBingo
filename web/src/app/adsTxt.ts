/**
 * The AdSense site files, written by the build (vite.config.ts) when `VITE_ADSENSE_CLIENT` is set (ADR 0012).
 * Kept free of browser code, because the build's Node config imports it too.
 */

/** An AdSense publisher id as the ad code takes it, e.g. `ca-pub-1234567890123456`. */
export const adSenseClient = /^ca-pub-\d{16}$/

/**
 * The site's `ads.txt` line, which tells ad buyers that this publisher may sell the site's ad space
 * (`f08c47fec0942fa0` is Google's own certification id, the same for every AdSense site).
 */
export function adsTxt(client: string): string {
  if (!adSenseClient.test(client)) throw new Error(`Not an AdSense publisher id: ${client}`)
  return `google.com, ${client.slice('ca-'.length)}, DIRECT, f08c47fec0942fa0\n`
}
