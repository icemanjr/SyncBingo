import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { seats } from '../../storage/sessions'
import { displayCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import { hostApi } from '../host/hostApi'

/**
 * What the TV's QR opens on the host's phone (ADR 0010): connect that TV to a game this browser is hosting,
 * in one tap. Only a host's token can approve, so anyone else is sent to host a game first.
 */
export function PairPage() {
  const pairingCode = useParams().pairingCode ?? ''
  const games = seats.hostedGames()
  const { busy, error, run } = useAction()
  const [connected, setConnected] = useState(false)

  if (games.length === 0) {
    return (
      <div className="m-auto flex flex-col items-center gap-3 text-center">
        <p>To connect a TV, open this on the device you host the game from.</p>
        <Link to="/host/new" className="font-semibold text-accent-text underline">
          Host a game
        </Link>
      </div>
    )
  }

  return (
    <div className="m-auto flex w-full max-w-sm flex-col gap-3">
      <h1 className="text-2xl font-bold">Connect the TV ({pairingCode.toUpperCase()})</h1>
      {games.map((code) => {
        const seat = seats.load(code)
        if (seat?.role !== 'host') return null
        return (
          <Button
            key={code}
            disabled={busy || connected}
            onClick={() =>
              void run(async () => {
                await hostApi(code, seat.token).connectTv(pairingCode)
                setConnected(true)
              })
            }
          >
            Show game {displayCode(code)} on the TV
          </Button>
        )
      })}
      {connected && (
        <p role="status" className="font-semibold">
          Connected! The TV is showing the game.
        </p>
      )}
      {error && <ErrorText>{error}</ErrorText>}
    </div>
  )
}
