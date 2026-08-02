---
title: "Decision 0006: One .NET/OpenHV Universe Runtime"
status: accepted
updated: 2026-08-02
sources:
  - ../universe-north-star.md
  - ../../raw/user-vision-2026-08-02.md
  - ../../raw/worldbox-2026-08-02.md
  - ../../raw/coruscantsim-analysis-2026-07-31.md
tags:
  - decision
  - architecture
  - runtime
---

# Decision 0006: One .NET/OpenHV Universe Runtime

## Context

The project currently has two capable but incompatible runtime centers:

- CoruscantSim/Python owns climate, biosphere, emergence, social simulation,
  policy, storyteller, characters, chronicle, and Web planetary views;
- OpenHV/.NET owns maps, actors, construction, pathfinding, combat, modular AI,
  synchronized civilization state, research, diplomacy, trade, replays,
  telemetry, and headless experiments.

The Web-to-per-cell battle bridge proved data exchange, but required observer
input, opened a second process, lost whole-world context, and reduced war to a
temporary arena. That contradicts the authoritative product direction.

## Options

### Web/TypeScript as the single runtime

This keeps browser distribution and flexible WebGL visualization, but requires
reimplementing OpenRA's RTS engine, pathfinding, combat, actor/content model,
AI, replay, and determinism. It discards the more expensive foundation.

### .NET/OpenHV as the single runtime

This keeps RTS and existing civil systems, then ports the planet simulation
and observer interfaces. The port is substantial, but its functions are
bounded, testable, and can be checked against Python golden outputs.

## Decision

Use **one .NET/OpenHV runtime and one authoritative C# state graph** for the
product. CoruscantSim becomes the migration oracle, golden-data producer, and
scientific reference until parity is complete. Web pages may consume exports
for diagnostics, but cannot own state or launch product battles.

Planet physics is stored as synchronized fixed-point integers. A port may use
transient numeric helpers only if values are deterministically quantized before
entering state. No synchronized float fields are allowed.

The product surface is multi-scale. Planet, region, local RTS, orbital, and
system views are projections of the same entities, not separate simulations or
matches. Multi-planet identities exist in the domain model before planet two is
activated.

## No-loss migration rule

A source feature may be retired only when:

1. its target C# owner is named;
2. its behavior has unit/invariant tests;
3. golden or statistical parity is demonstrated where exact parity is not
   meaningful;
4. its events and telemetry are observable;
5. native UI or developer tooling exposes the replacement;
6. the migration ledger row is marked `verified` with evidence.

Conflicting manual product interactions, such as clicking a cell to start a
battle, remain available only as developer diagnostics. Their underlying
contract/replay capability is retained; the observer-facing intervention is
not part of the autonomous scenario.

## Consequences

- The first implementation priority is the Universe/Planet domain boundary,
  clock, persistence, and event spine—not more isolated battle tuning.
- Python code is not deleted during migration.
- New gameplay systems are written in C# and must support headless execution.
- The OpenRA engine may require tracked patches for semantic zoom, large-world
  chunking, and multi-surface rendering.
- Public distribution of the named Tyranid material requires a separate IP and
  asset-license review; this does not block private simulation architecture.
- Decision 0003 remains valid: living factions precede and motivate war.
