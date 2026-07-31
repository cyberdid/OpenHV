---
title: Belligerence Bracket DIP-005b
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-dip005b-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-dip005b-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-31-dip005b-candidate-112-v1-terms.csv
  - ../../raw/experiments/2026-07-31-dip005b-candidate-112-v1-endings.csv
  - 2026-07-31-belligerence-baseline-dip005.md
  - 2026-07-31-long-match-baseline.md
tags:
  - experiment
  - diplomacy
---

# Belligerence Bracket DIP-005b

## Purpose

DIP-005 added 200 to every disposition. Every ending metric moved the right way
and it was rejected anyway, because three of four score-lead rates moved with
them. Its limitations recorded the obvious gap: **200 is known to be too much
and nothing smaller was measured.**

This measures half of it, to find whether a window exists between "changes
nothing" and "moves the rates".

## Implementation

`BelligerenceBaseline = 100`. The share of sides able to accumulate grievance at
all still roughly doubles, 10.6% to 24.9%, while the relative spread falls only
to 2.75 rather than DIP-005's 2.17, against the baseline's 4.50.

One number changed. Same long schedule, same baseline, 112/112 completed.

## Result: rejected, and the direction with it

Halving the constant halved the violations without removing them.

| Profile | `scoreLead` | `performanceDelta` |
|---|---|---|
| aggressor | **+0.071 [+0.027, +0.125]** | +8.1 [−3.8, +20.9] |
| economist | +0.018 [−0.107, +0.143] | −0.8 [−20.7, +18.8] |
| technologist | **−0.134 [−0.232, −0.035]** | −14.9 [−33.0, +3.4] |
| fortress | +0.045 [−0.054, +0.143] | +7.6 [−10.0, +25.7] |

Two of four instead of three, and they are the same two extremes: the profile
with the highest disposition gains, the one in the middle loses. Technologist's
loss is **larger** at C=100 than it was at C=200 (−0.134 against −0.125).

And the decisiveness that justified the direction is gone:

| | Baseline | C=200 | C=100 |
|---|---|---|---|
| Natural victories | 5 | **7** | 5 |
| Collapsed factions | 185 | 196 | 199 |
| Matches stalling at 3 survivors | 46 | 35 | **31** |
| Matches ending with 1 survivor | 11 | **15** | 12 |

The mass moved out of three-survivor stalls, but into two-survivor stalls
(53 → 66) rather than into endings. Half the constant bought none of the
natural victories and all of the redistribution.

## Interpretation: there is no window

The carrying-term instrument shows why, and it shows it as a continuum rather
than a threshold:

| Carrying the decision | Baseline | C=200 | C=100 |
|---|---|---|---|
| relative power | **471** | 277 | 351 |
| disposition | 375 | **879** | **656** |

At both constants disposition overtakes relative power. Halving the constant
halved the overtake; it did not prevent it. The defect scales continuously with
the constant, so no positive value avoids it — a smaller one simply buys a
smaller violation along with a smaller effect.

DIP-005 concluded that the level cannot be raised without changing the rank.
This closes the weaker reading of that, that some level might be small enough
to be safe. **The whole direction is closed**: disposition cannot be moved as a
level any more than as a spread, because both change how it ranks against the
terms it is summed with.

## The instrument, again

`performanceDelta` found nothing significant here where `scoreLead` found two.
That is the opposite of DIP-005, where it caught an Economist regression the
binary rank could not resolve. Two candidates, one disagreement each way: the
two metrics are not redundant and neither dominates. Report both.

## Limitations

Two constants tested, 200 and 100. Nothing between them was measured, and a
value small enough to move nothing at all would by construction also move no
endings, so the gap is not worth closing. Only the four military profiles on
the four held-out maps.

## Next

Unchanged from DIP-005, and now better supported. The 10.6% measurement asks
for a **source of pressure that does not compete with the roster** — one that
grows from a faction's own history rather than from a comparison with its
neighbour, so it cannot re-rank the pairwise sum because it is not in it.

The expectations gap is that shape, and the predecessor project has it working
and tested. Its constants come from our distribution, not theirs.

## Related pages

- [Belligerence Baseline DIP-005](2026-07-31-belligerence-baseline-dip005.md)
- [Long-Match Baseline 112](2026-07-31-long-match-baseline.md)
- [Relative Power DIP-003](2026-07-31-relative-power-dip003.md)
- [Performance Delta Metric](2026-07-31-performance-delta-metric.md)
