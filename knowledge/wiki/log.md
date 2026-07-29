# Project Log

This file is append-only. Entries summarize durable project events and link to
the pages where current knowledge is maintained.

## [2026-07-29] change | Autonomous simulation launcher

Added a hands-off OpenHV match mode in which the local client spectates and AI
players occupy the combat slots. Recorded in
[Architecture](architecture.md) and
[Decision 0001](decisions/0001-openhv-foundation.md).

## [2026-07-29] change | Four AI profiles and tournament runner

Added aggressor, economist, technologist, and fortress profiles; configurable
seed and duration; JSON results; and multi-map tournament aggregation. See
[AI profiles](ai-profiles.md) and [Architecture](architecture.md).

## [2026-07-29] experiment | Baseline four-profile tournament

Completed 10/10 timed matches across four maps. Technologist and Aggressor each
led three cutoffs; the short run validated infrastructure but not combat
balance. See the
[baseline experiment](experiments/2026-07-29-baseline-tournament.md).

## [2026-07-29] ingest | Karpathy LLM Wiki pattern

Adopted a persistent, Git-backed project memory with immutable sources,
agent-maintained synthesis, an index, an append-only log, and explicit
maintenance workflows. See
[Decision 0002](decisions/0002-persistent-project-wiki.md) and the
[source record](../raw/karpathy-llm-wiki-2026-07-29.md).

## [2026-07-29] query | Detailed execution plan

Expanded the compact roadmap into an evidence-gated plan from deterministic
headless execution through telemetry, strategic AI, faction identity,
automated improvement, and a persistent world. Local engine inspection showed
that the dedicated server does not advance `World` and that the normal client
loop couples logic and rendering, making a logic-only client runtime the first
recommended spike. See the [detailed execution plan](execution-plan.md).

## [2026-07-29] ingest | OpenCiv civilization concepts

Inspected `RyanGrieb/OpenCiv` at commit
`113eb908ffda2f50ff5c0d5a4a0bbe697904293a`. Its city population, worked
territory, differentiated yields, resources, building data, and civilization
configuration are useful conceptual references, but the TypeScript turn-based
implementation is incomplete and is not merged into OpenRA. See the
[source record](../raw/openciv-2026-07-29.md).

## [2026-07-29] decision | Living factions before war

Reframed the project from an autonomous war tournament into a real-time
simulation of living societies. Added population, needs, settlements, civil
economy, research, trade, migration, and dynamic diplomacy as core product
systems. Warfare remains possible but costly and non-mandatory. See
[Decision 0003](decisions/0003-living-factions-before-war.md) and
[Living Factions Design](faction-life.md).

## [2026-07-29] change | Commit and push workflow

The user established a standing delivery rule: meaningful completed work must
update the project wiki, pass relevant validation, be committed as a focused
change, and be pushed to the user-owned GitHub feature branch. Pull requests
remain a separate explicit action. The operational rule is recorded in
[AGENTS.md](../../AGENTS.md).

## [2026-07-29] change | Simulation contract v1 and deterministic cutoff

Completed Sprint 1 / SIM-001–002. Added strongly typed versioned
configuration/results, stable end reasons, JSON Schema, atomic output,
pre-match input validation, build/map/slot metadata, synchronized state hash,
and separate natural-winner/score-leader semantics. Replaced the primary
wall-clock duration with an exact `WorldTick` horizon; wall clock is now only a
deadlock watchdog. Deterministic lobby colors remove the remaining cosmetic
configuration drift between paired runs. The tournament aggregate now reports
end reasons, natural wins, and score leads separately. See
[Architecture](architecture.md) and
[Simulation Contract v1 Validation](experiments/2026-07-29-simulation-contract-v1.md).

## [2026-07-29] experiment | Contract v1 repeat and failure paths

Two graphical matches with map Cold Rage, seed 424242, and a 50-tick horizon
both stopped at tick 50 with sync hash `4F20B62A`. Normalized artifacts matched,
both passed JSON Schema v1, and an unknown bot failed before the match with a
non-zero process result. Compact evidence is preserved in
[the experiment record](experiments/2026-07-29-simulation-contract-v1.md).

## [2026-07-29] change | Deterministic headless client runtime

Completed SIM-003–005. Added an idempotent, version-pinned OpenRA SDK patch,
no-op graphics/sound platform, unpaced headless loop, deterministic local
server RNG, execution-mode metadata, headless-default tournament runs, and
graphical/headless parity automation. A lobby-start polling fallback also
removed an event-order race. See [Architecture](architecture.md) and
[Decision 0004](decisions/0004-logic-only-headless-runtime.md).

## [2026-07-29] correction | Separate bot randomness from renderer cosmetics

Longer parity testing disproved the earlier assumption that seeding the lobby
alone made bot behavior repeatable. OpenRA bot modules used the same local RNG
as renderer effects; graphical and headless runs consumed different sequences
before later strategic choices. Added a seed-derived `World.BotRandom` and
routed stock/OpenHV bot modules through it. This correction is documented in
the
[headless runtime experiment](experiments/2026-07-29-headless-runtime.md).

## [2026-07-29] experiment | Headless parity and performance

Graphical, headless, and repeated headless matches on Cold Rage with seed
424242 all stopped at tick 1,500 with synchronized hash `0AC799D4`; normalized
artifacts matched and passed Schema v1. Headless initialized no SDL, OpenGL, or
audio backend. The 30 simulated seconds took 40.66 wall seconds (0.738×), so
the 5× performance gate remains open. See
[Deterministic Headless Runtime Validation](experiments/2026-07-29-headless-runtime.md).

## [2026-07-29] experiment | Dummy-audio performance profile

A 15-second managed CPU/GC trace attributed 87.49% of the sampled main-thread
interval to OGG construction beneath `Sound.LoadSound`. The dummy sound
reported tracks complete immediately, causing the normal playlist lifecycle to
decode another file repeatedly. This replaced the earlier hypothesis that
synchronized gameplay was already the dominant cost. See
[Headless Dummy-Audio Performance Fix](experiments/2026-07-29-headless-performance-fix.md).

## [2026-07-29] change | Close the Sprint 2 throughput gate

Added a dummy-engine capability guard before lazy effect/music loading.
Repeated 1,500-tick runs fell from 40.66 seconds to 4.73 and 4.86 seconds
(6.342× and 6.173× real time) while preserving hash `0AC799D4`, graphical
parity, Schema v1 validity, and the full test suite. Sprint 2 is complete; the
next active work is the isolated manifest batch runner.
