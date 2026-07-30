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
  the initial failed throughput gate.
- [Headless dummy-audio performance fix, 2026-07-29](experiments/2026-07-29-headless-performance-fix.md)
  — managed profile, 88% wall-time reduction, repeated 6.17×+ throughput, and
  the closed Sprint 2 gate.
- [Resumable batch runner v1 validation, 2026-07-29](experiments/2026-07-29-batch-runner-v1.md)
  — process/support isolation, 100-match sequential and parallel soaks,
  failure taxonomy, interruption/resume, and verified replay artifacts.
- [Living Factions and Telemetry v1 validation, 2026-07-29](experiments/2026-07-29-living-factions-v1.md)
  — synchronized settlement life, balanced growth, food-scarcity mortality,
  peaceful Steward AI, JSONL schemas, determinism, and telemetry overhead.
- [Civil Research v1 validation, 2026-07-29](experiments/2026-07-29-civil-research-v1.md)
  — deterministic knowledge spending, first technology unlock, output modifier,
  and reason-coded research event.
- [Dynamic Diplomacy v1 validation, 2026-07-29](experiments/2026-07-29-dynamic-diplomacy-v1.md)
  — default neutrality, engine-effective war/peace masks, exhaustion,
  reason-coded transitions, and deterministic repeat.
- [Stock-Backed Trade v1 validation, 2026-07-29](experiments/2026-07-29-stock-backed-trade-v1.md)
  — real stock transfer, asymmetric production, route capacity/risk, war
  suspension, paired civil outcomes, and deterministic repeat.
- [Civilization AI and War Cost v1 validation, 2026-07-29](experiments/2026-07-29-civilization-ai-war-cost-v1.md)
  — multi-objective strategies, research opportunity costs, dependency-driven
  peace, mobilization, population casualties, recovery, and repeatability.
- [Scenario Lifecycle v1 validation, 2026-07-29](experiments/2026-07-29-scenario-lifecycle-v1.md)
  — observation horizons, partial and total faction collapse, hard ceilings,
  active-war stalemate protection, and deterministic repeat.
- [Civil and Military Baseline 112 v1, 2026-07-29](experiments/2026-07-29-baseline-112-v1.md)
  — clean-commit 112-match held-out matrix, uncertainty, civil/military
  profile differences, map sensitivity, and the failed natural-outcome gate.
- [Strategic Interval DIP-001, 2026-07-30](experiments/2026-07-30-strategic-interval-dip001.md)
  — every war involved one profile because diplomacy ran twice per match,
  and the rejected candidate that made war universal by making it brief.
- [Reachable Technology ECON-002, 2026-07-30](experiments/2026-07-30-reachable-technology-econ002.md)
  — the two technologies nobody had ever completed, the arithmetic that
  made them unreachable, and the halved costs that turned research into a
  profile difference.
- [Levelling Trade ECON-001, 2026-07-30](experiments/2026-07-30-levelling-trade-econ001.md)
  — the trade system that had moved zero goods in 672 routes, the reserve
  floor no settlement was ever below, and the accepted levelling rule that
  made war expensive for the one profile that will not trade.
- [Regroup Locality AI-005, 2026-07-30](experiments/2026-07-30-regroup-locality-ai005.md)
  — nearest-building fallback for retreating squads, the schema break that
  had left the matrix unusable, and a null result explained by how rarely
  squads regroup at all.
- [Combat Planner AI-004, 2026-07-30](experiments/2026-07-30-combat-planner-ai004.md)
  — squad target scoring, force preservation, the defending-squad retreat path,
  and the rejected candidate whose preserved squads produced more wars.
- [Civilization Planner AI-003, 2026-07-29](experiments/2026-07-29-civilization-planner-ai003.md)
  — executable opening/economy/technology/recovery plans, bounded native
  production requests, decision telemetry, deterministic smoke evidence, and
  the frozen paired candidate gate.

## Decisions

- [0001: Use OpenHV/OpenRA as the foundation](decisions/0001-openhv-foundation.md)
- [0002: Maintain persistent project memory](decisions/0002-persistent-project-wiki.md)
- [0003: Model living factions, not only war](decisions/0003-living-factions-before-war.md)
- [0004: Accept the logic-only headless client runtime](decisions/0004-logic-only-headless-runtime.md)
- [0005: Use process-isolated resumable batches](decisions/0005-process-isolated-resumable-batches.md)

## Operations

- [Project log](log.md) — append-only chronology of ingests, changes,
  experiments, queries, and lint passes.
- [Agent schema](../../AGENTS.md) — maintenance and evidence rules.
- [Raw sources](../raw/README.md) — immutable evidence layer.
