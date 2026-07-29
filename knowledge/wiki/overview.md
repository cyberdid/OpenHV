---
title: Project Overview
status: current
updated: 2026-07-29
sources:
  - ../../README.md
  - ../../run-simulation.sh
  - experiments/2026-07-29-baseline-tournament.md
tags:
  - vision
  - status
---

# Project Overview

## Mission

Build an observable autonomous RTS world inspired by the macro loop of
StarCraft: factions gather resources, construct bases, research technology,
produce armies, scout, attack, defend, and evolve without human players.

The immediate product is a simulation laboratory rather than a conventional
player-controlled game. It must support repeatable experiments, measurable
behavior, and progressively more distinct faction strategies.

## Current state

- OpenHV/OpenRA provides the engine, maps, economy, production, combat, replay,
  and modular-bot systems.
- A local observer can automatically start a free-for-all match with no human
  participant.
- Four AI profiles are selectable: aggressor, economist, technologist, and
  fortress.
- Match composition, map, speed, seed, duration, and result path are
  configurable.
- Matches write JSON statistics; a tournament runner rotates maps and seeds and
  produces aggregate standings.
- The full OpenHV validation suite passes.

## Current limitation

The runner still opens the graphical client. The initial 30-second tournament
is a startup and scoring test, not evidence of full strategic combat:
approximately 24 simulated seconds elapsed per match and all matches ended on
the configured time limit.

## Success criteria

The simulation becomes useful when it can:

1. run unattended without graphics;
2. finish matches naturally or under a documented deterministic cutoff;
3. execute hundreds of reproducible matches;
4. expose economy, production, territory, technology, army, and combat metrics;
5. demonstrate statistically distinguishable AI strategies;
6. turn experiment results into explicit balancing decisions.

## Related pages

- [Architecture](architecture.md)
- [AI profiles](ai-profiles.md)
- [Roadmap](roadmap.md)
- [Baseline tournament](experiments/2026-07-29-baseline-tournament.md)
