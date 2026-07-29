---
title: Roadmap
status: current
updated: 2026-07-29
sources:
  - overview.md
  - architecture.md
  - experiments/2026-07-29-baseline-tournament.md
tags:
  - roadmap
  - planning
---

# Roadmap

This page is the compact priority view. See the
[detailed execution plan](execution-plan.md) for implementation tasks,
dependency gates, metrics, experiment design, and delivery estimates.

## P0 — Headless simulation loop

Move autonomous match orchestration and result capture away from the rendered
client.

Acceptance:

- no game window or audio device;
- one command runs at least 100 sequential matches;
- deterministic reruns with the same map, composition, seed, and commit;
- failed matches are isolated and reported without losing completed results.

## P0 — Deterministic scenario lifecycle

Support both finite conflict scenarios and open-ended living-world observation
windows, with deterministic cutoffs and a wall-clock watchdog for deadlocks.

Acceptance:

- natural victory, faction collapse, observation horizon, stalemate, timeout,
  crash, and invalid-map outcomes are distinct;
- final world tick and the scenario-specific outcome are recorded;
- timed leaders are never labeled as natural winners.

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
