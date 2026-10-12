# ADR 0001 — Server-authoritative game state

**Status:** Accepted

## Context
Participants use their own phones and may be children or competing adults. Clients could be refreshed, slept, or tampered with.

## Decision
The server owns the hopper, called balls, marks, win evaluation and round state. Clients send intents (mark square N, claim Bingo); the server validates against current state and broadcasts results.

## Consequences
- Marking an uncalled number or claiming a false Bingo is rejected server-side.
- A client can always rebuild its state from a snapshot after a disconnect.
- Slightly more server work per tap; acceptable at the target scale.
