---
title: Relative Power DIP-003
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-dip003-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-dip003-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-31-dip003-candidate-112-v1-pairings.csv
  - ../../raw/experiments/2026-07-31-dip003-candidate-112-v1-terms.csv
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - 2026-07-31-compressed-disposition-dip002.md
tags:
  - experiment
  - diplomacy
---

# Relative Power DIP-003

## Purpose

DIP-002 established what disposition is: not a personality laid over the war
decision but the counterweight that keeps pressure above zero, because every
other term subtracts except relative power. Lowering it hands nobody the
decision — it lets the rest zero the pressure out.

So raise the one term that adds.

## The measurement that sized it

Relative power is a faction's share of the pair's combined army and half-assets,
in per mille. Across 1,344 observed pairings in the baseline it runs:

| | 5th percentile | median | 95th percentile |
|---|---|---|---|
| Relative power | 126 | 500 | 873 |
| Halved, as a term | −187 | 0 | +186 |

So the spread was never the problem — a term reaching ±186 is real. It was
outranked, by a disposition of up to 450 and a trade dependency of up to −500,
which is why it carried 25 decisions out of 1,344.

## Implementation

`(relativePower - 500) / 2` becomes `relativePower - 500`, so the same
percentiles give ±373 and the term can outweigh every profile but the
Aggressor.

The gap is symmetric across a pair and would move no sum on its own. But
pressure clamps at zero, so the weaker side's penalty is absorbed while the
stronger side's advantage is not: an unequal pair grows more warlike and an
even one does not.

One variable. Disposition, thresholds, exhaustion and the peace cooldown are
untouched.

## Method

Baseline `baseline-112-v3`, verified still valid at the commit before the
candidate by matching hash `DCA6BDCC` on a shared seed. Both runs completed
112/112, 448 paired observations.

## Result: accepted

War became situational without being taken from anyone.

| Pairing | Baseline | Candidate |
|---|---|---|
| aggressor / technologist | 100.0% | 100.0% |
| aggressor / economist | 92.9% | 93.8% |
| aggressor / fortress | 66.1% | **75.0%** |
| economist / technologist | 0.9% | **18.8%** |
| fortress / technologist | 4.5% | **8.0%** |
| economist / fortress | 0.9% | **4.5%** |

DIP-002 had redistributed war by taking it from the Aggressor. This adds it
everywhere: Economist +0.20 active wars, Technologist +0.21, Fortress +0.14,
all significant, and the Aggressor unchanged because it was already fighting.

The term that decides moved with it — relative power carried 150 decisions
against 25, a sixfold rise, while disposition fell from 883 to 785.

**Collapsed factions rose from 22 to 30.** Every previous candidate moved this
the wrong way: DIP-001 took it from 21 to 3, DIP-002 from 22 to 12. This is the
first change that made the world more decisive rather than less.

**No score-lead rate moved.** All four intervals straddle zero: Aggressor 0.00,
Economist −0.03, Technologist +0.02, Fortress +0.01. That is what rejected both
predecessors — Economist lost 0.13 in DIP-001, Technologist gained 0.13 in
DIP-002 — and it is unchanged here.

The cost is what more war costs. Population fell for all four and stability for
all four, both significant, and Fortress also lost army and prosperity. No
profile's score moved significantly.

## Interpretation

A strong faction now attacks a weak neighbour whatever its temperament, and two
even factions leave each other alone whatever theirs. That is the shape both
previous experiments were circling and neither reached: the roster stopped
deciding who fights, and the situation started.

The clamp is what makes it work. Without it the term would be symmetric and the
threshold, which sums both sides, would never notice a power gap at all.

## Limitations

Only the four military profiles on the four held-out maps. One scaling was
tested — the divisor was removed rather than tuned — and a larger factor was
not measured. The tick ceiling did not move: all 112 matches still end on the
clock, though 36% more factions are dead when they do.

## Next

Collapses moved for the first time, so the ceiling is reachable from here. The
same reasoning applies to the terms that subtract: trade dependency carried 399
decisions and spans 0 to −500, which is large enough to veto a war on its own.
Whether that is a feature or the next thing outranking the situation is a
measurement nobody has taken.

## Related pages

- [Compressed Disposition DIP-002](2026-07-31-compressed-disposition-dip002.md)
- [Strategic Interval DIP-001](2026-07-30-strategic-interval-dip001.md)
- [Open-source strategy references](../../raw/strategy-references-2026-07-31.md)
