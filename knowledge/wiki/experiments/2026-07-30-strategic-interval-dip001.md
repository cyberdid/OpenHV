---
title: Strategic Interval DIP-001
status: current
updated: 2026-07-30
sources:
  - ../../raw/experiments/2026-07-30-dip001-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-30-dip001-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-30-dip001-candidate-112-v1-pairings.csv
  - ../../../OpenRA.Mods.HV/Traits/World/DiplomacyManager.cs
  - ../../../batch-manifests/dip001-candidate-112-v1.json
  - 2026-07-30-reachable-technology-econ002.md
tags:
  - experiment
  - diplomacy
---

# Strategic Interval DIP-001

## Purpose

Every war in the simulation involved Aggressor.

| Pairing | Matches that ever went to war |
|---|---|
| aggressor / technologist | 100% |
| aggressor / economist | 92.9% |
| aggressor / fortress | 66.1% |
| fortress / technologist | 4.5% |
| economist / technologist | 0.9% |
| economist / fortress | 0.9% |

Probed at six times the length, matches still ended on the clock: exactly one
faction was eliminated in each of four seeds and the three survivors had never
declared war on one another at all, still carrying `transitionSequence` 0 and
their initial neutrality after 72,000 ticks. Conflict was a property of one
profile, so once it was gone the world had nothing left to resolve.

## The cause

`StrategicInterval` was 5000 ticks against a 12,000-tick match, so the whole
diplomatic system ran **twice**. Grievance is written to accumulate across a
match and two samples cannot accumulate anything: the 400-point war threshold
was reachable only by whichever profile carried the largest constant baseline.

| Profile | Pressure baseline | Grievance held at match end |
|---|---|---|
| Aggressor | 450 | 244 |
| Economist | 150 | 110 |
| Technologist | 250 | 87 |
| Fortress | 100 | 46 |

## Implementation

`StrategicInterval` 5000 to 1000, a twelfth of the match instead of nearly
half. One variable: the baselines, the war threshold, the exhaustion rule and
the peace cooldown are untouched.

## Method

Baseline `econ002-candidate-112-v1`. Both runs completed 112/112 on attempt 1,
448 paired observations.

## Result: rejected

The diagnosis was right and the outcome is wrong.

| Pairing | Before | After |
|---|---|---|
| aggressor / economist | 92.9% | **100%** |
| aggressor / fortress | 66.1% | **100%** |
| economist / technologist | 0.9% | **100%** |
| fortress / technologist | 4.5% | **100%** |
| economist / fortress | 0.9% | **91.1%** |

Every pairing now fights. And every profile ended up in **fewer** active wars:
Aggressor −1.43, Economist −0.60, Fortress −0.46, Technologist −0.38, all
significant.

The interval governs when a war *ends* as well as when it starts. Exhaustion
accrues a flat 250 per update, so six times the updates burns through the
800-point peace threshold six times faster. War became universal and brief
instead of rare and grinding.

That change of shape reaches everything:

| | Aggressor | Economist | Technologist | Fortress |
|---|---|---|---|---|
| Score | **+19,700** | −1,826 | **+9,881** | +1,727 |
| Army | **+12,792** | −1,195 | **+4,958** | **−2,077** |
| Deaths | **−12,729** | **+1,996** | **−7,359** | **+1,826** |
| Stability | **+246** | **+36** | **+106** | **+54** |
| Score-lead rate | +0.04 | **−0.13** | +0.10 | 0.00 |

Two reasons to reject. The Economist's score-lead rate fell 0.13 — the first
time any candidate has moved a score-lead rate at all, and in the wrong
direction. And collapsed factions fell from 21 to 3, so the world became
markedly less decisive: all 112 matches still end at the tick ceiling, and now
with almost everybody alive.

## Interpretation

Shorter wars are gentler wars. Aggressor was being ground down by two long
grinding conflicts and now fights many brief ones, which is why it gains 19,700
score and sheds 12,729 in deaths. Nothing is resolved, because a war that ends
on exhaustion before either side is broken cannot resolve anything.

The mechanism under test is real: the sampling rate, not the personalities, is
what decided who fought. It just cannot be moved on its own.

## Limitations

Only the four military profiles on the four held-out maps. A single interval
value was tested. Reverted after measurement, so `HEAD` is back on
`econ002-candidate-112-v1` behaviour.

## Next

A v2 has to decouple the two things this interval controls. Exhaustion accrues
a flat amount per update, so it must scale with the interval — accrue per tick,
or divide the increment by the same factor the interval was divided by — so
that raising the sampling rate changes *who* goes to war without changing *how
long* wars last.

That is a one-line change against a hypothesis this run has already made
specific, and it is the first candidate with a real chance at the tick ceiling:
universal war at unchanged intensity is the combination that has never been
measured.

## Related pages

- [Reachable Technology ECON-002](2026-07-30-reachable-technology-econ002.md)
- [Levelling Trade ECON-001](2026-07-30-levelling-trade-econ001.md)
- [Combat Planner AI-004](2026-07-30-combat-planner-ai004.md)
