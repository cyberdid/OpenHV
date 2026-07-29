---
title: Simulation Contract v1 Validation
status: complete
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-simulation-contract-v1.csv
  - ../../../schemas/simulation-result-v1.schema.json
  - ../../../check-simulation-determinism.sh
tags:
  - experiment
  - determinism
  - contract
---

# Simulation Contract v1 Validation

## Purpose

Validate that the graphical autonomous simulator has an explicit, versioned
result contract, stops on synchronized world ticks, rejects invalid inputs, and
repeats the same synchronized state for the same inputs.

## Configuration

- Date: 2026-07-29
- Map: Cold Rage
- Bots by slot: aggressor, economist, aggressor, economist
- Seed: 424242
- World-tick limit: 50
- Game speed: fastest, 20 milliseconds per world tick
- Wall-clock watchdog: 30 seconds
- Schema: `schemas/simulation-result-v1.schema.json`
- Compact evidence:
  [2026-07-29-simulation-contract-v1.csv](../../raw/experiments/2026-07-29-simulation-contract-v1.csv)

The repeatable command is:

```sh
CHECK_MAX_TICKS=50 ./check-simulation-determinism.sh coldrage
```

The harness runs two matches, removes only timestamps, match IDs, and output
paths, compares the normalized JSON byte-for-byte, validates both full results
against JSON Schema v1, and confirms that an unknown bot type returns non-zero.

## Results

- Both valid runs stopped at exactly world tick 50.
- Both produced synchronized state hash `4F20B62A`.
- Normalized results were identical.
- Ordered slot, bot profile, resolved faction, team, color, spawn point, home
  cell, scores, and final statistics matched.
- Both JSON documents passed Draft 2020-12 schema validation.
- `does-not-exist` was rejected before lobby construction completed and the
  process returned exit code 255.
- No atomic-write temporary files remained.

## Interpretation

SIM-001 and SIM-002 now have reproducible completion evidence. Render and audio
startup still occur, but they no longer determine how many synchronized world
ticks a finite observation contains. The wall clock is now only a deadlock
watchdog.

The synchronized hash and final metrics are the determinism authority.
Timestamps and artifact paths are deliberately environmental. The later
headless spike corrected the implementation detail behind deterministic lobby
colors: the local server RNG is seeded from the requested simulation seed, so
OpenRA's stock valid-color picker remains authoritative and repeatable.

## Remaining limits

- Natural victory and watchdog outcomes use the same v1 schema but still need
  dedicated golden fixtures.
- Crash, desync, stalemate, faction-collapse, and open-ended observation
  policies have identifiers but require their later lifecycle detectors.
- This historical validation used the graphical runtime. The later
  [headless runtime validation](2026-07-29-headless-runtime.md) demonstrated
  no-device execution and graphical parity at 1,500 ticks.
