---
title: Reachable Technology ECON-002
status: current
updated: 2026-07-30
sources:
  - ../../raw/experiments/2026-07-30-econ002-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-30-econ002-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-30-econ002-candidate-112-v1-technologies.csv
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../../batch-manifests/econ002-candidate-112-v1.json
  - 2026-07-30-levelling-trade-econ001.md
tags:
  - experiment
  - economy
  - research
---

# Reachable Technology ECON-002

## Purpose

Two of the five technologies had never been reached.

On the build that ECON-001 left behind, 322 of 448 civilisations ended a match
holding exactly one technology. `logistics` had been completed 15 times,
`civil-engineering` not once by anyone in 112 matches.

## The cause

Arithmetic, not a broken rule. The tree costs 400 knowledge. Research pulses
every 250 ticks, so a 12,000-tick match allows 48 of them.

| Profile | Knowledge per pulse | Knowledge per match | Technologies |
|---|---|---|---|
| Aggressor | 1.7 | ~82 | 1.01 |
| Economist | 1.9 | ~91 | 1.02 |
| Fortress | 2.1 | ~101 | 1.17 |
| Technologist | 8.7 | ~418 | 2.73 |

Ninety-one knowledge buys the first node at 40 and nothing else. There are in
fact two separate gates: everyone but the Technologist is short of knowledge,
while the Technologist can afford the whole tree and is instead throttled by
the per-strategy spending limit, which allows only half of what a node needs
per pulse while the profile sits in Mobilization.

## Implementation

Costs halved, `40, 60, 80, 100, 120` to `20, 30, 40, 50, 60`.

One variable. Knowledge production, the research interval, the spending
throttle and the per-profile priority orders are untouched, so the Technologist
was not expected to move as far as the rest.

## Method

Baseline `econ001-candidate-112-v1`, which is the behaviour of the build this
candidate was cut from. Both runs completed 112/112 on attempt 1, in 519.9 and
521.3 wall seconds, 448 paired observations.

```sh
python3 run-batch.py batch-manifests/econ002-candidate-112-v1.json
python3 compare-candidate.py \
  ../simulation-runs/econ001-candidate-112-v1 \
  ../simulation-runs/econ002-candidate-112-v1
```

## Result: accepted

The whole tree is live.

| Technology | Before | After |
|---|---|---|
| agricultural-systems | 321 | 444 |
| energy-grid | 224 | 240 |
| logistics | 15 | **378** |
| civil-engineering | **0** | **109** |
| research-networks | 104 | 112 |

Nobody now ends a match on one technology, and 105 civilisations complete all
five.

| Technologies held | Before | After |
|---|---|---|
| 1 | 322 | 0 |
| 2 | 48 | 272 |
| 3 | 66 | 70 |
| 4 | 12 | 1 |
| 5 | 0 | **105** |

Every profile gained significantly, and the profile built for research gained
most, which is what a research profile should do:

| | Aggressor | Economist | Technologist | Fortress |
|---|---|---|---|---|
| Technologies | **+1.13** | **+1.02** | **+2.12** | **+1.26** |
| Score | −605 | **−3,434** | **+3,485** | +206 |
| Stability | +16 | **+15** | +15 | +17 |
| Active wars | **−0.29** | **−0.15** | −0.04 | **−0.20** |

Bold entries clear the 95% bootstrap interval. No profile's score-lead rate
moved.

## Interpretation

Research now differentiates the profiles instead of flattening them. The
Technologist doubles its lead in nodes completed and is the only profile whose
score rises significantly, so the identity finally shows up in the outcome
rather than only in the priority table.

Wars fell slightly for the three non-research profiles. That is consistent with
where the technology effects land — more food and energy production, more
storage, more housing — since a faction that is not short of anything has less
to take.

The Economist lost 3,434 score. Research spends materials and energy, and the
Economist's score leans on accumulating exactly those, so the profile that
banks resources pays for spending them. Recorded, not argued away.

Neither standing acceptance target moved. All 112 matches still end at the tick
ceiling and collapses went 19 to 21. Four candidates have now failed to move
it.

## Limitations

Only the four military profiles on the four held-out maps. Halving is one scale
factor among many and no other was measured. The Technologist's real gate — the
Mobilization spending throttle — was deliberately left alone, so its result
understates what a research profile could reach.

## Next

The technology effects themselves are untested: each node applies a production
or storage percentage, but nothing has measured whether those percentages are
worth their cost now that the tree can be finished.

The tick ceiling has survived AI-003, AI-004, AI-005, ECON-001 and ECON-002. It
is not a combat-tuning problem and it is not an economy problem; matches do not
end because nothing in the scenario ever forces one to.

## Related pages

- [Levelling Trade ECON-001](2026-07-30-levelling-trade-econ001.md)
- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
- [Living Factions Design](../faction-life.md)
