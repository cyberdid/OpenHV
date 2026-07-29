---
title: Simulation Architecture
status: current
updated: 2026-07-29
sources:
  - ../../OpenRA.Mods.HV/LoadScreens/PanelLoadScreen.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationResultWriter.cs
  - ../../run-simulation.sh
  - ../../run-tournament.sh
tags:
  - architecture
  - runtime
---

# Simulation Architecture

## Runtime flow

1. `run-tournament.sh` selects a map and deterministic seed for each match.
2. `run-simulation.sh` translates environment variables into OpenRA launch
   arguments.
3. `PanelLoadScreen` starts a local server and joins the local client as a
   spectator.
4. Empty combat slots are populated by cycling through the requested bot types.
5. OpenRA executes the normal synchronized game loop.
6. Natural game-over or the configured duration calls
   `SimulationResultWriter`.
7. Each match writes JSON; the tournament runner aggregates match leaders and
   per-profile averages with `jq`.

## Components

| Component | Responsibility |
|---|---|
| `mods/hv/rules/bots.yaml` | Defines bot types and strategy-specific modules |
| `PanelLoadScreen.cs` | Constructs and starts autonomous lobbies |
| `SimulationResultWriter.cs` | Captures player statistics and ranks results |
| `run-simulation.sh` | Launches one reproducible simulation |
| `run-tournament.sh` | Runs a map/seed series and creates standings |

## Result scoring

For a timed match, the current composite score is:

`kills value - deaths value + army value + assets value + cash/resources + experience × 100`

A natural OpenRA win state takes ordering priority. If nobody has won when the
timer expires, the top composite score is stored in the `winner` field. Reports
must identify this as a timed score leader, not proof of annihilation.

## Reproducibility boundary

The match seed, map, bots, duration, and code commit are required experiment
inputs. GPU/audio warnings and wall-clock loading time are environmental noise;
game-state comparisons should use synchronized world ticks and recorded
statistics.

## Known architectural gap

Rendering and audio are still initialized for every match. A dedicated
headless host or server-side result hook is required to make large experiment
batches efficient.
