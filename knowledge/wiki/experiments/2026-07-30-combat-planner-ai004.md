---
title: Combat Planner AI-004
status: current
updated: 2026-07-30
sources:
  - ../../raw/experiments/2026-07-30-ai004-candidate-112-v1-run.csv
  - ../../raw/experiments/2026-07-30-ai004-candidate-112-v1-paired.csv
  - ../../../engine-patches/openra-ai-combat.patch
  - ../../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../../OpenRA.Mods.HV/Simulation/SimulationCivilizationSnapshotBuilder.cs
  - ../../../mods/hv/rules/bots.yaml
  - ../../../batch-manifests/ai004-candidate-112-v1.json
  - 2026-07-29-baseline-112-v1.md
tags:
  - experiment
  - ai
  - combat
  - determinism
---

# Combat Planner AI-004

## Purpose

The 112-match baseline finished every match at its tick ceiling and exposed
Technologist and Fortress losing armies without winning wars. AI-004 asks
whether squad-level target scoring and force preservation reduce that: value
and damage weighted target choice, a finishing bonus against owners who have
already lost heavily, and retreat, regroup, and re-engagement instead of
fighting to the last unit.

## Implementation

Target score combines unit value, accumulated damage, distance, a building and
construction-yard bonus, and a finishing bonus when the target's owner is below
the profile's assets threshold and above its loss threshold. `ShouldRetreat`
adds explicit low-health-and-power thresholds to the stock fuzzy decision.
Ground squads that retreat move to an own building, hold for `RegroupTicks`,
and re-engage rather than dissolving.

Two corrections were needed before the mechanism could fire at all. Protection
squads run about 65% of combat in these matches and their states never
evaluated retreat, so the path was extended to them; because
`StateBase.ShouldFlee` refuses to flee whenever an own building is inside the
danger radius — the permanent condition for a defender — that path skips the
veto and uses only the explicit thresholds. The profiles had also raised
`DangerScanRadius` above the engine default, which widens the same veto; it was
restored. Before these, 12,000 ticks produced zero retreats.

Each profile carries its own thresholds: Aggressor commits longest and returns
fastest, Technologist and Fortress disengage earliest and hold longest.

## Method

The candidate reuses all 112 baseline map, slot, profile, and seed cells
unchanged. `compare-candidate.py` pairs every match and profile observation
before estimating differences with the deterministic 2,000-resample bootstrap.
Two 12,000-tick smoke repeats matched hash `7191667E`.

Run `ai004-candidate-112-v1` at clean commit `7a946325` completed 112/112
matches in 9m15s against baseline commit `3308752a`, giving 448 paired
observations.

## Result: rejected

The mechanism is real and observable. Retreats, regroups, re-engagements, and
target selections rose significantly for all four profiles.

Neither acceptance target moved.

| | Baseline | Candidate |
|---|---|---|
| End reasons | 112 tick limit | 112 tick limit |
| Collapsed factions | 15 | 27 |

The tick-ceiling rate stayed at 100%, and collapses nearly doubled. Two
profiles regressed significantly (candidate minus baseline, 95% bootstrap):

| Profile | Deaths | Army | Active wars | Collapsed |
|---|---|---|---|---|
| Technologist | +3,983 | −2,527 | +0.7 | +0.1 |
| Fortress | +2,039 | −1,343 | +0.7 | — |

Fortress also took +4.1 war casualties. Aggressor and Economist showed no
significant outcome change in either direction; their kill and death intervals
straddle zero.

## Interpretation

Preserving squads did not preserve armies. Both regressed profiles gained
roughly 0.7 active wars per match, so the change reached diplomacy rather than
staying tactical: surviving squads keep army value in the field, relative power
feeds the war utility, and more wars produced more losses than the retreats
saved. The measured war exposure AI-004 was meant to reduce grew instead.

This is a falsified hypothesis, not a broken build. Force preservation at squad
level is not sufficient on its own, and coupling it to the war decision is the
part that needs to change.

## Limitations

Only the four military profiles were exercised, on the same four held-out maps
as the baseline. The comparison spans two commits that differ by more than the
combat planner, though the intervening commits touch tooling, observer UI, and
the server start path rather than synchronized logic. `combat-decision` events
are sampled at the telemetry interval, so the exact counters in the result are
authoritative and the event stream is a timeline, not a complete log.

## Next

A v2 should hold the war decision fixed while retreat changes: either exclude
retreating or regrouping strength from the relative-power term, or raise the
war threshold by the amount the preserved army adds. Without that, any further
tactical survivability is expected to keep converting into more wars.

## Related pages

- [Civil and Military Baseline 112 v1](2026-07-29-baseline-112-v1.md)
- [Civilization Planner AI-003](2026-07-29-civilization-planner-ai003.md)
- [Architecture: combat decision boundary](../architecture.md)
- [AI profiles](../ai-profiles.md)
