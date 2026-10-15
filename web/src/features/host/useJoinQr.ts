import QRCode from 'qrcode'
import { useEffect, useState } from 'react'

/** The join link for a game and its QR code (an SVG data URL, once drawn); the password is never in it. */
export function useJoinQr(code: string): { joinUrl: string; qr: string | undefined } {
  const joinUrl = `${window.location.origin}/join/${code}`
  const [qr, setQr] = useState<string>()

  useEffect(() => {
    void QRCode.toString(joinUrl, { type: 'svg', margin: 1 }).then((svg) => {
      setQr(`data:image/svg+xml;utf8,${encodeURIComponent(svg)}`)
    })
  }, [joinUrl])

  return { joinUrl, qr }
}
