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

## P0 — Natural match completion

Run matches long enough for combat victory, with a deterministic safety cutoff
for stalemates.

Acceptance:

- natural win, timeout, crash, and invalid-map outcomes are distinct;
- victory time and final world tick are recorded;
- timed leaders are never labeled as natural winners.

## P1 — Strategic telemetry

Capture time-series and final metrics for economy, construction, technology,
army composition, territory, scouting, damage, and combat efficiency.

Acceptance:

- metrics have documented definitions and units;
- schema version is included in every result;
- tournament aggregation can compare profiles by map and spawn.

## P1 — Automated balance loop

Use controlled batches to test one configuration change at a time.

Acceptance:

- baseline and candidate use identical seed/map schedules;
- reports include uncertainty, not only point estimates;
- changes are kept only when they improve an explicit target without violating
  guardrails.

## P2 — Strong faction identity

Progress from four parameter profiles to factions with distinct build orders,
technology paths, expansion logic, scouting models, and counter-strategies.

Acceptance:

- telemetry demonstrates distinguishable behavior;
- each faction has strengths, weaknesses, and viable counters;
- no single faction dominates across the full map suite.

## P2 — Persistent world layer

Evaluate whether matches should remain independent or feed a larger world with
territory, diplomacy, adaptation, and faction history. This is intentionally
deferred until the core match simulator is fast and measurable.
