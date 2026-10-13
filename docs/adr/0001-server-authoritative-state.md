# ADR 0001 — Server-authoritative game state

**Status:** Accepted

## Context
Participants use their own phones and may be children or competing adults. Clients could be refreshed, slept, or tampered with.

## Decision
The server owns the hopper, called balls, marks, win evaluation and round state. Clients send intents (mark square N, claim Bingo); the server validates against current state and broadcasts results.

## Consequences
- The server stores exactly what each player marked and judges every claim against the balls actually called, so a false Bingo can never be awarded by mistake.
- *Amended 2026-10-12:* marking an uncalled number (a "mismark") is allowed by default, because careless marking is part of the game for older players; the host can switch to strict marking, where the server rejects it (FR-22). Every Bingo press is passed to the host, valid or not (FR-37a).
- A client can always rebuild its state from a snapshot after a disconnect.
- Slightly more server work per tap; acceptable at the target scale.
