import type { Schemas } from '../api/client'
import type { Draft } from '../features/sets/draft'

/** A set being edited, with its Use groups choice: kept per game so a reload or a sleeping laptop loses nothing. */
export interface StoredDraft {
  draft: Draft
  useGroups: boolean
  /** The card size chosen in the editor, sent with the set (FR-26a); absent: the game's. */
  cardSize?: Schemas['CardSize']
}

const key = (code: string) => `syncbingo:set-draft:${code}`

/** Drafts live in this browser only; storage can be missing or full, so failures are swallowed. */
export const setDrafts = {
  load(code: string): StoredDraft | undefined {
    try {
      const saved = localStorage.getItem(key(code))
      if (!saved) return undefined
      // Drafts saved before 2026-10-22 called the choice groupItems.
      const stored = JSON.parse(saved) as Pick<StoredDraft, 'draft' | 'cardSize'> & {
        useGroups?: boolean
        groupItems?: boolean
      }
      return {
        draft: stored.draft,
        useGroups: stored.useGroups ?? stored.groupItems ?? false,
        cardSize: stored.cardSize,
      }
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
