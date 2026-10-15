import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

/** What a paired TV receives (ADR 0010): the game to show and its read-only display token. */
export interface Paired {
  sessionCode: string
  displayToken: string
}

export interface PairingHub {
  on(method: 'Paired', handler: (result: Paired) => void): void
  on(method: 'PairingExpired', handler: () => void): void
  start(): Promise<void>
  stop(): Promise<void>
}

/** The anonymous pairing hub, which can deliver only this pairing's result. */
export function buildPairingHub(pairingId: string): PairingHub {
  return new HubConnectionBuilder()
    .withUrl(`/hubs/pairing?pairingId=${encodeURIComponent(pairingId)}`, {
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    .configureLogging(LogLevel.Warning)
    .build()
}
