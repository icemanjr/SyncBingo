import { createContext, useContext, useEffect } from 'react'

/**
 * Lets a screen inside a page with ads turn them off while it shows a dead end (docs/06 §2, ADR 0012): a
 * game that has ended, a removed player, a loading or error state. Google allows no ads on screens without
 * content, or used only for alerts or navigation.
 */
export const AdsOff = createContext<(off: boolean) => void>(() => undefined)

/** No ads while `off` is true. */
export function useNoAds(off: boolean): void {
  const setOff = useContext(AdsOff)
  useEffect(() => {
    if (!off) return
    setOff(true)
    return () => {
      setOff(false)
    }
  }, [off, setOff])
}
