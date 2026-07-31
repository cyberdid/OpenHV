---
title: Belligerence Baseline DIP-005
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-dip005-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-dip005-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-31-dip005-candidate-112-v1-terms.csv
  - ../../raw/experiments/2026-07-31-dip005-candidate-112-v1-endings.csv
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - 2026-07-31-long-match-baseline.md
  - 2026-07-31-relative-power-dip003.md
tags:
  - experiment
  - diplomacy
---

# Belligerence Baseline DIP-005

## Purpose

The long baseline left 101 of 112 matches at two or three survivors with
nothing holding them back. Measuring the pressure sum on all 406 surviving
sides said why: only **43 of them, 10.6%**, ever summed above zero. Pressure
clamps at zero, so for the other 89% grievance was frozen wherever it had
reached, and no amount of further time could move it.

## The measurement that refuted the planned candidate

The long-baseline record proposed a term keyed to the surviving count. The
data says the shape is flat:

| Survivors | Sides | Median pressure sum |
|---|---|---|
| 2 | 106 | −238 |
| 3 | 276 | −238 |
| 4 | 24 | −303 |

A scarcity term would help least at three survivors, where 54 matches stall
against 47 at two. So the level was raised instead.

The breakdown named what absorbs the pressure: disposition **+150** median
against trade dependency **−204** and casualty aversion **−103**. Even
removing trade dependency entirely leaves the median at −34.

## Implementation

`BelligerenceBaseline = 200`, added to every non-zero disposition. The
absolute spread is held at exactly 350, because DIP-002 compressed the spread,
moved Technologist's score-lead rate by 0.13, and was rejected for it.

200 was taken from the distribution rather than picked: it is the value that
takes the share of sides capable of accumulating grievance from 10.6% to 40.6%.

## Method

Baseline `baseline-long-112-v1`, valid without re-running because `git diff`
of the simulation code between its commit and the candidate's parent is empty.
Both runs completed 112/112 on the 60,000-tick schedule, 448 paired
observations. The candidate ran 130.6 minutes against the baseline's 67.5,
which is itself a signal: more units are alive and fighting for longer.

## Result: rejected

**The world did get more decisive.** Every ending metric moved the right way:

| | Baseline | Candidate |
|---|---|---|
| Natural victories | 5 | **7** |
| Collapsed factions | 185 | **196** |
| Pairs that ever warred | 551 | **672** |
| Matches ending with 3 survivors | 46 | **35** |
| Matches ending with 1 survivor | 11 | **15** |

**And three of four score-lead rates moved with it.**

| Profile | `scoreLead` | `performanceDelta` |
|---|---|---|
| aggressor | **+0.080 [+0.036, +0.134]** | **+20.2 [+5.6, +36.3]** |
| economist | −0.107 [−0.223, +0.009] | **−24.7 [−44.7, −3.5]** |
| technologist | **−0.125 [−0.223, −0.018]** | −10.3 [−29.0, +8.6] |
| fortress | **+0.152 [+0.045, +0.259]** | +14.9 [−7.2, +36.8]  |

That is the same class of regression that rejected DIP-001 and DIP-002, except
those each violated it once and this violates it three times, with Fortress
gaining more than either predecessor moved anything.

## Interpretation: the level cannot be raised without changing the rank

Holding the absolute spread was not enough, for two reasons the data separates.

**The relative spread halved.** 450 against 100 is a ratio of 4.50; 650 against
300 is 2.17. The difference was preserved and the ratio was not, and it is the
ratio that decides how much of a faction's behaviour its temperament explains.

**The carrying term flipped back.** This is the damning one:

| Carrying the decision | Baseline | Candidate |
|---|---|---|
| relative power | **471** | 277 |
| disposition | 375 | **879** |
| trade dependency | 291 | 77 |

DIP-003 was accepted precisely because it moved the decision from the roster to
the situation — relative power rose from 25 carried decisions to 150, and no
score-lead rate moved. Adding 200 to every disposition made disposition larger
than every situational term again, so it took the decision back. **DIP-005
undoes DIP-003 by arithmetic, without touching a line of it.**

So the world became more decisive in the way a roster is decisive: the profiles
with the highest and lowest dispositions gained, and the one in the middle lost.

## The instrument earned its keep

`performanceDelta` was added on the argument that a continuous share resolves
more than a binary rank, and its own record noted the claim was unearned
because DIP-003 gave both metrics the same null.

Here they disagree, and the disagreement is informative. The Economist's
`scoreLead` interval is [−0.223, +0.009] — it straddles zero and resolves
nothing. Its `performanceDelta` is [−44.7, −3.5], which does not. The Economist
took a real regression that the binary rank could not see.

The claim is earned. Report both from here on.

## Limitations

One constant was tested. The finding that level changes rank would hold for any
positive constant, but the size at which the score-lead rates start to move was
not bracketed — 200 is known to be too much and nothing smaller was measured.
Only the four military profiles on the four held-out maps.

## Next

The pressure model cannot be fixed by moving any existing term, because every
one of them is ranked against the others and moving one re-ranks all of them.
What the 10.6% measurement actually asks for is a **new source of pressure that
does not compete with the roster** — one that grows from a faction's own
history rather than from a comparison with its neighbour.

The predecessor project has exactly that and it is measured, not invented: the
gap between expectations and reality. Expectations ratchet up faster than they
fall, and radicalisation grows from the gap, so a faction that has grown used
to better generates pressure endogenously when growth stops. It is not a term
in the pairwise sum and so cannot re-rank it.

That is DIP-006, and its constants come from our distribution, not from theirs.

## Related pages

- [Long-Match Baseline 112](2026-07-31-long-match-baseline.md)
- [Relative Power DIP-003](2026-07-31-relative-power-dip003.md)
- [Compressed Disposition DIP-002](2026-07-31-compressed-disposition-dip002.md)
- [Performance Delta Metric](2026-07-31-performance-delta-metric.md)
