# ADR 0003 — Redis with TTL, no relational database

**Status:** Accepted

## Context
Requirements state that nothing persists beyond the current session and there are no accounts.

## Decision
Live state is stored in Redis with idle expiry and a hard lifetime cap, behind an `ISessionStore` interface. Redis also serves as the SignalR backplane and the auto-call scheduler. No RDS/DynamoDB.

## Consequences
- Simple operations and a good privacy story: data disappears with the session.
- Per-session locks and atomic field updates replace transactions.
- If history or accounts are ever wanted, a durable store can be added behind the same interface.
