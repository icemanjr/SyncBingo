# ADR 0010 — Pairing display screens with a short code

**Status:** Accepted

## Context
The display view (FR-28) is often shown on a smart TV or projector that is not connected to the host's computer. Typing a game code, a password or a display link with a TV remote is painful, and the screen should never hold anything that can run the game. Building native apps for each TV platform would mean many app stores and constant updates.

## Decision
Use a **device pairing** flow in the TV's own web browser, the same pattern as "enter the code shown on your TV" sign-ins (the OAuth 2.0 Device Authorization Grant, RFC 8628, with the roles reversed: the screen shows the code and the already-authenticated host approves it).

1. The TV opens a short address (`/tv`). The server creates a **pairing**: a 6-character code from the game-code alphabet (no look-alikes), valid for 10 minutes and usable once. The page shows the code large, plus a QR code of the pairing link, and waits on an anonymous WebSocket that can receive only that pairing's result.
2. The host chooses **Connect a TV** and types the code, or scans the TV's QR with a phone already signed in as host.
3. The server issues a read-only **display token** (ADR 0005) and pushes it to the waiting TV, which switches to the display view and keeps the token for reconnects during the session.

## Consequences
- Works on any TV or projector with a modern-enough browser; no native apps to build, publish or keep updated.
- The TV never handles a secret typed by a person, and only ever holds read-only rights. The host can see connected displays and disconnect any of them.
- Pairing codes are short, so they are short-lived, single-use and rate-limited (per IP for creating pairings, per host for approving codes); about 887 million codes (31⁶) and a 10-minute life make guessing impractical.
- Smart TV browsers are often old and slow: the display page must stay lightweight, and real TV browsers join the Phase 2 test list.
- The HDMI route (opening the display view in a new tab on the host's computer) is unchanged.
- *Implementation note (2026-10-14):* an approved result is also kept for up to 2 minutes and handed over when the TV connects, so a TV that lost its connection for a moment is not stranded. That briefly holds the display token in clear in Redis (otherwise tokens are stored only as hashes); it is read-only and deleted once taken.
