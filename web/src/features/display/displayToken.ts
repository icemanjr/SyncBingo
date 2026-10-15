const key = (code: string) => `syncbingo:display:${code}`

/**
 * The display token arrives in the URL fragment (`#t=…`), which browsers never send to a server. It is kept
 * for this tab only (sessionStorage) and cleared from the address bar, so it isn't left in history or in a
 * screenshot of the URL (docs/04 §5).
 */
export function takeDisplayToken(code: string): string | undefined {
  const fromLink = new URLSearchParams(window.location.hash.slice(1)).get('t')
  try {
    if (fromLink) {
      sessionStorage.setItem(key(code), fromLink)
      history.replaceState(null, '', window.location.pathname + window.location.search)
      return fromLink
    }

    return sessionStorage.getItem(key(code)) ?? undefined
  } catch {
    return fromLink ?? undefined
  }
}

export function forgetDisplayToken(code: string): void {
  try {
    sessionStorage.removeItem(key(code))
  } catch {
    // Nothing to forget.
  }
}
