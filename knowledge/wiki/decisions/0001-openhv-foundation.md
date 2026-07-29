---
title: "Decision 0001: Use OpenHV/OpenRA as the Foundation"
status: accepted
updated: 2026-07-29
sources:
  - ../../../README.md
tags:
  - decision
  - engine
---

# Decision 0001: Use OpenHV/OpenRA as the Foundation

## Context

The project needs an RTS simulation with economy, base building, production,
combat, maps, replays, and autonomous players. Building every subsystem from
zero would postpone experiments.

## Decision

Use OpenHV as the mod and content foundation and OpenRA as the engine. Add the
autonomous orchestration, profiles, telemetry, and experiment layer locally.

## Consequences

- The first simulator became runnable quickly.
- Existing modular bot traits provide practical strategy controls.
- GPL-compatible development and upstream structure must be respected.
- Engine architecture constrains headless execution and may require carefully
  scoped OpenRA changes.
