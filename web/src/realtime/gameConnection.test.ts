import { describe, expect, it, vi } from 'vitest'
import { connectGame, type GameEvent, type HubLike, type Notice } from './gameConnection'

/** Stands in for the SignalR server: tests push messages and drop the connection by hand. */
class FakeHub implements HubLike {
  handlers = new Map<string, (message: never) => void>()
  reconnected: (() => void) | undefined

  on(method: string, handler: (message: never) => void): void {
    this.handlers.set(method, handler)
  }

  onreconnected(handler: () => void): void {
    this.reconnected = handler
  }

  start = vi.fn(() => Promise.resolve())
  stop = vi.fn(() => Promise.resolve())

  push(method: 'event' | 'notice', message: GameEvent | Notice): void {
    this.handlers.get(method)?.(message as never)
  }
}

const event = (seq: number, type = 'BallCalled'): GameEvent => ({ type, seq, data: {} })

async function connect(seq: number, resyncTo = 0) {
  const hub = new FakeHub()
  const applied: number[] = []
  const notices: string[] = []
  const resync = vi.fn(() => Promise.resolve(resyncTo))
  await connectGame(
    {
      code: '9ve4shp3k7mwa2qx',
      token: 't',
      seq,
      onEvent: (e) => applied.push(e.seq),
      onNotice: (n) => notices.push(n.type),
      resync,
    },
    hub,
  )
  return { hub, applied, notices, resync }
}

describe('connectGame', () => {
  it('applies events in order and ignores repeats', async () => {
    const { hub, applied, resync } = await connect(3)

    hub.push('event', event(4))
    hub.push('event', event(4))
    hub.push('event', event(5))

    expect(applied).toEqual([4, 5])
    expect(resync).not.toHaveBeenCalled()
  })

  it('reloads the snapshot after a gap and carries on from it', async () => {
    const { hub, applied, resync } = await connect(3, 9)

    hub.push('event', event(6))
    await vi.waitFor(() => {
      expect(resync).toHaveBeenCalledOnce()
    })
    await Promise.resolve()
    hub.push('event', event(10))

    expect(applied).toEqual([10])
  })

  it('ignores events while a resync is loading, and never overlaps resyncs', async () => {
    const { hub, resync } = await connect(3, 9)

    hub.push('event', event(6))
    hub.push('event', event(8))
    hub.reconnected?.()

    await vi.waitFor(() => {
      expect(resync).toHaveBeenCalledOnce()
    })
  })

  it('reloads the snapshot after coming back from a dropped connection', async () => {
    const { hub, resync } = await connect(3, 3)

    hub.reconnected?.()

    await vi.waitFor(() => {
      expect(resync).toHaveBeenCalledOnce()
    })
  })

  it('passes private notices straight through', async () => {
    const { hub, notices } = await connect(3)

    hub.push('notice', { type: 'AutoFillApplied', data: {} })

    expect(notices).toEqual(['AutoFillApplied'])
  })
})
