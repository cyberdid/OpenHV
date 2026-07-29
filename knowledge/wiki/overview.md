---
title: Project Overview
status: current
updated: 2026-07-29
sources:
  - ../../README.md
  - ../../run-simulation.sh
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-simulation-contract-v1.md
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
- Matches atomically write schema-versioned JSON with build/map/slot metadata,
  synchronized hash, explicit end reason, natural winners, score leader, and
  statistics.
- A tournament runner rotates maps and seeds and aggregates end reasons,
  natural wins, score leads, and per-profile metrics.
- Paired reference runs reach the same cutoff with identical normalized config,
  synchronized hash, and final metrics.
- The full OpenHV validation suite passes.

## Current limitation

The runner still opens the graphical client and initializes audio. The initial
30-second tournament used the deprecated wall-clock cutoff and remains only a
startup/scoring test. Current runs use exact world ticks, but fast large batches
still require the headless runtime.

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
