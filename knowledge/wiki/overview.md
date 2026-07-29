---
title: Project Overview
status: current
updated: 2026-07-29
sources:
  - ../../README.md
  - ../../run-simulation.sh
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-simulation-contract-v1.md
  - experiments/2026-07-29-headless-runtime.md
  - experiments/2026-07-29-headless-performance-fix.md
  - experiments/2026-07-29-batch-runner-v1.md
tags:
  - vision
  - status
---

# Project Overview

## Mission

Build an observable autonomous real-time world in which factions live rather
than merely fight. They grow populations, build settlements, satisfy needs,
develop technology and institutions, trade, negotiate, migrate, recover from
crises, and may scout, defend, or wage war without human players.

The StarCraft-like RTS layer provides visible geography, logistics,
construction, units, and combat. A civilization layer adds long-term societal
motives and consequences. The immediate product is a simulation laboratory
rather than a conventional player-controlled game.

## Current state

- OpenHV/OpenRA provides the engine, maps, economy, production, combat, replay,
  and modular-bot systems.
- A local observer can automatically start a free-for-all match with no human
  participant.
- Four AI profiles are selectable: aggressor, economist, technologist, and
  fortress.
- Match composition, map, speed, seed, synchronized world-tick horizon,
  watchdog, telemetry interval, and result path are configurable and validated.
- The same client gameplay path can run through a no-window/no-audio headless
  platform, and tournaments select it by default.
- Matches atomically write schema-versioned JSON with build/map/slot metadata,
  execution mode, synchronized hash, explicit end reason, natural winners,
  score leader, and statistics.
- A tournament runner rotates maps and seeds and aggregates end reasons,
  natural wins, score leads, and per-profile metrics.
- Seeded bot randomness is isolated from renderer cosmetics. A 1,500-tick
  graphical/headless reference pair and a repeated headless run produced the
  same normalized result and synchronized hash.
- Profiling removed repeated dummy-audio decoding; repeated 1,500-tick runs
  reached 6.173×–6.342× real time and passed the 5× engineering gate.
- A Schema v1 manifest runner provides process/support isolation, stable
  resolved schedules, bounded concurrency, hard watchdogs, failure-only
  retries, signal-safe resume, cumulative sessions, validated aggregation, and
  replay/failure diagnostics.
- Exact-commit 100-match soaks completed 100/100 sequentially and with four
  workers. Resume skipped all completed matches without adding attempts.
- The full OpenHV validation suite passes.

## Current limitation

Batch infrastructure is proven only on short 100-tick matches; late-game
memory/performance, natural outcomes, and telemetry overhead still need
measurement. The initial 30-second tournament used the deprecated wall-clock
cutoff and remains only a startup/scoring test. Time-series telemetry,
population, settlements, civil resources, research, trade, migration, and
dynamic diplomacy are not implemented yet.

## Success criteria

The simulation becomes useful when it can:

1. run unattended without graphics;
2. finish matches naturally or under a documented deterministic cutoff;
3. execute hundreds of reproducible matches;
4. expose population, needs, economy, settlement, territory, technology,
   diplomacy, army, and combat metrics;
5. allow factions to coexist, trade, prosper, stagnate, migrate, fragment, or
   collapse without mandatory war;
6. demonstrate statistically distinguishable societal and military strategies;
7. make warfare costly to population, economy, stability, and diplomacy;
8. turn experiment results into explicit design and balancing decisions.

## Related pages

- [Architecture](architecture.md)
- [AI profiles](ai-profiles.md)
- [Living factions](faction-life.md)
- [Roadmap](roadmap.md)
- [Baseline tournament](experiments/2026-07-29-baseline-tournament.md)
- [Simulation contract v1 validation](experiments/2026-07-29-simulation-contract-v1.md)
- [Deterministic headless runtime validation](experiments/2026-07-29-headless-runtime.md)
- [Headless dummy-audio performance fix](experiments/2026-07-29-headless-performance-fix.md)
- [Resumable batch runner v1 validation](experiments/2026-07-29-batch-runner-v1.md)
