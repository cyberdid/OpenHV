---
title: Dynamic Diplomacy v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-dynamic-diplomacy-v1.csv
  - ../faction-life.md
  - ../../../OpenRA.Mods.HV/Traits/World/DiplomacyManager.cs
tags:
  - experiment
  - diplomacy
  - determinism
---

# Dynamic Diplomacy v1 Validation

## Purpose

Validate DIP-001 as an engine-effective, synchronized relationship system:
factions begin neutral, neutral units are not auto-targeted, rivalry can
declare war, losses increase war exhaustion, and peace restores neutral OpenRA
targeting masks. Verify that transitions and final bilateral state are
observable and deterministic.

The raw results are preserved in
[2026-07-29-dynamic-diplomacy-v1.csv](../../raw/experiments/2026-07-29-dynamic-diplomacy-v1.csv).
The experiment ran from base commit `0a5e5535` with the Sprint 5 working tree;
the published implementation is the commit containing this record.

## Fixtures and commands

All cases used Cold Rage, four autonomous players, headless deterministic
execution, a world-tick horizon, and Schema v1 result/telemetry/event
artifacts. Representative neutral control:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=steward \
SIMULATION_SEED=7801 \
SIMULATION_MAX_TICKS=6000 \
SIMULATION_WATCHDOG_SECONDS=120 \
SIMULATION_TELEMETRY_INTERVAL_TICKS=500 \
SIMULATION_RESULT=/tmp/diplomacy-neutral/result.json \
./run-simulation.sh coldrage
```

The transition scenario changed bots to `rogue,fortress`, seed to 7802, the
horizon to 26,000, and telemetry interval to 1,000 ticks. Determinism runs A/B
used seed 7803 and an 11,000-tick horizon.

## Relationship rules

- One synchronized relation exists for each unordered active-player pair.
- Initial state is `neutral`; both allied and enemy mask bits are cleared.
- Every 5,000 ticks, bot dispositions add bilateral grievance.
- Combined grievance 400 declares `war` and sets both enemy mask bits.
- During war, losses add to a deterministic base exhaustion increment.
- Exhaustion 800 after the minimum war duration restores `neutral`, clears
  enemy mask bits, and starts a 10,000-tick peace cooldown.
- A lost participant also terminates its active wars with reason
  `faction-collapse`.

Trust, grievance, exhaustion, cooldown, transition tick/sequence, state, and
reason are synchronized integers. Human-readable identifiers are derived only
when artifacts are written.

## Results

The Steward control reached tick 6,000 with hash `FC0C7DE2`. All six faction
pairs remained neutral, and total kills and deaths were zero. This directly
confirms that the runtime manager replaced the map's default free-for-all
enemy masks.

The 26,000-tick Rogue/Fortress scenario reached hash `9F3B1BA2`. It recorded
11 `strategic-rivalry` war declarations and 11 `war-exhaustion` peace
transitions across repeated relationship cycles; all six pairs were neutral
at the horizon. Combat occurred only after explicit war transitions.

The paired seed-7803 runs both reached tick 11,000 with hash `E5E13643`,
identical transition counts, final relations, and combat totals. Each emitted
five war declarations, three exhaustion peaces, and two collapse-driven
neutralizations.

Every final result and every line of all four telemetry/event streams passed
the current JSON Schemas. The seven batch-runner integration tests also
passed.

## Interpretation and limits

DIP-001 is complete for neutral, war, and peace. The state affects native
OpenRA targeting, is part of the synchronized hash, survives headless
execution, and is fully observable. `Alliance`, fear, cultural affinity,
contact/fog-of-war, treaties, and defensive response are reserved states or
later signals rather than implemented behavior. The current disposition
pressure is deliberately simple and will move into Civilization AI utility
once trade and societal costs exist.

The next experiment is asymmetric scarcity with stock-backed bilateral trade,
followed by a paired war-cost run that couples casualties and mobilization to
civilian workforce, production, stability, and recovery.
