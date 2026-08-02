---
title: Project Overview
status: current
updated: 2026-08-02
sources:
  - universe-north-star.md
  - ../../README.md
  - ../../run-universe.sh
  - ../../run-simulation.sh
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-simulation-contract-v1.md
  - experiments/2026-07-29-headless-runtime.md
  - experiments/2026-07-29-headless-performance-fix.md
  - experiments/2026-07-29-batch-runner-v1.md
  - experiments/2026-07-29-living-factions-v1.md
  - experiments/2026-07-29-civil-research-v1.md
  - experiments/2026-07-29-dynamic-diplomacy-v1.md
  - experiments/2026-07-29-stock-backed-trade-v1.md
  - experiments/2026-07-29-civilization-ai-war-cost-v1.md
  - experiments/2026-07-29-scenario-lifecycle-v1.md
  - experiments/2026-07-29-baseline-112-v1.md
tags:
  - vision
  - status
---

# Project Overview

## Mission

Build an observable autonomous real-time history that begins with lifeless
planetary physics, produces ecosystems and a native race, grows civilizations
from prehistory to a technological Imperium, and ultimately connects three
independently evolved planets through trade, diplomacy, fleets, colonization,
and war—without human players.

The StarCraft-like RTS layer provides visible geography, logistics,
construction, units, and combat. A civilization layer adds long-term societal
motives and consequences. The immediate product is an autonomous observable
world rather than a conventional player-controlled game. The canonical runtime
is one OpenHV process; the Web/Python planet is an optional scientific observer
and mechanics prototype, not a second game engine.

## Current state

- OpenHV/OpenRA provides the engine, maps, economy, production, combat, replay,
  and modular-bot systems.
- `run-universe.sh` starts the canonical hands-off world: four AI
  civilizations on one persistent rendered map, normal speed, trade enabled,
  no wall-clock watchdog, and no cell or battle selection.
- This launcher is an interim single-process proof: it still begins with
  civilizations. The North Star requires a zero-state physical planet and will
  replace that start through the ordered migration plan.
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
- Stock-backed routes transfer food, materials, and energy from real exporter
  surplus into real importer deficit; infrastructure, distance, third-party
  conflict, and bilateral war affect capacity and availability.
- A synchronized Civilization AI scores survival, research, trade, security,
  recovery, and war; trade dependency can suppress conflict, while
  mobilization and casualties reduce workforce, production, population, and
  stability.
- Conflict and living-world scenarios now have distinct deterministic
  lifecycle semantics. Observation horizons never invent winners; persistent
  worlds ignore conquest victory and treat even total civil collapse as
  history rather than the end of planetary time. Finite collapse, stalemate,
  watchdog, and hard-ceiling endings remain explicit experiment options.
- One active 180×360 planet now evolves geology, climate, a conservative
  eight-level atmosphere/water cycle, and native cell-level habitability,
  abiogenesis precursor, biomass, churn, and complexity. It begins with zero
  life and can originate a biosphere without player or scripted race spawn.
- The first held-out civil/military baseline completed 112/112 clean-commit
  matches with 448 player observations, strict artifact QA, profile/map/spawn/
  faction breakdowns, and deterministic uncertainty estimates. It identified
  Fortress resilience, Aggressor collapse sensitivity, Technologist's missing
  technology advantage, Economist's over-mobilization, and near-zero balanced
  trade.
- The full OpenHV validation suite passes.

## Current limitation

Natural conflict completion is now a measured failure rather than an unknown:
all 112 baseline matches reached their 12,000-tick ceiling. AI finishing,
retreat, and regroup behavior must reduce that 100% cutoff rate. The
initial 30-second tournament used the deprecated wall-clock cutoff and remains
only a startup/scoring test. Civil telemetry covers the first settlement,
research, diplomacy, and first trade slices, but territory, physical cargo,
migration transfer, richer treaties, and tactical Civilization AI control of
build queues/retreats/targets are not implemented yet.

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
- [Stock-Backed Trade v1 validation](experiments/2026-07-29-stock-backed-trade-v1.md)
- [Civilization AI and War Cost v1 validation](experiments/2026-07-29-civilization-ai-war-cost-v1.md)
- [Scenario Lifecycle v1 validation](experiments/2026-07-29-scenario-lifecycle-v1.md)
- [Civil and Military Baseline 112 v1](experiments/2026-07-29-baseline-112-v1.md)
