import type { Schemas } from '../../api/client'
import { useAction } from '../game/useAction'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

/** Wins per player (FR-41); the host can show it on every screen, e.g. to announce the biggest winners. */
export function LeaderboardPanel({
  entries,
  shown,
  host,
}: {
  entries: Schemas['LeaderboardEntry'][]
  shown: boolean
  host: HostApi
}) {
  const { busy, error, run } = useAction()
  return (
    <Panel title="Leaderboard">
      {entries.length === 0 ? (
        <p className="text-ink-muted">No wins yet.</p>
      ) : (
        <ol className="mb-3 flex flex-col gap-1">
          {entries.map((entry, i) => (
            <li key={entry.participantId} className="flex justify-between">
              <span>
                {i + 1}. {entry.name}
              </span>
              <span className="font-bold tabular-nums">
                {entry.wins} win{entry.wins === 1 ? '' : 's'}
              </span>
            </li>
          ))}
        </ol>
      )}
      <Button
        look="secondary"
        disabled={busy}
        onClick={() => void run(() => host.showLeaderboard(!shown))}
      >
        {shown ? 'Hide from everyone' : 'Show to everyone'}
      </Button>
      {error && <ErrorText>{error}</ErrorText>}
    </Panel>
  )
}
