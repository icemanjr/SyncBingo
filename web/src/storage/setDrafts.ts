import type { Draft } from '../features/sets/draft'

/** A set being edited, with its Group items choice: kept per game so a reload or a sleeping laptop loses nothing. */
export interface StoredDraft {
  draft: Draft
  groupItems: boolean
}

const key = (code: string) => `syncbingo:set-draft:${code}`

/** Drafts live in this browser only; storage can be missing or full, so failures are swallowed. */
export const setDrafts = {
  load(code: string): StoredDraft | undefined {
    try {
      const saved = localStorage.getItem(key(code))
      return saved ? (JSON.parse(saved) as StoredDraft) : undefined
    } catch {
      return undefined
    }
  },

  save(code: string, stored: StoredDraft): void {
    try {
      localStorage.setItem(key(code), JSON.stringify(stored))
    } catch {
      // Not saved: the editor works, but a reload starts from the set in use.
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
