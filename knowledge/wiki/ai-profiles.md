---
title: AI Strategy Profiles
status: current
updated: 2026-07-29
sources:
  - ../../mods/hv/rules/bots.yaml
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-living-factions-v1.md
  - experiments/2026-07-29-civilization-ai-war-cost-v1.md
tags:
  - ai
  - balancing
---

# AI Strategy Profiles

The profiles share OpenRA's base construction, economy, scouting, repair,
support-power, and attack modules. Their squad timing, production priorities,
cash thresholds, and build limits differ.

## Aggressor — Vanguard AI

- Intended behavior: early pressure and frequent small attack groups.
- Small rush threshold and short attack delay.
- Favors inexpensive combat units.
- Backward-compatible `rogue` bot uses this personality.

## Economist — Foundry AI

- Intended behavior: preserve liquidity, expand income, and field a later
  balanced force.
- Higher minimum cash reserve and stronger miner/builder emphasis.
- Larger, slower attack groups.

## Technologist — Ascendant AI

- Intended behavior: delay commitment until advanced ground and air units are
  available.
- Prioritizes higher-tier tanks, aircraft, and specialized units.
- Medium attack cadence and cash reserve.

## Fortress — Bastion AI

- Intended behavior: defend infrastructure, use anti-air and artillery, then
  move out with large formations.
- Largest squad target and longest rush delay.
- Emphasizes repair, mine-laying, artillery, and defensive unit composition.

## Steward — Steward AI

- Intended behavior: peaceful civil-development fixture.
- Uses shared base, builder, repair, and economic construction logic.
- Builds no combat units and creates no attack squads.
- Exists to test population, infrastructure, research, shortages, trade, and
  diplomacy without making combat the default source of activity.
- Four Steward AIs reached tick 3,500 with population growth and zero
  kills/losses in the
  [Living Factions validation](experiments/2026-07-29-living-factions-v1.md).

Steward is an experiment control, not a substitute for runtime neutral
relationships.

## Shared Civilization AI v1

The tactical profiles now feed a synchronized multi-objective civilization
planner rather than only fixed build/squad parameters. Every 250 ticks it
scores survival, research, trade, security, recovery, and war; it selects
development, survival, research, trade, mobilization, or recovery.

War pressure includes personality but is reduced by shortages, instability,
bilateral trade dependency, research commitment, and prior casualties, and is
modified by relative power. Economist receives a trade-utility preference;
Technologist receives a research preference; Steward has zero offensive war
pressure. The selected state changes research budget and diplomacy. Military
mobilization and casualties alter workforce, production, population, and
stability for every profile.

The first evidence is in
[Civilization AI and War Cost v1 Validation](experiments/2026-07-29-civilization-ai-war-cost-v1.md):
trade dependency prevented a war seen in the paired no-trade control; five
wars triggered four mobilization states; peace triggered four recovery states;
and a repeat produced hash `8431018E`.

## Evidence status

The [baseline tournament](experiments/2026-07-29-baseline-tournament.md)
confirmed that every profile loads, runs, and produces different short-horizon
scores. It did not run long enough to validate the intended strategic
identities in combat. Those descriptions remain design intent until longer
telemetry-backed experiments confirm them.

Civilization-state transitions are now directly evidenced, but profile-wide
tactical identity still needs a held-out multi-map baseline.
