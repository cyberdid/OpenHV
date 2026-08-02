---
title: Simulation Architecture
status: current
updated: 2026-08-02
sources:
  - universe-north-star.md
  - decisions/0006-single-dotnet-universe-runtime.md
  - ../../run-universe.sh
  - ../../OpenRA.Mods.HV/LoadScreens/PanelLoadScreen.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationConfig.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationEndReason.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationResult.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationResultWriter.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationTelemetryWriter.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationUniverseSnapshotBuilder.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationCheckpointManifestWriter.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationLifecycleMonitor.cs
  - ../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
  - ../../OpenRA.Mods.HV/Traits/World/CivilizationScenario.cs
  - ../../OpenRA.Mods.HV/Traits/World/DiplomacyManager.cs
  - ../../OpenRA.Mods.HV/Traits/World/TradeManager.cs
  - ../../OpenRA.Mods.HV/Traits/World/UniverseState.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationDiplomacySnapshotBuilder.cs
  - ../../OpenRA.Mods.HV/Simulation/SimulationTradeSnapshotBuilder.cs
  - ../../schemas/simulation-result-v1.schema.json
  - ../../schemas/simulation-telemetry-v1.schema.json
  - ../../schemas/simulation-event-v1.schema.json
  - ../../schemas/universe-checkpoint-v1.schema.json
  - ../../apply-engine-patches.sh
  - ../../engine-patches/openra-headless.patch
  - ../../engine-patches/openra-ai-combat.patch
  - ../../engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs
  - ../../check-headless-equivalence.sh
  - ../../check-universe-checkpoint-equivalence.sh
  - ../../run-batch.py
  - ../../generate-baseline-manifest.py
  - ../../analyze-baseline.py
  - ../../batch-manifests/baseline-112-v1.json
  - ../../run-simulation.sh
  - ../../run-tournament.sh
  - ../../schemas/simulation-batch-manifest-v1.schema.json
  - experiments/2026-07-29-batch-runner-v1.md
  - experiments/2026-07-29-headless-performance-fix.md
  - decisions/0005-process-isolated-resumable-batches.md
  - experiments/2026-07-29-dynamic-diplomacy-v1.md
  - experiments/2026-07-29-stock-backed-trade-v1.md
  - experiments/2026-07-29-civilization-ai-war-cost-v1.md
  - experiments/2026-07-29-scenario-lifecycle-v1.md
  - experiments/2026-07-29-baseline-112-v1.md
tags:
  - architecture
  - runtime
---

# Simulation Architecture

## Product runtime boundary

The target boundary is defined by [Decision 0006](decisions/0006-single-dotnet-universe-runtime.md):
one C# state graph owns three planet slots, orbital space, civilization, and
RTS. `run-universe.sh` proves one-process observer operation today, but its
pre-spawned match is transitional until the Universe/Planet kernel and
zero-state lifecycle replace it.

`run-universe.sh` is the canonical visual runtime. It configures a graphical
`living-world` match with four Civilization AI profiles, trade enabled,
ordinary visual speed, a disabled wall-clock watchdog, and an effectively
unbounded synchronized horizon. `PanelLoadScreen` creates the local server,
places every bot, switches the local client to spectator, and starts the world
without any faction, cell, or battle selection.

Civil settlements, population, needs, research, trade, diplomacy, production,
units, and combat therefore advance in the same synchronized OpenHV `World`.
The Python/Web planet and `fight-cell.sh` are observer/bridge tools, not parts
of the canonical product runtime. If OpenHV reports a natural victory or total
civil collapse, the wrapper starts a new deterministic epoch; closing the
window produces no result and stops the wrapper.

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
9. `UniverseState` advances a synchronized integer macro clock and owns stable
   Universe/system identity plus three immutable planet slots. Tyranthos is
   active and lifeless; the other two slots exist but remain inactive.
10. `SettlementCore` actors advance civil and demographic pulses using only
   synchronized integer state; infrastructure is assigned by distance and
   actor-ID tie-breaking.
11. `CivilizationState` selects an opening, economy, technology, or recovery
    plan. A conditional bot module converts the plan into bounded production
    requests while the settlement model applies its synchronized opportunity
    costs and output bonuses.
12. When enabled, a mod-owned observer writes periodic JSONL snapshots and
    derives reason-coded civil events without mutating synchronized state.
13. A mod-owned callback checks `WorldTick` before each following logic tick.
14. Natural game-over, the synchronized tick limit, or the deadlock watchdog
    calls `SimulationResultWriter`.
15. Artificial terminal conditions finalize the `World` after result capture
    so replay metadata records the terminal tick without changing the captured
    synchronized state.
16. The batch runner validates Result Schema v1 plus config correspondence,
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
| `SimulationTelemetryWriter.cs` | Writes periodic JSONL snapshots and reason-coded civil events |
| `UniverseState.cs` | Owns synchronized Universe/system identity, three planet slots, lifecycle stage, activation, and macro time |
| `SimulationUniverseSnapshotBuilder.cs` | Projects the synchronized Universe root into result and telemetry contracts |
| `SimulationCheckpointManifestWriter.cs` | Atomically writes the versioned JSON companion for native OpenRA game saves |
| `CivilizationState.cs` | Defines synchronized civilization, settlement, and civil-infrastructure traits |
| `CivilizationPlannerBotModule.cs` | Converts civilization plans into bounded economic, technical, and recovery production requests |
| `CivilizationScenario.cs` | Defines synchronized balanced/scarcity lobby profiles |
| `schemas/simulation-result-v1.schema.json` | Validates serialized result artifacts |
| `schemas/simulation-telemetry-v1.schema.json` | Validates each periodic snapshot record |
| `schemas/simulation-event-v1.schema.json` | Validates each lifecycle/civil event record |
| `engine-patches/openra-headless.patch` | Adds the engine runtime flag, fast loop, deterministic local server and RNG streams |
| `engine-patches/openra-ai-combat.patch` | Adds squad target scoring, retreat/regroup/re-engagement, the defending-squad retreat path, and the decision notification interface |
| `engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs` | Supplies no-op window, graphics, font, cursor, and sound contracts |
| `apply-engine-patches.sh` / `.ps1` | Idempotently patches a version-pinned downloaded SDK and routes stock bot modules to `BotRandom` |
| `run-simulation.sh` | Launches one reproducible simulation |
| `run-batch.py` | Resolves manifests and runs isolated, resumable, validated attempts |
| `schemas/simulation-batch-manifest-v1.schema.json` | Validates schedule and execution controls |
| `batch-manifests/*.json` | Stores reproducible smoke, soak, failure, and Living Factions schedules |
| `analyze-baseline.py` / `compare-candidate.py` | Aggregates clean runs and performs paired exact-schedule candidate comparisons |
| `tests/test_batch_runner.py` | Exercises retry, timeout, signals, resume, drift, and partial artifacts |
| `run-tournament.sh` | Runs a map/seed series and creates standings |
| `check-simulation-determinism.sh` | Compares paired runs, validates schema, and checks invalid input |
| `check-headless-equivalence.sh` | Compares graphical/headless artifacts and proves that device backends were bypassed |
| `check-universe-checkpoint-equivalence.sh` | Forks one checkpoint into continued/resumed branches and requires identical full state |

`CivilizationState.MilitaryArmyValue` is the canonical civil/strategic measure
of armed power. It excludes explicit worker and support actor value and is used
by both workforce mobilization and diplomatic relative-power scoring; raw
`PlayerStatistics.ArmyValue` remains the RTS result statistic.

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
- final per-player combat/economy statistics and optional backward-compatible
  civilization/settlement state;
- synchronized `civilizationProfile` when produced by the current runtime;
- stable Universe/system IDs, macro day/remainder, and exactly three planet
  slots with activation, lifecycle, and optional native-race identity.

Telemetry-enabled matches additionally write:

- `telemetry.jsonl`: tick, synchronized hash, battle/economy counters, and
  complete civilization/settlement, bilateral diplomacy, and trade-route
  snapshots;
- `events.jsonl`: match lifecycle, settlement founding, population changes,
  research, shortage start/resolution, and diplomacy transitions with stable
  reason codes, plus route-state, stock-shipment, plan-transition, and bounded
  planner-request events.

Both streams are line-flushed so a process failure retains complete prior
records. A retry or resume moves an existing stream to an attempt-qualified
artifact before the canonical path is recreated.

## Planetary physical state

Every `PlanetState` owns a `PlanetPhysicalState` registered in stable Universe
hash order. Immutable definitions provide mass, radius, orbit, rotation, axial
tilt, eccentricity, stellar flux, and plate count for all three planet slots.
Mutable fixed-point fields track geological age, radiative equilibrium,
temperature, energy imbalance, atmosphere pressure/composition, condensed
surface water, ocean coverage, albedo, tectonic activity, and climate-pulse
sequence. There are no floating-point values in authoritative evolution.

During the lifeless accelerated timescale, one macro day represents one million
geological years. The active planet autonomously cools toward its integer
radiative equilibrium; orbital phase perturbs stellar flux; water vapor
condenses below the boiling threshold; ocean coverage changes albedo; wet
weathering removes CO₂; and tectonics outgas atmosphere and decay slowly. The
two reserved planets already have different physical seeds but do not advance
until activated. Result, telemetry, checkpoint manifest, full sync hash, and
native save data all expose or preserve the same state.

Each planet also owns a native `PlanetSurfaceState`: a 180×360 equirectangular
surface with 64,800 stable cells, partitioned into 450 independent 12×12
chunks. Coordinate-derived IDs survive renderer changes and reloads. Seeded
integer generation assigns tectonic plates, oceanic/continental/boundary crust,
elevation, basin/shelf/lowland/highland/mountain/volcanic terrain, and mineral
richness. Longitude wraps; latitude remains bounded.

The large cell arrays are not traversed by the normal 50 Hz RTS sync hash.
Instead, every mutation closes by recomputing a synchronized domain digest.
Immutable geology is regenerated from its seed during load and must match the
saved topology digest. Mutable water depth is Deflate-compressed in the native
save and must match its saved hydrology digest. This reduced the experimental
728 KiB textual payload that exceeded OpenRA's limit to a complete 23 KiB
`.orasav` while preserving exact branch parity.

This is still not the finished Phase 2 model. Spatial atmosphere, wind,
hydrology, golden calibration, native overlays, and measured chunk LOD budgets
remain required.

## Universe checkpoints

`Launch.SimulationCheckpointWorldTick` requests a save only when
`UniverseState.MacroTickRemainder` is zero. The observer temporarily freezes
world advancement while the server commits OpenRA's native `.orasav`, then
writes an atomic `universe-checkpoint-v1` JSON companion containing the exact
request tick, synchronized hash, and Universe snapshot. The process unpauses
without user input. `Launch.SimulationLoadCheckpoint` reconstructs the saved
lobby, actors, orders, AI slots, RNG streams, and versioned Universe trait
payload, then starts the observer and continues to the configured horizon.

The checkpoint contract includes the dedicated BotRandom position, pending bot
decisions, BaseBuilder queue state, production progress, player resources,
periodic cash state, civilization decisions, and settlement stocks and timers.
Universe trait-save schema 3 additionally persists physical state and compressed
mutable surface layers while validating regenerated geology by digest.
The barrier drains in-flight network orders without advancing the world, then
commits the native save and manifest on one macro boundary.

`check-universe-checkpoint-equivalence.sh` compares two branches from that same
checkpoint: one continues in-process and one reloads in a new process. It
requires identical normalized result JSON, full `World.SyncHash`, BotRandom
count, faction metrics, and Universe snapshot, then validates all three JSON
artifacts. The current seed-112 four-bot acceptance run, including global
physics and all three 64,800-cell surfaces, passed at tick 500 with hash
`12C56135` and BotRandom count `578`.

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

## Baseline analysis boundary

`generate-baseline-manifest.py` is the deterministic source for
`baseline-112-v1.json`. The matrix crosses four held-out maps, four cyclic
profile-to-slot assignments, and seven seeds, giving every profile exactly
seven observations in every map/slot cell. `--check` makes generated-manifest
drift a test failure.

`analyze-baseline.py` refuses incomplete batches, mixed commits, dirty runtime
trees, or matches without exactly one of each four military profiles. It
produces match- and player-grain CSV plus a JSON report with profile, map,
faction, and spawn breakdowns. Binary rates use Wilson 95% intervals;
continuous means use a deterministic 2,000-resample nonparametric bootstrap,
and candidate profile differences are paired within each match before
resampling.
Timed score leadership remains separate from natural victory. The full run
artifacts stay outside Git; compact immutable evidence is compiled into the
project Wiki.

## Diplomacy boundary

`DiplomacyManager` is a world trait enabled by deterministic simulation mode;
ordinary OpenHV player matches retain their lobby relationships. On simulation
world load it enumerates active playable factions in client-index order,
creates one synchronized effect per unordered pair, and clears the
lobby-created ally/enemy bits so every pair begins neutral. Strategic pulses
change synchronized integer state and apply the corresponding native OpenRA
player masks: war sets reciprocal enemy bits; neutral clears enemy and ally
bits; alliance support exists at the state/mask level but has no policy
transition yet.

Pair state includes grievance, trust, war exhaustion, war start, peace
cooldown, transition tick/sequence, and loss baselines. References and display
labels remain derived; only supported integer fields carry `[VerifySync]`.
`SimulationDiplomacySnapshotBuilder` maps the synchronized state into stable
identifiers for final Result v1, every telemetry snapshot, and
`diplomacy-transition` events. Result v1 keeps the top-level field optional for
backward compatibility; current telemetry requires it. See
[Dynamic Diplomacy v1 Validation](experiments/2026-07-29-dynamic-diplomacy-v1.md).

## Trade boundary

`TradeManager` is a synchronized world trait gated by both deterministic
simulation mode and the `tradeEnabled` lobby option. It creates one strategic
route for each diplomacy pair. Every 250 ticks, a non-hostile route selects
the largest deterministic stock movement across food, materials, and energy:
source stock above reserve, destination stock below reserve, free destination
storage, and effective capacity all bound the amount. The manager mutates the
same `SettlementCore` stock fields consumed by the civil pulse.

Capital and Trader `CivilInfrastructure.TradeCapacity` supply endpoint
throughput. Manhattan capital distance and wars with third parties contribute
0–750 operational risk; direct bilateral war sets risk 1000 and capacity zero.
Routes expose synchronized status/sequence, capacity, risk, last shipment, and
six directional resource totals. `SimulationTradeSnapshotBuilder` exports
these to final results and every snapshot; telemetry derives
`trade-route-state` and `trade-shipment` events. See
[Stock-Backed Trade v1 Validation](experiments/2026-07-29-stock-backed-trade-v1.md).

## Civilization strategy and war-cost boundary

`CivilizationState` owns the first synchronized strategic blackboard and state
machine. At its 250-tick decision/research pulse it aggregates settlement
needs and stability, diplomacy wars, trade-route dependency, player
army/assets, completed/current research, and prior casualties. It stores six
0–1000 utilities and one state: development, survival, research, trade,
mobilization, or recovery.

The pair-specific war utility replaces hard-coded diplomacy pressure. It
combines personality, relative power, shortages, instability, import
dependency on that partner, current research commitment, and casualty
aversion. Strategy limits research spending; research consumes real knowledge,
materials, and energy.

`SettlementCore` translates military state into civil cost. Army value reserves
adult workforce, with a higher ratio during active wars; available workforce
scales all four civil outputs. New death value converts to adult casualties at
the primary settlement. Wars and casualties lower the stability target and
increase migration pressure. All fields and `strategy-transition` events are
exported by existing civilization snapshots. See
[Civilization AI and War Cost v1 Validation](experiments/2026-07-29-civilization-ai-war-cost-v1.md).

## Combat decision boundary

`SquadManagerBotModule` scores candidate targets from value, damage, distance,
building and construction-yard bonuses, and a finishing bonus for owners whose
recorded losses and remaining army/assets fall under the profile thresholds.
`ShouldRetreat` combines the stock fuzzy attack decision with explicit
low-health-and-power thresholds. Ground squads that retreat move to an own
building, regroup for `RegroupTicks`, and then re-engage instead of dissolving.

Defending squads need a separate path. `StateBase.ShouldFlee` refuses to flee
whenever an own building stands inside the danger radius, which is the
permanent condition for a protection squad, so `ShouldRetreatUnderThreat`
skips that veto. It deliberately omits the stock fuzzy decision, leaving only
the explicit thresholds, and applies only to threat-based retreats: losing the
target still uses the stock flee state that dissolves the squad and returns its
units. Raising `DangerScanRadius` above the engine default widens the veto and
suppresses retreat, so the profiles keep the default.

`CivilizationState` implements `INotifySquadDecision` and stores the last
decision, reason, squad type, unit count, target actor, own/enemy value, and
one counter per decision kind as synchronized integers. A faction that never
commanded a squad exports a null decision rather than the default enum member,
so decision distributions cannot count selections that never happened.

Result v1 carries the exact counters; `combat-decision` events are sampled at
the telemetry interval and are therefore a timeline, not a complete log. The
baseline analyzer separates the null "none" of a current quiet faction from the
"unavailable" of a pre-AI-004 artifact, and both remain sortable strings.

## Scenario lifecycle boundary

`SimulationConfig` separates the scenario's semantic boundary from its hard
safety ceiling. Conflict mode has only `maxWorldTicks`; living-world mode also
requires a positive `observationHorizonTicks` no greater than that ceiling.
`PanelLoadScreen` applies terminal conditions in deterministic order: all
factions collapsed, living-world observation horizon, explicitly terminating
stalemate advisory, hard tick limit, then normal engine victory.

`SimulationLifecycleMonitor` owns civil/engine collapse observations and the
conservative quiet-window signature. Civil collapse thresholds are checked
every 250 synchronized ticks; one collapsed faction is recorded but does not
stop the others. The detector regards changes in economy, combat, army/assets,
population/stocks, research/technology, or trade shipments as meaningful
activity. Active war always suppresses a stalemate advisory. Advisory mode is
the default, and termination requires an explicit launch option.

Every result and telemetry snapshot exports the complete lifecycle state.
Events record `faction-collapsed`, `stalemate-advisory`, and
`stalemate-cleared`; batch manifests and fingerprints include every lifecycle
option. Result Schema v1 keeps the new top-level object optional for legacy
artifacts, while current Telemetry Schema v1 requires it. See
[Scenario Lifecycle v1 Validation](experiments/2026-07-29-scenario-lifecycle-v1.md).

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
