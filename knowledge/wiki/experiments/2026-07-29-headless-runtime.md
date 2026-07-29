---
title: Deterministic Headless Runtime Validation
status: complete-with-open-performance-gate
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-headless-runtime.csv
  - ../../../check-headless-equivalence.sh
  - ../../../engine-patches/openra-headless.patch
  - ../../../engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs
tags:
  - experiment
  - headless
  - determinism
  - performance
---

# Deterministic Headless Runtime Validation

## Purpose

Test whether the existing OpenRA client simulation can run without a window,
OpenGL context, or audio device while retaining the same bot decisions and
synchronized result as the graphical runtime.

## Configuration

- Date: 2026-07-29
- Code commit: `310501bd81af9eca038a998684570d49f7713371`
- Engine commit: `12f7ca7324a8dfc2e92a03d207a37b5bb905f923`
- Machine: Apple M4 Max, macOS Arm64, .NET 8.0.29
- Map: Cold Rage
- Bots by slot: aggressor, economist, aggressor, economist
- Seed: 424242
- Game speed: fastest, 20 milliseconds per world tick
- Main comparison horizon: 1,500 world ticks / 30 simulated seconds
- Wall-clock watchdog: 150 seconds
- Compact evidence:
  [2026-07-29-headless-runtime.csv](../../raw/experiments/2026-07-29-headless-runtime.csv)

The parity command is:

```sh
CHECK_MAX_TICKS=1500 CHECK_WATCHDOG_SECONDS=150 \
./check-headless-equivalence.sh coldrage
```

The benchmark command is:

```sh
/usr/bin/time -p env \
  SIMULATION_HEADLESS=true \
  SIMULATION_BOTS=aggressor,economist \
  SIMULATION_MAX_TICKS=1500 \
  SIMULATION_WATCHDOG_SECONDS=150 \
  SIMULATION_SEED=424242 \
  ./run-simulation.sh coldrage
```

## Results

- The headless log selected `HeadlessPlatform` and contained no SDL, OpenGL,
  or default-audio initialization.
- Graphical and headless runs both stopped at world tick 1,500 with
  synchronized state hash `0AC799D4`.
- After removing timestamps, match ID, result path, and the two declared
  execution-mode fields, their result documents were byte-identical.
- A second headless run produced the same normalized document and hash.
- All three artifacts passed JSON Schema v1 validation.
- The full Release build and OpenHV validation suite passed with zero compiler
  warnings or errors.
- The final headless benchmark simulated 30 seconds in 40.66 wall seconds:
  0.738× real time.

## Determinism correction discovered by the spike

The first 100- and 200-tick checks passed, but an initial 250-tick comparison
diverged. OpenRA's `World.LocalRandom` served both renderer effects and bot
strategy. A diagnostic at tick 200 observed 34,665 local-RNG draws in
graphical mode and 372 in headless mode. Later bot choices therefore consumed
different values even though `World.SharedRandom` and lobby configuration were
identical.

The final design gives bot modules a dedicated `World.BotRandom`, derived from
the match seed only for deterministic simulations. `LocalRandom` remains
available for visual and audio variation. This is why the acceptance test must
extend past the first strategic decisions rather than stop at a startup smoke
tick.

## Interpretation

SIM-003, SIM-004, and SIM-005 are implemented: the logic-only client
architecture is viable, no graphics/audio backend is required, and long
graphical/headless parity is demonstrated. The architecture is accepted in
[Decision 0004](../decisions/0004-logic-only-headless-runtime.md).

Sprint 2 is not fully closed because its 5× throughput gate failed. Removing
graphics and real-time sleeps exposed synchronized game logic, allocations,
local networking, and bot work as the dominant cost. At 0.738×, the observed
run is 6.78 times slower than the six-second wall budget required for 5×.

## Limits and next experiment

- Results cover one map, one seed, and two repeated bot profiles; the batch
  soak must broaden map, seed, and four-profile coverage.
- The headless platform preserves render-side trait ticking for behavioral
  compatibility even though drawing operations are no-ops.
- Windows patch application is implemented but was not executed on this macOS
  machine.
- The next performance experiment will profile synchronized tick cost by bot
  module, pathfinding, allocation/GC, and local server/order transport before
  deciding whether to optimize the current loop or introduce a more isolated
  runner.
