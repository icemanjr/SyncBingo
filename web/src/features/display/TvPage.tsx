import QRCode from 'qrcode'
import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { api, unwrap } from '../../api/client'
import { buildPairingHub, type PairingHub } from '../../realtime/pairingConnection'
import { useSecondsLeft } from '../game/Countdown'
import { BrandMark } from '../../app/Layout'
import { Button } from '../game/ui'

/**
 * Connecting a smart TV or projector (ADR 0010): the TV's own browser opens /tv, which shows a short code;
 * the host types it under **Open display ⧉ → Connect a TV**, or scans the QR with the phone they host from.
 * The page then becomes the display. Kept light: TV browsers are often old and slow.
 */
export function TvPage({
  buildHub = buildPairingHub,
}: {
  buildHub?: (pairingId: string) => PairingHub
}) {
  const navigate = useNavigate()
  const [pairing, setPairing] = useState<{
    pairingId: string
    pairingCode: string
    expiresAt: string
  }>()
  const [qr, setQr] = useState<string>()
  const [problem, setProblem] = useState<string>()
  const left = useSecondsLeft(pairing?.expiresAt)

  const request = useCallback(() => {
    unwrap(api.POST('/api/tv/pairings')).then(
      (started) => {
        setProblem(undefined)
        setPairing(started)
      },
      () => {
        setProblem('Could not get a code. Please try again in a moment.')
      },
    )
  }, [])

  useEffect(request, [request])

  useEffect(() => {
    if (!pairing) return
    void QRCode.toString(`${window.location.origin}/pair/${pairing.pairingCode}`, {
      type: 'svg',
      margin: 1,
    }).then((svg) => {
      setQr(`data:image/svg+xml;utf8,${encodeURIComponent(svg)}`)
    })
    const hub = buildHub(pairing.pairingId)
    hub.on('Paired', (result) => {
      void navigate(`/display/${result.sessionCode}#t=${result.displayToken}`)
    })
    hub.on('PairingExpired', () => {
      setPairing(undefined)
      setProblem('That code expired.')
    })
    void hub.start()
    return () => {
      void hub.stop()
    }
  }, [pairing, buildHub, navigate])

  const expired = pairing !== undefined && left === 0

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-6 p-8 text-center">
      <h1 className="flex items-center gap-3 font-display text-4xl font-extrabold tracking-tight">
        <BrandMark />
        SyncBingo display
      </h1>
      {pairing && !expired ? (
        <>
          <p className="text-2xl">Enter this code on the host’s screen:</p>
          <p
            className="rounded-3xl bg-surface px-10 py-6 font-display text-8xl font-extrabold tracking-widest shadow-card"
            aria-label={`Code ${pairing.pairingCode.split('').join(' ')}`}
          >
            {pairing.pairingCode}
          </p>
          {qr && (
            <img
              src={qr}
              alt="QR code to connect this TV from the host's phone"
              className="h-44 w-44 rounded-2xl bg-white p-3"
            />
          )}
          <p className="text-xl text-ink-muted">
            or scan with the host's phone · code expires in {Math.floor(left / 60)}:
            {String(left % 60).padStart(2, '0')}
          </p>
        </>
      ) : (
        <>
          <p className="text-2xl">
            {problem ?? (expired ? 'That code expired.' : 'Getting a code…')}
          </p>
          {(problem ?? expired) && <Button onClick={request}>Get a new code</Button>}
        </>
      )}
    </div>
  )
}
