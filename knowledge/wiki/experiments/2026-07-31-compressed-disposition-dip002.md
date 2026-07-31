---
title: Compressed Disposition DIP-002
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-dip002-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-dip002-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-31-dip002-candidate-112-v1-pairings.csv
  - ../../raw/experiments/2026-07-31-dip002-candidate-112-v1-terms.csv
  - ../../raw/strategy-references-2026-07-31.md
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - 2026-07-30-strategic-interval-dip001.md
tags:
  - experiment
  - diplomacy
---

# Compressed Disposition DIP-002

## Purpose

DIP-001 established that war was decided by a per-profile constant, and the
named-term instrument adapted from Unciv showed the same thing per relation:
`disposition` carried 883 of the recorded decisions against 428 for trade
dependency and 25 for relative power. The six situational terms almost never
decided anything.

This asks whether the *spread* of that constant is what does it.

## Implementation

The disposition spread across the four matrix profiles was 350 against a mean
of 237.5. The mean is held exactly and the spread cut to 100.

| Profile | Before | After |
|---|---|---|
| Aggressor | 450 | 290 |
| Technologist | 250 | 250 |
| Economist | 150 | 220 |
| Fortress | 100 | 190 |

Holding the mean is the whole design. Halving the numbers would have lowered
the spread and the appetite for war together, and nothing that moved could have
been attributed to either.

## Method

Baseline `baseline-112-v3`, run at the commit before the candidate. The art
work had moved the simulation — the swarm is a selectable random faction and
two of four players roll it on these seeds — so ECON-002 was no longer valid as
a baseline. Both runs completed 112/112, 448 paired observations.

## Result: rejected

The mechanism is confirmed. War stopped being one profile's property.

| Pairing | Baseline | Candidate |
|---|---|---|
| aggressor / technologist | 100.0% | 49.1% |
| aggressor / economist | 92.9% | 63.4% |
| aggressor / fortress | 66.1% | 30.4% |
| economist / technologist | 0.9% | **11.6%** |
| fortress / technologist | 4.5% | **12.5%** |
| economist / fortress | 0.9% | **8.9%** |

Technologist gained 0.36 active wars and Economist 0.18, both significant,
while Aggressor's count did not move at all: it fights as often, against
different neighbours.

Two reasons to reject.

**A score-lead rate moved.** Technologist gained 0.13, significant, along with
+11,692 score, +5,447 army and −5,036 deaths. That is the same class of
regression that rejected DIP-001, in the other direction.

**The world got less decisive.** Collapsed factions fell from 22 to 12, and all
112 matches still end at the tick ceiling.

## Interpretation

The instrument earned its keep here, because it explains why.

The six situational terms are **net negative**: prosperity, stability, trade
dependency, research commitment and casualty aversion all subtract, and only
relative power can add. Disposition is not merely a personality — it is the
counterweight that keeps pressure above zero at all.

Lowering it therefore does not hand the decision to the situational terms. It
lets them zero out the pressure entirely, which is why fewer pairings ever go
to war even though the mean appetite was held exactly, and why disposition
still carries the decision 888 times out of a possible 1,344 after the change
against 883 before. Compressing the spread redistributed who fights without
giving any other term the power to decide.

## Limitations

Only the four military profiles on the four held-out maps. One compression
ratio was tested. Reverted after measurement.

## Next

A v3 has to raise the situational terms rather than lower disposition. The
obvious candidate is `RelativePower`, which is the only term that can push
toward war and which carried just 25 decisions out of 1,344: at
`(relativePower - 500) / 2` it spans roughly ±250 in principle but sits near
zero in practice, because relative power between four developing factions
rarely diverges. Either widen its range or replace the divisor with something
that responds to a real power gap.

That is the first candidate that would make war a property of the situation
rather than of the roster, which is what both DIP experiments have been
circling.

## Related pages

- [Strategic Interval DIP-001](2026-07-30-strategic-interval-dip001.md)
- [Open-source strategy references](../../raw/strategy-references-2026-07-31.md)
- [Combat Planner AI-004](2026-07-30-combat-planner-ai004.md)
