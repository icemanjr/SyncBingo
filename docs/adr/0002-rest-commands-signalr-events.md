# ADR 0002 — REST for commands, SignalR for events

**Status:** Accepted

## Context
Real-time delivery is required, but commands also need clear semantics, easy testing and straightforward auth.

## Decision
All client→server commands are REST endpoints. The SignalR hub only pushes server→client events, over WebSockets only, with a Redis backplane. Events carry a sequence number; the snapshot endpoint is the source of truth after gaps or reconnects.

## Consequences
- Commands are testable with plain HTTP tooling and documented in OpenAPI.
- No sticky sessions needed behind the ALB (WebSocket-only, backplane for fan-out).
- Two channels to keep consistent; mitigated by the sequence-and-snapshot protocol.
