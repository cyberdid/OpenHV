---
title: Detailed Execution Plan
status: current
updated: 2026-07-29
sources:
  - overview.md
  - architecture.md
  - roadmap.md
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-simulation-contract-v1.md
  - experiments/2026-07-29-headless-runtime.md
  - experiments/2026-07-29-headless-performance-fix.md
  - ../../engine/OpenRA.Game/Game.cs
  - ../../engine/OpenRA.Server/Program.cs
tags:
  - planning
  - execution
  - milestones
---

# Detailed Execution Plan

## Strategic objective

Turn the current graphical proof of concept into a fast, deterministic,
observable laboratory for living autonomous factions. Add population,
settlements, civil resource flows, research, trade, and diplomacy before
treating military optimization as the center of faction development.

The order matters:

`correct simulation → headless speed → living-faction model → telemetry → reproducible experiments → civilization AI → faction identity → adaptation → persistent world`

War remains a deep RTS subsystem, but it is an emergent strategic choice rather
than the default purpose of every system. Balancing before measurement or
adding a metagame before stable matches would compound uncertainty.

## Direction correction: society before conquest

The initial prototype proves autonomous combat infrastructure but is too close
to a war tournament. The product direction is now defined by
[Living Factions Design](faction-life.md):

- population and needs make settlements worth protecting;
- civil production and technology create non-military progress;
- trade and migration connect factions during peace;
- dynamic diplomacy allows neutrality, cooperation, rivalry, war, and peace;
- war damages workforce, infrastructure, stability, and relationships;
- faction success includes continuity, wellbeing, resilience, knowledge, and
  influence, not only victory count.

Headless execution and telemetry remain the first engineering dependencies
because the civil model also needs fast, reproducible experiments.

## Verified starting point

- The autonomous launcher can populate a match with four AI profiles and make
  the local client spectate.
- Map, bot composition, seed, synchronized tick horizon, watchdog, telemetry
  interval, match ID, and output path are configurable and validated.
- Result contract v1 records build/map/slot metadata, synchronized state hash,
  explicit end reason, natural winners, score leader, and final metrics.
- A 1,500-tick graphical/headless pair and a repeated headless run reached the
  same hash `0AC799D4`, lobby assignments, and metrics.
- Ten timed matches completed without manual intervention.
- Headless matches initialize no SDL window, OpenGL context, or audio device;
  graphical inspection remains available.
- The primary cutoff is `WorldTick`; wall clock is only a deadlock watchdog.
- `OpenRA.Server` coordinates lobby/network state but does not advance the game
  `World`; a dedicated server alone is not a headless simulator.
- The accepted engine seam retains the production client world path but uses a
  no-op platform, an unpaced loop, seeded server RNG, and a renderer-independent
  bot RNG stream.
- After the dummy-audio profile/fix, repeated headless throughput is
  6.173×–6.342× real time; the 5× minimum is complete.

## North-star qualities

### Correctness

- Natural victory, simulation cutoff, stalemate, invalid configuration, and
  crash are different outcomes.
- A timed score leader is never presented as a natural winner.
- Match results derive from synchronized game state.

### Reproducibility

- Commit, engine/mod version, map identity, bot versions, slots, factions,
  spawn positions, seed, options, and tick cutoff are recorded.
- Identical inputs on the same build produce identical synchronized state and
  final metrics.

### Throughput

- No renderer, window, or audio device is initialized in batch mode.
- The minimum useful gate is 5× real-time simulation; the initial target is
  20× or better on the development machine.
- A 100-match batch is routine rather than an overnight manual process.

### Observability

- Every important result has a documented definition.
- Final statistics and time-series telemetry are versioned.
- AI decisions expose reason codes, not only resulting orders.

### Scientific discipline

- One hypothesis and one primary variable per balance experiment.
- Baseline and candidate use paired maps, spawns, and seeds.
- Raw evidence is immutable; interpretation can evolve.
- Failed hypotheses are recorded, not silently discarded.

## Phase 0 — Formalize the simulation contract

Goal: remove ambiguity before changing the engine.

Status: complete on 2026-07-29 for the graphical reference runtime. Evidence:
[Simulation Contract v1 Validation](experiments/2026-07-29-simulation-contract-v1.md).

### Work

1. Define a versioned `SimulationConfig`:
   - match ID;
   - map UID/path and map hash;
   - ordered player slots;
   - bot profile, faction, team, color, and spawn for each slot;
   - random seed;
   - game speed/options;
   - maximum world ticks;
   - wall-clock watchdog;
   - telemetry interval;
   - artifact directory.
2. Define `EndReason`:
   - `natural-victory`;
   - `faction-collapse`;
   - `observation-horizon`;
   - `world-tick-limit`;
   - `stalemate`;
   - `invalid-configuration`;
   - `desync`;
   - `crash`;
   - `external-cancel`.
3. Define process behavior:
   - expected match outcomes return success and are distinguished in JSON;
   - invalid configuration and crashes return non-zero;
   - result files are written atomically;
   - partial artifacts remain diagnosable.
4. Replace the primary wall-clock duration with a synchronized world-tick
   limit. Keep wall clock only as a deadlock watchdog.
5. Add `schemaVersion`, `engineVersion`, `modVersion`, and Git commit to every
   result.

Implementation notes:

- The serialized contract is
  `schemas/simulation-result-v1.schema.json`.
- `watchdog-timeout` was added to the original reason list so infrastructure
  failure cannot be confused with a synchronized observation horizon.
- The mod checks the completed `WorldTick` before allowing the next logic tick
  and locally pauses the world before result capture.
- The local server RNG is seeded from the requested simulation seed, preserving
  the stock map-valid color picker while making lobby assignments repeatable.
- Atomic writes use a same-directory temporary file followed by replace.

### Acceptance gate

- The same maximum tick is reached under different rendering frame rates.
- A natural win and tick limit produce distinct, tested JSON.
- Invalid map and invalid bot inputs fail before the match begins.
- The current graphical mode remains usable for visual inspection.

Gate result: passed for exact cutoff, paired repeat, JSON Schema, invalid bot,
and graphical usability. A dedicated natural-victory fixture will be added to
the scenario suite when long-running headless tests are affordable.

## Phase 1 — Headless execution spike

Goal: prove that the normal OpenRA simulation can advance without graphics or
audio while preserving gameplay behavior.

Status: complete on 2026-07-29 for correctness, device isolation, parity, and
the 5× performance gate. Evidence is recorded in
[Deterministic Headless Runtime Validation](experiments/2026-07-29-headless-runtime.md),
[Headless Dummy-Audio Performance Fix](experiments/2026-07-29-headless-performance-fix.md),
and [Decision 0004](decisions/0004-logic-only-headless-runtime.md).

### Candidate approaches

1. **Logic-only mode in the existing client runtime — preferred first.**
   Reuse map loading, local server, `OrderManager`, `World`, traits, and bot
   code, but bypass rendering/audio initialization and run logic ticks without
   wall-clock pacing.
2. **Dedicated server plus thin simulation client.**
   Useful for process isolation or multiplayer fidelity, but the thin client
   still must simulate `World`; the server alone is insufficient.
3. **Standalone world runner.**
   Potentially fastest, but highest risk because map loading, trait
   initialization, orders, and bot behavior may diverge from the real game.

Approach 1 is the initial recommendation because it minimizes gameplay
divergence. Approach 3 should be considered only if the engine seam required
for approach 1 becomes disproportionately invasive.

### Spike tasks

1. Trace dependencies of:
   - `Game.InitializeMod`;
   - local server and `OrderManager`;
   - map/world creation;
   - `World.LoadComplete` and `PostLoadComplete`;
   - `Game.InnerLogicTick`;
   - `World.TickRender`;
   - sound and UI tick paths.
2. Introduce a simulation runtime flag that is unavailable in normal play.
3. Split synchronized logic advancement from:
   - UI ticking;
   - cursor/input;
   - sound;
   - `TickRender`;
   - renderer frame preparation and presentation;
   - real-time sleeping.
4. Advance the world as fast as orders permit.
5. Preserve `OrderManager.TryTick`, synchronized RNG, bot ticks, frame-end
   actions, and game-over notifications.
6. Prove results against a graphical reference match using identical inputs:
   compare world ticks, end reason, player metrics, and synchronized hashes.

### Acceptance gate

- No SDL window, OpenGL context, or audio device appears.
- One match runs to a fixed tick and writes a valid result.
- Three identical runs produce identical synchronized hashes and metrics.
- A headless and graphical run with identical inputs agree on synchronized
  state.
- Simulation speed is measured; proceed if it exceeds 5× real time, optimize
  toward 20×.

Gate result:

- no SDL/OpenGL/audio: passed;
- fixed-tick valid result: passed;
- repeated deterministic result: passed at 1,500 ticks;
- graphical/headless parity: passed at 1,500 ticks, hash `0AC799D4`;
- minimum 5× speed: passed twice at 6.342× and 6.173× real time.

The initial 0.738× result failed because the dummy sound lifecycle repeatedly
decoded completed OGG music. A managed profile isolated that cost, and an
early dummy-engine media bypass closed the gate without changing synchronized
state. The implementation is retained without introducing a second gameplay
engine.

### Decision gate

The architecture decision selected:

- accept logic-only client mode for correctness and batch work;
- measure further scaling inside the batch soak;
- revisit a thin or standalone runner only if the accepted path cannot reach
  useful throughput.

## Phase 2 — Reliable batch orchestration

Goal: turn one headless match into a resumable experiment runner.

### Process model

Start with one OS process per match. OpenRA uses substantial global/static
runtime state; process isolation reduces contamination, memory-leak, and cleanup
risk. Add controlled parallelism only after the sequential soak test passes.

### Artifact layout

```text
runs/<run-id>/
  manifest.json
  summary.json
  runner.log
  matches/<match-id>/
    config.json
    result.json
    telemetry.jsonl
    events.jsonl
    stderr.log
    replay.orarep
```

### Work

1. Accept a declarative batch manifest rather than only environment variables.
2. Generate stable match IDs from schedule inputs.
3. Support:
   - sequential execution;
   - resume without rerunning completed matches;
   - limited retry for infrastructure failure only;
   - cancellation;
   - per-match wall-clock watchdog;
   - configurable worker count.
4. Never overwrite a completed raw run.
5. Aggregate only validated results.
6. Record machine/runtime metadata separately from synchronized inputs.
7. Preserve a replay for failures and a configurable sample of successful
   matches.

### Acceptance gate

- A 100-match sequential soak completes without user intervention.
- Interrupting and resuming the batch does not duplicate or lose matches.
- A deliberately invalid match is isolated and reported while the batch
  continues.
- Every result can be traced to one exact manifest entry.

## Phase 3 — Telemetry schema v1

Goal: observe strategy, not merely final score.

### Match metadata

- schema version;
- run/match ID;
- Git commit and dirty-tree flag;
- engine/mod versions;
- map UID, title, hash, size, and resource summary;
- ordered slots, spawns, teams, factions, bot profile/version;
- seed and all lobby options;
- world tick limit and watchdog limit;
- start/end tick, end reason, natural winner.

### Periodic player snapshots

At a fixed world-tick interval:

- cash, stored resources, income rate, spending rate;
- workers/miners and idle-worker count;
- production capacity and active queues;
- base count, building count, and asset value;
- power produced/used;
- army value and army population;
- unit counts by role and actor type;
- technology tier and completed upgrades;
- known enemy units/buildings;
- explored and currently visible map area;
- controlled-resource and controlled-territory estimates;
- damage dealt/received;
- kills/losses by value and role.

### Event stream

- actor produced, lost, captured, or transformed;
- building placed/completed/destroyed;
- technology/upgrades completed;
- expansion established or lost;
- squad created, launched, retreated, or dissolved;
- scouting discovery;
- attack target chosen;
- game-state transition and victory/defeat.

### Metric rules

- Define units, denominators, intervals, and exclusions.
- Separate stock metrics from flow metrics.
- Store raw counts; derive ratios during analysis.
- Include a synchronized state hash at selected checkpoints.
- Use JSONL for time series/events and compact JSON for final results.

### Acceptance gate

- JSON Schema validation passes for all artifacts.
- Metric definitions are documented in the wiki.
- Telemetry collection changes runtime by less than 10% at the default sample
  interval.
- A replay inspection confirms a sample of emitted events.

## Phase 4 — Scenario lifecycle and long-horizon stability

Goal: support strategically meaningful conflict outcomes and open-ended
observation of living factions.

### Work

1. Run conflict scenarios long enough for normal victory conditions.
2. Allow living-world scenarios to end at a declared observation horizon
   without inventing a winner.
3. Record faction collapse independently from scenario termination so other
   factions can continue living.
4. Add a high deterministic tick ceiling.
5. Detect likely stalemate using a conservative window:
   - no damage;
   - no kills;
   - no production/technology progress;
   - no meaningful resource or territory change.
6. Keep stalemate detection advisory first; compare with replays before making
   it terminating.
7. Record population, settlements, needs, relationships, surviving assets, and
   reason when a scenario reaches its horizon or cutoff.

### Acceptance gate

- Conflict scenarios produce reproducible natural wins or explicit cutoffs.
- Living-world scenarios complete their observation horizon without requiring
  a winner.
- Timeout and stalemate rates are separately reported.
- Every termination reason is reproducible from artifacts.
- No active combat match is incorrectly stopped in the reviewed sample.

## Phase 5 — Benchmark and balance methodology

Goal: establish a trustworthy baseline before deeper AI changes.

### Test suites

1. **Smoke:** one short match per supported map/profile configuration.
2. **Determinism:** repeated identical match inputs.
3. **Mirror:** same profile/faction on rotated spawns.
4. **Round robin:** every profile against every other profile in 1v1.
5. **Four-way FFA:** all profiles together with rotated slots.
6. **Map robustness:** open, choke-heavy, island, resource-rich, and
   resource-poor maps.
7. **Soak:** hundreds of mixed matches for crashes, leaks, and deadlocks.
8. **Peaceful growth:** war disabled, compare settlement resilience and
   prosperity.
9. **Scarcity/trade:** asymmetric resources, observe exchange, migration, and
   crisis handling.
10. **War cost:** compare otherwise paired societies with and without
    prolonged mobilization and infrastructure loss.

### Experimental design

- Pair baseline and candidate on identical map/seed/spawn schedules.
- Rotate positions so each profile occupies every slot equally.
- Separate tuning maps/seeds from evaluation maps/seeds.
- Change one primary behavior or parameter group per experiment.
- Predeclare the primary metric and guardrails.

### Statistical reporting

- natural win rate with confidence intervals;
- timeout/stalemate rate;
- median and distribution of victory time;
- paired differences in economy, tech, army, and combat metrics;
- population growth, mortality, migration, and faction continuity;
- food/energy security and shortage duration;
- settlement prosperity, stability, and recovery time;
- trade volume, dependency, and diplomatic-state duration;
- demographic and economic cost of mobilization and war;
- matchup and map sensitivity;
- bootstrap intervals where analytic assumptions are weak;
- raw sample count beside every aggregate.

### Initial balance guardrails

These are provisional and must be revised with evidence:

- crash/desync rate: 0%;
- infrastructure failure: below 1%;
- timeout/stalemate: below 10% after map validation;
- four-way overall win share: no profile above 40% or below 10%;
- 1v1 matchup: investigate persistent results worse than 35/65;
- no profile gains balance by creating substantially longer or stalled games.
- peaceful scenarios must permit stable population and settlement growth;
- scarcity must create observable adaptation rather than immediate scripted
  collapse;
- war-oriented policies must pay measurable demographic and economic costs.

### Acceptance gate

- At least 100 evaluation matches complete on held-out seeds.
- A generated report can explain performance by profile, opponent, map, spawn,
  and end reason.
- Conclusions include uncertainty and limitations.

## Phase 6 — Civilization AI v2

Goal: replace mostly static military production weights with observable
civilization-level decision-making.

### Shared perception/blackboard

- own economy, production, tech, army, and base state;
- remembered enemy composition and last-seen positions;
- threat map and defended-value map;
- expansion/resource opportunities;
- recent combat outcomes;
- confidence and age for incomplete intelligence;
- population, needs, jobs, and migration pressure;
- settlement prosperity and stability;
- trade dependency and diplomatic relationships;

### Strategic state machine

- opening;
- economic expansion;
- technology transition;
- pressure/rush;
- defense/emergency;
- counter-production;
- regroup/rebuild;
- finishing attack;
- famine/crisis response;
- peaceful development;
- trade/diplomatic initiative.

Transitions must emit reason codes and relevant inputs.

### Planners

1. Opening/build-order planner.
2. Worker and resource allocation.
3. Expansion timing and site selection.
4. Technology selection.
5. Army composition and counter-production.
6. Scouting targets and information refresh.
7. Defense allocation and reinforcement.
8. Attack timing, target scoring, path/rally selection.
9. Retreat, regroup, and re-engagement.
10. Population needs and labor allocation.
11. Trade and treaty selection.
12. Crisis recovery and demobilization.

### Profile policies

- **Aggressor:** values initiative, early army, exposed targets, and repeated
  pressure; accepts lower reserves.
- **Economist:** values income growth, expansion safety, production scaling, and
  efficient trades.
- **Technologist:** values information, tech timing, specialized counters, and
  preserving high-value units.
- **Fortress:** values defended assets, denial, layered response, attrition, and
  large counterattacks.

### Acceptance gate

- Telemetry shows distinct opening, expansion, technology, and attack timing.
- Decision logs explain major choices.
- Each profile pursues and achieves measurably different civil and military
  goals in reviewed simulations.
- Differences persist on held-out maps rather than only tuning maps.

## Phase 7 — Faction identity

Goal: separate an AI personality from the game faction it controls.

### Work

1. Make faction and spawn explicit experiment inputs.
2. Define four faction pillars, weaknesses, and counterplay.
3. Start with rules/tech-tree differences before expensive new art.
4. Add unique:
   - economy mechanics;
   - production constraints;
   - technology branches;
   - unit roles;
   - support powers;
   - strategic objectives.
5. Test every AI profile across factions before binding a canonical AI to each
   faction.
6. Build a matchup matrix and map-sensitivity report.

### Acceptance gate

- Faction and AI effects can be measured separately.
- Each faction has at least one exploitable weakness and viable counter.
- No faction dominates the held-out suite.
- Visual/content work follows validated gameplay needs.

## Phase 8 — Automated improvement and evolution

Goal: allow strategies to improve through experiments without losing
interpretability or overfitting.

### Parameter layer

Move tunable strategy values into versioned profiles:

- thresholds;
- utility weights;
- timing windows;
- squad sizes;
- risk tolerance;
- composition targets.

### Search methods

Begin with simple, inspectable methods:

1. paired parameter sweeps;
2. successive halving;
3. evolutionary search over bounded profiles;
4. population-based training only if simpler search plateaus.

### Fitness

Use multi-objective fitness:

- natural win rate;
- opponent/map robustness;
- victory time;
- resource/combat efficiency;
- population wellbeing and survival;
- settlement resilience and crisis recovery;
- knowledge, trade, and prosperity;
- cohesion and migration outcomes;
- demographic and economic cost of war;
- timeout and crash penalties;
- strategy-identity guardrails.

### Anti-overfitting

- tuning/evaluation split;
- unseen seeds and maps;
- hall-of-fame historical opponents;
- periodic regression against released profiles;
- retain lineage, config, artifacts, and rationale.

### Acceptance gate

- A candidate must outperform its parent on paired tuning matches and pass the
  held-out suite.
- Improvements remain reproducible from a committed profile and manifest.
- Automated search cannot directly modify production code or promote itself
  without review.

## Phase 9 — Persistent autonomous world

Goal: connect matches into a larger self-running simulation.

This phase starts only when the match laboratory is fast, stable, and measured.

### World model

- territories/nodes and connections;
- faction capitals, resources, armies, and technology;
- diplomacy, alliances, hostility, and treaties;
- strategic objectives and campaign plans;
- match scheduling from world conflicts;
- consequences returned from match results.

### State architecture

- event-sourced world history;
- deterministic epoch scheduler;
- periodic snapshots;
- explicit schema migration;
- replayable world evolution;
- separation between world RNG and match RNG.

### Faction memory/adaptation

- opponent tendencies;
- map/territory performance;
- prior diplomatic events;
- strategic doctrine changes;
- bounded adaptation with explainable reasons.

### Acceptance gate

- A world can run hundreds of epochs unattended.
- Every territorial or diplomatic change traces to an event.
- Loading a snapshot and replaying later events reproduces the same state.
- Individual matches remain independently reproducible.

## Phase 10 — Human observability

Goal: make the simulation understandable and enjoyable to watch.

### Views

- run dashboard;
- live match/spectator view;
- faction standings and history;
- economy/army/technology timelines;
- unit-composition charts;
- map heatmaps for movement, vision, control, and combat;
- decision timeline with AI reason codes;
- replay and artifact links;
- persistent-world map.

Build this after telemetry stabilizes so the interface reflects real,
versioned data rather than temporary fields.

## Phase 11 — Engineering and release discipline

### Continuous checks

- C# build and tests;
- OpenHV MiniYAML/map/localization lint;
- shell lint;
- JSON Schema validation;
- deterministic golden matches;
- short headless smoke batch;
- wiki link/frontmatter lint.

### Repository discipline

- keep engine changes narrow and documented;
- separate OpenHV content changes from OpenRA engine changes where practical;
- track upstream engine/mod commits;
- preserve license and attribution;
- avoid committing large generated run artifacts; store compact immutable raw
  evidence and manifests in project memory.

## Immediate backlog

Status marker: ✅ means implemented and validated on the feature branch.

| ID | Work item | Depends on | Completion evidence |
|---|---|---|---|
| SIM-001 ✅ | Versioned config/result contract | — | schema v1 and paired validation |
| SIM-002 ✅ | Tick-based end conditions | SIM-001 | exact tick/hash repeat evidence |
| SIM-003 ✅ | Logic-only engine spike | SIM-002 | no-window 1,500-tick match |
| SIM-004 ✅ | Disable renderer/audio initialization | SIM-003 | backend-negative log check |
| SIM-005 ✅ | Headless/reference equivalence | SIM-003 | hash `0AC799D4` and identical normalized results |
| SIM-006 | Isolated CLI process and exit codes | SIM-003 | failure-path integration tests |
| SIM-007 | Manifest-driven batch runner | SIM-006 | resumable 100-match soak |
| SIM-008 | Telemetry schema v1 | SIM-001 | validated JSON/JSONL artifacts |
| SIM-009 | Scenario lifecycle and long-horizon model | SIM-002, SIM-008 | reviewed conflict and living-world runs |
| SIM-010 | Baseline benchmark suite | SIM-007–009 | reproducible report |
| LIFE-001 | CivilizationState and SettlementCore traits | SIM-001–003 | synchronized state tests |
| LIFE-002 | Population, workforce, food, and housing | LIFE-001 | peaceful-growth scenario |
| LIFE-003 | Materials, energy, and building jobs | LIFE-001 | resource-flow telemetry |
| LIFE-004 | Research and small civil technology graph | LIFE-002–003 | deterministic unlock test |
| DIP-001 | Runtime diplomacy manager | LIFE-001 | neutral/war/peace transition tests |
| DIP-002 | Resource trade and route model | DIP-001, LIFE-003 | scarcity/trade scenario |
| AI-001 | Shared perception blackboard | SIM-008 | decision/telemetry traces |
| AI-002 | Strategic state machine | AI-001 | distinct measured transitions |
| AI-003 | Opening/economy/tech planners | AI-002 | held-out behavior report |
| AI-004 | Combat target/retreat planners | AI-002 | combat efficiency report |
| FAC-001 | Separate faction/profile inputs | SIM-001 | factorial test schedule |
| FAC-002 | Define faction gameplay pillars | FAC-001 | accepted design decision |
| EVO-001 | Versioned parameter profiles | SIM-010, AI-002 | reproducible candidate configs |
| WORLD-001 | Persistent-world design spike | SIM-010, FAC-002 | architecture decision |

## Delivery sequence and estimates

Estimates are engineering ranges, not promises; Phase 1 may change later
estimates because it tests the deepest engine coupling.

### Sprint 1 — 3 to 5 focused days

- SIM-001 result/config contract;
- SIM-002 world-tick cutoff;
- result/end-reason corrections;
- determinism test harness;
- headless dependency trace.

Exit: deterministic graphical simulation with correct outcome semantics.

Status: complete on 2026-07-29.

### Sprint 2 — 4 to 8 focused days

- SIM-003 logic-only spike;
- SIM-004 renderer/audio bypass;
- SIM-005 reference equivalence;
- performance profiling.

Exit: one verified no-window match, minimum 5× real-time.

Status: complete on 2026-07-29. Profiling reduced the 1,500-tick benchmark from
40.66 to 4.73–4.86 seconds, preserved hash `0AC799D4`, and passed the 5× exit.
The next active gate is Sprint 3 / SIM-006–007.

### Sprint 3 — 5 to 8 focused days

- SIM-006 isolated CLI;
- SIM-007 manifest runner, resume, watchdog;
- initial telemetry metadata/final state;
- 100-match soak.

Exit: repeatable unattended batch with complete artifacts.

### Sprint 4 — 8 to 12 focused days

- LIFE-001 civilization/settlement state;
- LIFE-002 population, food, housing, and workforce;
- LIFE-003 materials, energy, and civil jobs;
- civil telemetry and peaceful-growth scenarios.

Exit: four factions can live, grow, and experience shortages without mandatory
war.

### Sprint 5 — 8 to 12 focused days

- DIP-001 dynamic neutral/war/peace relationships;
- DIP-002 first resource trade;
- LIFE-004 first civil research graph;
- scarcity, trade, shock-recovery, and war-cost experiments;
- SIM-009 natural completion/stalemate;
- SIM-010 first statistically useful civil/military baseline.

Exit: trustworthy evidence about faction life, diplomacy, and warfare costs.

### Following 4 to 8 weeks

- Civilization AI v2 in small, separately measured increments.
- Faction/profile separation and first gameplay-identity prototypes.
- Dashboard only after telemetry fields stabilize.

### Later

- automated parameter search;
- hall-of-fame evaluation;
- persistent world and diplomacy;
- richer assets and presentation.

## Definition of the next release

The next meaningful milestone, provisionally `simulation-v0.2`, is complete
when:

1. matches run without window, renderer, or audio;
2. cutoff is based on synchronized world ticks;
3. natural victory, faction collapse, observation horizon, timeout, stalemate,
   invalid input, desync, and crash are distinguishable;
4. config and result schemas are versioned;
5. repeated identical matches are deterministic;
6. a resumable 100-match batch completes unattended;
7. artifacts include metadata, final metrics, and basic time series;
8. the batch produces a validated aggregate report;
9. the full OpenHV test/lint suite remains green;
10. architecture, experiment, decisions, and raw evidence are integrated into
    the project wiki.

The following `living-factions-v0.1` milestone then requires:

1. settlement population and workforce;
2. food, housing, materials, energy, knowledge, and credits;
3. needs-driven growth, shortage, and migration pressure;
4. useful civil building choices;
5. a small original technology graph;
6. default-neutral factions and explicit war transitions;
7. civil telemetry and at least four non-war/crisis scenarios;
8. visible demographic and economic consequences from military mobilization.
