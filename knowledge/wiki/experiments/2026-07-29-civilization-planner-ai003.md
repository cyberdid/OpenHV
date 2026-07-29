---
title: Civilization Planner AI-003
status: current
updated: 2026-07-29
sources:
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../../OpenRA.Mods.HV/Traits/BotModules/CivilizationPlannerBotModule.cs
  - ../../../OpenRA.Mods.HV/Simulation/SimulationTelemetryWriter.cs
  - ../../../batch-manifests/ai003-candidate-112-v1.json
  - ../../../compare-candidate.py
  - 2026-07-29-baseline-112-v1.md
tags:
  - experiment
  - ai
  - planning
  - civilization
  - determinism
---

# Civilization Planner AI-003

## Purpose

Convert the synchronized Civilization AI utilities and strategy state into
observable, profile-specific decisions that affect both faction life and RTS
production. The baseline showed that Economist and Technologist were names and
weight presets more than distinct civil plans: Economist ended mobilized in
111/112 observations and Technologist gained no research edge.

## Implementation

The planner has four explicit states:

| Plan | Entry rule | Civil effect | Bounded RTS requests |
|---|---|---|---|
| Opening | before tick 3,000 | +5% food, +10% materials | one miner request; Economist may invest twice |
| Economy | Economist doctrine or normal development | +10% food/energy, +15% materials, −20% knowledge | up to four Economist miner requests; two for other profiles |
| Technology | Technologist doctrine or Steward research | −10% materials/energy, +60% knowledge | two each of technician, observer, and radar tank |
| Recovery | survival/recovery strategy or stability below 650 | +20% food, −10% materials, −40% knowledge | up to two repair tanks and four cumulative miners |

Research order is also profile-specific. Technologist starts with energy-grid
and research-networks; Economist prioritizes agriculture, logistics, and civil
engineering; Fortress prioritizes energy security before food; other profiles
use agriculture/logistics first. An in-progress technology is never silently
switched.

Requests use OpenRA's native `IBotRequestUnitProduction` interface. A packed,
synchronized four-bit counter per requested actor caps cumulative planner
investment. This limit was added after the first smoke exposed repeated
builder orders: deployed builders disappear as units, so an ownership target
alone treated every successful expansion as a shortage.

## Observability

Result and telemetry civilization snapshots add:

- plan and reason;
- plan sequence and transition tick;
- planner request sequence;
- last request tick and actor type.

Events add `planner-transition` and `planner-request`, each with stable reason
code, selected plan, and sequence. Schema additions are optional so the
committed pre-AI-003 baseline remains readable.

## Smoke and determinism

Two independent 12,000-tick Cold Rage runs used the same four profiles and
seed `830001`. Both ended at the declared tick limit with synchronized hash
`DEB138AE` and identical plan, request, research, population, and score state.

The reviewed run emitted:

- opening transitions for all four profiles at tick 0;
- Economist economy, Technologist technology, and development economy
  transitions at tick 3,000;
- later crisis/recovery transitions driven by synchronized war damage;
- bounded request totals of 4 Aggressor, 5 Economist, 6 Technologist, and 3
  Fortress requests.

The Technologist collapsed in this single seed, so the smoke is correctness
evidence rather than promotion evidence.

## Held-out gate

`ai003-candidate-112-v1.json` reuses the exact 112 baseline matches: map, bot
order, seed, tick horizon, telemetry interval, trade setting, and lifecycle
thresholds are unchanged. `compare-candidate.py` additionally rejects any
paired faction or spawn mismatch before calculating candidate-minus-baseline
bootstrap intervals for technology, population, stability, army, casualties,
score, collapse, final plans, and request counts.

AI-003 remains open until this clean-commit batch and paired report are
published. Passing requires measurable plan specialization without increasing
infrastructure failure, collapse, or the already-failed natural-outcome
cutoff. A failed behavioral hypothesis will be recorded and revised rather
than hidden.
