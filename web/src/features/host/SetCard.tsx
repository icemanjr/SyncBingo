import { useState } from 'react'
import { Link } from 'react-router'
import type { Schemas } from '../../api/client'
import { downloadSet } from '../game/downloadSet'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

/**
 * The custom set in use, in the host's lobby (ADR 0009): its name, a download to keep it, and whether players
 * may download it too (off by default; for a teacher sharing a set with another who joined as a player).
 * Players can download only between rounds, so the file, which pairs every clue with its word, is never at
 * hand during one.
 */
export function SetCard({
  code,
  token,
  content,
  host,
}: {
  code: string
  token: string
  content: Schemas['ContentView']
  host: HostApi
}) {
  const { busy, error, run } = useAction()
  // The host's choice shows at once; the server's value takes over when it agrees (or the change fails).
  const [pending, setPending] = useState<boolean>()
  if (pending !== undefined && pending === content.playersMayDownload) setPending(undefined)
  const shared = pending ?? content.playersMayDownload
  return (
    <section
      aria-label="Custom set"
      className="flex flex-col gap-3 rounded-3xl bg-surface p-4 shadow-card"
    >
      <div>
        <p className="text-xs font-bold tracking-widest text-ink-muted uppercase">Custom set</p>
        <p className="font-display text-lg font-bold">{content.name}</p>
        <p className="text-sm text-ink-muted">
          {content.groupItems ? 'Dealt by group, one per column' : 'Dealt anywhere on the card'}
        </p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Link
          to={`/host/${code}/set`}
          className="inline-flex min-h-12 items-center justify-center rounded-2xl bg-accent px-5 font-bold text-accent-ink hover:bg-accent-strong focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Edit set
        </Link>
        <Button
          look="secondary"
          disabled={busy}
          onClick={() =>
            void run(async () => {
              await downloadSet(code, token)
            })
          }
        >
          Download this set
        </Button>
      </div>
      <label className="flex min-h-12 items-start gap-3">
        <input
          type="checkbox"
          checked={shared}
          disabled={busy}
          aria-describedby="share-set-help"
          className="mt-1 h-5 w-5 accent-accent"
          onChange={(e) => {
            const on = e.target.checked
            setPending(on)
            void run(async () => {
              try {
                await host.setContentSharing(on)
              } catch (failed) {
                setPending(undefined)
                throw failed
              }
            })
          }}
        />
        <span>
          <span className="font-semibold">Players can download this set</span>
          <span id="share-set-help" className="block text-sm text-ink-muted">
            Between rounds only. The file pairs every clue with its answer, so leave this off for
            players who shouldn&apos;t see it.
          </span>
        </span>
      </label>
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}

/** In the lobby while the game uses numbers: the way into the set editor (FR-44). */
export function NumbersCard({ code }: { code: string }) {
  return (
    <section
      aria-label="Custom set"
      className="flex flex-col gap-2 rounded-3xl bg-surface p-4 shadow-card"
    >
      <p className="text-xs font-bold tracking-widest text-ink-muted uppercase">Balls</p>
      <p className="font-display text-lg font-bold">Numbers 1–75</p>
      <p className="text-sm text-ink-muted">
        Or play with your own words, sums or clues: a custom set.
      </p>
      <Link
        to={`/host/${code}/set`}
        className="inline-flex min-h-12 items-center justify-center rounded-2xl bg-accent-soft px-5 font-bold text-accent-text hover:bg-accent-soft/70 focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        Use a custom set
      </Link>
    </section>
  )
}
