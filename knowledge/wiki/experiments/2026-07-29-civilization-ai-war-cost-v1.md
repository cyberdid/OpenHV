---
title: Civilization AI and War Cost v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-civilization-ai-war-cost-v1.csv
  - ../ai-profiles.md
  - ../faction-life.md
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
tags:
  - experiment
  - ai
  - war-cost
  - recovery
  - determinism
---

# Civilization AI and War Cost v1 Validation

## Purpose

Validate the first multi-objective Civilization AI loop and make military
policy visible in civil state. The test asks whether shortages, research,
trade dependency, relative power, active wars, mobilization, casualties,
stability, and recovery change synchronized strategic choices and diplomatic
outcomes.

The immutable results are in
[2026-07-29-civilization-ai-war-cost-v1.csv](../../raw/experiments/2026-07-29-civilization-ai-war-cost-v1.csv).
Runs used base commit `68474e7f` with the Civilization AI working tree; the
published implementation is the commit containing this record.

## Implemented decision model

Every 250 ticks, each `CivilizationState` computes 0–1000 utilities for:

- survival from food and prosperity pressure;
- research from remaining technologies, personality, crisis, and war;
- trade from shortages, import dependency, and Economist identity;
- security from active wars, stability, and relative power;
- recovery from prosperity/stability damage and recent/cumulative casualties;
- war from personality, relative power, shortages, instability, bilateral
  trade dependency, research commitment, and prior casualties.

The chosen synchronized strategy is one of `development`, `survival`,
`research`, `trade`, `mobilization`, or `recovery`. Transitions emit
`strategy-transition` events. Bilateral war pressure now comes from this
utility model rather than a fixed bot-type grievance constant.

Research consumes the settlement's real knowledge plus physical materials and
energy. Survival/recovery limits new research spending; mobilization limits it
to half of the current remaining target.

## Civil cost of military power

Standing armies reserve some adult workforce; active war increases that
mobilization ratio. Available workforce, not total adults, determines
employment and scales food, materials, energy, and knowledge production.
New combat death value converts deterministically into adult population
casualties at the capital. Active wars and casualties reduce the stability
target and increase migration pressure. Peace removes wartime mobilization,
but damaged factions select recovery until stability returns.

## Results

Four peaceful Steward factions selected `research`, spent 101 materials and
101 energy in total, completed agricultural systems, grew to combined
population 4,048, and stayed at stability 998–999 with zero mobilization.

The paired trade-dependency case used the same map, seed 8005, asymmetric
production, and Aggressor/Economist composition:

- with trade, three factions selected `trade`, dependency reached 616, all six
  relationships remained neutral, population ended at 4,006, and minimum
  stability was 893;
- without trade, dependency stayed zero, one relationship entered war, two
  factions selected `mobilization` and two `survival`, population ended at
  3,938, and minimum stability fell to 678.

This is not a pure estimate of the trade benefit alone—the diplomatic path is
part of the intended causal mechanism—but it demonstrates that real bilateral
dependency can rationally suppress war pressure.

The paired seed-8003 war runs each declared five wars at tick 5,000. All four
factions transitioned from research to mobilization. At tick 8,000 they had
382 mobilized adults, only 2,186 available workers, 32 population casualties,
and stability 656–773. Both runs produced hash `8431018E` and identical final
metrics/events.

In the 12,000-tick recovery case, five wars began at tick 5,000 and all ended
through exhaustion at tick 10,000. Every faction entered `recovery` at peace,
then returned to `research` between ticks 10,500 and 11,000 as stability rose
to 961–966. The societies retained 77 cumulative war casualties and 149
standing-army mobilized adults; peace did not erase history.

All result and JSONL artifacts passed Schema v1. The new implementation added
no analyzer warning.

## Limits and next work

The first planner changes research pace and diplomatic pressure; it does not
yet rewrite OpenRA tactical build queues, retreat logic, or target selection.
Army value is an abstract personnel proxy, and combat death value converts to
population with a fixed initial ratio. Multi-settlement casualty allocation,
military occupation, health, migration, faction institutions, and learned
weights remain later work.

Next: finish SIM-009 lifecycle semantics so observation horizons, faction
collapse, and advisory stalemate preserve these civil histories without
inventing a winner.
