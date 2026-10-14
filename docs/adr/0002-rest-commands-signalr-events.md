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

## Amendment (2026-10-14): one sequence per audience
A single session-wide `seq` breaks gap detection, because players never receive host-only events (Bingo presses, detection alerts, dismissed claims): a player's numbers would skip after every claim and every phone would refetch the snapshot. So:
- Each event has an **audience** (players, host, display), decided in the domain in the same step as the change, and is **numbered within each audience's stream**. Every client sees an unbroken 1, 2, 3…; a gap always means a missed event.
- The snapshot's `seq` is the caller's own stream position, so a client knows exactly where to resume.
- **Private messages** to one player (auto-fill, a replaced card) and to the host (a player joined) are unnumbered notices: applying one twice is harmless, and the snapshot after a reconnect covers any that were missed.
- Wins and removals are ordinary events to everyone ("Sam had Bingo!", "Sam was removed from the game"), named without internal ids for players and the display. A removed player recognises their own (unique) name, and their token stops working.
- Considered and rejected: one shared sequence with empty "something changed" events for those who may not see an event (leaks the timing of claims); dropping gap detection (a missed event while connected would go unnoticed until the next reconnect).
