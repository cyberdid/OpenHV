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

Build an observable autonomous real-time world in which factions live rather
than merely fight. They grow populations, build settlements, satisfy needs,
develop technology and institutions, trade, negotiate, migrate, recover from
crises, and may scout, defend, or wage war without human players.

The StarCraft-like RTS layer provides visible geography, logistics,
construction, units, and combat. A civilization layer adds long-term societal
motives and consequences. The immediate product is a simulation laboratory
rather than a conventional player-controlled game.

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
4. expose population, needs, economy, settlement, territory, technology,
   diplomacy, army, and combat metrics;
5. allow factions to coexist, trade, prosper, stagnate, migrate, fragment, or
   collapse without mandatory war;
6. demonstrate statistically distinguishable societal and military strategies;
7. make warfare costly to population, economy, stability, and diplomacy;
8. turn experiment results into explicit design and balancing decisions.

## Related pages

- [Architecture](architecture.md)
- [AI profiles](ai-profiles.md)
- [Living factions](faction-life.md)
- [Roadmap](roadmap.md)
- [Baseline tournament](experiments/2026-07-29-baseline-tournament.md)
