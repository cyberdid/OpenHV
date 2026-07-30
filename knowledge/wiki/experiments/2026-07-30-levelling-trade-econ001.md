---
title: Levelling Trade ECON-001
status: current
updated: 2026-07-30
sources:
  - ../../raw/experiments/2026-07-30-econ001-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-30-econ001-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-30-econ001-candidate-112-v1-trade.csv
  - ../../../OpenRA.Mods.HV/Traits/World/TradeManager.cs
  - ../../../batch-manifests/econ001-candidate-112-v1.json
  - 2026-07-30-regroup-locality-ai005.md
tags:
  - experiment
  - economy
  - trade
---

# Levelling Trade ECON-001

## Purpose

Trade had never moved a single unit of anything.

Across the 112-match baseline, 672 routes carried zero food, zero materials and
zero energy. Routes were created, distance and risk were computed, status
flipped between active and suspended, and the result file recorded all of it —
with every cargo field at zero.

## The cause

A shipment was capped by how far the destination sat below an absolute reserve
floor:

```
amount = min(capacity, sourceStock - sourceReserve, reserveTarget - destinationStock, freeStorage)
```

No settlement was ever below that floor.

| Resource | Floor | 10th percentile stock | Poorest settlement recorded |
|---|---|---|---|
| Food | 200 | 838 | 600 |
| Materials | 160 | 725 | 408 |
| Energy | 160 | 2,879 | 263 |

The poorest settlement in 448 civilisations still held more than its floor, so
the deficit term was always zero and the whole rule collapsed to "ship nothing".
The floor was sized for an economy that no longer exists: per-pulse demand is
25 food against holdings of roughly a thousand.

## Implementation

Trade now moves half the gap between the two holdings rather than only topping
a partner back up to a floor, still capped by the exporter's own reserve, the
importer's free storage, and route capacity. Half, so one shipment cannot
overshoot and make the exporter the poorer of the two.

One variable: the reserve floor, capacities, risk, and the suspension rules are
untouched.

## Method

Baseline `baseline-112-v2` at commit `60c54a75`. The two commits between it and
the candidate add match-time announcements and the Relations panel; both are
display-only and were verified inert by synchronized state hash `D4CB3490`
before this change went in. Both runs completed 112/112 on attempt 1, in 523.8
and 519.9 wall seconds, 448 paired observations.

```sh
python3 run-batch.py batch-manifests/econ001-candidate-112-v1.json
python3 compare-candidate.py \
  ../simulation-runs/baseline-112-v2 \
  ../simulation-runs/econ001-candidate-112-v1
```

## Result: accepted

Trade exists now.

| | Baseline | Candidate |
|---|---|---|
| Routes | 672 | 672 |
| Goods moved | **0** | **161,936** |
| Median per route | 0 | 251 |
| Active / suspended | 416 / 256 | 435 / 237 |

Forty of eighty paired differences are significant, and they tell one story:
the three profiles that trade got richer and much less bloody, and the one that
does not pay for it.

| | Aggressor | Economist | Technologist | Fortress |
|---|---|---|---|---|
| Score | **−11,913** | +778 | **+7,666** | +4,873 |
| Stability | **−180** | **+24** | **+157** | **+35** |
| Army | **−1,941** | **+3,168** | **+4,903** | **+4,014** |
| Deaths | **+2,559** | **−4,886** | **−8,227** | **−5,068** |
| Active wars | **+1.69** | **−0.20** | **−1.52** | **−0.22** |
| War casualties | **+2.57** | **−8.79** | **−14.60** | **−8.36** |

Bold entries clear the 95% bootstrap interval.

Technologist also completed +0.34 more technologies, the only research movement
any candidate has produced so far.

## Interpretation

Interdependence is now expensive to break. A profile that trades keeps its army
alive, loses far fewer people and is drawn into fewer wars; Technologist alone
sheds 1.5 active wars and 14.6 casualties per match. The war load did not
disappear — it moved onto Aggressor, who gains 1.69 wars and 2,559 deaths and
loses 11,913 score. That is the model behaving as a model of trading societies
should: the one who will not trade ends up fighting everybody.

No profile's score-lead rate moved. All four intervals straddle zero, so who
tends to be ahead is unchanged even though the magnitudes moved hard. This is a
world that behaves differently, not a rebalanced scoreboard.

Neither standing acceptance target moved. All 112 matches still end at the tick
ceiling and collapses went 18 to 19. That was not what this candidate was for,
and it remains untouched by anything tried so far.

## Limitations

Only the four military profiles on the four held-out maps. Half-the-gap is one
levelling rule among several plausible ones and was not compared against
alternatives; a slower or faster rate was not measured. Aggressor's regression
is large and was accepted on the grounds that its score-lead rate is unchanged,
which is a judgement about what the score measures rather than a measurement.

## Next

Research is the next system in the same condition: three of four profiles
finish exactly one technology in a whole match, `logistics` is completed 4
times in 448 civilisations, and `civil-engineering` never at all, so two of the
five nodes are unreachable. Knowledge production is 1.6 to 2.2 per pulse for
every profile except Technologist at 8.0.

## Related pages

- [Regroup Locality AI-005](2026-07-30-regroup-locality-ai005.md)
- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
- [Living Factions Design](../faction-life.md)
