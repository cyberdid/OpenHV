---
title: AI Strategy Profiles
status: current
updated: 2026-07-29
sources:
  - ../../mods/hv/rules/bots.yaml
  - experiments/2026-07-29-baseline-tournament.md
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

## Evidence status

The [baseline tournament](experiments/2026-07-29-baseline-tournament.md)
confirmed that every profile loads, runs, and produces different short-horizon
scores. It did not run long enough to validate the intended strategic
identities in combat. Those descriptions remain design intent until longer
telemetry-backed experiments confirm them.
