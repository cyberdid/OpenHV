---
title: Scenario Lifecycle v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-scenario-lifecycle-v1.csv
  - ../../../OpenRA.Mods.HV/Simulation/SimulationLifecycleMonitor.cs
  - ../../../OpenRA.Mods.HV/Simulation/SimulationConfig.cs
  - ../../../OpenRA.Mods.HV/LoadScreens/PanelLoadScreen.cs
tags:
  - experiment
  - lifecycle
  - collapse
  - stalemate
  - determinism
---

# Scenario Lifecycle v1 Validation

## Purpose

Validate SIM-009's distinction between a finite conflict, a living-world
observation, faction collapse, a hard synchronized safety ceiling, and a
conservative stalemate policy. No cutoff may invent a natural winner, and one
faction's civil collapse must not automatically stop the surviving societies.

The immutable summary is
[2026-07-29-scenario-lifecycle-v1.csv](../../raw/experiments/2026-07-29-scenario-lifecycle-v1.csv).
Runs used base commit `9ba87a35` with the scenario-lifecycle working tree; the
published implementation is the commit containing this record.

## Lifecycle contract

`scenarioMode=conflict` uses `maxWorldTicks` as a hard deterministic ceiling.
`scenarioMode=living-world` additionally requires a positive
`observationHorizonTicks` no greater than that ceiling. Reaching the
observation horizon writes `observation-horizon`, an empty `naturalWinners`
array, and the synchronized state exactly at that tick.

The monitor records a faction collapse when OpenRA marks it lost or when both
its population and weighted settlement stability are at or below configured
thresholds. Civil thresholds are checked every 250 synchronized ticks. A
partial collapse is evidence, not a global end condition; only collapse of
every active bot ends the scenario as `faction-collapse`.

The stalemate detector compares synchronized earned/spent resources,
kills/deaths, army/assets, population/stocks, research/technology, and trade
shipment progress. Any active bilateral war suppresses the detector. A
detected quiet window is advisory by default; it terminates only when
`stalemateTerminates=true` is explicitly configured. This deliberately favors
false negatives over stopping a living or fighting world.

## Commands

The observation repeat used:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=steward \
SIMULATION_SEED=8101 \
SIMULATION_MAX_TICKS=5000 \
SIMULATION_SCENARIO_MODE=living-world \
SIMULATION_OBSERVATION_HORIZON_TICKS=2000 \
SIMULATION_STALEMATE_WINDOW_TICKS=500 \
SIMULATION_TELEMETRY_INTERVAL_TICKS=250 \
SIMULATION_RESULT=<artifact-dir>/result.json \
./run-simulation.sh coldrage
```

The active-combat guard used Aggressor/Fortress, seed 8102, a 6,000-tick
conflict ceiling, trade disabled, a 500-tick stalemate window, and
`SIMULATION_STALEMATE_TERMINATES=true`.

The partial-collapse case used the trade profile without trade, seed 7901, a
4,000-tick living-world observation, and population/stability thresholds
990/850. The all-collapse fixture deliberately raised those thresholds to
1,100/1,000 so every faction met the condition at tick zero.

## Results

The two observation runs ended at tick 2,000 as `observation-horizon`, had no
natural winner, and matched synchronized hash `5578A2DB`. Thus the declared
observation boundary is deterministic and distinct from the 5,000-tick hard
limit.

In the partial-collapse run, exactly two Steward societies reached population
984 and crossed the stability threshold at tick 3,000. Both retained assets
worth 7,900 and all four OpenRA outcomes remained `undefined`. The world
continued for another 1,000 ticks and ended only at its observation horizon,
hash `7C0D04A9`.

The explicit all-collapse fixture recorded all four factions and ended at tick
zero as `faction-collapse`, hash `27686403`, again with no natural winner.

The active-combat guard contained five war relations at tick 6,000. Despite
terminating stalemate mode, its advisory stayed false, last meaningful
activity advanced to tick 6,000, and the scenario ended as
`world-tick-limit`, hash `6A1920FB`. It was not incorrectly stopped as a
stalemate.

All five results, 61 telemetry records, and 160 event records passed their
Schema v1 validators. The Release build completed with zero warnings, and all
nine batch-runner integration tests passed.

## Interpretation and limits

SIM-009 now provides reproducible observation, partial-collapse,
all-collapse, active-war guard, and hard-ceiling evidence. A positive
stalemate fixture was intentionally not manufactured by suppressing real
civil activity: continuously changing stocks, population, research, trade, or
combat mean the world is not semantically idle. The first later naturally
saturated scenario that raises an advisory must be replay-reviewed before
changing the advisory-first default.

The deliberately high all-collapse thresholds test termination plumbing, not
a balanced definition of societal failure. Default thresholds 250/250 remain
provisional and require long-horizon distribution evidence. Natural victory
also remains a separate long conflict fixture for the SIM-010 suite.

Next: run the statistically useful civil/military baseline with explicit
end-reason, collapse, prosperity, diplomacy, trade, and war-cost aggregates.
