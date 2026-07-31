---
title: Symmetric Trade Restraint DIP-004
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-dip004-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-dip004-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-31-dip004-candidate-112-v1-pairings.csv
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - 2026-07-31-relative-power-dip003.md
tags:
  - experiment
  - diplomacy
  - trade
---

# Symmetric Trade Restraint DIP-004

## Purpose

Trade dependency is the largest brake on war — the instrument had it carrying
399 decisions of 1,344 — and measuring it showed the brake grips only one side
of a pair. It was the share of a route's traffic flowing *inward*, so across
1,344 observed sides 36% received more than they sent and carried the full
restraint while 36% only sent and carried none at all. War needs the sum of
both sides to cross the threshold, so an unrestrained exporter drags the pair in
regardless of what flows between them.

## Implementation

Volume instead of direction, so one route holds both partners:

```
Ratio(incoming, incoming + outgoing + 100)  ->  Ratio(volume, volume + 537)
```

537 was taken from the data, not picked: median route volume is 262, and 537
puts the median restraint at 328 per mille, exactly what the old form produced.
The intent was to change the shape and leave the strength alone.

## Method

Baseline `dip003-candidate-112-v1`, the behaviour this candidate was cut from.
Both runs completed 112/112, 448 paired observations.

## Result: rejected

| | Baseline | Candidate |
|---|---|---|
| Collapsed factions | 30 | **25** |
| End reasons | 112 tick limit | 112 tick limit |

Active wars fell significantly for three profiles — Aggressor −0.36, Economist
−0.38, Technologist −0.17 — and collapses fell with them. DIP-003 had just
moved collapses from 22 to 30, the first candidate to make the world more
decisive; this gives a third of that back.

No score-lead rate moved, so nothing is unbalanced. It simply goes the wrong
way on the one metric that had started to move.

## Interpretation

Holding the median held the wrong statistic.

| | 5th pct | median | 95th pct | strongest |
|---|---|---|---|---|
| Old, by direction | −358 | −164 | −2 | **−422** |
| New, by volume | −210 | −158 | −68 | **−272** |

The medians match to six points, exactly as designed. But the old form's work
was done in its tail: a heavily dependent partner carried −422, enough to veto
a war on its own, and the new form cannot reach past −272 no matter how much
trade flows. Making the restraint symmetric spread it across both partners and
in doing so capped it, and trade dependency's share of carried decisions fell
from 399 to 291 while disposition's rose from 785 to 891.

So a veto term is not characterised by its median. DIP-002 held a mean and that
was the right invariant, because the question there was about spread. Here the
question was about how hard the brake can grip, and the mean says nothing about
that.

## Limitations

Only the four military profiles on the four held-out maps. One scale constant
was tested. Reverted after measurement.

## Next

The asymmetry is still real and still worth removing — an exporter with
everything to lose has no restraint at all today. But a v2 has to preserve the
tail, not the median: scale so that a partner at the 95th percentile of volume
reaches the same −358 the old form gave a partner at the 95th percentile of
dependence, and let the median fall where it lands.

## Related pages

- [Relative Power DIP-003](2026-07-31-relative-power-dip003.md)
- [Compressed Disposition DIP-002](2026-07-31-compressed-disposition-dip002.md)
- [Levelling Trade ECON-001](2026-07-30-levelling-trade-econ001.md)
