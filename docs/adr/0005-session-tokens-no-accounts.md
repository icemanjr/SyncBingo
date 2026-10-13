# ADR 0005 — Session tokens, no accounts

**Status:** Accepted

## Context
Players should join with only a name; nothing personally identifying is stored. Phones sleep and connections drop mid-game.

## Decision
On join the server issues a 256-bit random session token. Only its hash is stored. The client keeps the token and uses it to reclaim its name and card. The host receives a host-flagged token at session creation. The game password is only for joining and is stored hashed.

## Consequences
- Seamless recovery without user-managed credentials.
- A lost token (cleared browser data) means the participant cannot reclaim their identity; the host can kick the stale entry so the name can be reused.
- Join attempts need rate limiting because the password is the only gate.
- *Amended 2026-10-12:* a third, **read-only display** role (FR-28) uses the same token mechanism, for a projector or TV showing the call board. It can only read public state, never send commands, and the host can revoke it.
