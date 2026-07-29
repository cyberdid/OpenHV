---
title: Roadmap
status: current
updated: 2026-07-29
sources:
  - overview.md
  - architecture.md
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-headless-runtime.md
tags:
  - roadmap
  - planning
---

# Roadmap

This page is the compact priority view. See the
[detailed execution plan](execution-plan.md) for implementation tasks,
dependency gates, metrics, experiment design, and delivery estimates.

## Completed foundation

Simulation contract v1 and its deterministic graphical lifecycle are complete:

- versioned config/result DTOs and JSON Schema;
- exact synchronized world-tick cutoff with a separate wall-clock watchdog;
- pre-match map, bot, speed, seed, and limit validation;
- deterministic slot colors plus recorded faction, team, spawn, and home cell;
- atomic artifacts with separate natural winners and score leaders;
- paired-run, schema, and invalid-input validation.

See
[Simulation Contract v1 Validation](experiments/2026-07-29-simulation-contract-v1.md).

The headless correctness foundation is also complete:

- tracked, idempotent OpenRA SDK patching on normal build paths;
- no-op window/graphics/audio platform and unpaced logic loop;
- seed-derived lobby RNG and a renderer-independent bot RNG stream;
- 1,500-tick graphical/headless parity and repeat validation.

See
[Deterministic Headless Runtime Validation](experiments/2026-07-29-headless-runtime.md).

## P0 — Headless simulation loop

Move autonomous match orchestration and result capture away from the rendered
client.

Acceptance:

- no game window or audio device;
- one command runs at least 100 sequential matches;
- deterministic reruns with the same map, composition, seed, and commit;
- failed matches are isolated and reported without losing completed results.

Status: one-match no-device execution and deterministic cross-mode parity are
complete. Throughput is 0.738× rather than the required 5×, and the
manifest/resume/failure-isolation runner plus 100-match soak remain open.

## P0 — Deterministic scenario lifecycle

Support both finite conflict scenarios and open-ended living-world observation
windows, with deterministic cutoffs and a wall-clock watchdog for deadlocks.

Acceptance:

- natural victory, faction collapse, observation horizon, stalemate, timeout,
  crash, and invalid-map outcomes are distinct;
- final world tick and the scenario-specific outcome are recorded;
- timed leaders are never labeled as natural winners.

Status: the v1 identifiers, natural victory, tick limit, watchdog, and invalid
configuration paths exist. Faction-collapse, observation-horizon, stalemate,
desync, crash artifact, and external-cancel detectors remain part of the
headless/scenario lifecycle work.

## P0.5 — Living Factions vertical slice

Add an OpenRA-native civilization layer inspired by the useful concepts found
in OpenCiv: settlement population, worked territory, differentiated yields,
building effects, and civilization identity.

Acceptance:

- four factions can coexist without automatic war;
- population consumes food and responds to housing/shortages;
- settlements expose jobs, outputs, needs, prosperity, and stability;
- a faction can grow and research without combat;
- war requires an explicit relationship transition;
- military mobilization and losses affect civilian life.

See [Living Factions Design](faction-life.md).

## P1 — Strategic telemetry

Capture time-series and final metrics for population, needs, migration,
economy, construction, technology, trade, diplomacy, army composition,
territory, scouting, damage, and combat efficiency.

Acceptance:

- metrics have documented definitions and units;
- schema version is included in every result;
- tournament aggregation can compare profiles by map and spawn.

## P1 — Dynamic diplomacy and trade

Allow neutral factions to discover each other, exchange resources, form or
break agreements, develop grievances, declare war, and negotiate peace.

Acceptance:

- relationship changes are synchronized and reproducible;
- trade reflects real stocks, demand, routes, and risk;
- neutral factions are not auto-targeted;
- every treaty, grievance, and war transition is recorded;
- AI can rationally prefer peace, deterrence, or limited war.

## P1 — Automated experiment loop

Use controlled batches to test one configuration change at a time.

Acceptance:

- baseline and candidate use identical seed/map schedules;
- reports include uncertainty, not only point estimates;
- changes are kept only when they improve an explicit target without violating
  guardrails.

## P2 — Strong societal and faction identity

Progress from four parameter profiles to factions with distinct build orders,
needs priorities, institutions, technology paths, trade behavior, expansion
logic, diplomacy, scouting models, and counter-strategies.

Acceptance:

- telemetry demonstrates distinguishable behavior;
- each faction has strengths, weaknesses, and viable counters;
- no single faction dominates across the full map suite.

## P2 — Persistent world layer

Evaluate whether matches should remain independent or feed a larger world with
territory, diplomacy, adaptation, and faction history. This is intentionally
deferred until the core match simulator is fast and measurable.
