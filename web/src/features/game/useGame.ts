import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiError, unwrap } from '../../api/client'
import { connectGame, type Notice } from '../../realtime/gameConnection'
import { applyEvent, type Applied, type RulesNotice, type Snapshot } from './gameEvents'

export interface GameState {
  snapshot: Snapshot | undefined
  error: ApiError | undefined
  rulesNotice: RulesNotice | undefined
  dismissRulesNotice: () => void
  /** Alerts and notices for this screen, newest last (wins, detections, private notices). */
  messages: { type: string; data: unknown; at: number }[]
  dismissMessage: (at: number) => void
  refresh: () => Promise<void>
  /** Updates the snapshot locally from a command's reply (e.g. a player's own new marks). */
  patch: (change: (snapshot: Snapshot) => Snapshot) => void
}

/** How long to gather events that need a refetch before fetching once. */
const RefetchDelayMs = 150

/**
 * A game as this screen sees it: the snapshot, kept current from the event stream (docs/05), with a
 * refetch after a gap or reconnect, or after events the snapshot can't absorb. Bursts of such events
 * share one refetch.
 */
export function useGame(code: string, token: string | undefined): GameState {
  const [snapshot, setSnapshot] = useState<Snapshot>()
  const [error, setError] = useState<ApiError>()
  const [rulesNotice, setRulesNotice] = useState<RulesNotice>()
  const [messages, setMessages] = useState<GameState['messages']>([])
  const latest = useRef<Snapshot | undefined>(undefined)
  const refetchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const load = useCallback(async (): Promise<Snapshot> => {
    const loaded = await unwrap(
      api.GET('/api/sessions/{code}/snapshot', {
        params: { path: { code } },
        headers: { Authorization: `Bearer ${token ?? ''}` },
      }),
    )
    latest.current = loaded
    setSnapshot(loaded)
    return loaded
  }, [code, token])

  const refresh = useCallback(async () => {
    try {
      await load()
      setError(undefined)
    } catch (e) {
      setError(e instanceof ApiError ? e : ApiError.from(0, undefined))
    }
  }, [load])

  useEffect(() => {
    if (!token) {
      return
    }

    const lifetime = { stopped: false }
    // A function, not the field, so the checks after each await read the current value.
    const isStopped = () => lifetime.stopped
    let stop: (() => Promise<void>) | undefined
    const scheduleRefetch = () => {
      clearTimeout(refetchTimer.current)
      refetchTimer.current = setTimeout(() => void refresh(), RefetchDelayMs)
    }
    const note = (message: { type: string; data: unknown }) => {
      setMessages((list) => [...list, { ...message, at: Date.now() + Math.random() }])
    }

    void (async () => {
      try {
        const first = await load()
        if (isStopped()) return
        const connection = await connectGame({
          code,
          token,
          seq: first.game.seq,
          onEvent: (event) => {
            if (!latest.current) return
            const applied: Applied = applyEvent(latest.current, event)
            latest.current = applied.snapshot
            setSnapshot(applied.snapshot)
            if (applied.rulesNotice) setRulesNotice(applied.rulesNotice)
            else if (event.type === 'BallCalled') setRulesNotice(undefined) // the notice lasts until the next ball (FR-47)
            if (applied.alert) note(applied.alert)
            if (applied.refetch) scheduleRefetch()
          },
          onNotice: (notice: Notice) => {
            note(notice)
            scheduleRefetch()
          },
          resync: async () => (await load()).game.seq,
        })
        if (isStopped()) await connection.stop()
        else stop = connection.stop
      } catch (e) {
        if (!isStopped()) setError(e instanceof ApiError ? e : ApiError.from(0, undefined))
      }
    })()

    return () => {
      lifetime.stopped = true
      clearTimeout(refetchTimer.current)
      void stop?.()
    }
  }, [code, token, load, refresh])

  return {
    snapshot,
    error,
    rulesNotice,
    dismissRulesNotice: () => {
      setRulesNotice(undefined)
    },
    messages,
    dismissMessage: (at) => {
      setMessages((list) => list.filter((m) => m.at !== at))
    },
    refresh,
    patch: (change) => {
      if (!latest.current) return
      latest.current = change(latest.current)
      setSnapshot(latest.current)
    },
  }
}
