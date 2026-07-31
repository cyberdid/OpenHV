---
title: Performance Delta Metric
status: current
updated: 2026-07-31
sources:
  - ../../../analyze-baseline.py
  - ../../../compare-candidate.py
  - ../../../tests/test_baseline_analysis.py
  - ../../raw/experiments/2026-07-31-performance-delta-baseline.csv
  - 2026-07-31-long-match-baseline.md
  - 2026-07-31-relative-power-dip003.md
tags:
  - instrument
  - baseline
---

# Performance Delta Metric

## Purpose

`scoreLead` is a binary rank over four players, so a 112-match batch buys very
little resolution: the long baseline puts Economist and Technologist at
**exactly the same 0.348**, and the Aggressor at 0.000 with a Wilson interval
of width 0.033 that says nothing beyond "never leads".

Aligulac, which has tracked StarCraft II matchup balance for 192 months, does
not read raw win rate for the same reason. It reports a second chart:
performance against what the players' strength predicted. This adapts that
shape to the matrix.

## Definition

Three shares, per mille of each match's total, added to every player
observation:

| Field | Numerator |
|---|---|
| `powerShare` | `armyValue + assetsValue / 2` at the final tick |
| `committedShare` | the above **plus** `deathsValue` |
| `scoreShare` | `score` |

`performanceDelta = scoreShare - committedShare` — score earned above the
material fielded to earn it.

`committedShare`, not `powerShare`, is the denominator because score
accumulates over the whole match while army is a snapshot. Measuring
cumulative score against surviving army reads every profile that spent its
army as efficient. The unit test fixes this: two players with equal score, one
holding an army and one having lost it, differ 167 to 833 on `powerShare` and
are equal on `committedShare`.

`powerShare` is kept because it is the same shape as the simulation's own
`RelativePower`, which DIP-003 made decisive. It is **not** the same number:
`CivilizationState.MilitaryArmyValue` subtracts live civilian-mobilization
actors from `ArmyValue`, and the result document does not export that
subtraction. Exporting it would be a simulation change and a new baseline.

## Method

Analysis only. No simulation code was touched, so every existing run
re-analyses in place and no batch was re-run.

The four fields were **appended** to `PLAYER_FIELDS`, `PAIRED_FIELDS` and
`COMPARISON_FIELDS`. All three seed the bootstrap with `seed + field_index`,
so an insertion would silently move every interval already recorded here.
Regenerating the DIP-003 comparison against the committed CSV reproduced
**240 of 240 values exactly**.

## Result: kept as a descriptor, not as a more sensitive test

The sensitivity claim did not survive contact. Re-running DIP-003, an accepted
candidate, `performanceDelta` finds exactly what `scoreLead` found — nothing:

| Profile | `scoreLead` | `performanceDelta` |
|---|---|---|
| aggressor | +0.000 [−0.027, +0.027] | +2.1 [−5.8, +10.3] |
| economist | −0.027 [−0.062, +0.009] | −0.1 [−5.3, +5.0] |
| technologist | +0.018 [−0.027, +0.062] | −2.0 [−9.0, +4.9] |
| fortress | +0.009 [−0.027, +0.054] | +0.0 [−4.8, +4.2] |

That is the correct answer — DIP-003 genuinely moved no score leadership, which
is why it was accepted — but it is not evidence of extra power, and none should
be claimed until a candidate that does move something is measured with both.

Where it does earn its keep is describing the population. On the long baseline:

| Profile | `scoreLead` | `performanceDelta` |
|---|---|---|
| aggressor | 0.000 [0.000, 0.033] | **−18.2 [−22.8, −13.4]** |
| economist | 0.348 [0.266, 0.440] | +11.5 [−0.4, +24.1] |
| technologist | 0.348 [0.266, 0.440] | −3.5 [−15.9, +8.0] |
| fortress | 0.304 [0.226, 0.394] | +10.2 [−1.4, +21.6] |

Two things `scoreLead` cannot say:

**The Aggressor destroys value.** It holds 14.3 per mille of final power but
committed 53.1 and earned 34.9 of the score, and the interval clears zero. The
long baseline established that it dies in every seed; this adds that it loses
the exchange on the way, rather than trading evenly and being outlasted.

**Economist and Technologist are not the same faction.** `scoreLead` gives them
an identical 0.348. `performanceDelta` puts them 15 per mille apart in opposite
directions. The intervals overlap, so this is a direction and not a finding —
but a metric that returns the same number for two different profiles cannot
even offer one.

## Limitations

One baseline and one candidate. The claim that a continuous share resolves
more than a binary rank is untested against a candidate with a real effect.
`powerShare` differs from in-simulation `RelativePower` by the civilian
subtraction, so the two should not be quoted against each other.

## Next

Report both on the next candidate. If `performanceDelta` moves where
`scoreLead` does not, the sensitivity claim is earned and `scoreLead` can be
demoted to a summary statistic; if it never does, this stays a descriptor and
costs nothing.

## Related pages

- [Long-Match Baseline 112](2026-07-31-long-match-baseline.md)
- [Relative Power DIP-003](2026-07-31-relative-power-dip003.md)
- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
