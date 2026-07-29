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
  - experiments/2026-07-29-living-factions-v1.md
  - experiments/2026-07-29-civil-research-v1.md
  - experiments/2026-07-29-dynamic-diplomacy-v1.md
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
- Five AI profiles are selectable: aggressor, economist, technologist,
  fortress, and non-attacking civil-development Steward.
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
- Every faction now has synchronized civilization and settlement state with
  population cohorts, workforce, jobs, housing, food, materials, energy,
  knowledge, needs, prosperity, stability, and demographic consequences.
- Result Schema v1 has backward-compatible civil extensions; Telemetry and
  Event Schemas v1 capture periodic player/settlement snapshots and
  reason-coded civil transitions as JSONL.
- Balanced growth, deterministic telemetry parity, food-scarcity mortality,
  zero-combat Steward growth, and sub-1% observed CPU overhead passed.
- A five-node civil research graph spends knowledge on deterministic unlocks
  that modify real settlement production and capacity.
- Runtime diplomacy begins all active pairs neutral, changes native OpenRA
  targeting masks on explicit war/peace transitions, and exports synchronized
  bilateral state plus reason-coded events.
- The full OpenHV validation suite passes.

## Current limitation

Late-game memory/performance and natural outcomes still need measurement. The
initial 30-second tournament used the deprecated wall-clock cutoff and remains
only a startup/scoring test. Civil telemetry covers the first settlement,
research, and diplomacy slices, but territory, trade, migration transfer,
casualty coupling, richer treaties, and Civilization AI utility planning are
not implemented yet.

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
- [Living Factions and Telemetry v1 validation](experiments/2026-07-29-living-factions-v1.md)
- [Civil Research v1 validation](experiments/2026-07-29-civil-research-v1.md)
- [Dynamic Diplomacy v1 validation](experiments/2026-07-29-dynamic-diplomacy-v1.md)
