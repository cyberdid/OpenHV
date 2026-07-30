---
title: Regroup Locality AI-005
status: current
updated: 2026-07-30
sources:
  - ../../raw/experiments/2026-07-30-ai005-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-30-ai005-candidate-112-v1-paired.csv
  - ../../raw/experiments/2026-07-30-ai005-candidate-112-v1-levels.csv
  - ../../../engine-patches/openra-ai-combat.patch
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../../schemas/simulation-result-v1.schema.json
  - ../../../batch-manifests/ai005-candidate-112-v1.json
  - 2026-07-30-combat-planner-ai004.md
tags:
  - experiment
  - ai
  - combat
---

# Regroup Locality AI-005

## Purpose

A graphical match showed one bot standing in a crowd beside its own base while
the other two of the same profile fought. The counters put that bot at twelve
regroups against one each for the others. A squad that disengages moves to an
own building picked at random and holds there for `RegroupTicks`; on a
developed base the walk can cross the whole base, and for Aggressor, whose hold
is only 150 ticks, that travel is the larger term.

AI-005 asks whether the travel is costing anything measurable.

## Implementation

Ground and protection squads fall back to the closest own building instead of a
random one. Ties break on actor id so the choice stays synchronized. Air squads
still pick at random: returning to base to rearm is a different question and is
deliberately left out of the measurement.

One variable. AI-004 moved target scoring, retreat thresholds, and the regroup
cycle together and could not attribute its regression to any one of them.

## Method

The baseline was re-measured on the same 112 cells at commit `60c54a75`, the
commit immediately before the candidate at `8a058f78`, so the two runs differ
by the regroup change alone. AI-004 had to compare across commits that differed
by more than the change under test and recorded that in its limitations; this
run does not carry that caveat.

Both runs completed 112/112 on attempt 1, in 523.8 and 524.6 wall seconds, with
no retry, crash, desync, watchdog, or invalid-result artifacts. The schedule
hash differs between the two run ids because the id is part of the hash; the
112 map, slot, profile, and seed cells were verified identical to
`baseline-112-v1`. Two 12,000-tick repeats of the candidate matched hash
`A75BD56B`.

```sh
python3 run-batch.py batch-manifests/baseline-112-v2.json
python3 run-batch.py batch-manifests/ai005-candidate-112-v1.json
python3 compare-candidate.py \
  ../simulation-runs/baseline-112-v2 \
  ../simulation-runs/ai005-candidate-112-v1
```

### The matrix was unusable before this ran

The first baseline attempt scored invalid-result on every match. The batch
runner validates each result against `simulation-result-v1.schema.json` and
fails any match carrying an undeclared property. `config.factions` has been
written into every result since faction selection landed, and was never added
to the schema, so **any batch run after that change would have failed all 112
matches**. Nothing had exercised the matrix since. Admitting `factions` and the
two new squad-census counters to the schema is what let this experiment run at
all.

## Result: rejected

No effect.

| | Baseline | Candidate |
|---|---|---|
| End reasons | 112 tick limit | 112 tick limit |
| Collapsed factions | 18 | 21 |

Of eighty paired profile differences, three cleared the 95% bootstrap interval,
and all three are civil rather than combat: Technologist research input spent
+1.5, Fortress prosperity +11.7, Fortress available workforce +12.3. At eighty
comparisons roughly four false positives are expected by chance at that level,
so three is what noise looks like. Every combat metric — army, kills, deaths,
retreats, regroups, re-engagements, target selections, active wars, war
casualties — straddles zero for all four profiles.

## Interpretation

The mechanism barely fires, so where it sends the squad cannot matter.

| Profile | Regroups per match | Retreats | Idle at base | Held in squads |
|---|---|---|---|---|
| Aggressor | 4.05 | 1.64 | 3.9 | 12.2 |
| Economist | 0.54 | 0.26 | 6.8 | 27.1 |
| Technologist | 1.79 | 1.76 | 2.2 | 11.1 |
| Fortress | 0.19 | 0.14 | 9.0 | 29.0 |

Four regroups per 12,000 ticks is the busiest case. Even if every one of them
had been walking the full width of the base, the saving is a few hundred ticks
for one squad, against a match none of the profiles can finish. The twelve-
regroup bot that prompted the question is the tail of the distribution, not the
typical case, and reading a mechanism's importance off a single observed match
is exactly what this matrix exists to prevent.

The idle and committed columns are new here and answer a question that could
not be answered before: squads do form. The pool of units waiting at the base
never exceeds nine on average while squads hold eleven to twenty-nine, so
"units accumulate because no squad is created" is ruled out for every profile.

## Limitations

Only the four military profiles on the four held-out maps. The candidate was
kept in the tree rather than reverted; it costs about twenty-five lines of the
engine patch that has to survive every engine update, and carries no measured
benefit, so reverting it is defensible and is a maintenance judgement rather
than a correctness one. Air squads were not changed and not measured.

## Next

The regroup cycle is not a lever at this frequency, and neither the hold
duration nor the retreat threshold can become one while squads regroup a
handful of times per match. Anything that wants to move the tick ceiling has to
change how often squads engage, not what happens after they disengage.

## Related pages

- [Combat Planner AI-004](2026-07-30-combat-planner-ai004.md)
- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
- [AI profiles](../ai-profiles.md)
