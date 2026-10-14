import { useCallback, useState } from 'react'
import { ApiError } from '../../api/client'

/**
 * Runs a command from a button: `busy` while it runs (so a double tap sends it once) and the server's
 * own refusal message, which is written to be shown to people (docs/05 §5), when it fails.
 */
export function useAction() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  const run = useCallback(async (action: () => Promise<unknown>) => {
    setBusy(true)
    setError(undefined)
    try {
      await action()
    } catch (e) {
      setError(e instanceof ApiError ? e.detail : 'Something went wrong. Please try again.')
    } finally {
      setBusy(false)
    }
  }, [])

  return {
    busy,
    error,
    run,
    clearError: () => {
      setError(undefined)
    },
  }
}
