import { useSyncExternalStore } from 'react'

const never = () => () => undefined

/**
 * False while the build renders a page to HTML and while the browser adopts that HTML, true once the app runs
 * (straight away on pages that weren't pre-built). For what a pre-built page can't know, such as this device's
 * saved theme: showing it before then would differ from the HTML being adopted.
 */
export const useHydrated = (): boolean =>
  useSyncExternalStore(
    never,
    () => true,
    () => false,
  )
