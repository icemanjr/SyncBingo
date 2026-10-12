# SyncBingo — Design Documents

> A Kahoot-style, phone-first Bingo web app for family gatherings, parties and groups of adults. One person hosts, everyone else joins from their own phone by scanning a QR code or typing a game code.

**Stack:** C# / .NET 10 (API + SignalR) · React 19 + TypeScript (SPA) · Redis (ephemeral state) · Docker for local/test · AWS (ECS Fargate, ElastiCache, S3/CloudFront) provisioned with AWS CDK in C#.

**Status:** Design phase. These documents are the source of truth before implementation begins.

## Document index

| # | Document | Purpose |
|---|----------|---------|
| 01 | [Requirements](01-requirements.md) | Functional and non-functional requirements, scope, assumptions |
| 02 | [Architecture](02-architecture.md) | Components, layering, real-time design, concurrency |
| 03 | [Game state machine](03-game-state-machine.md) | Session/round lifecycle, allowed commands, win handling |
| 04 | [Data model](04-data-model.md) | Domain model, card/pattern bitmasks, Redis layout, security |
| 05 | [API contract](05-api-contract.md) | REST endpoints and SignalR events |
| 06 | [UI and ad slots](06-ui-and-ads.md) | Screens, layout, ad-slot rules, accessibility |
| 07 | [Infrastructure](07-infrastructure.md) | Docker local setup, AWS CDK stacks, CI/CD |
| 08 | [Testing strategy](08-testing-strategy.md) | Unit, integration, end-to-end, load |
| 09 | [Roadmap](09-roadmap.md) | MVP phases and future expansion (incl. "Serious Bingo") |
| — | [ADRs](adr/) | Short records of the key architectural decisions |

## Glossary

| Term | Meaning |
|------|---------|
| **Session** (a.k.a. game) | Everything that happens from the moment a host creates a game until it ends: all rounds, all participants. |
| **Round** | A span during which cards carry marks. A round begins when the balls go back into the hopper ("Clear your cards") and ends when the host ends it. Multiple winners and win-condition changes (e.g. Normal Bingo → Blackout) can happen inside one round. |
| **Hopper** | The pool of balls not yet called (75 for standard Bingo). |
| **Call / ball** | A number drawn from the hopper and announced. |
| **Win condition** | The pattern currently being played: Normal (any row, column or diagonal), Four Corners, letter shapes B / I / N / G / O, or Blackout. |
| **Host** | The person running the session. Optionally also a participant. |
| **Participant** | A player in the session, identified only by a name that is unique within the session. |
| **Game code** | 16-character random code (four groups of four, hyphenated) carried by the QR code. |
| **Game password** | Chosen by the host; required to join, whether by QR or by typing the code. |
| **Session token** | Opaque secret issued at join so a participant can recover after a sleeping phone or dropped connection. |
