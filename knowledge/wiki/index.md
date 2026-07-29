---
title: Universe RTS Project Wiki
status: current
updated: 2026-07-29
sources:
  - ../raw/karpathy-llm-wiki-2026-07-29.md
tags:
  - index
  - project
---

# Universe RTS Project Wiki

This is the maintained entry point for the autonomous RTS simulation project.

## Project

- [Overview](overview.md) — mission, present capabilities, constraints, and
  success criteria.
- [Architecture](architecture.md) — runtime components and data flow.
- [AI profiles](ai-profiles.md) — current strategy personalities and their
  intended behavior.
- [Living factions](faction-life.md) — population, settlements, needs,
  research, trade, diplomacy, culture, and the first civil vertical slice.
- [Roadmap](roadmap.md) — ordered next milestones and acceptance criteria.
- [Detailed execution plan](execution-plan.md) — phased implementation,
  experiments, gates, risks, backlog, and delivery sequence.

## Experiments

- [Baseline tournament, 2026-07-29](experiments/2026-07-29-baseline-tournament.md)
  — first ten-match timed comparison of four AI profiles.
- [Simulation contract v1 validation, 2026-07-29](experiments/2026-07-29-simulation-contract-v1.md)
  — exact tick cutoff, repeat hash/metrics, schema validation, and invalid-input
  evidence.
- [Deterministic headless runtime validation, 2026-07-29](experiments/2026-07-29-headless-runtime.md)
  — no-device execution, 1,500-tick graphical parity, repeat determinism, and
  the still-open throughput gate.

## Decisions

- [0001: Use OpenHV/OpenRA as the foundation](decisions/0001-openhv-foundation.md)
- [0002: Maintain persistent project memory](decisions/0002-persistent-project-wiki.md)
- [0003: Model living factions, not only war](decisions/0003-living-factions-before-war.md)
- [0004: Accept the logic-only headless client runtime](decisions/0004-logic-only-headless-runtime.md)

## Operations

- [Project log](log.md) — append-only chronology of ingests, changes,
  experiments, queries, and lint passes.
- [Agent schema](../../AGENTS.md) — maintenance and evidence rules.
- [Raw sources](../raw/README.md) — immutable evidence layer.
