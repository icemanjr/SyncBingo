import { useEffect, useState } from 'react'

/** Whole seconds left until `until`, ticking on this device's clock; 0 once it has passed. */
export function useSecondsLeft(until: string | null | undefined): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!until) return
    const timer = setInterval(() => {
      setNow(Date.now())
    }, 250)
    return () => {
      clearInterval(timer)
    }
  }, [until])
  return until ? Math.max(0, Math.ceil((Date.parse(until) - now) / 1000)) : 0
}
