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
  - ../../run-batch.py
  - ../../run-simulation.sh
  - ../../run-tournament.sh
  - ../../schemas/simulation-batch-manifest-v1.schema.json
  - experiments/2026-07-29-batch-runner-v1.md
  - experiments/2026-07-29-headless-performance-fix.md
  - decisions/0005-process-isolated-resumable-batches.md
tags:
  - architecture
  - runtime
---

# Simulation Architecture

## Runtime flow

1. `run-batch.py` validates a declarative manifest, resolves defaults and
   overrides, derives stable match IDs/fingerprints, and freezes a schedule
   hash.
2. The batch runner creates one OS process group and one OpenRA support
   directory for each match attempt, with bounded worker concurrency.
3. `run-simulation.sh` translates the resolved match config into OpenRA launch
   arguments and records Git commit/dirty metadata.
4. `SimulationConfig.Parse` resolves the map and rejects unknown bots, game
   speeds, malformed seeds, negative limits, and unavailable maps.
5. `PanelLoadScreen` starts a local server whose lobby RNG is seeded from the
   requested simulation seed, then joins the local client as a spectator.
6. Empty combat slots are populated by cycling through the requested bot
   types. OpenRA's normal color, faction, and spawn selection is deterministic
   because both lobby and player RNG streams are seed-derived.
7. In graphical mode OpenRA executes the normal client loop. In headless mode
   a no-op platform satisfies world/renderer contracts while the loop skips
   UI, input, audio devices, frame presentation, and real-time pacing.
8. Bot modules use the seed-derived `World.BotRandom`; render/audio cosmetics
   continue to use `World.LocalRandom`, so execution mode cannot change later
   strategic choices.
9. A mod-owned callback checks `WorldTick` before each following logic tick.
10. Natural game-over, the synchronized tick limit, or the deadlock watchdog
    calls `SimulationResultWriter`.
11. Artificial terminal conditions finalize the `World` after result capture
    so replay metadata records the terminal tick without changing the captured
    synchronized state.
12. The batch runner validates Result Schema v1 plus config correspondence,
    classifies the attempt, harvests replay/support diagnostics, atomically
    writes status, and aggregates only valid completed results.

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
| `run-batch.py` | Resolves manifests and runs isolated, resumable, validated attempts |
| `schemas/simulation-batch-manifest-v1.schema.json` | Validates schedule and execution controls |
| `batch-manifests/*.json` | Stores reproducible smoke, soak, interruption, and isolation schedules |
| `tests/test_batch_runner.py` | Exercises retry, timeout, signals, resume, drift, and partial artifacts |
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
The normal `Sound` manager also returns before opening sound/music assets when
its backend reports `DummyEngine`; otherwise an immediately-complete no-op
track would continuously advance and decode the music playlist.

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

## Batch orchestration boundary

Every attempt runs in a fresh process group and receives a unique
`OPENHV_SUPPORT_DIR`. This isolates OpenRA settings, logs, caches, replays,
native state, and connection lifecycle. A signal or hard watchdog terminates
the process group rather than only its shell parent.

The requested manifest, resolved manifest, and synchronized match configs are
immutable. Config fingerprints detect per-match drift; the schedule hash
detects any synchronized schedule change. Worker count, infrastructure retry
budget, timeout grace, and replay-sampling interval are execution controls and
may change between resume sessions.

Completed status is trusted only when the stored `result.json` still passes
Schema v1 and matches the resolved config. Infrastructure failures may retry
within a bounded budget; invalid configuration and external cancellation do
not. Any partial `attempt-N` file or directory reserves that attempt number,
preventing hard-crash recovery from overwriting evidence.

Successful support directories are removed after optional replay sampling.
Failed/interrupted attempts retain their support logs and any replay that
OpenRA managed to create. Each session records its exit code and active wall
time; the run summary reports cumulative active runner time across resumes.
See [Decision 0005](decisions/0005-process-isolated-resumable-batches.md).

## Current performance boundary

Correctness and device isolation are verified at 1,500 ticks. After profiling
and bypassing dummy-engine media decoding, repeated runs simulated 30 seconds
in 4.73 and 4.86 wall seconds (6.342× and 6.173× real time). This passes the 5×
Sprint 2 minimum; the aspirational 20× target remains open.

A 100-match four-map, 100-tick sequential soak completed in 364.466 seconds.
The same explicit schedule completed with four workers in 98.061 seconds:
3.717× speedup and 92.9% parallel efficiency. Both runs completed 100/100 with
no retry or infrastructure failure, and resume skipped all 100 without
creating attempts.

This is an infrastructure/startup benchmark, not a late-game performance
claim. Batch performance must still be measured at later-game unit counts and
with telemetry enabled. Synchronized logic, bot computation, allocations/GC,
pathfinding, and local order transport remain likely scaling surfaces. See the
[batch experiment](experiments/2026-07-29-batch-runner-v1.md) and
[performance fix experiment](experiments/2026-07-29-headless-performance-fix.md).
