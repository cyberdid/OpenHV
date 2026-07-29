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
  - ../../apply-engine-patches.sh
  - ../../engine-patches/openra-headless.patch
  - ../../engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs
  - ../../check-headless-equivalence.sh
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
4. `PanelLoadScreen` starts a local server whose lobby RNG is seeded from the
   requested simulation seed, then joins the local client as a spectator.
5. Empty combat slots are populated by cycling through the requested bot
   types. OpenRA's normal color, faction, and spawn selection is deterministic
   because both lobby and player RNG streams are seed-derived.
6. In graphical mode OpenRA executes the normal client loop. In headless mode
   a no-op platform satisfies world/renderer contracts while the loop skips
   UI, input, audio devices, frame presentation, and real-time pacing.
7. Bot modules use the seed-derived `World.BotRandom`; render/audio cosmetics
   continue to use `World.LocalRandom`, so execution mode cannot change later
   strategic choices.
8. A mod-owned callback checks `WorldTick` before each following logic tick.
9. Natural game-over, the synchronized tick limit, or the deadlock watchdog
   calls
   `SimulationResultWriter`.
10. Each match is atomically renamed into place; the tournament runner
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
| `engine-patches/openra-headless.patch` | Adds the engine runtime flag, fast loop, deterministic local server and RNG streams |
| `engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs` | Supplies no-op window, graphics, font, cursor, and sound contracts |
| `apply-engine-patches.sh` / `.ps1` | Idempotently patches a version-pinned downloaded SDK and routes stock bot modules to `BotRandom` |
| `run-simulation.sh` | Launches one reproducible simulation |
| `run-tournament.sh` | Runs a map/seed series and creates standings |
| `check-simulation-determinism.sh` | Compares paired runs, validates schema, and checks invalid input |
| `check-headless-equivalence.sh` | Compares graphical/headless artifacts and proves that device backends were bypassed |

## Result contract v1

Every result records:

- schema, engine, mod, Git commit, and dirty-tree state;
- map request, UID/content hash, title, speed, timestep, requested/effective
  seed, maximum tick, watchdog, telemetry interval, deterministic-simulation
  flag, and requested execution mode;
- actual build execution mode (`graphical` or `headless`);
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
seed-derived bot RNG, maximum world tick, options, and code commit define the
synchronized experiment. GPU/audio warnings, timestamps, output paths,
execution mode, and wall-clock loading time are environmental. Cross-mode
comparisons remove those environmental fields and require identical
synchronized state hashes and final metrics.

`SIMULATION_DURATION` is legacy-only and converts simulated seconds to ticks
using the selected game timestep. `SIMULATION_WATCHDOG_SECONDS` never defines a
valid experimental horizon.

## Headless runtime boundary

`OpenRA.Server` still owns only lobby/network coordination; it does not
construct or advance a gameplay `World`. Headless execution is therefore a
logic-only client, not a dedicated server pretending to be a simulator.

`Engine.Headless=true` selects `HeadlessPlatform`, which implements the
interfaces expected by `Renderer`, `WorldRenderer`, sound, font, textures,
shaders, cursors, and framebuffers without creating SDL, OpenGL, a native
window, or an audio device. The headless loop advances `LogicTick` as fast as
orders are available. UI/cursor work and device presentation are skipped, but
world render-trait ticks remain because some content expects their lifecycle.

`Engine.DeterministicSimulation=true` is separate from headlessness and is
enabled by every simulation launch. It seeds:

- the local server's lobby RNG, which governs valid bot colors and lobby
  randomization;
- normal synchronized `World.SharedRandom`;
- a dedicated `World.BotRandom` for stock and OpenHV bot modules.

The separate bot stream is essential: graphical rendering consumes
`World.LocalRandom` thousands of times and would otherwise alter later bot
orders. Normal player-launched games leave deterministic-simulation mode off,
and `BotRandom` aliases the existing local stream.

Because the SDK `engine/` tree is downloaded and ignored, engine changes live
in the tracked patch/overlay and are automatically re-applied by Unix and
Windows build scripts. Patch compatibility is a required engine-upgrade gate.

## Current performance boundary

Correctness and device isolation are verified at 1,500 ticks, but throughput is
not yet acceptable. The measured headless run simulated 30 seconds in 40.66
wall seconds (0.738× real time), below the 5× minimum. The no-op platform
removed graphics/audio dependencies; synchronized logic, bot computation,
allocations/GC, pathfinding, and local order transport are now the optimization
surface. See the
[headless runtime experiment](experiments/2026-07-29-headless-runtime.md).
