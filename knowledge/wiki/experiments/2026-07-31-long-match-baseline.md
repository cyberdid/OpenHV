---
title: Long-Match Baseline 112
status: current
updated: 2026-07-31
sources:
  - ../../raw/experiments/2026-07-31-baseline-long-112-v1-run.csv
  - ../../raw/experiments/2026-07-31-baseline-long-112-v1-endings.csv
  - ../../../batch-manifests/baseline-long-112-v1.json
  - 2026-07-31-relative-power-dip003.md
tags:
  - experiment
  - baseline
---

# Long-Match Baseline 112

## Purpose

Seven candidates in a row failed to move the tick-ceiling rate: AI-003, AI-004,
AI-005, ECON-001, ECON-002, DIP-001 and DIP-002 all left it at 112 matches out
of 112 ending on the clock. A four-seed probe on the current build then
produced a natural victory at tick 59,309, where the same probe before the
economy and diplomacy work produced one elimination and no endings at all.

Matches do resolve. The schedule was asking why they had not, after a fifth of
one.

## Method

The same 112 map, slot, profile and seed cells, run to 60,000 ticks instead of
12,000, with the watchdog raised from 180 to 900 seconds to match. 112/112
completed on attempt 1 with no failures.

## Result

The metric is alive, and small.

| | 12,000 ticks | 60,000 ticks |
|---|---|---|
| Natural victories | 0 of 112 | **5 of 112** |

They finish at ticks 32,562, 36,284, 53,112, 53,826 and 59,363 — so half are
done by 36,000, and the 60,000 estimate taken from the probe was generous.

## What actually stops a match

Not the clock. The count of survivors:

| Survivors at the end | Matches |
|---|---|
| 1 | 5 |
| 2 | **47** |
| 3 | **54** |
| 4 | 6 |

A hundred and one matches reach two or three survivors and stop there. The
diagnosis is in what those survivors are doing:

| Of the 47 two-survivor matches | |
|---|---|
| Standing between them | **neutral in 46, war in 1** |
| War exhaustion | median 0 of 1000 |
| Peace cooldown remaining | median 0 ticks |
| Stronger side's share of power | median 585 per mille |

Nothing is holding them back. They are not exhausted, not cooling off from a
treaty, and one of them has a real 17% power advantage. They are simply at
peace and have no reason to end it.

The winners say why: **four of the five are the Economist**, the profile with
the second-lowest disposition, and one is the Fortress, which has the lowest.
Never the Aggressor.

## Interpretation

The Aggressor is the engine of the whole conflict. It starts the wars, spends
itself in them, and dies — the long probe eliminated it in every one of four
seeds — and once it is gone nobody left has enough disposition to begin
anything. DIP-003 made war follow the power gap and that is what produced these
five endings, but a 585-to-415 advantage generates only +85 of pressure against
a surviving Economist's disposition of 150, and the terms that subtract absorb
it.

So the standing problem has a precise name now. It is not that matches do not
end. It is that **the last two factions have no mechanism for finishing each
other**, because the profile that supplies the aggression is always the first
to die.

## Limitations

One tick ceiling was tested. The natural-victory rate is 5 of 112, so a
candidate would need a large effect to clear the noise on that metric alone;
survivor count is the more sensitive one and should carry the comparison.

Island produced zero endings of 28, against two each on Cold Rage and Doubles
and one on Winter. A map where factions are separated by water cannot resolve
at all, which is worth separating out before reading any future result.

## Next

The candidate this points to is not another weight. A two-faction world at
peace with a 17% power gap should not be stable, and nothing in the pressure
model makes it unstable, because disposition is a constant per profile and the
situational terms only subtract.

A term that grows as the field narrows — pressure that rises when few factions
remain, or when one holds a majority of the world's power rather than merely a
plurality of a pair's — is the missing shape. That is the first thing since
AI-003 that would address the ending rate directly rather than through combat
or economy.

## Related pages

- [Relative Power DIP-003](2026-07-31-relative-power-dip003.md)
- [Symmetric Trade Restraint DIP-004](2026-07-31-symmetric-trade-dip004.md)
- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
