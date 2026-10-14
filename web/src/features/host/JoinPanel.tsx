import QRCode from 'qrcode'
import { useEffect, useState } from 'react'
import { displayCode } from '../game/codes'

/**
 * How players get in (FR-2): the QR opens the join page with the code filled in; the password is never
 * in it. The join note (FR-49), if the host set one, sits underneath.
 */
export function JoinPanel({ code, joinNote }: { code: string; joinNote: string | null }) {
  const joinUrl = `${window.location.origin}/join/${code}`
  const [qr, setQr] = useState<string>()

  useEffect(() => {
    void QRCode.toString(joinUrl, { type: 'svg', margin: 1 }).then((svg) => {
      setQr(`data:image/svg+xml;utf8,${encodeURIComponent(svg)}`)
    })
  }, [joinUrl])

  return (
    <div className="flex items-center gap-4">
      {qr && (
        <img
          src={qr}
          alt={`QR code to join game ${displayCode(code)}`}
          className="h-28 w-28 bg-white"
        />
      )}
      <div>
        <p className="text-sm text-slate-500">Game code</p>
        <p className="font-mono text-xl font-bold tracking-wider">{displayCode(code)}</p>
        <p className="text-sm break-all text-slate-600">{joinUrl}</p>
        {joinNote && <p className="mt-1 font-semibold">{joinNote}</p>}
      </div>
    </div>
  )
}
