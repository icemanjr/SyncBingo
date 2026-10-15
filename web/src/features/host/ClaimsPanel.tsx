import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { useAction } from '../game/useAction'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

type Claim = Schemas['Claim']
interface Detection {
  participantId: string
  name: string
  winConditionId: string
  matches: number
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
  host,
  onInspect,
  onDismissDetection,
}: {
  claims: Claim[]
  detections: { at: number; data: Detection }[]
  everyDeclaredWin: boolean
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
    <Panel title="Bingo!">
      <ul className="flex flex-col gap-3">
        {claims.map((claim) => (
          <li key={claim.id} className="flex flex-col gap-2 rounded-xl bg-notice p-3">
            <p className="font-semibold">
              🔔 {claim.name} pressed Bingo ·{' '}
              {claim.matches > 0
                ? `✓ ${String(claim.matches)} line${claim.matches > 1 ? 's' : ''}`
                : '✗ not on called numbers'}
              {claim.alreadyWon
                ? ' · already won, nothing more to award'
                : claim.awardable === 0 && claim.matches > 0 && ' · no awardable matches'}
            </p>
            <div className="flex flex-wrap gap-2">
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
          <li key={at} className="flex flex-col gap-2 rounded-xl bg-info p-3">
            <p className="font-semibold">
              Detected: {data.name} has {data.winConditionId} ({data.matches} line
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
    </Panel>
  )
}
