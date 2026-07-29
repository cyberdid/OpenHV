---
title: "Decision 0003: Model Living Factions, Not Only War"
status: accepted
updated: 2026-07-29
sources:
  - ../faction-life.md
  - ../../raw/openciv-2026-07-29.md
tags:
  - decision
  - civilization
  - product
---

# Decision 0003: Model Living Factions, Not Only War

## Context

The initial autonomous prototype and execution plan emphasized military
matches, win rates, combat balance, and faction warfare. That proves the RTS
foundation but does not create the intended feeling of autonomous societies.

OpenCiv demonstrates a useful conceptual layer: population, cities, worked
territory, differentiated yields, buildings, resources, and civilization
identity. Its current codebase is not a compatible or complete engine layer,
so direct merging would add complexity without solving the design problem.

## Decision

The project is a living-faction simulation with RTS combat, not a war
simulator with decorative economy.

Add a synchronized civilization layer for:

- population and needs;
- settlements and territory;
- civil resource flows;
- research and institutions;
- trade and dynamic diplomacy;
- migration, crises, and recovery.

War becomes an emergent, costly strategic choice. Factions begin neutral by
default in living-world scenarios and can prosper without combat.

Concepts from OpenCiv may inform independent OpenRA-native implementation.
Code/assets are not merged by default.

## Consequences

- Headless determinism and telemetry remain foundational because civil systems
  also need reproducible experiments.
- Success metrics expand from win rate to wellbeing, resilience, prosperity,
  knowledge, cohesion, trade, and continuity.
- AI development prioritizes civilization goals before advanced combat
  optimization.
- Dynamic relationship changes require a synchronized diplomacy extension to
  OpenRA's initially configured player masks.
- The persistent-world concept begins inside each match through settlement
  history and faction continuity, then later expands across matches.
