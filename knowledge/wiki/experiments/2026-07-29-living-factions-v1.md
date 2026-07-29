---
title: Living Factions and Telemetry v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-living-factions-v1.csv
  - ../../../batch-manifests/living-factions-v1.json
  - ../../../schemas/simulation-result-v1.schema.json
  - ../../../schemas/simulation-telemetry-v1.schema.json
  - ../../../schemas/simulation-event-v1.schema.json
  - ../faction-life.md
tags:
  - experiment
  - civilization
  - population
  - telemetry
  - determinism
---

# Living Factions and Telemetry v1 Validation

## Purpose

Validate the first synchronized civil vertical slice and its observation
boundary:

- settlement-level cohorts, stocks, production, jobs, needs, and demographic
  consequences;
- balanced growth and an explicit food-scarcity control;
- a non-attacking Steward AI that can develop without mandatory combat;
- deterministic state with telemetry enabled or disabled;
- JSON Schema conformance and telemetry overhead below 10%.

The code under test was the Sprint 4 working tree based on
`56cb5943d127cead45c54957f5cabd46a1267af8`. The exact results are preserved
in the immutable [raw CSV](../../raw/experiments/2026-07-29-living-factions-v1.csv);
the published implementation is the commit containing this record.

## Fixtures and commands

All matches used Cold Rage, headless deterministic execution, a world-tick
horizon, and a 120-second wall-clock watchdog. The checked-in
[Living Factions manifest](../../../batch-manifests/living-factions-v1.json)
reproduces the three behavioral cases.

Representative direct launch:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=steward \
SIMULATION_SEED=7501 \
SIMULATION_MAX_TICKS=3500 \
SIMULATION_WATCHDOG_SECONDS=120 \
SIMULATION_TELEMETRY_INTERVAL_TICKS=250 \
SIMULATION_CIVILIZATION_PROFILE=balanced \
SIMULATION_RESULT=/tmp/result.json \
./run-simulation.sh coldrage
```

The scarcity control changed only
`SIMULATION_CIVILIZATION_PROFILE=scarcity`. The determinism pair used seed
7401 and identical synchronized configuration; one run sampled every 250
ticks and the other disabled telemetry. The overhead pair used Steward AI,
seed 7601, 10,000 ticks, and `/usr/bin/time -p`.

## Metric definitions

- `population`: sum of synchronized children, adults, and elders.
- `workforce`: adult cohort; `employed` is capped by available jobs.
- `production`: integer units created per 250-tick civil pulse.
- `demand`: integer units consumed per civil pulse.
- `satisfaction`: fulfilled demand/capacity on a 0–1000 scale.
- `prosperity`: mean of food, housing, energy, and employment satisfaction.
- `stability`: one-quarter movement toward current prosperity per pulse.
- `migrationPressure`: `max(0, 1000 - prosperity)`.
- `populationDelta`: births minus baseline and shortage mortality on the
  3,000-tick demographic pulse.
- telemetry overhead: paired wall/CPU time with the synchronized hash used as
  a correctness guard.

Stocks and production are deliberately separate: a stored unit is not counted
as current output. Credits remain the existing OpenHV treasury and do not
silently replace food, materials, or energy.

## Results

Balanced Rogue AI reached tick 3,500 with hash `D8692141`. All four
settlements grew from 1,000 to 1,006 residents after one demographic pulse,
reported food satisfaction 1000, and emitted 15 snapshots plus founding,
population-change, and match lifecycle events. Repeating with telemetry
disabled produced the same hash and exactly equal final civilization objects.

The scarcity profile reached tick 3,500 with hash `9089031D`. Each settlement
exhausted food, crossed the shortage threshold at tick 2,250, reached food
satisfaction 280, and fell from 1,000 to 992 residents at tick 3,000.
Telemetry emitted explicit `food-shortage` and `mortality` reason codes.

Four Steward AIs reached tick 3,500 with hash `2D651694`. Every faction grew
to 1,006 residents and increased its infrastructure/assets while all kill,
loss, and destroyed-building counters remained zero. This proves that the
current world can remain behaviorally active without an AI attack loop; it
does not yet implement neutral diplomatic relationships.

The 10,000-tick overhead pair produced the same synchronized hash
`748B3676`:

| Mode | Wall seconds | User CPU seconds | Artifacts |
|---|---:|---:|---|
| Telemetry off | 11.12 | 6.90 | final result |
| Every 250 ticks | 11.06 | 6.94 | 41 snapshots, 18 events, final result |

Observed wall time was 0.5% lower due to measurement noise; user CPU increased
0.58%. Both are safely inside the 10% gate.

All result, telemetry, and event records passed their Draft 2020-12 schemas.
The batch integration suite also passed all seven retry/resume/signal tests,
including preservation of prior telemetry and event artifacts.

## Interpretation

LIFE-001–003 are operational: population is no longer a design-only concept,
existing buildings make explicit civil contributions, and shortages have
deterministic demographic consequences. SIM-008 now has a durable JSONL
foundation and civil event reasons without altering synchronized behavior.

The Steward fixture demonstrates peaceful development at the AI-policy level.
Default-neutral relationships, trade, explicit war transitions, migration
between settlements, casualty-to-workforce coupling, research unlocks, and
the wider tactical event catalog remain later gates.

## Limitations and next experiment

- The current civil economy is one-capital-first; nearest-settlement ownership
  is deterministic, but multi-settlement founding is not yet exercised.
- `health`, `safety`, `culture`, territory, trade, and diplomacy are not
  implemented.
- Knowledge accumulates but does not yet unlock a technology graph.
- The 10,000-tick overhead pair is a local paired measurement, not a
  multi-machine benchmark.
- Peace is achieved by an AI policy with no attack squads, not by neutral
  OpenRA targeting semantics.

The next experiment should validate a small research graph and then introduce
neutral/war/peace transitions before measuring trade under asymmetric
scarcity.
