import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { patternNames, type PatternData } from '../game/patterns'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

type Claim = Schemas['Claim']
interface Detection {
  participantId: string
  name: string
  winConditionId: string
  matches: number
  completed?: string[] | null
}

/**
 * Every Bingo press, valid or not (FR-37a), and every detected win (FR-19), for the host to decide.
 * Award buttons follow *Multiple wins* (FR-43, docs/06): "Award 1" … "Award n" when the player may be
 * credited several lines; awards send the claim's id, so a double tap counts once. A card that already won
 * (in the "once" modes) gets no Award button; a Bingo not on called numbers asks first, since the host has
 * the last word (ADR 0008) but may have tapped by mistake.
 */
export function ClaimsPanel({
  claims,
  detections,
  everyDeclaredWin,
  patterns,
  host,
  onInspect,
  onDismissDetection,
}: {
  claims: Claim[]
  detections: { at: number; data: Detection }[]
  everyDeclaredWin: boolean
  /** To name the patterns a card completes ("Rows, Four Corners"); only the host sees these. */
  patterns: PatternData
  host: HostApi
  onInspect: (participantId: string) => void
  onDismissDetection: (at: number) => void
}) {
  const { busy, error, run } = useAction()
  const [confirming, setConfirming] = useState<{ claimId: number; wins: number }>()
  if (claims.length === 0 && detections.length === 0) return null

  const award = (claim: Claim, wins: number) =>
    host.award(claim.participantId, claim.id, everyDeclaredWin ? wins : undefined)

  return (
    <section aria-label="Bingo!" className="flex flex-col gap-3">
      <h2 className="sr-only">Bingo!</h2>
      <ul className="flex flex-col gap-3">
        {claims.map((claim) => (
          <li
            key={claim.id}
            className="flex flex-col gap-3 rounded-[20px] border-2 border-notice-line bg-notice p-4 text-ink"
          >
            <p className="flex items-start gap-3 font-semibold">
              <span
                aria-hidden="true"
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-ink text-ball"
              >
                <svg
                  width="18"
                  height="18"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2.2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
                  <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
                </svg>
              </span>
              <span>
                {claim.name} pressed Bingo ·{' '}
                {claim.matches > 0
                  ? `✓ ${String(claim.matches)} line${claim.matches > 1 ? 's' : ''}: ${patternNames(claim.completed, patterns)}`
                  : '✗ not on called numbers'}
                {claim.alreadyWon
                  ? ' · already won, nothing more to award'
                  : claim.awardable === 0 && claim.matches > 0 && ' · no awardable matches'}
              </span>
            </p>
            <div className="flex flex-wrap gap-2 [&>button]:flex-1 [&>button]:px-3">
              <Button
                look="secondary"
                onClick={() => {
                  onInspect(claim.participantId)
                }}
              >
                Inspect
              </Button>
              {!claim.alreadyWon &&
                confirming?.claimId !== claim.id &&
                (everyDeclaredWin
                  ? Array.from({ length: Math.max(1, claim.awardable) }, (_, i) => i + 1)
                  : [1]
                ).map((wins) => (
                  <Button
                    key={wins}
                    disabled={busy}
                    onClick={() => {
                      if (claim.matches === 0) {
                        setConfirming({ claimId: claim.id, wins })
                        return
                      }

                      void run(() => award(claim, wins))
                    }}
                  >
                    {everyDeclaredWin ? `Award ${String(wins)}` : 'Award'}
                  </Button>
                ))}
              <Button
                look="quiet"
                disabled={busy}
                onClick={() => void run(() => host.dismiss(claim.id))}
              >
                Dismiss
              </Button>
            </div>
            {confirming?.claimId === claim.id && (
              <div role="alertdialog" aria-label="Confirm award" className="flex flex-col gap-2">
                <p>Award a Bingo that isn&apos;t on the called numbers?</p>
                <div className="flex flex-wrap gap-2">
                  <Button
                    disabled={busy}
                    onClick={() => {
                      const { wins } = confirming
                      setConfirming(undefined)
                      void run(() => award(claim, wins))
                    }}
                  >
                    Award anyway
                  </Button>
                  <Button
                    look="secondary"
                    onClick={() => {
                      setConfirming(undefined)
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </li>
        ))}
        {detections.map(({ at, data }) => (
          <li key={at} className="flex flex-col gap-3 rounded-[20px] bg-info p-4 shadow-card">
            <p className="font-semibold">
              Detected: {data.name} has {patternNames(data.completed, patterns)} ({data.matches}{' '}
              line
              {data.matches > 1 ? 's' : ''}) on called numbers
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                look="secondary"
                onClick={() => {
                  onInspect(data.participantId)
                }}
              >
                Inspect
              </Button>
              <Button
                disabled={busy}
                onClick={() =>
                  void run(() => host.award(data.participantId)).then(() => {
                    onDismissDetection(at)
                  })
                }
              >
                Award
              </Button>
              <Button
                look="quiet"
                onClick={() => {
                  onDismissDetection(at)
                }}
              >
                Dismiss
              </Button>
            </div>
          </li>
        ))}
      </ul>
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}
