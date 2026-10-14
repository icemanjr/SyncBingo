import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { StreamTracker } from './streamTracker'

/** A session event (docs/05 §4): numbered in the receiver's own stream. */
export interface GameEvent {
  type: string
  seq: number
  data: unknown
}

/** A private message to one player, the host or a display: unnumbered and safe to apply twice. */
export interface Notice {
  type: string
  data: unknown
}

export interface GameConnectionOptions {
  code: string
  token: string
  /** The `game.seq` of the snapshot the caller is showing. */
  seq: number
  onEvent: (event: GameEvent) => void
  onNotice: (notice: Notice) => void
  /** Reloads the snapshot (after a gap or a reconnect) and returns its `game.seq`. */
  resync: () => Promise<number>
}

/** The parts of a SignalR connection used here, so tests can stand in for the server. */
export type HubLike = Pick<HubConnection, 'on' | 'start' | 'stop' | 'onreconnected'>

export function buildHub(code: string, token: string): HubLike {
  return new HubConnectionBuilder()
    .withUrl(`/hubs/game?code=${encodeURIComponent(code)}`, {
      // WebSockets only (ADR 0002); a browser can't set headers on a WebSocket, so the token goes in the query.
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

/**
 * Listens to a game's events. Events are applied in order; a duplicate is ignored; a gap, or coming back
 * after a dropped connection, reloads the snapshot and carries on from its position. Resyncs never overlap.
 */
export async function connectGame(
  options: GameConnectionOptions,
  hub: HubLike = buildHub(options.code, options.token),
): Promise<{ stop: () => Promise<void> }> {
  const tracker = new StreamTracker(options.seq)
  let resyncing: Promise<void> | undefined

  const resync = () => {
    resyncing ??= options
      .resync()
      .then((seq) => {
        tracker.resetTo(seq)
      })
      .finally(() => {
        resyncing = undefined
      })
    return resyncing
  }

  hub.on('event', (event: GameEvent) => {
    if (resyncing) {
      return // the snapshot being loaded already includes it, or a later gap will catch it
    }

    switch (tracker.accept(event.seq)) {
      case 'apply':
        options.onEvent(event)
        break
      case 'gap':
        void resync()
        break
      case 'duplicate':
        break
    }
  })
  hub.on('notice', (notice: Notice) => {
    options.onNotice(notice)
  })
  hub.onreconnected(() => {
    void resync()
  })

  await hub.start()
  return { stop: () => hub.stop() }
}
