---
title: "Decision 0004: Accept the logic-only headless client runtime"
status: accepted-with-performance-follow-up
updated: 2026-07-29
sources:
  - ../architecture.md
  - ../experiments/2026-07-29-headless-runtime.md
  - ../../../engine-patches/openra-headless.patch
tags:
  - decision
  - architecture
  - headless
---

# Decision 0004: Accept the logic-only headless client runtime

## Context

OpenRA's dedicated server owns lobby and network coordination but does not
construct or advance a gameplay `World`. A batch simulator therefore still
needs a world-running client. The spike compared a logic-only mode inside that
client with the alternatives of a thin dedicated-server client or a standalone
world runner.

## Decision

Keep the normal local server, `OrderManager`, `World`, traits, bot modules, and
result capture, but provide a simulation-only engine mode that:

- supplies no-op graphics, sound, font, texture, shader, and window services;
- skips UI, cursor, input, frame presentation, and real-time sleeping;
- preserves synchronized world ticks and render-trait ticks needed for
  compatibility;
- seeds local-server lobby randomness from the requested match seed;
- uses a separate seed-derived `World.BotRandom` so renderer cosmetics cannot
  alter strategic bot decisions;
- is carried as a version-pinned, idempotent SDK patch plus tracked source
  overlay applied by the normal build workflow.

The regular game defaults remain graphical and retain normal random behavior.
Only `run-simulation.sh` enables deterministic simulation mode.

## Why

The approach reuses the production gameplay path and passed a 1,500-tick
graphical/headless parity test. It is substantially less divergent than a
standalone world runner and does not pretend that the dedicated server alone
simulates gameplay.

## Consequences

- `run-tournament.sh` can default to no-window execution.
- Engine upgrades must revalidate and, when necessary, rebase the patchset.
- Bot randomness is now an explicit strategic stream for deterministic
  simulation.
- No-op rendering removes device dependencies but not all render-trait work.
- The architecture is accepted for correctness, but its throughput is not:
  the measured 0.738× real time misses the 5× gate. Profiling and optimization
  remain mandatory before a 100-match soak is considered routine.

## Revisit conditions

Reconsider a thin client or standalone runner only if measured synchronized
logic/local-network bottlenecks cannot reach useful batch throughput without
large changes, or if repeated parity tests expose unavoidable coupling to the
graphical client.
