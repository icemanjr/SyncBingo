/** A game code as typed or linked (any case, hyphens or not) in the form used for storage keys. */
export function normalizeCode(code: string): string {
  return code.replace(/[-\s]/g, '').toLowerCase()
}

/** The display form, `xxxx-xxxx-xxxx-xxxx` (ADR 0006). */
export function displayCode(code: string): string {
  return normalizeCode(code).replace(/(.{4})(?=.)/g, '$1-')
}
