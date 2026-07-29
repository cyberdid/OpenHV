---
title: Simulation Architecture
status: current
updated: 2026-07-29
sources:
  - ../../OpenRA.Mods.HV/LoadScreens/PanelLoadScreen.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationConfig.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationEndReason.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationResult.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationResultWriter.cs
  - ../../schemas/simulation-result-v1.schema.json
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
   arguments and records Git commit/dirty metadata.
3. `SimulationConfig.Parse` resolves the map and rejects unknown bots, game
   speeds, malformed seeds, negative limits, and unavailable maps.
4. `PanelLoadScreen` starts a local server and joins the local client as a
   spectator.
5. Empty combat slots are populated by cycling through the requested bot
   types. A second lobby phase assigns deterministic, map-valid preset colors
   before starting the match.
6. OpenRA executes the normal synchronized game loop. A mod-owned callback
   checks `WorldTick` before each following logic tick.
7. Natural game-over, the synchronized tick limit, or the deadlock watchdog
   calls
   `SimulationResultWriter`.
8. Each match is atomically renamed into place; the tournament runner
   aggregates end reasons, natural wins, score leads, and per-profile averages
   with `jq`.

## Components

| Component | Responsibility |
|---|---|
| `mods/hv/rules/bots.yaml` | Defines bot types and strategy-specific modules |
| `SimulationConfig.cs` | Parses and validates versioned match inputs |
| `SimulationEndReason.cs` | Defines stable machine-readable lifecycle outcomes |
| `PanelLoadScreen.cs` | Deterministically constructs, starts, monitors, and stops autonomous lobbies |
| `SimulationResult.cs` | Defines the strongly typed result contract |
| `SimulationResultWriter.cs` | Captures synchronized state/statistics and atomically writes JSON |
| `schemas/simulation-result-v1.schema.json` | Validates serialized result artifacts |
| `run-simulation.sh` | Launches one reproducible simulation |
| `run-tournament.sh` | Runs a map/seed series and creates standings |
| `check-simulation-determinism.sh` | Compares paired runs, validates schema, and checks invalid input |

## Result contract v1

Every result records:

- schema, engine, mod, Git commit, and dirty-tree state;
- map request, UID/content hash, title, speed, timestep, requested/effective
  seed, maximum tick, watchdog, and telemetry interval;
- ordered slots with bot, resolved faction, team, deterministic color, spawn,
  and home cell;
- end reason/detail, final world tick, simulated seconds, and synchronized
  state hash;
- natural winners and score leader as separate fields;
- final per-player combat and economy statistics.

The current composite score is:

`kills value - deaths value + army value + assets value + cash/resources + experience × 100`

`naturalWinners` contains only OpenRA players whose synchronized `WinState` is
`Won`. `scoreLeader` is the top composite score regardless of outcome. A
tick-limited match can therefore have a score leader and no natural winner.

## Reproducibility boundary

The match seed, map UID/hash, ordered bots, deterministic lobby assignments,
maximum world tick, options, and code commit define the synchronized
experiment. GPU/audio warnings, timestamps, output paths, and wall-clock
loading time are environmental. Comparisons remove those environmental fields
and require identical synchronized state hashes and final metrics.

`SIMULATION_DURATION` is legacy-only and converts simulated seconds to ticks
using the selected game timestep. `SIMULATION_WATCHDOG_SECONDS` never defines a
valid experimental horizon.

## Headless dependency trace

Rendering and audio are still initialized for every match:

- the normal client startup creates SDL, a renderer, a window, and audio before
  `PanelLoadScreen.StartGame`;
- `Game.Loop` schedules logic and rendering together;
- `OpenRA.Server` owns lobby/network coordination but does not construct or
  advance a gameplay `World`;
- the SDK `engine/` tree is downloaded and ignored by this repository, so a
  durable engine seam must be carried as a reproducible SDK patch or upstream
  engine revision, not an untracked local edit.

The next spike must isolate the minimum client services required by
`World`, `OrderManager`, bot orders, and result capture while bypassing
renderer, window, and audio initialization.
