---
title: Civilization Planner AI-003
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-ai003-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-29-ai003-candidate-112-v1-metrics.csv
  - ../../raw/experiments/2026-07-29-ai003-candidate-112-v2-run.csv
  - ../../raw/experiments/2026-07-29-ai003-candidate-112-v2-metrics.csv
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../../OpenRA.Mods.HV/Traits/BotModules/CivilizationPlannerBotModule.cs
  - ../../../OpenRA.Mods.HV/Simulation/SimulationTelemetryWriter.cs
  - ../../../batch-manifests/ai003-candidate-112-v1.json
  - ../../../batch-manifests/ai003-candidate-112-v2.json
  - ../../../batch-manifests/ai003-candidate-112-v3.json
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
| Opening | before tick 3,000 | rounded +5% food, +10% materials | one miner request |
| Economy | Economist doctrine or normal development | rounded +10% food/energy, +15% materials; ordinary knowledge preserved | up to two Economist miner requests; one for other profiles |
| Technology | Technologist doctrine or Steward research | rounded −5% materials/energy, +200% knowledge | one each of technician, observer, and radar tank |
| Recovery | survival/recovery strategy or stability below 650 | rounded +20% food, −10% materials, −40% knowledge | one repair tank and up to two cumulative miners |

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

## Candidate v1 result

The clean `bfee9494` run completed 112/112 matches on attempt 1 in 795.208
seconds. All 112 results, 1,456 snapshots, and 11,388 events validated; four
sampled replays reported `FinalGameTick=12000`. Compact immutable evidence is
in the [run record](../../raw/experiments/2026-07-29-ai003-candidate-112-v1-run.csv)
and [paired metrics](../../raw/experiments/2026-07-29-ai003-candidate-112-v1-metrics.csv).

The implementation achieved visible specialization but not the desired
mechanism:

- Technologist ended in `technology` in 95/112 observations and completed
  exactly 1.0 technologies on average;
- the other profiles fell from about one technology to 0.00–0.04 because the
  −20% knowledge multiplier rounded a one-unit production pulse down to zero;
- Technologist itself gained only +0.009 technologies versus baseline
  (bootstrap 95% 0.000 to 0.027), while casualties rose by 4.268
  (1.151 to 7.483) and active wars by 0.134 (0.027 to 0.250);
- Economist prosperity fell 20.875 (−48.108 to −2.214), stability fell 17.250
  (−40.752 to −0.205), available workforce fell 18.688
  (−35.325 to −4.062), and two societies collapsed;
- total collapses improved from 15 to 13, Aggressor from 12 to 9, and Fortress
  from one to zero, but every match still hit the tick ceiling.

The root cause is twofold. Integer floor multiplication accidentally removed
ordinary knowledge production, and economic/technical support units inflated
`ArmyValue`, which the civil model interpreted as military mobilization. The
native request pulse also displaced more normal combat production than the
first hypothesis allowed.

Decision: candidate v1 is a documented failed promotion. AI-003 remains open.
The next candidate must use positive-value rounded production, preserve
ordinary research, exclude explicit civilian/support actors from mobilization,
and lower planner request budgets. It must create a real Technologist gain
without the significant Economist wellbeing or Technologist casualty
regressions.

## Candidate v2 correction

The corrected implementation applies division-round-up only to positive plan
production, so a one-unit knowledge pulse survives an economy plan. Technology
now raises knowledge to 300% while charging only a 5% materials/energy
opportunity cost. Native request budgets are one opening miner, at most two
Economist miners, and one of each technical/support actor.

Civil mobilization now subtracts the value of miners, builders, technicians,
observers, brokers, and tankers before converting `ArmyValue` into mobilized
adults. These actors still cost money and occupy real production queues, but
the society no longer mistakes them for soldiers.

Two identical corrected 12,000-tick smoke runs matched hash `9419B52D`.
Aggressor, Economist, and Fortress each completed their profile-specific first
technology; Technologist completed energy-grid, research-networks, and
agriculture, survived the seed that collapsed under v1, and issued four
bounded requests. The clean exact-schedule v2 batch remains the promotion
gate.

## Candidate v2 result

The clean `0afb8b5c` candidate completed 112/112 attempt-1 matches in 804.568
seconds. All 112 results, 1,456 snapshots, and 10,959 events validated, and
four sampled replays ended at tick 12,000. See the immutable
[run record](../../raw/experiments/2026-07-29-ai003-candidate-112-v2-run.csv)
and [paired metrics](../../raw/experiments/2026-07-29-ai003-candidate-112-v2-metrics.csv).

v2 fixed the v1 research/wellbeing failure:

- Technologist completed 2.455 technologies, +1.464 over baseline
  (bootstrap 95% 1.366 to 1.571);
- Economist prosperity rose 41.473 (21.651 to 53.750), stability rose 45.777
  (27.035 to 56.938), available workforce rose 122.205
  (106.561 to 135.914), and mobilization fell 126.714;
- total collapse count matched baseline at 15; all score-lead shares remained
  within the provisional 10–40% four-way range.

It still fails promotion because military/diplomatic regressions reveal an
inconsistent power definition. Technologist army fell 1,635
(−2,911 to −420), casualties rose 7.661 (4.723 to 10.911), and active wars rose
0.705 (0.607 to 0.804). Fortress army fell 1,760
(−2,758 to −759), casualties rose 3.661 (1.973 to 5.215), active wars rose
0.714 (0.625 to 0.804), and stability fell 38.725
(−55.511 to −17.024).

Mobilization subtracts civilian/support value, but diplomacy's relative-power
calculation still uses raw `PlayerStatistics.ArmyValue`. The same worker and
support investment is therefore “civilian” to production but “military” to
war pressure. Candidate v2 is retained but not promoted. v3 will centralize
military army value and use it consistently in both systems without changing
plan bonuses or request budgets.

## Candidate v3 consistency correction

v3 introduces one shared `MilitaryArmyValue` calculation. It subtracts the
same explicit civilian/support actors from raw army value before both
workforce mobilization and diplomatic relative-power scoring. Plan output,
technology order, request budgets, and tactical profile weights are unchanged.

Two 12,000-tick smoke runs matched synchronized hash `9419D0D9` and preserved
the corrected v2 research and civil outputs. The exact 112-match v3 matrix is
the final AI-003 promotion gate; any remaining combat target/retreat problem
will be handed to AI-004 only if the power-definition mismatch is removed.
