# ADR 0006 — Game code format

**Status:** Accepted (alphabet pending confirmation)

## Context
The code is carried by the QR code and sometimes typed by hand. The preferred format is 16 characters in four hyphenated groups of four.

## Decision
Generate 16 characters with a CSPRNG from `23456789abcdefghjkmnpqrstuvwxyz` (31 symbols, ≈ 79 bits), displayed as `xxxx-xxxx-xxxx-xxxx`. Accept input case-insensitively with or without hyphens.

## Consequences
- Omitting 0/o and 1/i/l reduces typing mistakes; the example code in the brief used some of those characters, so this is a deliberate, easily reversed tweak.
- The code space is large enough that guessing is impractical, and the join password adds a second factor.
