/** How this browser takes part in a game: as its host or as a player. */
export type Seat =
  | { role: 'host'; token: string }
  | { role: 'player'; token: string; participantId: string; name: string }

const key = (code: string) => `syncbingo:${code}`

/**
 * Keeps a game's token so a sleeping phone or a reload can carry on (ADR 0005). Storage can be missing
 * or blocked (private windows, strict settings), so failures are swallowed and the game simply can't resume.
 */
export const seats = {
  save(code: string, seat: Seat): void {
    try {
      localStorage.setItem(key(code), JSON.stringify(seat))
    } catch {
      // Not saved: the game works, but won't survive a reload.
    }
  },

  load(code: string): Seat | undefined {
    try {
      const saved = localStorage.getItem(key(code))
      return saved ? (JSON.parse(saved) as Seat) : undefined
    } catch {
      return undefined
    }
  },

  /** The games this browser is hosting, e.g. to connect a TV from the TV's QR (ADR 0010). */
  hostedGames(): string[] {
    try {
      const codes: string[] = []
      for (let i = 0; i < localStorage.length; i++) {
        const name = localStorage.key(i)
        if (!name?.startsWith('syncbingo:')) continue
        const code = name.slice('syncbingo:'.length)
        if (seats.load(code)?.role === 'host') codes.push(code)
      }

      return codes
    } catch {
      return []
    }
  },

  forget(code: string): void {
    try {
      localStorage.removeItem(key(code))
    } catch {
      // Nothing to forget.
    }
  },
}
